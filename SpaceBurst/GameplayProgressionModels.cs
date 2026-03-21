using SpaceBurst.RuntimeData;

namespace SpaceBurst
{
    enum UpgradeCardType
    {
        WeaponSurge,
        MobilityTuning,
        EmergencyReserve,
        RewindBattery,
        LuckyCore,
    }

    enum TutorialStep
    {
        Move,
        Aim,
        Fire,
        Rewind,
        CollectPower,
        UpgradeDraft,
        SwitchStyle,
        ShipsAndLives,
        Complete,
    }

    sealed class UpgradeDraftCard
    {
        public UpgradeCardType Type { get; set; }
        public WeaponStyleId StyleId { get; set; } = WeaponStyleId.Pulse;
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string PreviewText { get; set; } = string.Empty;
        public string DeltaText { get; set; } = string.Empty;
        public string HotkeyLabel { get; set; } = string.Empty;
        public string BadgeText { get; set; } = string.Empty;
        public string AccentColor { get; set; } = "#FFB347";
    }
}
