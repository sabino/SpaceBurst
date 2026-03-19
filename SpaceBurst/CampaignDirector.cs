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
        private const float StageTransitionSeconds = 2.2f;
        private const float BossApproachSeconds = 1.0f;
        private const float PlayerSafetyClearRadius = 220f;
        private const float RewindCapacitySeconds = 8f;
        private const float RewindSnapshotInterval = 1f / 30f;
        private const int HelpPageCount = 7;
        private const int AboutHelpPageIndex = 5;
        private const float TitleIntroDurationSeconds = 4.8f;
        private const float TitleIntroSkipDurationSeconds = 1.05f;

        private const string AboutHelpText =
            "ABOUT SPACEBURST\n" +
            "DEVELOPED BY SABINO SOFTWARE\n" +
            "PROCEDURAL SIDE SCROLLING SHOOTER WITH DETERMINISTIC REWIND,\n" +
            "UPGRADE DRAFTS, PROCEDURAL AUDIO, AND CROSS PLATFORM RELEASE BUILDS.\n" +
            "SOURCE REPOSITORY  GITHUB.COM/SABINO/SPACEBURST\n" +
            "LICENSE  THE UNLICENSE\n" +
            "TURN TO THE NEXT PAGE TO READ THE FULL LICENSE.";

        private const string UnlicenseText =
            "THE UNLICENSE\n" +
            "THIS IS FREE AND UNENCUMBERED SOFTWARE RELEASED INTO THE PUBLIC DOMAIN.\n\n" +
            "ANYONE IS FREE TO COPY, MODIFY, PUBLISH, USE, COMPILE, SELL, OR DISTRIBUTE THIS SOFTWARE,\n" +
            "EITHER IN SOURCE CODE FORM OR AS A COMPILED BINARY, FOR ANY PURPOSE, COMMERCIAL OR\n" +
            "NON COMMERCIAL, AND BY ANY MEANS.\n\n" +
            "IN JURISDICTIONS THAT RECOGNIZE COPYRIGHT LAWS, THE AUTHORS DEDICATE ANY AND ALL\n" +
            "COPYRIGHT INTEREST IN THE SOFTWARE TO THE PUBLIC DOMAIN. WE MAKE THIS DEDICATION FOR THE\n" +
            "BENEFIT OF THE PUBLIC AT LARGE AND TO THE DETRIMENT OF OUR HEIRS AND SUCCESSORS. WE INTEND\n" +
            "THIS DEDICATION TO BE AN OVERT ACT OF RELINQUISHMENT IN PERPETUITY OF ALL PRESENT AND FUTURE\n" +
            "RIGHTS TO THIS SOFTWARE UNDER COPYRIGHT LAW.\n\n" +
            "THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,\n" +
            "INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR\n" +
            "PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM,\n" +
            "DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING\n" +
            "FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.\n\n" +
            "FOR MORE INFORMATION, PLEASE REFER TO UNLICENSE.ORG";

        private readonly CampaignRepository repository = new CampaignRepository();
        private readonly OptionsData options;
        private readonly MedalProgress medals;
        private readonly List<ScheduledSpawn> scheduledSpawns = new List<ScheduledSpawn>();
        private readonly List<ScheduledEvent> scheduledEvents = new List<ScheduledEvent>();
        private readonly DeterministicRngState gameplayRandom = new DeterministicRngState(1u);
        private readonly List<RunSaveData> rewindFrames = new List<RunSaveData>();
        private readonly List<UpgradeDraftCard> draftCards = new List<UpgradeDraftCard>();
        private readonly Random titleVisualRandom = new Random(unchecked(Environment.TickCount * 397));

        private StageDefinition currentStage;
        private BossEnemy activeBoss;
        private GameFlowState state = GameFlowState.Title;
        private GameFlowState helpReturnState = GameFlowState.Title;
        private GameFlowState slotReturnState = GameFlowState.Title;
        private GameFlowState draftReturnState = GameFlowState.Playing;
        private GameFlowState pauseReturnState = GameFlowState.Playing;
        private int currentStageNumber = 1;
        private int currentSectionIndex;
        private int titleSelection;
        private int pauseSelection;
        private int optionsSelection;
        private int slotSelection;
        private int helpPageIndex;
        private int draftSelection;
        private WeaponStyleId draftChargeStyle = WeaponStyleId.Pulse;
        private float stageElapsedSeconds;
        private float stateTimer;
        private float activeEventTimer;
        private float activeEventSpawnTimer;
        private float rewindCaptureTimer;
        private float rewindMeterSeconds = RewindCapacitySeconds;
        private float rewindHoldSeconds;
        private float rewindStepAccumulator;
        private bool stageHadDeath;
        private bool campaignHadDeath;
        private string bannerText = string.Empty;
        private string activeEventWarning = string.Empty;
        private RandomEventType activeEventType = RandomEventType.None;
        private float activeEventIntensity;
        private int transitionTargetStageNumber;
        private bool transitionToBoss;
        private float transitionScrollFrom;
        private float transitionScrollTo;
        private float transitionHudBlend;
        private float bossApproachTimer;
        private float draftTimer;
        private bool draftFromTutorial;
        private bool tutorialReplayMode;
        private TutorialStep tutorialStep;
        private float tutorialProgressSeconds;
        private bool titleIntroActive = true;
        private bool titleIntroSeen;
        private bool titleIntroExploded;
        private float titleIntroTimer = TitleIntroDurationSeconds;
        private readonly List<IntroPixel> introPixels = new List<IntroPixel>();
        private Color titleIntroBackgroundColor = new Color(7, 12, 24);
        private Color titleIntroGlowColorA = new Color(110, 193, 255);
        private Color titleIntroGlowColorB = new Color(246, 198, 116);
        private Color titleIntroPrimaryColor = Color.White;
        private Color titleIntroSecondaryColor = new Color(110, 193, 255);
        private Color titleIntroPromptColor = Color.White;

#if ANDROID
        private static readonly int[] UiScaleOptions = { 80, 90, 100, 115, 130, 150, 170, 190, 220 };
        private static readonly int[] WorldScaleOptions = { 70, 80, 90, 100, 110, 120, 130, 140 };
#else
        private static readonly int[] UiScaleOptions = { 80, 90, 100, 110, 120, 130, 140, 160 };
        private static readonly int[] WorldScaleOptions = { 70, 80, 90, 100, 110, 120, 135, 150, 160 };
