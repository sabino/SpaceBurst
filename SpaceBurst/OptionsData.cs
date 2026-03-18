namespace SpaceBurst
{
    sealed class OptionsData
    {
        public bool ShowHelpHints { get; set; } = true;
        public bool TutorialCompleted { get; set; }
        public bool AutoUpgradeDraft { get; set; }
        public DesktopDisplayMode DisplayMode { get; set; } = DesktopDisplayMode.BorderlessFullscreen;
        public VisualPreset VisualPreset { get; set; } = VisualPreset.Standard;
        public bool EnableBloom { get; set; } = true;
        public bool EnableShockwaves { get; set; } = true;
        public bool EnableNeonOutlines { get; set; } = true;
    }
}
