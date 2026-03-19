using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using System;

namespace SpaceBurst
{
    static class ProceduralAudioSynth
    {
        public static SoundEffect CreateEffect(int sampleRate, float durationSeconds, SynthPatchDefinition patch)
        {
            byte[] pcm = RenderMonoPcm(sampleRate, durationSeconds, t => RenderPatchSample(t, durationSeconds, patch));
            return new SoundEffect(pcm, sampleRate, AudioChannels.Mono);
        }

        public static SoundEffect CreateMusicStem(MusicThemeDefinition theme, int sampleRate, float durationSeconds, MusicStemKind kind)
        {
            byte[] pcm = RenderMonoPcm(sampleRate, durationSeconds, t => RenderStemSample(theme, kind, t, durationSeconds));
            return new SoundEffect(pcm, sampleRate, AudioChannels.Mono);
        }

        public static SynthPatchDefinition PulseShotPatch(float colorShift = 0f)
        {
            return new SynthPatchDefinition { Name = "Pulse", PrimaryWaveform = SynthWaveform.Pulse, SecondaryWaveform = SynthWaveform.Triangle, AttackSeconds = 0.001f, DecaySeconds = 0.06f, SustainLevel = 0.14f, ReleaseSeconds = 0.05f, Detune = 0.012f + colorShift * 0.02f, NoiseMix = 0.05f, SweepAmount = 0.22f, Drive = 1.16f };
        }

        public static SynthPatchDefinition SpreadShotPatch(float colorShift = 0f)
        {
            return new SynthPatchDefinition { Name = "Spread", PrimaryWaveform = SynthWaveform.Square, SecondaryWaveform = SynthWaveform.Noise, AttackSeconds = 0.001f, DecaySeconds = 0.08f, SustainLevel = 0.12f, ReleaseSeconds = 0.06f, Detune = 0.018f + colorShift * 0.02f, NoiseMix = 0.18f, SweepAmount = 0.15f, Drive = 1.22f };
        }

        public static SynthPatchDefinition LaserShotPatch(float colorShift = 0f)
        {
            return new SynthPatchDefinition { Name = "Laser", PrimaryWaveform = SynthWaveform.Saw, SecondaryWaveform = SynthWaveform.Sine, AttackSeconds = 0.004f, DecaySeconds = 0.12f, SustainLevel = 0.28f, ReleaseSeconds = 0.08f, Detune = 0.008f, NoiseMix = 0.04f, VibratoFrequency = 10f, VibratoDepth = 0.012f + colorShift * 0.01f, SweepAmount = 0.05f, Drive = 1.08f };
        }

        public static SynthPatchDefinition PlasmaShotPatch(float colorShift = 0f)
        {
            return new SynthPatchDefinition { Name = "Plasma", PrimaryWaveform = SynthWaveform.Sine, SecondaryWaveform = SynthWaveform.Triangle, AttackSeconds = 0.006f, DecaySeconds = 0.14f, SustainLevel = 0.34f, ReleaseSeconds = 0.1f, Detune = 0.006f, NoiseMix = 0.03f, SweepAmount = -0.16f - colorShift * 0.04f, Drive = 1.04f };
        }

        public static SynthPatchDefinition MissileShotPatch(float colorShift = 0f)
        {
            return new SynthPatchDefinition { Name = "Missile", PrimaryWaveform = SynthWaveform.Saw, SecondaryWaveform = SynthWaveform.Noise, AttackSeconds = 0.002f, DecaySeconds = 0.18f, SustainLevel = 0.22f, ReleaseSeconds = 0.12f, Detune = 0.01f, NoiseMix = 0.22f, SweepAmount = -0.24f - colorShift * 0.05f, Drive = 1.15f };
        }

        public static SynthPatchDefinition RailShotPatch(float colorShift = 0f)
        {
            return new SynthPatchDefinition { Name = "Rail", PrimaryWaveform = SynthWaveform.Pulse, SecondaryWaveform = SynthWaveform.Saw, AttackSeconds = 0.001f, DecaySeconds = 0.07f, SustainLevel = 0.1f, ReleaseSeconds = 0.05f, Detune = 0.004f, NoiseMix = 0.03f, SweepAmount = 0.35f + colorShift * 0.05f, Drive = 1.24f };
        }

