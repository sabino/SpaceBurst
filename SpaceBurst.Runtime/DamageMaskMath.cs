using System;
using System.Collections.Generic;

namespace SpaceBurst.RuntimeData
{
    public sealed class MaskGrid
    {
        private readonly bool[] occupied;
        private readonly bool[] vitalCore;

        public int Width { get; }
        public int Height { get; }
        public int InitialOccupiedCount { get; }
        public int InitialCoreCount { get; }
        public int OccupiedCount { get; private set; }
        public int RemainingCoreCount { get; private set; }

        public MaskGrid(int width, int height, bool[] occupied, bool[] vitalCore)
        {
            Width = width;
            Height = height;
            this.occupied = occupied;
            this.vitalCore = vitalCore;

            for (int i = 0; i < occupied.Length; i++)
            {
                if (occupied[i])
                    OccupiedCount++;
                if (vitalCore[i])
                    RemainingCoreCount++;
            }

            InitialOccupiedCount = OccupiedCount;
            InitialCoreCount = RemainingCoreCount;
        }

        public bool IsOccupied(int x, int y)
        {
            return IsInside(x, y) && occupied[y * Width + x];
        }

        public bool IsCore(int x, int y)
        {
            return IsInside(x, y) && vitalCore[y * Width + x];
        }

        public void RemoveCell(int x, int y)
        {
            if (!IsInside(x, y))
                return;

            int index = y * Width + x;
            if (!occupied[index])
                return;

            occupied[index] = false;
            OccupiedCount--;

            if (vitalCore[index])
            {
                vitalCore[index] = false;
                RemainingCoreCount--;
            }
        }

        public MaskGrid Clone()
        {
            return new MaskGrid(Width, Height, (bool[])occupied.Clone(), (bool[])vitalCore.Clone());
        }

        public bool IsInside(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }
    }

    public readonly struct DamageResult
    {
        public int CellsRemoved { get; }
        public int CoreCellsRemoved { get; }
        public int RemainingOccupied { get; }
        public int RemainingCore { get; }
        public bool Destroyed { get; }

        public DamageResult(int cellsRemoved, int coreCellsRemoved, int remainingOccupied, int remainingCore, bool destroyed)
        {
            CellsRemoved = cellsRemoved;
            CoreCellsRemoved = coreCellsRemoved;
            RemainingOccupied = remainingOccupied;
            RemainingCore = remainingCore;
            Destroyed = destroyed;
        }
    }

    public static class DamageMaskMath
    {
        public static MaskGrid CreateGrid(ProceduralSpriteDefinition sprite)
        {
            if (sprite == null)
                return new MaskGrid(1, 1, new[] { true }, new[] { true });

            int height = Math.Max(sprite.Rows?.Count ?? 0, sprite.VitalCore?.Rows?.Count ?? 0);
            int width = 1;

            if (sprite.Rows != null)
            {
                foreach (string row in sprite.Rows)
                    width = Math.Max(width, row?.Length ?? 0);
            }

            if (sprite.VitalCore?.Rows != null)
            {
                foreach (string row in sprite.VitalCore.Rows)
                    width = Math.Max(width, row?.Length ?? 0);
            }

            int finalHeight = Math.Max(1, height);
            var occupied = new bool[width * finalHeight];
            var vitalCore = new bool[width * finalHeight];

            for (int y = 0; y < finalHeight; y++)
            {
                string spriteRow = sprite.Rows != null && y < sprite.Rows.Count ? sprite.Rows[y] ?? string.Empty : string.Empty;
                string coreRow = sprite.VitalCore?.Rows != null && y < sprite.VitalCore.Rows.Count ? sprite.VitalCore.Rows[y] ?? string.Empty : string.Empty;

                for (int x = 0; x < width; x++)
                {
                    char spriteCell = x < spriteRow.Length ? spriteRow[x] : '.';
                    char coreCell = x < coreRow.Length ? coreRow[x] : '.';
                    bool isOccupied = spriteCell != '.' && !char.IsWhiteSpace(spriteCell);
                    bool isCore = coreCell != '.' && !char.IsWhiteSpace(coreCell);

                    if (spriteCell == 'C' || spriteCell == '@')
                        isCore = true;

                    occupied[y * width + x] = isOccupied;
                    vitalCore[y * width + x] = isOccupied && isCore;
                }
            }

            return new MaskGrid(width, finalHeight, occupied, vitalCore);
        }

