using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpaceBurst.RuntimeData;

namespace SpaceBurst
{
    sealed class CampaignDirector
    {
        private const float gameOverDelaySeconds = 0.85f;
        private const float bossWarningSeconds = 1.25f;
        private const float levelClearSeconds = 1.25f;
        private const float playerSafetyClearRadius = 180f;

        private readonly CampaignRepository repository = new CampaignRepository();
        private readonly OptionsData options;
        private readonly MedalProgress medals;
        private readonly List<ScheduledSpawn> scheduledSpawns = new List<ScheduledSpawn>();

        private LevelDefinition currentLevel;
        private BossEnemy activeBoss;
        private GameFlowState state = GameFlowState.Title;
        private int currentLevelNumber = 1;
        private int currentWaveIndex;
        private int checkpointWaveIndex;
        private int levelStartWaveIndex;
        private float levelElapsedSeconds;
        private float stateTimer;
        private bool levelHadDeath;
        private bool campaignHadDeath;
        private bool medalEligibleRun;
        private string bannerText = string.Empty;
        private int titleSelection;
        private int optionSelection;

        public CampaignDirector()
        {
            options = PersistentStorage.LoadOptions();
            medals = PersistentStorage.LoadMedals();
        }

        public bool ShouldDrawWorld
        {
            get { return state != GameFlowState.Title && state != GameFlowState.Options; }
        }

        public bool ShouldDrawTouchControls
        {
            get { return state == GameFlowState.Playing && ShouldDrawWorld; }
        }

        public void Load()
        {
            repository.Load();
            state = GameFlowState.Title;
        }

        public void Update()
        {
            switch (state)
            {
                case GameFlowState.Title:
                    UpdateTitle();
                    break;
                case GameFlowState.Options:
                    UpdateOptions();
                    break;
                case GameFlowState.LevelIntro:
                    UpdateTimedState(GameFlowState.Playing);
                    break;
                case GameFlowState.BossWarning:
                    UpdateBossWarning();
                    break;
                case GameFlowState.LevelClear:
                    UpdateLevelClear();
                    break;
                case GameFlowState.GameOver:
                case GameFlowState.CampaignComplete:
                    UpdateEndState();
                    break;
                case GameFlowState.Playing:
                    UpdatePlaying();
                    break;
            }
        }

        public void HandlePlayerCollision()
        {
            if (state != GameFlowState.Playing || Player1.Instance.IsDead || Player1.Instance.IsInvulnerable)
                return;

            levelHadDeath = true;
            campaignHadDeath = true;
            PlayerStatus.RemoveLife();
            PlayerStatus.ResetMultiplier();

            if (PlayerStatus.IsGameOver)
            {
                EntityManager.ClearHostiles();
                scheduledSpawns.Clear();
                Player1.Instance.StartRespawn(gameOverDelaySeconds);
                EnterEndState(GameFlowState.GameOver, "Game Over");
                return;
            }

            switch (options.RetryMode)
            {
                case RetryMode.WaveCheckpoint:
                    PrepareLevel(currentLevelNumber, checkpointWaveIndex, true, true);
                    break;

                case RetryMode.CasualRespawn:
                    Player1.Instance.StartRespawn(1.1f);
                    EntityManager.ClearHostilesNear(Game1.ScreenSize / 2f, playerSafetyClearRadius);
                    break;

                default:
                    PrepareLevel(currentLevelNumber, 0, true, false);
                    break;
            }
        }

