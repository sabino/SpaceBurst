using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpaceBurst.RuntimeData;
using System;
using System.Collections.Generic;
using System.IO;

namespace SpaceBurst
{
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        private const int VirtualBaseWidth = 1280;
        private const int VirtualBaseHeight = 720;
        private const float MinimumBootVisibleSeconds = 1.35f;
        private const float ReadyBootHandoffSeconds = 0.55f;

        public static Game1 Instance { get; private set; }
        public static Texture2D UiPixel { get { return Instance != null ? Instance.uiPixel : null; } }
        public static Texture2D RadialTexture { get { return Instance != null ? Instance.radialTexture : null; } }
        public static int VirtualWidth { get { return Instance != null ? Instance.virtualWidth : VirtualBaseWidth; } }
        public static int VirtualHeight { get { return Instance != null ? Instance.virtualHeight : VirtualBaseHeight; } }
        public static Viewport Viewport { get { return new Viewport(0, 0, VirtualWidth, VirtualHeight); } }
        public static Vector2 ScreenSize { get { return new Vector2(VirtualWidth, VirtualHeight); } }
        public static Rectangle RenderBounds { get { return Instance != null ? Instance.worldRenderViewport : new Rectangle(0, 0, VirtualWidth, VirtualHeight); } }
        public static Rectangle UiRenderBounds { get { return Instance != null ? Instance.uiRenderViewport : new Rectangle(0, 0, VirtualWidth, VirtualHeight); } }
        public static Rectangle SafeUiBounds { get { return Instance != null ? Instance.safeUiBounds : new Rectangle(0, 0, VirtualWidth, VirtualHeight); } }
        public static GameTime GameTime { get; private set; }

        private readonly GraphicsDeviceManager graphics;
        private readonly OptionsData startupOptions;
        private SpriteBatch spriteBatch;
        private Rectangle worldRenderViewport;
        private Rectangle uiRenderViewport;
        private Rectangle safeUiBounds;
        private Matrix worldScaleMatrix;
        private Matrix uiScaleMatrix;
        private CampaignDirector campaignDirector;
        private AudioDirector audioDirector;
        private FeedbackDirector feedbackDirector;
        private CameraRig cameraRig;
        private ConsoleState developerConsole;
        private Texture2D uiPixel;
        private Texture2D radialTexture;
        private RenderTarget2D worldRenderTarget;
        private int virtualWidth = VirtualBaseWidth;
        private int virtualHeight = VirtualBaseHeight;
        private readonly string capturePath;
        private readonly string captureMode;
        private bool capturePrepared;
        private bool captureCompleted;
        private float captureDelaySeconds = 1.2f;
        private BootPhase bootPhase;
        private bool bootReady;
        private bool bootComplete;
        private bool bootExploded;
        private bool bootHasDrawn;
        private float bootVisibleSeconds;
        private float bootReadySeconds;
        private string bootStatus = "STARTING";
        private readonly List<BootPixel> bootPixels = new List<BootPixel>();
        private readonly Random bootVisualRandom = new Random(unchecked(Environment.TickCount * 7919));
        private Color bootBackgroundColor = Color.Black;
        private Color bootGlowColorA = Color.CornflowerBlue;
        private Color bootGlowColorB = Color.Orange;
        private Color bootPrimaryColor = Color.White;
        private Color bootSecondaryColor = Color.LightBlue;
        private Color bootPromptColor = Color.White;

#if ANDROID || BLAZORGL
        private Texture2D touchControlTexture;
#endif

        private enum BootPhase
        {
            PrepareElements,
            LoadCampaignRepository,
            InitializeCameraFeedback,
            InitializeAudio,
            FinalizeDirector,
            Complete,
        }