        public static DamageResult ApplyPointDamage(MaskGrid grid, int centerX, int centerY, int radius, int cellsToRemove, int integrityThresholdPercent)
        {
            if (grid == null || cellsToRemove <= 0)
                return new DamageResult(0, 0, grid?.OccupiedCount ?? 0, grid?.RemainingCoreCount ?? 0, false);

            int searchRadius = Math.Max(0, radius);
            int removed = 0;
            int coreRemoved = 0;

            while (removed < cellsToRemove)
            {
                CellCandidate? candidate = FindNearestOccupiedCell(grid, centerX, centerY, searchRadius);
                if (!candidate.HasValue)
                    break;

                if (grid.IsCore(candidate.Value.X, candidate.Value.Y))
                    coreRemoved++;
                grid.RemoveCell(candidate.Value.X, candidate.Value.Y);
                removed++;
            }

            bool destroyed = IsDestroyed(grid, integrityThresholdPercent);
            return new DamageResult(removed, coreRemoved, grid.OccupiedCount, grid.RemainingCoreCount, destroyed);
        }

        public static DamageResult ApplyImpactDamage(MaskGrid grid, int centerX, int centerY, ImpactProfileDefinition impact, int damageAmount, int integrityThresholdPercent)
        {
            if (grid == null || impact == null || damageAmount <= 0)
                return new DamageResult(0, 0, grid?.OccupiedCount ?? 0, grid?.RemainingCoreCount ?? 0, false);

            int removed = 0;
            int coreRemoved = 0;
            int targetCells = Math.Max(1, impact.BaseCellsRemoved + Math.Max(0, damageAmount - 1) * impact.BonusCellsPerDamage);
            int kernelRadius = GetKernelRadius(impact.Kernel);
            var kernelCandidates = CollectKernelCandidates(grid, centerX, centerY, impact.Kernel);

            for (int i = 0; i < kernelCandidates.Count && removed < targetCells; i++)
            {
                CellCandidate candidate = kernelCandidates[i];
                if (!grid.IsOccupied(candidate.X, candidate.Y))
                    continue;

                if (grid.IsCore(candidate.X, candidate.Y))
                    coreRemoved++;
                grid.RemoveCell(candidate.X, candidate.Y);
                removed++;
            }

            while (removed < targetCells)
            {
                CellCandidate? candidate = FindPenetratingOccupiedCell(grid, centerX, centerY, kernelRadius + 1);
                if (!candidate.HasValue)
                    candidate = FindNearestOccupiedCell(grid, centerX, centerY, kernelRadius + 1);
                if (!candidate.HasValue)
                    break;

                if (grid.IsCore(candidate.Value.X, candidate.Value.Y))
                    coreRemoved++;
                grid.RemoveCell(candidate.Value.X, candidate.Value.Y);
                removed++;
            }

            if (impact.SplashRadius > 0 && impact.SplashPercent > 0)
            {
                int splashCells = (int)Math.Ceiling(targetCells * impact.SplashPercent / 100f);
                int splashRemoved = 0;
                while (splashRemoved < splashCells)
                {
                    CellCandidate? splashCandidate = FindBestCandidateWithinRadius(
                        grid,
                        centerX,
                        centerY,
                        impact.SplashRadius,
                        (x, y) => !IsInKernel(impact.Kernel, centerX, centerY, x, y));
                    if (!splashCandidate.HasValue)
                        break;

                    if (grid.IsCore(splashCandidate.Value.X, splashCandidate.Value.Y))
                        coreRemoved++;
                    grid.RemoveCell(splashCandidate.Value.X, splashCandidate.Value.Y);
                    splashRemoved++;
                    removed++;
                }
            }

            bool destroyed = IsDestroyed(grid, integrityThresholdPercent);
            return new DamageResult(removed, coreRemoved, grid.OccupiedCount, grid.RemainingCoreCount, destroyed);
        }

