using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using System;

namespace SpaceBurst
{
    static class ProceduralAudioSynth
    {
        private static readonly float[] MidiFrequencies = BuildMidiFrequencies();
        private static readonly int[] UiConfirmNotes = { 76, 83 };
        private static readonly int[] UiCancelNotes = { 67, 60 };
        private static readonly int[] PickupNotes = { 72, 76, 79 };
        private static readonly int[] PickupBrightNotes = { 76, 81, 84 };
        private static readonly int[] UpgradeNotes = { 60, 64, 67, 72 };
        private static readonly int[] BossCueNotes = { 45, 45, 40 };
        private static readonly int[] PlayerDamageNotes = { 48, 43, 36 };
        private static readonly int[] RewindStartNotes = { 52, 59, 64 };

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

        internal static float RenderEffectPreviewSample(float time, float durationSeconds, SynthPatchDefinition patch)
        {
            return RenderPatchSample(time, durationSeconds, patch);
        }

        internal static float RenderMusicPreviewSample(MusicThemeDefinition theme, MusicStemKind kind, float time, float durationSeconds)
        {
            return RenderStemSample(theme, kind, time, durationSeconds);
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
#if BLAZORGL
            // Browser builds can run without the optional wasm-tools optimizer.
            // Evaluate the expensive oscillator graph at a quality-dependent
            // control rate, then interpolate into the requested PCM rate. This
            // keeps synthesis responsive while preserving smooth waveforms.
            int synthesisStride = sampleRate <= 11025 ? 8 : sampleRate <= 16000 ? 4 : 2;
            float currentSample = Math.Clamp(sampleFunc(0f), -1f, 1f);
            for (int blockStart = 0; blockStart < sampleCount; blockStart += synthesisStride)
            {
                int nextIndex = Math.Min(sampleCount - 1, blockStart + synthesisStride);
                float nextSample = Math.Clamp(sampleFunc(nextIndex / (float)sampleRate), -1f, 1f);
                int blockEnd = Math.Min(sampleCount, blockStart + synthesisStride);
                for (int i = blockStart; i < blockEnd; i++)
                {
                    float amount = (i - blockStart) / (float)synthesisStride;
                    float sample = MathHelper.Lerp(currentSample, nextSample, amount);
                    short pcm = (short)(sample * short.MaxValue);
                    int index = i * 2;
                    buffer[index] = (byte)(pcm & 0xFF);
                    buffer[index + 1] = (byte)((pcm >> 8) & 0xFF);
                }

                currentSample = nextSample;
            }
#else
            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float sample = Math.Clamp(sampleFunc(time), -1f, 1f);
                short pcm = (short)(sample * short.MaxValue);
                int index = i * 2;
                buffer[index] = (byte)(pcm & 0xFF);
                buffer[index + 1] = (byte)((pcm >> 8) & 0xFF);
            }
#endif

            return buffer;
        }

        private static float RenderPatchSample(float time, float durationSeconds, SynthPatchDefinition patch)
        {
            if (TryRenderAuthoredEffect(time, durationSeconds, patch, out float authoredSample))
                return authoredSample;

            float frequency = GetPatchFrequency(patch.Name, time);
            float vibrato = patch.VibratoDepth <= 0f ? 0f : OscillatorSin(time * patch.VibratoFrequency * MathF.Tau) * patch.VibratoDepth;
            float sweep = 1f + patch.SweepAmount * (1f - MathHelper.Clamp(time / MathF.Max(durationSeconds, 0.001f), 0f, 1f));
            float frequencyA = frequency * (1f + vibrato) * sweep;
            float frequencyB = frequency * (1f - patch.Detune + vibrato * 0.5f);
            float envelope = GetEnvelope(time, durationSeconds, patch);
            float waveformA = SampleWaveform(patch.PrimaryWaveform, frequencyA, time, patch.PulseWidth);
            float waveformB = SampleWaveform(patch.SecondaryWaveform, frequencyB, time, 0.5f);
            float noise = patch.NoiseMix <= 0f ? 0f : (HashNoise(time * 4800f) * 2f - 1f) * patch.NoiseMix;
            return ApplyDrive((waveformA * 0.68f + waveformB * 0.28f + noise) * envelope, patch.Drive) * 0.7f;
        }

