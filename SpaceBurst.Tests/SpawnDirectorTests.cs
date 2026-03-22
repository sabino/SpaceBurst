using Xunit;

namespace SpaceBurst.Tests
{
    public sealed class SpawnDirectorTests
    {
        [Fact]
        public void RestoreSnapshot_PreservesEliteAndKillChainProgress()
        {
            var director = new SpawnDirector();
            director.RestoreSnapshot(new SpawnDirectorSnapshotData
            {
                NextHordePacketIndex = 4,
                NextEliteBurstIndex = 2,
                NextPresentationCueIndex = 3,
                WarningTimerSeconds = 1.4f,
                WarningText = "PURSUIT DREADNOUGHT",
                WarningAccentColor = "#FFD166",
                WarningIntensity = 1.15f,
                WarningKind = SpaceBurst.RuntimeData.PresentationCueKind.BossSignal,
                TriggeredKillChainIndices = { 1, 3, 5 },
                PendingHordeBursts =
                {
                    new PendingHordePacketBurstSnapshotData
                    {
                        PacketIndex = 7,
                        NextBurstNumber = 2,
                        NextBurstAtSeconds = 48.5f,
                    }
                }
            });

            SpawnDirectorSnapshotData snapshot = director.CaptureSnapshot();

            Assert.Equal(4, snapshot.NextHordePacketIndex);
            Assert.Equal(2, snapshot.NextEliteBurstIndex);
            Assert.Equal(3, snapshot.NextPresentationCueIndex);
            Assert.Equal(1.4f, snapshot.WarningTimerSeconds);
            Assert.Equal("PURSUIT DREADNOUGHT", snapshot.WarningText);
            Assert.Equal("#FFD166", snapshot.WarningAccentColor);
            Assert.Equal(1.15f, snapshot.WarningIntensity);
            Assert.Equal(SpaceBurst.RuntimeData.PresentationCueKind.BossSignal, snapshot.WarningKind);
            Assert.Equal(new[] { 1, 3, 5 }, snapshot.TriggeredKillChainIndices);
            Assert.Single(snapshot.PendingHordeBursts);
            Assert.Equal(7, snapshot.PendingHordeBursts[0].PacketIndex);
            Assert.Equal(2, snapshot.PendingHordeBursts[0].NextBurstNumber);
            Assert.Equal(48.5f, snapshot.PendingHordeBursts[0].NextBurstAtSeconds);
        }
    }
}