        public void DrawUi(SpriteBatch spriteBatch, Texture2D pixel)
        {
            switch (state)
            {
                case GameFlowState.Title:
                    DrawTitle(spriteBatch, pixel);
                    break;

                case GameFlowState.Options:
                    DrawOptions(spriteBatch, pixel);
                    break;

                case GameFlowState.Playing:
                    DrawHud(spriteBatch, pixel);
                    break;

                case GameFlowState.LevelIntro:
                case GameFlowState.BossWarning:
                case GameFlowState.LevelClear:
                    DrawHud(spriteBatch, pixel);
                    DrawCenteredBanner(spriteBatch, bannerText, Color.White);
                    break;

                case GameFlowState.GameOver:
                    DrawHud(spriteBatch, pixel);
                    DrawCenteredBanner(
                        spriteBatch,
                        string.Concat("Game Over\nScore: ", PlayerStatus.Score.ToString(), "\nHigh Score: ", PlayerStatus.HighScore.ToString(), "\nTap or press Enter"),
                        Color.White);
                    break;

                case GameFlowState.CampaignComplete:
                    DrawHud(spriteBatch, pixel);
                    DrawCenteredBanner(
                        spriteBatch,
                        string.Concat("Campaign Complete\nScore: ", PlayerStatus.Score.ToString(), "\nHigh Score: ", PlayerStatus.HighScore.ToString(), "\nTap or press Enter"),
                        Color.White);
                    break;
            }
        }

        private void UpdateTitle()
        {
            var buttons = GetTitleButtons();
            UpdateVerticalSelection(ref titleSelection, buttons.Count);
            HandlePointerSelection(buttons, ref titleSelection);

            if (Input.WasConfirmPressed() || Input.WasPrimaryActionPressed())
                ActivateTitleSelection(titleSelection);
        }

        private void UpdateOptions()
        {
            int itemCount = 2;
            UpdateVerticalSelection(ref optionSelection, itemCount);

            var buttons = GetOptionButtons();
            HandlePointerSelection(buttons, ref optionSelection);

            if (optionSelection == 0 && (Input.WasNavigateLeftPressed() || Input.WasNavigateRightPressed() || Input.WasPrimaryActionPressed() || Input.WasConfirmPressed()))
            {
                CycleRetryMode(Input.WasNavigateLeftPressed() ? -1 : 1);
            }
            else if (optionSelection == 1 && (Input.WasPrimaryActionPressed() || Input.WasConfirmPressed() || Input.WasCancelPressed()))
            {
                state = GameFlowState.Title;
            }
        }

