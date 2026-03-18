using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceBurst.RuntimeData;
using System;

namespace SpaceBurst
{
    class Enemy : Entity
    {
        protected readonly EnemyArchetypeDefinition archetype;
        protected readonly DamageMaskDefinition damageMask;
        protected readonly Color accentColor;
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
        private static readonly DeterministicRngState fallbackGameplayRandom = new DeterministicRngState(0x5EED1234u);
        private static readonly Random cosmeticRandom = new Random();

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

        public bool ShowDurabilityBar
        {
            get { return archetype.ShowDurabilityBar; }
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
            accentColor = ColorUtil.ParseHex(archetype.Sprite.AccentColor, Color.Orange);
            phaseOffset = spawnPoint.Y * 0.013f;
            DeterministicRngState gameplayRandom = Game1.Instance?.GameplayRandom ?? fallbackGameplayRandom;
            fireCooldown = archetype.FireIntervalSeconds * (0.6f + gameplayRandom.NextFloat(0f, 0.6f));
        }

        public override void Update()
        {
            float deltaSeconds = (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            ageSeconds += deltaSeconds;

            if (flashTimer > 0f)
            {
                flashTimer -= deltaSeconds;
                if (flashTimer <= 0f)
                    color = Color.White;
            }

            UpdateMovement(deltaSeconds);
            TryFire(deltaSeconds);

            Position += Velocity * deltaSeconds;

            if (Position.X < -Size.X || Position.Y < -Size.Y || Position.Y > Game1.ScreenSize.Y + Size.Y || Position.X > Game1.ScreenSize.X + 400f)
                IsExpired = true;
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);

            if (!ShowDurabilityBar || IsExpired || sprite == null || Game1.UiPixel == null)
                return;

            Rectangle bounds = Bounds;
            Rectangle viewport = new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight);
            if (!viewport.Intersects(bounds))
                return;

            int width = Math.Max(18, bounds.Width - 12);
            int height = 5;
            int x = bounds.Center.X - width / 2;
            int y = Math.Max(4, bounds.Y - 10);
            Rectangle barBounds = new Rectangle(x, y, width, height);

            spriteBatch.Draw(Game1.UiPixel, barBounds, Color.Black * 0.55f);
            spriteBatch.Draw(Game1.UiPixel, new Rectangle(barBounds.X, barBounds.Y, barBounds.Width, 1), Color.White * 0.16f);
            spriteBatch.Draw(Game1.UiPixel, new Rectangle(barBounds.X, barBounds.Bottom - 1, barBounds.Width, 1), Color.White * 0.16f);
            spriteBatch.Draw(Game1.UiPixel, new Rectangle(barBounds.X, barBounds.Y, 1, barBounds.Height), Color.White * 0.16f);
            spriteBatch.Draw(Game1.UiPixel, new Rectangle(barBounds.Right - 1, barBounds.Y, 1, barBounds.Height), Color.White * 0.16f);

            int fillWidth = (int)MathF.Round((barBounds.Width - 2) * MathHelper.Clamp(IntegrityRatio, 0f, 1f));
            if (fillWidth > 0)
                spriteBatch.Draw(Game1.UiPixel, new Rectangle(barBounds.X + 1, barBounds.Y + 1, fillWidth, Math.Max(1, barBounds.Height - 2)), accentColor);
        }

        public virtual void ApplyBulletHit(Bullet bullet, Vector2 impactPoint)
        {
            ApplyImpact(impactPoint, Math.Max(1, bullet.Damage), bullet.ImpactProfile, bullet.Velocity, bullet.ImpactFxStyle, bullet.ExplosionRadius > 0f);
        }

        public void ApplyDirectHit(Vector2 impactPoint, int damage, ImpactProfileDefinition impactProfile, Vector2 sourceVelocity, ImpactFxStyle impactFxStyle)
        {
            ApplyImpact(impactPoint, Math.Max(1, damage), impactProfile, sourceVelocity, impactFxStyle, false);
        }

        public virtual void ApplyBeamHit(Vector2 impactPoint, int damage, ImpactProfileDefinition impactProfile)
        {
            ApplyImpact(impactPoint, Math.Max(1, damage), impactProfile, Vector2.UnitX * 900f, ImpactFxStyle.Beam, false);
        }

        public virtual void ApplyContactHit(Vector2 impactPoint, int contactDamage)
        {
            ApplyImpact(impactPoint, Math.Max(1, contactDamage), damageMask.ContactImpact, Vector2.Zero, ImpactFxStyle.Standard, false);
        }

        public void ApplyKnockback(Vector2 direction, float impulse)
        {
            if (direction == Vector2.Zero)
                return;

            Velocity += Vector2.Normalize(direction) * impulse;
        }

        public virtual EnemySnapshotData CaptureSnapshot()
        {
            return new EnemySnapshotData
            {
                ArchetypeId = archetype.Id,
                Position = new Vector2Data(Position.X, Position.Y),
                Velocity = new Vector2Data(Velocity.X, Velocity.Y),
                TargetY = targetY,
                MovePattern = movePattern,
                FirePattern = firePattern,
                SpeedMultiplier = speedMultiplier,
                MovementAmplitude = movementAmplitude,
                MovementFrequency = movementFrequency,
                AgeSeconds = ageSeconds,
                FlashTimer = flashTimer,
                FireCooldown = fireCooldown,
                PhaseOffset = phaseOffset,
                IsBoss = false,
                Mask = sprite?.CaptureMaskSnapshot() ?? new MaskSnapshotData(),
            };
        }

