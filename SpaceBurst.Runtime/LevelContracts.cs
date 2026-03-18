using System.Collections.Generic;

namespace SpaceBurst.RuntimeData
{
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

    public enum ImpactKernelShape
    {
        Point,
        Diamond3,
        Diamond5,
        Cross3,
        Blast5,
    }

    public enum RandomEventType
    {
        None,
        MeteorShower,
        DebrisDrift,
        CometSwarm,
        SolarFlare,
    }

    public enum WeaponStyleId
    {
        Pulse,
        Spread,
        Laser,
        Plasma,
        Missile,
        Rail,
        Arc,
        Blade,
        Drone,
        Fortress,
    }

    public enum FireMode
    {
        PulseBurst,
        SpreadShotgun,
        BeamBurst,
        PlasmaOrb,
        MissileLauncher,
        RailBurst,
        ArcChain,
        BladeWave,
        DroneCommand,
        FortressPulse,
    }

    public enum ProjectileBehavior
    {
        Bolt,
        Beam,
        PlasmaOrb,
        Missile,
        RailSlug,
        ArcBolt,
        BladeWave,
        DroneBolt,
        ShieldPulse,
    }

    public enum MuzzleFxStyle
    {
        None,
        Pulse,
        Spread,
        Laser,
        Plasma,
        Missile,
        Rail,
        Arc,
        Blade,
        Drone,
        Fortress,
    }

    public enum TrailFxStyle
    {
        None,
        Streak,
        Beam,
        Plasma,
        Smoke,
        Neon,
        Electric,
        Shield,
    }

    public enum ImpactFxStyle
    {
        Standard,
        Pulse,
        Spread,
        Beam,
        Plasma,
        Missile,
        Rail,
        Arc,
        Blade,
        Drone,
        Fortress,
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
        public float PowerupWeight { get; set; } = 1f;
        public bool PowerupEligible { get; set; } = true;
        public bool DestroyOnCoreBreach { get; set; } = true;
        public bool ShowDurabilityBar { get; set; }
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

    public sealed class ImpactProfileDefinition
    {
        public string Name { get; set; } = "Standard";
        public ImpactKernelShape Kernel { get; set; } = ImpactKernelShape.Diamond3;
        public int BaseCellsRemoved { get; set; } = 3;
        public int BonusCellsPerDamage { get; set; } = 1;
        public int SplashRadius { get; set; } = 0;
        public int SplashPercent { get; set; } = 0;
        public int DebrisBurstCount { get; set; } = 8;
        public float DebrisSpeed { get; set; } = 140f;
    }

    public sealed class DamageMaskDefinition
    {
        public int ContactDamage { get; set; } = 1;
        public int ProjectileDamage { get; set; } = 1;
        public int DamageRadius { get; set; } = 1;
        public int IntegrityThresholdPercent { get; set; } = 15;
        public float ContactImpulse { get; set; } = 180f;
        public ImpactProfileDefinition ContactImpact { get; set; } = new ImpactProfileDefinition
        {
            Name = "Contact",
            Kernel = ImpactKernelShape.Cross3,
            BaseCellsRemoved = 2,
            BonusCellsPerDamage = 1,
            DebrisBurstCount = 10,
            DebrisSpeed = 110f,
        };
    }

    public sealed class BackgroundMoodDefinition
    {
        public string PrimaryColor { get; set; } = "#0C1122";
        public string SecondaryColor { get; set; } = "#1C3055";
        public string AccentColor { get; set; } = "#6EC1FF";
        public string GlowColor { get; set; } = "#F6C674";
        public float StarDensity { get; set; } = 1f;
        public float DustDensity { get; set; } = 1f;
        public float LightIntensity { get; set; } = 0.7f;
        public float PlanetPresence { get; set; } = 0.5f;
        public float Contrast { get; set; } = 0.8f;
    }

