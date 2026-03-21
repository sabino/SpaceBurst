using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading;

namespace SpaceBurst
{
    abstract class Entity
    {
        private static long nextEntityId;
        protected ProceduralSpriteInstance sprite;
        protected Color color = Color.White;
        private Vector3 combatPosition;
        private Vector3 combatVelocity;

        public long EntityId { get; private set; } = Interlocked.Increment(ref nextEntityId);
        public float Orientation;
        public float RenderScale = 1f;
        public bool IsExpired;

        public Vector3 CombatPosition
        {
            get { return combatPosition; }
            set { combatPosition = value; }
        }

        public Vector3 CombatVelocity
        {
            get { return combatVelocity; }
            set { combatVelocity = value; }
        }

        public float Travel
        {
            get { return combatPosition.X; }
            set { combatPosition.X = value; }
        }

        public float Altitude
        {
            get { return combatPosition.Y; }
            set { combatPosition.Y = value; }
        }

        public float LateralDepth
        {
            get { return combatPosition.Z; }
            set { combatPosition.Z = value; }
        }

        public float TravelVelocity
        {
            get { return combatVelocity.X; }
            set { combatVelocity.X = value; }
        }

        public float AltitudeVelocity
        {
            get { return combatVelocity.Y; }
            set { combatVelocity.Y = value; }
        }

        public float DepthVelocity
        {
            get { return combatVelocity.Z; }
            set { combatVelocity.Z = value; }
        }

        public Vector2 Position
        {
            get { return new Vector2(combatPosition.X, combatPosition.Y); }
            set
            {
                combatPosition.X = value.X;
                combatPosition.Y = value.Y;
            }
        }

        public Vector2 Velocity
        {
            get { return new Vector2(combatVelocity.X, combatVelocity.Y); }
            set
            {
                combatVelocity.X = value.X;
                combatVelocity.Y = value.Y;
            }
        }

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

        public virtual float ApproximateDepthRadius
        {
            get { return ApproximateRadius * 0.72f + 4f; }
        }

        public Vector2 Size
        {
            get { return sprite == null ? Vector2.Zero : sprite.WorldSize * RenderScale; }
        }

        internal ProceduralSpriteInstance SpriteInstance
        {
            get { return sprite; }
        }

        internal Color RenderTint
        {
            get { return color; }
        }

        internal virtual float PresentationScaleMultiplier
        {
            get { return 1f; }
        }

        internal virtual float PresentationDepthBias
        {
            get { return 0f; }
        }

        internal void RestoreEntityId(long entityId)
        {
            EntityId = entityId;
            long observed;
            do
            {
                observed = nextEntityId;
                if (observed >= entityId)
                    break;
            }
            while (Interlocked.CompareExchange(ref nextEntityId, entityId, observed) != observed);
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
            {
                if (Game1.Instance != null && Game1.Instance.EnableNeonOutlines)
                {
                    Color outline = ColorUtil.ParseHex(sprite.AccentColorHex, color) * 0.16f;
                    sprite.Draw(spriteBatch, Position + new Vector2(-2f, 0f), outline, Orientation, RenderScale);
                    sprite.Draw(spriteBatch, Position + new Vector2(2f, 0f), outline, Orientation, RenderScale);
                    sprite.Draw(spriteBatch, Position + new Vector2(0f, -2f), outline, Orientation, RenderScale);
                    sprite.Draw(spriteBatch, Position + new Vector2(0f, 2f), outline, Orientation, RenderScale);
                }

                sprite.Draw(spriteBatch, Position, color, Orientation, RenderScale);
            }
        }

        public virtual bool ContainsPoint(Vector2 worldPoint)
        {
            return sprite != null && sprite.ContainsWorldPoint(Position, worldPoint, RenderScale);
        }

        public virtual bool ContainsCombatPoint(Vector3 combatPoint)
        {
            if (MathHelper.Distance(combatPoint.Z, LateralDepth) > ApproximateDepthRadius)
                return false;

            return ContainsPoint(new Vector2(combatPoint.X, combatPoint.Y));
        }

        public virtual bool Overlaps(Entity other)
        {
            return sprite != null && other.sprite != null && sprite.Overlaps(Position, other.sprite, other.Position, RenderScale, other.RenderScale);
        }

        public virtual bool OverlapsCombat(Entity other)
        {
            if (other == null)
                return false;

            float depthRadius = ApproximateDepthRadius + other.ApproximateDepthRadius;
            if (MathHelper.Distance(LateralDepth, other.LateralDepth) > depthRadius)
                return false;

            return Overlaps(other);
        }
    }
}
