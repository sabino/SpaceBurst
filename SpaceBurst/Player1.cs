using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceBurst.RuntimeData;
using System;

namespace SpaceBurst
{
    class Player1 : Entity
    {
        private const float MoveSpeed = 360f;
        private const float RespawnSeconds = 1.1f;
        private const float InvulnerabilitySeconds = 1.1f;
        private const float ContactInvulnerabilitySeconds = 0.45f;

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
        private DamageMaskDefinition damageMask;
        private Vector2 cannonDirection = Vector2.UnitX;
        private Vector2 knockbackVelocity;
        private Vector2 pendingRespawnPosition;
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

        public WeaponStyleId ActiveStyle
        {
            get { return PlayerStatus.RunProgress.Weapons.ActiveStyle; }
        }

        public int ActiveWeaponLevel
        {
            get { return PlayerStatus.RunProgress.Weapons.ActiveLevel; }
        }

        private Player1()
        {
            ResetForStage();
        }

        public override void Update()
        {
            float deltaSeconds = (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;

            if (respawnTimer > 0f)
            {
                respawnTimer -= deltaSeconds;
                if (respawnTimer <= 0f)
                    RestoreAfterRespawn();
                return;
            }

            if (hullDestroyedQueued)
                return;

            if (invulnerabilityTimer > 0f)
                invulnerabilityTimer -= deltaSeconds;
            if (fireCooldown > 0f)
                fireCooldown -= deltaSeconds;

            if (Input.WasPreviousStylePressed())
                CycleStyle(-1);
            else if (Input.WasNextStylePressed())
                CycleStyle(1);

            Vector2 moveDirection = Input.GetMovementDirection();
            Vector2 aimDirection = Input.GetAimDirection();
            if (aimDirection == Vector2.Zero)
                aimDirection = Vector2.UnitX;
            else
                aimDirection.Normalize();

            cannonDirection = aimDirection;
            Orientation = 0f;

            Vector2 movementVelocity = moveDirection * MoveSpeed;
            knockbackVelocity = Vector2.Lerp(knockbackVelocity, Vector2.Zero, MathHelper.Clamp(6f * deltaSeconds, 0f, 1f));
            Velocity = movementVelocity + knockbackVelocity;
            Position += Velocity * deltaSeconds;
            ClampToArena();

            if (Input.IsFireHeld())
                TryFire();
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (IsDead)
                return;

            bool flicker = invulnerabilityTimer > 0f && ((int)(Game1.GameTime.TotalGameTime.TotalSeconds * 18f) % 2 == 0);
            if (!flicker)
                sprite.Draw(spriteBatch, Position, color, 0f, RenderScale);

            DrawAuxiliaryModules(spriteBatch, flicker);
        }

        public void ResetForStage()
        {
            RefreshLoadoutVisuals();
            Position = GetSpawnPosition();
            pendingRespawnPosition = Position;
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
            pendingRespawnPosition = Position;
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

            DamageResult result = sprite.ApplyDamage(Position, impactPoint, RenderScale, damageMask, damageMask.ContactImpact, Math.Max(1, damage));
            if (result.CellsRemoved > 0)
            {
                invulnerabilityTimer = ContactInvulnerabilitySeconds;
                EntityManager.SpawnImpactParticles(impactPoint, ColorUtil.ParseHex(WeaponCatalog.GetStyle(ActiveStyle).AccentColor, Color.Orange), damageMask.ContactImpact.DebrisBurstCount, damageMask.ContactImpact.DebrisSpeed, Vector2.Zero);
            }

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
            pendingRespawnPosition = GetSpawnPosition();
            RestoreAfterRespawn();
        }

        public void CollectPowerup()
        {
            PowerupCollectOutcome outcome = PlayerStatus.RunProgress.Weapons.ApplyPowerup();
            switch (outcome)
            {
                case PowerupCollectOutcome.LevelUp:
                case PowerupCollectOutcome.UnlockedStyle:
                    RefreshLoadoutVisuals();
                    break;

                case PowerupCollectOutcome.OverflowReward:
                    PlayerStatus.AddPoints(200);
                    RefreshLoadoutVisuals();
                    break;
            }

            Sound.Spawn.Play(0.15f, 0.2f, 0.1f);
            invulnerabilityTimer = Math.Max(invulnerabilityTimer, 0.2f);
        }

        private void TryFire()
        {
            WeaponLevelDefinition level = WeaponCatalog.GetLevel(ActiveStyle, ActiveWeaponLevel);
            if (fireCooldown > 0f)
                return;

            fireCooldown = level.FireIntervalSeconds;
            switch (ActiveStyle)
            {
                case WeaponStyleId.Missile:
                    FireSpread(level, 0f, 4f + ActiveWeaponLevel * 2f);
                    break;

                case WeaponStyleId.Blade:
                    FireSpread(level, 0.45f, 0f, 0.7f);
                    break;

                case WeaponStyleId.Drone:
                    FirePrimary(level, 0f, 0f, 0f);
                    FireDroneSupport(level);
                    break;

                case WeaponStyleId.Fortress:
                    FireSpread(level, 0.12f, 0f, 1.2f);
                    break;

                default:
                    FireSpread(level, 0f, 0f);
                    break;
            }
        }

        private void FireSpread(WeaponLevelDefinition level, float homingStrength, float extraLifetime, float scale = 1f)
        {
            int count = Math.Max(1, level.ProjectileCount);
            float totalSpread = MathHelper.ToRadians(level.SpreadDegrees);
            float step = count <= 1 ? 0f : totalSpread / (count - 1);
            float start = -totalSpread * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float angle = start + step * i;
                Vector2 direction = Vector2.Transform(cannonDirection, Matrix.CreateRotationZ(angle));
                float lateral = count <= 1 ? 0f : (i - (count - 1) * 0.5f) * 8f;
                FirePrimary(level, lateral, extraLifetime, homingStrength, direction, scale);
            }
        }

