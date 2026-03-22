using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceBurst.RuntimeData;
using System;

namespace SpaceBurst
{
    internal readonly struct HudPresenterContext
    {
        public HudPresenterContext(
            HudLayout layout,
            WeaponInventoryState inventory,
            string stageLabel,
            string stageSubLabel,
            Color stageSubColor,
            string presentationLabel,
            float hudPulse,
            float pickupPulse,
            float pityRatio,
            float rewindRatio,
            float xpRatio,
            int runLevel,
            int scrap,
            bool showAndroidPauseChip)
        {
            Layout = layout;
            Inventory = inventory;
            StageLabel = stageLabel ?? string.Empty;
            StageSubLabel = stageSubLabel ?? string.Empty;
            StageSubColor = stageSubColor;
            PresentationLabel = presentationLabel ?? string.Empty;
            HudPulse = hudPulse;
            PickupPulse = pickupPulse;
            PityRatio = pityRatio;
            RewindRatio = rewindRatio;
            XpRatio = xpRatio;
            RunLevel = runLevel;
            Scrap = scrap;
            ShowAndroidPauseChip = showAndroidPauseChip;
        }

        public HudLayout Layout { get; }
        public WeaponInventoryState Inventory { get; }
        public string StageLabel { get; }
        public string StageSubLabel { get; }
        public Color StageSubColor { get; }
        public string PresentationLabel { get; }
        public float HudPulse { get; }
        public float PickupPulse { get; }
        public float PityRatio { get; }
        public float RewindRatio { get; }
        public float XpRatio { get; }
        public int RunLevel { get; }
        public int Scrap { get; }
        public bool ShowAndroidPauseChip { get; }
    }

