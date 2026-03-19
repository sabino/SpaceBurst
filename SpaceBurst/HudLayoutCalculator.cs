using Microsoft.Xna.Framework;
using SpaceBurst.RuntimeData;

namespace SpaceBurst
{
    internal readonly struct HudLayout
    {
        public HudLayout(Rectangle livesBounds, Rectangle activeBounds, Rectangle ownedBounds, Rectangle stageBounds, Rectangle pityBounds, Rectangle scoreBounds)
        {
            LivesBounds = livesBounds;
            ActiveBounds = activeBounds;
            OwnedBounds = ownedBounds;
            StageBounds = stageBounds;
            PityBounds = pityBounds;
            ScoreBounds = scoreBounds;
        }

        public Rectangle LivesBounds { get; }
        public Rectangle ActiveBounds { get; }
        public Rectangle OwnedBounds { get; }
        public Rectangle StageBounds { get; }
        public Rectangle PityBounds { get; }
        public Rectangle ScoreBounds { get; }
    }

    internal static class HudLayoutCalculator
    {
        public static HudLayout Calculate(int virtualWidth, GameFlowState state, int currentStageNumber, int transitionTargetStageNumber, bool transitionToBoss, WeaponInventoryState inventory, int score)
        {
            float uiScale = Game1.Instance != null ? Game1.Instance.UiLayoutScale : 1f;
            int margin = Scale(12, uiScale);
            int gap = Scale(10, uiScale);
            int top = Scale(10, uiScale);
            int height = Scale(88, uiScale);

            WeaponStyleDefinition activeStyle = WeaponCatalog.GetStyle(inventory.ActiveStyle);
            string stageLabel = state switch
            {
                GameFlowState.Tutorial => "TUTORIAL",
                GameFlowState.UpgradeDraft => "UPGRADE",
                _ when state == GameFlowState.StageTransition && transitionToBoss => "BOSS RUN",
                _ when state == GameFlowState.StageTransition && transitionTargetStageNumber > currentStageNumber => string.Concat("JUMP ", transitionTargetStageNumber.ToString("00")),
                _ => string.Concat("STAGE ", currentStageNumber.ToString("00")),
            };
            string scoreLabel = string.Concat("SCORE ", score.ToString());

            int totalWidth = virtualWidth - margin * 2 - gap * 5;
            int livesWidth = MeasureHudWidth(string.Concat("LIVES ", PlayerStatus.Lives.ToString()), 2f, Scale(40, uiScale), Scale(200, uiScale));
            int activeWidth = MeasureHudWidth(activeStyle.DisplayName, 1.8f, Scale(110, uiScale), Scale(220, uiScale));
            int stageWidth = MeasureHudWidth(stageLabel, 1.7f, Scale(54, uiScale), Scale(170, uiScale));
            int pityWidth = Scale(190, uiScale);
            int scoreWidth = MeasureHudWidth(scoreLabel, 1.65f, Scale(186, uiScale), Scale(272, uiScale));
            int ownedWidth = totalWidth - livesWidth - activeWidth - stageWidth - pityWidth - scoreWidth;

            if (ownedWidth < Scale(220, uiScale))
            {
                int deficit = Scale(220, uiScale) - ownedWidth;
                int reduceScore = System.Math.Min(deficit, System.Math.Max(0, scoreWidth - Scale(240, uiScale)));
                scoreWidth -= reduceScore;
                deficit -= reduceScore;

                int reduceActive = System.Math.Min(deficit, System.Math.Max(0, activeWidth - Scale(200, uiScale)));
                activeWidth -= reduceActive;
                deficit -= reduceActive;

                int reduceStage = System.Math.Min(deficit, System.Math.Max(0, stageWidth - Scale(150, uiScale)));
                stageWidth -= reduceStage;
                ownedWidth = totalWidth - livesWidth - activeWidth - stageWidth - pityWidth - scoreWidth;
            }

            Rectangle livesBounds = new Rectangle(margin, top, livesWidth, height);
            Rectangle activeBounds = new Rectangle(livesBounds.Right + gap, top, activeWidth, height);
            Rectangle ownedBounds = new Rectangle(activeBounds.Right + gap, top, ownedWidth, height);
            Rectangle stageBounds = new Rectangle(ownedBounds.Right + gap, top, stageWidth, height);
            Rectangle pityBounds = new Rectangle(stageBounds.Right + gap, top, pityWidth, height);
            Rectangle scoreBounds = new Rectangle(pityBounds.Right + gap, top, scoreWidth, height);

            return new HudLayout(livesBounds, activeBounds, ownedBounds, stageBounds, pityBounds, scoreBounds);
        }

        public static Rectangle GetAndroidWeaponTouchBounds(HudLayout layout)
        {
            return Rectangle.Union(layout.ActiveBounds, layout.OwnedBounds);
        }

        public static Rectangle GetAndroidPauseChipBounds(HudLayout layout)
        {
            Rectangle stageBounds = layout.StageBounds;
            float uiScale = Game1.Instance != null ? Game1.Instance.UiLayoutScale : 1f;
            int width = Scale(42, uiScale);
            int height = Scale(24, uiScale);
            return new Rectangle(stageBounds.Center.X - width / 2, stageBounds.Bottom - height - Scale(10, uiScale), width, height);
        }

        public static Rectangle GetAndroidPauseTouchBounds(HudLayout layout)
        {
            return layout.StageBounds;
        }

        private static int MeasureHudWidth(string text, float scale, int padding, int minimum)
        {
            return System.Math.Max(minimum, (int)System.MathF.Ceiling(BitmapFontRenderer.Measure(text, scale).X) + padding);
        }

        private static int Scale(int value, float scale)
        {
            return System.Math.Max(1, (int)System.MathF.Round(value * scale));
        }
    }
}
