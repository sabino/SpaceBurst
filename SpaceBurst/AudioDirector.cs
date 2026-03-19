using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using SpaceBurst.RuntimeData;

namespace SpaceBurst
{
    readonly struct GameAudioState
    {
        public GameAudioState(GameFlowState flowState, bool hasBoss, bool transitionToBoss, float dangerFactor, float transitionWarpStrength, float rewindStrength, float scrollSpeed)
        {
            FlowState = flowState;
            HasBoss = hasBoss;
            TransitionToBoss = transitionToBoss;
            DangerFactor = dangerFactor;
            TransitionWarpStrength = transitionWarpStrength;
            RewindStrength = rewindStrength;
            ScrollSpeed = scrollSpeed;
        }

        public GameFlowState FlowState { get; }
        public bool HasBoss { get; }
        public bool TransitionToBoss { get; }
        public float DangerFactor { get; }
        public float TransitionWarpStrength { get; }
        public float RewindStrength { get; }
        public float ScrollSpeed { get; }
    }

    sealed class AudioDirector : System.IDisposable
    {
        private readonly System.Collections.Generic.Dictionary<WeaponStyleId, GeneratedSoundBank> weaponBanks = new System.Collections.Generic.Dictionary<WeaponStyleId, GeneratedSoundBank>();
        private readonly GeneratedSoundBank explosionBank;
        private readonly GeneratedSoundBank impactBank;
        private readonly GeneratedSoundBank enemyShotBank;
        private readonly GeneratedSoundBank uiConfirmBank;
        private readonly GeneratedSoundBank uiCancelBank;
        private readonly GeneratedSoundBank pickupBank;
        private readonly GeneratedSoundBank upgradeBank;
        private readonly GeneratedSoundBank bossCueBank;
        private readonly GeneratedSoundBank transitionBank;
        private readonly GeneratedSoundBank playerDamageBank;
        private readonly GeneratedSoundBank rewindStartBank;
        private readonly MusicStemMixer musicMixer;
        private readonly SoundEffect rewindLoopEffect;
        private readonly SoundEffectInstance rewindLoopInstance;

        private float masterVolume;
        private float musicVolume;
        private float sfxVolume;
        private float rewindAmount;

        public AudioDirector(AudioQualityPreset qualityPreset)
        {
            int sampleRate = qualityPreset switch
            {
                AudioQualityPreset.Reduced => 22050,
                AudioQualityPreset.High => 44100,
                _ => 32000,
            };

            explosionBank = new GeneratedSoundBank(
                ProceduralAudioSynth.CreateEffect(sampleRate, 0.44f, ProceduralAudioSynth.ExplosionPatch(0.85f)),
                ProceduralAudioSynth.CreateEffect(sampleRate, 0.48f, ProceduralAudioSynth.ExplosionPatch(1f)),
                ProceduralAudioSynth.CreateEffect(sampleRate, 0.52f, ProceduralAudioSynth.ExplosionPatch(1.15f)));
            impactBank = new GeneratedSoundBank(
                ProceduralAudioSynth.CreateEffect(sampleRate, 0.12f, ProceduralAudioSynth.ImpactPatch()),
                ProceduralAudioSynth.CreateEffect(sampleRate, 0.14f, ProceduralAudioSynth.ImpactPatch(0.1f)));
            enemyShotBank = new GeneratedSoundBank(
                ProceduralAudioSynth.CreateEffect(sampleRate, 0.1f, ProceduralAudioSynth.EnemyShotPatch()),
                ProceduralAudioSynth.CreateEffect(sampleRate, 0.12f, ProceduralAudioSynth.EnemyShotPatch(0.08f)));
            uiConfirmBank = new GeneratedSoundBank(ProceduralAudioSynth.CreateEffect(sampleRate, 0.1f, ProceduralAudioSynth.UiPatch(true)));
            uiCancelBank = new GeneratedSoundBank(ProceduralAudioSynth.CreateEffect(sampleRate, 0.11f, ProceduralAudioSynth.UiPatch(false)));
            pickupBank = new GeneratedSoundBank(
                ProceduralAudioSynth.CreateEffect(sampleRate, 0.18f, ProceduralAudioSynth.PickupPatch(false)),
                ProceduralAudioSynth.CreateEffect(sampleRate, 0.2f, ProceduralAudioSynth.PickupPatch(true)));
            upgradeBank = new GeneratedSoundBank(
                ProceduralAudioSynth.CreateEffect(sampleRate, 0.32f, ProceduralAudioSynth.UpgradePatch()),
                ProceduralAudioSynth.CreateEffect(sampleRate, 0.35f, ProceduralAudioSynth.UpgradePatch(0.14f)));
            bossCueBank = new GeneratedSoundBank(ProceduralAudioSynth.CreateEffect(sampleRate, 0.5f, ProceduralAudioSynth.BossCuePatch()));
            transitionBank = new GeneratedSoundBank(ProceduralAudioSynth.CreateEffect(sampleRate, 0.46f, ProceduralAudioSynth.TransitionPatch()));
            playerDamageBank = new GeneratedSoundBank(ProceduralAudioSynth.CreateEffect(sampleRate, 0.24f, ProceduralAudioSynth.PlayerDamagePatch()));
            rewindStartBank = new GeneratedSoundBank(ProceduralAudioSynth.CreateEffect(sampleRate, 0.2f, ProceduralAudioSynth.RewindStartPatch()));

            foreach (WeaponStyleId styleId in WeaponCatalog.StyleOrder)
                weaponBanks[styleId] = BuildWeaponBank(styleId, sampleRate);

            musicMixer = new MusicStemMixer(qualityPreset);
            rewindLoopEffect = ProceduralAudioSynth.CreateEffect(sampleRate, qualityPreset == AudioQualityPreset.Reduced ? 2.2f : 3f, ProceduralAudioSynth.RewindLoopPatch());
            rewindLoopInstance = rewindLoopEffect.CreateInstance();
            rewindLoopInstance.IsLooped = true;
            rewindLoopInstance.Volume = 0f;
        }