    internal static class HudPresenter
    {
        public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, HudPresenterContext context)
        {
            Rectangle livesBounds = context.Layout.LivesBounds;
            Rectangle activeBounds = context.Layout.ActiveBounds;
            Rectangle ownedBounds = context.Layout.OwnedBounds;
            Rectangle stageBounds = context.Layout.StageBounds;
            Rectangle pityBounds = context.Layout.PityBounds;
            Rectangle scoreBounds = context.Layout.ScoreBounds;

            DrawPanel(spriteBatch, pixel, livesBounds, Color.Black * (0.26f + context.HudPulse * 0.05f), Color.White * (0.2f + context.HudPulse * 0.08f));
            DrawPanel(spriteBatch, pixel, activeBounds, Color.Black * (0.2f + context.PickupPulse * 0.06f), Color.White * (0.16f + context.PickupPulse * 0.1f));
            DrawPanel(spriteBatch, pixel, ownedBounds, Color.Black * 0.18f, Color.White * 0.14f);
            DrawPanel(spriteBatch, pixel, stageBounds, Color.Black * (0.2f + context.HudPulse * 0.03f), Color.White * 0.16f);
            DrawPanel(spriteBatch, pixel, pityBounds, Color.Black * (0.18f + context.PickupPulse * 0.04f), Color.White * 0.14f);
            DrawPanel(spriteBatch, pixel, scoreBounds, Color.Black * 0.24f, Color.White * (0.18f + context.HudPulse * 0.05f));

            BitmapFontRenderer.Draw(spriteBatch, pixel, string.Concat("LIVES ", PlayerStatus.Lives.ToString()), new Vector2(livesBounds.X + 12f, livesBounds.Y + 10f), Color.White, 1.9f);
            BitmapFontRenderer.Draw(spriteBatch, pixel, string.Concat("SHIPS ", PlayerStatus.Ships.ToString()), new Vector2(livesBounds.X + 12f, livesBounds.Y + 34f), Color.White, 1.9f);
            BitmapFontRenderer.Draw(spriteBatch, pixel, string.Concat("HULL ", Math.Max(0f, Player1.Instance.HullRatio * 100f).ToString("0"), "%"), new Vector2(livesBounds.X + 12f, livesBounds.Bottom - 22f), Color.White * 0.7f, 1.08f);

            DrawCoreWeapon(spriteBatch, pixel, activeBounds, context.Inventory);
            DrawWeaponStack(spriteBatch, pixel, ownedBounds, context.Inventory);

            DrawCenteredText(spriteBatch, pixel, context.StageLabel, stageBounds.Center.X, stageBounds.Y + 12f, Color.White, GetFittedScale(context.StageLabel, stageBounds.Width - 20f, 1.7f, 1.08f));
            DrawCenteredText(spriteBatch, pixel, context.StageSubLabel, stageBounds.Center.X, stageBounds.Y + 42f, context.StageSubColor, GetFittedScale(context.StageSubLabel, stageBounds.Width - 20f, 1.06f, 0.8f));
            DrawCenteredText(spriteBatch, pixel, context.PresentationLabel, stageBounds.Center.X, stageBounds.Bottom - 18f, Color.White * 0.58f, GetFittedScale(context.PresentationLabel, stageBounds.Width - 24f, 0.76f, 0.6f));

            if (context.ShowAndroidPauseChip)
            {
                Rectangle pauseChip = HudLayoutCalculator.GetAndroidPauseChipBounds(context.Layout);
                DrawPanel(spriteBatch, pixel, pauseChip, Color.Black * 0.32f, Color.Orange * 0.46f);
                DrawCenteredText(spriteBatch, pixel, "||", pauseChip.Center.X, pauseChip.Y + 3f, Color.White, 1.18f);
            }

            BitmapFontRenderer.Draw(spriteBatch, pixel, string.Concat("LV ", context.RunLevel.ToString()), new Vector2(pityBounds.X + 12f, pityBounds.Y + 10f), Color.White, 1.28f);
            DrawBar(spriteBatch, pixel, new Rectangle(pityBounds.X + 12, pityBounds.Y + 34, pityBounds.Width - 24, 12), context.XpRatio, Color.Lerp(Color.Cyan, Color.White, context.PickupPulse * 0.35f));
            BitmapFontRenderer.Draw(spriteBatch, pixel, string.Concat("PITY ", MathF.Round(context.PityRatio * 100f).ToString("0"), "%"), new Vector2(pityBounds.X + 12f, pityBounds.Y + 52f), Color.White * 0.62f, 0.92f);
            BitmapFontRenderer.Draw(spriteBatch, pixel, string.Concat("SCRAP ", context.Scrap.ToString()), new Vector2(pityBounds.X + 12f, pityBounds.Bottom - 22f), Color.Orange * 0.92f, 1f);

            BitmapFontRenderer.Draw(spriteBatch, pixel, string.Concat("SCORE ", PlayerStatus.Score.ToString()), new Vector2(scoreBounds.X + 12f, scoreBounds.Y + 10f), Color.White, 1.52f);
            BitmapFontRenderer.Draw(spriteBatch, pixel, string.Concat("MULTI ", PlayerStatus.Multiplier.ToString(), "  CHAIN ", PlayerStatus.RunProgress.KillChainTier.ToString()), new Vector2(scoreBounds.X + 12f, scoreBounds.Y + 34f), Color.White, 1.12f);
            BitmapFontRenderer.Draw(spriteBatch, pixel, "REWIND", new Vector2(scoreBounds.X + 12f, scoreBounds.Y + 56f), Color.White * 0.84f, 1.06f);
            DrawBar(spriteBatch, pixel, new Rectangle(scoreBounds.X + 92, scoreBounds.Y + 58, scoreBounds.Width - 104, 10), context.RewindRatio, Color.Lerp(Color.Cyan, Color.White, context.HudPulse * 0.2f) * 0.9f);
        }

        private static void DrawCoreWeapon(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, WeaponInventoryState inventory)
        {
            WeaponStyleDefinition activeStyle = WeaponCatalog.GetStyle(inventory.ActiveStyle);
            Color primary = ColorUtil.ParseHex(activeStyle.PrimaryColor, Color.White);
            Color secondary = ColorUtil.ParseHex(activeStyle.SecondaryColor, Color.LightBlue);
            Color accent = ColorUtil.ParseHex(activeStyle.AccentColor, Color.Orange);
            PixelArtRenderer.DrawRows(spriteBatch, pixel, activeStyle.IconRows, new Vector2(bounds.X + 22f, bounds.Y + 48f), 4.5f, primary, secondary, accent, true);
            BitmapFontRenderer.Draw(spriteBatch, pixel, activeStyle.DisplayName, new Vector2(bounds.X + 60f, bounds.Y + 8f), Color.White, GetFittedScale(activeStyle.DisplayName, bounds.Width - 72f, 1.72f, 1.05f));
            BitmapFontRenderer.Draw(spriteBatch, pixel, string.Concat("CORE  LV ", inventory.ActiveLevel.ToString(), inventory.ActiveRank > 0 ? string.Concat("  RK ", inventory.ActiveRank.ToString()) : string.Empty), new Vector2(bounds.X + 60f, bounds.Y + 32f), Color.White * 0.72f, 1.08f);
            for (int i = 0; i < 4; i++)
            {
                Color pip = i <= inventory.ActiveLevel ? accent : Color.White * 0.14f;
                spriteBatch.Draw(pixel, new Rectangle(bounds.X + 62 + i * 18, bounds.Y + 58, 12, 12), pip);
            }
        }

