using System.Collections.Generic;
using System.Linq;

namespace SpaceBurst.RuntimeData
{
    public static class LevelValidator
    {
        public static List<ValidationIssue> ValidateArchetypes(EnemyArchetypeCatalogDefinition catalog)
        {
            var issues = new List<ValidationIssue>();
            var ids = new HashSet<string>();

            if (catalog == null)
            {
                issues.Add(new ValidationIssue("Archetypes", "Catalog is missing."));
                return issues;
            }

            if (catalog.Archetypes == null || catalog.Archetypes.Count == 0)
                issues.Add(new ValidationIssue("Archetypes", "At least one archetype is required."));

            if (catalog.Archetypes == null)
                return issues;

            for (int i = 0; i < catalog.Archetypes.Count; i++)
            {
                var archetype = catalog.Archetypes[i];
                string path = string.Concat("Archetypes[", i.ToString(), "]");

                if (string.IsNullOrWhiteSpace(archetype.Id))
                    issues.Add(new ValidationIssue(path, "Id is required."));
                else if (!ids.Add(archetype.Id))
                    issues.Add(new ValidationIssue(path, "Id must be unique."));

                if (string.IsNullOrWhiteSpace(archetype.Texture))
                    issues.Add(new ValidationIssue(path, "Texture is required."));
                if (archetype.RenderScale <= 0f)
                    issues.Add(new ValidationIssue(path, "RenderScale must be greater than zero."));
                if (archetype.CollisionRadius <= 0f)
                    issues.Add(new ValidationIssue(path, "CollisionRadius must be greater than zero."));
                if (archetype.Speed <= 0f)
                    issues.Add(new ValidationIssue(path, "Speed must be greater than zero."));
                if (archetype.HitPoints <= 0)
                    issues.Add(new ValidationIssue(path, "HitPoints must be greater than zero."));
                if (archetype.ScoreValue < 0)
                    issues.Add(new ValidationIssue(path, "ScoreValue cannot be negative."));
                if (archetype.SpawnDelaySeconds < 0f)
                    issues.Add(new ValidationIssue(path, "SpawnDelaySeconds cannot be negative."));
            }

            return issues;
        }

