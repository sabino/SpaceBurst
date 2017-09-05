using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceBurst
{
    class Portal : Entity
    {
        private static Random rand = new Random();

        private int hitpoints = 10;

        public Portal(Vector2 position)
        {
            image = Element.Portal;
            Position = position;
            Radius = image.Width / 2f;
        }

        public override void Update()
        {
            var entities = EntityManager.GetNearbyEntities(Position, 250);

            foreach (var entity in entities)
            {
                if (entity is Enemy && !(entity as Enemy).IsActive)
                    continue;

				// bullets are attracted by portals and everything else is repelled
				if (entity is Bullet)
				{
					entity.Velocity += (entity.Position - Position).ScaleTo(0.3f);
				}
				else
				{
					var dPos = Position - entity.Position;
					var length = dPos.Length();

					entity.Velocity += dPos.ScaleTo(MathHelper.Lerp(2, 0, length / 250f));
				}
					
                    
            }
        }

        public void WasShot()
        {
            hitpoints--;
            if (hitpoints <= 0)
                IsExpired = true;

            Sound.Explosion.Play(0.5f, rand.NextFloat(-0.2f, 0.2f), 0);
        }

        public void Kill()
        {
            hitpoints = 0;
            WasShot();
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            // make the size of the black hole pulsate
            float scale = 1 + 0.1f * (float)Math.Sin(10 * Game1.GameTime.TotalGameTime.TotalSeconds);
            spriteBatch.Draw(image, Position, null, color, Orientation, Size / 2f, scale, 0, 0);
        }
    }
}
