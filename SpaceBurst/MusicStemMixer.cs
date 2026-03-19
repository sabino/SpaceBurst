using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;

namespace SpaceBurst
{
    enum MusicStemKind
    {
        Drums,
        Bass,
        Pad,
        Pulse,
        Lead,
        Danger,
        Boss,
    }

    sealed class MusicStemMixer : IDisposable
    {
        private sealed class ThemeStemSet
        {
            public SoundEffect Drums;
            public SoundEffect Bass;
            public SoundEffect Pad;
            public SoundEffect Pulse;
            public SoundEffect Lead;
            public SoundEffect Danger;
            public SoundEffect Boss;
        }

        private sealed class ThemePlayer : IDisposable
        {
            private readonly List<SoundEffectInstance> instances = new List<SoundEffectInstance>();

            public string ThemeId { get; private set; }
            public bool IsActive { get; private set; }
            public float Blend { get; set; }

            public void Play(ThemeStemSet stems, string themeId)
            {
                Dispose();
                ThemeId = themeId;
                IsActive = true;
                Blend = 0f;
                AddLoop(stems.Drums);
                AddLoop(stems.Bass);
                AddLoop(stems.Pad);
                AddLoop(stems.Pulse);
                AddLoop(stems.Lead);
                AddLoop(stems.Danger);
                AddLoop(stems.Boss);
            }

            public void ApplyVolumes(float[] layerVolumes, float masterVolume, float musicVolume)
            {
                int count = Math.Min(instances.Count, layerVolumes.Length);
                float scale = MathHelper.Clamp(masterVolume * musicVolume * Blend, 0f, 1f);
                for (int i = 0; i < count; i++)
                    instances[i].Volume = MathHelper.Clamp(layerVolumes[i] * scale, 0f, 1f);
            }

            public void Stop()
            {
                Dispose();
            }

            public void Dispose()
            {
                for (int i = 0; i < instances.Count; i++)
                {
                    try
                    {
                        instances[i].Stop();
                    }
                    catch
                    {
                    }

                    instances[i].Dispose();
                }

                instances.Clear();
                ThemeId = null;
                IsActive = false;
                Blend = 0f;
            }

            private void AddLoop(SoundEffect effect)
            {
                SoundEffectInstance instance = effect.CreateInstance();
                instance.IsLooped = true;
                instance.Volume = 0f;
                instance.Play();
                instances.Add(instance);
            }
        }

        private readonly Dictionary<string, ThemeStemSet> themes = new Dictionary<string, ThemeStemSet>(StringComparer.OrdinalIgnoreCase);
        private readonly ThemePlayer current = new ThemePlayer();
        private readonly ThemePlayer previous = new ThemePlayer();
        private readonly int sampleRate;
        private readonly float stemDurationSeconds;

        public MusicStemMixer(AudioQualityPreset qualityPreset)
        {
            sampleRate = qualityPreset switch
            {
                AudioQualityPreset.Reduced => 22050,
                AudioQualityPreset.High => 44100,
                _ => 32000,
            };
            stemDurationSeconds = qualityPreset == AudioQualityPreset.Reduced ? 4f : 6f;
            RegisterThemes();
        }

        public void Update(GameAudioState state, float masterVolume, float musicVolume, float deltaSeconds)
        {
            string nextTheme = ResolveThemeId(state);
            if (!string.Equals(current.ThemeId, nextTheme, StringComparison.OrdinalIgnoreCase))
                SwitchTheme(nextTheme);

            float[] targetLayers = ResolveLayerVolumes(state);
            current.Blend = MathHelper.Clamp(current.Blend + deltaSeconds * 1.35f, 0f, 1f);
            previous.Blend = MathHelper.Clamp(previous.Blend - deltaSeconds * 1.6f, 0f, 1f);

            current.ApplyVolumes(targetLayers, masterVolume, musicVolume);
            if (previous.IsActive)
            {
                previous.ApplyVolumes(targetLayers, masterVolume, musicVolume * 0.6f);
                if (previous.Blend <= 0.01f)
                    previous.Stop();
            }
        }

        public void Dispose()
        {
            current.Dispose();
            previous.Dispose();

            foreach (ThemeStemSet set in themes.Values)
            {
                set.Drums?.Dispose();
                set.Bass?.Dispose();
                set.Pad?.Dispose();
                set.Pulse?.Dispose();
                set.Lead?.Dispose();
                set.Danger?.Dispose();
                set.Boss?.Dispose();
            }

            themes.Clear();
        }

