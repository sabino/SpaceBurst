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

        public int StoredUpgradeCharges
        {
            get { return Weapons.StoredUpgradeCharges; }
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
                    + NonWeaponUpgradeCount * 0.75f;
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
        }

        public void ApplyStageDefaults(StageDefinition stage)
        {
            if (stage == null)
                return;

            StartingLives = ResolveStartingLives(stage);
            ShipsPerLife = ResolveShipsPerLife(stage);
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
            float floor = profile.BossPressureFloor;
            if (stageNumber <= 10)
                floor = MathF.Max(floor, profile.EarlyBossPressureFloor);

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
                Weapons = Weapons.CaptureSnapshot(),
                Powerups = Powerups.CaptureSnapshot(),
            };
        }

        public void RestoreSnapshot(PlayerRunProgressSnapshotData snapshot)
        {
            if (snapshot == null)
                return;

            Difficulty = snapshot.Difficulty;
            StartingLives = snapshot.StartingLives > 0 ? snapshot.StartingLives : 3;
            ShipsPerLife = snapshot.ShipsPerLife > 0 ? snapshot.ShipsPerLife : 2;
            MedalEligible = snapshot.MedalEligible;
            NonWeaponUpgradeCount = Math.Max(0, snapshot.NonWeaponUpgradeCount);
            MoveSpeedMultiplier = snapshot.MoveSpeedMultiplier > 0f ? snapshot.MoveSpeedMultiplier : 1f;
            RewindEfficiency = MathF.Max(0f, snapshot.RewindEfficiency);
            DropBonusChance = MathF.Max(0f, snapshot.DropBonusChance);
            Weapons.RestoreSnapshot(snapshot.Weapons);
            Powerups.RestoreSnapshot(snapshot.Powerups);
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
