using Microsoft.Xna.Framework;
using Xunit;

namespace SpaceBurst.Tests
{
    public sealed class UiPointerInteractionTests
    {
        [Fact]
        public void GetInteractionPosition_UsesReleasePositionWhenPointerReleased()
        {
            Vector2 position = UiPointerInteraction.GetInteractionPosition(
                pointerReleased: true,
                pointerPosition: new Vector2(960f, 540f),
                releasePosition: new Vector2(240f, 180f));

            Assert.Equal(new Vector2(240f, 180f), position);
        }

        [Fact]
        public void DidCapturedControlActivate_UsesReleasePositionForSyntheticRelease()
        {
            bool activated = UiPointerInteraction.DidCapturedControlActivate(
                capturedControlId: 101,
                expectedControlId: 101,
                bounds: new Rectangle(200, 120, 160, 60),
                pointerReleased: true,
                pointerDragging: false,
                pointerPosition: new Vector2(960f, 540f),
                releasePosition: new Vector2(240f, 150f));

            Assert.True(activated);
        }

        [Fact]
        public void DidCapturedControlActivate_RejectsDraggedReleaseOutsideBounds()
        {
            bool activated = UiPointerInteraction.DidCapturedControlActivate(
                capturedControlId: 101,
                expectedControlId: 101,
                bounds: new Rectangle(200, 120, 160, 60),
                pointerReleased: true,
                pointerDragging: false,
                pointerPosition: new Vector2(240f, 150f),
                releasePosition: new Vector2(420f, 150f));

            Assert.False(activated);
        }

        [Fact]
        public void TryGetActivatedCapturedIndex_UsesCapturedBoundsAtReleasePosition()
        {
            Rectangle[] bounds =
            {
                new Rectangle(100, 100, 120, 40),
                new Rectangle(100, 180, 120, 40),
                new Rectangle(100, 260, 120, 40),
            };

            bool activated = UiPointerInteraction.TryGetActivatedCapturedIndex(
                capturedControlId: 8001,
                controlIdBase: 8000,
                bounds: bounds,
                pointerReleased: true,
                pointerDragging: false,
                pointerPosition: new Vector2(960f, 540f),
                releasePosition: new Vector2(140f, 200f),
                activatedIndex: out int activatedIndex);

            Assert.True(activated);
            Assert.Equal(1, activatedIndex);
        }
    }
}
