namespace SpaceBurst
{
    enum SynthWaveform
    {
        Sine,
        Triangle,
        Square,
        Saw,
        Pulse,
        Noise,
    }

    sealed class SynthPatchDefinition
    {
        public string Name { get; set; } = string.Empty;
        public SynthWaveform PrimaryWaveform { get; set; } = SynthWaveform.Sine;
        public SynthWaveform SecondaryWaveform { get; set; } = SynthWaveform.Triangle;
        public float AttackSeconds { get; set; } = 0.005f;
        public float DecaySeconds { get; set; } = 0.08f;
        public float SustainLevel { get; set; } = 0.38f;
        public float ReleaseSeconds { get; set; } = 0.16f;
        public float Detune { get; set; } = 0.01f;
        public float NoiseMix { get; set; } = 0.05f;
        public float VibratoFrequency { get; set; } = 0f;
        public float VibratoDepth { get; set; } = 0f;
        public float SweepAmount { get; set; } = 0f;
        public float PulseWidth { get; set; } = 0.5f;
        public float Drive { get; set; } = 1f;
    }
}
