using System;
using System.Collections.Generic;
using System.Linq;
using TreeDistrict.Core.Foliage;

namespace TreeDistrict.Core.Canopy
{
    // A surface, not a collection of trees. All coordinates are metres in world X/Z.
    // Each square has the same boundary samples as its neighbours. Jittered lattice
    // triangles are clipped to the area outline. Fixed connectivity keeps generation
    // linear in sample count; this is not Heuser's constrained Delaunay algorithm.
    public sealed class CanopyGeometry
    {
        public const double TileSize = 48;
        public const double Spacing = 1;
        private const double Epsilon = 1e-8;
        private readonly Position2[] boundary;
        private readonly List<Face> outline;
        public readonly int MinTileX, MaxTileX, MinTileZ, MaxTileZ;

        public readonly struct Face
        {
            public readonly Position2 A, B, C;
            public Face(Position2 a, Position2 b, Position2 c) { A = a; B = b; C = c; }
        }
        public sealed class Tile
        {
            public readonly List<Position2> Points = new List<Position2>();
            // Counterclockwise in X/Z (reverse these indices for an upward Unity face).
            public readonly List<int> Indices = new List<int>();
            public readonly List<(Position2 A, Position2 B)> Edges = new List<(Position2, Position2)>();
        }

        public CanopyGeometry(IReadOnlyList<Position2> nodes)
        {
            var clean = new List<Position2>();
            foreach (var p in nodes)
            {
                if (double.IsNaN(p.X) || double.IsInfinity(p.X) || double.IsNaN(p.Z) || double.IsInfinity(p.Z))
                    throw new ArgumentException("Non-finite canopy boundary.");
                if (clean.Count == 0 || Distance2(clean[clean.Count - 1], p) > Epsilon) clean.Add(p);
            }
            if (clean.Count > 1 && Distance2(clean[0], clean[clean.Count - 1]) < Epsilon) clean.RemoveAt(clean.Count - 1);
            // Remove collinear nodes only for tessellation; native nodes remain untouched.
            bool changed = true;
            while (changed && clean.Count > 3)
            {
                changed = false;
                for (int i = 0; i < clean.Count; i++)
                    if (Math.Abs(Cross(clean[(i + clean.Count - 1) % clean.Count], clean[i], clean[(i + 1) % clean.Count])) < Epsilon)
                    { clean.RemoveAt(i); changed = true; break; }
            }
            if (clean.Count < 3) throw new ArgumentException("Canopy boundary needs three points.");
            double area = 0;
            for (int i = 1; i + 1 < clean.Count; i++) area += Cross(clean[0], clean[i], clean[i + 1]);
            if (Math.Abs(area) < Epsilon) throw new ArgumentException("Canopy boundary has zero area.");
            if (area < 0) clean.Reverse();
            boundary = clean.ToArray();
            outline = EarClip(boundary);
            MinTileX = (int)Math.Floor(boundary.Min(p => p.X) / TileSize);
            MaxTileX = (int)Math.Ceiling(boundary.Max(p => p.X) / TileSize) - 1;
            MinTileZ = (int)Math.Floor(boundary.Min(p => p.Z) / TileSize);
            MaxTileZ = (int)Math.Ceiling(boundary.Max(p => p.Z) / TileSize) - 1;
        }

