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
            float alpha = MathHelper.Clamp(1f - normalized, 0f, 1f);
            VisualPreset preset = Game1.Instance?.VisualPreset ?? VisualPreset.Standard;
            Vector2 origin = new Vector2(radial.Width / 2f, radial.Height / 2f);
            float baseScale = radius * 2f / radial.Width;

            Color coreTint = color * (0.12f + 0.18f * alpha);
            Color ringTint = color * (preset == VisualPreset.Low ? 0.18f : 0.24f) * alpha;
            Color haloTint = Color.Lerp(color, Color.White, preset == VisualPreset.Neon ? 0.35f : 0.18f) * (preset == VisualPreset.Neon ? 0.20f : 0.12f) * alpha;

            spriteBatch.Draw(radial, Position, null, coreTint, 0f, origin, baseScale * 0.9f, SpriteEffects.None, 0f);
            spriteBatch.Draw(radial, Position, null, ringTint, 0f, origin, new Vector2(baseScale * (1.02f + normalized * 0.05f), baseScale * 0.44f), SpriteEffects.None, 0f);
            spriteBatch.Draw(radial, Position, null, ringTint * 0.75f, 0f, origin, new Vector2(baseScale * 0.82f, baseScale * 0.22f), SpriteEffects.None, 0f);
            if (preset != VisualPreset.Low)
                spriteBatch.Draw(radial, Position, null, ringTint * 0.55f, 0f, origin, new Vector2(baseScale * (1.14f + normalized * 0.16f), baseScale * 0.12f), SpriteEffects.None, 0f);

            if (preset != VisualPreset.Low)
            {
                spriteBatch.Draw(radial, Position + new Vector2(0f, -radius * 0.08f), null, haloTint, 0f, origin, new Vector2(baseScale * 1.14f, baseScale * 0.32f), SpriteEffects.None, 0f);
                spriteBatch.Draw(radial, Position + new Vector2(0f, radius * 0.06f), null, haloTint * 0.55f, 0f, origin, new Vector2(baseScale * 1.32f, baseScale * 0.18f), SpriteEffects.None, 0f);
            }

            if (preset == VisualPreset.Neon)
            {
                spriteBatch.Draw(radial, Position, null, haloTint * 0.95f, 0f, origin, new Vector2(baseScale * 1.48f, baseScale * 0.14f), SpriteEffects.None, 0f);
                spriteBatch.Draw(radial, Position + new Vector2(radius * 0.03f, -radius * 0.03f), null, Color.White * (0.08f * alpha), 0f, origin, baseScale * 0.68f, SpriteEffects.None, 0f);
                spriteBatch.Draw(radial, Position, null, color * (0.05f * alpha), 0f, origin, new Vector2(baseScale * (1.65f + normalized * 0.24f), baseScale * 0.08f), SpriteEffects.None, 0f);
            }
        }
    }
}
