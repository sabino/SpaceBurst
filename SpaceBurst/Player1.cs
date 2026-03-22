using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceBurst.RuntimeData;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaceBurst
{
    class Player1 : Entity
    {
        private const float MoveSpeed = 360f;
        private const float RespawnSeconds = 1.1f;
        private const float InvulnerabilitySeconds = 1.1f;
        private const float ContactInvulnerabilitySeconds = 0.45f;
        private const float ChaseReticleSpeed = 7.8f;
        private const float ChaseEntryRecenterSeconds = 0.7f;

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
        private Vector3 chaseEntryRecenterTarget;
        private float respawnTimer;
        private float invulnerabilityTimer;
        private float fireCooldown;
        private float droneSupportTimer;
        private float chaseEntryRecenterTimer;
        private float authoritativeHullRatio = 1f;
        private bool hullDestroyedQueued;
        private readonly Dictionary<WeaponStyleId, float> supportFireCooldowns = new Dictionary<WeaponStyleId, float>();

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
                return MathHelper.Clamp(authoritativeHullRatio, 0f, 1f);
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

            if ((Game1.Instance?.CampaignDirector?.AllowLiveWeaponCycling ?? false) && Input.WasPreviousStylePressed())
                CycleStyle(-1);
            else if ((Game1.Instance?.CampaignDirector?.AllowLiveWeaponCycling ?? false) && Input.WasNextStylePressed())
                CycleStyle(1);

            ViewMode currentViewMode = Game1.Instance?.CurrentViewMode ?? ViewMode.SideScroller;
            PlayerCommandFrame command = Input.GetPlayerCommandFrame(currentViewMode);
            Vector2 aimDirection;
            if (currentViewMode == ViewMode.Chase3D)
            {
                chaseReticle += command.ReticleDelta * (deltaSeconds * ChaseReticleSpeed);
                chaseReticle = Late3DRenderer.ClampReticle(chaseReticle);
                combatAimDirection = ResolveChaseCombatAim();
                aimDirection = new Vector2(combatAimDirection.X, combatAimDirection.Y);
            }
            else
            {
                chaseReticle = Vector2.Lerp(chaseReticle, Vector2.Zero, MathHelper.Clamp(deltaSeconds * 6f, 0f, 1f));
                chaseEntryRecenterTimer = 0f;
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
            ApplyChaseEntryRecenter(deltaSeconds, currentViewMode);
            ClampToArena();

            PlayerStatus.RunProgress.UpdateFocusFire(command.FireHeld, deltaSeconds);
            UpdateSupportCooldowns(deltaSeconds);
            if (ActiveStyle == WeaponStyleId.Drone || PlayerStatus.RunProgress.Weapons.HasSupportWeapon(WeaponStyleId.Drone))
                UpdateDrones(WeaponStyleId.Drone, command.FireHeld);

            TryAutoFire(command.FireHeld);
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
            authoritativeHullRatio = 1f;
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
            chaseEntryRecenterTarget = Vector3.Zero;
            chaseEntryRecenterTimer = 0f;
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

            bool oneHitKill = PlayerStatus.RunProgress.IsOneHitKillEnabled();
            int resolvedDamage = oneHitKill && sprite?.Mask != null
                ? Math.Max(sprite.Mask.InitialOccupiedCount, Math.Max(1, damage))
                : Math.Max(1, damage);

            DamageResult result = sprite.ApplyDamage(Position, impactPoint, RenderScale, damageMask, damageMask.ContactImpact, resolvedDamage);
            if (result.CellsRemoved > 0)
            {
                invulnerabilityTimer = ContactInvulnerabilitySeconds;
                EntityManager.SpawnImpactParticles(impactPoint, ColorUtil.ParseHex(WeaponCatalog.GetStyle(ActiveStyle).AccentColor, Color.Orange), damageMask.ContactImpact.DebrisBurstCount, damageMask.ContactImpact.DebrisSpeed, Vector2.Zero);
                Game1.Instance.Feedback?.Handle(new FeedbackEvent(FeedbackEventType.PlayerDamaged, impactPoint, MathHelper.Clamp(damage * 0.18f, 0.45f, 1f), ActiveStyle, result.CoreCellsRemoved > 0));
                SyncAuthoritativeHullRatio();
            }

            if (result.Destroyed || oneHitKill)
            {
                hullDestroyedQueued = true;
                authoritativeHullRatio = 0f;
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

        public void CollectPowerup(PowerupPickup pickup)
        {
            if (pickup == null)
                return;

            switch (pickup.PickupKind)
            {
                case PickupKind.XpShard:
                    PlayerStatus.RunProgress.AddXp(pickup.Amount);
                    TriggerPickupFeedback(new Color(86, 240, 255), 0.44f + pickup.Amount * 0.06f, true);
                    break;

                case PickupKind.ScrapCache:
                    PlayerStatus.RunProgress.AddScrap(pickup.Amount);
                    TriggerPickupFeedback(new Color(255, 176, 87), 0.5f + pickup.Amount * 0.08f, true);
                    break;

                default:
                    CollectPowerup(pickup.StyleId);
                    break;
            }
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
            TriggerPickupFeedback(accent, immediateUpgrade ? 0.9f : 0.55f, immediateUpgrade);
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
                ChaseEntryRecenterTarget = new Vector3Data(chaseEntryRecenterTarget.X, chaseEntryRecenterTarget.Y, chaseEntryRecenterTarget.Z),
                ChaseEntryRecenterTimer = chaseEntryRecenterTimer,
                RespawnTimer = respawnTimer,
                InvulnerabilityTimer = invulnerabilityTimer,
                FireCooldown = fireCooldown,
                DroneSupportTimer = droneSupportTimer,
                SupportFireCooldowns = new Dictionary<WeaponStyleId, float>(supportFireCooldowns),
                HullDestroyedQueued = hullDestroyedQueued,
                HullIntegrityRatio = authoritativeHullRatio,
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
            chaseEntryRecenterTarget = snapshot.ChaseEntryRecenterTarget == null
                ? Vector3.Zero
                : new Vector3(snapshot.ChaseEntryRecenterTarget.X, snapshot.ChaseEntryRecenterTarget.Y, snapshot.ChaseEntryRecenterTarget.Z);
            chaseEntryRecenterTimer = snapshot.ChaseEntryRecenterTimer;
            respawnTimer = snapshot.RespawnTimer;
            invulnerabilityTimer = snapshot.InvulnerabilityTimer;
            fireCooldown = snapshot.FireCooldown;
            droneSupportTimer = snapshot.DroneSupportTimer;
            supportFireCooldowns.Clear();
            if (snapshot.SupportFireCooldowns != null)
            {
                foreach (var entry in snapshot.SupportFireCooldowns)
                    supportFireCooldowns[entry.Key] = Math.Max(0f, entry.Value);
            }
            hullDestroyedQueued = snapshot.HullDestroyedQueued;
            sprite?.RestoreMaskSnapshot(snapshot.HullMask);
            authoritativeHullRatio = snapshot.HullIntegrityRatio > 0f ? snapshot.HullIntegrityRatio : 1f;
            SyncAuthoritativeHullRatio();
            RestoreEntityId(snapshot.EntityId);
            ClampToArena();
        }

        private void TriggerPickupFeedback(Color accent, float intensity, bool major)
        {
            invulnerabilityTimer = Math.Max(invulnerabilityTimer, 0.2f);
            EntityManager.SpawnShockwave(Position, accent * (major ? 0.28f : 0.18f), 10f, major ? 74f : 52f, major ? 0.22f : 0.18f);
            EntityManager.SpawnFlash(Position, accent * 0.22f, 14f, major ? 66f : 54f, 0.14f);
            Game1.Instance.Feedback?.Handle(new FeedbackEvent(major ? FeedbackEventType.Upgrade : FeedbackEventType.Pickup, Position, intensity, ActiveStyle, major));
        }

        internal void BeginChaseEntryRecenter()
        {
            Vector2 spawn = GetSpawnPosition();
            chaseEntryRecenterTarget = new Vector3(spawn.X, spawn.Y, 0f);
            chaseEntryRecenterTimer = ChaseEntryRecenterSeconds;
        }

        internal void PreserveChaseViewState(Vector2 reticle)
        {
            chaseReticle = reticle;
            chaseEntryRecenterTarget = CombatPosition;
            chaseEntryRecenterTimer = 0f;
        }

        private void UpdateSupportCooldowns(float deltaSeconds)
        {
            WeaponInventoryState inventory = PlayerStatus.RunProgress.Weapons;
            List<WeaponStyleId> trackedStyles = supportFireCooldowns.Keys.ToList();
            for (int i = 0; i < trackedStyles.Count; i++)
            {
                WeaponStyleId styleId = trackedStyles[i];
                if (!inventory.HasSupportWeapon(styleId))
                {
                    supportFireCooldowns.Remove(styleId);
                    continue;
                }

                supportFireCooldowns[styleId] = Math.Max(0f, supportFireCooldowns[styleId] - deltaSeconds);
            }

            for (int i = 0; i < inventory.SupportWeapons.Count; i++)
            {
                WeaponStyleId styleId = inventory.SupportWeapons[i];
                if (!supportFireCooldowns.ContainsKey(styleId))
                    supportFireCooldowns[styleId] = 0f;
            }
        }

        private void TryAutoFire(bool focusHeld)
        {
            TryFireCore(focusHeld);
            TryFireSupportWeapons(focusHeld);
        }

        private void TryFireCore(bool focusHeld)
        {
            WeaponLevelDefinition level = ResolveWeaponLevel(ActiveStyle);
            FireWeapon(ActiveStyle, level, ref fireCooldown, focusHeld, true);
        }

        private void TryFireSupportWeapons(bool focusHeld)
        {
            IReadOnlyList<WeaponStyleId> supportWeapons = PlayerStatus.RunProgress.Weapons.SupportWeapons;
            for (int i = 0; i < supportWeapons.Count; i++)
            {
                WeaponStyleId styleId = supportWeapons[i];
                float supportCooldown = supportFireCooldowns.TryGetValue(styleId, out float value) ? value : 0f;
                FireWeapon(styleId, ResolveWeaponLevel(styleId), ref supportCooldown, focusHeld, false);
                supportFireCooldowns[styleId] = supportCooldown;
            }
        }

        private void FireWeapon(WeaponStyleId styleId, WeaponLevelDefinition level, ref float cooldown, bool focusHeld, bool primaryWeapon)
        {
            if (cooldown > 0f)
                return;

            cooldown = level.FireIntervalSeconds * PlayerStatus.RunProgress.GetFireIntervalScale(focusHeld) * (primaryWeapon ? 1f : 1.08f);

            switch (level.FireMode)
            {
                case FireMode.SpreadShotgun:
                    FireShotgun(styleId, level);
                    break;

                case FireMode.BeamBurst:
                    FireBeam(styleId, level);
                    break;

                case FireMode.PlasmaOrb:
                    FirePlasma(styleId, level);
                    break;

                case FireMode.MissileLauncher:
                    FireMissiles(styleId, level);
                    break;

                case FireMode.RailBurst:
                    FireRail(styleId, level);
                    break;

                case FireMode.ArcChain:
                    FireArc(styleId, level);
                    break;

                case FireMode.BladeWave:
                    FireBlade(styleId, level);
                    break;

                case FireMode.DroneCommand:
                    FirePulseLike(styleId, level);
                    break;

                case FireMode.FortressPulse:
                    FireFortress(styleId, level);
                    break;

                default:
                    FirePulseLike(styleId, level);
                    break;
            }

            int styleLevel = Math.Max(0, PlayerStatus.RunProgress.Weapons.GetLevel(styleId));
            Game1.Instance.Feedback?.Handle(new FeedbackEvent(FeedbackEventType.PlayerShot, Position + cannonDirection * 28f, (primaryWeapon ? 0.55f : 0.38f) + styleLevel * 0.1f, styleId));
        }

        private void FirePulseLike(WeaponStyleId styleId, WeaponLevelDefinition level)
        {
            SpawnVolley(styleId, level, level.ProjectileCount, level.SpreadDegrees, 0f, 0f, 1f);
            SpawnMuzzleFx(level, Position + cannonDirection * 30f);
        }

        private void FireShotgun(WeaponStyleId styleId, WeaponLevelDefinition level)
        {
            SpawnVolley(styleId, level, Math.Max(3, level.ProjectileCount), Math.Max(36f, level.SpreadDegrees), 0f, 0f, 0.95f);
            SpawnMuzzleFx(level, Position + cannonDirection * 28f);
        }

        private void FireBeam(WeaponStyleId styleId, WeaponLevelDefinition level)
        {
            if (styleId == ActiveStyle)
                EntityManager.ClearFriendlyBeams();

            int count = Math.Max(1, level.BeamCount);
            float spacing = Math.Max(0f, level.BeamSpacing);
            TryResolveChaseAimRay(out Vector3 baseCombatOrigin, out Vector3 combatAimPoint);
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
                Vector3 combatDirection = ResolveShotDirection3(direction, baseCombatOrigin, combatAimPoint, 0f, lateralFactor);
                Vector3 combatOrigin = CombatSpaceMath.IsDepthAwareViewActive
                    ? baseCombatOrigin + combatDirection * 4f + ResolveCombatSpreadOffset(direction, combatDirection, lateralOffset, lateralFactor)
                    : CombatPosition + combatDirection * 26f + ResolveCombatSpreadOffset(direction, combatDirection, lateralOffset, lateralFactor);
                WeaponStyleDefinition style = WeaponCatalog.GetStyle(styleId);
                EntityManager.Add(new BeamShot(
                    origin,
                    direction,
                    level.BeamLength,
                    level.BeamThickness,
                    level.BeamDurationSeconds,
                    level.BeamTickDamage,
                    true,
                    level.Impact,
                    style.PrimaryColor,
                    style.AccentColor,
                    combatOrigin,
                    combatDirection));
            }

            SpawnMuzzleFx(level, Position + cannonDirection * 30f);
        }

        private void FirePlasma(WeaponStyleId styleId, WeaponLevelDefinition level)
        {
            SpawnVolley(styleId, level, Math.Max(1, level.ProjectileCount), Math.Max(0f, level.SpreadDegrees), 0f, 0f, level.ProjectileScale);
            SpawnMuzzleFx(level, Position + cannonDirection * 28f);
        }

        private void FireMissiles(WeaponStyleId styleId, WeaponLevelDefinition level)
        {
            SpawnVolley(styleId, level, Math.Max(1, level.ProjectileCount), Math.Max(0f, level.SpreadDegrees), 0f, 2.2f, level.ProjectileScale);
            SpawnMuzzleFx(level, Position + cannonDirection * 28f);
        }

        private void FireRail(WeaponStyleId styleId, WeaponLevelDefinition level)
        {
            SpawnVolley(styleId, level, Math.Max(1, level.ProjectileCount), Math.Max(0f, level.SpreadDegrees), 0f, 0f, level.ProjectileScale);
            SpawnMuzzleFx(level, Position + cannonDirection * 34f);
        }

        private void FireArc(WeaponStyleId styleId, WeaponLevelDefinition level)
        {
            SpawnVolley(styleId, level, Math.Max(1, level.ProjectileCount), Math.Max(18f, level.SpreadDegrees), 0f, 0f, level.ProjectileScale);
            SpawnMuzzleFx(level, Position + cannonDirection * 26f);
        }

        private void FireBlade(WeaponStyleId styleId, WeaponLevelDefinition level)
        {
            SpawnVolley(styleId, level, Math.Max(2, level.ProjectileCount), Math.Max(32f, level.SpreadDegrees), 0f, 0f, level.ProjectileScale);
            SpawnMuzzleFx(level, Position + cannonDirection * 22f);
        }

        private void FireFortress(WeaponStyleId styleId, WeaponLevelDefinition level)
        {
            SpawnVolley(styleId, level, Math.Max(1, level.ProjectileCount), Math.Max(0f, level.SpreadDegrees), 0f, 0f, level.ProjectileScale);
            if (ResolveStyleLevel(styleId) >= 2)
                SpawnVolley(styleId, level, 2, 18f, -10f, 0f, level.ProjectileScale * 0.9f);
            SpawnMuzzleFx(level, Position + cannonDirection * 24f);
        }

        private void SpawnVolley(WeaponStyleId styleId, WeaponLevelDefinition level, int count, float spreadDegrees, float forwardOffset, float homingStrength, float scale)
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
                FirePrimary(styleId, level, direction, lateral, forwardOffset, homingStrength, scale, angle, lateralFactor);
            }
        }

        private void FirePrimary(WeaponStyleId styleId, WeaponLevelDefinition level, Vector2 direction, float lateralOffset, float forwardOffset, float homingStrength, float scale, float spreadAngle, float lateralFactor)
        {
            if (direction == Vector2.Zero)
                direction = Vector2.UnitX;
            else
                direction.Normalize();

            Vector2 perpendicular = new Vector2(-direction.Y, direction.X);
            Vector2 spawnPoint = Position + direction * (30f + forwardOffset) + perpendicular * lateralOffset;
            TryResolveChaseAimRay(out Vector3 baseCombatOrigin, out Vector3 combatAimPoint);
            Vector3 shotDirection = ResolveShotDirection3(direction, baseCombatOrigin, combatAimPoint, spreadAngle, lateralFactor);
            Vector3 combatSpawnPoint = CombatSpaceMath.IsDepthAwareViewActive
                ? baseCombatOrigin + shotDirection * (6f + forwardOffset) + ResolveCombatSpreadOffset(direction, shotDirection, lateralOffset, lateralFactor)
                : CombatPosition + shotDirection * (30f + forwardOffset) + ResolveCombatSpreadOffset(direction, shotDirection, lateralOffset, lateralFactor);
            int styleLevel = ResolveStyleLevel(styleId);
            ProceduralSpriteDefinition projectile = WeaponCatalog.CreateProjectileDefinition(styleId, styleLevel, true);
            float speedScale = PlayerStatus.RunProgress.GetProjectileSpeedScale(false);
            float effectiveSpeed = level.ProjectileSpeed * speedScale;
            EntityManager.Add(new Bullet(
                spawnPoint,
                direction * effectiveSpeed,
                true,
                level.ProjectileDamage + PlayerStatus.RunProgress.GetProjectileDamageBonus(styleId),
                level.Impact,
                projectile,
                level.Pierce ? Math.Max(1, level.PierceCount) : 0,
                level.ProjectileLifetimeSeconds,
                homingStrength + PlayerStatus.RunProgress.GetHomingBonus(false),
                level.ProjectileScale * scale,
                level.ProjectileBehavior,
                level.TrailFxStyle,
                level.ImpactFxStyle,
                level.ExplosionRadius + PlayerStatus.RunProgress.GetExplosionRadiusBonus(styleId),
                level.ChainCount + PlayerStatus.RunProgress.GetChainBonus(),
                level.HomingDelaySeconds,
                combatSpawnPoint,
                shotDirection * effectiveSpeed));
        }

        private Vector3 ResolveShotDirection3(Vector2 planarDirection, Vector3 combatOrigin, Vector3 combatTarget, float spreadAngle = 0f, float lateralFactor = 0f)
        {
            if (Game1.Instance != null && Game1.Instance.CurrentViewMode == ViewMode.Chase3D && CombatSpaceMath.IsDepthAwareViewActive)
            {
                Vector3 direction = combatTarget - combatOrigin;
                if (direction == Vector3.Zero)
                    direction = combatAimDirection;

                if (direction == Vector3.Zero)
                    direction = new Vector3(planarDirection.X, planarDirection.Y, 0f);

                if (direction == Vector3.Zero)
                    direction = Vector3.UnitX;

                direction.Normalize();

                direction = ResolveAimAssistedShotDirection(combatOrigin, direction);

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

        private Vector3 ResolveChaseCombatAim()
        {
            Vector3 resolvedAim = Late3DRenderer.ResolveCombatAimDirection(CombatPosition, chaseReticle);
            if (resolvedAim == Vector3.Zero)
                return Vector3.UnitX;

            resolvedAim.Normalize();
            return resolvedAim;
        }

        private bool TryResolveChaseAimRay(out Vector3 combatOrigin, out Vector3 combatTarget)
        {
            Vector3 fallbackDirection = combatAimDirection == Vector3.Zero ? Vector3.UnitX : Vector3.Normalize(combatAimDirection);
            combatOrigin = CombatPosition + fallbackDirection * 28f;
            combatTarget = combatOrigin + fallbackDirection * 1200f;

            if (Game1.Instance != null &&
                Game1.Instance.CurrentViewMode == ViewMode.Chase3D &&
                CombatSpaceMath.IsDepthAwareViewActive &&
                Late3DRenderer.TryResolvePlayerCombatAimRay(this, out Vector3 resolvedOrigin, out Vector3 resolvedTarget))
            {
                combatOrigin = resolvedOrigin;
                combatTarget = resolvedTarget;
                return true;
            }

            return false;
        }

        private Enemy FindChaseAimAssistTarget(Vector3 shotOrigin, Vector3 baseDirection)
        {
            if (baseDirection == Vector3.Zero)
                return null;

            baseDirection.Normalize();
            Enemy bestTarget = null;
            float bestScore = float.MaxValue;
            foreach (Enemy enemy in EntityManager.Enemies)
            {
                if (enemy == null || enemy.IsExpired)
                    continue;

                float forwardDelta = enemy.Travel - Travel;
                if (forwardDelta < -60f || forwardDelta > 1480f)
                    continue;

                if (!Late3DRenderer.TryProjectEntityToReticle(enemy, CombatPosition, chaseReticle, out Vector2 targetReticle))
                    continue;

                float reticleDistanceSquared = Vector2.DistanceSquared(targetReticle, chaseReticle);
                if (reticleDistanceSquared > 0.018f)
                    continue;

                Vector3 targetDirection = enemy.CombatPosition - shotOrigin;
                if (targetDirection == Vector3.Zero)
                    continue;

                targetDirection.Normalize();
                float aimAlignment = Vector3.Dot(baseDirection, targetDirection);
                if (aimAlignment < 0.985f)
                    continue;

                float score =
                    reticleDistanceSquared * 7.5f +
                    (1f - aimAlignment) * 14f +
                    Math.Max(0f, forwardDelta) * 0.00018f +
                    MathF.Abs(enemy.LateralDepth - shotOrigin.Z) * 0.0014f -
                    (enemy is BossEnemy ? 0.04f : 0f);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestTarget = enemy;
                }
            }

            return bestTarget;
        }

        private Vector3 ResolveAimAssistedShotDirection(Vector3 shotOrigin, Vector3 baseDirection)
        {
            if (Game1.Instance?.AimAssist3DMode != AimAssist3DMode.SoftLock)
                return baseDirection;

            Enemy assistTarget = FindChaseAimAssistTarget(shotOrigin, baseDirection);
            if (assistTarget == null)
                return baseDirection;

            Vector3 assistedDirection = assistTarget.CombatPosition - shotOrigin;
            if (assistedDirection == Vector3.Zero)
                return baseDirection;

            assistedDirection.Normalize();
            Vector3 blended = Vector3.Lerp(baseDirection, assistedDirection, 0.45f);
            if (blended == Vector3.Zero)
                return assistedDirection;

            blended.Normalize();
            return blended;
        }

        internal void DrawPresentationModules(SpriteBatch spriteBatch)
        {
            DrawAuxiliaryModules(spriteBatch, false);
        }

        private void UpdateDrones(WeaponStyleId styleId, bool focusHeld)
        {
            WeaponLevelDefinition level = ResolveWeaponLevel(styleId);
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
                Enemy nearest = EntityManager.FindNearestEnemy(combatSpawn, 420f);
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
                    direction * ((level.ProjectileSpeed + 70f) * PlayerStatus.RunProgress.GetProjectileSpeedScale(focusHeld)),
                    true,
                    Math.Max(1, level.ProjectileDamage + PlayerStatus.RunProgress.GetProjectileDamageBonus(styleId)),
                    level.Impact,
                    WeaponCatalog.CreateProjectileDefinition(WeaponStyleId.Drone, ResolveStyleLevel(styleId), true),
                    0,
                    level.ProjectileLifetimeSeconds,
                    nearest == null ? 0f : 0.6f + PlayerStatus.RunProgress.GetHomingBonus(focusHeld),
                    0.9f,
                    ProjectileBehavior.DroneBolt,
                    TrailFxStyle.Streak,
                    ImpactFxStyle.Drone,
                    0f,
                    PlayerStatus.RunProgress.GetChainBonus(),
                    0.08f,
                    combatSpawn,
                    combatDirection * ((level.ProjectileSpeed + 70f) * PlayerStatus.RunProgress.GetProjectileSpeedScale(focusHeld))));
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

        private void ApplyChaseEntryRecenter(float deltaSeconds, ViewMode currentViewMode)
        {
            if (currentViewMode != ViewMode.Chase3D || chaseEntryRecenterTimer <= 0f)
                return;

            chaseEntryRecenterTimer = Math.Max(0f, chaseEntryRecenterTimer - deltaSeconds);
            Vector3 toTarget = chaseEntryRecenterTarget - CombatPosition;
            CombatPosition += toTarget * MathHelper.Clamp(deltaSeconds * 4.8f, 0f, 1f);
            if (MathF.Abs(toTarget.X) < 1.5f && MathF.Abs(toTarget.Y) < 1.5f && MathF.Abs(toTarget.Z) < 1.5f)
                chaseEntryRecenterTimer = 0f;
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
            float preservedHullRatio = authoritativeHullRatio > 0f ? authoritativeHullRatio : 1f;
            WeaponStyleId style = ActiveStyle;
            int level = Math.Max(0, ActiveWeaponLevel);
            sprite = new ProceduralSpriteInstance(Game1.Instance.GraphicsDevice, WeaponCatalog.CreateHullDefinition(style, level));
            cannonSprite = new ProceduralSpriteInstance(Game1.Instance.GraphicsDevice, WeaponCatalog.CreateCannonDefinition(style, level));
            damageMask = WeaponCatalog.CreatePlayerDamageMask(style, level);
            RenderScale = 1f;
            ApplyHullIntegrityRatio(preservedHullRatio);
        }

        private int ResolveStyleLevel(WeaponStyleId styleId)
        {
            return Math.Max(0, PlayerStatus.RunProgress.Weapons.GetLevel(styleId));
        }

        private int ResolveStyleRank(WeaponStyleId styleId)
        {
            return Math.Max(0, PlayerStatus.RunProgress.Weapons.GetRank(styleId));
        }

        private WeaponLevelDefinition ResolveWeaponLevel(WeaponStyleId styleId)
        {
            WeaponLevelDefinition baseLevel = WeaponCatalog.GetLevel(styleId, ResolveStyleLevel(styleId));
            int rank = ResolveStyleRank(styleId);
            if (rank <= 0)
                baseLevel = CloneLevel(baseLevel);
            else
            {
                baseLevel = CloneLevel(baseLevel);
                baseLevel.FireIntervalSeconds *= MathF.Max(0.62f, 1f - rank * 0.022f);
                baseLevel.ProjectileSpeed *= 1f + MathF.Min(0.55f, rank * 0.03f);
                baseLevel.ProjectileDamage += rank / 3;
                baseLevel.ProjectileLifetimeSeconds *= 1f + MathF.Min(0.4f, rank * 0.018f);
                baseLevel.ExplosionRadius += (rank / 4) * 4f;
                baseLevel.ChainCount += rank / 5;

                switch (styleId)
                {
                    case WeaponStyleId.Pulse:
                        baseLevel.PierceCount += rank / 4;
                        break;
                    case WeaponStyleId.Spread:
                        baseLevel.SpreadDegrees += rank % 5 == 4 ? 6f : 0f;
                        break;
                    case WeaponStyleId.Laser:
                        baseLevel.BeamTickDamage += rank / 4;
                        baseLevel.BeamThickness += rank % 4 == 3 ? 2f : 0f;
                        baseLevel.BeamLength += rank * 10f;
                        break;
                    case WeaponStyleId.Plasma:
                    case WeaponStyleId.Missile:
                    case WeaponStyleId.Fortress:
                        baseLevel.ExplosionRadius += rank / 3 * 2f;
                        break;
                    case WeaponStyleId.Rail:
                        baseLevel.PierceCount += 1 + rank / 4;
                        break;
                    case WeaponStyleId.Arc:
                        baseLevel.ChainCount += 1 + rank / 4;
                        break;
                    case WeaponStyleId.Blade:
                        baseLevel.ProjectileCount += rank % 4 == 3 ? 1 : 0;
                        break;
                    case WeaponStyleId.Drone:
                        baseLevel.DroneCount += rank % 4 == 3 ? 1 : 0;
                        break;
                }
            }

            if (styleId == WeaponStyleId.Pulse && PlayerStatus.RunProgress.HasEvolution(EvolutionId.SingularityRail))
            {
                baseLevel.FireMode = FireMode.RailBurst;
                baseLevel.ProjectileBehavior = ProjectileBehavior.RailSlug;
                baseLevel.MuzzleFxStyle = MuzzleFxStyle.Rail;
                baseLevel.TrailFxStyle = TrailFxStyle.Neon;
                baseLevel.ImpactFxStyle = ImpactFxStyle.Rail;
                baseLevel.Pierce = true;
                baseLevel.PierceCount = Math.Max(baseLevel.PierceCount, 4);
                baseLevel.ProjectileCount = Math.Max(baseLevel.ProjectileCount, 2);
                baseLevel.ProjectileSpeed *= 1.18f;
            }

            if (styleId == WeaponStyleId.Missile && PlayerStatus.RunProgress.HasEvolution(EvolutionId.CataclysmRack))
            {
                baseLevel.ProjectileCount += 2;
                baseLevel.ExplosionRadius += 18f;
                baseLevel.HomingDelaySeconds = 0f;
            }

            if (styleId == WeaponStyleId.Drone && PlayerStatus.RunProgress.HasEvolution(EvolutionId.EchoHive))
            {
                baseLevel.DroneCount += 2;
                baseLevel.DroneIntervalSeconds = MathF.Max(0.2f, baseLevel.DroneIntervalSeconds * 0.74f);
                baseLevel.ChainCount += 1;
            }

            return baseLevel;
        }

        private static WeaponLevelDefinition CloneLevel(WeaponLevelDefinition baseLevel)
        {
            return new WeaponLevelDefinition
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
                DroneCount = baseLevel.DroneCount + PlayerStatus.RunProgress.GetDroneBonus(),
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
        }

        private void RestoreAfterRespawn()
        {
            authoritativeHullRatio = 1f;
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
            chaseEntryRecenterTarget = Vector3.Zero;
            chaseEntryRecenterTimer = 0f;
            color = Color.White;
        }

        private void ApplyHullIntegrityRatio(float ratio)
        {
            if (sprite?.Mask == null || sprite.Mask.InitialOccupiedCount <= 0)
            {
                authoritativeHullRatio = 1f;
                return;
            }

            float clampedRatio = MathHelper.Clamp(ratio, 0.04f, 1f);
            int targetOccupied = Math.Max(1, (int)MathF.Round(sprite.Mask.InitialOccupiedCount * clampedRatio));
            TrimHullCells(targetOccupied, preserveCore: true);
            TrimHullCells(targetOccupied, preserveCore: false);
            SyncAuthoritativeHullRatio();
        }

        private void TrimHullCells(int targetOccupied, bool preserveCore)
        {
            if (sprite?.Mask == null)
                return;

            for (int x = 0; x < sprite.Mask.Width && sprite.Mask.OccupiedCount > targetOccupied; x++)
            {
                for (int yStep = 0; yStep < sprite.Mask.Height && sprite.Mask.OccupiedCount > targetOccupied; yStep++)
                {
                    int y = yStep % 2 == 0
                        ? yStep / 2
                        : sprite.Mask.Height - 1 - yStep / 2;

                    if (!sprite.Mask.IsOccupied(x, y))
                        continue;

                    if (preserveCore && sprite.Mask.IsCore(x, y))
                        continue;

                    sprite.Mask.RemoveCell(x, y);
                }
            }
        }

        private void SyncAuthoritativeHullRatio()
        {
            if (sprite?.Mask == null || sprite.Mask.InitialOccupiedCount <= 0)
            {
                authoritativeHullRatio = 1f;
                return;
            }

            authoritativeHullRatio = MathHelper.Clamp(sprite.Mask.OccupiedCount / (float)sprite.Mask.InitialOccupiedCount, 0f, 1f);
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
