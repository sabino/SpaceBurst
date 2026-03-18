using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceBurst
{
    sealed class ShockwaveEffect : Entity
    {
        private readonly float startRadius;
        private readonly float endRadius;
        private readonly float maxLife;
        private float life;

        public ShockwaveEffect(Vector2 position, Color color, float startRadius, float endRadius, float lifeSeconds)
        {
            Position = position;
            this.color = color;
            this.startRadius = startRadius;
            this.endRadius = endRadius;
            maxLife = lifeSeconds;
            life = lifeSeconds;
        }

        public override void Update()
        {
            float deltaSeconds = (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            life -= deltaSeconds;
            if (life <= 0f)
                IsExpired = true;
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            Texture2D radial = Game1.RadialTexture;
            if (radial == null)
                return;

            float normalized = maxLife <= 0f ? 1f : 1f - life / maxLife;
            float radius = MathHelper.Lerp(startRadius, endRadius, normalized);
            float alpha = MathHelper.Clamp(1f - normalized, 0f, 1f) * 0.35f;
            Color tint = color * alpha;

            spriteBatch.Draw(
                radial,
                Position,
                null,
                tint,
                0f,
                new Vector2(radial.Width / 2f, radial.Height / 2f),
                radius * 2f / radial.Width,
                SpriteEffects.None,
                0f);

            spriteBatch.Draw(
                radial,
                Position,
                null,
                tint * 0.35f,
                0f,
                new Vector2(radial.Width / 2f, radial.Height / 2f),
                radius * 1.45f / radial.Width,
                SpriteEffects.None,
                0f);
        }
    }
}