        private struct BootPixel
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public Vector2 Target;
            public Color Color;
        }

        public float CurrentScrollSpeed
        {
            get { return campaignDirector != null ? campaignDirector.CurrentScrollSpeed : 0f; }
        }

        public string CurrentTheme
        {
            get { return campaignDirector != null ? campaignDirector.CurrentTheme : "Nebula"; }
        }

        public int CurrentBackgroundSeed
        {
            get { return campaignDirector != null ? campaignDirector.CurrentBackgroundSeed : 1; }
        }

        public BackgroundMoodDefinition CurrentBackgroundMood
        {
            get { return campaignDirector != null ? campaignDirector.CurrentBackgroundMood : new BackgroundMoodDefinition(); }
        }

        public float CurrentDifficultyFactor
        {
            get { return campaignDirector != null ? campaignDirector.CurrentDifficultyFactor : 0f; }
        }

        public RandomEventType ActiveEventType
        {
            get { return campaignDirector != null ? campaignDirector.ActiveEventType : RandomEventType.None; }
        }

        public float ActiveEventIntensity
        {
            get { return campaignDirector != null ? campaignDirector.ActiveEventIntensity : 0f; }
        }

        public float CurrentPowerDropBonusChance
        {
            get { return campaignDirector != null ? campaignDirector.CurrentPowerDropBonusChance : 0f; }
        }

        internal CampaignDirector CampaignDirector
        {
            get { return campaignDirector; }
        }

        public DeterministicRngState GameplayRandom
        {
            get { return campaignDirector != null ? campaignDirector.GameplayRandom : null; }
        }

        public float PowerupMagnetStrength
        {
            get { return campaignDirector != null ? campaignDirector.PowerupMagnetStrength : 0f; }
        }

        public bool EnableBloom
        {
            get { return campaignDirector != null && campaignDirector.EnableBloom; }
        }

        public bool EnableShockwaves
        {
            get { return campaignDirector != null && campaignDirector.EnableShockwaves; }
        }

        public bool EnableNeonOutlines
        {
            get { return campaignDirector != null && campaignDirector.EnableNeonOutlines; }
        }

        private FontTheme CurrentFontTheme
        {
            get { return FontTheme.Compact; }
        }

        internal float UiLayoutScale
        {
            get
            {
                int percent = campaignDirector != null ? campaignDirector.UiScalePercent : startupOptions.UiScalePercent;
                return UiScaleHelper.GetUiLayoutMultiplier(percent);
            }
        }

        private float UiTextScale
        {
            get
            {
                int percent = campaignDirector != null ? campaignDirector.UiScalePercent : startupOptions.UiScalePercent;
                return UiScaleHelper.GetUiTextMultiplier(percent);
            }
        }

        public VisualPreset VisualPreset
        {
            get { return campaignDirector != null ? campaignDirector.VisualPreset : VisualPreset.Standard; }
        }

        internal PresentationTier CurrentPresentationTier
        {
            get { return campaignDirector != null ? campaignDirector.CurrentPresentationTier : PresentationTier.Pixel2D; }
        }

        internal ViewMode CurrentViewMode
        {
            get { return campaignDirector != null ? campaignDirector.CurrentViewMode : ViewMode.SideScroller; }
        }

        internal float TouchControlsOpacity
        {
            get
            {
                int percent = campaignDirector != null ? campaignDirector.TouchControlsOpacity : startupOptions.TouchControlsOpacity;
                return UiScaleHelper.ClampTouchControlsOpacity(percent) / 100f;
            }
        }

        internal ScreenShakeStrength ScreenShakeStrength
        {
            get { return campaignDirector != null ? campaignDirector.ScreenShakeStrength : startupOptions.ScreenShakeStrength; }
        }

        internal AudioQualityPreset AudioQualityPreset
        {
            get { return campaignDirector != null ? campaignDirector.AudioQualityPreset : startupOptions.AudioQualityPreset; }
        }

        internal bool Invert3DHorizontal
        {
            get { return campaignDirector != null ? campaignDirector.Invert3DHorizontal : startupOptions.Invert3DHorizontal; }
        }

        internal bool Invert3DVertical
        {
            get { return campaignDirector != null ? campaignDirector.Invert3DVertical : startupOptions.Invert3DVertical; }
        }

        internal AimAssist3DMode AimAssist3DMode
        {
            get { return campaignDirector != null ? campaignDirector.AimAssist3DMode : startupOptions.AimAssist3DMode; }
        }

        internal FeedbackDirector Feedback
        {
            get { return feedbackDirector; }
        }

        internal AudioDirector Audio
        {
            get { return audioDirector; }
        }

        public Vector2 CameraOffset
        {
            get { return cameraRig != null ? cameraRig.WorldOffset : Vector2.Zero; }
        }

        internal float ImpactPulse
        {
            get { return feedbackDirector != null ? feedbackDirector.ImpactPulse : 0f; }
        }

        internal float HudPulse
        {
            get { return feedbackDirector != null ? feedbackDirector.HudPulse : 0f; }
        }

        internal float PickupPulse
        {
            get { return feedbackDirector != null ? feedbackDirector.PickupPulse : 0f; }
        }

        public Game1()
        {
            Instance = this;
            PlatformServices.EnsureInitialized();
            startupOptions = PersistentStorage.LoadOptions();
            capturePath = PlatformServices.Capabilities.SupportsScreenCapture
                ? Environment.GetEnvironmentVariable("SPACEBURST_CAPTURE_PATH") ?? string.Empty
                : string.Empty;
            captureMode = PlatformServices.Capabilities.SupportsScreenCapture
                ? Environment.GetEnvironmentVariable("SPACEBURST_CAPTURE_MODE") ?? string.Empty
                : string.Empty;
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

#if ANDROID
            graphics.IsFullScreen = true;
            graphics.SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight;
#else
            if (PlatformServices.Capabilities.SupportsWindowedDisplayModes)
            {
                graphics.HardwareModeSwitch = false;
                Window.AllowUserResizing = true;
                ConfigureDesktopDisplayMode(startupOptions.DisplayMode, false);
            }
#endif
        }

        protected override void Initialize()
        {
            if (PlatformServices.Capabilities.SupportsWindowedDisplayModes)
                ConfigureDesktopDisplayMode(startupOptions.DisplayMode, true);

            if (PlatformServices.Capabilities.SupportsTextInput)
                Window.TextInput += OnWindowTextInput;

            UpdateVirtualResolution();
            RecalculateViewportMatrices();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
            uiPixel = new Texture2D(GraphicsDevice, 1, 1);
            uiPixel.SetData(new[] { Color.White });
            radialTexture = CreateRadialTexture(128);

#if ANDROID || BLAZORGL
            if (PlatformServices.Capabilities.SupportsTouch)
                touchControlTexture = CreateControlTexture(128);
#endif
            InitializeBootLoader();
        }

        protected override void Update(GameTime gameTime)
        {
            GameTime = gameTime;
            BitmapFontRenderer.CurrentTheme = CurrentFontTheme;
            BitmapFontRenderer.GlobalScaleMultiplier = UiTextScale;
            RecalculateViewportMatrices();
            Input.Update();

            if (!bootComplete)
            {
                UpdateBootLoader((float)gameTime.ElapsedGameTime.TotalSeconds);
                base.Update(gameTime);
                return;
            }

            developerConsole?.Update();
            if (developerConsole != null && developerConsole.IsOpen)
            {
                GameAudioState pausedAudioState = new GameAudioState(
                    campaignDirector.CurrentState,
                    campaignDirector.HasActiveBoss,
                    campaignDirector.TransitionToBoss,
                    campaignDirector.CurrentDifficultyFactor,
                    campaignDirector.TransitionWarpStrength,
                    campaignDirector.RewindVisualStrength,
                    campaignDirector.CurrentScrollSpeed,
                    campaignDirector.CurrentStageNumber,
                    campaignDirector.TransitionTargetStageNumber,
                    campaignDirector.CurrentSectionIndex,
                    campaignDirector.CurrentSectionProgress);
                audioDirector?.Update(pausedAudioState, campaignDirector.MasterVolume, campaignDirector.MusicVolume, campaignDirector.SfxVolume, (float)gameTime.ElapsedGameTime.TotalSeconds);
                base.Update(gameTime);
                return;
            }

            campaignDirector.Update();
            GameAudioState audioState = new GameAudioState(
                campaignDirector.CurrentState,
                campaignDirector.HasActiveBoss,
                campaignDirector.TransitionToBoss,
                campaignDirector.CurrentDifficultyFactor,
                campaignDirector.TransitionWarpStrength,
                campaignDirector.RewindVisualStrength,
                campaignDirector.CurrentScrollSpeed,
                campaignDirector.CurrentStageNumber,
                campaignDirector.TransitionTargetStageNumber,
                campaignDirector.CurrentSectionIndex,
                campaignDirector.CurrentSectionProgress);
            feedbackDirector?.Update((float)gameTime.ElapsedGameTime.TotalSeconds, audioState, ScreenShakeStrength);
            audioDirector?.Update(audioState, campaignDirector.MasterVolume, campaignDirector.MusicVolume, campaignDirector.SfxVolume, (float)gameTime.ElapsedGameTime.TotalSeconds);

            if (!captureCompleted && !string.IsNullOrWhiteSpace(capturePath))
                captureDelaySeconds -= (float)gameTime.ElapsedGameTime.TotalSeconds;

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            if (!bootComplete)
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, uiScaleMatrix);
                DrawBootLoader(spriteBatch, uiPixel);
                spriteBatch.End();
                return;
            }

            if (campaignDirector.ShouldDrawWorld)
            {
                bool useTrue3DChase = CurrentViewMode == ViewMode.Chase3D && CurrentPresentationTier == PresentationTier.Late3D;
                if (useTrue3DChase)
                {
                    EnsureRenderTargets();
                    GraphicsDevice.SetRenderTarget(worldRenderTarget);
                    GraphicsDevice.Clear(Color.Transparent);

                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
                    Late3DRenderer.DrawBackdrop(spriteBatch, uiPixel, radialTexture, CurrentBackgroundMood, VisualPreset);
                    spriteBatch.End();

                    Late3DRenderer.Draw(GraphicsDevice, EntityManager.AllEntities, CurrentBackgroundMood, VisualPreset);

                    GraphicsDevice.SetRenderTarget(null);
                    DrawWorldComposite();
                }
                else
                {
                    Late3DRenderer.ResetTransientState();
                    if (VisualPreset == VisualPreset.Low)
                    {
                        bool chaseView = CurrentViewMode == ViewMode.Chase3D;
                        Matrix worldMatrix = (chaseView ? Matrix.Identity : Matrix.CreateTranslation(CameraOffset.X, CameraOffset.Y, 0f)) * worldScaleMatrix;
                        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, worldMatrix);
                        DrawBackground(spriteBatch, uiPixel);
                        WorldPresentationRenderer.Draw(spriteBatch, uiPixel, radialTexture, EntityManager.AllEntities, CurrentPresentationTier, CurrentViewMode, campaignDirector != null ? campaignDirector.CurrentStageNumber : 1);
                        spriteBatch.End();
                    }
                    else
                    {
                        EnsureRenderTargets();
                        GraphicsDevice.SetRenderTarget(worldRenderTarget);
                        GraphicsDevice.Clear(Color.Transparent);

                        Matrix worldMatrix = CurrentViewMode == ViewMode.Chase3D
                            ? Matrix.Identity
                            : Matrix.CreateTranslation(CameraOffset.X, CameraOffset.Y, 0f);
                        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, worldMatrix);
                        DrawBackground(spriteBatch, uiPixel);
                        WorldPresentationRenderer.Draw(spriteBatch, uiPixel, radialTexture, EntityManager.AllEntities, CurrentPresentationTier, CurrentViewMode, campaignDirector != null ? campaignDirector.CurrentStageNumber : 1);
                        spriteBatch.End();

                        GraphicsDevice.SetRenderTarget(null);
                        DrawWorldComposite();
                    }
                }
            }

            if (CurrentViewMode == ViewMode.Chase3D && CurrentPresentationTier == PresentationTier.Late3D)
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, uiScaleMatrix);
                Late3DRenderer.DrawInsetHud(spriteBatch, uiPixel, EntityManager.AllEntities);
                Late3DRenderer.DrawReticle(spriteBatch, uiPixel, radialTexture, ImpactPulse + HudPulse * 0.4f);
                spriteBatch.End();
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, uiScaleMatrix);
            campaignDirector.DrawUi(spriteBatch, uiPixel);
            spriteBatch.End();