        public static SynthPatchDefinition ArcShotPatch(float colorShift = 0f)
        {
            return new SynthPatchDefinition { Name = "Arc", PrimaryWaveform = SynthWaveform.Square, SecondaryWaveform = SynthWaveform.Noise, AttackSeconds = 0.001f, DecaySeconds = 0.09f, SustainLevel = 0.16f, ReleaseSeconds = 0.08f, Detune = 0.022f, NoiseMix = 0.3f, VibratoFrequency = 24f, VibratoDepth = 0.024f + colorShift * 0.01f, SweepAmount = 0.12f, Drive = 1.18f };
        }

        public static SynthPatchDefinition BladeShotPatch(float colorShift = 0f)
        {
            return new SynthPatchDefinition { Name = "Blade", PrimaryWaveform = SynthWaveform.Triangle, SecondaryWaveform = SynthWaveform.Square, AttackSeconds = 0.001f, DecaySeconds = 0.08f, SustainLevel = 0.18f, ReleaseSeconds = 0.05f, Detune = 0.018f, NoiseMix = 0.05f, SweepAmount = 0.18f + colorShift * 0.03f, Drive = 1.16f };
        }

        public static SynthPatchDefinition DroneShotPatch(float colorShift = 0f)
        {
            return new SynthPatchDefinition { Name = "Drone", PrimaryWaveform = SynthWaveform.Sine, SecondaryWaveform = SynthWaveform.Pulse, AttackSeconds = 0.001f, DecaySeconds = 0.07f, SustainLevel = 0.16f, ReleaseSeconds = 0.06f, Detune = 0.012f, NoiseMix = 0.04f, SweepAmount = 0.1f + colorShift * 0.04f, Drive = 1.08f };
        }

        public static SynthPatchDefinition FortressShotPatch(float colorShift = 0f)
        {
            return new SynthPatchDefinition { Name = "Fortress", PrimaryWaveform = SynthWaveform.Square, SecondaryWaveform = SynthWaveform.Triangle, AttackSeconds = 0.002f, DecaySeconds = 0.14f, SustainLevel = 0.24f, ReleaseSeconds = 0.08f, Detune = 0.009f, NoiseMix = 0.08f, SweepAmount = -0.08f - colorShift * 0.04f, Drive = 1.18f };
        }

        public static SynthPatchDefinition EnemyShotPatch(float colorShift = 0f)
        {
            return new SynthPatchDefinition { Name = "EnemyShot", PrimaryWaveform = SynthWaveform.Saw, SecondaryWaveform = SynthWaveform.Noise, AttackSeconds = 0.001f, DecaySeconds = 0.08f, SustainLevel = 0.14f, ReleaseSeconds = 0.05f, Detune = 0.015f, NoiseMix = 0.12f + colorShift, SweepAmount = -0.14f, Drive = 1.1f };
        }

        public static SynthPatchDefinition ImpactPatch(float colorShift = 0f)
        {
            return new SynthPatchDefinition { Name = "Impact", PrimaryWaveform = SynthWaveform.Noise, SecondaryWaveform = SynthWaveform.Triangle, AttackSeconds = 0.001f, DecaySeconds = 0.06f, SustainLevel = 0.08f, ReleaseSeconds = 0.03f, NoiseMix = 0.4f, SweepAmount = -0.1f - colorShift * 0.06f, Drive = 1.14f };
        }

        public static SynthPatchDefinition ExplosionPatch(float colorShift = 1f)
        {
            return new SynthPatchDefinition { Name = "Explosion", PrimaryWaveform = SynthWaveform.Noise, SecondaryWaveform = SynthWaveform.Saw, AttackSeconds = 0.001f, DecaySeconds = 0.2f, SustainLevel = 0.24f, ReleaseSeconds = 0.18f, NoiseMix = 0.55f, SweepAmount = -0.36f * colorShift, Drive = 1.24f };
        }