        private void UpdatePlaying()
        {
            float deltaSeconds = (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            levelElapsedSeconds += deltaSeconds;

            if (Input.WasCancelPressed())
            {
                state = GameFlowState.Title;
                PlayerStatus.FinalizeRun();
                return;
            }

            ScheduleDueWaves();
            SpawnDueEntities();
            EntityManager.Update();
            PlayerStatus.Update();

            if (activeBoss != null)
                DrainBossSupportQueue();

            if (activeBoss == null && currentLevel.Boss != null && currentWaveIndex >= currentLevel.Waves.Count && scheduledSpawns.Count == 0 && !EntityManager.HasHostiles)
            {
                state = GameFlowState.BossWarning;
                stateTimer = bossWarningSeconds;
                bannerText = string.Concat("Boss Incoming\n", currentLevel.Boss.DisplayName);
                return;
            }

            if (currentLevel.Boss == null && currentWaveIndex >= currentLevel.Waves.Count && scheduledSpawns.Count == 0 && !EntityManager.HasHostiles)
            {
                CompleteLevel();
                return;
            }

            if (activeBoss != null && activeBoss.IsExpired && !EntityManager.HasHostiles && scheduledSpawns.Count == 0)
                CompleteLevel();
        }

        private void UpdateBossWarning()
        {
            stateTimer -= (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            if (stateTimer <= 0f || Input.WasConfirmPressed() || Input.WasPrimaryActionPressed())
            {
                SpawnBoss();
                state = GameFlowState.Playing;
            }
        }

        private void UpdateLevelClear()
        {
            stateTimer -= (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            if (stateTimer > 0f && !Input.WasConfirmPressed() && !Input.WasPrimaryActionPressed())
                return;

            if (currentLevelNumber >= 50)
            {
                EnterEndState(GameFlowState.CampaignComplete, "Campaign Complete");
                return;
            }

            PrepareLevel(currentLevelNumber + 1, 0, false, false);
        }

        private void UpdateEndState()
        {
            stateTimer -= (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            if (stateTimer > 0f)
                return;

            if (Input.WasConfirmPressed() || Input.WasPrimaryActionPressed() || Input.WasCancelPressed())
                state = GameFlowState.Title;
        }

        private void UpdateTimedState(GameFlowState nextState)
        {
            stateTimer -= (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            if (stateTimer <= 0f || Input.WasConfirmPressed() || Input.WasPrimaryActionPressed())
                state = nextState;
        }

        private void PrepareLevel(int levelNumber, int startWaveIndex, bool isRetry, bool fromCheckpoint)
        {
            currentLevelNumber = levelNumber;
            currentLevel = repository.GetLevel(levelNumber);
            currentWaveIndex = startWaveIndex;
            checkpointWaveIndex = startWaveIndex;
            levelStartWaveIndex = startWaveIndex;
            levelElapsedSeconds = startWaveIndex > 0 ? currentLevel.Waves[startWaveIndex].StartSeconds : 0f;
            activeBoss = null;
            scheduledSpawns.Clear();

            EntityManager.Reset();
            Player1.Instance.ResetForLevel();
            EntityManager.Add(Player1.Instance);

            if (!isRetry)
                levelHadDeath = false;

            bannerText = isRetry
                ? (fromCheckpoint ? string.Concat("Checkpoint\nLevel ", levelNumber.ToString()) : string.Concat("Retry Level ", levelNumber.ToString()))
                : string.Concat("Level ", levelNumber.ToString(), "\n", currentLevel.Name);

            state = GameFlowState.LevelIntro;
            stateTimer = currentLevel.IntroSeconds;
        }

        private void StartCampaign()
        {
            PlayerStatus.FinalizeRun();
            PlayerStatus.BeginCampaign();
            medalEligibleRun = options.RetryMode == RetryMode.ClassicStageRestart;
            campaignHadDeath = false;
            levelHadDeath = false;
            currentLevelNumber = 1;
            PrepareLevel(1, 0, false, false);
        }

        private void CompleteLevel()
        {
            UnlockLevelMedals();
            state = GameFlowState.LevelClear;
            stateTimer = levelClearSeconds;
            bannerText = string.Concat("Level ", currentLevelNumber.ToString(), " Clear");
        }

        private void EnterEndState(GameFlowState endState, string text)
        {
            PlayerStatus.FinalizeRun();
            if (medalEligibleRun && endState == GameFlowState.CampaignComplete)
            {
                medals.CampaignClear = true;
                if (!campaignHadDeath)
                    medals.PerfectCampaign = true;
                PersistentStorage.SaveMedals(medals);
            }

            bannerText = text;
            state = endState;
            stateTimer = 0.35f;
        }

        private void ScheduleDueWaves()
        {
            while (currentWaveIndex < currentLevel.Waves.Count && currentLevel.Waves[currentWaveIndex].StartSeconds <= levelElapsedSeconds)
            {
                var wave = currentLevel.Waves[currentWaveIndex];
                if (wave.Checkpoint)
                    checkpointWaveIndex = currentWaveIndex;

                ScheduleWave(wave);
                currentWaveIndex++;
            }
        }

        private void ScheduleWave(WaveDefinition wave)
        {
            foreach (var group in wave.Groups)
            {
                var offsets = LevelMath.GetFormationOffsets(group.Formation, group.Count, group.Spacing);
                for (int i = 0; i < offsets.Count; i++)
                {
                    Vector2 anchor = GetSafeAnchor(LevelMath.GetAnchorPoint(Game1.ScreenSize.ToSystemNumerics(), group.AnchorX, group.AnchorY, offsets[i]).ToXna(), levelStartWaveIndex == 0);
                    Vector2 spawn = LevelMath.GetSpawnPoint(group.EntrySide, Game1.ScreenSize.ToSystemNumerics(), anchor.ToSystemNumerics(), offsets[i], 90f).ToXna();
                    scheduledSpawns.Add(new ScheduledSpawn
                    {
                        SpawnAtSeconds = wave.StartSeconds + group.DelayBetweenSpawns * i,
                        Group = group,
                        FormationIndex = i,
                        SpawnPoint = spawn,
                        AnchorPoint = anchor,
                    });
                }
            }

            scheduledSpawns.Sort((left, right) => left.SpawnAtSeconds.CompareTo(right.SpawnAtSeconds));
        }

        private void SpawnDueEntities()
        {
            while (scheduledSpawns.Count > 0 && scheduledSpawns[0].SpawnAtSeconds <= levelElapsedSeconds)
            {
                var scheduledSpawn = scheduledSpawns[0];
                scheduledSpawns.RemoveAt(0);

                var archetype = repository.ArchetypesById[scheduledSpawn.Group.ArchetypeId];
                var enemy = new Enemy(
                    archetype,
                    scheduledSpawn.SpawnPoint,
                    scheduledSpawn.AnchorPoint,
                    scheduledSpawn.Group.PathType,
                    scheduledSpawn.FormationIndex,
                    scheduledSpawn.Group.SpeedMultiplier);

                EntityManager.Add(enemy);
            }
        }

        private void SpawnBoss()
        {
            var archetype = repository.ArchetypesById[currentLevel.Boss.ArchetypeId];
            Vector2 anchor = LevelMath.GetAnchorPoint(Game1.ScreenSize.ToSystemNumerics(), currentLevel.Boss.AnchorX, currentLevel.Boss.AnchorY, System.Numerics.Vector2.Zero).ToXna();
            Vector2 spawn = LevelMath.GetSpawnPoint(currentLevel.Boss.EntrySide, Game1.ScreenSize.ToSystemNumerics(), anchor.ToSystemNumerics(), System.Numerics.Vector2.Zero, 120f).ToXna();

            activeBoss = new BossEnemy(archetype, currentLevel.Boss, spawn, anchor);
            EntityManager.Add(activeBoss);
        }

        private void DrainBossSupportQueue()
        {
            while (activeBoss != null && activeBoss.TryConsumeSupportWave(out SpawnGroupDefinition group))
            {
                var wave = new WaveDefinition
                {
                    StartSeconds = levelElapsedSeconds + 0.2f,
                    Checkpoint = false,
                    Groups = new List<SpawnGroupDefinition> { group },
                };
                ScheduleWave(wave);
            }
        }

        private void UnlockLevelMedals()
        {
            if (!medalEligibleRun)
                return;

            medals.UnlockStageClear(currentLevelNumber);
            if (!levelHadDeath)
                medals.UnlockNoDeath(currentLevelNumber);
            if (currentLevel.Boss != null)
                medals.UnlockBossClear(currentLevelNumber);

            PersistentStorage.SaveMedals(medals);
        }

        private void ActivateTitleSelection(int selection)
        {
            switch (selection)
            {
                case 1:
                    state = GameFlowState.Options;
                    break;

                case 2:
                    Game1.Instance.Exit();
                    break;

                default:
                    StartCampaign();
                    break;
            }
        }

        private void CycleRetryMode(int direction)
        {
            var values = (RetryMode[])Enum.GetValues(typeof(RetryMode));
            int current = Array.IndexOf(values, options.RetryMode);
            current = (current + values.Length + direction) % values.Length;
            options.RetryMode = values[current];
            PersistentStorage.SaveOptions(options);
        }

        private void HandlePointerSelection(List<UiButton> buttons, ref int selection)
        {
            if (!Input.WasPrimaryActionPressed())
                return;

            Vector2 pointer = Input.PointerPosition;
            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i].Bounds.Contains(pointer))
                {
                    selection = i;
                    break;
                }
            }
        }

        private void UpdateVerticalSelection(ref int selection, int count)
        {
            if (Input.WasNavigateUpPressed())
                selection = (selection + count - 1) % count;
            else if (Input.WasNavigateDownPressed())
                selection = (selection + 1) % count;
        }

        private List<UiButton> GetTitleButtons()
        {
            return new List<UiButton>
            {
                CreateButton("Start Campaign", 0, 3),
                CreateButton("Options", 1, 3),
                CreateButton("Quit", 2, 3),
            };
        }

        private List<UiButton> GetOptionButtons()
        {
            return new List<UiButton>
            {
                CreateButton(string.Concat("Retry Mode: ", GetRetryModeLabel(options.RetryMode)), 0, 2),
                CreateButton("Back", 1, 2),
            };
        }

        private UiButton CreateButton(string text, int index, int total)
        {
            Vector2 center = Game1.ScreenSize / 2f;
            int width = 360;
            int height = 54;
            int x = (int)center.X - width / 2;
            int y = (int)center.Y - 20 + index * 74 - (total - 1) * 37;
            return new UiButton(new Rectangle(x, y, width, height), text);
        }

        private void DrawTitle(SpriteBatch spriteBatch, Texture2D pixel)
        {
            DrawCenteredText(spriteBatch, "SpaceBurst", Game1.ScreenSize.X / 2f, 140f, Color.White, 1.4f);
            DrawCenteredText(spriteBatch, "Deterministic Campaign Build", Game1.ScreenSize.X / 2f, 200f, Color.White * 0.65f, 0.65f);
            DrawCenteredText(spriteBatch, string.Concat("High Score: ", PlayerStatus.HighScore.ToString()), Game1.ScreenSize.X / 2f, 240f, Color.White * 0.85f, 0.75f);

            var buttons = GetTitleButtons();
            for (int i = 0; i < buttons.Count; i++)
                DrawButton(spriteBatch, pixel, buttons[i], i == titleSelection);

            string medalText = medals.CampaignClear
                ? (medals.PerfectCampaign ? "Perfect Campaign Medal: Unlocked" : "Campaign Clear Medal: Unlocked")
                : "Campaign medals not yet unlocked";
            DrawCenteredText(spriteBatch, medalText, Game1.ScreenSize.X / 2f, 470f, Color.White * 0.65f, 0.6f);
        }

        private void DrawOptions(SpriteBatch spriteBatch, Texture2D pixel)
        {
            DrawCenteredText(spriteBatch, "Options", Game1.ScreenSize.X / 2f, 140f, Color.White, 1.15f);

            var buttons = GetOptionButtons();
            for (int i = 0; i < buttons.Count; i++)
                DrawButton(spriteBatch, pixel, buttons[i], i == optionSelection);

            DrawCenteredText(spriteBatch, "Classic restart is medal-eligible. Easier modes disable medals.", Game1.ScreenSize.X / 2f, 390f, Color.White * 0.6f, 0.58f);
        }

        private void DrawHud(SpriteBatch spriteBatch, Texture2D pixel)
        {
            spriteBatch.DrawString(Element.Font, string.Concat("Lives: ", PlayerStatus.Lives.ToString()), new Vector2(5f, 5f), Color.White);

            string stageLabel = string.Concat("Level ", currentLevelNumber.ToString("00"));
            DrawCenteredText(spriteBatch, stageLabel, Game1.ScreenSize.X / 2f, 10f, Color.White, 0.7f);

            DrawRightAlignedText(spriteBatch, string.Concat("Score: ", PlayerStatus.Score.ToString()), 5f);
            DrawRightAlignedText(spriteBatch, string.Concat("Multiplier: ", PlayerStatus.Multiplier.ToString()), 35f);

            if (!medalEligibleRun)
                DrawCenteredText(spriteBatch, "Medals Disabled", Game1.ScreenSize.X / 2f, 38f, Color.Orange, 0.55f);

            if (activeBoss != null && !activeBoss.IsExpired)
                DrawBossHealthBar(spriteBatch, pixel, activeBoss);
        }

        private void DrawBossHealthBar(SpriteBatch spriteBatch, Texture2D pixel, BossEnemy boss)
        {
            Rectangle bounds = new Rectangle(180, 72, 440, 18);
            spriteBatch.Draw(pixel, bounds, Color.White * 0.15f);
            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, (int)(bounds.Width * boss.HealthRatio), bounds.Height), Color.OrangeRed);
            DrawCenteredText(spriteBatch, boss.DisplayName, Game1.ScreenSize.X / 2f, 52f, Color.White, 0.65f);
        }

