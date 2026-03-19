namespace SpaceBurst
{
    static class UiScaleHelper
    {
        public static int ClampUiScalePercent(int percent)
        {
            return percent < 70 ? 70 : percent > 220 ? 220 : percent;
        }

        public static int ClampTouchControlsOpacity(int percent)
        {
            return percent < 20 ? 20 : percent > 100 ? 100 : percent;
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
    }
}
