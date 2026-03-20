using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceBurst.RuntimeData;
using System;
using System.Collections.Generic;

namespace SpaceBurst
{
    sealed class MeshVolumeCache
    {
        public string Key { get; init; } = string.Empty;
        public VertexPositionColor[] OpaqueVertices { get; init; } = Array.Empty<VertexPositionColor>();
        public short[] OpaqueIndices { get; init; } = Array.Empty<short>();
        public VertexPositionColor[] GlowVertices { get; init; } = Array.Empty<VertexPositionColor>();
        public short[] GlowIndices { get; init; } = Array.Empty<short>();
        public Vector3 LocalMin { get; init; }
        public Vector3 LocalMax { get; init; }
    }

    sealed class ProceduralMeshInstance
    {
        public MeshVolumeCache Cache { get; init; }
        public Matrix World { get; init; }
        public float GlowStrength { get; init; }
    }

    readonly struct ChaseCameraState
    {
        public ChaseCameraState(Vector3 position, Vector3 target, float bank)
        {
            Position = position;
            Target = target;
            Bank = bank;
        }

        public Vector3 Position { get; }
        public Vector3 Target { get; }
        public float Bank { get; }

        public Vector3 Up
        {
            get
            {
                Matrix roll = Matrix.CreateRotationZ(Bank);
                return Vector3.Transform(Vector3.Up, roll);
            }
        }
    }

    readonly struct ChaseReticleState
    {
        public ChaseReticleState(Vector2 normalized, Vector2 screenPosition)
        {
            Normalized = normalized;
            ScreenPosition = screenPosition;
        }

        public Vector2 Normalized { get; }
        public Vector2 ScreenPosition { get; }
    }

    static class Late3DRenderer
    {
        private const float ForwardWorldScale = 0.84f;
        private const float DepthWorldScale = 0.22f;
        private const float AltitudeWorldScale = 0.16f;
        private const float CameraBaseHeight = 26f;
        private const float CameraBaseDistance = 78f;
        private const float CameraLookAhead = 168f;
        private const float CameraLookHeight = 7f;
        private const float ReticleHorizontalRange = 0.26f;
        private const float ReticleVerticalRange = 0.2f;

        private static readonly Dictionary<string, MeshVolumeCache> cacheByKey = new Dictionary<string, MeshVolumeCache>(StringComparer.Ordinal);
        private static readonly RasterizerState cullNoneState = new RasterizerState
        {
            CullMode = CullMode.None,
            FillMode = FillMode.Solid,
            MultiSampleAntiAlias = true,
        };

        private static BasicEffect opaqueEffect;
        private static BasicEffect glowEffect;
        private static bool cameraInitialized;
        private static Vector3 smoothedCameraPosition;
        private static Vector3 smoothedCameraTarget;
        private static float smoothedCameraBank;
        private static ChaseReticleState activeReticle;

        public static void ResetTransientState()
        {
            cameraInitialized = false;
            smoothedCameraPosition = Vector3.Zero;
            smoothedCameraTarget = Vector3.Forward;
            smoothedCameraBank = 0f;
            activeReticle = new ChaseReticleState(Vector2.Zero, GetReticleScreenPosition(Vector2.Zero));
        }

        public static Vector3 ResolveCombatAimDirection(Vector3 playerCombatPosition, Vector2 reticle)
        {
            ChaseCameraState camera = BuildDesiredCamera(playerCombatPosition, Player1.Instance?.CombatVelocity ?? Vector3.Zero, reticle);
            Matrix view = Matrix.CreateLookAt(camera.Position, camera.Target, camera.Up);
            Matrix projection = CreateProjection();
            Vector2 screen = GetReticleScreenPosition(reticle);
            var viewport = new Viewport(0, 0, Game1.VirtualWidth, Game1.VirtualHeight);

            Vector3 nearPoint = viewport.Unproject(new Vector3(screen, 0f), projection, view, Matrix.Identity);
            Vector3 farPoint = viewport.Unproject(new Vector3(screen, 1f), projection, view, Matrix.Identity);
            Vector3 worldDirection = farPoint - nearPoint;
            if (worldDirection == Vector3.Zero)
                return Vector3.UnitX;

            worldDirection.Normalize();
            Vector3 combatDirection = new Vector3(
                worldDirection.Z / ForwardWorldScale,
                worldDirection.Y / AltitudeWorldScale,
                worldDirection.X / DepthWorldScale);
            if (combatDirection == Vector3.Zero)
                return Vector3.UnitX;

            combatDirection.Normalize();
            return combatDirection;
        }