#endif

        private static readonly IntroPalette[] IntroPalettes =
        {
            new IntroPalette(new Color(8, 12, 26), new Color(88, 196, 255), new Color(255, 188, 96), Color.White, new Color(88, 196, 255), new Color(255, 188, 96)),
            new IntroPalette(new Color(14, 8, 24), new Color(201, 116, 255), new Color(86, 237, 193), Color.White, new Color(201, 116, 255), new Color(86, 237, 193)),
            new IntroPalette(new Color(8, 16, 18), new Color(123, 229, 176), new Color(244, 232, 120), Color.White, new Color(123, 229, 176), new Color(244, 232, 120)),
            new IntroPalette(new Color(16, 10, 18), new Color(255, 132, 165), new Color(116, 170, 255), Color.White, new Color(255, 132, 165), new Color(116, 170, 255)),
            new IntroPalette(new Color(6, 14, 30), new Color(92, 160, 255), new Color(152, 246, 255), Color.White, new Color(152, 246, 255), new Color(92, 160, 255)),
        };

        private struct IntroPixel
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public Vector2 Target;
            public Color Color;
        }

        private readonly struct IntroPalette
        {
            public IntroPalette(Color background, Color glowA, Color glowB, Color primary, Color secondary, Color prompt)
            {
                Background = background;
                GlowA = glowA;
                GlowB = glowB;
                Primary = primary;
                Secondary = secondary;
                Prompt = prompt;
            }

            public Color Background { get; }
            public Color GlowA { get; }
            public Color GlowB { get; }
            public Color Primary { get; }
            public Color Secondary { get; }
            public Color Prompt { get; }
        }

        public CampaignDirector()
        {
            options = PersistentStorage.LoadOptions();
            medals = PersistentStorage.LoadMedals();
        }

        public bool ShouldDrawWorld
        {
            get { return state != GameFlowState.Title && currentStage != null; }
        }

        public bool ShouldDrawTouchControls
        {
            get { return (state == GameFlowState.Playing || state == GameFlowState.StageTransition || state == GameFlowState.Tutorial) && ShouldDrawWorld; }
        }

        public float CurrentScrollSpeed
        {
            get
            {
                if (currentStage == null || state == GameFlowState.Title || state == GameFlowState.Help || state == GameFlowState.Paused || state == GameFlowState.GameOver || state == GameFlowState.CampaignComplete)
                    return 0f;

                if (state == GameFlowState.SaveSlots || state == GameFlowState.LoadSlots || state == GameFlowState.Options || state == GameFlowState.UpgradeDraft)
                    return 0f;

                if (state == GameFlowState.Tutorial)
                    return 64f;

                if (state == GameFlowState.StageTransition)
                {
                    return GetTransitionScrollSpeed();
                }

                if (activeBoss != null && currentStage.Boss != null)
                    return currentStage.Boss.ArenaScrollSpeed;

                return GetCurrentStageScrollSpeed();
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
                float runFactor = MathHelper.Clamp(PlayerStatus.RunProgress.PowerBudget * 0.02f, 0f, 0.28f);
                return activeBoss != null ? MathHelper.Clamp(stageFactor + runFactor + 0.15f, 0f, 1f) : MathHelper.Clamp(stageFactor + runFactor, 0f, 1f);
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
                return (section?.PowerDropBonusChance ?? 0f) + PlayerStatus.RunProgress.DropBonusChance;
            }
        }

        public DeterministicRngState GameplayRandom
        {
            get { return gameplayRandom; }
        }

        public float PowerupMagnetStrength
        {
            get { return state == GameFlowState.StageTransition ? 320f + 420f * TransitionWarpStrength : 0f; }
        }

        public VisualPreset VisualPreset
        {
            get { return options.VisualPreset; }
        }

        public AudioQualityPreset AudioQualityPreset
        {
            get { return options.AudioQualityPreset; }
        }

        public ScreenShakeStrength ScreenShakeStrength
        {
            get { return options.ScreenShakeStrength; }
        }

        public int UiScalePercent
        {
            get { return options.UiScalePercent; }
        }

        public int WorldScalePercent
        {
            get { return options.WorldScalePercent; }
        }

        public FontTheme FontTheme
        {
            get { return options.FontTheme; }
        }

        public float MasterVolume
        {
            get { return options.MasterVolume; }
        }

        public float MusicVolume
        {
            get { return options.MusicVolume; }
        }

        public float SfxVolume
        {
            get { return options.SfxVolume; }
        }

        public GameFlowState CurrentState
        {
            get { return state; }
        }

        public int CurrentStageNumber
        {
            get { return currentStage != null ? currentStageNumber : 0; }
        }

        public int TransitionTargetStageNumber
        {
            get { return transitionTargetStageNumber; }
        }

        public int CurrentSectionIndex
        {
            get { return Math.Max(0, currentSectionIndex); }
        }

        public float CurrentSectionProgress
        {
            get
            {
                SectionDefinition section = GetActiveSection();
                if (section == null)
                    return 0f;

                float duration = Math.Max(0.01f, section.DurationSeconds);
                return MathHelper.Clamp((stageElapsedSeconds - section.StartSeconds) / duration, 0f, 1f);
            }
        }

        public bool HasActiveBoss
        {
            get { return activeBoss != null && !activeBoss.IsExpired; }
        }

        public bool TransitionToBoss
        {
            get { return transitionToBoss; }
        }

        public bool EnableBloom
        {
            get { return options.EnableBloom && options.VisualPreset != VisualPreset.Low; }
        }

        public bool EnableShockwaves
        {
            get { return options.EnableShockwaves && options.VisualPreset != VisualPreset.Low; }
        }

        public bool EnableNeonOutlines
        {
            get { return options.EnableNeonOutlines && options.VisualPreset != VisualPreset.Low; }
        }

        public float TransitionWarpStrength
        {
            get
            {
                if (state != GameFlowState.StageTransition)
                    return 0f;

                float progress = GetTransitionProgress();
                if (progress < 0.2f)
                    return progress / 0.2f * 0.35f;
                if (progress < 0.75f)
                    return MathHelper.Lerp(0.35f, 1f, (progress - 0.2f) / 0.55f);
                return MathHelper.Lerp(1f, 0.25f, (progress - 0.75f) / 0.25f);
            }
        }

        public float RewindVisualStrength
        {
            get
            {
                if (rewindHoldSeconds <= 0f || state != GameFlowState.Playing && state != GameFlowState.Tutorial)
                    return 0f;

                return MathHelper.Clamp(GetRewindSpeedMultiplier(rewindHoldSeconds) / 2.5f, 0f, 1f);
            }
        }

        public void Load()
        {
            LoadRepository();
            FinishBootToTitle();
        }

        public void LoadRepository()
        {
            repository.Load();
        }

        public void FinishBootToTitle()
        {
            titleIntroSeen = true;
            EnterTitle(false);
        }

        public bool LoadRunSlotForCapture(int slotNumber)
        {
            RunSaveData save = PersistentStorage.LoadRunSlot(slotNumber);
            if (save == null)
                return false;

            RestoreRunSaveData(save, false, false);
            if (state == GameFlowState.Paused)
                state = save.State == GameFlowState.Tutorial ? GameFlowState.Tutorial : GameFlowState.Playing;
            return true;
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
                case GameFlowState.Help:
                    UpdateHelp();
                    break;
                case GameFlowState.Paused:
                    UpdatePause();
                    break;
                case GameFlowState.SaveSlots:
                    UpdateSaveSlots(true);
                    break;
                case GameFlowState.LoadSlots:
                    UpdateSaveSlots(false);
                    break;
                case GameFlowState.LevelIntro:
                    UpdateTimedState(GameFlowState.Playing);
                    break;
                case GameFlowState.Tutorial:
                    UpdateTutorial();
                    break;
                case GameFlowState.StageTransition:
                    UpdateStageTransition();
                    break;
                case GameFlowState.UpgradeDraft:
                    UpdateUpgradeDraft();
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
                    if (slotReturnState != GameFlowState.Title)
                        DrawHud(spriteBatch, pixel);
                    DrawOptions(spriteBatch, pixel);
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
                case GameFlowState.SaveSlots:
                case GameFlowState.LoadSlots:
                    if (slotReturnState != GameFlowState.Title)
                        DrawHud(spriteBatch, pixel);
                    DrawSaveSlots(spriteBatch, pixel, state == GameFlowState.SaveSlots);
                    break;
                case GameFlowState.Playing:
                    DrawHud(spriteBatch, pixel);
                    DrawRewindOverlay(spriteBatch, pixel);
                    break;
                case GameFlowState.LevelIntro:
                    DrawHud(spriteBatch, pixel);
                    DrawCenteredBanner(spriteBatch, pixel, bannerText, Color.White, 3f);
                    break;
                case GameFlowState.Tutorial:
                    DrawHud(spriteBatch, pixel);
                    DrawRewindOverlay(spriteBatch, pixel);
                    DrawTutorialOverlay(spriteBatch, pixel);
                    break;
                case GameFlowState.StageTransition:
                    DrawHud(spriteBatch, pixel);
                    DrawTransitionOverlay(spriteBatch, pixel);
                    break;
                case GameFlowState.UpgradeDraft:
                    DrawHud(spriteBatch, pixel);
                    DrawUpgradeDraft(spriteBatch, pixel);
                    break;
                case GameFlowState.GameOver:
                    DrawHud(spriteBatch, pixel);
#if ANDROID
                    DrawCenteredBanner(spriteBatch, pixel, string.Concat("GAME OVER\nSCORE ", PlayerStatus.Score.ToString(), "\nHIGH ", PlayerStatus.HighScore.ToString(), "\nTAP TO CONTINUE"), Color.White, 3f);
#else
                    DrawCenteredBanner(spriteBatch, pixel, string.Concat("GAME OVER\nSCORE ", PlayerStatus.Score.ToString(), "\nHIGH ", PlayerStatus.HighScore.ToString(), "\nPRESS ENTER"), Color.White, 3f);
#endif
                    break;
                case GameFlowState.CampaignComplete:
                    DrawHud(spriteBatch, pixel);
#if ANDROID
                    DrawCenteredBanner(spriteBatch, pixel, string.Concat("CAMPAIGN COMPLETE\nSCORE ", PlayerStatus.Score.ToString(), "\nHIGH ", PlayerStatus.HighScore.ToString(), "\nTAP TO CONTINUE"), Color.White, 3f);
#else
                    DrawCenteredBanner(spriteBatch, pixel, string.Concat("CAMPAIGN COMPLETE\nSCORE ", PlayerStatus.Score.ToString(), "\nHIGH ", PlayerStatus.HighScore.ToString(), "\nPRESS ENTER"), Color.White, 3f);
#endif
                    break;
            }
        }
        private void UpdateTitle()
        {
            float deltaSeconds = (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            UpdateTitleIntro(deltaSeconds);
            if (titleIntroActive)
                return;

            List<UiButton> buttons = GetTitleButtons();
            UpdateVerticalSelection(ref titleSelection, buttons.Count);
            HandlePointerSelection(buttons, ref titleSelection);

            if (Input.WasHelpPressed())
            {
                helpReturnState = GameFlowState.Title;
                helpPageIndex = 0;
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
            pauseSelection = Math.Clamp(pauseSelection, 0, buttons.Count - 1);
            HandlePointerSelection(buttons, ref pauseSelection);

            bool tutorialPause = pauseReturnState == GameFlowState.Tutorial;

            if (Input.WasCancelPressed())
            {
                state = pauseReturnState;
                return;
            }

            if (Input.WasHelpPressed())
            {
                helpReturnState = GameFlowState.Paused;
                helpPageIndex = 0;
                state = GameFlowState.Help;
                return;
            }

            if (!Input.WasConfirmPressed() && !Input.WasPrimaryActionPressed())
                return;

            switch (pauseSelection)
            {
                case 1:
                    slotReturnState = GameFlowState.Paused;
                    slotSelection = 0;
                    state = GameFlowState.SaveSlots;
                    break;
                case 2:
                    slotReturnState = GameFlowState.Paused;
                    slotSelection = 0;
                    state = GameFlowState.LoadSlots;
                    break;
                case 3:
                    slotReturnState = GameFlowState.Paused;
                    optionsSelection = 0;
                    state = GameFlowState.Options;
                    break;
                case 4:
                    helpReturnState = GameFlowState.Paused;
                    state = GameFlowState.Help;
                    break;
                case 5:
                    if (tutorialPause)
                    {
                        options.TutorialCompleted = true;
                        PersistentStorage.SaveOptions(options);
                        if (tutorialReplayMode)
                            EnterTitle(false);
                        else
                            BeginFreshCampaign();
                    }
                    else
                    {
                        PlayerStatus.FinalizeRun();
                        EnterTitle(false);
                    }
                    break;
                case 6:
                    PlayerStatus.FinalizeRun();
                    EnterTitle(false);
                    break;
                default:
                    state = pauseReturnState;
                    break;
            }
        }

        private void UpdateOptions()
        {
            if (Input.WasCancelPressed())
            {
                PersistentStorage.SaveOptions(options);
                state = slotReturnState;
                return;
            }

            Rectangle[] optionBounds = GetOptionRowBounds();
            UpdateVerticalSelection(ref optionsSelection, optionBounds.Length);
            HandlePointerSelection(optionBounds, ref optionsSelection);

            int delta = 0;
            int swipeDelta = Input.ConsumeMenuHorizontalDelta();
            if (swipeDelta != 0)
                delta = swipeDelta;
            else if (Input.WasNavigateLeftPressed())
                delta = -1;
            else if (Input.WasNavigateRightPressed() || Input.WasConfirmPressed() || Input.WasPrimaryActionPressed())
                delta = 1;

#if ANDROID
            if (delta != 0 && Input.WasPrimaryActionPressed() && optionsSelection >= 0 && optionsSelection < optionBounds.Length && optionBounds[optionsSelection].Contains(Input.PointerPosition))
                delta = Input.PointerPosition.X < optionBounds[optionsSelection].Center.X ? -1 : 1;
#endif

            if (delta == 0)
                return;

            switch (optionsSelection)
            {
#if ANDROID
                case 0:
                    options.UiScalePercent = AdjustOptionPercent(options.UiScalePercent, delta, UiScaleOptions);
                    break;
                case 1:
                    options.WorldScalePercent = AdjustOptionPercent(options.WorldScalePercent, delta, WorldScaleOptions);
                    break;
                case 2:
                    options.FontTheme = options.FontTheme == FontTheme.Compact ? FontTheme.Readable : FontTheme.Compact;
                    break;
                case 3:
                    options.VisualPreset = (VisualPreset)(((int)options.VisualPreset + 3 + delta) % 3);
                    break;
                case 4:
                    options.EnableBloom = !options.EnableBloom;
                    break;
                case 5:
                    options.EnableShockwaves = !options.EnableShockwaves;
                    break;
                case 6:
                    options.EnableNeonOutlines = !options.EnableNeonOutlines;
                    break;
                case 7:
                    options.ScreenShakeStrength = (ScreenShakeStrength)(((int)options.ScreenShakeStrength + 3 + delta) % 3);
                    break;
                case 8:
                    options.AudioQualityPreset = (AudioQualityPreset)(((int)options.AudioQualityPreset + 3 + delta) % 3);
                    Game1.Instance.ApplyAudioQuality(options.AudioQualityPreset);
                    break;
                case 9:
                    options.MasterVolume = AdjustVolume(options.MasterVolume, delta);
                    break;
                case 10:
                    options.MusicVolume = AdjustVolume(options.MusicVolume, delta);
                    break;
                case 11:
                    options.SfxVolume = AdjustVolume(options.SfxVolume, delta);
                    break;
                case 12:
                    options.AutoUpgradeDraft = !options.AutoUpgradeDraft;
                    break;
                case 13:
                    options.ShowHelpHints = !options.ShowHelpHints;
                    break;
#else
                case 0:
                    options.DisplayMode = options.DisplayMode == DesktopDisplayMode.BorderlessFullscreen
                        ? DesktopDisplayMode.Windowed
                        : DesktopDisplayMode.BorderlessFullscreen;
                    Game1.Instance.ApplyDisplayMode(options.DisplayMode);
                    break;
                case 1:
                    options.UiScalePercent = AdjustOptionPercent(options.UiScalePercent, delta, UiScaleOptions);
                    break;
                case 2:
                    options.WorldScalePercent = AdjustOptionPercent(options.WorldScalePercent, delta, WorldScaleOptions);
                    break;
                case 3:
                    options.FontTheme = options.FontTheme == FontTheme.Compact ? FontTheme.Readable : FontTheme.Compact;
                    break;
                case 4:
                    options.VisualPreset = (VisualPreset)(((int)options.VisualPreset + 3 + delta) % 3);
                    break;
                case 5:
                    options.EnableBloom = !options.EnableBloom;
                    break;
                case 6:
                    options.EnableShockwaves = !options.EnableShockwaves;
                    break;
                case 7:
                    options.EnableNeonOutlines = !options.EnableNeonOutlines;
                    break;
                case 8:
                    options.ScreenShakeStrength = (ScreenShakeStrength)(((int)options.ScreenShakeStrength + 3 + delta) % 3);
                    break;
                case 9:
                    options.AudioQualityPreset = (AudioQualityPreset)(((int)options.AudioQualityPreset + 3 + delta) % 3);
                    Game1.Instance.ApplyAudioQuality(options.AudioQualityPreset);
                    break;
                case 10:
                    options.MasterVolume = AdjustVolume(options.MasterVolume, delta);
                    break;
                case 11:
                    options.MusicVolume = AdjustVolume(options.MusicVolume, delta);
                    break;
                case 12:
                    options.SfxVolume = AdjustVolume(options.SfxVolume, delta);
                    break;
                case 13:
                    options.AutoUpgradeDraft = !options.AutoUpgradeDraft;
                    break;
                case 14:
                    options.ShowHelpHints = !options.ShowHelpHints;
                    break;
#endif
            }
        }

        private void UpdateSaveSlots(bool saving)
        {
            UpdateVerticalSelection(ref slotSelection, 4);
            HandlePointerSelection(GetSaveSlotSelectionBounds(), ref slotSelection);

            if (Input.WasCancelPressed())
            {
                state = slotReturnState;
                return;
            }

            if (!Input.WasConfirmPressed() && !Input.WasPrimaryActionPressed())
                return;

            if (slotSelection == 3)
            {
                state = slotReturnState;
                return;
            }

            if (saving)
            {
                PersistentStorage.SaveRunSlot(slotSelection + 1, CaptureRunSaveData(slotSelection + 1, true));
            }
            else
            {
                RunSaveData save = PersistentStorage.LoadRunSlot(slotSelection + 1);
                if (save != null)
                {
                    RestoreRunSaveData(save, true, false);
                    return;
                }
            }

            state = slotReturnState;
        }

        private void UpdateHelp()
        {
            if (Input.WasNavigateLeftPressed())
                helpPageIndex = (helpPageIndex + HelpPageCount - 1) % HelpPageCount;
            else if (Input.WasNavigateRightPressed())
                helpPageIndex = (helpPageIndex + 1) % HelpPageCount;

            if ((Input.WasConfirmPressed() || Input.WasPrimaryActionPressed()) && helpPageIndex == 0)
            {
                StartTutorial(true, helpReturnState);
                return;
            }

            if (Input.WasCancelPressed() || Input.WasHelpPressed() || ((Input.WasConfirmPressed() || Input.WasPrimaryActionPressed()) && helpPageIndex != 0))
                state = helpReturnState;
        }

        private void UpdatePlaying()
        {
            float deltaSeconds = (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;

            if (Input.WasCancelPressed())
            {
                pauseReturnState = GameFlowState.Playing;
                state = GameFlowState.Paused;
                return;
            }

            if (Input.WasHelpPressed())
            {
                helpReturnState = GameFlowState.Paused;
                state = GameFlowState.Help;
                return;
            }

            if (Input.IsRewindHeld())
            {
                UpdateRewind(deltaSeconds);
                return;
            }

            rewindHoldSeconds = 0f;

            stageElapsedSeconds += deltaSeconds;

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
                BeginBossApproachTransition();
                return;
            }

            if (currentStage.Boss == null && currentSectionIndex >= currentStage.Sections.Count && scheduledSpawns.Count == 0 && !EntityManager.HasHostiles)
            {
                CompleteStage();
                return;
            }

            if (activeBoss != null && activeBoss.IsExpired && !EntityManager.HasHostiles && scheduledSpawns.Count == 0)
                CompleteStage();

            CaptureRewindFrame(deltaSeconds);
        }

        private void UpdateTutorial()
        {
            float deltaSeconds = (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;

            if (Input.WasCancelPressed())
            {
                pauseReturnState = GameFlowState.Tutorial;
                state = GameFlowState.Paused;
                return;
            }

            if (Input.WasHelpPressed())
            {
                helpReturnState = GameFlowState.Tutorial;
                helpPageIndex = 0;
                state = GameFlowState.Help;
                return;
            }

            if (Input.IsRewindHeld())
            {
                UpdateRewind(deltaSeconds);
                return;
            }

            rewindHoldSeconds = 0f;

            stageElapsedSeconds += deltaSeconds;

            EntityManager.Update();
            PlayerStatus.Update();

            if (Player1.Instance.ConsumeHullDestroyed() || EntityManager.ConsumePlayerHullDestruction())
            {
                Player1.Instance.RestoreToWindow();
                Player1.Instance.MakeInvulnerable(1.2f);
            }

            UpdateTutorialStep(deltaSeconds);
            CaptureRewindFrame(deltaSeconds);
        }

        private void UpdateUpgradeDraft()
        {
            if (draftCards.Count == 0)
            {
                FinishUpgradeDraft();
                return;
            }

            if (Input.WasNavigateLeftPressed())
                draftSelection = (draftSelection + draftCards.Count - 1) % draftCards.Count;
            else if (Input.WasNavigateRightPressed())
                draftSelection = (draftSelection + 1) % draftCards.Count;

            if (Input.WasCancelPressed())
            {
                ApplyDraftSelection(draftSelection);
                return;
            }

            draftTimer -= (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            if (options.AutoUpgradeDraft || draftTimer <= 0f)
            {
                ApplyDraftSelection(draftSelection);
                return;
            }

            if (Input.WasConfirmPressed() || Input.WasPrimaryActionPressed())
                ApplyDraftSelection(draftSelection);
        }

        private void UpdateStageTransition()
        {
            float deltaSeconds = (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            stateTimer -= deltaSeconds;
            transitionHudBlend = GetTransitionProgress();

            EntityManager.Update();
            PlayerStatus.Update();

            if (Player1.Instance.ConsumeHullDestroyed() || EntityManager.ConsumePlayerHullDestruction())
            {
                HandlePlayerShipDestroyed();
                return;
            }

            if (!transitionToBoss && PlayerStatus.RunProgress.StoredUpgradeCharges > 0 && stateTimer <= StageTransitionSeconds * 0.48f)
            {
                OpenUpgradeDraft(GameFlowState.StageTransition, false);
                return;
            }

            if (stateTimer > 0f)
                return;

            if (transitionToBoss)
            {
                SpawnBoss();
                state = GameFlowState.Playing;
                transitionToBoss = false;
                transitionHudBlend = 0f;
                return;
            }

            if (transitionTargetStageNumber >= 50 && currentStageNumber >= 50)
            {
                EnterEndState(GameFlowState.CampaignComplete, "CAMPAIGN COMPLETE");
                return;
            }

            StartStageFromTransition(transitionTargetStageNumber);
        }

        private void UpdateEndState()
        {
            stateTimer -= (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            if (stateTimer > 0f)
                return;

            if (Input.WasConfirmPressed() || Input.WasPrimaryActionPressed() || Input.WasCancelPressed())
                EnterTitle(false);
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
                    ResetRewindBuffer();
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
            ResetRewindBuffer();

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
            if (!options.TutorialCompleted)
            {
                StartTutorial(false, GameFlowState.Playing);
                return;
            }

            BeginFreshCampaign();
        }

        private void CompleteStage()
        {
            UnlockStageMedals();
            if (currentStageNumber >= 50)
            {
                EnterEndState(GameFlowState.CampaignComplete, "CAMPAIGN COMPLETE");
                return;
            }

            state = GameFlowState.StageTransition;
            stateTimer = StageTransitionSeconds;
            transitionTargetStageNumber = currentStageNumber + 1;
            transitionToBoss = false;
            transitionScrollFrom = GetCurrentStageScrollSpeed();
            transitionScrollTo = Math.Max(70f, GetStageBaseScrollSpeed(currentStageNumber + 1, repository.GetStage(currentStageNumber + 1)) * 0.88f);
            transitionHudBlend = 0f;
            bannerText = string.Concat("STAGE ", currentStageNumber.ToString("00"), " TO ", (currentStageNumber + 1).ToString("00"));
            Game1.Instance.Feedback?.Handle(new FeedbackEvent(FeedbackEventType.StageTransition, Player1.Instance.Position, 0.7f));
        }

        private void BeginFreshCampaign()
        {
            PlayerStatus.FinalizeRun();
            PlayerStatus.BeginCampaign(repository.GetStage(1));
            gameplayRandom.Restore(0xC0FFEEu);
            campaignHadDeath = false;
            stageHadDeath = false;
            tutorialReplayMode = false;
            tutorialStep = TutorialStep.Move;
            tutorialProgressSeconds = 0f;
            currentStageNumber = 1;
            PrepareStage(1, false);
        }

        private void StartTutorial(bool replayMode, GameFlowState returnState)
        {
            PlayerStatus.FinalizeRun();
            PlayerStatus.BeginCampaign(repository.GetStage(1));
            PlayerStatus.RunProgress.Weapons.SetStyleProgress(WeaponStyleId.Pulse, 3, 0, true);
            gameplayRandom.Restore(0xC0FFEEu);
            currentStageNumber = 1;
            currentStage = repository.GetStage(1);
            currentSectionIndex = 0;
            stageElapsedSeconds = 0f;
            stateTimer = 0f;
            activeBoss = null;
            scheduledSpawns.Clear();
            scheduledEvents.Clear();
            activeEventType = RandomEventType.None;
            activeEventTimer = 0f;
            activeEventSpawnTimer = 0f;
            activeEventIntensity = 0f;
            activeEventWarning = string.Empty;
            bannerText = string.Empty;
            transitionToBoss = false;
            transitionTargetStageNumber = 0;
            transitionHudBlend = 0f;
            titleSelection = 0;
            helpPageIndex = 0;
            tutorialReplayMode = replayMode;
            tutorialStep = TutorialStep.Move;
            tutorialProgressSeconds = 0f;
            draftCards.Clear();
            draftTimer = 0f;
            draftSelection = 0;
            draftChargeStyle = WeaponStyleId.Pulse;
            draftFromTutorial = false;
            pauseReturnState = GameFlowState.Tutorial;
            draftReturnState = returnState;
            EntityManager.Reset();
            Player1.Instance.ResetForStage();
            Player1.Instance.RefreshLoadout();
            Player1.Instance.MakeInvulnerable(1.5f);
            EntityManager.Add(Player1.Instance);
            ResetRewindBuffer();
            state = GameFlowState.Tutorial;
        }

        private void EnterTitle(bool finalizeRun)
        {
            if (finalizeRun)
                PlayerStatus.FinalizeRun();

            state = GameFlowState.Title;
            helpReturnState = GameFlowState.Title;
            slotReturnState = GameFlowState.Title;
            pauseReturnState = GameFlowState.Playing;
            draftReturnState = GameFlowState.Playing;
            currentStage = null;
            activeBoss = null;
            currentStageNumber = 1;
            currentSectionIndex = 0;
            stageElapsedSeconds = 0f;
            stateTimer = 0f;
            activeEventTimer = 0f;
            activeEventSpawnTimer = 0f;
            activeEventWarning = string.Empty;
            activeEventType = RandomEventType.None;
            activeEventIntensity = 0f;
            bannerText = string.Empty;
            transitionTargetStageNumber = 0;
            transitionToBoss = false;
            transitionScrollFrom = 0f;
            transitionScrollTo = 0f;
            transitionHudBlend = 0f;
            bossApproachTimer = 0f;
            titleSelection = 0;
            pauseSelection = 0;
            optionsSelection = 0;
            slotSelection = 0;
            helpPageIndex = 0;
            draftCards.Clear();
            draftSelection = 0;
            draftTimer = 0f;
            draftChargeStyle = WeaponStyleId.Pulse;
            draftFromTutorial = false;
            tutorialStep = TutorialStep.Move;
            tutorialProgressSeconds = 0f;
            if (!titleIntroSeen)
                ResetTitleIntro();
            else
            {
                titleIntroActive = false;
                titleIntroExploded = true;
                titleIntroTimer = 0f;
                introPixels.Clear();
            }
            EntityManager.Reset();
            ResetRewindBuffer();
        }

        private void ResetTitleIntro()
        {
            titleIntroActive = true;
            titleIntroExploded = false;
            titleIntroTimer = TitleIntroDurationSeconds;
            introPixels.Clear();
            RandomizeTitleIntroPalette();

            Rectangle logo = GetTitleLogoBounds();
            int columns = 56;
            int rows = 18;
            float spacingX = logo.Width / (float)columns;
            float spacingY = logo.Height / (float)rows;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    if ((x + y) % 3 == 1)
                        continue;

                    Vector2 target = new Vector2(logo.Left + spacingX * (x + 0.5f), logo.Top + spacingY * (y + 0.5f));
                    float seed = MathF.Abs(MathF.Sin((x + 1) * 12.73f + (y + 1) * 19.19f));
                    Vector2 start = new Vector2(target.X, -64f - seed * 320f - y * 8f);
                    Color color = Color.Lerp(titleIntroGlowColorA, titleIntroGlowColorB, MathF.Abs(MathF.Sin((x + y) * 0.37f)));

                    introPixels.Add(new IntroPixel
                    {
                        Position = start,
                        Velocity = new Vector2((seed - 0.5f) * 42f, 220f + seed * 180f),
                        Target = target,
                        Color = color
                    });
                }
            }
        }

        private void UpdateTitleIntro(float deltaSeconds)
        {
            if (!titleIntroActive)
                return;

            titleIntroTimer -= deltaSeconds;
            Rectangle logoBounds = GetTitleLogoBounds();
            if (Input.WasPrimaryActionPressed() && logoBounds.Contains(Input.PointerPosition))
                TriggerIntroExplosion();

            for (int i = 0; i < introPixels.Count; i++)
            {
                IntroPixel pixel = introPixels[i];
                if (!titleIntroExploded)
                {
                    Vector2 toTarget = pixel.Target - pixel.Position;
                    pixel.Velocity += toTarget * MathF.Min(9f * deltaSeconds, 0.28f);
                    pixel.Velocity *= 0.94f;
                }
                else
                {
                    pixel.Velocity *= 0.985f;
                }

                pixel.Position += pixel.Velocity * deltaSeconds;
                introPixels[i] = pixel;
            }

            if (titleIntroTimer <= 0f)
            {
                titleIntroActive = false;
                titleIntroSeen = true;
            }
        }

        private void TriggerIntroExplosion()
        {
            if (titleIntroExploded)
                return;

            titleIntroExploded = true;
            titleIntroTimer = Math.Min(titleIntroTimer, TitleIntroSkipDurationSeconds);
            Rectangle logoBounds = GetTitleLogoBounds();
            Vector2 origin = new Vector2(logoBounds.Center.X, logoBounds.Center.Y);
            for (int i = 0; i < introPixels.Count; i++)
            {
                IntroPixel pixel = introPixels[i];
                Vector2 direction = pixel.Position - origin;
                if (direction == Vector2.Zero)
                    direction = new Vector2(0f, -1f);
                direction.Normalize();
                pixel.Velocity = direction * (220f + (i % 13) * 16f);
                introPixels[i] = pixel;
            }
        }

        private static Rectangle GetTitleLogoBounds()
        {
            int width = Math.Min(Game1.VirtualWidth - 280, 760);
            int height = Math.Min(Game1.VirtualHeight / 3, 210);
            return new Rectangle((Game1.VirtualWidth - width) / 2, 96, width, height);
        }

        private void EnterEndState(GameFlowState endState, string text)
        {
            PlayerStatus.FinalizeRun();
            if (endState == GameFlowState.CampaignComplete)
            {
                if (PlayerStatus.RunProgress.MedalEligible)
                {
                    medals.CampaignClear = true;
                    if (!campaignHadDeath)
                        medals.PerfectCampaign = true;
                    PersistentStorage.SaveMedals(medals);
                }
            }

            bannerText = text;
            state = endState;
            stateTimer = 0.35f;
        }

        private void BeginBossApproachTransition()
        {
            state = GameFlowState.StageTransition;
            stateTimer = BossApproachSeconds;
            transitionToBoss = true;
            transitionTargetStageNumber = currentStageNumber;
            transitionScrollFrom = GetCurrentStageScrollSpeed();
            transitionScrollTo = currentStage.Boss != null ? currentStage.Boss.ArenaScrollSpeed : transitionScrollFrom;
            transitionHudBlend = 0f;
            activeEventType = RandomEventType.None;
            activeEventTimer = 0f;
            activeEventIntensity = 0f;
            activeEventWarning = "THREAT APPROACH";
            bossApproachTimer = BossApproachSeconds;
            ResetRewindBuffer();
            Game1.Instance.Feedback?.Handle(new FeedbackEvent(FeedbackEventType.BossEntry, Player1.Instance.Position, 1f));
        }

        private void StartStageFromTransition(int stageNumber)
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
            Player1.Instance.MakeInvulnerable(0.8f);
            state = GameFlowState.Playing;
            transitionHudBlend = 0f;
            transitionToBoss = false;
            bannerText = string.Empty;
            ResetRewindBuffer();
        }

        private void CaptureRewindFrame(float deltaSeconds)
        {
            rewindCaptureTimer -= deltaSeconds;
            if (rewindCaptureTimer > 0f || (state != GameFlowState.Playing && state != GameFlowState.Tutorial))
                return;

            rewindCaptureTimer = RewindSnapshotInterval;
            rewindFrames.Add(CaptureRunSaveData(0, false));

            int maxFrames = (int)MathF.Ceiling(RewindCapacitySeconds / RewindSnapshotInterval);
            while (rewindFrames.Count > maxFrames)
                rewindFrames.RemoveAt(0);
        }

        private void UpdateRewind(float deltaSeconds)
        {
            if (rewindFrames.Count <= 1 || rewindMeterSeconds <= 0f)
            {
                rewindHoldSeconds = 0f;
                return;
            }

            PlayerStatus.RunProgress.MarkMedalIneligible();
            rewindHoldSeconds += deltaSeconds;
            float rewindSpeed = GetRewindSpeedMultiplier(rewindHoldSeconds);
            float drainRate = MathHelper.Lerp(0.22f, 1.35f, MathHelper.Clamp(rewindSpeed / 2.5f, 0f, 1f));
            drainRate *= 1f - MathHelper.Clamp(PlayerStatus.RunProgress.RewindEfficiency, 0f, 0.75f);
            rewindMeterSeconds = Math.Max(0f, rewindMeterSeconds - deltaSeconds * drainRate);

            rewindStepAccumulator += deltaSeconds * rewindSpeed;
            while (rewindStepAccumulator >= RewindSnapshotInterval && rewindFrames.Count > 1)
            {
                rewindStepAccumulator -= RewindSnapshotInterval;
                rewindFrames.RemoveAt(rewindFrames.Count - 1);
            }

            RestoreRunSaveData(rewindFrames[rewindFrames.Count - 1], false, true);
            if (state == GameFlowState.Tutorial && tutorialStep == TutorialStep.Rewind && rewindHoldSeconds >= 0.18f)
                AdvanceTutorialStep(TutorialStep.CollectPower);
        }

        private void ResetRewindBuffer()
        {
            rewindFrames.Clear();
            rewindCaptureTimer = 0f;
            rewindMeterSeconds = RewindCapacitySeconds;
            rewindHoldSeconds = 0f;
            rewindStepAccumulator = 0f;
        }

        private float GetTransitionProgress()
        {
            float duration = transitionToBoss ? BossApproachSeconds : StageTransitionSeconds;
            if (duration <= 0f)
                return 1f;

            return 1f - MathHelper.Clamp(stateTimer / duration, 0f, 1f);
        }

        private float GetTransitionScrollSpeed()
        {
            float progress = GetTransitionProgress();
            if (transitionToBoss)
                return MathHelper.SmoothStep(transitionScrollFrom, transitionScrollTo, progress);

            if (progress < 0.24f)
                return MathHelper.Lerp(transitionScrollFrom, transitionScrollFrom * 0.78f, progress / 0.24f);

            if (progress < 0.72f)
            {
                float phase = (progress - 0.24f) / 0.48f;
                return MathHelper.SmoothStep(transitionScrollFrom * 0.78f, Math.Min(transitionScrollTo * 2.35f, 420f), phase);
            }

            return MathHelper.SmoothStep(Math.Min(transitionScrollTo * 2.35f, 420f), transitionScrollTo, (progress - 0.72f) / 0.28f);
        }

        private static float GetRewindSpeedMultiplier(float holdSeconds)
        {
            if (holdSeconds <= 0.35f)
                return 0.2f;

            if (holdSeconds >= 1.25f)
                return 2.5f;

            float t = (holdSeconds - 0.35f) / 0.9f;
            return MathHelper.SmoothStep(0.2f, 2.5f, t);
        }

        private void UpdateTutorialStep(float deltaSeconds)
        {
            switch (tutorialStep)
            {
                case TutorialStep.Move:
                    tutorialProgressSeconds = Input.GetMovementDirection().LengthSquared() > 0.01f ? tutorialProgressSeconds + deltaSeconds : 0f;
                    if (tutorialProgressSeconds >= 0.25f)
                        AdvanceTutorialStep(TutorialStep.Aim);
                    break;

                case TutorialStep.Aim:
                    EnsureTutorialTarget(false);
                    tutorialProgressSeconds = Input.GetAimDirection() != Vector2.Zero ? tutorialProgressSeconds + deltaSeconds : 0f;
                    if (tutorialProgressSeconds >= 0.2f)
                        AdvanceTutorialStep(TutorialStep.Fire);
                    break;

                case TutorialStep.Fire:
                    if (!EntityManager.HasHostiles)
                    {
                        AdvanceTutorialStep(TutorialStep.Rewind);
                        break;
                    }

                    EnsureTutorialTarget(false);
                    break;

                case TutorialStep.CollectPower:
                    EnsureTutorialPickup();
                    if (PlayerStatus.RunProgress.StoredUpgradeCharges > 0)
                        AdvanceTutorialStep(TutorialStep.UpgradeDraft);
                    break;

                case TutorialStep.UpgradeDraft:
                    if (state != GameFlowState.UpgradeDraft && PlayerStatus.RunProgress.StoredUpgradeCharges > 0)
                        OpenUpgradeDraft(GameFlowState.Tutorial, true);
                    break;

                case TutorialStep.SwitchStyle:
                    if (PlayerStatus.RunProgress.Weapons.OwnedStyles.Count > 1 && (Input.WasPreviousStylePressed() || Input.WasNextStylePressed()))
                        AdvanceTutorialStep(TutorialStep.ShipsAndLives);
                    break;

                case TutorialStep.ShipsAndLives:
                    if (Input.WasConfirmPressed() || Input.WasPrimaryActionPressed())
                        AdvanceTutorialStep(TutorialStep.Complete);
                    break;

                case TutorialStep.Complete:
                    options.TutorialCompleted = true;
                    PersistentStorage.SaveOptions(options);
                    if (tutorialReplayMode)
                        EnterTitle(false);
                    else
                        BeginFreshCampaign();
                    break;
            }
        }

        private void AdvanceTutorialStep(TutorialStep nextStep)
        {
            tutorialStep = nextStep;
            tutorialProgressSeconds = 0f;

            if (nextStep == TutorialStep.Rewind)
            {
                EnsureTutorialTarget(true);
                Player1.Instance.Position += new Vector2(80f, 0f);
                Player1.Instance.ApplyKnockback(Vector2.Zero, 0f);
            }
        }

        private void EnsureTutorialTarget(bool moving)
        {
            if (EntityManager.HasHostiles || currentStage == null || !repository.ArchetypesById.TryGetValue("Walker", out EnemyArchetypeDefinition archetype))
                return;

            Vector2 spawn = new Vector2(Game1.ScreenSize.X * 0.78f, Game1.ScreenSize.Y * 0.48f);
            EntityManager.Add(new Enemy(
                archetype,
                spawn,
                spawn.Y,
                moving ? MovePattern.SineWave : MovePattern.TurretCarrier,
                FirePattern.None,
                moving ? 0.28f : 0.14f,
                moving ? 28f : 8f,
                moving ? 0.55f : 0.22f));
        }

        private void EnsureTutorialPickup()
        {
            if (EntityManager.Powerups.Any())
                return;

            EntityManager.Add(new PowerupPickup(Player1.Instance.Position + new Vector2(150f, -32f), WeaponStyleId.Spread));
        }

        private void OpenUpgradeDraft(GameFlowState returnState, bool tutorialMode)
        {
            if (PlayerStatus.RunProgress.StoredUpgradeCharges <= 0)
                return;

            draftChargeStyle = tutorialMode ? WeaponStyleId.Spread : PlayerStatus.RunProgress.PriorityChargeStyle;
            draftCards.Clear();
            draftCards.Add(BuildWeaponDraftCard(draftChargeStyle));
            if (tutorialMode)
            {
                draftCards.Add(CreateDraftCard(UpgradeCardType.MobilityTuning, "THRUSTER TUNE", "MOVE FASTER THROUGH THE FIELD", "#6EC1FF"));
                draftCards.Add(CreateDraftCard(UpgradeCardType.RewindBattery, "REWIND CELL", "LOWER REWIND METER DRAIN", "#56F0FF"));
            }
            else
            {
                var pool = new List<UpgradeCardType>
                {
                    UpgradeCardType.MobilityTuning,
                    UpgradeCardType.EmergencyReserve,
                    UpgradeCardType.RewindBattery,
                    UpgradeCardType.LuckyCore,
                };

                while (draftCards.Count < 3 && pool.Count > 0)
                {
                    int index = gameplayRandom.NextInt(0, pool.Count);
                    UpgradeCardType type = pool[index];
                    pool.RemoveAt(index);
                    draftCards.Add(CreateDraftCard(type));
                }
            }

            draftSelection = 0;
            draftTimer = 3f;
            draftReturnState = returnState;
            draftFromTutorial = tutorialMode;
            state = GameFlowState.UpgradeDraft;
        }

        private UpgradeDraftCard BuildWeaponDraftCard(WeaponStyleId styleId)
        {
            WeaponInventoryState inventory = PlayerStatus.RunProgress.Weapons;
            WeaponStyleDefinition activeStyle = WeaponCatalog.GetStyle(styleId);
            string description;
            if (!inventory.OwnsStyle(styleId))
            {
                description = string.Concat("UNLOCK ", activeStyle.DisplayName, " AND EQUIP IT");
            }
            else
            {
                int styleLevel = inventory.GetLevel(styleId);
                if (styleLevel < 3)
                    description = string.Concat("BOOST ", activeStyle.DisplayName, " TO LEVEL ", (styleLevel + 1).ToString());
                else
                    description = string.Concat("RAISE ", activeStyle.DisplayName, " TO RANK ", (inventory.GetRank(styleId) + 1).ToString());
            }

            return new UpgradeDraftCard
            {
                Type = UpgradeCardType.WeaponSurge,
                StyleId = styleId,
                Title = string.Concat(activeStyle.DisplayName, " SURGE"),
                Description = description,
                AccentColor = activeStyle.AccentColor,
            };
        }

        private UpgradeDraftCard CreateDraftCard(UpgradeCardType type)
        {
            switch (type)
            {
                case UpgradeCardType.MobilityTuning:
                    return CreateDraftCard(type, "THRUSTER TUNE", "INCREASE SHIP MOVE SPEED FOR THE RUN", "#6EC1FF");
                case UpgradeCardType.EmergencyReserve:
                    return CreateDraftCard(type, "EMERGENCY RESERVE", "GAIN +1 SHIP NOW AND ON EACH NEW LIFE", "#FFB347");
                case UpgradeCardType.RewindBattery:
                    return CreateDraftCard(type, "REWIND CELL", "LOWER REWIND METER DRAIN AND REFILL IT", "#56F0FF");
                case UpgradeCardType.LuckyCore:
                    return CreateDraftCard(type, "LUCKY CORE", "IMPROVE DROP MOMENTUM AND EARN SCORE", "#7AE582");
                default:
                    return BuildWeaponDraftCard(draftChargeStyle);
            }
        }

        private static UpgradeDraftCard CreateDraftCard(UpgradeCardType type, string title, string description, string accent)
        {
            return new UpgradeDraftCard
            {
                Type = type,
                Title = title,
                Description = description,
                AccentColor = accent,
            };
        }

        private void ApplyDraftSelection(int selection)
        {
            if (draftCards.Count == 0)
            {
                FinishUpgradeDraft();
                return;
            }

            int safeSelection = Math.Clamp(selection, 0, draftCards.Count - 1);
            UpgradeDraftCard card = draftCards[safeSelection];
            if (!PlayerStatus.RunProgress.TryConsumeUpgradeCharge(draftChargeStyle))
            {
                FinishUpgradeDraft();
                return;
            }

            switch (card.Type)
            {
                case UpgradeCardType.WeaponSurge:
                    PlayerStatus.RunProgress.ApplyWeaponUpgrade(card.StyleId, true);
                    Player1.Instance.RefreshLoadout();
                    break;
                case UpgradeCardType.MobilityTuning:
                    PlayerStatus.RunProgress.ApplyMobilityUpgrade();
                    break;
                case UpgradeCardType.EmergencyReserve:
                    PlayerStatus.RunProgress.ApplyEmergencyReserveUpgrade();
                    PlayerStatus.GrantShips(1);
                    break;
                case UpgradeCardType.RewindBattery:
                    PlayerStatus.RunProgress.ApplyRewindUpgrade();
                    rewindMeterSeconds = RewindCapacitySeconds;
                    break;
                case UpgradeCardType.LuckyCore:
                    PlayerStatus.RunProgress.ApplyEconomyUpgrade();
                    PlayerStatus.AddPoints(250);
                    break;
            }

            if (draftFromTutorial && PlayerStatus.RunProgress.Weapons.OwnedStyles.Count < 2)
            {
                PlayerStatus.RunProgress.ApplyWeaponUpgrade(WeaponStyleId.Spread, true);
                Player1.Instance.RefreshLoadout();
            }

            EntityManager.SpawnShockwave(Player1.Instance.Position, ColorUtil.ParseHex(card.AccentColor, Color.Orange) * 0.16f, 14f, 72f, 0.22f);
            Game1.Instance.Feedback?.Handle(new FeedbackEvent(FeedbackEventType.Upgrade, Player1.Instance.Position, 0.8f, card.StyleId, true));

            if (draftFromTutorial && tutorialStep == TutorialStep.UpgradeDraft)
                AdvanceTutorialStep(TutorialStep.SwitchStyle);

            if (PlayerStatus.RunProgress.StoredUpgradeCharges > 0)
            {
                OpenUpgradeDraft(draftReturnState, false);
                return;
            }

            FinishUpgradeDraft();
        }

        private void FinishUpgradeDraft()
        {
            draftCards.Clear();
            draftSelection = 0;
            draftTimer = 0f;
            draftChargeStyle = WeaponStyleId.Pulse;
            bool returnToTutorial = draftFromTutorial;
            draftFromTutorial = false;
            state = draftReturnState;
            if (returnToTutorial && tutorialStep == TutorialStep.UpgradeDraft)
                AdvanceTutorialStep(TutorialStep.SwitchStyle);
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
            List<SpawnGroupDefinition> orderedGroups = section.Groups
                .OrderBy(group => group.StartSeconds)
                .ThenBy(group => group.ArchetypeId ?? string.Empty)
                .ToList();
            float previousStartSeconds = -1f;
            float maxGapSeconds = GetSectionGapClampSeconds(currentStageNumber, currentSectionIndex);
            for (int groupIndex = 0; groupIndex < orderedGroups.Count; groupIndex++)
            {
                SpawnGroupDefinition group = orderedGroups[groupIndex];
                float scheduledStartSeconds = group.StartSeconds;
                if (previousStartSeconds >= 0f && scheduledStartSeconds - previousStartSeconds > maxGapSeconds)
                    scheduledStartSeconds = previousStartSeconds + maxGapSeconds;

                ScheduleGroup(section.StartSeconds, section, group, scheduledStartSeconds);
                previousStartSeconds = scheduledStartSeconds;
            }

            if (section.EventWindows == null)
                return;

            for (int i = 0; i < section.EventWindows.Count; i++)
            {
                RandomEventWindowDefinition window = section.EventWindows[i];
                float triggerOffset = window.StartSeconds + gameplayRandom.NextFloat(0f, Math.Max(0f, window.DurationSeconds * 0.7f));
                scheduledEvents.Add(new ScheduledEvent
                {
                    TriggerAtSeconds = section.StartSeconds + triggerOffset,
                    Window = window,
                });
            }

            scheduledEvents.Sort((left, right) => left.TriggerAtSeconds.CompareTo(right.TriggerAtSeconds));
        }

        private void ScheduleGroup(float stageStartSeconds, SectionDefinition section, SpawnGroupDefinition group)
        {
            ScheduleGroup(stageStartSeconds, section, group, group.StartSeconds);
        }

        private void ScheduleGroup(float stageStartSeconds, SectionDefinition section, SpawnGroupDefinition group, float groupStartSeconds)
        {
            EnemyArchetypeDefinition archetype = repository.ArchetypesById[group.ArchetypeId];
            float targetY = LevelMath.ResolveTargetY(Game1.ScreenSize.ToSystemNumerics(), group);
            int sectionIndex = section != null ? currentSectionIndex : GetActiveSectionIndex();
            float sectionSpeedMultiplier = section != null && section.EnemySpeedMultiplier > 0f
                ? section.EnemySpeedMultiplier
                : GetDefaultEnemySpeedMultiplier(sectionIndex);
            float runPressureMultiplier = 1f + MathHelper.Clamp(PlayerStatus.RunProgress.PowerBudget * 0.02f, 0f, 0.32f);
            int scheduledCount = GetAdjustedGroupCount(group, archetype, currentStageNumber, sectionIndex);
            float spawnIntervalSeconds = Math.Max(0.08f, group.SpawnIntervalSeconds * GetSpawnIntervalScale(archetype.Id, currentStageNumber, sectionIndex));
            float spacingX = Math.Max(52f, group.SpacingX * GetSpacingScale(archetype.Id, currentStageNumber, sectionIndex));

            for (int index = 0; index < scheduledCount; index++)
            {
                scheduledSpawns.Add(new ScheduledSpawn
                {
                    SpawnAtSeconds = stageStartSeconds + groupStartSeconds + spawnIntervalSeconds * index,
                    Group = group,
                    SpawnPoint = new Vector2(
                        Game1.ScreenSize.X + Math.Max(40f, group.SpawnLeadDistance + index * spacingX),
                        targetY),
                    TargetY = targetY,
                    MovePattern = group.MovePatternOverride ?? archetype.MovePattern,
                    FirePattern = group.FirePatternOverride ?? archetype.FirePattern,
                    Amplitude = group.Amplitude > 0f ? group.Amplitude : archetype.MovementAmplitude,
                    Frequency = group.Frequency > 0f ? group.Frequency : archetype.MovementFrequency,
                    SpeedMultiplier = group.SpeedMultiplier * sectionSpeedMultiplier * runPressureMultiplier,
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
                    scheduledSpawn.SpeedMultiplier,
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
                    position = new Vector2(Game1.ScreenSize.X + 40f + gameplayRandom.NextInt(0, 160), gameplayRandom.NextInt(0, Game1.VirtualHeight / 2));
                    velocity = new Vector2(-320f - 60f * intensity, 180f + gameplayRandom.NextInt(-40, 80));
                    sprite = CreateEventProjectileDefinition("#B56A46", "#EABF8F", "#FFF1C9", new[] { ".#.", "###", ".#." });
                    impact = new ImpactProfileDefinition { Name = "Meteor", Kernel = ImpactKernelShape.Blast5, BaseCellsRemoved = 5, BonusCellsPerDamage = 1, SplashRadius = 1, SplashPercent = 35, DebrisBurstCount = 10, DebrisSpeed = 150f };
                    damage = 2;
                    scale = 1.4f;
                    break;
                case RandomEventType.CometSwarm:
                    position = new Vector2(Game1.ScreenSize.X + 40f + gameplayRandom.NextInt(0, 180), gameplayRandom.NextInt(40, Game1.VirtualHeight - 40));
                    velocity = new Vector2(-420f - 100f * intensity, gameplayRandom.NextInt(-80, 81));
                    sprite = CreateEventProjectileDefinition("#DFF4FF", "#8FD3FF", "#FFF0AF", new[] { "##", "##" });
                    impact = new ImpactProfileDefinition { Name = "Comet", Kernel = ImpactKernelShape.Diamond3, BaseCellsRemoved = 4, BonusCellsPerDamage = 1, SplashRadius = 0, SplashPercent = 0, DebrisBurstCount = 8, DebrisSpeed = 180f };
                    damage = 2;
                    scale = 1.1f;
                    break;
                default:
                    position = new Vector2(Game1.ScreenSize.X + 30f + gameplayRandom.NextInt(0, 120), gameplayRandom.NextInt(0, Game1.VirtualHeight));
                    velocity = new Vector2(-210f - 40f * intensity, gameplayRandom.NextInt(-30, 31));
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
                ScheduleGroup(stageElapsedSeconds + 0.35f, null, group);
        }

        private void UnlockStageMedals()
        {
            if (!PlayerStatus.RunProgress.MedalEligible)
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
            bool showSkipTutorial = !options.TutorialCompleted;
            int index = 0;
            if (selection == index++)
            {
                StartCampaign();
                return;
            }

            if (showSkipTutorial && selection == index++)
            {
                options.TutorialCompleted = true;
                PersistentStorage.SaveOptions(options);
                BeginFreshCampaign();
                return;
            }

            if (selection == index++)
            {
                slotReturnState = GameFlowState.Title;
                slotSelection = 0;
                state = GameFlowState.LoadSlots;
                return;
            }

            if (selection == index++)
            {
                slotReturnState = GameFlowState.Title;
                optionsSelection = 0;
                state = GameFlowState.Options;
                return;
            }

            if (selection == index++)
            {
                helpReturnState = GameFlowState.Title;
                helpPageIndex = 0;
                state = GameFlowState.Help;
                return;
            }

            if (selection == index++)
            {
                helpReturnState = GameFlowState.Title;
                helpPageIndex = AboutHelpPageIndex;
                state = GameFlowState.Help;
                return;
            }

            Game1.Instance.Exit();
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

        private void HandlePointerSelection(IReadOnlyList<Rectangle> bounds, ref int selection)
        {
            if (!Input.WasPrimaryActionPressed())
                return;

            Vector2 pointer = Input.PointerPosition;
            for (int i = 0; i < bounds.Count; i++)
            {
                if (bounds[i].Contains(pointer))
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
            int total = options.TutorialCompleted ? 6 : 7;
            var buttons = new List<UiButton>
            {
                CreateButton("START CAMPAIGN", 0, total),
            };

            int nextIndex = 1;
            if (!options.TutorialCompleted)
                buttons.Add(CreateButton("START WITHOUT TUTORIAL", nextIndex++, total));

            buttons.Add(CreateButton("LOAD GAME", nextIndex++, total));
            buttons.Add(CreateButton("OPTIONS", nextIndex++, total));
            buttons.Add(CreateButton("HELP", nextIndex++, total));
            buttons.Add(CreateButton("ABOUT / LEGAL", nextIndex++, total));
            buttons.Add(CreateButton("QUIT", nextIndex, total));
            return buttons;
        }

        private List<UiButton> GetPauseButtons()
        {
            bool tutorialPause = pauseReturnState == GameFlowState.Tutorial;
            int total = tutorialPause ? 7 : 6;
            var buttons = new List<UiButton>
            {
                CreateButton("RESUME", 0, total),
                CreateButton("SAVE GAME", 1, total),
                CreateButton("LOAD GAME", 2, total),
                CreateButton("OPTIONS", 3, total),
                CreateButton("HELP", 4, total),
            };

            if (tutorialPause)
                buttons.Add(CreateButton("SKIP TUTORIAL", 5, total));

            buttons.Add(CreateButton("QUIT TO TITLE", total - 1, total));
            return buttons;
        }

        private UiButton CreateButton(string text, int index, int total)
        {
            float uiScale = Game1.Instance != null ? Game1.Instance.UiLayoutScale : 1f;
            Vector2 center = Game1.ScreenSize / 2f;
            int width = Math.Max(440, (int)MathF.Round(440f * uiScale));
            int height = (int)MathF.Round((total >= 6 ? 48f : 54f) * uiScale);
            int spacing = (int)MathF.Round((total >= 6 ? 12f : 18f) * uiScale);
            int x = (int)center.X - width / 2;
            int totalHeight = total * height + (total - 1) * spacing;
            int top = (Game1.VirtualHeight - totalHeight) / 2 + (int)MathF.Round(34f * uiScale);
            int y = top + index * (height + spacing);
            return new UiButton(new Rectangle(x, y, width, height), text);
        }

        private int UiPx(int value)
        {
            float uiScale = Game1.Instance != null ? Game1.Instance.UiLayoutScale : 1f;
            return Math.Max(1, (int)MathF.Round(value * uiScale));
        }

        private Rectangle[] GetOptionRowBounds()
        {
            string[] rows = GetOptionRows();
            int rowHeight = UiPx(rows.Length > 12 ? 28 : 32);
            int rowStep = UiPx(rows.Length > 12 ? 34 : 42);
            int top = UiPx(rows.Length > 12 ? 156 : 172);
            int rowWidth = Game1.VirtualWidth - UiPx(360);
            int rowX = (Game1.VirtualWidth - rowWidth) / 2;
            var bounds = new Rectangle[rows.Length];
            for (int i = 0; i < rows.Length; i++)
                bounds[i] = new Rectangle(rowX, top + i * rowStep, rowWidth, rowHeight);

            return bounds;
        }

        private Rectangle[] GetSaveSlotSelectionBounds()
        {
            int rowX = (Game1.VirtualWidth - UiPx(920)) / 2;
            var bounds = new Rectangle[4];
            for (int i = 0; i < 3; i++)
                bounds[i] = new Rectangle(rowX, UiPx(180) + i * UiPx(88), UiPx(920), UiPx(68));

            bounds[3] = new Rectangle(rowX, UiPx(180) + 3 * UiPx(88), UiPx(920), UiPx(58));
            return bounds;
        }

        private string[] GetOptionRows()
        {
#if ANDROID
            return new[]
            {
                string.Concat("UI SCALE  ", options.UiScalePercent.ToString(), "%"),
                string.Concat("WORLD SCALE  ", options.WorldScalePercent.ToString(), "%"),
                string.Concat("FONT THEME  ", options.FontTheme.ToString().ToUpperInvariant()),
                string.Concat("VISUAL PRESET  ", options.VisualPreset.ToString().ToUpperInvariant()),
                string.Concat("BLOOM  ", options.EnableBloom ? "ON" : "OFF"),
                string.Concat("SHOCKWAVES  ", options.EnableShockwaves ? "ON" : "OFF"),
                string.Concat("NEON OUTLINES  ", options.EnableNeonOutlines ? "ON" : "OFF"),
                string.Concat("SCREEN SHAKE  ", options.ScreenShakeStrength.ToString().ToUpperInvariant()),
                string.Concat("AUDIO QUALITY  ", options.AudioQualityPreset.ToString().ToUpperInvariant()),
                string.Concat("MASTER VOLUME  ", (int)(options.MasterVolume * 100f), "%"),
                string.Concat("MUSIC VOLUME  ", (int)(options.MusicVolume * 100f), "%"),
                string.Concat("SFX VOLUME  ", (int)(options.SfxVolume * 100f), "%"),
                string.Concat("AUTO DRAFT  ", options.AutoUpgradeDraft ? "ON" : "OFF"),
                string.Concat("HELP HINTS  ", options.ShowHelpHints ? "ON" : "OFF"),
            };
#else
            return new[]
            {
                string.Concat("DISPLAY MODE  ", options.DisplayMode == DesktopDisplayMode.BorderlessFullscreen ? "BORDERLESS FULLSCREEN" : "WINDOWED"),
                string.Concat("UI SCALE  ", options.UiScalePercent.ToString(), "%"),
                string.Concat("WORLD SCALE  ", options.WorldScalePercent.ToString(), "%"),
                string.Concat("FONT THEME  ", options.FontTheme.ToString().ToUpperInvariant()),
                string.Concat("VISUAL PRESET  ", options.VisualPreset.ToString().ToUpperInvariant()),
                string.Concat("BLOOM  ", options.EnableBloom ? "ON" : "OFF"),
                string.Concat("SHOCKWAVES  ", options.EnableShockwaves ? "ON" : "OFF"),
                string.Concat("NEON OUTLINES  ", options.EnableNeonOutlines ? "ON" : "OFF"),
                string.Concat("SCREEN SHAKE  ", options.ScreenShakeStrength.ToString().ToUpperInvariant()),
                string.Concat("AUDIO QUALITY  ", options.AudioQualityPreset.ToString().ToUpperInvariant()),
                string.Concat("MASTER VOLUME  ", (int)(options.MasterVolume * 100f), "%"),
                string.Concat("MUSIC VOLUME  ", (int)(options.MusicVolume * 100f), "%"),
                string.Concat("SFX VOLUME  ", (int)(options.SfxVolume * 100f), "%"),
                string.Concat("AUTO DRAFT  ", options.AutoUpgradeDraft ? "ON" : "OFF"),
                string.Concat("HELP HINTS  ", options.ShowHelpHints ? "ON" : "OFF"),
            };
#endif
        }

        private static float AdjustVolume(float value, int delta)
        {
            float adjusted = value + delta * 0.05f;
            return MathF.Round(MathHelper.Clamp(adjusted, 0f, 1f) * 20f) / 20f;
        }

        private static int AdjustOptionPercent(int current, int delta, int[] options)
        {
            int nearestIndex = 0;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < options.Length; i++)
            {
                float distance = MathF.Abs(options[i] - current);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }

            int nextIndex = Math.Clamp(nearestIndex + delta, 0, options.Length - 1);
            return options[nextIndex];
        }

        private void RandomizeTitleIntroPalette()
        {
            IntroPalette palette = IntroPalettes[titleVisualRandom.Next(IntroPalettes.Length)];
            titleIntroBackgroundColor = palette.Background;
            titleIntroGlowColorA = palette.GlowA;
            titleIntroGlowColorB = palette.GlowB;
            titleIntroPrimaryColor = palette.Primary;
            titleIntroSecondaryColor = palette.Secondary;
            titleIntroPromptColor = palette.Prompt;
        }

        private void DrawIntroBackdrop(SpriteBatch spriteBatch, Texture2D pixel)
        {
            Rectangle full = new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight);
            spriteBatch.Draw(pixel, full, titleIntroBackgroundColor);

            float time = (float)Game1.GameTime.TotalGameTime.TotalSeconds;
            for (int i = 0; i < 80; i++)
            {
                float xSeed = MathF.Abs(MathF.Sin(i * 12.913f + 0.71f));
                float ySeed = MathF.Abs(MathF.Sin(i * 31.137f + 2.31f));
                float twinkle = 0.4f + 0.6f * MathF.Abs(MathF.Sin(time * (0.8f + i * 0.03f) + i));
                int x = (int)(xSeed * (Game1.VirtualWidth - 8));
                int y = (int)(18f + ySeed * (Game1.VirtualHeight - 36f));
                int size = i % 9 == 0 ? 3 : 2;
                spriteBatch.Draw(pixel, new Rectangle(x, y, size, size), Color.White * (0.12f + twinkle * 0.18f));
            }

            if (Game1.RadialTexture == null)
                return;

            Texture2D radial = Game1.RadialTexture;
            Vector2 origin = new Vector2(radial.Width / 2f, radial.Height / 2f);
            spriteBatch.Draw(radial, new Vector2(Game1.VirtualWidth * 0.18f, Game1.VirtualHeight * 0.24f), null, titleIntroGlowColorA * 0.18f, 0f, origin, 2.8f, SpriteEffects.None, 0f);
            spriteBatch.Draw(radial, new Vector2(Game1.VirtualWidth * 0.82f, Game1.VirtualHeight * 0.2f), null, titleIntroGlowColorB * 0.16f, 0f, origin, 2.45f, SpriteEffects.None, 0f);
            spriteBatch.Draw(radial, new Vector2(Game1.VirtualWidth * 0.56f, Game1.VirtualHeight * 0.72f), null, Color.Lerp(titleIntroGlowColorA, titleIntroGlowColorB, 0.5f) * 0.08f, 0f, origin, 3.2f, SpriteEffects.None, 0f);
        }

        private void DrawBackdrop(SpriteBatch spriteBatch, Texture2D pixel, float strength, string headline = "")
        {
            Rectangle full = new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight);
            Color baseColor = Color.Lerp(new Color(4, 8, 18), new Color(10, 18, 36), MathHelper.Clamp(strength, 0f, 1f));
            spriteBatch.Draw(pixel, full, baseColor);

            float time = (float)Game1.GameTime.TotalGameTime.TotalSeconds;
            int streakCount = 28 + (int)(strength * 18f);
            for (int i = 0; i < 84; i++)
            {
                float xSeed = MathF.Abs(MathF.Sin(i * 12.913f + 0.71f));
                float ySeed = MathF.Abs(MathF.Sin(i * 31.137f + 2.31f));
                float twinkle = 0.4f + 0.6f * MathF.Abs(MathF.Sin(time * (0.8f + i * 0.03f) + i));
                int x = (int)(xSeed * (Game1.VirtualWidth - 8));
                int y = (int)(18f + ySeed * (Game1.VirtualHeight - 36f));
                int size = i % 9 == 0 ? 3 : 2;
                spriteBatch.Draw(pixel, new Rectangle(x, y, size, size), Color.White * (0.18f + twinkle * 0.24f * strength));
            }

            for (int i = 0; i < streakCount; i++)
            {
                float ySeed = MathF.Abs(MathF.Sin(i * 23.87f + 0.42f));
                float xSeed = MathF.Abs(MathF.Sin(i * 8.27f + 0.19f));
                float width = 38f + xSeed * 108f;
                float x = Game1.VirtualWidth - ((time * (40f + strength * 120f) + xSeed * (Game1.VirtualWidth + width)) % (Game1.VirtualWidth + width));
                float y = 40f + ySeed * (Game1.VirtualHeight - 80f);
                spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, (int)width, 2), new Color(110, 193, 255) * (0.08f + 0.12f * strength));
            }
            if (Game1.RadialTexture != null)
            {
                Texture2D radial = Game1.RadialTexture;
                Vector2 origin = new Vector2(radial.Width / 2f, radial.Height / 2f);
                spriteBatch.Draw(radial, new Vector2(Game1.VirtualWidth * 0.18f, Game1.VirtualHeight * 0.28f), null, new Color(110, 193, 255) * (0.08f + 0.05f * strength), 0f, origin, 2.6f + strength * 0.4f, SpriteEffects.None, 0f);
                spriteBatch.Draw(radial, new Vector2(Game1.VirtualWidth * 0.82f, Game1.VirtualHeight * 0.24f), null, new Color(246, 198, 116) * (0.06f + 0.05f * strength), 0f, origin, 2.1f + strength * 0.35f, SpriteEffects.None, 0f);
                spriteBatch.Draw(radial, new Vector2(Game1.VirtualWidth * 0.62f, Game1.VirtualHeight * 0.72f), null, new Color(90, 140, 200) * 0.05f, 0f, origin, 3.1f, SpriteEffects.None, 0f);
            }

            if (!string.IsNullOrEmpty(headline))
                DrawCenteredText(spriteBatch, pixel, headline, Game1.ScreenSize.X / 2f, 26f, Color.White * 0.2f, 1.1f);
        }
        private void DrawTitle(SpriteBatch spriteBatch, Texture2D pixel)
        {
            if (titleIntroActive)
                DrawIntroBackdrop(spriteBatch, pixel);
            else
                DrawBackdrop(spriteBatch, pixel, 0.92f, "SEAMLESS CAMPAIGN WITH REWIND");

            if (titleIntroActive)
            {
                DrawSabinoIntro(spriteBatch, pixel);
                return;
            }

            int shellMarginX = UiPx(150);
            int shellMarginY = UiPx(62);
            Rectangle shell = new Rectangle(shellMarginX, shellMarginY, Game1.VirtualWidth - shellMarginX * 2, Game1.VirtualHeight - shellMarginY * 2);
            DrawPanel(spriteBatch, pixel, shell, Color.Black * 0.18f, Color.White * 0.15f);

            DrawCenteredText(spriteBatch, pixel, "SPACEBURST", Game1.ScreenSize.X / 2f, 88f, Color.White, 4f);
            DrawCenteredText(spriteBatch, pixel, "PROCEDURAL SIDE SCROLLER", Game1.ScreenSize.X / 2f, 148f, Color.White * 0.7f, 1.9f);
            DrawCenteredText(spriteBatch, pixel, string.Concat("HIGH ", PlayerStatus.HighScore.ToString()), Game1.ScreenSize.X / 2f, 186f, Color.White * 0.84f, 1.7f);

            List<UiButton> buttons = GetTitleButtons();
            for (int i = 0; i < buttons.Count; i++)
                DrawButton(spriteBatch, pixel, buttons[i], i == titleSelection);

            if (!options.TutorialCompleted)
            {
                DrawCenteredText(spriteBatch, pixel, "FIRST LAUNCH STARTS THE TUTORIAL PROLOGUE", Game1.ScreenSize.X / 2f, 230f, Color.Orange * 0.9f, 1.25f);
                DrawCenteredText(spriteBatch, pixel, "START WITHOUT TUTORIAL SKIPS IT IMMEDIATELY", Game1.ScreenSize.X / 2f, 254f, Color.White * 0.66f, 1.05f);
            }

            DrawCenteredText(spriteBatch, pixel, "DEVELOPED BY SABINO SOFTWARE  RELEASED UNDER THE UNLICENSE", Game1.ScreenSize.X / 2f, Game1.VirtualHeight - 150f, Color.White * 0.66f, 1.08f);
            string medalText = medals.CampaignClear
                ? (medals.PerfectCampaign ? "PERFECT CAMPAIGN MEDAL UNLOCKED" : "CAMPAIGN CLEAR MEDAL UNLOCKED")
                : "NO CAMPAIGN MEDALS YET";
            DrawCenteredText(spriteBatch, pixel, medalText, Game1.ScreenSize.X / 2f, Game1.VirtualHeight - 116f, Color.White * 0.65f, 1.35f);
#if ANDROID
            DrawCenteredText(spriteBatch, pixel, "TAP A BUTTON TO NAVIGATE  HELP AND LEGAL ARE BELOW", Game1.ScreenSize.X / 2f, Game1.VirtualHeight - 82f, Color.White * 0.58f, 1.15f);
#else
            DrawCenteredText(spriteBatch, pixel, "ENTER CONFIRM  F1 HELP  ARROWS OR POINTER NAVIGATE", Game1.ScreenSize.X / 2f, Game1.VirtualHeight - 82f, Color.White * 0.58f, 1.15f);
#endif
        }

        private void DrawSabinoIntro(SpriteBatch spriteBatch, Texture2D pixel)
        {
            Rectangle logoBounds = GetTitleLogoBounds();
            float progress = 1f - MathHelper.Clamp(titleIntroTimer / TitleIntroDurationSeconds, 0f, 1f);
            float fade = MathHelper.Clamp(progress / 0.38f, 0f, 1f);
            float promptPulse = 0.4f + 0.6f * MathF.Abs(MathF.Sin((float)Game1.GameTime.TotalGameTime.TotalSeconds * 4.2f));

            DrawCenteredText(spriteBatch, pixel, "SABINO SOFTWARE", Game1.ScreenSize.X / 2f, logoBounds.Y + 72f, titleIntroPrimaryColor * (0.92f * fade), 2.45f);
            DrawCenteredText(spriteBatch, pixel, "PIXEL FOUNDRY", Game1.ScreenSize.X / 2f, logoBounds.Bottom - 46f, titleIntroSecondaryColor * (0.88f * fade), 1.35f);

            for (int i = 0; i < introPixels.Count; i++)
            {
                IntroPixel px = introPixels[i];
                Color tint = px.Color * (titleIntroExploded ? 0.75f * fade : 0.92f * fade);
                spriteBatch.Draw(pixel, new Rectangle((int)px.Position.X, (int)px.Position.Y, 4, 4), tint);
            }

#if ANDROID
            DrawCenteredText(spriteBatch, pixel, "TAP THE LOGO TO DETONATE + SKIP", Game1.ScreenSize.X / 2f, Game1.VirtualHeight - 120f, titleIntroPromptColor * ((0.35f + promptPulse * 0.35f) * fade), 1.12f);
#else
            DrawCenteredText(spriteBatch, pixel, "TAP OR CLICK LOGO TO DETONATE + SKIP", Game1.ScreenSize.X / 2f, Game1.VirtualHeight - 120f, titleIntroPromptColor * ((0.35f + promptPulse * 0.35f) * fade), 1.12f);
#endif
            DrawCenteredText(spriteBatch, pixel, "GENERATING PROCEDURAL SYSTEMS...", Game1.ScreenSize.X / 2f, Game1.VirtualHeight - 86f, titleIntroSecondaryColor * (0.62f * fade), 1.04f);
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Black * (1f - fade));
        }

        private void DrawPause(SpriteBatch spriteBatch, Texture2D pixel)
        {
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Black * 0.45f);
            DrawCenteredText(spriteBatch, pixel, "PAUSED", Game1.ScreenSize.X / 2f, 148f, Color.White, 3f);
            List<UiButton> buttons = GetPauseButtons();
            for (int i = 0; i < buttons.Count; i++)
                DrawButton(spriteBatch, pixel, buttons[i], i == pauseSelection);
            if (pauseReturnState == GameFlowState.Tutorial)
                DrawCenteredText(spriteBatch, pixel, "SKIP TUTORIAL STARTS THE REAL CAMPAIGN", Game1.ScreenSize.X / 2f, Game1.VirtualHeight - 92f, Color.Orange * 0.8f, 1.2f);
#if ANDROID
            DrawCenteredText(spriteBatch, pixel, "TAP A BUTTON OR TAP THE PAUSE CHIP AGAIN TO RETURN", Game1.ScreenSize.X / 2f, Game1.VirtualHeight - 62f, Color.White * 0.62f, 1.05f);
#else
            DrawCenteredText(spriteBatch, pixel, "ENTER CONFIRMS  ESC RETURNS", Game1.ScreenSize.X / 2f, Game1.VirtualHeight - 62f, Color.White * 0.62f, 1.05f);
#endif
        }

        private void DrawOptions(SpriteBatch spriteBatch, Texture2D pixel)
        {
            int frameMarginX = UiPx(90);
            int frameMarginY = UiPx(90);
            if (slotReturnState == GameFlowState.Title)
                DrawBackdrop(spriteBatch, pixel, 0.8f, "CONFIGURE VISUALS AND DISPLAY");
            else
                spriteBatch.Draw(pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Black * 0.55f);

            spriteBatch.Draw(pixel, new Rectangle(frameMarginX, frameMarginY, Game1.VirtualWidth - frameMarginX * 2, Game1.VirtualHeight - frameMarginY * 2), Color.Black * 0.75f);
            DrawCenteredText(spriteBatch, pixel, "OPTIONS", Game1.ScreenSize.X / 2f, 112f, Color.White, 3f);

            string[] rows = GetOptionRows();
            int rowHeight = UiPx(rows.Length > 12 ? 28 : 32);
            int rowStep = UiPx(rows.Length > 12 ? 34 : 42);
            int top = UiPx(rows.Length > 12 ? 156 : 172);
            int rowWidth = Game1.VirtualWidth - UiPx(360);
            int rowX = (Game1.VirtualWidth - rowWidth) / 2;

            for (int i = 0; i < rows.Length; i++)
            {
                Rectangle rowBounds = new Rectangle(rowX, top + i * rowStep, rowWidth, rowHeight);
                DrawPanel(spriteBatch, pixel, rowBounds, i == optionsSelection ? Color.White * 0.12f : Color.White * 0.05f, i == optionsSelection ? Color.Orange : Color.White * 0.2f);
                BitmapFontRenderer.Draw(spriteBatch, pixel, rows[i], new Vector2(rowBounds.X + UiPx(18), rowBounds.Y + UiPx(4)), Color.White, rows.Length > 12 ? 1.05f : 1.15f);
            }

#if ANDROID
            DrawCenteredText(spriteBatch, pixel, "SWIPE A ROW LEFT OR RIGHT TO ADJUST  TAP LEFT TO LOWER  TAP RIGHT TO RAISE", Game1.ScreenSize.X / 2f, Game1.VirtualHeight - UiPx(120), Color.White * 0.75f, 0.96f);
            DrawCenteredText(spriteBatch, pixel, "USE ANDROID BACK TO CLOSE", Game1.ScreenSize.X / 2f, Game1.VirtualHeight - UiPx(88), Color.White * 0.62f, 0.98f);
#else
            DrawCenteredText(spriteBatch, pixel, "LEFT RIGHT OR ENTER TO CHANGE  ESC TO CLOSE", Game1.ScreenSize.X / 2f, Game1.VirtualHeight - UiPx(96), Color.White * 0.75f, 1.1f);
#endif
        }

        private void DrawSaveSlots(SpriteBatch spriteBatch, Texture2D pixel, bool saving)
        {
            int frameMarginX = UiPx(90);
            int frameMarginY = UiPx(90);
            if (slotReturnState == GameFlowState.Title)
                DrawBackdrop(spriteBatch, pixel, 0.74f, saving ? "STORE A RUN SNAPSHOT" : "RESTORE A RUN SNAPSHOT");
            else
                spriteBatch.Draw(pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Black * 0.55f);

            spriteBatch.Draw(pixel, new Rectangle(frameMarginX, frameMarginY, Game1.VirtualWidth - frameMarginX * 2, Game1.VirtualHeight - frameMarginY * 2), Color.Black * 0.75f);
            DrawCenteredText(spriteBatch, pixel, saving ? "SAVE SLOTS" : "LOAD SLOTS", Game1.ScreenSize.X / 2f, 112f, Color.White, 3f);

            SaveSlotSummary[] summaries = PersistentStorage.LoadSaveSlotSummaries();
            for (int i = 0; i < 3; i++)
            {
                SaveSlotSummary summary = summaries[i];
                Rectangle rowBounds = new Rectangle((Game1.VirtualWidth - UiPx(920)) / 2, UiPx(180) + i * UiPx(88), UiPx(920), UiPx(68));
                DrawPanel(spriteBatch, pixel, rowBounds, i == slotSelection ? Color.White * 0.12f : Color.White * 0.05f, i == slotSelection ? Color.Orange : Color.White * 0.2f);
                string line = summary.HasData
                    ? string.Concat("SLOT ", (i + 1).ToString(), "  STAGE ", summary.StageNumber.ToString("00"), "  SCORE ", summary.Score.ToString(), "  ", summary.ActiveStyle)
                    : string.Concat("SLOT ", (i + 1).ToString(), "  EMPTY");
                BitmapFontRenderer.Draw(spriteBatch, pixel, line, new Vector2(rowBounds.X + UiPx(16), rowBounds.Y + UiPx(10)), Color.White, 1.5f);
                if (summary.HasData && !string.IsNullOrWhiteSpace(summary.SavedAtUtc))
                    BitmapFontRenderer.Draw(spriteBatch, pixel, summary.SavedAtUtc, new Vector2(rowBounds.X + UiPx(16), rowBounds.Y + UiPx(38)), Color.White * 0.65f, 1.1f);
            }

            Rectangle backBounds = new Rectangle((Game1.VirtualWidth - UiPx(920)) / 2, UiPx(180) + 3 * UiPx(88), UiPx(920), UiPx(58));
            DrawPanel(spriteBatch, pixel, backBounds, slotSelection == 3 ? Color.White * 0.12f : Color.White * 0.05f, slotSelection == 3 ? Color.Orange : Color.White * 0.2f);
            BitmapFontRenderer.Draw(spriteBatch, pixel, "BACK", new Vector2(backBounds.X + UiPx(16), backBounds.Y + UiPx(12)), Color.White, 1.5f);

#if ANDROID
            DrawCenteredText(spriteBatch, pixel, saving ? "TAP A SLOT TO OVERWRITE IT  USE ANDROID BACK TO RETURN" : "TAP A SLOT TO LOAD IT  USE ANDROID BACK TO RETURN", Game1.ScreenSize.X / 2f, Game1.VirtualHeight - UiPx(120), Color.White * 0.75f, 1.12f);
#else
            DrawCenteredText(spriteBatch, pixel, saving ? "ENTER TO OVERWRITE SLOT" : "ENTER TO LOAD SLOT", Game1.ScreenSize.X / 2f, Game1.VirtualHeight - UiPx(120), Color.White * 0.75f, 1.4f);
#endif
        }

        private void DrawHelp(SpriteBatch spriteBatch, Texture2D pixel)
        {
            if (helpReturnState == GameFlowState.Title)
                DrawBackdrop(spriteBatch, pixel, 0.78f, "RUN THE TUTORIAL AGAIN FROM PAGE 1 OR OPEN ABOUT / LEGAL");
            else
                spriteBatch.Draw(pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Black * 0.55f);

            spriteBatch.Draw(pixel, new Rectangle(90, 90, Game1.VirtualWidth - 180, Game1.VirtualHeight - 180), Color.Black * 0.75f);
            DrawCenteredText(spriteBatch, pixel, "HELP", Game1.ScreenSize.X / 2f, 112f, Color.White, 3f);
            DrawCenteredText(spriteBatch, pixel, string.Concat("PAGE ", (helpPageIndex + 1).ToString(), " / ", HelpPageCount.ToString()), Game1.ScreenSize.X / 2f, 152f, Color.White * 0.7f, 1.5f);

            switch (helpPageIndex)
            {
                case 0:
#if ANDROID
                    DrawHelpPage(spriteBatch, pixel, "CONTROLS\nLEFT PAD MOVE  RIGHT PAD AIM AND FIRE\nTOP WEAPON HUD SWAPS STYLE  TOP STAGE CHIP PAUSES\nHOLD THE TOP RIGHT R BUTTON TO REWIND\nTAP HERE TO REPLAY THE TUTORIAL", 186f);
#else
                    DrawHelpPage(spriteBatch, pixel, "CONTROLS\nWASD MOVE  ARROWS AIM  SPACE FIRE\nQ E SWITCH STYLE  R REWIND  ESC PAUSE  F1 HELP\nPRESS ENTER HERE TO REPLAY THE TUTORIAL", 186f);
#endif
                    break;
                case 1:
                    DrawHelpPage(spriteBatch, pixel, "POWER CORES\nEACH P CORE MATCHES A WEAPON STYLE\nMATCH YOUR ACTIVE STYLE TO BOOST IT IMMEDIATELY UP TO LEVEL 3\nOTHER CORES STORE STYLE SPECIFIC CHARGES FOR TRANSITION DRAFTS\nIF TIME RUNS OUT THE HIGHLIGHTED CARD IS AUTO PICKED", 186f);
                    DrawWeaponIcons(spriteBatch, pixel, 410f, 0, 5);
                    break;
                case 2:
#if ANDROID
                    DrawHelpPage(spriteBatch, pixel, "STYLE LADDER\nLEVELS 0 TO 3 IMPROVE THE CURRENT STYLE\nAFTER LEVEL 3 NEW SURGES UNLOCK MORE STYLES\nFURTHER SURGES RAISE STYLE RANKS WITH DIMINISHING RETURNS\nTAP THE TOP WEAPON HUD TO ROTATE OWNED STYLES", 186f);
#else
                    DrawHelpPage(spriteBatch, pixel, "STYLE LADDER\nLEVELS 0 TO 3 IMPROVE THE CURRENT STYLE\nAFTER LEVEL 3 NEW SURGES UNLOCK MORE STYLES\nFURTHER SURGES RAISE STYLE RANKS WITH DIMINISHING RETURNS\nSWAP OWNED STYLES ANY TIME WITH Q AND E", 186f);
#endif
                    DrawWeaponIcons(spriteBatch, pixel, 410f, 5, 5);
                    break;
                case 3:
                    DrawHelpPage(spriteBatch, pixel, "LIVES AND SHIPS\nSHIPS ARE YOUR IN PLACE RESPAWNS\nIF SHIPS HIT ZERO THE NEXT DEATH COSTS A LIFE\nLOSING A LIFE RESTARTS THE WHOLE STAGE\nDEATH ALSO WEAKENS YOUR CURRENT LOADOUT", 186f);
                    break;
                case 4:
#if ANDROID
                    DrawHelpPage(spriteBatch, pixel, "FX AND REWIND\nHOLD THE TOP RIGHT REWIND BUTTON TO REWIND 8 SECONDS\nREWIND STARTS SLOW AND ACCELERATES THE LONGER YOU HOLD\nLOADS AND REWINDS DISABLE MEDALS FOR THE RUN\nLOW STANDARD AND NEON VISUAL PRESETS ARE IN OPTIONS", 186f);
#else
                    DrawHelpPage(spriteBatch, pixel, "FX AND REWIND\nHOLD R TO REWIND 8 SECONDS OF GAMEPLAY\nREWIND STARTS SLOW AND ACCELERATES THE LONGER YOU HOLD\nLOADS AND REWINDS DISABLE MEDALS FOR THE RUN\nLOW STANDARD AND NEON VISUAL PRESETS ARE IN OPTIONS", 186f);
#endif
                    break;
                case AboutHelpPageIndex:
                    DrawHelpPage(spriteBatch, pixel, AboutHelpText, 186f, 1.34f);
                    break;
                default:
                    DrawHelpPage(spriteBatch, pixel, UnlicenseText, 182f, 0.84f);
                    break;
            }

#if ANDROID
            DrawCenteredText(spriteBatch, pixel, helpPageIndex == 0 ? "SWIPE LEFT OR RIGHT  TAP TO REPLAY TUTORIAL  USE ANDROID BACK TO CLOSE" : "SWIPE LEFT OR RIGHT  TAP TO CLOSE OR USE ANDROID BACK", Game1.ScreenSize.X / 2f, Game1.VirtualHeight - 120f, Color.White * 0.75f, 1.05f);
#else
            DrawCenteredText(spriteBatch, pixel, helpPageIndex == 0 ? "LEFT RIGHT TO CHANGE PAGE  ENTER TO REPLAY TUTORIAL  ESC TO CLOSE" : "LEFT RIGHT TO CHANGE PAGE  ENTER ESC F1 TO CLOSE", Game1.ScreenSize.X / 2f, Game1.VirtualHeight - 120f, Color.White * 0.75f, 1.35f);
#endif
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
            DrawHelpPage(spriteBatch, pixel, text, y, 1.6f);
        }

        private void DrawHelpPage(SpriteBatch spriteBatch, Texture2D pixel, string text, float y, float scale)
        {
            Vector2 size = BitmapFontRenderer.Measure(text, scale);
            BitmapFontRenderer.Draw(spriteBatch, pixel, text, new Vector2(Game1.ScreenSize.X / 2f - size.X / 2f, y), Color.White, scale);
        }

        private void DrawHud(SpriteBatch spriteBatch, Texture2D pixel)
        {
            float hudPulse = Game1.Instance?.HudPulse ?? 0f;
            float pickupPulse = Game1.Instance?.PickupPulse ?? 0f;
            WeaponInventoryState inventory = PlayerStatus.RunProgress.Weapons;
            WeaponStyleDefinition activeStyle = WeaponCatalog.GetStyle(inventory.ActiveStyle);
            string stageLabel = state switch
            {
                GameFlowState.Tutorial => "TUTORIAL",
                GameFlowState.UpgradeDraft => "UPGRADE",
                _ when state == GameFlowState.StageTransition && transitionToBoss => "BOSS RUN",
                _ when state == GameFlowState.StageTransition && transitionTargetStageNumber > currentStageNumber => string.Concat("JUMP ", transitionTargetStageNumber.ToString("00")),
                _ => string.Concat("STAGE ", currentStageNumber.ToString("00")),
            };
            string scoreLabel = string.Concat("SCORE ", PlayerStatus.Score.ToString());
            HudLayout layout = HudLayoutCalculator.Calculate(Game1.VirtualWidth, state, currentStageNumber, transitionTargetStageNumber, transitionToBoss, inventory, PlayerStatus.Score);
            Rectangle livesBounds = layout.LivesBounds;
            Rectangle activeBounds = layout.ActiveBounds;
            Rectangle ownedBounds = layout.OwnedBounds;
            Rectangle stageBounds = layout.StageBounds;
            Rectangle pityBounds = layout.PityBounds;
            Rectangle scoreBounds = layout.ScoreBounds;

            DrawPanel(spriteBatch, pixel, livesBounds, Color.Black * (0.22f + hudPulse * 0.05f), Color.White * (0.18f + hudPulse * 0.08f));
            DrawPanel(spriteBatch, pixel, activeBounds, Color.Black * (0.18f + pickupPulse * 0.06f), Color.White * (0.14f + pickupPulse * 0.12f));
            DrawPanel(spriteBatch, pixel, ownedBounds, Color.Black * 0.16f, Color.White * 0.12f);
            DrawPanel(spriteBatch, pixel, stageBounds, Color.Black * (0.18f + hudPulse * 0.03f), Color.White * 0.14f);
            DrawPanel(spriteBatch, pixel, pityBounds, Color.Black * (0.18f + pickupPulse * 0.04f), Color.White * (0.14f + pickupPulse * 0.08f));
            DrawPanel(spriteBatch, pixel, scoreBounds, Color.Black * 0.22f, Color.White * (0.18f + hudPulse * 0.05f));

            BitmapFontRenderer.Draw(spriteBatch, pixel, string.Concat("LIVES ", PlayerStatus.Lives.ToString()), new Vector2(livesBounds.X + 12f, livesBounds.Y + 12f), Color.White, 2f);
            BitmapFontRenderer.Draw(spriteBatch, pixel, string.Concat("SHIPS ", PlayerStatus.Ships.ToString()), new Vector2(livesBounds.X + 12f, livesBounds.Y + 38f), Color.White, 2f);
            BitmapFontRenderer.Draw(spriteBatch, pixel, string.Concat("SAFE ", Math.Max(0f, Player1.Instance.HullRatio * 100f).ToString("0"), "%"), new Vector2(livesBounds.X + 12f, livesBounds.Bottom - 22f), Color.White * 0.55f, 1.15f);

            DrawStyleHud(spriteBatch, pixel, activeBounds);
            DrawOwnedStyleHud(spriteBatch, pixel, ownedBounds, inventory);
#if ANDROID
            BitmapFontRenderer.Draw(spriteBatch, pixel, "TAP WEAPONS TO SWAP", new Vector2(ownedBounds.X + 12f, ownedBounds.Y + 24f), Color.White * 0.4f, 0.78f);
#endif

            DrawCenteredText(spriteBatch, pixel, stageLabel, stageBounds.Center.X, stageBounds.Y + 14f, Color.White, GetFittedScale(stageLabel, stageBounds.Width - 20f, 1.7f, 1.15f));
            string stageSubLabel = state == GameFlowState.StageTransition
                ? (transitionToBoss ? "THREAT LOCK" : "FTL TRANSIT")
                : (!string.IsNullOrEmpty(activeEventWarning) ? activeEventWarning : (state == GameFlowState.Tutorial ? tutorialStep.ToString().ToUpperInvariant() : currentStage?.Name?.ToUpperInvariant() ?? "RUN"));
            DrawCenteredText(spriteBatch, pixel, stageSubLabel, stageBounds.Center.X, stageBounds.Y + 46f, !string.IsNullOrEmpty(activeEventWarning) ? Color.Orange : Color.White * 0.7f, GetFittedScale(stageSubLabel, stageBounds.Width - 20f, 1.08f, 0.82f));
#if ANDROID
            Rectangle pauseChip = HudLayoutCalculator.GetAndroidPauseChipBounds(layout);
            DrawPanel(spriteBatch, pixel, pauseChip, Color.Black * 0.3f, Color.Orange * 0.42f);
            DrawCenteredText(spriteBatch, pixel, "PAUSE", pauseChip.Center.X, pauseChip.Y + 5f, Color.White, 0.84f);
#endif

            BitmapFontRenderer.Draw(spriteBatch, pixel, "PITY", new Vector2(pityBounds.X + 12f, pityBounds.Y + 10f), Color.White, 1.25f);
            DrawBar(spriteBatch, pixel, new Rectangle(pityBounds.X + 12, pityBounds.Y + 34, pityBounds.Width - 24, 12), PlayerStatus.RunProgress.Powerups.PityMeter, Color.Lerp(Color.Orange, Color.White, pickupPulse * 0.35f));
            DrawChargeTray(spriteBatch, pixel, pityBounds, inventory);

            BitmapFontRenderer.Draw(spriteBatch, pixel, scoreLabel, new Vector2(scoreBounds.X + 12f, scoreBounds.Y + 10f), Color.White, 1.65f);
            BitmapFontRenderer.Draw(spriteBatch, pixel, string.Concat("MULTI ", PlayerStatus.Multiplier.ToString()), new Vector2(scoreBounds.X + 12f, scoreBounds.Y + 34f), Color.White, 1.45f);
            BitmapFontRenderer.Draw(spriteBatch, pixel, "REWIND", new Vector2(scoreBounds.X + 12f, scoreBounds.Y + 56f), Color.White * 0.85f, 1.08f);
            DrawBar(spriteBatch, pixel, new Rectangle(scoreBounds.X + 92, scoreBounds.Y + 58, scoreBounds.Width - 104, 10), rewindMeterSeconds / RewindCapacitySeconds, Color.Lerp(Color.Cyan, Color.White, hudPulse * 0.2f) * 0.9f);

            if (activeBoss != null && !activeBoss.IsExpired)
                DrawBossHealthBar(spriteBatch, pixel, activeBoss);
        }

        private void DrawStyleHud(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds)
        {
            WeaponInventoryState inventory = PlayerStatus.RunProgress.Weapons;
            WeaponStyleDefinition activeStyle = WeaponCatalog.GetStyle(inventory.ActiveStyle);
            Color primary = ColorUtil.ParseHex(activeStyle.PrimaryColor, Color.White);
            Color secondary = ColorUtil.ParseHex(activeStyle.SecondaryColor, Color.LightBlue);
            Color accent = ColorUtil.ParseHex(activeStyle.AccentColor, Color.Orange);

            PixelArtRenderer.DrawRows(spriteBatch, pixel, activeStyle.IconRows, new Vector2(bounds.X + 22f, bounds.Y + 46f), 4.5f, primary, secondary, accent, true);
            BitmapFontRenderer.Draw(spriteBatch, pixel, activeStyle.DisplayName, new Vector2(bounds.X + 60f, bounds.Y + 10f), Color.White, GetFittedScale(activeStyle.DisplayName, bounds.Width - 72f, 1.8f, 1.15f));
            string levelLabel = string.Concat("LV ", inventory.ActiveLevel.ToString(), inventory.ActiveRank > 0 ? string.Concat("  RK ", inventory.ActiveRank.ToString()) : string.Empty);
            BitmapFontRenderer.Draw(spriteBatch, pixel, levelLabel, new Vector2(bounds.X + 60f, bounds.Y + 34f), Color.White * 0.72f, 1.18f);

            for (int i = 0; i < 4; i++)
            {
                Color pip = i <= inventory.ActiveLevel ? accent : Color.White * 0.14f;
                spriteBatch.Draw(pixel, new Rectangle(bounds.X + 62 + i * 18, bounds.Y + 58, 12, 12), pip);
            }
        }

        private void DrawOwnedStyleHud(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, WeaponInventoryState inventory)
        {
            IReadOnlyList<WeaponStyleId> ownedStyles = inventory.OwnedStyles;
            BitmapFontRenderer.Draw(spriteBatch, pixel, "ARSENAL", new Vector2(bounds.X + 12f, bounds.Y + 10f), Color.White * 0.8f, 1.05f);
            if (ownedStyles.Count == 0)
                return;

            float availableWidth = bounds.Width - 24f;
            float spacing = ownedStyles.Count <= 1 ? 0f : MathF.Min(34f, availableWidth / Math.Max(1f, ownedStyles.Count - 1));
            float scale = ownedStyles.Count >= 8 ? 1.45f : 1.7f;
            float totalWidth = (ownedStyles.Count - 1) * spacing;
            float startX = bounds.X + 12f + Math.Max(0f, (availableWidth - totalWidth) * 0.5f);
            for (int i = 0; i < ownedStyles.Count; i++)
            {
                WeaponStyleDefinition style = WeaponCatalog.GetStyle(ownedStyles[i]);
                float x = startX + i * spacing;
                Color iconAccent = ColorUtil.ParseHex(style.AccentColor, Color.Orange);
                Vector2 iconPosition = new Vector2(x, bounds.Y + 58f);
                PixelArtRenderer.DrawRows(spriteBatch, pixel, style.IconRows, iconPosition, scale, ColorUtil.ParseHex(style.PrimaryColor, Color.White), ColorUtil.ParseHex(style.SecondaryColor, Color.LightBlue), iconAccent, true);
                if (ownedStyles[i] == inventory.ActiveStyle)
                    spriteBatch.Draw(pixel, new Rectangle((int)x - 10, bounds.Bottom - 10, 22, 3), iconAccent);

                int stored = inventory.GetStoredCharge(ownedStyles[i]);
                if (stored > 0)
                {
                    Rectangle badgeBounds = new Rectangle((int)x + 6, bounds.Y + 20, stored >= 10 ? 26 : 18, 14);
                    DrawPanel(spriteBatch, pixel, badgeBounds, iconAccent * 0.18f, iconAccent * 0.72f);
                    BitmapFontRenderer.Draw(spriteBatch, pixel, stored.ToString(), new Vector2(badgeBounds.X + 4f, badgeBounds.Y + 2f), Color.White, 0.82f);
                }
            }
        }

        private void DrawChargeTray(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, WeaponInventoryState inventory)
        {
            IReadOnlyList<WeaponStyleId> chargedStyles = inventory.ChargedStyles;
            if (chargedStyles.Count == 0)
            {
                BitmapFontRenderer.Draw(spriteBatch, pixel, "CORES BANK 0", new Vector2(bounds.X + 12f, bounds.Y + 54f), Color.White * 0.58f, 1.02f);
                return;
            }

            int visibleCount = System.Math.Min(4, chargedStyles.Count);
            float spacing = 34f;
            float startX = bounds.X + 14f;
            for (int i = 0; i < visibleCount; i++)
            {
                WeaponStyleId styleId = chargedStyles[i];
                WeaponStyleDefinition style = WeaponCatalog.GetStyle(styleId);
                float x = startX + i * spacing;
                PixelArtRenderer.DrawRows(spriteBatch, pixel, style.IconRows, new Vector2(x, bounds.Y + 63f), 1.55f, ColorUtil.ParseHex(style.PrimaryColor, Color.White), ColorUtil.ParseHex(style.SecondaryColor, Color.LightBlue), ColorUtil.ParseHex(style.AccentColor, Color.Orange), true);
                BitmapFontRenderer.Draw(spriteBatch, pixel, inventory.GetStoredCharge(styleId).ToString(), new Vector2(x + 11f, bounds.Y + 56f), Color.White * 0.86f, 0.84f);
            }

            if (chargedStyles.Count > visibleCount)
                BitmapFontRenderer.Draw(spriteBatch, pixel, string.Concat("+", (chargedStyles.Count - visibleCount).ToString()), new Vector2(bounds.Right - 28f, bounds.Y + 55f), Color.White * 0.7f, 0.95f);
        }

        private static float GetFittedScale(string text, float maxWidth, float preferredScale, float minimumScale)
        {
            if (string.IsNullOrWhiteSpace(text))
                return preferredScale;

            float measuredWidth = BitmapFontRenderer.Measure(text, preferredScale).X;
            if (measuredWidth <= maxWidth || measuredWidth <= 0f)
                return preferredScale;

            float ratio = maxWidth / measuredWidth;
            return MathHelper.Clamp(preferredScale * ratio, minimumScale, preferredScale);
        }

        private void DrawBossHealthBar(SpriteBatch spriteBatch, Texture2D pixel, BossEnemy boss)
        {
            Rectangle bounds = new Rectangle(320, 94, 640, 18);
            spriteBatch.Draw(pixel, bounds, Color.White * 0.12f);
            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, (int)(bounds.Width * boss.HealthRatio), bounds.Height), Color.OrangeRed);
            for (int i = 0; i < boss.PhaseThresholds.Count; i++)
            {
                int tickX = bounds.X + (int)MathF.Round(bounds.Width * boss.PhaseThresholds[i]);
                spriteBatch.Draw(pixel, new Rectangle(tickX, bounds.Y - 2, 2, bounds.Height + 4), Color.White * 0.28f);
            }
            DrawCenteredText(spriteBatch, pixel, boss.DisplayName, Game1.ScreenSize.X / 2f, 118f, Color.White, 1.5f);
        }

        private void DrawTransitionOverlay(SpriteBatch spriteBatch, Texture2D pixel)
        {
            float warp = TransitionWarpStrength;
            if (warp <= 0f)
                return;

            float time = (float)Game1.GameTime.TotalGameTime.TotalSeconds;
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Lerp(Color.Transparent, new Color(180, 230, 255), 0.08f + 0.12f * warp));

            int streaks = 18 + (int)(warp * 34f);
            for (int i = 0; i < streaks; i++)
            {
                float ySeed = MathF.Abs(MathF.Sin(i * 17.137f + 0.52f));
                float xSeed = MathF.Abs(MathF.Sin(i * 8.913f + 0.17f + time));
                float width = 120f + warp * (180f + xSeed * 260f);
                float x = Game1.VirtualWidth - ((time * (180f + 360f * warp) + xSeed * (Game1.VirtualWidth + width)) % (Game1.VirtualWidth + width));
                float y = 94f + ySeed * (Game1.VirtualHeight - 188f);
                spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, (int)width, 2 + (int)(warp * 3f)), new Color(170, 240, 255) * (0.06f + 0.1f * warp));
            }

            Rectangle chip = new Rectangle(Game1.VirtualWidth / 2 - 180, 118, 360, 44);
            DrawPanel(spriteBatch, pixel, chip, Color.Black * 0.24f, Color.White * 0.2f);
            string header = transitionToBoss ? "BOSS APPROACH" : "FTL TRANSIT";
            string detail = transitionToBoss
                ? currentStage?.Boss?.DisplayName?.ToUpperInvariant() ?? "THREAT LOCK"
                : string.Concat("JUMPING TO STAGE ", transitionTargetStageNumber.ToString("00"));
            DrawCenteredText(spriteBatch, pixel, header, chip.Center.X, chip.Y + 6f, Color.White * 0.85f, 1.15f);
            DrawCenteredText(spriteBatch, pixel, detail, chip.Center.X, chip.Y + 24f, Color.Orange * (0.78f + warp * 0.22f), 1.15f);

            if (PlayerStatus.RunProgress.StoredUpgradeCharges > 0 && !transitionToBoss)
                DrawCenteredText(spriteBatch, pixel, "UPGRADE DRAFT CHARGED", Game1.ScreenSize.X / 2f, Game1.VirtualHeight - 64f, Color.White * 0.72f, 1.15f);
        }

        private void DrawRewindOverlay(SpriteBatch spriteBatch, Texture2D pixel)
        {
            float strength = RewindVisualStrength;
            if (strength <= 0.01f)
                return;

            float speed = GetRewindSpeedMultiplier(rewindHoldSeconds);
            float time = (float)Game1.GameTime.TotalGameTime.TotalSeconds;
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Cyan * (0.025f + 0.1f * strength));

            int streaks = 12 + (int)(strength * 20f);
            for (int i = 0; i < streaks; i++)
            {
                float ySeed = MathF.Abs(MathF.Sin(i * 22.177f + 0.48f));
                float width = 48f + strength * 160f + (i % 5) * 18f;
                float x = (time * (140f + strength * 320f) + i * 92f) % (Game1.VirtualWidth + width) - width;
                float y = 92f + ySeed * (Game1.VirtualHeight - 184f);
                spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, (int)width, 2), Color.White * (0.05f + 0.08f * strength));
            }

            Rectangle panel = new Rectangle(Game1.VirtualWidth - 230, Game1.VirtualHeight - 86, 210, 50);
            DrawPanel(spriteBatch, pixel, panel, Color.Black * 0.22f, Color.Cyan * 0.3f);
            BitmapFontRenderer.Draw(spriteBatch, pixel, string.Concat("REWIND x", speed.ToString("0.0")), new Vector2(panel.X + 12f, panel.Y + 8f), Color.White, 1.3f);
            BitmapFontRenderer.Draw(spriteBatch, pixel, speed < 1f ? "HOLD TO ACCELERATE" : "RELEASING RESTORES FLOW", new Vector2(panel.X + 12f, panel.Y + 28f), Color.White * 0.65f, 0.95f);
        }

        private void DrawTutorialOverlay(SpriteBatch spriteBatch, Texture2D pixel)
        {
            Rectangle panel = new Rectangle(160, Game1.VirtualHeight - 156, Game1.VirtualWidth - 320, 118);
            DrawPanel(spriteBatch, pixel, panel, Color.Black * 0.34f, Color.Orange * 0.28f);

            string title;
            string body;
            int stepIndex;
            GetTutorialPrompt(out title, out body, out stepIndex);
            BitmapFontRenderer.Draw(spriteBatch, pixel, string.Concat("TUTORIAL ", stepIndex.ToString(), " / 8  ", title), new Vector2(panel.X + 16f, panel.Y + 12f), Color.White, 1.35f);
            BitmapFontRenderer.Draw(spriteBatch, pixel, body, new Vector2(panel.X + 16f, panel.Y + 38f), Color.White * 0.82f, 1.15f);
#if ANDROID
            BitmapFontRenderer.Draw(spriteBatch, pixel, "WEAPON HUD SWAPS  PAUSE CHIP OPENS SKIP MENU", new Vector2(panel.X + 16f, panel.Bottom - 24f), Color.White * 0.6f, 0.87f);
#else
            BitmapFontRenderer.Draw(spriteBatch, pixel, "ESC PAUSE  F1 HELP", new Vector2(panel.X + 16f, panel.Bottom - 24f), Color.White * 0.6f, 0.95f);
#endif
        }

        private void DrawUpgradeDraft(SpriteBatch spriteBatch, Texture2D pixel)
        {
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Black * 0.58f);

            DrawCenteredText(spriteBatch, pixel, "UPGRADE DRAFT", Game1.ScreenSize.X / 2f, 108f, Color.White, 2.6f);
            DrawCenteredText(spriteBatch, pixel, options.AutoUpgradeDraft ? "AUTO DRAFT ENABLED" : string.Concat("CHOOSE IN ", Math.Max(0f, draftTimer).ToString("0.0"), "s"), Game1.ScreenSize.X / 2f, 146f, Color.White * 0.72f, 1.18f);

            int cardWidth = 300;
            int cardHeight = 220;
            int gap = 24;
            int totalWidth = cardWidth * 3 + gap * 2;
            int startX = (Game1.VirtualWidth - totalWidth) / 2;
            int y = 214;

            for (int i = 0; i < draftCards.Count; i++)
            {
                UpgradeDraftCard card = draftCards[i];
                Rectangle bounds = new Rectangle(startX + i * (cardWidth + gap), y, cardWidth, cardHeight);
                Color accent = ColorUtil.ParseHex(card.AccentColor, Color.Orange);
                DrawPanel(spriteBatch, pixel, bounds, i == draftSelection ? accent * 0.16f : Color.Black * 0.45f, i == draftSelection ? accent : Color.White * 0.2f);
                BitmapFontRenderer.Draw(spriteBatch, pixel, card.Title, new Vector2(bounds.X + 18f, bounds.Y + 16f), Color.White, 1.45f);
                BitmapFontRenderer.Draw(spriteBatch, pixel, card.Description, new Vector2(bounds.X + 18f, bounds.Y + 52f), Color.White * 0.8f, 1.05f);

                if (card.Type == UpgradeCardType.WeaponSurge)
                {
                    WeaponStyleDefinition style = WeaponCatalog.GetStyle(card.StyleId);
                    PixelArtRenderer.DrawRows(spriteBatch, pixel, style.IconRows, new Vector2(bounds.Center.X, bounds.Bottom - 56f), 5f, ColorUtil.ParseHex(style.PrimaryColor, Color.White), ColorUtil.ParseHex(style.SecondaryColor, Color.LightBlue), accent, true);
                }
                else
                {
                    spriteBatch.Draw(pixel, new Rectangle(bounds.X + 18, bounds.Bottom - 58, bounds.Width - 36, 14), accent * 0.18f);
                    spriteBatch.Draw(pixel, new Rectangle(bounds.X + 18, bounds.Bottom - 58, Math.Max(10, (bounds.Width - 36) * (i == draftSelection ? 3 : 2) / 5), 14), accent * 0.7f);
                }
            }

            DrawCenteredText(spriteBatch, pixel, string.Concat("STORED CHARGES ", PlayerStatus.RunProgress.StoredUpgradeCharges.ToString()), Game1.ScreenSize.X / 2f, 480f, Color.White * 0.75f, 1.15f);
