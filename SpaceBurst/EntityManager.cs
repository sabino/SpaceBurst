using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;

namespace SpaceBurst
{
    static class EntityManager
    {
        private static List<Entity> entities = new List<Entity>();
        private static List<Enemy> enemies = new List<Enemy>();
        private static List<Bullet> bullets = new List<Bullet>();
        private static List<Entity> addedEntities = new List<Entity>();

        private static bool isUpdating;
        private static bool queuedPlayerHullDestruction;

        public static IEnumerable<Enemy> Enemies
        {
            get { return enemies; }
        }

        public static int Count
        {
            get { return entities.Count; }
        }

        public static bool HasHostiles
        {
            get { return enemies.Any(x => !x.IsExpired); }
        }

        public static void Add(Entity entity)
        {
            if (entity == null)
                return;

            if (!isUpdating)
                AddEntity(entity);
            else
                addedEntities.Add(entity);
        }

        public static void Reset()
        {
            entities.Clear();
            enemies.Clear();
            bullets.Clear();
            addedEntities.Clear();
            isUpdating = false;
            queuedPlayerHullDestruction = false;
        }

        public static void ClearHostiles()
        {
            foreach (Enemy enemy in enemies)
                enemy.IsExpired = true;
        }

        public static void ClearHostilesNear(Vector2 position, float radius)
        {
            float radiusSquared = radius * radius;
            foreach (Enemy enemy in enemies)
            {
                if (Vector2.DistanceSquared(enemy.Position, position) <= radiusSquared)
                    enemy.IsExpired = true;
            }
        }

        public static void Update()
        {
            isUpdating = true;

            for (int i = 0; i < entities.Count; i++)
            {
                if (!entities[i].IsExpired)
                    entities[i].Update();
            }

            HandleCollisions();

            isUpdating = false;

            foreach (Entity entity in addedEntities)
                AddEntity(entity);

            addedEntities.Clear();
            entities = entities.Where(x => !x.IsExpired).ToList();
            enemies = enemies.Where(x => !x.IsExpired).ToList();
            bullets = bullets.Where(x => !x.IsExpired).ToList();
        }

        public static bool ConsumePlayerHullDestruction()
        {
            if (!queuedPlayerHullDestruction)
                return false;

            queuedPlayerHullDestruction = false;
            return true;
        }

        public static void Draw(SpriteBatch spriteBatch)
        {
            foreach (Entity entity in entities)
                entity.Draw(spriteBatch);
        }

        private static void AddEntity(Entity entity)
        {
            if (entities.Contains(entity))
                return;

            entities.Add(entity);
            if (entity is Enemy enemy)
                enemies.Add(enemy);
            else if (entity is Bullet bullet)
                bullets.Add(bullet);
        }

        private static void HandleCollisions()
        {
            HandlePlayerAndEnemies();
            HandleBullets();
        }

        private static void HandlePlayerAndEnemies()
        {
            if (Player1.Instance.IsDead || Player1.Instance.IsInvulnerable)
                return;

            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy.IsExpired || !MayOverlap(Player1.Instance, enemy))
                    continue;

                if (!Player1.Instance.Overlaps(enemy))
                    continue;

                Vector2 delta = Player1.Instance.Position - enemy.Position;
                if (delta == Vector2.Zero)
                    delta = Vector2.UnitX;

                bool destroyed = Player1.Instance.ApplyDamage((Player1.Instance.Position + enemy.Position) * 0.5f, enemy.ContactDamage);
                Player1.Instance.ApplyKnockback(delta, 260f);
                enemy.ApplyContactHit(Player1.Instance.Position, 1);
                enemy.ApplyKnockback(-delta, 180f);

                if (destroyed)
                    queuedPlayerHullDestruction = true;

                if (destroyed || Player1.Instance.IsInvulnerable)
                    break;
            }
        }

        private static void HandleBullets()
        {
            for (int bulletIndex = 0; bulletIndex < bullets.Count; bulletIndex++)
            {
                Bullet bullet = bullets[bulletIndex];
                if (bullet.IsExpired)
                    continue;

                if (bullet.Friendly)
                    HandleFriendlyBullet(bullet);
                else
                    HandleEnemyBullet(bullet);
            }
        }

        private static void HandleFriendlyBullet(Bullet bullet)
        {
            for (int enemyIndex = 0; enemyIndex < enemies.Count; enemyIndex++)
            {
                Enemy enemy = enemies[enemyIndex];
                if (enemy.IsExpired || !MayOverlap(enemy, bullet))
                    continue;

                if (bullet.TryGetImpactPoint(enemy, out Vector2 impactPoint))
                {
                    enemy.ApplyBulletHit(bullet, impactPoint);
                    bullet.IsExpired = true;
                    return;
                }
            }
        }

        private static void HandleEnemyBullet(Bullet bullet)
        {
            if (Player1.Instance.IsDead || Player1.Instance.IsInvulnerable || !MayOverlap(Player1.Instance, bullet))
                return;

            if (bullet.TryGetImpactPoint(Player1.Instance, out Vector2 impactPoint))
            {
                bool destroyed = Player1.Instance.ApplyDamage(impactPoint, bullet.Damage);
                if (destroyed)
                    queuedPlayerHullDestruction = true;

                Player1.Instance.ApplyKnockback(Player1.Instance.Position - impactPoint, 220f);
                bullet.IsExpired = true;
            }
        }

        private static bool MayOverlap(Entity first, Entity second)
        {
            if (first.IsExpired || second.IsExpired)
                return false;

            float radius = first.ApproximateRadius + second.ApproximateRadius + 6f;
            return Vector2.DistanceSquared(first.Position, second.Position) <= radius * radius;
        }
    }
}
