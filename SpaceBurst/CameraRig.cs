using Microsoft.Xna.Framework;
using System;

namespace SpaceBurst
{
    sealed class CameraRig
    {
        private float trauma;
        private Vector2 recoil;
        private float time;

        public Vector2 WorldOffset { get; private set; }
        public float PulseStrength { get; private set; }

        public void AddShake(float amount)
        {
            trauma = MathHelper.Clamp(trauma + amount, 0f, 1f);
        }

        public void AddKick(Vector2 direction, float amount)
        {
            if (direction == Vector2.Zero)
                direction = Vector2.UnitX;
            else
                direction.Normalize();

            recoil += direction * amount;
        }

        public void Update(float deltaSeconds, float scrollSpeed, float transitionWarp, float rewindStrength, ScreenShakeStrength strength)
        {
            time += deltaSeconds;
            trauma = Math.Max(0f, trauma - deltaSeconds * 1.8f);
            recoil = Vector2.Lerp(recoil, Vector2.Zero, MathHelper.Clamp(8.5f * deltaSeconds, 0f, 1f));

            float amplitude = strength switch
            {
                ScreenShakeStrength.Off => 0f,
                ScreenShakeStrength.Reduced => 0.58f,
                _ => 1f,
            };

            float shake = trauma * trauma * 18f * amplitude;
            float driftX = MathF.Sin(time * 0.7f + scrollSpeed * 0.0012f) * 1.2f;
            float driftY = MathF.Cos(time * 0.46f) * 0.9f;
            float warpX = -transitionWarp * 4.5f;
            float rewindX = MathF.Sin(time * 14f) * rewindStrength * 2.5f;
            float shakeX = MathF.Sin(time * 39f) * shake + MathF.Cos(time * 19f) * shake * 0.45f;
            float shakeY = MathF.Cos(time * 31f) * shake * 0.8f + MathF.Sin(time * 17f) * shake * 0.35f;

            WorldOffset = recoil + new Vector2(driftX + warpX + rewindX + shakeX, driftY + shakeY);
            PulseStrength = MathHelper.Clamp(transitionWarp * 0.75f + trauma * 0.4f + rewindStrength * 0.25f, 0f, 1f);
        }
    }
}
