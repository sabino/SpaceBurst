using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceBurst
{
    class Turret : Entity
    {

        private static Turret instance;
        public static Turret Instance
        {
            get
            {
                if (instance == null)
                    instance = new Turret();

                return instance;
            }
        }
        float angle;

        public Turret()
        {
            image = Element.Turret;
            Position = Game.ScreenSize / 2;
            Orientation = Velocity.ToAngle();
            Radius = 10;
        }

        public override void Update()
        {
            MouseState state = Mouse.GetState();
            Vector2 mouseLoc = new Vector2(state.X, state.Y);
            Vector2 direction = mouseLoc - Position;

            angle = (float)(Math.Atan2(direction.Y, direction.X));
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            spriteBatch.Draw(image, Player1.Instance.Position, null, color, angle, Size / 2f, 1f, 0, 0);
        }
    }
}
