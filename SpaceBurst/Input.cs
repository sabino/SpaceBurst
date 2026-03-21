using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using SpaceBurst.RuntimeData;
using System;
using System.Collections.Generic;

namespace SpaceBurst
{
    static class Input
    {
        private static KeyboardState keyboardState;
        private static KeyboardState lastKeyboardState;
        private static MouseState mouseState;
        private static MouseState lastMouseState;
        private static GamePadState gamepadState;
        private static GamePadState lastGamepadState;
        private static readonly HashSet<Keys> consumedKeys = new HashSet<Keys>();
#if ANDROID
        private static bool androidCancelPressed;
#endif

        private static bool primaryActionPressed;
        private static bool fireHeld;
        private static bool rewindHeld;
        private static Vector2 pointerPosition;
        private static bool uiPointerPressed;
        private static bool uiPointerReleased;
        private static bool uiPointerHeld;
        private static bool uiPointerDragging;
        private static Vector2 uiPointerPressPosition;
        private static Vector2 uiPointerReleasePosition;
        private static Vector2 uiPointerDelta;
        private static Vector2 uiPointerDragDelta;
        private static int uiPointerScrollWheelDelta;
        private static int capturedUiControlId = -1;

        private const float MenuTapThreshold = 18f;
#if ANDROID || BLAZORGL
        private const float StickRadiusFactor = 0.11f;
        private static Vector2 touchMovementDirection;
        private static Vector2 touchAimDirection;
        private static int menuTouchId = -1;
        private static Vector2 menuTouchStartPosition;
        private static Vector2 menuTouchLastPosition;
        private static bool menuTouchHeld;
        private static bool menuTouchDragging;
        private static int movementTouchId = -1;
        private static int aimTouchId = -1;
        private static int rewindTouchId = -1;
        private static bool touchPausePressed;
        private static bool touchStyleCyclePressed;
        private static int touchTopButtonId = -1;
        private static Vector2 touchTopButtonStartPosition;
        private static TouchTopButton touchTopButtonTarget = TouchTopButton.None;
        private static Vector2 movementTouchOrigin;
        private static Vector2 movementTouchPosition;
        private static Vector2 aimTouchOrigin;
        private static Vector2 aimTouchPosition;

        private enum TouchTopButton
        {
            None,
            WeaponSwap,
            Pause,
        }
#endif

        public static Vector2 PointerPosition
        {
            get { return pointerPosition; }
        }

        public static Vector2 UiPointerPressPosition
        {
            get { return uiPointerPressPosition; }
        }

        public static Vector2 UiPointerReleasePosition
        {
            get { return uiPointerReleasePosition; }
        }

        public static bool WasUiPointerPressed()
        {
            return uiPointerPressed;
        }

        public static bool WasUiPointerReleased()
        {
            return uiPointerReleased;
        }

        public static bool IsUiPointerHeld()
        {
            return uiPointerHeld;
        }

        public static bool IsUiPointerDragging()
        {
            return uiPointerDragging;
        }

        public static bool DidUiPointerMove()
        {
            return uiPointerDelta.LengthSquared() > 0.01f;
        }

        public static int CapturedUiControlId
        {
            get { return capturedUiControlId; }
        }

