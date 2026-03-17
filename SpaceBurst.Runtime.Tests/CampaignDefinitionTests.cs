using SpaceBurst.RuntimeData;
using Xunit;

namespace SpaceBurst.Runtime.Tests
{
    public sealed class CampaignDefinitionTests
    {
        [Fact]
        public void CampaignFilesDeserializeAndValidate()
        {
            string repoRoot = FindRepositoryRoot();
            string levelsDirectory = Path.Combine(repoRoot, "Levels");

            EnemyArchetypeCatalogDefinition archetypeCatalog = LevelSerializer.LoadArchetypesFromFile(Path.Combine(levelsDirectory, "enemy-archetypes.json"));
            Dictionary<string, EnemyArchetypeDefinition> archetypes = archetypeCatalog.Archetypes.ToDictionary(x => x.Id, x => x);
            List<LevelDefinition> levels = new List<LevelDefinition>();

            for (int levelNumber = 1; levelNumber <= 50; levelNumber++)
            {
                string path = Path.Combine(levelsDirectory, string.Concat("level-", levelNumber.ToString("00"), ".json"));
                levels.Add(LevelSerializer.LoadLevelFromFile(path));
            }

            List<ValidationIssue> archetypeIssues = LevelValidator.ValidateArchetypes(archetypeCatalog);
            List<ValidationIssue> campaignIssues = LevelValidator.ValidateCampaign(levels, archetypes);

            Assert.Empty(archetypeIssues);
            Assert.Empty(campaignIssues);
        }

        [Fact]
        public void BossLevelsOnlyAppearEveryTenthStage()
        {
            string repoRoot = FindRepositoryRoot();
            string levelsDirectory = Path.Combine(repoRoot, "Levels");

            for (int levelNumber = 1; levelNumber <= 50; levelNumber++)
            {
                LevelDefinition level = LevelSerializer.LoadLevelFromFile(Path.Combine(levelsDirectory, string.Concat("level-", levelNumber.ToString("00"), ".json")));
                bool shouldHaveBoss = levelNumber % 10 == 0;

                Assert.Equal(shouldHaveBoss, level.Boss != null);
            }
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string levelsDirectory = Path.Combine(directory.FullName, "Levels");
                string solutionPath = Path.Combine(directory.FullName, "SpaceBurst.sln");
                if (Directory.Exists(levelsDirectory) && File.Exists(solutionPath))
                    return directory.FullName;

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate the SpaceBurst repository root.");
        }
    }
}
