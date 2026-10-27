// Adapted from HungryProton's ProtonScatter create_inside_poisson.gd (MIT).
// Copyright (c) 2020-present HungryProton. See third-party/proton-scatter/LICENSE.
// Upstream credits Sebastian Lague's Poisson disc sampling implementation.
using System;
using System.Collections.Generic;
using System.Threading;

namespace TreeDistrict.Core.Foliage
{
    /// <summary>ProtonScatter's 2D active-frontier Poisson sampler, independent of Godot.</summary>
    public sealed class ProtonPoissonSampler
    {
        public IReadOnlyList<Position2> Generate(IReadOnlyList<Position2> boundary, double spacing,
            ulong seed, int attemptsPerPoint = 15, int maxPoints = 100000,
            int maxAttempts = 2000000, CancellationToken cancellationToken = default)
            => GenerateWithDiagnostics(boundary, spacing, seed, attemptsPerPoint, maxPoints, maxAttempts,
                cancellationToken, out _);

        internal IReadOnlyList<Position2> GenerateWithDiagnostics(IReadOnlyList<Position2> boundary, double spacing,
            ulong seed, int attemptsPerPoint, int maxPoints, int maxAttempts,
            CancellationToken cancellationToken, out int positionAttempts)
        {
            positionAttempts = 0;
            cancellationToken.ThrowIfCancellationRequested();
            if (!Polygon.Finite(spacing) || spacing < 0.001 || attemptsPerPoint < 1 || attemptsPerPoint > 1000
                || maxPoints < 1 || maxAttempts < 1)
                throw new ArgumentException("Positive finite spacing >= 0.001m and bounded sampling limits required.");
            var domain = new Polygon(boundary);
            var random = new StableRandom(seed);
            var points = new List<Position2>();
            var spawnPoints = new List<Position2>();
            double cellSize = spacing / Math.Sqrt(2), squaredRadius = spacing * spacing;
            // Sparse coordinates avoid upstream's rectangular-grid stride and empty-cell sentinel issues.
            var grid = new Dictionary<(long, long), Position2>();
            (long, long) Cell(Position2 p) => ((long)Math.Floor((p.X - domain.MinX) / cellSize),
                (long)Math.Floor((p.Z - domain.MinZ) / cellSize));
            int attempts = 0;
            void Work()
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++attempts > maxAttempts) throw new InvalidOperationException("Poisson sampling exceeded MaxPositionAttempts; no partial plan returned.");
            }
            bool Valid(Position2 candidate)
            {
                if (!domain.Contains(candidate)) return false;
                var (cx, cz) = Cell(candidate);
                for (long x = cx - 2; x <= cx + 2; x++)
                    for (long z = cz - 2; z <= cz + 2; z++)
                        if (grid.TryGetValue((x, z), out var other)
                            && Polygon.DistanceSquared(candidate, other) < squaredRadius) return false;
                return true;
            }
            void Add(Position2 p)
            {
                if (points.Count >= maxPoints) throw new InvalidOperationException("Poisson population exceeded MaxCandidates; no partial plan returned.");
                points.Add(p); spawnPoints.Add(p); grid[Cell(p)] = p;
            }
            var start = new Position2((domain.MinX + domain.MaxX) / 2, (domain.MinZ + domain.MaxZ) / 2);
            while (!domain.Contains(start))
            {
                Work();
                start = new Position2(domain.MinX + random.Unit() * (domain.MaxX - domain.MinX),
                    domain.MinZ + random.Unit() * (domain.MaxZ - domain.MinZ));
            }
            // Retain the initial sample: small domains must still yield a point.
            Add(start);
            while (spawnPoints.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int spawnIndex = (int)(random.Unit() * spawnPoints.Count);
                var center = spawnPoints[spawnIndex];
                bool accepted = false;
                for (int tries = 0; tries < attemptsPerPoint; tries++)
                {
                    Work();
                    double angle = random.Unit() * Math.PI * 2;
                    double distance = spacing * (1 + random.Unit());
                    var candidate = new Position2(center.X + Math.Sin(angle) * distance,
                        center.Z + Math.Cos(angle) * distance);
                    if (!Valid(candidate)) continue;
                    Add(candidate); accepted = true; break;
                }
                if (!accepted) spawnPoints.RemoveAt(spawnIndex);
            }
            // Same final shuffle stage as ProtonScatter; explicit PRNG makes our port repeatable.
            for (int i = points.Count - 1; i > 0; i--)
            {
                int j = (int)(random.Unit() * (i + 1));
                var swap = points[i]; points[i] = points[j]; points[j] = swap;
            }
            positionAttempts = attempts;
            return points.AsReadOnly();
        }
    }
}
