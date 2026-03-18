using Microsoft.Xna.Framework;
using SpaceBurst.RuntimeData;
using System;
using System.Collections.Generic;

namespace SpaceBurst
{
    static class WeaponCatalog
    {
        public static IReadOnlyList<WeaponStyleId> StyleOrder { get; } = new[]
        {
            WeaponStyleId.Pulse,
            WeaponStyleId.Spread,
            WeaponStyleId.Laser,
            WeaponStyleId.Plasma,
            WeaponStyleId.Missile,
            WeaponStyleId.Rail,
            WeaponStyleId.Arc,
            WeaponStyleId.Blade,
            WeaponStyleId.Drone,
            WeaponStyleId.Fortress,
        };

        private static readonly Dictionary<WeaponStyleId, WeaponStyleDefinition> styles = BuildStyles();

        public static WeaponStyleDefinition GetStyle(WeaponStyleId style)
        {
            return styles[style];
        }

        public static WeaponLevelDefinition GetLevel(WeaponStyleId style, int level)
        {
            WeaponStyleDefinition definition = GetStyle(style);
            if (definition.Levels.Count == 0)
                return new WeaponLevelDefinition();

            int index = Math.Clamp(level, 0, definition.Levels.Count - 1);
            return definition.Levels[index];
        }

        public static ProceduralSpriteDefinition CreateHullDefinition(WeaponStyleId style, int level)
        {
            WeaponStyleDefinition definition = GetStyle(style);
            return new ProceduralSpriteDefinition
            {
                Id = string.Concat(style.ToString(), "HullL", level.ToString()),
                PixelScale = 5,
                PrimaryColor = definition.PrimaryColor,
                SecondaryColor = definition.SecondaryColor,
                AccentColor = definition.AccentColor,
                Rows = BuildHullRows(style, level),
                VitalCore = new VitalCoreMaskDefinition { Rows = BuildCoreRows() },
            };
        }

        public static ProceduralSpriteDefinition CreateCannonDefinition(WeaponStyleId style, int level)
        {
            WeaponStyleDefinition definition = GetStyle(style);
            return new ProceduralSpriteDefinition
            {
                Id = string.Concat(style.ToString(), "CannonL", level.ToString()),
                PixelScale = 5,
                PrimaryColor = definition.SecondaryColor,
                SecondaryColor = definition.PrimaryColor,
                AccentColor = definition.AccentColor,
                Rows = BuildCannonRows(style, level),
            };
        }

        public static ProceduralSpriteDefinition CreateProjectileDefinition(WeaponStyleId style, int level, bool friendly)
        {
            WeaponStyleDefinition definition = GetStyle(style);
            string primary = friendly ? definition.PrimaryColor : "#FFCCAA";
            string secondary = friendly ? definition.AccentColor : "#FF6A59";
            string accent = friendly ? ScaleColor(definition.AccentColor, 1.15f) : "#FFD166";

            return new ProceduralSpriteDefinition
            {
                Id = string.Concat(style.ToString(), "ProjectileL", level.ToString(), friendly ? "F" : "E"),
                PixelScale = friendly ? 4 : 3,
                PrimaryColor = primary,
                SecondaryColor = secondary,
                AccentColor = accent,
                Rows = BuildProjectileRows(style, level, friendly),
            };
        }

