using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceBurst.RuntimeData;
using System;

namespace SpaceBurst
{
    static class BackgroundRenderer
    {
        public static void Draw(
            SpriteBatch spriteBatch,
            Texture2D pixel,
            Texture2D radialTexture,
            BackgroundMoodDefinition mood,
            int seed,
            float scrollSpeed,
            float difficulty,
            RandomEventType activeEvent,
            float activeEventIntensity)
        {
            BackgroundMoodDefinition resolvedMood = mood ?? new BackgroundMoodDefinition();
            Color backColor = ColorUtil.ParseHex(resolvedMood.PrimaryColor, new Color(10, 14, 22));
            Color midColor = ColorUtil.ParseHex(resolvedMood.SecondaryColor, new Color(26, 34, 52));
            Color accentColor = ColorUtil.ParseHex(resolvedMood.AccentColor, new Color(110, 193, 255));
            Color glowColor = ColorUtil.ParseHex(resolvedMood.GlowColor, new Color(246, 198, 116));
            float light = MathHelper.Clamp(resolvedMood.LightIntensity, 0.35f, 1.4f);
            float contrast = MathHelper.Clamp(resolvedMood.Contrast, 0.55f, 1.2f);

            spriteBatch.Draw(pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), backColor);
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), midColor * (0.06f + 0.04f * contrast));

            DrawNebulaBand(spriteBatch, radialTexture, new Vector2(Game1.VirtualWidth * 0.18f, Game1.VirtualHeight * 0.32f), 420f + 40f * light, accentColor * (0.16f + 0.06f * light));
            DrawNebulaBand(spriteBatch, radialTexture, new Vector2(Game1.VirtualWidth * 0.58f, Game1.VirtualHeight * 0.52f), 520f + 60f * contrast, midColor * (0.10f + 0.05f * contrast));
            DrawNebulaBand(spriteBatch, radialTexture, new Vector2(Game1.VirtualWidth * 0.84f, Game1.VirtualHeight * 0.22f), 300f + 30f * light, glowColor * (0.12f + 0.05f * light));
            DrawNebulaBand(spriteBatch, radialTexture, new Vector2(Game1.VirtualWidth * 0.78f, Game1.VirtualHeight * 0.78f), 240f + 45f * contrast, accentColor * 0.08f);
            DrawNebulaBand(spriteBatch, radialTexture, new Vector2(Game1.VirtualWidth * 0.08f, Game1.VirtualHeight * 0.82f), 180f + 20f * light, glowColor * 0.06f);

            DrawPlanet(spriteBatch, radialTexture, resolvedMood, seed, new Vector2(Game1.VirtualWidth * 0.78f, Game1.VirtualHeight * 0.22f), 120f + resolvedMood.PlanetPresence * 50f);
            DrawPlanet(spriteBatch, radialTexture, resolvedMood, seed + 19, new Vector2(Game1.VirtualWidth * 0.18f, Game1.VirtualHeight * 0.76f), 72f + resolvedMood.PlanetPresence * 30f);

            DrawStarLayer(spriteBatch, pixel, seed, scrollSpeed, 0.10f, 96, accentColor * 0.45f, 1, resolvedMood.StarDensity);
            DrawStarLayer(spriteBatch, pixel, seed + 7, scrollSpeed, 0.24f, 84, Color.White * 0.45f, 2, resolvedMood.StarDensity);
            DrawStarLayer(spriteBatch, pixel, seed + 13, scrollSpeed, 0.42f, 48, glowColor * 0.55f, 2, resolvedMood.StarDensity * 0.85f);
            DrawDust(spriteBatch, pixel, seed + 3, scrollSpeed, accentColor, resolvedMood);
            DrawDebris(spriteBatch, pixel, seed + 11, scrollSpeed, difficulty, activeEvent, activeEventIntensity);

            if (activeEvent == RandomEventType.SolarFlare)
            {
                Color flare = Color.Lerp(glowColor, Color.White, 0.35f) * (0.12f + activeEventIntensity * 0.22f);
                spriteBatch.Draw(pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), flare);
                DrawNebulaBand(spriteBatch, radialTexture, new Vector2(Game1.VirtualWidth * 0.9f, Game1.VirtualHeight * 0.1f), 360f, flare);
            }
        }

        private static void DrawPlanet(SpriteBatch spriteBatch, Texture2D radialTexture, BackgroundMoodDefinition mood, int seed, Vector2 center, float radius)
        {
            float presence = mood?.PlanetPresence ?? 0.5f;
            if (presence <= 0.15f)
                return;

            Color bodyColor = Color.Lerp(
                ColorUtil.ParseHex(mood.SecondaryColor, Color.CornflowerBlue),
                ColorUtil.ParseHex(mood.AccentColor, Color.LightGoldenrodYellow),
                Hash01(seed, 3));
            DrawNebulaBand(spriteBatch, radialTexture, center, radius * 1.6f, bodyColor * 0.08f);
            spriteBatch.Draw(
                radialTexture,
                center,
                null,
                bodyColor * 0.5f,
                0f,
                new Vector2(radialTexture.Width / 2f, radialTexture.Height / 2f),
                radius * 2f / radialTexture.Width,
                SpriteEffects.None,
                0f);
        }

        private static void DrawNebulaBand(SpriteBatch spriteBatch, Texture2D radialTexture, Vector2 center, float radius, Color color)
        {
            spriteBatch.Draw(
                radialTexture,
                center,
                null,
                color,
                0f,
                new Vector2(radialTexture.Width / 2f, radialTexture.Height / 2f),
                radius * 2f / radialTexture.Width,
                SpriteEffects.None,
                0f);
        }

        private static void DrawStarLayer(SpriteBatch spriteBatch, Texture2D pixel, int seed, float scrollSpeed, float parallax, int count, Color color, int size, float density)
        {
            int finalCount = Math.Max(8, (int)(count * Math.Max(0.35f, density)));
            float time = (float)Game1.GameTime.TotalGameTime.TotalSeconds;
            float offset = (time * scrollSpeed * parallax) % (Game1.VirtualWidth + 80f);

            for (int i = 0; i < finalCount; i++)
            {
                float xSeed = Hash01(seed, i * 17 + 1);
                float ySeed = Hash01(seed, i * 17 + 5);
                float twinkle = 0.45f + 0.55f * MathF.Sin(time * (1.5f + Hash01(seed, i * 17 + 9) * 2f) + i);
                float x = Game1.VirtualWidth + 40f - ((xSeed * (Game1.VirtualWidth + 80f) + offset) % (Game1.VirtualWidth + 80f));
                float y = 24f + ySeed * (Game1.VirtualHeight - 48f);
                spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, size, size), color * twinkle);
            }
        }

        private static void DrawDust(SpriteBatch spriteBatch, Texture2D pixel, int seed, float scrollSpeed, Color accentColor, BackgroundMoodDefinition mood)
        {
            int count = Math.Max(10, (int)(18f * mood.DustDensity));
            float time = (float)Game1.GameTime.TotalGameTime.TotalSeconds;
            float offset = (time * scrollSpeed * 0.18f) % (Game1.VirtualWidth + 120f);

            for (int i = 0; i < count; i++)
            {
                float xSeed = Hash01(seed, i * 31 + 2);
                float ySeed = Hash01(seed, i * 31 + 7);
                float width = 28f + Hash01(seed, i * 31 + 11) * 72f;
                float height = 2f + Hash01(seed, i * 31 + 13) * 3f;
                float x = Game1.VirtualWidth + 60f - ((xSeed * (Game1.VirtualWidth + 120f) + offset) % (Game1.VirtualWidth + 120f));
                float y = 64f + ySeed * (Game1.VirtualHeight - 128f);
                spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, (int)width, (int)height), accentColor * 0.08f);
            }
        }

        private static void DrawDebris(SpriteBatch spriteBatch, Texture2D pixel, int seed, float scrollSpeed, float difficulty, RandomEventType activeEvent, float activeEventIntensity)
        {
            int count = 10 + (int)(difficulty * 6f);
            if (activeEvent == RandomEventType.MeteorShower || activeEvent == RandomEventType.CometSwarm)
                count += (int)(12f * activeEventIntensity);

            float time = (float)Game1.GameTime.TotalGameTime.TotalSeconds;
            for (int i = 0; i < count; i++)
            {
                float velocity = scrollSpeed * (0.25f + Hash01(seed, i * 23 + 4) * 0.55f);
                float x = Game1.VirtualWidth - ((time * velocity + Hash01(seed, i * 23 + 7) * (Game1.VirtualWidth + 120f)) % (Game1.VirtualWidth + 120f));
                float y = 36f + Hash01(seed, i * 23 + 12) * (Game1.VirtualHeight - 72f);
                int width = 4 + (int)(Hash01(seed, i * 23 + 19) * 10f);
                int height = 2 + (int)(Hash01(seed, i * 23 + 21) * 5f);
                Color color = activeEvent == RandomEventType.CometSwarm ? new Color(255, 214, 153) : new Color(255, 255, 255);
                float alpha = activeEvent == RandomEventType.CometSwarm ? 0.22f : 0.08f + difficulty * 0.06f;
                spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, width, height), color * alpha);
            }
        }

        private static float Hash01(int seed, int salt)
        {
            int value = seed * 73856093 ^ salt * 19349663;
            value ^= value >> 13;
            value *= 1274126177;
            value ^= value >> 16;
            uint positive = unchecked((uint)value);
            return (positive & 0x00FFFFFF) / 16777215f;
        }
    }
}
