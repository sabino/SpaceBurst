using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using SpaceBurst.RuntimeData;

namespace SpaceBurst
{
    readonly struct GameAudioState
    {
        public GameAudioState(GameFlowState flowState, bool hasBoss, bool transitionToBoss, float dangerFactor, float transitionWarpStrength, float rewindStrength, float scrollSpeed, int currentStageNumber, int transitionTargetStageNumber, int currentSectionIndex, float currentSectionProgress)
        {
            FlowState = flowState;
            HasBoss = hasBoss;
            TransitionToBoss = transitionToBoss;
            DangerFactor = dangerFactor;
            TransitionWarpStrength = transitionWarpStrength;
            RewindStrength = rewindStrength;
            ScrollSpeed = scrollSpeed;
            CurrentStageNumber = currentStageNumber;
            TransitionTargetStageNumber = transitionTargetStageNumber;
            CurrentSectionIndex = currentSectionIndex;
            CurrentSectionProgress = currentSectionProgress;
        }

        public GameFlowState FlowState { get; }
        public bool HasBoss { get; }
        public bool TransitionToBoss { get; }
        public float DangerFactor { get; }
        public float TransitionWarpStrength { get; }
        public float RewindStrength { get; }
        public float ScrollSpeed { get; }
        public int CurrentStageNumber { get; }
        public int TransitionTargetStageNumber { get; }
        public int CurrentSectionIndex { get; }
        public float CurrentSectionProgress { get; }
    }

    sealed class AudioDirector : System.IDisposable
    {
        private readonly System.Collections.Generic.Dictionary<WeaponStyleId, GeneratedSoundBank> weaponBanks = new System.Collections.Generic.Dictionary<WeaponStyleId, GeneratedSoundBank>();
        private readonly System.Lazy<GeneratedSoundBank> explosionBank;
        private readonly System.Lazy<GeneratedSoundBank> impactBank;
        private readonly System.Lazy<GeneratedSoundBank> enemyShotBank;
        private readonly System.Lazy<GeneratedSoundBank> uiConfirmBank;
        private readonly System.Lazy<GeneratedSoundBank> uiCancelBank;
        private readonly System.Lazy<GeneratedSoundBank> pickupBank;
        private readonly System.Lazy<GeneratedSoundBank> upgradeBank;
        private readonly System.Lazy<GeneratedSoundBank> bossCueBank;
        private readonly System.Lazy<GeneratedSoundBank> transitionBank;
        private readonly System.Lazy<GeneratedSoundBank> playerDamageBank;
        private readonly System.Lazy<GeneratedSoundBank> rewindStartBank;
        private readonly MusicStemMixer musicMixer;
        private readonly int sampleRate;
        private readonly float rewindLoopSeconds;
        private SoundEffect rewindLoopEffect;
        private SoundEffectInstance rewindLoopInstance;

        private float masterVolume;
        private float musicVolume;
        private float sfxVolume;
        private float rewindAmount;
        private bool musicAvailable = true;

