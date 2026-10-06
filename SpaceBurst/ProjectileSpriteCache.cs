using Microsoft.Xna.Framework.Graphics;
using SpaceBurst.RuntimeData;
using System;
using System.Collections.Generic;

namespace SpaceBurst
{
    // Projectile masks never take damage. One immutable sprite per specification
    // can be shared by all matching shots, including reconstructed rewind shots.
    sealed class ProjectileSpriteCache : IDisposable
    {
        private readonly GraphicsDevice graphicsDevice;
        private readonly Dictionary<(string, int, string, string, string, string, string), ProceduralSpriteInstance> sprites = new();

        public int Count { get { return sprites.Count; } }

        public ProjectileSpriteCache(GraphicsDevice graphicsDevice)
        {
            this.graphicsDevice = graphicsDevice;
        }

        public ProceduralSpriteInstance Get(ProceduralSpriteDefinition definition)
        {
            var key = (definition.Id, definition.PixelScale, definition.PrimaryColor,
                definition.SecondaryColor, definition.AccentColor,
                definition.Rows == null ? string.Empty : string.Join("\n", definition.Rows),
                definition.VitalCore?.Rows == null ? string.Empty : string.Join("\n", definition.VitalCore.Rows));
            if (!sprites.TryGetValue(key, out ProceduralSpriteInstance sprite))
            {
                sprite = new ProceduralSpriteInstance(graphicsDevice, definition);
                sprites.Add(key, sprite);
            }
            return sprite;
        }

        public void Dispose()
        {
            foreach (ProceduralSpriteInstance sprite in sprites.Values)
                sprite.Dispose();
            sprites.Clear();
        }
    }
}
