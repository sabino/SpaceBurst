using System.Collections.Generic;

namespace SpaceBurst.RuntimeData
{
    /// <summary>
    /// Identifies the campaign medals that can be unlocked during a run.
    /// </summary>
    public enum MedalId
    {
        StageClear,
        NoDeath,
        BossClear,
        CampaignClear,
        PerfectCampaign,
    }

    /// <summary>
    /// Enumerates the authored enemy and boss movement patterns used by stage data.
    /// </summary>
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

    /// <summary>
    /// Enumerates the authored firing patterns used by enemies and bosses.
    /// </summary>
    public enum FirePattern
    {
        None,
        ForwardPulse,
        SpreadPulse,
        AimedShot,
        BossFan,
    }

    /// <summary>
    /// Identifies the built-in boss archetypes for milestone stages.
    /// </summary>
    public enum BossType
    {
        DestroyerBoss,
        WalkerBoss,
        DestroyerBossMk2,
        WalkerBossMk2,
        FinalBoss,
        CombinedBossSixth,
    }

    /// <summary>
    /// Describes the crater shape applied to a destructible mask when a hit lands.
    /// </summary>
    public enum ImpactKernelShape
    {
        Point,
        Diamond3,
        Diamond5,
        Cross3,
        Blast5,
    }

    /// <summary>
    /// Lists the semi-authored random event families that can appear in stage sections.
    /// </summary>
    public enum RandomEventType
    {
        None,
        MeteorShower,
        DebrisDrift,
        CometSwarm,
        SolarFlare,
    }

    /// <summary>
    /// Describes the active presentation fidelity layer used for a stage.
    /// </summary>
    public enum PresentationTier
    {
        Pixel2D,
        VoxelShell,
        HybridMesh,
        Late3D,
    }

    /// <summary>
    /// Describes the player-facing camera presentation mode.
    /// </summary>
    public enum ViewMode
    {
        SideScroller,
        Chase3D,
    }

    /// <summary>
    /// Lists the player weapon styles that can be unlocked during a run.
    /// </summary>
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

    /// <summary>
    /// Describes the controller behavior used when a weapon style fires.
    /// </summary>
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

    /// <summary>
    /// Describes how spawned projectiles behave after they are emitted.
    /// </summary>
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

    /// <summary>
    /// Selects the muzzle-flash family to use for a weapon level.
    /// </summary>
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

    /// <summary>
    /// Selects the trail-rendering family to use for a projectile or beam.
    /// </summary>
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

    /// <summary>
    /// Selects the impact effect family to use when a hit resolves.
    /// </summary>
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

    /// <summary>
    /// Root JSON object for the enemy archetype catalog.
    /// </summary>
    public sealed class EnemyArchetypeCatalogDefinition
    {
        public List<EnemyArchetypeDefinition> Archetypes { get; set; } = new List<EnemyArchetypeDefinition>();
    }

    /// <summary>
    /// Defines a reusable enemy archetype referenced by stage spawn groups.
    /// </summary>
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

    /// <summary>
    /// Defines the procedural sprite rows and palette for a runtime-generated actor.
    /// </summary>
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

    /// <summary>
    /// Defines the vital core cells used by the destructible damage model.
    /// </summary>
    public sealed class VitalCoreMaskDefinition
    {
        public List<string> Rows { get; set; } = new List<string>();
    }

    /// <summary>
    /// Defines how a hit removes cells and emits debris from a destructible mask.
    /// </summary>
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

    /// <summary>
    /// Defines projectile and contact damage behavior for a destructible actor.
    /// </summary>
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

    /// <summary>
    /// Defines background palette and density parameters for a stage or section.
    /// </summary>
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

    /// <summary>
    /// Defines a weighted random event window inside a section timeline.
    /// </summary>
    public sealed class RandomEventWindowDefinition
    {
        public RandomEventType EventType { get; set; } = RandomEventType.None;
        public float StartSeconds { get; set; }
        public float DurationSeconds { get; set; } = 4f;
        public float Weight { get; set; } = 1f;
        public float Intensity { get; set; } = 1f;
    }

