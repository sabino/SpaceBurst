using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceBurst
{
    sealed class ImpactParticle : Entity
    {
        private readonly float drag;
        private readonly float maxLife;
        private readonly float size;
        private float life;

        public ImpactParticle(Vector2 position, Vector2 velocity, Color color, float size, float lifeSeconds, float drag = 2.5f)
        {
            Position = position;
            Velocity = velocity;
            this.color = color;
            this.size = size;
            this.drag = drag;
            maxLife = lifeSeconds;
            life = lifeSeconds;
        }

        public override void Update()
        {
            float deltaSeconds = (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            life -= deltaSeconds;
            if (life <= 0f)
            {
                IsExpired = true;
                return;
            }

            Velocity = Vector2.Lerp(Velocity, Vector2.Zero, System.Math.Min(1f, drag * deltaSeconds));
            Position += Velocity * deltaSeconds;
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            float alpha = maxLife <= 0f ? 0f : life / maxLife;
            spriteBatch.Draw(
                Game1.UiPixel,
                new Rectangle((int)Position.X, (int)Position.Y, (int)size, (int)size),
                color * alpha);
        }
    }
}
