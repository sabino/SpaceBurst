//using BloomPostprocess;
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace SpaceBurst
{
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        private const int DesktopVirtualWidth = 800;
        private const int DesktopVirtualHeight = 600;
        private const int AndroidBaseVirtualWidth = 540;
        private const int AndroidMinimumVirtualHeight = 960;

        // some helpful static properties
        public static Game1 Instance { get; private set; }
        public static int VirtualWidth { get { return Instance != null ? Instance.virtualWidth : GetDefaultVirtualWidth(); } }
        public static int VirtualHeight { get { return Instance != null ? Instance.virtualHeight : GetDefaultVirtualHeight(); } }
        public static Viewport Viewport { get { return new Viewport(0, 0, VirtualWidth, VirtualHeight); } }
        public static Vector2 ScreenSize { get { return new Vector2(VirtualWidth, VirtualHeight); } }
        public static Rectangle RenderBounds { get { return Instance != null ? Instance.renderViewport : new Rectangle(0, 0, VirtualWidth, VirtualHeight); } }
        public static GameTime GameTime { get; private set; }

        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        //BloomComponent bloom;
        Rectangle renderViewport;
        Matrix scaleMatrix;
        int virtualWidth = DesktopVirtualWidth;
        int virtualHeight = DesktopVirtualHeight;
#if ANDROID
        Texture2D touchControlTexture;
#endif

        bool paused = false;
        bool useBloom = false;

        public Game1()
        {
            Instance = this;
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

#if ANDROID
            graphics.IsFullScreen = true;
            graphics.SupportedOrientations = DisplayOrientation.Portrait | DisplayOrientation.PortraitDown;
#else
            graphics.PreferredBackBufferWidth = DesktopVirtualWidth;
            graphics.PreferredBackBufferHeight = DesktopVirtualHeight;
            graphics.IsFullScreen = false;
#endif

            //bloom = new BloomComponent(this);
            //Components.Add(bloom);
            //bloom.Settings = new BloomSettings(null, 0.25f, 4, 2, 1, 1.5f, 1);
        }

        protected override void Initialize()
        {
            UpdateVirtualResolution();
            RecalculateScaleMatrix();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
            Element.Load(Content);
            Sound.Load(Content);
#if ANDROID
            touchControlTexture = CreateControlTexture(128);
#endif

            EntityManager.Add(Player1.Instance);
            //EntityManager.Add(Turret.Instance);

            MediaPlayer.IsRepeating = true;
            MediaPlayer.Play(Sound.Music);
        }

        protected override void Update(GameTime gameTime)
        {
            GameTime = gameTime;
            RecalculateScaleMatrix();
            Input.Update();

            // Allows the game to exit
            if (Input.WasButtonPressed(Buttons.Back) || Input.WasKeyPressed(Keys.Escape))
                Exit();

            if (Input.WasKeyPressed(Keys.P))
                paused = !paused;
            if (Input.WasKeyPressed(Keys.B))
                useBloom = !useBloom;

            if (!paused)
            {
                EntityManager.Update();
                EnemySpawner.Update();
                PlayerStatus.Update();
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            //bloom.BeginDraw();
            if (!useBloom)
                base.Draw(gameTime);

            GraphicsDevice.Clear(Color.Black);

            spriteBatch.Begin(SpriteSortMode.Texture, BlendState.Additive, null, null, null, null, scaleMatrix);
            EntityManager.Draw(spriteBatch);
            spriteBatch.End();

            if (useBloom)
                base.Draw(gameTime);

            // Draw user interface
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, null, null, null, null, scaleMatrix);

            spriteBatch.DrawString(Element.Font, "Lives: " + PlayerStatus.Lives, new Vector2(5), Color.White);
            DrawRightAlignedString("Score: " + PlayerStatus.Score, 5);
            DrawRightAlignedString("Multiplier: " + PlayerStatus.Multiplier, 35);

            // draw the custom mouse cursor
#if !ANDROID
            spriteBatch.Draw(Element.Pointer, Input.MousePosition, Color.White);
#endif

            if (PlayerStatus.IsGameOver)
            {
                string text = "Game Over\n" +
                    "Your Score: " + PlayerStatus.Score + "\n" +
                    "High Score: " + PlayerStatus.HighScore;

                Vector2 textSize = Element.Font.MeasureString(text);
                spriteBatch.DrawString(Element.Font, text, ScreenSize / 2 - textSize / 2, Color.White);
            }

            spriteBatch.End();

#if ANDROID
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            Input.DrawTouchControls(spriteBatch, touchControlTexture);
            spriteBatch.End();
#endif
        }

        private void DrawRightAlignedString(string text, float y)
        {
            var textWidth = Element.Font.MeasureString(text).X;
            spriteBatch.DrawString(Element.Font, text, new Vector2(ScreenSize.X - textWidth - 5, y), Color.White);
        }

        private void RecalculateScaleMatrix()
        {
            UpdateVirtualResolution();

            var viewport = GraphicsDevice.Viewport;
            float scale = Math.Min(viewport.Width / (float)virtualWidth, viewport.Height / (float)virtualHeight);
            if (scale <= 0)
                scale = 1f;

            int width = (int)(virtualWidth * scale);
            int height = (int)(virtualHeight * scale);
            int x = (viewport.Width - width) / 2;
            int y = (viewport.Height - height) / 2;

            renderViewport = new Rectangle(x, y, width, height);
            scaleMatrix = Matrix.CreateScale(scale, scale, 1f) * Matrix.CreateTranslation(renderViewport.X, renderViewport.Y, 0f);
        }

        public static Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            if (Instance == null || Instance.renderViewport.Width == 0 || Instance.renderViewport.Height == 0)
                return screenPosition;

            float x = (screenPosition.X - Instance.renderViewport.X) * VirtualWidth / Instance.renderViewport.Width;
            float y = (screenPosition.Y - Instance.renderViewport.Y) * VirtualHeight / Instance.renderViewport.Height;

            return Vector2.Clamp(new Vector2(x, y), Vector2.Zero, ScreenSize);
        }

        private void UpdateVirtualResolution()
        {
#if ANDROID
            virtualWidth = AndroidBaseVirtualWidth;

            var viewport = GraphicsDevice.Viewport;
            if (viewport.Width <= 0 || viewport.Height <= 0)
            {
                virtualHeight = AndroidMinimumVirtualHeight;
                return;
            }

            int shortSide = Math.Min(viewport.Width, viewport.Height);
            int longSide = Math.Max(viewport.Width, viewport.Height);
            float aspectRatio = longSide / (float)shortSide;
            virtualHeight = Math.Max(AndroidMinimumVirtualHeight, (int)Math.Round(virtualWidth * aspectRatio));
#else
            virtualWidth = DesktopVirtualWidth;
            virtualHeight = DesktopVirtualHeight;
#endif
        }

        private static int GetDefaultVirtualWidth()
        {
#if ANDROID
            return AndroidBaseVirtualWidth;
#else
            return DesktopVirtualWidth;
#endif
        }

        private static int GetDefaultVirtualHeight()
        {
#if ANDROID
            return AndroidMinimumVirtualHeight;
#else
            return DesktopVirtualHeight;
#endif
        }

#if ANDROID
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

                    float normalizedDistance = distance / radius;
                    float alpha = 1f - normalizedDistance * normalizedDistance;
                    data[y * size + x] = Color.White * alpha;
                }
            }

            texture.SetData(data);
            return texture;
        }
#endif
    }
}
