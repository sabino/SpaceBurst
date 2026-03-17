using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
        private const float StickRadiusFactor = 0.12f;
        private const float StickZoneWidthFactor = 0.48f;
        private const float StickZoneHeightFactor = 0.4f;
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
            Rectangle gameplayBounds = Game1.RenderBounds;
            Rectangle movementZone = GetMovementZone(gameplayBounds);
            float stickRadius = GetStickRadius(gameplayBounds);
            movementTouchOrigin = GetStickOrigin(gameplayBounds);

            foreach (var touch in touches)
            {
                var rawScreenPosition = touch.Position;
                var screenPosition = Vector2.Clamp(
                    rawScreenPosition,
                    new Vector2(gameplayBounds.Left, gameplayBounds.Top),
                    new Vector2(gameplayBounds.Right - 1, gameplayBounds.Bottom - 1));

                if (touch.Id == movementTouchId)
                {
                    movementTouchFound = true;
                    movementTouchPosition = ClampToRadius(screenPosition, movementTouchOrigin, stickRadius);
                    continue;
                }

                if (touch.Id == aimTouchId)
                {
                    aimTouchFound = true;
                    aimTouchPosition = screenPosition;
                    continue;
                }

                if (!gameplayBounds.Contains(rawScreenPosition.ToPoint()))
                    continue;

                if (!movementTouchFound && movementTouchId == -1 && movementZone.Contains(screenPosition.ToPoint()))
                {
                    movementTouchId = touch.Id;
                    movementTouchFound = true;
                    movementTouchPosition = ClampToRadius(screenPosition, movementTouchOrigin, stickRadius);
                }
                else if (!aimTouchFound && aimTouchId == -1)
                {
                    aimTouchId = touch.Id;
                    aimTouchFound = true;
                    aimTouchPosition = screenPosition;
                }
            }

            if (!movementTouchFound)
            {
                movementTouchId = -1;
                movementTouchPosition = movementTouchOrigin;
            }

            if (!aimTouchFound)
            {
                aimTouchId = -1;
                aimTouchPosition = Vector2.Zero;
            }

            touchMovementDirection = GetVirtualStickDirection(movementTouchOrigin, movementTouchPosition, stickRadius);
            touchAimDirection = aimTouchId == -1
                ? Vector2.Zero
                : GetDirectionToPoint(Player1.Instance.Position, Game1.ScreenToWorld(aimTouchPosition));
        }

        private static Vector2 GetVirtualStickDirection(Vector2 origin, Vector2 current, float maxDistance)
        {
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

        public static void DrawTouchControls(SpriteBatch spriteBatch, Texture2D controlTexture)
        {
            if (controlTexture == null)
                return;

            Rectangle gameplayBounds = Game1.RenderBounds;
            float stickRadius = GetStickRadius(gameplayBounds);
            Vector2 stickCenter = GetStickOrigin(gameplayBounds);
            Vector2 stickThumb = movementTouchId == -1 ? stickCenter : movementTouchPosition;

            DrawCircle(spriteBatch, controlTexture, stickCenter, stickRadius * 1.15f, Color.White * 0.12f);
            DrawCircle(spriteBatch, controlTexture, stickCenter, stickRadius * 0.78f, Color.White * 0.08f);
            DrawCircle(spriteBatch, controlTexture, stickThumb, stickRadius * 0.46f, Color.White * 0.28f);

            if (aimTouchId != -1)
            {
                DrawCircle(spriteBatch, controlTexture, aimTouchPosition, stickRadius * 0.32f, Color.White * 0.18f);
                DrawCircle(spriteBatch, controlTexture, aimTouchPosition, stickRadius * 0.16f, Color.White * 0.3f);
            }
        }

        private static Rectangle GetMovementZone(Rectangle gameplayBounds)
        {
            int width = (int)(gameplayBounds.Width * StickZoneWidthFactor);
            int height = (int)(gameplayBounds.Height * StickZoneHeightFactor);
            return new Rectangle(gameplayBounds.Left, gameplayBounds.Bottom - height, width, height);
        }

        private static Vector2 GetStickOrigin(Rectangle gameplayBounds)
        {
            float radius = GetStickRadius(gameplayBounds);
            float margin = radius * 0.6f;

            return new Vector2(
                gameplayBounds.Left + margin + radius,
                gameplayBounds.Bottom - margin - radius);
        }

        private static float GetStickRadius(Rectangle gameplayBounds)
        {
            return System.Math.Min(gameplayBounds.Width, gameplayBounds.Height) * StickRadiusFactor;
        }

        private static Vector2 ClampToRadius(Vector2 position, Vector2 origin, float radius)
        {
            Vector2 delta = position - origin;
            if (delta == Vector2.Zero)
                return origin;

            if (delta.LengthSquared() > radius * radius)
                return origin + Vector2.Normalize(delta) * radius;

            return position;
        }

        private static void DrawCircle(SpriteBatch spriteBatch, Texture2D texture, Vector2 center, float radius, Color color)
        {
            float scale = radius * 2f / texture.Width;
            spriteBatch.Draw(
                texture,
                center,
                null,
                color,
                0f,
                new Vector2(texture.Width / 2f, texture.Height / 2f),
                scale,
                SpriteEffects.None,
                0f);
        }
#endif
    }
}
