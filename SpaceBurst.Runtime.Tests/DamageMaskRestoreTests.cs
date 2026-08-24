using Xunit;

namespace SpaceBurst.RuntimeData.Tests
{
    public sealed class DamageMaskRestoreTests
    {
        [Fact]
        public void RestoredGrid_PreservesPristineCountsForIntegrityRatios()
        {
            var restored = new MaskGrid(
                3,
                2,
                new[] { true, false, true, false, false, true },
                new[] { true, false, false, false, false, false },
                initialOccupiedCount: 6,
                initialCoreCount: 2);

            Assert.Equal(3, restored.OccupiedCount);
            Assert.Equal(6, restored.InitialOccupiedCount);
            Assert.Equal(1, restored.RemainingCoreCount);
            Assert.Equal(2, restored.InitialCoreCount);
            Assert.Equal(0.5f, restored.OccupiedCount / (float)restored.InitialOccupiedCount);
        }
    }
}
