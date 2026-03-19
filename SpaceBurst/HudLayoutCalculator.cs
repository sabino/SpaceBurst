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
            const int margin = 12;
            const int gap = 10;
            const int top = 10;
            const int height = 88;

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
            int livesWidth = MeasureHudWidth(string.Concat("LIVES ", PlayerStatus.Lives.ToString()), 2f, 40, 200);
            int activeWidth = MeasureHudWidth(activeStyle.DisplayName, 1.8f, 110, 220);
            int stageWidth = MeasureHudWidth(stageLabel, 1.7f, 54, 170);
            int pityWidth = 190;
            int scoreWidth = MeasureHudWidth(scoreLabel, 1.65f, 186, 272);
            int ownedWidth = totalWidth - livesWidth - activeWidth - stageWidth - pityWidth - scoreWidth;

            if (ownedWidth < 180)
            {
                int deficit = 180 - ownedWidth;
                int reduceScore = System.Math.Min(deficit, System.Math.Max(0, scoreWidth - 240));
                scoreWidth -= reduceScore;
                deficit -= reduceScore;

                int reduceActive = System.Math.Min(deficit, System.Math.Max(0, activeWidth - 200));
                activeWidth -= reduceActive;
                deficit -= reduceActive;

                int reduceStage = System.Math.Min(deficit, System.Math.Max(0, stageWidth - 150));
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
            return new Rectangle(stageBounds.Right - 34, stageBounds.Y + 10, 20, 18);
        }

        private static int MeasureHudWidth(string text, float scale, int padding, int minimum)
        {
            return System.Math.Max(minimum, (int)System.MathF.Ceiling(BitmapFontRenderer.Measure(text, scale).X) + padding);
        }
    }
}
