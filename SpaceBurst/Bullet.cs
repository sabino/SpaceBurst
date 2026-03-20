using Microsoft.Xna.Framework;
using SpaceBurst.RuntimeData;
using System;
using System.Linq;

namespace SpaceBurst
{
    class Bullet : Entity
    {
        private float remainingLifetime;
        private float trailTimer;
        private readonly float homingStrength;

        public Vector2 PreviousPosition { get; private set; }
        public Vector3 PreviousCombatPosition { get; private set; }

        public override bool IsFriendly
        {
            get { return Friendly; }
        }

        public bool Friendly { get; }
        public int Damage { get; }
        public int RemainingPierceHits { get; private set; }
        public ImpactProfileDefinition ImpactProfile { get; }
        public ProjectileBehavior ProjectileBehavior { get; }
        public TrailFxStyle TrailFxStyle { get; }
        public ImpactFxStyle ImpactFxStyle { get; }
        public float ExplosionRadius { get; }
        public int ChainCount { get; }
        public float RemainingLifetime
        {
            get { return remainingLifetime; }
        }

        public float HomingStrength
        {
            get { return homingStrength; }
        }

        public float HomingDelayRemaining { get; private set; }

        public ProceduralSpriteDefinition SpriteDefinition
        {
            get { return sprite?.Definition; }
        }

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
            float renderScale = 1f,
            ProjectileBehavior projectileBehavior = ProjectileBehavior.Bolt,
            TrailFxStyle trailFxStyle = TrailFxStyle.None,
            ImpactFxStyle impactFxStyle = ImpactFxStyle.Standard,
            float explosionRadius = 0f,
            int chainCount = 0,
            float homingDelaySeconds = 0f,
            Vector3? combatPosition = null,
            Vector3? combatVelocity = null)
        {
            Friendly = friendly;
            Damage = damage;
            ImpactProfile = impactProfile ?? new ImpactProfileDefinition();
            RemainingPierceHits = pierceCount;
            remainingLifetime = lifetimeSeconds;
            this.homingStrength = homingStrength;
            HomingDelayRemaining = homingDelaySeconds;
            ProjectileBehavior = projectileBehavior;
            TrailFxStyle = trailFxStyle;
            ImpactFxStyle = impactFxStyle;
            ExplosionRadius = explosionRadius;
            ChainCount = chainCount;
            CombatPosition = combatPosition ?? new Vector3(position.X, position.Y, 0f);
            CombatVelocity = combatVelocity ?? new Vector3(velocity.X, velocity.Y, 0f);
            PreviousPosition = position;
            PreviousCombatPosition = CombatPosition;
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
            PreviousCombatPosition = CombatPosition;
            UpdateBehavior(deltaSeconds);
            CombatPosition += CombatVelocity * deltaSeconds;
            EmitTrail(deltaSeconds);

            if (Velocity != Vector2.Zero)
                Orientation = Velocity.ToAngle();

            Rectangle bounds = Bounds;
            Rectangle screenBounds = new Rectangle(-240, -240, Game1.VirtualWidth + 480, Game1.VirtualHeight + 480);
            if (!screenBounds.Intersects(bounds) && MathF.Abs(LateralDepth) > ApproximateDepthRadius + 420f)
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

        public bool TryGetCombatImpactPoint(Entity target, out Vector3 impactPoint)
        {
            Vector3 delta = CombatPosition - PreviousCombatPosition;
            int steps = delta == Vector3.Zero ? 1 : System.Math.Max(1, (int)(delta.Length() / 8f));

            for (int step = 0; step <= steps; step++)
            {
                float t = steps == 0 ? 1f : step / (float)steps;
                Vector3 sample = Vector3.Lerp(PreviousCombatPosition, CombatPosition, t);
                if (target.ContainsCombatPoint(sample))
                {
                    impactPoint = sample;
                    return true;
                }
            }

            impactPoint = CombatPosition;
            return false;
        }

        public void RegisterImpact()
        {
            if (RemainingPierceHits > 0)
            {
                RemainingPierceHits--;
                PreviousPosition = Position;
                PreviousCombatPosition = CombatPosition;
                if (CombatVelocity != Vector3.Zero)
                    CombatPosition += Vector3.Normalize(CombatVelocity) * 10f;
                return;
            }

            IsExpired = true;
        }

        public BulletSnapshotData CaptureSnapshot()
        {
            return new BulletSnapshotData
            {
                EntityId = EntityId,
                Position = new Vector2Data(Position.X, Position.Y),
                PreviousPosition = new Vector2Data(PreviousPosition.X, PreviousPosition.Y),
                Velocity = new Vector2Data(Velocity.X, Velocity.Y),
                CombatPosition = new Vector3Data(CombatPosition.X, CombatPosition.Y, CombatPosition.Z),
                PreviousCombatPosition = new Vector3Data(PreviousCombatPosition.X, PreviousCombatPosition.Y, PreviousCombatPosition.Z),
                CombatVelocity = new Vector3Data(CombatVelocity.X, CombatVelocity.Y, CombatVelocity.Z),
                Friendly = Friendly,
                Damage = Damage,
                RemainingPierceHits = RemainingPierceHits,
                RemainingLifetime = remainingLifetime,
                HomingStrength = homingStrength,
                HomingDelayRemaining = HomingDelayRemaining,
                ExplosionRadius = ExplosionRadius,
                ChainCount = ChainCount,
                RenderScale = RenderScale,
                ProjectileBehavior = ProjectileBehavior,
                TrailFxStyle = TrailFxStyle,
                ImpactFxStyle = ImpactFxStyle,
                SpriteDefinition = SpriteDefinition,
                ImpactProfile = ImpactProfile,
            };
        }

        public static Bullet FromSnapshot(BulletSnapshotData snapshot)
        {
            if (snapshot == null)
                return null;

            var bullet = new Bullet(
                new Vector2(snapshot.Position.X, snapshot.Position.Y),
                new Vector2(snapshot.Velocity.X, snapshot.Velocity.Y),
                snapshot.Friendly,
                snapshot.Damage,
                snapshot.ImpactProfile,
                snapshot.SpriteDefinition,
                snapshot.RemainingPierceHits,
                snapshot.RemainingLifetime,
                snapshot.HomingStrength,
                snapshot.RenderScale,
                snapshot.ProjectileBehavior,
                snapshot.TrailFxStyle,
                snapshot.ImpactFxStyle,
                snapshot.ExplosionRadius,
                snapshot.ChainCount,
                snapshot.HomingDelayRemaining,
                snapshot.CombatPosition == null ? null : new Vector3(snapshot.CombatPosition.X, snapshot.CombatPosition.Y, snapshot.CombatPosition.Z),
                snapshot.CombatVelocity == null ? null : new Vector3(snapshot.CombatVelocity.X, snapshot.CombatVelocity.Y, snapshot.CombatVelocity.Z));
            bullet.PreviousPosition = new Vector2(snapshot.PreviousPosition.X, snapshot.PreviousPosition.Y);
            bullet.PreviousCombatPosition = snapshot.PreviousCombatPosition == null
                ? bullet.CombatPosition
                : new Vector3(snapshot.PreviousCombatPosition.X, snapshot.PreviousCombatPosition.Y, snapshot.PreviousCombatPosition.Z);
            bullet.RestoreEntityId(snapshot.EntityId);
            return bullet;
        }

        private void UpdateBehavior(float deltaSeconds)
        {
            if (HomingDelayRemaining > 0f)
                HomingDelayRemaining -= deltaSeconds;

            switch (ProjectileBehavior)
            {
                case ProjectileBehavior.Missile:
                    Velocity *= 1f - MathHelper.Clamp(deltaSeconds * 0.08f, 0f, 0.06f);
                    UpdateHoming(deltaSeconds, 5.4f);
                    break;

                case ProjectileBehavior.PlasmaOrb:
                    RenderScale = MathHelper.Lerp(RenderScale, 1.04f + MathF.Sin((float)Game1.GameTime.TotalGameTime.TotalSeconds * 8f) * 0.06f, Math.Min(1f, deltaSeconds * 6f));
                    break;

                case ProjectileBehavior.ArcBolt:
                    Velocity += new Vector2(0f, MathF.Sin((float)Game1.GameTime.TotalGameTime.TotalSeconds * 24f + Position.X * 0.02f) * 14f * deltaSeconds);
                    break;

                case ProjectileBehavior.BladeWave:
                    Velocity *= 1f - MathHelper.Clamp(deltaSeconds * 0.22f, 0f, 0.18f);
                    break;

                case ProjectileBehavior.ShieldPulse:
                    Velocity *= 1f - MathHelper.Clamp(deltaSeconds * 0.18f, 0f, 0.14f);
                    break;

                default:
                    UpdateHoming(deltaSeconds, 3.6f);
                    break;
            }
        }

        private void UpdateHoming(float deltaSeconds, float responsiveness)
        {
            if (homingStrength <= 0f || HomingDelayRemaining > 0f)
                return;

            Vector3? desiredDirection = Friendly
                ? GetEnemySeekDirection()
                : GetPlayerSeekDirection();

            if (!desiredDirection.HasValue)
                return;

            float speed = CombatVelocity.Length();
            if (speed <= 0f)
                return;

            Vector3 desiredVelocity = desiredDirection.Value * speed;
            CombatVelocity = Vector3.Lerp(CombatVelocity, desiredVelocity, System.Math.Min(1f, homingStrength * responsiveness * deltaSeconds));
        }

        private Vector3? GetEnemySeekDirection()
        {
            Enemy target = EntityManager.Enemies
                .Where(enemy => !enemy.IsExpired)
                .OrderBy(enemy => Vector3.DistanceSquared(enemy.CombatPosition, CombatPosition))
                .FirstOrDefault();
            if (target == null)
                return null;

            Vector3 desired = target.CombatPosition - CombatPosition;
            if (desired == Vector3.Zero)
                return null;

            desired.Normalize();
            return desired;
        }

        private Vector3? GetPlayerSeekDirection()
        {
            if (Player1.Instance == null || Player1.Instance.IsDead)
                return null;

            Vector3 desired = Player1.Instance.CombatPosition - CombatPosition;
            if (desired == Vector3.Zero)
                return null;

            desired.Normalize();
            return desired;
        }

        private void EmitTrail(float deltaSeconds)
        {
            if (Game1.Instance == null || Game1.Instance.VisualPreset == VisualPreset.Low || TrailFxStyle == TrailFxStyle.None)
                return;

            trailTimer -= deltaSeconds;
            if (trailTimer > 0f)
                return;

            trailTimer = TrailFxStyle == TrailFxStyle.Beam ? 0.02f : 0.05f;
            Color tint = ResolveTrailColor();

            switch (TrailFxStyle)
            {
                case TrailFxStyle.Smoke:
                    EntityManager.SpawnImpactParticles(Position, tint * 0.8f, 1, 32f, -Velocity * 0.02f);
                    break;

                case TrailFxStyle.Electric:
                    EntityManager.SpawnFlash(Position, tint * 0.22f, 10f, 2f, 0.08f);
                    break;

                case TrailFxStyle.Plasma:
                case TrailFxStyle.Shield:
                case TrailFxStyle.Neon:
                case TrailFxStyle.Streak:
                    EntityManager.SpawnFlash(Position, tint * 0.18f, 8f * RenderScale, 1f, 0.06f);
                    break;
            }
        }

        private Color ResolveTrailColor()
        {
            if (sprite == null)
                return Color.White;

            switch (TrailFxStyle)
            {
                case TrailFxStyle.Smoke:
                    return new Color(180, 190, 210);
                case TrailFxStyle.Electric:
                    return ColorUtil.ParseHex(sprite.AccentColorHex, Color.Cyan);
                case TrailFxStyle.Plasma:
                    return ColorUtil.ParseHex(sprite.SecondaryColorHex, Color.Violet);
                case TrailFxStyle.Shield:
                    return ColorUtil.ParseHex(sprite.PrimaryColorHex, Color.Goldenrod);
                default:
                    return ColorUtil.ParseHex(sprite.AccentColorHex, Color.White);
            }
        }
    }
}