        private static bool TryRenderAuthoredEffect(float time, float durationSeconds, SynthPatchDefinition patch, out float sample)
        {
            switch (patch.Name)
            {
                case "UiConfirm":
                    sample = RenderNoteSequence(time, durationSeconds, UiConfirmNotes, SynthWaveform.Pulse, 0.54f, 1f + patch.Detune * 0.2f);
                    return true;
                case "UiCancel":
                    sample = RenderNoteSequence(time, durationSeconds, UiCancelNotes, SynthWaveform.Square, 0.48f, 1f + patch.Detune * 0.2f);
                    return true;
                case "Pickup":
                    sample = RenderNoteSequence(time, durationSeconds, PickupNotes, SynthWaveform.Pulse, 0.54f, 1f + patch.Detune * 0.2f);
                    return true;
                case "PickupBright":
                    sample = RenderNoteSequence(time, durationSeconds, PickupBrightNotes, SynthWaveform.Pulse, 0.58f, 1f + patch.Detune * 0.2f);
                    return true;
                case "Upgrade":
                    sample = RenderNoteSequence(time, durationSeconds, UpgradeNotes, SynthWaveform.Pulse, 0.62f, 1f + patch.Detune * 0.2f);
                    return true;
                case "BossCue":
                    sample = RenderNoteSequence(time, durationSeconds, BossCueNotes, SynthWaveform.Square, 0.66f, 1f + patch.Detune * 0.2f);
                    return true;
                case "PlayerDamage":
                    sample = RenderNoteSequence(time, durationSeconds, PlayerDamageNotes, SynthWaveform.Saw, 0.62f, 1f + patch.Detune * 0.2f);
                    return true;
                case "RewindStart":
                    sample = RenderNoteSequence(time, durationSeconds, RewindStartNotes, SynthWaveform.Triangle, 0.5f, 1f + patch.Detune * 0.2f);
                    return true;
                case "Impact":
                    sample = RenderImpact(time, durationSeconds, patch);
                    return true;
                case "Explosion":
                    sample = RenderExplosion(time, durationSeconds, patch);
                    return true;
                case "Transition":
                    sample = RenderTransition(time, durationSeconds);
                    return true;
                case "Pulse":
                    sample = RenderWeaponCue(time, durationSeconds, 920f, 520f, SynthWaveform.Pulse, 0.2f, 0.025f, patch);
                    return true;
                case "Spread":
                    sample = RenderWeaponCue(time, durationSeconds, 390f, 210f, SynthWaveform.Square, 0.5f, 0.14f, patch);
                    return true;
                case "Laser":
                    sample = RenderWeaponCue(time, durationSeconds, 620f, 1040f, SynthWaveform.Saw, 0.5f, 0.015f, patch);
                    return true;
                case "Plasma":
                    sample = RenderWeaponCue(time, durationSeconds, 280f, 180f, SynthWaveform.Sine, 0.5f, 0.01f, patch);
                    return true;
                case "Missile":
                    sample = RenderWeaponCue(time, durationSeconds, 210f, 92f, SynthWaveform.Saw, 0.5f, 0.18f, patch);
                    return true;
                case "Rail":
                    sample = RenderWeaponCue(time, durationSeconds, 1320f, 360f, SynthWaveform.Pulse, 0.16f, 0.02f, patch);
                    return true;
                case "Arc":
                    sample = RenderWeaponCue(time, durationSeconds, 560f, 760f, SynthWaveform.Square, 0.5f, 0.12f, patch);
                    return true;
                case "Blade":
                    sample = RenderWeaponCue(time, durationSeconds, 820f, 430f, SynthWaveform.Triangle, 0.5f, 0.025f, patch);
                    return true;
                case "Drone":
                    sample = RenderWeaponCue(time, durationSeconds, 720f, 560f, SynthWaveform.Pulse, 0.28f, 0.015f, patch);
                    return true;
                case "Fortress":
                    sample = RenderWeaponCue(time, durationSeconds, 250f, 138f, SynthWaveform.Square, 0.5f, 0.055f, patch);
                    return true;
                case "EnemyShot":
                    sample = RenderWeaponCue(time, durationSeconds, 330f, 155f, SynthWaveform.Square, 0.5f, 0.09f, patch);
                    return true;
                default:
                    sample = 0f;
                    return false;
            }
        }

