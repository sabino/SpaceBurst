using SpaceBurst.RuntimeData;
using Xunit;

namespace SpaceBurst.Tests
{
    public sealed class WeaponInventoryStateTests
    {
        [Fact]
        public void CaptureAndRestore_PreservesExtendedLoadoutState()
        {
            var inventory = new WeaponInventoryState();
            inventory.Reset();
            inventory.SetStyleProgress(WeaponStyleId.Pulse, 3, 1, true);
            inventory.ApplyWeaponUpgrade(WeaponStyleId.Missile, activateStyle: false);
            inventory.ApplyWeaponUpgrade(WeaponStyleId.Arc, activateStyle: false);
            inventory.TryEquipPassive(PassiveReactorId.Overclock, 3);
            inventory.TryEquipPassive(PassiveReactorId.TimeBattery, 3);
            inventory.TryAddEvolution(EvolutionId.SingularityRail);
            inventory.AddUpgradeCharge(WeaponStyleId.Pulse, 2);

            WeaponInventorySnapshotData snapshot = inventory.CaptureSnapshot();

            var restored = new WeaponInventoryState();
            restored.RestoreSnapshot(snapshot);

            Assert.Equal(WeaponStyleId.Pulse, restored.ActiveStyle);
            Assert.Equal(3, restored.GetLevel(WeaponStyleId.Pulse));
            Assert.Equal(1, restored.GetRank(WeaponStyleId.Pulse));
            Assert.Equal(2, restored.GetStoredCharge(WeaponStyleId.Pulse));
            Assert.Contains(WeaponStyleId.Missile, restored.SupportWeapons);
            Assert.Contains(WeaponStyleId.Arc, restored.SupportWeapons);
            Assert.Contains(PassiveReactorId.Overclock, restored.PassiveReactors);
            Assert.Contains(PassiveReactorId.TimeBattery, restored.PassiveReactors);
            Assert.Contains(EvolutionId.SingularityRail, restored.Evolutions);
        }

        [Fact]
        public void RestoreSnapshot_NormalizesInvalidSupportAndMissingPulseState()
        {
            var restored = new WeaponInventoryState();
            restored.RestoreSnapshot(new WeaponInventorySnapshotData
            {
                ActiveStyle = WeaponStyleId.Blade,
                StyleLevels =
                {
                    [WeaponStyleId.Missile] = 2,
                },
                StyleRanks =
                {
                    [WeaponStyleId.Missile] = 1,
                },
                SupportWeapons =
                {
                    WeaponStyleId.Blade,
                    WeaponStyleId.Arc,
                    WeaponStyleId.Missile,
                },
            });

            Assert.Equal(WeaponStyleId.Pulse, restored.ActiveStyle);
            Assert.True(restored.OwnsStyle(WeaponStyleId.Pulse));
            Assert.True(restored.OwnsStyle(WeaponStyleId.Missile));
            Assert.DoesNotContain(WeaponStyleId.Blade, restored.SupportWeapons);
            Assert.DoesNotContain(WeaponStyleId.Arc, restored.SupportWeapons);
            Assert.Contains(WeaponStyleId.Missile, restored.SupportWeapons);
        }

        [Fact]
        public void ApplyWeaponUpgrade_UnlocksSupportWeaponWhenNotActivated()
        {
            var inventory = new WeaponInventoryState();
            inventory.Reset();

            WeaponUpgradeOutcome outcome = inventory.ApplyWeaponUpgrade(WeaponStyleId.Drone, activateStyle: false);

            Assert.Equal(WeaponUpgradeOutcome.UnlockedStyle, outcome);
            Assert.True(inventory.OwnsStyle(WeaponStyleId.Drone));
            Assert.Contains(WeaponStyleId.Drone, inventory.SupportWeapons);
            Assert.Equal(WeaponStyleId.Pulse, inventory.ActiveStyle);
        }
    }
}
