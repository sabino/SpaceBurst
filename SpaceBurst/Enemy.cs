using Microsoft.Xna.Framework;
using SpaceBurst.RuntimeData;
using System;

namespace SpaceBurst
{
    class Enemy : Entity
    {
        protected readonly EnemyArchetypeDefinition archetype;
        protected readonly DamageMaskDefinition damageMask;
        protected readonly float targetY;
        protected readonly float speedMultiplier;
        protected readonly float movementAmplitude;
        protected readonly float movementFrequency;
        protected readonly MovePattern movePattern;
        protected readonly FirePattern firePattern;

        protected float ageSeconds;
        protected float flashTimer;
        protected float fireCooldown;
        protected float phaseOffset;

        public static Random Random { get; } = new Random();

        public override bool IsDamageable
        {
            get { return true; }
        }

        public override int ContactDamage
        {
            get { return damageMask.ContactDamage; }
        }

        public virtual bool IsBoss
        {
            get { return false; }
        }

        public virtual int PointValue
        {
            get { return archetype.ScoreValue; }
        }

        public string ArchetypeId
        {
            get { return archetype.Id; }
        }

        public float IntegrityRatio
        {
            get
            {
                if (sprite == null || sprite.Mask.InitialOccupiedCount <= 0)
                    return 0f;

                return sprite.Mask.OccupiedCount / (float)sprite.Mask.InitialOccupiedCount;
            }
        }

        public Enemy(
            EnemyArchetypeDefinition archetype,
            Vector2 spawnPoint,
            float targetY,
            MovePattern movePattern,
            FirePattern firePattern,
            float speedMultiplier,
            float amplitude,
            float frequency)
        {
            this.archetype = archetype;
            this.targetY = targetY;
            this.movePattern = movePattern;
            this.firePattern = firePattern;
            this.speedMultiplier = speedMultiplier;
            movementAmplitude = amplitude;
            movementFrequency = frequency;
            damageMask = archetype.DamageMask;
            Position = spawnPoint;
            RenderScale = archetype.RenderScale;
            sprite = new ProceduralSpriteInstance(Game1.Instance.GraphicsDevice, archetype.Sprite);
            phaseOffset = spawnPoint.Y * 0.013f;
            fireCooldown = archetype.FireIntervalSeconds * (0.6f + Random.NextFloat(0f, 0.6f));
        }