        private static float RenderNoteSequence(float time, float durationSeconds, int[] midiNotes, SynthWaveform waveform, float gain, float pitchScale)
        {
            float safeDuration = Math.Max(0.001f, durationSeconds);
            float noteDuration = safeDuration / Math.Max(1, midiNotes.Length);
            int noteIndex = Math.Clamp((int)(time / noteDuration), 0, midiNotes.Length - 1);
            float localTime = Math.Max(0f, time - noteIndex * noteDuration);
            float notePhase = MathHelper.Clamp(localTime / noteDuration, 0f, 1f);
            float attack = MathHelper.Clamp(localTime / 0.004f, 0f, 1f);
            float release = 1f - MathHelper.Clamp((notePhase - 0.68f) / 0.32f, 0f, 1f);
            float frequency = MidiToFrequency(midiNotes[noteIndex]) * pitchScale;
            float primary = SampleWaveform(waveform, frequency, localTime, 0.24f) * 0.72f;
            float octave = SampleWaveform(SynthWaveform.Sine, frequency * 2f, localTime, 0.5f) * 0.2f;
            float sub = SampleWaveform(SynthWaveform.Triangle, frequency * 0.5f, localTime, 0.5f) * 0.08f;
            return ApplyDrive((primary + octave + sub) * attack * release, 1.08f) * gain;
        }

        private static float RenderWeaponCue(float time, float durationSeconds, float startFrequency, float endFrequency, SynthWaveform waveform, float pulseWidth, float noiseMix, SynthPatchDefinition patch)
        {
            float safeDuration = Math.Max(0.001f, durationSeconds);
            float progress = MathHelper.Clamp(time / safeDuration, 0f, 1f);
            float detuneScale = 1f + patch.Detune * 1.8f;
            float cycles = (startFrequency * time + 0.5f * (endFrequency - startFrequency) * time * time / safeDuration) * detuneScale;
            float currentFrequency = MathHelper.Lerp(startFrequency, endFrequency, progress) * detuneScale;
            float phaseTime = cycles / Math.Max(1f, currentFrequency);
            float primary = SampleWaveform(waveform, currentFrequency, phaseTime, pulseWidth) * 0.76f;
            float harmonic = SampleWaveform(SynthWaveform.Sine, currentFrequency * 2f, phaseTime, 0.5f) * 0.18f;
            float noise = (HashNoise(time * 13200f + patch.Detune * 91f) * 2f - 1f) * noiseMix;
            float attack = MathHelper.Clamp(time / 0.0025f, 0f, 1f);
            float envelope = attack * MathF.Pow(Math.Max(0f, 1f - progress), 1.35f);
            return ApplyDrive((primary + harmonic + noise) * envelope, Math.Max(1f, patch.Drive)) * 0.64f;
        }

        private static float RenderImpact(float time, float durationSeconds, SynthPatchDefinition patch)
        {
            float progress = MathHelper.Clamp(time / Math.Max(0.001f, durationSeconds), 0f, 1f);
            float envelope = MathHelper.Clamp(time / 0.0015f, 0f, 1f) * MathF.Pow(1f - progress, 2.6f);
            float variation = MathF.Abs(patch.SweepAmount + 0.1f);
            float click = (HashNoise(time * 18000f + variation * 970f) * 2f - 1f) * 0.58f;
            float body = OscillatorSin(time * (165f + variation * 420f) * MathF.Tau) * 0.54f;
            return ApplyDrive((click + body) * envelope, 1.18f) * 0.68f;
        }

