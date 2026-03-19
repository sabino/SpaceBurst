using System;

namespace SpaceBurst.RuntimeData
{
    /// <summary>
    /// Computes presentation defaults from campaign progression while keeping authored data optional.
    /// </summary>
    public static class PresentationProgression
    {
        public static PresentationTier GetTierForStage(int stageNumber, PresentationTier? authoredTier = null)
        {
            if (authoredTier.HasValue)
                return authoredTier.Value;

            if (stageNumber >= 40)
                return PresentationTier.Late3D;
            if (stageNumber >= 30)
                return PresentationTier.HybridMesh;
            if (stageNumber >= 20)
                return PresentationTier.VoxelShell;
            if (stageNumber >= 10)
                return PresentationTier.VoxelShell;

            return PresentationTier.Pixel2D;
        }

        public static bool IsVoxelAccentStage(int stageNumber)
        {
            return stageNumber >= 10 && stageNumber <= 19;
        }

        public static bool IsChaseViewUnlocked(int stageNumber, StageDefinition stage = null)
        {
            if (stage != null && !stage.EnableChaseView)
                return false;

            return stageNumber >= 40;
        }

        public static float GetBossPresentationScale(int stageNumber, BossDefinition boss = null)
        {
            if (boss != null && boss.PresentationScale > 0f && Math.Abs(boss.PresentationScale - 1f) > 0.001f)
                return boss.PresentationScale;

            int chapter = Math.Max(1, (int)Math.Ceiling(stageNumber / 10f));
            return chapter switch
            {
                1 => 1.45f,
                2 => 1.82f,
                3 => 2.24f,
                4 => 2.92f,
                _ => 3.8f,
            };
        }

        public static float GetBossCoverageTarget(int stageNumber, BossDefinition boss = null)
        {
            if (boss != null && boss.ScreenCoverageTarget > 0f)
                return boss.ScreenCoverageTarget;

            int chapter = Math.Max(1, (int)Math.Ceiling(stageNumber / 10f));
            return chapter switch
            {
                1 => 0.2f,
                2 => 0.28f,
                3 => 0.35f,
                4 => 0.44f,
                _ => 0.52f,
            };
        }
    }
}
