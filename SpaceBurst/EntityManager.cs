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
        private static List<PowerupPickup> powerups = new List<PowerupPickup>();
        private static List<Entity> addedEntities = new List<Entity>();

        private static bool isUpdating;
        private static bool queuedPlayerHullDestruction;
        private static readonly System.Random random = new System.Random();

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
            powerups.Clear();
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
            powerups = powerups.Where(x => !x.IsExpired).ToList();
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
            else if (entity is PowerupPickup powerup)
                powerups.Add(powerup);
        }

        private static void HandleCollisions()
        {
            HandlePlayerAndEnemies();
            HandleBullets();
            HandlePowerups();
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
                    bullet.RegisterImpact();
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
                bullet.RegisterImpact();
            }
        }

        private static void HandlePowerups()
        {
            if (Player1.Instance.IsDead)
                return;

            for (int i = 0; i < powerups.Count; i++)
            {
                PowerupPickup powerup = powerups[i];
                if (powerup.IsExpired)
                    continue;

                if (powerup.OverlapsPlayer())
                {
                    Player1.Instance.CollectPowerup();
                    powerup.IsExpired = true;
                }
            }
        }

        public static void SpawnImpactParticles(Vector2 position, Color color, int count, float speed, Vector2 bias)
        {
            int safeCount = System.Math.Max(0, count);
            for (int i = 0; i < safeCount; i++)
            {
                float angle = (float)(random.NextDouble() * MathHelper.TwoPi);
                Vector2 direction = new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle));
                Vector2 velocity = direction * speed * (0.35f + (float)random.NextDouble() * 0.75f) + bias;
                Add(new ImpactParticle(position, velocity, color, 3f + (float)random.NextDouble() * 3f, 0.2f + (float)random.NextDouble() * 0.28f));
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