        private static float RenderExplosion(float time, float durationSeconds, SynthPatchDefinition patch)
        {
            float safeDuration = Math.Max(0.001f, durationSeconds);
            float progress = MathHelper.Clamp(time / safeDuration, 0f, 1f);
            float attack = MathHelper.Clamp(time / 0.004f, 0f, 1f);
            float envelope = attack * MathF.Pow(1f - progress, 1.7f);
            float color = MathHelper.Clamp(1f + (MathF.Abs(patch.SweepAmount) - 0.36f) * 1.2f, 0.88f, 1.12f);
            float cycles = 104f * color * time + 0.5f * (38f * color - 104f * color) * time * time / safeDuration;
            float thump = OscillatorSin(cycles * MathF.Tau) * 0.68f;
            float crack = (HashNoise(time * 11800f + color * 31f) * 2f - 1f) * 0.46f;
            float rumble = (HashNoise(time * 2100f + color * 17f) * 2f - 1f) * 0.28f;
            return ApplyDrive((thump + crack + rumble) * envelope, 1.3f) * 0.72f;
        }

        private static float RenderTransition(float time, float durationSeconds)
        {
            float safeDuration = Math.Max(0.001f, durationSeconds);
            float progress = MathHelper.Clamp(time / safeDuration, 0f, 1f);
            float envelope = MathF.Sin(progress * MathF.PI);
            float cycles = 160f * time + 0.5f * (620f - 160f) * time * time / safeDuration;
            float tone = OscillatorSin(cycles * MathF.Tau) * 0.34f;
            float air = (HashNoise(time * (3200f + progress * 9200f)) * 2f - 1f) * 0.44f;
            return ApplyDrive((tone + air) * envelope, 1.08f) * 0.56f;
        }

        private static float RenderStemSample(MusicThemeDefinition theme, MusicStemKind kind, float time, float durationSeconds)
        {
            float beatLength = 60f / Math.Max(40f, theme.Tempo);
            float loopSeconds = Math.Max(durationSeconds, theme.Bars * 4f * beatLength);
            float wrapped = loopSeconds <= 0f ? time : time % loopSeconds;
            float beat = wrapped / beatLength;
            int barIndex = Math.Min(theme.Bars - 1, (int)MathF.Floor(beat / 4f));
            float beatInBar = beat - barIndex * 4f;
            int barSixteenth = (int)MathF.Floor(beatInBar * 4f) % 16;
            int barEighth = (int)MathF.Floor(beatInBar * 2f) % 8;
            float sixteenthPhase = beatInBar * 4f - MathF.Floor(beatInBar * 4f);
            float eighthPhase = beatInBar * 2f - MathF.Floor(beatInBar * 2f);
            float swungBeat = ApplySwing(beatInBar, theme.Swing);
            int chordDegree = theme.ChordDegrees[barIndex % theme.ChordDegrees.Length];

            return kind switch
            {
                MusicStemKind.Drums => RenderDrums(theme, barIndex, barSixteenth, sixteenthPhase, time),
                MusicStemKind.Bass => RenderBass(theme, chordDegree, barIndex, barEighth, eighthPhase, time),
                MusicStemKind.Pad => RenderPad(theme, chordDegree, time, beatInBar),
                MusicStemKind.Pulse => RenderPulse(theme, chordDegree, barIndex, barSixteenth, sixteenthPhase, time),
                MusicStemKind.Lead => RenderLead(theme, chordDegree, barIndex, barEighth, eighthPhase, swungBeat, time),
                MusicStemKind.Danger => RenderDanger(theme, chordDegree, barIndex, barSixteenth, sixteenthPhase, time),
                MusicStemKind.Boss => RenderBoss(theme, chordDegree, barIndex, barEighth, eighthPhase, time),
                MusicStemKind.ReducedMix => RenderReducedMix(theme, chordDegree, barIndex, barSixteenth, barEighth, sixteenthPhase, eighthPhase, time, beatInBar),
                _ => 0f,
            };
        }

