using System;
using System.IO;

namespace SpaceBurst
{
    static class PlayerStatus
    {
        private const float multiplierExpiryTime = 1.2f;
        private const int maxMultiplier = 20;
        private const int campaignStartingLives = 4;
        private const int extraLifeScoreStep = 3000;

        public static int Lives { get; private set; }
        public static int Score { get; private set; }
        public static int HighScore { get; private set; }
        public static int Multiplier { get; private set; }
        public static bool IsGameOver { get { return Lives <= 0; } }

        private static float multiplierTimeLeft;    // time until the current multiplier expires
        private static int scoreForExtraLife;       // score required to gain an extra life

        private static readonly string highScoreFilename = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpaceBurst",
            "highscore.txt");

        // Static constructor
        static PlayerStatus()
        {
            HighScore = LoadHighScore();
            BeginCampaign();
        }

        public static void BeginCampaign()
        {
            Score = 0;
            Multiplier = 1;
            Lives = campaignStartingLives;
            scoreForExtraLife = extraLifeScoreStep;
            multiplierTimeLeft = 0;
        }

        public static void Update()
        {
            if (Multiplier > 1)
            {
                // update the multiplier timer
                if ((multiplierTimeLeft -= (float)Game1.GameTime.ElapsedGameTime.TotalSeconds) <= 0)
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

        public static void RemoveLife()
        {
            Lives--;
        }

        public static void FinalizeRun()
        {
            if (Score > HighScore)
                SaveHighScore(HighScore = Score);
        }

        private static int LoadHighScore()
        {
            // return the saved high score if possible and return 0 otherwise
            int score;
            return File.Exists(highScoreFilename) && int.TryParse(File.ReadAllText(highScoreFilename), out score) ? score : 0;
        }

        private static void SaveHighScore(int score)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(highScoreFilename));
            File.WriteAllText(highScoreFilename, score.ToString());
        }
    }
}
