using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using SpaceBurst.RuntimeData;
using System.Collections.Generic;

namespace SpaceBurst
{
    static class Element
    {
        public static SpriteFont Font { get; private set; }

        public static ProceduralSpriteDefinition PlayerHullDefinition { get; private set; }
        public static ProceduralSpriteDefinition PlayerCannonDefinition { get; private set; }
        public static ProceduralSpriteDefinition PlayerBulletDefinition { get; private set; }
        public static ProceduralSpriteDefinition EnemyBulletDefinition { get; private set; }

        public static DamageMaskDefinition PlayerDamageMask { get; private set; }

        public static void Load(ContentManager content)
        {
            Font = content.Load<SpriteFont>("Font");

            PlayerHullDefinition = new ProceduralSpriteDefinition
            {
                Id = "PlayerHull",
                PixelScale = 5,
                PrimaryColor = "#D7F5FF",
                SecondaryColor = "#5AAFCB",
                AccentColor = "#FFB347",
                Rows = new List<string>
                {
                    ".....##......",
                    "...######....",
                    "..###++###...",
                    ".###++++###..",
                    "###++CC++###.",
                    "###++CC++##**",
                    "###++CC++###.",
                    ".###++++###..",
                    "..###++###...",
                    "...######....",
                    ".....##......",
                },
                VitalCore = new VitalCoreMaskDefinition
                {
                    Rows = new List<string>
                    {
                        ".............",
                        ".............",
                        ".............",
                        ".............",
                        ".....XX......",
                        ".....XX......",
                        ".....XX......",
                        ".............",
                        ".............",
                        ".............",
                        ".............",
                    }
                }
            };

            PlayerCannonDefinition = new ProceduralSpriteDefinition
            {
                Id = "PlayerCannon",
                PixelScale = 5,
                PrimaryColor = "#F7FBFF",
                SecondaryColor = "#A3C7DB",
                AccentColor = "#FFB347",
                Rows = new List<string>
                {
                    "...##.....",
                    ".######...",
                    "##########",
                    ".######...",
                    "...##.....",
                }
            };

            PlayerBulletDefinition = new ProceduralSpriteDefinition
            {
                Id = "PlayerBullet",
                PixelScale = 4,
                PrimaryColor = "#FFF4DA",
                SecondaryColor = "#FFB347",
                AccentColor = "#FF7A59",
                Rows = new List<string>
                {
                    "##",
                    "##",
                }
            };

            EnemyBulletDefinition = new ProceduralSpriteDefinition
            {
                Id = "EnemyBullet",
                PixelScale = 4,
                PrimaryColor = "#FFBFA8",
                SecondaryColor = "#F26D5B",
                AccentColor = "#FFD166",
                Rows = new List<string>
                {
                    "##",
                    "##",
                }
            };

            PlayerDamageMask = new DamageMaskDefinition
            {
                ContactDamage = 3,
                ProjectileDamage = 2,
                DamageRadius = 1,
                IntegrityThresholdPercent = 15,
            };
        }
    }
}
