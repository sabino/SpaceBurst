using System;
using Xunit;

namespace SpaceBurst.Tests
{
    public sealed class ProceduralAudioSynthTests
    {
        [Fact]
        public void ClassicDrumGridKeepsDownbeatsStrongerThanTheGaps()
        {
            MusicThemeDefinition theme = CreateTheme();
            double downbeatEnergy = MeasureMusicRms(theme, MusicStemKind.Drums, 0f, 0.045f);
            double gapEnergy = MeasureMusicRms(theme, MusicStemKind.Drums, 0.17f, 0.215f);

            Assert.True(downbeatEnergy > gapEnergy * 2.5, $"Expected a clear downbeat. Downbeat={downbeatEnergy:0.0000}, gap={gapEnergy:0.0000}");
        }

        [Fact]
        public void ExplosionHasAFrontLoadedImpactEnvelope()
        {
            SynthPatchDefinition patch = ProceduralAudioSynth.ExplosionPatch();
            double attackEnergy = MeasureEffectRms(patch, 0.01f, 0.11f, 0.5f);
            double tailEnergy = MeasureEffectRms(patch, 0.39f, 0.49f, 0.5f);

            Assert.True(attackEnergy > tailEnergy * 2.0, $"Expected the explosion to decay. Attack={attackEnergy:0.0000}, tail={tailEnergy:0.0000}");
        }

        [Fact]
        public void ConfirmAndCancelUseDistinctMusicalSignatures()
        {
            SynthPatchDefinition confirm = ProceduralAudioSynth.UiPatch(true);
            SynthPatchDefinition cancel = ProceduralAudioSynth.UiPatch(false);
            double difference = 0d;
            const int sampleRate = 8000;
            const float duration = 0.1f;
            for (int i = 0; i < sampleRate * duration; i++)
            {
                float time = i / (float)sampleRate;
                difference += Math.Abs(
                    ProceduralAudioSynth.RenderEffectPreviewSample(time, duration, confirm)
                    - ProceduralAudioSynth.RenderEffectPreviewSample(time, duration, cancel));
            }

            Assert.True(difference / (sampleRate * duration) > 0.12d);
        }

        [Fact]
        public void ExplosionBankVariationsDoNotRenderTheSameWaveform()
        {
            SynthPatchDefinition low = ProceduralAudioSynth.ExplosionPatch(0.85f);
            SynthPatchDefinition high = ProceduralAudioSynth.ExplosionPatch(1.15f);
            double difference = 0d;
            const int sampleRate = 8000;
            const float duration = 0.5f;
            for (int i = 0; i < sampleRate * duration; i++)
            {
                float time = i / (float)sampleRate;
                difference += Math.Abs(
                    ProceduralAudioSynth.RenderEffectPreviewSample(time, duration, low)
                    - ProceduralAudioSynth.RenderEffectPreviewSample(time, duration, high));
            }

            Assert.True(difference / (sampleRate * duration) > 0.08d);
        }

        private static double MeasureMusicRms(MusicThemeDefinition theme, MusicStemKind kind, float start, float end)
        {
            const int sampleRate = 8000;
            double sum = 0d;
            int count = 0;
            for (int i = (int)(start * sampleRate); i < (int)(end * sampleRate); i++)
            {
                float sample = ProceduralAudioSynth.RenderMusicPreviewSample(theme, kind, i / (float)sampleRate, 2f);
                sum += sample * sample;
                count++;
            }

            return Math.Sqrt(sum / Math.Max(1, count));
        }

        private static double MeasureEffectRms(SynthPatchDefinition patch, float start, float end, float duration)
        {
            const int sampleRate = 8000;
            double sum = 0d;
            int count = 0;
            for (int i = (int)(start * sampleRate); i < (int)(end * sampleRate); i++)
            {
                float sample = ProceduralAudioSynth.RenderEffectPreviewSample(i / (float)sampleRate, duration, patch);
                sum += sample * sample;
                count++;
            }

            return Math.Sqrt(sum / Math.Max(1, count));
        }

        private static MusicThemeDefinition CreateTheme()
        {
            return new MusicThemeDefinition
            {
                Id = "test-main",
                Bars = 1,
                Tempo = 120,
                RootMidiNote = 48,
                ScaleOffsets = new[] { 0, 2, 3, 5, 7, 9, 10 },
                ChordDegrees = new[] { 0 },
                BassPattern = new[] { 0, -99, 0, -99, 0, -99, 0, -99 },
                PulsePattern = new[] { 0, -99, 2, -99, 4, -99, 2, -99, 0, -99, 2, -99, 4, -99, 2, -99 },
                LeadPatternA = new[] { -99, 0, -99, 2, -99, 4, -99, 2 },
                LeadPatternB = new[] { -99, 2, -99, 4, -99, 2, -99, 0 },
                BossPattern = new[] { 0, -99, 0, -99, 0, -99, 0, -99 },
                PadChordSteps = new[] { 0, 2, 4 },
                RhythmDensity = 0.62f,
                LeadDensity = 0.5f,
            };
        }
    }
}
