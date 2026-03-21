using System;
using Microsoft.Xna.Framework;

namespace SpaceBurst
{
    enum GameDifficulty
    {
        Easy,
        Normal,
        Hard,
        Insane,
        Realistic,
    }

    readonly struct DifficultyProfile
    {
        public DifficultyProfile(
            int livesDelta,
            int shipsDelta,
            float waveDamageMultiplier,
            float waveDurabilityMultiplier,
            float waveFireIntervalScale,
            float bossDamageMultiplier,
            float bossDurabilityMultiplier,
            float bossFireIntervalScale,
            float dropChanceMultiplier,
            float dropWeightMultiplier,
            float wavePressureFloor,
            float bossPressureFloor,
            float earlyBossPressureFloor,
            float stagePressureScale,
            float powerPressureScale,
            float bossPowerPressureScale,
            float bossProjectileSpeedMultiplier,
            int bossExtraFanShots,
            int bossSupportCountBonus,
            float bossMinimumFireCooldown,
            bool oneHitKill)
        {
            LivesDelta = livesDelta;
            ShipsDelta = shipsDelta;
            WaveDamageMultiplier = waveDamageMultiplier;
            WaveDurabilityMultiplier = waveDurabilityMultiplier;
            WaveFireIntervalScale = waveFireIntervalScale;
            BossDamageMultiplier = bossDamageMultiplier;
            BossDurabilityMultiplier = bossDurabilityMultiplier;
            BossFireIntervalScale = bossFireIntervalScale;
            DropChanceMultiplier = dropChanceMultiplier;
            DropWeightMultiplier = dropWeightMultiplier;
            WavePressureFloor = wavePressureFloor;
            BossPressureFloor = bossPressureFloor;
            EarlyBossPressureFloor = earlyBossPressureFloor;
            StagePressureScale = stagePressureScale;
            PowerPressureScale = powerPressureScale;
            BossPowerPressureScale = bossPowerPressureScale;
            BossProjectileSpeedMultiplier = bossProjectileSpeedMultiplier;
            BossExtraFanShots = bossExtraFanShots;
            BossSupportCountBonus = bossSupportCountBonus;
            BossMinimumFireCooldown = bossMinimumFireCooldown;
            OneHitKill = oneHitKill;
        }

        public int LivesDelta { get; }
        public int ShipsDelta { get; }
        public float WaveDamageMultiplier { get; }
        public float WaveDurabilityMultiplier { get; }
        public float WaveFireIntervalScale { get; }
        public float BossDamageMultiplier { get; }
        public float BossDurabilityMultiplier { get; }
        public float BossFireIntervalScale { get; }
        public float DropChanceMultiplier { get; }
        public float DropWeightMultiplier { get; }
        public float WavePressureFloor { get; }
        public float BossPressureFloor { get; }
        public float EarlyBossPressureFloor { get; }
        public float StagePressureScale { get; }
        public float PowerPressureScale { get; }
        public float BossPowerPressureScale { get; }
        public float BossProjectileSpeedMultiplier { get; }
        public int BossExtraFanShots { get; }
        public int BossSupportCountBonus { get; }
        public float BossMinimumFireCooldown { get; }
        public bool OneHitKill { get; }
    }

