using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceBurst.RuntimeData;
using System;
using System.Collections.Generic;

namespace SpaceBurst
{
    sealed class BeamShot : Entity
    {
        private readonly HashSet<Enemy> hitThisTick = new HashSet<Enemy>();
        private float remainingLifetime;
        private float tickTimer;
        private Vector3 combatDirection;

        public Vector2 Direction { get; private set; }
        public Vector3 CombatDirection
        {
            get { return combatDirection; }
            private set { combatDirection = value; }
        }
        public float Length { get; private set; }
        public float Thickness { get; private set; }
        public int Damage { get; private set; }
        public bool Friendly { get; }
        public ImpactProfileDefinition ImpactProfile { get; }
        public string PrimaryColorHex { get; }
        public string AccentColorHex { get; }

        public override bool IsFriendly
        {
            get { return Friendly; }
        }

        public BeamShot(
            Vector2 origin,
            Vector2 direction,
            float length,
            float thickness,
            float lifetimeSeconds,
            int damage,
            bool friendly,
            ImpactProfileDefinition impactProfile,
            string primaryColorHex,
            string accentColorHex,
            Vector3? combatOrigin = null,
            Vector3? combatDirection = null)
        {
            CombatPosition = combatOrigin ?? new Vector3(origin.X, origin.Y, 0f);
            Direction = direction == Vector2.Zero ? Vector2.UnitX : Vector2.Normalize(direction);
            CombatDirection = combatDirection.HasValue && combatDirection.Value != Vector3.Zero
                ? Vector3.Normalize(combatDirection.Value)
                : new Vector3(Direction.X, Direction.Y, 0f);
            Orientation = Direction.ToAngle();
            Length = Math.Max(24f, length);
            Thickness = Math.Max(2f, thickness);
            remainingLifetime = lifetimeSeconds;
            Damage = Math.Max(1, damage);
            Friendly = friendly;
            ImpactProfile = impactProfile ?? new ImpactProfileDefinition();
            PrimaryColorHex = string.IsNullOrWhiteSpace(primaryColorHex) ? "#FFFFFF" : primaryColorHex;
            AccentColorHex = string.IsNullOrWhiteSpace(accentColorHex) ? "#6EC1FF" : accentColorHex;
        }

