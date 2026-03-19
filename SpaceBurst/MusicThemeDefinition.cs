namespace SpaceBurst
{
    sealed class MusicThemeDefinition
    {
        public string Id { get; set; } = string.Empty;
        public int Bars { get; set; } = 4;
        public int ThemeSeed { get; set; } = 1;
        public int Tempo { get; set; } = 124;
        public int RootMidiNote { get; set; } = 48;
        public int[] ScaleOffsets { get; set; } = new[] { 0, 3, 5, 7, 10 };
        public int[] ChordDegrees { get; set; } = new[] { 0, 3, 4, 2 };
        public int[] BassPattern { get; set; } = new[] { 0, -99, 0, 1, 0, -99, 2, 1 };
        public int[] PulsePattern { get; set; } = new[] { 0, 2, 4, 2, 0, 2, 5, 4 };
        public int[] LeadPatternA { get; set; } = new[] { -99, 0, 2, 4, -99, 2, 1, 0 };
        public int[] LeadPatternB { get; set; } = new[] { 4, -99, 2, 1, 0, -99, 2, 3 };
        public int[] BossPattern { get; set; } = new[] { 0, 0, 2, 0, 3, 0, 2, 0 };
        public int[] PadChordSteps { get; set; } = new[] { 0, 2, 4 };
        public float Brightness { get; set; } = 0.5f;
        public float PulseDrive { get; set; } = 0.5f;
        public float PadSpread { get; set; } = 0.55f;
        public float Swing { get; set; } = 0.08f;
        public float RhythmDensity { get; set; } = 0.5f;
        public float Syncopation { get; set; } = 0.4f;
        public float LeadDensity { get; set; } = 0.5f;
        public float DangerWeight { get; set; } = 0.4f;
        public float BossWeight { get; set; } = 0.5f;
        public float VariantIntensity { get; set; } = 0.5f;
        public int BassOctave { get; set; } = -2;
        public int LeadOctave { get; set; } = 1;
    }
}
