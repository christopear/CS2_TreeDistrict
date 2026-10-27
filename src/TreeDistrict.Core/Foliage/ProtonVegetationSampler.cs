// Adapted from HungryProton's ProtonScatter create_inside_poisson.gd (MIT).
// Copyright (c) 2020-present HungryProton. See third-party/proton-scatter/LICENSE.
// Extension: choose a species before each proposal, use summed preferred radii.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace TreeDistrict.Core.Foliage
{
    internal sealed class VegetationPoint
    {
        internal Position2 Position;
        internal int Species;
        internal double Radius;
    }
    internal static class ProtonVegetationSampler
    {
        internal static IReadOnlyList<VegetationPoint> Generate(GenerationRequest request, SpeciesProfile[] profiles,
            double[] weights, int retries, CancellationToken token, out int attempts, out long checks,
            Func<Position2, double, int>? selectLocally = null, double fallbackRadius = 3.125,
            Action<bool>? reportLocalChoice = null, Func<Position2, double>? spacingMultiplier = null)
        {
            var domain = new Polygon(request.Boundary);
            double scale = 1 / Math.Sqrt(request.OverallDensityPercent / 100);
            double[] radii = profiles.Select(p => (p.PreferredSpacingRadiusM ?? fallbackRadius) * scale).ToArray();
            if (radii.Any(r => !Polygon.Finite(r) || r < .001)) throw new ArgumentException("Scaled spacing radius is invalid.");
            double maxRadius = radii.Max(), total = weights.Sum();
            var grid = new SpatialIndex<VegetationPoint>(maxRadius * 2);
            var points = new List<VegetationPoint>(); var active = new List<VegetationPoint>();
            var random = new StableRandom(request.Seed);
            int work = 0; long neighbours = 0;
            void Work()
            {
                token.ThrowIfCancellationRequested();
                if (++work > request.MaxPositionAttempts) throw new InvalidOperationException("Poisson sampling exceeds MaxPositionAttempts.");
            }
            double choiceTicket = 0;
            int Choose()
            {
                double ticket = random.Unit() * total; choiceTicket = ticket; int i = 0;
                while (i < weights.Length - 1 && ticket >= weights[i]) ticket -= weights[i++];
                return i;
            }
            double RadiusAt(Position2 p, int species)
            {
                double radius = radii[species] * (spacingMultiplier?.Invoke(p) ?? 1);
                if (!Polygon.Finite(radius)) throw new InvalidOperationException("Local spacing radius overflow.");
                return radius;
            }
            double diagonal = Math.Sqrt(Math.Pow(domain.MaxX-domain.MinX,2) + Math.Pow(domain.MaxZ-domain.MinZ,2));
            bool Valid(Position2 p, double radius)
            {
                if (!domain.Contains(p)) return false;
                // A very sparse region must not enumerate billions of empty grid cells.
                IEnumerable<VegetationPoint> nearby = radius + maxRadius > diagonal
                    ? points : grid.Around(p, radius + maxRadius);
                foreach (var other in nearby)
                {
                    token.ThrowIfCancellationRequested();
                    if (++neighbours > request.MaxNeighbourChecks) throw new InvalidOperationException("Poisson sampling exceeds MaxNeighbourChecks.");
                    double distance = radius + other.Radius;
                    if (Polygon.DistanceSquared(p, other.Position) < distance * distance) return false;
                }
                return true;
            }
            void Add(Position2 p, int species)
            {
                if (points.Count >= request.MaxCandidates) throw new InvalidOperationException("Poisson population exceeds MaxCandidates.");
                var point = new VegetationPoint { Position = p, Species = species, Radius = RadiusAt(p, species) };
                maxRadius = Math.Max(maxRadius, point.Radius);
                points.Add(point); active.Add(point); grid.Add(p, point);
            }
            var start = new Position2((domain.MinX + domain.MaxX) / 2, (domain.MinZ + domain.MaxZ) / 2);
            while (!domain.Contains(start))
            {
                Work(); start = new Position2(domain.MinX + random.Unit() * (domain.MaxX - domain.MinX),
                    domain.MinZ + random.Unit() * (domain.MaxZ - domain.MinZ));
            }
            bool encounteredBarrenGround = false;
            int LocalChoice(Position2 position, int proposed)
            {
                if (selectLocally == null) return proposed;
                int selected = selectLocally(position, choiceTicket);
                reportLocalChoice?.Invoke(selected < 0);
                // -2 means no eligible vegetation at all; -1 is a probabilistic empty.
                if (selected == -2) encounteredBarrenGround = true;
                return selected;
            }
            token.ThrowIfCancellationRequested();
            int initial = LocalChoice(start, Choose());
            if (initial >= 0) Add(start, initial);
            else if (initial == -1)
                active.Add(new VegetationPoint { Position = start, Radius = radii.Where((r,i) => weights[i]>0).Min(), Species = -1 });
            int restarts = 0;
            while (active.Count > 0 || (selectLocally != null && encounteredBarrenGround && restarts < 64))
            {
                token.ThrowIfCancellationRequested();
                // Bounded independent seeds can reach land beyond completely barren bands.
                // No restart pass on fully suitable ground: preserve the original layout exactly.
                if (active.Count == 0)
                {
                    restarts++; Work();
                    var seed = new Position2(domain.MinX + random.Unit() * (domain.MaxX-domain.MinX),
                        domain.MinZ + random.Unit() * (domain.MaxZ-domain.MinZ));
                    if (!domain.Contains(seed)) continue;
                    int seedSpecies = LocalChoice(seed, Choose());
                    if (seedSpecies >= 0 && Valid(seed, RadiusAt(seed, seedSpecies))) Add(seed, seedSpecies);
                    continue;
                }
                int index = (int)(random.Unit() * active.Count); var center = active[index]; bool found = false;
                for (int trial = 0; trial < retries; trial++)
                {
                    Work(); int species = Choose();
                    if (selectLocally != null)
                    {
                        // Choose a proposal scale from locally eligible vegetation, without reserving it.
                        // Fully suitable ground preserves the original choice and random stream.
                        int localScale = selectLocally(center.Position, choiceTicket);
                        if (localScale < 0) localScale = selectLocally(center.Position, 0);
                        if (localScale >= 0) species = localScale;
                    }
                    double angle = random.Unit() * Math.PI * 2;
                    double distance = (center.Radius + RadiusAt(center.Position, species)) * (1 + random.Unit());
                    var p = new Position2(center.Position.X + Math.Sin(angle) * distance, center.Position.Z + Math.Cos(angle) * distance);
                    if (!domain.Contains(p)) continue;
                    species = LocalChoice(p, species);
                    if (species < 0) continue;
                    if (!Valid(p, RadiusAt(p, species))) continue;
                    Add(p, species); found = true; break;
                }
                if (!found) { active[index] = active[active.Count - 1]; active.RemoveAt(active.Count - 1); }
            }
            for (int i = points.Count - 1; i > 0; i--)
            {
                int j = (int)(random.Unit() * (i + 1)); var swap = points[i]; points[i] = points[j]; points[j] = swap;
            }
            attempts = work; checks = neighbours; return points.AsReadOnly();
        }
    }
}