        public AudioDirector(AudioQualityPreset qualityPreset)
        {
#if BLAZORGL
            sampleRate = qualityPreset switch
            {
                AudioQualityPreset.Reduced => 11025,
                AudioQualityPreset.High => 22050,
                _ => 16000,
            };
            rewindLoopSeconds = qualityPreset == AudioQualityPreset.Reduced ? 0.8f : 1.2f;
#else
            sampleRate = qualityPreset switch
            {
                AudioQualityPreset.Reduced => 22050,
                AudioQualityPreset.High => 44100,
                _ => 32000,
            };
            rewindLoopSeconds = qualityPreset == AudioQualityPreset.Reduced ? 2.2f : 3f;
#endif

            // Synthesize banks on first use. Eagerly rendering every weapon and
            // effect made browser startup pay for sounds a run may never use.
            explosionBank = new System.Lazy<GeneratedSoundBank>(() => new GeneratedSoundBank(
                ProceduralAudioSynth.CreateEffect(sampleRate, 0.44f, ProceduralAudioSynth.ExplosionPatch(0.85f)),
                ProceduralAudioSynth.CreateEffect(sampleRate, 0.48f, ProceduralAudioSynth.ExplosionPatch(1f)),
                ProceduralAudioSynth.CreateEffect(sampleRate, 0.52f, ProceduralAudioSynth.ExplosionPatch(1.15f))));
            impactBank = new System.Lazy<GeneratedSoundBank>(() => new GeneratedSoundBank(
                ProceduralAudioSynth.CreateEffect(sampleRate, 0.12f, ProceduralAudioSynth.ImpactPatch()),
                ProceduralAudioSynth.CreateEffect(sampleRate, 0.14f, ProceduralAudioSynth.ImpactPatch(0.1f))));
            enemyShotBank = new System.Lazy<GeneratedSoundBank>(() => new GeneratedSoundBank(
                ProceduralAudioSynth.CreateEffect(sampleRate, 0.1f, ProceduralAudioSynth.EnemyShotPatch()),
                ProceduralAudioSynth.CreateEffect(sampleRate, 0.12f, ProceduralAudioSynth.EnemyShotPatch(0.08f))));
            uiConfirmBank = new System.Lazy<GeneratedSoundBank>(() => new GeneratedSoundBank(ProceduralAudioSynth.CreateEffect(sampleRate, 0.1f, ProceduralAudioSynth.UiPatch(true))));
            uiCancelBank = new System.Lazy<GeneratedSoundBank>(() => new GeneratedSoundBank(ProceduralAudioSynth.CreateEffect(sampleRate, 0.11f, ProceduralAudioSynth.UiPatch(false))));
            pickupBank = new System.Lazy<GeneratedSoundBank>(() => new GeneratedSoundBank(
                ProceduralAudioSynth.CreateEffect(sampleRate, 0.18f, ProceduralAudioSynth.PickupPatch(false)),
                ProceduralAudioSynth.CreateEffect(sampleRate, 0.2f, ProceduralAudioSynth.PickupPatch(true))));
            upgradeBank = new System.Lazy<GeneratedSoundBank>(() => new GeneratedSoundBank(
                ProceduralAudioSynth.CreateEffect(sampleRate, 0.32f, ProceduralAudioSynth.UpgradePatch()),
                ProceduralAudioSynth.CreateEffect(sampleRate, 0.35f, ProceduralAudioSynth.UpgradePatch(0.14f))));
            bossCueBank = new System.Lazy<GeneratedSoundBank>(() => new GeneratedSoundBank(ProceduralAudioSynth.CreateEffect(sampleRate, 0.5f, ProceduralAudioSynth.BossCuePatch())));
            transitionBank = new System.Lazy<GeneratedSoundBank>(() => new GeneratedSoundBank(ProceduralAudioSynth.CreateEffect(sampleRate, 0.46f, ProceduralAudioSynth.TransitionPatch())));
            playerDamageBank = new System.Lazy<GeneratedSoundBank>(() => new GeneratedSoundBank(ProceduralAudioSynth.CreateEffect(sampleRate, 0.24f, ProceduralAudioSynth.PlayerDamagePatch())));
            rewindStartBank = new System.Lazy<GeneratedSoundBank>(() => new GeneratedSoundBank(ProceduralAudioSynth.CreateEffect(sampleRate, 0.2f, ProceduralAudioSynth.RewindStartPatch())));

            musicMixer = new MusicStemMixer(qualityPreset);
        }

        public void Update(GameAudioState state, float masterVolume, float musicVolume, float sfxVolume, float deltaSeconds)
        {
            this.masterVolume = MathHelper.Clamp(masterVolume, 0f, 1f);
            this.musicVolume = MathHelper.Clamp(musicVolume, 0f, 1f);
            this.sfxVolume = MathHelper.Clamp(sfxVolume, 0f, 1f);

            if (musicAvailable)
            {
                try
                {
                    musicMixer.Update(state, this.masterVolume, this.musicVolume, deltaSeconds);
                }
                catch
                {
                    musicAvailable = false;
                }
            }

            if (rewindLoopInstance != null)
            {
                try
                {
                    float targetLoopVolume = this.masterVolume * this.sfxVolume * MathHelper.Clamp(rewindAmount * 0.4f, 0f, 0.4f);
                    rewindLoopInstance.Volume = MathHelper.Lerp(rewindLoopInstance.Volume, targetLoopVolume, MathHelper.Clamp(deltaSeconds * 7f, 0f, 1f));
                    if (rewindLoopInstance.State != SoundState.Playing && rewindLoopInstance.Volume > 0.005f)
                        rewindLoopInstance.Play();
                    else if (rewindLoopInstance.State == SoundState.Playing && rewindLoopInstance.Volume <= 0.005f && rewindAmount <= 0.001f)
                        rewindLoopInstance.Stop();
                }
                catch
                {
                    DisposeRewindLoop();
                }
            }
        }

