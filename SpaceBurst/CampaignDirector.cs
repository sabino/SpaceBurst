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
        private readonly List<ScheduledEvent> scheduledEvents = new List<ScheduledEvent>();
        private readonly Random random = new Random();

        private StageDefinition currentStage;
        private BossEnemy activeBoss;
        private GameFlowState state = GameFlowState.Title;
        private GameFlowState helpReturnState = GameFlowState.Title;
        private int currentStageNumber = 1;
        private int currentSectionIndex;
        private int titleSelection;
        private int pauseSelection;
        private int helpPageIndex;
        private float stageElapsedSeconds;
        private float stateTimer;
        private float activeEventTimer;
        private float activeEventSpawnTimer;
        private bool stageHadDeath;
        private bool campaignHadDeath;
        private string bannerText = string.Empty;
        private string activeEventWarning = string.Empty;
        private RandomEventType activeEventType = RandomEventType.None;
        private float activeEventIntensity;

        public CampaignDirector()
        {
            options = PersistentStorage.LoadOptions();
            medals = PersistentStorage.LoadMedals();
        }

        public bool ShouldDrawWorld
        {
            get { return state != GameFlowState.Title || currentStage != null; }
        }

        public bool ShouldDrawTouchControls
        {
            get { return state == GameFlowState.Playing && ShouldDrawWorld; }
        }

        public float CurrentScrollSpeed
        {
            get
            {
                if (currentStage == null || state == GameFlowState.Title || state == GameFlowState.Help || state == GameFlowState.Paused || state == GameFlowState.GameOver || state == GameFlowState.CampaignComplete)
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

        public BackgroundMoodDefinition CurrentBackgroundMood
        {
            get
            {
                if (activeBoss != null && currentStage?.Boss?.MoodOverride != null)
                    return currentStage.Boss.MoodOverride;

                SectionDefinition section = GetActiveSection();
                return section?.Mood ?? currentStage?.BackgroundMood ?? new BackgroundMoodDefinition();
            }
        }

        public float CurrentDifficultyFactor
        {
            get
            {
                float stageFactor = MathHelper.Clamp((currentStageNumber - 1) / 49f, 0f, 1f);
                return activeBoss != null ? MathHelper.Clamp(stageFactor + 0.15f, 0f, 1f) : stageFactor;
            }
        }

        public RandomEventType ActiveEventType
        {
            get { return activeEventType; }
        }

        public float ActiveEventIntensity
        {
            get { return activeEventIntensity; }
        }

        public float CurrentPowerDropBonusChance
        {
            get
            {
                SectionDefinition section = GetActiveSection();
                return section?.PowerDropBonusChance ?? 0f;
            }
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
                case GameFlowState.Help:
                    UpdateHelp();
                    break;
                case GameFlowState.Paused:
                    UpdatePause();
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
                case GameFlowState.Help:
                    if (helpReturnState != GameFlowState.Title)
                        DrawHud(spriteBatch, pixel);
                    DrawHelp(spriteBatch, pixel);
                    break;
                case GameFlowState.Paused:
                    DrawHud(spriteBatch, pixel);
                    DrawPause(spriteBatch, pixel);
                    break;
                case GameFlowState.Playing:
                    DrawHud(spriteBatch, pixel);
                    break;
                case GameFlowState.LevelIntro:
                case GameFlowState.BossWarning:
                case GameFlowState.LevelClear:
                    DrawHud(spriteBatch, pixel);
                    DrawCenteredBanner(spriteBatch, pixel, bannerText, Color.White, 3f);
                    break;
                case GameFlowState.GameOver:
                    DrawHud(spriteBatch, pixel);
                    DrawCenteredBanner(spriteBatch, pixel, string.Concat("GAME OVER\nSCORE ", PlayerStatus.Score.ToString(), "\nHIGH ", PlayerStatus.HighScore.ToString(), "\nPRESS ENTER"), Color.White, 3f);
                    break;
                case GameFlowState.CampaignComplete:
                    DrawHud(spriteBatch, pixel);
                    DrawCenteredBanner(spriteBatch, pixel, string.Concat("CAMPAIGN COMPLETE\nSCORE ", PlayerStatus.Score.ToString(), "\nHIGH ", PlayerStatus.HighScore.ToString(), "\nPRESS ENTER"), Color.White, 3f);
                    break;
            }
        }
        private void UpdateTitle()
        {
            List<UiButton> buttons = GetTitleButtons();
            UpdateVerticalSelection(ref titleSelection, buttons.Count);
            HandlePointerSelection(buttons, ref titleSelection);

            if (Input.WasHelpPressed())
            {
                helpReturnState = GameFlowState.Title;
                state = GameFlowState.Help;
                return;
            }

            if (Input.WasConfirmPressed() || Input.WasPrimaryActionPressed())
                ActivateTitleSelection(titleSelection);
        }

        private void UpdatePause()
        {
            List<UiButton> buttons = GetPauseButtons();
            UpdateVerticalSelection(ref pauseSelection, buttons.Count);
            HandlePointerSelection(buttons, ref pauseSelection);

            if (Input.WasCancelPressed())
            {
                state = GameFlowState.Playing;
                return;
            }

            if (Input.WasHelpPressed())
            {
                helpReturnState = GameFlowState.Paused;
                state = GameFlowState.Help;
                return;
            }

            if (!Input.WasConfirmPressed() && !Input.WasPrimaryActionPressed())
                return;

            switch (pauseSelection)
            {
                case 1:
                    helpReturnState = GameFlowState.Paused;
                    state = GameFlowState.Help;
                    break;
                case 2:
                    PlayerStatus.FinalizeRun();
                    state = GameFlowState.Title;
                    break;
                default:
                    state = GameFlowState.Playing;
                    break;
            }
        }

        private void UpdateHelp()
        {
            if (Input.WasNavigateLeftPressed())
                helpPageIndex = (helpPageIndex + 3) % 4;
            else if (Input.WasNavigateRightPressed())
                helpPageIndex = (helpPageIndex + 1) % 4;

            if (Input.WasCancelPressed() || Input.WasConfirmPressed() || Input.WasPrimaryActionPressed() || Input.WasHelpPressed())
                state = helpReturnState;
        }

        private void UpdatePlaying()
        {
            float deltaSeconds = (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            stageElapsedSeconds += deltaSeconds;

            if (Input.WasCancelPressed())
            {
                state = GameFlowState.Paused;
                return;
            }

            if (Input.WasHelpPressed())
            {
                helpReturnState = GameFlowState.Paused;
                state = GameFlowState.Help;
                return;
            }

            ScheduleDueSections();
            SpawnDueEntities();
            SpawnDueEvents();
            UpdateActiveEvent(deltaSeconds);
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
                bannerText = string.Concat("BOSS INCOMING\n", currentStage.Boss.DisplayName);
                activeEventType = RandomEventType.None;
                activeEventTimer = 0f;
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
                EnterEndState(GameFlowState.CampaignComplete, "CAMPAIGN COMPLETE");
                return;
            }

            PrepareStage(currentStageNumber + 1, false);
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
            PlayerStatus.ResetMultiplier();

            PlayerDeathOutcome outcome = PlayerStatus.ConsumeDeath(currentStage);
            switch (outcome)
            {
                case PlayerDeathOutcome.RespawnInPlace:
                    Player1.Instance.StartRespawn(0.95f);
                    EntityManager.ClearHostilesNear(Player1.Instance.Position, PlayerSafetyClearRadius);
                    break;
                case PlayerDeathOutcome.RestartStage:
                    PrepareStage(currentStageNumber, true);
                    break;
                default:
                    EntityManager.ClearHostiles();
                    scheduledSpawns.Clear();
                    scheduledEvents.Clear();
                    Player1.Instance.StartRespawn(GameOverDelaySeconds);
                    EnterEndState(GameFlowState.GameOver, "GAME OVER");
                    break;
            }
        }

        private void PrepareStage(int stageNumber, bool isRetry)
        {
            currentStageNumber = stageNumber;
            currentStage = repository.GetStage(stageNumber);
            currentSectionIndex = 0;
            stageElapsedSeconds = 0f;
            activeBoss = null;
            activeEventType = RandomEventType.None;
            activeEventTimer = 0f;
            activeEventIntensity = 0f;
            activeEventSpawnTimer = 0f;
            activeEventWarning = string.Empty;
            scheduledSpawns.Clear();
            scheduledEvents.Clear();

            PlayerStatus.PrepareStage(currentStage, false);
            EntityManager.Reset();
            Player1.Instance.ResetForStage();
            EntityManager.Add(Player1.Instance);

            if (!isRetry)
                stageHadDeath = false;

            bannerText = isRetry
                ? string.Concat("LIFE LOST\nRESTART STAGE ", stageNumber.ToString("00"))
                : string.Concat("STAGE ", stageNumber.ToString("00"), "\n", currentStage.Name);

            state = GameFlowState.LevelIntro;
            stateTimer = currentStage.IntroSeconds;
        }

        private void StartCampaign()
        {
            PlayerStatus.FinalizeRun();
            PlayerStatus.BeginCampaign(repository.GetStage(1));
            campaignHadDeath = false;
            stageHadDeath = false;
            currentStageNumber = 1;
            PrepareStage(1, false);
        }

        private void CompleteStage()
        {
            UnlockStageMedals();
            state = GameFlowState.LevelClear;
            stateTimer = LevelClearSeconds;
            bannerText = string.Concat("STAGE ", currentStageNumber.ToString("00"), " CLEAR");
        }

        private void EnterEndState(GameFlowState endState, string text)
        {
            PlayerStatus.FinalizeRun();
            if (endState == GameFlowState.CampaignComplete)
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
                ScheduleSection(currentStage.Sections[currentSectionIndex]);
                currentSectionIndex++;
            }
        }

        private void ScheduleSection(SectionDefinition section)
        {
            for (int groupIndex = 0; groupIndex < section.Groups.Count; groupIndex++)
                ScheduleGroup(section.StartSeconds, section.Groups[groupIndex]);

            if (section.EventWindows == null)
                return;

            for (int i = 0; i < section.EventWindows.Count; i++)
            {
                RandomEventWindowDefinition window = section.EventWindows[i];
                float triggerOffset = window.StartSeconds + (float)random.NextDouble() * Math.Max(0f, window.DurationSeconds * 0.7f);
                scheduledEvents.Add(new ScheduledEvent
                {
                    TriggerAtSeconds = section.StartSeconds + triggerOffset,
                    Window = window,
                });
            }

            scheduledEvents.Sort((left, right) => left.TriggerAtSeconds.CompareTo(right.TriggerAtSeconds));
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
                EntityManager.Add(new Enemy(
                    archetype,
                    scheduledSpawn.SpawnPoint,
                    scheduledSpawn.TargetY,
                    scheduledSpawn.MovePattern,
                    scheduledSpawn.FirePattern,
                    scheduledSpawn.Group.SpeedMultiplier,
                    scheduledSpawn.Amplitude,
                    scheduledSpawn.Frequency));
            }
        }

        private void SpawnDueEvents()
        {
            while (scheduledEvents.Count > 0 && scheduledEvents[0].TriggerAtSeconds <= stageElapsedSeconds)
            {
                ScheduledEvent scheduledEvent = scheduledEvents[0];
                scheduledEvents.RemoveAt(0);
                StartRandomEvent(scheduledEvent.Window);
            }
        }

        private void StartRandomEvent(RandomEventWindowDefinition window)
        {
            activeEventType = window.EventType;
            activeEventIntensity = MathHelper.Clamp(window.Intensity, 0.2f, 2f);
            activeEventTimer = GetEventDuration(window.EventType, activeEventIntensity);
            activeEventSpawnTimer = 0f;
            activeEventWarning = GetEventLabel(window.EventType);
        }

        private void UpdateActiveEvent(float deltaSeconds)
        {
            if (activeEventType == RandomEventType.None)
                return;

            activeEventTimer -= deltaSeconds;
            activeEventSpawnTimer -= deltaSeconds;

            if (activeEventTimer <= 0f)
            {
                activeEventType = RandomEventType.None;
                activeEventIntensity = 0f;
                activeEventWarning = string.Empty;
                return;
            }

            if (activeEventType == RandomEventType.SolarFlare)
                return;

            float interval = GetEventSpawnInterval(activeEventType, activeEventIntensity);
            if (activeEventSpawnTimer > 0f)
                return;

            activeEventSpawnTimer = interval;
            SpawnEventHazard(activeEventType, activeEventIntensity);
        }

        private void SpawnEventHazard(RandomEventType eventType, float intensity)
        {
            Vector2 position;
            Vector2 velocity;
            ProceduralSpriteDefinition sprite;
            ImpactProfileDefinition impact;
            int damage;
            float scale;

            switch (eventType)
            {
                case RandomEventType.MeteorShower:
                    position = new Vector2(Game1.ScreenSize.X + 40f + random.Next(0, 160), random.Next(0, Game1.VirtualHeight / 2));
                    velocity = new Vector2(-320f - 60f * intensity, 180f + random.Next(-40, 80));
                    sprite = CreateEventProjectileDefinition("#B56A46", "#EABF8F", "#FFF1C9", new[] { ".#.", "###", ".#." });
                    impact = new ImpactProfileDefinition { Name = "Meteor", Kernel = ImpactKernelShape.Blast5, BaseCellsRemoved = 5, BonusCellsPerDamage = 1, SplashRadius = 1, SplashPercent = 35, DebrisBurstCount = 10, DebrisSpeed = 150f };
                    damage = 2;
                    scale = 1.4f;
                    break;
                case RandomEventType.CometSwarm:
                    position = new Vector2(Game1.ScreenSize.X + 40f + random.Next(0, 180), random.Next(40, Game1.VirtualHeight - 40));
                    velocity = new Vector2(-420f - 100f * intensity, random.Next(-80, 81));
                    sprite = CreateEventProjectileDefinition("#DFF4FF", "#8FD3FF", "#FFF0AF", new[] { "##", "##" });
                    impact = new ImpactProfileDefinition { Name = "Comet", Kernel = ImpactKernelShape.Diamond3, BaseCellsRemoved = 4, BonusCellsPerDamage = 1, SplashRadius = 0, SplashPercent = 0, DebrisBurstCount = 8, DebrisSpeed = 180f };
                    damage = 2;
                    scale = 1.1f;
                    break;
                default:
                    position = new Vector2(Game1.ScreenSize.X + 30f + random.Next(0, 120), random.Next(0, Game1.VirtualHeight));
                    velocity = new Vector2(-210f - 40f * intensity, random.Next(-30, 31));
                    sprite = CreateEventProjectileDefinition("#B7C6D8", "#6D859A", "#EAF6FF", new[] { "##.", ".##" });
                    impact = new ImpactProfileDefinition { Name = "Debris", Kernel = ImpactKernelShape.Cross3, BaseCellsRemoved = 3, BonusCellsPerDamage = 1, SplashRadius = 0, SplashPercent = 0, DebrisBurstCount = 6, DebrisSpeed = 110f };
                    damage = 1;
                    scale = 1f;
                    break;
            }

            EntityManager.Add(new Bullet(position, velocity, false, damage, impact, sprite, 0, 3.8f, 0f, scale));
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
                    helpReturnState = GameFlowState.Title;
                    state = GameFlowState.Help;
                    break;
                case 2:
                    Game1.Instance.Exit();
                    break;
                default:
                    StartCampaign();
                    break;
            }
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
                CreateButton("START CAMPAIGN", 0, 3),
                CreateButton("HELP", 1, 3),
                CreateButton("QUIT", 2, 3),
            };
        }

        private List<UiButton> GetPauseButtons()
        {
            return new List<UiButton>
            {
                CreateButton("RESUME", 0, 3),
                CreateButton("HELP", 1, 3),
                CreateButton("QUIT TO TITLE", 2, 3),
            };
        }

        private UiButton CreateButton(string text, int index, int total)
        {
            Vector2 center = Game1.ScreenSize / 2f;
            int width = 430;
            int height = 58;
            int x = (int)center.X - width / 2;
            int y = (int)center.Y - 12 + index * 78 - (total - 1) * 39;
            return new UiButton(new Rectangle(x, y, width, height), text);
        }
        private void DrawTitle(SpriteBatch spriteBatch, Texture2D pixel)
        {
            DrawCenteredText(spriteBatch, pixel, "SPACEBURST", Game1.ScreenSize.X / 2f, 120f, Color.White, 4f);
            DrawCenteredText(spriteBatch, pixel, "PROCEDURAL SIDE SCROLLER", Game1.ScreenSize.X / 2f, 182f, Color.White * 0.68f, 2f);
            DrawCenteredText(spriteBatch, pixel, string.Concat("HIGH ", PlayerStatus.HighScore.ToString()), Game1.ScreenSize.X / 2f, 230f, Color.White * 0.85f, 2f);

            List<UiButton> buttons = GetTitleButtons();
            for (int i = 0; i < buttons.Count; i++)
                DrawButton(spriteBatch, pixel, buttons[i], i == titleSelection);

            string medalText = medals.CampaignClear
                ? (medals.PerfectCampaign ? "PERFECT CAMPAIGN MEDAL UNLOCKED" : "CAMPAIGN CLEAR MEDAL UNLOCKED")
                : "NO CAMPAIGN MEDALS YET";
            DrawCenteredText(spriteBatch, pixel, medalText, Game1.ScreenSize.X / 2f, 520f, Color.White * 0.65f, 1.5f);
        }

        private void DrawPause(SpriteBatch spriteBatch, Texture2D pixel)
        {
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Black * 0.45f);
            DrawCenteredText(spriteBatch, pixel, "PAUSED", Game1.ScreenSize.X / 2f, 148f, Color.White, 3f);
            List<UiButton> buttons = GetPauseButtons();
            for (int i = 0; i < buttons.Count; i++)
                DrawButton(spriteBatch, pixel, buttons[i], i == pauseSelection);
        }

        private void DrawHelp(SpriteBatch spriteBatch, Texture2D pixel)
        {
            spriteBatch.Draw(pixel, new Rectangle(90, 90, Game1.VirtualWidth - 180, Game1.VirtualHeight - 180), Color.Black * 0.75f);
            DrawCenteredText(spriteBatch, pixel, "HELP", Game1.ScreenSize.X / 2f, 112f, Color.White, 3f);
            DrawCenteredText(spriteBatch, pixel, string.Concat("PAGE ", (helpPageIndex + 1).ToString(), " / 4"), Game1.ScreenSize.X / 2f, 152f, Color.White * 0.7f, 1.5f);

            switch (helpPageIndex)
            {
                case 0:
                    DrawHelpPage(spriteBatch, pixel, "CONTROLS\nWASD MOVE  ARROWS AIM  SPACE FIRE\nQ E SWITCH STYLE  ESC PAUSE  F1 HELP\nANDROID LEFT PAD MOVE  RIGHT PAD AIM FIRE", 186f);
                    break;
                case 1:
                    DrawHelpPage(spriteBatch, pixel, "POWERUPS\nP DROPS FROM ELIGIBLE ENEMIES\nLEVELS 0 TO 3 UPGRADE THE ACTIVE STYLE\nAFTER LEVEL 3 THE NEXT P UNLOCKS THE NEXT STYLE\nPOWERUPS PERSIST ACROSS STAGES IN THE RUN", 186f);
                    DrawWeaponIcons(spriteBatch, pixel, 410f, 0, 5);
                    break;
                case 2:
                    DrawHelpPage(spriteBatch, pixel, "LIVES AND SHIPS\nSHIPS GIVE IN PLACE RESPAWNS\nWHEN SHIPS HIT ZERO THE NEXT DEATH COSTS A LIFE\nLOSING A LIFE RESTARTS THE WHOLE STAGE\nDEATH ALSO WEAKENS YOUR CURRENT WEAPON STYLE", 186f);
                    DrawWeaponIcons(spriteBatch, pixel, 410f, 5, 5);
                    break;
                default:
                    DrawHelpPage(spriteBatch, pixel, "RANDOM EVENTS\nMETEOR SHOWER  DEBRIS DRIFT  COMET SWARM  SOLAR FLARE\nWATCH THE HUD ALERTS AND COLOR SHIFTS\nTHE PITY BAR SHOWS POWERUP DROP MOMENTUM", 186f);
                    break;
            }

            DrawCenteredText(spriteBatch, pixel, "LEFT RIGHT TO CHANGE PAGE  ENTER ESC F1 TO CLOSE", Game1.ScreenSize.X / 2f, Game1.VirtualHeight - 120f, Color.White * 0.75f, 1.5f);
        }

        private void DrawWeaponIcons(SpriteBatch spriteBatch, Texture2D pixel, float y, int startIndex, int count)
        {
            int safeCount = Math.Min(count, WeaponCatalog.StyleOrder.Count - startIndex);
            float spacing = 200f;
            float startX = Game1.ScreenSize.X / 2f - ((safeCount - 1) * spacing) / 2f;
            for (int i = 0; i < safeCount; i++)
            {
                WeaponStyleId styleId = WeaponCatalog.StyleOrder[startIndex + i];
                WeaponStyleDefinition style = WeaponCatalog.GetStyle(styleId);
                Vector2 iconPos = new Vector2(startX + spacing * i, y);
                PixelArtRenderer.DrawRows(spriteBatch, pixel, style.IconRows, iconPos, 6f, ColorUtil.ParseHex(style.PrimaryColor, Color.White), ColorUtil.ParseHex(style.SecondaryColor, Color.LightBlue), ColorUtil.ParseHex(style.AccentColor, Color.Orange), true);
                DrawCenteredText(spriteBatch, pixel, style.DisplayName, iconPos.X, y + 36f, Color.White, 1.2f);
            }
        }

        private void DrawHelpPage(SpriteBatch spriteBatch, Texture2D pixel, string text, float y)
        {
            Vector2 size = BitmapFontRenderer.Measure(text, 1.6f);
            BitmapFontRenderer.Draw(spriteBatch, pixel, text, new Vector2(Game1.ScreenSize.X / 2f - size.X / 2f, y), Color.White, 1.6f);
        }

        private void DrawHud(SpriteBatch spriteBatch, Texture2D pixel)
        {
            DrawPanel(spriteBatch, pixel, new Rectangle(12, 10, 320, 88), Color.Black * 0.22f, Color.White * 0.18f);
            DrawPanel(spriteBatch, pixel, new Rectangle(Game1.VirtualWidth - 356, 10, 344, 88), Color.Black * 0.22f, Color.White * 0.18f);
            DrawPanel(spriteBatch, pixel, new Rectangle(344, 10, Game1.VirtualWidth - 688, 88), Color.Black * 0.18f, Color.White * 0.14f);

            BitmapFontRenderer.Draw(spriteBatch, pixel, string.Concat("LIVES ", PlayerStatus.Lives.ToString()), new Vector2(24f, 20f), Color.White, 2f);
            BitmapFontRenderer.Draw(spriteBatch, pixel, string.Concat("SHIPS ", PlayerStatus.Ships.ToString()), new Vector2(24f, 46f), Color.White, 2f);
            BitmapFontRenderer.Draw(spriteBatch, pixel, string.Concat("SCORE ", PlayerStatus.Score.ToString()), new Vector2(Game1.VirtualWidth - 338f, 20f), Color.White, 2f);
            BitmapFontRenderer.Draw(spriteBatch, pixel, string.Concat("MULTI ", PlayerStatus.Multiplier.ToString()), new Vector2(Game1.VirtualWidth - 338f, 46f), Color.White, 2f);

            DrawCenteredText(spriteBatch, pixel, string.Concat("STAGE ", currentStageNumber.ToString("00")), Game1.ScreenSize.X / 2f, 18f, Color.White, 1.8f);
            DrawStyleHud(spriteBatch, pixel);

            if (!string.IsNullOrEmpty(activeEventWarning))
                DrawCenteredText(spriteBatch, pixel, activeEventWarning, Game1.ScreenSize.X / 2f, 70f, Color.Orange, 1.4f);

            if (activeBoss != null && !activeBoss.IsExpired)
                DrawBossHealthBar(spriteBatch, pixel, activeBoss);
        }

        private void DrawStyleHud(SpriteBatch spriteBatch, Texture2D pixel)
        {
            WeaponInventoryState inventory = PlayerStatus.RunProgress.Weapons;
            WeaponStyleDefinition activeStyle = WeaponCatalog.GetStyle(inventory.ActiveStyle);
            PixelArtRenderer.DrawRows(spriteBatch, pixel, activeStyle.IconRows, new Vector2(388f, 54f), 5f, ColorUtil.ParseHex(activeStyle.PrimaryColor, Color.White), ColorUtil.ParseHex(activeStyle.SecondaryColor, Color.LightBlue), ColorUtil.ParseHex(activeStyle.AccentColor, Color.Orange), true);
            BitmapFontRenderer.Draw(spriteBatch, pixel, activeStyle.DisplayName, new Vector2(420f, 20f), Color.White, 2f);

            for (int i = 0; i < 4; i++)
            {
                Color pip = i <= inventory.ActiveLevel ? ColorUtil.ParseHex(activeStyle.AccentColor, Color.Orange) : Color.White * 0.18f;
                spriteBatch.Draw(pixel, new Rectangle(422 + i * 20, 52, 14, 14), pip);
            }

            IReadOnlyList<WeaponStyleId> ownedStyles = inventory.OwnedStyles;
            for (int i = 0; i < ownedStyles.Count; i++)
            {
                WeaponStyleDefinition style = WeaponCatalog.GetStyle(ownedStyles[i]);
                float x = 550f + i * 56f;
                PixelArtRenderer.DrawRows(spriteBatch, pixel, style.IconRows, new Vector2(x, 42f), 2.8f, ColorUtil.ParseHex(style.PrimaryColor, Color.White), ColorUtil.ParseHex(style.SecondaryColor, Color.LightBlue), ColorUtil.ParseHex(style.AccentColor, Color.Orange), true);
                if (ownedStyles[i] == inventory.ActiveStyle)
                    spriteBatch.Draw(pixel, new Rectangle((int)x - 16, 64, 32, 3), ColorUtil.ParseHex(style.AccentColor, Color.Orange));
            }

            Rectangle pityBounds = new Rectangle(876, 52, 160, 10);
            spriteBatch.Draw(pixel, pityBounds, Color.White * 0.12f);
            spriteBatch.Draw(pixel, new Rectangle(pityBounds.X, pityBounds.Y, (int)(pityBounds.Width * PlayerStatus.RunProgress.Powerups.PityMeter), pityBounds.Height), Color.Orange);
            BitmapFontRenderer.Draw(spriteBatch, pixel, "PITY", new Vector2(876f, 20f), Color.White, 1.4f);
        }

        private void DrawBossHealthBar(SpriteBatch spriteBatch, Texture2D pixel, BossEnemy boss)
        {
            Rectangle bounds = new Rectangle(320, 94, 640, 18);
            spriteBatch.Draw(pixel, bounds, Color.White * 0.12f);
            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, (int)(bounds.Width * boss.HealthRatio), bounds.Height), Color.OrangeRed);
            DrawCenteredText(spriteBatch, pixel, boss.DisplayName, Game1.ScreenSize.X / 2f, 118f, Color.White, 1.5f);
        }

        private void DrawCenteredBanner(SpriteBatch spriteBatch, Texture2D pixel, string text, Color color, float scale)
        {
            Vector2 size = BitmapFontRenderer.Measure(text, scale);
            BitmapFontRenderer.Draw(spriteBatch, pixel, text, Game1.ScreenSize / 2f - size / 2f, color, scale);
        }

        private void DrawButton(SpriteBatch spriteBatch, Texture2D pixel, UiButton button, bool selected)
        {
            Color fill = selected ? Color.White * 0.18f : Color.White * 0.08f;
            Color border = selected ? Color.Orange : Color.White * 0.35f;

            DrawPanel(spriteBatch, pixel, button.Bounds, fill, border);
            Vector2 textSize = BitmapFontRenderer.Measure(button.Text, 1.8f);
            Vector2 textPosition = new Vector2(button.Bounds.Center.X - textSize.X / 2f, button.Bounds.Center.Y - textSize.Y / 2f);
            BitmapFontRenderer.Draw(spriteBatch, pixel, button.Text, textPosition, Color.White, 1.8f);
        }

        private void DrawCenteredText(SpriteBatch spriteBatch, Texture2D pixel, string text, float x, float y, Color color, float scale)
        {
            Vector2 size = BitmapFontRenderer.Measure(text, scale);
            BitmapFontRenderer.Draw(spriteBatch, pixel, text, new Vector2(x - size.X / 2f, y), color, scale);
        }

        private void DrawPanel(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, Color fill, Color border)
        {
            spriteBatch.Draw(pixel, bounds, fill);
            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), border);
            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2), border);
            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, 2, bounds.Height), border);
            spriteBatch.Draw(pixel, new Rectangle(bounds.Right - 2, bounds.Y, 2, bounds.Height), border);
        }

        private SectionDefinition GetActiveSection()
        {
            if (currentStage?.Sections == null || currentStage.Sections.Count == 0)
                return null;

            for (int i = currentStage.Sections.Count - 1; i >= 0; i--)
            {
                if (currentStage.Sections[i].StartSeconds <= stageElapsedSeconds)
                    return currentStage.Sections[i];
            }

            return currentStage.Sections[0];
        }

        private static string GetEventLabel(RandomEventType eventType)
        {
            switch (eventType)
            {
                case RandomEventType.MeteorShower:
                    return "METEOR SHOWER";
                case RandomEventType.CometSwarm:
                    return "COMET SWARM";
                case RandomEventType.SolarFlare:
                    return "SOLAR FLARE";
                case RandomEventType.DebrisDrift:
                    return "DEBRIS DRIFT";
                default:
                    return string.Empty;
            }
        }

        private static float GetEventDuration(RandomEventType eventType, float intensity)
        {
            switch (eventType)
            {
                case RandomEventType.SolarFlare:
                    return 3.2f + intensity * 1.4f;
                case RandomEventType.CometSwarm:
                    return 4.6f + intensity * 1.2f;
                case RandomEventType.MeteorShower:
                    return 5.2f + intensity * 1.4f;
                default:
                    return 4.4f + intensity;
            }
        }

        private static float GetEventSpawnInterval(RandomEventType eventType, float intensity)
        {
            float scale = MathHelper.Clamp(intensity, 0.5f, 2f);
            switch (eventType)
            {
                case RandomEventType.CometSwarm:
                    return 0.18f / scale;
                case RandomEventType.MeteorShower:
                    return 0.26f / scale;
                default:
                    return 0.34f / scale;
            }
        }

        private static ProceduralSpriteDefinition CreateEventProjectileDefinition(string primary, string secondary, string accent, IList<string> rows)
        {
            return new ProceduralSpriteDefinition
            {
                Id = "EventProjectile",
                PixelScale = 4,
                PrimaryColor = primary,
                SecondaryColor = secondary,
                AccentColor = accent,
                Rows = rows.ToList(),
            };
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

        private sealed class ScheduledEvent
        {
            public float TriggerAtSeconds { get; set; }
            public RandomEventWindowDefinition Window { get; set; }
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
