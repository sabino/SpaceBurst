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

            public void TakeOver(ThemePlayer source)
            {
                Dispose();
                ThemeId = source.ThemeId;
                IsActive = source.IsActive;
                Blend = source.Blend;
                instances.AddRange(source.instances);
                source.instances.Clear();
                source.ThemeId = null;
                source.IsActive = false;
                source.Blend = 0f;
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

        private sealed class ChapterProfile
        {
            public string IdPrefix;
            public int RootMidiNote;
            public int BaseTempo;
            public int BossTempo;
            public int ThemeSeed;
            public int[] NormalScale;
            public int[] BossScale;
            public int[] ApproachChords;
            public int[] CruiseChords;
            public int[] SurgeChords;
            public int[] BossChords;
            public int[] BassMotif;
            public int[] PulseMotif;
            public int[] LeadCall;
            public int[] LeadResponse;
            public int[] BossMotif;
            public float Brightness;
            public float PulseDrive;
            public float PadSpread;
            public float Swing;
            public float Syncopation;
        }

        private readonly Dictionary<string, ThemeStemSet> themes = new Dictionary<string, ThemeStemSet>(StringComparer.OrdinalIgnoreCase);
        private readonly ThemePlayer current = new ThemePlayer();
        private readonly ThemePlayer previous = new ThemePlayer();
        private readonly int sampleRate;

        public MusicStemMixer(AudioQualityPreset qualityPreset)
        {
            sampleRate = qualityPreset switch
            {
                AudioQualityPreset.Reduced => 22050,
                AudioQualityPreset.High => 44100,
                _ => 32000,
            };
            RegisterThemes();
        }

        public void Update(GameAudioState state, float masterVolume, float musicVolume, float deltaSeconds)
        {
            string nextTheme = ResolveThemeId(state);
            if (!string.Equals(current.ThemeId, nextTheme, StringComparison.OrdinalIgnoreCase))
                SwitchTheme(nextTheme);

            float[] targetLayers = ResolveLayerVolumes(state);
            float fadeInRate = 0.9f + state.TransitionWarpStrength * 0.8f + (state.HasBoss ? 0.18f : 0f);
            float fadeOutRate = 1.2f + state.TransitionWarpStrength * 0.6f;
            current.Blend = MathHelper.Clamp(current.Blend + deltaSeconds * fadeInRate, 0f, 1f);
            previous.Blend = MathHelper.Clamp(previous.Blend - deltaSeconds * fadeOutRate, 0f, 1f);

            current.ApplyVolumes(targetLayers, masterVolume, musicVolume);
            if (previous.IsActive)
            {
                previous.ApplyVolumes(targetLayers, masterVolume, musicVolume * 0.76f);
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
            RegisterTheme(CreateSpecialTheme(
                "title",
                108,
                45,
                new[] { 0, 3, 7, 10, 12 },
                ChainPatterns(new[] { 0, 3, 4, 2 }, new[] { 0, 5, 4, 3 }),
                ChainPatterns(new[] { 0, -99, 0, 1, 0, -99, 2, 1 }, new[] { 0, -99, 1, 2, 0, -99, 3, 2 }),
                ChainPatterns(
                    new[] { 0, 2, 4, 2, 1, 2, 4, 5, 0, 2, 4, 2, 1, 3, 4, 5 },
                    new[] { 4, 5, 7, 5, 4, 5, 7, 9, 4, 5, 7, 5, 3, 5, 7, 9 }),
                ChainPatterns(
                    new[] { -99, 0, 2, 4, -99, 2, 1, 0, -99, 1, 2, 4, -99, 3, 5, 4 },
                    new[] { -99, 4, 5, 7, -99, 7, 5, 4, -99, 5, 7, 9, -99, 7, 5, 4 }),
                ChainPatterns(
                    new[] { 4, -99, 2, 1, 0, -99, 2, 3, 4, -99, 5, 4, 2, -99, 1, 0 },
                    new[] { 9, -99, 7, 5, 4, -99, 5, 7, 9, -99, 10, 9, 7, -99, 5, 4 }),
                ChainPatterns(new[] { 0, 0, 2, 0, 3, 0, 2, 0 }, new[] { 0, 0, 4, 0, 3, 0, 2, 0 }),
                0.52f,
                0.34f,
                0.76f,
                0.08f,
                0.28f,
                0.36f,
                0.18f));
            RegisterTheme(CreateSpecialTheme(
                "tutorial",
                102,
                47,
                new[] { 0, 2, 5, 7, 9, 10 },
                new[] { 0, 2, 3, 1 },
                new[] { 0, -99, 0, 1, 0, -99, 2, 1 },
                new[] { 0, 2, 4, 2, 1, 2, 4, 2, 0, 2, 5, 2, 1, 3, 4, 2 },
                new[] { -99, 0, 2, 3, -99, 2, 1, 0, -99, 1, 2, 4, -99, 2, 3, 2 },
                new[] { 3, -99, 2, 1, 0, -99, 2, 1, 3, -99, 4, 3, 2, -99, 1, 0 },
                new[] { 0, 0, 1, 0, 2, 0, 1, 0 },
                0.5f,
                0.3f,
                0.72f,
                0.06f,
                0.22f,
                0.28f,
                0.15f));
            RegisterTheme(CreateSpecialTheme(
                "results",
                96,
                50,
                new[] { 0, 2, 4, 7, 9, 11 },
                new[] { 0, 3, 4, 2 },
                new[] { 0, -99, 0, 2, 0, -99, 1, 2 },
                new[] { 0, 2, 4, 5, 2, 4, 5, 7, 0, 2, 4, 5, 3, 4, 5, 7 },
                new[] { -99, 0, 2, 4, -99, 4, 5, 7, -99, 7, 5, 4, -99, 2, 4, 5 },
                new[] { 7, -99, 5, 4, 2, -99, 4, 5, 7, -99, 9, 7, 5, -99, 4, 2 },
                new[] { 0, 0, 2, 0, 4, 0, 3, 0 },
                0.7f,
                0.42f,
                0.82f,
                0.05f,
                0.18f,
                0.3f,
                0.1f,
                8));

            foreach (ChapterProfile profile in BuildChapterProfiles())
                RegisterChapterThemes(profile);

            SwitchTheme("title");
            current.Blend = 1f;
        }

        private void RegisterChapterThemes(ChapterProfile profile)
        {
            RegisterTheme(CreateMainChapterTheme(profile));
            RegisterTheme(CreateBossChapterTheme(profile));
        }

        private void RegisterTheme(MusicThemeDefinition definition)
        {
            float durationSeconds = definition.Bars * 4f * 60f / Math.Max(60f, definition.Tempo);
            themes[definition.Id] = new ThemeStemSet
            {
                Drums = ProceduralAudioSynth.CreateMusicStem(definition, sampleRate, durationSeconds, MusicStemKind.Drums),
                Bass = ProceduralAudioSynth.CreateMusicStem(definition, sampleRate, durationSeconds, MusicStemKind.Bass),
                Pad = ProceduralAudioSynth.CreateMusicStem(definition, sampleRate, durationSeconds, MusicStemKind.Pad),
                Pulse = ProceduralAudioSynth.CreateMusicStem(definition, sampleRate, durationSeconds, MusicStemKind.Pulse),
                Lead = ProceduralAudioSynth.CreateMusicStem(definition, sampleRate, durationSeconds, MusicStemKind.Lead),
                Danger = ProceduralAudioSynth.CreateMusicStem(definition, sampleRate, durationSeconds, MusicStemKind.Danger),
                Boss = ProceduralAudioSynth.CreateMusicStem(definition, sampleRate, durationSeconds, MusicStemKind.Boss),
            };
        }

        private void SwitchTheme(string themeId)
        {
            if (!themes.TryGetValue(themeId, out ThemeStemSet stems))
                return;

            if (current.IsActive && !string.IsNullOrWhiteSpace(current.ThemeId))
            {
                previous.Stop();
                previous.TakeOver(current);
            }

            current.Play(stems, themeId);
        }

        private static string ResolveThemeId(GameAudioState state)
        {
            if (state.FlowState == GameFlowState.Title)
                return "title";

            if (state.FlowState == GameFlowState.Tutorial)
                return "tutorial";

            if (state.FlowState == GameFlowState.GameOver || state.FlowState == GameFlowState.CampaignComplete)
                return "results";

            int stageNumber = state.CurrentStageNumber;
            if (state.FlowState == GameFlowState.StageTransition && state.TransitionTargetStageNumber > 0)
                stageNumber = state.TransitionTargetStageNumber;

            if (stageNumber <= 0)
                return "title";

            int chapter = Math.Clamp((stageNumber - 1) / 10 + 1, 1, 5);
            int stageWithinChapter = ((stageNumber - 1) % 10) + 1;

            if (state.HasBoss || state.TransitionToBoss || stageWithinChapter == 10)
                return string.Concat("chapter", chapter.ToString(), "-boss");

            return string.Concat("chapter", chapter.ToString(), "-main");
        }

        private static float[] ResolveLayerVolumes(GameAudioState state)
        {
            float pause = state.FlowState == GameFlowState.Paused || state.FlowState == GameFlowState.Help || state.FlowState == GameFlowState.Options || state.FlowState == GameFlowState.SaveSlots || state.FlowState == GameFlowState.LoadSlots ? 0.22f : 1f;
            float rewind = MathHelper.Clamp(state.RewindStrength, 0f, 1f);
            float rewindDuck = 1f - rewind * 0.72f;
            float transition = MathHelper.Clamp(state.TransitionWarpStrength, 0f, 1f);
            float danger = MathHelper.Clamp(state.DangerFactor, 0f, 1f);

            if (state.FlowState == GameFlowState.Title)
                return new[] { 0.06f * pause, 0.14f * pause, 0.22f * pause, 0.1f * pause, 0.12f * pause, 0f, 0f };

            if (state.FlowState == GameFlowState.Tutorial)
                return new[] { 0.08f * pause, 0.16f * pause, 0.18f * pause, 0.1f * pause, 0.08f * pause, 0.04f * pause, 0f };

            if (state.FlowState == GameFlowState.GameOver || state.FlowState == GameFlowState.CampaignComplete)
                return new[] { 0.04f, 0.08f, 0.16f, 0.05f, 0.1f, 0f, 0f };

            int stageNumber = state.CurrentStageNumber > 0 ? state.CurrentStageNumber : Math.Max(1, state.TransitionTargetStageNumber);
            int stageWithinChapter = ((stageNumber - 1) % 10) + 1;
            float chapterArc = MathHelper.Clamp((stageWithinChapter - 1) / 9f, 0f, 1f);
            float sectionArc = MathHelper.Clamp(state.CurrentSectionIndex * 0.12f + state.CurrentSectionProgress * 0.24f, 0f, 1f);
            float escalation = MathHelper.Clamp(chapterArc * 0.68f + sectionArc * 0.32f, 0f, 1f);
            float warpBoost = transition * 0.18f;

            if (state.HasBoss || state.TransitionToBoss || stageWithinChapter == 10)
            {
                float bossLayer = MathHelper.Clamp(0.24f + transition * 0.18f + danger * 0.14f, 0f, 0.52f);
                return new[]
                {
                    (0.16f + escalation * 0.12f + danger * 0.06f) * pause * rewindDuck,
                    (0.18f + escalation * 0.1f) * pause * rewindDuck,
                    (0.12f + warpBoost) * pause * (1f - rewind * 0.25f),
                    (0.12f + escalation * 0.09f + transition * 0.06f) * pause,
                    (0.1f + escalation * 0.1f + danger * 0.06f) * pause * rewindDuck,
                    (0.1f + danger * 0.16f + transition * 0.04f) * pause,
                    bossLayer * pause,
                };
            }

            return new[]
            {
                (0.08f + escalation * 0.16f + danger * 0.05f) * pause * rewindDuck,
                (0.12f + escalation * 0.14f + danger * 0.04f) * pause * rewindDuck,
                (0.16f + warpBoost) * pause * (1f - rewind * 0.2f),
                (0.08f + escalation * 0.12f + transition * 0.05f) * pause,
                (0.06f + escalation * 0.14f + danger * 0.08f) * pause * rewindDuck,
                (0.02f + escalation * 0.04f + danger * 0.14f + transition * 0.04f) * pause,
                transition * 0.04f * pause,
            };
        }

        private static IEnumerable<ChapterProfile> BuildChapterProfiles()
        {
            yield return new ChapterProfile
            {
                IdPrefix = "chapter1",
                RootMidiNote = 46,
                BaseTempo = 116,
                BossTempo = 132,
                ThemeSeed = 11,
                NormalScale = new[] { 0, 2, 3, 5, 7, 9, 10 },
                BossScale = new[] { 0, 1, 3, 5, 7, 8, 10 },
                ApproachChords = new[] { 0, 3, 4, 2 },
                CruiseChords = new[] { 0, 4, 5, 3 },
                SurgeChords = new[] { 0, 5, 4, 6 },
                BossChords = new[] { 0, 1, 4, 3 },
                BassMotif = new[] { 0, -99, 0, 2, 1, -99, 0, 3 },
                PulseMotif = new[] { 0, 2, 4, 2, 1, 2, 4, 5, 0, 2, 4, 2, 1, 3, 5, 4 },
                LeadCall = new[] { -99, 0, 2, 4, -99, 2, 1, 0, -99, 1, 2, 4, -99, 2, 5, 4 },
                LeadResponse = new[] { 4, -99, 2, 1, 0, -99, 2, 3, 4, -99, 5, 4, 2, -99, 1, 0 },
                BossMotif = new[] { 0, 0, 2, 0, 3, 0, 4, 0 },
                Brightness = 0.62f,
                PulseDrive = 0.56f,
                PadSpread = 0.68f,
                Swing = 0.08f,
                Syncopation = 0.28f,
            };

            yield return new ChapterProfile
            {
                IdPrefix = "chapter2",
                RootMidiNote = 43,
                BaseTempo = 122,
                BossTempo = 138,
                ThemeSeed = 23,
                NormalScale = new[] { 0, 1, 3, 5, 7, 8, 10 },
                BossScale = new[] { 0, 1, 3, 5, 6, 8, 10 },
                ApproachChords = new[] { 0, 2, 5, 3 },
                CruiseChords = new[] { 0, 4, 2, 5 },
                SurgeChords = new[] { 0, 5, 3, 6 },
                BossChords = new[] { 0, 1, 5, 4 },
                BassMotif = new[] { 0, -99, 1, 2, 0, -99, 3, 2 },
                PulseMotif = new[] { 0, 2, 3, 5, 1, 3, 5, 6, 0, 2, 3, 5, 1, 4, 6, 5 },
                LeadCall = new[] { -99, 0, 1, 3, -99, 3, 2, 1, -99, 2, 3, 5, -99, 4, 5, 3 },
                LeadResponse = new[] { 5, -99, 3, 2, 1, -99, 3, 4, 5, -99, 6, 5, 3, -99, 2, 1 },
                BossMotif = new[] { 0, 0, 1, 0, 4, 0, 5, 0 },
                Brightness = 0.58f,
                PulseDrive = 0.6f,
                PadSpread = 0.56f,
                Swing = 0.05f,
                Syncopation = 0.34f,
            };

            yield return new ChapterProfile
            {
                IdPrefix = "chapter3",
                RootMidiNote = 48,
                BaseTempo = 126,
                BossTempo = 144,
                ThemeSeed = 37,
                NormalScale = new[] { 0, 2, 4, 7, 9, 11 },
                BossScale = new[] { 0, 2, 3, 6, 7, 9, 11 },
                ApproachChords = new[] { 0, 2, 4, 3 },
                CruiseChords = new[] { 0, 4, 5, 2 },
                SurgeChords = new[] { 0, 5, 4, 6 },
                BossChords = new[] { 0, 3, 4, 1 },
                BassMotif = new[] { 0, -99, 0, 2, 4, -99, 2, 1 },
                PulseMotif = new[] { 0, 2, 4, 6, 2, 4, 6, 4, 0, 2, 4, 6, 3, 4, 6, 7 },
                LeadCall = new[] { -99, 0, 2, 4, -99, 4, 6, 4, -99, 2, 4, 6, -99, 6, 7, 6 },
                LeadResponse = new[] { 6, -99, 4, 2, 4, -99, 6, 7, 6, -99, 4, 2, 4, -99, 2, 0 },
                BossMotif = new[] { 0, 0, 2, 0, 4, 0, 6, 0 },
                Brightness = 0.72f,
                PulseDrive = 0.68f,
                PadSpread = 0.5f,
                Swing = 0.03f,
                Syncopation = 0.42f,
            };

            yield return new ChapterProfile
            {
                IdPrefix = "chapter4",
                RootMidiNote = 42,
                BaseTempo = 132,
                BossTempo = 150,
                ThemeSeed = 53,
                NormalScale = new[] { 0, 2, 3, 5, 6, 8, 10 },
                BossScale = new[] { 0, 1, 3, 5, 6, 8, 9 },
                ApproachChords = new[] { 0, 3, 1, 4 },
                CruiseChords = new[] { 0, 4, 3, 5 },
                SurgeChords = new[] { 0, 5, 2, 6 },
                BossChords = new[] { 0, 1, 3, 6 },
                BassMotif = new[] { 0, -99, 1, 0, 3, -99, 2, 4 },
                PulseMotif = new[] { 0, 1, 3, 5, 1, 3, 5, 6, 0, 1, 3, 5, 2, 3, 5, 6 },
                LeadCall = new[] { -99, 0, 1, 3, -99, 3, 5, 3, -99, 1, 3, 5, -99, 5, 6, 5 },
                LeadResponse = new[] { 5, -99, 3, 1, 3, -99, 5, 6, 5, -99, 3, 1, 2, -99, 1, 0 },
                BossMotif = new[] { 0, 0, 1, 0, 3, 0, 6, 0 },
                Brightness = 0.56f,
                PulseDrive = 0.74f,
                PadSpread = 0.44f,
                Swing = 0.02f,
                Syncopation = 0.48f,
            };

            yield return new ChapterProfile
            {
                IdPrefix = "chapter5",
                RootMidiNote = 40,
                BaseTempo = 138,
                BossTempo = 156,
                ThemeSeed = 71,
                NormalScale = new[] { 0, 1, 3, 6, 7, 10 },
                BossScale = new[] { 0, 1, 3, 5, 6, 8, 10 },
                ApproachChords = new[] { 0, 2, 4, 1 },
                CruiseChords = new[] { 0, 4, 2, 5 },
                SurgeChords = new[] { 0, 5, 3, 6 },
                BossChords = new[] { 0, 1, 5, 2 },
                BassMotif = new[] { 0, -99, 0, 3, 1, -99, 4, 2 },
                PulseMotif = new[] { 0, 1, 3, 6, 1, 3, 6, 4, 0, 1, 3, 6, 2, 3, 5, 6 },
                LeadCall = new[] { -99, 0, 1, 3, -99, 3, 6, 4, -99, 1, 3, 6, -99, 5, 6, 4 },
                LeadResponse = new[] { 6, -99, 4, 3, 1, -99, 3, 5, 6, -99, 4, 3, 2, -99, 1, 0 },
                BossMotif = new[] { 0, 0, 1, 0, 5, 0, 3, 0 },
                Brightness = 0.64f,
                PulseDrive = 0.8f,
                PadSpread = 0.4f,
                Swing = 0.01f,
                Syncopation = 0.54f,
            };
        }

        private static MusicThemeDefinition CreateMainChapterTheme(ChapterProfile profile)
        {
            return new MusicThemeDefinition
            {
                Id = string.Concat(profile.IdPrefix, "-main"),
                Bars = 8,
                ThemeSeed = profile.ThemeSeed + 17,
                Tempo = profile.BaseTempo,
                RootMidiNote = profile.RootMidiNote,
                ScaleOffsets = profile.NormalScale,
                ChordDegrees = ChainPatterns(profile.ApproachChords, profile.CruiseChords),
                BassPattern = ChainPatterns(profile.BassMotif, RotatePattern(profile.BassMotif, 2), RotatePattern(TransposePattern(profile.BassMotif, 1), 1), RotatePattern(profile.BassMotif, 5)),
                PulsePattern = ChainPatterns(profile.PulseMotif, RotatePattern(profile.PulseMotif, 4), RotatePattern(TransposePattern(profile.PulseMotif, 1), 2), RotatePattern(profile.PulseMotif, 8)),
                LeadPatternA = ChainPatterns(profile.LeadCall, RotatePattern(profile.LeadResponse, 2), RotatePattern(TransposePattern(profile.LeadCall, 1), 4), RotatePattern(profile.LeadResponse, 6)),
                LeadPatternB = ChainPatterns(profile.LeadResponse, RotatePattern(profile.LeadCall, 2), RotatePattern(TransposePattern(profile.LeadResponse, 1), 4), RotatePattern(profile.LeadCall, 6)),
                BossPattern = ChainPatterns(profile.BossMotif, RotatePattern(profile.BossMotif, 2), RotatePattern(TransposePattern(profile.BossMotif, 1), 1), RotatePattern(profile.BossMotif, 4)),
                PadChordSteps = new[] { 0, 2, 4 },
                Brightness = MathHelper.Clamp(profile.Brightness * 1.04f, 0.35f, 0.92f),
                PulseDrive = MathHelper.Clamp(profile.PulseDrive * 1.08f, 0.18f, 1.1f),
                PadSpread = MathHelper.Clamp(profile.PadSpread + 0.05f, 0.32f, 0.84f),
                Swing = profile.Swing,
                RhythmDensity = 0.58f,
                Syncopation = MathHelper.Clamp(profile.Syncopation + 0.08f, 0.1f, 0.92f),
                LeadDensity = 0.68f,
                DangerWeight = 0.3f,
                BossWeight = 0.22f,
                VariantIntensity = 0.74f,
                BassOctave = -1,
                LeadOctave = 2,
            };
        }

        private static MusicThemeDefinition CreateBossChapterTheme(ChapterProfile profile)
        {
            return new MusicThemeDefinition
            {
                Id = string.Concat(profile.IdPrefix, "-boss"),
                Bars = 8,
                ThemeSeed = profile.ThemeSeed + 97,
                Tempo = profile.BossTempo,
                RootMidiNote = profile.RootMidiNote - 2,
                ScaleOffsets = profile.BossScale,
                ChordDegrees = ChainPatterns(profile.BossChords, RotatePattern(profile.BossChords, 1)),
                BassPattern = ChainPatterns(RotatePattern(TransposePattern(profile.BassMotif, 1), 1), RotatePattern(TransposePattern(profile.BassMotif, 2), 4), RotatePattern(TransposePattern(profile.BassMotif, 1), 2), RotatePattern(TransposePattern(profile.BassMotif, 3), 5)),
                PulsePattern = ChainPatterns(RotatePattern(TransposePattern(profile.PulseMotif, 1), 3), RotatePattern(TransposePattern(profile.PulseMotif, 2), 5), RotatePattern(TransposePattern(profile.PulseMotif, 1), 7), RotatePattern(TransposePattern(profile.PulseMotif, 3), 9)),
                LeadPatternA = ChainPatterns(RotatePattern(TransposePattern(profile.LeadCall, 1), 1), RotatePattern(TransposePattern(profile.LeadResponse, 2), 3), RotatePattern(TransposePattern(profile.LeadCall, 2), 5), RotatePattern(TransposePattern(profile.LeadResponse, 3), 7)),
                LeadPatternB = ChainPatterns(RotatePattern(TransposePattern(profile.LeadResponse, 2), 1), RotatePattern(TransposePattern(profile.LeadCall, 1), 3), RotatePattern(TransposePattern(profile.LeadResponse, 3), 5), RotatePattern(TransposePattern(profile.LeadCall, 2), 7)),
                BossPattern = ChainPatterns(profile.BossMotif, RotatePattern(profile.BossMotif, 2), RotatePattern(TransposePattern(profile.BossMotif, 1), 1), RotatePattern(TransposePattern(profile.BossMotif, 2), 3)),
                PadChordSteps = new[] { 0, 2, 5 },
                Brightness = MathHelper.Clamp(profile.Brightness * 1.18f, 0.35f, 0.96f),
                PulseDrive = MathHelper.Clamp(profile.PulseDrive * 1.24f, 0.18f, 1.16f),
                PadSpread = MathHelper.Clamp(profile.PadSpread + 0.02f, 0.32f, 0.82f),
                Swing = profile.Swing * 0.5f,
                RhythmDensity = 0.82f,
                Syncopation = MathHelper.Clamp(profile.Syncopation + 0.18f, 0.1f, 0.96f),
                LeadDensity = 0.82f,
                DangerWeight = 0.76f,
                BossWeight = 0.88f,
                VariantIntensity = 1f,
                BassOctave = -2,
                LeadOctave = 1,
            };
        }

        private static MusicThemeDefinition CreateSpecialTheme(string id, int tempo, int root, int[] scale, int[] chords, int[] bass, int[] pulse, int[] leadA, int[] leadB, int[] boss, float brightness, float pulseDrive, float padSpread, float swing, float syncopation, float leadDensity, float dangerWeight, int bars = 4)
        {
            return new MusicThemeDefinition
            {
                Id = id,
                Bars = bars,
                ThemeSeed = Math.Abs(id.GetHashCode()),
                Tempo = tempo,
                RootMidiNote = root,
                ScaleOffsets = scale,
                ChordDegrees = chords,
                BassPattern = bass,
                PulsePattern = pulse,
                LeadPatternA = leadA,
                LeadPatternB = leadB,
                BossPattern = boss,
                PadChordSteps = new[] { 0, 2, 4 },
                Brightness = brightness,
                PulseDrive = pulseDrive,
                PadSpread = padSpread,
                Swing = swing,
                RhythmDensity = 0.42f,
                Syncopation = syncopation,
                LeadDensity = leadDensity,
                DangerWeight = dangerWeight,
                BossWeight = 0.18f,
                VariantIntensity = 0.4f,
                BassOctave = -2,
                LeadOctave = 1,
            };
        }

        private static int[] ChainPatterns(params int[][] segments)
        {
            int totalLength = 0;
            for (int i = 0; i < segments.Length; i++)
                totalLength += segments[i]?.Length ?? 0;

            if (totalLength == 0)
                return Array.Empty<int>();

            int[] combined = new int[totalLength];
            int offset = 0;
            for (int i = 0; i < segments.Length; i++)
            {
                int[] segment = segments[i];
                if (segment == null || segment.Length == 0)
                    continue;

                Array.Copy(segment, 0, combined, offset, segment.Length);
                offset += segment.Length;
            }

            return combined;
        }

        private static int[] RotatePattern(int[] source, int shift)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<int>();

            int[] rotated = new int[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                int index = (i + shift) % source.Length;
                if (index < 0)
                    index += source.Length;

                rotated[i] = source[index];
            }

            return rotated;
        }

        private static int[] TransposePattern(int[] source, int semitoneStep)
        {
            if (source == null || source.Length == 0 || semitoneStep == 0)
                return source ?? Array.Empty<int>();

            int[] shifted = new int[source.Length];
            for (int i = 0; i < source.Length; i++)
                shifted[i] = source[i] <= -99 ? source[i] : source[i] + semitoneStep;

            return shifted;
        }
    }
}