    /// <summary>
    /// Defines one authored enemy spawn group inside a section timeline.
    /// </summary>
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

    /// <summary>
    /// Defines a paced slice of a stage timeline, including mood, events, and groups.
    /// </summary>
    public sealed class SectionDefinition
    {
        public string Label { get; set; }
        public float StartSeconds { get; set; }
        public float DurationSeconds { get; set; } = 12f;
        public bool Checkpoint { get; set; }
        public bool AllowReentryAmbushes { get; set; } = true;
        public float PowerDropBonusChance { get; set; }
        public float ScrollMultiplier { get; set; } = 1f;
        public float EnemySpeedMultiplier { get; set; } = 1f;
        public BackgroundMoodDefinition Mood { get; set; } = new BackgroundMoodDefinition();
        public List<RandomEventWindowDefinition> EventWindows { get; set; } = new List<RandomEventWindowDefinition>();
        public List<SpawnGroupDefinition> Groups { get; set; } = new List<SpawnGroupDefinition>();
    }

    /// <summary>
    /// Defines an optional runtime-selected boss variant for a milestone stage.
    /// </summary>
    public sealed class BossVariantDefinition
    {
        public string Id { get; set; } = string.Empty;
        public BossType Type { get; set; } = BossType.FinalBoss;
        public string DisplayName { get; set; }
        public string ArchetypeId { get; set; }
        public float ChancePercent { get; set; }
        public float PresentationScaleMultiplier { get; set; } = 1f;
        public float HitPointMultiplier { get; set; } = 1f;
    }

    /// <summary>
    /// Defines the boss encounter for a milestone stage.
    /// </summary>
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
        public float PresentationScale { get; set; } = 1f;
        public float ScreenCoverageTarget { get; set; }
        public List<float> PhaseThresholds { get; set; } = new List<float> { 0.75f, 0.5f, 0.25f };
        public List<RandomEventType> HazardOverrides { get; set; } = new List<RandomEventType>();
        public List<BossVariantDefinition> Variants { get; set; } = new List<BossVariantDefinition>();
        public BackgroundMoodDefinition MoodOverride { get; set; } = new BackgroundMoodDefinition();
        public MovePattern MovePattern { get; set; } = MovePattern.BossOrbit;
        public FirePattern FirePattern { get; set; } = FirePattern.BossFan;
    }

    /// <summary>
    /// Defines one full stage in the campaign.
    /// </summary>
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
        public PresentationTier? PresentationTierOverride { get; set; }
        public bool EnableChaseView { get; set; } = true;
        public BackgroundMoodDefinition BackgroundMood { get; set; } = new BackgroundMoodDefinition();
        public List<float> CheckpointMarkers { get; set; } = new List<float>();
        public List<SectionDefinition> Sections { get; set; } = new List<SectionDefinition>();
        public BossDefinition Boss { get; set; }
    }

    /// <summary>
    /// Defines one upgrade tier for a player weapon style.
    /// </summary>
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
        public float BeamLength { get; set; } = 520f;
        public int BeamCount { get; set; } = 1;
        public float BeamSpacing { get; set; }
        public float BeamThickness { get; set; } = 12f;
        public int BeamTickDamage { get; set; } = 1;
        public FireMode FireMode { get; set; } = FireMode.PulseBurst;
        public ProjectileBehavior ProjectileBehavior { get; set; } = ProjectileBehavior.Bolt;
        public MuzzleFxStyle MuzzleFxStyle { get; set; } = MuzzleFxStyle.Pulse;
        public TrailFxStyle TrailFxStyle { get; set; } = TrailFxStyle.Streak;
        public ImpactFxStyle ImpactFxStyle { get; set; } = ImpactFxStyle.Standard;
        public ImpactProfileDefinition Impact { get; set; } = new ImpactProfileDefinition();
    }

    /// <summary>
    /// Defines the icon, palette, and upgrade levels for a player weapon style.
    /// </summary>
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

    /// <summary>
    /// Represents a validation problem discovered while loading runtime content.
    /// </summary>
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
