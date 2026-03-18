using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceBurst.RuntimeData;
using System;
using System.Linq;

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
        private float droneSupportTimer;
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

        public int ActiveWeaponRank
        {
            get { return PlayerStatus.RunProgress.Weapons.ActiveRank; }
        }

        public float HullRatio
        {
            get
            {
                if (sprite?.Mask == null || sprite.Mask.InitialOccupiedCount <= 0)
                    return 1f;

                return sprite.Mask.OccupiedCount / (float)sprite.Mask.InitialOccupiedCount;
            }
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
            if (droneSupportTimer > 0f)
                droneSupportTimer -= deltaSeconds;

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

            Vector2 movementVelocity = moveDirection * (MoveSpeed * PlayerStatus.RunProgress.MoveSpeedMultiplier);
            knockbackVelocity = Vector2.Lerp(knockbackVelocity, Vector2.Zero, MathHelper.Clamp(6f * deltaSeconds, 0f, 1f));
            Velocity = movementVelocity + knockbackVelocity;
            Position += Velocity * deltaSeconds;
            ClampToArena();

            if (ActiveStyle == WeaponStyleId.Drone)
                UpdateDrones();

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
            droneSupportTimer = 0f;
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
            droneSupportTimer = 0f;
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

        public void CollectPowerup(WeaponStyleId styleId)
        {
            WeaponStyleDefinition style = WeaponCatalog.GetStyle(styleId);
            bool immediateUpgrade = styleId == ActiveStyle && ActiveWeaponLevel < 3;
            if (immediateUpgrade)
            {
                PlayerStatus.RunProgress.ApplyWeaponUpgrade(styleId);
                RefreshLoadout();
            }
            else
            {
                PlayerStatus.RunProgress.AddUpgradeCharge(styleId);
            }

            Sound.Spawn.Play(0.15f, 0.2f, 0.1f);
            invulnerabilityTimer = Math.Max(invulnerabilityTimer, 0.2f);
            Color accent = ColorUtil.ParseHex(style.AccentColor, Color.Orange);
            EntityManager.SpawnShockwave(Position, accent * (immediateUpgrade ? 0.28f : 0.18f), 10f, immediateUpgrade ? 74f : 52f, immediateUpgrade ? 0.22f : 0.18f);
            EntityManager.SpawnFlash(Position, accent * 0.22f, 14f, immediateUpgrade ? 66f : 54f, 0.14f);
        }

        public void RefreshLoadout()
        {
            RefreshLoadoutVisuals();
        }

        public PlayerSnapshotData CaptureSnapshot()
        {
            return new PlayerSnapshotData
            {
                Position = new Vector2Data(Position.X, Position.Y),
                Velocity = new Vector2Data(Velocity.X, Velocity.Y),
                CannonDirection = new Vector2Data(cannonDirection.X, cannonDirection.Y),
                KnockbackVelocity = new Vector2Data(knockbackVelocity.X, knockbackVelocity.Y),
                PendingRespawnPosition = new Vector2Data(pendingRespawnPosition.X, pendingRespawnPosition.Y),
                RespawnTimer = respawnTimer,
                InvulnerabilityTimer = invulnerabilityTimer,
                FireCooldown = fireCooldown,
                DroneSupportTimer = droneSupportTimer,
                HullDestroyedQueued = hullDestroyedQueued,
                HullMask = sprite?.CaptureMaskSnapshot() ?? new MaskSnapshotData(),
            };
        }

        public void RestoreSnapshot(PlayerSnapshotData snapshot)
        {
            if (snapshot == null)
                return;

            RefreshLoadoutVisuals();
            Position = new Vector2(snapshot.Position.X, snapshot.Position.Y);
            Velocity = new Vector2(snapshot.Velocity.X, snapshot.Velocity.Y);
            cannonDirection = new Vector2(snapshot.CannonDirection.X, snapshot.CannonDirection.Y);
            knockbackVelocity = new Vector2(snapshot.KnockbackVelocity.X, snapshot.KnockbackVelocity.Y);
            pendingRespawnPosition = new Vector2(snapshot.PendingRespawnPosition.X, snapshot.PendingRespawnPosition.Y);
            respawnTimer = snapshot.RespawnTimer;
            invulnerabilityTimer = snapshot.InvulnerabilityTimer;
            fireCooldown = snapshot.FireCooldown;
            droneSupportTimer = snapshot.DroneSupportTimer;
            hullDestroyedQueued = snapshot.HullDestroyedQueued;
            sprite?.RestoreMaskSnapshot(snapshot.HullMask);
            ClampToArena();
        }

        private void TryFire()
        {
            WeaponLevelDefinition level = ResolveWeaponLevel();
            if (fireCooldown > 0f)
                return;

            fireCooldown = level.FireIntervalSeconds;

            switch (level.FireMode)
            {
                case FireMode.SpreadShotgun:
                    FireShotgun(level);
                    break;

                case FireMode.BeamBurst:
                    FireBeam(level);
                    break;

                case FireMode.PlasmaOrb:
                    FirePlasma(level);
                    break;

                case FireMode.MissileLauncher:
                    FireMissiles(level);
                    break;

                case FireMode.RailBurst:
                    FireRail(level);
                    break;

                case FireMode.ArcChain:
                    FireArc(level);
                    break;

                case FireMode.BladeWave:
                    FireBlade(level);
                    break;

                case FireMode.DroneCommand:
                    FirePulseLike(level);
                    break;

                case FireMode.FortressPulse:
                    FireFortress(level);
                    break;

                default:
                    FirePulseLike(level);
                    break;
            }
        }

        private void FirePulseLike(WeaponLevelDefinition level)
        {
            SpawnVolley(level, level.ProjectileCount, level.SpreadDegrees, 0f, 0f, 1f);
            SpawnMuzzleFx(level, Position + cannonDirection * 30f);
        }

        private void FireShotgun(WeaponLevelDefinition level)
        {
            SpawnVolley(level, Math.Max(3, level.ProjectileCount), Math.Max(36f, level.SpreadDegrees), 0f, 0f, 0.95f);
            SpawnMuzzleFx(level, Position + cannonDirection * 28f);
        }

        private void FireBeam(WeaponLevelDefinition level)
        {
            int count = Math.Max(1, level.ProjectileCount);
            float spread = MathHelper.ToRadians(Math.Max(0f, level.SpreadDegrees));
            float step = count <= 1 ? 0f : spread / (count - 1);
            float start = -spread * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float angle = start + step * i;
                Vector2 direction = Vector2.Transform(cannonDirection, Matrix.CreateRotationZ(angle));
                if (direction == Vector2.Zero)
                    direction = Vector2.UnitX;
                else
                    direction.Normalize();

                Vector2 origin = Position + direction * 26f;
                EntityManager.Add(new BeamShot(
                    origin,
                    direction,
                    520f + ActiveWeaponLevel * 40f + ActiveWeaponRank * 20f,
                    level.BeamThickness,
                    level.BeamDurationSeconds,
                    level.BeamTickDamage,
                    true,
                    level.Impact,
                    WeaponCatalog.GetStyle(ActiveStyle).PrimaryColor,
                    WeaponCatalog.GetStyle(ActiveStyle).AccentColor));
            }

            SpawnMuzzleFx(level, Position + cannonDirection * 30f);
        }

        private void FirePlasma(WeaponLevelDefinition level)
        {
            SpawnVolley(level, Math.Max(1, level.ProjectileCount), Math.Max(0f, level.SpreadDegrees), 0f, 0f, level.ProjectileScale);
            SpawnMuzzleFx(level, Position + cannonDirection * 28f);
        }

        private void FireMissiles(WeaponLevelDefinition level)
        {
            SpawnVolley(level, Math.Max(1, level.ProjectileCount), Math.Max(0f, level.SpreadDegrees), 0f, 2.2f, level.ProjectileScale);
            SpawnMuzzleFx(level, Position + cannonDirection * 28f);
        }

        private void FireRail(WeaponLevelDefinition level)
        {
            SpawnVolley(level, Math.Max(1, level.ProjectileCount), Math.Max(0f, level.SpreadDegrees), 0f, 0f, level.ProjectileScale);
            SpawnMuzzleFx(level, Position + cannonDirection * 34f);
        }

        private void FireArc(WeaponLevelDefinition level)
        {
            SpawnVolley(level, Math.Max(1, level.ProjectileCount), Math.Max(18f, level.SpreadDegrees), 0f, 0f, level.ProjectileScale);
            SpawnMuzzleFx(level, Position + cannonDirection * 26f);
        }

        private void FireBlade(WeaponLevelDefinition level)
        {
            SpawnVolley(level, Math.Max(2, level.ProjectileCount), Math.Max(32f, level.SpreadDegrees), 0f, 0f, level.ProjectileScale);
            SpawnMuzzleFx(level, Position + cannonDirection * 22f);
        }

        private void FireFortress(WeaponLevelDefinition level)
        {
            SpawnVolley(level, Math.Max(1, level.ProjectileCount), Math.Max(0f, level.SpreadDegrees), 0f, 0f, level.ProjectileScale);
            if (ActiveWeaponLevel >= 2)
                SpawnVolley(level, 2, 18f, -10f, 0f, level.ProjectileScale * 0.9f);
            SpawnMuzzleFx(level, Position + cannonDirection * 24f);
        }

        private void SpawnVolley(WeaponLevelDefinition level, int count, float spreadDegrees, float forwardOffset, float homingStrength, float scale)
        {
            float totalSpread = MathHelper.ToRadians(spreadDegrees);
            float step = count <= 1 ? 0f : totalSpread / (count - 1);
            float start = -totalSpread * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float angle = start + step * i;
                Vector2 direction = Vector2.Transform(cannonDirection, Matrix.CreateRotationZ(angle));
                float lateral = count <= 1 ? 0f : (i - (count - 1) * 0.5f) * 8f;
                FirePrimary(level, direction, lateral, forwardOffset, homingStrength, scale);
            }
        }

        private void FirePrimary(WeaponLevelDefinition level, Vector2 direction, float lateralOffset, float forwardOffset, float homingStrength, float scale)
        {
            if (direction == Vector2.Zero)
                direction = Vector2.UnitX;
            else
                direction.Normalize();

            Vector2 perpendicular = new Vector2(-direction.Y, direction.X);
            Vector2 spawnPoint = Position + direction * (30f + forwardOffset) + perpendicular * lateralOffset;
            ProceduralSpriteDefinition projectile = WeaponCatalog.CreateProjectileDefinition(ActiveStyle, ActiveWeaponLevel, true);
            EntityManager.Add(new Bullet(
                spawnPoint,
                direction * level.ProjectileSpeed,
                true,
                level.ProjectileDamage,
                level.Impact,
                projectile,
                level.Pierce ? Math.Max(1, level.PierceCount) : 0,
                level.ProjectileLifetimeSeconds,
                homingStrength,
                level.ProjectileScale * scale,
                level.ProjectileBehavior,
                level.TrailFxStyle,
                level.ImpactFxStyle,
                level.ExplosionRadius,
                level.ChainCount,
                level.HomingDelaySeconds));
            Sound.Shot.Play(0.16f, ResolveShotPitch(level), 0f);
        }

        private void UpdateDrones()
        {
            WeaponLevelDefinition level = ResolveWeaponLevel();
            if (level.DroneCount <= 0 || droneSupportTimer > 0f)
                return;

            droneSupportTimer = Math.Max(0.24f, level.DroneIntervalSeconds);
            for (int i = 0; i < level.DroneCount; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float vertical = (i / 2) * 12f + 18f;
                Vector2 offset = new Vector2(-20f, side * vertical);
                Vector2 spawn = Position + offset;
                Enemy nearest = EntityManager.Enemies.OrderBy(enemy => Vector2.DistanceSquared(enemy.Position, spawn)).FirstOrDefault();
                Vector2 direction = nearest == null ? Vector2.UnitX : nearest.Position - spawn;
                if (direction == Vector2.Zero)
                    direction = Vector2.UnitX;
                else
                    direction.Normalize();

                EntityManager.Add(new Bullet(
                    spawn,
                    direction * (level.ProjectileSpeed + 70f),
                    true,
                    Math.Max(1, level.ProjectileDamage),
                    level.Impact,
                    WeaponCatalog.CreateProjectileDefinition(WeaponStyleId.Drone, ActiveWeaponLevel, true),
                    0,
                    level.ProjectileLifetimeSeconds,
                    nearest == null ? 0f : 0.6f,
                    0.9f,
                    ProjectileBehavior.DroneBolt,
                    TrailFxStyle.Streak,
                    ImpactFxStyle.Drone,
                    0f,
                    0,
                    0.08f));
            }
        }

        private void CycleStyle(int direction)
        {
            PlayerStatus.RunProgress.Weapons.Cycle(direction);
            RefreshLoadoutVisuals();
            fireCooldown = 0f;
            droneSupportTimer = 0f;
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
            else if (ActiveStyle == WeaponStyleId.Fortress)
            {
                Color accent = ColorUtil.ParseHex(WeaponCatalog.GetStyle(WeaponStyleId.Fortress).AccentColor, Color.Orange);
                spriteBatch.Draw(Game1.UiPixel, new Rectangle((int)Position.X - 22, (int)Position.Y - 18, 6, 36), accent * 0.75f);
                spriteBatch.Draw(Game1.UiPixel, new Rectangle((int)Position.X - 14, (int)Position.Y - 22, 4, 44), accent * 0.4f);
            }
            else if (ActiveStyle == WeaponStyleId.Blade)
            {
                Color accent = ColorUtil.ParseHex(WeaponCatalog.GetStyle(WeaponStyleId.Blade).AccentColor, Color.Pink);
                float orbit = (float)Math.Sin(Game1.GameTime.TotalGameTime.TotalSeconds * 6f) * 6f;
                spriteBatch.Draw(Game1.UiPixel, new Rectangle((int)Position.X - 10, (int)(Position.Y - 22 + orbit), 18, 2), accent * 0.85f);
                spriteBatch.Draw(Game1.UiPixel, new Rectangle((int)Position.X - 10, (int)(Position.Y + 20 - orbit), 18, 2), accent * 0.85f);
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

        private WeaponLevelDefinition ResolveWeaponLevel()
        {
            WeaponLevelDefinition baseLevel = WeaponCatalog.GetLevel(ActiveStyle, ActiveWeaponLevel);
            int rank = Math.Max(0, ActiveWeaponRank);
            if (rank <= 0)
                return baseLevel;

            var level = new WeaponLevelDefinition
            {
                FireIntervalSeconds = baseLevel.FireIntervalSeconds,
                ProjectileSpeed = baseLevel.ProjectileSpeed,
                ProjectileDamage = baseLevel.ProjectileDamage,
                ProjectileCount = baseLevel.ProjectileCount,
                SpreadDegrees = baseLevel.SpreadDegrees,
                Pierce = baseLevel.Pierce,
                PierceCount = baseLevel.PierceCount,
                ProjectileLifetimeSeconds = baseLevel.ProjectileLifetimeSeconds,
                ProjectileScale = baseLevel.ProjectileScale,
                HomingDelaySeconds = baseLevel.HomingDelaySeconds,
                ExplosionRadius = baseLevel.ExplosionRadius,
                ChainCount = baseLevel.ChainCount,
                DroneCount = baseLevel.DroneCount,
                DroneIntervalSeconds = baseLevel.DroneIntervalSeconds,
                BeamDurationSeconds = baseLevel.BeamDurationSeconds,
                BeamThickness = baseLevel.BeamThickness,
                BeamTickDamage = baseLevel.BeamTickDamage,
                FireMode = baseLevel.FireMode,
                ProjectileBehavior = baseLevel.ProjectileBehavior,
                MuzzleFxStyle = baseLevel.MuzzleFxStyle,
                TrailFxStyle = baseLevel.TrailFxStyle,
                ImpactFxStyle = baseLevel.ImpactFxStyle,
                Impact = baseLevel.Impact,
            };

            level.FireIntervalSeconds *= MathF.Max(0.62f, 1f - rank * 0.022f);
            level.ProjectileSpeed *= 1f + MathF.Min(0.55f, rank * 0.03f);
            level.ProjectileDamage += rank / 3;
            level.ProjectileLifetimeSeconds *= 1f + MathF.Min(0.4f, rank * 0.018f);
            level.ExplosionRadius += (rank / 4) * 4f;
            level.ChainCount += rank / 5;

            switch (ActiveStyle)
            {
                case WeaponStyleId.Pulse:
                    level.PierceCount += rank / 4;
                    break;
                case WeaponStyleId.Spread:
                    level.SpreadDegrees += rank % 5 == 4 ? 6f : 0f;
                    break;
                case WeaponStyleId.Laser:
                    level.BeamTickDamage += rank / 4;
                    level.BeamThickness += rank % 4 == 3 ? 2f : 0f;
                    break;
                case WeaponStyleId.Plasma:
                case WeaponStyleId.Missile:
                case WeaponStyleId.Fortress:
                    level.ExplosionRadius += rank / 3 * 2f;
                    break;
                case WeaponStyleId.Rail:
                    level.PierceCount += 1 + rank / 4;
                    break;
                case WeaponStyleId.Arc:
                    level.ChainCount += 1 + rank / 4;
                    break;
                case WeaponStyleId.Blade:
                    level.ProjectileCount += rank % 4 == 3 ? 1 : 0;
                    break;
                case WeaponStyleId.Drone:
                    level.DroneCount += rank % 4 == 3 ? 1 : 0;
                    break;
            }

            return level;
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

        private void SpawnMuzzleFx(WeaponLevelDefinition level, Vector2 position)
        {
            Color color = ColorUtil.ParseHex(WeaponCatalog.GetStyle(ActiveStyle).AccentColor, Color.White);
            float startRadius = 10f;
            float endRadius = 26f;

            switch (level.MuzzleFxStyle)
            {
                case MuzzleFxStyle.Laser:
                    startRadius = 14f;
                    endRadius = 34f;
                    break;
                case MuzzleFxStyle.Plasma:
                    startRadius = 12f;
                    endRadius = 30f;
                    break;
                case MuzzleFxStyle.Missile:
                    startRadius = 12f;
                    endRadius = 26f;
                    color = Color.OrangeRed;
                    break;
                case MuzzleFxStyle.Rail:
                    startRadius = 8f;
                    endRadius = 36f;
                    break;
                case MuzzleFxStyle.Arc:
                    startRadius = 10f;
                    endRadius = 30f;
                    color = Color.Cyan;
                    break;
                case MuzzleFxStyle.Fortress:
                    startRadius = 14f;
                    endRadius = 32f;
                    break;
            }

            EntityManager.SpawnFlash(position, color * 0.22f, startRadius, endRadius, 0.08f);
        }

        private float ResolveShotPitch(WeaponLevelDefinition level)
        {
            switch (level.MuzzleFxStyle)
            {
                case MuzzleFxStyle.Laser:
                    return 0.18f;
                case MuzzleFxStyle.Plasma:
                    return -0.12f;
                case MuzzleFxStyle.Missile:
                    return -0.18f;
                case MuzzleFxStyle.Rail:
                    return 0.28f;
                case MuzzleFxStyle.Arc:
                    return 0.1f;
                case MuzzleFxStyle.Blade:
                    return 0.22f;
                case MuzzleFxStyle.Fortress:
                    return -0.06f;
                default:
                    return 0f;
            }
        }
    }
}