    public sealed class RandomEventWindowDefinition
    {
        public RandomEventType EventType { get; set; } = RandomEventType.None;
        public float StartSeconds { get; set; }
        public float DurationSeconds { get; set; } = 4f;
        public float Weight { get; set; } = 1f;
        public float Intensity { get; set; } = 1f;
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
        public float PowerDropBonusChance { get; set; }
        public float ScrollMultiplier { get; set; } = 1f;
        public float EnemySpeedMultiplier { get; set; } = 1f;
        public BackgroundMoodDefinition Mood { get; set; } = new BackgroundMoodDefinition();
        public List<RandomEventWindowDefinition> EventWindows { get; set; } = new List<RandomEventWindowDefinition>();
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
        public bool AllowRandomEvents { get; set; }
        public List<float> PhaseThresholds { get; set; } = new List<float> { 0.75f, 0.5f, 0.25f };
        public List<RandomEventType> HazardOverrides { get; set; } = new List<RandomEventType>();
        public BackgroundMoodDefinition MoodOverride { get; set; } = new BackgroundMoodDefinition();
        public MovePattern MovePattern { get; set; } = MovePattern.BossOrbit;
        public FirePattern FirePattern { get; set; } = FirePattern.BossFan;
    }

    public sealed class StageDefinition
    {
        public int StageNumber { get; set; }
        public string Name { get; set; }
        public float IntroSeconds { get; set; } = 1.25f;
        public float ScrollSpeed { get; set; } = 180f;
        public float BaseScrollSpeed { get; set; }
        public string Theme { get; set; } = "Nebula";
        public int BackgroundSeed { get; set; } = 1;
        public int StartingLives { get; set; } = 3;
        public int ShipsPerLife { get; set; } = 2;
        public BackgroundMoodDefinition BackgroundMood { get; set; } = new BackgroundMoodDefinition();
        public List<float> CheckpointMarkers { get; set; } = new List<float>();
        public List<SectionDefinition> Sections { get; set; } = new List<SectionDefinition>();
        public BossDefinition Boss { get; set; }
    }

    public sealed class WeaponLevelDefinition
    {
        public float FireIntervalSeconds { get; set; } = 0.12f;
        public float ProjectileSpeed { get; set; } = 720f;
        public int ProjectileDamage { get; set; } = 1;
        public int ProjectileCount { get; set; } = 1;
        public float SpreadDegrees { get; set; }
        public bool Pierce { get; set; }
        public int PierceCount { get; set; }
        public float ProjectileLifetimeSeconds { get; set; } = 2.4f;
        public float ProjectileScale { get; set; } = 1f;
        public float HomingDelaySeconds { get; set; }
        public float ExplosionRadius { get; set; }
        public int ChainCount { get; set; }
        public int DroneCount { get; set; }
        public float DroneIntervalSeconds { get; set; } = 0.45f;
        public float BeamDurationSeconds { get; set; }
        public float BeamThickness { get; set; } = 12f;
        public int BeamTickDamage { get; set; } = 1;
        public FireMode FireMode { get; set; } = FireMode.PulseBurst;
        public ProjectileBehavior ProjectileBehavior { get; set; } = ProjectileBehavior.Bolt;
        public MuzzleFxStyle MuzzleFxStyle { get; set; } = MuzzleFxStyle.Pulse;
        public TrailFxStyle TrailFxStyle { get; set; } = TrailFxStyle.Streak;
        public ImpactFxStyle ImpactFxStyle { get; set; } = ImpactFxStyle.Standard;
        public ImpactProfileDefinition Impact { get; set; } = new ImpactProfileDefinition();
    }

    public sealed class WeaponStyleDefinition
    {
        public WeaponStyleId Id { get; set; }
        public string DisplayName { get; set; }
        public string PrimaryColor { get; set; } = "#D9F1FF";
        public string SecondaryColor { get; set; } = "#76BEDA";
        public string AccentColor { get; set; } = "#F4B860";
        public List<string> IconRows { get; set; } = new List<string>();
        public List<WeaponLevelDefinition> Levels { get; set; } = new List<WeaponLevelDefinition>();
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
