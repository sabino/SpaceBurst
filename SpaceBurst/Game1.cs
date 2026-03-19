using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceBurst.RuntimeData;
using System;
using System.IO;

namespace SpaceBurst
{
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        private const int VirtualBaseWidth = 1280;
        private const int VirtualBaseHeight = 720;

        public static Game1 Instance { get; private set; }
        public static Texture2D UiPixel { get { return Instance != null ? Instance.uiPixel : null; } }
        public static Texture2D RadialTexture { get { return Instance != null ? Instance.radialTexture : null; } }
        public static int VirtualWidth { get { return Instance != null ? Instance.virtualWidth : VirtualBaseWidth; } }
        public static int VirtualHeight { get { return Instance != null ? Instance.virtualHeight : VirtualBaseHeight; } }
        public static Viewport Viewport { get { return new Viewport(0, 0, VirtualWidth, VirtualHeight); } }
        public static Vector2 ScreenSize { get { return new Vector2(VirtualWidth, VirtualHeight); } }
        public static Rectangle RenderBounds { get { return Instance != null ? Instance.renderViewport : new Rectangle(0, 0, VirtualWidth, VirtualHeight); } }
        public static GameTime GameTime { get; private set; }

        private readonly GraphicsDeviceManager graphics;
        private readonly OptionsData startupOptions;
        private SpriteBatch spriteBatch;
        private Rectangle renderViewport;
        private Matrix scaleMatrix;
        private CampaignDirector campaignDirector;
        private AudioDirector audioDirector;
        private FeedbackDirector feedbackDirector;
        private CameraRig cameraRig;
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

#if ANDROID
        private Texture2D touchControlTexture;
#endif

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

        public VisualPreset VisualPreset
        {
            get { return campaignDirector != null ? campaignDirector.VisualPreset : VisualPreset.Standard; }
        }

        internal ScreenShakeStrength ScreenShakeStrength
        {
            get { return campaignDirector != null ? campaignDirector.ScreenShakeStrength : startupOptions.ScreenShakeStrength; }
        }

        internal AudioQualityPreset AudioQualityPreset
        {
            get { return campaignDirector != null ? campaignDirector.AudioQualityPreset : startupOptions.AudioQualityPreset; }
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
            startupOptions = PersistentStorage.LoadOptions();
            capturePath = Environment.GetEnvironmentVariable("SPACEBURST_CAPTURE_PATH") ?? string.Empty;
            captureMode = Environment.GetEnvironmentVariable("SPACEBURST_CAPTURE_MODE") ?? string.Empty;
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

#if ANDROID
            graphics.IsFullScreen = true;
            graphics.SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight;
#else
            graphics.HardwareModeSwitch = false;
            Window.AllowUserResizing = true;
            ConfigureDesktopDisplayMode(startupOptions.DisplayMode, false);
#endif
        }

        protected override void Initialize()
        {
#if !ANDROID
            ConfigureDesktopDisplayMode(startupOptions.DisplayMode, true);
#endif
            UpdateVirtualResolution();
            RecalculateScaleMatrix();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
            Element.Load();

            uiPixel = new Texture2D(GraphicsDevice, 1, 1);
            uiPixel.SetData(new[] { Color.White });
            radialTexture = CreateRadialTexture(128);

#if ANDROID
            touchControlTexture = CreateControlTexture(128);
#endif

            campaignDirector = new CampaignDirector();
            campaignDirector.Load();
            cameraRig = new CameraRig();
            audioDirector = new AudioDirector(AudioQualityPreset);
            feedbackDirector = new FeedbackDirector(cameraRig, audioDirector);

            PrepareCaptureMode();
        }

