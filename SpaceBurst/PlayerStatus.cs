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
        public const int MaximumLives = 9;
        public const int MaximumShips = 9;

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
            Lives = Math.Min(MaximumLives, RunProgress.StartingLives);
            Ships = Math.Min(MaximumShips, RunProgress.ShipsPerLife);
            scoreForExtraLife = extraLifeScoreStep;
            multiplierTimeLeft = 0f;
        }

        public static void PrepareStage(StageDefinition stage, bool resetLives)
        {
            RunProgress.ApplyStageDefaults(stage);
            if (resetLives)
                Lives = Math.Min(MaximumLives, RunProgress.StartingLives);

            Ships = Math.Min(MaximumShips, RunProgress.ShipsPerLife);
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

            RunProgress.UpdateKillChain(Multiplier);
        }

        public static void AddPoints(int basePoints)
        {
            if (Player1.Instance.IsDead)
                return;

            if (basePoints <= 0)
                return;

            int previousScore = Score;
            Score = (int)Math.Min(int.MaxValue, (long)Score + (long)basePoints * Multiplier);
            if (previousScore < scoreForExtraLife && Score >= scoreForExtraLife)
            {
                int awards = (Score - scoreForExtraLife) / extraLifeScoreStep + 1;
                GrantLife(awards);
                // Advance even at full stock, so spent lives cannot redeem old score again.
                scoreForExtraLife = (int)Math.Min(int.MaxValue, (long)scoreForExtraLife + (long)awards * extraLifeScoreStep);
            }
        }

        public static void IncreaseMultiplier()
        {
            if (Player1.Instance.IsDead)
                return;

            multiplierTimeLeft = multiplierExpiryTime;
            if (Multiplier < maxMultiplier)
                Multiplier++;
            RunProgress.UpdateKillChain(Multiplier);
        }

        public static void ResetMultiplier()
        {
            Multiplier = 1;
            RunProgress.UpdateKillChain(Multiplier);
        }

        public static void GrantShips(int count)
        {
            if (count > 0)
                Ships = (int)Math.Min(MaximumShips, (long)Ships + count);
        }

        public static void GrantLife(int count = 1)
        {
            if (count > 0)
                Lives = (int)Math.Min(MaximumLives, (long)Lives + count);
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

            Ships = Math.Min(MaximumShips, RunProgress.ShipsPerLife);
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

            Lives = Math.Clamp(snapshot.Lives, 0, MaximumLives);
            Ships = Math.Clamp(snapshot.Ships, 0, MaximumShips);
            Score = Math.Max(0, snapshot.Score);
            Multiplier = Math.Clamp(snapshot.Multiplier, 1, maxMultiplier);
            multiplierTimeLeft = snapshot.MultiplierTimeLeft;
            int nextThreshold = (int)Math.Min(int.MaxValue, ((long)Score / extraLifeScoreStep + 1) * extraLifeScoreStep);
            scoreForExtraLife = snapshot.ScoreForExtraLife > Score ? snapshot.ScoreForExtraLife : nextThreshold;
            RunProgress.RestoreSnapshot(snapshot.RunProgress);
        }
    }
}