        public void Update(GameAudioState state, float masterVolume, float musicVolume, float sfxVolume, float deltaSeconds)
        {
            this.masterVolume = MathHelper.Clamp(masterVolume, 0f, 1f);
            this.musicVolume = MathHelper.Clamp(musicVolume, 0f, 1f);
            this.sfxVolume = MathHelper.Clamp(sfxVolume, 0f, 1f);

            musicMixer.Update(state, this.masterVolume, this.musicVolume, deltaSeconds);

            float targetLoopVolume = this.masterVolume * this.sfxVolume * MathHelper.Clamp(rewindAmount * 0.4f, 0f, 0.4f);
            rewindLoopInstance.Volume = MathHelper.Lerp(rewindLoopInstance.Volume, targetLoopVolume, MathHelper.Clamp(deltaSeconds * 7f, 0f, 1f));
            if (rewindLoopInstance.State != SoundState.Playing && rewindLoopInstance.Volume > 0.005f)
                rewindLoopInstance.Play();
            else if (rewindLoopInstance.State == SoundState.Playing && rewindLoopInstance.Volume <= 0.005f && rewindAmount <= 0.001f)
                rewindLoopInstance.Stop();
        }

        public void SetRewindAmount(float amount)
        {
            rewindAmount = MathHelper.Clamp(amount, 0f, 1f);
        }

        public void PlayPlayerShot(WeaponStyleId styleId, float intensity)
        {
            if (weaponBanks.TryGetValue(styleId, out GeneratedSoundBank bank))
                bank.Play(masterVolume, sfxVolume, 0.1f + intensity * 0.12f, 0f, 0f);
        }

        public void PlayEnemyShot(float intensity = 1f)
        {
            enemyShotBank.Play(masterVolume, sfxVolume, 0.08f + intensity * 0.05f, -0.08f, -0.15f);
        }

        public void PlayEnemyImpact(float intensity, bool coreHit)
        {
            impactBank.Play(masterVolume, sfxVolume, coreHit ? 0.22f : 0.16f + intensity * 0.04f, coreHit ? 0.08f : -0.04f, 0f);
        }

        public void PlayExplosion(float intensity, bool heavy)
        {
            explosionBank.Play(masterVolume, sfxVolume, heavy ? 0.5f : 0.34f + intensity * 0.08f, heavy ? -0.1f : -0.18f, 0f);
        }

        public void PlayPickup(WeaponStyleId styleId, bool immediate)
        {
            pickupBank.Play(masterVolume, sfxVolume, immediate ? 0.34f : 0.22f, immediate ? 0.14f : 0.06f, 0f);
        }

        public void PlayUpgrade(WeaponStyleId styleId)
        {
            upgradeBank.Play(masterVolume, sfxVolume, 0.34f, 0.12f, 0f);
        }

        public void PlayPlayerDamaged()
        {
            playerDamageBank.Play(masterVolume, sfxVolume, 0.28f, -0.08f, 0f);
        }

        public void PlayBossCue()
        {
            bossCueBank.Play(masterVolume, sfxVolume, 0.36f, -0.04f, 0f);
        }

        public void PlayTransitionWhoosh()
        {
            transitionBank.Play(masterVolume, sfxVolume, 0.28f, 0f, 0f);
        }

        public void PlayUiConfirm()
        {
            uiConfirmBank.Play(masterVolume, sfxVolume, 0.18f);
        }

        public void PlayUiCancel()
        {
            uiCancelBank.Play(masterVolume, sfxVolume, 0.16f);
        }

        public void StartRewindLoop()
        {
            rewindStartBank.Play(masterVolume, sfxVolume, 0.2f, 0.08f, 0f);
            if (rewindLoopInstance.State != SoundState.Playing)
                rewindLoopInstance.Play();
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
            explosionBank.Dispose();
            impactBank.Dispose();
            enemyShotBank.Dispose();
            uiConfirmBank.Dispose();
            uiCancelBank.Dispose();
            pickupBank.Dispose();
            upgradeBank.Dispose();
            bossCueBank.Dispose();
            transitionBank.Dispose();
            playerDamageBank.Dispose();
            rewindStartBank.Dispose();
            rewindLoopInstance?.Dispose();
            rewindLoopEffect?.Dispose();
            musicMixer.Dispose();
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
