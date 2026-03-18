using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceBurst
{
    abstract class Entity
    {
        protected ProceduralSpriteInstance sprite;
        protected Color color = Color.White;

        public Vector2 Position;
        public Vector2 Velocity;
        public float Orientation;
        public float RenderScale = 1f;
        public bool IsExpired;

        public virtual bool IsFriendly
        {
            get { return false; }
        }

        public virtual bool IsDamageable
        {
            get { return false; }
        }

        public virtual int ContactDamage
        {
            get { return 0; }
        }

        public virtual float ApproximateRadius
        {
            get
            {
                Vector2 size = Size;
                return size == Vector2.Zero ? 0f : (size.X > size.Y ? size.X : size.Y) * 0.5f;
            }
        }

        public Vector2 Size
        {
            get { return sprite == null ? Vector2.Zero : sprite.WorldSize * RenderScale; }
        }

        public Rectangle Bounds
        {
            get
            {
                if (sprite == null)
                    return new Rectangle((int)Position.X - 1, (int)Position.Y - 1, 2, 2);

                return sprite.GetWorldBounds(Position, RenderScale);
            }
        }

        public abstract void Update();

        public virtual void Draw(SpriteBatch spriteBatch)
        {
            if (sprite != null)
                sprite.Draw(spriteBatch, Position, color, Orientation, RenderScale);
        }

        public virtual bool ContainsPoint(Vector2 worldPoint)
        {
            return sprite != null && sprite.ContainsWorldPoint(Position, worldPoint, RenderScale);
        }

        public virtual bool Overlaps(Entity other)
        {
            return sprite != null && other.sprite != null && sprite.Overlaps(Position, other.sprite, other.Position, RenderScale, other.RenderScale);
        }
    }
}
