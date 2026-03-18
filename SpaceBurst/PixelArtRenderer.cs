using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace SpaceBurst
{
    static class PixelArtRenderer
    {
        public static void DrawRows(
            SpriteBatch spriteBatch,
            Texture2D pixel,
            IList<string> rows,
            Vector2 position,
            float scale,
            Color primaryColor,
            Color secondaryColor,
            Color accentColor,
            bool centered)
        {
            if (rows == null || rows.Count == 0)
                return;

            int width = 0;
            for (int i = 0; i < rows.Count; i++)
                width = System.Math.Max(width, rows[i]?.Length ?? 0);

            Vector2 origin = centered ? new Vector2(width * scale * 0.5f, rows.Count * scale * 0.5f) : Vector2.Zero;

            for (int y = 0; y < rows.Count; y++)
            {
                string row = rows[y] ?? string.Empty;
                for (int x = 0; x < row.Length; x++)
                {
                    char glyph = row[x];
                    if (glyph == '.' || glyph == ' ')
                        continue;

                    Color color;
                    switch (glyph)
                    {
                        case '+':
                        case 'o':
                            color = secondaryColor;
                            break;
                        case '*':
                        case 'x':
                        case 'C':
                        case '@':
                            color = accentColor;
                            break;
                        default:
                            color = primaryColor;
                            break;
                    }

                    spriteBatch.Draw(
                        pixel,
                        new Rectangle(
                            (int)System.MathF.Round(position.X - origin.X + x * scale),
                            (int)System.MathF.Round(position.Y - origin.Y + y * scale),
                            System.Math.Max(1, (int)System.MathF.Ceiling(scale)),
                            System.Math.Max(1, (int)System.MathF.Ceiling(scale))),
                        color);
                }
            }
        }
    }
}
