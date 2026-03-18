using Microsoft.Xna.Framework;

namespace SpaceBurst
{
    class Bullet : Entity
    {
        public Vector2 PreviousPosition { get; private set; }

        public override bool IsFriendly
        {
            get { return Friendly; }
        }

        public bool Friendly { get; }
        public int Damage { get; }

        public Bullet(Vector2 position, Vector2 velocity, bool friendly, int damage)
        {
            Friendly = friendly;
            Damage = damage;
            Position = position;
            Velocity = velocity;
            PreviousPosition = position;
            Orientation = velocity == Vector2.Zero ? 0f : velocity.ToAngle();
            sprite = new ProceduralSpriteInstance(
                Game1.Instance.GraphicsDevice,
                friendly ? Element.PlayerBulletDefinition : Element.EnemyBulletDefinition);
        }

        public override void Update()
        {
            float deltaSeconds = (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            PreviousPosition = Position;
            Position += Velocity * deltaSeconds;

            if (Velocity != Vector2.Zero)
                Orientation = Velocity.ToAngle();

            Rectangle bounds = Bounds;
            Rectangle screenBounds = new Rectangle(-120, -120, Game1.VirtualWidth + 240, Game1.VirtualHeight + 240);
            if (!screenBounds.Intersects(bounds))
                IsExpired = true;
        }

        public bool TryGetImpactPoint(Entity target, out Vector2 impactPoint)
        {
            Vector2 delta = Position - PreviousPosition;
            int steps = delta == Vector2.Zero ? 1 : System.Math.Max(1, (int)(delta.Length() / 6f));

            for (int step = 0; step <= steps; step++)
            {
                float t = steps == 0 ? 1f : step / (float)steps;
                Vector2 sample = Vector2.Lerp(PreviousPosition, Position, t);
                if (target.ContainsPoint(sample))
                {
                    impactPoint = sample;
                    return true;
                }
            }

            impactPoint = Position;
            return false;
        }
    }
}
