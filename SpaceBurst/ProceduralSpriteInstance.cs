using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceBurst.RuntimeData;
using System;

namespace SpaceBurst
{
    sealed class ProceduralSpriteInstance
    {
        private readonly GraphicsDevice graphicsDevice;
        private readonly ProceduralSpriteDefinition definition;
        private readonly char[] glyphs;
        private readonly Color primaryColor;
        private readonly Color secondaryColor;
        private readonly Color accentColor;
        private readonly Texture2D texture;
        private readonly Color[] pixels;
        private MaskGrid mask;

        public ProceduralSpriteDefinition Definition
        {
            get { return definition; }
        }

        public MaskGrid Mask
        {
            get { return mask; }
        }

        public Texture2D Texture
        {
            get { return texture; }
        }

        public string PrimaryColorHex
        {
            get { return definition.PrimaryColor; }
        }

        public string SecondaryColorHex
        {
            get { return definition.SecondaryColor; }
        }

        public string AccentColorHex
        {
            get { return definition.AccentColor; }
        }

        public int PixelScale
        {
            get { return Math.Max(1, definition.PixelScale); }
        }

        public Vector2 TextureSize
        {
            get { return new Vector2(texture.Width, texture.Height); }
        }

        public Vector2 WorldSize
        {
            get { return TextureSize * PixelScale; }
        }

        public ProceduralSpriteInstance(GraphicsDevice graphicsDevice, ProceduralSpriteDefinition definition)
        {
            this.graphicsDevice = graphicsDevice;
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));

            mask = DamageMaskMath.CreateGrid(definition);
            glyphs = new char[mask.Width * mask.Height];
            primaryColor = ParseHexColor(definition.PrimaryColor);
            secondaryColor = ParseHexColor(definition.SecondaryColor);
            accentColor = ParseHexColor(definition.AccentColor);
            pixels = new Color[mask.Width * mask.Height];
            texture = new Texture2D(graphicsDevice, mask.Width, mask.Height, false, SurfaceFormat.Color);

            for (int y = 0; y < mask.Height; y++)
            {
                string row = definition.Rows != null && y < definition.Rows.Count ? definition.Rows[y] ?? string.Empty : string.Empty;
                for (int x = 0; x < mask.Width; x++)
                    glyphs[y * mask.Width + x] = x < row.Length ? row[x] : '.';
            }

