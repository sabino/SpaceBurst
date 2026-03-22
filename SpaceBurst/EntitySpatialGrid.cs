using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace SpaceBurst
{
    sealed class EntitySpatialGrid<T>
    {
        private readonly Dictionary<Point, List<T>> cells = new Dictionary<Point, List<T>>();
        private readonly float cellSize;

        public EntitySpatialGrid(float cellSize)
        {
            this.cellSize = Math.Max(16f, cellSize);
        }

        public void Clear()
        {
            cells.Clear();
        }

        public void Add(Vector2 position, T entity)
        {
            Point key = ToCell(position);
            if (!cells.TryGetValue(key, out List<T> bucket))
            {
                bucket = new List<T>();
                cells[key] = bucket;
            }

            bucket.Add(entity);
        }

        public IEnumerable<T> Query(Vector2 position, float radius)
        {
            int minX = (int)MathF.Floor((position.X - radius) / cellSize);
            int maxX = (int)MathF.Floor((position.X + radius) / cellSize);
            int minY = (int)MathF.Floor((position.Y - radius) / cellSize);
            int maxY = (int)MathF.Floor((position.Y + radius) / cellSize);
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (!cells.TryGetValue(new Point(x, y), out List<T> bucket))
                        continue;

                    for (int i = 0; i < bucket.Count; i++)
                        yield return bucket[i];
                }
            }
        }

        private Point ToCell(Vector2 position)
        {
            return new Point(
                (int)MathF.Floor(position.X / cellSize),
                (int)MathF.Floor(position.Y / cellSize));
        }
    }
}