        protected override void Update(GameTime gameTime)
        {
            GameTime = gameTime;
            RecalculateScaleMatrix();
            Input.Update();
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
            feedbackDirector.Update((float)gameTime.ElapsedGameTime.TotalSeconds, audioState, ScreenShakeStrength);
            audioDirector.Update(audioState, campaignDirector.MasterVolume, campaignDirector.MusicVolume, campaignDirector.SfxVolume, (float)gameTime.ElapsedGameTime.TotalSeconds);

            if (!captureCompleted && !string.IsNullOrWhiteSpace(capturePath))
                captureDelaySeconds -= (float)gameTime.ElapsedGameTime.TotalSeconds;

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            if (campaignDirector.ShouldDrawWorld)
            {
                if (VisualPreset == VisualPreset.Low)
                {
                    Matrix worldScaleMatrix = Matrix.CreateTranslation(CameraOffset.X, CameraOffset.Y, 0f) * scaleMatrix;
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, worldScaleMatrix);
                    DrawBackground(spriteBatch, uiPixel);
                    EntityManager.Draw(spriteBatch);
                    spriteBatch.End();
                }
                else
                {
                    EnsureRenderTargets();
                    GraphicsDevice.SetRenderTarget(worldRenderTarget);
                    GraphicsDevice.Clear(Color.Transparent);

                    Matrix worldMatrix = Matrix.CreateTranslation(CameraOffset.X, CameraOffset.Y, 0f);
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, worldMatrix);
                    DrawBackground(spriteBatch, uiPixel);
                    EntityManager.Draw(spriteBatch);
                    spriteBatch.End();

                    GraphicsDevice.SetRenderTarget(null);
                    DrawWorldComposite();
                }
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, scaleMatrix);
            campaignDirector.DrawUi(spriteBatch, uiPixel);
            spriteBatch.End();

#if ANDROID
            if (campaignDirector.ShouldDrawTouchControls)
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
                Input.DrawTouchControls(spriteBatch, touchControlTexture);
                spriteBatch.End();
            }
#endif

            if (!captureCompleted && !string.IsNullOrWhiteSpace(capturePath) && captureDelaySeconds <= 0f)
            {
                CaptureFrameToPng(capturePath);
                captureCompleted = true;
                Exit();
            }
        }

        public static Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            if (Instance == null || Instance.renderViewport.Width == 0 || Instance.renderViewport.Height == 0)
                return screenPosition;

            float x = (screenPosition.X - Instance.renderViewport.X) * VirtualWidth / Instance.renderViewport.Width;
            float y = (screenPosition.Y - Instance.renderViewport.Y) * VirtualHeight / Instance.renderViewport.Height;
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
                VisualPreset);
        }

        private void DrawWorldComposite()
        {
            Rectangle destination = renderViewport;
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

        private void RecalculateScaleMatrix()
        {
            UpdateVirtualResolution();

            Viewport viewport = GraphicsDevice.Viewport;
            float scale = Math.Min(viewport.Width / (float)virtualWidth, viewport.Height / (float)virtualHeight);
            if (scale <= 0f)
                scale = 1f;

            int width = (int)(virtualWidth * scale);
            int height = (int)(virtualHeight * scale);
            int x = (viewport.Width - width) / 2;
            int y = (viewport.Height - height) / 2;

            renderViewport = new Rectangle(x, y, width, height);
            scaleMatrix = Matrix.CreateScale(scale, scale, 1f) * Matrix.CreateTranslation(renderViewport.X, renderViewport.Y, 0f);
        }

        private void UpdateVirtualResolution()
        {
            virtualWidth = VirtualBaseWidth;
            virtualHeight = VirtualBaseHeight;
        }

        private void EnsureRenderTargets()
        {
            if (worldRenderTarget != null && (worldRenderTarget.Width != virtualWidth || worldRenderTarget.Height != virtualHeight))
            {
                worldRenderTarget.Dispose();
                worldRenderTarget = null;
            }

            if (worldRenderTarget == null)
                worldRenderTarget = new RenderTarget2D(GraphicsDevice, virtualWidth, virtualHeight, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.DiscardContents);
        }

        protected override void UnloadContent()
        {
            audioDirector?.Dispose();
            worldRenderTarget?.Dispose();
            radialTexture?.Dispose();
            uiPixel?.Dispose();
#if ANDROID
            touchControlTexture?.Dispose();
#endif
            base.UnloadContent();
        }

        internal void ApplyAudioQuality(AudioQualityPreset qualityPreset)
        {
            audioDirector?.Dispose();
            audioDirector = new AudioDirector(qualityPreset);
            feedbackDirector = new FeedbackDirector(cameraRig, audioDirector);
        }

        private void PrepareCaptureMode()
        {
            if (capturePrepared || string.IsNullOrWhiteSpace(capturePath))
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
#if !ANDROID
            ConfigureDesktopDisplayMode(mode, true);
#endif
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

#if ANDROID
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