        public static SynthPatchDefinition PickupPatch(bool bright)
        {
            return new SynthPatchDefinition { Name = bright ? "PickupBright" : "Pickup", PrimaryWaveform = SynthWaveform.Sine, SecondaryWaveform = SynthWaveform.Pulse, AttackSeconds = 0.002f, DecaySeconds = 0.12f, SustainLevel = 0.22f, ReleaseSeconds = 0.08f, Detune = 0.015f, NoiseMix = 0.01f, SweepAmount = bright ? 0.14f : 0.08f, Drive = 1.02f };
        }

        public static SynthPatchDefinition UpgradePatch(float colorShift = 0f)
        {
            return new SynthPatchDefinition { Name = "Upgrade", PrimaryWaveform = SynthWaveform.Pulse, SecondaryWaveform = SynthWaveform.Sine, AttackSeconds = 0.003f, DecaySeconds = 0.22f, SustainLevel = 0.22f, ReleaseSeconds = 0.16f, Detune = 0.02f + colorShift, NoiseMix = 0.02f, SweepAmount = 0.22f, Drive = 1.1f };
        }

        public static SynthPatchDefinition BossCuePatch()
        {
            return new SynthPatchDefinition { Name = "BossCue", PrimaryWaveform = SynthWaveform.Saw, SecondaryWaveform = SynthWaveform.Square, AttackSeconds = 0.01f, DecaySeconds = 0.28f, SustainLevel = 0.24f, ReleaseSeconds = 0.18f, Detune = 0.015f, NoiseMix = 0.06f, SweepAmount = -0.2f, Drive = 1.24f };
        }

        public static SynthPatchDefinition TransitionPatch()
        {
            return new SynthPatchDefinition { Name = "Transition", PrimaryWaveform = SynthWaveform.Noise, SecondaryWaveform = SynthWaveform.Sine, AttackSeconds = 0.006f, DecaySeconds = 0.2f, SustainLevel = 0.22f, ReleaseSeconds = 0.18f, Detune = 0.006f, NoiseMix = 0.36f, SweepAmount = -0.32f, Drive = 1.08f };
        }

        public static SynthPatchDefinition PlayerDamagePatch()
        {
            return new SynthPatchDefinition { Name = "PlayerDamage", PrimaryWaveform = SynthWaveform.Noise, SecondaryWaveform = SynthWaveform.Saw, AttackSeconds = 0.002f, DecaySeconds = 0.15f, SustainLevel = 0.16f, ReleaseSeconds = 0.08f, NoiseMix = 0.28f, SweepAmount = -0.2f, Drive = 1.18f };
        }

        public static SynthPatchDefinition RewindStartPatch()
        {
            return new SynthPatchDefinition { Name = "RewindStart", PrimaryWaveform = SynthWaveform.Sine, SecondaryWaveform = SynthWaveform.Pulse, AttackSeconds = 0.01f, DecaySeconds = 0.12f, SustainLevel = 0.18f, ReleaseSeconds = 0.09f, Detune = 0.01f, VibratoFrequency = 6f, VibratoDepth = 0.02f, SweepAmount = 0.18f, Drive = 1.06f };
        }

        public static SynthPatchDefinition RewindLoopPatch()
        {
            return new SynthPatchDefinition { Name = "RewindLoop", PrimaryWaveform = SynthWaveform.Sine, SecondaryWaveform = SynthWaveform.Triangle, AttackSeconds = 0.02f, DecaySeconds = 0.18f, SustainLevel = 0.4f, ReleaseSeconds = 0.24f, Detune = 0.004f, VibratoFrequency = 3.5f, VibratoDepth = 0.03f, SweepAmount = -0.06f, Drive = 1f };
        }

        public static SynthPatchDefinition UiPatch(bool confirm)
        {
            return new SynthPatchDefinition { Name = confirm ? "UiConfirm" : "UiCancel", PrimaryWaveform = SynthWaveform.Pulse, SecondaryWaveform = SynthWaveform.Sine, AttackSeconds = 0.001f, DecaySeconds = 0.08f, SustainLevel = 0.14f, ReleaseSeconds = 0.04f, Detune = confirm ? 0.01f : 0.005f, NoiseMix = 0.01f, SweepAmount = confirm ? 0.12f : -0.08f, Drive = 1.02f };
        }

