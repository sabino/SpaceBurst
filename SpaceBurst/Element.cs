using SpaceBurst.RuntimeData;
using System.Collections.Generic;

namespace SpaceBurst
{
    static class Element
    {
        public static ProceduralSpriteDefinition PlayerBulletDefinition { get; private set; }
        public static ProceduralSpriteDefinition EnemyBulletDefinition { get; private set; }

        public static void Load()
        {
            PlayerBulletDefinition = WeaponCatalog.CreateProjectileDefinition(WeaponStyleId.Pulse, 0, true);

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
        }
    }
}
