using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceBurst.RuntimeData;
using System;
using System.Collections.Generic;

namespace SpaceBurst
{
    sealed class PowerupPickup : Entity
    {
        private static readonly List<string> weaponGlyphRows = new List<string>
        {
            "..###..",
            ".#...#.",
            "#.###.#",
            "#.#.#.#",
            "#.###.#",
            ".#...#.",
            "..###..",
        };

        private static readonly List<string> xpGlyphRows = new List<string>
        {
            "...#...",
            "..###..",
            ".#####.",
            "#######",
            ".#####.",
            "..###..",
            "...#...",
        };

        private static readonly List<string> scrapGlyphRows = new List<string>
        {
            ".##.##.",
            "#######",
            "##...##",
            "##.#.##",
            "##...##",
            "#######",
            ".##.##.",
        };

        private readonly float bobSeed;
        private readonly Color primaryColor;
        private readonly Color secondaryColor;
        private readonly Color accentColor;
        private readonly List<string> iconRows;
        private readonly List<string> glyphRows;
        private float ageSeconds;

        public WeaponStyleId StyleId { get; }
        public PickupKind PickupKind { get; }
        public int Amount { get; }

        public PowerupPickup(Vector2 position, WeaponStyleId styleId, Vector3? combatPosition = null)
            : this(position, PickupKind.WeaponCore, styleId, 1, combatPosition)
        {
        }

        private PowerupPickup(Vector2 position, PickupKind pickupKind, WeaponStyleId styleId, int amount, Vector3? combatPosition = null)
        {
            CombatPosition = combatPosition ?? new Vector3(position.X, position.Y, 0f);
            CombatVelocity = new Vector3(-40f, 0f, 0f);
            bobSeed = position.X * 0.013f + position.Y * 0.009f;
            StyleId = styleId;
            PickupKind = pickupKind;
            Amount = Math.Max(1, amount);

            if (pickupKind == PickupKind.WeaponCore)
            {
                WeaponStyleDefinition style = WeaponCatalog.GetStyle(styleId);
                primaryColor = ColorUtil.ParseHex(style.PrimaryColor, new Color(255, 244, 202));
                secondaryColor = ColorUtil.ParseHex(style.SecondaryColor, new Color(255, 179, 71));
                accentColor = ColorUtil.ParseHex(style.AccentColor, new Color(255, 122, 89));
                iconRows = style.IconRows;
                glyphRows = weaponGlyphRows;
                sprite = new ProceduralSpriteInstance(Game1.Instance.GraphicsDevice, new ProceduralSpriteDefinition
                {
                    Id = string.Concat("Powerup_", styleId.ToString()),
                    PixelScale = 4,
                    PrimaryColor = style.PrimaryColor,
                    SecondaryColor = style.SecondaryColor,
                    AccentColor = style.AccentColor,
                    Rows = new List<string>(glyphRows),
                });
            }
            else if (pickupKind == PickupKind.XpShard)
            {
                primaryColor = new Color(215, 245, 255);
                secondaryColor = new Color(110, 193, 255);
                accentColor = new Color(86, 240, 255);
                iconRows = xpGlyphRows;
                glyphRows = xpGlyphRows;
            }
            else
            {
                primaryColor = new Color(255, 225, 160);
                secondaryColor = new Color(255, 176, 87);
                accentColor = new Color(255, 122, 89);
                iconRows = scrapGlyphRows;
                glyphRows = scrapGlyphRows;
            }
        }

        public static PowerupPickup CreateXpShard(Vector2 position, int amount = 1, Vector3? combatPosition = null)
        {
            return new PowerupPickup(position, PickupKind.XpShard, WeaponStyleId.Pulse, amount, combatPosition);
        }

        public static PowerupPickup CreateScrapCache(Vector2 position, int amount = 1, Vector3? combatPosition = null)
        {
            return new PowerupPickup(position, PickupKind.ScrapCache, WeaponStyleId.Missile, amount, combatPosition);
        }

