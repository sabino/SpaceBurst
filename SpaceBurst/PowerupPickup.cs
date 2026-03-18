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
            Position += new Vector2(Velocity.X - Game1.Instance.CurrentScrollSpeed * 0.18f, MathF.Sin(ageSeconds * 3.4f + bobSeed) * 18f) * deltaSeconds;

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
    }
}