        public void RestoreSnapshot(EnemySnapshotData snapshot)
        {
            if (snapshot == null)
                return;

            Position = new Vector2(snapshot.Position.X, snapshot.Position.Y);
            Velocity = new Vector2(snapshot.Velocity.X, snapshot.Velocity.Y);
            ageSeconds = snapshot.AgeSeconds;
            flashTimer = snapshot.FlashTimer;
            fireCooldown = snapshot.FireCooldown;
            phaseOffset = snapshot.PhaseOffset;
            sprite?.RestoreMaskSnapshot(snapshot.Mask);
            color = flashTimer > 0f ? Color.Lerp(Color.White, accentColor, 0.28f) : Color.White;
        }

        public static Enemy FromSnapshot(EnemyArchetypeDefinition archetype, EnemySnapshotData snapshot, BossDefinition bossDefinition = null)
        {
            if (snapshot == null || archetype == null)
                return null;

            Enemy enemy = snapshot.IsBoss
                ? new BossEnemy(archetype, bossDefinition, new Vector2(snapshot.Position.X, snapshot.Position.Y))
                : new Enemy(
                    archetype,
                    new Vector2(snapshot.Position.X, snapshot.Position.Y),
                    snapshot.TargetY,
                    snapshot.MovePattern,
                    snapshot.FirePattern,
                    snapshot.SpeedMultiplier,
                    snapshot.MovementAmplitude,
                    snapshot.MovementFrequency);

            enemy.RestoreSnapshot(snapshot);
            if (enemy is BossEnemy boss)
                boss.RestoreBossSnapshot(snapshot);
            return enemy;
        }

        protected virtual void Destroy(bool coreBreach = false)
        {
            if (IsExpired)
                return;

            IsExpired = true;
            int debrisCount = 18 + (IsBoss ? 18 : 0) + (coreBreach && !IsBoss ? 14 : 0);
            float debrisSpeed = 180f + (coreBreach && !IsBoss ? 40f : 0f);
            EntityManager.SpawnImpactParticles(Position, accentColor, debrisCount, debrisSpeed, new Vector2(-80f, 0f));
            EntityManager.SpawnFlash(Position, accentColor * 0.24f, 28f, coreBreach ? 92f : 64f, coreBreach ? 0.22f : 0.16f);
            if (coreBreach)
                EntityManager.SpawnShockwave(Position, accentColor * 0.22f, 12f, IsBoss ? 160f : 88f, 0.28f);
            Sound.Explosion.Play(0.45f, cosmeticRandom.NextFloat(-0.2f, 0.2f), 0);
            PlayerStatus.AddPoints(PointValue);
            PlayerStatus.IncreaseMultiplier();

            float bonusChance = Game1.Instance != null ? Game1.Instance.CurrentPowerDropBonusChance : 0f;
            if (archetype.PowerupEligible && PlayerStatus.RunProgress.Powerups.ShouldDrop(Game1.Instance?.GameplayRandom, bonusChance, archetype.PowerupWeight, IsBoss))
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
            Sound.Shot.Play(0.08f, cosmeticRandom.NextFloat(-0.25f, 0.25f), -0.2f);
        }

        private void ApplyImpact(Vector2 impactPoint, int damage, ImpactProfileDefinition impactProfile, Vector2 sourceVelocity, ImpactFxStyle impactFxStyle, bool causeShockwave)
        {
            DamageResult result = sprite.ApplyDamage(Position, impactPoint, RenderScale, damageMask, impactProfile, damage);
            flashTimer = result.CoreCellsRemoved > 0 ? 0.14f : 0.08f;
            color = result.CoreCellsRemoved > 0 ? Color.Lerp(Color.White, Color.OrangeRed, 0.55f) : (result.CellsRemoved > 0 ? Color.Lerp(Color.White, accentColor, 0.28f) : Color.White);

            if (result.CellsRemoved > 0)
            {
                int debris = impactProfile.DebrisBurstCount + (result.CoreCellsRemoved > 0 ? 6 : 0);
                float speed = impactProfile.DebrisSpeed * (result.CoreCellsRemoved > 0 ? 1.15f : 1f);
                EntityManager.SpawnImpactParticles(impactPoint, accentColor, debris, speed, -sourceVelocity * 0.08f);

                if (impactFxStyle == ImpactFxStyle.Plasma || impactFxStyle == ImpactFxStyle.Missile || impactFxStyle == ImpactFxStyle.Fortress)
                    EntityManager.SpawnShockwave(impactPoint, accentColor * 0.2f, 6f, 34f + damage * 6f, 0.16f);
                else
                    EntityManager.SpawnFlash(impactPoint, accentColor * 0.18f, 8f, 24f + damage * 4f, 0.1f);
            }

            if (DamageMaskMath.ShouldDestroyOnImpact(result, archetype.DestroyOnCoreBreach))
                Destroy(result.CoreCellsRemoved > 0 && archetype.DestroyOnCoreBreach);
            else if (causeShockwave && result.CoreCellsRemoved > 0)
                EntityManager.SpawnShockwave(impactPoint, accentColor * 0.24f, 8f, 54f, 0.18f);
        }
    }
}
