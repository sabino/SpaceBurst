using Microsoft.Xna.Framework;

namespace SpaceBurst
{
    readonly struct PlayerCommandFrame
    {
        public Vector2 PlanarMovement { get; init; }
        public Vector2 ReticleDelta { get; init; }
        public bool FireHeld { get; init; }
        public bool RewindHeld { get; init; }
    }

    struct CombatPosition3
    {
        public float Travel;
        public float Altitude;
        public float LateralDepth;

        public CombatPosition3(float travel, float altitude, float lateralDepth)
        {
            Travel = travel;
            Altitude = altitude;
            LateralDepth = lateralDepth;
        }

        public Vector2 ToSideScrollerPosition()
        {
            return new Vector2(Travel, Altitude);
        }

        public Vector3 ToVector3()
        {
            return new Vector3(Travel, Altitude, LateralDepth);
        }

        public static CombatPosition3 FromVector2(Vector2 value, float lateralDepth = 0f)
        {
            return new CombatPosition3(value.X, value.Y, lateralDepth);
        }

        public static CombatPosition3 Lerp(CombatPosition3 from, CombatPosition3 to, float amount)
        {
            return new CombatPosition3(
                MathHelper.Lerp(from.Travel, to.Travel, amount),
                MathHelper.Lerp(from.Altitude, to.Altitude, amount),
                MathHelper.Lerp(from.LateralDepth, to.LateralDepth, amount));
        }

        public static CombatPosition3 operator +(CombatPosition3 position, Vector3 velocity)
        {
            return new CombatPosition3(
                position.Travel + velocity.X,
                position.Altitude + velocity.Y,
                position.LateralDepth + velocity.Z);
        }

        public static Vector3 operator -(CombatPosition3 left, CombatPosition3 right)
        {
            return new Vector3(
                left.Travel - right.Travel,
                left.Altitude - right.Altitude,
                left.LateralDepth - right.LateralDepth);
        }
    }

    static class CombatMath
    {
        public static float DistanceSquared(CombatPosition3 left, CombatPosition3 right)
        {
            return Vector3.DistanceSquared(left.ToVector3(), right.ToVector3());
        }

        public static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
        {
            return value.LengthSquared() <= 0.0001f ? fallback : Vector3.Normalize(value);
        }

        public static bool SegmentIntersectsAabb(Vector3 start, Vector3 end, Vector3 min, Vector3 max, out Vector3 hitPoint)
        {
            Vector3 direction = end - start;
            float tMin = 0f;
            float tMax = 1f;

            if (!ClipAxis(start.X, direction.X, min.X, max.X, ref tMin, ref tMax) ||
                !ClipAxis(start.Y, direction.Y, min.Y, max.Y, ref tMin, ref tMax) ||
                !ClipAxis(start.Z, direction.Z, min.Z, max.Z, ref tMin, ref tMax))
            {
                hitPoint = end;
                return false;
            }

            hitPoint = start + direction * tMin;
            return true;
        }

        private static bool ClipAxis(float start, float direction, float min, float max, ref float tMin, ref float tMax)
        {
            if (System.MathF.Abs(direction) <= 0.0001f)
                return start >= min && start <= max;

            float inverse = 1f / direction;
            float t1 = (min - start) * inverse;
            float t2 = (max - start) * inverse;
            if (t1 > t2)
            {
                float swap = t1;
                t1 = t2;
                t2 = swap;
            }

            tMin = System.MathF.Max(tMin, t1);
            tMax = System.MathF.Min(tMax, t2);
            return tMin <= tMax;
        }
    }
}
