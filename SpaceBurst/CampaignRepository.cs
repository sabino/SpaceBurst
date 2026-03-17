using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using SpaceBurst.RuntimeData;

namespace SpaceBurst
{
    sealed class CampaignRepository
    {
        public EnemyArchetypeCatalogDefinition ArchetypeCatalog { get; private set; }
        public Dictionary<string, EnemyArchetypeDefinition> ArchetypesById { get; private set; }
        public List<LevelDefinition> Levels { get; private set; }

        public void Load()
        {
            ArchetypeCatalog = LevelSerializer.DeserializeArchetypes(ReadTextAsset("Levels/enemy-archetypes.json"));
            var archetypeIssues = LevelValidator.ValidateArchetypes(ArchetypeCatalog);
            if (archetypeIssues.Count > 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, archetypeIssues.Select(x => x.ToString())));

            ArchetypesById = ArchetypeCatalog.Archetypes.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
            Levels = new List<LevelDefinition>();

            for (int i = 1; i <= 50; i++)
            {
                string fileName = string.Concat("Levels/level-", i.ToString("00"), ".json");
                Levels.Add(LevelSerializer.DeserializeLevel(ReadTextAsset(fileName)));
            }

            var campaignIssues = LevelValidator.ValidateCampaign(Levels, ArchetypesById);
            if (campaignIssues.Count > 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, campaignIssues.Select(x => x.ToString())));
        }

        public LevelDefinition GetLevel(int levelNumber)
        {
            if (Levels == null || levelNumber < 1 || levelNumber > Levels.Count)
                throw new ArgumentOutOfRangeException(nameof(levelNumber));

            return Levels[levelNumber - 1];
        }

        private static string ReadTextAsset(string relativePath)
        {
            using (Stream stream = TitleContainer.OpenStream(relativePath.Replace('\\', '/')))
            using (var reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }
    }
}