        public static void Update()
        {
            lastKeyboardState = keyboardState;
            lastMouseState = mouseState;
            lastGamepadState = gamepadState;
            primaryActionPressed = false;
            fireHeld = false;
            rewindHeld = false;
            uiPointerPressed = false;
            uiPointerReleased = false;
            uiPointerDelta = Vector2.Zero;
            uiPointerDragDelta = Vector2.Zero;
            uiPointerScrollWheelDelta = 0;
            if (!uiPointerHeld)
            {
                uiPointerDragging = false;
                capturedUiControlId = -1;
            }
            consumedKeys.Clear();

            keyboardState = Keyboard.GetState();
            gamepadState = GamePad.GetState(PlayerIndex.One);

#if ANDROID
            UpdateTouchState(TouchPanel.GetState());
            mouseState = default(MouseState);
#else
            mouseState = Mouse.GetState();
            Vector2 previousPointer = pointerPosition;
            pointerPosition = Game1.ScreenToUi(new Vector2(mouseState.X, mouseState.Y));
            uiPointerDelta = pointerPosition - previousPointer;
            uiPointerScrollWheelDelta = mouseState.ScrollWheelValue - lastMouseState.ScrollWheelValue;

            bool wasPrimaryDown = lastMouseState.LeftButton == ButtonState.Pressed;
            bool isPrimaryDown = mouseState.LeftButton == ButtonState.Pressed;
            if (!wasPrimaryDown && isPrimaryDown)
            {
                primaryActionPressed = true;
                uiPointerPressed = true;
                uiPointerHeld = true;
                uiPointerDragging = false;
                uiPointerPressPosition = pointerPosition;
            }
            else if (wasPrimaryDown && isPrimaryDown)
            {
                uiPointerHeld = true;
                if (!uiPointerDragging)
                {
                    Vector2 totalDelta = pointerPosition - uiPointerPressPosition;
                    uiPointerDragging = totalDelta.LengthSquared() > MenuTapThreshold * MenuTapThreshold;
                }

                if (uiPointerDragging)
                    uiPointerDragDelta += uiPointerDelta;
            }
            else if (wasPrimaryDown && !isPrimaryDown)
            {
                uiPointerReleased = true;
                uiPointerReleasePosition = pointerPosition;
                uiPointerHeld = false;
            }
            else
            {
                uiPointerHeld = false;
            }

            fireHeld = keyboardState.IsKeyDown(Keys.Space) || gamepadState.Triggers.Right > 0.25f;
            rewindHeld = keyboardState.IsKeyDown(Keys.R) || gamepadState.IsButtonDown(Buttons.LeftShoulder);
#if BLAZORGL
            UpdateBrowserTouchState();
#endif
#endif
        }

        public static bool WasKeyPressed(Keys key)
        {
            return !consumedKeys.Contains(key) && lastKeyboardState.IsKeyUp(key) && keyboardState.IsKeyDown(key);
        }

        public static bool WasButtonPressed(Buttons button)
        {
            return lastGamepadState.IsButtonUp(button) && gamepadState.IsButtonDown(button);
        }

        public static bool WasPrimaryActionPressed()
        {
            return primaryActionPressed;
        }

        public static bool WasConfirmPressed()
        {
            return WasKeyPressed(Keys.Enter) || WasKeyPressed(Keys.Space) || WasButtonPressed(Buttons.A);
        }

        public static bool WasCancelPressed()
        {
#if ANDROID || BLAZORGL
            if (touchPausePressed)
            {
#if ANDROID
                androidCancelPressed = false;
#endif
                return true;
            }
#endif
#if ANDROID
            if (androidCancelPressed)
            {
                androidCancelPressed = false;
                return true;
            }
#endif
            return WasKeyPressed(Keys.Escape) || WasButtonPressed(Buttons.Back) || WasButtonPressed(Buttons.B);
        }

        public static void NotifyAndroidBackPressed()
        {
#if ANDROID
            androidCancelPressed = true;
#endif
        }

        public static bool WasHelpPressed()
        {
            return WasKeyPressed(Keys.F1) || WasKeyPressed(Keys.Tab) || WasButtonPressed(Buttons.Y);
        }

        public static bool WasPreviousStylePressed()
        {
            return WasKeyPressed(Keys.Q) || WasButtonPressed(Buttons.DPadLeft);
        }

        public static bool WasNextStylePressed()
        {
#if ANDROID || BLAZORGL
            if (touchStyleCyclePressed)
                return true;
#endif
            return WasKeyPressed(Keys.E) || WasButtonPressed(Buttons.DPadRight);
        }

