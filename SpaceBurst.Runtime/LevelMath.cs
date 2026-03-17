using System;
using System.Collections.Generic;
using System.Numerics;

namespace SpaceBurst.RuntimeData
{
    public static class LevelMath
    {
        public static IReadOnlyList<Vector2> GetFormationOffsets(FormationType formation, int count, float spacing)
        {
            var offsets = new List<Vector2>();

            if (count <= 0)
                return offsets;

            switch (formation)
            {
                case FormationType.Line:
                    for (int i = 0; i < count; i++)
                        offsets.Add(new Vector2((i - (count - 1) / 2f) * spacing, 0f));
                    break;

                case FormationType.Column:
                    for (int i = 0; i < count; i++)
                        offsets.Add(new Vector2(0f, (i - (count - 1) / 2f) * spacing));
                    break;

                case FormationType.V:
                    for (int i = 0; i < count; i++)
                    {
                        float centerOffset = i - (count - 1) / 2f;
                        offsets.Add(new Vector2(centerOffset * spacing * 0.75f, Math.Abs(centerOffset) * spacing * 0.6f));
                    }
                    break;

                case FormationType.Arc:
                    float radius = Math.Max(spacing, count * spacing * 0.18f);
                    float startAngle = -0.8f;
                    float angleStep = count == 1 ? 0f : 1.6f / (count - 1);
                    for (int i = 0; i < count; i++)
                    {
                        float angle = startAngle + angleStep * i;
                        offsets.Add(new Vector2((float)Math.Sin(angle), (float)(1 - Math.Cos(angle))) * radius);
                    }
                    break;

                case FormationType.Ring:
                    radius = Math.Max(spacing * 0.7f, count * spacing * 0.08f);
                    for (int i = 0; i < count; i++)
                    {
                        float angle = MathF.Tau * i / count;
                        offsets.Add(new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius);
                    }
                    break;
            }

            return offsets;
        }

        public static Vector2 GetSpawnPoint(EntrySide entrySide, Vector2 arenaSize, Vector2 anchor, Vector2 offset, float margin)
        {
            switch (entrySide)
            {
                case EntrySide.Bottom:
                    return new Vector2(anchor.X + offset.X, arenaSize.Y + margin + Math.Abs(offset.Y));
                case EntrySide.Left:
                    return new Vector2(-margin - Math.Abs(offset.X), anchor.Y + offset.Y);
                case EntrySide.Right:
                    return new Vector2(arenaSize.X + margin + Math.Abs(offset.X), anchor.Y + offset.Y);
                default:
                    return new Vector2(anchor.X + offset.X, -margin - Math.Abs(offset.Y));
            }
        }

        public static Vector2 GetAnchorPoint(Vector2 arenaSize, float anchorX, float anchorY, Vector2 offset)
        {
            return new Vector2(arenaSize.X * anchorX, arenaSize.Y * anchorY) + offset;
        }

        public static Vector2 SamplePreviewPath(PathType pathType, Vector2 spawn, Vector2 anchor, float progress, int index)
        {
            progress = Math.Clamp(progress, 0f, 1f);
            Vector2 basePosition = Vector2.Lerp(spawn, anchor, progress);

            switch (pathType)
            {
                case PathType.Swoop:
                    Vector2 direction = anchor - spawn;
                    float length = direction.Length();
                    if (length > 0f)
                    {
                        direction /= length;
                        Vector2 perpendicular = new Vector2(-direction.Y, direction.X);
                        basePosition += perpendicular * MathF.Sin(progress * MathF.PI) * (45f + index * 6f);
                    }
                    break;

                case PathType.LaneSweep:
                    basePosition.X = anchor.X + MathF.Sin(progress * MathF.PI * 2f) * 120f;
                    break;

                case PathType.ChaseAfterDelay:
                    if (progress > 0.55f)
                        basePosition = Vector2.Lerp(anchor, new Vector2(anchor.X, anchor.Y + 140f), (progress - 0.55f) / 0.45f);
                    break;

                case PathType.OrbitAnchor:
                    float angle = MathF.Tau * progress + index * 0.35f;
                    basePosition = anchor + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 90f;
                    break;
            }

            return basePosition;
        }
    }
}