        public override void Update()
        {
            float deltaSeconds = (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            ageSeconds += deltaSeconds;

            if (flashTimer > 0f)
            {
                flashTimer -= deltaSeconds;
                color = flashTimer > 0f ? Color.White : Color.White;
            }

            UpdateMovement(deltaSeconds);
            TryFire(deltaSeconds);

            Position += Velocity * deltaSeconds;

            if (Position.X < -Size.X || Position.Y < -Size.Y || Position.Y > Game1.ScreenSize.Y + Size.Y || Position.X > Game1.ScreenSize.X + 400f)
                IsExpired = true;
        }

        public virtual void ApplyBulletHit(Bullet bullet, Vector2 impactPoint)
        {
            DamageResult result = sprite.ApplyDamage(Position, impactPoint, RenderScale, damageMask, bullet.ImpactProfile, Math.Max(1, bullet.Damage));
            flashTimer = 0.08f;
            color = result.CellsRemoved > 0 ? Color.Lerp(Color.White, Color.OrangeRed, 0.2f) : Color.White;
            if (result.CellsRemoved > 0)
                EntityManager.SpawnImpactParticles(impactPoint, ColorUtil.ParseHex(archetype.Sprite.AccentColor, Color.Orange), bullet.ImpactProfile.DebrisBurstCount, bullet.ImpactProfile.DebrisSpeed, -bullet.Velocity * 0.08f);
            if (result.Destroyed)
                Destroy();
        }

        public virtual void ApplyContactHit(Vector2 impactPoint, int contactDamage)
        {
            DamageResult result = sprite.ApplyDamage(Position, impactPoint, RenderScale, damageMask, damageMask.ContactImpact, Math.Max(1, contactDamage));
            if (result.CellsRemoved > 0)
                EntityManager.SpawnImpactParticles(impactPoint, ColorUtil.ParseHex(archetype.Sprite.AccentColor, Color.OrangeRed), damageMask.ContactImpact.DebrisBurstCount, damageMask.ContactImpact.DebrisSpeed, Vector2.Zero);
            if (result.Destroyed)
                Destroy();
        }

        public void ApplyKnockback(Vector2 direction, float impulse)
        {
            if (direction == Vector2.Zero)
                return;

            Velocity += Vector2.Normalize(direction) * impulse;
        }

        protected virtual void Destroy()
        {
            if (IsExpired)
                return;

            IsExpired = true;
            EntityManager.SpawnImpactParticles(Position, ColorUtil.ParseHex(archetype.Sprite.AccentColor, Color.OrangeRed), 18 + (IsBoss ? 18 : 0), 180f, new Vector2(-80f, 0f));
            Sound.Explosion.Play(0.45f, Random.NextFloat(-0.2f, 0.2f), 0);
            PlayerStatus.AddPoints(PointValue);
            PlayerStatus.IncreaseMultiplier();

            float bonusChance = Game1.Instance != null ? Game1.Instance.CurrentPowerDropBonusChance : 0f;
            if (archetype.PowerupEligible && PlayerStatus.RunProgress.Powerups.ShouldDrop(Random, bonusChance, archetype.PowerupWeight, IsBoss))
                EntityManager.Add(new PowerupPickup(Position));
        }

        protected virtual void UpdateMovement(float deltaSeconds)
        {
            float scrollSpeed = Game1.Instance.CurrentScrollSpeed;
            float baseSpeed = archetype.MoveSpeed * Math.Max(0.35f, speedMultiplier);
            float phase = ageSeconds * Math.Max(0.2f, movementFrequency) + phaseOffset;
            float desiredY = targetY;
            float desiredXVelocity = -(baseSpeed + scrollSpeed);

            switch (movePattern)
            {
                case MovePattern.SineWave:
                    desiredY = targetY + (float)Math.Sin(phase * MathF.Tau) * movementAmplitude;
                    break;

                case MovePattern.Dive:
                    desiredXVelocity *= 1.08f;
                    desiredY = Position.X > Game1.ScreenSize.X * 0.62f
                        ? targetY + (float)Math.Sin(phase * MathF.PI) * movementAmplitude * 0.35f
                        : MathHelper.Lerp(targetY, Player1.Instance.Position.Y, 0.7f);
                    break;

                case MovePattern.RetreatBackfire:
                    desiredXVelocity = ageSeconds < 1.6f
                        ? -(baseSpeed * 0.75f + scrollSpeed)
                        : baseSpeed * 0.25f - scrollSpeed * 0.35f;
                    desiredY = targetY + (float)Math.Sin(phase * MathF.PI) * movementAmplitude * 0.3f;
                    break;

                case MovePattern.TurretCarrier:
                    desiredXVelocity = -(baseSpeed * 0.55f + scrollSpeed);
                    desiredY = targetY + (float)Math.Sin(phase * MathF.PI) * movementAmplitude * 0.2f;
                    break;
            }

            float yVelocity = (desiredY - Position.Y) * 3.5f;
            Velocity = Vector2.Lerp(Velocity, new Vector2(desiredXVelocity, yVelocity), Math.Min(1f, 5.5f * deltaSeconds));
        }

        protected virtual void TryFire(float deltaSeconds)
        {
            if (firePattern == FirePattern.None || archetype.FireIntervalSeconds <= 0f || Position.X > Game1.ScreenSize.X + 40f || Position.X < Game1.ScreenSize.X * 0.1f)
                return;

            fireCooldown -= deltaSeconds;
            if (fireCooldown > 0f)
                return;

            fireCooldown = archetype.FireIntervalSeconds;

            switch (firePattern)
            {
                case FirePattern.ForwardPulse:
                    SpawnBullet(new Vector2(-1f, 0f));
                    break;

                case FirePattern.SpreadPulse:
                    SpawnBullet(new Vector2(-1f, -0.18f));
                    SpawnBullet(new Vector2(-1f, 0f));
                    SpawnBullet(new Vector2(-1f, 0.18f));
                    break;

                case FirePattern.AimedShot:
                    Vector2 aim = Player1.Instance.Position - Position;
                    if (aim == Vector2.Zero)
                        aim = -Vector2.UnitX;
                    else
                        aim.Normalize();

                    SpawnBullet(aim);
                    break;
            }
        }

        protected void SpawnBullet(Vector2 direction)
        {
            if (direction == Vector2.Zero)
                direction = -Vector2.UnitX;
            else
                direction.Normalize();

            Vector2 spawnPoint = Position + direction * (ApproximateRadius * 0.75f);
            EntityManager.Add(new Bullet(
                spawnPoint,
                direction * 420f,
                false,
                Math.Max(1, damageMask.ProjectileDamage),
                damageMask.ContactImpact,
                Element.EnemyBulletDefinition,
                0,
                3.2f,
                0f,
                1f));
            Sound.Shot.Play(0.08f, Random.NextFloat(-0.25f, 0.25f), -0.2f);
        }
    }
}
