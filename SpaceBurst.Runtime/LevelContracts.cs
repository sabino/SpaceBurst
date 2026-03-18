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

    public enum MovePattern
    {
        StraightFlyIn,
        SineWave,
        Dive,
        RetreatBackfire,
        TurretCarrier,
        BossOrbit,
        BossCharge,
    }

    public enum FirePattern
    {
        None,
        ForwardPulse,
        SpreadPulse,
        AimedShot,
        BossFan,
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
        public string DisplayName { get; set; }
        public float RenderScale { get; set; } = 1f;
        public float MoveSpeed { get; set; } = 240f;
        public int HitPoints { get; set; } = 6;
        public int ScoreValue { get; set; } = 100;
        public float SpawnLeadDistance { get; set; } = 280f;
        public float FireIntervalSeconds { get; set; } = 0f;
        public float MovementAmplitude { get; set; } = 64f;
        public float MovementFrequency { get; set; } = 1f;
        public MovePattern MovePattern { get; set; } = MovePattern.StraightFlyIn;
        public FirePattern FirePattern { get; set; } = FirePattern.None;
        public ProceduralSpriteDefinition Sprite { get; set; } = new ProceduralSpriteDefinition();
        public DamageMaskDefinition DamageMask { get; set; } = new DamageMaskDefinition();
    }

    public sealed class ProceduralSpriteDefinition
    {
        public string Id { get; set; }
        public int PixelScale { get; set; } = 4;
        public string PrimaryColor { get; set; } = "#D9F1FF";
        public string SecondaryColor { get; set; } = "#76BEDA";
        public string AccentColor { get; set; } = "#F4B860";
        public List<string> Rows { get; set; } = new List<string>();
        public VitalCoreMaskDefinition VitalCore { get; set; } = new VitalCoreMaskDefinition();
    }

    public sealed class VitalCoreMaskDefinition
    {
        public List<string> Rows { get; set; } = new List<string>();
    }

    public sealed class DamageMaskDefinition
    {
        public int ContactDamage { get; set; } = 1;
        public int ProjectileDamage { get; set; } = 1;
        public int DamageRadius { get; set; } = 1;
        public int IntegrityThresholdPercent { get; set; } = 15;
    }

    public sealed class SpawnGroupDefinition
    {
        public string ArchetypeId { get; set; }
        public float StartSeconds { get; set; }
        public int Lane { get; set; } = 2;
        public float? TargetY { get; set; }
        public int Count { get; set; } = 1;
        public float SpawnLeadDistance { get; set; } = 280f;
        public float SpawnIntervalSeconds { get; set; } = 0.4f;
        public float SpacingX { get; set; } = 88f;
        public float SpeedMultiplier { get; set; } = 1f;
        public MovePattern? MovePatternOverride { get; set; }
        public FirePattern? FirePatternOverride { get; set; }
        public float Amplitude { get; set; } = 64f;
        public float Frequency { get; set; } = 1f;
    }

    public sealed class SectionDefinition
    {
        public string Label { get; set; }
        public float StartSeconds { get; set; }
        public float DurationSeconds { get; set; } = 12f;
        public bool Checkpoint { get; set; }
        public List<SpawnGroupDefinition> Groups { get; set; } = new List<SpawnGroupDefinition>();
    }

    public sealed class BossDefinition
    {
        public BossType Type { get; set; }
        public string DisplayName { get; set; }
        public string ArchetypeId { get; set; }
        public float IntroSeconds { get; set; } = 1.25f;
        public float TargetY { get; set; } = 0.5f;
        public float ArenaScrollSpeed { get; set; } = 70f;
        public int HitPoints { get; set; } = 160;
        public List<float> PhaseThresholds { get; set; } = new List<float> { 0.75f, 0.5f, 0.25f };
        public MovePattern MovePattern { get; set; } = MovePattern.BossOrbit;
        public FirePattern FirePattern { get; set; } = FirePattern.BossFan;
    }

    public sealed class StageDefinition
    {
        public int StageNumber { get; set; }
        public string Name { get; set; }
        public float IntroSeconds { get; set; } = 1.25f;
        public float ScrollSpeed { get; set; } = 180f;
        public string Theme { get; set; } = "Nebula";
        public int BackgroundSeed { get; set; } = 1;
        public List<float> CheckpointMarkers { get; set; } = new List<float>();
        public List<SectionDefinition> Sections { get; set; } = new List<SectionDefinition>();
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
