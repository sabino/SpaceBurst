using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceBurst.Root
{
    static class Element
    {
        public static Texture2D Player { get; private set; }
        public static Texture2D Pointer { get; private set; }

        public static SpriteFont Font { get; private set; }

        public static void Load(ContentManager content)
        {
            Player = content.Load<Texture2D>("Player");
            Pointer = content.Load<Texture2D>("Pointer");
            Font = content.Load<SpriteFont>("Font");
        }
    }
}
