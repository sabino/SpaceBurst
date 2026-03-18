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
        public string Description { get; set; } = string.Empty;
        public string AccentColor { get; set; } = "#FFB347";
    }
}
