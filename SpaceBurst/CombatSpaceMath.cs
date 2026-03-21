using Microsoft.Xna.Framework;
using SpaceBurst.RuntimeData;

namespace SpaceBurst
{
    static class CombatSpaceMath
    {
        public const float MaxDepth = 180f;
        public const float MaxAltitude = 220f;

        public static bool IsDepthAwareViewActive
        {
            get
            {
                Game1 game = Game1.Instance;
                return game != null
                    && game.CurrentViewMode == ViewMode.Chase3D
                    && game.CurrentPresentationTier == PresentationTier.Late3D;
            }
        }

        public static Vector2 ProjectToSideScroller(Vector3 combatPosition)
        {
            return new Vector2(combatPosition.X, combatPosition.Y);
        }

        public static Vector3 ClampToArena(Vector3 combatPosition, Vector2 halfSize)
        {
            float left = Game1.ScreenSize.X * 0.06f + halfSize.X;
            float right = Game1.ScreenSize.X * 0.94f - halfSize.X;
            float top = Game1.ScreenSize.Y * 0.06f + halfSize.Y;
            float bottom = Game1.ScreenSize.Y * 0.94f - halfSize.Y;
            combatPosition.X = MathHelper.Clamp(combatPosition.X, left, right);
            combatPosition.Y = MathHelper.Clamp(combatPosition.Y, top, bottom);
            combatPosition.Z = MathHelper.Clamp(combatPosition.Z, -MaxDepth, MaxDepth);
            return combatPosition;
        }

        public static Vector3 CombinePlanarAndDepth(Vector2 planarDirection, float lateralDepth)
        {
            Vector3 combined = new Vector3(planarDirection.X, planarDirection.Y, lateralDepth);
            if (combined == Vector3.Zero)
                return Vector3.UnitX;

            combined.Normalize();
            return combined;
        }

        public static Vector3 CreatePosition(float travel, float altitude, float lateralDepth)
        {
            return new Vector3(travel, altitude, lateralDepth);
        }

        public static Vector3 CreateVelocity(float travel, float altitude, float lateralDepth)
        {
            return new Vector3(travel, altitude, lateralDepth);
        }
    }
}
