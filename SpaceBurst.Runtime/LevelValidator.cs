using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaceBurst.RuntimeData
{
    public static class LevelValidator
    {
        public static List<ValidationIssue> ValidateArchetypes(EnemyArchetypeCatalogDefinition catalog)
        {
            var issues = new List<ValidationIssue>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                EnemyArchetypeDefinition archetype = catalog.Archetypes[i];
                string path = string.Concat("Archetypes[", i.ToString(), "]");

                if (string.IsNullOrWhiteSpace(archetype.Id))
                    issues.Add(new ValidationIssue(path, "Id is required."));
                else if (!ids.Add(archetype.Id))
                    issues.Add(new ValidationIssue(path, "Id must be unique."));

                if (string.IsNullOrWhiteSpace(archetype.DisplayName))
                    issues.Add(new ValidationIssue(path, "DisplayName is required."));
                if (archetype.RenderScale <= 0f)
                    issues.Add(new ValidationIssue(path, "RenderScale must be greater than zero."));
                if (archetype.MoveSpeed <= 0f)
                    issues.Add(new ValidationIssue(path, "MoveSpeed must be greater than zero."));
                if (archetype.HitPoints <= 0)
                    issues.Add(new ValidationIssue(path, "HitPoints must be greater than zero."));
                if (archetype.ScoreValue < 0)
                    issues.Add(new ValidationIssue(path, "ScoreValue cannot be negative."));
                if (archetype.SpawnLeadDistance < 0f)
                    issues.Add(new ValidationIssue(path, "SpawnLeadDistance cannot be negative."));
                if (archetype.FireIntervalSeconds < 0f)
                    issues.Add(new ValidationIssue(path, "FireIntervalSeconds cannot be negative."));
                if (archetype.MovementAmplitude < 0f)
                    issues.Add(new ValidationIssue(path, "MovementAmplitude cannot be negative."));
                if (archetype.MovementFrequency <= 0f)
                    issues.Add(new ValidationIssue(path, "MovementFrequency must be greater than zero."));

                issues.AddRange(ValidateSpriteDefinition(archetype.Sprite).Select(x => new ValidationIssue(string.Concat(path, ".Sprite/", x.Path), x.Message)));
                issues.AddRange(ValidateDamageMask(archetype.DamageMask).Select(x => new ValidationIssue(string.Concat(path, ".DamageMask/", x.Path), x.Message)));
            }

            return issues;
        }

        public static List<ValidationIssue> ValidateStage(StageDefinition stage, IDictionary<string, EnemyArchetypeDefinition> archetypes)
        {
            var issues = new List<ValidationIssue>();

            if (stage == null)
            {
                issues.Add(new ValidationIssue("Stage", "Definition is missing."));
                return issues;
            }

            if (stage.StageNumber < 1 || stage.StageNumber > 50)
                issues.Add(new ValidationIssue("StageNumber", "StageNumber must be between 1 and 50."));
            if (string.IsNullOrWhiteSpace(stage.Name))
                issues.Add(new ValidationIssue("Name", "Name is required."));
            if (stage.IntroSeconds < 0f)
                issues.Add(new ValidationIssue("IntroSeconds", "IntroSeconds cannot be negative."));
            if (stage.ScrollSpeed <= 0f)
                issues.Add(new ValidationIssue("ScrollSpeed", "ScrollSpeed must be greater than zero."));
            if (string.IsNullOrWhiteSpace(stage.Theme))
                issues.Add(new ValidationIssue("Theme", "Theme is required."));
            if (stage.Sections == null || stage.Sections.Count == 0)
                issues.Add(new ValidationIssue("Sections", "At least one section is required."));

            bool bossStage = stage.StageNumber > 0 && stage.StageNumber % 10 == 0;
            if (bossStage && stage.Boss == null)
                issues.Add(new ValidationIssue("Boss", "Boss stages require a Boss definition."));
            if (!bossStage && stage.Boss != null)
                issues.Add(new ValidationIssue("Boss", "Only stages 10, 20, 30, 40, and 50 may define a Boss."));

            if (stage.CheckpointMarkers != null)
            {
                foreach (float marker in stage.CheckpointMarkers)
                {
                    if (marker < 0f)
                        issues.Add(new ValidationIssue("CheckpointMarkers", "Checkpoint markers cannot be negative."));
                }
            }

            if (stage.Sections != null)
            {
                float lastStart = -1f;
                for (int i = 0; i < stage.Sections.Count; i++)
                {
                    SectionDefinition section = stage.Sections[i];
                    string sectionPath = string.Concat("Sections[", i.ToString(), "]");

                    if (section == null)
                    {
                        issues.Add(new ValidationIssue(sectionPath, "Section cannot be null."));
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(section.Label))
                        issues.Add(new ValidationIssue(sectionPath, "Section label is required."));
                    if (section.StartSeconds < 0f)
                        issues.Add(new ValidationIssue(sectionPath, "StartSeconds cannot be negative."));
                    if (section.StartSeconds < lastStart)
                        issues.Add(new ValidationIssue(sectionPath, "Sections must be ordered by StartSeconds."));
                    if (section.DurationSeconds <= 0.25f)
                        issues.Add(new ValidationIssue(sectionPath, "DurationSeconds must be greater than 0.25."));
                    if (section.Groups == null || section.Groups.Count == 0)
                        issues.Add(new ValidationIssue(sectionPath, "Each section requires at least one spawn group."));

                    lastStart = section.StartSeconds;

                    if (section.Groups == null)
                        continue;

                    for (int j = 0; j < section.Groups.Count; j++)
                    {
                        SpawnGroupDefinition group = section.Groups[j];
                        string groupPath = string.Concat(sectionPath, ".Groups[", j.ToString(), "]");

                        if (group == null)
                        {
                            issues.Add(new ValidationIssue(groupPath, "Spawn group cannot be null."));
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(group.ArchetypeId))
                            issues.Add(new ValidationIssue(groupPath, "ArchetypeId is required."));
                        else if (archetypes == null || !archetypes.ContainsKey(group.ArchetypeId))
                            issues.Add(new ValidationIssue(groupPath, string.Concat("Unknown ArchetypeId '", group.ArchetypeId, "'.")));

                        if (group.StartSeconds < 0f)
                            issues.Add(new ValidationIssue(groupPath, "StartSeconds cannot be negative."));
                        if (group.Count <= 0)
                            issues.Add(new ValidationIssue(groupPath, "Count must be greater than zero."));
                        if (group.Lane < 0 || group.Lane > 4)
                            issues.Add(new ValidationIssue(groupPath, "Lane must be between 0 and 4."));
                        if (group.TargetY.HasValue && (group.TargetY.Value < 0f || group.TargetY.Value > 1f))
                            issues.Add(new ValidationIssue(groupPath, "TargetY must be between 0 and 1 when provided."));
                        if (group.SpawnLeadDistance < 0f)
                            issues.Add(new ValidationIssue(groupPath, "SpawnLeadDistance cannot be negative."));
                        if (group.SpawnIntervalSeconds < 0f)
                            issues.Add(new ValidationIssue(groupPath, "SpawnIntervalSeconds cannot be negative."));
                        if (group.SpacingX < 0f)
                            issues.Add(new ValidationIssue(groupPath, "SpacingX cannot be negative."));
                        if (group.SpeedMultiplier <= 0f)
                            issues.Add(new ValidationIssue(groupPath, "SpeedMultiplier must be greater than zero."));
                        if (group.Amplitude < 0f)
                            issues.Add(new ValidationIssue(groupPath, "Amplitude cannot be negative."));
                        if (group.Frequency <= 0f)
                            issues.Add(new ValidationIssue(groupPath, "Frequency must be greater than zero."));
                    }
                }
            }

            if (stage.Boss != null)
                issues.AddRange(ValidateBoss(stage.Boss, archetypes).Select(x => new ValidationIssue(string.Concat("Boss/", x.Path), x.Message)));

            return issues;
        }

        public static List<ValidationIssue> ValidateCampaign(IList<StageDefinition> stages, IDictionary<string, EnemyArchetypeDefinition> archetypes)
        {
            var issues = new List<ValidationIssue>();

            if (stages == null || stages.Count != 50)
                issues.Add(new ValidationIssue("Campaign", "Exactly 50 stages are required."));

            if (stages == null)
                return issues;

            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i] == null)
                {
                    issues.Add(new ValidationIssue(string.Concat("Stages[", i.ToString(), "]"), "Stage cannot be null."));
                    continue;
                }

                if (stages[i].StageNumber != i + 1)
                    issues.Add(new ValidationIssue(string.Concat("Stages[", i.ToString(), "]"), "Stage files must be sequentially numbered."));

                issues.AddRange(ValidateStage(stages[i], archetypes).Select(x => new ValidationIssue(string.Concat("Stage ", stages[i].StageNumber.ToString(), " / ", x.Path), x.Message)));
            }

            return issues;
        }

        private static List<ValidationIssue> ValidateSpriteDefinition(ProceduralSpriteDefinition sprite)
        {
            var issues = new List<ValidationIssue>();

            if (sprite == null)
            {
                issues.Add(new ValidationIssue("Sprite", "Sprite definition is required."));
                return issues;
            }

            if (sprite.PixelScale <= 0)
                issues.Add(new ValidationIssue("PixelScale", "PixelScale must be greater than zero."));
            if (sprite.Rows == null || sprite.Rows.Count == 0)
                issues.Add(new ValidationIssue("Rows", "At least one sprite row is required."));

            if (sprite.Rows != null)
            {
                int width = sprite.Rows.Max(x => x?.Length ?? 0);
                if (width <= 0)
                    issues.Add(new ValidationIssue("Rows", "Sprite rows cannot all be empty."));

                for (int i = 0; i < sprite.Rows.Count; i++)
                {
                    string row = sprite.Rows[i] ?? string.Empty;
                    if (row.Length != width)
                        issues.Add(new ValidationIssue(string.Concat("Rows[", i.ToString(), "]"), "All sprite rows must have the same width."));
                }

                if (sprite.VitalCore?.Rows != null && sprite.VitalCore.Rows.Count > 0)
                {
                    if (sprite.VitalCore.Rows.Count != sprite.Rows.Count)
                        issues.Add(new ValidationIssue("VitalCore.Rows", "Vital core rows must match sprite row count."));

                    for (int i = 0; i < sprite.VitalCore.Rows.Count; i++)
                    {
                        string row = sprite.VitalCore.Rows[i] ?? string.Empty;
                        if (row.Length != width)
                            issues.Add(new ValidationIssue(string.Concat("VitalCore.Rows[", i.ToString(), "]"), "All vital core rows must have the same width as the sprite."));
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(sprite.PrimaryColor))
                issues.Add(new ValidationIssue("PrimaryColor", "PrimaryColor is required."));
            if (string.IsNullOrWhiteSpace(sprite.SecondaryColor))
                issues.Add(new ValidationIssue("SecondaryColor", "SecondaryColor is required."));
            if (string.IsNullOrWhiteSpace(sprite.AccentColor))
                issues.Add(new ValidationIssue("AccentColor", "AccentColor is required."));

            return issues;
        }

        private static List<ValidationIssue> ValidateDamageMask(DamageMaskDefinition damageMask)
        {
            var issues = new List<ValidationIssue>();

            if (damageMask == null)
            {
                issues.Add(new ValidationIssue("DamageMask", "Damage mask definition is required."));
                return issues;
            }

            if (damageMask.ContactDamage <= 0)
                issues.Add(new ValidationIssue("ContactDamage", "ContactDamage must be greater than zero."));
            if (damageMask.ProjectileDamage <= 0)
                issues.Add(new ValidationIssue("ProjectileDamage", "ProjectileDamage must be greater than zero."));
            if (damageMask.DamageRadius < 0)
                issues.Add(new ValidationIssue("DamageRadius", "DamageRadius cannot be negative."));
            if (damageMask.IntegrityThresholdPercent < 1 || damageMask.IntegrityThresholdPercent > 100)
                issues.Add(new ValidationIssue("IntegrityThresholdPercent", "IntegrityThresholdPercent must be between 1 and 100."));

            return issues;
        }

        private static List<ValidationIssue> ValidateBoss(BossDefinition boss, IDictionary<string, EnemyArchetypeDefinition> archetypes)
        {
            var issues = new List<ValidationIssue>();

            if (string.IsNullOrWhiteSpace(boss.DisplayName))
                issues.Add(new ValidationIssue("DisplayName", "DisplayName is required."));
            if (string.IsNullOrWhiteSpace(boss.ArchetypeId))
                issues.Add(new ValidationIssue("ArchetypeId", "ArchetypeId is required."));
            else if (archetypes == null || !archetypes.ContainsKey(boss.ArchetypeId))
                issues.Add(new ValidationIssue("ArchetypeId", string.Concat("Unknown Boss ArchetypeId '", boss.ArchetypeId, "'.")));
            if (boss.IntroSeconds < 0f)
                issues.Add(new ValidationIssue("IntroSeconds", "IntroSeconds cannot be negative."));
            if (boss.TargetY < 0.1f || boss.TargetY > 0.9f)
                issues.Add(new ValidationIssue("TargetY", "TargetY must be between 0.1 and 0.9."));
            if (boss.ArenaScrollSpeed < 0f)
                issues.Add(new ValidationIssue("ArenaScrollSpeed", "ArenaScrollSpeed cannot be negative."));
            if (boss.HitPoints <= 0)
                issues.Add(new ValidationIssue("HitPoints", "HitPoints must be greater than zero."));
            if (boss.PhaseThresholds == null || boss.PhaseThresholds.Count == 0)
                issues.Add(new ValidationIssue("PhaseThresholds", "At least one phase threshold is required."));
            else
            {
                float last = 1.1f;
                for (int i = 0; i < boss.PhaseThresholds.Count; i++)
                {
                    float threshold = boss.PhaseThresholds[i];
                    if (threshold <= 0f || threshold >= 1f)
                        issues.Add(new ValidationIssue(string.Concat("PhaseThresholds[", i.ToString(), "]"), "Thresholds must be between 0 and 1."));
                    if (threshold >= last)
                        issues.Add(new ValidationIssue(string.Concat("PhaseThresholds[", i.ToString(), "]"), "Thresholds must descend from high to low."));

                    last = threshold;
                }
            }

            return issues;
        }
    }
}