    static class DifficultyTuning
    {
        public static DifficultyProfile GetProfile(GameDifficulty difficulty)
        {
            return difficulty switch
            {
                GameDifficulty.Normal => new DifficultyProfile(
                    livesDelta: -1,
                    shipsDelta: 0,
                    waveDamageMultiplier: 1.08f,
                    waveDurabilityMultiplier: 1.08f,
                    waveFireIntervalScale: 0.95f,
                    bossDamageMultiplier: 1.16f,
                    bossDurabilityMultiplier: 1.2f,
                    bossFireIntervalScale: 0.9f,
                    dropChanceMultiplier: 0.92f,
                    dropWeightMultiplier: 0.94f,
                    wavePressureFloor: 0.1f,
                    bossPressureFloor: 0.28f,
                    earlyBossPressureFloor: 0.42f,
                    stagePressureScale: 1.05f,
                    powerPressureScale: 1.08f,
                    bossPowerPressureScale: 0.92f,
                    bossProjectileSpeedMultiplier: 1.04f,
                    bossExtraFanShots: 0,
                    bossSupportCountBonus: 0,
                    bossMinimumFireCooldown: 0.22f,
                    oneHitKill: false),
                GameDifficulty.Hard => new DifficultyProfile(
                    livesDelta: -1,
                    shipsDelta: -1,
                    waveDamageMultiplier: 1.2f,
                    waveDurabilityMultiplier: 1.18f,
                    waveFireIntervalScale: 0.88f,
                    bossDamageMultiplier: 1.42f,
                    bossDurabilityMultiplier: 1.56f,
                    bossFireIntervalScale: 0.76f,
                    dropChanceMultiplier: 0.78f,
                    dropWeightMultiplier: 0.82f,
                    wavePressureFloor: 0.18f,
                    bossPressureFloor: 0.42f,
                    earlyBossPressureFloor: 0.86f,
                    stagePressureScale: 1.1f,
                    powerPressureScale: 1.14f,
                    bossPowerPressureScale: 0.96f,
                    bossProjectileSpeedMultiplier: 1.18f,
                    bossExtraFanShots: 2,
                    bossSupportCountBonus: 1,
                    bossMinimumFireCooldown: 0.18f,
                    oneHitKill: false),
                GameDifficulty.Insane => new DifficultyProfile(
                    livesDelta: -2,
                    shipsDelta: -1,
                    waveDamageMultiplier: 1.4f,
                    waveDurabilityMultiplier: 1.34f,
                    waveFireIntervalScale: 0.76f,
                    bossDamageMultiplier: 1.72f,
                    bossDurabilityMultiplier: 1.92f,
                    bossFireIntervalScale: 0.62f,
                    dropChanceMultiplier: 0.58f,
                    dropWeightMultiplier: 0.62f,
                    wavePressureFloor: 0.3f,
                    bossPressureFloor: 0.6f,
                    earlyBossPressureFloor: 1.08f,
                    stagePressureScale: 1.2f,
                    powerPressureScale: 1.2f,
                    bossPowerPressureScale: 1f,
                    bossProjectileSpeedMultiplier: 1.32f,
                    bossExtraFanShots: 4,
                    bossSupportCountBonus: 2,
                    bossMinimumFireCooldown: 0.14f,
                    oneHitKill: false),
                GameDifficulty.Realistic => new DifficultyProfile(
                    livesDelta: -2,
                    shipsDelta: -1,
                    waveDamageMultiplier: 1.9f,
                    waveDurabilityMultiplier: 1.5f,
                    waveFireIntervalScale: 0.68f,
                    bossDamageMultiplier: 2.3f,
                    bossDurabilityMultiplier: 2.1f,
                    bossFireIntervalScale: 0.52f,
                    dropChanceMultiplier: 0.42f,
                    dropWeightMultiplier: 0.5f,
                    wavePressureFloor: 0.38f,
                    bossPressureFloor: 0.78f,
                    earlyBossPressureFloor: 1.28f,
                    stagePressureScale: 1.28f,
                    powerPressureScale: 1.24f,
                    bossPowerPressureScale: 1.04f,
                    bossProjectileSpeedMultiplier: 1.46f,
                    bossExtraFanShots: 4,
                    bossSupportCountBonus: 3,
                    bossMinimumFireCooldown: 0.1f,
                    oneHitKill: true),
                _ => new DifficultyProfile(
                    livesDelta: 0,
                    shipsDelta: 0,
                    waveDamageMultiplier: 1f,
                    waveDurabilityMultiplier: 1f,
                    waveFireIntervalScale: 1f,
                    bossDamageMultiplier: 1.06f,
                    bossDurabilityMultiplier: 1.12f,
                    bossFireIntervalScale: 0.96f,
                    dropChanceMultiplier: 1f,
                    dropWeightMultiplier: 1f,
                    wavePressureFloor: 0f,
                    bossPressureFloor: 0.15f,
                    earlyBossPressureFloor: 0.22f,
                    stagePressureScale: 1f,
                    powerPressureScale: 1f,
                    bossPowerPressureScale: 0.88f,
                    bossProjectileSpeedMultiplier: 1f,
                    bossExtraFanShots: 0,
                    bossSupportCountBonus: 0,
                    bossMinimumFireCooldown: 0.25f,
                    oneHitKill: false),
            };
        }

        public static string GetLabel(GameDifficulty difficulty)
        {
            return difficulty.ToString().ToUpperInvariant();
        }

        public static float GetStagePressure(int stageNumber)
        {
            return MathHelper.Clamp((stageNumber - 1) / 49f, 0f, 1f);
        }

        public static float GetPowerPressure(float powerBudget)
        {
            return MathHelper.Clamp(powerBudget * 0.055f, 0f, 0.65f);
        }
    }
}
