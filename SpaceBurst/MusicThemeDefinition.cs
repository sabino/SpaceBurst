namespace SpaceBurst
{
    sealed class MusicThemeDefinition
    {
        public string Id { get; set; } = string.Empty;
        public int Tempo { get; set; } = 124;
        public int RootMidiNote { get; set; } = 48;
        public int[] ScaleOffsets { get; set; } = new[] { 0, 3, 5, 7, 10 };
        public float Brightness { get; set; } = 0.5f;
        public float PulseDrive { get; set; } = 0.5f;
        public float PadSpread { get; set; } = 0.55f;
    }
}