        public static bool WasToggleViewPressed()
        {
#if ANDROID
            return false;
#else
            return WasKeyPressed(Keys.V) || WasButtonPressed(Buttons.RightShoulder);
#endif
        }

        public static bool WasConsoleTogglePressed()
        {
            return PlatformServices.Capabilities.SupportsTextInput && WasKeyPressed(Keys.OemTilde);
        }

        public static bool IsRewindHeld()
        {
            return rewindHeld;
        }

        public static Vector2 ConsumeMenuDragDelta()
        {
            Vector2 delta = uiPointerDragDelta;
            uiPointerDragDelta = Vector2.Zero;
            return delta;
        }

        public static bool IsMenuPointerHeld()
        {
            return uiPointerHeld;
        }

        public static bool IsMenuPointerDragging()
        {
            return uiPointerDragging;
        }

        public static int ConsumeUiPointerScrollWheelDelta()
        {
            int delta = uiPointerScrollWheelDelta;
            uiPointerScrollWheelDelta = 0;
            return delta;
        }

        public static void CaptureUiControl(int controlId)
        {
            capturedUiControlId = controlId;
        }

        public static bool IsUiControlCaptured(int controlId)
        {
            return capturedUiControlId == controlId;
        }

        public static void ClearUiControlCapture()
        {
            capturedUiControlId = -1;
        }

        public static bool WasNavigateUpPressed()
        {
            return WasKeyPressed(Keys.Up) || WasKeyPressed(Keys.W) || WasButtonPressed(Buttons.DPadUp);
        }

        public static bool WasNavigateDownPressed()
        {
            return WasKeyPressed(Keys.Down) || WasKeyPressed(Keys.S) || WasButtonPressed(Buttons.DPadDown);
        }

        public static bool WasNavigateLeftPressed()
        {
            return WasKeyPressed(Keys.Left) || WasKeyPressed(Keys.A) || WasButtonPressed(Buttons.DPadLeft);
        }

        public static bool WasNavigateRightPressed()
        {
            return WasKeyPressed(Keys.Right) || WasKeyPressed(Keys.D) || WasButtonPressed(Buttons.DPadRight);
        }

        public static bool IsFireHeld()
        {
            return fireHeld;
        }

        public static PlayerCommandFrame GetPlayerCommandFrame(ViewMode viewMode)
        {
            Vector2 movement = GetMovementDirection(viewMode);
            return new PlayerCommandFrame
            {
                TravelInput = viewMode == ViewMode.Chase3D ? 0f : movement.X,
                AltitudeInput = movement.Y,
                DepthInput = viewMode == ViewMode.Chase3D ? movement.X : 0f,
                ReticleDelta = GetChaseReticleDelta(),
                FireHeld = IsFireHeld(),
                RewindHeld = IsRewindHeld(),
                BoostHeld = IsBoostHeld(),
                BrakeHeld = IsBrakeHeld(),
            };
        }

        public static Vector2 GetMovementDirection()
        {
            return GetMovementDirection(ViewMode.SideScroller);
        }