        public static DamageMaskDefinition CreatePlayerDamageMask(WeaponStyleId style, int level)
        {
            return new DamageMaskDefinition
            {
                ContactDamage = style == WeaponStyleId.Blade ? 4 : 3,
                ProjectileDamage = Math.Max(1, GetLevel(style, level).ProjectileDamage),
                DamageRadius = 1,
                IntegrityThresholdPercent = style == WeaponStyleId.Fortress ? 10 : 16,
                ContactImpulse = style == WeaponStyleId.Fortress ? 110f : 180f,
                ContactImpact = new ImpactProfileDefinition
                {
                    Name = "PlayerContact",
                    Kernel = style == WeaponStyleId.Blade ? ImpactKernelShape.Diamond5 : ImpactKernelShape.Cross3,
                    BaseCellsRemoved = style == WeaponStyleId.Blade ? 4 : 3,
                    BonusCellsPerDamage = 1,
                    SplashRadius = style == WeaponStyleId.Fortress ? 1 : 0,
                    SplashPercent = style == WeaponStyleId.Fortress ? 25 : 0,
                    DebrisBurstCount = style == WeaponStyleId.Fortress ? 12 : 8,
                    DebrisSpeed = 120f,
                },
            };
        }

        private static Dictionary<WeaponStyleId, WeaponStyleDefinition> BuildStyles()
        {
            return new Dictionary<WeaponStyleId, WeaponStyleDefinition>
            {
                [WeaponStyleId.Pulse] = CreateStyle(WeaponStyleId.Pulse, "PULSE", "#D7F5FF", "#5AAFCB", "#FFB347", new[] { ".#...", "###..", ".#...", ".#...", "###.." }, new[]
                {
                    Level(0.12f, 760f, 1, 1, 0f, false, 0, Impact("Pulse", ImpactKernelShape.Diamond3, 3, 1, 0, 0, 8)),
                    Level(0.11f, 780f, 1, 2, 12f, false, 0, Impact("PulseTwin", ImpactKernelShape.Diamond3, 3, 1, 0, 0, 8)),
                    Level(0.10f, 800f, 1, 3, 18f, false, 0, Impact("PulseTriple", ImpactKernelShape.Diamond3, 4, 1, 0, 0, 10)),
                    Level(0.09f, 840f, 2, 1, 0f, true, 2, Impact("PulsePierce", ImpactKernelShape.Diamond3, 4, 2, 1, 15, 12)),
                }),
                [WeaponStyleId.Spread] = CreateStyle(WeaponStyleId.Spread, "SPREAD", "#FFF2D6", "#F2AE49", "#FF7A59", new[] { "#...#", ".#.#.", "..#..", ".#.#.", "#...#" }, new[]
                {
                    Level(0.16f, 700f, 1, 3, 24f, false, 0, Impact("Spread", ImpactKernelShape.Diamond3, 3, 1, 0, 0, 8)),
                    Level(0.14f, 710f, 1, 5, 34f, false, 0, Impact("WideSpread", ImpactKernelShape.Diamond3, 3, 1, 0, 0, 9)),
                    Level(0.13f, 720f, 2, 5, 30f, false, 0, Impact("HeavyCenter", ImpactKernelShape.Diamond3, 4, 1, 0, 0, 10)),
                    Level(0.12f, 740f, 2, 7, 42f, false, 0, Impact("SevenWay", ImpactKernelShape.Diamond5, 4, 1, 1, 20, 12)),
                }),
                [WeaponStyleId.Laser] = CreateStyle(WeaponStyleId.Laser, "LASER", "#E9F4FF", "#A4D2FF", "#6EC1FF", new[] { "####.", "...#.", "..#..", ".#...", "####." }, new[]
                {
                    Level(0.13f, 980f, 2, 1, 0f, true, 1, Impact("Laser", ImpactKernelShape.Point, 4, 1, 0, 0, 8)),
                    Level(0.11f, 1040f, 2, 1, 0f, true, 2, Impact("LongLaser", ImpactKernelShape.Point, 5, 1, 0, 0, 8)),
                    Level(0.10f, 1080f, 2, 2, 8f, true, 2, Impact("DualPulse", ImpactKernelShape.Point, 5, 1, 0, 0, 9)),
                    Level(0.08f, 1120f, 3, 2, 8f, true, 4, Impact("Carve", ImpactKernelShape.Point, 6, 1, 0, 0, 10)),
                }),
                [WeaponStyleId.Plasma] = CreateStyle(WeaponStyleId.Plasma, "PLASMA", "#F4E0FF", "#B57CFF", "#FF8BD7", new[] { ".###.", "##.##", "..#..", ".#.#.", "#...#" }, new[]
                {
                    Level(0.20f, 520f, 2, 1, 0f, false, 0, Impact("Plasma", ImpactKernelShape.Blast5, 4, 1, 1, 35, 12)),
                    Level(0.18f, 540f, 3, 1, 0f, false, 0, Impact("PlasmaHeavy", ImpactKernelShape.Blast5, 5, 1, 1, 45, 14)),
                    Level(0.17f, 560f, 3, 2, 10f, false, 0, Impact("PlasmaDual", ImpactKernelShape.Blast5, 5, 2, 1, 50, 16)),
                    Level(0.15f, 580f, 4, 3, 18f, false, 0, Impact("PlasmaBurst", ImpactKernelShape.Blast5, 6, 2, 2, 55, 18)),
                }),
                [WeaponStyleId.Missile] = CreateStyle(WeaponStyleId.Missile, "MISSILE", "#FFE1D6", "#FF925C", "#FFD166", new[] { ".#...", "###..", ".##..", "..##.", "..#.." }, new[]
                {
                    Level(0.24f, 460f, 3, 1, 0f, false, 0, Impact("Missile", ImpactKernelShape.Blast5, 6, 2, 2, 55, 16)),
                    Level(0.22f, 470f, 3, 2, 10f, false, 0, Impact("TwinMissile", ImpactKernelShape.Blast5, 6, 2, 2, 60, 18)),
                    Level(0.21f, 480f, 4, 3, 16f, false, 0, Impact("Cluster", ImpactKernelShape.Blast5, 7, 2, 2, 65, 20)),
                    Level(0.19f, 500f, 4, 4, 20f, false, 0, Impact("HeavyCluster", ImpactKernelShape.Blast5, 8, 2, 2, 70, 22)),
                }),
                [WeaponStyleId.Rail] = CreateStyle(WeaponStyleId.Rail, "RAIL", "#EEF6FF", "#7DA4D9", "#F4B860", new[] { "####.", "...#.", "####.", "...#.", "####." }, new[]
                {
                    Level(0.18f, 1060f, 3, 1, 0f, true, 3, Impact("Rail", ImpactKernelShape.Cross3, 6, 2, 0, 0, 10)),
                    Level(0.16f, 1080f, 3, 2, 6f, true, 4, Impact("DualRail", ImpactKernelShape.Cross3, 6, 2, 0, 0, 12)),
                    Level(0.17f, 1120f, 5, 1, 0f, true, 5, Impact("ChargedRail", ImpactKernelShape.Cross3, 8, 2, 1, 20, 14)),
                    Level(0.14f, 1150f, 5, 2, 4f, true, 6, Impact("CarveLine", ImpactKernelShape.Cross3, 9, 2, 1, 25, 16)),
                }),
                [WeaponStyleId.Arc] = CreateStyle(WeaponStyleId.Arc, "ARC", "#D8FFF2", "#59C9A5", "#6EC1FF", new[] { "#.#..", ".#.#.", "..#..", ".#.#.", "#.#.." }, new[]
                {
                    Level(0.15f, 720f, 1, 2, 18f, false, 0, Impact("Fork", ImpactKernelShape.Diamond3, 4, 1, 1, 25, 12)),
                    Level(0.13f, 740f, 1, 3, 24f, false, 0, Impact("Chain1", ImpactKernelShape.Diamond3, 4, 1, 1, 30, 14)),
                    Level(0.12f, 760f, 2, 4, 28f, false, 0, Impact("Chain2", ImpactKernelShape.Diamond5, 5, 1, 1, 35, 16)),
                    Level(0.11f, 780f, 2, 5, 34f, false, 0, Impact("StunChain", ImpactKernelShape.Diamond5, 5, 2, 1, 40, 18)),
                }),
                [WeaponStyleId.Blade] = CreateStyle(WeaponStyleId.Blade, "BLADE", "#FFF0F5", "#E58AB0", "#FFD166", new[] { "#...#", ".###.", "..#..", ".###.", "#...#" }, new[]
                {
                    Level(0.14f, 620f, 2, 2, 30f, false, 0, Impact("Crescent", ImpactKernelShape.Diamond5, 5, 1, 0, 0, 12)),
                    Level(0.13f, 640f, 2, 3, 36f, false, 0, Impact("DualBlade", ImpactKernelShape.Diamond5, 5, 1, 0, 0, 12)),
                    Level(0.12f, 660f, 3, 4, 42f, false, 0, Impact("GuardShard", ImpactKernelShape.Diamond5, 6, 1, 1, 20, 14)),
                    Level(0.10f, 680f, 3, 5, 48f, false, 0, Impact("ContactGuard", ImpactKernelShape.Diamond5, 6, 2, 1, 25, 16)),
                }),
                [WeaponStyleId.Drone] = CreateStyle(WeaponStyleId.Drone, "DRONE", "#E5FBDD", "#7AAA6E", "#F4B860", new[] { ".#.#.", "#####", ".###.", "#####", ".#.#." }, new[]
                {
                    Level(0.13f, 760f, 1, 1, 0f, false, 0, Impact("Drone", ImpactKernelShape.Diamond3, 3, 1, 0, 0, 8)),
                    Level(0.12f, 780f, 1, 1, 0f, false, 0, Impact("TwinDrone", ImpactKernelShape.Diamond3, 4, 1, 0, 0, 10)),
                    Level(0.11f, 800f, 2, 1, 0f, false, 0, Impact("MirrorDrone", ImpactKernelShape.Diamond3, 4, 1, 0, 0, 10)),
                    Level(0.10f, 820f, 2, 1, 0f, false, 0, Impact("Intercept", ImpactKernelShape.Diamond5, 5, 1, 1, 15, 12)),
                }),
                [WeaponStyleId.Fortress] = CreateStyle(WeaponStyleId.Fortress, "FORTRESS", "#E9E3D2", "#A98F6C", "#FFB347", new[] { "#####", "#...#", "#####", "#...#", "#####"}, new[]
                {
                    Level(0.18f, 620f, 2, 1, 0f, false, 0, Impact("ShieldPulse", ImpactKernelShape.Cross3, 5, 1, 1, 20, 12)),
                    Level(0.17f, 640f, 2, 2, 10f, false, 0, Impact("ShieldPulse2", ImpactKernelShape.Cross3, 5, 1, 1, 25, 14)),
                    Level(0.16f, 660f, 3, 3, 14f, false, 0, Impact("GuardPlate", ImpactKernelShape.Cross3, 6, 1, 1, 30, 16)),
                    Level(0.14f, 680f, 4, 3, 18f, false, 0, Impact("ShieldBurst", ImpactKernelShape.Blast5, 7, 2, 2, 45, 20)),
                }),
            };
        }

