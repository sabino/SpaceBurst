using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;
using SpaceBurst.RuntimeData;
using System;

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
        private Texture2D uiPixel;
        private Texture2D radialTexture;
        private RenderTarget2D worldRenderTarget;
        private int virtualWidth = VirtualBaseWidth;
        private int virtualHeight = VirtualBaseHeight;

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

        public Game1()
        {
            Instance = this;
            startupOptions = PersistentStorage.LoadOptions();
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
            Sound.Load(Content);

            uiPixel = new Texture2D(GraphicsDevice, 1, 1);
            uiPixel.SetData(new[] { Color.White });
            radialTexture = CreateRadialTexture(128);

#if ANDROID
            touchControlTexture = CreateControlTexture(128);
#endif

            campaignDirector = new CampaignDirector();
            campaignDirector.Load();

            MediaPlayer.IsRepeating = true;
            MediaPlayer.Play(Sound.Music);
        }

        protected override void Update(GameTime gameTime)
        {
            GameTime = gameTime;
            RecalculateScaleMatrix();
            Input.Update();
            campaignDirector.Update();
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            if (campaignDirector.ShouldDrawWorld)
            {
                if (VisualPreset == VisualPreset.Low)
                {
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, scaleMatrix);
                    DrawBackground(spriteBatch, uiPixel);
                    EntityManager.Draw(spriteBatch);
                    spriteBatch.End();
                }
                else
                {
                    EnsureRenderTargets();
                    GraphicsDevice.SetRenderTarget(worldRenderTarget);
                    GraphicsDevice.Clear(Color.Transparent);

                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
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
                ActiveEventIntensity);
        }

        private void DrawWorldComposite()
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            spriteBatch.Draw(worldRenderTarget, renderViewport, Color.White);
            spriteBatch.End();

            if (!EnableBloom)
                return;

            float glowStrength = VisualPreset == VisualPreset.Neon ? 0.14f : 0.08f;
            int offset = VisualPreset == VisualPreset.Neon ? 4 : 2;

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp);
            spriteBatch.Draw(worldRenderTarget, new Rectangle(renderViewport.X - offset, renderViewport.Y, renderViewport.Width, renderViewport.Height), Color.White * glowStrength);
            spriteBatch.Draw(worldRenderTarget, new Rectangle(renderViewport.X + offset, renderViewport.Y, renderViewport.Width, renderViewport.Height), Color.White * glowStrength);
            spriteBatch.Draw(worldRenderTarget, new Rectangle(renderViewport.X, renderViewport.Y - offset, renderViewport.Width, renderViewport.Height), Color.White * glowStrength);
            spriteBatch.Draw(worldRenderTarget, new Rectangle(renderViewport.X, renderViewport.Y + offset, renderViewport.Width, renderViewport.Height), Color.White * glowStrength);
            if (VisualPreset == VisualPreset.Neon)
                spriteBatch.Draw(worldRenderTarget, new Rectangle(renderViewport.X - 2, renderViewport.Y - 2, renderViewport.Width + 4, renderViewport.Height + 4), Color.White * 0.06f);
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
