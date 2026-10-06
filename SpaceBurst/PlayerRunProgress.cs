using Microsoft.Xna.Framework;
using SpaceBurst.RuntimeData;
using System;

namespace SpaceBurst
{
    sealed class PlayerRunProgress
    {
        public WeaponInventoryState Weapons { get; } = new WeaponInventoryState();
        public PowerupDropState Powerups { get; } = new PowerupDropState();

        public GameDifficulty Difficulty { get; private set; } = GameDifficulty.Easy;
        public int StartingLives { get; private set; } = 3;
        public int ShipsPerLife { get; private set; } = 2;
        public bool MedalEligible { get; private set; } = true;
        public int NonWeaponUpgradeCount { get; private set; }
        public float MoveSpeedMultiplier { get; private set; } = 1f;
        public float RewindEfficiency { get; private set; }
        public float DropBonusChance { get; private set; }
        public float RunXp { get; private set; }
        public int RunLevel { get; private set; } = 1;
        public int Scrap { get; private set; }
        public int PendingLevelUps { get; private set; }
        public int PassiveSlots { get; private set; } = 1;
        public int KillChainTier { get; private set; }
        public float FocusFireSeconds { get; private set; }

        public int StoredUpgradeCharges
        {
            get { return Weapons.StoredUpgradeCharges; }
        }

        public bool HasPendingLevelUp
        {
            get { return PendingLevelUps > 0; }
        }

        public WeaponStyleId PriorityChargeStyle
        {
            get { return Weapons.GetPriorityChargeStyle(); }
        }

        public float PowerBudget
        {
            get
            {
                return Weapons.HighestRank
                    + Math.Max(0, Weapons.UnlockedStyleCount - 1) * 1.2f
                    + NonWeaponUpgradeCount * 0.75f
                    + Weapons.SupportWeapons.Count * 0.4f
                    + Weapons.PassiveReactors.Count * 0.5f
                    + Weapons.Evolutions.Count * 0.9f
                    + (RunLevel - 1) * 0.18f;
            }
        }

        public void BeginCampaign(StageDefinition stage, GameDifficulty difficulty)
        {
            Difficulty = difficulty;
            StartingLives = ResolveStartingLives(stage);
            ShipsPerLife = ResolveShipsPerLife(stage);
            Weapons.Reset();
            Powerups.Reset();
            MedalEligible = true;
            NonWeaponUpgradeCount = 0;
            MoveSpeedMultiplier = 1f;
            RewindEfficiency = 0f;
            DropBonusChance = 0f;
            RunXp = 0f;
            RunLevel = 1;
            Scrap = 0;
            PendingLevelUps = 0;
            PassiveSlots = 1;
            KillChainTier = 0;
            FocusFireSeconds = 0f;
        }

        public void ApplyStageDefaults(StageDefinition stage)
        {
            if (stage == null)
                return;

            StartingLives = ResolveStartingLives(stage);
            // Reserve upgrades belong to the run and survive stage changes/retries.
            ShipsPerLife = Math.Max(ShipsPerLife, ResolveShipsPerLife(stage));
        }

        public void MarkMedalIneligible()
        {
            MedalEligible = false;
        }

        public int GetStoredCharge(WeaponStyleId style)
        {
            return Weapons.GetStoredCharge(style);
        }

        public void AddUpgradeCharge(WeaponStyleId style, int count = 1)
        {
            Weapons.AddUpgradeCharge(style, count);
        }

        public bool TryConsumeUpgradeCharge(WeaponStyleId style)
        {
            return Weapons.ConsumeUpgradeCharge(style);
        }

        public WeaponUpgradeOutcome ApplyWeaponUpgrade()
        {
            return Weapons.ApplyWeaponUpgrade();
        }

        public WeaponUpgradeOutcome ApplyWeaponUpgrade(WeaponStyleId style, bool activateStyle = false)
        {
            return Weapons.ApplyWeaponUpgrade(style, activateStyle);
        }

        public bool TryEquipSupportWeapon(WeaponStyleId style)
        {
            if (!Weapons.TryEquipSupportWeapon(style))
                return false;

            NonWeaponUpgradeCount++;
            return true;
        }