        private static WeaponStyleDefinition CreateStyle(WeaponStyleId id, string name, string primary, string secondary, string accent, IList<string> iconRows, IList<WeaponLevelDefinition> levels)
        {
            return new WeaponStyleDefinition
            {
                Id = id,
                DisplayName = name,
                PrimaryColor = primary,
                SecondaryColor = secondary,
                AccentColor = accent,
                IconRows = new List<string>(iconRows),
                Levels = new List<WeaponLevelDefinition>(levels),
            };
        }

        private static WeaponLevelDefinition Level(float interval, float speed, int damage, int count, float spread, bool pierce, int pierceCount, ImpactProfileDefinition impact)
        {
            return new WeaponLevelDefinition
            {
                FireIntervalSeconds = interval,
                ProjectileSpeed = speed,
                ProjectileDamage = damage,
                ProjectileCount = count,
                SpreadDegrees = spread,
                Pierce = pierce,
                PierceCount = pierceCount,
                Impact = impact,
            };
        }

        private static ImpactProfileDefinition Impact(string name, ImpactKernelShape kernel, int baseCells, int bonusCells, int splashRadius, int splashPercent, int debris)
        {
            return new ImpactProfileDefinition
            {
                Name = name,
                Kernel = kernel,
                BaseCellsRemoved = baseCells,
                BonusCellsPerDamage = bonusCells,
                SplashRadius = splashRadius,
                SplashPercent = splashPercent,
                DebrisBurstCount = debris,
                DebrisSpeed = 150f + splashRadius * 20f,
            };
        }

