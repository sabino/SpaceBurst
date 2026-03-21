using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace SpaceBurst
{
    internal static class UiPointerInteraction
    {
        public static Vector2 GetInteractionPosition(bool pointerReleased, Vector2 pointerPosition, Vector2 releasePosition)
        {
            return pointerReleased ? releasePosition : pointerPosition;
        }

        public static bool DidCapturedControlActivate(
            int capturedControlId,
            int expectedControlId,
            Rectangle bounds,
            bool pointerReleased,
            bool pointerDragging,
            Vector2 pointerPosition,
            Vector2 releasePosition)
        {
            return pointerReleased
                && capturedControlId == expectedControlId
                && !pointerDragging
                && bounds.Contains(GetInteractionPosition(pointerReleased, pointerPosition, releasePosition));
        }

        public static bool TryGetActivatedCapturedIndex(
            int capturedControlId,
            int controlIdBase,
            IReadOnlyList<Rectangle> bounds,
            bool pointerReleased,
            bool pointerDragging,
            Vector2 pointerPosition,
            Vector2 releasePosition,
            out int activatedIndex)
        {
            activatedIndex = -1;
            if (!pointerReleased || pointerDragging)
                return false;

            int capturedIndex = capturedControlId - controlIdBase;
            if (capturedIndex < 0 || capturedIndex >= bounds.Count)
                return false;

            Vector2 interactionPosition = GetInteractionPosition(pointerReleased, pointerPosition, releasePosition);
            if (!bounds[capturedIndex].Contains(interactionPosition))
                return false;

            activatedIndex = capturedIndex;
            return true;
        }
    }
}
