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
        private Vector3 combatAimDirection = Vector3.UnitX;
        private Vector2 knockbackVelocity;
        private Vector2 pendingRespawnPosition;
        private Vector2 chaseReticle;
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

        internal ProceduralSpriteInstance CannonSpriteInstance
        {
            get { return cannonSprite; }
        }

        internal Vector2 ChaseReticle
        {
            get { return chaseReticle; }
        }

        internal Vector2 CannonDirection
        {
            get { return cannonDirection; }
        }

        internal Vector3 CombatAimDirection
        {
            get { return combatAimDirection; }
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

            ViewMode currentViewMode = Game1.Instance?.CurrentViewMode ?? ViewMode.SideScroller;
            PlayerCommandFrame command = Input.GetPlayerCommandFrame(currentViewMode);
            Vector2 aimDirection;
            if (currentViewMode == ViewMode.Chase3D)
            {
                chaseReticle += command.ReticleDelta * (deltaSeconds * 1.85f);
                chaseReticle = Vector2.Clamp(chaseReticle, new Vector2(-0.92f, -0.7f), new Vector2(0.92f, 0.84f));
                combatAimDirection = Late3DRenderer.ResolveCombatAimDirection(CombatPosition, chaseReticle);
                aimDirection = new Vector2(combatAimDirection.X, combatAimDirection.Y);
            }
            else
            {
                chaseReticle = Vector2.Lerp(chaseReticle, Vector2.Zero, MathHelper.Clamp(deltaSeconds * 6f, 0f, 1f));
                aimDirection = Input.GetAimDirection();
                combatAimDirection = new Vector3(aimDirection.X, aimDirection.Y, 0f);
            }

            if (aimDirection == Vector2.Zero)
                aimDirection = Vector2.UnitX;
            else
                aimDirection.Normalize();

            cannonDirection = aimDirection;
            if (combatAimDirection == Vector3.Zero)
                combatAimDirection = Vector3.UnitX;
            else
                combatAimDirection.Normalize();
            Orientation = 0f;

            float moveSpeed = MoveSpeed * PlayerStatus.RunProgress.MoveSpeedMultiplier;
            knockbackVelocity = Vector2.Lerp(knockbackVelocity, Vector2.Zero, MathHelper.Clamp(6f * deltaSeconds, 0f, 1f));
            if (currentViewMode == ViewMode.Chase3D && CombatSpaceMath.IsDepthAwareViewActive)
            {
                float travelVelocity = 0f;
                if (command.BoostHeld)
                    travelVelocity += moveSpeed * 0.82f;
                if (command.BrakeHeld)
                    travelVelocity -= moveSpeed * 0.46f;

                CombatVelocity = new Vector3(
                    travelVelocity,
                    command.AltitudeInput * moveSpeed + knockbackVelocity.Y,
                    command.DepthInput * moveSpeed + knockbackVelocity.X);
                Orientation = MathHelper.Clamp(-command.DepthInput * 0.16f, -0.18f, 0.18f);
            }
            else
            {
                CombatVelocity = new Vector3(
                    command.TravelInput * moveSpeed + knockbackVelocity.X,
                    command.AltitudeInput * moveSpeed + knockbackVelocity.Y,
                    0f);
            }

            CombatPosition += CombatVelocity * deltaSeconds;
            ClampToArena();

            if (ActiveStyle == WeaponStyleId.Drone)
                UpdateDrones();

            if (command.FireHeld)
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
            CombatPosition = new Vector3(GetSpawnPosition(), 0f);
            pendingRespawnPosition = Position;
            CombatVelocity = Vector3.Zero;
            knockbackVelocity = Vector2.Zero;
            fireCooldown = 0f;
            droneSupportTimer = 0f;
            respawnTimer = 0f;
            invulnerabilityTimer = InvulnerabilitySeconds;
            hullDestroyedQueued = false;
            cannonDirection = Vector2.UnitX;
            combatAimDirection = Vector3.UnitX;
            chaseReticle = Vector2.Zero;
            color = Color.White;
            LateralDepth = 0f;
        }

        public void StartRespawn(float delaySeconds)
        {
            pendingRespawnPosition = Position;
            respawnTimer = delaySeconds <= 0f ? RespawnSeconds : delaySeconds;
            CombatVelocity = Vector3.Zero;
            knockbackVelocity = Vector2.Zero;
            fireCooldown = 0f;
            droneSupportTimer = 0f;
            DepthVelocity = 0f;
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
                Game1.Instance.Feedback?.Handle(new FeedbackEvent(FeedbackEventType.PlayerDamaged, impactPoint, MathHelper.Clamp(damage * 0.18f, 0.45f, 1f), ActiveStyle, result.CoreCellsRemoved > 0));
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

            invulnerabilityTimer = Math.Max(invulnerabilityTimer, 0.2f);
            Color accent = ColorUtil.ParseHex(style.AccentColor, Color.Orange);
            EntityManager.SpawnShockwave(Position, accent * (immediateUpgrade ? 0.28f : 0.18f), 10f, immediateUpgrade ? 74f : 52f, immediateUpgrade ? 0.22f : 0.18f);
            EntityManager.SpawnFlash(Position, accent * 0.22f, 14f, immediateUpgrade ? 66f : 54f, 0.14f);
            Game1.Instance.Feedback?.Handle(new FeedbackEvent(immediateUpgrade ? FeedbackEventType.Upgrade : FeedbackEventType.Pickup, Position, immediateUpgrade ? 0.9f : 0.55f, styleId, immediateUpgrade));
        }

        public void RefreshLoadout()
        {
            RefreshLoadoutVisuals();
        }

        public PlayerSnapshotData CaptureSnapshot()
        {
            return new PlayerSnapshotData
            {
                EntityId = EntityId,
                Position = new Vector2Data(Position.X, Position.Y),
                Velocity = new Vector2Data(Velocity.X, Velocity.Y),
                CombatPosition = new Vector3Data(CombatPosition.X, CombatPosition.Y, CombatPosition.Z),
                CombatVelocity = new Vector3Data(CombatVelocity.X, CombatVelocity.Y, CombatVelocity.Z),
                CannonDirection = new Vector2Data(cannonDirection.X, cannonDirection.Y),
                KnockbackVelocity = new Vector2Data(knockbackVelocity.X, knockbackVelocity.Y),
                PendingRespawnPosition = new Vector2Data(pendingRespawnPosition.X, pendingRespawnPosition.Y),
                ChaseReticle = new Vector2Data(chaseReticle.X, chaseReticle.Y),
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
            if (snapshot.CombatPosition != null)
                CombatPosition = new Vector3(snapshot.CombatPosition.X, snapshot.CombatPosition.Y, snapshot.CombatPosition.Z);
            else
                CombatPosition = new Vector3(snapshot.Position.X, snapshot.Position.Y, 0f);
            if (snapshot.CombatVelocity != null)
                CombatVelocity = new Vector3(snapshot.CombatVelocity.X, snapshot.CombatVelocity.Y, snapshot.CombatVelocity.Z);
            else
                CombatVelocity = new Vector3(snapshot.Velocity.X, snapshot.Velocity.Y, 0f);
            cannonDirection = new Vector2(snapshot.CannonDirection.X, snapshot.CannonDirection.Y);
            combatAimDirection = new Vector3(cannonDirection.X, cannonDirection.Y, 0f);
            knockbackVelocity = new Vector2(snapshot.KnockbackVelocity.X, snapshot.KnockbackVelocity.Y);
            pendingRespawnPosition = new Vector2(snapshot.PendingRespawnPosition.X, snapshot.PendingRespawnPosition.Y);
            chaseReticle = snapshot.ChaseReticle == null ? Vector2.Zero : new Vector2(snapshot.ChaseReticle.X, snapshot.ChaseReticle.Y);
            respawnTimer = snapshot.RespawnTimer;
            invulnerabilityTimer = snapshot.InvulnerabilityTimer;
            fireCooldown = snapshot.FireCooldown;
            droneSupportTimer = snapshot.DroneSupportTimer;
            hullDestroyedQueued = snapshot.HullDestroyedQueued;
            sprite?.RestoreMaskSnapshot(snapshot.HullMask);
            RestoreEntityId(snapshot.EntityId);
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

            Game1.Instance.Feedback?.Handle(new FeedbackEvent(FeedbackEventType.PlayerShot, Position + cannonDirection * 28f, 0.55f + ActiveWeaponLevel * 0.12f, ActiveStyle));
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
            EntityManager.ClearFriendlyBeams();

            int count = Math.Max(1, level.BeamCount);
            float spacing = Math.Max(0f, level.BeamSpacing);
            Vector2 direction = cannonDirection;
            if (direction == Vector2.Zero)
                direction = Vector2.UnitX;
            else
                direction.Normalize();

            Vector2 perpendicular = new Vector2(-direction.Y, direction.X);
            float centerOffset = (count - 1) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float lateralOffset = count <= 1 ? 0f : (i - centerOffset) * spacing;
                float lateralFactor = count <= 1 ? 0f : (i - centerOffset) / Math.Max(1f, centerOffset);
                Vector2 origin = Position + direction * 26f + perpendicular * lateralOffset;
                Vector3 combatDirection = ResolveShotDirection3(direction, 0f, lateralFactor);
                Vector3 combatOrigin = CombatPosition + combatDirection * 26f + ResolveCombatSpreadOffset(direction, combatDirection, lateralOffset, lateralFactor);
                EntityManager.Add(new BeamShot(
                    origin,
                    direction,
                    level.BeamLength,
                    level.BeamThickness,
                    level.BeamDurationSeconds,
                    level.BeamTickDamage,
                    true,
                    level.Impact,
                    WeaponCatalog.GetStyle(ActiveStyle).PrimaryColor,
                    WeaponCatalog.GetStyle(ActiveStyle).AccentColor,
                    combatOrigin,
                    combatDirection));
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
            float centerOffset = (count - 1) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float angle = start + step * i;
                Vector2 direction = Vector2.Transform(cannonDirection, Matrix.CreateRotationZ(angle));
                float lateral = count <= 1 ? 0f : (i - centerOffset) * 8f;
                float lateralFactor = count <= 1 ? 0f : (i - centerOffset) / Math.Max(1f, centerOffset);
                FirePrimary(level, direction, lateral, forwardOffset, homingStrength, scale, angle, lateralFactor);
            }
        }

        private void FirePrimary(WeaponLevelDefinition level, Vector2 direction, float lateralOffset, float forwardOffset, float homingStrength, float scale, float spreadAngle, float lateralFactor)
        {
            if (direction == Vector2.Zero)
                direction = Vector2.UnitX;
            else
                direction.Normalize();

            Vector2 perpendicular = new Vector2(-direction.Y, direction.X);
            Vector2 spawnPoint = Position + direction * (30f + forwardOffset) + perpendicular * lateralOffset;
            Vector3 shotDirection = ResolveShotDirection3(direction, spreadAngle, lateralFactor);
            Vector3 combatSpawnPoint = CombatPosition + shotDirection * (30f + forwardOffset) + ResolveCombatSpreadOffset(direction, shotDirection, lateralOffset, lateralFactor);
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
                level.HomingDelaySeconds,
                combatSpawnPoint,
                shotDirection * level.ProjectileSpeed));
        }

        private Vector3 ResolveShotDirection3(Vector2 planarDirection, float spreadAngle = 0f, float lateralFactor = 0f)
        {
            if (Game1.Instance != null && Game1.Instance.CurrentViewMode == ViewMode.Chase3D && CombatSpaceMath.IsDepthAwareViewActive)
            {
                Vector3 direction = combatAimDirection;
                if (direction == Vector3.Zero)
                    direction = new Vector3(planarDirection.X, planarDirection.Y, 0f);

                if (direction == Vector3.Zero)
                    direction = Vector3.UnitX;

                if (spreadAngle != 0f || MathF.Abs(lateralFactor) > 0.001f)
                {
                    ResolveCombatSpreadBasis(direction, out Vector3 upAxis, out Vector3 rightAxis);
                    float spreadStrength = MathF.Sin(MathF.Abs(spreadAngle));
                    direction += upAxis * MathF.Sin(spreadAngle) * 0.78f;
                    direction += rightAxis * lateralFactor * MathF.Max(0.18f, spreadStrength) * 0.84f;
                }

                direction.Normalize();
                return direction;
            }

            Vector3 planar = new Vector3(planarDirection.X, planarDirection.Y, 0f);
            if (planar == Vector3.Zero)
                planar = Vector3.UnitX;

            planar.Normalize();
            return planar;
        }

        internal void DrawPresentationModules(SpriteBatch spriteBatch)
        {
            DrawAuxiliaryModules(spriteBatch, false);
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
                Vector3 combatSpawn = CombatPosition + new Vector3(offset.X, offset.Y, side * 16f);
                Vector2 spawn = new Vector2(combatSpawn.X, combatSpawn.Y);
                Enemy nearest = EntityManager.Enemies.OrderBy(enemy => Vector3.DistanceSquared(enemy.CombatPosition, combatSpawn)).FirstOrDefault();
                Vector2 direction = nearest == null ? Vector2.UnitX : nearest.Position - spawn;
                if (direction == Vector2.Zero)
                    direction = Vector2.UnitX;
                else
                    direction.Normalize();
                Vector3 combatDirection = nearest == null
                    ? new Vector3(direction.X, direction.Y, 0f)
                    : Vector3.Normalize(nearest.CombatPosition - combatSpawn);

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
                    0.08f,
                    combatSpawn,
                    combatDirection * (level.ProjectileSpeed + 70f)));
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
            CombatPosition = CombatSpaceMath.ClampToArena(CombatPosition, Size * 0.5f);
        }

        private static Vector2 GetSpawnPosition()
        {
            return new Vector2(Game1.ScreenSize.X * 0.18f, Game1.ScreenSize.Y * 0.5f);
        }

        private static void ResolveCombatSpreadBasis(Vector3 forward, out Vector3 upAxis, out Vector3 rightAxis)
        {
            Vector3 baseUp = MathF.Abs(Vector3.Dot(forward, Vector3.UnitY)) > 0.92f ? Vector3.UnitZ : Vector3.UnitY;
            rightAxis = Vector3.Cross(forward, baseUp);
            if (rightAxis == Vector3.Zero)
                rightAxis = Vector3.UnitZ;
            else
                rightAxis.Normalize();

            upAxis = Vector3.Cross(rightAxis, forward);
            if (upAxis == Vector3.Zero)
                upAxis = Vector3.UnitY;
            else
                upAxis.Normalize();
        }

        private Vector3 ResolveCombatSpreadOffset(Vector2 planarDirection, Vector3 combatDirection, float lateralOffset, float lateralFactor)
        {
            if (Game1.Instance != null && Game1.Instance.CurrentViewMode == ViewMode.Chase3D && CombatSpaceMath.IsDepthAwareViewActive)
            {
                ResolveCombatSpreadBasis(combatDirection, out Vector3 upAxis, out Vector3 rightAxis);
                return rightAxis * lateralOffset + upAxis * (lateralFactor * 6f);
            }

            Vector2 perpendicular = new Vector2(-planarDirection.Y, planarDirection.X);
            return new Vector3(perpendicular.X, perpendicular.Y, 0f) * lateralOffset;
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
                BeamLength = baseLevel.BeamLength,
                BeamCount = baseLevel.BeamCount,
                BeamSpacing = baseLevel.BeamSpacing,
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
                    level.BeamLength += rank * 10f;
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
            CombatPosition = new Vector3(pendingRespawnPosition, 0f);
            ClampToArena();
            CombatVelocity = Vector3.Zero;
            knockbackVelocity = Vector2.Zero;
            respawnTimer = 0f;
            invulnerabilityTimer = InvulnerabilitySeconds;
            cannonDirection = Vector2.UnitX;
            combatAimDirection = Vector3.UnitX;
            chaseReticle = Vector2.Zero;
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
