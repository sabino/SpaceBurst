using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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
        private const float DeveloperCodeTimeoutSeconds = 1.5f;
        private const int PointerControlNone = -1;
        private const int PointerControlOptionsViewport = 1000;
        private const int PointerControlOptionsScrollbarUp = 1001;
        private const int PointerControlOptionsScrollbarDown = 1002;
        private const int PointerControlOptionsScrollbarTrack = 1003;
        private const int PointerControlOptionsScrollbarThumb = 1004;
        private const int PointerControlOptionsDiscard = 1005;
        private const int PointerControlOptionsApply = 1006;
        private const int PointerControlAudioDialogCancel = 1010;
        private const int PointerControlAudioDialogApply = 1011;
        private const int PointerControlHelpLeft = 1020;
        private const int PointerControlHelpRight = 1021;
        private const int PointerControlHelpTutorial = 1022;
        private const int PointerControlOptionRowBase = 2000;
        private const int PointerControlOptionStepperLeftBase = 3000;
        private const int PointerControlOptionStepperRightBase = 4000;
        private const int PointerControlOptionSliderBase = 5000;
        private const int PointerControlAudioDialogPresetBase = 6000;

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
        private OptionsData optionsSnapshot;
        private readonly MedalProgress medals;
        private readonly List<ScheduledSpawn> scheduledSpawns = new List<ScheduledSpawn>();
        private readonly List<ScheduledEvent> scheduledEvents = new List<ScheduledEvent>();
        private readonly List<ReentryTicket> reentryTickets = new List<ReentryTicket>();
        private readonly DeterministicRngState gameplayRandom = new DeterministicRngState(1u);
        private readonly List<RunSaveData> rewindFrames = new List<RunSaveData>();
        private readonly List<UpgradeDraftCard> draftCards = new List<UpgradeDraftCard>();
        private readonly Random titleVisualRandom = new Random(unchecked(Environment.TickCount * 397));
        private static readonly Keys[] DeveloperToolsCode =
        {
            Keys.Up, Keys.Up, Keys.Down, Keys.Down, Keys.Left, Keys.Right, Keys.Left, Keys.Right, Keys.D, Keys.E, Keys.V
        };
        private static readonly GameDifficulty[] DifficultyChoices =
        {
            GameDifficulty.Easy,
            GameDifficulty.Normal,
            GameDifficulty.Hard,
            GameDifficulty.Insane,
            GameDifficulty.Realistic,
        };

        private StageDefinition currentStage;
        private BossDefinition resolvedBossDefinition;
        private BossEnemy activeBoss;
        private GameFlowState state = GameFlowState.Title;
        private GameFlowState helpReturnState = GameFlowState.Title;
        private GameFlowState slotReturnState = GameFlowState.Title;
        private GameFlowState draftReturnState = GameFlowState.Playing;
        private GameFlowState pauseReturnState = GameFlowState.Playing;
        private int currentStageNumber = 1;
        private int currentSectionIndex;
        private int titleSelection;
        private int difficultySelection;
        private int pauseSelection;
        private int optionsSelection;
        private int slotSelection;
        private int helpPageIndex;
        private int draftSelection;
        private WeaponStyleId draftChargeStyle = WeaponStyleId.Pulse;
        private float stageElapsedSeconds;
        private float stateTimer;
        private float optionsScrollOffset;
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
        private float audioQualityApplyDelay;
        private bool difficultySelectSkipTutorial;
        private bool draftFromTutorial;
        private bool tutorialReplayMode;
        private bool audioQualityDialogOpen;
        private bool audioQualityApplyPending;
        private TutorialStep tutorialStep;
        private float tutorialProgressSeconds;
        private int optionsSliderDragIndex = -1;
        private OptionMenuSection optionsSection;
        private ViewMode viewMode = ViewMode.SideScroller;
        private PresentationTier presentationTier = PresentationTier.Pixel2D;
        private string selectedBossVariantId = string.Empty;
        private bool titleIntroActive = true;
        private bool titleIntroSeen;
        private bool titleIntroExploded;
        private float titleIntroTimer = TitleIntroDurationSeconds;
        private int developerCodeProgress;
        private float developerCodeTimer;
        private PresentationTier? developerPresentationOverride;
        private readonly List<IntroPixel> introPixels = new List<IntroPixel>();
        private Color titleIntroBackgroundColor = new Color(7, 12, 24);
        private Color titleIntroGlowColorA = new Color(110, 193, 255);
        private Color titleIntroGlowColorB = new Color(246, 198, 116);
        private Color titleIntroPrimaryColor = Color.White;
        private Color titleIntroSecondaryColor = new Color(110, 193, 255);
        private Color titleIntroPromptColor = Color.White;
        private AudioQualityPreset pendingAudioQualityPreset;

#if ANDROID
        private static readonly int[] UiScaleOptions = { 80, 90, 100, 115, 130, 150, 170, 190, 220 };
