using SpaceBurst.RuntimeData;
using Xunit;

namespace SpaceBurst.Tests
{
    public sealed class DifficultyTuningTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(25)]
        [InlineData(50)]
        public void ThreatAndScarcityIncreaseMonotonicallyAcrossDifficulties(int stageNumber)
        {
            GameDifficulty[] difficulties =
            {
                GameDifficulty.Easy,
                GameDifficulty.Normal,
                GameDifficulty.Hard,
                GameDifficulty.Insane,
                GameDifficulty.Realistic,
            };

            float previousDamage = 0f;
            float previousDurability = 0f;
            float previousFireIntervalScale = float.MaxValue;
            float previousDropChance = float.MaxValue;

            foreach (GameDifficulty difficulty in difficulties)
            {
                PlayerRunProgress progress = CreateProgress(difficulty);
                float damage = progress.GetEnemyDamageMultiplier(stageNumber, false);
                float durability = progress.GetEnemyDurabilityMultiplier(stageNumber, false);
                float fireIntervalScale = progress.GetEnemyFireIntervalScale(stageNumber, false);
                float dropChance = progress.GetDropChanceMultiplier(stageNumber, false);

                Assert.True(damage >= previousDamage);
                Assert.True(durability >= previousDurability);
                Assert.True(fireIntervalScale <= previousFireIntervalScale);
                Assert.True(dropChance <= previousDropChance);

                previousDamage = damage;
                previousDurability = durability;
                previousFireIntervalScale = fireIntervalScale;
                previousDropChance = dropChance;
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void CampaignPressureNeverRegressesBetweenStages(int difficultyValue)
        {
            GameDifficulty difficulty = (GameDifficulty)difficultyValue;
            PlayerRunProgress progress = CreateProgress(difficulty);
            float previousWavePressure = -1f;
            float previousBossPressure = -1f;

            for (int stageNumber = 1; stageNumber <= 50; stageNumber++)
            {
                float wavePressure = progress.GetWavePressure(stageNumber);
                float bossPressure = progress.GetBossPressure(stageNumber);
                Assert.True(wavePressure >= previousWavePressure);
                Assert.True(bossPressure >= previousBossPressure);
                previousWavePressure = wavePressure;
                previousBossPressure = bossPressure;
            }
        }

        private static PlayerRunProgress CreateProgress(GameDifficulty difficulty)
        {
            var progress = new PlayerRunProgress();
            progress.BeginCampaign(new StageDefinition
            {
                StartingLives = 3,
                ShipsPerLife = 2,
            }, difficulty);
            return progress;
        }
    }
}