        public Tile BuildTile(int tx, int tz)
        {
            double x0 = tx * TileSize, z0 = tz * TileSize;
            var candidates = outline.Where(f => Overlap(f, x0, z0, x0 + TileSize, z0 + TileSize)).ToArray();
            var result = new Tile();
            if (candidates.Length == 0) return result;
            var points = new List<Position2>();
            int n = (int)(TileSize / Spacing);
            for (int z = 0; z <= n; z++)
                for (int x = 0; x <= n; x++)
                {
                    // Keep the square perimeter fixed; jitter the interior deterministically.
                    bool edge = x == 0 || z == 0 || x == n || z == n;
                    double jx = edge ? 0 : (Hash(tx * n + x, tz * n + z, 17) - .5) * Spacing * .4;
                    double jz = edge ? 0 : (Hash(tx * n + x, tz * n + z, 93) - .5) * Spacing * .4;
                    points.Add(new Position2(x * Spacing + jx, z * Spacing + jz));
                }
            // Fixed connectivity on a jittered lattice: linear work, no per-tile
            // circumcircle search. Jitter below .25 cell preserves convex cells.
            var triangles = new List<(int A, int B, int C)>();
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    int a = z * (n + 1) + x, b = a + 1, c = a + n + 1, d = c + 1;
                    if (Hash(tx * n + x, tz * n + z, 157) < .5)
                    { triangles.Add((a, b, d)); triangles.Add((a, d, c)); }
                    else { triangles.Add((a, b, c)); triangles.Add((b, d, c)); }
                }
            var vertexMap = new Dictionary<(long, long), int>();
            int Vertex(Position2 p)
            {
                var key = ((long)Math.Round(p.X * 1000000), (long)Math.Round(p.Z * 1000000));
                if (!vertexMap.TryGetValue(key, out int i))
                { i = result.Points.Count; vertexMap.Add(key, i); result.Points.Add(p); }
                return i;
            }
            foreach (var t in triangles)
            {
                var a = Move(points[t.A], x0, z0); var b = Move(points[t.B], x0, z0); var c = Move(points[t.C], x0, z0);
                foreach (var mask in candidates)
                {
                    if (!Overlap(mask, Math.Min(a.X, Math.Min(b.X, c.X)), Math.Min(a.Z, Math.Min(b.Z, c.Z)),
                        Math.Max(a.X, Math.Max(b.X, c.X)), Math.Max(a.Z, Math.Max(b.Z, c.Z)))) continue;
                    var clipped = new List<Position2> { a, b, c };
                    clipped = Clip(clipped, mask.A, mask.B);
                    clipped = Clip(clipped, mask.B, mask.C);
                    clipped = Clip(clipped, mask.C, mask.A);
                    for (int i = 1; i + 1 < clipped.Count; i++)
                    {
                        if (Cross(clipped[0], clipped[i], clipped[i + 1]) <= Epsilon) continue;
                        result.Indices.Add(Vertex(clipped[0])); result.Indices.Add(Vertex(clipped[i])); result.Indices.Add(Vertex(clipped[i + 1]));
                    }
                }
            }
            // Use the exact top rim for skirts (independent edge resampling leaves cracks).
            var rim = new Dictionary<(int, int), (int Count, int A, int B)>();
            void Rim(int a, int b)
            {
                var key = (Math.Min(a, b), Math.Max(a, b));
                rim.TryGetValue(key, out var old); rim[key] = (old.Count + 1, a, b);
            }
            for (int i = 0; i < result.Indices.Count; i += 3)
            { Rim(result.Indices[i], result.Indices[i + 1]); Rim(result.Indices[i + 1], result.Indices[i + 2]); Rim(result.Indices[i + 2], result.Indices[i]); }
            foreach (var e in rim.Values)
                if (e.Count == 1)
                {
                    var a = result.Points[e.A]; var b = result.Points[e.B]; var mid = Lerp(a, b, .5);
                    for (int i = 0; i < boundary.Length; i++)
                    {
                        var u = boundary[i]; var v = boundary[(i + 1) % boundary.Length];
                        if (Math.Abs(Cross(u, v, mid)) > 1e-5 * Math.Sqrt(Distance2(u, v))) continue;
                        if (mid.X < Math.Min(u.X, v.X) - 1e-5 || mid.X > Math.Max(u.X, v.X) + 1e-5 ||
                            mid.Z < Math.Min(u.Z, v.Z) - 1e-5 || mid.Z > Math.Max(u.Z, v.Z) + 1e-5) continue;
                        result.Edges.Add((a, b)); break;
                    }
                }
            return result;
        }

