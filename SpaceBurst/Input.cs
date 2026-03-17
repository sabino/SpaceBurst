using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using System.Linq;

namespace SpaceBurst
{
    static class Input
    {
        private static KeyboardState keyboardState, lastKeyboardState;
        private static MouseState mouseState, lastMouseState;
        private static GamePadState gamepadState, lastGamepadState;

        private static bool isAimingWithMouse = false;
#if ANDROID
        private static Vector2 touchMovementDirection;
        private static Vector2 touchAimDirection;
        private static int movementTouchId = -1;
        private static int aimTouchId = -1;
        private static Vector2 movementTouchOrigin;
        private static Vector2 movementTouchPosition;
        private static Vector2 aimTouchPosition;
#endif

        public static Vector2 MousePosition { get { return Game1.ScreenToWorld(new Vector2(mouseState.X, mouseState.Y)); } }

        public static void Update()
        {
            lastKeyboardState = keyboardState;
            lastMouseState = mouseState;
            lastGamepadState = gamepadState;

            keyboardState = Keyboard.GetState();
            gamepadState = GamePad.GetState(PlayerIndex.One);

#if ANDROID
            UpdateTouchState();
            mouseState = default(MouseState);
            isAimingWithMouse = false;
#else
            mouseState = Mouse.GetState();

            // If the player pressed one of the arrow keys or is using a gamepad to aim, we want to disable mouse aiming. Otherwise,
            // if the player moves the mouse, enable mouse aiming.
            if (new[] { Keys.Left, Keys.Right, Keys.Up, Keys.Down }.Any(x => keyboardState.IsKeyDown(x)) || gamepadState.ThumbSticks.Right != Vector2.Zero)
                isAimingWithMouse = false;
            else if (mouseState.LeftButton == ButtonState.Pressed)
                isAimingWithMouse = true;
            else
                isAimingWithMouse = false;
#endif
        }

        // Checks if a key was just pressed down
        public static bool WasKeyPressed(Keys key)
        {
            return lastKeyboardState.IsKeyUp(key) && keyboardState.IsKeyDown(key);
        }

        public static bool WasButtonPressed(Buttons button)
        {
            return lastGamepadState.IsButtonUp(button) && gamepadState.IsButtonDown(button);
        }

        public static Vector2 GetMovementDirection()
        {
#if ANDROID
            if (touchMovementDirection != Vector2.Zero)
                return touchMovementDirection;
#endif
            Vector2 direction = gamepadState.ThumbSticks.Left;
            direction.Y *= -1;  // invert the y-axis

            if (keyboardState.IsKeyDown(Keys.A))
                direction.X -= 1;
            if (keyboardState.IsKeyDown(Keys.D))
                direction.X += 1;
            if (keyboardState.IsKeyDown(Keys.W))
                direction.Y -= 1;
            if (keyboardState.IsKeyDown(Keys.S))
                direction.Y += 1;

            // Clamp the length of the vector to a maximum of 1.
            if (direction.LengthSquared() > 1)
                direction.Normalize();

            return direction;
        }

        public static Vector2 GetAimDirection()
        {
#if ANDROID
            if (touchAimDirection != Vector2.Zero)
                return touchAimDirection;
#endif
            if (isAimingWithMouse)
                return GetMouseAimDirection();

            Vector2 direction = gamepadState.ThumbSticks.Right;
            direction.Y *= -1;

            if (keyboardState.IsKeyDown(Keys.Left))
                direction.X -= 1;
            if (keyboardState.IsKeyDown(Keys.Right))
                direction.X += 1;
            if (keyboardState.IsKeyDown(Keys.Up))
                direction.Y -= 1;
            if (keyboardState.IsKeyDown(Keys.Down))
                direction.Y += 1;

            // If there's no aim input, return zero. Otherwise normalize the direction to have a length of 1.
            if (direction == Vector2.Zero)
                return Vector2.Zero;
            else
                return Vector2.Normalize(direction);
        }

        private static Vector2 GetMouseAimDirection()
        {
            Vector2 direction = MousePosition - Player1.Instance.Position;

            if (direction == Vector2.Zero)
                return Vector2.Zero;
            else
                return Vector2.Normalize(direction);
        }

        public static bool WasBombButtonPressed()
        {
            return WasButtonPressed(Buttons.LeftTrigger) || WasButtonPressed(Buttons.RightTrigger) || WasKeyPressed(Keys.Space);
        }

#if ANDROID
        private static void UpdateTouchState()
        {
            var touches = TouchPanel.GetState();
            bool movementTouchFound = false;
            bool aimTouchFound = false;
            float splitX = Game1.ScreenSize.X * 0.4f;

            foreach (var touch in touches)
            {
                var worldPosition = Game1.ScreenToWorld(touch.Position);

                if (touch.Id == movementTouchId)
                {
                    movementTouchFound = true;
                    movementTouchPosition = worldPosition;
                    continue;
                }

                if (touch.Id == aimTouchId)
                {
                    aimTouchFound = true;
                    aimTouchPosition = worldPosition;
                    continue;
                }

                if (!movementTouchFound && movementTouchId == -1 && worldPosition.X <= splitX)
                {
                    movementTouchId = touch.Id;
                    movementTouchFound = true;
                    movementTouchOrigin = worldPosition;
                    movementTouchPosition = worldPosition;
                }
                else if (!aimTouchFound && aimTouchId == -1)
                {
                    aimTouchId = touch.Id;
                    aimTouchFound = true;
                    aimTouchPosition = worldPosition;
                }
            }

            if (!movementTouchFound)
            {
                movementTouchId = -1;
                movementTouchOrigin = Vector2.Zero;
                movementTouchPosition = Vector2.Zero;
            }

            if (!aimTouchFound)
            {
                aimTouchId = -1;
                aimTouchPosition = Vector2.Zero;
            }

            touchMovementDirection = GetVirtualStickDirection(movementTouchOrigin, movementTouchPosition, 70f);
            touchAimDirection = aimTouchId == -1
                ? Vector2.Zero
                : GetDirectionToPoint(Player1.Instance.Position, aimTouchPosition);
        }

        private static Vector2 GetVirtualStickDirection(Vector2 origin, Vector2 current, float maxDistance)
        {
            if (origin == Vector2.Zero && current == Vector2.Zero)
                return Vector2.Zero;

            Vector2 delta = current - origin;
            if (delta == Vector2.Zero)
                return Vector2.Zero;

            if (delta.LengthSquared() > maxDistance * maxDistance)
                delta = Vector2.Normalize(delta) * maxDistance;

            return delta / maxDistance;
        }

        private static Vector2 GetDirectionToPoint(Vector2 origin, Vector2 target)
        {
            Vector2 direction = target - origin;
            return direction == Vector2.Zero ? Vector2.Zero : Vector2.Normalize(direction);
        }
#endif
    }
}