        public override void Update()
        {
            float deltaSeconds = (float)Game1.GameTime.ElapsedGameTime.TotalSeconds;
            ageSeconds += deltaSeconds;
            Vector3 drift = new Vector3(TravelVelocity - Game1.Instance.CurrentScrollSpeed * 0.18f, MathF.Sin(ageSeconds * 3.4f + bobSeed) * 18f, 0f);

            if (Game1.Instance.PowerupMagnetStrength > 0f && !Player1.Instance.IsDead)
            {
                Vector3 delta = Player1.Instance.CombatPosition - CombatPosition;
                if (delta != Vector3.Zero)
                {
                    float distance = delta.Length();
                    delta /= distance;
                    drift += delta * Game1.Instance.PowerupMagnetStrength * MathHelper.Clamp(1f - distance / 420f, 0.15f, 1f);
                }
            }

            CombatVelocity = drift;
            CombatPosition += CombatVelocity * deltaSeconds;

            if (ageSeconds > 12f || Position.X < -40f || MathF.Abs(LateralDepth) > CombatSpaceMath.MaxDepth + 60f)
                IsExpired = true;
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            float pulse = 1f + MathF.Sin(ageSeconds * 6f) * 0.08f;
            PixelArtRenderer.DrawRows(spriteBatch, Game1.UiPixel, glyphRows, Position, 4f * pulse, primaryColor, secondaryColor, accentColor, true);
            if (PickupKind == PickupKind.WeaponCore)
                PixelArtRenderer.DrawRows(spriteBatch, Game1.UiPixel, iconRows, Position + new Vector2(0f, 3f), 1.8f * pulse, primaryColor, secondaryColor, accentColor, true);
            else if (Amount > 1)
                BitmapFontRenderer.Draw(spriteBatch, Game1.UiPixel, Amount.ToString(), Position + new Vector2(12f, -16f), Color.White, 0.9f);
        }

        public bool OverlapsPlayer()
        {
            if (!CombatSpaceMath.IsDepthAwareViewActive)
            {
                float radius2D = 42f + ApproximateRadius + Player1.Instance.ApproximateRadius;
                return Vector2.DistanceSquared(Position, Player1.Instance.Position) <= radius2D * radius2D;
            }

            float radius = 42f + ApproximateDepthRadius + Player1.Instance.ApproximateDepthRadius;
            return Vector3.DistanceSquared(CombatPosition, Player1.Instance.CombatPosition) <= radius * radius;
        }

        public PowerupSnapshotData CaptureSnapshot()
        {
            return new PowerupSnapshotData
            {
                EntityId = EntityId,
                Position = new Vector2Data(Position.X, Position.Y),
                Velocity = new Vector2Data(Velocity.X, Velocity.Y),
                CombatPosition = new Vector3Data(CombatPosition.X, CombatPosition.Y, CombatPosition.Z),
                CombatVelocity = new Vector3Data(CombatVelocity.X, CombatVelocity.Y, CombatVelocity.Z),
                AgeSeconds = ageSeconds,
                StyleId = StyleId,
                PickupKind = PickupKind,
                Amount = Amount,
            };
        }

        public static PowerupPickup FromSnapshot(PowerupSnapshotData snapshot)
        {
            if (snapshot == null)
                return null;

            PowerupPickup pickup = snapshot.PickupKind switch
            {
                PickupKind.XpShard => CreateXpShard(new Vector2(snapshot.Position.X, snapshot.Position.Y), snapshot.Amount,
                    snapshot.CombatPosition == null ? null : new Vector3(snapshot.CombatPosition.X, snapshot.CombatPosition.Y, snapshot.CombatPosition.Z)),
                PickupKind.ScrapCache => CreateScrapCache(new Vector2(snapshot.Position.X, snapshot.Position.Y), snapshot.Amount,
                    snapshot.CombatPosition == null ? null : new Vector3(snapshot.CombatPosition.X, snapshot.CombatPosition.Y, snapshot.CombatPosition.Z)),
                _ => new PowerupPickup(new Vector2(snapshot.Position.X, snapshot.Position.Y), snapshot.StyleId,
                    snapshot.CombatPosition == null ? null : new Vector3(snapshot.CombatPosition.X, snapshot.CombatPosition.Y, snapshot.CombatPosition.Z)),
            };

            if (snapshot.CombatPosition != null)
                pickup.CombatPosition = new Vector3(snapshot.CombatPosition.X, snapshot.CombatPosition.Y, snapshot.CombatPosition.Z);
            if (snapshot.CombatVelocity != null)
                pickup.CombatVelocity = new Vector3(snapshot.CombatVelocity.X, snapshot.CombatVelocity.Y, snapshot.CombatVelocity.Z);
            else
                pickup.CombatVelocity = new Vector3(snapshot.Velocity.X, snapshot.Velocity.Y, 0f);
            pickup.ageSeconds = snapshot.AgeSeconds;
            pickup.RestoreEntityId(snapshot.EntityId);
            return pickup;
        }
    }
}