        public override void Update()
        {
            float deltaSeconds = (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            remainingLifetime -= deltaSeconds;
            if (remainingLifetime <= 0f)
            {
                IsExpired = true;
                return;
            }

            tickTimer -= deltaSeconds;
            if (tickTimer > 0f)
                return;

            tickTimer = 0.05f;
            hitThisTick.Clear();

            if (!Friendly)
                return;

            Vector2 searchCenter = Position + Direction * (Length * 0.5f);
            foreach (Enemy enemy in EntityManager.QueryNearbyEnemies(searchCenter, Length * 0.6f + 80f))
            {
                if (enemy.IsExpired || hitThisTick.Contains(enemy))
                    continue;

                Vector3 combatImpactPoint = default;
                bool hit = CombatSpaceMath.IsDepthAwareViewActive
                    ? TryGetCombatHitPoint(enemy, out combatImpactPoint)
                    : TryGetHitPoint(enemy, out _);

                if (hit)
                {
                    Vector2 impactPoint = CombatSpaceMath.IsDepthAwareViewActive
                        ? new Vector2(combatImpactPoint.X, combatImpactPoint.Y)
                        : GetFallbackImpactPoint(enemy);
                    enemy.ApplyBeamHit(impactPoint, Damage, ImpactProfile);
                    hitThisTick.Add(enemy);
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            Texture2D pixel = Game1.UiPixel;
            if (pixel == null)
                return;

            float pulse = 0.92f + 0.08f * MathF.Sin((float)Game1.GameTime.TotalGameTime.TotalSeconds * 18f);
            Color outer = ColorUtil.ParseHex(AccentColorHex, Color.Cyan) * 0.24f;
            Color mid = ColorUtil.ParseHex(AccentColorHex, Color.Cyan) * 0.48f;
            Color inner = ColorUtil.ParseHex(PrimaryColorHex, Color.White) * 0.92f;
            Vector2 origin = new Vector2(0f, 0.5f);

            spriteBatch.Draw(
                pixel,
                Position,
                null,
                outer,
                Orientation,
                origin,
                new Vector2(Length, Thickness * 1.9f * pulse),
                SpriteEffects.None,
                0f);

            spriteBatch.Draw(
                pixel,
                Position,
                null,
                mid,
                Orientation,
                origin,
                new Vector2(Length, Thickness * 1.08f * pulse),
                SpriteEffects.None,
                0f);

            spriteBatch.Draw(
                pixel,
                Position,
                null,
                inner,
                Orientation,
                origin,
                new Vector2(Length, Math.Max(1f, Thickness * 0.28f * pulse)),
                SpriteEffects.None,
                0f);
        }

        public BeamSnapshotData CaptureSnapshot()
        {
            return new BeamSnapshotData
            {
                EntityId = EntityId,
                Origin = new Vector2Data(Position.X, Position.Y),
                Direction = new Vector2Data(Direction.X, Direction.Y),
                CombatOrigin = new Vector3Data(CombatPosition.X, CombatPosition.Y, CombatPosition.Z),
                CombatDirection = new Vector3Data(CombatDirection.X, CombatDirection.Y, CombatDirection.Z),
                Length = Length,
                Thickness = Thickness,
                RemainingLifetime = remainingLifetime,
                TickTimer = tickTimer,
                Damage = Damage,
                Friendly = Friendly,
                ImpactProfile = ImpactProfile,
                PrimaryColor = PrimaryColorHex,
                AccentColor = AccentColorHex,
            };
        }

        public static BeamShot FromSnapshot(BeamSnapshotData snapshot)
        {
            if (snapshot == null)
                return null;

            var beam = new BeamShot(
                new Vector2(snapshot.Origin.X, snapshot.Origin.Y),
                new Vector2(snapshot.Direction.X, snapshot.Direction.Y),
                snapshot.Length,
                snapshot.Thickness,
                snapshot.RemainingLifetime,
                snapshot.Damage,
                snapshot.Friendly,
                snapshot.ImpactProfile,
                snapshot.PrimaryColor,
                snapshot.AccentColor,
                snapshot.CombatOrigin == null ? null : new Vector3(snapshot.CombatOrigin.X, snapshot.CombatOrigin.Y, snapshot.CombatOrigin.Z),
                snapshot.CombatDirection == null ? null : new Vector3(snapshot.CombatDirection.X, snapshot.CombatDirection.Y, snapshot.CombatDirection.Z));
            beam.tickTimer = snapshot.TickTimer;
            beam.RestoreEntityId(snapshot.EntityId);
            return beam;
        }

        private bool TryGetHitPoint(Enemy enemy, out Vector2 impactPoint)
        {
            int sampleCount = Math.Max(16, (int)(Length / 18f));
            for (int i = 1; i <= sampleCount; i++)
            {
                float t = i / (float)sampleCount;
                Vector2 sample = Position + Direction * (Length * t);
                if (enemy.ContainsPoint(sample))
                {
                    impactPoint = sample;
                    return true;
                }
            }

            impactPoint = Position + Direction * Length;
            return false;
        }

        private bool TryGetCombatHitPoint(Enemy enemy, out Vector3 impactPoint)
        {
            int sampleCount = Math.Max(18, (int)(Length / 20f));
            for (int i = 1; i <= sampleCount; i++)
            {
                float t = i / (float)sampleCount;
                Vector3 sample = CombatPosition + CombatDirection * (Length * t);
                if (enemy.ContainsCombatPoint(sample))
                {
                    impactPoint = sample;
                    return true;
                }
            }

            impactPoint = CombatPosition + CombatDirection * Length;
            return false;
        }

        private Vector2 GetFallbackImpactPoint(Enemy enemy)
        {
            return enemy == null ? Position + Direction * Length : enemy.Position;
        }
    }
}
