using SpaceBurst.RuntimeData;
using System;
using System.Collections.Generic;

namespace SpaceBurst
{
    sealed class WeaponInventorySnapshotData
    {
        public WeaponStyleId ActiveStyle { get; set; } = WeaponStyleId.Pulse;
        public Dictionary<WeaponStyleId, int> StyleLevels { get; set; } = new Dictionary<WeaponStyleId, int>();
        public Dictionary<WeaponStyleId, int> StyleRanks { get; set; } = new Dictionary<WeaponStyleId, int>();
        public Dictionary<WeaponStyleId, int> StyleCharges { get; set; } = new Dictionary<WeaponStyleId, int>();
        public int StoredUpgradeCharges { get; set; }
    }

    sealed class PowerupDropSnapshotData
    {
        public int EligibleKillsSinceLastDrop { get; set; }
    }

    sealed class PlayerRunProgressSnapshotData
    {
        public GameDifficulty Difficulty { get; set; } = GameDifficulty.Easy;
        public int StartingLives { get; set; } = 3;
        public int ShipsPerLife { get; set; } = 2;
        public bool MedalEligible { get; set; } = true;
        public int NonWeaponUpgradeCount { get; set; }
        public float MoveSpeedMultiplier { get; set; } = 1f;
        public float RewindEfficiency { get; set; }
        public float DropBonusChance { get; set; }
        public WeaponInventorySnapshotData Weapons { get; set; } = new WeaponInventorySnapshotData();
        public PowerupDropSnapshotData Powerups { get; set; } = new PowerupDropSnapshotData();
    }

    sealed class PlayerStatusSnapshotData
    {
        public int Lives { get; set; }
        public int Ships { get; set; }
        public int Score { get; set; }
        public int Multiplier { get; set; } = 1;
        public float MultiplierTimeLeft { get; set; }
        public int ScoreForExtraLife { get; set; }
        public PlayerRunProgressSnapshotData RunProgress { get; set; } = new PlayerRunProgressSnapshotData();
    }

    sealed class MaskSnapshotData
    {
        public List<string> OccupiedRows { get; set; } = new List<string>();
        public List<string> CoreRows { get; set; } = new List<string>();
    }

    sealed class PlayerSnapshotData
    {
        public long EntityId { get; set; }
        public Vector2Data Position { get; set; } = new Vector2Data();
        public Vector2Data Velocity { get; set; } = new Vector2Data();
        public Vector3Data CombatPosition { get; set; } = new Vector3Data();
        public Vector3Data CombatVelocity { get; set; } = new Vector3Data();
        public Vector2Data CannonDirection { get; set; } = new Vector2Data(1f, 0f);
        public Vector2Data KnockbackVelocity { get; set; } = new Vector2Data();
        public Vector2Data PendingRespawnPosition { get; set; } = new Vector2Data();
        public Vector2Data ChaseReticle { get; set; } = new Vector2Data();
        public Vector3Data ChaseEntryRecenterTarget { get; set; } = new Vector3Data();
        public float ChaseEntryRecenterTimer { get; set; }
        public float RespawnTimer { get; set; }
        public float InvulnerabilityTimer { get; set; }
        public float FireCooldown { get; set; }
        public float DroneSupportTimer { get; set; }
        public bool HullDestroyedQueued { get; set; }
        public float HullIntegrityRatio { get; set; } = 1f;
        public MaskSnapshotData HullMask { get; set; } = new MaskSnapshotData();
    }

    sealed class EnemySnapshotData
    {
        public long EntityId { get; set; }
        public string ArchetypeId { get; set; }
        public Vector2Data Position { get; set; } = new Vector2Data();
        public Vector2Data Velocity { get; set; } = new Vector2Data();
        public Vector3Data CombatPosition { get; set; } = new Vector3Data();
        public Vector3Data CombatVelocity { get; set; } = new Vector3Data();
        public float TargetY { get; set; }
        public MovePattern MovePattern { get; set; }
        public FirePattern FirePattern { get; set; }
        public float SpeedMultiplier { get; set; } = 1f;
        public float MovementAmplitude { get; set; }
        public float MovementFrequency { get; set; } = 1f;
        public float DepthAnchor { get; set; }
        public float DepthAmplitude { get; set; }
        public float DepthFrequency { get; set; } = 1f;
        public float AgeSeconds { get; set; }
        public float FlashTimer { get; set; }
        public float FireCooldown { get; set; }
        public float PhaseOffset { get; set; }
        public bool IsBoss { get; set; }
        public int BossPhase { get; set; }
        public float BossSweepDirection { get; set; }
        public float BossFireCooldown { get; set; }
        public bool ReentryRollConsumed { get; set; }
        public bool WasReentrySpawn { get; set; }
        public List<SpawnGroupDefinition> PendingSupportGroups { get; set; } = new List<SpawnGroupDefinition>();
        public MaskSnapshotData Mask { get; set; } = new MaskSnapshotData();
    }