        public static Vector2 GetMovementDirection(ViewMode viewMode)
        {
#if ANDROID || BLAZORGL
            if (touchMovementDirection != Vector2.Zero)
                return touchMovementDirection;
#endif

            Vector2 direction = viewMode == ViewMode.Chase3D
                ? new Vector2(gamepadState.ThumbSticks.Left.X, -gamepadState.ThumbSticks.Left.Y)
                : new Vector2(gamepadState.ThumbSticks.Left.X, -gamepadState.ThumbSticks.Left.Y);

            if (viewMode == ViewMode.Chase3D)
            {
                if (keyboardState.IsKeyDown(Keys.A))
                    direction.X -= 1f;
                if (keyboardState.IsKeyDown(Keys.D))
                    direction.X += 1f;
                if (keyboardState.IsKeyDown(Keys.W))
                    direction.Y -= 1f;
                if (keyboardState.IsKeyDown(Keys.S))
                    direction.Y += 1f;

                if (Game1.Instance?.Invert3DHorizontal == true)
                    direction.X *= -1f;
                if (Game1.Instance?.Invert3DVertical == true)
                    direction.Y *= -1f;
            }
            else
            {
                if (keyboardState.IsKeyDown(Keys.A))
                    direction.X -= 1f;
                if (keyboardState.IsKeyDown(Keys.D))
                    direction.X += 1f;
                if (keyboardState.IsKeyDown(Keys.W))
                    direction.Y -= 1f;
                if (keyboardState.IsKeyDown(Keys.S))
                    direction.Y += 1f;
            }

            if (direction.LengthSquared() > 1f)
                direction.Normalize();

            return direction;
        }

        public static Vector2 GetAimDirection()
        {
#if ANDROID || BLAZORGL
            if (touchAimDirection != Vector2.Zero)
                return touchAimDirection;
#endif

            Vector2 keyboardAim = Vector2.Zero;
            if (keyboardState.IsKeyDown(Keys.Left))
                keyboardAim.X -= 1f;
            if (keyboardState.IsKeyDown(Keys.Right))
                keyboardAim.X += 1f;
            if (keyboardState.IsKeyDown(Keys.Up))
                keyboardAim.Y -= 1f;
            if (keyboardState.IsKeyDown(Keys.Down))
                keyboardAim.Y += 1f;

            if (keyboardAim != Vector2.Zero)
            {
                keyboardAim.Normalize();
                return keyboardAim;
            }

            Vector2 gamepadAim = gamepadState.ThumbSticks.Right;
            gamepadAim.Y *= -1f;
            if (gamepadAim.LengthSquared() > 0.01f)
            {
                if (gamepadAim.LengthSquared() > 1f)
                    gamepadAim.Normalize();
                return gamepadAim;
            }

            return Vector2.Zero;
        }

        public static bool IsBoostHeld()
        {
#if ANDROID
            return false;
#else
            return keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
#endif
        }

        public static bool IsBrakeHeld()
        {
#if ANDROID
            return false;
#else
            return keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);
#endif
        }

        public static Keys[] GetPressedKeysThisFrame()
        {
#if ANDROID
            return Array.Empty<Keys>();
#else
            Keys[] pressed = keyboardState.GetPressedKeys();
            Keys[] buffer = new Keys[pressed.Length];
            int count = 0;
            for (int i = 0; i < pressed.Length; i++)
            {
                if (lastKeyboardState.IsKeyUp(pressed[i]))
                    buffer[count++] = pressed[i];
            }

            if (count == buffer.Length)
                return buffer;

            var result = new Keys[count];
            Array.Copy(buffer, result, count);
            return result;
#endif
        }

        public static void ConsumeKey(Keys key)
        {
#if !ANDROID
            consumedKeys.Add(key);
#endif
        }

