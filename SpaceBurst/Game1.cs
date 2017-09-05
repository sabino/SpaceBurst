//using BloomPostprocess;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace SpaceBurst
{

    public class Game1 : Microsoft.Xna.Framework.Game
    {
		// some helpful static properties
		public static Game1 Instance { get; private set; }
		public static Viewport Viewport { get { return Instance.GraphicsDevice.Viewport; } }
		public static Vector2 ScreenSize { get { return new Vector2(Viewport.Width, Viewport.Height); } }
		public static GameTime GameTime { get; private set; }

		GraphicsDeviceManager graphics;
		SpriteBatch spriteBatch;
        //BloomComponent bloom;

        bool paused = false;
        bool useBloom = false;

        public Game1()
		{
            Instance = this;
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            graphics.PreferredBackBufferWidth = 800;
            graphics.PreferredBackBufferHeight = 600;
            graphics.IsFullScreen = false;

            //bloom = new BloomComponent(this);
            //Components.Add(bloom);
            //bloom.Settings = new BloomSettings(null, 0.25f, 4, 2, 1, 1.5f, 1);
        }

		protected override void Initialize()
		{
			base.Initialize();

			EntityManager.Add(Player1.Instance);
            //EntityManager.Add(Turret.Instance);

            MediaPlayer.IsRepeating = true;
			MediaPlayer.Play(Sound.Music);
		}

		protected override void LoadContent()
		{
			spriteBatch = new SpriteBatch(GraphicsDevice);
			Element.Load(Content);
			Sound.Load(Content);
		}

		protected override void Update(GameTime gameTime)
		{
            GameTime = gameTime;
            Input.Update();

            // Allows the game to exit
            if (Input.WasButtonPressed(Buttons.Back) || Input.WasKeyPressed(Keys.Escape))
                this.Exit();

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

            spriteBatch.Begin(SpriteSortMode.Texture, BlendState.Additive);
            EntityManager.Draw(spriteBatch);
            spriteBatch.End();

            if (useBloom)
                base.Draw(gameTime);

            // Draw user interface
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive);

            spriteBatch.DrawString(Element.Font, "Lives: " + PlayerStatus.Lives, new Vector2(5), Color.White);
            DrawRightAlignedString("Score: " + PlayerStatus.Score, 5);
            DrawRightAlignedString("Multiplier: " + PlayerStatus.Multiplier, 35);

            // draw the custom mouse cursor
            spriteBatch.Draw(Element.Pointer, Input.MousePosition, Color.White);

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
	}
}
