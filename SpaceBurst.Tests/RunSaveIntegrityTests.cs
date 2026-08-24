using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace SpaceBurst.Tests
{
    public sealed class RunSaveIntegrityTests
    {
        [Fact]
        public void SealAndLoadPreparation_PreserveValidSave()
        {
            JsonSerializerOptions options = CreateOptions();
            var save = new RunSaveData
            {
                CurrentStageNumber = 27,
                CurrentSectionIndex = 3,
                GameplayRngState = 42,
            };

            RunSaveIntegrity.Seal(save, options);
            string json = JsonSerializer.Serialize(save, options);
            RunSaveData restored = JsonSerializer.Deserialize<RunSaveData>(json, options);

            Assert.Equal(RunSaveData.CurrentSchemaVersion, restored.SchemaVersion);
            Assert.NotEmpty(restored.IntegrityHash);
            Assert.True(RunSaveIntegrity.TryPrepareForLoad(restored, options, out string warning));
            Assert.Equal(string.Empty, warning);
            Assert.Equal(27, restored.CurrentStageNumber);
            Assert.Equal(42u, restored.GameplayRngState);
        }

        [Fact]
        public void LoadPreparation_RejectsTamperedCurrentSave()
        {
            JsonSerializerOptions options = CreateOptions();
            var save = new RunSaveData { CurrentStageNumber = 12 };
            RunSaveIntegrity.Seal(save, options);

            save.CurrentStageNumber = 49;

            Assert.False(RunSaveIntegrity.TryPrepareForLoad(save, options, out string warning));
            Assert.Equal("SAVE INTEGRITY CHECK FAILED", warning);
        }

        [Fact]
        public void LoadPreparation_MigratesLegacySaveAndNormalizesUnsafeValues()
        {
            JsonSerializerOptions options = CreateOptions();
            var save = new RunSaveData
            {
                SchemaVersion = 0,
                CurrentStageNumber = 8,
                CurrentSectionIndex = -5,
                RewindMeterSeconds = float.PositiveInfinity,
                GameplayRngState = 0,
            };

            Assert.True(RunSaveIntegrity.TryPrepareForLoad(save, options, out string warning));
            Assert.Equal("LEGACY SAVE MIGRATED", warning);
            Assert.Equal(RunSaveData.CurrentSchemaVersion, save.SchemaVersion);
            Assert.Equal(0, save.CurrentSectionIndex);
            Assert.Equal(0f, save.RewindMeterSeconds);
            Assert.Equal(1u, save.GameplayRngState);
            Assert.NotEmpty(save.IntegrityHash);
        }

        [Fact]
        public void LoadPreparation_RemovesNullEntitiesAndRepairsRequiredNestedState()
        {
            JsonSerializerOptions options = CreateOptions();
            var save = new RunSaveData
            {
                SchemaVersion = 0,
                CurrentStageNumber = 8,
                PlayerStatus = new PlayerStatusSnapshotData
                {
                    Lives = int.MaxValue,
                    RunProgress = null,
                },
                Player = new PlayerSnapshotData
                {
                    Position = null,
                    Velocity = null,
                    CannonDirection = null,
                },
                Enemies = new System.Collections.Generic.List<EnemySnapshotData> { null, new EnemySnapshotData { Position = null } },
                Bullets = new System.Collections.Generic.List<BulletSnapshotData> { null, new BulletSnapshotData { Position = null, SpriteDefinition = null } },
                ScheduledSpawns = new System.Collections.Generic.List<ScheduledSpawnSnapshotData> { null },
            };

            Assert.True(RunSaveIntegrity.TryPrepareForLoad(save, options, out _));
            Assert.Equal(99, save.PlayerStatus.Lives);
            Assert.NotNull(save.PlayerStatus.RunProgress);
            Assert.NotNull(save.Player.Position);
            Assert.NotNull(save.Player.Velocity);
            Assert.NotNull(save.Player.CannonDirection);
            Assert.Single(save.Enemies);
            Assert.NotNull(save.Enemies[0].Position);
            Assert.Single(save.Bullets);
            Assert.NotNull(save.Bullets[0].Position);
            Assert.NotNull(save.Bullets[0].SpriteDefinition);
            Assert.Empty(save.ScheduledSpawns);
        }

        [Fact]
        public void FileStorageBackend_ReplacesFilesWithoutLeavingTemporaryArtifacts()
        {
            string directory = Path.Combine(Path.GetTempPath(), string.Concat("spaceburst-storage-test-", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            try
            {
                var backend = new FileStorageBackend(directory, null);
                backend.WriteAllText("slot-1.json", "first");
                backend.WriteAllText("slot-1.json", "second");

                Assert.Equal("second", backend.ReadAllText("slot-1.json"));
                Assert.Empty(Directory.GetFiles(directory, "*.tmp-*", SearchOption.AllDirectories));
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }
    }
}
