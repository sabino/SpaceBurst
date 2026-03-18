using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceBurst.RuntimeData;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaceBurst
{
    sealed class CampaignDirector
    {
        private const float GameOverDelaySeconds = 0.85f;
        private const float LevelClearSeconds = 1.35f;
        private const float PlayerSafetyClearRadius = 220f;

        private readonly CampaignRepository repository = new CampaignRepository();
        private readonly OptionsData options;
        private readonly MedalProgress medals;
        private readonly List<ScheduledSpawn> scheduledSpawns = new List<ScheduledSpawn>();

        private StageDefinition currentStage;
        private BossEnemy activeBoss;
        private GameFlowState state = GameFlowState.Title;
        private int currentStageNumber = 1;
        private int currentSectionIndex;
        private int checkpointSectionIndex;
        private float stageElapsedSeconds;
        private float stateTimer;
        private bool stageHadDeath;
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

        public float CurrentScrollSpeed
        {
            get
            {
                if (currentStage == null || state == GameFlowState.Title || state == GameFlowState.Options || state == GameFlowState.GameOver || state == GameFlowState.CampaignComplete)
                    return 0f;

                if (activeBoss != null && currentStage.Boss != null)
                    return currentStage.Boss.ArenaScrollSpeed;

                return currentStage.ScrollSpeed;
            }
        }

        public string CurrentTheme
        {
            get { return currentStage != null ? currentStage.Theme : "Nebula"; }
        }

        public int CurrentBackgroundSeed
        {
            get { return currentStage != null ? currentStage.BackgroundSeed : 1; }
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
            List<UiButton> buttons = GetTitleButtons();
            UpdateVerticalSelection(ref titleSelection, buttons.Count);
            HandlePointerSelection(buttons, ref titleSelection);

            if (Input.WasConfirmPressed() || Input.WasPrimaryActionPressed())
                ActivateTitleSelection(titleSelection);
        }

        private void UpdateOptions()
        {
            int itemCount = 2;
            UpdateVerticalSelection(ref optionSelection, itemCount);

            List<UiButton> buttons = GetOptionButtons();
            HandlePointerSelection(buttons, ref optionSelection);

            if (optionSelection == 0 && (Input.WasNavigateLeftPressed() || Input.WasNavigateRightPressed() || Input.WasPrimaryActionPressed() || Input.WasConfirmPressed()))
                CycleRetryMode(Input.WasNavigateLeftPressed() ? -1 : 1);
            else if (optionSelection == 1 && (Input.WasPrimaryActionPressed() || Input.WasConfirmPressed() || Input.WasCancelPressed()))
                state = GameFlowState.Title;
        }

        private void UpdatePlaying()
        {
            float deltaSeconds = (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            stageElapsedSeconds += deltaSeconds;

            if (Input.WasCancelPressed())
            {
                state = GameFlowState.Title;
                PlayerStatus.FinalizeRun();
                return;
            }

            ScheduleDueSections();
            SpawnDueEntities();
            EntityManager.Update();

            if (Player1.Instance.ConsumeHullDestroyed() || EntityManager.ConsumePlayerHullDestruction())
            {
                HandlePlayerShipDestroyed();
                return;
            }

            PlayerStatus.Update();

            if (activeBoss != null)
                DrainBossSupportQueue();

            if (activeBoss == null && currentStage.Boss != null && currentSectionIndex >= currentStage.Sections.Count && scheduledSpawns.Count == 0 && !EntityManager.HasHostiles)
            {
                state = GameFlowState.BossWarning;
                stateTimer = currentStage.Boss.IntroSeconds;
                bannerText = string.Concat("Boss Incoming\n", currentStage.Boss.DisplayName);
                return;
            }

            if (currentStage.Boss == null && currentSectionIndex >= currentStage.Sections.Count && scheduledSpawns.Count == 0 && !EntityManager.HasHostiles)
            {
                CompleteStage();
                return;
            }

            if (activeBoss != null && activeBoss.IsExpired && !EntityManager.HasHostiles && scheduledSpawns.Count == 0)
                CompleteStage();
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

            if (currentStageNumber >= 50)
            {
                EnterEndState(GameFlowState.CampaignComplete, "Campaign Complete");
                return;
            }

            PrepareStage(currentStageNumber + 1, 0, false, false);
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

        private void HandlePlayerShipDestroyed()
        {
            if (state != GameFlowState.Playing)
                return;

            stageHadDeath = true;
            campaignHadDeath = true;
            PlayerStatus.RemoveLife();
            PlayerStatus.ResetMultiplier();

            if (PlayerStatus.IsGameOver)
            {
                EntityManager.ClearHostiles();
                scheduledSpawns.Clear();
                Player1.Instance.StartRespawn(GameOverDelaySeconds);
                EnterEndState(GameFlowState.GameOver, "Game Over");
                return;
            }

            switch (options.RetryMode)
            {
                case RetryMode.WaveCheckpoint:
                    PrepareStage(currentStageNumber, checkpointSectionIndex, true, true);
                    break;

                case RetryMode.CasualRespawn:
                    Player1.Instance.StartRespawn(1.05f);
                    EntityManager.ClearHostilesNear(new Vector2(Game1.ScreenSize.X * 0.22f, Game1.ScreenSize.Y * 0.5f), PlayerSafetyClearRadius);
                    break;

                default:
                    PrepareStage(currentStageNumber, 0, true, false);
                    break;
            }
        }

        private void PrepareStage(int stageNumber, int startSectionIndex, bool isRetry, bool fromCheckpoint)
        {
            currentStageNumber = stageNumber;
            currentStage = repository.GetStage(stageNumber);
            currentSectionIndex = Math.Clamp(startSectionIndex, 0, currentStage.Sections.Count - 1);
            checkpointSectionIndex = currentSectionIndex;
            stageElapsedSeconds = currentSectionIndex > 0 ? currentStage.Sections[currentSectionIndex].StartSeconds : 0f;
            activeBoss = null;
            scheduledSpawns.Clear();

            EntityManager.Reset();
            Player1.Instance.ResetForStage();
            EntityManager.Add(Player1.Instance);

            if (!isRetry)
                stageHadDeath = false;

            bannerText = isRetry
                ? (fromCheckpoint ? string.Concat("Checkpoint\nStage ", stageNumber.ToString()) : string.Concat("Retry Stage ", stageNumber.ToString()))
                : string.Concat("Stage ", stageNumber.ToString(), "\n", currentStage.Name);

            state = GameFlowState.LevelIntro;
            stateTimer = currentStage.IntroSeconds;
        }

        private void StartCampaign()
        {
            PlayerStatus.FinalizeRun();
            PlayerStatus.BeginCampaign();
            medalEligibleRun = options.RetryMode == RetryMode.ClassicStageRestart;
            campaignHadDeath = false;
            stageHadDeath = false;
            currentStageNumber = 1;
            PrepareStage(1, 0, false, false);
        }

        private void CompleteStage()
        {
            UnlockStageMedals();
            state = GameFlowState.LevelClear;
            stateTimer = LevelClearSeconds;
            bannerText = string.Concat("Stage ", currentStageNumber.ToString(), " Clear");
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

        private void ScheduleDueSections()
        {
            while (currentSectionIndex < currentStage.Sections.Count && currentStage.Sections[currentSectionIndex].StartSeconds <= stageElapsedSeconds)
            {
                SectionDefinition section = currentStage.Sections[currentSectionIndex];
                if (section.Checkpoint)
                    checkpointSectionIndex = currentSectionIndex;

                ScheduleSection(section);
                currentSectionIndex++;
            }
        }

        private void ScheduleSection(SectionDefinition section)
        {
            for (int groupIndex = 0; groupIndex < section.Groups.Count; groupIndex++)
                ScheduleGroup(section.StartSeconds, section.Groups[groupIndex]);
        }

        private void ScheduleGroup(float stageStartSeconds, SpawnGroupDefinition group)
        {
            EnemyArchetypeDefinition archetype = repository.ArchetypesById[group.ArchetypeId];
            float targetY = LevelMath.ResolveTargetY(Game1.ScreenSize.ToSystemNumerics(), group);

            for (int index = 0; index < group.Count; index++)
            {
                scheduledSpawns.Add(new ScheduledSpawn
                {
                    SpawnAtSeconds = stageStartSeconds + group.StartSeconds + group.SpawnIntervalSeconds * index,
                    Group = group,
                    SpawnPoint = LevelMath.GetSpawnPoint(Game1.ScreenSize.ToSystemNumerics(), group, index).ToXna(),
                    TargetY = targetY,
                    MovePattern = group.MovePatternOverride ?? archetype.MovePattern,
                    FirePattern = group.FirePatternOverride ?? archetype.FirePattern,
                    Amplitude = group.Amplitude > 0f ? group.Amplitude : archetype.MovementAmplitude,
                    Frequency = group.Frequency > 0f ? group.Frequency : archetype.MovementFrequency,
                });
            }

            scheduledSpawns.Sort((left, right) => left.SpawnAtSeconds.CompareTo(right.SpawnAtSeconds));
        }

        private void SpawnDueEntities()
        {
            while (scheduledSpawns.Count > 0 && scheduledSpawns[0].SpawnAtSeconds <= stageElapsedSeconds)
            {
                ScheduledSpawn scheduledSpawn = scheduledSpawns[0];
                scheduledSpawns.RemoveAt(0);

                EnemyArchetypeDefinition archetype = repository.ArchetypesById[scheduledSpawn.Group.ArchetypeId];
                var enemy = new Enemy(
                    archetype,
                    scheduledSpawn.SpawnPoint,
                    scheduledSpawn.TargetY,
                    scheduledSpawn.MovePattern,
                    scheduledSpawn.FirePattern,
                    scheduledSpawn.Group.SpeedMultiplier,
                    scheduledSpawn.Amplitude,
                    scheduledSpawn.Frequency);

                EntityManager.Add(enemy);
            }
        }

        private void SpawnBoss()
        {
            EnemyArchetypeDefinition archetype = repository.ArchetypesById[currentStage.Boss.ArchetypeId];
            Vector2 spawnPoint = new Vector2(Game1.ScreenSize.X + archetype.SpawnLeadDistance, currentStage.Boss.TargetY * Game1.ScreenSize.Y);
            activeBoss = new BossEnemy(archetype, currentStage.Boss, spawnPoint);
            EntityManager.Add(activeBoss);
        }

        private void DrainBossSupportQueue()
        {
            while (activeBoss != null && activeBoss.TryConsumeSupportGroup(out SpawnGroupDefinition group))
                ScheduleGroup(stageElapsedSeconds + 0.35f, group);
        }

        private void UnlockStageMedals()
        {
            if (!medalEligibleRun)
                return;

            medals.UnlockStageClear(currentStageNumber);
            if (!stageHadDeath)
                medals.UnlockNoDeath(currentStageNumber);
            if (currentStage.Boss != null)
                medals.UnlockBossClear(currentStageNumber);

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
            RetryMode[] values = (RetryMode[])Enum.GetValues(typeof(RetryMode));
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
            int width = 400;
            int height = 58;
            int x = (int)center.X - width / 2;
            int y = (int)center.Y - 12 + index * 78 - (total - 1) * 39;
            return new UiButton(new Rectangle(x, y, width, height), text);
        }

        private void DrawTitle(SpriteBatch spriteBatch, Texture2D pixel)
        {
            DrawCenteredText(spriteBatch, "SpaceBurst", Game1.ScreenSize.X / 2f, 132f, Color.White, 1.6f);
            DrawCenteredText(spriteBatch, "Autoscroll Destructible Campaign", Game1.ScreenSize.X / 2f, 198f, Color.White * 0.68f, 0.7f);
            DrawCenteredText(spriteBatch, string.Concat("High Score: ", PlayerStatus.HighScore.ToString()), Game1.ScreenSize.X / 2f, 240f, Color.White * 0.85f, 0.8f);

            List<UiButton> buttons = GetTitleButtons();
            for (int i = 0; i < buttons.Count; i++)
                DrawButton(spriteBatch, pixel, buttons[i], i == titleSelection);

            string medalText = medals.CampaignClear
                ? (medals.PerfectCampaign ? "Perfect Campaign Medal: Unlocked" : "Campaign Clear Medal: Unlocked")
                : "Campaign medals not yet unlocked";
            DrawCenteredText(spriteBatch, medalText, Game1.ScreenSize.X / 2f, 520f, Color.White * 0.65f, 0.62f);
        }

        private void DrawOptions(SpriteBatch spriteBatch, Texture2D pixel)
        {
            DrawCenteredText(spriteBatch, "Options", Game1.ScreenSize.X / 2f, 140f, Color.White, 1.2f);

            List<UiButton> buttons = GetOptionButtons();
            for (int i = 0; i < buttons.Count; i++)
                DrawButton(spriteBatch, pixel, buttons[i], i == optionSelection);

            DrawCenteredText(spriteBatch, "Classic restart is medal-eligible. Easier modes disable medals.", Game1.ScreenSize.X / 2f, 410f, Color.White * 0.62f, 0.58f);
        }

        private void DrawHud(SpriteBatch spriteBatch, Texture2D pixel)
        {
            spriteBatch.DrawString(Element.Font, string.Concat("Lives: ", PlayerStatus.Lives.ToString()), new Vector2(16f, 12f), Color.White);
            spriteBatch.DrawString(Element.Font, string.Concat("Score: ", PlayerStatus.Score.ToString()), new Vector2(Game1.ScreenSize.X - 180f, 12f), Color.White);
            spriteBatch.DrawString(Element.Font, string.Concat("Multiplier: ", PlayerStatus.Multiplier.ToString()), new Vector2(Game1.ScreenSize.X - 180f, 42f), Color.White);
            DrawCenteredText(spriteBatch, string.Concat("Stage ", currentStageNumber.ToString("00")), Game1.ScreenSize.X / 2f, 12f, Color.White, 0.72f);

            if (!medalEligibleRun)
                DrawCenteredText(spriteBatch, "Medals Disabled", Game1.ScreenSize.X / 2f, 44f, Color.Orange, 0.56f);

            if (activeBoss != null && !activeBoss.IsExpired)
                DrawBossHealthBar(spriteBatch, pixel, activeBoss);
        }

        private void DrawBossHealthBar(SpriteBatch spriteBatch, Texture2D pixel, BossEnemy boss)
        {
            Rectangle bounds = new Rectangle(320, 78, 640, 20);
            spriteBatch.Draw(pixel, bounds, Color.White * 0.14f);
            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, (int)(bounds.Width * boss.HealthRatio), bounds.Height), Color.OrangeRed);
            DrawCenteredText(spriteBatch, boss.DisplayName, Game1.ScreenSize.X / 2f, 54f, Color.White, 0.68f);
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
            Vector2 textPosition = new Vector2(button.Bounds.Center.X - textSize.X / 2f, button.Bounds.Center.Y - textSize.Y / 2f);
            spriteBatch.DrawString(Element.Font, button.Text, textPosition, Color.White);
        }

        private void DrawCenteredText(SpriteBatch spriteBatch, string text, float x, float y, Color color, float scale)
        {
            Vector2 size = Element.Font.MeasureString(text) * scale;
            spriteBatch.DrawString(Element.Font, text, new Vector2(x - size.X / 2f, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
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
            public Vector2 SpawnPoint { get; set; }
            public float TargetY { get; set; }
            public MovePattern MovePattern { get; set; }
            public FirePattern FirePattern { get; set; }
            public float Amplitude { get; set; }
            public float Frequency { get; set; }
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
