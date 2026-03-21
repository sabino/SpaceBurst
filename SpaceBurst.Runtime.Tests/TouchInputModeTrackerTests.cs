using Xunit;

namespace SpaceBurst.Runtime.Tests
{
    public sealed class TouchInputModeTrackerTests
    {
        [Fact]
        public void ConsecutiveMenuFramesDoNotResetTrackedMenuState()
        {
            var tracker = new TouchInputModeTracker();

            Assert.Equal(TouchInputResetTarget.None, tracker.TransitionTo(TouchInputMode.Menu));
            Assert.Equal(TouchInputResetTarget.None, tracker.TransitionTo(TouchInputMode.Menu));
        }

        [Fact]
        public void SwitchingFromMenuToGameplayResetsMenuStateOnce()
        {
            var tracker = new TouchInputModeTracker();

            tracker.TransitionTo(TouchInputMode.Menu);

            Assert.Equal(TouchInputResetTarget.Menu, tracker.TransitionTo(TouchInputMode.Gameplay));
            Assert.Equal(TouchInputResetTarget.None, tracker.TransitionTo(TouchInputMode.Gameplay));
        }

        [Fact]
        public void SwitchingFromGameplayToMenuResetsGameplayStateOnce()
        {
            var tracker = new TouchInputModeTracker();

            tracker.TransitionTo(TouchInputMode.Gameplay);

            Assert.Equal(TouchInputResetTarget.Gameplay, tracker.TransitionTo(TouchInputMode.Menu));
            Assert.Equal(TouchInputResetTarget.None, tracker.TransitionTo(TouchInputMode.Menu));
        }
    }
}
