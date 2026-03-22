using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceBurst.RuntimeData;
using System.Collections.Generic;
using System.Linq;
using System;

namespace SpaceBurst
{
    static class EntityManager
    {
        private static List<Entity> entities = new List<Entity>();
        private static List<Enemy> enemies = new List<Enemy>();
        private static List<Bullet> bullets = new List<Bullet>();
        private static List<BeamShot> beams = new List<BeamShot>();
        private static List<PowerupPickup> powerups = new List<PowerupPickup>();
        private static List<Entity> addedEntities = new List<Entity>();
        private static readonly EntitySpatialGrid<Enemy> enemyGrid = new EntitySpatialGrid<Enemy>(160f);

        private static bool isUpdating;
        private static bool queuedPlayerHullDestruction;
        private static readonly System.Random random = new System.Random();

        public static IEnumerable<Enemy> Enemies
        {
            get { return enemies; }
        }

        public static IEnumerable<Entity> AllEntities
        {
            get { return entities; }
        }

        public static IEnumerable<PowerupPickup> Powerups
        {
            get { return powerups; }
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
            beams.Clear();
            powerups.Clear();
            addedEntities.Clear();
            isUpdating = false;
            queuedPlayerHullDestruction = false;
            enemyGrid.Clear();
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

            RebuildSpatialIndex();
            HandleCollisions();

            isUpdating = false;

            foreach (Entity entity in addedEntities)
                AddEntity(entity);

            addedEntities.Clear();
            entities = entities.Where(x => !x.IsExpired).ToList();
            enemies = enemies.Where(x => !x.IsExpired).ToList();
            bullets = bullets.Where(x => !x.IsExpired).ToList();
            beams = beams.Where(x => !x.IsExpired).ToList();
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

        public static List<EnemySnapshotData> CaptureEnemies()
        {
            return enemies.Where(enemy => !enemy.IsExpired).Select(enemy => enemy.CaptureSnapshot()).ToList();
        }

        public static List<BulletSnapshotData> CaptureBullets()
        {
            return bullets.Where(bullet => !bullet.IsExpired).Select(bullet => bullet.CaptureSnapshot()).ToList();
        }

        public static List<BeamSnapshotData> CaptureBeams()
        {
            return beams.Where(beam => !beam.IsExpired).Select(beam => beam.CaptureSnapshot()).ToList();
        }

        public static List<PowerupSnapshotData> CapturePowerups()
        {
            return powerups.Where(powerup => !powerup.IsExpired).Select(powerup => powerup.CaptureSnapshot()).ToList();
        }

        public static void RestoreEnemies(IEnumerable<EnemySnapshotData> snapshots, CampaignRepository repository, BossDefinition bossDefinition)
        {
            if (snapshots == null || repository?.ArchetypesById == null)
                return;

            foreach (EnemySnapshotData snapshot in snapshots)
            {
                if (!repository.ArchetypesById.TryGetValue(snapshot.ArchetypeId, out EnemyArchetypeDefinition archetype))
                    continue;

                Enemy enemy = Enemy.FromSnapshot(archetype, snapshot, bossDefinition);
                Add(enemy);
            }
        }

        public static void RestoreBullets(IEnumerable<BulletSnapshotData> snapshots)
        {
            if (snapshots == null)
                return;

            foreach (BulletSnapshotData snapshot in snapshots)
                Add(Bullet.FromSnapshot(snapshot));
        }

        public static void RestoreBeams(IEnumerable<BeamSnapshotData> snapshots)
        {
            if (snapshots == null)
                return;

            foreach (BeamSnapshotData snapshot in snapshots)
                Add(BeamShot.FromSnapshot(snapshot));
        }

        public static void ClearFriendlyBeams()
        {
            for (int i = 0; i < beams.Count; i++)
            {
                if (beams[i].IsFriendly)
                    beams[i].IsExpired = true;
            }
        }

        public static void RestorePowerups(IEnumerable<PowerupSnapshotData> snapshots)
        {
            if (snapshots == null)
                return;

            foreach (PowerupSnapshotData snapshot in snapshots)
                Add(PowerupPickup.FromSnapshot(snapshot));
        }

        public static IEnumerable<Enemy> QueryNearbyEnemies(Vector2 position, float radius)
        {
            return enemyGrid.Query(position, radius);
        }

        public static Enemy FindNearestEnemy(Vector3 position, float radius = 260f)
        {
            Enemy best = null;
            float bestDistanceSquared = radius * radius;
            foreach (Enemy enemy in enemyGrid.Query(new Vector2(position.X, position.Y), radius))
            {
                if (enemy == null || enemy.IsExpired)
                    continue;

                Vector3 delta = enemy.CombatPosition - position;
                float distanceSquared = delta.LengthSquared();
                if (distanceSquared >= bestDistanceSquared)
                    continue;

                bestDistanceSquared = distanceSquared;
                best = enemy;
            }

            return best;
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
            else if (entity is BeamShot beam)
                beams.Add(beam);
            else if (entity is PowerupPickup powerup)
                powerups.Add(powerup);
        }

        private static void RebuildSpatialIndex()
        {
            enemyGrid.Clear();
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (!enemy.IsExpired)
                    enemyGrid.Add(enemy.Position, enemy);
            }
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

            bool depthAware = CombatSpaceMath.IsDepthAwareViewActive;

            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy.IsExpired || !MayOverlap(Player1.Instance, enemy))
                    continue;

