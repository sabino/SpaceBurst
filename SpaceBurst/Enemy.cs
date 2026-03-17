using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceBurst.RuntimeData;

namespace SpaceBurst
{
    class Enemy : Entity
    {
        protected readonly EnemyArchetypeDefinition archetype;
        protected readonly Vector2 spawnPoint;
        protected readonly Vector2 anchorPoint;
        protected readonly PathType pathType;
        protected readonly float speedMultiplier;
        protected readonly int formationIndex;
        protected readonly float spawnDelaySeconds;
        protected readonly float orbitRadius;
        protected readonly float orbitPhase;

        protected float ageSeconds;
        protected float spawnCountdownSeconds;
        protected int hitPoints;
        protected float laneDirection = 1f;

        public static Random Random { get; } = new Random();

        public bool IsActive
        {
            get { return spawnCountdownSeconds <= 0f; }
        }

        public virtual bool IsBoss
        {
            get { return false; }
        }

        public virtual int PointValue { get; protected set; }

        public int HitPoints
        {
            get { return hitPoints; }
        }

        public Enemy(
            EnemyArchetypeDefinition archetype,
            Vector2 spawnPoint,
            Vector2 anchorPoint,
            PathType pathType,
            int formationIndex,
            float speedMultiplier)
        {
            this.archetype = archetype;
            this.spawnPoint = spawnPoint;
            this.anchorPoint = anchorPoint;
            this.pathType = pathType;
            this.formationIndex = formationIndex;
            this.speedMultiplier = speedMultiplier;
            spawnDelaySeconds = archetype.SpawnDelaySeconds;
            spawnCountdownSeconds = spawnDelaySeconds;
            hitPoints = archetype.HitPoints;
            PointValue = archetype.ScoreValue;
            orbitRadius = 48f + formationIndex * 10f;
            orbitPhase = formationIndex * 0.7f;

            image = Element.GetTexture(archetype.Texture);
            Position = spawnPoint;
            color = Color.Transparent;
            RenderScale = archetype.RenderScale;
            Radius = archetype.CollisionRadius;

            laneDirection = spawnPoint.X <= Game1.ScreenSize.X / 2f ? 1f : -1f;
        }

        public override void Update()
        {
            float deltaSeconds = (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            float frameScale = deltaSeconds * 60f;

            if (!IsActive)
            {
                spawnCountdownSeconds -= deltaSeconds;
                float fade = 1f - MathHelper.Clamp(spawnCountdownSeconds / Math.Max(0.001f, spawnDelaySeconds), 0f, 1f);
                color = Color.White * fade;
                return;
            }

            ageSeconds += deltaSeconds;
            UpdateMovement(frameScale);
            Position += Velocity * frameScale;
            Position = Vector2.Clamp(Position, Size / 2f, Game1.ScreenSize - Size / 2f);
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!IsActive)
            {
                float progress = 1f - MathHelper.Clamp(spawnCountdownSeconds / Math.Max(0.001f, spawnDelaySeconds), 0f, 1f);
                float burstScale = RenderScale * MathHelper.Lerp(1.6f, 1f, progress);
                spriteBatch.Draw(
                    image,
                    Position,
                    null,
                    Color.White * progress,
                    Orientation,
                    new Vector2(image.Width, image.Height) / 2f,
                    burstScale,
                    0,
                    0f);
            }

            base.Draw(spriteBatch);
        }

        public virtual void HandleCollision(Enemy other)
        {
            Vector2 separation = Position - other.Position;
            if (separation != Vector2.Zero)
                Velocity += separation.ScaleTo(0.4f);
        }

        public virtual void HandleBulletHit(Bullet bullet)
        {
            hitPoints--;
            color = Color.White;
            if (hitPoints <= 0)
                Destroy();
        }

        protected virtual void Destroy()
        {
            IsExpired = true;
            Sound.Explosion.Play(0.45f, Random.NextFloat(-0.2f, 0.2f), 0);
            PlayerStatus.AddPoints(PointValue);
            PlayerStatus.IncreaseMultiplier();
        }

        protected virtual void UpdateMovement(float frameScale)
        {
            Vector2 desiredVelocity;
            switch (pathType)
            {
                case PathType.Swoop:
                    desiredVelocity = GetSwoopVelocity();
                    break;
                case PathType.LaneSweep:
                    desiredVelocity = GetLaneSweepVelocity();
                    break;
                case PathType.ChaseAfterDelay:
                    desiredVelocity = GetChaseVelocity();
                    break;
                case PathType.OrbitAnchor:
                    desiredVelocity = GetOrbitVelocity();
                    break;
                default:
                    desiredVelocity = MoveToward(anchorPoint, archetype.Speed * speedMultiplier);
                    break;
            }

            Velocity = Vector2.Lerp(Velocity, desiredVelocity, 0.16f * frameScale);
            if (Velocity.LengthSquared() > 0.001f)
                Orientation = Velocity.ToAngle();
        }

        protected Vector2 MoveToward(Vector2 target, float speed)
        {
            Vector2 delta = target - Position;
            if (delta == Vector2.Zero)
                return Vector2.Zero;

            if (delta.LengthSquared() < 64f)
                return delta * 0.15f;

            return delta.ScaleTo(speed);
        }

        private Vector2 GetSwoopVelocity()
        {
            Vector2 toAnchor = anchorPoint - Position;
            if (toAnchor == Vector2.Zero)
                return Vector2.Zero;

            Vector2 direction = Vector2.Normalize(toAnchor);
            Vector2 perpendicular = new Vector2(-direction.Y, direction.X);
            float sway = (float)Math.Sin(ageSeconds * 4.5f + formationIndex * 0.8f);
            return direction * archetype.Speed * speedMultiplier + perpendicular * sway * archetype.Speed * 0.85f;
        }

        private Vector2 GetLaneSweepVelocity()
        {
            if (Math.Abs(Position.Y - anchorPoint.Y) > 18f)
                return MoveToward(anchorPoint, archetype.Speed * speedMultiplier);

            float horizontalSpeed = archetype.Speed * speedMultiplier;
            float leftBound = Size.X / 2f + 28f;
            float rightBound = Game1.ScreenSize.X - Size.X / 2f - 28f;

            if (Position.X <= leftBound)
                laneDirection = 1f;
            else if (Position.X >= rightBound)
                laneDirection = -1f;

            return new Vector2(horizontalSpeed * laneDirection, (anchorPoint.Y - Position.Y) * 0.04f);
        }

        private Vector2 GetChaseVelocity()
        {
            if (ageSeconds < 1.25f || Vector2.DistanceSquared(Position, anchorPoint) > 6400f)
                return MoveToward(anchorPoint, archetype.Speed * speedMultiplier);

            return MoveToward(Player1.Instance.Position, archetype.Speed * speedMultiplier * 0.95f);
        }

        private Vector2 GetOrbitVelocity()
        {
            Vector2 orbitTarget = anchorPoint + MathUtil.FromPolar(orbitPhase + ageSeconds * 1.8f, orbitRadius);
            return MoveToward(orbitTarget, archetype.Speed * speedMultiplier);
        }
    }
}
