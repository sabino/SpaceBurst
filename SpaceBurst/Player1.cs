using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace SpaceBurst
{
    class Player1 : Entity
    {
        private const float DefaultMoveSpeed = 8f;
        private const float DefaultRespawnSeconds = 1.4f;
        private const float DefaultInvulnerabilitySeconds = 1.6f;

        private static Player1 instance;
        public static Player1 Instance
        {
            get
            {
                if (instance == null)
                    instance = new Player1();

                return instance;
            }
        }

        const int cooldownFrames = 6;
        int cooldownRemaining = 0;

        float respawnTimer;
        float invulnerabilityTimer;
        public bool IsDead { get { return respawnTimer > 0f; } }
        public bool IsInvulnerable { get { return IsDead || invulnerabilityTimer > 0f; } }

        static Random rand = new Random();

        private Player1()
        {
            image = Element.Player;
            Position = Game1.ScreenSize / 2;
            Radius = 14;
        }

        public override void Update()
        {
            if (IsDead)
            {
                respawnTimer -= (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
                if (respawnTimer <= 0f)
                    RestoreToCenter();
                return;
            }

            if (invulnerabilityTimer > 0f)
                invulnerabilityTimer -= (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;

            var aim = Input.GetAimDirection();

            if (aim.LengthSquared() > 0 && cooldownRemaining <= 0)
            {
                cooldownRemaining = cooldownFrames;
                float aimAngle = aim.ToAngle();
                Quaternion aimQuat = Quaternion.CreateFromYawPitchRoll(0, 0, aimAngle);

                float randomSpread = rand.NextFloat(-0.04f, 0.04f) + rand.NextFloat(-0.04f, 0.04f);
                Vector2 vel = MathUtil.FromPolar(aimAngle + randomSpread, 11f);

                Vector2 offset = Vector2.Transform(new Vector2(35, -8), aimQuat);
                EntityManager.Add(new Bullet(Position + offset, vel));

                offset = Vector2.Transform(new Vector2(35, 8), aimQuat);
                EntityManager.Add(new Bullet(Position + offset, vel));

                Sound.Shot.Play(0.2f, rand.NextFloat(-0.2f, 0.2f), 0);
            }

            if (cooldownRemaining > 0)
                cooldownRemaining--;

            Velocity = DefaultMoveSpeed * Input.GetMovementDirection();
            Position += Velocity;
            Position = Vector2.Clamp(Position, Size / 2, Game1.ScreenSize - Size / 2);

            if (Velocity.LengthSquared() > 0)
                Orientation = Velocity.ToAngle();
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!IsDead && (invulnerabilityTimer <= 0f || ((int)(Game1.GameTime.TotalGameTime.TotalSeconds * 12f) % 2 == 0)))
                base.Draw(spriteBatch);
        }

        public void ResetForLevel()
        {
            cooldownRemaining = 0;
            Velocity = Vector2.Zero;
            Position = Game1.ScreenSize / 2;
            respawnTimer = 0f;
            invulnerabilityTimer = DefaultInvulnerabilitySeconds;
        }

        public void StartRespawn(float delaySeconds)
        {
            cooldownRemaining = 0;
            Velocity = Vector2.Zero;
            respawnTimer = delaySeconds <= 0f ? DefaultRespawnSeconds : delaySeconds;
        }

        public void MakeInvulnerable(float durationSeconds)
        {
            invulnerabilityTimer = durationSeconds;
        }

        public void RestoreToCenter()
        {
            Position = Game1.ScreenSize / 2;
            Velocity = Vector2.Zero;
            respawnTimer = 0f;
            invulnerabilityTimer = DefaultInvulnerabilitySeconds;
        }
    }
}