        public static Vector2 GetChaseReticleDelta()
        {
#if ANDROID
            return Vector2.Zero;
#else
            Vector2 delta = gamepadState.ThumbSticks.Right;

            if (keyboardState.IsKeyDown(Keys.Left))
                delta.X -= 1f;
            if (keyboardState.IsKeyDown(Keys.Right))
                delta.X += 1f;
            if (keyboardState.IsKeyDown(Keys.Up))
                delta.Y += 1f;
            if (keyboardState.IsKeyDown(Keys.Down))
                delta.Y -= 1f;

            if (delta.LengthSquared() > 1f)
                delta.Normalize();

            return delta;
#endif
        }

#if ANDROID || BLAZORGL
        private static void UpdateTouchState(TouchCollection touches)
        {
            pointerPosition = Game1.ScreenSize / 2f;
            touchPausePressed = false;
            touchStyleCyclePressed = false;
            fireHeld = false;
            rewindHeld = false;

            if (!ShouldUseGameplayTouchControls())
            {
                ResetGameplayTouchState();
                UpdateMenuTouchState(touches);
                return;
            }

            Rectangle gameplayBounds = Game1.RenderBounds;
            float stickRadius = GetStickRadius(gameplayBounds);
            Vector2 movementOrigin = GetMovementStickOrigin(gameplayBounds, stickRadius);

            bool movementFound = false;
            bool aimFound = false;
            bool rewindFound = false;
            fireHeld = aimTouchId != -1;
            rewindHeld = false;
            Rectangle rewindBounds = GetRewindButtonBounds(gameplayBounds, stickRadius);
            HudLayout layout = GetTouchHudLayout();
            Rectangle weaponBounds = HudLayoutCalculator.GetAndroidWeaponTouchBounds(layout);
            Rectangle pauseBounds = HudLayoutCalculator.GetAndroidPauseTouchBounds(layout);
            bool topButtonFound = false;
            bool hadTrackedTopButton = touchTopButtonId != -1;

            foreach (TouchLocation touch in touches)
            {
                Vector2 rawScreenPosition = touch.Position;
                Vector2 screenPosition = Vector2.Clamp(
                    touch.Position,
                    new Vector2(gameplayBounds.Left, gameplayBounds.Top),
                    new Vector2(gameplayBounds.Right - 1, gameplayBounds.Bottom - 1));
                Vector2 uiPosition = Game1.ScreenToUi(rawScreenPosition);

                pointerPosition = uiPosition;

                if (touch.State == TouchLocationState.Pressed)
                    primaryActionPressed = true;

                if (touch.Id == rewindTouchId)
                {
                    rewindFound = true;
                    rewindHeld = true;
                    continue;
                }

                if (touch.Id == touchTopButtonId)
                {
                    topButtonFound = touch.State != TouchLocationState.Released && touch.State != TouchLocationState.Invalid;
                    Vector2 delta = uiPosition - touchTopButtonStartPosition;
                    bool tapRelease = touch.State == TouchLocationState.Released && delta.LengthSquared() <= MenuTapThreshold * MenuTapThreshold;
                    if (tapRelease)
                    {
                        if (touchTopButtonTarget == TouchTopButton.WeaponSwap)
                            touchStyleCyclePressed = true;
                        else if (touchTopButtonTarget == TouchTopButton.Pause)
                            touchPausePressed = true;
                    }

                    if (!topButtonFound)
                    {
                        touchTopButtonId = -1;
                        touchTopButtonTarget = TouchTopButton.None;
                        touchTopButtonStartPosition = Vector2.Zero;
                    }

                    continue;
                }

                if (touch.Id == movementTouchId)
                {
                    movementFound = true;
                    movementTouchPosition = ClampToRadius(screenPosition, movementOrigin, stickRadius);
                    continue;
                }

                if (touch.Id == aimTouchId)
                {
                    aimFound = true;
                    aimTouchPosition = screenPosition;
                    fireHeld = true;
                    continue;
                }

                if (rewindBounds.Contains(screenPosition))
                {
                    rewindTouchId = touch.Id;
                    rewindFound = true;
                    rewindHeld = true;
                }
                else if (weaponBounds.Contains(uiPosition))
                {
                    touchTopButtonId = touch.Id;
                    topButtonFound = true;
                    touchTopButtonTarget = TouchTopButton.WeaponSwap;
                    touchTopButtonStartPosition = uiPosition;
                }
                else if (pauseBounds.Contains(uiPosition))
                {
                    touchTopButtonId = touch.Id;
                    topButtonFound = true;
                    touchTopButtonTarget = TouchTopButton.Pause;
                    touchTopButtonStartPosition = uiPosition;
                }
                else if (screenPosition.X < gameplayBounds.Center.X)
                {
                    if (movementTouchId == -1)
                    {
                        movementTouchId = touch.Id;
                        movementFound = true;
                        movementTouchOrigin = movementOrigin;
                        movementTouchPosition = ClampToRadius(screenPosition, movementOrigin, stickRadius);
                    }
                }
                else if (aimTouchId == -1)
                {
                    aimTouchId = touch.Id;
                    aimFound = true;
                    aimTouchOrigin = screenPosition;
                    aimTouchPosition = screenPosition;
                    fireHeld = true;
                }
            }

            if (!movementFound)
            {
                movementTouchId = -1;
                movementTouchOrigin = movementOrigin;
                movementTouchPosition = movementOrigin;
            }

            if (!aimFound)
            {
                aimTouchId = -1;
                aimTouchOrigin = Vector2.Zero;
                aimTouchPosition = Vector2.Zero;
            }

            if (!rewindFound)
                rewindTouchId = -1;
            if (!topButtonFound)
            {
                if (hadTrackedTopButton)
                {
                    Vector2 delta = pointerPosition - touchTopButtonStartPosition;
                    if (delta.LengthSquared() <= MenuTapThreshold * MenuTapThreshold)
                    {
                        if (touchTopButtonTarget == TouchTopButton.WeaponSwap)
                            touchStyleCyclePressed = true;
                        else if (touchTopButtonTarget == TouchTopButton.Pause)
                            touchPausePressed = true;
                    }
                }

                touchTopButtonId = -1;
                touchTopButtonTarget = TouchTopButton.None;
                touchTopButtonStartPosition = Vector2.Zero;
            }

            touchMovementDirection = GetVirtualStickDirection(movementTouchOrigin, movementTouchPosition, stickRadius);

            if (aimTouchId == -1)
            {
                touchAimDirection = Vector2.Zero;
                fireHeld = false;
            }
            else
            {
                Vector2 delta = aimTouchPosition - aimTouchOrigin;
                touchAimDirection = delta.LengthSquared() > 625f ? Vector2.Normalize(new Vector2(delta.X, delta.Y)) : Vector2.Zero;
                fireHeld = true;
            }
        }

#if BLAZORGL
        private static void UpdateBrowserTouchState()
        {
            if (!PlatformServices.Capabilities.SupportsTouch)
                return;

            TouchCollection touches = TouchPanel.GetState();
            if (touches.Count > 0 || ShouldContinueBrowserTouchProcessing())
                UpdateTouchState(touches);
        }