        public static bool Overlaps(MaskGrid left, int leftX, int leftY, MaskGrid right, int rightX, int rightY)
        {
            if (left == null || right == null)
                return false;

            int overlapLeft = Math.Max(leftX, rightX);
            int overlapTop = Math.Max(leftY, rightY);
            int overlapRight = Math.Min(leftX + left.Width, rightX + right.Width);
            int overlapBottom = Math.Min(leftY + left.Height, rightY + right.Height);

            if (overlapLeft >= overlapRight || overlapTop >= overlapBottom)
                return false;

            for (int y = overlapTop; y < overlapBottom; y++)
            {
                for (int x = overlapLeft; x < overlapRight; x++)
                {
                    if (left.IsOccupied(x - leftX, y - leftY) && right.IsOccupied(x - rightX, y - rightY))
                        return true;
                }
            }

            return false;
        }

        public static bool ContainsPoint(MaskGrid grid, int x, int y)
        {
            return grid != null && grid.IsOccupied(x, y);
        }

        public static bool IsDestroyed(MaskGrid grid, int integrityThresholdPercent)
        {
            if (grid == null)
                return true;

            if (grid.InitialCoreCount > 0 && grid.RemainingCoreCount <= 0)
                return true;

            if (grid.InitialOccupiedCount <= 0)
                return true;

            int thresholdCount = (int)Math.Ceiling(grid.InitialOccupiedCount * Math.Clamp(integrityThresholdPercent, 1, 100) / 100f);
            return grid.OccupiedCount <= thresholdCount;
        }

        public static bool ShouldDestroyOnImpact(DamageResult result, bool destroyOnCoreBreach)
        {
            return result.Destroyed || (destroyOnCoreBreach && result.CoreCellsRemoved > 0);
        }

        private static CellCandidate? FindNearestOccupiedCell(MaskGrid grid, int centerX, int centerY, int radius)
        {
            var candidates = new List<CellCandidate>();
            int expandedRadius = radius;

            while (expandedRadius <= Math.Max(grid.Width, grid.Height))
            {
                candidates.Clear();
                for (int y = centerY - expandedRadius; y <= centerY + expandedRadius; y++)
                {
                    for (int x = centerX - expandedRadius; x <= centerX + expandedRadius; x++)
                    {
                        if (!grid.IsOccupied(x, y))
                            continue;

                        int distanceSquared = (x - centerX) * (x - centerX) + (y - centerY) * (y - centerY);
                        if (distanceSquared > expandedRadius * expandedRadius)
                            continue;

                        candidates.Add(new CellCandidate(x, y, distanceSquared, grid.IsCore(x, y), GetNearestCoreDistanceSquared(grid, x, y)));
                    }
                }

                if (candidates.Count > 0)
                {
                    candidates.Sort((left, right) =>
                    {
                        int distanceComparison = left.DistanceSquared.CompareTo(right.DistanceSquared);
                        if (distanceComparison != 0)
                            return distanceComparison;

                        return left.IsCore.CompareTo(right.IsCore);
                    });

                    return candidates[0];
                }

                expandedRadius++;
            }

            return null;
        }

        private static CellCandidate? FindPenetratingOccupiedCell(MaskGrid grid, int centerX, int centerY, int radius)
        {
            var candidates = new List<CellCandidate>();
            int expandedRadius = radius;

            while (expandedRadius <= Math.Max(grid.Width, grid.Height))
            {
                candidates.Clear();
                for (int y = centerY - expandedRadius; y <= centerY + expandedRadius; y++)
                {
                    for (int x = centerX - expandedRadius; x <= centerX + expandedRadius; x++)
                    {
                        if (!grid.IsOccupied(x, y))
                            continue;

                        int distanceSquared = (x - centerX) * (x - centerX) + (y - centerY) * (y - centerY);
                        if (distanceSquared > expandedRadius * expandedRadius)
                            continue;

                        int coreDistanceSquared = GetNearestCoreDistanceSquared(grid, x, y);
                        if (coreDistanceSquared == int.MaxValue)
                            continue;

                        candidates.Add(new CellCandidate(x, y, distanceSquared, grid.IsCore(x, y), coreDistanceSquared));
                    }
                }

                if (candidates.Count > 0)
                {
                    candidates.Sort((left, right) =>
                    {
                        int coreDistanceComparison = left.CoreDistanceSquared.CompareTo(right.CoreDistanceSquared);
                        if (coreDistanceComparison != 0)
                            return coreDistanceComparison;

                        int distanceComparison = left.DistanceSquared.CompareTo(right.DistanceSquared);
                        if (distanceComparison != 0)
                            return distanceComparison;

                        return left.IsCore.CompareTo(right.IsCore);
                    });

                    return candidates[0];
                }

                expandedRadius++;
            }

            return null;
        }