        private void FirePrimary(WeaponLevelDefinition level, float lateralOffset, float extraLifetime, float homingStrength, Vector2? directionOverride = null, float scale = 1f)
        {
            Vector2 direction = directionOverride ?? cannonDirection;
            if (direction == Vector2.Zero)
                direction = Vector2.UnitX;
            else
                direction.Normalize();

            Vector2 perpendicular = new Vector2(-direction.Y, direction.X);
            Vector2 spawnPoint = Position + direction * 30f + perpendicular * lateralOffset;
            ProceduralSpriteDefinition projectile = WeaponCatalog.CreateProjectileDefinition(ActiveStyle, ActiveWeaponLevel, true);
            EntityManager.Add(new Bullet(
                spawnPoint,
                direction * level.ProjectileSpeed,
                true,
                level.ProjectileDamage,
                level.Impact,
                projectile,
                level.Pierce ? Math.Max(1, level.PierceCount) : 0,
                2.4f + extraLifetime,
                homingStrength,
                scale));
            Sound.Shot.Play(0.16f, 0f, 0f);
        }

        private void FireDroneSupport(WeaponLevelDefinition level)
        {
            int drones = 1 + ActiveWeaponLevel;
            for (int i = 0; i < drones; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float vertical = (i / 2) * 12f + 18f;
                Vector2 offset = new Vector2(-18f, side * vertical);
                Vector2 spawn = Position + offset + cannonDirection * 24f;
                EntityManager.Add(new Bullet(
                    spawn,
                    cannonDirection * (level.ProjectileSpeed + 40f),
                    true,
                    Math.Max(1, level.ProjectileDamage),
                    level.Impact,
                    WeaponCatalog.CreateProjectileDefinition(WeaponStyleId.Drone, ActiveWeaponLevel, true),
                    0,
                    2.2f,
                    0f,
                    0.9f));
            }
        }

        private void CycleStyle(int direction)
        {
            PlayerStatus.RunProgress.Weapons.Cycle(direction);
            RefreshLoadoutVisuals();
            fireCooldown = 0f;
        }

        private void DrawAuxiliaryModules(SpriteBatch spriteBatch, bool flicker)
        {
            if (flicker)
                return;

            float cannonAngle = cannonDirection.ToAngle();
            cannonSprite.Draw(spriteBatch, Position + new Vector2(14f, 0f), Color.White, cannonAngle, 1f);

            if (ActiveStyle == WeaponStyleId.Drone)
            {
                int drones = 1 + ActiveWeaponLevel;
                for (int i = 0; i < drones; i++)
                {
                    float side = i % 2 == 0 ? -1f : 1f;
                    float vertical = (i / 2) * 12f + 18f;
                    Vector2 dronePos = Position + new Vector2(-18f, side * vertical);
                    PixelArtRenderer.DrawRows(
                        spriteBatch,
                        Game1.UiPixel,
                        WeaponCatalog.GetStyle(WeaponStyleId.Drone).IconRows,
                        dronePos,
                        2.2f,
                        ColorUtil.ParseHex(WeaponCatalog.GetStyle(WeaponStyleId.Drone).PrimaryColor, Color.White),
                        ColorUtil.ParseHex(WeaponCatalog.GetStyle(WeaponStyleId.Drone).SecondaryColor, Color.LightGreen),
                        ColorUtil.ParseHex(WeaponCatalog.GetStyle(WeaponStyleId.Drone).AccentColor, Color.Orange),
                        true);
                }
            }
        }

        private void ClampToArena()
        {
            Vector2 size = Size * 0.5f;
            float left = Game1.ScreenSize.X * 0.06f + size.X;
            float right = Game1.ScreenSize.X * 0.94f - size.X;
            float top = Game1.ScreenSize.Y * 0.06f + size.Y;
            float bottom = Game1.ScreenSize.Y * 0.94f - size.Y;

            Position = new Vector2(
                MathHelper.Clamp(Position.X, left, right),
                MathHelper.Clamp(Position.Y, top, bottom));
        }

        private static Vector2 GetSpawnPosition()
        {
            return new Vector2(Game1.ScreenSize.X * 0.18f, Game1.ScreenSize.Y * 0.5f);
        }

        private void RefreshLoadoutVisuals()
        {
            WeaponStyleId style = ActiveStyle;
            int level = Math.Max(0, ActiveWeaponLevel);
            sprite = new ProceduralSpriteInstance(Game1.Instance.GraphicsDevice, WeaponCatalog.CreateHullDefinition(style, level));
            cannonSprite = new ProceduralSpriteInstance(Game1.Instance.GraphicsDevice, WeaponCatalog.CreateCannonDefinition(style, level));
            damageMask = WeaponCatalog.CreatePlayerDamageMask(style, level);
            RenderScale = 1f;
        }

        private void RestoreAfterRespawn()
        {
            RefreshLoadoutVisuals();
            Position = pendingRespawnPosition;
            ClampToArena();
            Velocity = Vector2.Zero;
            knockbackVelocity = Vector2.Zero;
            respawnTimer = 0f;
            invulnerabilityTimer = InvulnerabilitySeconds;
            cannonDirection = Vector2.UnitX;
            color = Color.White;
        }
    }
}
