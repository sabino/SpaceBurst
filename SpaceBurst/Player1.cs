using Microsoft.Xna.Framework;
using SpaceBurst.RuntimeData;

namespace SpaceBurst
{
    class Player1 : Entity
    {
        private const float MoveSpeed = 320f;
        private const float BackDriftTargetFactor = 0.22f;
        private const float RespawnSeconds = 1.25f;
        private const float InvulnerabilitySeconds = 1.1f;
        private const float ContactInvulnerabilitySeconds = 0.45f;
        private const float FireIntervalSeconds = 0.1f;

        private static Player1 instance;
        public static Player1 Instance
        {
            get
            {
                if (instance == null)
                    instance = new Player1();

                return instance;
            }
        }

        private ProceduralSpriteInstance cannonSprite;
        private Vector2 cannonDirection = Vector2.UnitX;
        private Vector2 knockbackVelocity;
        private float respawnTimer;
        private float invulnerabilityTimer;
        private float fireCooldown;
        private bool hullDestroyedQueued;

        public bool IsDead
        {
            get { return respawnTimer > 0f || hullDestroyedQueued; }
        }

        public bool IsInvulnerable
        {
            get { return IsDead || invulnerabilityTimer > 0f; }
        }

        public override bool IsFriendly
        {
            get { return true; }
        }

        public override bool IsDamageable
        {
            get { return true; }
        }

        private Player1()
        {
            ResetSprites();
            ResetForStage();
        }

        public override void Update()
        {
            float deltaSeconds = (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;

            if (respawnTimer > 0f)
            {
                respawnTimer -= deltaSeconds;
                if (respawnTimer <= 0f)
                    RestoreToWindow();
                return;
            }

            if (hullDestroyedQueued)
                return;

            if (invulnerabilityTimer > 0f)
                invulnerabilityTimer -= deltaSeconds;

            if (fireCooldown > 0f)
                fireCooldown -= deltaSeconds;

            Vector2 moveDirection = Input.GetMovementDirection();
            Vector2 aimDirection = Input.GetAimDirection();
            if (aimDirection == Vector2.Zero)
                aimDirection = Vector2.UnitX;
            else
                aimDirection.Normalize();

            cannonDirection = aimDirection;
            Orientation = 0f;

            Vector2 movementVelocity = new Vector2(moveDirection.X * MoveSpeed, moveDirection.Y * MoveSpeed);
            if (moveDirection.X >= 0f)
            {
                float targetX = Game1.ScreenSize.X * BackDriftTargetFactor;
                movementVelocity.X += (targetX - Position.X) * 2.5f;
            }

            knockbackVelocity = Vector2.Lerp(knockbackVelocity, Vector2.Zero, MathHelper.Clamp(7f * deltaSeconds, 0f, 1f));
            Velocity = movementVelocity + knockbackVelocity;
            Position += Velocity * deltaSeconds;

            ClampToPlayerWindow();

            if (Input.IsFireHeld() && fireCooldown <= 0f)
            {
                fireCooldown = FireIntervalSeconds;
                Fire(aimDirection);
            }
        }

        public override void Draw(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch)
        {
            if (IsDead)
                return;

            bool flicker = invulnerabilityTimer > 0f && ((int)(Game1.GameTime.TotalGameTime.TotalSeconds * 18f) % 2 == 0);
            if (!flicker)
                sprite.Draw(spriteBatch, Position, color, 0f, RenderScale);

            if (!flicker)
            {
                float cannonAngle = cannonDirection.ToAngle();
                cannonSprite.Draw(spriteBatch, Position + new Vector2(12f, 0f), Color.White, cannonAngle, 1f);
            }
        }

        public void ResetForStage()
        {
            ResetSprites();
            Position = GetSpawnPosition();
            Velocity = Vector2.Zero;
            knockbackVelocity = Vector2.Zero;
            fireCooldown = 0f;
            respawnTimer = 0f;
            invulnerabilityTimer = InvulnerabilitySeconds;
            hullDestroyedQueued = false;
            cannonDirection = Vector2.UnitX;
            color = Color.White;
        }

        public void StartRespawn(float delaySeconds)
        {
            respawnTimer = delaySeconds <= 0f ? RespawnSeconds : delaySeconds;
            Velocity = Vector2.Zero;
            knockbackVelocity = Vector2.Zero;
            fireCooldown = 0f;
        }

        public void MakeInvulnerable(float durationSeconds)
        {
            invulnerabilityTimer = durationSeconds;
        }

        public bool ApplyDamage(Vector2 impactPoint, int damage)
        {
            if (IsInvulnerable)
                return false;

            DamageResult result = sprite.ApplyDamage(Position, impactPoint, RenderScale, Element.PlayerDamageMask, System.Math.Max(1, damage));
            if (result.CellsRemoved > 0)
                invulnerabilityTimer = ContactInvulnerabilitySeconds;

            if (result.Destroyed)
            {
                hullDestroyedQueued = true;
                return true;
            }

            return false;
        }

        public void ApplyKnockback(Vector2 direction, float impulse)
        {
            if (direction == Vector2.Zero)
                return;

            direction.Normalize();
            knockbackVelocity += direction * impulse;
        }

        public bool ConsumeHullDestroyed()
        {
            if (!hullDestroyedQueued)
                return false;

            hullDestroyedQueued = false;
            return true;
        }

        public void RestoreToWindow()
        {
            ResetSprites();
            Position = GetSpawnPosition();
            Velocity = Vector2.Zero;
            knockbackVelocity = Vector2.Zero;
            respawnTimer = 0f;
            invulnerabilityTimer = InvulnerabilitySeconds;
            cannonDirection = Vector2.UnitX;
        }

        private void Fire(Vector2 aimDirection)
        {
            Vector2 muzzleOffset = aimDirection * 26f;
            Vector2 spawnPoint = Position + muzzleOffset;
            EntityManager.Add(new Bullet(spawnPoint, aimDirection * 760f, true, Element.PlayerDamageMask.ProjectileDamage));
            Sound.Shot.Play(0.16f, 0f, 0f);
        }

        private void ClampToPlayerWindow()
        {
            Vector2 size = Size * 0.5f;
            float left = Game1.ScreenSize.X * 0.1f + size.X;
            float right = Game1.ScreenSize.X * 0.4f - size.X;
            float top = Game1.ScreenSize.Y * 0.08f + size.Y;
            float bottom = Game1.ScreenSize.Y * 0.92f - size.Y;

            Position = new Vector2(
                MathHelper.Clamp(Position.X, left, right),
                MathHelper.Clamp(Position.Y, top, bottom));
        }

        private static Vector2 GetSpawnPosition()
        {
            return new Vector2(Game1.ScreenSize.X * BackDriftTargetFactor, Game1.ScreenSize.Y * 0.5f);
        }

        private void ResetSprites()
        {
            sprite = new ProceduralSpriteInstance(Game1.Instance.GraphicsDevice, Element.PlayerHullDefinition);
            cannonSprite = new ProceduralSpriteInstance(Game1.Instance.GraphicsDevice, Element.PlayerCannonDefinition);
            RenderScale = 1f;
        }
    }
}
