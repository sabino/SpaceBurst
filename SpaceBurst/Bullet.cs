using Microsoft.Xna.Framework;
using SpaceBurst.RuntimeData;
using System.Linq;

namespace SpaceBurst
{
    class Bullet : Entity
    {
        private float remainingLifetime;
        private readonly float homingStrength;

        public Vector2 PreviousPosition { get; private set; }

        public override bool IsFriendly
        {
            get { return Friendly; }
        }

        public bool Friendly { get; }
        public int Damage { get; }
        public int RemainingPierceHits { get; private set; }
        public ImpactProfileDefinition ImpactProfile { get; }

        public Bullet(
            Vector2 position,
            Vector2 velocity,
            bool friendly,
            int damage,
            ImpactProfileDefinition impactProfile,
            ProceduralSpriteDefinition spriteDefinition,
            int pierceCount,
            float lifetimeSeconds,
            float homingStrength,
            float renderScale = 1f)
        {
            Friendly = friendly;
            Damage = damage;
            ImpactProfile = impactProfile ?? new ImpactProfileDefinition();
            RemainingPierceHits = pierceCount;
            remainingLifetime = lifetimeSeconds;
            this.homingStrength = homingStrength;
            Position = position;
            Velocity = velocity;
            PreviousPosition = position;
            Orientation = velocity == Vector2.Zero ? 0f : velocity.ToAngle();
            RenderScale = renderScale;
            sprite = new ProceduralSpriteInstance(
                Game1.Instance.GraphicsDevice,
                spriteDefinition ?? (friendly ? Element.PlayerBulletDefinition : Element.EnemyBulletDefinition));
        }

        public override void Update()
        {
            float deltaSeconds = (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            remainingLifetime -= deltaSeconds;
            if (remainingLifetime <= 0f)
            {
                IsExpired = true;
                return;
            }

            PreviousPosition = Position;
            UpdateHoming(deltaSeconds);
            Position += Velocity * deltaSeconds;

            if (Velocity != Vector2.Zero)
                Orientation = Velocity.ToAngle();

            Rectangle bounds = Bounds;
            Rectangle screenBounds = new Rectangle(-160, -160, Game1.VirtualWidth + 320, Game1.VirtualHeight + 320);
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

        public void RegisterImpact()
        {
            if (RemainingPierceHits > 0)
            {
                RemainingPierceHits--;
                PreviousPosition = Position;
                if (Velocity != Vector2.Zero)
                    Position += Vector2.Normalize(Velocity) * 10f;
                return;
            }

            IsExpired = true;
        }

        private void UpdateHoming(float deltaSeconds)
        {
            if (homingStrength <= 0f || !Friendly)
                return;

            Enemy target = EntityManager.Enemies
                .Where(enemy => !enemy.IsExpired)
                .OrderBy(enemy => Vector2.DistanceSquared(enemy.Position, Position))
                .FirstOrDefault();
            if (target == null)
                return;

            Vector2 desired = target.Position - Position;
            if (desired == Vector2.Zero)
                return;

            desired.Normalize();
            float speed = Velocity.Length();
            Vector2 desiredVelocity = desired * speed;
            Velocity = Vector2.Lerp(Velocity, desiredVelocity, System.Math.Min(1f, homingStrength * deltaSeconds));
        }
    }
}
