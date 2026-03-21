using SpaceBurst.RuntimeData;
using System;

namespace SpaceBurst
{
    enum PlayerDeathOutcome
    {
        RespawnInPlace,
        RestartStage,
        GameOver,
    }

    static class PlayerStatus
    {
        private const float multiplierExpiryTime = 1.2f;
        private const int maxMultiplier = 20;
        private const int extraLifeScoreStep = 3000;

        public static int Lives { get; private set; }
        public static int Ships { get; private set; }
        public static int Score { get; private set; }
        public static int HighScore { get; private set; }
        public static int Multiplier { get; private set; }
        public static bool IsGameOver { get { return Lives <= 0; } }
        public static PlayerRunProgress RunProgress { get; } = new PlayerRunProgress();

        private static float multiplierTimeLeft;
        private static int scoreForExtraLife;

        static PlayerStatus()
        {
            HighScore = PersistentStorage.LoadHighScore();
            BeginCampaign(null, GameDifficulty.Easy);
        }

        public static void BeginCampaign(StageDefinition openingStage, GameDifficulty difficulty)
        {
            RunProgress.BeginCampaign(openingStage, difficulty);
            Score = 0;
            Multiplier = 1;
            Lives = RunProgress.StartingLives;
            Ships = RunProgress.ShipsPerLife;
            scoreForExtraLife = extraLifeScoreStep;
            multiplierTimeLeft = 0f;
        }

        public static void PrepareStage(StageDefinition stage, bool resetLives)
        {
            RunProgress.ApplyStageDefaults(stage);
            if (resetLives)
                Lives = RunProgress.StartingLives;

            Ships = RunProgress.ShipsPerLife;
        }

        public static void Update()
        {
            if (Multiplier > 1)
            {
                if ((multiplierTimeLeft -= (float)Game1.GameTime.ElapsedGameTime.TotalSeconds) <= 0f)
                {
                    multiplierTimeLeft = multiplierExpiryTime;
                    ResetMultiplier();
                }
            }
        }

        public static void AddPoints(int basePoints)
        {
            if (Player1.Instance.IsDead)
                return;

            Score += basePoints * Multiplier;
            while (Score >= scoreForExtraLife)
            {
                scoreForExtraLife += extraLifeScoreStep;
                Lives++;
            }
        }

        public static void IncreaseMultiplier()
        {
            if (Player1.Instance.IsDead)
                return;

            multiplierTimeLeft = multiplierExpiryTime;
            if (Multiplier < maxMultiplier)
                Multiplier++;
        }

        public static void ResetMultiplier()
        {
            Multiplier = 1;
        }

        public static void GrantShips(int count)
        {
            if (count > 0)
                Ships += count;
        }

        public static void GrantLife(int count = 1)
        {
            if (count > 0)
                Lives += count;
        }

        public static PlayerDeathOutcome ConsumeDeath(StageDefinition stage)
        {
            RunProgress.Weapons.ApplyDeathPenalty();

            if (Ships > 0)
            {
                Ships--;
                return PlayerDeathOutcome.RespawnInPlace;
            }

            Lives--;
            if (Lives <= 0)
                return PlayerDeathOutcome.GameOver;

            Ships = RunProgress.ShipsPerLife;
            return PlayerDeathOutcome.RestartStage;
        }

        public static void FinalizeRun()
        {
            if (Score > HighScore)
                PersistentStorage.SaveHighScore(HighScore = Score);
        }

        public static PlayerStatusSnapshotData CaptureSnapshot()
        {
            return new PlayerStatusSnapshotData
            {
                Lives = Lives,
                Ships = Ships,
                Score = Score,
                Multiplier = Multiplier,
                MultiplierTimeLeft = multiplierTimeLeft,
                ScoreForExtraLife = scoreForExtraLife,
                RunProgress = RunProgress.CaptureSnapshot(),
            };
        }

        public static void RestoreSnapshot(PlayerStatusSnapshotData snapshot)
        {
            if (snapshot == null)
                return;

            Lives = snapshot.Lives;
            Ships = snapshot.Ships;
            Score = snapshot.Score;
            Multiplier = snapshot.Multiplier;
            multiplierTimeLeft = snapshot.MultiplierTimeLeft;
            scoreForExtraLife = snapshot.ScoreForExtraLife > 0 ? snapshot.ScoreForExtraLife : extraLifeScoreStep;
            RunProgress.RestoreSnapshot(snapshot.RunProgress);
        }
    }
}