                if (!(depthAware ? Player1.Instance.OverlapsCombat(enemy) : Player1.Instance.Overlaps(enemy)))
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
            bool depthAware = CombatSpaceMath.IsDepthAwareViewActive;
            float searchRadius = Math.Max(160f, bullet.Velocity.Length() * 0.05f + bullet.ApproximateRadius * 2f);
            foreach (Enemy enemy in enemyGrid.Query(bullet.Position, searchRadius))
            {
                if (enemy.IsExpired || !MayOverlap(enemy, bullet))
                    continue;

                Vector2 impactPoint;
                if (depthAware)
                {
                    if (!bullet.TryGetCombatImpactPoint(enemy, out Vector3 combatImpactPoint))
                        continue;

                    impactPoint = new Vector2(combatImpactPoint.X, combatImpactPoint.Y);
                }
                else
                {
                    if (!bullet.TryGetImpactPoint(enemy, out impactPoint))
                        continue;
                }

                enemy.ApplyBulletHit(bullet, impactPoint);
                ApplyExplosionSplash(bullet, impactPoint, enemy);
                ApplyChainHits(bullet, impactPoint, enemy);
                bullet.RegisterImpact();
                return;
            }
        }

        private static void HandleEnemyBullet(Bullet bullet)
        {
            if (Player1.Instance.IsDead || Player1.Instance.IsInvulnerable || !MayOverlap(Player1.Instance, bullet))
                return;

            bool depthAware = CombatSpaceMath.IsDepthAwareViewActive;
            Vector2 impactPoint;
            if (depthAware)
            {
                if (!bullet.TryGetCombatImpactPoint(Player1.Instance, out Vector3 combatImpactPoint))
                    return;

                impactPoint = new Vector2(combatImpactPoint.X, combatImpactPoint.Y);
            }
            else
            {
                if (!bullet.TryGetImpactPoint(Player1.Instance, out impactPoint))
                    return;
            }

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
                    Player1.Instance.CollectPowerup(powerup);
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

        public static void SpawnFlash(Vector2 position, Color color, float startRadius, float endRadius, float lifeSeconds)
        {
            Add(new FlashEffect(position, color, startRadius, endRadius, lifeSeconds));
        }

        public static void SpawnShockwave(Vector2 position, Color color, float startRadius, float endRadius, float lifeSeconds)
        {
            if (Game1.Instance == null || !Game1.Instance.EnableShockwaves)
                return;

            Add(new ShockwaveEffect(position, color, startRadius, endRadius, lifeSeconds));
        }

        private static void ApplyExplosionSplash(Bullet bullet, Vector2 impactPoint, Enemy primaryTarget)
        {
            if (bullet.ExplosionRadius <= 0f)
                return;

            bool depthAware = CombatSpaceMath.IsDepthAwareViewActive;
            float radiusSquared = bullet.ExplosionRadius * bullet.ExplosionRadius;
            int splashDamage = System.Math.Max(1, (int)System.MathF.Round(bullet.Damage * 0.6f));
            SpawnShockwave(impactPoint, ColorUtil.ParseHex(bullet.SpriteDefinition?.AccentColor, Color.OrangeRed) * 0.22f, 10f, bullet.ExplosionRadius, 0.18f);

            foreach (Enemy enemy in enemyGrid.Query(impactPoint, bullet.ExplosionRadius + 42f))
            {
                if (enemy.IsExpired || ReferenceEquals(enemy, primaryTarget))
                    continue;

                if (depthAware)
                {
                    Vector3 delta = enemy.CombatPosition - bullet.CombatPosition;
                    delta.X = enemy.Position.X - impactPoint.X;
                    delta.Y = enemy.Position.Y - impactPoint.Y;
                    if (delta.LengthSquared() > radiusSquared)
                        continue;
                }
                else
                {
                    Vector2 delta2 = enemy.Position - impactPoint;
                    if (delta2.LengthSquared() > radiusSquared)
                        continue;
                }

                enemy.ApplyDirectHit(enemy.Position, splashDamage, bullet.ImpactProfile, bullet.Velocity * 0.6f, bullet.ImpactFxStyle);
            }
        }

        private static void ApplyChainHits(Bullet bullet, Vector2 impactPoint, Enemy primaryTarget)
        {
            if (bullet.ChainCount <= 0)
                return;

            bool depthAware = CombatSpaceMath.IsDepthAwareViewActive;
            Enemy[] chainTargets = enemyGrid.Query(impactPoint, 520f)
                .Where(enemy => enemy != null && !enemy.IsExpired && !ReferenceEquals(enemy, primaryTarget))
                .OrderBy(enemy =>
                {
                    if (depthAware)
                    {
                        Vector3 delta = enemy.CombatPosition - new Vector3(impactPoint, primaryTarget?.LateralDepth ?? 0f);
                        return delta.LengthSquared();
                    }

                    Vector2 delta2 = enemy.Position - impactPoint;
                    return delta2.LengthSquared();
                })
                .Take(bullet.ChainCount)
                .ToArray();

            for (int i = 0; i < chainTargets.Length; i++)
            {
                Enemy enemy = chainTargets[i];
                Vector2 from = i == 0 ? impactPoint : chainTargets[i - 1].Position;
                Vector2 to = enemy.Position;
                Vector2 delta = to - from;
                int linkSteps = System.Math.Max(3, (int)(delta.Length() / 20f));
                for (int step = 0; step <= linkSteps; step++)
                {
                    Vector2 sample = Vector2.Lerp(from, to, step / (float)linkSteps);
                    SpawnFlash(sample, Color.Cyan * 0.16f, 6f, 1f, 0.05f);
                }

                int chainDamage = System.Math.Max(1, bullet.Damage - i);
                enemy.ApplyDirectHit(enemy.Position, chainDamage, bullet.ImpactProfile, Vector2.Normalize(delta == Vector2.Zero ? Vector2.UnitX : delta) * 640f, ImpactFxStyle.Arc);
            }
        }

        private static bool MayOverlap(Entity first, Entity second)
        {
            return CombatSpaceMath.IsDepthAwareViewActive ? MayOverlap3D(first, second) : MayOverlap2D(first, second);
        }

        private static bool MayOverlap2D(Entity first, Entity second)
        {
            if (first.IsExpired || second.IsExpired)
                return false;

            float radius = first.ApproximateRadius + second.ApproximateRadius + 6f;
            Vector2 delta = first.Position - second.Position;
            return delta.LengthSquared() <= radius * radius;
        }

        private static bool MayOverlap3D(Entity first, Entity second)
        {
            if (first.IsExpired || second.IsExpired)
                return false;

            float radius = first.ApproximateRadius + second.ApproximateRadius + 6f;
            float depthRadius = first.ApproximateDepthRadius + second.ApproximateDepthRadius + 10f;
            Vector3 delta = first.CombatPosition - second.CombatPosition;
            if (MathF.Abs(delta.Z) > depthRadius)
                return false;

            return delta.X * delta.X + delta.Y * delta.Y <= radius * radius;
        }
    }
}
