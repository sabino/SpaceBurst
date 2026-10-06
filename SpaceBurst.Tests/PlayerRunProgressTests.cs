using SpaceBurst.RuntimeData;
using Xunit;

namespace SpaceBurst.Tests
{
    public sealed class PlayerRunProgressTests
    {
        [Fact]
        public void UpgradeOrder_NeverReducesExistingBenefits()
        {
            var mobility = new PlayerRunProgress();
            for (int i = 0; i < 12; i++) mobility.ApplyMobilityUpgrade();
            float speed = mobility.MoveSpeedMultiplier;
            Assert.True(mobility.TryEquipPassive(PassiveReactorId.Overclock));
            Assert.Equal(speed, mobility.MoveSpeedMultiplier);
            mobility.AddXp(90);
            Assert.True(mobility.TryEquipPassive(PassiveReactorId.ChainReactor));
            Assert.Equal(speed, mobility.MoveSpeedMultiplier);

            var rewind = new PlayerRunProgress();
            for (int i = 0; i < 6; i++) rewind.ApplyRewindUpgrade();
            Assert.True(rewind.TryEquipPassive(PassiveReactorId.TimeBattery));
            float efficiency = rewind.RewindEfficiency;
            rewind.ApplyRewindUpgrade();
            Assert.Equal(0.65f, efficiency);
            Assert.Equal(efficiency, rewind.RewindEfficiency);

            var salvage = new PlayerRunProgress();
            for (int i = 0; i < 10; i++) salvage.ApplyEconomyUpgrade();
            Assert.True(salvage.TryEquipPassive(PassiveReactorId.SalvageNode));
            float dropChance = salvage.DropBonusChance;
            salvage.ApplyEconomyUpgrade();
            Assert.Equal(dropChance, salvage.DropBonusChance);
            salvage.AddXp(90);
            Assert.True(salvage.TryEquipPassive(PassiveReactorId.MagnetCore));
            Assert.Equal(dropChance, salvage.DropBonusChance);

            var armor = new PlayerRunProgress();
            for (int i = 0; i < 6; i++) armor.ApplyEmergencyReserveUpgrade();
            Assert.True(armor.TryEquipPassive(PassiveReactorId.ArmorPlating));
            armor.ApplyEmergencyReserveUpgrade();
            Assert.Equal(7, armor.ShipsPerLife);
        }

        [Fact]
        public void ScrapRewards_CrossThresholdsAndPreservePartialProgressThroughSave()
        {
            var progress = new PlayerRunProgress();
            Assert.Equal(0, progress.AddScrap(4));
            var restored = new PlayerRunProgress();
            restored.RestoreSnapshot(progress.CaptureSnapshot());
            Assert.Equal(4, restored.ScrapTowardNextShip);
            Assert.Equal(1, restored.AddScrap(1));
            Assert.Equal(2, restored.AddScrap(11));
            Assert.Equal(1, restored.ScrapTowardNextShip);
            Assert.Equal(0, restored.AddScrap(0));
            Assert.Equal(0, restored.AddScrap(-5));
            var historical = new PlayerRunProgress();
            historical.RestoreSnapshot(new PlayerRunProgressSnapshotData { Scrap = 51 });
            Assert.Equal(0, historical.AddScrap(3));
            Assert.Equal(1, historical.AddScrap(1));
            Assert.Equal(199988, historical.AddScrap(int.MaxValue));
            Assert.Equal(999999, historical.Scrap);
            Assert.Equal(0, historical.AddScrap(1));
        }

        [Fact]
        public void LegacyCharges_ConvertOnceAtStageBoundaryWithoutUnlockingFutureWeapons()
        {
            var progress = new PlayerRunProgress();
            progress.AddUpgradeCharge(WeaponStyleId.Fortress, 2);
            progress.AddUpgradeCharge(WeaponStyleId.Laser, 1);
            var restored = new PlayerRunProgress();
            restored.RestoreSnapshot(progress.CaptureSnapshot());
            restored.ApplyStageDefaults(new StageDefinition());
            Assert.Equal(0, restored.StoredUpgradeCharges);
            Assert.Equal(2, restored.RunLevel);
            Assert.Equal(1f, restored.RunXp);
            Assert.Equal(1, restored.PendingLevelUps);
            Assert.False(restored.Weapons.OwnsStyle(WeaponStyleId.Fortress));
            Assert.False(restored.Weapons.OwnsStyle(WeaponStyleId.Laser));
            restored.ApplyStageDefaults(new StageDefinition());
            Assert.Equal(1f, restored.RunXp);
            Assert.Equal(1, restored.PendingLevelUps);
        }

