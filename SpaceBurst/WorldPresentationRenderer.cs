using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceBurst.RuntimeData;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaceBurst
{
    sealed class RenderableHullCache
    {
        public string Key { get; init; } = string.Empty;
        public int Width { get; init; }
        public int Height { get; init; }
        public VoxelHullInstance[] Voxels { get; init; } = Array.Empty<VoxelHullInstance>();
        public MeshHullInstance Mesh { get; init; } = new MeshHullInstance();
    }

    readonly struct VoxelHullInstance
    {
        public VoxelHullInstance(Point cell, bool isCore, bool exposeLeft, bool exposeRight, bool exposeTop, bool exposeBottom)
        {
            Cell = cell;
            IsCore = isCore;
            ExposeLeft = exposeLeft;
            ExposeRight = exposeRight;
            ExposeTop = exposeTop;
            ExposeBottom = exposeBottom;
        }

        public Point Cell { get; }
        public bool IsCore { get; }
        public bool ExposeLeft { get; }
        public bool ExposeRight { get; }
        public bool ExposeTop { get; }
        public bool ExposeBottom { get; }
    }

    sealed class MeshHullInstance
    {
        public Rectangle Bounds { get; init; }
        public Point[] OutlineCells { get; init; } = Array.Empty<Point>();
    }

    static class WorldPresentationRenderer
    {
        private sealed class ProjectedEntity
        {
            public Entity Entity { get; init; }
            public Vector2 Position { get; init; }
            public float Scale { get; init; }
            public float Depth { get; init; }
        }

        private static readonly Dictionary<string, RenderableHullCache> hullCacheByKey = new Dictionary<string, RenderableHullCache>(StringComparer.Ordinal);

        public static void Draw(
            SpriteBatch spriteBatch,
            Texture2D pixel,
            Texture2D radialTexture,
            IEnumerable<Entity> entities,
            PresentationTier tier,
            ViewMode viewMode,
            int stageNumber)
        {
            if (entities == null)
                return;

            bool voxelAccentOnly = PresentationProgression.IsVoxelAccentStage(stageNumber);
            if (viewMode == ViewMode.Chase3D)
            {
                DrawProjected(spriteBatch, pixel, radialTexture, entities, tier, voxelAccentOnly);
                return;
            }

            foreach (Entity entity in entities)
            {
                if (entity == null || entity.IsExpired)
                    continue;

                if (entity.SpriteInstance == null || tier == PresentationTier.Pixel2D)
                {
                    entity.Draw(spriteBatch);
                    continue;
                }

                DrawSideScrollerEntity(spriteBatch, pixel, radialTexture, entity, tier, voxelAccentOnly);
            }
        }

        private static void DrawProjected(
            SpriteBatch spriteBatch,
            Texture2D pixel,
            Texture2D radialTexture,
            IEnumerable<Entity> entities,
            PresentationTier tier,
            bool voxelAccentOnly)
        {
            var projected = new List<ProjectedEntity>();
            var passthrough = new List<Entity>();
            foreach (Entity entity in entities)
            {
                if (entity == null || entity.IsExpired)
                    continue;

                if (entity.SpriteInstance == null)
                {
                    passthrough.Add(entity);
                    continue;
                }

                projected.Add(ProjectEntity(entity));
            }

            foreach (ProjectedEntity item in projected.OrderByDescending(value => value.Depth))
                DrawProjectedEntity(spriteBatch, pixel, radialTexture, item, tier, voxelAccentOnly);

            for (int i = 0; i < passthrough.Count; i++)
                passthrough[i].Draw(spriteBatch);
        }

        private static void DrawSideScrollerEntity(SpriteBatch spriteBatch, Texture2D pixel, Texture2D radialTexture, Entity entity, PresentationTier tier, bool voxelAccentOnly)
        {
            ProceduralSpriteInstance sprite = entity.SpriteInstance;
            RenderableHullCache cache = GetHullCache(sprite);
            float scale = entity.RenderScale * entity.PresentationScaleMultiplier;
            Vector2 position = entity.Position;
            Color tint = entity.RenderTint;
            Color accent = ResolveAccent(entity);

            if (tier >= PresentationTier.VoxelShell)
                DrawSideVoxelShell(spriteBatch, pixel, cache, sprite, position, scale, tint, accent, voxelAccentOnly ? 2 : 4);

            if (tier >= PresentationTier.HybridMesh)
                DrawSideMesh(spriteBatch, radialTexture, cache, sprite, position, scale, accent, tier == PresentationTier.Late3D ? 0.32f : 0.22f);

            sprite.Draw(spriteBatch, position, tint, entity.Orientation, scale);
        }

        private static void DrawProjectedEntity(SpriteBatch spriteBatch, Texture2D pixel, Texture2D radialTexture, ProjectedEntity item, PresentationTier tier, bool voxelAccentOnly)
        {
            Entity entity = item.Entity;
            ProceduralSpriteInstance sprite = entity.SpriteInstance;
            RenderableHullCache cache = GetHullCache(sprite);
            Color tint = entity.RenderTint;
            Color accent = ResolveAccent(entity);
            float scale = item.Scale * entity.PresentationScaleMultiplier;

            if (tier >= PresentationTier.VoxelShell)
                DrawProjectedVoxelShell(spriteBatch, pixel, radialTexture, cache, sprite, item.Position, scale, tint, accent, voxelAccentOnly ? 1 : 3, item.Depth);

            if (tier >= PresentationTier.HybridMesh)
                DrawProjectedMesh(spriteBatch, radialTexture, cache, item.Position, scale, accent, tier == PresentationTier.Late3D ? 0.34f : 0.22f, item.Depth);

            DrawProjectedBillboard(spriteBatch, sprite, item.Position, tint, entity.Orientation, scale, item.Depth);
        }

        private static ProjectedEntity ProjectEntity(Entity entity)
        {
            float baseWidth = Game1.VirtualWidth;
            float baseHeight = Game1.VirtualHeight;
            Vector2 player = Player1.Instance != null ? Player1.Instance.Position : new Vector2(baseWidth * 0.2f, baseHeight * 0.5f);
            float forward = Math.Max(-120f, entity.Position.X - player.X);
            float relativeY = player.Y - entity.Position.Y;
            float lane = (entity.PresentationDepthBias * 180f) + ((entity.Position.Y / Math.Max(1f, baseHeight)) - 0.5f) * 90f;
            float depth = 260f + Math.Max(0f, forward) * 0.82f;
            float perspective = MathHelper.Clamp(560f / (560f + depth), 0.18f, 1.28f);

            if (entity is Player1)
            {
                return new ProjectedEntity
                {
                    Entity = entity,
                    Position = new Vector2(baseWidth * 0.5f + lane * 0.35f, baseHeight * 0.76f),
                    Scale = 1.38f,
                    Depth = 0f,
                };
            }

            return new ProjectedEntity
            {
                Entity = entity,
                Position = new Vector2(
                    baseWidth * 0.5f + lane * perspective,
                    baseHeight * 0.76f - relativeY * 0.42f * perspective - forward * 0.08f),
                Scale = Math.Max(0.22f, perspective * 1.65f),
                Depth = depth,
            };
        }

        private static void DrawProjectedBillboard(SpriteBatch spriteBatch, ProceduralSpriteInstance sprite, Vector2 position, Color tint, float orientation, float scale, float depth)
        {
            Color depthTint = Color.Lerp(tint * 0.7f, tint, MathHelper.Clamp(1f - depth / 980f, 0.25f, 1f));
            sprite.Draw(spriteBatch, position, depthTint, orientation, scale);
        }

        private static void DrawSideVoxelShell(SpriteBatch spriteBatch, Texture2D pixel, RenderableHullCache cache, ProceduralSpriteInstance sprite, Vector2 position, float scale, Color tint, Color accent, int depthSlices)
        {
            float worldPixel = sprite.PixelScale * scale;
            Vector2 topLeft = position - sprite.WorldSize * scale / 2f;
            for (int slice = depthSlices; slice >= 1; slice--)
            {
                Vector2 offset = new Vector2(slice * worldPixel * 0.14f, -slice * worldPixel * 0.1f);
                float shadeFactor = 0.14f + slice * 0.04f;
                foreach (VoxelHullInstance voxel in cache.Voxels)
                {
                    Rectangle cell = new Rectangle(
                        (int)MathF.Round(topLeft.X + voxel.Cell.X * worldPixel + offset.X),
                        (int)MathF.Round(topLeft.Y + voxel.Cell.Y * worldPixel + offset.Y),
                        Math.Max(1, (int)MathF.Ceiling(worldPixel)),
                        Math.Max(1, (int)MathF.Ceiling(worldPixel)));
                    Color shade = voxel.IsCore ? Color.Lerp(accent, Color.White, 0.18f) : tint;
                    spriteBatch.Draw(pixel, cell, shade * shadeFactor);
                }
            }
        }

        private static void DrawProjectedVoxelShell(SpriteBatch spriteBatch, Texture2D pixel, Texture2D radialTexture, RenderableHullCache cache, ProceduralSpriteInstance sprite, Vector2 position, float scale, Color tint, Color accent, int depthSlices, float depth)
        {
            float cellSize = Math.Max(1.8f, sprite.PixelScale * scale * 0.34f);
            Vector2 topLeft = position - new Vector2(cache.Width * cellSize, cache.Height * cellSize) / 2f;
            float light = MathHelper.Clamp(1f - depth / 1200f, 0.2f, 1f);
            for (int slice = depthSlices; slice >= 0; slice--)
            {
                float sliceOffset = slice * Math.Max(1f, cellSize * 0.7f);
                float sliceShade = 0.12f + (depthSlices - slice) * 0.06f;
                foreach (VoxelHullInstance voxel in cache.Voxels)
                {
                    float x = topLeft.X + voxel.Cell.X * cellSize + sliceOffset;
                    float y = topLeft.Y + voxel.Cell.Y * cellSize - sliceOffset * 0.38f;
                    Rectangle cell = new Rectangle((int)MathF.Round(x), (int)MathF.Round(y), Math.Max(1, (int)MathF.Ceiling(cellSize)), Math.Max(1, (int)MathF.Ceiling(cellSize)));
                    Color shade = voxel.IsCore ? Color.Lerp(accent, Color.White, 0.26f) : tint;
                    spriteBatch.Draw(pixel, cell, shade * (sliceShade * light));
                }
            }

            if (radialTexture != null && depth < 520f)
            {
                float glowScale = MathHelper.Lerp(0.22f, 0.48f, light);
                spriteBatch.Draw(radialTexture, position, null, accent * 0.08f * light, 0f, new Vector2(radialTexture.Width / 2f, radialTexture.Height / 2f), glowScale, SpriteEffects.None, 0f);
            }
        }

        private static void DrawSideMesh(SpriteBatch spriteBatch, Texture2D radialTexture, RenderableHullCache cache, ProceduralSpriteInstance sprite, Vector2 position, float scale, Color accent, float intensity)
        {
            Rectangle meshBounds = GetEntityBounds(cache.Mesh.Bounds, sprite, position, scale);
            DrawBoundsGlow(spriteBatch, radialTexture, meshBounds, accent * intensity, 0.34f);
            DrawBoundsOutline(spriteBatch, meshBounds, accent * Math.Min(1f, intensity + 0.15f));
        }

        private static void DrawProjectedMesh(SpriteBatch spriteBatch, Texture2D radialTexture, RenderableHullCache cache, Vector2 position, float scale, Color accent, float intensity, float depth)
        {
            int width = Math.Max(10, (int)MathF.Round(cache.Mesh.Bounds.Width * scale * 0.55f));
            int height = Math.Max(10, (int)MathF.Round(cache.Mesh.Bounds.Height * scale * 0.55f));
            var bounds = new Rectangle((int)MathF.Round(position.X - width * 0.5f), (int)MathF.Round(position.Y - height * 0.5f), width, height);
            float depthFactor = MathHelper.Clamp(1f - depth / 1400f, 0.2f, 1f);
            DrawBoundsGlow(spriteBatch, radialTexture, bounds, accent * intensity * depthFactor, 0.42f);
            DrawBoundsOutline(spriteBatch, bounds, accent * (0.28f + depthFactor * 0.18f));
        }

        private static Rectangle GetEntityBounds(Rectangle localBounds, ProceduralSpriteInstance sprite, Vector2 position, float scale)
        {
            float worldPixel = sprite.PixelScale * scale;
            Vector2 topLeft = position - sprite.WorldSize * scale / 2f;
            return new Rectangle(
                (int)MathF.Round(topLeft.X + localBounds.X * worldPixel),
                (int)MathF.Round(topLeft.Y + localBounds.Y * worldPixel),
                Math.Max(1, (int)MathF.Round(localBounds.Width * worldPixel)),
                Math.Max(1, (int)MathF.Round(localBounds.Height * worldPixel)));
        }

        private static void DrawBoundsGlow(SpriteBatch spriteBatch, Texture2D radialTexture, Rectangle bounds, Color color, float scale)
        {
            if (radialTexture == null)
                return;

            spriteBatch.Draw(
                radialTexture,
                new Vector2(bounds.Center.X, bounds.Center.Y),
                null,
                color,
                0f,
                new Vector2(radialTexture.Width / 2f, radialTexture.Height / 2f),
                new Vector2(Math.Max(bounds.Width, 1) * scale / radialTexture.Width, Math.Max(bounds.Height, 1) * scale / radialTexture.Height),
                SpriteEffects.None,
                0f);
        }

        private static void DrawBoundsOutline(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            Texture2D pixel = Game1.UiPixel;
            if (pixel == null || bounds.Width <= 0 || bounds.Height <= 0)
                return;

            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), color);
        }

        private static RenderableHullCache GetHullCache(ProceduralSpriteInstance sprite)
        {
            string key = sprite.RenderStateKey;
            if (string.IsNullOrEmpty(key))
                key = Guid.NewGuid().ToString("N");

            if (hullCacheByKey.TryGetValue(key, out RenderableHullCache existing))
                return existing;

            var voxels = new List<VoxelHullInstance>();
            var outlineCells = new List<Point>();
            MaskGrid mask = sprite.Mask;
            Rectangle bounds = new Rectangle(mask.Width, mask.Height, 0, 0);
            bool found = false;

            for (int y = 0; y < mask.Height; y++)
            {
                for (int x = 0; x < mask.Width; x++)
                {
                    if (!mask.IsOccupied(x, y))
                        continue;

                    bool exposeLeft = !mask.IsInside(x - 1, y) || !mask.IsOccupied(x - 1, y);
                    bool exposeRight = !mask.IsInside(x + 1, y) || !mask.IsOccupied(x + 1, y);
                    bool exposeTop = !mask.IsInside(x, y - 1) || !mask.IsOccupied(x, y - 1);
                    bool exposeBottom = !mask.IsInside(x, y + 1) || !mask.IsOccupied(x, y + 1);
                    voxels.Add(new VoxelHullInstance(new Point(x, y), mask.IsCore(x, y), exposeLeft, exposeRight, exposeTop, exposeBottom));
                    if (exposeLeft || exposeRight || exposeTop || exposeBottom)
                        outlineCells.Add(new Point(x, y));

                    if (!found)
                    {
                        bounds = new Rectangle(x, y, 1, 1);
                        found = true;
                    }
                    else
                    {
                        bounds = Rectangle.Union(bounds, new Rectangle(x, y, 1, 1));
                    }
                }
            }

            var cache = new RenderableHullCache
            {
                Key = key,
                Width = mask.Width,
                Height = mask.Height,
                Voxels = voxels.ToArray(),
                Mesh = new MeshHullInstance
                {
                    Bounds = found ? bounds : Rectangle.Empty,
                    OutlineCells = outlineCells.ToArray(),
                },
            };

            hullCacheByKey[key] = cache;
            return cache;
        }

        private static Color ResolveAccent(Entity entity)
        {
            return entity switch
            {
                Enemy enemy => enemy.PresentationAccentColor,
                _ => ColorUtil.ParseHex(entity.SpriteInstance?.AccentColorHex, entity.RenderTint),
            };
        }
    }
}