#if ANDROID || BLAZORGL
            if (PlatformServices.Capabilities.SupportsTouch && campaignDirector.ShouldDrawTouchControls)
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
                Input.DrawTouchControls(spriteBatch, touchControlTexture);
                spriteBatch.End();
            }
#endif

            if (developerConsole != null && developerConsole.IsOpen)
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, uiScaleMatrix);
                developerConsole.Draw(spriteBatch, uiPixel);
                spriteBatch.End();
            }

            if (PlatformServices.Capabilities.SupportsMouseCursor
                && ((campaignDirector != null && campaignDirector.ShouldDrawMenuCursor) || (developerConsole != null && developerConsole.IsOpen)))
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, uiScaleMatrix);
                DrawMenuCursor(spriteBatch, uiPixel, Input.PointerPosition);
                spriteBatch.End();
            }

            if (!captureCompleted && !string.IsNullOrWhiteSpace(capturePath) && captureDelaySeconds <= 0f)
            {
                CaptureFrameToPng(capturePath);
                captureCompleted = true;
                Exit();
            }
        }

        public static Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            if (Instance == null || Instance.worldRenderViewport.Width == 0 || Instance.worldRenderViewport.Height == 0)
                return screenPosition;

            float x = (screenPosition.X - Instance.worldRenderViewport.X) * VirtualWidth / Instance.worldRenderViewport.Width;
            float y = (screenPosition.Y - Instance.worldRenderViewport.Y) * VirtualHeight / Instance.worldRenderViewport.Height;
            return Vector2.Clamp(new Vector2(x, y), Vector2.Zero, ScreenSize);
        }

        private static void DrawMenuCursor(SpriteBatch spriteBatch, Texture2D pixel, Vector2 position)
        {
            if (spriteBatch == null || pixel == null)
                return;

            int x = (int)MathF.Round(position.X);
            int y = (int)MathF.Round(position.Y);
            Color glow = Color.Black * 0.55f;
            Color primary = Color.White;
            Color accent = Color.Orange;

            spriteBatch.Draw(pixel, new Rectangle(x, y, 2, 16), glow);
            spriteBatch.Draw(pixel, new Rectangle(x, y, 12, 2), glow);
            spriteBatch.Draw(pixel, new Rectangle(x + 2, y + 2, 2, 10), glow);
            spriteBatch.Draw(pixel, new Rectangle(x + 4, y + 4, 2, 8), glow);
            spriteBatch.Draw(pixel, new Rectangle(x + 6, y + 6, 2, 6), glow);
            spriteBatch.Draw(pixel, new Rectangle(x + 8, y + 8, 2, 4), glow);

            spriteBatch.Draw(pixel, new Rectangle(x, y, 2, 14), primary);
            spriteBatch.Draw(pixel, new Rectangle(x, y, 10, 2), primary);
            spriteBatch.Draw(pixel, new Rectangle(x + 2, y + 2, 2, 10), accent);
            spriteBatch.Draw(pixel, new Rectangle(x + 4, y + 4, 2, 8), accent);
            spriteBatch.Draw(pixel, new Rectangle(x + 6, y + 6, 2, 6), accent);
            spriteBatch.Draw(pixel, new Rectangle(x + 8, y + 8, 2, 4), primary);
        }

        public static Vector2 ScreenToUi(Vector2 screenPosition)
        {
            if (Instance == null || Instance.uiRenderViewport.Width == 0 || Instance.uiRenderViewport.Height == 0)
                return screenPosition;

            float x = (screenPosition.X - Instance.uiRenderViewport.X) * VirtualWidth / Instance.uiRenderViewport.Width;
            float y = (screenPosition.Y - Instance.uiRenderViewport.Y) * VirtualHeight / Instance.uiRenderViewport.Height;
            return Vector2.Clamp(new Vector2(x, y), Vector2.Zero, ScreenSize);
        }

        private void DrawBackground(SpriteBatch spriteBatch, Texture2D pixel)
        {
            BackgroundRenderer.Draw(
                spriteBatch,
                pixel,
                radialTexture,
                CurrentBackgroundMood,
                CurrentBackgroundSeed,
                CurrentScrollSpeed,
                CurrentDifficultyFactor,
                ActiveEventType,
                ActiveEventIntensity,
                CameraOffset,
                campaignDirector != null ? campaignDirector.TransitionWarpStrength : 0f,
                campaignDirector != null ? campaignDirector.RewindVisualStrength : 0f,
                ImpactPulse,
                PickupPulse,
                VisualPreset,
                CurrentPresentationTier,
                CurrentViewMode);
        }

        private void DrawWorldComposite()
        {
            Rectangle destination = worldRenderViewport;
            float warp = campaignDirector != null ? campaignDirector.TransitionWarpStrength : 0f;
            float pulse = MathHelper.Clamp((cameraRig != null ? cameraRig.PulseStrength : 0f) + ImpactPulse * 0.35f, 0f, 1f);
            if (pulse > 0f)
                destination = new Rectangle(destination.X - (int)(pulse * 2f), destination.Y - (int)(pulse * 2f), destination.Width + (int)(pulse * 4f), destination.Height + (int)(pulse * 4f));

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            spriteBatch.Draw(worldRenderTarget, destination, Color.White);
            spriteBatch.End();

            if (!EnableBloom)
                return;

            float glowStrength = VisualPreset == VisualPreset.Neon ? 0.14f : 0.08f;
            int offset = VisualPreset == VisualPreset.Neon ? 4 : 2;

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp);
            spriteBatch.Draw(worldRenderTarget, new Rectangle(destination.X - offset, destination.Y, destination.Width, destination.Height), Color.White * glowStrength);
            spriteBatch.Draw(worldRenderTarget, new Rectangle(destination.X + offset, destination.Y, destination.Width, destination.Height), Color.White * glowStrength);
            spriteBatch.Draw(worldRenderTarget, new Rectangle(destination.X, destination.Y - offset, destination.Width, destination.Height), Color.White * glowStrength);
            spriteBatch.Draw(worldRenderTarget, new Rectangle(destination.X, destination.Y + offset, destination.Width, destination.Height), Color.White * glowStrength);
            if (VisualPreset == VisualPreset.Neon)
            {
                int neonInset = 2 + (int)(warp * 5f);
                spriteBatch.Draw(worldRenderTarget, new Rectangle(destination.X - neonInset, destination.Y - 2, destination.Width + neonInset * 2, destination.Height + 4), Color.White * (0.06f + pulse * 0.08f));
                if (pulse > 0.08f)
                    spriteBatch.Draw(worldRenderTarget, new Rectangle(destination.X - 4, destination.Y - 4, destination.Width + 8, destination.Height + 8), Color.Cyan * (0.03f + pulse * 0.06f));
            }
            spriteBatch.End();
        }

        private void RecalculateViewportMatrices()
        {
            UpdateVirtualResolution();

            Viewport viewport = GraphicsDevice.Viewport;
            float fitScale = Math.Min(viewport.Width / (float)virtualWidth, viewport.Height / (float)virtualHeight);
            float uiScale = fitScale;
            if (uiScale <= 0f)
                uiScale = 1f;

            uiRenderViewport = CreateViewport(viewport, uiScale);
            worldRenderViewport = uiRenderViewport;
            uiScaleMatrix = Matrix.CreateScale(uiScale, uiScale, 1f) * Matrix.CreateTranslation(uiRenderViewport.X, uiRenderViewport.Y, 0f);
            worldScaleMatrix = uiScaleMatrix;
            safeUiBounds = ComputeSafeUiBounds(viewport);
        }

        private Rectangle CreateViewport(Viewport viewport, float scale)
        {
            int width = (int)MathF.Round(virtualWidth * scale);
            int height = (int)MathF.Round(virtualHeight * scale);
            int x = (viewport.Width - width) / 2;
            int y = (viewport.Height - height) / 2;
            return new Rectangle(x, y, width, height);
        }

        private int UiPx(int value)
        {
            return Math.Max(1, (int)MathF.Round(value * UiLayoutScale));
        }

        private void UpdateVirtualResolution()
        {
            virtualWidth = VirtualBaseWidth;
            virtualHeight = VirtualBaseHeight;
        }

        private Rectangle ComputeSafeUiBounds(Viewport viewport)
        {
            Rectangle safeArea = viewport.TitleSafeArea;
            if (safeArea.Width <= 0 || safeArea.Height <= 0 || uiRenderViewport.Width <= 0 || uiRenderViewport.Height <= 0)
                return new Rectangle(0, 0, VirtualWidth, VirtualHeight);

            float x = (safeArea.X - uiRenderViewport.X) * VirtualWidth / (float)uiRenderViewport.Width;
            float y = (safeArea.Y - uiRenderViewport.Y) * VirtualHeight / (float)uiRenderViewport.Height;
            float width = safeArea.Width * VirtualWidth / (float)uiRenderViewport.Width;
            float height = safeArea.Height * VirtualHeight / (float)uiRenderViewport.Height;

            Rectangle bounds = new Rectangle(
                (int)MathF.Round(x),
                (int)MathF.Round(y),
                Math.Max(1, (int)MathF.Round(width)),
                Math.Max(1, (int)MathF.Round(height)));

            Rectangle full = new Rectangle(0, 0, VirtualWidth, VirtualHeight);
            return Rectangle.Intersect(full, bounds);
        }

        private void EnsureRenderTargets()
        {
            if (worldRenderTarget != null && (worldRenderTarget.Width != virtualWidth || worldRenderTarget.Height != virtualHeight))
            {
                worldRenderTarget.Dispose();
                worldRenderTarget = null;
            }

            if (worldRenderTarget == null)
            {
                DepthFormat depthFormat = PlatformServices.Capabilities.PreferDepth16RenderTargets
                    ? DepthFormat.Depth16
                    : DepthFormat.Depth24;
                worldRenderTarget = new RenderTarget2D(GraphicsDevice, virtualWidth, virtualHeight, false, SurfaceFormat.Color, depthFormat, 0, RenderTargetUsage.DiscardContents);
            }
        }

        private void InitializeBootLoader()
        {
            bootPhase = BootPhase.PrepareElements;
            bootReady = false;
            bootComplete = false;
            bootExploded = false;
            bootHasDrawn = false;
            bootVisibleSeconds = 0f;
            bootReadySeconds = 0f;
            bootStatus = "STARTING";
            RandomizeBootPalette();
            BuildBootPixels();
        }

        private void UpdateBootLoader(float deltaSeconds)
        {
            if (bootHasDrawn)
                bootVisibleSeconds += deltaSeconds;

            UpdateBootPixels(deltaSeconds);

            if (Input.WasPrimaryActionPressed())
            {
                PlatformServices.AudioStartGate.NotifyPrimaryGesture();
                if (GetBootLogoBounds().Contains(Input.PointerPosition))
                    TriggerBootExplosion();
            }

            if (!bootReady)
            {
                RunNextBootPhase();
                return;
            }

            bootReadySeconds += deltaSeconds;
            if (bootHasDrawn && bootVisibleSeconds >= MinimumBootVisibleSeconds && (Input.WasPrimaryActionPressed() || bootExploded || bootReadySeconds >= ReadyBootHandoffSeconds))
                bootComplete = true;
        }

        private void RunNextBootPhase()
        {
            switch (bootPhase)
            {
                case BootPhase.PrepareElements:
                    bootStatus = "PROCEDURAL ELEMENT SETUP";
                    Element.Load();
                    bootPhase = BootPhase.LoadCampaignRepository;
                    break;
                case BootPhase.LoadCampaignRepository:
                    bootStatus = "CAMPAIGN LOAD AND VALIDATION";
                    campaignDirector = new CampaignDirector();
                    campaignDirector.LoadRepository();
                    bootPhase = BootPhase.InitializeCameraFeedback;
                    break;
                case BootPhase.InitializeCameraFeedback:
                    bootStatus = "CAMERA AND FEEDBACK";
                    cameraRig = new CameraRig();
                    feedbackDirector = new FeedbackDirector(cameraRig, audioDirector);
                    bootPhase = BootPhase.InitializeAudio;
                    break;
                case BootPhase.InitializeAudio:
                    if (PlatformServices.AudioStartGate.RequiresUserGesture && !PlatformServices.AudioStartGate.IsReady)
                    {
                        bootStatus = "WAITING FOR A TAP TO ARM AUDIO";
                        return;
                    }

                    bootStatus = "PROCEDURAL AUDIO SYNTHESIS";
                    TryInitializeAudio(AudioQualityPreset);
                    bootPhase = BootPhase.FinalizeDirector;
                    break;
                case BootPhase.FinalizeDirector:
                    bootStatus = "FINAL DIRECTOR HANDOFF";
                    campaignDirector.FinishBootToTitle();
                    if (PlatformServices.Capabilities.SupportsTextInput)
                        developerConsole = new ConsoleState(this, campaignDirector);
                    PrepareCaptureMode();
                    bootPhase = BootPhase.Complete;
                    bootReady = true;
                    break;
                default:
                    bootReady = true;
                    break;
            }
        }

        private void DrawBootLoader(SpriteBatch spriteBatch, Texture2D pixel)
        {
            bootHasDrawn = true;
            Rectangle full = new Rectangle(0, 0, VirtualWidth, VirtualHeight);
            spriteBatch.Draw(pixel, full, bootBackgroundColor);

            float time = (float)GameTime.TotalGameTime.TotalSeconds;
            for (int i = 0; i < 80; i++)
            {
                float xSeed = MathF.Abs(MathF.Sin(i * 13.117f + 0.71f));
                float ySeed = MathF.Abs(MathF.Sin(i * 29.137f + 2.31f));
                float twinkle = 0.35f + 0.65f * MathF.Abs(MathF.Sin(time * (0.72f + i * 0.03f) + i));
                int x = (int)(xSeed * (VirtualWidth - 8));
                int y = (int)(16f + ySeed * (VirtualHeight - 32f));
                int size = i % 11 == 0 ? 3 : 2;
                spriteBatch.Draw(pixel, new Rectangle(x, y, size, size), Color.White * (0.1f + twinkle * 0.14f));
            }

            if (radialTexture != null)
            {
                Vector2 origin = new Vector2(radialTexture.Width / 2f, radialTexture.Height / 2f);
                spriteBatch.Draw(radialTexture, new Vector2(VirtualWidth * 0.2f, VirtualHeight * 0.24f), null, bootGlowColorA * 0.18f, 0f, origin, 2.8f, SpriteEffects.None, 0f);
                spriteBatch.Draw(radialTexture, new Vector2(VirtualWidth * 0.82f, VirtualHeight * 0.22f), null, bootGlowColorB * 0.16f, 0f, origin, 2.45f, SpriteEffects.None, 0f);
                spriteBatch.Draw(radialTexture, new Vector2(VirtualWidth * 0.56f, VirtualHeight * 0.72f), null, Color.Lerp(bootGlowColorA, bootGlowColorB, 0.45f) * 0.08f, 0f, origin, 3.1f, SpriteEffects.None, 0f);
            }

            Rectangle logoBounds = GetBootLogoBounds();
            float fade = MathHelper.Clamp(bootVisibleSeconds / 0.42f, 0f, 1f);
            for (int i = 0; i < bootPixels.Count; i++)
            {
                BootPixel px = bootPixels[i];
                spriteBatch.Draw(pixel, new Rectangle((int)px.Position.X, (int)px.Position.Y, 4, 4), px.Color * fade);
            }

            BitmapFontRenderer.DrawCentered(spriteBatch, pixel, "SABINO SOFTWARE", new Vector2(ScreenSize.X / 2f, logoBounds.Y + UiLayoutScale * 58f), bootPrimaryColor * fade, 2.35f);
            BitmapFontRenderer.DrawCentered(spriteBatch, pixel, "GENERATING PROCEDURAL SYSTEMS", new Vector2(ScreenSize.X / 2f, logoBounds.Bottom + UiLayoutScale * 18f), bootSecondaryColor * (0.9f * fade), 1.05f);
            BitmapFontRenderer.DrawCentered(spriteBatch, pixel, bootStatus, new Vector2(ScreenSize.X / 2f, logoBounds.Bottom + UiLayoutScale * 56f), Color.White * (0.75f * fade), 0.95f);

            Rectangle progressBounds = new Rectangle((int)(VirtualWidth * 0.5f - UiLayoutScale * 180f), (int)(logoBounds.Bottom + UiLayoutScale * 86f), (int)(UiLayoutScale * 360f), UiPx(10));
            spriteBatch.Draw(pixel, progressBounds, Color.White * 0.12f);
            float progress = bootReady ? 1f : ((float)bootPhase + 0.15f) / (int)BootPhase.Complete;
            spriteBatch.Draw(pixel, new Rectangle(progressBounds.X, progressBounds.Y, Math.Max(1, (int)MathF.Round(progressBounds.Width * progress)), progressBounds.Height), Color.Lerp(bootGlowColorA, bootGlowColorB, 0.4f));

            float promptPulse = 0.4f + 0.6f * MathF.Abs(MathF.Sin(time * 4.1f));
            string prompt = bootReady
#if ANDROID
                ? "TAP THE LOGO TO BURST TO THE MENU"
                : "TAP THE LOGO TO SPARK THE LOADER";
#else
                ? "TAP OR CLICK THE LOGO TO BURST TO THE MENU"
                : "TAP OR CLICK THE LOGO TO SPARK THE LOADER";
#endif
            BitmapFontRenderer.DrawCentered(spriteBatch, pixel, prompt, new Vector2(ScreenSize.X / 2f, VirtualHeight - UiLayoutScale * 96f), bootPromptColor * ((0.35f + promptPulse * 0.35f) * fade), 0.95f);
            spriteBatch.Draw(pixel, full, Color.Black * (1f - fade));
        }

        private void OnWindowTextInput(object sender, TextInputEventArgs e)
        {
            if (PlatformServices.Capabilities.SupportsTextInput)
                developerConsole?.HandleTextInput(e.Character);
        }

        private void UpdateBootPixels(float deltaSeconds)
        {
            for (int i = 0; i < bootPixels.Count; i++)
            {
                BootPixel pixel = bootPixels[i];
                Vector2 toTarget = pixel.Target - pixel.Position;
                float attraction = bootExploded && bootReady ? 0.04f : 0.18f;
                pixel.Velocity += toTarget * attraction * deltaSeconds * 60f;
                pixel.Velocity *= bootExploded && bootReady ? 0.985f : 0.92f;
                pixel.Position += pixel.Velocity * deltaSeconds;
                bootPixels[i] = pixel;
            }
        }

        private void TriggerBootExplosion()
        {
            Rectangle logoBounds = GetBootLogoBounds();
            Vector2 origin = new Vector2(logoBounds.Center.X, logoBounds.Center.Y);
            bootExploded = true;
            for (int i = 0; i < bootPixels.Count; i++)
            {
                BootPixel pixel = bootPixels[i];
                Vector2 direction = pixel.Position - origin;
                if (direction == Vector2.Zero)
                    direction = new Vector2(0f, -1f);
                direction.Normalize();
                pixel.Velocity = direction * (180f + (i % 17) * 10f);
                bootPixels[i] = pixel;
            }
        }

        private void BuildBootPixels()
        {
            bootPixels.Clear();
            Rectangle logo = GetBootLogoBounds();
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
                    float seed = MathF.Abs(MathF.Sin((x + 1) * 11.73f + (y + 1) * 17.19f));
                    Vector2 start = new Vector2(target.X, -64f - seed * 320f - y * 8f);
                    bootPixels.Add(new BootPixel
                    {
                        Position = start,
                        Velocity = new Vector2(0f, 64f + seed * 48f),
                        Target = target,
                        Color = (x + y) % 2 == 0 ? bootPrimaryColor : bootSecondaryColor,
                    });
                }
            }
        }

        private Rectangle GetBootLogoBounds()
        {
            int width = Math.Min(VirtualWidth - UiPx(280), UiPx(760));
            int height = Math.Min(VirtualHeight / 3, UiPx(210));
            return new Rectangle((VirtualWidth - width) / 2, UiPx(96), width, height);
        }

        private void RandomizeBootPalette()
        {
            switch (bootVisualRandom.Next(5))
            {
                case 0:
                    bootBackgroundColor = new Color(8, 12, 26);
                    bootGlowColorA = new Color(88, 196, 255);
                    bootGlowColorB = new Color(255, 188, 96);
                    bootPrimaryColor = Color.White;
                    bootSecondaryColor = new Color(88, 196, 255);
                    bootPromptColor = new Color(255, 188, 96);
                    break;
                case 1:
                    bootBackgroundColor = new Color(14, 8, 24);
                    bootGlowColorA = new Color(201, 116, 255);
                    bootGlowColorB = new Color(86, 237, 193);
                    bootPrimaryColor = Color.White;
                    bootSecondaryColor = new Color(201, 116, 255);
                    bootPromptColor = new Color(86, 237, 193);
                    break;
                case 2:
                    bootBackgroundColor = new Color(8, 16, 18);
                    bootGlowColorA = new Color(123, 229, 176);
                    bootGlowColorB = new Color(244, 232, 120);
                    bootPrimaryColor = Color.White;
                    bootSecondaryColor = new Color(123, 229, 176);
                    bootPromptColor = new Color(244, 232, 120);
                    break;
                case 3:
                    bootBackgroundColor = new Color(16, 10, 18);
                    bootGlowColorA = new Color(255, 132, 165);
                    bootGlowColorB = new Color(116, 170, 255);
                    bootPrimaryColor = Color.White;
                    bootSecondaryColor = new Color(255, 132, 165);
                    bootPromptColor = new Color(116, 170, 255);
                    break;
                default:
                    bootBackgroundColor = new Color(6, 14, 30);
                    bootGlowColorA = new Color(92, 160, 255);
                    bootGlowColorB = new Color(152, 246, 255);
                    bootPrimaryColor = Color.White;
                    bootSecondaryColor = new Color(152, 246, 255);
                    bootPromptColor = new Color(92, 160, 255);
                    break;
            }
        }

        protected override void UnloadContent()
        {
            audioDirector?.Dispose();
            worldRenderTarget?.Dispose();
            radialTexture?.Dispose();
            uiPixel?.Dispose();
#if ANDROID || BLAZORGL
            touchControlTexture?.Dispose();
#endif
            base.UnloadContent();
        }

        internal void ApplyAudioQuality(AudioQualityPreset qualityPreset)
        {
            TryInitializeAudio(qualityPreset);
        }

        private void PrepareCaptureMode()
        {
            if (!PlatformServices.Capabilities.SupportsScreenCapture || capturePrepared || string.IsNullOrWhiteSpace(capturePath))
                return;

            capturePrepared = true;
            string mode = captureMode.Trim().ToLowerInvariant();
            if (mode.StartsWith("slot-", StringComparison.Ordinal))
            {
                captureDelaySeconds = 5.5f;
                if (int.TryParse(mode.Substring(5), out int slotNumber) && slotNumber > 0)
                    campaignDirector.LoadRunSlotForCapture(slotNumber);
            }
            else
            {
                captureDelaySeconds = 1.2f;
            }
        }

        private void CaptureFrameToPng(string path)
        {
            if (!PlatformServices.Capabilities.SupportsScreenCapture)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

            int width = GraphicsDevice.PresentationParameters.BackBufferWidth;
            int height = GraphicsDevice.PresentationParameters.BackBufferHeight;
            var data = new Color[width * height];
            GraphicsDevice.GetBackBufferData(data);

            using var texture = new Texture2D(GraphicsDevice, width, height, false, SurfaceFormat.Color);
            texture.SetData(data);
            using FileStream stream = File.Create(path);
            texture.SaveAsPng(stream, width, height);
        }

        public void ApplyDisplayMode(DesktopDisplayMode mode)
        {
            if (PlatformServices.Capabilities.SupportsWindowedDisplayModes)
                ConfigureDesktopDisplayMode(mode, true);
        }

        private bool TryInitializeAudio(AudioQualityPreset qualityPreset)
        {
            audioDirector?.Dispose();
            audioDirector = null;

            try
            {
                audioDirector = new AudioDirector(qualityPreset);
                feedbackDirector = new FeedbackDirector(cameraRig, audioDirector);
                return true;
            }
            catch
            {
                feedbackDirector = new FeedbackDirector(cameraRig, null);
                return false;
            }
        }