        public static void Draw(GraphicsDevice graphicsDevice, IEnumerable<Entity> entities, BackgroundMoodDefinition mood, VisualPreset preset)
        {
            if (graphicsDevice == null || entities == null || Player1.Instance == null)
                return;

            EnsureEffects(graphicsDevice);

            Vector3 playerCombatPosition = Player1.Instance.CombatPosition;
            Vector3 playerCombatVelocity = Player1.Instance.CombatVelocity;
            Vector2 reticleNormalized = Player1.Instance.ChaseReticle;
            activeReticle = new ChaseReticleState(reticleNormalized, GetReticleScreenPosition(reticleNormalized));

            ChaseCameraState desiredCamera = BuildDesiredCamera(playerCombatPosition, playerCombatVelocity, reticleNormalized);
            float deltaSeconds = Game1.GameTime == null ? 1f / 60f : (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            float cameraBlend = MathHelper.Clamp(deltaSeconds * 5.2f, 0f, 1f);
            if (!cameraInitialized)
            {
                smoothedCameraPosition = desiredCamera.Position;
                smoothedCameraTarget = desiredCamera.Target;
                smoothedCameraBank = desiredCamera.Bank;
                cameraInitialized = true;
            }
            else
            {
                smoothedCameraPosition = Vector3.Lerp(smoothedCameraPosition, desiredCamera.Position, cameraBlend);
                smoothedCameraTarget = Vector3.Lerp(smoothedCameraTarget, desiredCamera.Target, cameraBlend);
                smoothedCameraBank = MathHelper.Lerp(smoothedCameraBank, desiredCamera.Bank, cameraBlend);
            }

            var camera = new ChaseCameraState(smoothedCameraPosition, smoothedCameraTarget, smoothedCameraBank);
            Matrix view = Matrix.CreateLookAt(camera.Position, camera.Target, camera.Up);
            Matrix projection = CreateProjection();
            ConfigureEffects(view, projection, mood, preset);

            graphicsDevice.BlendState = BlendState.AlphaBlend;
            graphicsDevice.DepthStencilState = DepthStencilState.Default;
            graphicsDevice.RasterizerState = cullNoneState;

            DrawEntityMeshes(graphicsDevice, entities);
            DrawBeams(graphicsDevice, entities);

            graphicsDevice.DepthStencilState = DepthStencilState.None;
            graphicsDevice.BlendState = BlendState.AlphaBlend;
        }

        public static void DrawOverlayEntities(SpriteBatch spriteBatch, IEnumerable<Entity> entities)
        {
            if (spriteBatch == null || entities == null)
                return;

            foreach (Entity entity in entities)
            {
                if (entity == null || entity.IsExpired)
                    continue;

                if (entity.SpriteInstance != null || entity is BeamShot)
                    continue;

                entity.Draw(spriteBatch);
            }
        }

        public static void DrawReticle(SpriteBatch spriteBatch, Texture2D pixel, Texture2D radialTexture, float intensity)
        {
            if (spriteBatch == null || pixel == null)
                return;

            Vector2 center = activeReticle.ScreenPosition;
            Color reticleColor = Color.White * MathHelper.Clamp(0.7f + intensity * 0.25f, 0.7f, 1f);
            int radius = 14;
            int gap = 5;
            spriteBatch.Draw(pixel, new Rectangle((int)center.X - radius, (int)center.Y - 1, radius - gap, 2), reticleColor);
            spriteBatch.Draw(pixel, new Rectangle((int)center.X + gap, (int)center.Y - 1, radius - gap, 2), reticleColor);
            spriteBatch.Draw(pixel, new Rectangle((int)center.X - 1, (int)center.Y - radius, 2, radius - gap), reticleColor);
            spriteBatch.Draw(pixel, new Rectangle((int)center.X - 1, (int)center.Y + gap, 2, radius - gap), reticleColor);
            spriteBatch.Draw(pixel, new Rectangle((int)center.X - 1, (int)center.Y - 1, 2, 2), Color.Cyan * 0.95f);

            if (radialTexture != null)
            {
                spriteBatch.Draw(
                    radialTexture,
                    center,
                    null,
                    Color.Cyan * 0.08f,
                    0f,
                    new Vector2(radialTexture.Width / 2f, radialTexture.Height / 2f),
                    24f / radialTexture.Width,
                    SpriteEffects.None,
                    0f);
            }
        }

        public static void DrawInsetHud(SpriteBatch spriteBatch, Texture2D pixel, IEnumerable<Entity> entities)
        {
            if (spriteBatch == null || pixel == null || Player1.Instance == null)
                return;

            Rectangle panel = new Rectangle(Game1.VirtualWidth - 234, Game1.VirtualHeight - 152, 214, 124);
            Rectangle content = new Rectangle(panel.X + 12, panel.Y + 22, panel.Width - 24, panel.Height - 34);
            int playerX = content.X + 18;
            int playerY = content.Center.Y;
            float forwardRange = 1040f;
            float altitudeScale = 0.14f;

            spriteBatch.Draw(pixel, panel, Color.Black * 0.56f);
            DrawUiBorder(spriteBatch, pixel, panel, Color.White * 0.18f);
            spriteBatch.Draw(pixel, new Rectangle(content.X, content.Center.Y, content.Width, 1), Color.White * 0.08f);
            BitmapFontRenderer.Draw(spriteBatch, pixel, "SIDE VIEW", new Vector2(panel.X + 12, panel.Y + 8), Color.White * 0.72f, 0.62f);

            AutoAcquire2DResolver.Result autoAcquire = AutoAcquire2DResolver.Resolve(Player1.Instance.CombatPosition, Player1.Instance.CannonDirection, EntityManager.Enemies);
            Vector2 playerMarker = new Vector2(playerX, playerY);
            Vector2? targetMarker = null;

            foreach (Entity entity in entities)
            {
                if (entity == null || entity.IsExpired || entity is BeamShot)
                    continue;

                float relativeTravel = entity.Travel - Player1.Instance.Travel;
                if (relativeTravel < -140f || relativeTravel > 900f)
                    continue;

                float relativeAltitude = entity.Altitude - Player1.Instance.Altitude;
                float relativeDepth = entity.LateralDepth - Player1.Instance.LateralDepth;
                float t = MathHelper.Clamp((relativeTravel + 140f) / forwardRange, 0f, 1f);
                float x = content.X + 6f + t * (content.Width - 12f);
                float y = content.Center.Y - relativeAltitude * altitudeScale;
                y = MathHelper.Clamp(y, content.Y + 4f, content.Bottom - 4f);

                Color tint = ResolveInsetColor(entity, relativeDepth);
                int markerSize = entity is BossEnemy ? 6 : entity is Bullet ? 2 : entity is PowerupPickup ? 3 : 4;
                Rectangle marker = new Rectangle((int)MathF.Round(x - markerSize * 0.5f), (int)MathF.Round(y - markerSize * 0.5f), markerSize, markerSize);
                spriteBatch.Draw(pixel, marker, tint);

                int depthHeight = Math.Max(1, (int)MathF.Round(MathHelper.Clamp(MathF.Abs(relativeDepth) / 26f, 1f, 9f)));
                if (depthHeight > markerSize)
                {
                    Rectangle depthBar = new Rectangle(marker.Center.X, marker.Center.Y - depthHeight / 2, 1, depthHeight);
                    spriteBatch.Draw(pixel, depthBar, tint * 0.5f);
                }

                if (autoAcquire.Target == entity)
                    targetMarker = new Vector2(marker.Center.X, marker.Center.Y);
            }

            spriteBatch.Draw(pixel, new Rectangle(playerX - 4, playerY - 4, 8, 8), Color.Cyan * 0.95f);
            if (targetMarker.HasValue)
                DrawUiLine(spriteBatch, pixel, playerMarker, targetMarker.Value, Color.Gold * 0.52f, 2f);
        }

        private static void DrawEntityMeshes(GraphicsDevice graphicsDevice, IEnumerable<Entity> entities)
        {
            foreach (Entity entity in entities)
            {
                if (entity == null || entity.IsExpired || entity.SpriteInstance == null)
                    continue;

                ProceduralMeshInstance mesh = CreateMeshInstance(entity);
                if (mesh == null || mesh.Cache == null || mesh.Cache.OpaqueIndices.Length == 0)
                    continue;

                opaqueEffect.World = mesh.World;
                for (int passIndex = 0; passIndex < opaqueEffect.CurrentTechnique.Passes.Count; passIndex++)
                {
                    EffectPass pass = opaqueEffect.CurrentTechnique.Passes[passIndex];
                    pass.Apply();
                    graphicsDevice.DrawUserIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        mesh.Cache.OpaqueVertices,
                        0,
                        mesh.Cache.OpaqueVertices.Length,
                        mesh.Cache.OpaqueIndices,
                        0,
                        mesh.Cache.OpaqueIndices.Length / 3);
                }

                if (mesh.Cache.GlowIndices.Length > 0)
                {
                    graphicsDevice.BlendState = BlendState.Additive;
                    graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
                    glowEffect.World = mesh.World;
                    glowEffect.Alpha = MathHelper.Clamp(mesh.GlowStrength, 0.15f, 1f);
                    for (int passIndex = 0; passIndex < glowEffect.CurrentTechnique.Passes.Count; passIndex++)
                    {
                        EffectPass pass = glowEffect.CurrentTechnique.Passes[passIndex];
                        pass.Apply();
                        graphicsDevice.DrawUserIndexedPrimitives(
                            PrimitiveType.TriangleList,
                            mesh.Cache.GlowVertices,
                            0,
                            mesh.Cache.GlowVertices.Length,
                            mesh.Cache.GlowIndices,
                            0,
                            mesh.Cache.GlowIndices.Length / 3);
                    }

                    graphicsDevice.BlendState = BlendState.AlphaBlend;
                    graphicsDevice.DepthStencilState = DepthStencilState.Default;
                }
            }
        }