        private static void DrawWeaponStack(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, WeaponInventoryState inventory)
        {
            BitmapFontRenderer.Draw(spriteBatch, pixel, "STACK", new Vector2(bounds.X + 12f, bounds.Y + 10f), Color.White * 0.8f, 1.02f);
            BitmapFontRenderer.Draw(spriteBatch, pixel, string.Concat("SUPPORT ", inventory.SupportWeapons.Count.ToString(), "  PASSIVE ", inventory.PassiveReactors.Count.ToString()), new Vector2(bounds.X + 12f, bounds.Y + 28f), Color.White * 0.56f, 0.9f);

            float x = bounds.X + 20f;
            for (int i = 0; i < inventory.SupportWeapons.Count; i++)
            {
                WeaponStyleId styleId = inventory.SupportWeapons[i];
                WeaponStyleDefinition style = WeaponCatalog.GetStyle(styleId);
                PixelArtRenderer.DrawRows(spriteBatch, pixel, style.IconRows, new Vector2(x, bounds.Y + 58f), 1.8f, ColorUtil.ParseHex(style.PrimaryColor, Color.White), ColorUtil.ParseHex(style.SecondaryColor, Color.LightBlue), ColorUtil.ParseHex(style.AccentColor, Color.Orange), true);
                x += 34f;
            }

            for (int i = 0; i < inventory.PassiveReactors.Count; i++)
            {
                Rectangle chip = new Rectangle((int)x, bounds.Y + 50, 44, 18);
                DrawPanel(spriteBatch, pixel, chip, Color.White * 0.06f, Color.White * 0.14f);
                DrawCenteredText(spriteBatch, pixel, GetPassiveAbbreviation(inventory.PassiveReactors[i]), chip.Center.X, chip.Y + 4f, Color.White, 0.78f);
                x += 50f;
            }
        }

        private static string GetPassiveAbbreviation(PassiveReactorId passive)
        {
            return passive switch
            {
                PassiveReactorId.Overclock => "OC",
                PassiveReactorId.MagnetCore => "MG",
                PassiveReactorId.ArmorPlating => "AR",
                PassiveReactorId.TimeBattery => "TB",
                PassiveReactorId.SalvageNode => "SN",
                _ => "CR",
            };
        }

        private static void DrawPanel(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, Color fill, Color border)
        {
            spriteBatch.Draw(pixel, bounds, fill);
            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), border);
            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), border);
            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), border);
            spriteBatch.Draw(pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), border);
        }

        private static void DrawBar(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, float ratio, Color fill)
        {
            spriteBatch.Draw(pixel, bounds, Color.White * 0.08f);
            Rectangle inner = new Rectangle(bounds.X, bounds.Y, (int)MathF.Round(bounds.Width * MathHelper.Clamp(ratio, 0f, 1f)), bounds.Height);
            if (inner.Width > 0)
                spriteBatch.Draw(pixel, inner, fill);
        }

        private static float GetFittedScale(string text, float maxWidth, float preferredScale, float minimumScale)
        {
            if (string.IsNullOrWhiteSpace(text))
                return preferredScale;

            float measuredWidth = BitmapFontRenderer.Measure(text, preferredScale).X;
            if (measuredWidth <= maxWidth || measuredWidth <= 0f)
                return preferredScale;

            float ratio = maxWidth / measuredWidth;
            return MathHelper.Clamp(preferredScale * ratio, minimumScale, preferredScale);
        }

        private static void DrawCenteredText(SpriteBatch spriteBatch, Texture2D pixel, string text, float x, float y, Color color, float scale)
        {
            Vector2 size = BitmapFontRenderer.Measure(text, scale);
            BitmapFontRenderer.Draw(spriteBatch, pixel, text, new Vector2(x - size.X / 2f, y), color, scale);
        }
    }
}
