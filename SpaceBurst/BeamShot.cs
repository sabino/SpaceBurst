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

        public Vector2 Direction { get; private set; }
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
            string accentColorHex)
        {
            Position = origin;
            Direction = direction == Vector2.Zero ? Vector2.UnitX : Vector2.Normalize(direction);
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

            foreach (Enemy enemy in EntityManager.Enemies)
            {
                if (enemy.IsExpired || hitThisTick.Contains(enemy))
                    continue;

                if (TryGetHitPoint(enemy, out Vector2 impactPoint))
                {
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
                Origin = new Vector2Data(Position.X, Position.Y),
                Direction = new Vector2Data(Direction.X, Direction.Y),
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
                snapshot.AccentColor);
            beam.tickTimer = snapshot.TickTimer;
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
    }
}