        private static void DrawBeams(GraphicsDevice graphicsDevice, IEnumerable<Entity> entities)
        {
            foreach (Entity entity in entities)
            {
                if (entity is not BeamShot beam || beam.IsExpired)
                    continue;

                Vector3 start = MapCombatPoint(beam.CombatPosition, 1.8f);
                Vector3 end = MapCombatPoint(beam.CombatPosition + beam.CombatDirection * beam.Length, 1.8f);
                Vector3 axis = end - start;
                if (axis.LengthSquared() <= 0.0001f)
                    continue;

                axis.Normalize();
                Vector3 toCamera = smoothedCameraPosition - (start + end) * 0.5f;
                if (toCamera == Vector3.Zero)
                    toCamera = Vector3.Backward;
                toCamera.Normalize();

                Vector3 right = Vector3.Cross(axis, toCamera);
                if (right == Vector3.Zero)
                    right = Vector3.Right;
                right.Normalize();

                float beamWidth = Math.Max(0.55f, beam.Thickness * 0.08f);
                Vector3 offset = right * beamWidth;
                Color outer = ColorUtil.ParseHex(beam.AccentColorHex, Color.Cyan) * 0.48f;
                Color inner = ColorUtil.ParseHex(beam.PrimaryColorHex, Color.White) * 0.92f;

                DrawBeamQuad(graphicsDevice, start, end, offset * 1.7f, outer, true);
                DrawBeamQuad(graphicsDevice, start, end, offset * 0.65f, inner, false);
            }
        }