        public void SetRewindAmount(float amount)
        {
            rewindAmount = MathHelper.Clamp(amount, 0f, 1f);
        }

        public void PlayPlayerShot(WeaponStyleId styleId, float intensity)
        {
            try
            {
                if (!weaponBanks.TryGetValue(styleId, out GeneratedSoundBank bank))
                {
                    bank = BuildWeaponBank(styleId, sampleRate);
                    weaponBanks[styleId] = bank;
                }

                bank.Play(masterVolume, sfxVolume, 0.1f + intensity * 0.12f, 0f, 0f);
            }
            catch
            {
            }
        }

        public void PlayEnemyShot(float intensity = 1f)
        {
            PlaySafely(enemyShotBank, 0.08f + intensity * 0.05f, -0.08f, -0.15f);
        }

        public void PlayEnemyImpact(float intensity, bool coreHit)
        {
            PlaySafely(impactBank, coreHit ? 0.22f : 0.16f + intensity * 0.04f, coreHit ? 0.08f : -0.04f, 0f);
        }

        public void PlayExplosion(float intensity, bool heavy)
        {
            PlaySafely(explosionBank, heavy ? 0.5f : 0.34f + intensity * 0.08f, heavy ? -0.1f : -0.18f, 0f);
        }

        public void PlayPickup(WeaponStyleId styleId, bool immediate)
        {
            PlaySafely(pickupBank, immediate ? 0.34f : 0.22f, immediate ? 0.14f : 0.06f, 0f);
        }

        public void PlayUpgrade(WeaponStyleId styleId)
        {
            PlaySafely(upgradeBank, 0.34f, 0.12f, 0f);
        }

        public void PlayPlayerDamaged()
        {
            PlaySafely(playerDamageBank, 0.28f, -0.08f, 0f);
        }

        public void PlayBossCue()
        {
            PlaySafely(bossCueBank, 0.36f, -0.04f, 0f);
        }

        public void PlayTransitionWhoosh()
        {
            PlaySafely(transitionBank, 0.28f, 0f, 0f);
        }

        public void PlayUiConfirm()
        {
            PlaySafely(uiConfirmBank, 0.18f);
        }

        public void PlayUiCancel()
        {
            PlaySafely(uiCancelBank, 0.16f);
        }

        public void StartRewindLoop()
        {
            PlaySafely(rewindStartBank, 0.2f, 0.08f, 0f);
            try
            {
                EnsureRewindLoop();
                if (rewindLoopInstance.State != SoundState.Playing)
                    rewindLoopInstance.Play();
            }
            catch
            {
                DisposeRewindLoop();
            }
        }

        public void StopRewindLoop()
        {
            rewindAmount = 0f;
        }

        public void Dispose()
        {
            foreach (GeneratedSoundBank bank in weaponBanks.Values)
                bank.Dispose();

            weaponBanks.Clear();
            DisposeIfCreated(explosionBank);
            DisposeIfCreated(impactBank);
            DisposeIfCreated(enemyShotBank);
            DisposeIfCreated(uiConfirmBank);
            DisposeIfCreated(uiCancelBank);
            DisposeIfCreated(pickupBank);
            DisposeIfCreated(upgradeBank);
            DisposeIfCreated(bossCueBank);
            DisposeIfCreated(transitionBank);
            DisposeIfCreated(playerDamageBank);
            DisposeIfCreated(rewindStartBank);
            DisposeRewindLoop();
            musicMixer.Dispose();
        }

        private void PlaySafely(System.Lazy<GeneratedSoundBank> bank, float volume, float pitch = 0f, float pan = 0f)
        {
            try
            {
                bank.Value.Play(masterVolume, sfxVolume, volume, pitch, pan);
            }
            catch
            {
            }
        }

        private void EnsureRewindLoop()
        {
            if (rewindLoopInstance != null)
                return;

            SoundEffect effect = ProceduralAudioSynth.CreateEffect(sampleRate, rewindLoopSeconds, ProceduralAudioSynth.RewindLoopPatch());
            try
            {
                SoundEffectInstance instance = effect.CreateInstance();
                instance.IsLooped = true;
                instance.Volume = 0f;
                rewindLoopEffect = effect;
                rewindLoopInstance = instance;
            }
            catch
            {
                effect.Dispose();
                throw;
            }
        }

