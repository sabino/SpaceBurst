using SpaceBurst.RuntimeData;
using System.Linq;
using Xunit;

namespace SpaceBurst.Tests
{
    public sealed class RunProgressionControllerTests
    {
        [Fact]
        public void SupportDraft_UnlocksAndEquipsExactlyOnceAndReportsSuccess()
        {
            var progress = CreateProgress();
            var controller = new RunProgressionController();
            float rewindSeconds = 3f;

            bool applied = controller.ApplyDraftSelection(
                progress,
                new UpgradeDraftCard
                {
                    Type = UpgradeCardType.SupportWeapon,
                    StyleId = WeaponStyleId.Missile,
                },
                ref rewindSeconds,
                8f);

            Assert.True(applied);
            Assert.True(progress.Weapons.OwnsStyle(WeaponStyleId.Missile));
            Assert.Contains(WeaponStyleId.Missile, progress.Weapons.SupportWeapons);
            Assert.Equal(0, progress.Weapons.GetLevel(WeaponStyleId.Missile));
            Assert.Equal(1, progress.NonWeaponUpgradeCount);
            Assert.Equal(3f, rewindSeconds);
        }

        [Fact]
        public void FullSupportStack_StillOffersAndUnlocksLaterChapterWeaponsAsCoreSwaps()
        {
            var progress = CreateProgress();
            Assert.True(progress.TryEquipSupportWeapon(WeaponStyleId.Spread));
            Assert.True(progress.TryEquipSupportWeapon(WeaponStyleId.Missile));
            Assert.True(progress.TryEquipSupportWeapon(WeaponStyleId.Laser));
            Assert.True(progress.TryEquipSupportWeapon(WeaponStyleId.Arc));
            Assert.False(progress.Weapons.HasSupportCapacity);

            var controller = new RunProgressionController();
            UpgradeDraftCard swapCard = controller
                .BuildDraftCards(progress, null, false, 21)
                .Single(card => card.Type == UpgradeCardType.SupportWeapon);

            Assert.Equal("CORE SWAP", swapCard.BadgeText);
            Assert.Contains(swapCard.StyleId, new[] { WeaponStyleId.Plasma, WeaponStyleId.Drone });

            float rewindSeconds = 3f;
            bool applied = controller.ApplyDraftSelection(progress, swapCard, ref rewindSeconds, 8f);

            Assert.True(applied);
            Assert.Equal(swapCard.StyleId, progress.Weapons.ActiveStyle);
            Assert.True(progress.Weapons.OwnsStyle(swapCard.StyleId));
            Assert.Equal(WeaponInventoryState.SupportWeaponCapacity, progress.Weapons.SupportWeapons.Count);
            Assert.DoesNotContain(swapCard.StyleId, progress.Weapons.SupportWeapons);
            Assert.True(progress.Weapons.OwnsStyle(WeaponStyleId.Pulse));
        }

        [Fact]
        public void EveryWeaponHasOneDistinctEvolution()
        {
            Assert.Equal(WeaponCatalog.StyleOrder.Count, WeaponProgressionCatalog.Evolutions.Count);
            Assert.Equal(WeaponCatalog.StyleOrder.Count, WeaponProgressionCatalog.Evolutions.Select(definition => definition.StyleId).Distinct().Count());
            Assert.Equal(WeaponCatalog.StyleOrder.Count, WeaponProgressionCatalog.Evolutions.Select(definition => definition.Id).Distinct().Count());
            Assert.All(WeaponCatalog.StyleOrder, style => Assert.NotNull(WeaponProgressionCatalog.GetEvolutionForStyle(style)));
        }

        [Theory]
        [InlineData(1, WeaponStyleId.Spread, true)]
        [InlineData(1, WeaponStyleId.Laser, false)]
        [InlineData(11, WeaponStyleId.Laser, true)]
        [InlineData(20, WeaponStyleId.Plasma, false)]
        [InlineData(21, WeaponStyleId.Plasma, true)]
        [InlineData(31, WeaponStyleId.Rail, true)]
        [InlineData(40, WeaponStyleId.Fortress, false)]
        [InlineData(41, WeaponStyleId.Fortress, true)]
        public void WeaponAvailability_FollowsChapterMilestones(int stageNumber, WeaponStyleId styleId, bool expected)
        {
            Assert.Equal(expected, WeaponProgressionCatalog.IsAvailableAtStage(styleId, stageNumber));
        }

        [Fact]
        public void EvolutionCannotBeAppliedBeforeItsWeaponIsOwned()
        {
            var progress = CreateProgress();

            Assert.False(progress.TryAddEvolution(EvolutionId.PrismLance));

            progress.Weapons.SetStyleProgress(WeaponStyleId.Laser, 3);
            Assert.False(progress.TryAddEvolution(EvolutionId.PrismLance));

            Assert.True(progress.TryEquipPassive(PassiveReactorId.ChainReactor));
            Assert.True(progress.TryAddEvolution(EvolutionId.PrismLance));
            Assert.False(progress.TryAddEvolution(EvolutionId.PrismLance));
        }

        [Fact]
        public void ForgedEvolutionCardCannotBypassWeaponAndPassiveRequirements()
        {
            var progress = CreateProgress();
            var controller = new RunProgressionController();
            float rewindSeconds = 2f;

            bool applied = controller.ApplyDraftSelection(
                progress,
                new UpgradeDraftCard
                {
                    Type = UpgradeCardType.EvolutionSurge,
                    EvolutionId = EvolutionId.CitadelNova,
                    StyleId = WeaponStyleId.Fortress,
                },
                ref rewindSeconds,
                8f);

            Assert.False(applied);
            Assert.False(progress.HasEvolution(EvolutionId.CitadelNova));
            Assert.Equal(2f, rewindSeconds);
        }

        private static PlayerRunProgress CreateProgress()
        {
            var progress = new PlayerRunProgress();
            progress.BeginCampaign(new StageDefinition
            {
                StartingLives = 3,
                ShipsPerLife = 2,
            }, GameDifficulty.Normal);
            return progress;
        }
    }
}
