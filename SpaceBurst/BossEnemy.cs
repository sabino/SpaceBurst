using Microsoft.Xna.Framework;
using SpaceBurst.RuntimeData;
using System;
using System.Collections.Generic;

namespace SpaceBurst
{
    sealed class BossEnemy : Enemy
    {
        private readonly BossDefinition definition;
        private readonly Queue<SpawnGroupDefinition> pendingSupportGroups = new Queue<SpawnGroupDefinition>();
        private readonly List<float> phaseThresholds;

        private int currentPhase;
        private float sweepDirection = -1f;
        private float bossFireCooldown;

        public override bool IsBoss
        {
            get { return true; }
        }

        public override int PointValue
        {
            get { return archetype.ScoreValue * 5; }
        }

        public float HealthRatio
        {
            get { return IntegrityRatio; }
        }

        public string DisplayName
        {
            get { return definition.DisplayName; }
        }

        public IReadOnlyList<float> PhaseThresholds
        {
            get { return phaseThresholds; }
        }

        public BossEnemy(
            EnemyArchetypeDefinition archetype,
            BossDefinition definition,
            Vector2 spawnPoint)
            : base(
                archetype,
                spawnPoint,
                definition.TargetY * Game1.ScreenSize.Y,
                definition.MovePattern,
                definition.FirePattern,
                1f,
                archetype.MovementAmplitude * 1.15f,
                archetype.MovementFrequency)
        {
            this.definition = definition;
            phaseThresholds = new List<float>(definition.PhaseThresholds ?? new List<float> { 0.75f, 0.5f, 0.25f });
            bossFireCooldown = Math.Max(0.45f, archetype.FireIntervalSeconds);
        }

        public bool TryConsumeSupportGroup(out SpawnGroupDefinition group)
        {
            if (pendingSupportGroups.Count > 0)
            {
                group = pendingSupportGroups.Dequeue();
                return true;
            }

            group = null;
            return false;
        }

        public override void ApplyBulletHit(Bullet bullet, Vector2 impactPoint)
        {
            base.ApplyBulletHit(bullet, impactPoint);
            CheckPhaseTransitions();
        }

        public override void ApplyBeamHit(Vector2 impactPoint, int damage, ImpactProfileDefinition impactProfile)
        {
            base.ApplyBeamHit(impactPoint, damage, impactProfile);
            CheckPhaseTransitions();
        }

        public override EnemySnapshotData CaptureSnapshot()
        {
            EnemySnapshotData snapshot = base.CaptureSnapshot();
            snapshot.IsBoss = true;
            snapshot.BossPhase = currentPhase;
            snapshot.BossSweepDirection = sweepDirection;
            snapshot.BossFireCooldown = bossFireCooldown;
            snapshot.PendingSupportGroups = new List<SpawnGroupDefinition>(pendingSupportGroups);
            return snapshot;
        }

        public void RestoreBossSnapshot(EnemySnapshotData snapshot)
        {
            currentPhase = snapshot.BossPhase;
            sweepDirection = snapshot.BossSweepDirection;
            bossFireCooldown = snapshot.BossFireCooldown;
            pendingSupportGroups.Clear();
            if (snapshot.PendingSupportGroups != null)
            {
                for (int i = 0; i < snapshot.PendingSupportGroups.Count; i++)
                    pendingSupportGroups.Enqueue(snapshot.PendingSupportGroups[i]);
            }
        }

        protected override void UpdateMovement(float deltaSeconds)
        {
            float scrollSpeed = Game1.Instance.CurrentScrollSpeed;
            float phase = ageSeconds * (0.75f + currentPhase * 0.18f);
            float desiredY = targetY;
            float desiredX = Game1.ScreenSize.X * 0.77f;

            switch (definition.Type)
            {
                case BossType.DestroyerBoss:
                case BossType.DestroyerBossMk2:
                    desiredX = Game1.ScreenSize.X * 0.72f + sweepDirection * 90f;
                    desiredY = targetY + (float)Math.Sin(phase * 1.4f) * (movementAmplitude * (0.6f + currentPhase * 0.1f));
                    if (Position.X <= Game1.ScreenSize.X * 0.58f)
                        sweepDirection = 1f;
                    else if (Position.X >= Game1.ScreenSize.X * 0.82f)
                        sweepDirection = -1f;
                    break;

                case BossType.FinalBoss:
                    if (currentPhase % 2 == 0)
                    {
                        desiredX = Game1.ScreenSize.X * 0.74f + (float)Math.Cos(phase * 0.7f) * 80f;
                        desiredY = targetY + (float)Math.Sin(phase * 1.8f) * movementAmplitude;
                    }
                    else
                    {
                        desiredX = Game1.ScreenSize.X * 0.68f + sweepDirection * 110f;
                        desiredY = targetY + (float)Math.Sin(phase * 1.1f) * movementAmplitude * 0.65f;
                        if (Position.X <= Game1.ScreenSize.X * 0.54f)
                            sweepDirection = 1f;
                        else if (Position.X >= Game1.ScreenSize.X * 0.84f)
                            sweepDirection = -1f;
                    }
                    break;

                default:
                    desiredX = Game1.ScreenSize.X * 0.78f + (float)Math.Cos(phase * 0.9f) * 70f;
                    desiredY = targetY + (float)Math.Sin(phase * 1.7f) * movementAmplitude * 0.85f;
                    break;
            }

            Vector2 desiredVelocity = new Vector2(
                (desiredX - Position.X) * 1.8f - scrollSpeed * 0.45f,
                (desiredY - Position.Y) * 2.8f);

            Velocity = Vector2.Lerp(Velocity, desiredVelocity, Math.Min(1f, 3.8f * deltaSeconds));
        }

