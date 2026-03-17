using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceBurst
{
    abstract class Entity
    {
        protected Texture2D image;
        protected Color color = Color.White;

        public Vector2 Position, Velocity;
        public float Orientation;
        public float RenderScale = 1f;
        public float Radius = 20;   // used for circular collision detection
        public bool IsExpired;      // true if the entity was destroyed and should be deleted.

        public Vector2 Size
        {
            get
            {
                return image == null ? Vector2.Zero : new Vector2(image.Width, image.Height) * RenderScale;
            }
        }

        public abstract void Update();

        public virtual void Draw(SpriteBatch spriteBatch)
        {
            spriteBatch.Draw(image, Position, null, color, Orientation, new Vector2(image.Width, image.Height) / 2f, RenderScale, 0, 0);
        }
    }
}