        private static float RenderReducedMix(
            MusicThemeDefinition theme,
            int chordDegree,
            int barIndex,
            int barSixteenth,
            int barEighth,
            float sixteenthPhase,
            float eighthPhase,
            float time,
            float beatInBar)
        {
            float drums = RenderDrums(theme, barIndex, barSixteenth, sixteenthPhase, time) * 0.78f;
            float bass = RenderBass(theme, chordDegree, barIndex, barEighth, eighthPhase, time) * 0.68f;
            float pad = RenderPad(theme, chordDegree, time, beatInBar) * 0.28f;
            float pulse = RenderPulse(theme, chordDegree, barIndex, barSixteenth, sixteenthPhase, time) * 0.5f;
            float boss = theme.Id.EndsWith("-boss", StringComparison.OrdinalIgnoreCase)
                ? RenderBoss(theme, chordDegree, barIndex, barEighth, eighthPhase, time) * 0.46f
                : 0f;
            return Math.Clamp((drums + bass + pad + pulse + boss) * 0.66f, -1f, 1f);
        }

        private static float RenderDrums(MusicThemeDefinition theme, int barIndex, int barSixteenth, float sixteenthPhase, float time)
        {
            bool kickHit = barSixteenth == 0 || barSixteenth == 8
                || (barSixteenth == 12 && theme.RhythmDensity >= 0.78f && (barIndex & 1) == 1);
            bool snareHit = barSixteenth == 4 || barSixteenth == 12;
            bool ghostHit = barSixteenth == 14 && theme.RhythmDensity >= 0.82f && (barIndex & 1) == 1;
            bool hatHit = (barSixteenth & 1) == 0 || (theme.RhythmDensity >= 0.72f && (barSixteenth & 3) == 3);
            float sixteenthSeconds = 60f / Math.Max(40f, theme.Tempo) * 0.25f;
            float localSeconds = sixteenthPhase * sixteenthSeconds;

            float kick = kickHit ? DrumKick(localSeconds) : 0f;
            float snare = snareHit ? DrumSnare(localSeconds) : 0f;
            float ghost = ghostHit ? DrumSnare(localSeconds) * 0.2f : 0f;
            float hat = hatHit ? DrumHat(localSeconds, theme.RhythmDensity) : 0f;
            return Math.Clamp(kick + snare + ghost + hat, -1f, 1f) * 0.86f;
        }

        private static float RenderBass(MusicThemeDefinition theme, int chordDegree, int barIndex, int barEighth, float eighthPhase, float time)
        {
            int patternValue = theme.BassPattern[(barIndex * 8 + barEighth) % theme.BassPattern.Length];
            if (patternValue <= -99)
                return 0f;

            int note = GetScaleNote(theme, chordDegree + patternValue, theme.BassOctave);
            float frequency = MidiToFrequency(note);
            float envelope = GetStepEnvelope(eighthPhase, 0.88f);
            float body = SampleWaveform(SynthWaveform.Square, frequency, time, 0.42f) * 0.58f;
            float sub = SampleWaveform(SynthWaveform.Sine, frequency * 0.5f, time, 0.5f) * 0.4f;
            float grit = SampleWaveform(SynthWaveform.Saw, frequency * 1.01f, time, 0.5f) * theme.Brightness * 0.16f;
            return ApplyDrive((body + sub + grit) * envelope, 1.04f + theme.VariantIntensity * 0.18f) * 0.48f;
        }

        private static float RenderPad(MusicThemeDefinition theme, int chordDegree, float time, float beatInBar)
        {
            float slowPulse = 0.88f + OscillatorSin(time * 0.42f + theme.ThemeSeed * 0.1f) * 0.12f;
            float sample = 0f;
            for (int i = 0; i < theme.PadChordSteps.Length; i++)
            {
                int note = GetScaleNote(theme, chordDegree + theme.PadChordSteps[i], 0);
                float frequency = MidiToFrequency(note) * (1f + theme.PadSpread * 0.004f * i);
                float voice = SampleWaveform(i == 0 ? SynthWaveform.Triangle : SynthWaveform.Sine, frequency, time, 0.5f) * (0.28f - i * 0.03f);
                voice += SampleWaveform(SynthWaveform.Sine, frequency * (1.002f + i * 0.0012f), time, 0.5f) * (0.12f - i * 0.015f);
                if (i == theme.PadChordSteps.Length - 1)
                    voice += SampleWaveform(SynthWaveform.Sine, frequency * 2f, time, 0.5f) * 0.06f;

                sample += voice;
            }

            float swell = 0.16f + 0.08f * slowPulse + 0.02f * OscillatorSin(beatInBar * MathF.Tau * 0.5f);
            return sample * swell;
        }