        private static List<string> BuildHullRows(WeaponStyleId style, int level)
        {
            char[,] grid = CreateGrid(19, 11, '.');
            DrawBody(grid, 4, 5, 4 + level);

            switch (style)
            {
                case WeaponStyleId.Spread:
                    DrawWing(grid, 3, 5, 4 + level, '+');
                    break;
                case WeaponStyleId.Laser:
                    DrawNose(grid, 12, 16 + level / 2, '*');
                    DrawFin(grid, '+');
                    break;
                case WeaponStyleId.Plasma:
                    DrawBulk(grid, 4, 6 + level);
                    break;
                case WeaponStyleId.Missile:
                    DrawPods(grid, 3 + level / 2);
                    break;
                case WeaponStyleId.Rail:
                    DrawNose(grid, 11, 17, '+');
                    DrawSpine(grid, '+');
                    break;
                case WeaponStyleId.Arc:
                    DrawArcWings(grid, 4 + level, '*');
                    break;
                case WeaponStyleId.Blade:
                    DrawBladeWings(grid, 4 + level, '*');
                    break;
                case WeaponStyleId.Drone:
                    DrawDroneArms(grid, 3 + level, '*');
                    break;
                case WeaponStyleId.Fortress:
                    DrawFortressPlates(grid, 5 + level, '+');
                    break;
                default:
                    DrawWing(grid, 2, 4, 4, '+');
                    break;
            }

            PlaceCore(grid);
            return ToRows(grid);
        }

