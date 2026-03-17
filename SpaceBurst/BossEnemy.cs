using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using SpaceBurst.RuntimeData;

namespace SpaceBurst
{
    sealed class BossEnemy : Enemy
    {
        private readonly BossDefinition definition;
        private readonly Queue<SpawnGroupDefinition> pendingSupportGroups = new Queue<SpawnGroupDefinition>();
        private readonly int[] phaseThresholds;

        private float bossTimer;
        private int currentPhase;
        private float dashDirection = 1f;

        public override bool IsBoss
        {
            get { return true; }
        }

        public override int PointValue
        {
            get { return 500; }
            protected set { }
        }

        public float HealthRatio
        {
            get { return hitPoints / (float)definition.HitPoints; }
        }

        public string DisplayName
        {
            get { return definition.DisplayName; }
        }

        public BossType BossType
        {
            get { return definition.Type; }
        }

        public BossEnemy(
            EnemyArchetypeDefinition archetype,
            BossDefinition definition,
            Vector2 spawnPoint,
            Vector2 anchorPoint)
            : base(archetype, spawnPoint, anchorPoint, PathType.OrbitAnchor, 0, 1f)
        {
            this.definition = definition;
            hitPoints = definition.HitPoints;
            RenderScale = definition.RenderScale;
            Radius = Math.Max(archetype.CollisionRadius * definition.RenderScale * 0.85f, archetype.CollisionRadius + 10f);
            phaseThresholds = new[]
            {
                (int)(definition.HitPoints * 0.75f),
                (int)(definition.HitPoints * 0.5f),
                (int)(definition.HitPoints * 0.25f),
            };
        }

        public override void HandleBulletHit(Bullet bullet)
        {
            hitPoints--;
            color = Color.White;
            CheckPhaseTransitions();

            if (hitPoints <= 0)
                Destroy();
        }

        public bool TryConsumeSupportWave(out SpawnGroupDefinition group)
        {
            if (pendingSupportGroups.Count > 0)
            {
                group = pendingSupportGroups.Dequeue();
                return true;
            }

            group = null;
            return false;
        }

        protected override void Destroy()
        {
            base.Destroy();
        }

        protected override void UpdateMovement(float frameScale)
        {
            bossTimer += (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;

            Vector2 desiredVelocity;
            switch (definition.Type)
            {
                case BossType.DestroyerBoss:
                case BossType.DestroyerBossMk2:
                    desiredVelocity = GetDestroyerBossVelocity();
                    break;

                case BossType.FinalBoss:
                    desiredVelocity = currentPhase % 2 == 0 ? GetDestroyerBossVelocity() : GetWalkerBossVelocity();
                    break;

                default:
                    desiredVelocity = GetWalkerBossVelocity();
                    break;
            }

            Velocity = Vector2.Lerp(Velocity, desiredVelocity, 0.14f * frameScale);
            if (Velocity.LengthSquared() > 0.001f)
                Orientation = Velocity.ToAngle();
        }

        private void CheckPhaseTransitions()
        {
            while (currentPhase < phaseThresholds.Length && hitPoints <= phaseThresholds[currentPhase])
            {
                currentPhase++;
                QueueSupportBurst();
            }
        }

        private void QueueSupportBurst()
        {
            int count = 2 + currentPhase;
            pendingSupportGroups.Enqueue(new SpawnGroupDefinition
            {
                ArchetypeId = currentPhase % 2 == 0 ? "Walker" : "Destroyer",
                Count = count,
                Formation = currentPhase % 2 == 0 ? FormationType.Line : FormationType.Arc,
                EntrySide = currentPhase % 2 == 0 ? EntrySide.Left : EntrySide.Right,
                PathType = currentPhase % 2 == 0 ? PathType.LaneSweep : PathType.ChaseAfterDelay,
                AnchorX = currentPhase % 2 == 0 ? 0.28f : 0.72f,
                AnchorY = 0.28f + currentPhase * 0.12f,
                Spacing = 70f,
                DelayBetweenSpawns = 0.18f,
                TravelDuration = 2.6f,
                SpeedMultiplier = 1f + currentPhase * 0.12f,
            });
        }

        private Vector2 GetDestroyerBossVelocity()
        {
            float topLane = Game1.ScreenSize.Y * (0.22f + currentPhase * 0.04f);
            float left = Game1.ScreenSize.X * 0.22f;
            float right = Game1.ScreenSize.X * 0.78f;

            if (Position.X <= left)
                dashDirection = 1f;
            else if (Position.X >= right)
                dashDirection = -1f;

            float dashSpeed = archetype.Speed * (1.35f + currentPhase * 0.18f);
            float verticalCorrection = (topLane - Position.Y) * 0.06f;
            return new Vector2(dashDirection * dashSpeed, verticalCorrection);
        }

        private Vector2 GetWalkerBossVelocity()
        {
            float angle = bossTimer * (1.25f + currentPhase * 0.2f);
            Vector2 target = anchorPoint + new Vector2((float)Math.Cos(angle) * 130f, (float)Math.Sin(angle * 1.3f) * 70f);
            if (definition.Type == BossType.WalkerBossMk2)
                target += new Vector2((float)Math.Sin(angle * 0.7f) * 90f, 0f);

            return MoveToward(target, archetype.Speed * (1.05f + currentPhase * 0.12f));
        }
    }
}