#else
        private static readonly int[] UiScaleOptions = { 80, 90, 100, 110, 120, 130, 140, 160 };
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

        private enum OptionMenuSection
        {
            General,
            ThreeDGameplay,
        }

        private enum OptionRowControlKind
        {
            Slider,
            Toggle,
            Cycle,
            Submenu,
            Action,
        }

        private readonly struct OptionRowDefinition
        {
            public OptionRowDefinition(string label, string value, string description, OptionRowControlKind controlKind)
            {
                Label = label ?? string.Empty;
                Value = value ?? string.Empty;
                Description = description ?? string.Empty;
                ControlKind = controlKind;
            }

            public string Label { get; }
            public string Value { get; }
            public string Description { get; }
            public OptionRowControlKind ControlKind { get; }
        }

        private enum PauseMenuAction
        {
            Resume,
            SaveGame,
            LoadGame,
            Options,
            Help,
            DeveloperStage,
            DeveloperDetail,
            SkipTutorial,
            QuitToTitle,
        }

        private readonly struct PauseMenuEntry
        {
            public PauseMenuEntry(PauseMenuAction action, UiButton button)
            {
                Action = action;
                Button = button;
            }

            public PauseMenuAction Action { get; }
            public UiButton Button { get; }
        }

        public CampaignDirector()
        {
            options = PersistentStorage.LoadOptions();
            optionsSnapshot = CloneOptions(options);
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

                if (activeBoss != null && ActiveStageBossDefinition != null)
                    return ActiveStageBossDefinition.ArenaScrollSpeed;

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
                if (activeBoss != null && ActiveStageBossDefinition?.MoodOverride != null)
                    return ActiveStageBossDefinition.MoodOverride;

                SectionDefinition section = GetActiveSection();
                return section?.Mood ?? currentStage?.BackgroundMood ?? new BackgroundMoodDefinition();
            }
        }

        public float CurrentDifficultyFactor
        {
            get
            {
                float pressure = activeBoss != null
                    ? PlayerStatus.RunProgress.GetBossPressure(currentStageNumber)
                    : PlayerStatus.RunProgress.GetWavePressure(currentStageNumber);
                return MathHelper.Clamp(pressure / (activeBoss != null ? 1.45f : 1.2f), 0f, 1f);
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
                float baseChance = (section?.PowerDropBonusChance ?? 0f) + PlayerStatus.RunProgress.DropBonusChance;
                return baseChance * PlayerStatus.RunProgress.GetDropChanceMultiplier(currentStageNumber, activeBoss != null);
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

        public int TouchControlsOpacity
        {
            get { return options.TouchControlsOpacity; }
        }

        public bool Invert3DHorizontal
        {
            get { return options.Invert3DHorizontal; }
        }

        public bool Invert3DVertical
        {
            get { return options.Invert3DVertical; }
        }

        public AimAssist3DMode AimAssist3DMode
        {
            get { return options.AimAssist3DMode; }
        }

        public FontTheme FontTheme
        {
            get { return FontTheme.Compact; }
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

        internal bool ShouldDrawMenuCursor
        {
            get
            {
                return state == GameFlowState.Title
                    || state == GameFlowState.DifficultySelect
                    || state == GameFlowState.Help
                    || state == GameFlowState.Paused
                    || state == GameFlowState.Options
                    || state == GameFlowState.SaveSlots
                    || state == GameFlowState.LoadSlots;
            }
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

        public PresentationTier CurrentPresentationTier
        {
            get { return developerPresentationOverride ?? presentationTier; }
        }

        public ViewMode CurrentViewMode
        {
            get { return viewMode; }
        }

        public bool DeveloperToolsUnlocked
        {
            get { return options.DeveloperToolsUnlocked; }
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

        private BossDefinition ActiveStageBossDefinition
        {
            get { return resolvedBossDefinition ?? currentStage?.Boss; }
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
            UpdateDeveloperToolsCode();

            switch (state)
            {
                case GameFlowState.Title:
                    UpdateTitle();
                    break;
                case GameFlowState.DifficultySelect:
                    UpdateDifficultySelect();
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
                case GameFlowState.DifficultySelect:
                    DrawBackdrop(spriteBatch, pixel, 0.92f, "SEAMLESS CAMPAIGN WITH REWIND");
                    DrawDifficultySelect(spriteBatch, pixel);
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
            bool pointerActivated = HandlePointerSelection(buttons, ref titleSelection);

            if (Input.WasHelpPressed())
            {
                helpReturnState = GameFlowState.Title;
                helpPageIndex = 0;
                state = GameFlowState.Help;
                return;
            }

            if (Input.WasConfirmPressed() || pointerActivated)
                ActivateTitleSelection(titleSelection);
        }

        private void UpdateDifficultySelect()
        {
            List<UiButton> buttons = GetDifficultyButtons();
            UpdateVerticalSelection(ref difficultySelection, buttons.Count);
            bool pointerActivated = HandlePointerSelection(buttons, ref difficultySelection);

            if (Input.WasCancelPressed())
            {
                state = GameFlowState.Title;
                return;
            }

            if (!Input.WasConfirmPressed() && !pointerActivated)
                return;

            if (difficultySelection >= DifficultyChoices.Length)
            {
                state = GameFlowState.Title;
                return;
            }

            ConfirmDifficultySelection();
        }

        private void UpdatePause()
        {
            List<PauseMenuEntry> entries = GetPauseEntries();
            UpdateVerticalSelection(ref pauseSelection, entries.Count);
            pauseSelection = Math.Clamp(pauseSelection, 0, entries.Count - 1);
            bool pointerActivated = HandlePointerSelection(entries.Select(entry => entry.Button.Bounds).ToArray(), ref pauseSelection);

            bool tutorialPause = pauseReturnState == GameFlowState.Tutorial;
            PauseMenuAction selectedAction = entries[pauseSelection].Action;

            if (options.DeveloperToolsUnlocked && !tutorialPause)
            {
                if (Input.WasKeyPressed(Keys.PageUp))
                {
                    JumpToDeveloperStage(-1);
                    return;
                }

                if (Input.WasKeyPressed(Keys.PageDown))
                {
                    JumpToDeveloperStage(1);
                    return;
                }

                if (Input.WasKeyPressed(Keys.F6))
                {
                    CycleDeveloperPresentationOverride(1);
                    return;
                }

                if (selectedAction == PauseMenuAction.DeveloperStage)
                {
                    if (Input.WasNavigateLeftPressed())
                    {
                        JumpToDeveloperStage(-1);
                        return;
                    }

                    if (Input.WasNavigateRightPressed())
                    {
                        JumpToDeveloperStage(1);
                        return;
                    }
                }
                else if (selectedAction == PauseMenuAction.DeveloperDetail)
                {
                    if (Input.WasNavigateLeftPressed())
                    {
                        CycleDeveloperPresentationOverride(-1);
                        return;
                    }

                    if (Input.WasNavigateRightPressed())
                    {
                        CycleDeveloperPresentationOverride(1);
                        return;
                    }
                }
            }

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

            if (!Input.WasConfirmPressed() && !pointerActivated)
                return;

            switch (entries[pauseSelection].Action)
            {
                case PauseMenuAction.SaveGame:
                    slotReturnState = GameFlowState.Paused;
                    slotSelection = 0;
                    state = GameFlowState.SaveSlots;
                    break;
                case PauseMenuAction.LoadGame:
                    slotReturnState = GameFlowState.Paused;
                    slotSelection = 0;
                    state = GameFlowState.LoadSlots;
                    break;
                case PauseMenuAction.Options:
                    OpenOptions(GameFlowState.Paused);
                    break;
                case PauseMenuAction.Help:
                    helpReturnState = GameFlowState.Paused;
                    state = GameFlowState.Help;
                    break;
                case PauseMenuAction.DeveloperStage:
                    JumpToDeveloperStage(1);
                    break;
                case PauseMenuAction.DeveloperDetail:
                    CycleDeveloperPresentationOverride(1);
                    break;
                case PauseMenuAction.SkipTutorial:
                    if (tutorialPause)
                    {
                        options.TutorialCompleted = true;
                        PersistentStorage.SaveOptions(options);
                        if (tutorialReplayMode)
                            EnterTitle(false);
                        else
                            BeginFreshCampaign(PlayerStatus.RunProgress.Difficulty);
                    }
                    else
                    {
                        PlayerStatus.FinalizeRun();
                        EnterTitle(false);
                    }
                    break;
                case PauseMenuAction.QuitToTitle:
                    PlayerStatus.FinalizeRun();
                    EnterTitle(false);
                    break;
                case PauseMenuAction.Resume:
                default:
                    state = pauseReturnState;
                    break;
            }
        }

        private void UpdateOptions()
        {
            float deltaSeconds = (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            OptionRowDefinition[] rows = GetOptionRows();
            Rectangle viewport = GetOptionsListViewportBounds();
            Rectangle[] optionBounds = GetOptionRowBounds(viewport, rows.Length);
            (Rectangle discardBounds, Rectangle applyBounds) = GetOptionActionBounds();
            Vector2 pointer = Input.PointerPosition;

            if (audioQualityApplyPending)
            {
                audioQualityApplyDelay -= deltaSeconds;
                if (audioQualityApplyDelay <= 0f)
                {
                    Game1.Instance?.ApplyAudioQuality(options.AudioQualityPreset);
                    audioQualityApplyPending = false;
                }

                return;
            }

            if (audioQualityDialogOpen)
            {
                UpdateAudioQualityDialog();
                return;
            }

            if (Input.WasCancelPressed())
            {
#if ANDROID
                DiscardOptionsAndClose();
#else
                if (optionsSection == OptionMenuSection.ThreeDGameplay)
                {
                    optionsSection = OptionMenuSection.General;
                    optionsSelection = GetOptionRows().Length - 1;
                    optionsScrollOffset = GetOptionsMaxScroll();
                    return;
                }
                ConfirmOptionsAndClose();
#endif
                return;
            }

            int previousSelection = optionsSelection;
            UpdateVerticalSelection(ref optionsSelection, optionBounds.Length);
            if (optionsSelection != previousSelection)
            {
                EnsureOptionSelectionVisible(viewport, optionBounds);
                optionBounds = GetOptionRowBounds(viewport, rows.Length);
            }

            int hoveredIndex = ResolveHoveredOptionIndex(optionBounds, pointer);
            if (hoveredIndex >= 0 && (Input.DidUiPointerMove() || Input.IsMenuPointerHeld() || Input.WasUiPointerPressed()))
                optionsSelection = hoveredIndex;

            int wheelDelta = Input.ConsumeUiPointerScrollWheelDelta();
            if (wheelDelta != 0 && (viewport.Contains(pointer) || GetOptionScrollbarBounds(viewport).Contains(pointer)))
            {
                optionsScrollOffset -= Math.Sign(wheelDelta) * UiPx(42);
                ClampOptionsScroll();
                optionBounds = GetOptionRowBounds(viewport, rows.Length);
            }

            if (Input.WasUiPointerPressed())
            {
                int controlId = ResolveOptionPointerControlId(rows, viewport, optionBounds, discardBounds, applyBounds, pointer);
                if (controlId != PointerControlNone)
                {
                    Input.CaptureUiControl(controlId);
                    if (IsOptionSliderControlId(controlId))
                    {
                        int sliderIndex = DecodeIndexedControlId(controlId, PointerControlOptionSliderBase);
                        optionsSelection = sliderIndex;
                        optionsSliderDragIndex = sliderIndex;
                        ApplyOptionSliderPointerValue(sliderIndex, optionBounds, pointer);
                    }
                    else if (IsOptionRowControlId(controlId))
                    {
                        optionsSelection = DecodeIndexedControlId(controlId, PointerControlOptionRowBase);
                    }
                }
                else
                {
                    Input.ClearUiControlCapture();
                }
            }

            Vector2 dragDelta = Input.ConsumeMenuDragDelta();
            if (Input.IsMenuPointerHeld())
            {
                int capturedControlId = Input.CapturedUiControlId;
                if (IsOptionSliderControlId(capturedControlId))
                {
                    int sliderIndex = DecodeIndexedControlId(capturedControlId, PointerControlOptionSliderBase);
                    optionsSliderDragIndex = sliderIndex;
                    ApplyOptionSliderPointerValue(sliderIndex, optionBounds, pointer);
                }
                else if (capturedControlId == PointerControlOptionsScrollbarThumb)
                {
                    Rectangle trackBounds = GetOptionScrollbarTrackBounds(viewport);
                    Rectangle thumbBounds = GetOptionScrollbarThumbBounds(viewport);
                    float maxScroll = GetOptionsMaxScroll();
                    float ratio = maxScroll <= 0f
                        ? 0f
                        : (pointer.Y - trackBounds.Y - thumbBounds.Height * 0.5f) / Math.Max(1f, trackBounds.Height - thumbBounds.Height);
                    optionsScrollOffset = MathHelper.Clamp(ratio, 0f, 1f) * maxScroll;
                    ClampOptionsScroll();
                    optionBounds = GetOptionRowBounds(viewport, rows.Length);
                }
                else if ((capturedControlId == PointerControlOptionsViewport || IsOptionRowControlId(capturedControlId)) && MathF.Abs(dragDelta.Y) > 0.1f)
                {
                    optionsSliderDragIndex = -1;
                    optionsScrollOffset -= dragDelta.Y;
                    ClampOptionsScroll();
                    optionBounds = GetOptionRowBounds(viewport, rows.Length);
                }
            }
            else
            {
                optionsSliderDragIndex = -1;
            }

            if (Input.WasUiPointerReleased())
            {
                int capturedControlId = Input.CapturedUiControlId;
                bool activateOnRelease = !Input.IsUiPointerDragging();

                if (capturedControlId == PointerControlOptionsDiscard && discardBounds.Contains(pointer) && activateOnRelease)
                {
                    Input.ClearUiControlCapture();
                    DiscardOptionsAndClose();
                    return;
                }

                if (capturedControlId == PointerControlOptionsApply && applyBounds.Contains(pointer) && activateOnRelease)
                {
                    Input.ClearUiControlCapture();
                    ConfirmOptionsAndClose();
                    return;
                }

                if (capturedControlId == PointerControlOptionsScrollbarUp && GetOptionScrollbarButtonBounds(viewport, true).Contains(pointer) && activateOnRelease)
                {
                    optionsScrollOffset -= UiPx(96);
                    ClampOptionsScroll();
                }
                else if (capturedControlId == PointerControlOptionsScrollbarDown && GetOptionScrollbarButtonBounds(viewport, false).Contains(pointer) && activateOnRelease)
                {
                    optionsScrollOffset += UiPx(96);
                    ClampOptionsScroll();
                }
                else if (capturedControlId == PointerControlOptionsScrollbarTrack && GetOptionScrollbarTrackBounds(viewport).Contains(pointer) && activateOnRelease)
                {
                    Rectangle thumbBounds = GetOptionScrollbarThumbBounds(viewport);
                    optionsScrollOffset += pointer.Y < thumbBounds.Y ? -viewport.Height * 0.7f : viewport.Height * 0.7f;
                    ClampOptionsScroll();
                }
                else if (IsOptionStepperLeftControlId(capturedControlId))
                {
                    int index = DecodeIndexedControlId(capturedControlId, PointerControlOptionStepperLeftBase);
                    if (index >= 0 && index < optionBounds.Length && GetOptionStepperLeftBounds(optionBounds[index]).Contains(pointer) && activateOnRelease)
                        AdjustOptionByStep(index, -1);
                }
                else if (IsOptionStepperRightControlId(capturedControlId))
                {
                    int index = DecodeIndexedControlId(capturedControlId, PointerControlOptionStepperRightBase);
                    if (index >= 0 && index < optionBounds.Length && GetOptionStepperRightBounds(optionBounds[index]).Contains(pointer) && activateOnRelease)
                        AdjustOptionByStep(index, 1);
                }
                else if (IsOptionRowControlId(capturedControlId))
                {
                    int index = DecodeIndexedControlId(capturedControlId, PointerControlOptionRowBase);
                    if (index >= 0 && index < optionBounds.Length && optionBounds[index].Contains(pointer) && activateOnRelease)
                    {
                        optionsSelection = index;
                        ActivateOptionFromPointer(index, optionBounds[index], pointer);
                    }
                }

                Input.ClearUiControlCapture();
                optionBounds = GetOptionRowBounds(viewport, rows.Length);
            }

            if (Input.WasNavigateLeftPressed())
                AdjustOptionByStep(optionsSelection, -1);
            else if (Input.WasNavigateRightPressed() || Input.WasConfirmPressed())
                AdjustOptionByStep(optionsSelection, 1);
        }

        private void UpdateSaveSlots(bool saving)
        {
            UpdateVerticalSelection(ref slotSelection, 4);
            bool pointerActivated = HandlePointerSelection(GetSaveSlotSelectionBounds(), ref slotSelection);

            if (Input.WasCancelPressed())
            {
                state = slotReturnState;
                return;
            }

            if (!Input.WasConfirmPressed() && !pointerActivated)
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
            (Rectangle leftBounds, Rectangle rightBounds, Rectangle tutorialBounds) = GetHelpActionBounds();
            Vector2 pointer = Input.PointerPosition;
            if (Input.WasUiPointerPressed())
            {
                if (leftBounds.Contains(pointer))
                    Input.CaptureUiControl(PointerControlHelpLeft);
                else if (rightBounds.Contains(pointer))
                    Input.CaptureUiControl(PointerControlHelpRight);
                else if (helpPageIndex == 0 && tutorialBounds.Contains(pointer))
                    Input.CaptureUiControl(PointerControlHelpTutorial);
                else
                    Input.ClearUiControlCapture();
            }

            if (Input.WasUiPointerReleased())
            {
                bool activateOnRelease = !Input.IsUiPointerDragging();
                if (Input.IsUiControlCaptured(PointerControlHelpLeft) && leftBounds.Contains(pointer) && activateOnRelease)
                    helpPageIndex = (helpPageIndex + HelpPageCount - 1) % HelpPageCount;
                else if (Input.IsUiControlCaptured(PointerControlHelpRight) && rightBounds.Contains(pointer) && activateOnRelease)
                    helpPageIndex = (helpPageIndex + 1) % HelpPageCount;
                else if (Input.IsUiControlCaptured(PointerControlHelpTutorial) && helpPageIndex == 0 && tutorialBounds.Contains(pointer) && activateOnRelease)
                {
                    Input.ClearUiControlCapture();
                    StartTutorial(true, helpReturnState);
                    return;
                }

                Input.ClearUiControlCapture();
            }

            if (Input.WasNavigateLeftPressed())
                helpPageIndex = (helpPageIndex + HelpPageCount - 1) % HelpPageCount;
            else if (Input.WasNavigateRightPressed())
                helpPageIndex = (helpPageIndex + 1) % HelpPageCount;

            if (Input.WasConfirmPressed() && helpPageIndex == 0)
            {
                StartTutorial(true, helpReturnState);
                return;
            }

            if (Input.WasCancelPressed() || Input.WasHelpPressed())
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

            HandleViewToggleInput();

            if (Input.IsRewindHeld())
            {
                UpdateRewind(deltaSeconds);
                return;
            }

            rewindHoldSeconds = 0f;

            stageElapsedSeconds += deltaSeconds;

            ScheduleDueSections();
            SpawnDueEntities();
            SpawnDueReentries();
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

            if (activeBoss == null && ActiveStageBossDefinition != null && currentSectionIndex >= currentStage.Sections.Count && scheduledSpawns.Count == 0 && reentryTickets.Count == 0 && !EntityManager.HasHostiles)
            {
                BeginBossApproachTransition();
                return;
            }

            if (ActiveStageBossDefinition == null && currentSectionIndex >= currentStage.Sections.Count && scheduledSpawns.Count == 0 && reentryTickets.Count == 0 && !EntityManager.HasHostiles)
            {
                CompleteStage();
                return;
            }

            if (activeBoss != null && activeBoss.IsExpired && !EntityManager.HasHostiles && scheduledSpawns.Count == 0 && reentryTickets.Count == 0)
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

            HandleViewToggleInput();

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

#if !ANDROID
            if (Input.WasKeyPressed(Keys.A))
            {
                ApplyDraftSelection(0);
                return;
            }

            if (Input.WasKeyPressed(Keys.S) && draftCards.Count > 1)
            {
                ApplyDraftSelection(1);
                return;
            }

            if (Input.WasKeyPressed(Keys.D) && draftCards.Count > 2)
            {
                ApplyDraftSelection(2);
                return;
            }
#endif

            if (Input.WasNavigateLeftPressed())
                draftSelection = (draftSelection + draftCards.Count - 1) % draftCards.Count;
            else if (Input.WasNavigateRightPressed())
                draftSelection = (draftSelection + 1) % draftCards.Count;

            Rectangle[] cardBounds = GetUpgradeDraftCardBounds();
            bool pointerActivated = HandlePointerSelection(cardBounds, ref draftSelection);

            if (Input.WasCancelPressed())
            {
                ApplyDraftSelection(options.AutoUpgradeDraft ? gameplayRandom.NextInt(0, draftCards.Count) : draftSelection);
                return;
            }

            draftTimer -= (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            if (draftTimer <= 0f)
            {
                ApplyDraftSelection(options.AutoUpgradeDraft ? gameplayRandom.NextInt(0, draftCards.Count) : draftSelection);
                return;
            }

            if (Input.WasConfirmPressed() || pointerActivated)
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
            presentationTier = PresentationProgression.GetTierForStage(stageNumber, currentStage?.PresentationTierOverride);
            resolvedBossDefinition = ResolveBossDefinitionForStage(currentStageNumber, currentStage);
            if (!CanUseChaseView())
                viewMode = ViewMode.SideScroller;
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
            reentryTickets.Clear();

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
            OpenDifficultySelect(false);
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
            BeginFreshCampaign(options.LastSelectedDifficulty);
        }

        private void BeginFreshCampaign(GameDifficulty difficulty)
        {
            options.LastSelectedDifficulty = difficulty;
            PlayerStatus.FinalizeRun();
            PlayerStatus.BeginCampaign(repository.GetStage(1), difficulty);
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
            StartTutorial(replayMode, returnState, ResolvePreferredRunDifficulty());
        }

        private void StartTutorial(bool replayMode, GameFlowState returnState, GameDifficulty difficulty)
        {
            options.LastSelectedDifficulty = difficulty;
            PlayerStatus.FinalizeRun();
            PlayerStatus.BeginCampaign(repository.GetStage(1), difficulty);
            PlayerStatus.RunProgress.Weapons.SetStyleProgress(WeaponStyleId.Pulse, 3, 0, true);
            gameplayRandom.Restore(0xC0FFEEu);
            currentStageNumber = 1;
            currentStage = repository.GetStage(1);
            presentationTier = PresentationTier.Pixel2D;
            resolvedBossDefinition = ResolveBossDefinitionForStage(currentStageNumber, currentStage);
            viewMode = ViewMode.SideScroller;
            currentSectionIndex = 0;
            stageElapsedSeconds = 0f;
            stateTimer = 0f;
            activeBoss = null;
            scheduledSpawns.Clear();
            scheduledEvents.Clear();
            reentryTickets.Clear();
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
            resolvedBossDefinition = null;
            activeBoss = null;
            currentStageNumber = 1;
            presentationTier = PresentationTier.Pixel2D;
            viewMode = ViewMode.SideScroller;
            selectedBossVariantId = string.Empty;
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
            transitionScrollTo = ActiveStageBossDefinition != null ? ActiveStageBossDefinition.ArenaScrollSpeed : transitionScrollFrom;
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
            presentationTier = PresentationProgression.GetTierForStage(stageNumber, currentStage?.PresentationTierOverride);
            resolvedBossDefinition = ResolveBossDefinitionForStage(currentStageNumber, currentStage);
            if (!CanUseChaseView())
                viewMode = ViewMode.SideScroller;
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
            reentryTickets.Clear();
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

        private void HandleViewToggleInput()
        {
            if (!CanUseChaseView() || !Input.WasToggleViewPressed())
                return;

            viewMode = viewMode == ViewMode.SideScroller ? ViewMode.Chase3D : ViewMode.SideScroller;
            if (viewMode == ViewMode.Chase3D)
            {
                Player1.Instance?.BeginChaseEntryRecenter();
                Late3DRenderer.BeginChaseEntry(Player1.Instance?.CombatPosition ?? Vector3.Zero);
            }
            Game1.Instance.Feedback?.Handle(new FeedbackEvent(FeedbackEventType.StageTransition, Player1.Instance.Position, 0.35f));
        }

        private bool CanUseChaseView()
        {
#if ANDROID
            return false;
#else
            bool unlockedByProgress = PresentationProgression.IsChaseViewUnlocked(currentStageNumber, currentStage);
            return (unlockedByProgress || options.DeveloperToolsUnlocked || DeveloperVisualSettings.CheatsEnabled) && CurrentPresentationTier == PresentationTier.Late3D;
#endif
        }

        internal bool TryConsoleLoadStage(int stageNumber)
        {
            if (stageNumber < 1 || stageNumber > 50 || repository.GetStage(stageNumber) == null)
                return false;

            if (currentStage == null || state == GameFlowState.Title || state == GameFlowState.GameOver || state == GameFlowState.CampaignComplete)
            {
                PlayerStatus.FinalizeRun();
                PlayerStatus.BeginCampaign(repository.GetStage(1), ResolvePreferredRunDifficulty());
                gameplayRandom.Restore(0xC0FFEEu);
                campaignHadDeath = false;
                stageHadDeath = false;
                tutorialReplayMode = false;
                tutorialStep = TutorialStep.Move;
                tutorialProgressSeconds = 0f;
            }

            PrepareStage(stageNumber, false);
            state = GameFlowState.Paused;
            pauseReturnState = GameFlowState.Playing;
            stateTimer = 0f;
            bannerText = string.Concat("STAGE ", stageNumber.ToString("00"));
            transitionToBoss = false;
            transitionTargetStageNumber = 0;
            activeEventWarning = string.Empty;
            Player1.Instance.MakeInvulnerable(1f);
            return true;
        }

        internal bool TryConsoleSetViewMode(ViewMode requestedMode)
        {
#if ANDROID
            return requestedMode == ViewMode.SideScroller;
#else
            if (requestedMode == ViewMode.SideScroller)
            {
                viewMode = ViewMode.SideScroller;
                return true;
            }

            if (currentStage == null)
                return false;

            if (CurrentPresentationTier != PresentationTier.Late3D)
            {
                if (!DeveloperVisualSettings.CheatsEnabled && !options.DeveloperToolsUnlocked)
                    return false;

                developerPresentationOverride = PresentationTier.Late3D;
            }

            if (!CanUseChaseView())
                return false;

            viewMode = ViewMode.Chase3D;
            Player1.Instance?.BeginChaseEntryRecenter();
            Late3DRenderer.BeginChaseEntry(Player1.Instance?.CombatPosition ?? Vector3.Zero);
            return true;
#endif
        }

        internal bool TryConsoleSetPresentationOverride(PresentationTier? overrideTier)
        {
            developerPresentationOverride = overrideTier;
            if (!CanUseChaseView())
                viewMode = ViewMode.SideScroller;
            return true;
        }

        internal List<Vector3> GetChaseSpawnPreviewPoints()
        {
            var previews = new List<Vector3>();
            if (state != GameFlowState.Playing && state != GameFlowState.Tutorial && state != GameFlowState.Paused)
                return previews;

            const float previewWindowSeconds = 6.5f;
            float previewCutoff = stageElapsedSeconds + previewWindowSeconds;

            for (int i = 0; i < scheduledSpawns.Count; i++)
            {
                ScheduledSpawn spawn = scheduledSpawns[i];
                if (spawn.SpawnAtSeconds < stageElapsedSeconds || spawn.SpawnAtSeconds > previewCutoff)
                    continue;

                previews.Add(spawn.CombatSpawnPoint);
            }

            for (int i = 0; i < reentryTickets.Count; i++)
            {
                ReentryTicket ticket = reentryTickets[i];
                if (ticket.TriggerAtSeconds < stageElapsedSeconds || ticket.TriggerAtSeconds > previewCutoff)
                    continue;

                previews.Add(ticket.CombatSpawnPoint);
            }

            return previews;
        }

        private void JumpToDeveloperStage(int delta)
        {
            if (!options.DeveloperToolsUnlocked || currentStage == null)
                return;

            int targetStage = Math.Clamp(currentStageNumber + delta, 1, 50);
            if (targetStage == currentStageNumber)
                return;

            PrepareStage(targetStage, false);
            state = GameFlowState.Paused;
            pauseReturnState = GameFlowState.Playing;
            stateTimer = 0f;
            transitionToBoss = false;
            transitionTargetStageNumber = 0;
            Player1.Instance.MakeInvulnerable(1f);
            Game1.Instance.Feedback?.Handle(new FeedbackEvent(FeedbackEventType.StageTransition, Player1.Instance.Position, 0.45f));
        }

        private void CycleDeveloperPresentationOverride(int delta)
        {
            if (!options.DeveloperToolsUnlocked)
                return;

            PresentationTier?[] values =
            {
                null,
                PresentationTier.Pixel2D,
                PresentationTier.VoxelShell,
                PresentationTier.HybridMesh,
                PresentationTier.Late3D,
            };

            int currentIndex = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == developerPresentationOverride)
                {
                    currentIndex = i;
                    break;
                }
            }

            int nextIndex = (currentIndex + delta) % values.Length;
            if (nextIndex < 0)
                nextIndex += values.Length;

            developerPresentationOverride = values[nextIndex];
            if (!CanUseChaseView())
                viewMode = ViewMode.SideScroller;
        }

        private string GetDeveloperDetailLabel()
        {
            return developerPresentationOverride.HasValue
                ? string.Concat("DEV DETAIL ", developerPresentationOverride.Value.ToString().ToUpperInvariant())
                : "DEV DETAIL AUTO";
        }

        private void UpdateDeveloperToolsCode()
        {
            bool canEnterCode =
                state == GameFlowState.Title ||
                state == GameFlowState.Paused ||
                state == GameFlowState.Help ||
                state == GameFlowState.Options ||
                state == GameFlowState.SaveSlots ||
                state == GameFlowState.LoadSlots;

            if (!canEnterCode)
            {
                developerCodeProgress = 0;
                developerCodeTimer = 0f;
                return;
            }

            float deltaSeconds = (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            if (developerCodeProgress > 0)
            {
                developerCodeTimer -= deltaSeconds;
                if (developerCodeTimer <= 0f)
                {
                    developerCodeProgress = 0;
                    developerCodeTimer = 0f;
                }
            }

            Keys[] observedKeys =
            {
                Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.D, Keys.E, Keys.V,
            };

            for (int i = 0; i < observedKeys.Length; i++)
            {
                Keys key = observedKeys[i];
                if (!Input.WasKeyPressed(key))
                    continue;

                Keys expected = DeveloperToolsCode[developerCodeProgress];
                if (key == expected)
                {
                    developerCodeProgress++;
                    developerCodeTimer = DeveloperCodeTimeoutSeconds;
                    if (developerCodeProgress >= DeveloperToolsCode.Length)
                    {
                        options.DeveloperToolsUnlocked = !options.DeveloperToolsUnlocked;
                        if (optionsSnapshot != null)
                            optionsSnapshot.DeveloperToolsUnlocked = options.DeveloperToolsUnlocked;
                        PersistentStorage.SaveOptions(options);
                        developerCodeProgress = 0;
                        developerCodeTimer = 0f;
                    }
                }
                else
                {
                    developerCodeProgress = key == DeveloperToolsCode[0] ? 1 : 0;
                    developerCodeTimer = developerCodeProgress > 0 ? DeveloperCodeTimeoutSeconds : 0f;
                }

                break;
            }
        }

        private BossDefinition ResolveBossDefinitionForStage(int stageNumber, StageDefinition stage)
        {
            if (stage?.Boss == null)
            {
                selectedBossVariantId = string.Empty;
                return null;
            }

            BossDefinition resolved = CloneBossDefinition(stage.Boss);
            resolved.PresentationScale = PresentationProgression.GetBossPresentationScale(stageNumber, resolved);
            resolved.ScreenCoverageTarget = PresentationProgression.GetBossCoverageTarget(stageNumber, resolved);
            selectedBossVariantId = string.Empty;

            BossVariantDefinition secretVariant = null;
            if (stageNumber == 40)
            {
                secretVariant = stage.Boss.Variants?.FirstOrDefault(variant => string.Equals(variant.Id, "combined-sixth", StringComparison.OrdinalIgnoreCase));
                if (secretVariant == null)
                {
                    secretVariant = new BossVariantDefinition
                    {
                        Id = "combined-sixth",
                        Type = BossType.CombinedBossSixth,
                        DisplayName = "SIXFOLD FUSION",
                        ArchetypeId = "BossFinal",
                        ChancePercent = 10f,
                        PresentationScaleMultiplier = 1.16f,
                        HitPointMultiplier = 1.24f,
                    };
                }
            }

            if (secretVariant != null && gameplayRandom.NextDouble() < secretVariant.ChancePercent / 100f)
            {
                selectedBossVariantId = secretVariant.Id;
                resolved.Type = secretVariant.Type;
                resolved.DisplayName = string.IsNullOrWhiteSpace(secretVariant.DisplayName) ? resolved.DisplayName : secretVariant.DisplayName;
                resolved.ArchetypeId = string.IsNullOrWhiteSpace(secretVariant.ArchetypeId) ? resolved.ArchetypeId : secretVariant.ArchetypeId;
                resolved.HitPoints = Math.Max(resolved.HitPoints + 24, (int)MathF.Round(resolved.HitPoints * secretVariant.HitPointMultiplier));
                resolved.PresentationScale = Math.Max(resolved.PresentationScale, resolved.PresentationScale * secretVariant.PresentationScaleMultiplier);
                resolved.ScreenCoverageTarget = Math.Max(0.48f, resolved.ScreenCoverageTarget);
                resolved.PhaseThresholds = new List<float> { 0.88f, 0.7f, 0.52f, 0.34f, 0.18f };
                resolved.FirePattern = FirePattern.BossFan;
                resolved.MovePattern = MovePattern.BossOrbit;
            }

            return resolved;
        }

        private static BossDefinition CloneBossDefinition(BossDefinition source)
        {
            if (source == null)
                return null;

            return new BossDefinition
            {
                Type = source.Type,
                DisplayName = source.DisplayName,
                ArchetypeId = source.ArchetypeId,
                IntroSeconds = source.IntroSeconds,
                TargetY = source.TargetY,
                ArenaScrollSpeed = source.ArenaScrollSpeed,
                HitPoints = source.HitPoints,
                AllowRandomEvents = source.AllowRandomEvents,
                PresentationScale = source.PresentationScale,
                ScreenCoverageTarget = source.ScreenCoverageTarget,
                PhaseThresholds = new List<float>(source.PhaseThresholds ?? new List<float>()),
                HazardOverrides = new List<RandomEventType>(source.HazardOverrides ?? new List<RandomEventType>()),
                Variants = source.Variants != null ? new List<BossVariantDefinition>(source.Variants) : new List<BossVariantDefinition>(),
                MoodOverride = source.MoodOverride ?? new BackgroundMoodDefinition(),
                MovePattern = source.MovePattern,
                FirePattern = source.FirePattern,
            };
        }

        private static BossDefinitionSnapshotData CaptureBossDefinition(BossDefinition source)
        {
            if (source == null)
                return null;

            return new BossDefinitionSnapshotData
            {
                VariantId = source.Type == BossType.CombinedBossSixth ? "combined-sixth" : string.Empty,
                Type = source.Type,
                DisplayName = source.DisplayName ?? string.Empty,
                ArchetypeId = source.ArchetypeId ?? string.Empty,
                IntroSeconds = source.IntroSeconds,
                TargetY = source.TargetY,
                ArenaScrollSpeed = source.ArenaScrollSpeed,
                HitPoints = source.HitPoints,
                AllowRandomEvents = source.AllowRandomEvents,
                PresentationScale = source.PresentationScale,
                ScreenCoverageTarget = source.ScreenCoverageTarget,
                PhaseThresholds = source.PhaseThresholds != null ? new List<float>(source.PhaseThresholds) : new List<float>(),
                HazardOverrides = source.HazardOverrides != null ? new List<RandomEventType>(source.HazardOverrides) : new List<RandomEventType>(),
                MoodOverride = source.MoodOverride ?? new BackgroundMoodDefinition(),
                MovePattern = source.MovePattern,
                FirePattern = source.FirePattern,
            };
        }

        private static BossDefinition RestoreBossDefinition(BossDefinitionSnapshotData snapshot, BossDefinition fallback)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.ArchetypeId))
                return CloneBossDefinition(fallback);

            return new BossDefinition
            {
                Type = snapshot.Type,
                DisplayName = snapshot.DisplayName,
                ArchetypeId = snapshot.ArchetypeId,
                IntroSeconds = snapshot.IntroSeconds,
                TargetY = snapshot.TargetY,
                ArenaScrollSpeed = snapshot.ArenaScrollSpeed,
                HitPoints = snapshot.HitPoints,
                AllowRandomEvents = snapshot.AllowRandomEvents,
                PresentationScale = snapshot.PresentationScale,
                ScreenCoverageTarget = snapshot.ScreenCoverageTarget,
                PhaseThresholds = snapshot.PhaseThresholds != null ? new List<float>(snapshot.PhaseThresholds) : new List<float>(),
                HazardOverrides = snapshot.HazardOverrides != null ? new List<RandomEventType>(snapshot.HazardOverrides) : new List<RandomEventType>(),
                MoodOverride = snapshot.MoodOverride ?? new BackgroundMoodDefinition(),
                MovePattern = snapshot.MovePattern,
                FirePattern = snapshot.FirePattern,
            };
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
                        BeginFreshCampaign(PlayerStatus.RunProgress.Difficulty);
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
                draftCards.Add(CreateDraftCard(UpgradeCardType.MobilityTuning));
                draftCards.Add(CreateDraftCard(UpgradeCardType.RewindBattery));
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
            string[] hotkeys = { "A", "S", "D" };
            for (int i = 0; i < draftCards.Count; i++)
                draftCards[i].HotkeyLabel = i < hotkeys.Length ? hotkeys[i] : string.Empty;

            draftTimer = 5f;
            draftReturnState = returnState;
            draftFromTutorial = tutorialMode;
            state = GameFlowState.UpgradeDraft;
        }

        private UpgradeDraftCard BuildWeaponDraftCard(WeaponStyleId styleId)
        {
            WeaponInventoryState inventory = PlayerStatus.RunProgress.Weapons;
            WeaponStyleDefinition activeStyle = WeaponCatalog.GetStyle(styleId);
            string subtitle;
            string description;
            string deltaText;
            string previewText = GetWeaponDraftPreview(styleId);
            if (!inventory.OwnsStyle(styleId))
            {
                subtitle = "UNLOCK + EQUIP";
                description = string.Concat("UNLOCK ", activeStyle.DisplayName, " AND EQUIP IT");
                deltaText = "LV 0 -> LV 1";
            }
            else
            {
                int styleLevel = inventory.GetLevel(styleId);
                if (styleLevel < 3)
                {
                    subtitle = "LEVEL SURGE";
                    description = string.Concat("BOOST ", activeStyle.DisplayName, " TO LEVEL ", (styleLevel + 1).ToString());
                    deltaText = string.Concat("LV ", styleLevel.ToString(), " -> LV ", (styleLevel + 1).ToString());
                }
                else
                {
                    subtitle = "RANK SURGE";
                    description = string.Concat("RAISE ", activeStyle.DisplayName, " TO RANK ", (inventory.GetRank(styleId) + 1).ToString());
                    deltaText = string.Concat("RK ", inventory.GetRank(styleId).ToString(), " -> RK ", (inventory.GetRank(styleId) + 1).ToString());
                }
            }

            return new UpgradeDraftCard
            {
                Type = UpgradeCardType.WeaponSurge,
                StyleId = styleId,
                Title = string.Concat(activeStyle.DisplayName, " SURGE"),
                Subtitle = subtitle,
                Description = description,
                PreviewText = previewText,
                DeltaText = deltaText,
                BadgeText = "WEAPON",
                AccentColor = activeStyle.AccentColor,
            };
        }

        private UpgradeDraftCard CreateDraftCard(UpgradeCardType type)
        {
            switch (type)
            {
                case UpgradeCardType.MobilityTuning:
                    return CreateDraftCard(
                        type,
                        "THRUSTER TUNE",
                        "SYSTEM BOOST",
                        "INCREASE SHIP MOVE SPEED FOR THE RUN",
                        string.Concat("SPD ", MathF.Round(PlayerStatus.RunProgress.MoveSpeedMultiplier * 100f).ToString("0"), "% -> ", MathF.Round(MathF.Min(1.8f, PlayerStatus.RunProgress.MoveSpeedMultiplier + 0.08f) * 100f).ToString("0"), "%"),
                        "+SPEED",
                        "#6EC1FF");
                case UpgradeCardType.EmergencyReserve:
                    return CreateDraftCard(
                        type,
                        "EMERGENCY RESERVE",
                        "SURVIVAL",
                        "GAIN +1 SHIP NOW AND ON EACH NEW LIFE",
                        string.Concat("SHIPS ", PlayerStatus.RunProgress.ShipsPerLife.ToString(), " -> ", (PlayerStatus.RunProgress.ShipsPerLife + 1).ToString()),
                        "+1 SHIP",
                        "#FFB347");
                case UpgradeCardType.RewindBattery:
                    return CreateDraftCard(
                        type,
                        "REWIND CELL",
                        "UTILITY",
                        "LOWER REWIND METER DRAIN AND REFILL IT",
                        string.Concat("DRAIN -", MathF.Round((PlayerStatus.RunProgress.RewindEfficiency + 0.12f) * 100f).ToString("0"), "%"),
                        "-DRAIN",
                        "#56F0FF");
                case UpgradeCardType.LuckyCore:
                    return CreateDraftCard(
                        type,
                        "LUCKY CORE",
                        "ECONOMY",
                        "IMPROVE DROP MOMENTUM AND EARN SCORE",
                        string.Concat("DROP +", MathF.Round((PlayerStatus.RunProgress.DropBonusChance + 0.03f) * 100f).ToString("0"), "%"),
                        "+DROP",
                        "#7AE582");
                default:
                    return BuildWeaponDraftCard(draftChargeStyle);
            }
        }

        private static UpgradeDraftCard CreateDraftCard(UpgradeCardType type, string title, string subtitle, string description, string deltaText, string previewText, string accent)
        {
            return new UpgradeDraftCard
            {
                Type = type,
                Title = title,
                Subtitle = subtitle,
                Description = description,
                DeltaText = deltaText,
                PreviewText = previewText,
                BadgeText = "SYSTEM",
                AccentColor = accent,
            };
        }

        private static string GetWeaponDraftPreview(WeaponStyleId styleId)
        {
            return styleId switch
            {
                WeaponStyleId.Pulse => "DMG +RATE",
                WeaponStyleId.Spread => "ARC +PELLETS",
                WeaponStyleId.Laser => "BEAM +TICKS",
                WeaponStyleId.Plasma => "BLAST +AREA",
                WeaponStyleId.Missile => "HOMING +BLAST",
                WeaponStyleId.Rail => "PIERCE +RANGE",
                WeaponStyleId.Arc => "CHAIN +JOLT",
                WeaponStyleId.Blade => "WAVE +COVER",
                WeaponStyleId.Drone => "DRONES +BOLTS",
                WeaponStyleId.Fortress => "BURST +WALL",
                _ => "POWER UP",
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
                float spawnX = Game1.ScreenSize.X + Math.Max(40f, group.SpawnLeadDistance + 120f + index * spacingX);
                float depthAnchor = gameplayRandom.NextFloat(-CombatSpaceMath.MaxDepth * 0.78f, CombatSpaceMath.MaxDepth * 0.78f);
                float depthAmplitude = 18f + gameplayRandom.NextFloat(0f, 34f);
                float depthFrequency = 0.48f + gameplayRandom.NextFloat(0f, 0.55f);
                scheduledSpawns.Add(new ScheduledSpawn
                {
                    SpawnAtSeconds = stageStartSeconds + groupStartSeconds + spawnIntervalSeconds * index,
                    Group = group,
                    SpawnPoint = new Vector2(spawnX, targetY),
                    CombatSpawnPoint = new Vector3(spawnX, targetY, depthAnchor),
                    TargetY = targetY,
                    MovePattern = group.MovePatternOverride ?? archetype.MovePattern,
                    FirePattern = group.FirePatternOverride ?? archetype.FirePattern,
                    Amplitude = group.Amplitude > 0f ? group.Amplitude : archetype.MovementAmplitude,
                    Frequency = group.Frequency > 0f ? group.Frequency : archetype.MovementFrequency,
                    DepthAnchor = depthAnchor,
                    DepthAmplitude = depthAmplitude,
                    DepthFrequency = depthFrequency,
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
                    scheduledSpawn.Frequency,
                    scheduledSpawn.CombatSpawnPoint,
                    scheduledSpawn.DepthAnchor,
                    scheduledSpawn.DepthAmplitude,
                    scheduledSpawn.DepthFrequency));
            }
        }

        public bool TryQueueEnemyReentry(Enemy enemy)
        {
            if (enemy == null || enemy.IsBoss || currentStage?.Sections == null || currentStage.Sections.Count == 0)
                return false;

            if (state != GameFlowState.Playing || activeBoss != null || transitionToBoss || currentSectionIndex >= currentStage.Sections.Count)
                return false;

            if (enemy.ReentryRollConsumed || gameplayRandom.NextInt(0, 100) >= 50)
                return false;

            SectionDefinition section = ResolveReentrySection();
            if (section == null)
                return false;

            EnemySnapshotData snapshot = enemy.CaptureSnapshot();
            snapshot.ReentryRollConsumed = true;
            snapshot.WasReentrySpawn = true;

            float spawnAt = Math.Max(
                stageElapsedSeconds + gameplayRandom.NextFloat(2.2f, 5.4f),
                section.StartSeconds + gameplayRandom.NextFloat(0.4f, Math.Max(1.2f, section.DurationSeconds * 0.6f)));

            float targetY = ResolveReentryTargetY(enemy.Position.Y);
            float depthAnchor = ResolveReentryDepth(enemy.LateralDepth);
            Vector2 spawnPoint = new Vector2(Game1.ScreenSize.X + gameplayRandom.NextFloat(360f, 620f), targetY);
            reentryTickets.Add(new ReentryTicket
            {
                TriggerAtSeconds = spawnAt,
                SpawnPoint = spawnPoint,
                CombatSpawnPoint = new Vector3(spawnPoint.X, spawnPoint.Y, depthAnchor),
                TargetY = targetY,
                SpeedMultiplier = Math.Max(0.75f, snapshot.SpeedMultiplier),
                Amplitude = snapshot.MovementAmplitude,
                Frequency = snapshot.MovementFrequency,
                DepthAnchor = depthAnchor,
                DepthAmplitude = Math.Max(16f, snapshot.DepthAmplitude),
                DepthFrequency = snapshot.DepthFrequency <= 0f ? 1f : snapshot.DepthFrequency,
                Enemy = snapshot,
            });
            reentryTickets.Sort((left, right) => left.TriggerAtSeconds.CompareTo(right.TriggerAtSeconds));
            return true;
        }

        private void SpawnDueReentries()
        {
            while (reentryTickets.Count > 0 && reentryTickets[0].TriggerAtSeconds <= stageElapsedSeconds)
            {
                ReentryTicket ticket = reentryTickets[0];
                reentryTickets.RemoveAt(0);

                if (!repository.ArchetypesById.TryGetValue(ticket.Enemy.ArchetypeId, out EnemyArchetypeDefinition archetype))
                    continue;

                Enemy enemy = Enemy.FromReentrySnapshot(
                    archetype,
                    ticket.Enemy,
                    ticket.SpawnPoint,
                    ticket.CombatSpawnPoint,
                    ticket.TargetY,
                    ticket.SpeedMultiplier);
                if (enemy != null)
                    EntityManager.Add(enemy);
            }
        }

        private SectionDefinition ResolveReentrySection()
        {
            if (currentStage?.Sections == null)
                return null;

            for (int index = currentSectionIndex; index < currentStage.Sections.Count; index++)
            {
                SectionDefinition section = currentStage.Sections[index];
                if (!section.AllowReentryAmbushes)
                    continue;

                if (section.StartSeconds > stageElapsedSeconds + 0.9f)
                    return section;
            }

            return null;
        }

        private float ResolveReentryTargetY(float previousY)
        {
            float margin = Game1.ScreenSize.Y * 0.12f;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                float targetY = gameplayRandom.NextFloat(margin, Game1.ScreenSize.Y - margin);
                if (Math.Abs(targetY - previousY) >= Game1.ScreenSize.Y * 0.16f)
                    return targetY;
            }

            return MathHelper.Clamp(Game1.ScreenSize.Y - previousY, margin, Game1.ScreenSize.Y - margin);
        }

        private float ResolveReentryDepth(float previousDepth)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                float depth = gameplayRandom.NextFloat(-CombatSpaceMath.MaxDepth * 0.82f, CombatSpaceMath.MaxDepth * 0.82f);
                if (Math.Abs(depth - previousDepth) >= 28f)
                    return depth;
            }

            return MathHelper.Clamp(-previousDepth, -CombatSpaceMath.MaxDepth, CombatSpaceMath.MaxDepth);
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
            Vector3 combatPosition;
            Vector3 combatVelocity;
            ProceduralSpriteDefinition sprite;
            ImpactProfileDefinition impact;
            int damage;
            float scale;
            float depth = gameplayRandom.NextFloat(-CombatSpaceMath.MaxDepth * 0.82f, CombatSpaceMath.MaxDepth * 0.82f);
            float depthVelocity;

            switch (eventType)
            {
                case RandomEventType.MeteorShower:
                    position = new Vector2(Game1.ScreenSize.X + 40f + gameplayRandom.NextInt(0, 160), gameplayRandom.NextInt(0, Game1.VirtualHeight / 2));
                    velocity = new Vector2(-320f - 60f * intensity, 180f + gameplayRandom.NextInt(-40, 80));
                    depthVelocity = gameplayRandom.NextFloat(-36f, 36f);
                    sprite = CreateEventProjectileDefinition("#B56A46", "#EABF8F", "#FFF1C9", new[] { ".#.", "###", ".#." });
                    impact = new ImpactProfileDefinition { Name = "Meteor", Kernel = ImpactKernelShape.Blast5, BaseCellsRemoved = 5, BonusCellsPerDamage = 1, SplashRadius = 1, SplashPercent = 35, DebrisBurstCount = 10, DebrisSpeed = 150f };
                    damage = 2;
                    scale = 1.4f;
                    break;
                case RandomEventType.CometSwarm:
                    position = new Vector2(Game1.ScreenSize.X + 40f + gameplayRandom.NextInt(0, 180), gameplayRandom.NextInt(40, Game1.VirtualHeight - 40));
                    velocity = new Vector2(-420f - 100f * intensity, gameplayRandom.NextInt(-80, 81));
                    depthVelocity = gameplayRandom.NextFloat(-28f, 28f);
                    sprite = CreateEventProjectileDefinition("#DFF4FF", "#8FD3FF", "#FFF0AF", new[] { "##", "##" });
                    impact = new ImpactProfileDefinition { Name = "Comet", Kernel = ImpactKernelShape.Diamond3, BaseCellsRemoved = 4, BonusCellsPerDamage = 1, SplashRadius = 0, SplashPercent = 0, DebrisBurstCount = 8, DebrisSpeed = 180f };
                    damage = 2;
                    scale = 1.1f;
                    break;
                default:
                    position = new Vector2(Game1.ScreenSize.X + 30f + gameplayRandom.NextInt(0, 120), gameplayRandom.NextInt(0, Game1.VirtualHeight));
                    velocity = new Vector2(-210f - 40f * intensity, gameplayRandom.NextInt(-30, 31));
                    depthVelocity = gameplayRandom.NextFloat(-20f, 20f);
                    sprite = CreateEventProjectileDefinition("#B7C6D8", "#6D859A", "#EAF6FF", new[] { "##.", ".##" });
                    impact = new ImpactProfileDefinition { Name = "Debris", Kernel = ImpactKernelShape.Cross3, BaseCellsRemoved = 3, BonusCellsPerDamage = 1, SplashRadius = 0, SplashPercent = 0, DebrisBurstCount = 6, DebrisSpeed = 110f };
                    damage = 1;
                    scale = 1f;
                    break;
            }

            combatPosition = new Vector3(position.X, position.Y, depth);
            combatVelocity = new Vector3(velocity.X, velocity.Y, depthVelocity);
            EntityManager.Add(new Bullet(position, velocity, false, damage, impact, sprite, 0, 3.8f, 0f, scale, ProjectileBehavior.Bolt, TrailFxStyle.None, ImpactFxStyle.Standard, 0f, 0, 0f, combatPosition, combatVelocity));
        }

        private void SpawnBoss()
        {
            BossDefinition bossDefinition = ActiveStageBossDefinition;
            if (bossDefinition == null)
                return;

            EnemyArchetypeDefinition archetype = repository.ArchetypesById[bossDefinition.ArchetypeId];
            Vector2 spawnPoint = new Vector2(Game1.ScreenSize.X + archetype.SpawnLeadDistance, bossDefinition.TargetY * Game1.ScreenSize.Y);
            activeBoss = new BossEnemy(archetype, bossDefinition, spawnPoint, new Vector3(spawnPoint.X, spawnPoint.Y, 0f));
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
            if (ActiveStageBossDefinition != null)
                medals.UnlockBossClear(currentStageNumber);
            PersistentStorage.SaveMedals(medals);
        }

        private void OpenDifficultySelect(bool skipTutorial)
        {
            difficultySelectSkipTutorial = skipTutorial;
            int selection = Array.IndexOf(DifficultyChoices, options.LastSelectedDifficulty);
            difficultySelection = selection >= 0 ? selection : 0;
            state = GameFlowState.DifficultySelect;
        }

        private void ConfirmDifficultySelection()
        {
            GameDifficulty difficulty = DifficultyChoices[Math.Clamp(difficultySelection, 0, DifficultyChoices.Length - 1)];
            options.LastSelectedDifficulty = difficulty;
            if (optionsSnapshot != null)
                optionsSnapshot.LastSelectedDifficulty = difficulty;

            if (difficultySelectSkipTutorial)
            {
                options.TutorialCompleted = true;
                if (optionsSnapshot != null)
                    optionsSnapshot.TutorialCompleted = true;
            }

            PersistentStorage.SaveOptions(options);

            if (difficultySelectSkipTutorial)
            {
                BeginFreshCampaign(difficulty);
                return;
            }

            if (!options.TutorialCompleted)
            {
                StartTutorial(false, GameFlowState.Playing, difficulty);
                return;
            }

            BeginFreshCampaign(difficulty);
        }

        private GameDifficulty ResolvePreferredRunDifficulty()
        {
            if (currentStage != null && state != GameFlowState.Title && state != GameFlowState.GameOver && state != GameFlowState.CampaignComplete)
                return PlayerStatus.RunProgress.Difficulty;

            return options.LastSelectedDifficulty;
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
                OpenDifficultySelect(true);
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
                OpenOptions(GameFlowState.Title);
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

        private Rectangle GetDifficultySelectPanelBounds()
        {
            return new Rectangle(Game1.VirtualWidth / 2 - UiPx(470), UiPx(88), UiPx(940), Game1.VirtualHeight - UiPx(176));
        }

        private Rectangle GetDifficultySummaryBounds(Rectangle panel)
        {
            return new Rectangle(panel.X + UiPx(404), panel.Y + UiPx(122), panel.Width - UiPx(458), panel.Height - UiPx(212));
        }

        private List<UiButton> GetDifficultyButtons()
        {
            float uiScale = Game1.Instance != null ? Game1.Instance.UiLayoutScale : 1f;
            Rectangle panel = GetDifficultySelectPanelBounds();
            int width = Math.Max(UiPx(300), (int)MathF.Round(324f * uiScale));
            int height = Math.Max(UiPx(42), (int)MathF.Round(44f * uiScale));
            int spacing = Math.Max(UiPx(10), (int)MathF.Round(12f * uiScale));
            int x = panel.X + UiPx(52);
            int top = panel.Y + UiPx(122);

            var buttons = new List<UiButton>(DifficultyChoices.Length + 1);
            for (int i = 0; i < DifficultyChoices.Length; i++)
                buttons.Add(new UiButton(new Rectangle(x, top + i * (height + spacing), width, height), DifficultyTuning.GetLabel(DifficultyChoices[i])));

            buttons.Add(new UiButton(new Rectangle(x, top + DifficultyChoices.Length * (height + spacing) + UiPx(6), width, height), "BACK"));
            return buttons;
        }

        private bool HandlePointerSelection(List<UiButton> buttons, ref int selection)
        {
            return HandlePointerSelectionCore(buttons.Select(button => button.Bounds).ToArray(), ref selection, 7000);
        }

        private bool HandlePointerSelection(IReadOnlyList<Rectangle> bounds, ref int selection)
        {
            return HandlePointerSelectionCore(bounds, ref selection, 8000);
        }

        private bool HandlePointerSelectionCore(IReadOnlyList<Rectangle> bounds, ref int selection, int controlIdBase)
        {
            Vector2 pointer = Input.PointerPosition;
            int hitIndex = -1;
            for (int i = 0; i < bounds.Count; i++)
            {
                if (bounds[i].Contains(pointer))
                {
                    hitIndex = i;
                    break;
                }
            }

            if (hitIndex >= 0 && (Input.DidUiPointerMove() || Input.IsUiPointerHeld() || Input.WasUiPointerPressed()))
                selection = hitIndex;

            if (Input.WasUiPointerPressed())
            {
                if (hitIndex >= 0)
                    Input.CaptureUiControl(controlIdBase + hitIndex);
                else
                    Input.ClearUiControlCapture();
            }

            if (Input.WasUiPointerReleased())
            {
                bool activated = hitIndex >= 0
                    && Input.IsUiControlCaptured(controlIdBase + hitIndex)
                    && !Input.IsUiPointerDragging();
                Input.ClearUiControlCapture();
                return activated;
            }

            return false;
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

        private List<PauseMenuEntry> GetPauseEntries()
        {
            bool tutorialPause = pauseReturnState == GameFlowState.Tutorial;
            bool includeDeveloperEntries = options.DeveloperToolsUnlocked && !tutorialPause && currentStage != null;
            int total = tutorialPause ? 7 : includeDeveloperEntries ? 8 : 6;
            var entries = new List<PauseMenuEntry>
            {
                new PauseMenuEntry(PauseMenuAction.Resume, CreateButton("RESUME", 0, total)),
                new PauseMenuEntry(PauseMenuAction.SaveGame, CreateButton("SAVE GAME", 1, total)),
                new PauseMenuEntry(PauseMenuAction.LoadGame, CreateButton("LOAD GAME", 2, total)),
                new PauseMenuEntry(PauseMenuAction.Options, CreateButton("OPTIONS", 3, total)),
                new PauseMenuEntry(PauseMenuAction.Help, CreateButton("HELP", 4, total)),
            };

            int nextIndex = 5;
            if (includeDeveloperEntries)
            {
                entries.Add(new PauseMenuEntry(PauseMenuAction.DeveloperStage, CreateButton(string.Concat("DEV STAGE ", currentStageNumber.ToString("00")), nextIndex++, total)));
                entries.Add(new PauseMenuEntry(PauseMenuAction.DeveloperDetail, CreateButton(GetDeveloperDetailLabel(), nextIndex++, total)));
            }

            if (tutorialPause)
                entries.Add(new PauseMenuEntry(PauseMenuAction.SkipTutorial, CreateButton("SKIP TUTORIAL", nextIndex++, total)));

            entries.Add(new PauseMenuEntry(PauseMenuAction.QuitToTitle, CreateButton("QUIT TO TITLE", total - 1, total)));
            return entries;
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

        private OptionsData CloneOptions(OptionsData source)
        {
            return new OptionsData
            {
                ShowHelpHints = source.ShowHelpHints,
                TutorialCompleted = source.TutorialCompleted,
                AutoUpgradeDraft = source.AutoUpgradeDraft,
                LastSelectedDifficulty = source.LastSelectedDifficulty,
                DisplayMode = source.DisplayMode,
                UiScalePercent = source.UiScalePercent,
                TouchControlsOpacity = source.TouchControlsOpacity,
                Invert3DHorizontal = source.Invert3DHorizontal,
                Invert3DVertical = source.Invert3DVertical,
                HasMigrated3DHorizontalDefault = source.HasMigrated3DHorizontalDefault,
                AimAssist3DMode = source.AimAssist3DMode,
                FontTheme = source.FontTheme,
                VisualPreset = source.VisualPreset,
                EnableBloom = source.EnableBloom,
                EnableShockwaves = source.EnableShockwaves,
                EnableNeonOutlines = source.EnableNeonOutlines,
                MasterVolume = source.MasterVolume,
                MusicVolume = source.MusicVolume,
                SfxVolume = source.SfxVolume,
                AudioQualityPreset = source.AudioQualityPreset,
                ScreenShakeStrength = source.ScreenShakeStrength,
                DeveloperToolsUnlocked = source.DeveloperToolsUnlocked,
            };
        }

        private void CopyOptions(OptionsData source, OptionsData target)
        {
            target.ShowHelpHints = source.ShowHelpHints;
            target.TutorialCompleted = source.TutorialCompleted;
            target.AutoUpgradeDraft = source.AutoUpgradeDraft;
            target.LastSelectedDifficulty = source.LastSelectedDifficulty;
            target.DisplayMode = source.DisplayMode;
            target.UiScalePercent = source.UiScalePercent;
            target.TouchControlsOpacity = source.TouchControlsOpacity;
            target.Invert3DHorizontal = source.Invert3DHorizontal;
            target.Invert3DVertical = source.Invert3DVertical;
            target.HasMigrated3DHorizontalDefault = source.HasMigrated3DHorizontalDefault;
            target.AimAssist3DMode = source.AimAssist3DMode;
            target.FontTheme = source.FontTheme;
            target.VisualPreset = source.VisualPreset;
            target.EnableBloom = source.EnableBloom;
            target.EnableShockwaves = source.EnableShockwaves;
            target.EnableNeonOutlines = source.EnableNeonOutlines;
            target.MasterVolume = source.MasterVolume;
            target.MusicVolume = source.MusicVolume;
            target.SfxVolume = source.SfxVolume;
            target.AudioQualityPreset = source.AudioQualityPreset;
            target.ScreenShakeStrength = source.ScreenShakeStrength;
            target.DeveloperToolsUnlocked = source.DeveloperToolsUnlocked;
        }

        private void ApplyOptionsState()
        {
#if !ANDROID
            Game1.Instance?.ApplyDisplayMode(options.DisplayMode);
#endif
            if (Game1.Instance != null && Game1.Instance.AudioQualityPreset != options.AudioQualityPreset)
                Game1.Instance.ApplyAudioQuality(options.AudioQualityPreset);
        }

        private void OpenOptions(GameFlowState returnState)
        {
            slotReturnState = returnState;
            optionsSelection = 0;
            optionsSliderDragIndex = -1;
            optionsScrollOffset = 0f;
            optionsSection = OptionMenuSection.General;
            optionsSnapshot = CloneOptions(options);
            pendingAudioQualityPreset = options.AudioQualityPreset;
            audioQualityDialogOpen = false;
            audioQualityApplyPending = false;
            audioQualityApplyDelay = 0f;
            Input.ClearUiControlCapture();
            state = GameFlowState.Options;
        }

        private void ConfirmOptionsAndClose()
        {
            optionsSnapshot = CloneOptions(options);
            PersistentStorage.SaveOptions(options);
            Input.ClearUiControlCapture();
            state = slotReturnState;
        }

        private void DiscardOptionsAndClose()
        {
            if (optionsSnapshot != null)
            {
                CopyOptions(optionsSnapshot, options);
                ApplyOptionsState();
            }

            Input.ClearUiControlCapture();
            state = slotReturnState;
        }

        private Rectangle GetOptionsFrameBounds()
        {
            Rectangle safeBounds = Game1.SafeUiBounds;
            int horizontalMargin = UiPx(36);
            int verticalMargin = UiPx(24);
            return new Rectangle(
                safeBounds.X + horizontalMargin,
                safeBounds.Y + verticalMargin,
                Math.Max(UiPx(820), safeBounds.Width - horizontalMargin * 2),
                Math.Max(UiPx(520), safeBounds.Height - verticalMargin * 2));
        }

        private Rectangle GetOptionsListViewportBounds()
        {
            Rectangle frame = GetOptionsFrameBounds();
            int headerHeight = UiPx(108);
            int footerHeight = UiPx(156);
            return new Rectangle(
                frame.X + UiPx(18),
                frame.Y + headerHeight,
                frame.Width - UiPx(54),
                Math.Max(UiPx(180), frame.Height - headerHeight - footerHeight));
        }

        private Rectangle[] GetOptionRowBounds()
        {
            return GetOptionRowBounds(GetOptionsListViewportBounds(), GetOptionRows().Length);
        }

        private Rectangle[] GetOptionRowBounds(Rectangle viewport, int rowCount)
        {
            int rowHeight = UiPx(40);
            int rowStep = UiPx(46);
            int rowWidth = viewport.Width - UiPx(18);
            int rowX = viewport.X;
            int top = viewport.Y + UiPx(8) - (int)MathF.Round(optionsScrollOffset);
            var bounds = new Rectangle[rowCount];
            for (int i = 0; i < rowCount; i++)
                bounds[i] = new Rectangle(rowX, top + i * rowStep, rowWidth, rowHeight);

            return bounds;
        }

        private float GetOptionsMaxScroll()
        {
            Rectangle viewport = GetOptionsListViewportBounds();
            int rowHeight = UiPx(40);
            int rowStep = UiPx(46);
            int rowCount = GetOptionRows().Length;
            int contentHeight = UiPx(16) + rowCount * rowStep - (rowStep - rowHeight);
            return MathF.Max(0f, contentHeight - viewport.Height);
        }

        private void ClampOptionsScroll()
        {
            optionsScrollOffset = MathHelper.Clamp(optionsScrollOffset, 0f, GetOptionsMaxScroll());
        }

        private (Rectangle discardBounds, Rectangle applyBounds) GetOptionActionBounds()
        {
            Rectangle frame = GetOptionsFrameBounds();
            int buttonWidth = UiPx(200);
            int buttonHeight = UiPx(40);
            int gap = UiPx(24);
            int totalWidth = buttonWidth * 2 + gap;
            int x = frame.Center.X - totalWidth / 2;
            int y = frame.Bottom - UiPx(54);
            return
            (
                new Rectangle(x, y, buttonWidth, buttonHeight),
                new Rectangle(x + buttonWidth + gap, y, buttonWidth, buttonHeight)
            );
        }

        private Rectangle GetOptionScrollbarBounds(Rectangle viewport)
        {
            return new Rectangle(viewport.Right + UiPx(8), viewport.Y, UiPx(16), viewport.Height);
        }

        private Rectangle GetOptionScrollbarButtonBounds(Rectangle viewport, bool isUp)
        {
            Rectangle scrollbar = GetOptionScrollbarBounds(viewport);
            int buttonHeight = UiPx(24);
            return isUp
                ? new Rectangle(scrollbar.X, scrollbar.Y, scrollbar.Width, buttonHeight)
                : new Rectangle(scrollbar.X, scrollbar.Bottom - buttonHeight, scrollbar.Width, buttonHeight);
        }

        private Rectangle GetOptionScrollbarTrackBounds(Rectangle viewport)
        {
            Rectangle scrollbar = GetOptionScrollbarBounds(viewport);
            Rectangle upButton = GetOptionScrollbarButtonBounds(viewport, true);
            Rectangle downButton = GetOptionScrollbarButtonBounds(viewport, false);
            return new Rectangle(
                scrollbar.X,
                upButton.Bottom + UiPx(4),
                scrollbar.Width,
                Math.Max(UiPx(32), downButton.Y - upButton.Bottom - UiPx(8)));
        }

        private Rectangle GetOptionScrollbarThumbBounds(Rectangle viewport)
        {
            Rectangle trackBounds = GetOptionScrollbarTrackBounds(viewport);
            float maxScroll = GetOptionsMaxScroll();
            float thumbRatio = viewport.Height / Math.Max((float)viewport.Height, viewport.Height + maxScroll);
            int thumbHeight = Math.Max(UiPx(42), (int)MathF.Round(trackBounds.Height * thumbRatio));
            float scrollRatio = maxScroll <= 0f ? 0f : optionsScrollOffset / maxScroll;
            int thumbY = trackBounds.Y + (int)MathF.Round((trackBounds.Height - thumbHeight) * scrollRatio);
            return new Rectangle(trackBounds.X, thumbY, trackBounds.Width, thumbHeight);
        }

        private Rectangle GetOptionsDescriptionBounds()
        {
            Rectangle frame = GetOptionsFrameBounds();
            return new Rectangle(frame.X + UiPx(18), frame.Bottom - UiPx(112), frame.Width - UiPx(36), UiPx(34));
        }

        private Rectangle GetOptionsControlsHintBounds()
        {
            Rectangle frame = GetOptionsFrameBounds();
            return new Rectangle(frame.X + UiPx(18), frame.Bottom - UiPx(84), frame.Width - UiPx(36), UiPx(22));
        }

        private Rectangle GetOptionSliderTrackBounds(Rectangle rowBounds)
        {
            int width = Math.Min(UiPx(250), rowBounds.Width / 4);
            return new Rectangle(rowBounds.Right - width - UiPx(18), rowBounds.Y + UiPx(13), width, UiPx(14));
        }

        private Rectangle GetOptionStepperLeftBounds(Rectangle rowBounds)
        {
            return new Rectangle(rowBounds.Right - UiPx(96), rowBounds.Y + UiPx(6), UiPx(38), rowBounds.Height - UiPx(12));
        }

        private Rectangle GetOptionStepperRightBounds(Rectangle rowBounds)
        {
            return new Rectangle(rowBounds.Right - UiPx(48), rowBounds.Y + UiPx(6), UiPx(38), rowBounds.Height - UiPx(12));
        }

        private (Rectangle leftBounds, Rectangle rightBounds, Rectangle tutorialBounds) GetHelpActionBounds()
        {
            Rectangle safe = Game1.SafeUiBounds;
            int arrowSize = UiPx(46);
            Rectangle leftBounds = new Rectangle(safe.X + UiPx(42), safe.Center.Y - arrowSize / 2, arrowSize, arrowSize);
            Rectangle rightBounds = new Rectangle(safe.Right - UiPx(42) - arrowSize, safe.Center.Y - arrowSize / 2, arrowSize, arrowSize);
            Rectangle tutorialBounds = new Rectangle((Game1.VirtualWidth - UiPx(320)) / 2, Game1.VirtualHeight - UiPx(182), UiPx(320), UiPx(44));
            return (leftBounds, rightBounds, tutorialBounds);
        }

        private Rectangle[] GetUpgradeDraftCardBounds()
        {
            int cardWidth = 300;
            int cardHeight = 220;
            int gap = 24;
            int totalWidth = cardWidth * 3 + gap * 2;
            int startX = (Game1.VirtualWidth - totalWidth) / 2;
            int y = 214;
            var bounds = new Rectangle[draftCards.Count];
            for (int i = 0; i < draftCards.Count; i++)
                bounds[i] = new Rectangle(startX + i * (cardWidth + gap), y, cardWidth, cardHeight);

            return bounds;
        }

        private (Rectangle dialogBounds, Rectangle[] presetBounds, Rectangle cancelBounds, Rectangle applyBounds) GetAudioQualityDialogBounds()
        {
            Rectangle dialogBounds = new Rectangle(Game1.VirtualWidth / 2 - UiPx(290), Game1.VirtualHeight / 2 - UiPx(170), UiPx(580), UiPx(340));
            Rectangle[] presetBounds = new Rectangle[3];
            int presetWidth = UiPx(150);
            int presetGap = UiPx(12);
            int presetStartX = dialogBounds.Center.X - ((presetWidth * 3 + presetGap * 2) / 2);
            for (int i = 0; i < presetBounds.Length; i++)
                presetBounds[i] = new Rectangle(presetStartX + i * (presetWidth + presetGap), dialogBounds.Y + UiPx(132), presetWidth, UiPx(72));

            Rectangle cancelBounds = new Rectangle(dialogBounds.Center.X - UiPx(176), dialogBounds.Bottom - UiPx(62), UiPx(150), UiPx(40));
            Rectangle applyBounds = new Rectangle(dialogBounds.Center.X + UiPx(26), dialogBounds.Bottom - UiPx(62), UiPx(150), UiPx(40));
            return (dialogBounds, presetBounds, cancelBounds, applyBounds);
        }

        private void UpdateAudioQualityDialog()
        {
            AudioQualityPreset[] presets = GetAudioQualityOptions();
            (Rectangle _, Rectangle[] presetBounds, Rectangle cancelBounds, Rectangle applyBounds) = GetAudioQualityDialogBounds();
            Vector2 pointer = Input.PointerPosition;

            if (Input.WasCancelPressed())
            {
                audioQualityDialogOpen = false;
                pendingAudioQualityPreset = options.AudioQualityPreset;
                return;
            }

            if (Input.WasNavigateLeftPressed())
                pendingAudioQualityPreset = CycleAudioQuality(-1, pendingAudioQualityPreset);
            else if (Input.WasNavigateRightPressed())
                pendingAudioQualityPreset = CycleAudioQuality(1, pendingAudioQualityPreset);

            if (Input.WasUiPointerPressed())
            {
                int controlId = PointerControlNone;
                for (int i = 0; i < presetBounds.Length; i++)
                {
                    if (presetBounds[i].Contains(pointer))
                    {
                        controlId = GetAudioDialogPresetControlId(i);
                        break;
                    }
                }

                if (controlId == PointerControlNone && cancelBounds.Contains(pointer))
                    controlId = PointerControlAudioDialogCancel;
                else if (controlId == PointerControlNone && applyBounds.Contains(pointer))
                    controlId = PointerControlAudioDialogApply;

                if (controlId != PointerControlNone)
                    Input.CaptureUiControl(controlId);
            }

            if (Input.WasUiPointerReleased())
            {
                bool activateOnRelease = !Input.IsUiPointerDragging();
                int capturedControlId = Input.CapturedUiControlId;

                for (int i = 0; i < presetBounds.Length; i++)
                {
                    if (capturedControlId == GetAudioDialogPresetControlId(i) && presetBounds[i].Contains(pointer) && activateOnRelease)
                    {
                        pendingAudioQualityPreset = presets[i];
                        Input.ClearUiControlCapture();
                        return;
                    }
                }

                if (capturedControlId == PointerControlAudioDialogCancel && cancelBounds.Contains(pointer) && activateOnRelease)
                {
                    audioQualityDialogOpen = false;
                    pendingAudioQualityPreset = options.AudioQualityPreset;
                    Input.ClearUiControlCapture();
                    return;
                }

                if (capturedControlId == PointerControlAudioDialogApply && applyBounds.Contains(pointer) && activateOnRelease)
                {
                    options.AudioQualityPreset = pendingAudioQualityPreset;
                    audioQualityDialogOpen = false;
                    audioQualityApplyPending = true;
                    audioQualityApplyDelay = 0.12f;
                    Input.ClearUiControlCapture();
                    return;
                }

                Input.ClearUiControlCapture();
            }

            if (Input.WasConfirmPressed())
            {
                options.AudioQualityPreset = pendingAudioQualityPreset;
                audioQualityDialogOpen = false;
                audioQualityApplyPending = true;
                audioQualityApplyDelay = 0.12f;
            }
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

        private OptionRowDefinition[] GetOptionRows()
        {
#if ANDROID
            return new[]
            {
                new OptionRowDefinition("UI SCALE", string.Concat(options.UiScalePercent, "%"), "SETS MENU SIZE AND GENERAL UI READABILITY.", OptionRowControlKind.Slider),
                new OptionRowDefinition("VISUAL PRESET", options.VisualPreset.ToString().ToUpperInvariant(), "CHANGES OVERALL EFFECT DENSITY, BLOOM, AND POST PROCESSING STYLE.", OptionRowControlKind.Cycle),
                new OptionRowDefinition("BLOOM", options.EnableBloom ? "ON" : "OFF", "ADDS GLOW AROUND BRIGHT SHOTS, EXPLOSIONS, AND HIGHLIGHTS.", OptionRowControlKind.Toggle),
                new OptionRowDefinition("SHOCKWAVES", options.EnableShockwaves ? "ON" : "OFF", "ENABLES RIPPLE AND IMPACT WAVE EFFECTS ON HEAVIER HITS.", OptionRowControlKind.Toggle),
                new OptionRowDefinition("NEON OUTLINES", options.EnableNeonOutlines ? "ON" : "OFF", "OUTLINES HULLS AND FX WITH A SHARPER ARCADE SILHOUETTE.", OptionRowControlKind.Toggle),
                new OptionRowDefinition("SCREEN SHAKE", options.ScreenShakeStrength.ToString().ToUpperInvariant(), "SETS HOW HARD THE CAMERA REACTS TO HITS, BOSSES, AND TRANSITIONS.", OptionRowControlKind.Cycle),
                new OptionRowDefinition("AUDIO QUALITY", options.AudioQualityPreset.ToString().ToUpperInvariant(), "CHANGES PROCEDURAL AUDIO DENSITY. APPLYING IT REBUILDS THE AUDIO BANKS.", OptionRowControlKind.Cycle),
                new OptionRowDefinition("MASTER VOLUME", string.Concat((int)(options.MasterVolume * 100f), "%"), "CONTROLS THE OVERALL MIX FOR MUSIC AND SOUND EFFECTS.", OptionRowControlKind.Slider),
                new OptionRowDefinition("MUSIC VOLUME", string.Concat((int)(options.MusicVolume * 100f), "%"), "SETS THE LEVEL OF THE PROCEDURAL MUSIC STEM MIX.", OptionRowControlKind.Slider),
                new OptionRowDefinition("SFX VOLUME", string.Concat((int)(options.SfxVolume * 100f), "%"), "SETS THE LEVEL OF SHOTS, HITS, WARNINGS, AND UI FEEDBACK.", OptionRowControlKind.Slider),
                new OptionRowDefinition("TOUCH CONTROLS", string.Concat(options.TouchControlsOpacity, "%"), "SETS HOW VISIBLE THE ON-SCREEN STICK, AIM PAD, REWIND BUTTON, AND HUD CHIPS ARE.", OptionRowControlKind.Slider),
                new OptionRowDefinition("AUTO DRAFT", options.AutoUpgradeDraft ? "ON" : "OFF", "IF A DRAFT TIMER EXPIRES, PICK A RANDOM OFFER AUTOMATICALLY.", OptionRowControlKind.Toggle),
                new OptionRowDefinition("HELP HINTS", options.ShowHelpHints ? "ON" : "OFF", "SHOWS SHORT CONTROL AND SYSTEM REMINDERS DURING PLAY.", OptionRowControlKind.Toggle),
            };
#else
            if (optionsSection == OptionMenuSection.ThreeDGameplay)
            {
                return new[]
                {
                    new OptionRowDefinition("INVERT 3D HORIZONTAL", options.Invert3DHorizontal ? "ON" : "OFF", "FLIPS LEFT AND RIGHT MOVEMENT IN CHASE VIEW.", OptionRowControlKind.Toggle),
                    new OptionRowDefinition("INVERT 3D VERTICAL", options.Invert3DVertical ? "ON" : "OFF", "FLIPS UP AND DOWN MOVEMENT IN CHASE VIEW.", OptionRowControlKind.Toggle),
                    new OptionRowDefinition("3D AIM ASSIST", options.AimAssist3DMode == AimAssist3DMode.SoftLock ? "SOFT LOCK" : "OFF", "SOFT LOCK NUDGES SHOTS TOWARD A NEARBY TARGET INSIDE A NARROW RETICLE CONE.", OptionRowControlKind.Cycle),
                    new OptionRowDefinition("BACK TO GENERAL", string.Empty, "RETURNS TO THE MAIN OPTIONS LIST.", OptionRowControlKind.Action),
                };
            }

            return new[]
            {
                new OptionRowDefinition("DISPLAY MODE", options.DisplayMode == DesktopDisplayMode.BorderlessFullscreen ? "BORDERLESS FULLSCREEN" : "WINDOWED", "SWITCHES BETWEEN WINDOWED PLAY AND BORDERLESS FULLSCREEN.", OptionRowControlKind.Cycle),
                new OptionRowDefinition("UI SCALE", string.Concat(options.UiScalePercent, "%"), "SETS MENU SIZE AND GENERAL UI READABILITY.", OptionRowControlKind.Slider),
                new OptionRowDefinition("VISUAL PRESET", options.VisualPreset.ToString().ToUpperInvariant(), "CHANGES OVERALL EFFECT DENSITY, BLOOM, AND POST PROCESSING STYLE.", OptionRowControlKind.Cycle),
                new OptionRowDefinition("BLOOM", options.EnableBloom ? "ON" : "OFF", "ADDS GLOW AROUND BRIGHT SHOTS, EXPLOSIONS, AND HIGHLIGHTS.", OptionRowControlKind.Toggle),
                new OptionRowDefinition("SHOCKWAVES", options.EnableShockwaves ? "ON" : "OFF", "ENABLES RIPPLE AND IMPACT WAVE EFFECTS ON HEAVIER HITS.", OptionRowControlKind.Toggle),
                new OptionRowDefinition("NEON OUTLINES", options.EnableNeonOutlines ? "ON" : "OFF", "OUTLINES HULLS AND FX WITH A SHARPER ARCADE SILHOUETTE.", OptionRowControlKind.Toggle),
                new OptionRowDefinition("SCREEN SHAKE", options.ScreenShakeStrength.ToString().ToUpperInvariant(), "SETS HOW HARD THE CAMERA REACTS TO HITS, BOSSES, AND TRANSITIONS.", OptionRowControlKind.Cycle),
                new OptionRowDefinition("AUDIO QUALITY", options.AudioQualityPreset.ToString().ToUpperInvariant(), "CHANGES PROCEDURAL AUDIO DENSITY. APPLYING IT REBUILDS THE AUDIO BANKS.", OptionRowControlKind.Cycle),
                new OptionRowDefinition("MASTER VOLUME", string.Concat((int)(options.MasterVolume * 100f), "%"), "CONTROLS THE OVERALL MIX FOR MUSIC AND SOUND EFFECTS.", OptionRowControlKind.Slider),
                new OptionRowDefinition("MUSIC VOLUME", string.Concat((int)(options.MusicVolume * 100f), "%"), "SETS THE LEVEL OF THE PROCEDURAL MUSIC STEM MIX.", OptionRowControlKind.Slider),
                new OptionRowDefinition("SFX VOLUME", string.Concat((int)(options.SfxVolume * 100f), "%"), "SETS THE LEVEL OF SHOTS, HITS, WARNINGS, AND UI FEEDBACK.", OptionRowControlKind.Slider),
                new OptionRowDefinition("AUTO DRAFT", options.AutoUpgradeDraft ? "ON" : "OFF", "IF A DRAFT TIMER EXPIRES, PICK A RANDOM OFFER AUTOMATICALLY.", OptionRowControlKind.Toggle),
                new OptionRowDefinition("HELP HINTS", options.ShowHelpHints ? "ON" : "OFF", "SHOWS SHORT CONTROL AND SYSTEM REMINDERS DURING PLAY.", OptionRowControlKind.Toggle),
                new OptionRowDefinition("3D GAMEPLAY", "OPEN", "OPENS CHASE-VIEW CONTROLS AND AIM-ASSIST SETTINGS.", OptionRowControlKind.Submenu),
            };
#endif
        }

        private static AudioQualityPreset[] GetAudioQualityOptions()
        {
            return new[]
            {
                AudioQualityPreset.Reduced,
                AudioQualityPreset.Standard,
                AudioQualityPreset.High,
            };
        }

        private bool IsSliderOptionRow(int index)
        {
            OptionRowDefinition[] rows = GetOptionRows();
            return index >= 0
                && index < rows.Length
                && rows[index].ControlKind == OptionRowControlKind.Slider;
        }

        private float GetSliderRatioForOption(int index)
        {
            return index switch
            {
                0 => (options.UiScalePercent - 70f) / 150f,
#if ANDROID
                7 => options.MasterVolume,
                8 => options.MusicVolume,
                9 => options.SfxVolume,
                10 => (options.TouchControlsOpacity - 20f) / 80f,
#else
                _ when optionsSection == OptionMenuSection.ThreeDGameplay => 0f,
                8 => options.MasterVolume,
                9 => options.MusicVolume,
                10 => options.SfxVolume,
#endif
                _ => 0f,
            };
        }

        private void SetSliderOptionFromRatio(int index, float ratio)
        {
            float clamped = MathHelper.Clamp(ratio, 0f, 1f);
            switch (index)
            {
                case 0:
                {
                    int uiPercent = 70 + (int)MathF.Round(clamped * 150f);
                    options.UiScalePercent = UiScaleHelper.ClampUiScalePercent(uiPercent);
                    break;
                }
#if ANDROID
                case 7:
                    options.MasterVolume = MathF.Round(clamped * 20f) / 20f;
                    break;
                case 8:
                    options.MusicVolume = MathF.Round(clamped * 20f) / 20f;
                    break;
                case 9:
                    options.SfxVolume = MathF.Round(clamped * 20f) / 20f;
                    break;
                case 10:
                    options.TouchControlsOpacity = UiScaleHelper.ClampTouchControlsOpacity(20 + (int)MathF.Round(clamped * 80f));
                    break;
#else
                case 8:
                    options.MasterVolume = MathF.Round(clamped * 20f) / 20f;
                    break;
                case 9:
                    options.MusicVolume = MathF.Round(clamped * 20f) / 20f;
                    break;
                case 10:
                    options.SfxVolume = MathF.Round(clamped * 20f) / 20f;
                    break;
#endif
            }
        }

        private void AdjustOptionByStep(int index, int delta)
        {
#if !ANDROID
            if (optionsSection == OptionMenuSection.ThreeDGameplay)
            {
                switch (index)
                {
                    case 0:
                        options.Invert3DHorizontal = !options.Invert3DHorizontal;
                        break;
                    case 1:
                        options.Invert3DVertical = !options.Invert3DVertical;
                        break;
                    case 2:
                        options.AimAssist3DMode = options.AimAssist3DMode == AimAssist3DMode.Off
                            ? AimAssist3DMode.SoftLock
                            : AimAssist3DMode.Off;
                        break;
                    case 3:
                        optionsSection = OptionMenuSection.General;
                        optionsSelection = GetOptionRows().Length - 1;
                        optionsScrollOffset = GetOptionsMaxScroll();
                        break;
                }

                return;
            }
#endif
            switch (index)
            {
#if ANDROID
                case 0:
                    options.UiScalePercent = AdjustOptionPercent(options.UiScalePercent, delta, UiScaleOptions);
                    break;
                case 1:
                    options.VisualPreset = (VisualPreset)(((int)options.VisualPreset + 3 + delta) % 3);
                    break;
                case 2:
                    options.EnableBloom = !options.EnableBloom;
                    break;
                case 3:
                    options.EnableShockwaves = !options.EnableShockwaves;
                    break;
                case 4:
                    options.EnableNeonOutlines = !options.EnableNeonOutlines;
                    break;
                case 5:
                    options.ScreenShakeStrength = (ScreenShakeStrength)(((int)options.ScreenShakeStrength + 3 + delta) % 3);
                    break;
                case 6:
                    pendingAudioQualityPreset = CycleAudioQuality(delta, pendingAudioQualityPreset);
                    audioQualityDialogOpen = true;
                    break;
                case 7:
                    options.MasterVolume = AdjustVolume(options.MasterVolume, delta);
                    break;
                case 8:
                    options.MusicVolume = AdjustVolume(options.MusicVolume, delta);
                    break;
                case 9:
                    options.SfxVolume = AdjustVolume(options.SfxVolume, delta);
                    break;
                case 10:
                    options.TouchControlsOpacity = UiScaleHelper.ClampTouchControlsOpacity(options.TouchControlsOpacity + delta * 5);
                    break;
                case 11:
                    options.AutoUpgradeDraft = !options.AutoUpgradeDraft;
                    break;
                case 12:
                    options.ShowHelpHints = !options.ShowHelpHints;
                    break;
#else
                case 0:
                    options.DisplayMode = options.DisplayMode == DesktopDisplayMode.BorderlessFullscreen
                        ? DesktopDisplayMode.Windowed
                        : DesktopDisplayMode.BorderlessFullscreen;
                    Game1.Instance?.ApplyDisplayMode(options.DisplayMode);
                    break;
                case 1:
                    options.UiScalePercent = AdjustOptionPercent(options.UiScalePercent, delta, UiScaleOptions);
                    break;
                case 2:
                    options.VisualPreset = (VisualPreset)(((int)options.VisualPreset + 3 + delta) % 3);
                    break;
                case 3:
                    options.EnableBloom = !options.EnableBloom;
                    break;
                case 4:
                    options.EnableShockwaves = !options.EnableShockwaves;
                    break;
                case 5:
                    options.EnableNeonOutlines = !options.EnableNeonOutlines;
                    break;
                case 6:
                    options.ScreenShakeStrength = (ScreenShakeStrength)(((int)options.ScreenShakeStrength + 3 + delta) % 3);
                    break;
                case 7:
                    pendingAudioQualityPreset = CycleAudioQuality(delta, pendingAudioQualityPreset);
                    audioQualityDialogOpen = true;
                    break;
                case 8:
                    options.MasterVolume = AdjustVolume(options.MasterVolume, delta);
                    break;
                case 9:
                    options.MusicVolume = AdjustVolume(options.MusicVolume, delta);
                    break;
                case 10:
                    options.SfxVolume = AdjustVolume(options.SfxVolume, delta);
                    break;
                case 11:
                    options.AutoUpgradeDraft = !options.AutoUpgradeDraft;
                    break;
                case 12:
                    options.ShowHelpHints = !options.ShowHelpHints;
                    break;
                case 13:
                    optionsSection = OptionMenuSection.ThreeDGameplay;
                    optionsSelection = 0;
                    optionsScrollOffset = 0f;
                    break;
#endif
            }
        }

        private void ActivateOptionFromPointer(int index, Rectangle rowBounds, Vector2 pointer)
        {
            OptionRowDefinition[] rows = GetOptionRows();
            OptionRowControlKind controlKind = index >= 0 && index < rows.Length
                ? rows[index].ControlKind
                : OptionRowControlKind.Cycle;

            if (IsSliderOptionRow(index))
            {
                Rectangle trackBounds = GetOptionSliderTrackBounds(rowBounds);
                float ratio = (pointer.X - trackBounds.X) / Math.Max(1f, trackBounds.Width);
                SetSliderOptionFromRatio(index, ratio);
                return;
            }

            Rectangle leftBounds = GetOptionStepperLeftBounds(rowBounds);
            Rectangle rightBounds = GetOptionStepperRightBounds(rowBounds);
            if (leftBounds.Contains(pointer) && controlKind != OptionRowControlKind.Submenu && controlKind != OptionRowControlKind.Action)
            {
                AdjustOptionByStep(index, -1);
                return;
            }

            if (rightBounds.Contains(pointer))
            {
                AdjustOptionByStep(index, 1);
                return;
            }

            AdjustOptionByStep(index, 1);
        }

        private static AudioQualityPreset CycleAudioQuality(int delta, AudioQualityPreset current)
        {
            AudioQualityPreset[] options = GetAudioQualityOptions();
            int index = Array.IndexOf(options, current);
            if (index < 0)
                index = 0;

            index = (index + options.Length + delta) % options.Length;
            return options[index];
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

        private static int GetOptionRowControlId(int index)
        {
            return PointerControlOptionRowBase + index;
        }

        private static int GetOptionStepperLeftControlId(int index)
        {
            return PointerControlOptionStepperLeftBase + index;
        }

        private static int GetOptionStepperRightControlId(int index)
        {
            return PointerControlOptionStepperRightBase + index;
        }

        private static int GetOptionSliderControlId(int index)
        {
            return PointerControlOptionSliderBase + index;
        }

        private static int GetAudioDialogPresetControlId(int index)
        {
            return PointerControlAudioDialogPresetBase + index;
        }

        private static bool IsOptionRowControlId(int controlId)
        {
            return controlId >= PointerControlOptionRowBase && controlId < PointerControlOptionStepperLeftBase;
        }

        private static bool IsOptionStepperLeftControlId(int controlId)
        {
            return controlId >= PointerControlOptionStepperLeftBase && controlId < PointerControlOptionStepperRightBase;
        }

        private static bool IsOptionStepperRightControlId(int controlId)
        {
            return controlId >= PointerControlOptionStepperRightBase && controlId < PointerControlOptionSliderBase;
        }

        private static bool IsOptionSliderControlId(int controlId)
        {
            return controlId >= PointerControlOptionSliderBase && controlId < PointerControlAudioDialogPresetBase;
        }

        private static int DecodeIndexedControlId(int controlId, int baseControlId)
        {
            return controlId - baseControlId;
        }

        private void EnsureOptionSelectionVisible(Rectangle viewport, Rectangle[] optionBounds)
        {
            if (optionsSelection < 0 || optionsSelection >= optionBounds.Length)
                return;

            Rectangle selectedBounds = optionBounds[optionsSelection];
            int margin = UiPx(6);
            int visibleTop = viewport.Y + margin;
            int visibleBottom = viewport.Bottom - margin;

            if (selectedBounds.Y < visibleTop)
                optionsScrollOffset -= visibleTop - selectedBounds.Y;
            else if (selectedBounds.Bottom > visibleBottom)
                optionsScrollOffset += selectedBounds.Bottom - visibleBottom;

            ClampOptionsScroll();
        }

        private int ResolveHoveredOptionIndex(IReadOnlyList<Rectangle> optionBounds, Vector2 pointer)
        {
            for (int i = 0; i < optionBounds.Count; i++)
            {
                if (optionBounds[i].Contains(pointer))
                    return i;
            }

            return -1;
        }

        private int ResolveOptionPointerControlId(OptionRowDefinition[] rows, Rectangle viewport, Rectangle[] optionBounds, Rectangle discardBounds, Rectangle applyBounds, Vector2 pointer)
        {
            if (discardBounds.Contains(pointer))
                return PointerControlOptionsDiscard;

            if (applyBounds.Contains(pointer))
                return PointerControlOptionsApply;

            if (GetOptionsMaxScroll() > 0f)
            {
                Rectangle upBounds = GetOptionScrollbarButtonBounds(viewport, true);
                Rectangle downBounds = GetOptionScrollbarButtonBounds(viewport, false);
                Rectangle thumbBounds = GetOptionScrollbarThumbBounds(viewport);
                Rectangle trackBounds = GetOptionScrollbarTrackBounds(viewport);
                if (upBounds.Contains(pointer))
                    return PointerControlOptionsScrollbarUp;
                if (downBounds.Contains(pointer))
                    return PointerControlOptionsScrollbarDown;
                if (thumbBounds.Contains(pointer))
                    return PointerControlOptionsScrollbarThumb;
                if (trackBounds.Contains(pointer))
                    return PointerControlOptionsScrollbarTrack;
            }

            for (int i = 0; i < optionBounds.Length; i++)
            {
                Rectangle rowBounds = optionBounds[i];
                if (!rowBounds.Contains(pointer))
                    continue;

                if (rows[i].ControlKind == OptionRowControlKind.Slider)
                    return GetOptionSliderControlId(i);

                Rectangle leftBounds = GetOptionStepperLeftBounds(rowBounds);
                Rectangle rightBounds = GetOptionStepperRightBounds(rowBounds);
                if (leftBounds.Contains(pointer))
                    return GetOptionStepperLeftControlId(i);
                if (rightBounds.Contains(pointer))
                    return GetOptionStepperRightControlId(i);

                return GetOptionRowControlId(i);
            }

            if (viewport.Contains(pointer))
                return PointerControlOptionsViewport;

            return PointerControlNone;
        }

        private void ApplyOptionSliderPointerValue(int index, Rectangle[] optionBounds, Vector2 pointer)
        {
            if (index < 0 || index >= optionBounds.Length)
                return;

            Rectangle trackBounds = GetOptionSliderTrackBounds(optionBounds[index]);
            float ratio = (pointer.X - trackBounds.X) / Math.Max(1f, trackBounds.Width);
            SetSliderOptionFromRatio(index, ratio);
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

        private void DrawDifficultySelect(SpriteBatch spriteBatch, Texture2D pixel)
        {
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Black * 0.72f);

            Rectangle panel = GetDifficultySelectPanelBounds();
            Rectangle summaryBounds = GetDifficultySummaryBounds(panel);
            DrawPanel(spriteBatch, pixel, panel, Color.Black * 0.82f, Color.White * 0.18f);
            DrawCenteredText(spriteBatch, pixel, "SELECT DIFFICULTY", panel.Center.X, panel.Y + UiPx(22), Color.White, 2.35f);
            DrawCenteredText(
                spriteBatch,
                pixel,
                difficultySelectSkipTutorial ? "START WITHOUT TUTORIAL" : options.TutorialCompleted ? "START CAMPAIGN" : "NEW RUN STARTS WITH THE TUTORIAL PROLOGUE",
                panel.Center.X,
                panel.Y + UiPx(58),
                Color.White * 0.7f,
                1.05f);

            List<UiButton> buttons = GetDifficultyButtons();
            for (int i = 0; i < buttons.Count; i++)
                DrawButton(spriteBatch, pixel, buttons[i], i == difficultySelection);

            DrawPanel(spriteBatch, pixel, summaryBounds, Color.Black * 0.3f, Color.White * 0.14f);
            if (difficultySelection < DifficultyChoices.Length)
            {
                GameDifficulty difficulty = DifficultyChoices[difficultySelection];
                DrawCenteredText(spriteBatch, pixel, DifficultyTuning.GetLabel(difficulty), summaryBounds.Center.X, summaryBounds.Y + UiPx(18), Color.Orange, 1.42f);
                string wrappedSummary = BitmapFontRenderer.Wrap(GetDifficultySummaryText(difficulty), summaryBounds.Width - UiPx(34), 0.94f);
                BitmapFontRenderer.Draw(
                    spriteBatch,
                    pixel,
                    wrappedSummary,
                    new Vector2(summaryBounds.X + UiPx(18), summaryBounds.Y + UiPx(58)),
                    Color.White * 0.82f,
                    0.94f);
            }
            else
            {
                DrawCenteredText(spriteBatch, pixel, "RETURN TO TITLE", summaryBounds.Center.X, summaryBounds.Y + UiPx(18), Color.White, 1.28f);
                string wrappedCancel = BitmapFontRenderer.Wrap("CANCELS THE CURRENT START FLOW WITHOUT CHANGING YOUR LAST PICK.", summaryBounds.Width - UiPx(34), 0.9f);
                BitmapFontRenderer.Draw(spriteBatch, pixel, wrappedCancel, new Vector2(summaryBounds.X + UiPx(18), summaryBounds.Y + UiPx(60)), Color.White * 0.72f, 0.9f);
            }

#if ANDROID
            const string footerText = "TAP A DIFFICULTY TO BEGIN  ANDROID BACK RETURNS";
#else
            const string footerText = "ENTER STARTS  ESC RETURNS  LAST PICK STAYS PRESELECTED";
#endif
            float footerScale = ScaleTextToFit(footerText, panel.Width - UiPx(40), 0.96f, 0.7f);
            DrawCenteredText(spriteBatch, pixel, footerText, panel.Center.X, panel.Bottom - UiPx(42), Color.White * 0.7f, footerScale);
        }

        private string GetDifficultySummaryText(GameDifficulty difficulty)
        {
            DifficultyProfile profile = DifficultyTuning.GetProfile(difficulty);
            StageDefinition openingStage = repository.GetStage(1);
            int openingLives = Math.Max(1, (openingStage?.StartingLives ?? 3) + profile.LivesDelta);
            int openingShips = Math.Max(1, (openingStage?.ShipsPerLife ?? 2) + profile.ShipsDelta);
            string retries = string.Concat("OPENING RETRIES  ", openingLives.ToString(), " LIVES  /  ", openingShips.ToString(), " SHIPS");
            string waves = difficulty switch
            {
                GameDifficulty.Easy => "WAVES  CURRENT BASELINE PRESSURE AND GENEROUS CORE FLOW",
                GameDifficulty.Normal => "WAVES  MODERATE AGGRESSION RISE WITH LIGHTLY REDUCED DROPS",
                GameDifficulty.Hard => "WAVES  FASTER, TOUGHER, AND LESS GENEROUS FROM THE OPENING STAGES",
                GameDifficulty.Insane => "WAVES  HIGH THREAT WITH VERY LITTLE RECOVERY ROOM",
                _ => "WAVES  EXTREME PRESSURE WITH SLOWEST POWER GROWTH",
            };
            string bosses = difficulty switch
            {
                GameDifficulty.Easy => "BOSSES  KEEP A THREAT FLOOR ABOVE REGULAR WAVES",
                GameDifficulty.Normal => "BOSSES  HIT HARDER, FIRE FASTER, AND SCALE ABOVE PLAYER POWER",
                GameDifficulty.Hard => "BOSSES  OPEN WITH HEAVIER BARRAGES, FASTER SHOTS, AND TOUGHER PHASES",
                GameDifficulty.Insane => "BOSSES  BRUTAL OPENERS, DENSE SUPPORT BURSTS, AND LITTLE MERCY",
                _ => "BOSSES  ONE HIT COSTS THE CURRENT SHIP",
            };
            return string.Concat(retries, "\n", waves, "\n", bosses);
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
            List<PauseMenuEntry> entries = GetPauseEntries();
            for (int i = 0; i < entries.Count; i++)
                DrawButton(spriteBatch, pixel, entries[i].Button, i == pauseSelection);
            if (pauseReturnState == GameFlowState.Tutorial)
                DrawCenteredText(spriteBatch, pixel, "SKIP TUTORIAL STARTS THE REAL CAMPAIGN", Game1.ScreenSize.X / 2f, Game1.VirtualHeight - 92f, Color.Orange * 0.8f, 1.2f);
            else if (options.DeveloperToolsUnlocked)
                DrawCenteredText(spriteBatch, pixel, "DEV TOOLS ENABLED  PAGEUP/PAGEDOWN STAGE  F6 DETAIL  V VIEW", Game1.ScreenSize.X / 2f, Game1.VirtualHeight - 92f, Color.Cyan * 0.85f, 1.05f);
#if ANDROID
            DrawCenteredText(spriteBatch, pixel, "TAP A BUTTON OR TAP THE PAUSE CHIP AGAIN TO RETURN", Game1.ScreenSize.X / 2f, Game1.VirtualHeight - 62f, Color.White * 0.62f, 1.05f);
#else
            DrawCenteredText(spriteBatch, pixel, "ENTER CONFIRMS  ESC RETURNS", Game1.ScreenSize.X / 2f, Game1.VirtualHeight - 62f, Color.White * 0.62f, 1.05f);
#endif
        }

        private void DrawOptions(SpriteBatch spriteBatch, Texture2D pixel)
        {
            Rectangle frameBounds = GetOptionsFrameBounds();
            Rectangle viewport = GetOptionsListViewportBounds();
            Rectangle descriptionBounds = GetOptionsDescriptionBounds();
            Rectangle hintBounds = GetOptionsControlsHintBounds();
            if (slotReturnState == GameFlowState.Title)
                DrawBackdrop(spriteBatch, pixel, 0.8f, "CONFIGURE VISUALS AND DISPLAY");
            else
                spriteBatch.Draw(pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Black * 0.55f);

            spriteBatch.Draw(pixel, frameBounds, Color.Black * 0.78f);
            string headerText = optionsSection == OptionMenuSection.ThreeDGameplay ? "OPTIONS / 3D GAMEPLAY" : "OPTIONS";
            DrawCenteredText(spriteBatch, pixel, headerText, frameBounds.Center.X, frameBounds.Y + UiPx(22), Color.White, optionsSection == OptionMenuSection.ThreeDGameplay ? 2.35f : 3f);
            OptionRowDefinition[] rows = GetOptionRows();
            Rectangle[] rowBounds = GetOptionRowBounds(viewport, rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                Rectangle row = rowBounds[i];
                if (row.Bottom < viewport.Y || row.Y > viewport.Bottom)
                    continue;

                bool selected = i == optionsSelection;
                DrawPanel(spriteBatch, pixel, row, selected ? Color.White * 0.1f : Color.White * 0.04f, selected ? Color.Orange : Color.White * 0.16f);

                Rectangle labelBounds = new Rectangle(row.X + UiPx(14), row.Y + UiPx(6), row.Width - UiPx(360), row.Height - UiPx(12));
                Rectangle valueBounds = new Rectangle(row.Right - UiPx(280), row.Y + UiPx(6), UiPx(160), row.Height - UiPx(12));
                float labelScale = ScaleTextToFit(rows[i].Label, labelBounds.Width, 1.02f, 0.76f);
                float valueScale = ScaleTextToFit(rows[i].Value, valueBounds.Width, 0.98f, 0.72f);
                BitmapFontRenderer.Draw(spriteBatch, pixel, rows[i].Label, new Vector2(labelBounds.X, labelBounds.Y + UiPx(2)), Color.White, labelScale);

                if (IsSliderOptionRow(i))
                {
                    Rectangle trackBounds = GetOptionSliderTrackBounds(row);
                    spriteBatch.Draw(pixel, trackBounds, Color.White * 0.1f);
                    int fillWidth = (int)MathF.Round(trackBounds.Width * MathHelper.Clamp(GetSliderRatioForOption(i), 0f, 1f));
                    if (fillWidth > 0)
                        spriteBatch.Draw(pixel, new Rectangle(trackBounds.X, trackBounds.Y, fillWidth, trackBounds.Height), Color.Orange * 0.8f);

                    Rectangle knob = new Rectangle(trackBounds.X + Math.Max(0, fillWidth - UiPx(6)), trackBounds.Y - UiPx(3), UiPx(12), trackBounds.Height + UiPx(6));
                    spriteBatch.Draw(pixel, knob, Color.White);
                    if (!string.IsNullOrEmpty(rows[i].Value))
                        BitmapFontRenderer.Draw(spriteBatch, pixel, rows[i].Value, new Vector2(valueBounds.X, valueBounds.Y + UiPx(2)), Color.White * 0.9f, valueScale);
                }
                else
                {
                    Rectangle leftBounds = GetOptionStepperLeftBounds(row);
                    Rectangle rightBounds = GetOptionStepperRightBounds(row);
                    if (!string.IsNullOrEmpty(rows[i].Value))
                        BitmapFontRenderer.Draw(spriteBatch, pixel, rows[i].Value, new Vector2(valueBounds.X, valueBounds.Y + UiPx(2)), Color.White * 0.9f, valueScale);

                    string leftText = rows[i].ControlKind == OptionRowControlKind.Submenu || rows[i].ControlKind == OptionRowControlKind.Action ? string.Empty : "-";
                    string rightText = rows[i].ControlKind switch
                    {
                        OptionRowControlKind.Submenu => "OPEN",
                        OptionRowControlKind.Action => "GO",
                        _ => "+",
                    };

                    DrawPanel(spriteBatch, pixel, leftBounds, Color.Black * 0.18f, Color.White * 0.12f);
                    DrawPanel(spriteBatch, pixel, rightBounds, Color.Black * 0.18f, Color.White * 0.12f);
                    if (!string.IsNullOrEmpty(leftText))
                        DrawCenteredText(spriteBatch, pixel, leftText, leftBounds.Center.X, leftBounds.Y + UiPx(3), Color.White * 0.78f, 0.95f);
                    DrawCenteredText(spriteBatch, pixel, rightText, rightBounds.Center.X, rightBounds.Y + UiPx(3), Color.White * 0.78f, rightText.Length > 1 ? 0.7f : 0.95f);
                }
            }

            if (GetOptionsMaxScroll() > 0f)
            {
                Rectangle scrollbarBounds = GetOptionScrollbarBounds(viewport);
                Rectangle upBounds = GetOptionScrollbarButtonBounds(viewport, true);
                Rectangle downBounds = GetOptionScrollbarButtonBounds(viewport, false);
                Rectangle trackBounds = GetOptionScrollbarTrackBounds(viewport);
                Rectangle thumbBounds = GetOptionScrollbarThumbBounds(viewport);
                DrawPanel(spriteBatch, pixel, scrollbarBounds, Color.Black * 0.16f, Color.White * 0.12f);
                DrawPanel(spriteBatch, pixel, upBounds, Color.Black * 0.22f, Color.White * 0.18f);
                DrawPanel(spriteBatch, pixel, downBounds, Color.Black * 0.22f, Color.White * 0.18f);
                spriteBatch.Draw(pixel, trackBounds, Color.White * 0.08f);
                spriteBatch.Draw(pixel, thumbBounds, Color.White * 0.52f);
                DrawCenteredText(spriteBatch, pixel, "^", upBounds.Center.X, upBounds.Y + UiPx(2), Color.White * 0.8f, 0.9f);
                DrawCenteredText(spriteBatch, pixel, "v", downBounds.Center.X, downBounds.Y + UiPx(2), Color.White * 0.8f, 0.9f);
            }

            DrawPanel(spriteBatch, pixel, descriptionBounds, Color.Black * 0.24f, Color.White * 0.14f);
            string selectedDescription = rows.Length > 0 && optionsSelection >= 0 && optionsSelection < rows.Length
                ? rows[optionsSelection].Description
                : "ADJUST OPTIONS FOR THE CURRENT PLATFORM.";
            float descriptionScale = ScaleTextToFit(selectedDescription, descriptionBounds.Width - UiPx(18), 0.84f, 0.64f);
            BitmapFontRenderer.Draw(spriteBatch, pixel, selectedDescription, new Vector2(descriptionBounds.X + UiPx(10), descriptionBounds.Y + UiPx(8)), Color.White * 0.74f, descriptionScale);

            string hintText =
#if ANDROID
                "DRAG THE LIST OR SCROLLBAR. DRAG SLIDERS. RELEASE ON A CONTROL TO APPLY IT. ANDROID BACK DISCARDS.";
#else
                "ARROWS NAVIGATE. WHEEL OR DRAG SCROLLS. CLICK-RELEASE ACTIVATES. ENTER ADJUSTS THE SELECTED ROW.";
#endif
            float hintScale = ScaleTextToFit(hintText, hintBounds.Width, 0.76f, 0.58f);
            DrawCenteredText(spriteBatch, pixel, hintText, hintBounds.Center.X, hintBounds.Y + UiPx(2), Color.White * 0.62f, hintScale);

            (Rectangle discardBounds, Rectangle applyBounds) = GetOptionActionBounds();
            DrawPanel(spriteBatch, pixel, discardBounds, Color.Black * 0.22f, Color.Orange * 0.85f);
            DrawPanel(spriteBatch, pixel, applyBounds, Color.Black * 0.18f, Color.LimeGreen * 0.78f);
            DrawCenteredText(spriteBatch, pixel, "X DISCARD", discardBounds.Center.X, discardBounds.Y + UiPx(8), Color.White, 1.02f);
            DrawCenteredText(spriteBatch, pixel, "V APPLY", applyBounds.Center.X, applyBounds.Y + UiPx(8), Color.White, 1.02f);
#if ANDROID
            if (audioQualityDialogOpen)
                DrawAudioQualityDialog(spriteBatch, pixel);
            else if (audioQualityApplyPending)
                DrawAudioRebuildOverlay(spriteBatch, pixel);
#else
            if (audioQualityDialogOpen)
                DrawAudioQualityDialog(spriteBatch, pixel);
            else if (audioQualityApplyPending)
                DrawAudioRebuildOverlay(spriteBatch, pixel);
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
                    ? string.Concat("SLOT ", (i + 1).ToString(), "  ", DifficultyTuning.GetLabel(summary.Difficulty), "  STAGE ", summary.StageNumber.ToString("00"), "  SCORE ", summary.Score.ToString(), "  ", summary.ActiveStyle)
                    : string.Concat("SLOT ", (i + 1).ToString(), "  EMPTY");
                BitmapFontRenderer.Draw(spriteBatch, pixel, line, new Vector2(rowBounds.X + UiPx(16), rowBounds.Y + UiPx(10)), Color.White, 1.5f);
                if (summary.HasData && !string.IsNullOrWhiteSpace(summary.SavedAtUtc))
                    BitmapFontRenderer.Draw(spriteBatch, pixel, string.Concat(summary.StageName?.ToUpperInvariant() ?? string.Empty, "  ", summary.SavedAtUtc), new Vector2(rowBounds.X + UiPx(16), rowBounds.Y + UiPx(38)), Color.White * 0.65f, 1.1f);
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

            Rectangle panelBounds = new Rectangle(90, 90, Game1.VirtualWidth - 180, Game1.VirtualHeight - 180);
            spriteBatch.Draw(pixel, panelBounds, Color.Black * 0.75f);
            DrawCenteredText(spriteBatch, pixel, "HELP", Game1.ScreenSize.X / 2f, 112f, Color.White, 3f);
            DrawCenteredText(spriteBatch, pixel, string.Concat("PAGE ", (helpPageIndex + 1).ToString(), " / ", HelpPageCount.ToString()), Game1.ScreenSize.X / 2f, 152f, Color.White * 0.7f, 1.5f);
            (Rectangle leftBounds, Rectangle rightBounds, Rectangle tutorialBounds) = GetHelpActionBounds();

            switch (helpPageIndex)
            {
                case 0:
#if ANDROID
                    DrawHelpPage(spriteBatch, pixel, "CONTROLS\nLEFT PAD MOVE  RIGHT PAD AIM AND FIRE\nTOP WEAPON HUD SWAPS STYLE  STAGE HEADER PAUSES\nHOLD THE TOP RIGHT REWIND BUTTON TO REWIND\nUSE THE ARROWS BELOW TO BROWSE HELP", 186f);
#else
                    DrawHelpPage(spriteBatch, pixel, "CONTROLS\nWASD MOVE  ARROWS AIM  SPACE FIRE\nQ E SWITCH STYLE  R REWIND  ESC PAUSE  F1 HELP\nA S D PICK DRAFT CARDS  ENTER REPLAYS TUTORIAL HERE", 186f);
#endif
                    break;
                case 1:
                    DrawHelpPage(spriteBatch, pixel, "POWER CORES\nEACH P CORE MATCHES A WEAPON STYLE\nMATCH YOUR ACTIVE STYLE TO BOOST IT IMMEDIATELY UP TO LEVEL 3\nOTHER CORES STORE STYLE SPECIFIC CHARGES FOR TRANSITION DRAFTS\nAUTO DRAFT RANDOMIZES AT ZERO  A S D PICK LEFT MID RIGHT", 186f);
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
                    DrawHelpPage(spriteBatch, pixel, "FX AND REWIND\nHOLD R TO REWIND 8 SECONDS OF GAMEPLAY\nREWIND STARTS SLOW AND ACCELERATES THE LONGER YOU HOLD\nSTAGE 40 AND BEYOND CAN TOGGLE CHASE VIEW WITH V\nLOW STANDARD AND NEON VISUAL PRESETS ARE IN OPTIONS", 186f);
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
            DrawPanel(spriteBatch, pixel, leftBounds, Color.Black * 0.22f, Color.White * 0.32f);
            DrawPanel(spriteBatch, pixel, rightBounds, Color.Black * 0.22f, Color.White * 0.32f);
            DrawCenteredText(spriteBatch, pixel, "<", leftBounds.Center.X, leftBounds.Y + UiPx(4), Color.White, 1.2f);
            DrawCenteredText(spriteBatch, pixel, ">", rightBounds.Center.X, rightBounds.Y + UiPx(4), Color.White, 1.2f);
            if (helpPageIndex == 0)
            {
                DrawPanel(spriteBatch, pixel, tutorialBounds, Color.Black * 0.24f, Color.Orange * 0.8f);
                DrawCenteredText(spriteBatch, pixel, "START TUTORIAL", tutorialBounds.Center.X, tutorialBounds.Y + UiPx(8), Color.White, 1.08f);
            }

            DrawCenteredText(spriteBatch, pixel, "TAP ARROWS TO CHANGE PAGE  USE ANDROID BACK TO CLOSE", Game1.ScreenSize.X / 2f, Game1.VirtualHeight - 120f, Color.White * 0.75f, 1.05f);
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

        private void DrawAudioQualityDialog(SpriteBatch spriteBatch, Texture2D pixel)
        {
            (Rectangle dialogBounds, Rectangle[] presetBounds, Rectangle cancelBounds, Rectangle applyBounds) = GetAudioQualityDialogBounds();
            AudioQualityPreset[] presets = GetAudioQualityOptions();
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Black * 0.58f);
            DrawPanel(spriteBatch, pixel, dialogBounds, Color.Black * 0.85f, Color.White * 0.18f);
            DrawCenteredText(spriteBatch, pixel, "AUDIO QUALITY", dialogBounds.Center.X, dialogBounds.Y + UiPx(20), Color.White, 1.8f);
            DrawCenteredText(spriteBatch, pixel, "REBUILDS PROCEDURAL AUDIO. APPLY MAY PAUSE BRIEFLY ON ANDROID.", dialogBounds.Center.X, dialogBounds.Y + UiPx(54), Color.White * 0.68f, 0.95f);

            for (int i = 0; i < presetBounds.Length; i++)
            {
                AudioQualityPreset preset = presets[i];
                bool selected = preset == pendingAudioQualityPreset;
                DrawPanel(spriteBatch, pixel, presetBounds[i], selected ? Color.White * 0.12f : Color.Black * 0.28f, selected ? Color.Orange : Color.White * 0.18f);
                DrawCenteredText(spriteBatch, pixel, preset.ToString().ToUpperInvariant(), presetBounds[i].Center.X, presetBounds[i].Y + UiPx(12), Color.White, 1.04f);
                string detail = preset switch
                {
                    AudioQualityPreset.Reduced => "LOWER CPU",
                    AudioQualityPreset.Standard => "BALANCED MIX",
                    _ => "FULL VOICES",
                };
                DrawCenteredText(spriteBatch, pixel, detail, presetBounds[i].Center.X, presetBounds[i].Y + UiPx(40), Color.White * 0.62f, 0.86f);
            }

            DrawPanel(spriteBatch, pixel, cancelBounds, Color.Black * 0.24f, Color.Orange * 0.78f);
            DrawPanel(spriteBatch, pixel, applyBounds, Color.Black * 0.18f, Color.LimeGreen * 0.78f);
            DrawCenteredText(spriteBatch, pixel, "X CANCEL", cancelBounds.Center.X, cancelBounds.Y + UiPx(8), Color.White, 1f);
            DrawCenteredText(spriteBatch, pixel, "V APPLY", applyBounds.Center.X, applyBounds.Y + UiPx(8), Color.White, 1f);
        }

        private void DrawAudioRebuildOverlay(SpriteBatch spriteBatch, Texture2D pixel)
        {
            Rectangle bounds = new Rectangle(Game1.VirtualWidth / 2 - UiPx(210), Game1.VirtualHeight / 2 - UiPx(54), UiPx(420), UiPx(108));
            DrawPanel(spriteBatch, pixel, bounds, Color.Black * 0.82f, Color.Cyan * 0.45f);
            DrawCenteredText(spriteBatch, pixel, "REPROCESSING AUDIO", bounds.Center.X, bounds.Y + UiPx(18), Color.White, 1.5f);
            DrawCenteredText(spriteBatch, pixel, "PLEASE WAIT", bounds.Center.X, bounds.Y + UiPx(52), Color.White * 0.72f, 1f);
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
            BitmapFontRenderer.Draw(spriteBatch, pixel, string.Concat("HULL ", Math.Max(0f, Player1.Instance.HullRatio * 100f).ToString("0"), "%"), new Vector2(livesBounds.X + 12f, livesBounds.Bottom - 22f), Color.White * 0.55f, 1.15f);

            DrawStyleHud(spriteBatch, pixel, activeBounds);
            DrawOwnedStyleHud(spriteBatch, pixel, ownedBounds, inventory);
#if ANDROID
            BitmapFontRenderer.Draw(spriteBatch, pixel, "TAP WEAPON HUD TO SWAP", new Vector2(ownedBounds.X + 12f, ownedBounds.Y + 24f), Color.White * 0.4f, 0.86f);
#endif

            DrawCenteredText(spriteBatch, pixel, stageLabel, stageBounds.Center.X, stageBounds.Y + 14f, Color.White, GetFittedScale(stageLabel, stageBounds.Width - 20f, 1.7f, 1.15f));
            string stageSubLabel = state == GameFlowState.StageTransition
                ? (transitionToBoss ? "THREAT LOCK" : "FTL TRANSIT")
                : (!string.IsNullOrEmpty(activeEventWarning) ? activeEventWarning : (state == GameFlowState.Tutorial ? tutorialStep.ToString().ToUpperInvariant() : currentStage?.Name?.ToUpperInvariant() ?? "RUN"));
            DrawCenteredText(spriteBatch, pixel, stageSubLabel, stageBounds.Center.X, stageBounds.Y + 46f, !string.IsNullOrEmpty(activeEventWarning) ? Color.Orange : Color.White * 0.7f, GetFittedScale(stageSubLabel, stageBounds.Width - 20f, 1.08f, 0.82f));
            string presentationLabel = string.Concat(
                DifficultyTuning.GetLabel(PlayerStatus.RunProgress.Difficulty), "  ",
                presentationTier.ToString().ToUpperInvariant(),
                CanUseChaseView()
                    ? (viewMode == ViewMode.Chase3D ? "  V SIDE" : "  V CHASE")
                    : string.Empty);
            DrawCenteredText(spriteBatch, pixel, presentationLabel, stageBounds.Center.X, stageBounds.Bottom - 18f, Color.White * 0.55f, GetFittedScale(presentationLabel, stageBounds.Width - 20f, 0.8f, 0.62f));
#if ANDROID
            Rectangle pauseChip = HudLayoutCalculator.GetAndroidPauseChipBounds(layout);
            DrawPanel(spriteBatch, pixel, pauseChip, Color.Black * 0.3f, Color.Orange * 0.42f);
            DrawCenteredText(spriteBatch, pixel, "||", pauseChip.Center.X, pauseChip.Y + 3f, Color.White, 1.2f);
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

            int activeIndex = 0;
            for (int i = 0; i < ownedStyles.Count; i++)
            {
                if (ownedStyles[i] == inventory.ActiveStyle)
                {
                    activeIndex = i;
                    break;
                }
            }
#if ANDROID
            int maxVisible = 5;
            int visibleCount = Math.Min(maxVisible, ownedStyles.Count);
            int startIndex = Math.Clamp(activeIndex - visibleCount / 2, 0, Math.Max(0, ownedStyles.Count - visibleCount));
            float spacing = 46f;
            float scale = 2.1f;
#else
            int visibleCount = ownedStyles.Count;
            int startIndex = 0;
            float spacing = ownedStyles.Count <= 1 ? 0f : MathF.Min(34f, (bounds.Width - 24f) / Math.Max(1f, ownedStyles.Count - 1));
            float scale = ownedStyles.Count >= 8 ? 1.45f : 1.7f;
#endif
            float totalWidth = (visibleCount - 1) * spacing;
            float startX = bounds.X + 12f + Math.Max(0f, ((bounds.Width - 24f) - totalWidth) * 0.5f);
            for (int i = 0; i < visibleCount; i++)
            {
                WeaponStyleId styleId = ownedStyles[startIndex + i];
                WeaponStyleDefinition style = WeaponCatalog.GetStyle(styleId);
                float x = startX + i * spacing;
                Color iconAccent = ColorUtil.ParseHex(style.AccentColor, Color.Orange);
                Vector2 iconPosition = new Vector2(x, bounds.Y + 58f);
                PixelArtRenderer.DrawRows(spriteBatch, pixel, style.IconRows, iconPosition, scale, ColorUtil.ParseHex(style.PrimaryColor, Color.White), ColorUtil.ParseHex(style.SecondaryColor, Color.LightBlue), iconAccent, true);
                if (styleId == inventory.ActiveStyle)
                    spriteBatch.Draw(pixel, new Rectangle((int)x - 10, bounds.Bottom - 10, 22, 3), iconAccent);

                int stored = inventory.GetStoredCharge(styleId);
                if (stored > 0)
                {
                    Rectangle badgeBounds = new Rectangle((int)x + 6, bounds.Y + 20, stored >= 10 ? 26 : 18, 14);
                    DrawPanel(spriteBatch, pixel, badgeBounds, iconAccent * 0.18f, iconAccent * 0.72f);
                    BitmapFontRenderer.Draw(spriteBatch, pixel, stored.ToString(), new Vector2(badgeBounds.X + 4f, badgeBounds.Y + 2f), Color.White, 0.82f);
                }
            }

#if ANDROID
            if (startIndex > 0)
                BitmapFontRenderer.Draw(spriteBatch, pixel, "<", new Vector2(bounds.X + 12f, bounds.Y + 46f), Color.White * 0.55f, 1.05f);
            if (startIndex + visibleCount < ownedStyles.Count)
                BitmapFontRenderer.Draw(spriteBatch, pixel, ">", new Vector2(bounds.Right - 24f, bounds.Y + 46f), Color.White * 0.55f, 1.05f);
#endif
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
                    ? ActiveStageBossDefinition?.DisplayName?.ToUpperInvariant() ?? "THREAT LOCK"
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
            BitmapFontRenderer.Draw(spriteBatch, pixel, "WEAPON HUD SWAPS  STAGE HEADER OPENS SKIP MENU", new Vector2(panel.X + 16f, panel.Bottom - 24f), Color.White * 0.6f, 0.87f);
#else
            BitmapFontRenderer.Draw(spriteBatch, pixel, "ESC PAUSE  F1 HELP", new Vector2(panel.X + 16f, panel.Bottom - 24f), Color.White * 0.6f, 0.95f);
#endif
        }

        private void DrawUpgradeDraft(SpriteBatch spriteBatch, Texture2D pixel)
        {
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Black * 0.58f);

            DrawCenteredText(spriteBatch, pixel, "UPGRADE DRAFT", Game1.ScreenSize.X / 2f, 108f, Color.White, 2.6f);
            DrawCenteredText(spriteBatch, pixel, options.AutoUpgradeDraft ? string.Concat("AUTO PICKS RANDOMLY IN ", Math.Max(0f, draftTimer).ToString("0.0"), "s") : string.Concat("CHOOSE IN ", Math.Max(0f, draftTimer).ToString("0.0"), "s"), Game1.ScreenSize.X / 2f, 146f, Color.White * 0.72f, 1.18f);

            int cardWidth = 300;
            int cardHeight = 246;
            int gap = 24;
            int totalWidth = cardWidth * 3 + gap * 2;
            int startX = (Game1.VirtualWidth - totalWidth) / 2;
            int y = 198;

            for (int i = 0; i < draftCards.Count; i++)
            {
                UpgradeDraftCard card = draftCards[i];
                Rectangle bounds = new Rectangle(startX + i * (cardWidth + gap), y, cardWidth, cardHeight);
                Color accent = ColorUtil.ParseHex(card.AccentColor, Color.Orange);
                DrawPanel(spriteBatch, pixel, bounds, i == draftSelection ? accent * 0.16f : Color.Black * 0.45f, i == draftSelection ? accent : Color.White * 0.2f);
                spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 10), accent * 0.82f);
                if (!string.IsNullOrWhiteSpace(card.HotkeyLabel))
                {
                    Rectangle hotkeyBounds = new Rectangle(bounds.Right - 54, bounds.Y + 14, 34, 20);
                    DrawPanel(spriteBatch, pixel, hotkeyBounds, accent * 0.16f, accent * 0.8f);
                    DrawCenteredText(spriteBatch, pixel, card.HotkeyLabel, hotkeyBounds.Center.X, hotkeyBounds.Y + 2f, Color.White, 0.96f);
                }

                BitmapFontRenderer.Draw(spriteBatch, pixel, card.Title, new Vector2(bounds.X + 18f, bounds.Y + 22f), Color.White, 1.38f);
                BitmapFontRenderer.Draw(spriteBatch, pixel, card.Subtitle, new Vector2(bounds.X + 18f, bounds.Y + 52f), accent * 0.92f, 0.95f);
                if (!string.IsNullOrWhiteSpace(card.DeltaText))
                    BitmapFontRenderer.Draw(spriteBatch, pixel, card.DeltaText, new Vector2(bounds.X + 18f, bounds.Y + 78f), Color.White, 1.06f);
                BitmapFontRenderer.Draw(spriteBatch, pixel, card.Description, new Vector2(bounds.X + 18f, bounds.Y + 106f), Color.White * 0.82f, 0.94f);

                if (card.Type == UpgradeCardType.WeaponSurge)
                {
                    WeaponStyleDefinition style = WeaponCatalog.GetStyle(card.StyleId);
                    PixelArtRenderer.DrawRows(spriteBatch, pixel, style.IconRows, new Vector2(bounds.Center.X, bounds.Bottom - 64f), 6f, ColorUtil.ParseHex(style.PrimaryColor, Color.White), ColorUtil.ParseHex(style.SecondaryColor, Color.LightBlue), accent, true);
                    BitmapFontRenderer.DrawCentered(spriteBatch, pixel, card.PreviewText, new Vector2(bounds.Center.X, bounds.Bottom - 30f), Color.White * 0.8f, 0.95f);
                }
                else
                {
                    Rectangle previewPanel = new Rectangle(bounds.X + 18, bounds.Bottom - 72, bounds.Width - 36, 38);
                    DrawPanel(spriteBatch, pixel, previewPanel, accent * 0.15f, accent * 0.55f);
                    DrawCenteredText(spriteBatch, pixel, card.PreviewText, previewPanel.Center.X, previewPanel.Y + 6f, Color.White, 1.08f);
                    BitmapFontRenderer.DrawCentered(spriteBatch, pixel, card.BadgeText, new Vector2(previewPanel.Center.X, previewPanel.Bottom - 12f), Color.White * 0.6f, 0.78f);
                }
            }

            DrawCenteredText(spriteBatch, pixel, string.Concat("STORED CHARGES ", PlayerStatus.RunProgress.StoredUpgradeCharges.ToString()), Game1.ScreenSize.X / 2f, 476f, Color.White * 0.75f, 1.15f);
#if ANDROID
            DrawCenteredText(spriteBatch, pixel, "TAP A CARD TO PICK IT  AUTO DRAFT ROLLS RANDOMLY AT 0", Game1.ScreenSize.X / 2f, 520f, Color.White * 0.62f, 1.05f);
#else
            DrawCenteredText(spriteBatch, pixel, "A / S / D PICK LEFT / MID / RIGHT   ARROWS + ENTER STILL WORK", Game1.ScreenSize.X / 2f, 520f, Color.White * 0.62f, 1.05f);
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
                    body =
#if ANDROID
                        "PICK A CARD. TRANSITIONS SPEND STORED CHARGES THIS WAY BETWEEN STAGES.";
#else
                        "PICK A CARD. PRESS A / S / D TO TAKE LEFT / MID / RIGHT INSTANTLY.";
#endif
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

        private static float ScaleTextToFit(string text, float maxWidth, float preferredScale, float minimumScale)
        {
            if (string.IsNullOrWhiteSpace(text))
                return preferredScale;

            float measuredWidth = BitmapFontRenderer.Measure(text, preferredScale).X;
            if (measuredWidth <= maxWidth || measuredWidth <= 0f)
                return preferredScale;

            float ratio = maxWidth / measuredWidth;
            return MathHelper.Clamp(preferredScale * ratio, minimumScale, preferredScale);
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
                Difficulty = PlayerStatus.RunProgress.Difficulty,
                ViewMode = viewMode,
                PresentationTier = presentationTier,
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
                ActiveBossDefinition = CaptureBossDefinition(ActiveStageBossDefinition),
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
                ReentryTickets = CaptureReentryTickets(),
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
                    Difficulty = PlayerStatus.RunProgress.Difficulty,
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

            ViewMode preservedViewMode = viewMode;
            Vector2 preservedChaseReticle = Player1.Instance?.ChaseReticle ?? Vector2.Zero;
            currentStageNumber = Math.Max(1, save.CurrentStageNumber);
            currentStage = repository.GetStage(currentStageNumber);
            presentationTier = save.PresentationTier;
            viewMode = fromRewind ? preservedViewMode : save.ViewMode;
            resolvedBossDefinition = RestoreBossDefinition(save.ActiveBossDefinition, currentStage?.Boss) ?? ResolveBossDefinitionForStage(currentStageNumber, currentStage);
            selectedBossVariantId = save.ActiveBossDefinition?.VariantId ?? string.Empty;
            if (!CanUseChaseView())
                viewMode = ViewMode.SideScroller;
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
            options.LastSelectedDifficulty = save.Difficulty;
            if (!fromRewind)
                PersistentStorage.SaveOptions(options);
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
                        CombatSpawnPoint = snapshot.CombatSpawnPoint == null
                            ? new Vector3(snapshot.SpawnPoint.X, snapshot.SpawnPoint.Y, snapshot.DepthAnchor)
                            : new Vector3(snapshot.CombatSpawnPoint.X, snapshot.CombatSpawnPoint.Y, snapshot.CombatSpawnPoint.Z),
                        TargetY = snapshot.TargetY,
                        MovePattern = snapshot.MovePattern,
                        FirePattern = snapshot.FirePattern,
                        Amplitude = snapshot.Amplitude,
                        Frequency = snapshot.Frequency,
                        DepthAnchor = snapshot.DepthAnchor,
                        DepthAmplitude = snapshot.DepthAmplitude,
                        DepthFrequency = snapshot.DepthFrequency <= 0f ? 1f : snapshot.DepthFrequency,
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

            reentryTickets.Clear();
            if (save.ReentryTickets != null)
            {
                for (int i = 0; i < save.ReentryTickets.Count; i++)
                {
                    ReentryTicketSnapshotData snapshot = save.ReentryTickets[i];
                    if (snapshot?.Enemy == null || string.IsNullOrWhiteSpace(snapshot.Enemy.ArchetypeId))
                        continue;

                    reentryTickets.Add(new ReentryTicket
                    {
                        TriggerAtSeconds = snapshot.TriggerAtSeconds,
                        SpawnPoint = new Vector2(snapshot.SpawnPoint.X, snapshot.SpawnPoint.Y),
                        CombatSpawnPoint = snapshot.CombatSpawnPoint == null
                            ? new Vector3(snapshot.SpawnPoint.X, snapshot.SpawnPoint.Y, snapshot.DepthAnchor)
                            : new Vector3(snapshot.CombatSpawnPoint.X, snapshot.CombatSpawnPoint.Y, snapshot.CombatSpawnPoint.Z),
                        TargetY = snapshot.TargetY,
                        SpeedMultiplier = snapshot.SpeedMultiplier,
                        Amplitude = snapshot.Amplitude,
                        Frequency = snapshot.Frequency,
                        DepthAnchor = snapshot.DepthAnchor,
                        DepthAmplitude = snapshot.DepthAmplitude,
                        DepthFrequency = snapshot.DepthFrequency <= 0f ? 1f : snapshot.DepthFrequency,
                        Enemy = snapshot.Enemy,
                    });
                }
            }

            EntityManager.Reset();
            Player1.Instance.RestoreSnapshot(save.Player);
            EntityManager.Add(Player1.Instance);
            EntityManager.RestoreEnemies(save.Enemies, repository, ActiveStageBossDefinition);
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
                if (viewMode == ViewMode.Chase3D)
                    Player1.Instance.PreserveChaseViewState(preservedChaseReticle);
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
                    CombatSpawnPoint = new Vector3Data(spawn.CombatSpawnPoint.X, spawn.CombatSpawnPoint.Y, spawn.CombatSpawnPoint.Z),
                    TargetY = spawn.TargetY,
                    MovePattern = spawn.MovePattern,
                    FirePattern = spawn.FirePattern,
                    Amplitude = spawn.Amplitude,
                    Frequency = spawn.Frequency,
                    DepthAnchor = spawn.DepthAnchor,
                    DepthAmplitude = spawn.DepthAmplitude,
                    DepthFrequency = spawn.DepthFrequency,
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

        private List<ReentryTicketSnapshotData> CaptureReentryTickets()
        {
            var snapshots = new List<ReentryTicketSnapshotData>(reentryTickets.Count);
            for (int i = 0; i < reentryTickets.Count; i++)
            {
                ReentryTicket ticket = reentryTickets[i];
                snapshots.Add(new ReentryTicketSnapshotData
                {
                    TriggerAtSeconds = ticket.TriggerAtSeconds,
                    SpawnPoint = new Vector2Data(ticket.SpawnPoint.X, ticket.SpawnPoint.Y),
                    CombatSpawnPoint = new Vector3Data(ticket.CombatSpawnPoint.X, ticket.CombatSpawnPoint.Y, ticket.CombatSpawnPoint.Z),
                    TargetY = ticket.TargetY,
                    SpeedMultiplier = ticket.SpeedMultiplier,
                    Amplitude = ticket.Amplitude,
                    Frequency = ticket.Frequency,
                    DepthAnchor = ticket.DepthAnchor,
                    DepthAmplitude = ticket.DepthAmplitude,
                    DepthFrequency = ticket.DepthFrequency,
                    Enemy = ticket.Enemy,
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
            public Vector3 CombatSpawnPoint { get; set; }
            public float TargetY { get; set; }
            public MovePattern MovePattern { get; set; }
            public FirePattern FirePattern { get; set; }
            public float Amplitude { get; set; }
            public float Frequency { get; set; }
            public float DepthAnchor { get; set; }
            public float DepthAmplitude { get; set; }
            public float DepthFrequency { get; set; } = 1f;
            public float SpeedMultiplier { get; set; } = 1f;
        }

        private sealed class ScheduledEvent
        {
            public float TriggerAtSeconds { get; set; }
            public RandomEventWindowDefinition Window { get; set; }
        }

        private sealed class ReentryTicket
        {
            public float TriggerAtSeconds { get; set; }
            public Vector2 SpawnPoint { get; set; }
            public Vector3 CombatSpawnPoint { get; set; }
            public float TargetY { get; set; }
            public float SpeedMultiplier { get; set; } = 1f;
            public float Amplitude { get; set; }
            public float Frequency { get; set; } = 1f;
            public float DepthAnchor { get; set; }
            public float DepthAmplitude { get; set; }
            public float DepthFrequency { get; set; } = 1f;
            public EnemySnapshotData Enemy { get; set; }
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