        private static List<string> BuildCannonRows(WeaponStyleId style, int level)
        {
            char[,] grid = CreateGrid(13, 5, '.');
            for (int x = 2; x < 10 + level / 2; x++)
                grid[2, x] = '#';
            for (int x = 4; x < 9; x++)
            {
                grid[1, x] = '+';
                grid[3, x] = '+';
            }

            switch (style)
            {
                case WeaponStyleId.Laser:
                case WeaponStyleId.Rail:
                    for (int x = 8; x < 12; x++)
                        grid[2, x] = '*';
                    break;
                case WeaponStyleId.Plasma:
                    grid[1, 10] = '*';
                    grid[2, 10] = '*';
                    grid[3, 10] = '*';
                    break;
                case WeaponStyleId.Missile:
                    grid[1, 9] = '*';
                    grid[3, 9] = '*';
                    break;
                case WeaponStyleId.Fortress:
                    for (int x = 1; x < 5; x++)
                    {
                        grid[0, x] = '+';
                        grid[4, x] = '+';
                    }
                    break;
                case WeaponStyleId.Drone:
                    grid[0, 3] = '*';
                    grid[4, 3] = '*';
                    break;
            }

            return ToRows(grid);
        }

        private static List<string> BuildProjectileRows(WeaponStyleId style, int level, bool friendly)
        {
            switch (style)
            {
                case WeaponStyleId.Laser:
                    return new List<string> { "#####", "+++++", "#####", };
                case WeaponStyleId.Plasma:
                    return new List<string> { ".#.", "###", ".#." };
                case WeaponStyleId.Missile:
                    return new List<string> { ".*..", "####", ".++." };
                case WeaponStyleId.Rail:
                    return new List<string> { "#####", "*****" };
                case WeaponStyleId.Arc:
                    return new List<string> { "#.#", ".#.", "#.#" };
                case WeaponStyleId.Blade:
                    return new List<string> { "..#..", ".###.", "#####", ".###.", "..#.." };
                case WeaponStyleId.Fortress:
                    return new List<string> { "####", "++++", "####" };
                default:
                    return friendly
                        ? new List<string> { "##", "##" }
                        : new List<string> { "##", "##" };
            }
        }

