using System;
using System.Collections.Generic;
using System.Linq;

namespace TreeDistrict.Core.Foliage
{
    internal sealed class Polygon
    {
        private readonly Position2[] nodes;
        internal readonly double MinX, MaxX, MinZ, MaxZ, Area;
        internal Polygon(IReadOnlyList<Position2> boundary)
        {
            if (boundary == null || boundary.Count < 3 || boundary.Count > 2048)
                throw new ArgumentException("Boundary must have 3–2,048 vertices.");
            nodes = boundary.ToArray();
            foreach (var p in nodes) CheckPosition(p);
            double twiceArea = 0;
            for (int i = 0; i < nodes.Length; i++)
            {
                var a = nodes[i]; var b = nodes[(i + 1) % nodes.Length];
                if (DistanceSquared(a, b) == 0) throw new ArgumentException("Duplicate consecutive boundary vertices.");
                // Translate the shoelace calculation for stability far from world origin.
                twiceArea += (a.X - nodes[0].X) * (b.Z - nodes[0].Z) - (b.X - nodes[0].X) * (a.Z - nodes[0].Z);
                for (int j = i + 1; j < nodes.Length; j++)
                {
                    if (j == i + 1 || (i == 0 && j == nodes.Length - 1)) continue;
                    if (Intersects(a, b, nodes[j], nodes[(j + 1) % nodes.Length]))
                        throw new ArgumentException("Boundary must be a simple polygon without crossings or touching edges.");
                }
            }
            Area = Math.Abs(twiceArea) / 2;
            if (Area < 0.000001) throw new ArgumentException("Boundary has no usable area.");
            MinX = nodes.Min(p => p.X); MaxX = nodes.Max(p => p.X);
            MinZ = nodes.Min(p => p.Z); MaxZ = nodes.Max(p => p.Z);
        }
        internal static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        internal static void CheckPosition(Position2 p)
        {
            if (!Finite(p.X) || !Finite(p.Z) || Math.Abs(p.X) > 1e9 || Math.Abs(p.Z) > 1e9)
                throw new ArgumentException("Positions must be finite and within +/-1 billion metres.");
        }
        private static double Cross(Position2 a, Position2 b, Position2 c) =>
            (b.X - a.X) * (c.Z - a.Z) - (b.Z - a.Z) * (c.X - a.X);
        private static bool OnSegment(Position2 a, Position2 b, Position2 p) => Cross(a, b, p) == 0
            && p.X >= Math.Min(a.X, b.X) && p.X <= Math.Max(a.X, b.X)
            && p.Z >= Math.Min(a.Z, b.Z) && p.Z <= Math.Max(a.Z, b.Z);
        private static bool Intersects(Position2 a, Position2 b, Position2 c, Position2 d)
        {
            double abC = Cross(a, b, c), abD = Cross(a, b, d), cdA = Cross(c, d, a), cdB = Cross(c, d, b);
            return ((abC > 0 && abD < 0 || abC < 0 && abD > 0) && (cdA > 0 && cdB < 0 || cdA < 0 && cdB > 0))
                || OnSegment(a, b, c) || OnSegment(a, b, d) || OnSegment(c, d, a) || OnSegment(c, d, b);
        }
        internal bool Contains(Position2 p)
        {
            bool inside = false;
            for (int i = 0, j = nodes.Length - 1; i < nodes.Length; j = i++)
            {
                var a = nodes[i]; var b = nodes[j];
                if ((a.Z > p.Z) != (b.Z > p.Z) && p.X < (b.X - a.X) * (p.Z - a.Z) / (b.Z - a.Z) + a.X)
                    inside = !inside;
            }
            return inside;
        }
        internal static double DistanceSquared(Position2 a, Position2 b) =>
            (a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z);
    }

    internal sealed class SpatialIndex<T>
    {
        private readonly double cellSize;
        private readonly Dictionary<(long, long), List<T>> cells = new Dictionary<(long, long), List<T>>();
        internal SpatialIndex(double size) { cellSize = Math.Max(1, size); }
        private long Cell(double coordinate) => (long)Math.Floor(coordinate / cellSize);
        internal void Add(Position2 position, T item)
        {
            var key = (Cell(position.X), Cell(position.Z));
            if (!cells.TryGetValue(key, out var bucket)) cells[key] = bucket = new List<T>();
            bucket.Add(item);
        }
        internal IEnumerable<T> Around(Position2 p, double radius)
        {
            for (long x = Cell(p.X - radius); x <= Cell(p.X + radius); x++)
                for (long z = Cell(p.Z - radius); z <= Cell(p.Z + radius); z++)
                    if (cells.TryGetValue((x, z), out var bucket))
                        foreach (var item in bucket) yield return item;
        }
    }

    // Explicit fixed PRNG/hash: avoids System.Random and string.GetHashCode runtime differences.
    internal struct StableRandom
    {
        private ulong state;
        internal StableRandom(ulong seed) { state = seed; }
        internal ulong Next()
        {
            unchecked
            {
                ulong z = (state += 0x9E3779B97F4A7C15UL);
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
        internal double Unit() => (Next() >> 11) * (1.0 / 9007199254740992.0);
        internal static ulong Key(ulong seed, string id, int index, ulong channel)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                foreach (char c in id) { hash ^= (byte)c; hash *= 1099511628211UL; hash ^= (byte)(c >> 8); hash *= 1099511628211UL; }
                return new StableRandom(seed ^ hash ^ ((ulong)(uint)index * 0x9E3779B97F4A7C15UL) ^ channel).Next();
            }
        }
    }
}
