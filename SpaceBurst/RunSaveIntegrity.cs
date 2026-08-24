using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SpaceBurst
{
    static class RunSaveIntegrity
    {
        public static void Seal(RunSaveData save, JsonSerializerOptions jsonOptions)
        {
            if (save == null)
                throw new ArgumentNullException(nameof(save));
            if (jsonOptions == null)
                throw new ArgumentNullException(nameof(jsonOptions));

            Normalize(save);
            save.SchemaVersion = RunSaveData.CurrentSchemaVersion;
            save.IntegrityHash = string.Empty;
            save.IntegrityHash = ComputeHash(save, jsonOptions);
        }

        public static bool TryPrepareForLoad(RunSaveData save, JsonSerializerOptions jsonOptions, out string warning)
        {
            warning = string.Empty;
            if (save == null || jsonOptions == null)
                return false;

            if (save.SchemaVersion < 0 || save.SchemaVersion > RunSaveData.CurrentSchemaVersion)
            {
                warning = "SAVE VERSION IS NEWER THAN THIS BUILD";
                return false;
            }

            if (save.SchemaVersion == RunSaveData.CurrentSchemaVersion)
            {
                if (string.IsNullOrWhiteSpace(save.IntegrityHash))
                {
                    warning = "SAVE INTEGRITY STAMP IS MISSING";
                    return false;
                }

                string expectedHash = save.IntegrityHash;
                save.IntegrityHash = string.Empty;
                string actualHash = ComputeHash(save, jsonOptions);
                save.IntegrityHash = expectedHash;
                if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
                {
                    warning = "SAVE INTEGRITY CHECK FAILED";
                    return false;
                }
            }
            else
            {
                warning = "LEGACY SAVE MIGRATED";
            }

            if (save.CurrentStageNumber < 1 || save.CurrentStageNumber > 50)
            {
                warning = "SAVE STAGE IS OUT OF RANGE";
                return false;
            }

            Normalize(save);
            save.SchemaVersion = RunSaveData.CurrentSchemaVersion;
            save.IntegrityHash = string.Empty;
            save.IntegrityHash = ComputeHash(save, jsonOptions);
            return true;
        }

        private static string ComputeHash(RunSaveData save, JsonSerializerOptions jsonOptions)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(save, jsonOptions));
            return Convert.ToHexString(SHA256.HashData(bytes));
        }

        private static void Normalize(RunSaveData save)
        {
            save.CurrentStageNumber = Math.Clamp(save.CurrentStageNumber <= 0 ? 1 : save.CurrentStageNumber, 1, 50);
            save.CurrentSectionIndex = Math.Max(0, save.CurrentSectionIndex);
            save.Difficulty = IsDefined(save.Difficulty) ? save.Difficulty : GameDifficulty.Easy;
            save.ViewMode = IsDefined(save.ViewMode) ? save.ViewMode : RuntimeData.ViewMode.SideScroller;
            save.PresentationTier = IsDefined(save.PresentationTier) ? save.PresentationTier : RuntimeData.PresentationTier.Pixel2D;
            save.State = IsDefined(save.State) ? save.State : GameFlowState.Paused;
            save.HelpReturnState = IsDefined(save.HelpReturnState) ? save.HelpReturnState : GameFlowState.Title;
            save.DraftReturnState = IsDefined(save.DraftReturnState) ? save.DraftReturnState : GameFlowState.Playing;
            save.StageElapsedSeconds = ClampFinite(save.StageElapsedSeconds, 0f, 86400f);
            save.StateTimer = ClampFinite(save.StateTimer, 0f, 3600f);
            save.ActiveEventTimer = ClampFinite(save.ActiveEventTimer, 0f, 3600f);
            save.ActiveEventSpawnTimer = ClampFinite(save.ActiveEventSpawnTimer, 0f, 3600f);
            save.RewindMeterSeconds = ClampFinite(save.RewindMeterSeconds, 0f, 8f);
            save.RewindHoldSeconds = ClampFinite(save.RewindHoldSeconds, 0f, 60f);
            save.RewindAccumulatorSeconds = ClampFinite(save.RewindAccumulatorSeconds, 0f, 60f);
            save.ActiveEventType = IsDefined(save.ActiveEventType) ? save.ActiveEventType : RuntimeData.RandomEventType.None;
            save.ActiveEventIntensity = ClampFinite(save.ActiveEventIntensity, 0f, 4f);
            save.TransitionTargetStageNumber = Math.Clamp(save.TransitionTargetStageNumber <= 0 ? save.CurrentStageNumber : save.TransitionTargetStageNumber, 1, 50);
            save.TransitionScrollFrom = ClampFinite(save.TransitionScrollFrom, 0f, 2000f);
            save.TransitionScrollTo = ClampFinite(save.TransitionScrollTo, 0f, 2000f);
            save.TransitionHudBlend = ClampFinite(save.TransitionHudBlend, 0f, 1f);
            save.BossApproachTimer = ClampFinite(save.BossApproachTimer, 0f, 120f);
            save.DraftTimer = ClampFinite(save.DraftTimer, 0f, 120f);
            save.DraftSelection = Math.Clamp(save.DraftSelection, 0, 2);
            save.DraftChargeStyle = IsDefined(save.DraftChargeStyle) ? save.DraftChargeStyle : RuntimeData.WeaponStyleId.Pulse;
            save.TutorialStep = IsDefined(save.TutorialStep) ? save.TutorialStep : TutorialStep.Move;
            save.TutorialProgressSeconds = ClampFinite(save.TutorialProgressSeconds, 0f, 3600f);
            save.BannerText ??= string.Empty;
            save.ActiveEventWarning ??= string.Empty;
            save.Summary ??= new SaveSlotSummary();
            save.DraftCards ??= new List<UpgradeDraftCard>();
            save.PlayerStatus ??= new PlayerStatusSnapshotData();
            save.Player ??= new PlayerSnapshotData();
            save.Enemies ??= new List<EnemySnapshotData>();
            save.Bullets ??= new List<BulletSnapshotData>();
            save.Beams ??= new List<BeamSnapshotData>();
            save.Powerups ??= new List<PowerupSnapshotData>();
            save.ScheduledSpawns ??= new List<ScheduledSpawnSnapshotData>();
            save.ScheduledEvents ??= new List<ScheduledEventSnapshotData>();
            save.ReentryTickets ??= new List<ReentryTicketSnapshotData>();
            save.SpawnDirector ??= new SpawnDirectorSnapshotData();
            save.ActiveBossDefinition ??= new BossDefinitionSnapshotData();
            save.GameplayRngState = save.GameplayRngState == 0 ? 1u : save.GameplayRngState;

            NormalizeList(save.DraftCards, 3);
            NormalizeList(save.Enemies, 512);
            NormalizeList(save.Bullets, 2048);
            NormalizeList(save.Beams, 256);
            NormalizeList(save.Powerups, 512);
            NormalizeList(save.ScheduledSpawns, 2048);
            NormalizeList(save.ScheduledEvents, 256);
            NormalizeList(save.ReentryTickets, 256);

            save.PlayerStatus.RunProgress ??= new PlayerRunProgressSnapshotData();
            save.PlayerStatus.Lives = Math.Clamp(save.PlayerStatus.Lives, 0, 99);
            save.PlayerStatus.Ships = Math.Clamp(save.PlayerStatus.Ships, 0, 99);
            save.PlayerStatus.Score = Math.Max(0, save.PlayerStatus.Score);
            save.PlayerStatus.Multiplier = Math.Clamp(save.PlayerStatus.Multiplier, 1, 99);
            save.PlayerStatus.MultiplierTimeLeft = ClampFinite(save.PlayerStatus.MultiplierTimeLeft, 0f, 3600f);
            save.PlayerStatus.ScoreForExtraLife = Math.Max(0, save.PlayerStatus.ScoreForExtraLife);

            save.Player.Position ??= new Vector2Data();
            save.Player.Velocity ??= new Vector2Data();
            save.Player.CannonDirection ??= new Vector2Data(1f, 0f);
            save.Player.KnockbackVelocity ??= new Vector2Data();
            save.Player.PendingRespawnPosition ??= new Vector2Data();
            save.Player.ChaseReticle ??= new Vector2Data();
            save.Player.ChaseEntryRecenterTarget ??= new Vector3Data();
            save.Player.SupportFireCooldowns ??= new Dictionary<RuntimeData.WeaponStyleId, float>();
            save.Player.HullMask ??= new MaskSnapshotData();

            for (int index = 0; index < save.Enemies.Count; index++)
            {
                EnemySnapshotData enemy = save.Enemies[index];
                enemy.ArchetypeId ??= string.Empty;
                enemy.Position ??= new Vector2Data();
                enemy.Velocity ??= new Vector2Data();
                enemy.PendingSupportGroups ??= new List<RuntimeData.SpawnGroupDefinition>();
                enemy.Mask ??= new MaskSnapshotData();
            }

            for (int index = 0; index < save.Bullets.Count; index++)
            {
                BulletSnapshotData bullet = save.Bullets[index];
                bullet.Position ??= new Vector2Data();
                bullet.PreviousPosition ??= new Vector2Data();
                bullet.Velocity ??= new Vector2Data();
                bullet.SpriteDefinition ??= new RuntimeData.ProceduralSpriteDefinition();
                bullet.ImpactProfile ??= new RuntimeData.ImpactProfileDefinition();
            }

            for (int index = 0; index < save.Beams.Count; index++)
            {
                BeamSnapshotData beam = save.Beams[index];
                beam.Origin ??= new Vector2Data();
                beam.Direction ??= new Vector2Data(1f, 0f);
                beam.ImpactProfile ??= new RuntimeData.ImpactProfileDefinition();
                beam.PrimaryColor ??= "#FFFFFF";
                beam.AccentColor ??= "#6EC1FF";
            }

            for (int index = 0; index < save.Powerups.Count; index++)
            {
                PowerupSnapshotData powerup = save.Powerups[index];
                powerup.Position ??= new Vector2Data();
                powerup.Velocity ??= new Vector2Data();
                powerup.StyleId = IsDefined(powerup.StyleId) ? powerup.StyleId : RuntimeData.WeaponStyleId.Pulse;
                powerup.PickupKind = IsDefined(powerup.PickupKind) ? powerup.PickupKind : PickupKind.WeaponCore;
                powerup.Amount = Math.Clamp(powerup.Amount, 1, 999);
            }

            save.SpawnDirector.TriggeredKillChainIndices ??= new List<int>();
            save.SpawnDirector.TriggeredKillChainIndices.RemoveAll(index => index < 0 || index > 64);
            if (save.SpawnDirector.TriggeredKillChainIndices.Count > 64)
                save.SpawnDirector.TriggeredKillChainIndices.RemoveRange(64, save.SpawnDirector.TriggeredKillChainIndices.Count - 64);
            save.SpawnDirector.PendingHordeBursts ??= new List<PendingHordePacketBurstSnapshotData>();
            NormalizeList(save.SpawnDirector.PendingHordeBursts, 128);

            save.Summary.SlotIndex = Math.Clamp(save.Summary.SlotIndex, 0, 3);
            save.Summary.StageNumber = Math.Clamp(save.Summary.StageNumber <= 0 ? save.CurrentStageNumber : save.Summary.StageNumber, 1, 50);
            save.Summary.Difficulty = IsDefined(save.Summary.Difficulty) ? save.Summary.Difficulty : save.Difficulty;
            save.Summary.Score = Math.Max(0, save.Summary.Score);
            save.Summary.StageName ??= string.Empty;
            save.Summary.SavedAtUtc ??= string.Empty;
            save.Summary.ActiveStyle ??= string.Empty;
        }

        private static void NormalizeList<T>(List<T> values, int maximumCount) where T : class
        {
            values.RemoveAll(value => value == null);
            if (values.Count > maximumCount)
                values.RemoveRange(maximumCount, values.Count - maximumCount);
        }

        private static bool IsDefined<T>(T value) where T : struct, Enum
        {
            return Enum.IsDefined(typeof(T), value);
        }

        private static float ClampFinite(float value, float minimum, float maximum)
        {
            return float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : minimum;
        }
    }
}