        private void DrawCenteredBanner(SpriteBatch spriteBatch, string text, Color color)
        {
            Vector2 size = Element.Font.MeasureString(text);
            Vector2 position = Game1.ScreenSize / 2f - size / 2f;
            spriteBatch.DrawString(Element.Font, text, position, color);
        }

        private void DrawButton(SpriteBatch spriteBatch, Texture2D pixel, UiButton button, bool selected)
        {
            Color fill = selected ? Color.White * 0.18f : Color.White * 0.08f;
            Color border = selected ? Color.Orange : Color.White * 0.35f;

            spriteBatch.Draw(pixel, button.Bounds, fill);
            spriteBatch.Draw(pixel, new Rectangle(button.Bounds.X, button.Bounds.Y, button.Bounds.Width, 2), border);
            spriteBatch.Draw(pixel, new Rectangle(button.Bounds.X, button.Bounds.Bottom - 2, button.Bounds.Width, 2), border);
            spriteBatch.Draw(pixel, new Rectangle(button.Bounds.X, button.Bounds.Y, 2, button.Bounds.Height), border);
            spriteBatch.Draw(pixel, new Rectangle(button.Bounds.Right - 2, button.Bounds.Y, 2, button.Bounds.Height), border);

            Vector2 textSize = Element.Font.MeasureString(button.Text);
            Vector2 textPosition = new Vector2(
                button.Bounds.Center.X - textSize.X / 2f,
                button.Bounds.Center.Y - textSize.Y / 2f);
            spriteBatch.DrawString(Element.Font, button.Text, textPosition, Color.White);
        }

