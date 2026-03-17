using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceBurst
{
    static class EntityManager
    {
        static List<Entity> entities = new List<Entity>();
        static List<Enemy> enemies = new List<Enemy>();
        static List<Bullet> bullets = new List<Bullet>();
        static List<Portal> portals = new List<Portal>();

        public static IEnumerable<Portal> Portals { get { return portals; } }
        public static IEnumerable<Enemy> Enemies { get { return enemies; } }

        static bool isUpdating;
        static List<Entity> addedEntities = new List<Entity>();

        public static int Count { get { return entities.Count; } }
        public static int PortalCount { get { return portals.Count; } }
        public static int EnemyCount { get { return enemies.Count; } }
        public static bool HasHostiles { get { return enemies.Count > 0 || portals.Count > 0; } }

        public static void Add(Entity entity)
        {
            if (!isUpdating)
                AddEntity(entity);
            else
                addedEntities.Add(entity);
        }

        private static void AddEntity(Entity entity)
        {
            if (entities.Contains(entity))
                return;

            entities.Add(entity);
            if (entity is Bullet)
                bullets.Add(entity as Bullet);
            else if (entity is Enemy)
                enemies.Add(entity as Enemy);
            else if (entity is Portal)
                portals.Add(entity as Portal);
        }

        public static void Reset()
        {
            entities.Clear();
            enemies.Clear();
            bullets.Clear();
            portals.Clear();
            addedEntities.Clear();
            isUpdating = false;
        }

        public static void ClearHostiles()
        {
            enemies.ForEach(x => x.IsExpired = true);
            portals.ForEach(x => x.IsExpired = true);
        }

        public static void ClearHostilesNear(Vector2 position, float radius)
        {
            float radiusSquared = radius * radius;

            foreach (var enemy in enemies)
            {
                if (Vector2.DistanceSquared(enemy.Position, position) <= radiusSquared)
                    enemy.IsExpired = true;
            }

            foreach (var portal in portals)
            {
                if (Vector2.DistanceSquared(portal.Position, position) <= radiusSquared)
                    portal.IsExpired = true;
            }
        }

        public static void Update()
        {
            isUpdating = true;
            HandleCollisions();

            foreach (var entity in entities)
                entity.Update();

            isUpdating = false;

            foreach (var entity in addedEntities)
                AddEntity(entity);

            addedEntities.Clear();

            entities = entities.Where(x => !x.IsExpired).ToList();
            bullets = bullets.Where(x => !x.IsExpired).ToList();
            enemies = enemies.Where(x => !x.IsExpired).ToList();
            portals = portals.Where(x => !x.IsExpired).ToList();
        }

        static void HandleCollisions()
        {
            // handle collisions between enemies
            for (int i = 0; i < enemies.Count; i++)
                for (int j = i + 1; j < enemies.Count; j++)
                {
                    if (IsColliding(enemies[i], enemies[j]))
                    {
                        enemies[i].HandleCollision(enemies[j]);
                        enemies[j].HandleCollision(enemies[i]);
                    }
                }

            // handle collisions between bullets and enemies
            for (int i = 0; i < enemies.Count; i++)
                for (int j = 0; j < bullets.Count; j++)
                {
                    if (IsColliding(enemies[i], bullets[j]))
                    {
                        enemies[i].HandleBulletHit(bullets[j]);
                        bullets[j].IsExpired = true;
                    }
                }

            // handle collisions between the player and enemies
            if (!Player1.Instance.IsDead && !Player1.Instance.IsInvulnerable)
            {
                for (int i = 0; i < enemies.Count; i++)
                {
                    if (enemies[i].IsActive && IsColliding(Player1.Instance, enemies[i]))
                    {
                        Game1.Instance.HandlePlayerCollision();
                        break;
                    }
                }
            }

            // handle collisions with portals
            for (int i = 0; i < portals.Count; i++)
            {
                for (int j = 0; j < enemies.Count; j++)
                    if (enemies[j].IsActive && IsColliding(portals[i], enemies[j]))
                        enemies[j].HandleBulletHit(null);

                for (int j = 0; j < bullets.Count; j++)
                {
                    if (IsColliding(portals[i], bullets[j]))
                    {
                        bullets[j].IsExpired = true;
                        portals[i].WasShot();
                    }
                }

                if (!Player1.Instance.IsDead && !Player1.Instance.IsInvulnerable && IsColliding(Player1.Instance, portals[i]))
                {
                    Game1.Instance.HandlePlayerCollision();
                    break;
                }
            }
        }

        private static bool IsColliding(Entity a, Entity b)
        {
            float radius = a.Radius + b.Radius;
            return !a.IsExpired && !b.IsExpired && Vector2.DistanceSquared(a.Position, b.Position) < radius * radius;
        }

        public static IEnumerable<Entity> GetNearbyEntities(Vector2 position, float radius)
        {
            return entities.Where(x => Vector2.DistanceSquared(position, x.Position) < radius * radius);
        }

        public static void Draw(SpriteBatch spriteBatch)
        {
            foreach (var entity in entities)
                entity.Draw(spriteBatch);
        }
    }
}