        public bool TryEquipPassive(PassiveReactorId passive)
        {
            if (!Weapons.TryEquipPassive(passive, PassiveSlots))
                return false;

            switch (passive)
            {
                case PassiveReactorId.Overclock:
                    MoveSpeedMultiplier = MathF.Min(1.4f, MoveSpeedMultiplier + 0.04f);
                    break;
                case PassiveReactorId.MagnetCore:
                    DropBonusChance = MathF.Min(0.28f, DropBonusChance + 0.02f);
                    break;
                case PassiveReactorId.ArmorPlating:
                    ShipsPerLife = Math.Min(7, ShipsPerLife + 1);
                    break;
                case PassiveReactorId.TimeBattery:
                    RewindEfficiency = MathF.Min(0.65f, RewindEfficiency + 0.16f);
                    break;
                case PassiveReactorId.SalvageNode:
                    DropBonusChance = MathF.Min(0.3f, DropBonusChance + 0.04f);
                    break;
                case PassiveReactorId.ChainReactor:
                    MoveSpeedMultiplier = MathF.Min(1.5f, MoveSpeedMultiplier + 0.05f);
                    break;
            }

            NonWeaponUpgradeCount++;
            return true;
        }

        public bool TryAddEvolution(EvolutionId evolution)
        {
            return Weapons.TryAddEvolution(evolution);
        }

        public bool HasPassive(PassiveReactorId passive)
        {
            return Weapons.HasPassiveReactor(passive);
        }

        public bool HasEvolution(EvolutionId evolution)
        {
            return Weapons.HasEvolution(evolution);
        }

        public void AddXp(float amount)
        {
            if (amount <= 0f)
                return;

            RunXp += amount;
            while (RunXp >= GetXpRequiredForNextLevel())
            {
                RunXp -= GetXpRequiredForNextLevel();
                RunLevel++;
                PendingLevelUps++;
                if (RunLevel == 4 || RunLevel == 7)
                    PassiveSlots = Math.Min(3, PassiveSlots + 1);
            }
        }

        public bool TryConsumeLevelUp()
        {
            if (PendingLevelUps <= 0)
                return false;

            PendingLevelUps--;
            return true;
        }

        public float GetXpRatio()
        {
            float next = GetXpRequiredForNextLevel();
            if (next <= 0f)
                return 1f;

            return MathHelper.Clamp(RunXp / next, 0f, 1f);
        }

        public float GetXpRequiredForNextLevel()
        {
            return 5f + (RunLevel - 1) * 3f + Math.Max(0, RunLevel - 5) * 2f;
        }

        public void AddScrap(int amount)
        {
            if (amount > 0)
                Scrap += amount;
        }

        public void UpdateFocusFire(bool focusHeld, float deltaSeconds)
        {
            if (focusHeld)
                FocusFireSeconds = MathF.Min(4f, FocusFireSeconds + deltaSeconds);
            else
                FocusFireSeconds = MathF.Max(0f, FocusFireSeconds - deltaSeconds * 2.2f);
        }

        public float GetFocusFireIntensity()
        {
            return MathHelper.Clamp(FocusFireSeconds / 1.2f, 0f, 1f);
        }

        public float GetFireIntervalScale(WeaponStyleId style, bool focusHeld)
        {
            float scale = 1f;
            if (focusHeld)
                scale *= MathHelper.Lerp(0.9f, 0.76f, GetFocusFireIntensity());
            if (HasPassive(PassiveReactorId.Overclock))
                scale *= 0.9f;

            WeaponEvolutionDefinition evolution = WeaponProgressionCatalog.GetEvolutionForStyle(style);
            if (evolution != null && HasEvolution(evolution.Id))
            {
                scale *= evolution.Id switch
                {
                    EvolutionId.TempestCircuit => 0.82f,
                    EvolutionId.VoidLance => 0.86f,
                    EvolutionId.EchoHive => 0.86f,
                    EvolutionId.NovaFan => 0.88f,
                    EvolutionId.AegisStorm => 0.88f,
                    EvolutionId.CitadelNova => 0.88f,
                    EvolutionId.PrismLance => 0.9f,
                    EvolutionId.ChronoNova => 0.9f,
                    EvolutionId.CataclysmRack => 0.9f,
                    _ => 0.94f,
                };
            }

            return MathF.Max(0.34f, scale);
        }

        public float GetProjectileSpeedScale(WeaponStyleId style, bool focusHeld)
        {
            float scale = 1f;
            float focusIntensity = GetFocusFireIntensity();
            if (focusHeld || focusIntensity > 0f)
                scale *= 1f + focusIntensity * 0.18f;
            if (HasPassive(PassiveReactorId.ChainReactor))
                scale *= 1.06f;
            if (style == WeaponStyleId.Rail && HasEvolution(EvolutionId.VoidLance))
                scale *= 1.2f;
            if (style == WeaponStyleId.Pulse && HasEvolution(EvolutionId.SingularityRail))
                scale *= 1.1f;
            return scale;
        }

