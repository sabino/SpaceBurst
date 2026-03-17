using System.Collections.Generic;

namespace SpaceBurst.RuntimeData
{
    public enum RetryMode
    {
        ClassicStageRestart,
        WaveCheckpoint,
        CasualRespawn,
    }

    public enum MedalId
    {
        StageClear,
        NoDeath,
        BossClear,
        CampaignClear,
        PerfectCampaign,
    }

    public enum FormationType
    {
        Line,
        Column,
        V,
        Arc,
        Ring,
    }

    public enum EntrySide
    {
        Top,
        Bottom,
        Left,
        Right,
    }

    public enum PathType
    {
        Straight,
        Swoop,
        LaneSweep,
        ChaseAfterDelay,
        OrbitAnchor,
    }

    public enum BossType
    {
        DestroyerBoss,
        WalkerBoss,
        DestroyerBossMk2,
        WalkerBossMk2,
        FinalBoss,
    }

    public sealed class EnemyArchetypeCatalogDefinition
    {
        public List<EnemyArchetypeDefinition> Archetypes { get; set; } = new List<EnemyArchetypeDefinition>();
    }

    public sealed class EnemyArchetypeDefinition
    {
        public string Id { get; set; }
        public string Texture { get; set; }
        public float RenderScale { get; set; } = 1f;
        public float CollisionRadius { get; set; } = 20f;
        public float Speed { get; set; } = 3f;
        public int HitPoints { get; set; } = 1;
        public int ScoreValue { get; set; } = 1;
        public float SpawnDelaySeconds { get; set; } = 0.75f;
    }

    public sealed class SpawnGroupDefinition
    {
        public string ArchetypeId { get; set; }
        public int Count { get; set; } = 1;
        public FormationType Formation { get; set; } = FormationType.Line;
        public EntrySide EntrySide { get; set; } = EntrySide.Top;
        public PathType PathType { get; set; } = PathType.Straight;
        public float AnchorX { get; set; } = 0.5f;
        public float AnchorY { get; set; } = 0.3f;
        public float Spacing { get; set; } = 70f;
        public float DelayBetweenSpawns { get; set; } = 0.2f;
        public float TravelDuration { get; set; } = 3.5f;
        public float SpeedMultiplier { get; set; } = 1f;
    }

    public sealed class WaveDefinition
    {
        public string Label { get; set; }
        public float StartSeconds { get; set; }
        public bool Checkpoint { get; set; }
        public List<SpawnGroupDefinition> Groups { get; set; } = new List<SpawnGroupDefinition>();
    }

    public sealed class BossDefinition
    {
        public BossType Type { get; set; }
        public string DisplayName { get; set; }
        public string ArchetypeId { get; set; }
        public EntrySide EntrySide { get; set; } = EntrySide.Top;
        public float AnchorX { get; set; } = 0.5f;
        public float AnchorY { get; set; } = 0.24f;
        public float RenderScale { get; set; } = 1.8f;
        public int HitPoints { get; set; } = 80;
    }

    public sealed class LevelDefinition
    {
        public int LevelNumber { get; set; }
        public string Name { get; set; }
        public float IntroSeconds { get; set; } = 1.5f;
        public List<WaveDefinition> Waves { get; set; } = new List<WaveDefinition>();
        public BossDefinition Boss { get; set; }
    }

    public sealed class ValidationIssue
    {
        public string Path { get; set; }
        public string Message { get; set; }

        public ValidationIssue()
        {
        }

        public ValidationIssue(string path, string message)
        {
            Path = path;
            Message = message;
        }

        public override string ToString()
        {
            return string.Concat(Path, ": ", Message);
        }
    }
}