        private static float RenderPulse(MusicThemeDefinition theme, int chordDegree, int barIndex, int barSixteenth, float sixteenthPhase, float time)
        {
            int patternValue = theme.PulsePattern[(barIndex * 16 + barSixteenth) % theme.PulsePattern.Length];
            if (patternValue <= -99)
                return 0f;

            int note = GetScaleNote(theme, chordDegree + patternValue, 1);
            float frequency = MidiToFrequency(note);
            float gateWidth = 0.22f + theme.RhythmDensity * 0.18f;
            float gate = sixteenthPhase < gateWidth ? 1f - sixteenthPhase / gateWidth : 0f;
            float waveform = SampleWaveform(SynthWaveform.Pulse, frequency, time, 0.22f + theme.Brightness * 0.12f);
            waveform += SampleWaveform(SynthWaveform.Sine, frequency * 2f, time, 0.5f) * 0.12f;
            if ((barSixteenth & 3) == 2)
                waveform += SampleWaveform(SynthWaveform.Pulse, frequency * 2f, time, 0.18f) * 0.08f;

            return ApplyDrive(waveform * gate, 1f + theme.PulseDrive) * (0.16f + theme.Brightness * 0.04f);
        }

        private static float RenderLead(MusicThemeDefinition theme, int chordDegree, int barIndex, int barEighth, float eighthPhase, float swungBeat, float time)
        {
            int[] phrase = (barIndex % 2 == 0) ? theme.LeadPatternA : theme.LeadPatternB;
            int patternValue = phrase[(barIndex * 8 + barEighth) % phrase.Length];
            if (patternValue <= -99)
                return 0f;

            int phraseStep = barIndex * 8 + barEighth;
            if (theme.LeadDensity < 0.4f && phraseStep % 4 == 3)
                return 0f;

            int note = GetScaleNote(theme, chordDegree + patternValue, theme.LeadOctave);
            if (barIndex == theme.Bars - 1 && barEighth >= 6 && theme.VariantIntensity > 0.72f)
                note += 12;

            float frequency = MidiToFrequency(note);
            float envelope = eighthPhase < 0.86f ? 1f - eighthPhase * 0.62f : 0.1f;
            float vibrato = OscillatorSin((time + swungBeat * 0.02f) * (5.8f + theme.Brightness * 3.4f) * MathF.Tau) * (0.003f + theme.Brightness * 0.004f);
            float saw = SampleWaveform(SynthWaveform.Saw, frequency * (1f + vibrato), time, 0.5f);
            float shimmer = SampleWaveform(SynthWaveform.Sine, frequency * 2f, time, 0.5f) * (0.18f + theme.Brightness * 0.08f);
            float triangle = SampleWaveform(SynthWaveform.Triangle, frequency * 0.5f, time, 0.5f) * 0.12f;
            float echo = 0f;
            if (time > 0.085f)
                echo = SampleWaveform(SynthWaveform.Sine, frequency * 0.998f, time - 0.085f, 0.5f) * 0.1f;

            return ApplyDrive((saw + shimmer + triangle + echo) * envelope, 1.08f + theme.Brightness * 0.2f) * 0.22f;
        }

