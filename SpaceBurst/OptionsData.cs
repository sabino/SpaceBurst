namespace SpaceBurst
{
    sealed class OptionsData
    {
        public bool ShowHelpHints { get; set; } = true;
        public bool TutorialCompleted { get; set; }
        public bool AutoUpgradeDraft { get; set; }
        public DesktopDisplayMode DisplayMode { get; set; } = DesktopDisplayMode.BorderlessFullscreen;
        public float GameScale { get; set; } = 1f;
        public VisualPreset VisualPreset { get; set; } = VisualPreset.Standard;
        public bool EnableBloom { get; set; } = true;
        public bool EnableShockwaves { get; set; } = true;
        public bool EnableNeonOutlines { get; set; } = true;
        public float MasterVolume { get; set; } = 1f;
        public float MusicVolume { get; set; } = 0.8f;
        public float SfxVolume { get; set; } = 0.95f;
#if ANDROID
        public AudioQualityPreset AudioQualityPreset { get; set; } = AudioQualityPreset.Reduced;
        public ScreenShakeStrength ScreenShakeStrength { get; set; } = ScreenShakeStrength.Reduced;
#else
        public AudioQualityPreset AudioQualityPreset { get; set; } = AudioQualityPreset.Standard;
        public ScreenShakeStrength ScreenShakeStrength { get; set; } = ScreenShakeStrength.Full;
#endif
    }
}