        public static List<ValidationIssue> ValidateLevel(LevelDefinition level, IDictionary<string, EnemyArchetypeDefinition> archetypes)
        {
            var issues = new List<ValidationIssue>();

            if (level == null)
            {
                issues.Add(new ValidationIssue("Level", "Definition is missing."));
                return issues;
            }

            if (level.LevelNumber < 1 || level.LevelNumber > 50)
                issues.Add(new ValidationIssue("LevelNumber", "LevelNumber must be between 1 and 50."));
            if (string.IsNullOrWhiteSpace(level.Name))
                issues.Add(new ValidationIssue("Name", "Name is required."));
            if (level.IntroSeconds < 0f)
                issues.Add(new ValidationIssue("IntroSeconds", "IntroSeconds cannot be negative."));
            if (level.Waves == null || level.Waves.Count == 0)
                issues.Add(new ValidationIssue("Waves", "At least one wave is required."));

            bool bossLevel = level.LevelNumber > 0 && level.LevelNumber % 10 == 0;
            if (bossLevel && level.Boss == null)
                issues.Add(new ValidationIssue("Boss", "Boss levels require a Boss definition."));
            if (!bossLevel && level.Boss != null)
                issues.Add(new ValidationIssue("Boss", "Only levels 10, 20, 30, 40, and 50 may define a Boss."));

            if (level.Waves != null)
            {
                float lastStart = -1f;
                for (int i = 0; i < level.Waves.Count; i++)
                {
                    var wave = level.Waves[i];
                    string wavePath = string.Concat("Waves[", i.ToString(), "]");

                    if (wave == null)
                    {
                        issues.Add(new ValidationIssue(wavePath, "Wave cannot be null."));
                        continue;
                    }

                    if (wave.StartSeconds < 0f)
                        issues.Add(new ValidationIssue(wavePath, "StartSeconds cannot be negative."));
                    if (wave.StartSeconds < lastStart)
                        issues.Add(new ValidationIssue(wavePath, "Waves must be ordered by StartSeconds."));
                    lastStart = wave.StartSeconds;

                    if (wave.Groups == null || wave.Groups.Count == 0)
                        issues.Add(new ValidationIssue(wavePath, "Each wave requires at least one spawn group."));

                    if (wave.Groups == null)
                        continue;

                    for (int j = 0; j < wave.Groups.Count; j++)
                    {
                        var group = wave.Groups[j];
                        string groupPath = string.Concat(wavePath, ".Groups[", j.ToString(), "]");

                        if (group == null)
                        {
                            issues.Add(new ValidationIssue(groupPath, "Spawn group cannot be null."));
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(group.ArchetypeId))
                            issues.Add(new ValidationIssue(groupPath, "ArchetypeId is required."));
                        else if (archetypes == null || !archetypes.ContainsKey(group.ArchetypeId))
                            issues.Add(new ValidationIssue(groupPath, string.Concat("Unknown ArchetypeId '", group.ArchetypeId, "'.")));

                        if (group.Count <= 0)
                            issues.Add(new ValidationIssue(groupPath, "Count must be greater than zero."));
                        if (group.AnchorX < 0f || group.AnchorX > 1f)
                            issues.Add(new ValidationIssue(groupPath, "AnchorX must be between 0 and 1."));
                        if (group.AnchorY < 0f || group.AnchorY > 1f)
                            issues.Add(new ValidationIssue(groupPath, "AnchorY must be between 0 and 1."));
                        if (group.Spacing < 16f)
                            issues.Add(new ValidationIssue(groupPath, "Spacing must be at least 16."));
                        if (group.DelayBetweenSpawns < 0f)
                            issues.Add(new ValidationIssue(groupPath, "DelayBetweenSpawns cannot be negative."));
                        if (group.TravelDuration <= 0.25f)
                            issues.Add(new ValidationIssue(groupPath, "TravelDuration must be greater than 0.25 seconds."));
                        if (group.SpeedMultiplier <= 0f)
                            issues.Add(new ValidationIssue(groupPath, "SpeedMultiplier must be greater than zero."));
                    }
                }
            }

            if (level.Boss != null)
            {
                if (string.IsNullOrWhiteSpace(level.Boss.DisplayName))
                    issues.Add(new ValidationIssue("Boss.DisplayName", "Boss DisplayName is required."));
                if (string.IsNullOrWhiteSpace(level.Boss.ArchetypeId))
                    issues.Add(new ValidationIssue("Boss.ArchetypeId", "Boss ArchetypeId is required."));
                else if (archetypes == null || !archetypes.ContainsKey(level.Boss.ArchetypeId))
                    issues.Add(new ValidationIssue("Boss.ArchetypeId", string.Concat("Unknown Boss ArchetypeId '", level.Boss.ArchetypeId, "'.")));
                if (level.Boss.AnchorX < 0f || level.Boss.AnchorX > 1f)
                    issues.Add(new ValidationIssue("Boss.AnchorX", "Boss AnchorX must be between 0 and 1."));
                if (level.Boss.AnchorY < 0f || level.Boss.AnchorY > 1f)
                    issues.Add(new ValidationIssue("Boss.AnchorY", "Boss AnchorY must be between 0 and 1."));
                if (level.Boss.RenderScale <= 0f)
                    issues.Add(new ValidationIssue("Boss.RenderScale", "Boss RenderScale must be greater than zero."));
                if (level.Boss.HitPoints <= 0)
                    issues.Add(new ValidationIssue("Boss.HitPoints", "Boss HitPoints must be greater than zero."));
            }

            return issues;
        }

        public static List<ValidationIssue> ValidateCampaign(IList<LevelDefinition> levels, IDictionary<string, EnemyArchetypeDefinition> archetypes)
        {
            var issues = new List<ValidationIssue>();

            if (levels == null || levels.Count != 50)
                issues.Add(new ValidationIssue("Campaign", "Exactly 50 levels are required."));

            if (levels == null)
                return issues;

            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i] == null)
                {
                    issues.Add(new ValidationIssue(string.Concat("Levels[", i.ToString(), "]"), "Level cannot be null."));
                    continue;
                }

                if (levels[i].LevelNumber != i + 1)
                    issues.Add(new ValidationIssue(string.Concat("Levels[", i.ToString(), "]"), "Level files must be sequentially numbered."));

                issues.AddRange(ValidateLevel(levels[i], archetypes).Select(x => new ValidationIssue(string.Concat("Level ", levels[i].LevelNumber.ToString(), " / ", x.Path), x.Message)));
            }

            return issues;
        }
    }
}
