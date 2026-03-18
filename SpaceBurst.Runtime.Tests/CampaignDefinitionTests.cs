using SpaceBurst.RuntimeData;
using Xunit;

namespace SpaceBurst.Runtime.Tests
{
    public sealed class CampaignDefinitionTests
    {
        [Fact]
        public void StageFilesDeserializeAndValidate()
        {
            string repoRoot = FindRepositoryRoot();
            string levelsDirectory = Path.Combine(repoRoot, "Levels");

            EnemyArchetypeCatalogDefinition catalog = LevelSerializer.LoadArchetypesFromFile(Path.Combine(levelsDirectory, "enemy-archetypes.json"));
            Dictionary<string, EnemyArchetypeDefinition> archetypes = catalog.Archetypes.ToDictionary(x => x.Id, x => x);
            List<StageDefinition> stages = new();

            for (int stageNumber = 1; stageNumber <= 50; stageNumber++)
                stages.Add(LevelSerializer.LoadLevelFromFile(Path.Combine(levelsDirectory, $"level-{stageNumber:00}.json")));

            Assert.Empty(LevelValidator.ValidateArchetypes(catalog));
            Assert.Empty(LevelValidator.ValidateCampaign(stages, archetypes));
        }

        [Fact]
        public void BossStagesOnlyAppearEveryTenthStage()
        {
            string repoRoot = FindRepositoryRoot();
            string levelsDirectory = Path.Combine(repoRoot, "Levels");

            for (int stageNumber = 1; stageNumber <= 50; stageNumber++)
            {
                StageDefinition stage = LevelSerializer.LoadLevelFromFile(Path.Combine(levelsDirectory, $"level-{stageNumber:00}.json"));
                Assert.Equal(stageNumber % 10 == 0, stage.Boss != null);
            }
        }

        [Fact]
        public void DamageMaskRemovesCellsAndDestroysOnCoreBreach()
        {
            var sprite = new ProceduralSpriteDefinition
            {
                Rows = new List<string>
                {
                    ".....",
                    ".###.",
                    ".#C#.",
                    ".###.",
                    ".....",
                },
                VitalCore = new VitalCoreMaskDefinition
                {
                    Rows = new List<string>
                    {
                        ".....",
                        ".....",
                        "..X..",
                        ".....",
                        ".....",
                    }
                }
            };

            MaskGrid mask = DamageMaskMath.CreateGrid(sprite);
            DamageResult result = DamageMaskMath.ApplyPointDamage(mask, 2, 2, 0, 1, 15);

            Assert.Equal(1, result.CellsRemoved);
            Assert.True(result.Destroyed);
            Assert.Equal(8, result.RemainingOccupied);
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo directory = new(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "Levels")) && File.Exists(Path.Combine(directory.FullName, "SpaceBurst.sln")))
                    return directory.FullName;
                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate the SpaceBurst repository root.");
        }
    }
}