#if !ANDROID
        private void ConfigureDesktopDisplayMode(DesktopDisplayMode mode, bool applyChanges)
        {
            if (mode == DesktopDisplayMode.BorderlessFullscreen)
            {
                Microsoft.Xna.Framework.Graphics.DisplayMode displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
                Window.IsBorderless = true;
                graphics.IsFullScreen = true;
                graphics.PreferredBackBufferWidth = displayMode.Width;
                graphics.PreferredBackBufferHeight = displayMode.Height;
            }
            else
            {
                Window.IsBorderless = false;
                graphics.IsFullScreen = false;
                graphics.PreferredBackBufferWidth = VirtualBaseWidth;
                graphics.PreferredBackBufferHeight = VirtualBaseHeight;
            }

            if (applyChanges)
                graphics.ApplyChanges();
        }
#endif

#if ANDROID || BLAZORGL
        private Texture2D CreateRadialTexture(int size)
        {
            var texture = new Texture2D(GraphicsDevice, size, size);
            var data = new Color[size * size];
            float radius = (size - 1) / 2f;
            var center = new Vector2(radius);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    if (distance > radius)
                        continue;

                    float alpha = 1f - distance / radius;
                    data[y * size + x] = Color.White * alpha * alpha;
                }
            }

            texture.SetData(data);
            return texture;
        }

        private Texture2D CreateControlTexture(int size)
        {
            var texture = new Texture2D(GraphicsDevice, size, size);
            var data = new Color[size * size];
            float radius = (size - 1) / 2f;
            var center = new Vector2(radius);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    if (distance > radius)
                        continue;

                    float alpha = 1f - distance / radius;
                    data[y * size + x] = Color.White * alpha * alpha;
                }
            }

            texture.SetData(data);
            return texture;
        }
#else
        private Texture2D CreateRadialTexture(int size)
        {
            var texture = new Texture2D(GraphicsDevice, size, size);
            var data = new Color[size * size];
            float radius = (size - 1) / 2f;
            var center = new Vector2(radius);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    if (distance > radius)
                        continue;

                    float alpha = 1f - distance / radius;
                    data[y * size + x] = Color.White * alpha * alpha;
                }
            }

            texture.SetData(data);
            return texture;
        }
#endif
    }
}