        [Fact]
        public void StageChangesAndSaveRestore_PreserveReserveUpgrades()
        {
            var stage = new StageDefinition { ShipsPerLife = 2 };
            var progress = new PlayerRunProgress();
            progress.BeginCampaign(stage, GameDifficulty.Normal);
            Assert.True(progress.TryEquipPassive(PassiveReactorId.ArmorPlating));
            progress.ApplyEmergencyReserveUpgrade();
            Assert.Equal(4, progress.ShipsPerLife);

            progress.ApplyStageDefaults(stage);
            var restored = new PlayerRunProgress();
            restored.RestoreSnapshot(progress.CaptureSnapshot());
            restored.ApplyStageDefaults(stage);

            Assert.Equal(4, progress.ShipsPerLife);
            Assert.Equal(4, restored.ShipsPerLife);
            restored.ApplyStageDefaults(new StageDefinition { ShipsPerLife = 5 });
            Assert.Equal(5, restored.ShipsPerLife);
        }

        [Fact]
        public void CaptureAndRestore_PreservesRunProgressionState()
        {
            var progress = new PlayerRunProgress();
            progress.BeginCampaign(new StageDefinition
            {
                StartingLives = 3,
                ShipsPerLife = 2,
            }, GameDifficulty.Normal);

            progress.AddXp(90f);
            progress.AddScrap(5);
            progress.TryEquipSupportWeapon(WeaponStyleId.Missile);
            progress.TryEquipPassive(PassiveReactorId.Overclock);
            progress.TryEquipPassive(PassiveReactorId.TimeBattery);
            progress.Weapons.SetStyleProgress(WeaponStyleId.Pulse, 3);
            progress.TryAddEvolution(EvolutionId.SingularityRail);
            progress.UpdateFocusFire(true, 0.75f);
            progress.UpdateKillChain(12);

            PlayerRunProgressSnapshotData snapshot = progress.CaptureSnapshot();

            var restored = new PlayerRunProgress();
            restored.RestoreSnapshot(snapshot);

            Assert.Equal(progress.RunXp, restored.RunXp);
            Assert.Equal(7, restored.RunLevel);
            Assert.Equal(6, restored.PendingLevelUps);
            Assert.Equal(3, restored.PassiveSlots);
            Assert.Equal(5, restored.Scrap);
            Assert.Equal(3, restored.KillChainTier);
            Assert.Equal(0.75f, restored.FocusFireSeconds);
            Assert.Contains(WeaponStyleId.Missile, restored.Weapons.SupportWeapons);
            Assert.Contains(PassiveReactorId.Overclock, restored.Weapons.PassiveReactors);
            Assert.Contains(PassiveReactorId.TimeBattery, restored.Weapons.PassiveReactors);
            Assert.Contains(EvolutionId.SingularityRail, restored.Weapons.Evolutions);
        }

        [Fact]
        public void RestoreSnapshot_ClampsInvalidValuesToSafeRanges()
        {
            var progress = new PlayerRunProgress();
            progress.RestoreSnapshot(new PlayerRunProgressSnapshotData
            {
                StartingLives = -1,
                ShipsPerLife = -2,
                NonWeaponUpgradeCount = -3,
                MoveSpeedMultiplier = -4f,
                RewindEfficiency = -5f,
                DropBonusChance = -6f,
                RunXp = -7f,
                RunLevel = 0,
                Scrap = -8,
                PendingLevelUps = -9,
                PassiveSlots = 9,
                KillChainTier = -10,
                FocusFireSeconds = -11f,
            });

            Assert.Equal(3, progress.StartingLives);
            Assert.Equal(2, progress.ShipsPerLife);
            Assert.Equal(0, progress.NonWeaponUpgradeCount);
            Assert.Equal(1f, progress.MoveSpeedMultiplier);
            Assert.Equal(0f, progress.RewindEfficiency);
            Assert.Equal(0f, progress.DropBonusChance);
            Assert.Equal(0f, progress.RunXp);
            Assert.Equal(1, progress.RunLevel);
            Assert.Equal(0, progress.Scrap);
            Assert.Equal(0, progress.PendingLevelUps);
            Assert.Equal(3, progress.PassiveSlots);
            Assert.Equal(0, progress.KillChainTier);
            Assert.Equal(0f, progress.FocusFireSeconds);
        }
    }
}