#if ANDROID
            DrawCenteredText(spriteBatch, pixel, "SWIPE LEFT OR RIGHT TO PICK  TAP A CARD TO LOCK IT", Game1.ScreenSize.X / 2f, 520f, Color.White * 0.62f, 1.05f);
#else
            DrawCenteredText(spriteBatch, pixel, "LEFT RIGHT SELECT  ENTER PICK  ESC AUTO PICK", Game1.ScreenSize.X / 2f, 520f, Color.White * 0.62f, 1.05f);
#endif
        }

        private void GetTutorialPrompt(out string title, out string body, out int stepIndex)
        {
            switch (tutorialStep)
            {
                case TutorialStep.Move:
                    stepIndex = 1;
                    title = "MOVE";
#if ANDROID
                    body = "DRAG THE LEFT PAD TO ROAM THE FIELD. KEEP MOVING FOR A MOMENT TO CONTINUE.";
#else
                    body = "USE WASD TO ROAM THE FIELD. KEEP MOVING FOR A MOMENT TO CONTINUE.";
#endif
                    break;
                case TutorialStep.Aim:
                    stepIndex = 2;
                    title = "AIM";
#if ANDROID
                    body = "DRAG THE RIGHT PAD TO AIM THE CANNON. KEEP IT OFF CENTER FOR A MOMENT.";
#else
                    body = "USE THE ARROW KEYS TO SWING THE CANNON. KEEP IT OFF CENTER FOR A MOMENT.";
#endif
                    break;
                case TutorialStep.Fire:
                    stepIndex = 3;
                    title = "FIRE";
#if ANDROID
                    body = "HOLD THE RIGHT PAD TO FIRE FORWARD. BREAK THE TRAINING TARGET TO ADVANCE.";
#else
                    body = "PRESS SPACE TO FIRE FORWARD. BREAK THE TRAINING TARGET TO ADVANCE.";
#endif
                    break;
                case TutorialStep.Rewind:
                    stepIndex = 4;
                    title = "REWIND";
#if ANDROID
                    body = "TOUCH AND HOLD THE TOP-RIGHT R BUTTON TO REWIND. IT STARTS SLOW, THEN ACCELERATES.";
#else
                    body = "HOLD R. REWIND STARTS VERY SLOW AND SPEEDS UP THE LONGER YOU HOLD IT.";
#endif
                    break;
                case TutorialStep.CollectPower:
                    stepIndex = 5;
                    title = "COLLECT";
                    body = "PICK UP THE SPREAD CORE. MATCHING YOUR ACTIVE STYLE BOOSTS IT NOW. OTHER STYLES STORE A CHARGE FOR THE NEXT DRAFT.";
                    break;
                case TutorialStep.UpgradeDraft:
                    stepIndex = 6;
                    title = "UPGRADE DRAFT";
                    body = "PICK A CARD. TRANSITIONS SPEND STORED CHARGES THIS WAY BETWEEN STAGES.";
                    break;
                case TutorialStep.SwitchStyle:
                    stepIndex = 7;
                    title = "STYLE SWAP";
#if ANDROID
                    body = "TAP THE WEAPON HUD AT THE TOP TO ROTATE OWNED STYLES. THE HUD CAROUSEL SHOWS YOUR LOADOUT.";
#else
                    body = "PRESS Q OR E TO ROTATE BETWEEN OWNED STYLES. THE HUD CAROUSEL SHOWS WHAT YOU HAVE.";
#endif
                    break;
                case TutorialStep.ShipsAndLives:
                    stepIndex = 8;
                    title = "SHIPS AND LIVES";
#if ANDROID
                    body = "SHIPS RESPAWN YOU IN PLACE. RUN OUT OF SHIPS AND A LIFE IS SPENT TO RESTART THE STAGE. TAP TO CONTINUE.";
#else
                    body = "SHIPS RESPAWN YOU IN PLACE. RUN OUT OF SHIPS AND THE NEXT DEATH SPENDS A LIFE TO RESTART THE STAGE. PRESS ENTER.";
#endif
                    break;
                default:
                    stepIndex = 8;
                    title = "COMPLETE";
                    body = "TUTORIAL COMPLETE.";
                    break;
            }
        }

        private void DrawBar(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, float ratio, Color fill)
        {
            float clamped = MathHelper.Clamp(ratio, 0f, 1f);
            spriteBatch.Draw(pixel, bounds, Color.White * 0.12f);
            int width = Math.Max(0, (int)MathF.Round(bounds.Width * clamped));
            if (width > 0)
                spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, width, bounds.Height), fill);
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

        private RunSaveData CaptureRunSaveData(int slotIndex, bool includeSummary)
        {
            var save = new RunSaveData
            {
                CurrentStageNumber = currentStageNumber,
                CurrentSectionIndex = currentSectionIndex,
                State = state == GameFlowState.SaveSlots || state == GameFlowState.LoadSlots || state == GameFlowState.Options ? GameFlowState.Paused : state,
                HelpReturnState = helpReturnState,
                DraftReturnState = draftReturnState,
                StageElapsedSeconds = stageElapsedSeconds,
                StateTimer = stateTimer,
                ActiveEventTimer = activeEventTimer,
                ActiveEventSpawnTimer = activeEventSpawnTimer,
                RewindMeterSeconds = rewindMeterSeconds,
                RewindHoldSeconds = rewindHoldSeconds,
                RewindAccumulatorSeconds = rewindStepAccumulator,
                StageHadDeath = stageHadDeath,
                CampaignHadDeath = campaignHadDeath,
                BannerText = bannerText,
                ActiveEventWarning = activeEventWarning,
                ActiveEventType = activeEventType,
                ActiveEventIntensity = activeEventIntensity,
                HasActiveBoss = activeBoss != null && !activeBoss.IsExpired,
                PendingBossSpawn = transitionToBoss,
                TransitionTargetStageNumber = transitionTargetStageNumber,
                TransitionToBoss = transitionToBoss,
                TransitionScrollFrom = transitionScrollFrom,
                TransitionScrollTo = transitionScrollTo,
                TransitionHudBlend = transitionHudBlend,
                BossApproachTimer = bossApproachTimer,
                DraftTimer = draftTimer,
                DraftSelection = draftSelection,
                DraftChargeStyle = draftChargeStyle,
                DraftFromTutorial = draftFromTutorial,
                TutorialReplayMode = tutorialReplayMode,
                TutorialStep = tutorialStep,
                TutorialProgressSeconds = tutorialProgressSeconds,
                DraftCards = new List<UpgradeDraftCard>(draftCards),
                PlayerStatus = PlayerStatus.CaptureSnapshot(),
                Player = Player1.Instance.CaptureSnapshot(),
                Enemies = EntityManager.CaptureEnemies(),
                Bullets = EntityManager.CaptureBullets(),
                Beams = EntityManager.CaptureBeams(),
                Powerups = EntityManager.CapturePowerups(),
                ScheduledSpawns = CaptureScheduledSpawns(),
                ScheduledEvents = CaptureScheduledEvents(),
                GameplayRngState = gameplayRandom.State,
            };

            if (includeSummary)
            {
                save.Summary = new SaveSlotSummary
                {
                    SlotIndex = slotIndex,
                    HasData = true,
                    StageNumber = currentStageNumber,
                    StageName = currentStage?.Name ?? string.Empty,
                    Score = PlayerStatus.Score,
                    SavedAtUtc = System.DateTime.UtcNow.ToString("u"),
                    ActiveStyle = PlayerStatus.RunProgress.Weapons.ActiveStyle.ToString().ToUpperInvariant(),
                };
            }

            return save;
        }

        private void RestoreRunSaveData(RunSaveData save, bool invalidateMedals, bool fromRewind)
        {
            if (save == null)
                return;

            currentStageNumber = Math.Max(1, save.CurrentStageNumber);
            currentStage = repository.GetStage(currentStageNumber);
            currentSectionIndex = save.CurrentSectionIndex;
            helpReturnState = save.HelpReturnState;
            draftReturnState = save.DraftReturnState;
            stageElapsedSeconds = save.StageElapsedSeconds;
            stateTimer = save.StateTimer;
            activeEventTimer = save.ActiveEventTimer;
            activeEventSpawnTimer = save.ActiveEventSpawnTimer;
            float preservedRewindMeter = rewindMeterSeconds;
            float preservedRewindHold = rewindHoldSeconds;
            float preservedRewindAccumulator = rewindStepAccumulator;
            rewindMeterSeconds = save.RewindMeterSeconds > 0f ? save.RewindMeterSeconds : RewindCapacitySeconds;
            rewindHoldSeconds = save.RewindHoldSeconds;
            rewindStepAccumulator = save.RewindAccumulatorSeconds;
            stageHadDeath = save.StageHadDeath;
            campaignHadDeath = save.CampaignHadDeath;
            bannerText = save.BannerText ?? string.Empty;
            activeEventWarning = save.ActiveEventWarning ?? string.Empty;
            activeEventType = save.ActiveEventType;
            activeEventIntensity = save.ActiveEventIntensity;
            transitionTargetStageNumber = save.TransitionTargetStageNumber;
            transitionToBoss = save.TransitionToBoss;
            transitionScrollFrom = save.TransitionScrollFrom;
            transitionScrollTo = save.TransitionScrollTo;
            transitionHudBlend = save.TransitionHudBlend;
            bossApproachTimer = save.BossApproachTimer;
            draftTimer = save.DraftTimer;
            draftSelection = save.DraftSelection;
            draftChargeStyle = save.DraftChargeStyle;
            draftFromTutorial = save.DraftFromTutorial;
            tutorialReplayMode = save.TutorialReplayMode;
            tutorialStep = save.TutorialStep;
            tutorialProgressSeconds = save.TutorialProgressSeconds;
            draftCards.Clear();
            if (save.DraftCards != null)
                draftCards.AddRange(save.DraftCards);

            gameplayRandom.Restore(save.GameplayRngState == 0 ? 1u : save.GameplayRngState);
            PlayerStatus.RestoreSnapshot(save.PlayerStatus);
            if (invalidateMedals)
                PlayerStatus.RunProgress.MarkMedalIneligible();

            scheduledSpawns.Clear();
            if (save.ScheduledSpawns != null)
            {
                for (int i = 0; i < save.ScheduledSpawns.Count; i++)
                {
                    ScheduledSpawnSnapshotData snapshot = save.ScheduledSpawns[i];
                    scheduledSpawns.Add(new ScheduledSpawn
                    {
                        SpawnAtSeconds = snapshot.SpawnAtSeconds,
                        Group = snapshot.Group,
                        SpawnPoint = new Vector2(snapshot.SpawnPoint.X, snapshot.SpawnPoint.Y),
                        TargetY = snapshot.TargetY,
                        MovePattern = snapshot.MovePattern,
                        FirePattern = snapshot.FirePattern,
                        Amplitude = snapshot.Amplitude,
                        Frequency = snapshot.Frequency,
                        SpeedMultiplier = snapshot.SpeedMultiplier > 0f ? snapshot.SpeedMultiplier : snapshot.Group?.SpeedMultiplier ?? 1f,
                    });
                }
            }

            scheduledEvents.Clear();
            if (save.ScheduledEvents != null)
            {
                for (int i = 0; i < save.ScheduledEvents.Count; i++)
                {
                    ScheduledEventSnapshotData snapshot = save.ScheduledEvents[i];
                    scheduledEvents.Add(new ScheduledEvent
                    {
                        TriggerAtSeconds = snapshot.TriggerAtSeconds,
                        Window = snapshot.Window,
                    });
                }
            }

            EntityManager.Reset();
            Player1.Instance.RestoreSnapshot(save.Player);
            EntityManager.Add(Player1.Instance);
            EntityManager.RestoreEnemies(save.Enemies, repository, currentStage?.Boss);
            EntityManager.RestoreBullets(save.Bullets);
            EntityManager.RestoreBeams(save.Beams);
            EntityManager.RestorePowerups(save.Powerups);
            activeBoss = EntityManager.Enemies.OfType<BossEnemy>().FirstOrDefault(enemy => !enemy.IsExpired);

            if (!fromRewind)
            {
                state = save.State == GameFlowState.Playing || save.State == GameFlowState.Tutorial ? GameFlowState.Paused : save.State;
                float restoredMeter = rewindMeterSeconds;
                ResetRewindBuffer();
                rewindMeterSeconds = restoredMeter;
                rewindFrames.Add(CaptureRunSaveData(0, false));
            }
            else
            {
                rewindMeterSeconds = preservedRewindMeter;
                rewindHoldSeconds = preservedRewindHold;
                rewindStepAccumulator = preservedRewindAccumulator;
                state = save.State == GameFlowState.Tutorial ? GameFlowState.Tutorial : GameFlowState.Playing;
            }
        }

        private List<ScheduledSpawnSnapshotData> CaptureScheduledSpawns()
        {
            var snapshots = new List<ScheduledSpawnSnapshotData>(scheduledSpawns.Count);
            for (int i = 0; i < scheduledSpawns.Count; i++)
            {
                ScheduledSpawn spawn = scheduledSpawns[i];
                snapshots.Add(new ScheduledSpawnSnapshotData
                {
                    SpawnAtSeconds = spawn.SpawnAtSeconds,
                    Group = spawn.Group,
                    SpawnPoint = new Vector2Data(spawn.SpawnPoint.X, spawn.SpawnPoint.Y),
                    TargetY = spawn.TargetY,
                    MovePattern = spawn.MovePattern,
                    FirePattern = spawn.FirePattern,
                    Amplitude = spawn.Amplitude,
                    Frequency = spawn.Frequency,
                    SpeedMultiplier = spawn.SpeedMultiplier,
                });
            }

            return snapshots;
        }

        private List<ScheduledEventSnapshotData> CaptureScheduledEvents()
        {
            var snapshots = new List<ScheduledEventSnapshotData>(scheduledEvents.Count);
            for (int i = 0; i < scheduledEvents.Count; i++)
            {
                ScheduledEvent scheduledEvent = scheduledEvents[i];
                snapshots.Add(new ScheduledEventSnapshotData
                {
                    TriggerAtSeconds = scheduledEvent.TriggerAtSeconds,
                    Window = scheduledEvent.Window,
                });
            }

            return snapshots;
        }

        private float GetCurrentStageScrollSpeed()
        {
            if (currentStage == null)
                return 0f;

            float baseSpeed = GetStageBaseScrollSpeed(currentStageNumber, currentStage);
            SectionDefinition section = GetActiveSection();
            float sectionMultiplier = section != null && section.ScrollMultiplier > 0f
                ? section.ScrollMultiplier
                : GetDefaultSectionScrollMultiplier(GetActiveSectionIndex());

            return baseSpeed * sectionMultiplier;
        }

        private static float GetStageBaseScrollSpeed(int stageNumber, StageDefinition stage)
        {
            if (stage != null && stage.BaseScrollSpeed > 0f)
                return stage.BaseScrollSpeed;

            return MathHelper.Clamp(118f + (stageNumber - 1) * 1.4f, 118f, 186f);
        }

        private int GetActiveSectionIndex()
        {
            if (currentStage?.Sections == null || currentStage.Sections.Count == 0)
                return 0;

            for (int i = currentStage.Sections.Count - 1; i >= 0; i--)
            {
                if (currentStage.Sections[i].StartSeconds <= stageElapsedSeconds)
                    return i;
            }

            return 0;
        }

        private static float GetDefaultSectionScrollMultiplier(int sectionIndex)
        {
            float[] multipliers = { 0.90f, 0.98f, 1.06f, 1.12f, 1.18f };
            return multipliers[Math.Clamp(sectionIndex, 0, multipliers.Length - 1)];
        }

        private static float GetDefaultEnemySpeedMultiplier(int sectionIndex)
        {
            float[] multipliers = { 0.82f, 0.94f, 1.0f, 1.12f, 1.18f };
            return multipliers[Math.Clamp(sectionIndex, 0, multipliers.Length - 1)];
        }

        private static int GetStageDensityBand(int stageNumber)
        {
            if (stageNumber <= 5)
                return 0;
            if (stageNumber <= 15)
                return 1;
            if (stageNumber <= 30)
                return 2;

            return 3;
        }

        private static int GetTargetActiveEnemyBudget(int stageNumber, int sectionIndex)
        {
            int band = GetStageDensityBand(stageNumber);
            int[][] budgets =
            {
                new[] { 2, 3, 4, 5, 5 },
                new[] { 4, 5, 6, 7, 8 },
                new[] { 6, 7, 8, 9, 10 },
                new[] { 7, 8, 10, 11, 12 },
            };

            int[] tier = budgets[Math.Clamp(band, 0, budgets.Length - 1)];
            return tier[Math.Clamp(sectionIndex, 0, tier.Length - 1)];
        }

        private static float GetSectionGapClampSeconds(int stageNumber, int sectionIndex)
        {
            float[] gaps = { 3.4f, 2.9f, 2.45f, 2.15f };
            float gap = gaps[Math.Clamp(GetStageDensityBand(stageNumber), 0, gaps.Length - 1)] - sectionIndex * 0.18f;
            return Math.Max(1.65f, gap);
        }

        private static int GetAdjustedGroupCount(SpawnGroupDefinition group, EnemyArchetypeDefinition archetype, int stageNumber, int sectionIndex)
        {
            int band = GetStageDensityBand(stageNumber);
            int bonus = 0;
            switch (archetype.Id)
            {
                case "Walker":
                case "Interceptor":
                    bonus = band + (sectionIndex >= 2 ? 1 : 0);
                    break;
                case "Destroyer":
                    bonus = band >= 1 ? 1 : 0;
                    if (band >= 3 && sectionIndex >= 2)
                        bonus++;
                    break;
                case "Carrier":
                    bonus = band >= 2 ? 1 : 0;
                    break;
                case "Bulwark":
                    bonus = band >= 3 && sectionIndex >= 2 ? 1 : 0;
                    break;
            }

            int targetBudget = GetTargetActiveEnemyBudget(stageNumber, sectionIndex);
            return Math.Max(1, Math.Min(group.Count + bonus, targetBudget + 1));
        }

        private static float GetSpawnIntervalScale(string archetypeId, int stageNumber, int sectionIndex)
        {
            int band = GetStageDensityBand(stageNumber);
            float scale = 1f;
            switch (archetypeId)
            {
                case "Walker":
                case "Interceptor":
                    scale = 0.92f - band * 0.08f - sectionIndex * 0.04f;
                    break;
                case "Destroyer":
                    scale = 0.96f - band * 0.05f - sectionIndex * 0.03f;
                    break;
                case "Carrier":
                case "Bulwark":
                    scale = 0.98f - band * 0.03f - sectionIndex * 0.02f;
                    break;
            }

            return MathHelper.Clamp(scale, 0.56f, 1f);
        }

        private static float GetSpacingScale(string archetypeId, int stageNumber, int sectionIndex)
        {
            int band = GetStageDensityBand(stageNumber);
            float scale = 1f;
            switch (archetypeId)
            {
                case "Walker":
                case "Interceptor":
                    scale = 0.94f - band * 0.06f - sectionIndex * 0.03f;
                    break;
                case "Destroyer":
                    scale = 0.96f - band * 0.04f - sectionIndex * 0.025f;
                    break;
                case "Carrier":
                case "Bulwark":
                    scale = 0.98f - band * 0.03f - sectionIndex * 0.02f;
                    break;
            }

            return MathHelper.Clamp(scale, 0.7f, 1f);
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
            public float SpeedMultiplier { get; set; } = 1f;
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
