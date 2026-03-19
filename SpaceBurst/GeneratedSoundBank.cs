using Microsoft.Xna.Framework.Audio;
using System;

namespace SpaceBurst
{
    sealed class GeneratedSoundBank : IDisposable
    {
        private readonly SoundEffect[] variations;
        private readonly Random random = new Random();

        public GeneratedSoundBank(params SoundEffect[] variations)
        {
            this.variations = variations ?? Array.Empty<SoundEffect>();
        }

        public void Play(float masterVolume, float sfxVolume, float volume, float pitch = 0f, float pan = 0f)
        {
            if (variations.Length == 0)
                return;

            SoundEffect effect = variations[random.Next(variations.Length)];
            effect.Play(
                Math.Clamp(masterVolume * sfxVolume * volume, 0f, 1f),
                Math.Clamp(pitch, -1f, 1f),
                Math.Clamp(pan, -1f, 1f));
        }

        public void Dispose()
        {
            for (int i = 0; i < variations.Length; i++)
                variations[i]?.Dispose();
        }
    }
}
