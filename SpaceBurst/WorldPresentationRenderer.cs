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
            public float Lateral { get; init; }
            public float Elevation { get; init; }
            public float Bank { get; init; }
            public bool IsPlayer { get; init; }
        }

        private readonly struct ChaseCamera
        {
            public ChaseCamera(float forward, float lateral, float height, float focalLength, float horizonY, float centerX)
            {
                Forward = forward;
                Lateral = lateral;
                Height = height;
                FocalLength = focalLength;
                HorizonY = horizonY;
                CenterX = centerX;
            }

            public float Forward { get; }
            public float Lateral { get; }
            public float Height { get; }
            public float FocalLength { get; }
            public float HorizonY { get; }
            public float CenterX { get; }
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
            ChaseCamera camera = BuildChaseCamera();
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

                projected.Add(ProjectEntity(entity, camera));
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

            if (item.IsPlayer)
            {
                DrawChasePlayerHull(spriteBatch, pixel, radialTexture, item.Position, scale, tint, accent, item.Bank, Player1.Instance.ActiveStyle, Player1.Instance.ActiveWeaponLevel, tier);
                return;
            }

            DrawProjectedShadow(spriteBatch, radialTexture, item.Position, scale, accent, item.Depth, item.Elevation);

            if (tier >= PresentationTier.VoxelShell)
                DrawProjectedVoxelShell(spriteBatch, pixel, radialTexture, cache, sprite, item.Position, scale, tint, accent, voxelAccentOnly ? 1 : 3, item.Depth);

            if (tier >= PresentationTier.HybridMesh)
                DrawProjectedMesh(spriteBatch, radialTexture, cache, item.Position, scale, accent, tier == PresentationTier.Late3D ? 0.34f : 0.22f, item.Depth);

            DrawProjectedBillboard(spriteBatch, sprite, item.Position, tint, entity.Orientation, scale, item.Depth);
        }

        private static ChaseCamera BuildChaseCamera()
        {
            float baseWidth = Game1.VirtualWidth;
            float baseHeight = Game1.VirtualHeight;
            Vector2 player = Player1.Instance != null ? Player1.Instance.Position : new Vector2(baseWidth * 0.2f, baseHeight * 0.5f);
            float playerLateral = ToWorldLateral(player.Y, baseHeight);
            float spawnX = baseWidth * 0.18f;
            float forwardLead = 208f + MathHelper.Clamp((player.X - spawnX) * 0.34f, -44f, 64f);
            return new ChaseCamera(
                player.X - forwardLead,
                playerLateral * 0.42f,
                144f,
                MathF.Max(420f, baseWidth * 0.58f),
                baseHeight * 0.25f,
                baseWidth * 0.5f);
        }

        private static float ToWorldLateral(float yPosition, float baseHeight)
        {
            return (yPosition - baseHeight * 0.5f) * 1.8f;
        }

        private static float GetEntityElevation(Entity entity)
        {
            Vector2 size = entity.Size;
            float sizeFactor = Math.Max(size.X, size.Y) * 0.08f;
            if (entity is Player1)
                return 24f + sizeFactor;

            if (entity is Bullet)
                return 12f + sizeFactor * 0.35f;

            return 18f + sizeFactor;
        }

        private static float GetEntityBank(Entity entity)
        {
            return MathHelper.Clamp(-entity.Velocity.Y * 0.0028f + entity.Velocity.X * 0.0008f, -0.48f, 0.48f);
        }

        private static ProjectedEntity ProjectEntity(Entity entity, ChaseCamera camera)
        {
            float baseWidth = Game1.VirtualWidth;
            float baseHeight = Game1.VirtualHeight;
            Vector2 player = Player1.Instance != null ? Player1.Instance.Position : new Vector2(baseWidth * 0.2f, baseHeight * 0.5f);
            float worldForward = entity.Position.X;
            float worldLateral = ToWorldLateral(entity.Position.Y, baseHeight) + entity.PresentationDepthBias * 110f;
            float depth = Math.Max(48f, worldForward - camera.Forward);
            float lateral = worldLateral - camera.Lateral;
            float elevation = GetEntityElevation(entity);
            float bank = GetEntityBank(entity);

            if (entity is Player1)
            {
                float spawnX = baseWidth * 0.18f;
                depth = MathHelper.Clamp(180f + (entity.Position.X - spawnX) * 0.48f, 144f, 248f);
                lateral = worldLateral - camera.Lateral;
                elevation = 24f + Math.Abs(entity.Velocity.Y) * 0.02f;
                return new ProjectedEntity
                {
                    Entity = entity,
                    Position = new Vector2(
                        camera.CenterX + lateral * camera.FocalLength / depth,
                        camera.HorizonY + (camera.Height - elevation) * camera.FocalLength / depth - lateral * 0.04f),
                    Scale = MathHelper.Clamp(camera.FocalLength / depth * 0.72f, 0.9f, 2.4f),
                    Depth = depth,
                    Lateral = lateral,
                    Elevation = elevation,
                    Bank = bank,
                    IsPlayer = true,
                };
            }

            float perspective = camera.FocalLength / depth;
            return new ProjectedEntity
            {
                Entity = entity,
                Position = new Vector2(
                    camera.CenterX + lateral * perspective,
                    camera.HorizonY + (camera.Height - elevation) * perspective - lateral * 0.03f * perspective),
                Scale = MathHelper.Clamp(perspective * 0.98f, entity is Bullet ? 0.14f : 0.2f, entity is BossEnemy ? 3.2f : 2.25f),
                Depth = depth,
                Lateral = lateral,
                Elevation = elevation,
                Bank = bank,
                IsPlayer = false,
            };
        }

        private static void DrawProjectedBillboard(SpriteBatch spriteBatch, ProceduralSpriteInstance sprite, Vector2 position, Color tint, float orientation, float scale, float depth)
        {
            Color depthTint = Color.Lerp(tint * 0.7f, tint, MathHelper.Clamp(1f - depth / 980f, 0.25f, 1f));
            sprite.Draw(spriteBatch, position, depthTint, orientation, scale);
        }

        private static void DrawProjectedShadow(SpriteBatch spriteBatch, Texture2D radialTexture, Vector2 position, float scale, Color accent, float depth, float elevation)
        {
            if (radialTexture == null)
                return;

            float alpha = MathHelper.Clamp(0.18f - depth / 3200f, 0.03f, 0.18f);
            float width = Math.Max(14f, scale * 42f);
            float height = Math.Max(5f, scale * 12f);
            Vector2 shadowPosition = new Vector2(position.X, position.Y + elevation * 0.24f + scale * 12f);
            spriteBatch.Draw(
                radialTexture,
                shadowPosition,
                null,
                Color.Lerp(Color.Black, accent, 0.15f) * alpha,
                0f,
                new Vector2(radialTexture.Width / 2f, radialTexture.Height / 2f),
                new Vector2(width / radialTexture.Width, height / radialTexture.Height),
                SpriteEffects.None,
                0f);
        }

        private static void DrawChasePlayerHull(
            SpriteBatch spriteBatch,
            Texture2D pixel,
            Texture2D radialTexture,
            Vector2 center,
            float scale,
            Color tint,
            Color accent,
            float bank,
            WeaponStyleId styleId,
            int level,
            PresentationTier tier)
        {
            float hullScale = MathHelper.Lerp(26f, 40f, MathHelper.Clamp(scale / 2.4f, 0f, 1f));
            float wingSpan = 2.35f;
            float fuselageWidth = 0.72f;
            float noseLength = 3.15f;
            float engineWidth = 1.18f;
            float bankFactor = 1f;

            switch (styleId)
            {
                case WeaponStyleId.Spread:
                    wingSpan = 2.9f;
                    fuselageWidth = 0.62f;
                    break;
                case WeaponStyleId.Laser:
                    wingSpan = 1.85f;
                    fuselageWidth = 0.58f;
                    noseLength = 3.85f;
                    break;
                case WeaponStyleId.Plasma:
                    wingSpan = 2.15f;
                    fuselageWidth = 0.92f;
                    break;
                case WeaponStyleId.Missile:
                    wingSpan = 2.55f;
                    engineWidth = 1.34f;
                    break;
                case WeaponStyleId.Rail:
                    wingSpan = 1.7f;
                    fuselageWidth = 0.56f;
                    noseLength = 4.2f;
                    break;
                case WeaponStyleId.Arc:
                    wingSpan = 2.25f;
                    bankFactor = 1.18f;
                    break;
                case WeaponStyleId.Blade:
                    wingSpan = 3.1f;
                    fuselageWidth = 0.54f;
                    break;
                case WeaponStyleId.Drone:
                    wingSpan = 2.5f;
                    engineWidth = 0.94f;
                    break;
                case WeaponStyleId.Fortress:
                    wingSpan = 3.25f;
                    fuselageWidth = 1.08f;
                    engineWidth = 1.45f;
                    break;
            }

            wingSpan += level * 0.12f;
            fuselageWidth += level * 0.04f;
            noseLength += level * 0.18f;
            float rotation = bank * bankFactor;
            Color hullColor = Color.Lerp(tint, Color.White, 0.14f);
            Color panelColor = Color.Lerp(hullColor, accent, 0.18f);
            Color shadowColor = Color.Black * 0.34f;

            DrawProjectedShadow(spriteBatch, radialTexture, center, scale * 1.1f, accent, 120f, 34f);

            DrawChaseBeam(spriteBatch, pixel, center, new Vector2(0f, -0.15f), new Vector2(0f, 2.95f + noseLength * 0.35f), hullScale, rotation, fuselageWidth * 0.92f, shadowColor);
            DrawChaseBeam(spriteBatch, pixel, center, new Vector2(0f, 0f), new Vector2(0f, 2.65f + noseLength * 0.3f), hullScale, rotation, fuselageWidth, hullColor);
            DrawChaseBeam(spriteBatch, pixel, center, new Vector2(0f, 0.12f), new Vector2(0f, 2.1f + noseLength * 0.18f), hullScale, rotation, fuselageWidth * 0.34f, Color.White * 0.9f);

            DrawChaseBeam(spriteBatch, pixel, center, new Vector2(0f, 0.6f), new Vector2(-wingSpan, -0.7f), hullScale, rotation, 0.34f + fuselageWidth * 0.42f, panelColor);
            DrawChaseBeam(spriteBatch, pixel, center, new Vector2(0f, 0.6f), new Vector2(wingSpan, -0.7f), hullScale, rotation, 0.34f + fuselageWidth * 0.42f, panelColor);
            DrawChaseBeam(spriteBatch, pixel, center, new Vector2(-wingSpan * 0.55f, -0.25f), new Vector2(-wingSpan * 0.88f, -1.45f), hullScale, rotation, 0.18f + level * 0.03f, accent * 0.78f);
            DrawChaseBeam(spriteBatch, pixel, center, new Vector2(wingSpan * 0.55f, -0.25f), new Vector2(wingSpan * 0.88f, -1.45f), hullScale, rotation, 0.18f + level * 0.03f, accent * 0.78f);

            if (tier >= PresentationTier.HybridMesh)
            {
                DrawChaseBeam(spriteBatch, pixel, center, new Vector2(0f, 1.7f), new Vector2(-wingSpan * 0.22f, 2.45f), hullScale, rotation, 0.24f, accent * 0.92f);
                DrawChaseBeam(spriteBatch, pixel, center, new Vector2(0f, 1.7f), new Vector2(wingSpan * 0.22f, 2.45f), hullScale, rotation, 0.24f, accent * 0.92f);
            }

            if (styleId == WeaponStyleId.Drone || styleId == WeaponStyleId.Fortress)
            {
                DrawChaseNode(spriteBatch, pixel, radialTexture, center, new Vector2(-wingSpan * 0.88f, 0.1f), hullScale, rotation, 0.5f, accent, hullColor);
                DrawChaseNode(spriteBatch, pixel, radialTexture, center, new Vector2(wingSpan * 0.88f, 0.1f), hullScale, rotation, 0.5f, accent, hullColor);
            }

            if (styleId == WeaponStyleId.Missile || styleId == WeaponStyleId.Rail)
            {
                DrawChaseBeam(spriteBatch, pixel, center, new Vector2(-fuselageWidth * 0.52f, 0.35f), new Vector2(-fuselageWidth * 0.52f, 2.25f), hullScale, rotation, 0.16f, accent * 0.85f);
                DrawChaseBeam(spriteBatch, pixel, center, new Vector2(fuselageWidth * 0.52f, 0.35f), new Vector2(fuselageWidth * 0.52f, 2.25f), hullScale, rotation, 0.16f, accent * 0.85f);
            }

            if (styleId == WeaponStyleId.Blade)
            {
                DrawChaseBeam(spriteBatch, pixel, center, new Vector2(-wingSpan * 0.3f, 0.85f), new Vector2(-wingSpan * 1.1f, 2.1f), hullScale, rotation, 0.14f, Color.White * 0.9f);
                DrawChaseBeam(spriteBatch, pixel, center, new Vector2(wingSpan * 0.3f, 0.85f), new Vector2(wingSpan * 1.1f, 2.1f), hullScale, rotation, 0.14f, Color.White * 0.9f);
            }

            DrawChaseNode(spriteBatch, pixel, radialTexture, center, new Vector2(0f, 1.35f + noseLength * 0.18f), hullScale, rotation, 0.36f + level * 0.02f, accent, Color.White);

            int thrusterCount = styleId == WeaponStyleId.Fortress ? 3 : styleId == WeaponStyleId.Spread ? 2 : 1;
            for (int i = 0; i < thrusterCount; i++)
            {
                float offset = thrusterCount == 1 ? 0f : (i - (thrusterCount - 1) * 0.5f) * engineWidth * 0.75f;
                Vector2 thrusterLocal = new Vector2(offset, -0.95f);
                DrawChaseThruster(spriteBatch, pixel, radialTexture, center, thrusterLocal, hullScale, rotation, accent, level);
            }
        }

        private static void DrawChaseThruster(SpriteBatch spriteBatch, Texture2D pixel, Texture2D radialTexture, Vector2 center, Vector2 local, float hullScale, float rotation, Color accent, int level)
        {
            Vector2 nozzle = TransformChasePoint(local, center, hullScale, rotation);
            float glowScale = 0.16f + level * 0.02f;
            Color flameColor = Color.Lerp(accent, Color.White, 0.25f);
            DrawChaseBeam(spriteBatch, pixel, center, local, new Vector2(local.X, local.Y - 0.68f - level * 0.08f), hullScale, rotation, 0.28f + level * 0.03f, flameColor * 0.78f);
            if (radialTexture != null)
            {
                spriteBatch.Draw(radialTexture, nozzle, null, flameColor * 0.34f, 0f, new Vector2(radialTexture.Width / 2f, radialTexture.Height / 2f), glowScale, SpriteEffects.None, 0f);
            }
        }

        private static void DrawChaseNode(SpriteBatch spriteBatch, Texture2D pixel, Texture2D radialTexture, Vector2 center, Vector2 local, float hullScale, float rotation, float radius, Color accent, Color fill)
        {
            Vector2 nodeCenter = TransformChasePoint(local, center, hullScale, rotation);
            int size = Math.Max(2, (int)MathF.Round(radius * hullScale * 0.34f));
            Rectangle rect = new Rectangle((int)MathF.Round(nodeCenter.X - size * 0.5f), (int)MathF.Round(nodeCenter.Y - size * 0.5f), size, size);
            spriteBatch.Draw(pixel, rect, fill);
            if (radialTexture != null)
            {
                spriteBatch.Draw(radialTexture, nodeCenter, null, accent * 0.22f, 0f, new Vector2(radialTexture.Width / 2f, radialTexture.Height / 2f), size * 1.8f / radialTexture.Width, SpriteEffects.None, 0f);
            }
        }

        private static void DrawChaseBeam(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, Vector2 startLocal, Vector2 endLocal, float hullScale, float rotation, float thickness, Color color)
        {
            Vector2 start = TransformChasePoint(startLocal, center, hullScale, rotation);
            Vector2 end = TransformChasePoint(endLocal, center, hullScale, rotation);
            DrawSegment(spriteBatch, pixel, start, end, color, Math.Max(1f, thickness * hullScale * 0.34f));
        }

        private static Vector2 TransformChasePoint(Vector2 local, Vector2 center, float hullScale, float rotation)
        {
            Vector2 scaled = new Vector2(local.X * hullScale, -local.Y * hullScale);
            float cos = MathF.Cos(rotation);
            float sin = MathF.Sin(rotation);
            return center + new Vector2(scaled.X * cos - scaled.Y * sin, scaled.X * sin + scaled.Y * cos);
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

        private static void DrawSegment(SpriteBatch spriteBatch, Texture2D pixel, Vector2 start, Vector2 end, Color color, float thickness)
        {
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length <= 0.1f)
                return;

            float rotation = MathF.Atan2(edge.Y, edge.X);
            spriteBatch.Draw(pixel, start, null, color, rotation, new Vector2(0f, 0.5f), new Vector2(length, Math.Max(1f, thickness)), SpriteEffects.None, 0f);
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