    sealed class BossDefinitionSnapshotData
    {
        public string VariantId { get; set; } = string.Empty;
        public BossType Type { get; set; } = BossType.DestroyerBoss;
        public string DisplayName { get; set; } = string.Empty;
        public string ArchetypeId { get; set; } = string.Empty;
        public float IntroSeconds { get; set; } = 1.25f;
        public float TargetY { get; set; } = 0.5f;
        public float ArenaScrollSpeed { get; set; } = 70f;
        public int HitPoints { get; set; } = 160;
        public bool AllowRandomEvents { get; set; }
        public float PresentationScale { get; set; } = 1f;
        public float ScreenCoverageTarget { get; set; }
        public List<float> PhaseThresholds { get; set; } = new List<float>();
        public List<RandomEventType> HazardOverrides { get; set; } = new List<RandomEventType>();
        public BackgroundMoodDefinition MoodOverride { get; set; } = new BackgroundMoodDefinition();
        public MovePattern MovePattern { get; set; } = MovePattern.BossOrbit;
        public FirePattern FirePattern { get; set; } = FirePattern.BossFan;
    }

    sealed class BulletSnapshotData
    {
        public long EntityId { get; set; }
        public Vector2Data Position { get; set; } = new Vector2Data();
        public Vector2Data PreviousPosition { get; set; } = new Vector2Data();
        public Vector2Data Velocity { get; set; } = new Vector2Data();
        public Vector3Data CombatPosition { get; set; } = new Vector3Data();
        public Vector3Data PreviousCombatPosition { get; set; } = new Vector3Data();
        public Vector3Data CombatVelocity { get; set; } = new Vector3Data();
        public bool Friendly { get; set; }
        public int Damage { get; set; }
        public int RemainingPierceHits { get; set; }
        public float RemainingLifetime { get; set; } = 2f;
        public float HomingStrength { get; set; }
        public float HomingDelayRemaining { get; set; }
        public float ExplosionRadius { get; set; }
        public int ChainCount { get; set; }
        public float RenderScale { get; set; } = 1f;
        public ProjectileBehavior ProjectileBehavior { get; set; } = ProjectileBehavior.Bolt;
        public TrailFxStyle TrailFxStyle { get; set; } = TrailFxStyle.None;
        public ImpactFxStyle ImpactFxStyle { get; set; } = ImpactFxStyle.Standard;
        public ProceduralSpriteDefinition SpriteDefinition { get; set; } = new ProceduralSpriteDefinition();
        public ImpactProfileDefinition ImpactProfile { get; set; } = new ImpactProfileDefinition();
    }

    sealed class BeamSnapshotData
    {
        public long EntityId { get; set; }
        public Vector2Data Origin { get; set; } = new Vector2Data();
        public Vector2Data Direction { get; set; } = new Vector2Data(1f, 0f);
        public Vector3Data CombatOrigin { get; set; } = new Vector3Data();
        public Vector3Data CombatDirection { get; set; } = new Vector3Data(1f, 0f, 0f);
        public float Length { get; set; } = 100f;
        public float Thickness { get; set; } = 8f;
        public float RemainingLifetime { get; set; } = 0.08f;
        public float TickTimer { get; set; }
        public int Damage { get; set; } = 1;
        public bool Friendly { get; set; } = true;
        public ImpactProfileDefinition ImpactProfile { get; set; } = new ImpactProfileDefinition();
        public string PrimaryColor { get; set; } = "#FFFFFF";
        public string AccentColor { get; set; } = "#6EC1FF";
    }

    sealed class PowerupSnapshotData
    {
        public long EntityId { get; set; }
        public Vector2Data Position { get; set; } = new Vector2Data();
        public Vector2Data Velocity { get; set; } = new Vector2Data();
        public Vector3Data CombatPosition { get; set; } = new Vector3Data();
        public Vector3Data CombatVelocity { get; set; } = new Vector3Data();
        public float AgeSeconds { get; set; }
        public WeaponStyleId StyleId { get; set; } = WeaponStyleId.Pulse;
    }

    sealed class ScheduledSpawnSnapshotData
    {
        public float SpawnAtSeconds { get; set; }
        public SpawnGroupDefinition Group { get; set; } = new SpawnGroupDefinition();
        public Vector2Data SpawnPoint { get; set; } = new Vector2Data();
        public Vector3Data CombatSpawnPoint { get; set; } = new Vector3Data();
        public float TargetY { get; set; }
        public MovePattern MovePattern { get; set; }
        public FirePattern FirePattern { get; set; }
        public float Amplitude { get; set; }
        public float Frequency { get; set; }
        public float DepthAnchor { get; set; }
        public float DepthAmplitude { get; set; }
        public float DepthFrequency { get; set; } = 1f;
        public float SpeedMultiplier { get; set; } = 1f;
    }

    sealed class ScheduledEventSnapshotData
    {
        public float TriggerAtSeconds { get; set; }
        public RandomEventWindowDefinition Window { get; set; } = new RandomEventWindowDefinition();
    }