        private void RegisterThemes()
        {
            RegisterTheme(new MusicThemeDefinition
            {
                Id = "title",
                Tempo = 108,
                RootMidiNote = 45,
                ScaleOffsets = new[] { 0, 3, 7, 10 },
                Brightness = 0.56f,
                PulseDrive = 0.34f,
                PadSpread = 0.74f,
            });
            RegisterTheme(new MusicThemeDefinition
            {
                Id = "combat",
                Tempo = 132,
                RootMidiNote = 48,
                ScaleOffsets = new[] { 0, 2, 3, 7, 10 },
                Brightness = 0.68f,
                PulseDrive = 0.62f,
                PadSpread = 0.52f,
            });
            RegisterTheme(new MusicThemeDefinition
            {
                Id = "boss",
                Tempo = 140,
                RootMidiNote = 41,
                ScaleOffsets = new[] { 0, 1, 5, 7, 8 },
                Brightness = 0.78f,
                PulseDrive = 0.84f,
                PadSpread = 0.48f,
            });

            SwitchTheme("title");
            current.Blend = 1f;
        }

        private void RegisterTheme(MusicThemeDefinition definition)
        {
            themes[definition.Id] = new ThemeStemSet
            {
                Drums = ProceduralAudioSynth.CreateMusicStem(definition, sampleRate, stemDurationSeconds, MusicStemKind.Drums),
                Bass = ProceduralAudioSynth.CreateMusicStem(definition, sampleRate, stemDurationSeconds, MusicStemKind.Bass),
                Pad = ProceduralAudioSynth.CreateMusicStem(definition, sampleRate, stemDurationSeconds, MusicStemKind.Pad),
                Pulse = ProceduralAudioSynth.CreateMusicStem(definition, sampleRate, stemDurationSeconds, MusicStemKind.Pulse),
                Lead = ProceduralAudioSynth.CreateMusicStem(definition, sampleRate, stemDurationSeconds, MusicStemKind.Lead),
                Danger = ProceduralAudioSynth.CreateMusicStem(definition, sampleRate, stemDurationSeconds, MusicStemKind.Danger),
                Boss = ProceduralAudioSynth.CreateMusicStem(definition, sampleRate, stemDurationSeconds, MusicStemKind.Boss),
            };
        }

        private void SwitchTheme(string themeId)
        {
            if (!themes.TryGetValue(themeId, out ThemeStemSet stems))
                return;

            if (current.IsActive && !string.IsNullOrWhiteSpace(current.ThemeId))
            {
                previous.Stop();
                previous.Play(themes[current.ThemeId], current.ThemeId);
                previous.Blend = current.Blend;
            }

            current.Stop();
            current.Play(stems, themeId);
        }

        private static string ResolveThemeId(GameAudioState state)
        {
            if (state.FlowState == GameFlowState.Title)
                return "title";

            if (state.HasBoss || state.FlowState == GameFlowState.StageTransition && state.TransitionToBoss)
                return "boss";

            return "combat";
        }

        private static float[] ResolveLayerVolumes(GameAudioState state)
        {
            float danger = MathHelper.Clamp(state.DangerFactor, 0f, 1f);
            float transition = MathHelper.Clamp(state.TransitionWarpStrength, 0f, 1f);
            float rewind = MathHelper.Clamp(state.RewindStrength, 0f, 1f);
            float pause = state.FlowState == GameFlowState.Paused || state.FlowState == GameFlowState.Help || state.FlowState == GameFlowState.Options ? 0.24f : 1f;

            if (state.FlowState == GameFlowState.Title)
                return new[] { 0f, 0.12f * pause, 0.24f * pause, 0.08f * pause, 0.18f * pause, 0f, 0f };

            if (state.FlowState == GameFlowState.GameOver || state.FlowState == GameFlowState.CampaignComplete)
                return new[] { 0f, 0.08f, 0.18f, 0.02f, 0.06f, 0f, 0f };

            if (state.HasBoss)
            {
                return new[]
                {
                    (0.24f + danger * 0.08f) * pause,
                    (0.24f + danger * 0.06f) * pause,
                    0.12f * pause,
                    0.18f * pause,
                    0.16f * pause,
                    0.08f * pause,
                    (0.22f + transition * 0.08f) * pause,
                };
            }

            float transitionBoost = transition * 0.18f;
            float rewindDuck = 1f - rewind * 0.7f;
            return new[]
            {
                (0.18f + danger * 0.12f) * pause * rewindDuck,
                (0.18f + danger * 0.08f) * pause * rewindDuck,
                (0.16f + transitionBoost) * pause * (1f - rewind * 0.35f),
                (0.12f + danger * 0.08f + transitionBoost) * pause,
                (0.08f + danger * 0.06f) * pause * rewindDuck,
                (danger * 0.1f + transition * 0.08f) * pause,
                transition * 0.08f * pause,
            };
        }
    }
}