        // The upper envelope of overlapping lobes describes one continuous surface.
        // Cells are field samples, never individual tree assets or game entities.
        private static (double Height, double Crown, double Tint) Envelope(double x, double z)
        {
            const double cell = 7;
            int ix = (int)Math.Floor(x / cell), iz = (int)Math.Floor(z / cell);
            double best = 3, crown = 0, tint = .5;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = ix + dx, cz = iz + dz;
                    double px = (cx + .18 + .64 * Hash(cx, cz, 201)) * cell;
                    double pz = (cz + .18 + .64 * Hash(cx, cz, 211)) * cell;
                    double rx = 4.8 + 1.6 * Hash(cx, cz, 223), rz = 4.8 + 1.6 * Hash(cx, cz, 227);
                    double u = (x - px) / rx, v = (z - pz) / rz;
                    double q = 1 - u * u - v * v;
                    if (q <= 0) continue;
                    double lobe = Math.Pow(q, .65);
                    double h = 3 + (9 + 7 * Hash(cx, cz, 229)) * lobe;
                    if (h > best) { best = h; crown = lobe; tint = Hash(cx, cz, 233); }
                }
            return (best, crown, tint);
        }
        public static double Height(double x, double z) => Envelope(x, z).Height
            + .9 * Noise(x / 1.5, z / 1.5, 53) + .35 * Noise(x / .65, z / .65, 71);

        public double BoundaryDistance(double x, double z)
        {
            double best = double.MaxValue;
            for (int i = 0; i < boundary.Length; i++)
            {
                var a = boundary[i]; var b = boundary[(i + 1) % boundary.Length];
                double dx = b.X - a.X, dz = b.Z - a.Z;
                double t = Math.Max(0, Math.Min(1, ((x - a.X) * dx + (z - a.Z) * dz) / (dx * dx + dz * dz)));
                double u = x - a.X - t * dx, v = z - a.Z - t * dz;
                best = Math.Min(best, u * u + v * v);
            }
            return Math.Sqrt(best);
        }
        public double SurfaceHeight(double x, double z)
        {
            double width = 2.5 + 2 * Noise(x / 5, z / 5, 239);
            double t = Math.Min(1, BoundaryDistance(x, z) / width);
            t = Math.Sqrt(t * (2 - t));
            return .5 + (Height(x, z) - .5) * t;
        }
        public static double Shade(double x, double z) => .32 + .68 * Envelope(x, z).Crown;
        public static (double R, double G, double B) Colour(double x, double z)
        {
            var e = Envelope(x, z);
            double foliage = .68 + .22 * Noise(x / .65, z / .65, 149) + .1 * Noise(x / .19, z / .19, 151);
            double shade = (.32 + .68 * e.Crown) * foliage;
            return ((.29 + .07 * e.Tint) * shade, (.34 + .025 * e.Tint) * shade,
                (.20 + .035 * e.Tint) * shade);
        }
        // Small-scale relief is lighting detail, not extra mesh geometry.
        public static double Relief(double x, double z) => .22 * Noise(x / .55, z / .55, 251)
            + .075 * Noise(x / .18, z / .18, 257);
        public static (double X, double Y, double Z) DetailNormal(double x, double z)
        {
            const double e = .08;
            double nx = (Relief(x - e, z) - Relief(x + e, z)) / (2 * e);
            double ny = (Relief(x, z - e) - Relief(x, z + e)) / (2 * e);
            double length = Math.Sqrt(nx * nx + ny * ny + 1);
            return (nx / length, ny / length, 1 / length);
        }
        private static double Noise(double x, double z, int seed)
        {
            int ix = (int)Math.Floor(x), iz = (int)Math.Floor(z);
            double u = x - ix, v = z - iz; u = u * u * (3 - 2 * u); v = v * v * (3 - 2 * v);
            double a = Hash(ix, iz, seed) * (1 - u) + Hash(ix + 1, iz, seed) * u;
            double b = Hash(ix, iz + 1, seed) * (1 - u) + Hash(ix + 1, iz + 1, seed) * u;
            return a * (1 - v) + b * v;
        }
        private static double Hash(int x, int z, int seed)
        {
            unchecked { uint h = (uint)x * 374761393u + (uint)z * 668265263u + (uint)seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u; return (h ^ (h >> 16)) / (double)uint.MaxValue; }
        }
        private static List<Face> EarClip(Position2[] points)
        {
            var order = Enumerable.Range(0, points.Length).ToList();
            var faces = new List<Face>();
            while (order.Count > 3)
            {
                bool found = false;
                for (int i = 0; i < order.Count; i++)
                {
                    int ai = order[(i + order.Count - 1) % order.Count], bi = order[i], ci = order[(i + 1) % order.Count];
                    var a = points[ai]; var b = points[bi]; var c = points[ci];
                    if (Cross(a, b, c) <= Epsilon) continue;
                    bool occupied = order.Any(j => j != ai && j != bi && j != ci &&
                        Cross(a, b, points[j]) >= -Epsilon && Cross(b, c, points[j]) >= -Epsilon && Cross(c, a, points[j]) >= -Epsilon);
                    if (occupied) continue;
                    faces.Add(new Face(a, b, c)); order.RemoveAt(i); found = true; break;
                }
                if (!found) throw new ArgumentException("Cannot triangulate canopy boundary.");
            }
            faces.Add(new Face(points[order[0]], points[order[1]], points[order[2]]));
            return faces;
        }
        private static List<Position2> Clip(List<Position2> input, Position2 a, Position2 b)
        {
            var output = new List<Position2>();
            if (input.Count == 0) return output;
            var prev = input[input.Count - 1]; double pd = Cross(a, b, prev);
            foreach (var curr in input)
            {
                double cd = Cross(a, b, curr);
                if ((cd >= 0) != (pd >= 0)) output.Add(Lerp(prev, curr, pd / (pd - cd)));
                if (cd >= 0) output.Add(curr);
                prev = curr; pd = cd;
            }
            return output;
        }
        private static bool Overlap(Face f, double x0, double z0, double x1, double z1) =>
            Math.Max(f.A.X, Math.Max(f.B.X, f.C.X)) >= x0 && Math.Min(f.A.X, Math.Min(f.B.X, f.C.X)) <= x1 &&
            Math.Max(f.A.Z, Math.Max(f.B.Z, f.C.Z)) >= z0 && Math.Min(f.A.Z, Math.Min(f.B.Z, f.C.Z)) <= z1;
        private static double Cross(Position2 a, Position2 b, Position2 c) => (b.X - a.X) * (c.Z - a.Z) - (b.Z - a.Z) * (c.X - a.X);
        private static double Distance2(Position2 a, Position2 b) => (b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z);
        private static Position2 Lerp(Position2 a, Position2 b, double t) => new Position2(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t);
        private static Position2 Move(Position2 p, double x, double z) => new Position2(p.X + x, p.Z + z);
    }
}