        private static CellCandidate? FindBestCandidateWithinRadius(MaskGrid grid, int centerX, int centerY, int radius, Func<int, int, bool> filter)
        {
            var candidates = new List<CellCandidate>();
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    if (!grid.IsOccupied(x, y))
                        continue;

                    int distanceSquared = (x - centerX) * (x - centerX) + (y - centerY) * (y - centerY);
                    if (distanceSquared > radius * radius)
                        continue;
                    if (filter != null && !filter(x, y))
                        continue;

                    candidates.Add(new CellCandidate(x, y, distanceSquared, grid.IsCore(x, y), GetNearestCoreDistanceSquared(grid, x, y)));
                }
            }

            if (candidates.Count == 0)
                return null;

            candidates.Sort((left, right) =>
            {
                int coreComparison = left.IsCore.CompareTo(right.IsCore);
                if (coreComparison != 0)
                    return coreComparison;

                return left.DistanceSquared.CompareTo(right.DistanceSquared);
            });

            return candidates[0];
        }

        private static List<CellCandidate> CollectKernelCandidates(MaskGrid grid, int centerX, int centerY, ImpactKernelShape kernel)
        {
            var candidates = new List<CellCandidate>();
            int radius = GetKernelRadius(kernel);
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    if (!grid.IsOccupied(x, y) || !IsInKernel(kernel, centerX, centerY, x, y))
                        continue;

                    int distanceSquared = (x - centerX) * (x - centerX) + (y - centerY) * (y - centerY);
                    candidates.Add(new CellCandidate(x, y, distanceSquared, grid.IsCore(x, y), GetNearestCoreDistanceSquared(grid, x, y)));
                }
            }

            candidates.Sort((left, right) =>
            {
                int coreDistanceComparison = left.CoreDistanceSquared.CompareTo(right.CoreDistanceSquared);
                if (coreDistanceComparison != 0)
                    return coreDistanceComparison;

                return left.DistanceSquared.CompareTo(right.DistanceSquared);
            });
            return candidates;
        }

        private static int GetNearestCoreDistanceSquared(MaskGrid grid, int x, int y)
        {
            int best = int.MaxValue;
            for (int coreY = 0; coreY < grid.Height; coreY++)
            {
                for (int coreX = 0; coreX < grid.Width; coreX++)
                {
                    if (!grid.IsCore(coreX, coreY))
                        continue;

                    int distanceSquared = (coreX - x) * (coreX - x) + (coreY - y) * (coreY - y);
                    if (distanceSquared < best)
                        best = distanceSquared;
                }
            }

            return best;
        }

        private static bool IsInKernel(ImpactKernelShape kernel, int centerX, int centerY, int x, int y)
        {
            int dx = Math.Abs(x - centerX);
            int dy = Math.Abs(y - centerY);
            switch (kernel)
            {
                case ImpactKernelShape.Point:
                    return dx == 0 && dy == 0;
                case ImpactKernelShape.Cross3:
                    return dx + dy <= 1 && (dx == 0 || dy == 0);
                case ImpactKernelShape.Diamond5:
                    return dx + dy <= 2;
                case ImpactKernelShape.Blast5:
                    return dx * dx + dy * dy <= 4;
                default:
                    return dx + dy <= 1;
            }
        }

        private static int GetKernelRadius(ImpactKernelShape kernel)
        {
            switch (kernel)
            {
                case ImpactKernelShape.Point:
                    return 0;
                case ImpactKernelShape.Diamond5:
                case ImpactKernelShape.Blast5:
                    return 2;
                default:
                    return 1;
            }
        }

        private readonly struct CellCandidate
        {
            public int X { get; }
            public int Y { get; }
            public int DistanceSquared { get; }
            public bool IsCore { get; }
            public int CoreDistanceSquared { get; }

            public CellCandidate(int x, int y, int distanceSquared, bool isCore, int coreDistanceSquared)
            {
                X = x;
                Y = y;
                DistanceSquared = distanceSquared;
                IsCore = isCore;
                CoreDistanceSquared = coreDistanceSquared;
            }
        }
    }
}