        private static List<string> BuildCoreRows()
        {
            return new List<string>
            {
                "...................",
                "...................",
                "...................",
                "...................",
                "........XX.........",
                "........XX.........",
                "........XX.........",
                "...................",
                "...................",
                "...................",
                "...................",
            };
        }

        private static char[,] CreateGrid(int width, int height, char fill)
        {
            var grid = new char[height, width];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    grid[y, x] = fill;
            }

            return grid;
        }

        private static void DrawBody(char[,] grid, int startX, int centerY, int wingDepth)
        {
            FillSymmetric(grid, centerY, startX, 12, '#');
            FillSymmetric(grid, centerY - 1, startX + 1, 11, '#');
            FillSymmetric(grid, centerY - 2, startX + 2, 9 + wingDepth / 2, '#');
            FillSymmetric(grid, centerY - 3, startX + 4, 6 + wingDepth / 3, '#');
            DrawNose(grid, 11, 15, '#');
        }

        private static void DrawWing(char[,] grid, int topOffset, int startX, int extent, char glyph)
        {
            int centerY = grid.GetLength(0) / 2;
            FillSymmetric(grid, centerY - topOffset, startX, startX + extent, glyph);
            FillSymmetric(grid, centerY - topOffset + 1, startX + 1, startX + extent - 1, glyph);
        }

        private static void DrawBulk(char[,] grid, int topOffset, int extent)
        {
            int centerY = grid.GetLength(0) / 2;
            FillSymmetric(grid, centerY - topOffset, 6, 6 + extent, '+');
            FillSymmetric(grid, centerY - topOffset + 1, 4, 8 + extent / 2, '+');
        }

        private static void DrawPods(char[,] grid, int size)
        {
            int centerY = grid.GetLength(0) / 2;
            for (int x = 3; x < 3 + size; x++)
            {
                grid[centerY - 3, x] = '*';
                grid[centerY + 3, x] = '*';
            }
        }

        private static void DrawSpine(char[,] grid, char glyph)
        {
            for (int x = 6; x < 13; x++)
                grid[5, x] = glyph;
        }

        private static void DrawFin(char[,] grid, char glyph)
        {
            for (int i = 0; i < 3; i++)
            {
                grid[2 + i, 5 - i] = glyph;
                grid[8 - i, 5 - i] = glyph;
            }
        }

        private static void DrawArcWings(char[,] grid, int extent, char glyph)
        {
            for (int i = 0; i < extent; i++)
            {
                SetSymmetric(grid, 1 + i / 2, 4 + i, glyph);
                SetSymmetric(grid, 2 + i / 2, 3 + i, glyph);
            }
        }

        private static void DrawBladeWings(char[,] grid, int extent, char glyph)
        {
            for (int i = 0; i < extent; i++)
            {
                SetSymmetric(grid, 1 + i / 2, 8 + i / 2, glyph);
                SetSymmetric(grid, 2 + i / 2, 6 + i / 2, glyph);
            }
        }

        private static void DrawDroneArms(char[,] grid, int reach, char glyph)
        {
            int centerY = grid.GetLength(0) / 2;
            for (int x = 4; x < 4 + reach; x++)
            {
                grid[centerY - 3, x] = glyph;
                grid[centerY + 3, x] = glyph;
            }

            grid[centerY - 3, 3 + reach] = '#';
            grid[centerY + 3, 3 + reach] = '#';
        }