        private static float RenderDanger(MusicThemeDefinition theme, int chordDegree, int barIndex, int barSixteenth, float sixteenthPhase, float time)
        {
            bool trigger = barSixteenth == 0
                || (barSixteenth == 8 && theme.DangerWeight > 0.4f)
                || (barSixteenth == 12 && theme.DangerWeight > 0.7f && PatternChance(theme.ThemeSeed + 71, barIndex, 12) > 0.45f);
            if (!trigger)
                return 0f;

            int note = GetScaleNote(theme, chordDegree - 2, -2);
            float frequency = MidiToFrequency(note);
            float gate = GetStepEnvelope(sixteenthPhase, 0.92f);
            float low = SampleWaveform(SynthWaveform.Saw, frequency, time, 0.5f) * 0.28f;
            float growl = SampleWaveform(SynthWaveform.Pulse, frequency * 0.5f, time, 0.3f) * 0.16f;
            float noise = (HashNoise(time * 3200f) * 2f - 1f) * 0.14f * theme.DangerWeight;
            return ApplyDrive((low + growl + noise) * gate, 1.12f + theme.DangerWeight * 0.28f) * 0.24f;
        }

        private static float RenderBoss(MusicThemeDefinition theme, int chordDegree, int barIndex, int barEighth, float eighthPhase, float time)
        {
            int patternValue = theme.BossPattern[(barIndex * 8 + barEighth) % theme.BossPattern.Length];
            if (patternValue <= -99)
                return 0f;

            int note = GetScaleNote(theme, chordDegree + patternValue, -1);
            float frequency = MidiToFrequency(note);
            float gate = GetStepEnvelope(eighthPhase, 0.84f);
            float body = SampleWaveform(SynthWaveform.Saw, frequency, time, 0.5f) * 0.46f;
            float edge = SampleWaveform(SynthWaveform.Pulse, frequency * 2f, time, 0.3f) * 0.18f;
            float sub = SampleWaveform(SynthWaveform.Sine, frequency * 0.5f, time, 0.5f) * 0.16f;
            return ApplyDrive((body + edge + sub) * gate, 1.18f + theme.BossWeight * 0.2f) * 0.24f;
        }

        private static float DrumKick(float localSeconds)
        {
            const float startFrequency = 118f;
            const float endFrequency = 42f;
            const float sweepSeconds = 0.14f;
            float sweepTime = Math.Min(localSeconds, sweepSeconds);
            float cycles = startFrequency * sweepTime + 0.5f * (endFrequency - startFrequency) * sweepTime * sweepTime / sweepSeconds;
            if (localSeconds > sweepSeconds)
                cycles += endFrequency * (localSeconds - sweepSeconds);

            float click = localSeconds < 0.008f ? (1f - localSeconds / 0.008f) * 0.16f : 0f;
            return (OscillatorSin(cycles * MathF.Tau) * 0.92f + click) * Decay(localSeconds * 18f);
        }

        private static float DrumSnare(float localSeconds)
        {
            float envelope = Decay(localSeconds * 24f);
            float tone = OscillatorSin(localSeconds * 190f * MathF.Tau) * 0.24f;
            float noise = (HashNoise(localSeconds * 13800f) * 2f - 1f) * 0.76f;
            return (tone + noise) * envelope * 0.48f;
        }

        private static float DrumHat(float localSeconds, float density)
        {
            float color = 0.07f + density * 0.045f;
            float brightNoise = HashNoise(localSeconds * 21000f) * 2f - 1f;
            float darkNoise = HashNoise(localSeconds * 9400f + 0.37f) * 2f - 1f;
            return (brightNoise - darkNoise * 0.55f) * Decay(localSeconds * (54f + density * 18f)) * color;
        }

        private static float ApplySwing(float beatInBar, float swingAmount)
        {
            float step = beatInBar * 2f;
            int baseStep = (int)MathF.Floor(step);
            float phase = step - baseStep;
            if ((baseStep & 1) == 1)
                phase = MathHelper.Clamp(phase + swingAmount * 0.18f, 0f, 1f);

            return (baseStep + phase) / 2f;
        }

        private static float GetStepEnvelope(float phase, float sustainPoint)
        {
            if (phase <= 0f)
                return 1f;

            float clampedSustain = MathHelper.Clamp(sustainPoint, 0.1f, 0.98f);
            if (phase < clampedSustain)
                return MathHelper.Lerp(1f, 0.68f, phase / clampedSustain);

            return 0.68f * (1f - MathHelper.Clamp((phase - clampedSustain) / Math.Max(0.02f, 1f - clampedSustain), 0f, 1f));
        }

