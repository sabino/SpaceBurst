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
        private const float CameraBaseDistance = 118f;
        private const float CameraLookAhead = 244f;
        private const float CameraLookHeight = 10f;
        private const float CameraSideAnchor = -22f;
        private const float CameraTargetSideAnchor = -8f;
        private const float ChaseEntryDurationSeconds = 0.7f;
        private const float ReticleMarginX = 14f;
        private const float ReticleMarginY = 18f;

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
        private static float chaseEntryTimer;

        public static void ResetTransientState()
        {
            cameraInitialized = false;
            smoothedCameraPosition = Vector3.Zero;
            smoothedCameraTarget = Vector3.Forward;
            smoothedCameraBank = 0f;
            activeReticle = new ChaseReticleState(Vector2.Zero, GetReticleScreenPosition(Vector2.Zero));
            chaseEntryTimer = 0f;
        }

        public static void BeginChaseEntry(Vector3 playerCombatPosition)
        {
            chaseEntryTimer = ChaseEntryDurationSeconds;
            activeReticle = new ChaseReticleState(Vector2.Zero, GetReticleScreenPosition(Vector2.Zero));
        }

        public static Vector2 ClampReticle(Vector2 normalized)
        {
            return Vector2.Clamp(normalized, new Vector2(-1f, -1f), new Vector2(1f, 1f));
        }

        public static Vector3 ResolveCombatAimDirection(Vector3 playerCombatPosition, Vector2 reticle)
        {
            Vector3 aimCombatPoint = ResolveCombatAimPoint(playerCombatPosition, reticle);
            Vector3 combatDirection = aimCombatPoint - playerCombatPosition;
            if (combatDirection == Vector3.Zero)
                return Vector3.UnitX;

            combatDirection.Normalize();
            return combatDirection;
        }

        public static Vector3 ResolveCombatAimPoint(Vector3 playerCombatPosition, Vector2 reticle)
        {
            ChaseCameraState camera = ResolveAimCamera(playerCombatPosition, reticle);
            Matrix view = Matrix.CreateLookAt(camera.Position, camera.Target, camera.Up);
            Matrix projection = CreateProjection();
            Vector2 screen = GetReticleScreenPosition(reticle);
            var viewport = new Viewport(0, 0, Game1.VirtualWidth, Game1.VirtualHeight);
            Vector3 playerWorld = MapCombatPoint(playerCombatPosition, 0f);

            Vector3 nearPoint = viewport.Unproject(new Vector3(screen, 0f), projection, view, Matrix.Identity);
            Vector3 farPoint = viewport.Unproject(new Vector3(screen, 1f), projection, view, Matrix.Identity);
            Vector3 worldDirection = farPoint - nearPoint;
            if (worldDirection == Vector3.Zero)
                return playerCombatPosition + Vector3.UnitX * 1200f;

            worldDirection.Normalize();
            float planeForward = playerWorld.Z + CameraLookAhead + 540f;
            float rayT = Math.Abs(worldDirection.Z) < 0.0001f
                ? 1200f
                : (planeForward - nearPoint.Z) / worldDirection.Z;
            if (rayT < 96f)
                rayT = 1200f;

            Vector3 aimWorldPoint = nearPoint + worldDirection * rayT;
            return MapWorldPointToCombat(aimWorldPoint);
        }

        internal static bool TryResolvePlayerCombatAimRay(Player1 player, out Vector3 combatOrigin, out Vector3 combatTarget)
        {
            combatOrigin = Vector3.Zero;
            combatTarget = Vector3.UnitX * 1200f;
            if (player == null || player.SpriteInstance == null || player.CannonSpriteInstance == null)
                return false;

            if (!TryResolvePlayerCannonPose(player, out _, out _, out Vector3 muzzleWorld, out _))
                return false;

            Vector3 muzzleCombat = MapWorldPointToCombat(muzzleWorld);
            Vector3 aimCombatPoint = ResolveCombatAimPoint(player.CombatPosition, player.ChaseReticle);
            Vector3 aimDirection = aimCombatPoint - muzzleCombat;
            if (aimDirection == Vector3.Zero)
                aimDirection = player.CombatAimDirection == Vector3.Zero ? Vector3.UnitX : player.CombatAimDirection;
            else
                aimDirection.Normalize();

            combatOrigin = muzzleCombat;
            combatTarget = muzzleCombat + aimDirection * 1400f;
            return true;
        }

        public static bool TryProjectEntityToReticle(Entity entity, Vector3 playerCombatPosition, Vector2 currentReticle, out Vector2 normalized)
        {
            normalized = Vector2.Zero;
            if (entity == null)
                return false;

            ChaseCameraState camera = ResolveAimCamera(playerCombatPosition, currentReticle);
            Matrix view = Matrix.CreateLookAt(camera.Position, camera.Target, camera.Up);
            Matrix projection = CreateProjection();
            var viewport = new Viewport(0, 0, Game1.VirtualWidth, Game1.VirtualHeight);
            Vector3 projected = viewport.Project(
                MapCombatPoint(entity.CombatPosition, ResolveEntityElevation(entity)),
                projection,
                view,
                Matrix.Identity);
            if (projected.Z <= 0f || projected.Z >= 1f)
                return false;
            if (projected.X < -64f || projected.X > Game1.VirtualWidth + 64f || projected.Y < -64f || projected.Y > Game1.VirtualHeight + 64f)
                return false;

            normalized = ClampReticle(ScreenToNormalizedReticle(new Vector2(projected.X, projected.Y)));
            return true;
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
            if (chaseEntryTimer > 0f)
                chaseEntryTimer = Math.Max(0f, chaseEntryTimer - (Game1.GameTime == null ? 1f / 60f : (float)Game1.GameTime.ElapsedGameTime.TotalSeconds));

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
            if (DeveloperVisualSettings.ShowGizmos)
                DrawCombatGizmos(graphicsDevice);
            if (DeveloperVisualSettings.ShowAimRay)
                DrawAimRay(graphicsDevice);
            if (DeveloperVisualSettings.ShowSpawnPreview)
                DrawSpawnPreview(graphicsDevice, Game1.Instance?.CampaignDirector?.GetChaseSpawnPreviewPoints());
            if (DeveloperVisualSettings.ShowBounds)
                DrawBounds(graphicsDevice, entities);

            graphicsDevice.DepthStencilState = DepthStencilState.None;
            graphicsDevice.BlendState = BlendState.AlphaBlend;
        }

        public static void DrawBackdrop(SpriteBatch spriteBatch, Texture2D pixel, Texture2D radialTexture, BackgroundMoodDefinition mood, VisualPreset preset)
        {
            if (spriteBatch == null || pixel == null)
                return;

            BackgroundMoodDefinition resolvedMood = mood ?? new BackgroundMoodDefinition();
            Color backColor = ColorUtil.ParseHex(resolvedMood.PrimaryColor, new Color(8, 10, 18));
            Color midColor = ColorUtil.ParseHex(resolvedMood.SecondaryColor, new Color(18, 22, 38));
            Color accentColor = ColorUtil.ParseHex(resolvedMood.AccentColor, new Color(110, 193, 255));
            Color glowColor = ColorUtil.ParseHex(resolvedMood.GlowColor, new Color(246, 198, 116));
            Rectangle world = new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight);
            float time = (float)Game1.GameTime.TotalGameTime.TotalSeconds;

            spriteBatch.Draw(pixel, world, backColor);
            spriteBatch.Draw(pixel, world, midColor * 0.08f);

            if (radialTexture != null)
            {
                DrawBackdropGlow(spriteBatch, radialTexture, new Vector2(Game1.VirtualWidth * 0.16f, Game1.VirtualHeight * 0.2f), 440f, accentColor * 0.12f);
                DrawBackdropGlow(spriteBatch, radialTexture, new Vector2(Game1.VirtualWidth * 0.72f, Game1.VirtualHeight * 0.16f), 320f, glowColor * 0.09f);
                DrawBackdropGlow(spriteBatch, radialTexture, new Vector2(Game1.VirtualWidth * 0.58f, Game1.VirtualHeight * 0.64f), 560f, midColor * 0.08f);
                DrawBackdropGlow(spriteBatch, radialTexture, new Vector2(Game1.VirtualWidth * 0.86f, Game1.VirtualHeight * 0.8f), 180f, accentColor * 0.05f);
            }

            int farStars = preset == VisualPreset.Neon ? 180 : 140;
            int nearStars = preset == VisualPreset.Low ? 48 : 72;
            DrawBackdropStars(spriteBatch, pixel, 11, farStars, 1, Color.White * 0.55f, time * 1.6f, 0.018f);
            DrawBackdropStars(spriteBatch, pixel, 23, nearStars, 2, Color.Lerp(accentColor, glowColor, 0.4f) * 0.75f, time * 4.8f, 0.042f);
            DrawBackdropDust(spriteBatch, pixel, accentColor, glowColor, time, preset);
        }

        public static void DrawOverlayEntities(SpriteBatch spriteBatch, IEnumerable<Entity> entities) { }

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

            Vector2 playerMarker = new Vector2(playerX, playerY);
            Vector2 aimDirection = Player1.Instance.CannonDirection;
            if (aimDirection == Vector2.Zero)
                aimDirection = Vector2.UnitX;
            else
                aimDirection.Normalize();
            Vector2 aimMarker = playerMarker + new Vector2(aimDirection.X * 26f, aimDirection.Y * 16f);

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
                float y = content.Center.Y + relativeAltitude * altitudeScale;
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
            }

            spriteBatch.Draw(pixel, new Rectangle(playerX - 4, playerY - 4, 8, 8), Color.Cyan * 0.95f);
            DrawUiLine(spriteBatch, pixel, playerMarker, aimMarker, Color.Cyan * 0.58f, 2f);
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

                if (entity is Player1 player && player.CannonSpriteInstance != null)
                    DrawPlayerCannon(player, mesh.World, mesh.Cache);
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
                Matrix hullRotation = CreatePlayerHullRotation(player);
                float playerScale = ResolveWorldScale(entity);
                Matrix playerWorld = hullRotation * Matrix.CreateScale(playerScale) * Matrix.CreateTranslation(worldPosition);
                return new ProceduralMeshInstance
                {
                    Cache = cache,
                    World = playerWorld,
                    GlowStrength = 0.72f,
                };
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

            return new ProceduralMeshInstance
            {
                Cache = cache,
                World = world,
                GlowStrength = glowStrength,
            };
        }

        private static Matrix CreatePlayerHullRotation(Player1 player)
        {
            float yaw = MathHelper.Clamp(player.DepthVelocity * 0.0014f, -0.18f, 0.18f);
            float pitch = MathHelper.Clamp(CombatAltitudeVelocityToWorld(player.CombatVelocity.Y) * 0.04f + player.TravelVelocity * 0.00055f, -0.16f, 0.16f);
            float roll = MathHelper.Clamp(-player.DepthVelocity * 0.0038f, -0.45f, 0.45f);
            return Matrix.CreateFromYawPitchRoll(yaw, pitch, roll);
        }

        private static void DrawPlayerCannon(Player1 player, Matrix playerHullWorld, MeshVolumeCache hullCache)
        {
            ProceduralSpriteInstance cannon = player?.CannonSpriteInstance;
            if (cannon == null || opaqueEffect == null || hullCache == null)
                return;

            MeshVolumeCache cache = GetMeshCache(cannon);
            if (cache.OpaqueIndices.Length == 0)
                return;

            if (!TryResolvePlayerCannonPose(player, out _, out Matrix world, out _, out _))
                return;
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

        private static Vector3[] BuildBoundsCorners(Vector3 localMin, Vector3 localMax, Matrix world)
        {
            Vector3[] corners =
            {
                new Vector3(localMin.X, localMin.Y, localMin.Z),
                new Vector3(localMax.X, localMin.Y, localMin.Z),
                new Vector3(localMax.X, localMax.Y, localMin.Z),
                new Vector3(localMin.X, localMax.Y, localMin.Z),
                new Vector3(localMin.X, localMin.Y, localMax.Z),
                new Vector3(localMax.X, localMin.Y, localMax.Z),
                new Vector3(localMax.X, localMax.Y, localMax.Z),
                new Vector3(localMin.X, localMax.Y, localMax.Z),
            };

            for (int i = 0; i < corners.Length; i++)
                corners[i] = Vector3.Transform(corners[i], world);

            return corners;
        }

        private static Vector3 MapCombatPoint(Vector3 combatPoint, float elevation)
        {
            float lateral = combatPoint.Z * DepthWorldScale;
            float altitude = CombatAltitudeToWorld(combatPoint.Y) + elevation;
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
            float altitudeLead = CombatAltitudeVelocityToWorld(playerCombatVelocity.Y) * 0.1f;
            float forwardPush = MathHelper.Clamp(playerCombatVelocity.X * 0.045f, -18f, 28f);
            float chaseEntryBlend = chaseEntryTimer <= 0f ? 0f : MathHelper.SmoothStep(0f, 1f, chaseEntryTimer / ChaseEntryDurationSeconds);
            float entrySideBias = -18f * chaseEntryBlend;
            float entryDistanceBonus = 20f * chaseEntryBlend;
            float entryLookAhead = 34f * chaseEntryBlend;
            Vector3 position = playerWorld + new Vector3(CameraSideAnchor + entrySideBias + lateralLead * 0.7f, CameraBaseHeight + altitudeLead, -CameraBaseDistance - forwardPush - entryDistanceBonus);
            Vector3 target = new Vector3(
                CameraTargetSideAnchor + entrySideBias * 0.62f + reticle.X * 34f + lateralLead,
                CameraLookHeight + reticle.Y * 26f + altitudeLead * 0.35f,
                CameraLookAhead + entryLookAhead + Math.Max(0f, playerCombatVelocity.X * 0.08f));
            target += playerWorld;
            float bank = MathHelper.Clamp(-playerCombatVelocity.Z * 0.0026f, -0.24f, 0.24f);
            return new ChaseCameraState(position, target, bank);
        }

        private static ChaseCameraState ResolveAimCamera(Vector3 playerCombatPosition, Vector2 reticle)
        {
            if (cameraInitialized)
                return new ChaseCameraState(smoothedCameraPosition, smoothedCameraTarget, smoothedCameraBank);

            return BuildDesiredCamera(playerCombatPosition, Player1.Instance?.CombatVelocity ?? Vector3.Zero, reticle);
        }

        private static Vector3 MapCombatDirection(Vector3 combatDirection)
        {
            return new Vector3(
                combatDirection.Z * DepthWorldScale,
                CombatAltitudeVelocityToWorld(combatDirection.Y),
                combatDirection.X * ForwardWorldScale);
        }

        private static Vector3 MapWorldPointToCombat(Vector3 worldPoint)
        {
            return new Vector3(
                worldPoint.Z / ForwardWorldScale,
                Game1.VirtualHeight * 0.5f - worldPoint.Y / AltitudeWorldScale,
                worldPoint.X / DepthWorldScale);
        }

        private static float CombatAltitudeToWorld(float combatAltitude)
        {
            return (Game1.VirtualHeight * 0.5f - combatAltitude) * AltitudeWorldScale;
        }

        private static float CombatAltitudeVelocityToWorld(float combatAltitudeVelocity)
        {
            return -combatAltitudeVelocity * AltitudeWorldScale;
        }

        private static float WorldAltitudeDirectionToCombat(float worldAltitudeDirection)
        {
            return -worldAltitudeDirection / AltitudeWorldScale;
        }

        private static bool TryResolvePlayerCannonPose(Player1 player, out Matrix playerHullWorld, out Matrix cannonWorld, out Vector3 muzzleWorld, out Vector3 muzzleForward)
        {
            playerHullWorld = Matrix.Identity;
            cannonWorld = Matrix.Identity;
            muzzleWorld = Vector3.Zero;
            muzzleForward = Vector3.Forward;

            if (player == null || player.SpriteInstance == null || player.CannonSpriteInstance == null)
                return false;

            MeshVolumeCache hullCache = GetMeshCache(player.SpriteInstance);
            MeshVolumeCache cannonCache = GetMeshCache(player.CannonSpriteInstance);
            if (hullCache.OpaqueIndices.Length == 0 || cannonCache.OpaqueIndices.Length == 0)
                return false;

            Vector3 playerWorld = MapCombatPoint(player.CombatPosition, 0f);
            Matrix hullRotation = CreatePlayerHullRotation(player);
            float playerScale = ResolveWorldScale(player);
            playerHullWorld = hullRotation * Matrix.CreateScale(playerScale) * Matrix.CreateTranslation(playerWorld);

            Vector3 worldAim = MapCombatDirection(player.CombatAimDirection);
            if (worldAim == Vector3.Zero)
                worldAim = Vector3.Forward;
            else
                worldAim.Normalize();

            float yaw = MathHelper.Clamp(MathF.Atan2(worldAim.X, worldAim.Z) * 0.82f, -0.72f, 0.72f);
            float pitch = MathHelper.Clamp(-MathF.Asin(MathHelper.Clamp(worldAim.Y, -1f, 1f)) * 0.5f, -0.34f, 0.32f);
            float roll = MathHelper.Clamp(-player.DepthVelocity * 0.0012f, -0.12f, 0.12f);
            Matrix aimRotation = Matrix.CreateFromYawPitchRoll(yaw, pitch, roll);
            Matrix combinedRotation = aimRotation * hullRotation;
            Vector3 localHardpoint = new Vector3(
                0f,
                MathHelper.Lerp(hullCache.LocalMin.Y, hullCache.LocalMax.Y, 0.62f),
                hullCache.LocalMax.Z - 2.4f);
            Vector3 hardpointWorld = Vector3.Transform(localHardpoint, playerHullWorld);
            cannonWorld = combinedRotation * Matrix.CreateScale(player.CannonSpriteInstance.PixelScale * 0.44f) * Matrix.CreateTranslation(hardpointWorld);
            muzzleWorld = Vector3.Transform(new Vector3(0f, 0f, cannonCache.LocalMax.Z + 0.75f), cannonWorld);
            muzzleForward = Vector3.Transform(Vector3.Forward, combinedRotation);
            if (muzzleForward == Vector3.Zero)
                muzzleForward = Vector3.Forward;
            else
                muzzleForward.Normalize();

            return true;
        }

        private static Matrix CreateProjection()
        {
            float aspectRatio = Math.Max(1f, Game1.VirtualWidth) / (float)Math.Max(1, Game1.VirtualHeight);
            return Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(54f), aspectRatio, 1f, 2600f);
        }

        private static Vector2 GetReticleScreenPosition(Vector2 normalized)
        {
            float horizontalT = normalized.X * 0.5f + 0.5f;
            float verticalT = 0.5f - normalized.Y * 0.5f;
            float x = MathHelper.Lerp(ReticleMarginX, Game1.VirtualWidth - ReticleMarginX, horizontalT);
            float y = MathHelper.Lerp(ReticleMarginY, Game1.VirtualHeight - ReticleMarginY, verticalT);
            return new Vector2(
                MathHelper.Clamp(x, ReticleMarginX, Game1.VirtualWidth - ReticleMarginX),
                MathHelper.Clamp(y, ReticleMarginY, Game1.VirtualHeight - ReticleMarginY));
        }

        private static Vector2 ScreenToNormalizedReticle(Vector2 screenPosition)
        {
            float horizontal = (screenPosition.X - ReticleMarginX) / Math.Max(1f, Game1.VirtualWidth - ReticleMarginX * 2f);
            float vertical = (screenPosition.Y - ReticleMarginY) / Math.Max(1f, Game1.VirtualHeight - ReticleMarginY * 2f);
            return new Vector2(
                MathHelper.Clamp(horizontal * 2f - 1f, -1f, 1f),
                MathHelper.Clamp(1f - vertical * 2f, -1f, 1f));
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

        private static void DrawBackdropGlow(SpriteBatch spriteBatch, Texture2D radialTexture, Vector2 center, float radius, Color color)
        {
            spriteBatch.Draw(
                radialTexture,
                center,
                null,
                color,
                0f,
                new Vector2(radialTexture.Width / 2f, radialTexture.Height / 2f),
                radius * 2f / radialTexture.Width,
                SpriteEffects.None,
                0f);
        }

        private static void DrawBackdropStars(SpriteBatch spriteBatch, Texture2D pixel, int seed, int count, int size, Color color, float drift, float parallax)
        {
            for (int i = 0; i < count; i++)
            {
                float xSeed = Hash01(seed, i * 13 + 1);
                float ySeed = Hash01(seed, i * 13 + 5);
                float twinkle = 0.42f + 0.58f * MathF.Abs(MathF.Sin(drift * (0.8f + i * 0.013f) + i));
                float x = (xSeed * Game1.VirtualWidth + drift * (18f + i * parallax)) % (Game1.VirtualWidth + 60f) - 30f;
                float y = 20f + ySeed * (Game1.VirtualHeight - 40f);
                spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, size, size), color * twinkle);
            }
        }

        private static void DrawBounds(GraphicsDevice graphicsDevice, IEnumerable<Entity> entities)
        {
            foreach (Entity entity in entities)
            {
                if (entity == null || entity.IsExpired || entity.SpriteInstance == null)
                    continue;

                ProceduralMeshInstance mesh = CreateMeshInstance(entity);
                if (mesh?.Cache == null)
                    continue;

                Vector3[] corners = BuildBoundsCorners(mesh.Cache.LocalMin, mesh.Cache.LocalMax, mesh.World);
                var vertices = new[]
                {
                    new VertexPositionColor(corners[0], Color.Lime), new VertexPositionColor(corners[1], Color.Lime),
                    new VertexPositionColor(corners[1], Color.Lime), new VertexPositionColor(corners[2], Color.Lime),
                    new VertexPositionColor(corners[2], Color.Lime), new VertexPositionColor(corners[3], Color.Lime),
                    new VertexPositionColor(corners[3], Color.Lime), new VertexPositionColor(corners[0], Color.Lime),
                    new VertexPositionColor(corners[4], Color.Lime), new VertexPositionColor(corners[5], Color.Lime),
                    new VertexPositionColor(corners[5], Color.Lime), new VertexPositionColor(corners[6], Color.Lime),
                    new VertexPositionColor(corners[6], Color.Lime), new VertexPositionColor(corners[7], Color.Lime),
                    new VertexPositionColor(corners[7], Color.Lime), new VertexPositionColor(corners[4], Color.Lime),
                    new VertexPositionColor(corners[0], Color.Lime), new VertexPositionColor(corners[4], Color.Lime),
                    new VertexPositionColor(corners[1], Color.Lime), new VertexPositionColor(corners[5], Color.Lime),
                    new VertexPositionColor(corners[2], Color.Lime), new VertexPositionColor(corners[6], Color.Lime),
                    new VertexPositionColor(corners[3], Color.Lime), new VertexPositionColor(corners[7], Color.Lime),
                };

                opaqueEffect.World = Matrix.Identity;
                opaqueEffect.Alpha = 0.85f;
                for (int passIndex = 0; passIndex < opaqueEffect.CurrentTechnique.Passes.Count; passIndex++)
                {
                    EffectPass pass = opaqueEffect.CurrentTechnique.Passes[passIndex];
                    pass.Apply();
                    graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, vertices, 0, vertices.Length / 2);
                }
            }
        }

        private static void DrawCombatGizmos(GraphicsDevice graphicsDevice)
        {
            Player1 player = Player1.Instance;
            if (player == null || player.IsDead)
                return;

            Vector3 center = MapCombatPoint(player.CombatPosition, 0f);
            DrawDebugCross(graphicsDevice, center, 2.4f, Color.White * 0.85f);
            DrawDebugLine(graphicsDevice, center, MapCombatPoint(player.CombatPosition + new Vector3(100f, 0f, 0f), 0f), Color.Orange * 0.92f);
            DrawDebugLine(graphicsDevice, center, MapCombatPoint(player.CombatPosition + new Vector3(0f, -84f, 0f), 0f), Color.Lime * 0.92f);
            DrawDebugLine(graphicsDevice, center, MapCombatPoint(player.CombatPosition + new Vector3(0f, 0f, 100f), 0f), Color.Cyan * 0.92f);
        }

        private static void DrawAimRay(GraphicsDevice graphicsDevice)
        {
            Player1 player = Player1.Instance;
            if (player == null || player.IsDead || !TryResolvePlayerCombatAimRay(player, out Vector3 combatOrigin, out Vector3 combatTarget))
                return;

            Vector3 start = MapCombatPoint(combatOrigin, 1.2f);
            Vector3 end = MapCombatPoint(combatTarget, 1.2f);
            DrawDebugLine(graphicsDevice, start, end, Color.Cyan * 0.8f);
            DrawDebugCross(graphicsDevice, end, 3f, Color.Cyan * 0.75f);
        }

        private static void DrawSpawnPreview(GraphicsDevice graphicsDevice, IEnumerable<Vector3> previewPoints)
        {
            if (previewPoints == null)
                return;

            foreach (Vector3 combatPoint in previewPoints)
            {
                Vector3 center = MapCombatPoint(combatPoint, 2.2f);
                DrawDebugCross(graphicsDevice, center, 3.2f, Color.Orange * 0.78f);
            }
        }

        private static void DrawDebugCross(GraphicsDevice graphicsDevice, Vector3 center, float size, Color color)
        {
            DrawDebugLine(graphicsDevice, center + Vector3.Left * size, center + Vector3.Right * size, color);
            DrawDebugLine(graphicsDevice, center + Vector3.Up * size, center + Vector3.Down * size, color);
            DrawDebugLine(graphicsDevice, center + Vector3.Forward * size, center + Vector3.Backward * size, color);
        }

        private static void DrawDebugLine(GraphicsDevice graphicsDevice, Vector3 start, Vector3 end, Color color)
        {
            if (opaqueEffect == null)
                return;

            VertexPositionColor[] vertices =
            {
                new VertexPositionColor(start, color),
                new VertexPositionColor(end, color),
            };

            opaqueEffect.World = Matrix.Identity;
            opaqueEffect.Alpha = 0.95f;
            for (int passIndex = 0; passIndex < opaqueEffect.CurrentTechnique.Passes.Count; passIndex++)
            {
                EffectPass pass = opaqueEffect.CurrentTechnique.Passes[passIndex];
                pass.Apply();
                graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, vertices, 0, 1);
            }
        }

        private static void DrawBackdropDust(SpriteBatch spriteBatch, Texture2D pixel, Color accentColor, Color glowColor, float time, VisualPreset preset)
        {
            int moteCount = preset == VisualPreset.Low ? 12 : preset == VisualPreset.Neon ? 30 : 20;
            for (int i = 0; i < moteCount; i++)
            {
                float x = (Hash01(71, i * 17 + 3) * (Game1.VirtualWidth + 120f) + time * (8f + i * 0.22f)) % (Game1.VirtualWidth + 120f) - 60f;
                float y = Hash01(71, i * 17 + 7) * Game1.VirtualHeight;
                int size = 2 + (int)(Hash01(71, i * 17 + 11) * (preset == VisualPreset.Neon ? 7f : 5f));
                float drift = MathF.Sin(time * (0.18f + i * 0.01f) + i * 0.73f) * (3f + Hash01(71, i * 17 + 13) * 8f);
                Rectangle moteBounds = new Rectangle((int)MathF.Round(x), (int)MathF.Round(y + drift), size, size);
                Color tint = Color.Lerp(accentColor, glowColor, Hash01(71, i * 17 + 19)) * (0.06f + 0.05f * MathF.Abs(MathF.Sin(time * 0.2f + i)));
                spriteBatch.Draw(pixel, moteBounds, tint);
            }
        }

        private static float Hash01(int seed, int salt)
        {
            int value = seed * 73856093 ^ salt * 19349663;
            value ^= value >> 13;
            value *= 1274126177;
            value ^= value >> 16;
            uint positive = unchecked((uint)value);
            return (positive & 0x00FFFFFF) / 16777215f;
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