        public float GetHomingBonus(WeaponStyleId style, bool focusHeld)
        {
            float bonus = 0f;
            float focusIntensity = GetFocusFireIntensity();
            if (focusHeld || focusIntensity > 0f)
                bonus += 0.25f + focusIntensity * 0.25f;
            if (HasPassive(PassiveReactorId.ChainReactor))
                bonus += 0.12f;
            if (style == WeaponStyleId.Missile && HasEvolution(EvolutionId.CataclysmRack))
                bonus += 0.3f;
            if (style == WeaponStyleId.Drone && HasEvolution(EvolutionId.EchoHive))
                bonus += 0.18f;
            return bonus;
        }

        public float GetMagnetStrength()
        {
            float baseStrength = 0f;
            if (HasPassive(PassiveReactorId.MagnetCore))
                baseStrength += 160f;
            if (HasPassive(PassiveReactorId.TimeBattery))
                baseStrength += 60f;
            return baseStrength;
        }

        public int GetChainBonus(WeaponStyleId style)
        {
            int bonus = 0;
            if (HasPassive(PassiveReactorId.ChainReactor))
                bonus++;
            if (style == WeaponStyleId.Drone && HasEvolution(EvolutionId.EchoHive))
                bonus++;
            if (style == WeaponStyleId.Arc && HasEvolution(EvolutionId.TempestCircuit))
                bonus += 4;
            return bonus;
        }

        public int GetProjectileDamageBonus(WeaponStyleId style)
        {
            int bonus = 0;
            if (HasPassive(PassiveReactorId.Overclock))
                bonus++;
            if (style == WeaponStyleId.Missile && HasEvolution(EvolutionId.CataclysmRack))
                bonus += 2;
            if (style == WeaponStyleId.Pulse && HasEvolution(EvolutionId.SingularityRail))
                bonus += 2;
            if (style == WeaponStyleId.Drone && HasEvolution(EvolutionId.EchoHive))
                bonus += 1;
            if (style == WeaponStyleId.Spread && HasEvolution(EvolutionId.NovaFan))
                bonus += 1;
            if (style == WeaponStyleId.Laser && HasEvolution(EvolutionId.PrismLance))
                bonus += 1;
            if (style == WeaponStyleId.Plasma && HasEvolution(EvolutionId.ChronoNova))
                bonus += 2;
            if (style == WeaponStyleId.Rail && HasEvolution(EvolutionId.VoidLance))
                bonus += 2;
            if (style == WeaponStyleId.Arc && HasEvolution(EvolutionId.TempestCircuit))
                bonus += 1;
            if (style == WeaponStyleId.Blade && HasEvolution(EvolutionId.AegisStorm))
                bonus += 1;
            if (style == WeaponStyleId.Fortress && HasEvolution(EvolutionId.CitadelNova))
                bonus += 2;
            return bonus;
        }

        public float GetExplosionRadiusBonus(WeaponStyleId style)
        {
            if (style == WeaponStyleId.Missile && HasEvolution(EvolutionId.CataclysmRack))
                return 24f;
            if (style == WeaponStyleId.Plasma && HasEvolution(EvolutionId.ChronoNova))
                return 26f;
            if (style == WeaponStyleId.Fortress && HasEvolution(EvolutionId.CitadelNova))
                return 22f;

            return 0f;
        }

        public void UpdateKillChain(int multiplier)
        {
            KillChainTier = multiplier switch
            {
                >= 12 => 3,
                >= 8 => 2,
                >= 4 => 1,
                _ => 0,
            };
        }

        public void ApplyMobilityUpgrade()
        {
            MoveSpeedMultiplier = MathF.Min(1.8f, MoveSpeedMultiplier + 0.08f);
            NonWeaponUpgradeCount++;
        }

        public void ApplyEmergencyReserveUpgrade()
        {
            ShipsPerLife = Math.Min(6, ShipsPerLife + 1);
            NonWeaponUpgradeCount++;
        }

        public void ApplyRewindUpgrade()
        {
            RewindEfficiency = MathF.Min(0.6f, RewindEfficiency + 0.12f);
            NonWeaponUpgradeCount++;
        }

        public void ApplyEconomyUpgrade()
        {
            DropBonusChance = MathF.Min(0.24f, DropBonusChance + 0.03f);
            NonWeaponUpgradeCount++;
        }