        private void DisposeRewindLoop()
        {
            rewindLoopInstance?.Dispose();
            rewindLoopEffect?.Dispose();
            rewindLoopInstance = null;
            rewindLoopEffect = null;
        }

        private static void DisposeIfCreated(System.Lazy<GeneratedSoundBank> bank)
        {
            if (bank?.IsValueCreated == true)
                bank.Value.Dispose();
        }

        private static GeneratedSoundBank BuildWeaponBank(WeaponStyleId styleId, int sampleRate)
        {
            switch (styleId)
            {
                case WeaponStyleId.Spread:
                    return new GeneratedSoundBank(
                        ProceduralAudioSynth.CreateEffect(sampleRate, 0.12f, ProceduralAudioSynth.SpreadShotPatch()),
                        ProceduralAudioSynth.CreateEffect(sampleRate, 0.13f, ProceduralAudioSynth.SpreadShotPatch(0.08f)));
                case WeaponStyleId.Laser:
                    return new GeneratedSoundBank(
                        ProceduralAudioSynth.CreateEffect(sampleRate, 0.16f, ProceduralAudioSynth.LaserShotPatch()),
                        ProceduralAudioSynth.CreateEffect(sampleRate, 0.18f, ProceduralAudioSynth.LaserShotPatch(0.06f)));
                case WeaponStyleId.Plasma:
                    return new GeneratedSoundBank(
                        ProceduralAudioSynth.CreateEffect(sampleRate, 0.16f, ProceduralAudioSynth.PlasmaShotPatch()),
                        ProceduralAudioSynth.CreateEffect(sampleRate, 0.18f, ProceduralAudioSynth.PlasmaShotPatch(0.08f)));
                case WeaponStyleId.Missile:
                    return new GeneratedSoundBank(
                        ProceduralAudioSynth.CreateEffect(sampleRate, 0.18f, ProceduralAudioSynth.MissileShotPatch()),
                        ProceduralAudioSynth.CreateEffect(sampleRate, 0.2f, ProceduralAudioSynth.MissileShotPatch(0.1f)));
                case WeaponStyleId.Rail:
                    return new GeneratedSoundBank(
                        ProceduralAudioSynth.CreateEffect(sampleRate, 0.14f, ProceduralAudioSynth.RailShotPatch()),
                        ProceduralAudioSynth.CreateEffect(sampleRate, 0.16f, ProceduralAudioSynth.RailShotPatch(0.12f)));
                case WeaponStyleId.Arc:
                    return new GeneratedSoundBank(
                        ProceduralAudioSynth.CreateEffect(sampleRate, 0.12f, ProceduralAudioSynth.ArcShotPatch()),
                        ProceduralAudioSynth.CreateEffect(sampleRate, 0.14f, ProceduralAudioSynth.ArcShotPatch(0.1f)));
                case WeaponStyleId.Blade:
                    return new GeneratedSoundBank(
                        ProceduralAudioSynth.CreateEffect(sampleRate, 0.12f, ProceduralAudioSynth.BladeShotPatch()),
                        ProceduralAudioSynth.CreateEffect(sampleRate, 0.15f, ProceduralAudioSynth.BladeShotPatch(0.14f)));
                case WeaponStyleId.Drone:
                    return new GeneratedSoundBank(
                        ProceduralAudioSynth.CreateEffect(sampleRate, 0.1f, ProceduralAudioSynth.DroneShotPatch()),
                        ProceduralAudioSynth.CreateEffect(sampleRate, 0.11f, ProceduralAudioSynth.DroneShotPatch(0.1f)));
                case WeaponStyleId.Fortress:
                    return new GeneratedSoundBank(
                        ProceduralAudioSynth.CreateEffect(sampleRate, 0.16f, ProceduralAudioSynth.FortressShotPatch()),
                        ProceduralAudioSynth.CreateEffect(sampleRate, 0.18f, ProceduralAudioSynth.FortressShotPatch(0.08f)));
                default:
                    return new GeneratedSoundBank(
                        ProceduralAudioSynth.CreateEffect(sampleRate, 0.11f, ProceduralAudioSynth.PulseShotPatch()),
                        ProceduralAudioSynth.CreateEffect(sampleRate, 0.12f, ProceduralAudioSynth.PulseShotPatch(0.08f)));
            }
        }
    }
}