        private static int GetScaleNote(MusicThemeDefinition theme, int scaleDegree, int octaveOffset)
        {
            int scaleLength = Math.Max(1, theme.ScaleOffsets.Length);
            int octaveDelta = (int)MathF.Floor(scaleDegree / (float)scaleLength);
            int normalizedDegree = scaleDegree % scaleLength;
            if (normalizedDegree < 0)
            {
                normalizedDegree += scaleLength;
                octaveDelta--;
            }

            return theme.RootMidiNote + theme.ScaleOffsets[normalizedDegree] + (octaveOffset + octaveDelta) * 12;
        }

        private static float PatternChance(int seed, int a, int b)
        {
            return HashNoise(seed * 0.137f + a * 1.913f + b * 0.719f);
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
                "Arc" => 420f + OscillatorSin(time * 14f) * 90f,
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
                "RewindLoop" => 140f + OscillatorSin(time * 3.4f) * 18f,
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
                SynthWaveform.Sine => OscillatorSin(MathF.Tau * wrapped),
                SynthWaveform.Triangle => 1f - 4f * MathF.Abs(wrapped - 0.5f),
                SynthWaveform.Square => wrapped < 0.5f ? 1f : -1f,
                SynthWaveform.Pulse => wrapped < MathHelper.Clamp(pulseWidth, 0.05f, 0.95f) ? 1f : -1f,
                SynthWaveform.Saw => wrapped * 2f - 1f,
                _ => 0f,
            };
        }

        private static float ApplyDrive(float sample, float drive)
        {
            return SoftClip(sample * Math.Max(0.1f, drive));
        }

        private static float MidiToFrequency(int midiNote)
        {
            return MidiFrequencies[Math.Clamp(midiNote, 0, MidiFrequencies.Length - 1)];
        }

        private static float HashNoise(float value)
        {
            return FastNoise(value);
        }

        private static float[] BuildMidiFrequencies()
        {
            var frequencies = new float[128];
            float frequency = 8.1757989156f;
            const float semitoneRatio = 1.05946309436f;
            for (int note = 0; note < frequencies.Length; note++)
            {
                frequencies[note] = frequency;
                frequency *= semitoneRatio;
            }

            return frequencies;
        }

        private static float OscillatorSin(float radians)
        {
#if BLAZORGL
            float wrapped = radians - MathF.Floor((radians + MathF.PI) / MathF.Tau) * MathF.Tau;
            float shaped = 1.27323954f * wrapped - 0.405284735f * wrapped * MathF.Abs(wrapped);
            return 0.225f * (shaped * MathF.Abs(shaped) - shaped) + shaped;
#else
            return MathF.Sin(radians);
#endif
        }

        private static float Decay(float positiveExponent)
        {
#if BLAZORGL
            float value = 1f / (1f + Math.Max(0f, positiveExponent) * 0.125f);
            value *= value;
            value *= value;
            return value * value;
#else
            return MathF.Exp(-positiveExponent);
#endif
        }

        private static float SoftClip(float sample)
        {
#if BLAZORGL
            float clamped = MathHelper.Clamp(sample, -3f, 3f);
            float square = clamped * clamped;
            return clamped * (27f + square) / (27f + 9f * square);
#else
            return MathF.Tanh(sample);
#endif
        }

        private static float FastNoise(float value)
        {
#if BLAZORGL
            unchecked
            {
                uint bits = (uint)BitConverter.SingleToInt32Bits(value);
                bits ^= bits >> 16;
                bits *= 0x7FEB352Du;
                bits ^= bits >> 15;
                bits *= 0x846CA68Bu;
                bits ^= bits >> 16;
                return (bits & 0x00FFFFFFu) / 16777215f;
            }
#else
            float sine = MathF.Sin(value * 12.9898f) * 43758.5453f;
            return sine - MathF.Floor(sine);
#endif
        }
    }
}