        public float GetWavePressure(int stageNumber)
        {
            DifficultyProfile profile = DifficultyTuning.GetProfile(Difficulty);
            float pressure = DifficultyTuning.GetStagePressure(stageNumber) * profile.StagePressureScale
                + DifficultyTuning.GetPowerPressure(PowerBudget) * profile.PowerPressureScale;
            return MathHelper.Clamp(MathF.Max(profile.WavePressureFloor, pressure), 0f, 1.75f);
        }

        public float GetBossPressure(int stageNumber)
        {
            DifficultyProfile profile = DifficultyTuning.GetProfile(Difficulty);
            float pressure = DifficultyTuning.GetStagePressure(stageNumber) * (profile.StagePressureScale + 0.1f)
                + DifficultyTuning.GetPowerPressure(PowerBudget) * profile.BossPowerPressureScale;
            float floor = MathF.Max(profile.BossPressureFloor, profile.EarlyBossPressureFloor);

            return MathHelper.Clamp(MathF.Max(floor, pressure), 0f, 2f);
        }

        public float GetEnemyDamageMultiplier(int stageNumber, bool boss)
        {
            DifficultyProfile profile = DifficultyTuning.GetProfile(Difficulty);
            float pressure = boss ? GetBossPressure(stageNumber) : GetWavePressure(stageNumber);
            float baseMultiplier = boss ? profile.BossDamageMultiplier : profile.WaveDamageMultiplier;
            float bonus = boss ? 0.6f : 0.38f;
            return baseMultiplier * (1f + pressure * bonus);
        }

        public float GetEnemyDurabilityMultiplier(int stageNumber, bool boss)
        {
            DifficultyProfile profile = DifficultyTuning.GetProfile(Difficulty);
            float pressure = boss ? GetBossPressure(stageNumber) : GetWavePressure(stageNumber);
            float baseMultiplier = boss ? profile.BossDurabilityMultiplier : profile.WaveDurabilityMultiplier;
            float bonus = boss ? 0.82f : 0.34f;
            return baseMultiplier * (1f + pressure * bonus);
        }

        public float GetEnemyFireIntervalScale(int stageNumber, bool boss)
        {
            DifficultyProfile profile = DifficultyTuning.GetProfile(Difficulty);
            float pressure = boss ? GetBossPressure(stageNumber) : GetWavePressure(stageNumber);
            float baseScale = boss ? profile.BossFireIntervalScale : profile.WaveFireIntervalScale;
            float reduction = boss ? 0.16f : 0.1f;
            return MathF.Max(0.34f, baseScale - pressure * reduction);
        }

        public float GetDropChanceMultiplier(int stageNumber, bool boss)
        {
            DifficultyProfile profile = DifficultyTuning.GetProfile(Difficulty);
            float pressure = boss ? GetBossPressure(stageNumber) : GetWavePressure(stageNumber);
            return MathF.Max(0.2f, profile.DropChanceMultiplier * (1f - pressure * 0.12f));
        }

        public float GetDropWeightMultiplier(int stageNumber, bool boss)
        {
            DifficultyProfile profile = DifficultyTuning.GetProfile(Difficulty);
            float pressure = boss ? GetBossPressure(stageNumber) : GetWavePressure(stageNumber);
            return MathF.Max(0.2f, profile.DropWeightMultiplier * (1f - pressure * 0.08f));
        }

        public bool IsOneHitKillEnabled()
        {
            return DifficultyTuning.GetProfile(Difficulty).OneHitKill;
        }

        public float GetBossProjectileSpeedMultiplier(int stageNumber)
        {
            DifficultyProfile profile = DifficultyTuning.GetProfile(Difficulty);
            float pressure = GetBossPressure(stageNumber);
            float pressureBlend = MathHelper.Clamp(pressure * 0.8f, 0f, 1f);
            return MathHelper.Lerp(1f, profile.BossProjectileSpeedMultiplier, pressureBlend);
        }

        public int GetBossExtraFanShots(int stageNumber)
        {
            DifficultyProfile profile = DifficultyTuning.GetProfile(Difficulty);
            if (profile.BossExtraFanShots <= 0)
                return 0;

            return GetBossPressure(stageNumber) >= 0.75f ? profile.BossExtraFanShots : Math.Max(0, profile.BossExtraFanShots - 2);
        }