        private static byte[] RenderMonoPcm(int sampleRate, float durationSeconds, Func<float, float> sampleFunc)
        {
            int sampleCount = Math.Max(1, (int)(sampleRate * durationSeconds));
            byte[] buffer = new byte[sampleCount * 2];
            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float sample = Math.Clamp(sampleFunc(time), -1f, 1f);
                short pcm = (short)(sample * short.MaxValue);
                int index = i * 2;
                buffer[index] = (byte)(pcm & 0xFF);
                buffer[index + 1] = (byte)((pcm >> 8) & 0xFF);
            }

            return buffer;
        }

        private static float RenderPatchSample(float time, float durationSeconds, SynthPatchDefinition patch)
        {
            float frequency = GetPatchFrequency(patch.Name, time);
            float vibrato = patch.VibratoDepth <= 0f ? 0f : MathF.Sin(time * patch.VibratoFrequency * MathF.Tau) * patch.VibratoDepth;
            float sweep = 1f + patch.SweepAmount * (1f - MathHelper.Clamp(time / MathF.Max(durationSeconds, 0.001f), 0f, 1f));
            float frequencyA = frequency * (1f + vibrato) * sweep;
            float frequencyB = frequency * (1f - patch.Detune + vibrato * 0.5f);
            float envelope = GetEnvelope(time, durationSeconds, patch);
            float waveformA = SampleWaveform(patch.PrimaryWaveform, frequencyA, time, patch.PulseWidth);
            float waveformB = SampleWaveform(patch.SecondaryWaveform, frequencyB, time, 0.5f);
            float noise = patch.NoiseMix <= 0f ? 0f : (HashNoise(time * 4800f) * 2f - 1f) * patch.NoiseMix;
            return ApplyDrive((waveformA * 0.68f + waveformB * 0.28f + noise) * envelope, patch.Drive) * 0.7f;
        }

        private static float RenderStemSample(MusicThemeDefinition theme, MusicStemKind kind, float time, float durationSeconds)
        {
            float beatLength = 60f / theme.Tempo;
            float loopSeconds = 8f * beatLength;
            float wrapped = loopSeconds <= 0f ? time : time % loopSeconds;
            float beat = wrapped / beatLength;
            int stepIndex = ((int)MathF.Floor(beat * 2f)) % 16;
            float stepPhase = beat * 2f - MathF.Floor(beat * 2f);
            float subBeat = beat - MathF.Floor(beat);

            return kind switch
            {
                MusicStemKind.Drums => RenderDrums(stepIndex, stepPhase, subBeat),
                MusicStemKind.Bass => RenderBass(theme, beat, stepIndex, stepPhase),
                MusicStemKind.Pad => RenderPad(theme, time),
                MusicStemKind.Pulse => RenderPulse(theme, stepIndex, stepPhase, time),
                MusicStemKind.Lead => RenderLead(theme, beat, stepIndex, stepPhase),
                MusicStemKind.Danger => RenderDanger(theme, stepIndex, stepPhase, time),
                _ => RenderBoss(theme, stepIndex, stepPhase, time),
            };
        }

        private static float RenderDrums(int stepIndex, float stepPhase, float subBeat)
        {
            float kick = (stepIndex == 0 || stepIndex == 8 || stepIndex == 12) ? DrumKick(stepPhase) : 0f;
            float snare = (stepIndex == 4 || stepIndex == 12) ? DrumSnare(stepPhase) : 0f;
            float hat = (stepIndex % 2 == 1) ? DrumHat(subBeat) : 0f;
            return Math.Clamp(kick + snare + hat, -1f, 1f) * 0.75f;
        }

        private static float RenderBass(MusicThemeDefinition theme, float beat, int stepIndex, float stepPhase)
        {
            int note = theme.RootMidiNote + theme.ScaleOffsets[(stepIndex / 4) % theme.ScaleOffsets.Length] - 12;
            float frequency = MidiToFrequency(note);
            float envelope = 1f - MathHelper.Clamp(stepPhase * 1.1f, 0f, 1f);
            float body = SampleWaveform(SynthWaveform.Square, frequency, beat * (60f / theme.Tempo), 0.42f) * 0.65f;
            float sub = SampleWaveform(SynthWaveform.Sine, frequency * 0.5f, beat * (60f / theme.Tempo), 0.5f) * 0.35f;
            return ApplyDrive((body + sub) * envelope, 1.08f) * 0.42f;
        }

