using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace SpaceBurst
{
    static class BitmapFontRenderer
    {
        private static readonly Dictionary<char, string[]> glyphs = new Dictionary<char, string[]>
        {
            [' '] = new[] { "...", "...", "...", "...", "...", "...", "..." },
            ['!'] = new[] { ".#.", ".#.", ".#.", ".#.", ".#.", "...", ".#." },
            ['?'] = new[] { "###", "..#", ".##", ".#.", "...", "...", ".#." },
            ['-'] = new[] { ".....", ".....", ".....", "#####", ".....", ".....", "....." },
            ['+'] = new[] { ".....", "..#..", "..#..", "#####", "..#..", "..#..", "....." },
            ['/'] = new[] { "....#", "...#.", "..#..", ".#...", "#....", ".....", "....." },
            [':'] = new[] { "...", ".#.", "...", "...", ".#.", "...", "..." },
            ['.'] = new[] { "...", "...", "...", "...", "...", "...", ".#." },
            [','] = new[] { "...", "...", "...", "...", "...", ".#.", "#.." },
            ['('] = new[] { "..#", ".#.", "#..", "#..", "#..", ".#.", "..#" },
            [')'] = new[] { "#..", ".#.", "..#", "..#", "..#", ".#.", "#.." },
            ['%'] = new[] { "##..#", "##.#.", "...#.", "..#..", ".#...", ".#.##", "#..##" },
            ['0'] = new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." },
            ['1'] = new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['2'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####"},
            ['3'] = new[] { ".###.", "#...#", "....#", "..##.", "....#", "#...#", ".###." },
            ['4'] = new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." },
            ['5'] = new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." },
            ['6'] = new[] { ".###.", "#...#", "#....", "####.", "#...#", "#...#", ".###." },
            ['7'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." },
            ['8'] = new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." },
            ['9'] = new[] { ".###.", "#...#", "#...#", ".####", "....#", "#...#", ".###." },
            ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['B'] = new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." },
            ['C'] = new[] { ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###." },
            ['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
            ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
            ['F'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." },
            ['G'] = new[] { ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###." },
            ['H'] = new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['I'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####" },
            ['J'] = new[] { "..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##.." },
            ['K'] = new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" },
            ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
            ['M'] = new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" },
            ['N'] = new[] { "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#" },
            ['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['P'] = new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
            ['Q'] = new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" },
            ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
            ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
            ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
            ['U'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['V'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." },
            ['W'] = new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#" },
            ['X'] = new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" },
            ['Y'] = new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." },
            ['Z'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" },
        };

        private static readonly string[] fallback = new[] { "#####", "#...#", "...#.", "..#..", "...#.", "#...#", "#####" };

        public static Vector2 Measure(string text, float scale)
        {
            string[] lines = Normalize(text).Split('\n');
            int maxWidth = 0;
            for (int i = 0; i < lines.Length; i++)
                maxWidth = Math.Max(maxWidth, MeasureLine(lines[i]));

            float pixelScale = Math.Max(1f, scale);
            return new Vector2(maxWidth * pixelScale, lines.Length * 8f * pixelScale);
        }

        public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, string text, Vector2 position, Color color, float scale)
        {
            string normalized = Normalize(text);
            float pixelScale = Math.Max(1f, scale);
            float x = position.X;
            float y = position.Y;

            foreach (char ch in normalized)
            {
                if (ch == '\n')
                {
                    y += 8f * pixelScale;
                    x = position.X;
                    continue;
                }

                string[] glyph = ResolveGlyph(ch);
                for (int row = 0; row < glyph.Length; row++)
                {
                    string line = glyph[row];
                    for (int column = 0; column < line.Length; column++)
                    {
                        if (line[column] != '#')
                            continue;

                        spriteBatch.Draw(
                            pixel,
                            new Rectangle(
                                (int)MathF.Round(x + column * pixelScale),
                                (int)MathF.Round(y + row * pixelScale),
                                Math.Max(1, (int)MathF.Ceiling(pixelScale)),
                                Math.Max(1, (int)MathF.Ceiling(pixelScale))),
                            color);
                    }
                }

                x += (glyph[0].Length + 1) * pixelScale;
            }
        }

        public static void DrawCentered(SpriteBatch spriteBatch, Texture2D pixel, string text, Vector2 center, Color color, float scale)
        {
            Vector2 size = Measure(text, scale);
            Draw(spriteBatch, pixel, text, center - new Vector2(size.X / 2f, 0f), color, scale);
        }

        private static string Normalize(string text)
        {
            return (text ?? string.Empty).ToUpperInvariant().Replace("\r\n", "\n");
        }

        private static int MeasureLine(string line)
        {
            int width = 0;
            for (int i = 0; i < line.Length; i++)
                width += ResolveGlyph(line[i])[0].Length + 1;

            return Math.Max(0, width - 1);
        }

        private static string[] ResolveGlyph(char ch)
        {
            return glyphs.TryGetValue(ch, out string[] glyph) ? glyph : fallback;
        }
    }
}
