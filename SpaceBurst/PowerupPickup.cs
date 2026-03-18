using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace SpaceBurst
{
    sealed class PowerupPickup : Entity
    {
        private static readonly List<string> glyphRows = new List<string>
        {
            "..###..",
            ".#...#.",
            "#.###.#",
            "#.#.#.#",
            "#.###.#",
            ".#...#.",
            "..###..",
        };

        private readonly float bobSeed;
        private readonly Color primaryColor = new Color(255, 244, 202);
        private readonly Color secondaryColor = new Color(255, 179, 71);
        private readonly Color accentColor = new Color(255, 122, 89);
        private float ageSeconds;

        public PowerupPickup(Vector2 position)
        {
            Position = position;
            Velocity = new Vector2(-40f, 0f);
            bobSeed = position.X * 0.013f + position.Y * 0.009f;
        }

        public override void Update()
        {
            float deltaSeconds = (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            ageSeconds += deltaSeconds;
            Vector2 drift = new Vector2(Velocity.X - Game1.Instance.CurrentScrollSpeed * 0.18f, MathF.Sin(ageSeconds * 3.4f + bobSeed) * 18f);

            if (Game1.Instance.PowerupMagnetStrength > 0f && !Player1.Instance.IsDead)
            {
                Vector2 delta = Player1.Instance.Position - Position;
                if (delta != Vector2.Zero)
                {
                    float distance = delta.Length();
                    delta /= distance;
                    drift += delta * Game1.Instance.PowerupMagnetStrength * MathHelper.Clamp(1f - distance / 420f, 0.15f, 1f);
                }
            }

            Position += drift * deltaSeconds;

            if (ageSeconds > 12f || Position.X < -40f)
                IsExpired = true;
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            float pulse = 1f + MathF.Sin(ageSeconds * 6f) * 0.08f;
            PixelArtRenderer.DrawRows(spriteBatch, Game1.UiPixel, glyphRows, Position, 4f * pulse, primaryColor, secondaryColor, accentColor, true);
            BitmapFontRenderer.Draw(spriteBatch, Game1.UiPixel, "P", Position - new Vector2(5f, 10f), Color.Black, 2f * pulse);
        }

        public bool OverlapsPlayer()
        {
            return Vector2.DistanceSquared(Position, Player1.Instance.Position) <= 42f * 42f;
        }

        public PowerupSnapshotData CaptureSnapshot()
        {
            return new PowerupSnapshotData
            {
                Position = new Vector2Data(Position.X, Position.Y),
                Velocity = new Vector2Data(Velocity.X, Velocity.Y),
                AgeSeconds = ageSeconds,
            };
        }

        public static PowerupPickup FromSnapshot(PowerupSnapshotData snapshot)
        {
            if (snapshot == null)
                return null;

            var pickup = new PowerupPickup(new Vector2(snapshot.Position.X, snapshot.Position.Y));
            pickup.Velocity = new Vector2(snapshot.Velocity.X, snapshot.Velocity.Y);
            pickup.ageSeconds = snapshot.AgeSeconds;
            return pickup;
        }
    }
}