        protected override void TryFire(float deltaSeconds)
        {
            bossFireCooldown -= deltaSeconds;
            if (bossFireCooldown > 0f)
                return;

            bossFireCooldown = Math.Max(0.35f, archetype.FireIntervalSeconds - currentPhase * 0.08f);

            switch (definition.FirePattern)
            {
                case FirePattern.AimedShot:
                    Vector2 aimed = Player1.Instance.Position - Position;
                    if (aimed == Vector2.Zero)
                        aimed = -Vector2.UnitX;
                    else
                        aimed.Normalize();

                    SpawnBullet(aimed);
                    break;

                case FirePattern.SpreadPulse:
                    SpawnBullet(new Vector2(-1f, -0.25f));
                    SpawnBullet(new Vector2(-1f, 0f));
                    SpawnBullet(new Vector2(-1f, 0.25f));
                    break;

                default:
                    SpawnBullet(new Vector2(-1f, -0.35f));
                    SpawnBullet(new Vector2(-1f, -0.15f));
                    SpawnBullet(new Vector2(-1f, 0f));
                    SpawnBullet(new Vector2(-1f, 0.15f));
                    SpawnBullet(new Vector2(-1f, 0.35f));
                    break;
            }
        }

        private void CheckPhaseTransitions()
        {
            while (currentPhase < phaseThresholds.Count && HealthRatio <= phaseThresholds[currentPhase])
            {
                currentPhase++;
                QueueSupportBurst();
            }
        }

        private void QueueSupportBurst()
        {
            switch (definition.Type)
            {
                case BossType.WalkerBoss:
                case BossType.WalkerBossMk2:
                    pendingSupportGroups.Enqueue(new SpawnGroupDefinition
                    {
                        ArchetypeId = currentPhase % 2 == 0 ? "Interceptor" : "Carrier",
                        StartSeconds = 0f,
                        Lane = currentPhase % 5,
                        Count = 3 + currentPhase,
                        SpawnLeadDistance = 240f,
                        SpawnIntervalSeconds = 0.25f,
                        SpacingX = 84f,
                        SpeedMultiplier = 1f + currentPhase * 0.1f,
                        MovePatternOverride = MovePattern.SineWave,
                        FirePatternOverride = currentPhase > 1 ? FirePattern.ForwardPulse : FirePattern.None,
                        Amplitude = 60f + currentPhase * 12f,
                        Frequency = 0.9f + currentPhase * 0.15f,
                    });
                    break;

                case BossType.FinalBoss:
                    pendingSupportGroups.Enqueue(new SpawnGroupDefinition
                    {
                        ArchetypeId = currentPhase % 2 == 0 ? "Destroyer" : "Bulwark",
                        StartSeconds = 0f,
                        Lane = 1 + (currentPhase % 3),
                        Count = 2 + currentPhase,
                        SpawnLeadDistance = 300f,
                        SpawnIntervalSeconds = 0.32f,
                        SpacingX = 112f,
                        SpeedMultiplier = 1.05f + currentPhase * 0.08f,
                        MovePatternOverride = currentPhase % 2 == 0 ? MovePattern.Dive : MovePattern.TurretCarrier,
                        FirePatternOverride = FirePattern.SpreadPulse,
                        Amplitude = 72f + currentPhase * 8f,
                        Frequency = 0.8f + currentPhase * 0.1f,
                    });
                    break;

                default:
                    pendingSupportGroups.Enqueue(new SpawnGroupDefinition
                    {
                        ArchetypeId = currentPhase % 2 == 0 ? "Destroyer" : "Interceptor",
                        StartSeconds = 0f,
                        Lane = 2,
                        Count = 3 + currentPhase,
                        SpawnLeadDistance = 260f,
                        SpawnIntervalSeconds = 0.22f,
                        SpacingX = 96f,
                        SpeedMultiplier = 1.05f + currentPhase * 0.12f,
                        MovePatternOverride = currentPhase % 2 == 0 ? MovePattern.RetreatBackfire : MovePattern.SineWave,
                        FirePatternOverride = currentPhase > 1 ? FirePattern.ForwardPulse : FirePattern.None,
                        Amplitude = 64f,
                        Frequency = 1f + currentPhase * 0.1f,
                    });
                    break;
            }
        }
    }
}