        private static float RenderPad(MusicThemeDefinition theme, float time)
        {
            int root = theme.RootMidiNote + theme.ScaleOffsets[0];
            float n1 = MidiToFrequency(root);
            float n2 = MidiToFrequency(root + theme.ScaleOffsets[Math.Min(2, theme.ScaleOffsets.Length - 1)]);
            float n3 = MidiToFrequency(root + theme.ScaleOffsets[Math.Min(4, theme.ScaleOffsets.Length - 1)]);
            float lfo = 0.9f + MathF.Sin(time * 0.6f) * 0.1f;
            float sample = SampleWaveform(SynthWaveform.Triangle, n1, time, 0.5f) * 0.38f;
            sample += SampleWaveform(SynthWaveform.Sine, n2, time, 0.5f) * 0.31f;
            sample += SampleWaveform(SynthWaveform.Sine, n3 * (1f + theme.PadSpread * 0.01f), time, 0.5f) * 0.23f;
            return sample * (0.18f + 0.06f * lfo);
        }

        private static float RenderPulse(MusicThemeDefinition theme, int stepIndex, float stepPhase, float time)
        {
            int note = theme.RootMidiNote + theme.ScaleOffsets[(stepIndex + 1) % theme.ScaleOffsets.Length] + 12;
            float frequency = MidiToFrequency(note);
            float gate = stepPhase < 0.32f ? 1f - stepPhase / 0.32f : 0f;
            return ApplyDrive(SampleWaveform(SynthWaveform.Pulse, frequency, time, 0.24f) * gate, 1f + theme.PulseDrive) * 0.2f;
        }

        private static float RenderLead(MusicThemeDefinition theme, float beat, int stepIndex, float stepPhase)
        {
            int[] pattern = { 0, 2, 4, 2, 1, 2, 3, 2 };
            int note = theme.RootMidiNote + 12 + theme.ScaleOffsets[pattern[(stepIndex / 2) % pattern.Length] % theme.ScaleOffsets.Length];
            float frequency = MidiToFrequency(note);
            float envelope = stepPhase < 0.8f ? 1f - stepPhase * 0.7f : 0.1f;
            float time = beat * (60f / theme.Tempo);
            float sample = SampleWaveform(SynthWaveform.Saw, frequency, time, 0.5f);
            sample += SampleWaveform(SynthWaveform.Sine, frequency * 2f, time, 0.5f) * 0.22f;
            return ApplyDrive(sample * envelope, 1.1f) * 0.18f;
        }

        private static float RenderDanger(MusicThemeDefinition theme, int stepIndex, float stepPhase, float time)
        {
            float gate = stepIndex % 4 == 0 ? 1f - MathHelper.Clamp(stepPhase * 1.2f, 0f, 1f) : 0f;
            float sample = SampleWaveform(SynthWaveform.Noise, 40f, time, 0.5f) * 0.4f;
            sample += SampleWaveform(SynthWaveform.Saw, MidiToFrequency(theme.RootMidiNote - 5), time, 0.5f) * 0.16f;
            return sample * gate * 0.24f;
        }

        private static float RenderBoss(MusicThemeDefinition theme, int stepIndex, float stepPhase, float time)
        {
            int note = theme.RootMidiNote - 12 + theme.ScaleOffsets[(stepIndex / 4) % theme.ScaleOffsets.Length];
            float frequency = MidiToFrequency(note);
            float gate = stepPhase < 0.75f ? 1f - stepPhase * 0.55f : 0.12f;
            float sample = SampleWaveform(SynthWaveform.Saw, frequency, time, 0.5f) * 0.52f;
            sample += SampleWaveform(SynthWaveform.Pulse, frequency * 2f, time, 0.28f) * 0.22f;
            return ApplyDrive(sample * gate, 1.24f) * 0.2f;
        }

