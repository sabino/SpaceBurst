using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceBurst
{
    static class Element
    {
        public static Texture2D Player { get; private set; }
        public static Texture2D Pointer { get; private set; }
        public static Texture2D Bullet { get; private set; }
        public static Texture2D Turret { get; private set; }
        public static Texture2D Destroyer { get; private set; }
        public static Texture2D Walker { get; private set; }
        public static Texture2D Portal { get; private set; }

        public static SpriteFont Font { get; private set; }

        public static void Load(ContentManager content)
        {
            Font = content.Load<SpriteFont>("Font");

            Player = content.Load<Texture2D>("Player");
            Pointer = content.Load<Texture2D>("Pointer");
            Bullet = content.Load<Texture2D>("Bullet");
            Destroyer = content.Load<Texture2D>("Destroyer");
            Walker = content.Load<Texture2D>("Walker");
            Turret = content.Load<Texture2D>("Turret");
            Portal = content.Load<Texture2D>("Portal");
        }
    }
}
