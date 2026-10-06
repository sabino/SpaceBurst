using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceBurst
{
    // Presentation only: never change projectile size, damage, or collision geometry.
    internal static class CombatReadability
    {
        internal const float FriendlyFireOpacity = 0.62f;

        internal static bool IsFriendlyFire(Entity entity)
        {
            return entity.IsFriendly && (entity is Bullet || entity is BeamShot);
        }

        internal static float GetOpacity(Entity entity)
        {
            return IsFriendlyFire(entity) ? FriendlyFireOpacity : 1f;
        }

        internal static int GetLayer(Entity entity)
        {
            if (IsFriendlyFire(entity))
                return 0;
            if (entity is Player1)
                return 2;
            if (entity is Bullet || entity is BeamShot)
                return 3;
            return 1;
        }

        internal static void DrawThreats(SpriteBatch spriteBatch, IEnumerable<Entity> entities)
        {
            foreach (Entity entity in entities)
            {
                if (entity is not Bullet bullet || bullet.Friendly || bullet.IsExpired || bullet.SpriteInstance == null)
                    continue;

                ProceduralSpriteInstance sprite = bullet.SpriteInstance;
                float scale = bullet.RenderScale * bullet.PresentationScaleMultiplier;
                // A crisp dark contour separates small threats from bright friendly beams.
                sprite.Draw(spriteBatch, bullet.Position + new Vector2(-2, 0), Color.Black, bullet.Orientation, scale);
                sprite.Draw(spriteBatch, bullet.Position + new Vector2(2, 0), Color.Black, bullet.Orientation, scale);
                sprite.Draw(spriteBatch, bullet.Position + new Vector2(0, -2), Color.Black, bullet.Orientation, scale);
                sprite.Draw(spriteBatch, bullet.Position + new Vector2(0, 2), Color.Black, bullet.Orientation, scale);
                sprite.Draw(spriteBatch, bullet.Position, bullet.RenderTint, bullet.Orientation, scale);
            }
        }

        internal static IEnumerable<Entity> InDrawOrder(IEnumerable<Entity> entities)
        {
            // Preserve ordering within a layer without allocating/sorting a list each frame.
            for (int layer = 0; layer < 4; layer++)
            {
                foreach (Entity entity in entities)
                {
                    if (entity != null && !entity.IsExpired && GetLayer(entity) == layer)
                        yield return entity;
                }
            }
        }
    }
}