        public int GetBossSupportCountBonus(int stageNumber)
        {
            DifficultyProfile profile = DifficultyTuning.GetProfile(Difficulty);
            if (profile.BossSupportCountBonus <= 0)
                return 0;

            return GetBossPressure(stageNumber) >= 0.7f ? profile.BossSupportCountBonus : Math.Max(0, profile.BossSupportCountBonus - 1);
        }

        public float GetBossMinimumFireCooldown(int stageNumber)
        {
            DifficultyProfile profile = DifficultyTuning.GetProfile(Difficulty);
            float pressure = GetBossPressure(stageNumber);
            float relaxed = MathF.Max(0.08f, profile.BossMinimumFireCooldown + 0.03f);
            return MathHelper.Lerp(relaxed, profile.BossMinimumFireCooldown, MathHelper.Clamp(pressure, 0f, 1f));
        }

        public PlayerRunProgressSnapshotData CaptureSnapshot()
        {
            return new PlayerRunProgressSnapshotData
            {
                Difficulty = Difficulty,
                StartingLives = StartingLives,
                ShipsPerLife = ShipsPerLife,
                MedalEligible = MedalEligible,
                NonWeaponUpgradeCount = NonWeaponUpgradeCount,
                MoveSpeedMultiplier = MoveSpeedMultiplier,
                RewindEfficiency = RewindEfficiency,
                DropBonusChance = DropBonusChance,
                RunXp = RunXp,
                RunLevel = RunLevel,
                Scrap = Scrap,
                PendingLevelUps = PendingLevelUps,
                PassiveSlots = PassiveSlots,
                KillChainTier = KillChainTier,
                FocusFireSeconds = FocusFireSeconds,
                Weapons = Weapons.CaptureSnapshot(),
                Powerups = Powerups.CaptureSnapshot(),
            };
        }

        public void RestoreSnapshot(PlayerRunProgressSnapshotData snapshot)
        {
            if (snapshot == null)
                return;

            Difficulty = Enum.IsDefined(typeof(GameDifficulty), snapshot.Difficulty) ? snapshot.Difficulty : GameDifficulty.Easy;
            StartingLives = Math.Clamp(snapshot.StartingLives > 0 ? snapshot.StartingLives : 3, 1, 9);
            ShipsPerLife = Math.Clamp(snapshot.ShipsPerLife > 0 ? snapshot.ShipsPerLife : 2, 1, 9);
            MedalEligible = snapshot.MedalEligible;
            NonWeaponUpgradeCount = Math.Clamp(snapshot.NonWeaponUpgradeCount, 0, 999);
            MoveSpeedMultiplier = float.IsFinite(snapshot.MoveSpeedMultiplier) && snapshot.MoveSpeedMultiplier > 0f
                ? Math.Clamp(snapshot.MoveSpeedMultiplier, 0.5f, 1.8f)
                : 1f;
            RewindEfficiency = ClampFinite(snapshot.RewindEfficiency, 0f, 0.65f, 0f);
            DropBonusChance = ClampFinite(snapshot.DropBonusChance, 0f, 0.3f, 0f);
            RunXp = ClampFinite(snapshot.RunXp, 0f, 100000f, 0f);
            RunLevel = Math.Clamp(snapshot.RunLevel, 1, 999);
            Scrap = Math.Clamp(snapshot.Scrap, 0, 999999);
            PendingLevelUps = Math.Clamp(snapshot.PendingLevelUps, 0, 999);
            PassiveSlots = Math.Clamp(snapshot.PassiveSlots, 1, 3);
            KillChainTier = Math.Clamp(snapshot.KillChainTier, 0, 3);
            FocusFireSeconds = ClampFinite(snapshot.FocusFireSeconds, 0f, 4f, 0f);
            Weapons.RestoreSnapshot(snapshot.Weapons);
            Powerups.RestoreSnapshot(snapshot.Powerups);
        }

        private static float ClampFinite(float value, float minimum, float maximum, float fallback)
        {
            return float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
        }

        private int ResolveStartingLives(StageDefinition stage)
        {
            DifficultyProfile profile = DifficultyTuning.GetProfile(Difficulty);
            int baseLives = stage?.StartingLives > 0 ? stage.StartingLives : 3;
            return Math.Max(1, baseLives + profile.LivesDelta);
        }

        private int ResolveShipsPerLife(StageDefinition stage)
        {
            DifficultyProfile profile = DifficultyTuning.GetProfile(Difficulty);
            int baseShips = stage?.ShipsPerLife > 0 ? stage.ShipsPerLife : 2;
            return Math.Max(1, baseShips + profile.ShipsDelta);
        }
    }
}
