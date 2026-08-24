using SpaceBurst.RuntimeData;
using System.Linq;
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

        [Fact]
        public void Cycle_SwapsCoreAndSupportWithoutDoubleEquippingEitherWeapon()
        {
            var inventory = new WeaponInventoryState();
            inventory.Reset();
            inventory.ApplyWeaponUpgrade(WeaponStyleId.Missile, activateStyle: false);

            inventory.Cycle(1);

            Assert.Equal(WeaponStyleId.Missile, inventory.ActiveStyle);
            Assert.DoesNotContain(WeaponStyleId.Missile, inventory.SupportWeapons);
            Assert.Contains(WeaponStyleId.Pulse, inventory.SupportWeapons);
            Assert.Equal(2, inventory.EquippedWeapons.Count);
            Assert.Equal(inventory.EquippedWeapons.Count, inventory.EquippedWeapons.Distinct().Count());
        }

        [Fact]
        public void RestoreSnapshot_ClampsCorruptInventoryAndSlotCounts()
        {
            var snapshot = new WeaponInventorySnapshotData
            {
                ActiveStyle = WeaponStyleId.Pulse,
                StyleLevels =
                {
                    [WeaponStyleId.Pulse] = 900,
                    [WeaponStyleId.Spread] = -10,
                    [WeaponStyleId.Laser] = 2,
                    [WeaponStyleId.Plasma] = 2,
                    [WeaponStyleId.Missile] = 2,
                    [WeaponStyleId.Rail] = 2,
                },
                StyleRanks =
                {
                    [WeaponStyleId.Pulse] = 900,
                },
                StyleCharges =
                {
                    [WeaponStyleId.Pulse] = int.MaxValue,
                },
                SupportWeapons =
                {
                    WeaponStyleId.Spread,
                    WeaponStyleId.Laser,
                    WeaponStyleId.Plasma,
                    WeaponStyleId.Missile,
                    WeaponStyleId.Rail,
                },
                PassiveReactors =
                {
                    PassiveReactorId.Overclock,
                    PassiveReactorId.MagnetCore,
                    PassiveReactorId.ArmorPlating,
                    PassiveReactorId.TimeBattery,
                },
            };

            var restored = new WeaponInventoryState();
            restored.RestoreSnapshot(snapshot);

            Assert.Equal(3, restored.GetLevel(WeaponStyleId.Pulse));
            Assert.Equal(0, restored.GetLevel(WeaponStyleId.Spread));
            Assert.Equal(99, restored.GetRank(WeaponStyleId.Pulse));
            Assert.Equal(99, restored.GetStoredCharge(WeaponStyleId.Pulse));
            Assert.Equal(WeaponInventoryState.SupportWeaponCapacity, restored.SupportWeapons.Count);
            Assert.Equal(3, restored.PassiveReactors.Count);
        }
    }
}