    sealed class ReentryTicketSnapshotData
    {
        public float TriggerAtSeconds { get; set; }
        public Vector2Data SpawnPoint { get; set; } = new Vector2Data();
        public Vector3Data CombatSpawnPoint { get; set; } = new Vector3Data();
        public float TargetY { get; set; }
        public float SpeedMultiplier { get; set; } = 1f;
        public float Amplitude { get; set; }
        public float Frequency { get; set; } = 1f;
        public float DepthAnchor { get; set; }
        public float DepthAmplitude { get; set; }
        public float DepthFrequency { get; set; } = 1f;
        public EnemySnapshotData Enemy { get; set; } = new EnemySnapshotData();
    }

    sealed class SaveSlotSummary
    {
        public int SlotIndex { get; set; }
        public bool HasData { get; set; }
        public int StageNumber { get; set; }
        public string StageName { get; set; } = string.Empty;
        public GameDifficulty Difficulty { get; set; } = GameDifficulty.Easy;
        public int Score { get; set; }
        public string SavedAtUtc { get; set; } = string.Empty;
        public string ActiveStyle { get; set; } = string.Empty;
    }

    sealed class RunSaveData
    {
        public SaveSlotSummary Summary { get; set; } = new SaveSlotSummary();
        public int CurrentStageNumber { get; set; }
        public int CurrentSectionIndex { get; set; }
        public GameDifficulty Difficulty { get; set; } = GameDifficulty.Easy;
        public ViewMode ViewMode { get; set; } = ViewMode.SideScroller;
        public PresentationTier PresentationTier { get; set; } = PresentationTier.Pixel2D;
        public GameFlowState State { get; set; } = GameFlowState.Paused;
        public GameFlowState HelpReturnState { get; set; } = GameFlowState.Title;
        public GameFlowState DraftReturnState { get; set; } = GameFlowState.Playing;
        public float StageElapsedSeconds { get; set; }
        public float StateTimer { get; set; }
        public float ActiveEventTimer { get; set; }
        public float ActiveEventSpawnTimer { get; set; }
        public float RewindMeterSeconds { get; set; }
        public float RewindHoldSeconds { get; set; }
        public float RewindAccumulatorSeconds { get; set; }
        public bool StageHadDeath { get; set; }
        public bool CampaignHadDeath { get; set; }
        public string BannerText { get; set; } = string.Empty;
        public string ActiveEventWarning { get; set; } = string.Empty;
        public RandomEventType ActiveEventType { get; set; } = RandomEventType.None;
        public float ActiveEventIntensity { get; set; }
        public bool HasActiveBoss { get; set; }
        public bool PendingBossSpawn { get; set; }
        public BossDefinitionSnapshotData ActiveBossDefinition { get; set; } = new BossDefinitionSnapshotData();
        public int TransitionTargetStageNumber { get; set; }
        public bool TransitionToBoss { get; set; }
        public float TransitionScrollFrom { get; set; }
        public float TransitionScrollTo { get; set; }
        public float TransitionHudBlend { get; set; }
        public float BossApproachTimer { get; set; }
        public float DraftTimer { get; set; }
        public int DraftSelection { get; set; }
        public WeaponStyleId DraftChargeStyle { get; set; } = WeaponStyleId.Pulse;
        public bool DraftFromTutorial { get; set; }
        public bool TutorialReplayMode { get; set; }
        public TutorialStep TutorialStep { get; set; } = TutorialStep.Move;
        public float TutorialProgressSeconds { get; set; }
        public List<UpgradeDraftCard> DraftCards { get; set; } = new List<UpgradeDraftCard>();
        public PlayerStatusSnapshotData PlayerStatus { get; set; } = new PlayerStatusSnapshotData();
        public PlayerSnapshotData Player { get; set; } = new PlayerSnapshotData();
        public List<EnemySnapshotData> Enemies { get; set; } = new List<EnemySnapshotData>();
        public List<BulletSnapshotData> Bullets { get; set; } = new List<BulletSnapshotData>();
        public List<BeamSnapshotData> Beams { get; set; } = new List<BeamSnapshotData>();
        public List<PowerupSnapshotData> Powerups { get; set; } = new List<PowerupSnapshotData>();
        public List<ScheduledSpawnSnapshotData> ScheduledSpawns { get; set; } = new List<ScheduledSpawnSnapshotData>();
        public List<ScheduledEventSnapshotData> ScheduledEvents { get; set; } = new List<ScheduledEventSnapshotData>();
        public List<ReentryTicketSnapshotData> ReentryTickets { get; set; } = new List<ReentryTicketSnapshotData>();
        public uint GameplayRngState { get; set; } = 1;
    }

    sealed class Vector2Data
    {
        public float X { get; set; }
        public float Y { get; set; }

        public Vector2Data()
        {
        }

        public Vector2Data(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    sealed class Vector3Data
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public Vector3Data()
        {
        }

        public Vector3Data(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