        private static void DrawBeamQuad(GraphicsDevice graphicsDevice, Vector3 start, Vector3 end, Vector3 offset, Color color, bool additive)
        {
            var vertices = new[]
            {
                new VertexPositionColor(start - offset, color),
                new VertexPositionColor(start + offset, color),
                new VertexPositionColor(end + offset, color),
                new VertexPositionColor(end - offset, color),
            };
            short[] indices = { 0, 1, 2, 0, 2, 3 };

            if (additive)
            {
                graphicsDevice.BlendState = BlendState.Additive;
                graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
                glowEffect.World = Matrix.Identity;
                glowEffect.Alpha = 0.6f;
                for (int passIndex = 0; passIndex < glowEffect.CurrentTechnique.Passes.Count; passIndex++)
                {
                    EffectPass pass = glowEffect.CurrentTechnique.Passes[passIndex];
                    pass.Apply();
                    graphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length, indices, 0, 2);
                }

                graphicsDevice.BlendState = BlendState.AlphaBlend;
                graphicsDevice.DepthStencilState = DepthStencilState.Default;
            }
            else
            {
                opaqueEffect.World = Matrix.Identity;
                for (int passIndex = 0; passIndex < opaqueEffect.CurrentTechnique.Passes.Count; passIndex++)
                {
                    EffectPass pass = opaqueEffect.CurrentTechnique.Passes[passIndex];
                    pass.Apply();
                    graphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length, indices, 0, 2);
                }
            }
        }

        private static ProceduralMeshInstance CreateMeshInstance(Entity entity)
        {
            MeshVolumeCache cache = GetMeshCache(entity.SpriteInstance);
            if (cache.OpaqueVertices.Length == 0)
                return null;

            Vector3 worldPosition = MapCombatPoint(entity.CombatPosition, ResolveEntityElevation(entity));
            float yaw;
            float pitch;
            float roll;

            if (entity is Player1 player)
            {
                Vector3 worldAim = MapCombatDirection(player.CombatAimDirection);
                if (worldAim == Vector3.Zero)
                    worldAim = Vector3.Forward;
                else
                    worldAim.Normalize();

                yaw = MathHelper.Clamp(MathF.Atan2(worldAim.X, worldAim.Z) * 0.72f, -0.48f, 0.48f);
                pitch = MathHelper.Clamp(-MathF.Asin(MathHelper.Clamp(worldAim.Y, -1f, 1f)) * 0.42f + player.TravelVelocity * 0.0007f, -0.26f, 0.3f);
                roll = MathHelper.Clamp(-player.DepthVelocity * 0.0038f, -0.55f, 0.55f);
            }
            else
            {
                Vector3 combatDirection = entity.CombatVelocity;
                if (combatDirection == Vector3.Zero)
                    combatDirection = entity.IsFriendly ? Vector3.UnitX : -Vector3.UnitX;

                Vector3 worldDirection = MapCombatDirection(combatDirection);
                if (worldDirection == Vector3.Zero)
                    worldDirection = entity.IsFriendly ? Vector3.Forward : Vector3.Backward;
                else
                    worldDirection.Normalize();

                yaw = MathF.Atan2(worldDirection.X, worldDirection.Z);
                pitch = MathHelper.Clamp(-MathF.Asin(MathHelper.Clamp(worldDirection.Y, -1f, 1f)) * 0.65f, -0.32f, 0.32f);
                roll = MathHelper.Clamp(-entity.DepthVelocity * 0.0024f, -0.34f, 0.34f);
            }

            float scale = ResolveWorldScale(entity);
            Matrix rotation = Matrix.CreateFromYawPitchRoll(yaw, pitch, roll);
            Matrix world = rotation * Matrix.CreateScale(scale) * Matrix.CreateTranslation(worldPosition);
            float glowStrength = entity is BossEnemy ? 0.95f : entity is Bullet ? 0.45f : 0.68f;

            if (entity is Player1 && Player1.Instance.CannonSpriteInstance != null)
                DrawPlayerCannon(worldPosition);

            return new ProceduralMeshInstance
            {
                Cache = cache,
                World = world,
                GlowStrength = glowStrength,
            };
        }

        private static void DrawPlayerCannon(Vector3 playerWorldPosition)
        {
            ProceduralSpriteInstance cannon = Player1.Instance.CannonSpriteInstance;
            if (cannon == null || opaqueEffect == null)
                return;

            MeshVolumeCache cache = GetMeshCache(cannon);
            if (cache.OpaqueIndices.Length == 0)
                return;

            Vector3 worldAim = MapCombatDirection(Player1.Instance.CombatAimDirection);
            if (worldAim == Vector3.Zero)
                worldAim = Vector3.Forward;
            else
                worldAim.Normalize();

            float yaw = MathHelper.Clamp(MathF.Atan2(worldAim.X, worldAim.Z) * 0.85f, -0.62f, 0.62f);
            float pitch = MathHelper.Clamp(-MathF.Asin(MathHelper.Clamp(worldAim.Y, -1f, 1f)) * 0.54f, -0.3f, 0.28f);
            float roll = MathHelper.Clamp(-Player1.Instance.DepthVelocity * 0.0018f, -0.18f, 0.18f);
            Matrix rotation = Matrix.CreateFromYawPitchRoll(yaw, pitch, roll);
            Vector3 offset = Vector3.Transform(new Vector3(0f, 2.6f, 10.5f), rotation);
            Matrix world = rotation * Matrix.CreateScale(cannon.PixelScale * 0.26f) * Matrix.CreateTranslation(playerWorldPosition + offset);
            GraphicsDevice graphicsDevice = Game1.Instance.GraphicsDevice;

            opaqueEffect.World = world;
            for (int passIndex = 0; passIndex < opaqueEffect.CurrentTechnique.Passes.Count; passIndex++)
            {
                EffectPass pass = opaqueEffect.CurrentTechnique.Passes[passIndex];
                pass.Apply();
                graphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    cache.OpaqueVertices,
                    0,
                    cache.OpaqueVertices.Length,
                    cache.OpaqueIndices,
                    0,
                    cache.OpaqueIndices.Length / 3);
            }

            if (cache.GlowIndices.Length > 0)
            {
                graphicsDevice.BlendState = BlendState.Additive;
                graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
                glowEffect.World = world;
                glowEffect.Alpha = 0.75f;
                for (int passIndex = 0; passIndex < glowEffect.CurrentTechnique.Passes.Count; passIndex++)
                {
                    EffectPass pass = glowEffect.CurrentTechnique.Passes[passIndex];
                    pass.Apply();
                    graphicsDevice.DrawUserIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        cache.GlowVertices,
                        0,
                        cache.GlowVertices.Length,
                        cache.GlowIndices,
                        0,
                        cache.GlowIndices.Length / 3);
                }

                graphicsDevice.BlendState = BlendState.AlphaBlend;
                graphicsDevice.DepthStencilState = DepthStencilState.Default;
            }
        }

        private static MeshVolumeCache GetMeshCache(ProceduralSpriteInstance sprite)
        {
            string key = string.Concat(sprite.RenderStateKey, "::late3d");
            if (cacheByKey.TryGetValue(key, out MeshVolumeCache existing))
                return existing;

            MaskGrid mask = sprite.Mask;
            ProceduralSpriteDefinition definition = sprite.Definition;
            int width = mask.Width;
            int height = mask.Height;
            int lateralWidth = Math.Max(5, Math.Min(17, width / 2 + 6));
            int center = lateralWidth / 2;
            int[] columnOccupancy = new int[width];
            bool[,,] volume = new bool[lateralWidth, height, width];
            bool[,,] core = new bool[lateralWidth, height, width];
            Color[,,] colorVolume = new Color[lateralWidth, height, width];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (mask.IsOccupied(x, y))
                        columnOccupancy[x]++;
                }
            }

            for (int y = 0; y < height; y++)
            {
                string row = definition.Rows != null && y < definition.Rows.Count ? definition.Rows[y] ?? string.Empty : string.Empty;
                for (int x = 0; x < width; x++)
                {
                    if (!mask.IsOccupied(x, y))
                        continue;

                    bool isCore = mask.IsCore(x, y);
                    char glyph = x < row.Length ? row[x] : '#';
                    Color baseColor = ResolveCellColor(definition, glyph, isCore);
                    int halfThickness = ComputeHalfThickness(x, y, width, height, columnOccupancy[x], isCore, lateralWidth);
                    int start = Math.Max(0, center - halfThickness);
                    int end = Math.Min(lateralWidth - 1, center + halfThickness);
                    for (int lateral = start; lateral <= end; lateral++)
                    {
                        volume[lateral, y, x] = true;
                        core[lateral, y, x] = isCore;
                        colorVolume[lateral, y, x] = baseColor;
                    }
                }
            }

            var opaqueVertices = new List<VertexPositionColor>();
            var opaqueIndices = new List<short>();
            var glowVertices = new List<VertexPositionColor>();
            var glowIndices = new List<short>();
            Vector3 localMin = new Vector3(float.MaxValue);
            Vector3 localMax = new Vector3(float.MinValue);

            for (int lateral = 0; lateral < lateralWidth; lateral++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (!volume[lateral, y, x])
                            continue;

                        Vector3 centerPoint = GetLocalVoxelCenter(lateral, y, x, lateralWidth, height, width);
                        Vector3 half = new Vector3(0.33f, 0.42f, 0.48f);
                        UpdateBounds(ref localMin, ref localMax, centerPoint - half, centerPoint + half);
                        Color baseColor = colorVolume[lateral, y, x];

                        if (!IsOccupied(volume, lateral + 1, y, x))
                            AddFace(opaqueVertices, opaqueIndices, centerPoint, half, Face.Right, ShadeFace(baseColor, 0.78f));
                        if (!IsOccupied(volume, lateral - 1, y, x))
                            AddFace(opaqueVertices, opaqueIndices, centerPoint, half, Face.Left, ShadeFace(baseColor, 0.66f));
                        if (!IsOccupied(volume, lateral, y - 1, x))
                            AddFace(opaqueVertices, opaqueIndices, centerPoint, half, Face.Top, ShadeFace(baseColor, 1.08f));
                        if (!IsOccupied(volume, lateral, y + 1, x))
                            AddFace(opaqueVertices, opaqueIndices, centerPoint, half, Face.Bottom, ShadeFace(baseColor, 0.48f));
                        if (!IsOccupied(volume, lateral, y, x + 1))
                            AddFace(opaqueVertices, opaqueIndices, centerPoint, half, Face.Front, ShadeFace(baseColor, 0.92f));
                        if (!IsOccupied(volume, lateral, y, x - 1))
                            AddFace(opaqueVertices, opaqueIndices, centerPoint, half, Face.Back, ShadeFace(baseColor, 0.54f));

                        if (core[lateral, y, x])
                        {
                            Vector3 glowHalf = half * 0.52f;
                            Color glowColor = Color.Lerp(baseColor, Color.White, 0.45f) * 0.85f;
                            AddBox(glowVertices, glowIndices, centerPoint, glowHalf, glowColor);
                        }
                    }
                }
            }

            if (opaqueVertices.Count == 0)
            {
                localMin = Vector3.Zero;
                localMax = Vector3.Zero;
            }

            var cache = new MeshVolumeCache
            {
                Key = key,
                OpaqueVertices = opaqueVertices.ToArray(),
                OpaqueIndices = opaqueIndices.ToArray(),
                GlowVertices = glowVertices.ToArray(),
                GlowIndices = glowIndices.ToArray(),
                LocalMin = localMin,
                LocalMax = localMax,
            };
            cacheByKey[key] = cache;
            return cache;
        }

        private static int ComputeHalfThickness(int x, int y, int width, int height, int columnOccupancy, bool isCore, int lateralWidth)
        {
            float forwardT = width <= 1 ? 1f : x / (float)(width - 1);
            float centerBias = 1f - MathF.Abs(forwardT * 2f - 1f);
            float rowT = height <= 1 ? 0.5f : y / (float)(height - 1);
            float rowBias = 1f - MathF.Abs(rowT * 2f - 1f);
            float columnBias = columnOccupancy / (float)Math.Max(1, height);
            float thickness = 0.4f + centerBias * 2.1f + columnBias * 1.1f + rowBias * 0.5f + (isCore ? 0.85f : 0f);
            return Math.Clamp((int)MathF.Round(thickness), 1, Math.Max(1, (lateralWidth - 1) / 2));
        }

        private static Vector3 GetLocalVoxelCenter(int lateral, int y, int x, int lateralWidth, int height, int width)
        {
            float localX = (lateral - (lateralWidth - 1) * 0.5f) * 0.72f;
            float localY = ((height - 1) * 0.5f - y) * 0.84f;
            float localZ = (x - (width - 1) * 0.5f) * 1f;
            return new Vector3(localX, localY, localZ);
        }

        private static bool IsOccupied(bool[,,] volume, int lateral, int y, int x)
        {
            return lateral >= 0 &&
                   y >= 0 &&
                   x >= 0 &&
                   lateral < volume.GetLength(0) &&
                   y < volume.GetLength(1) &&
                   x < volume.GetLength(2) &&
                   volume[lateral, y, x];
        }

        private static void AddBox(List<VertexPositionColor> vertices, List<short> indices, Vector3 center, Vector3 half, Color color)
        {
            AddFace(vertices, indices, center, half, Face.Left, color);
            AddFace(vertices, indices, center, half, Face.Right, color);
            AddFace(vertices, indices, center, half, Face.Top, color);
            AddFace(vertices, indices, center, half, Face.Bottom, color);
            AddFace(vertices, indices, center, half, Face.Front, color);
            AddFace(vertices, indices, center, half, Face.Back, color);
        }

        private static void AddFace(List<VertexPositionColor> vertices, List<short> indices, Vector3 center, Vector3 half, Face face, Color color)
        {
            Vector3 a;
            Vector3 b;
            Vector3 c;
            Vector3 d;
            switch (face)
            {
                case Face.Left:
                    a = center + new Vector3(-half.X, half.Y, -half.Z);
                    b = center + new Vector3(-half.X, half.Y, half.Z);
                    c = center + new Vector3(-half.X, -half.Y, half.Z);
                    d = center + new Vector3(-half.X, -half.Y, -half.Z);
                    break;
                case Face.Right:
                    a = center + new Vector3(half.X, half.Y, half.Z);
                    b = center + new Vector3(half.X, half.Y, -half.Z);
                    c = center + new Vector3(half.X, -half.Y, -half.Z);
                    d = center + new Vector3(half.X, -half.Y, half.Z);
                    break;
                case Face.Top:
                    a = center + new Vector3(-half.X, half.Y, half.Z);
                    b = center + new Vector3(half.X, half.Y, half.Z);
                    c = center + new Vector3(half.X, half.Y, -half.Z);
                    d = center + new Vector3(-half.X, half.Y, -half.Z);
                    break;
                case Face.Bottom:
                    a = center + new Vector3(-half.X, -half.Y, -half.Z);
                    b = center + new Vector3(half.X, -half.Y, -half.Z);
                    c = center + new Vector3(half.X, -half.Y, half.Z);
                    d = center + new Vector3(-half.X, -half.Y, half.Z);
                    break;
                case Face.Front:
                    a = center + new Vector3(-half.X, half.Y, half.Z);
                    b = center + new Vector3(-half.X, -half.Y, half.Z);
                    c = center + new Vector3(half.X, -half.Y, half.Z);
                    d = center + new Vector3(half.X, half.Y, half.Z);
                    break;
                default:
                    a = center + new Vector3(half.X, half.Y, -half.Z);
                    b = center + new Vector3(half.X, -half.Y, -half.Z);
                    c = center + new Vector3(-half.X, -half.Y, -half.Z);
                    d = center + new Vector3(-half.X, half.Y, -half.Z);
                    break;
            }

            AddQuad(vertices, indices, a, b, c, d, color);
        }

        private static void AddQuad(List<VertexPositionColor> vertices, List<short> indices, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            short baseIndex = (short)vertices.Count;
            vertices.Add(new VertexPositionColor(a, color));
            vertices.Add(new VertexPositionColor(b, color));
            vertices.Add(new VertexPositionColor(c, color));
            vertices.Add(new VertexPositionColor(d, color));
            indices.Add(baseIndex);
            indices.Add((short)(baseIndex + 1));
            indices.Add((short)(baseIndex + 2));
            indices.Add(baseIndex);
            indices.Add((short)(baseIndex + 2));
            indices.Add((short)(baseIndex + 3));
        }

        private static Color ResolveCellColor(ProceduralSpriteDefinition definition, char glyph, bool isCore)
        {
            Color primary = ColorUtil.ParseHex(definition.PrimaryColor, Color.White);
            Color secondary = ColorUtil.ParseHex(definition.SecondaryColor, primary);
            Color accent = ColorUtil.ParseHex(definition.AccentColor, secondary);
            Color baseColor = glyph switch
            {
                '+' or 'o' => secondary,
                '*' or 'x' or '@' => accent,
                _ => primary,
            };

            return isCore ? Color.Lerp(baseColor, Color.White, 0.2f) : baseColor;
        }

        private static Color ShadeFace(Color color, float factor)
        {
            return new Color(
                (byte)Math.Clamp((int)MathF.Round(color.R * factor), 0, 255),
                (byte)Math.Clamp((int)MathF.Round(color.G * factor), 0, 255),
                (byte)Math.Clamp((int)MathF.Round(color.B * factor), 0, 255),
                color.A);
        }

        private static void UpdateBounds(ref Vector3 min, ref Vector3 max, Vector3 localMin, Vector3 localMax)
        {
            min = Vector3.Min(min, localMin);
            max = Vector3.Max(max, localMax);
        }

        private static Vector3 MapCombatPoint(Vector3 combatPoint, float elevation)
        {
            float lateral = combatPoint.Z * DepthWorldScale;
            float altitude = combatPoint.Y * AltitudeWorldScale + elevation;
            float forward = combatPoint.X * ForwardWorldScale;
            return new Vector3(lateral, altitude, forward);
        }

        private static float ResolveEntityElevation(Entity entity)
        {
            if (entity is Player1)
                return 0f;

            if (entity is Bullet)
                return 1.5f;

            if (entity is BossEnemy boss)
                return 4.5f + boss.Size.Y * 0.03f;

            return 2.8f + entity.Size.Y * 0.018f + entity.PresentationDepthBias;
        }

        private static float ResolveWorldScale(Entity entity)
        {
            float baseScale = entity.SpriteInstance.PixelScale * 0.36f * entity.RenderScale * entity.PresentationScaleMultiplier;
            if (entity is Player1)
                return baseScale * 1.18f;
            if (entity is Bullet)
                return baseScale * 0.72f;
            return baseScale;
        }

        private static ChaseCameraState BuildDesiredCamera(Vector3 playerCombatPosition, Vector3 playerCombatVelocity, Vector2 reticle)
        {
            Vector3 playerWorld = MapCombatPoint(playerCombatPosition, 0f);
            float lateralLead = playerCombatVelocity.Z * 0.022f;
            float altitudeLead = playerCombatVelocity.Y * 0.016f;
            float forwardPush = MathHelper.Clamp(playerCombatVelocity.X * 0.045f, -18f, 28f);
            Vector3 position = playerWorld + new Vector3(lateralLead * 0.7f, CameraBaseHeight + altitudeLead, -CameraBaseDistance - forwardPush);
            Vector3 target = new Vector3(
                reticle.X * 34f + lateralLead,
                CameraLookHeight + reticle.Y * 26f + altitudeLead * 0.35f,
                CameraLookAhead + Math.Max(0f, playerCombatVelocity.X * 0.08f));
            target += playerWorld;
            float bank = MathHelper.Clamp(-playerCombatVelocity.Z * 0.0026f, -0.24f, 0.24f);
            return new ChaseCameraState(position, target, bank);
        }

        private static Vector3 MapCombatDirection(Vector3 combatDirection)
        {
            return new Vector3(
                combatDirection.Z * DepthWorldScale,
                combatDirection.Y * AltitudeWorldScale,
                combatDirection.X * ForwardWorldScale);
        }

        private static Matrix CreateProjection()
        {
            float aspectRatio = Math.Max(1f, Game1.VirtualWidth) / (float)Math.Max(1, Game1.VirtualHeight);
            return Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(52f), aspectRatio, 1f, 1800f);
        }

        private static Vector2 GetReticleScreenPosition(Vector2 normalized)
        {
            float x = Game1.VirtualWidth * 0.5f + normalized.X * Game1.VirtualWidth * ReticleHorizontalRange;
            float y = Game1.VirtualHeight * 0.46f - normalized.Y * Game1.VirtualHeight * ReticleVerticalRange;
            return new Vector2(
                MathHelper.Clamp(x, 96f, Game1.VirtualWidth - 96f),
                MathHelper.Clamp(y, 96f, Game1.VirtualHeight - 96f));
        }

        private static void EnsureEffects(GraphicsDevice graphicsDevice)
        {
            if (opaqueEffect == null || opaqueEffect.GraphicsDevice != graphicsDevice)
            {
                opaqueEffect?.Dispose();
                glowEffect?.Dispose();

                opaqueEffect = new BasicEffect(graphicsDevice)
                {
                    VertexColorEnabled = true,
                    LightingEnabled = false,
                    TextureEnabled = false,
                    FogEnabled = true,
                };

                glowEffect = new BasicEffect(graphicsDevice)
                {
                    VertexColorEnabled = true,
                    LightingEnabled = false,
                    TextureEnabled = false,
                    FogEnabled = true,
                };
            }
        }

        private static void ConfigureEffects(Matrix view, Matrix projection, BackgroundMoodDefinition mood, VisualPreset preset)
        {
            Color fogBase = ColorUtil.ParseHex(mood?.PrimaryColor, new Color(8, 12, 26));
            Vector3 fogColor = new Vector3(fogBase.R / 255f, fogBase.G / 255f, fogBase.B / 255f);
            float fogEnd = preset == VisualPreset.Neon ? 1500f : 1180f;
            float fogStart = preset == VisualPreset.Low ? 260f : 180f;

            opaqueEffect.View = view;
            opaqueEffect.Projection = projection;
            opaqueEffect.FogColor = fogColor;
            opaqueEffect.FogStart = fogStart;
            opaqueEffect.FogEnd = fogEnd;
            opaqueEffect.Alpha = 1f;

            glowEffect.View = view;
            glowEffect.Projection = projection;
            glowEffect.FogColor = fogColor;
            glowEffect.FogStart = fogStart * 0.7f;
            glowEffect.FogEnd = fogEnd;
            glowEffect.Alpha = 0.7f;
        }

        private static Color ResolveInsetColor(Entity entity, float relativeDepth)
        {
            Color baseColor;
            if (entity is BossEnemy)
                baseColor = new Color(255, 186, 92);
            else if (entity is Bullet bullet)
                baseColor = bullet.Friendly ? new Color(102, 244, 255) : new Color(255, 146, 112);
            else if (entity is PowerupPickup)
                baseColor = new Color(124, 214, 255);
            else
                baseColor = entity.IsFriendly ? new Color(112, 228, 255) : new Color(255, 236, 220);

            if (relativeDepth == 0f)
                return baseColor;

            Color depthTint = relativeDepth > 0f ? new Color(118, 188, 255) : new Color(255, 124, 214);
            return Color.Lerp(baseColor, depthTint, MathHelper.Clamp(MathF.Abs(relativeDepth) / 140f, 0.18f, 0.55f));
        }

        private static void DrawUiBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, Color color)
        {
            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), color);
        }

        private static void DrawUiLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 from, Vector2 to, Color color, float thickness)
        {
            Vector2 delta = to - from;
            if (delta.LengthSquared() <= 0.001f)
                return;

            float length = delta.Length();
            float rotation = MathF.Atan2(delta.Y, delta.X);
            spriteBatch.Draw(pixel, from, null, color, rotation, new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        private enum Face
        {
            Left,
            Right,
            Top,
            Bottom,
            Front,
            Back,
        }
    }
}
