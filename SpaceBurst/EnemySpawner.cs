using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceBurst
{
    static class EnemySpawner
    {
        static Random rand = new Random();
        static float inverseSpawnChance = 90;
        static float inversePortalChance = 600;

        public static void Update()
        {
            if (!Player1.Instance.IsDead && EntityManager.Count < 200)
            {
                if (rand.Next((int)inverseSpawnChance) == 0)
                    EntityManager.Add(Enemy.CreateDestroyer(GetSpawnPosition()));

                if (rand.Next((int)inverseSpawnChance) == 0)
                    EntityManager.Add(Enemy.CreateWalker(GetSpawnPosition()));

                if (EntityManager.PortalCount < 5 && rand.Next((int)inversePortalChance) == 0)
                    EntityManager.Add(new Portal(GetSpawnPosition()));
            }

            // slowly increase the spawn rate as time progresses
            if (inverseSpawnChance > 30)
                inverseSpawnChance -= 0.005f;
        }

        private static Vector2 GetSpawnPosition()
        {
            Vector2 pos;
            do
            {
                pos = new Vector2(rand.Next((int)Game1.ScreenSize.X), rand.Next((int)Game1.ScreenSize.Y));
            }
            while (Vector2.DistanceSquared(pos, Player1.Instance.Position) < 250 * 250);

            return pos;
        }

        public static void Reset()
        {
            inverseSpawnChance = 90;
        }
    }
}
