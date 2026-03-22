using Microsoft.Xna.Framework;
using SpaceBurst.RuntimeData;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaceBurst
{
    sealed class SpawnDirector
    {
        private struct QueuedHordePacketBurst
        {
            public int PacketIndex;
            public int NextBurstNumber;
            public float NextBurstAtSeconds;
        }

        private readonly HashSet<int> triggeredKillChainIndices = new HashSet<int>();
        private readonly List<QueuedHordePacketBurst> queuedHordePackets = new List<QueuedHordePacketBurst>();
        private int nextHordePacketIndex;
        private int nextEliteBurstIndex;
        private int nextPresentationCueIndex;
        private float warningTimerSeconds;
        private string warningText = string.Empty;
        private string warningAccentColor = "#FFB347";
        private float warningIntensity = 1f;
        private PresentationCueKind warningKind = PresentationCueKind.Warning;

        public string ActiveWarning
        {
            get { return warningTimerSeconds > 0f ? warningText : string.Empty; }
        }

        public Color ActiveWarningColor
        {
            get { return warningTimerSeconds > 0f ? ColorUtil.ParseHex(warningAccentColor, Color.Orange) : Color.White * 0.72f; }
        }

        public float ActiveWarningIntensity
        {
            get { return warningTimerSeconds > 0f ? warningIntensity : 0f; }
        }

        public PresentationCueKind ActiveWarningKind
        {
            get { return warningTimerSeconds > 0f ? warningKind : PresentationCueKind.Warning; }
        }

        public void Reset()
        {
            nextHordePacketIndex = 0;
            nextEliteBurstIndex = 0;
            nextPresentationCueIndex = 0;
            warningTimerSeconds = 0f;
            warningText = string.Empty;
            warningAccentColor = "#FFB347";
            warningIntensity = 1f;
            warningKind = PresentationCueKind.Warning;
            triggeredKillChainIndices.Clear();
            queuedHordePackets.Clear();
        }

        public void Update(float stageElapsedSeconds, float deltaSeconds, StageDefinition stage, CampaignRepository repository, ref float rewindMeterSeconds, float rewindCapacitySeconds)
        {
            if (warningTimerSeconds > 0f)
            {
                warningTimerSeconds = Math.Max(0f, warningTimerSeconds - deltaSeconds);
                if (warningTimerSeconds <= 0f)
                    warningText = string.Empty;
            }

            if (stage == null || repository?.ArchetypesById == null)
                return;

            while (nextPresentationCueIndex < stage.PresentationCues.Count &&
                stage.PresentationCues[nextPresentationCueIndex].StartSeconds <= stageElapsedSeconds)
            {
                PresentationCueDefinition cue = stage.PresentationCues[nextPresentationCueIndex++];
                if (!string.IsNullOrWhiteSpace(cue.Label))
                    SetWarning(cue.Label, cue.DurationSeconds, cue.AccentColor, cue.Intensity, cue.Kind);
                TriggerPresentationCue(cue);
            }

            while (nextHordePacketIndex < stage.HordePackets.Count &&
                stage.HordePackets[nextHordePacketIndex].StartSeconds <= stageElapsedSeconds)
            {
                QueueHordePacket(stage.HordePackets[nextHordePacketIndex], nextHordePacketIndex, stageElapsedSeconds);
                nextHordePacketIndex++;
            }

            ProcessQueuedHordePackets(stageElapsedSeconds, stage, repository);

            while (nextEliteBurstIndex < stage.EliteBursts.Count &&
                stage.EliteBursts[nextEliteBurstIndex].StartSeconds <= stageElapsedSeconds)
            {
                SpawnEliteBurst(stage.EliteBursts[nextEliteBurstIndex++], repository);
            }

            for (int i = 0; i < stage.KillChainEvents.Count; i++)
            {
                if (triggeredKillChainIndices.Contains(i))
                    continue;

                KillChainEventDefinition definition = stage.KillChainEvents[i];
                if (PlayerStatus.Multiplier < definition.TriggerMultiplier)
                    continue;

                triggeredKillChainIndices.Add(i);
                PlayerStatus.RunProgress.AddXp(definition.BonusXp);
                PlayerStatus.RunProgress.AddScrap(definition.BonusScrap);
                if (definition.BonusRewindPercent > 0f)
                    rewindMeterSeconds = Math.Min(rewindCapacitySeconds, rewindMeterSeconds + rewindCapacitySeconds * definition.BonusRewindPercent);
                SetWarning(
                    string.IsNullOrWhiteSpace(definition.Label) ? "CHAIN SPIKE" : definition.Label,
                    1.6f,
                    definition.AccentColor,
                    1f,
                    PresentationCueKind.Reward);
            }
        }

        public SpawnDirectorSnapshotData CaptureSnapshot()
        {
            return new SpawnDirectorSnapshotData
            {
                NextHordePacketIndex = nextHordePacketIndex,
                NextEliteBurstIndex = nextEliteBurstIndex,
                NextPresentationCueIndex = nextPresentationCueIndex,
                WarningTimerSeconds = warningTimerSeconds,
                WarningText = warningText,
                WarningAccentColor = warningAccentColor,
                WarningIntensity = warningIntensity,
                WarningKind = warningKind,
                TriggeredKillChainIndices = triggeredKillChainIndices.OrderBy(index => index).ToList(),
                PendingHordeBursts = queuedHordePackets
                    .OrderBy(packet => packet.NextBurstAtSeconds)
                    .ThenBy(packet => packet.PacketIndex)
                    .Select(packet => new PendingHordePacketBurstSnapshotData
                    {
                        PacketIndex = packet.PacketIndex,
                        NextBurstNumber = packet.NextBurstNumber,
                        NextBurstAtSeconds = packet.NextBurstAtSeconds,
                    })
                    .ToList(),
            };
        }

        public void RestoreSnapshot(SpawnDirectorSnapshotData snapshot)
        {
            nextHordePacketIndex = Math.Max(0, snapshot?.NextHordePacketIndex ?? 0);
            nextEliteBurstIndex = Math.Max(0, snapshot?.NextEliteBurstIndex ?? 0);
            nextPresentationCueIndex = Math.Max(0, snapshot?.NextPresentationCueIndex ?? 0);
            warningTimerSeconds = Math.Max(0f, snapshot?.WarningTimerSeconds ?? 0f);
            warningText = snapshot?.WarningText ?? string.Empty;
            warningAccentColor = string.IsNullOrWhiteSpace(snapshot?.WarningAccentColor) ? "#FFB347" : snapshot.WarningAccentColor;
            warningIntensity = Math.Max(0f, snapshot?.WarningIntensity ?? 1f);
            warningKind = snapshot?.WarningKind ?? PresentationCueKind.Warning;

            triggeredKillChainIndices.Clear();
            if (snapshot?.TriggeredKillChainIndices != null)
            {
                for (int i = 0; i < snapshot.TriggeredKillChainIndices.Count; i++)
                    triggeredKillChainIndices.Add(snapshot.TriggeredKillChainIndices[i]);
            }

            queuedHordePackets.Clear();
            if (snapshot?.PendingHordeBursts != null)
            {
                for (int i = 0; i < snapshot.PendingHordeBursts.Count; i++)
                {
                    PendingHordePacketBurstSnapshotData queued = snapshot.PendingHordeBursts[i];
                    queuedHordePackets.Add(new QueuedHordePacketBurst
                    {
                        PacketIndex = Math.Max(0, queued.PacketIndex),
                        NextBurstNumber = Math.Max(0, queued.NextBurstNumber),
                        NextBurstAtSeconds = Math.Max(0f, queued.NextBurstAtSeconds),
                    });
                }
            }
        }

        private void QueueHordePacket(HordePacketDefinition packet, int packetIndex, float stageElapsedSeconds)
        {
            if (packet == null || string.IsNullOrWhiteSpace(packet.ArchetypeId))
                return;

            queuedHordePackets.Add(new QueuedHordePacketBurst
            {
                PacketIndex = packetIndex,
                NextBurstNumber = 0,
                NextBurstAtSeconds = Math.Max(packet.StartSeconds, stageElapsedSeconds),
            });
        }

        private void ProcessQueuedHordePackets(float stageElapsedSeconds, StageDefinition stage, CampaignRepository repository)
        {
            if (stage == null || repository?.ArchetypesById == null)
                return;

            for (int i = queuedHordePackets.Count - 1; i >= 0; i--)
            {
                QueuedHordePacketBurst queued = queuedHordePackets[i];
                if (queued.PacketIndex < 0 || queued.PacketIndex >= stage.HordePackets.Count)
                {
                    queuedHordePackets.RemoveAt(i);
                    continue;
                }

                HordePacketDefinition packet = stage.HordePackets[queued.PacketIndex];
                int burstCount = Math.Max(1, packet.BurstCount);
                bool delayedForCap = false;
                while (queued.NextBurstNumber < burstCount && queued.NextBurstAtSeconds <= stageElapsedSeconds + 0.001f)
                {
                    if (!TrySpawnHordeBurst(packet, queued.NextBurstNumber, repository))
                    {
                        queued.NextBurstAtSeconds = stageElapsedSeconds + 0.4f;
                        delayedForCap = true;
                        break;
                    }

                    queued.NextBurstNumber++;
                    queued.NextBurstAtSeconds += Math.Max(0.18f, packet.BurstIntervalSeconds);
                }

                if (!delayedForCap && queued.NextBurstNumber >= burstCount)
                {
                    queuedHordePackets.RemoveAt(i);
                }
                else
                {
                    queuedHordePackets[i] = queued;
                }
            }
        }

        private bool TrySpawnHordeBurst(HordePacketDefinition packet, int burstIndex, CampaignRepository repository)
        {
            if (packet == null || string.IsNullOrWhiteSpace(packet.ArchetypeId))
                return false;
            if (!repository.ArchetypesById.TryGetValue(packet.ArchetypeId, out EnemyArchetypeDefinition archetype))
                return false;

            int hostileCap = PlatformServices.Capabilities.SupportsTouch ? 30 : 52;
            if (!packet.IgnoreHostileCap && EntityManager.Enemies.Count() >= hostileCap)
                return false;

            for (int countIndex = 0; countIndex < Math.Max(1, packet.CountPerBurst); countIndex++)
            {
                float offset = countIndex * packet.SpacingX
                    + burstIndex * Math.Max(18f, packet.SpacingX * 0.4f)
                    + countIndex * packet.SpawnIntervalSeconds * 120f;
                Vector2 spawnPoint = ResolveSpawnPoint(packet.SpawnLeadDistance, packet.Lane, packet.TargetY, offset);
                EntityManager.Add(new Enemy(
                    archetype,
                    spawnPoint,
                    spawnPoint.Y,
                    packet.MovePatternOverride ?? archetype.MovePattern,
                    packet.FirePatternOverride ?? archetype.FirePattern,
                    packet.SpeedMultiplier,
                    packet.Amplitude,
                    packet.Frequency));
            }

            return true;
        }

        private void SpawnEliteBurst(EliteBurstEventDefinition burst, CampaignRepository repository)
        {
            if (burst == null || string.IsNullOrWhiteSpace(burst.ArchetypeId))
                return;
            if (!repository.ArchetypesById.TryGetValue(burst.ArchetypeId, out EnemyArchetypeDefinition archetype))
                return;

            SetWarning(
                string.IsNullOrWhiteSpace(burst.WarningText) ? "ELITE BURST" : burst.WarningText,
                1.8f,
                "#FFD166",
                1.05f,
                PresentationCueKind.BossSignal);

            for (int i = 0; i < Math.Max(1, burst.EliteCount); i++)
            {
                float spread = (i - (burst.EliteCount - 1) * 0.5f) * 64f;
                Vector2 spawnPoint = ResolveSpawnPoint(burst.SpawnLeadDistance, 2, burst.TargetY, spread);
                Enemy enemy = new Enemy(
                    archetype,
                    spawnPoint,
                    spawnPoint.Y,
                    burst.MovePatternOverride ?? archetype.MovePattern,
                    burst.FirePatternOverride ?? archetype.FirePattern,
                    burst.SpeedMultiplier,
                    archetype.MovementAmplitude,
                    archetype.MovementFrequency);
                enemy.ConfigureSliceEliteReward(burst.ScrapReward, burst.RewindRefillPercent);
                EntityManager.Add(enemy);
            }
        }

        private void SetWarning(string text, float durationSeconds, string accentColor = null, float intensity = 1f, PresentationCueKind kind = PresentationCueKind.Warning)
        {
            warningText = text ?? string.Empty;
            warningTimerSeconds = Math.Max(warningTimerSeconds, Math.Max(0.6f, durationSeconds));
            warningAccentColor = string.IsNullOrWhiteSpace(accentColor) ? ResolveCueAccent(kind) : accentColor;
            warningIntensity = Math.Max(0f, intensity);
            warningKind = kind;
        }

        private static string ResolveCueAccent(PresentationCueKind kind)
        {
            return kind switch
            {
                PresentationCueKind.Reward => "#73F3E8",
                PresentationCueKind.BossSignal => "#FFD166",
                PresentationCueKind.PaletteSpike => "#FF7A59",
                PresentationCueKind.ChapterBeat => "#56F0FF",
                _ => "#FFB347",
            };
        }

        private static void TriggerPresentationCue(PresentationCueDefinition cue)
        {
            if (cue == null || Game1.Instance?.Feedback == null)
                return;

            float intensity = MathHelper.Clamp(0.2f + cue.Intensity * 0.22f, 0.2f, 1.2f);
            Vector2 origin = Player1.Instance?.Position ?? new Vector2(Game1.VirtualWidth * 0.5f, Game1.VirtualHeight * 0.5f);
            FeedbackEventType type = cue.Kind switch
            {
                PresentationCueKind.Reward => FeedbackEventType.Upgrade,
                PresentationCueKind.BossSignal => FeedbackEventType.BossEntry,
                _ => FeedbackEventType.StageTransition,
            };

            Game1.Instance.Feedback.Handle(new FeedbackEvent(
                type,
                origin,
                intensity,
                WeaponStyleId.Pulse,
                cue.Kind == PresentationCueKind.BossSignal || cue.Kind == PresentationCueKind.Reward));
        }

        private static Vector2 ResolveSpawnPoint(float leadDistance, int lane, float? targetY, float xOffset)
        {
            float[] laneY = { 0.18f, 0.33f, 0.48f, 0.63f, 0.78f };
            int safeLane = Math.Clamp(lane, 0, laneY.Length - 1);
            float y = targetY.HasValue
                ? Game1.VirtualHeight * MathHelper.Clamp(targetY.Value, 0.1f, 0.9f)
                : Game1.VirtualHeight * laneY[safeLane];
            return new Vector2(Game1.VirtualWidth + Math.Max(140f, leadDistance) + xOffset, y);
        }
    }
}
