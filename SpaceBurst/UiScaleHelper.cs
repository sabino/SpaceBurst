namespace SpaceBurst
{
    static class UiScaleHelper
    {
        public static int ClampUiScalePercent(int percent)
        {
            return percent < 70 ? 70 : percent > 220 ? 220 : percent;
        }

        public static int ClampWorldScalePercent(int percent)
        {
#if ANDROID
            return percent < 70 ? 70 : percent > 140 ? 140 : percent;
#else
            return percent < 70 ? 70 : percent > 160 ? 160 : percent;
#endif
        }

        public static float GetUiLayoutMultiplier(int percent)
        {
#if ANDROID
            const float platformBase = 1.18f;
#else
            const float platformBase = 1f;
#endif
            return platformBase * ClampUiScalePercent(percent) / 100f;
        }

        public static float GetUiTextMultiplier(int percent)
        {
#if ANDROID
            const float platformBase = 1.3f;
#else
            const float platformBase = 1f;
#endif
            return platformBase * ClampUiScalePercent(percent) / 100f;
        }

        public static float GetWorldScale(int percent)
        {
            return ClampWorldScalePercent(percent) / 100f;
        }
    }
}