        private static float DrumKick(float phase)
        {
            float pitch = MathHelper.Lerp(90f, 34f, MathHelper.Clamp(phase * 1.8f, 0f, 1f));
            return MathF.Sin(phase * pitch) * MathF.Exp(-phase * 10f) * 0.9f;
        }

        private static float DrumSnare(float phase)
        {
            float envelope = MathF.Exp(-phase * 14f);
            float tone = MathF.Sin(phase * 210f) * 0.18f;
            float noise = (HashNoise(phase * 12000f) * 2f - 1f) * 0.82f;
            return (tone + noise) * envelope * 0.44f;
        }

        private static float DrumHat(float phase)
        {
            return (HashNoise(phase * 18000f) * 2f - 1f) * MathF.Exp(-phase * 26f) * 0.08f;
        }

        private static float GetPatchFrequency(string patchName, float time)
        {
            return patchName switch
            {
                "Pulse" => 760f - time * 180f,
                "Spread" => 420f - time * 120f,
                "Laser" => 520f,
                "Plasma" => 240f - time * 40f,
                "Missile" => 170f - time * 35f,
                "Rail" => 890f,
                "Arc" => 420f + MathF.Sin(time * 14f) * 90f,
                "Blade" => 660f - time * 140f,
                "Drone" => 580f - time * 60f,
                "Fortress" => 220f - time * 20f,
                "EnemyShot" => 260f - time * 55f,
                "Impact" => 140f,
                "Explosion" => 80f,
                "Pickup" => 540f + time * 160f,
                "PickupBright" => 620f + time * 220f,
                "Upgrade" => 480f + time * 320f,
                "BossCue" => 140f - time * 35f,
                "Transition" => 240f - time * 160f,
                "PlayerDamage" => 180f - time * 60f,
                "RewindStart" => 320f + time * 140f,
                "RewindLoop" => 140f + MathF.Sin(time * 3.4f) * 18f,
                "UiConfirm" => 640f,
                "UiCancel" => 460f,
                _ => 420f,
            };
        }

        private static float GetEnvelope(float time, float durationSeconds, SynthPatchDefinition patch)
        {
            float releaseStart = Math.Max(0f, durationSeconds - patch.ReleaseSeconds);
            if (time < patch.AttackSeconds)
                return patch.AttackSeconds <= 0f ? 1f : time / patch.AttackSeconds;
            if (time < patch.AttackSeconds + patch.DecaySeconds)
                return MathHelper.Lerp(1f, patch.SustainLevel, (time - patch.AttackSeconds) / Math.Max(0.001f, patch.DecaySeconds));
            if (time < releaseStart)
                return patch.SustainLevel;
            return patch.SustainLevel * (1f - MathHelper.Clamp((time - releaseStart) / Math.Max(0.001f, patch.ReleaseSeconds), 0f, 1f));
        }

        private static float SampleWaveform(SynthWaveform waveform, float frequency, float time, float pulseWidth)
        {
            if (waveform == SynthWaveform.Noise)
                return HashNoise(time * 4400f) * 2f - 1f;

            float wrapped = time * frequency - MathF.Floor(time * frequency);
            return waveform switch
            {
                SynthWaveform.Sine => MathF.Sin(MathF.Tau * wrapped),
                SynthWaveform.Triangle => 1f - 4f * MathF.Abs(wrapped - 0.5f),
                SynthWaveform.Square => wrapped < 0.5f ? 1f : -1f,
                SynthWaveform.Pulse => wrapped < MathHelper.Clamp(pulseWidth, 0.05f, 0.95f) ? 1f : -1f,
                SynthWaveform.Saw => wrapped * 2f - 1f,
                _ => 0f,
            };
        }

        private static float ApplyDrive(float sample, float drive)
        {
            return MathF.Tanh(sample * Math.Max(0.1f, drive));
        }

        private static float MidiToFrequency(int midiNote)
        {
            return 440f * MathF.Pow(2f, (midiNote - 69) / 12f);
        }

        private static float HashNoise(float value)
        {
            float sine = MathF.Sin(value * 12.9898f) * 43758.5453f;
            return sine - MathF.Floor(sine);
        }
    }
}
