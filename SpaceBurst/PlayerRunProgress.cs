using SpaceBurst.RuntimeData;
using System;

namespace SpaceBurst
{
    sealed class PlayerRunProgress
    {
        public WeaponInventoryState Weapons { get; } = new WeaponInventoryState();
        public PowerupDropState Powerups { get; } = new PowerupDropState();

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

        public void BeginCampaign(StageDefinition stage)
        {
            StartingLives = stage?.StartingLives > 0 ? stage.StartingLives : 3;
            ShipsPerLife = stage?.ShipsPerLife > 0 ? stage.ShipsPerLife : 2;
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

            StartingLives = stage.StartingLives > 0 ? stage.StartingLives : StartingLives;
            ShipsPerLife = stage.ShipsPerLife > 0 ? stage.ShipsPerLife : ShipsPerLife;
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

        public PlayerRunProgressSnapshotData CaptureSnapshot()
        {
            return new PlayerRunProgressSnapshotData
            {
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
    }
}