        private static void DrawFortressPlates(char[,] grid, int extent, char glyph)
        {
            FillSymmetric(grid, 1, 5, Math.Min(15, 5 + extent), glyph);
            FillSymmetric(grid, 2, 3, Math.Min(14, 4 + extent), glyph);
            for (int y = 3; y <= 7; y++)
                grid[y, 3] = glyph;
        }

        private static void DrawNose(char[,] grid, int startX, int endX, char glyph)
        {
            int centerY = grid.GetLength(0) / 2;
            for (int x = startX; x <= Math.Min(endX, grid.GetLength(1) - 1); x++)
            {
                int falloff = Math.Max(0, x - startX - 1);
                int yTop = Math.Max(0, centerY - Math.Max(0, 1 - falloff / 3));
                int yBottom = Math.Min(grid.GetLength(0) - 1, centerY + Math.Max(0, 1 - falloff / 3));
                for (int y = yTop; y <= yBottom; y++)
                    grid[y, x] = glyph;
            }
        }

        private static void PlaceCore(char[,] grid)
        {
            grid[4, 8] = 'C';
            grid[4, 9] = 'C';
            grid[5, 8] = 'C';
            grid[5, 9] = 'C';
            grid[6, 8] = 'C';
            grid[6, 9] = 'C';
        }

        private static void FillSymmetric(char[,] grid, int upperRow, int left, int right, char glyph)
        {
            int lowerRow = grid.GetLength(0) - 1 - upperRow;
            FillRow(grid, upperRow, left, right, glyph);
            if (lowerRow != upperRow)
                FillRow(grid, lowerRow, left, right, glyph);
        }

        private static void FillRow(char[,] grid, int row, int left, int right, char glyph)
        {
            if (row < 0 || row >= grid.GetLength(0))
                return;

            int clampedLeft = Math.Max(0, left);
            int clampedRight = Math.Min(grid.GetLength(1) - 1, right);
            for (int x = clampedLeft; x <= clampedRight; x++)
                grid[row, x] = glyph;
        }

        private static void SetSymmetric(char[,] grid, int topOffset, int x, char glyph)
        {
            int upperRow = topOffset;
            int lowerRow = grid.GetLength(0) - 1 - topOffset;
            if (upperRow >= 0 && upperRow < grid.GetLength(0) && x >= 0 && x < grid.GetLength(1))
                grid[upperRow, x] = glyph;
            if (lowerRow >= 0 && lowerRow < grid.GetLength(0) && x >= 0 && x < grid.GetLength(1))
                grid[lowerRow, x] = glyph;
        }

        private static List<string> ToRows(char[,] grid)
        {
            var rows = new List<string>(grid.GetLength(0));
            for (int y = 0; y < grid.GetLength(0); y++)
            {
                var chars = new char[grid.GetLength(1)];
                for (int x = 0; x < grid.GetLength(1); x++)
                    chars[x] = grid[y, x];
                rows.Add(new string(chars));
            }

            return rows;
        }

        private static string ScaleColor(string hex, float factor)
        {
            Color color = ParseHex(hex);
            return string.Format("#{0:X2}{1:X2}{2:X2}",
                ClampToByte(color.R * factor),
                ClampToByte(color.G * factor),
                ClampToByte(color.B * factor));
        }

        private static byte ClampToByte(float value)
        {
            return (byte)Math.Clamp((int)MathF.Round(value), 0, 255);
        }

        private static Color ParseHex(string hex)
        {
            string value = hex.StartsWith("#", StringComparison.Ordinal) ? hex.Substring(1) : hex;
            return new Color(
                Convert.ToByte(value.Substring(0, 2), 16),
                Convert.ToByte(value.Substring(2, 2), 16),
                Convert.ToByte(value.Substring(4, 2), 16));
        }
    }
}
