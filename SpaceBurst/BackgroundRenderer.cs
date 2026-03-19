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
            float activeEventIntensity,
            Vector2 cameraOffset,
            float transitionWarp,
            float rewindStrength,
            float impactPulse,
            float pickupPulse,
            VisualPreset preset)
        {
            BackgroundMoodDefinition resolvedMood = mood ?? new BackgroundMoodDefinition();
            Color backColor = ColorUtil.ParseHex(resolvedMood.PrimaryColor, new Color(10, 14, 22));
            Color midColor = ColorUtil.ParseHex(resolvedMood.SecondaryColor, new Color(26, 34, 52));
            Color accentColor = ColorUtil.ParseHex(resolvedMood.AccentColor, new Color(110, 193, 255));
            Color glowColor = ColorUtil.ParseHex(resolvedMood.GlowColor, new Color(246, 198, 116));
            float light = MathHelper.Clamp(resolvedMood.LightIntensity, 0.35f, 1.4f);
            float contrast = MathHelper.Clamp(resolvedMood.Contrast, 0.55f, 1.2f);
            float time = (float)Game1.GameTime.TotalGameTime.TotalSeconds;
            float warpStrength = MathHelper.Clamp(transitionWarp, 0f, 1f);
            float rewindTint = MathHelper.Clamp(rewindStrength, 0f, 1f);
            float eventBoost = activeEvent == RandomEventType.SolarFlare ? activeEventIntensity * 0.18f : 0f;
            float overscan = 96f;
            Rectangle world = new Rectangle(-(int)overscan, -(int)overscan, Game1.VirtualWidth + (int)overscan * 2, Game1.VirtualHeight + (int)overscan * 2);

            spriteBatch.Draw(pixel, world, backColor);
            spriteBatch.Draw(pixel, world, midColor * (0.05f + contrast * 0.05f + pickupPulse * 0.04f));
            if (rewindTint > 0f)
                spriteBatch.Draw(pixel, world, Color.Cyan * (0.03f + rewindTint * 0.08f));

            DrawNebulaCloud(spriteBatch, radialTexture, new Vector2(Game1.VirtualWidth * 0.14f, Game1.VirtualHeight * 0.24f), 480f + 50f * light, accentColor * (0.1f + 0.04f * light), cameraOffset * 0.06f);
            DrawNebulaCloud(spriteBatch, radialTexture, new Vector2(Game1.VirtualWidth * 0.48f, Game1.VirtualHeight * 0.52f), 560f + 80f * contrast, midColor * (0.08f + 0.05f * contrast), cameraOffset * 0.12f);
            DrawNebulaCloud(spriteBatch, radialTexture, new Vector2(Game1.VirtualWidth * 0.82f, Game1.VirtualHeight * 0.18f), 320f + 40f * light, glowColor * (0.09f + 0.06f * light + eventBoost), cameraOffset * 0.08f);
            DrawNebulaCloud(spriteBatch, radialTexture, new Vector2(Game1.VirtualWidth * 0.76f, Game1.VirtualHeight * 0.72f), 260f + 50f * contrast, accentColor * (0.05f + impactPulse * 0.06f), cameraOffset * 0.2f);

            DrawPlanet(spriteBatch, radialTexture, resolvedMood, seed, new Vector2(Game1.VirtualWidth * 0.8f, Game1.VirtualHeight * 0.2f), 134f + resolvedMood.PlanetPresence * 50f, cameraOffset * 0.04f);
            DrawPlanet(spriteBatch, radialTexture, resolvedMood, seed + 17, new Vector2(Game1.VirtualWidth * 0.12f, Game1.VirtualHeight * 0.76f), 82f + resolvedMood.PlanetPresence * 30f, cameraOffset * 0.15f);

            DrawStarField(spriteBatch, pixel, seed, scrollSpeed, 0.08f, 120, accentColor * 0.42f, 1, resolvedMood.StarDensity, cameraOffset * 0.05f, transitionWarp, false);
            DrawStarField(spriteBatch, pixel, seed + 7, scrollSpeed, 0.22f, 96, Color.White * 0.45f, 2, resolvedMood.StarDensity, cameraOffset * 0.1f, transitionWarp, false);
            DrawStarField(spriteBatch, pixel, seed + 13, scrollSpeed, 0.38f, 68, glowColor * 0.55f, 2, resolvedMood.StarDensity * 0.9f, cameraOffset * 0.18f, transitionWarp, true);
            DrawDust(spriteBatch, pixel, seed + 3, scrollSpeed, accentColor, resolvedMood, cameraOffset * 0.28f, transitionWarp, rewindStrength);
            DrawNearDebris(spriteBatch, pixel, seed + 11, scrollSpeed, difficulty, activeEvent, activeEventIntensity, cameraOffset * 0.42f, transitionWarp, preset);
            DrawGlints(spriteBatch, radialTexture, seed + 29, accentColor, glowColor, cameraOffset * 0.16f, transitionWarp, time);

            if (activeEvent == RandomEventType.SolarFlare)
            {
                Color flare = Color.Lerp(glowColor, Color.White, 0.35f) * (0.08f + activeEventIntensity * 0.2f);
                spriteBatch.Draw(pixel, world, flare);
                DrawNebulaCloud(spriteBatch, radialTexture, new Vector2(Game1.VirtualWidth * 0.94f, Game1.VirtualHeight * 0.12f), 420f, flare, cameraOffset * 0.03f);
            }
        }

        private static void DrawPlanet(SpriteBatch spriteBatch, Texture2D radialTexture, BackgroundMoodDefinition mood, int seed, Vector2 center, float radius, Vector2 offset)
        {
            float presence = mood?.PlanetPresence ?? 0.5f;
            if (presence <= 0.15f)
                return;

            Color bodyColor = Color.Lerp(ColorUtil.ParseHex(mood.SecondaryColor, Color.CornflowerBlue), ColorUtil.ParseHex(mood.AccentColor, Color.LightGoldenrodYellow), Hash01(seed, 3));
            DrawNebulaCloud(spriteBatch, radialTexture, center + offset, radius * 1.6f, bodyColor * 0.08f, Vector2.Zero);
            spriteBatch.Draw(radialTexture, center + offset, null, bodyColor * 0.44f, 0f, new Vector2(radialTexture.Width / 2f, radialTexture.Height / 2f), radius * 2f / radialTexture.Width, SpriteEffects.None, 0f);
            spriteBatch.Draw(radialTexture, center + offset + new Vector2(-radius * 0.18f, -radius * 0.1f), null, Color.White * 0.06f, 0f, new Vector2(radialTexture.Width / 2f, radialTexture.Height / 2f), radius * 1.4f / radialTexture.Width, SpriteEffects.None, 0f);
        }

        private static void DrawNebulaCloud(SpriteBatch spriteBatch, Texture2D radialTexture, Vector2 center, float radius, Color color, Vector2 offset)
        {
            spriteBatch.Draw(radialTexture, center + offset, null, color, 0f, new Vector2(radialTexture.Width / 2f, radialTexture.Height / 2f), radius * 2f / radialTexture.Width, SpriteEffects.None, 0f);
        }

        private static void DrawStarField(SpriteBatch spriteBatch, Texture2D pixel, int seed, float scrollSpeed, float parallax, int count, Color color, int size, float density, Vector2 cameraOffset, float transitionWarp, bool elongated)
        {
            int finalCount = Math.Max(10, (int)(count * Math.Max(0.35f, density)));
            float time = (float)Game1.GameTime.TotalGameTime.TotalSeconds;
            float offset = (time * scrollSpeed * parallax) % (Game1.VirtualWidth + 120f);
            float stretch = elongated ? 1f + transitionWarp * 8f : 1f + transitionWarp * 4f;

            for (int i = 0; i < finalCount; i++)
            {
                float xSeed = Hash01(seed, i * 17 + 1);
                float ySeed = Hash01(seed, i * 17 + 5);
                float twinkle = 0.45f + 0.55f * MathF.Sin(time * (1.5f + Hash01(seed, i * 17 + 9) * 2f) + i);
                float x = Game1.VirtualWidth + 60f - ((xSeed * (Game1.VirtualWidth + 120f) + offset) % (Game1.VirtualWidth + 120f));
                float y = 24f + ySeed * (Game1.VirtualHeight - 48f);
                Vector2 position = new Vector2(x, y) + cameraOffset;
                int width = Math.Max(1, (int)(size * stretch));
                spriteBatch.Draw(pixel, new Rectangle((int)position.X, (int)position.Y, width, size), color * twinkle);
            }
        }

        private static void DrawDust(SpriteBatch spriteBatch, Texture2D pixel, int seed, float scrollSpeed, Color accentColor, BackgroundMoodDefinition mood, Vector2 cameraOffset, float transitionWarp, float rewindStrength)
        {
            int count = Math.Max(10, (int)(22f * mood.DustDensity));
            float time = (float)Game1.GameTime.TotalGameTime.TotalSeconds;
            float offset = (time * scrollSpeed * 0.18f) % (Game1.VirtualWidth + 140f);

            for (int i = 0; i < count; i++)
            {
                float xSeed = Hash01(seed, i * 31 + 2);
                float ySeed = Hash01(seed, i * 31 + 7);
                float width = 34f + Hash01(seed, i * 31 + 11) * 96f;
                float height = 1f + Hash01(seed, i * 31 + 13) * 3f;
                float x = Game1.VirtualWidth + 70f - ((xSeed * (Game1.VirtualWidth + 140f) + offset) % (Game1.VirtualWidth + 140f));
                float y = 34f + ySeed * (Game1.VirtualHeight - 68f);
                float warpStretch = 1f + transitionWarp * 6f;
                Color tint = Color.Lerp(accentColor * 0.06f, Color.Cyan * 0.08f, rewindStrength * 0.6f);
                Vector2 position = new Vector2(x, y) + cameraOffset;
                spriteBatch.Draw(pixel, new Rectangle((int)position.X, (int)position.Y, (int)(width * warpStretch), (int)height), tint);
            }
        }

        private static void DrawNearDebris(SpriteBatch spriteBatch, Texture2D pixel, int seed, float scrollSpeed, float difficulty, RandomEventType activeEvent, float activeEventIntensity, Vector2 cameraOffset, float transitionWarp, VisualPreset preset)
        {
            int count = 12 + (int)(difficulty * 8f);
            if (activeEvent == RandomEventType.MeteorShower || activeEvent == RandomEventType.CometSwarm)
                count += (int)(14f * activeEventIntensity);

            float time = (float)Game1.GameTime.TotalGameTime.TotalSeconds;
            for (int i = 0; i < count; i++)
            {
                float velocity = scrollSpeed * (0.38f + Hash01(seed, i * 23 + 4) * 0.7f) + transitionWarp * 320f;
                float x = Game1.VirtualWidth + 40f - ((time * velocity + Hash01(seed, i * 23 + 7) * (Game1.VirtualWidth + 160f)) % (Game1.VirtualWidth + 160f));
                float y = 30f + Hash01(seed, i * 23 + 12) * (Game1.VirtualHeight - 60f);
                int width = 4 + (int)(Hash01(seed, i * 23 + 19) * 14f);
                int height = 2 + (int)(Hash01(seed, i * 23 + 21) * 6f);
                Color color = activeEvent == RandomEventType.CometSwarm ? new Color(255, 214, 153) : new Color(255, 255, 255);
                float alpha = activeEvent == RandomEventType.CometSwarm ? 0.24f : 0.08f + difficulty * 0.08f;
                if (preset == VisualPreset.Neon)
                    alpha += 0.02f;
                Vector2 position = new Vector2(x, y) + cameraOffset;
                spriteBatch.Draw(pixel, new Rectangle((int)position.X, (int)position.Y, width + (int)(transitionWarp * 16f), height), color * alpha);
            }
        }

        private static void DrawGlints(SpriteBatch spriteBatch, Texture2D radialTexture, int seed, Color accentColor, Color glowColor, Vector2 cameraOffset, float transitionWarp, float time)
        {
            int count = 4 + (int)(transitionWarp * 3f);
            for (int i = 0; i < count; i++)
            {
                float x = Hash01(seed, i * 13 + 1) * Game1.VirtualWidth;
                float y = Hash01(seed, i * 13 + 3) * Game1.VirtualHeight;
                float pulse = 0.04f + MathF.Abs(MathF.Sin(time * (0.8f + i * 0.13f))) * 0.08f;
                Color tint = Color.Lerp(accentColor, glowColor, Hash01(seed, i * 13 + 7)) * (pulse + transitionWarp * 0.1f);
                spriteBatch.Draw(radialTexture, new Vector2(x, y) + cameraOffset, null, tint, 0f, new Vector2(radialTexture.Width / 2f, radialTexture.Height / 2f), 48f / radialTexture.Width, SpriteEffects.None, 0f);
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