        private static bool ShouldContinueBrowserTouchProcessing()
        {
            return menuTouchId != -1
                || menuTouchHeld
                || movementTouchId != -1
                || aimTouchId != -1
                || rewindTouchId != -1
                || touchTopButtonId != -1
                || touchPausePressed
                || touchStyleCyclePressed
                || touchMovementDirection != Vector2.Zero
                || touchAimDirection != Vector2.Zero;
        }
#endif

        public static void DrawTouchControls(SpriteBatch spriteBatch, Texture2D controlTexture)
        {
            if (controlTexture == null)
                return;

            float opacity = Game1.Instance != null ? Game1.Instance.TouchControlsOpacity : 0.58f;
            Rectangle gameplayBounds = Game1.RenderBounds;
            float stickRadius = GetStickRadius(gameplayBounds);
            Vector2 leftStick = GetMovementStickOrigin(gameplayBounds, stickRadius);
            Vector2 thumb = movementTouchId == -1 ? leftStick : movementTouchPosition;
            Vector2 rightPad = GetAimStickOrigin(gameplayBounds, stickRadius);

            DrawCircle(spriteBatch, controlTexture, leftStick, stickRadius * 1.12f, Color.White * (0.12f * opacity));
            DrawCircle(spriteBatch, controlTexture, leftStick, stickRadius * 0.7f, Color.White * (0.08f * opacity));
            DrawCircle(spriteBatch, controlTexture, thumb, stickRadius * 0.42f, Color.White * (0.28f * opacity));

            DrawCircle(spriteBatch, controlTexture, rightPad, stickRadius * 1.05f, Color.White * (0.08f * opacity));
            if (aimTouchId != -1)
            {
                DrawCircle(spriteBatch, controlTexture, aimTouchPosition, stickRadius * 0.34f, Color.White * (0.18f * opacity));
                if (touchAimDirection != Vector2.Zero)
                    DrawCircle(spriteBatch, controlTexture, aimTouchOrigin, stickRadius * 0.16f, Color.White * (0.24f * opacity));
            }

            Rectangle rewindBounds = GetRewindButtonBounds(gameplayBounds, stickRadius);
            Vector2 rewindCenter = new Vector2(rewindBounds.Center.X, rewindBounds.Center.Y);
            DrawCircle(spriteBatch, controlTexture, rewindCenter, rewindBounds.Width * 0.5f, Color.White * ((rewindTouchId == -1 ? 0.1f : 0.24f) * opacity));
            BitmapFontRenderer.Draw(spriteBatch, Game1.UiPixel, "R", rewindCenter - new Vector2(6f, 8f), Color.White * opacity, 1.4f);
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

        private static Vector2 GetMovementStickOrigin(Rectangle gameplayBounds, float radius)
        {
            float margin = radius * 0.7f;
            return new Vector2(gameplayBounds.Left + margin + radius, gameplayBounds.Bottom - margin - radius);
        }

        private static Vector2 GetAimStickOrigin(Rectangle gameplayBounds, float radius)
        {
            float margin = radius * 0.7f;
            return new Vector2(gameplayBounds.Right - margin - radius, gameplayBounds.Bottom - margin - radius);
        }

        private static Rectangle GetRewindButtonBounds(Rectangle gameplayBounds, float radius)
        {
            float margin = radius * 0.6f;
            int size = (int)(radius * 1.15f);
            return new Rectangle(
                (int)(gameplayBounds.Right - margin - size),
                (int)(gameplayBounds.Top + margin),
                size,
                size);
        }

        private static bool ShouldUseGameplayTouchControls()
        {
            return Game1.Instance?.CampaignDirector != null && Game1.Instance.CampaignDirector.ShouldDrawTouchControls;
        }

        private static void UpdateMenuTouchState(TouchCollection touches)
        {
            bool trackedMenuTouchFound = false;
            bool hadTrackedMenuTouch = menuTouchId != -1 && menuTouchHeld;
            bool releasedMenuTouchThisFrame = false;

            foreach (TouchLocation touch in touches)
            {
                Vector2 screenPosition = Vector2.Clamp(
                    touch.Position,
                    Vector2.Zero,
                    Game1.ScreenSize - Vector2.One);

                Vector2 uiPosition = Game1.ScreenToUi(screenPosition);
                pointerPosition = uiPosition;

                if (touch.Id == menuTouchId)
                {
                    trackedMenuTouchFound = touch.State != TouchLocationState.Released && touch.State != TouchLocationState.Invalid;
                    menuTouchHeld = trackedMenuTouchFound;
                    uiPointerHeld = trackedMenuTouchFound;
                    if (touch.State == TouchLocationState.Moved)
                    {
                        Vector2 delta = uiPosition - menuTouchLastPosition;
                        uiPointerDelta = delta;
                        menuTouchLastPosition = uiPosition;
                        if (!menuTouchDragging)
                        {
                            Vector2 totalDelta = uiPosition - menuTouchStartPosition;
                            menuTouchDragging = totalDelta.LengthSquared() > MenuTapThreshold * MenuTapThreshold;
                            uiPointerDragging = menuTouchDragging;
                        }

                        if (menuTouchDragging)
                            uiPointerDragDelta += delta;
                    }
                    else if (touch.State == TouchLocationState.Pressed)
                    {
                        menuTouchStartPosition = uiPosition;
                        menuTouchLastPosition = uiPosition;
                        menuTouchDragging = false;
                        menuTouchHeld = true;
                        uiPointerPressed = true;
                        uiPointerHeld = true;
                        uiPointerDragging = false;
                        uiPointerPressPosition = uiPosition;
                    }
                    else if (touch.State == TouchLocationState.Released)
                    {
                        Vector2 totalDelta = uiPosition - menuTouchStartPosition;
                        if (!menuTouchDragging && totalDelta.LengthSquared() <= MenuTapThreshold * MenuTapThreshold)
                            primaryActionPressed = true;

                        uiPointerReleased = true;
                        uiPointerReleasePosition = uiPosition;
                        releasedMenuTouchThisFrame = true;
                        menuTouchId = -1;
                        menuTouchStartPosition = Vector2.Zero;
                        menuTouchLastPosition = Vector2.Zero;
                        menuTouchDragging = false;
                        menuTouchHeld = false;
                        uiPointerHeld = false;
                        trackedMenuTouchFound = false;
                    }

                    continue;
                }

                if (menuTouchId == -1 && touch.State == TouchLocationState.Pressed)
                {
                    menuTouchId = touch.Id;
                    menuTouchStartPosition = uiPosition;
                    menuTouchLastPosition = uiPosition;
                    menuTouchHeld = true;
                    menuTouchDragging = false;
                    uiPointerPressed = true;
                    uiPointerHeld = true;
                    uiPointerDragging = false;
                    uiPointerPressPosition = uiPosition;
                    trackedMenuTouchFound = true;
                }
            }

            if (!trackedMenuTouchFound)
            {
                if (hadTrackedMenuTouch && !releasedMenuTouchThisFrame)
                {
                    Vector2 releasePosition = menuTouchLastPosition != Vector2.Zero ? menuTouchLastPosition : menuTouchStartPosition;
                    Vector2 totalDelta = releasePosition - menuTouchStartPosition;
                    if (!menuTouchDragging && totalDelta.LengthSquared() <= MenuTapThreshold * MenuTapThreshold)
                        primaryActionPressed = true;

                    uiPointerReleased = true;
                    uiPointerReleasePosition = releasePosition;
                }

                menuTouchId = -1;
                menuTouchHeld = false;
                menuTouchDragging = false;
                uiPointerHeld = false;
            }
        }

        private static void ResetGameplayTouchState()
        {
            menuTouchId = -1;
            menuTouchStartPosition = Vector2.Zero;
            menuTouchLastPosition = Vector2.Zero;
            menuTouchHeld = false;
            menuTouchDragging = false;
            movementTouchId = -1;
            aimTouchId = -1;
            rewindTouchId = -1;
            touchTopButtonId = -1;
            touchTopButtonTarget = TouchTopButton.None;
            touchTopButtonStartPosition = Vector2.Zero;
            movementTouchOrigin = Vector2.Zero;
            movementTouchPosition = Vector2.Zero;
            aimTouchOrigin = Vector2.Zero;
            aimTouchPosition = Vector2.Zero;
            touchMovementDirection = Vector2.Zero;
            touchAimDirection = Vector2.Zero;
        }

        private static HudLayout GetTouchHudLayout()
        {
            CampaignDirector director = Game1.Instance?.CampaignDirector;
            WeaponInventoryState inventory = PlayerStatus.RunProgress.Weapons;
            return HudLayoutCalculator.Calculate(
                Game1.VirtualWidth,
                director != null ? director.CurrentState : GameFlowState.Playing,
                director != null ? director.CurrentStageNumber : 1,
                director != null ? director.TransitionTargetStageNumber : 0,
                director != null && director.TransitionToBoss,
                inventory,
                PlayerStatus.Score);
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
#else
        public static void DrawTouchControls(SpriteBatch spriteBatch, Texture2D controlTexture)
        {
        }
#endif
    }
}
