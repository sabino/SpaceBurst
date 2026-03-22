using SpaceBurst.RuntimeData;
using Xunit;

namespace SpaceBurst.Tests
{
    public sealed class PlayerRunProgressTests
    {
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