            RebuildTexture();
        }

        public ProceduralSpriteInstance Clone()
        {
            return new ProceduralSpriteInstance(graphicsDevice, definition);
        }

        public void Draw(SpriteBatch spriteBatch, Vector2 position, Color tint, float rotation, float extraScale)
        {
            spriteBatch.Draw(
                texture,
                position,
                null,
                tint,
                rotation,
                TextureSize / 2f,
                PixelScale * extraScale,
                SpriteEffects.None,
                0f);
        }

        public Rectangle GetWorldBounds(Vector2 position, float extraScale)
        {
            Vector2 size = WorldSize * extraScale;
            Vector2 topLeft = position - size / 2f;
            return new Rectangle(
                (int)MathF.Floor(topLeft.X),
                (int)MathF.Floor(topLeft.Y),
                Math.Max(1, (int)MathF.Ceiling(size.X)),
                Math.Max(1, (int)MathF.Ceiling(size.Y)));
        }

        public bool ContainsWorldPoint(Vector2 position, Vector2 worldPoint, float extraScale)
        {
            return TryGetCell(position, worldPoint, extraScale, out int cellX, out int cellY) && mask.IsOccupied(cellX, cellY);
        }

        public bool Overlaps(Vector2 position, ProceduralSpriteInstance other, Vector2 otherPosition, float extraScale, float otherScale)
        {
            Rectangle overlap = Rectangle.Intersect(GetWorldBounds(position, extraScale), other.GetWorldBounds(otherPosition, otherScale));
            if (overlap.Width <= 0 || overlap.Height <= 0)
                return false;

            float sampleStep = Math.Max(1f, Math.Min(PixelScale * extraScale, other.PixelScale * otherScale) * 0.5f);

            for (float y = overlap.Top + sampleStep * 0.5f; y < overlap.Bottom; y += sampleStep)
            {
                for (float x = overlap.Left + sampleStep * 0.5f; x < overlap.Right; x += sampleStep)
                {
                    Vector2 sample = new Vector2(x, y);
                    if (ContainsWorldPoint(position, sample, extraScale) && other.ContainsWorldPoint(otherPosition, sample, otherScale))
                        return true;
                }
            }

            return false;
        }

        public DamageResult ApplyDamage(Vector2 position, Vector2 worldPoint, float extraScale, DamageMaskDefinition damageMask, int damageAmount)
        {
            ImpactProfileDefinition impact = damageMask != null ? damageMask.ContactImpact : null;
            if (impact == null)
            {
                impact = new ImpactProfileDefinition
                {
                    Kernel = ImpactKernelShape.Diamond3,
                    BaseCellsRemoved = Math.Max(1, damageMask?.DamageRadius ?? 1),
                    BonusCellsPerDamage = 1,
                };
            }

            return ApplyDamage(position, worldPoint, extraScale, damageMask, impact, damageAmount);
        }

        public DamageResult ApplyDamage(Vector2 position, Vector2 worldPoint, float extraScale, DamageMaskDefinition damageMask, ImpactProfileDefinition impact, int damageAmount)
        {
            if (!TryGetCell(position, worldPoint, extraScale, out int cellX, out int cellY))
                return new DamageResult(0, 0, mask.OccupiedCount, mask.RemainingCoreCount, false);

            var result = DamageMaskMath.ApplyImpactDamage(
                mask,
                cellX,
                cellY,
                impact,
                Math.Max(1, damageAmount),
                damageMask.IntegrityThresholdPercent);

            if (result.CellsRemoved > 0)
                RebuildTexture();

            return result;
        }

        public Vector2 GetImpactNormal(Vector2 position, Vector2 worldPoint)
        {
            Vector2 delta = position - worldPoint;
            if (delta == Vector2.Zero)
                return Vector2.UnitX;

            delta.Normalize();
            return delta;
        }

        public MaskSnapshotData CaptureMaskSnapshot()
        {
            var snapshot = new MaskSnapshotData();
            for (int y = 0; y < mask.Height; y++)
            {
                char[] occupiedRow = new char[mask.Width];
                char[] coreRow = new char[mask.Width];
                for (int x = 0; x < mask.Width; x++)
                {
                    occupiedRow[x] = mask.IsOccupied(x, y) ? '#' : '.';
                    coreRow[x] = mask.IsCore(x, y) ? 'X' : '.';
                }

                snapshot.OccupiedRows.Add(new string(occupiedRow));
                snapshot.CoreRows.Add(new string(coreRow));
            }

            return snapshot;
        }

        public void RestoreMaskSnapshot(MaskSnapshotData snapshot)
        {
            if (snapshot == null || snapshot.OccupiedRows == null || snapshot.OccupiedRows.Count == 0)
                return;

            int width = mask.Width;
            int height = mask.Height;
            bool[] occupied = new bool[width * height];
            bool[] core = new bool[width * height];

            for (int y = 0; y < height; y++)
            {
                string occupiedRow = y < snapshot.OccupiedRows.Count ? snapshot.OccupiedRows[y] ?? string.Empty : string.Empty;
                string coreRow = y < snapshot.CoreRows.Count ? snapshot.CoreRows[y] ?? string.Empty : string.Empty;
                for (int x = 0; x < width; x++)
                {
                    bool isOccupied = x < occupiedRow.Length && occupiedRow[x] != '.';
                    bool isCore = x < coreRow.Length && coreRow[x] != '.';
                    occupied[y * width + x] = isOccupied;
                    core[y * width + x] = isOccupied && isCore;
                }
            }

            mask = new MaskGrid(width, height, occupied, core);
            RebuildTexture();
        }

        private bool TryGetCell(Vector2 position, Vector2 worldPoint, float extraScale, out int cellX, out int cellY)
        {
            Vector2 size = WorldSize * extraScale;
            Vector2 topLeft = position - size / 2f;
            float worldPixel = PixelScale * extraScale;

            float localX = (worldPoint.X - topLeft.X) / worldPixel;
            float localY = (worldPoint.Y - topLeft.Y) / worldPixel;
            cellX = (int)MathF.Floor(localX);
            cellY = (int)MathF.Floor(localY);
            return mask.IsInside(cellX, cellY);
        }

        private void RebuildTexture()
        {
            for (int y = 0; y < mask.Height; y++)
            {
                for (int x = 0; x < mask.Width; x++)
                {
                    int index = y * mask.Width + x;
                    pixels[index] = mask.IsOccupied(x, y) ? ResolveGlyphColor(glyphs[index], mask.IsCore(x, y)) : Color.Transparent;
                }
            }

            texture.SetData(pixels);
        }

        private Color ResolveGlyphColor(char glyph, bool isCore)
        {
            Color baseColor;
            switch (glyph)
            {
                case '+':
                case 'o':
                    baseColor = secondaryColor;
                    break;

                case '*':
                case 'x':
                case '@':
                    baseColor = accentColor;
                    break;

                default:
                    baseColor = primaryColor;
                    break;
            }

            if (isCore)
                baseColor = Color.Lerp(baseColor, Color.White, 0.2f);

            return baseColor;
        }

        private static Color ParseHexColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return Color.White;

            string value = hex.StartsWith("#", StringComparison.Ordinal) ? hex.Substring(1) : hex;
            if (value.Length != 6)
                return Color.White;

            return new Color(
                Convert.ToByte(value.Substring(0, 2), 16),
                Convert.ToByte(value.Substring(2, 2), 16),
                Convert.ToByte(value.Substring(4, 2), 16));
        }
    }
}
