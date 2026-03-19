using System;
using System.Numerics;

namespace SpaceBurst.RuntimeData
{
    public static class LevelMath
    {
        public static float GetLanePosition(Vector2 arenaSize, int lane)
        {
            int clampedLane = Math.Clamp(lane, 0, 4);
            float top = arenaSize.Y * 0.16f;
            float bottom = arenaSize.Y * 0.84f;
            float step = (bottom - top) / 4f;
            return top + step * clampedLane;
        }

        public static float ResolveTargetY(Vector2 arenaSize, SpawnGroupDefinition group)
        {
            if (group.TargetY.HasValue)
                return Math.Clamp(group.TargetY.Value, 0.08f, 0.92f) * arenaSize.Y;

            return GetLanePosition(arenaSize, group.Lane);
        }

        public static Vector2 GetSpawnPoint(Vector2 arenaSize, SpawnGroupDefinition group, int index)
        {
            return new Vector2(
                arenaSize.X + Math.Max(40f, group.SpawnLeadDistance + index * group.SpacingX),
                ResolveTargetY(arenaSize, group));
        }

        public static float EstimateGroupLifetimeSeconds(SpawnGroupDefinition group, EnemyArchetypeDefinition archetype, float stageWidth)
        {
            float speed = Math.Max(60f, archetype.MoveSpeed * Math.Max(0.35f, group.SpeedMultiplier));
            return (group.SpawnLeadDistance + stageWidth + Math.Max(0f, (group.Count - 1) * group.SpacingX)) / speed + 4f;
        }

        public static Vector2 SamplePreviewPosition(
            MovePattern movePattern,
            Vector2 spawn,
            float elapsedSeconds,
            float moveSpeed,
            float scrollSpeed,
            float targetY,
            float amplitude,
            float frequency)
        {
            float combinedSpeed = Math.Max(40f, moveSpeed + scrollSpeed);
            float x = spawn.X - combinedSpeed * elapsedSeconds;
            float y = targetY;
            float phase = elapsedSeconds * Math.Max(0.2f, frequency);

            switch (movePattern)
            {
                case MovePattern.SineWave:
                    y += MathF.Sin(phase * MathF.Tau) * amplitude;
                    break;

                case MovePattern.Dive:
                    y += (1f - MathF.Exp(-elapsedSeconds * 2.2f)) * MathF.Sin((phase + 0.25f) * MathF.PI) * amplitude * 0.8f;
                    break;

                case MovePattern.RetreatBackfire:
                    if (elapsedSeconds > 1.8f)
                        x += (elapsedSeconds - 1.8f) * moveSpeed * 0.45f;
                    y += MathF.Sin(phase * MathF.PI) * amplitude * 0.35f;
                    break;

                case MovePattern.TurretCarrier:
                    y += MathF.Sin(phase * MathF.PI) * amplitude * 0.2f;
                    break;

                case MovePattern.BossOrbit:
                    x = spawn.X - scrollSpeed * elapsedSeconds - 160f;
                    y = targetY + MathF.Sin(phase * 1.7f) * amplitude;
                    break;

                case MovePattern.BossCharge:
                    x = spawn.X - scrollSpeed * elapsedSeconds - MathF.Min(360f, elapsedSeconds * moveSpeed * 1.35f);
                    y = targetY + MathF.Sin(phase * 2.1f) * amplitude * 0.45f;
                    break;

                default:
                    break;
            }

            return new Vector2(x, y);
        }
    }
}
