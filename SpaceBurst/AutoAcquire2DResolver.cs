using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System;

namespace SpaceBurst
{
    static class AutoAcquire2DResolver
    {
        public readonly struct Result
        {
            public Result(Vector3 direction, Enemy target)
            {
                Direction = direction;
                Target = target;
            }

            public Vector3 Direction { get; }
            public Enemy Target { get; }
        }

        public static Result Resolve(Vector3 origin, Vector2 planarAim, IEnumerable<Enemy> enemies)
        {
            Vector2 planar = planarAim == Vector2.Zero ? Vector2.UnitX : Vector2.Normalize(planarAim);
            Vector3 fallback = Vector3.Normalize(new Vector3(planar.X, planar.Y, 0f));
            Enemy bestEnemy = null;
            float bestScore = float.NegativeInfinity;

            foreach (Enemy enemy in enemies)
            {
                if (enemy == null || enemy.IsExpired)
                    continue;

                Vector3 toEnemy = enemy.CombatPosition - origin;
                if (toEnemy.X < -24f)
                    continue;

                Vector2 planarToEnemy = new Vector2(toEnemy.X, toEnemy.Y);
                float planarLength = planarToEnemy.Length();
                if (planarLength <= 0.001f)
                    planarToEnemy = planar;
                else
                    planarToEnemy /= planarLength;

                float aimAlignment = Vector2.Dot(planar, planarToEnemy);
                if (aimAlignment < 0.35f)
                    continue;

                float forwardPreference = MathHelper.Clamp(1f - MathF.Abs(toEnemy.X) / 960f, 0f, 1f);
                float altitudePreference = MathHelper.Clamp(1f - MathF.Abs(toEnemy.Y) / 320f, 0f, 1f);
                float threatPreference = enemy.IsBoss ? 0.35f : 0f;
                float score = aimAlignment * 2.3f + forwardPreference * 1.45f + altitudePreference * 0.7f + threatPreference;
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestEnemy = enemy;
            }

            if (bestEnemy == null)
                return new Result(fallback, null);

            Vector3 desired = bestEnemy.CombatPosition - origin;
            if (desired == Vector3.Zero)
                desired = fallback;
            else
                desired.Normalize();

            return new Result(desired, bestEnemy);
        }
    }
}
