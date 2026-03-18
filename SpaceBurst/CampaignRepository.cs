using Microsoft.Xna.Framework;
using SpaceBurst.RuntimeData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpaceBurst
{
    sealed class CampaignRepository
    {
        public EnemyArchetypeCatalogDefinition ArchetypeCatalog { get; private set; }
        public Dictionary<string, EnemyArchetypeDefinition> ArchetypesById { get; private set; }
        public List<StageDefinition> Stages { get; private set; }

        public void Load()
        {
            ArchetypeCatalog = LevelSerializer.DeserializeArchetypes(ReadTextAsset("Levels/enemy-archetypes.json"));
            List<ValidationIssue> archetypeIssues = LevelValidator.ValidateArchetypes(ArchetypeCatalog);
            if (archetypeIssues.Count > 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, archetypeIssues.Select(x => x.ToString())));

            ArchetypesById = ArchetypeCatalog.Archetypes.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
            Stages = new List<StageDefinition>();

            for (int stageNumber = 1; stageNumber <= 50; stageNumber++)
            {
                string fileName = string.Concat("Levels/level-", stageNumber.ToString("00"), ".json");
                Stages.Add(LevelSerializer.DeserializeLevel(ReadTextAsset(fileName)));
            }

            List<ValidationIssue> campaignIssues = LevelValidator.ValidateCampaign(Stages, ArchetypesById);
            if (campaignIssues.Count > 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, campaignIssues.Select(x => x.ToString())));
        }

        public StageDefinition GetStage(int stageNumber)
        {
            if (Stages == null || stageNumber < 1 || stageNumber > Stages.Count)
                throw new ArgumentOutOfRangeException(nameof(stageNumber));

            return Stages[stageNumber - 1];
        }

        private static string ReadTextAsset(string relativePath)
        {
            using (Stream stream = TitleContainer.OpenStream(relativePath.Replace('\\', '/')))
            using (var reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }
    }
}