        private void DrawCenteredText(SpriteBatch spriteBatch, string text, float x, float y, Color color, float scale)
        {
            Vector2 size = Element.Font.MeasureString(text) * scale;
            spriteBatch.DrawString(Element.Font, text, new Vector2(x - size.X / 2f, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawRightAlignedText(SpriteBatch spriteBatch, string text, float y)
        {
            float width = Element.Font.MeasureString(text).X;
            spriteBatch.DrawString(Element.Font, text, new Vector2(Game1.ScreenSize.X - width - 5f, y), Color.White);
        }

        private Vector2 GetSafeAnchor(Vector2 proposedAnchor, bool guardOpening)
        {
            if (!guardOpening)
                return proposedAnchor;

            Vector2 center = Game1.ScreenSize / 2f;
            Vector2 delta = proposedAnchor - center;
            if (delta == Vector2.Zero)
                delta = new Vector2(1f, 0f);

            if (delta.LengthSquared() < playerSafetyClearRadius * playerSafetyClearRadius)
                proposedAnchor = center + delta.ScaleTo(playerSafetyClearRadius);

            return Vector2.Clamp(proposedAnchor, new Vector2(80f, 90f), Game1.ScreenSize - new Vector2(80f, 90f));
        }

        private static string GetRetryModeLabel(RetryMode retryMode)
        {
            switch (retryMode)
            {
                case RetryMode.WaveCheckpoint:
                    return "Wave Checkpoint";
                case RetryMode.CasualRespawn:
                    return "Casual Respawn";
                default:
                    return "Classic Restart";
            }
        }

        private sealed class ScheduledSpawn
        {
            public float SpawnAtSeconds { get; set; }
            public SpawnGroupDefinition Group { get; set; }
            public int FormationIndex { get; set; }
            public Vector2 SpawnPoint { get; set; }
            public Vector2 AnchorPoint { get; set; }
        }

        private readonly struct UiButton
        {
            public Rectangle Bounds { get; }
            public string Text { get; }

            public UiButton(Rectangle bounds, string text)
            {
                Bounds = bounds;
                Text = text;
            }
        }
    }

    static class NumericsConversionExtensions
    {
        public static System.Numerics.Vector2 ToSystemNumerics(this Vector2 vector)
        {
            return new System.Numerics.Vector2(vector.X, vector.Y);
        }

        public static Vector2 ToXna(this System.Numerics.Vector2 vector)
        {
            return new Vector2(vector.X, vector.Y);
        }
    }
}
