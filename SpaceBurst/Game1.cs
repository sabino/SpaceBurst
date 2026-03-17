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
        public const int VirtualWidth = 800;
        public const int VirtualHeight = 600;

        // some helpful static properties
        public static Game1 Instance { get; private set; }
        public static Viewport Viewport { get { return new Viewport(0, 0, VirtualWidth, VirtualHeight); } }
        public static Vector2 ScreenSize { get { return new Vector2(VirtualWidth, VirtualHeight); } }
        public static GameTime GameTime { get; private set; }

        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        //BloomComponent bloom;
        Rectangle renderViewport;
        Matrix scaleMatrix;

        bool paused = false;
        bool useBloom = false;

        public Game1()
        {
            Instance = this;
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

#if ANDROID
            graphics.IsFullScreen = true;
            graphics.SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight;
#else
            graphics.PreferredBackBufferWidth = VirtualWidth;
            graphics.PreferredBackBufferHeight = VirtualHeight;
            graphics.IsFullScreen = false;
#endif

            //bloom = new BloomComponent(this);
            //Components.Add(bloom);
            //bloom.Settings = new BloomSettings(null, 0.25f, 4, 2, 1, 1.5f, 1);
        }

        protected override void Initialize()
        {
            RecalculateScaleMatrix();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
            Element.Load(Content);
            Sound.Load(Content);

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
        }

        private void DrawRightAlignedString(string text, float y)
        {
            var textWidth = Element.Font.MeasureString(text).X;
            spriteBatch.DrawString(Element.Font, text, new Vector2(ScreenSize.X - textWidth - 5, y), Color.White);
        }

        private void RecalculateScaleMatrix()
        {
            var viewport = GraphicsDevice.Viewport;
            float scale = Math.Min(viewport.Width / (float)VirtualWidth, viewport.Height / (float)VirtualHeight);
            if (scale <= 0)
                scale = 1f;

            int width = (int)(VirtualWidth * scale);
            int height = (int)(VirtualHeight * scale);
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
    }
}
