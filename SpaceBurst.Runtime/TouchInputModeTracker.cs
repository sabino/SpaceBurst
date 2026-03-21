namespace SpaceBurst
{
    public enum TouchInputMode
    {
        Menu,
        Gameplay,
    }

    public enum TouchInputResetTarget
    {
        None,
        Menu,
        Gameplay,
    }

    public sealed class TouchInputModeTracker
    {
        private bool initialized;
        private TouchInputMode currentMode;

        public TouchInputResetTarget TransitionTo(TouchInputMode nextMode)
        {
            if (!initialized)
            {
                initialized = true;
                currentMode = nextMode;
                return TouchInputResetTarget.None;
            }

            if (currentMode == nextMode)
                return TouchInputResetTarget.None;

            TouchInputResetTarget resetTarget = currentMode == TouchInputMode.Menu
                ? TouchInputResetTarget.Menu
                : TouchInputResetTarget.Gameplay;
            currentMode = nextMode;
            return resetTarget;
        }

        public void Reset()
        {
            initialized = false;
            currentMode = TouchInputMode.Menu;
        }
    }
}
