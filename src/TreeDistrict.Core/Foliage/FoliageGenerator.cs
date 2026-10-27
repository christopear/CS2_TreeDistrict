using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace TreeDistrict.Core.Foliage
{
    /// <summary>
    /// Pure planning: no entities, files, UI or engine types. Caller supplies a stable world snapshot
    /// through the terrain callback and existing plants. Do not mutate inputs during Generate.
    /// </summary>
    public sealed class FoliageGenerator
    {
        public const string AlgorithmVersion = "foliage-v1-local-priority";
        private sealed class Candidate
        {
            internal SpeciesProfile Species = null!;
            internal SpeciesCounts Counts = null!;
            internal int Index;
            internal Position2 Position;
            internal double Elevation, Light = 1;
            internal ulong Priority;
            internal double Top => Elevation + Species.MatureHeightM;
        }
        private sealed class Canopy
        {
            internal string Key = "";
            internal Position2 Position;
            internal double Top, Radius, Opacity;
        }

        public GenerationResult Generate(GenerationRequest request, Func<Position2, GroundSample> sample,
            CancellationToken cancellationToken = default)
        {
            if (request == null || sample == null) throw new ArgumentNullException(request == null ? nameof(request) : nameof(sample));
            cancellationToken.ThrowIfCancellationRequested();
            Validate(request);
            var region = new Polygon(request.Boundary);
            var profiles = request.Species.OrderBy(p => p.Id, StringComparer.Ordinal).ToArray();
            var existing = request.ExistingPlants.OrderBy(p => p.Id, StringComparer.Ordinal).ToArray();
            var counts = profiles.Select(p => new SpeciesCounts { SpeciesId = p.Id }).ToList();
            long totalCandidates = 0;
            for (int i = 0; i < profiles.Length; i++)
            {
                double expected = region.Area / 10000 * profiles[i].CandidatesPerHectare
                    * (profiles[i].DensityPercent / 100) * (request.OverallDensityPercent / 100);
                if (!Polygon.Finite(expected) || expected > request.MaxCandidates)
                    throw new InvalidOperationException("Candidate budget exceeds MaxCandidates; no partial plan returned.");
                var random = new StableRandom(StableRandom.Key(request.Seed, profiles[i].Id, 0, 1));
                counts[i].ExpectedCandidates = expected;
                counts[i].Candidates = (int)Math.Floor(expected) + (random.Unit() < expected - Math.Floor(expected) ? 1 : 0);
                totalCandidates += counts[i].Candidates;
            }
            if (totalCandidates > request.MaxCandidates)
                throw new InvalidOperationException("Combined candidate budget exceeds MaxCandidates; no partial plan returned.");

            double maxExclusion = Math.Max(profiles.Select(p => p.ExclusionRadiusM).DefaultIfEmpty(0).Max(),
                existing.Select(p => p.ExclusionRadiusM).DefaultIfEmpty(0).Max());
            double maxCrown = Math.Max(profiles.Select(p => p.CrownRadiusM).DefaultIfEmpty(0).Max(),
                existing.Select(p => p.CrownRadiusM).DefaultIfEmpty(0).Max());
            var obstacles = new SpatialIndex<ExistingPlant>(maxExclusion * 2);
            foreach (var plant in existing) obstacles.Add(plant.Position, plant);
            int attempts = 0;
            long checks = 0;
            void CheckWork()
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++checks > request.MaxNeighbourChecks)
                    throw new InvalidOperationException("Neighbour workload exceeds MaxNeighbourChecks; no partial plan returned.");
            }
            var candidates = new List<Candidate>();
            for (int speciesIndex = 0; speciesIndex < profiles.Length; speciesIndex++)
            {
                var profile = profiles[speciesIndex]; var diagnostic = counts[speciesIndex];
                for (int index = 0; index < diagnostic.Candidates; index++)
                {
                    var positionRandom = new StableRandom(StableRandom.Key(request.Seed, profile.Id, index, 2));
                    Position2 position;
                    do
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (++attempts > request.MaxPositionAttempts)
                            throw new InvalidOperationException("Boundary sampling exceeds MaxPositionAttempts; no partial plan returned.");
                        position = new Position2(region.MinX + positionRandom.Unit() * (region.MaxX - region.MinX),
                            region.MinZ + positionRandom.Unit() * (region.MaxZ - region.MinZ));
                    } while (!region.Contains(position));
                    var ground = sample(position);
                    if ((request.Climate == null && !Polygon.Finite(ground.TemperatureC))
                        || !request.Terrain.Allows(ground.ElevationM, ground.SlopeDegrees, ground.IsWater, ground.IsBlocked)
                        || !profile.Terrain.Allows(ground.ElevationM, ground.SlopeDegrees, ground.IsWater, ground.IsBlocked))
                    { diagnostic.TerrainRejected++; continue; }
                    double temperature = request.Climate?.TemperatureAt(ground.ElevationM) ?? ground.TemperatureC;
                    double suitability = Math.Min(profile.Temperature?.Evaluate(temperature) ?? 1,
                        profile.Slope?.Evaluate(ground.SlopeDegrees, true) ?? 1);
                    var environmentRandom = new StableRandom(StableRandom.Key(request.Seed, profile.Id, index, 3));
                    if (environmentRandom.Unit() >= suitability) { diagnostic.EnvironmentRejected++; continue; }
                    bool blocked = false;
                    foreach (var plant in obstacles.Around(position, profile.ExclusionRadiusM + maxExclusion))
                    {
                        CheckWork();
                        double separation = profile.ExclusionRadiusM + plant.ExclusionRadiusM;
                        if (Polygon.DistanceSquared(position, plant.Position) < separation * separation) { blocked = true; break; }
                    }
                    if (blocked) { diagnostic.ExistingPlantRejected++; continue; }
                    candidates.Add(new Candidate
                    {
                        Species = profile,
                        Counts = diagnostic,
                        Index = index,
                        Position = position,
                        Elevation = ground.ElevationM,
                        Priority = StableRandom.Key(request.Seed, profile.Id, index, 4)
                    });
                }
            }

            var neighbourhood = new SpatialIndex<Candidate>(maxExclusion * 2);
            foreach (var candidate in candidates) neighbourhood.Add(candidate.Position, candidate);
            var survivors = new List<Candidate>();
            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool loses = false;
                if (request.ResolveCompetition)
                    foreach (var other in neighbourhood.Around(candidate.Position, candidate.Species.ExclusionRadiusM + maxExclusion))
                    {
                        CheckWork();
                        if (ReferenceEquals(candidate, other) || ComparePriority(other, candidate) >= 0) continue;
                        double separation = candidate.Species.ExclusionRadiusM + other.Species.ExclusionRadiusM;
                        if (Polygon.DistanceSquared(candidate.Position, other.Position) < separation * separation) { loses = true; break; }
                    }
                // Intentionally test against ALL eligible candidates, even ones that also lose.
                // This is local-priority (Matérn-II-style) thinning, not greedy packing.
                if (loses) candidate.Counts.CompetitionRejected++;
                else survivors.Add(candidate);
            }

            var canopies = new SpatialIndex<Canopy>(maxCrown);
            foreach (var plant in existing)
                canopies.Add(plant.Position, new Canopy
                {
                    Key = "existing:" + plant.Id,
                    Position = plant.Position,
                    Top = plant.ElevationM + plant.HeightM,
                    Radius = plant.CrownRadiusM,
                    Opacity = plant.CrownOpacity
                });
            var accepted = new List<Candidate>();
            foreach (var candidate in survivors.OrderByDescending(c => c.Top).ThenBy(c => c.Priority)
                .ThenBy(c => c.Species.Id, StringComparer.Ordinal).ThenBy(c => c.Index))
            {
                cancellationToken.ThrowIfCancellationRequested();
                double light = 1;
                if (request.ResolveShade)
                {
                    foreach (var canopy in canopies.Around(candidate.Position, maxCrown).OrderBy(c => c.Key, StringComparer.Ordinal))
                    {
                        CheckWork();
                        // Vertical top-down canopy proxy: only genuinely taller crowns shade this crown centre.
                        if (canopy.Top - candidate.Top > request.MinimumShadeHeightDifferenceM
                            && Polygon.DistanceSquared(candidate.Position, canopy.Position) < canopy.Radius * canopy.Radius)
                            light *= 1 - canopy.Opacity;
                    }
                    double acceptance = Math.Max(0, Math.Min(1, (light - candidate.Species.MinimumLight)
                        / (candidate.Species.FullLight - candidate.Species.MinimumLight)));
                    if (candidate.Species.RequiresShade && light >= 1) acceptance = 0;
                    var shadeRandom = new StableRandom(StableRandom.Key(request.Seed, candidate.Species.Id, candidate.Index, 5));
                    if (shadeRandom.Unit() >= acceptance) { candidate.Counts.ShadeRejected++; continue; }
                }
                if (accepted.Count >= request.MaxPlacements)
                    throw new InvalidOperationException("Accepted population exceeds MaxPlacements; no partial plan returned.");
                candidate.Light = light; accepted.Add(candidate); candidate.Counts.Accepted++;
                canopies.Add(candidate.Position, new Canopy
                {
                    Key = "generated:" + candidate.Species.Id + ":" + candidate.Index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Position = candidate.Position,
                    Top = candidate.Top,
                    Radius = candidate.Species.CrownRadiusM,
                    Opacity = candidate.Species.CrownOpacity
                });
            }

            var placements = new List<Placement>();
            foreach (var group in accepted.GroupBy(c => c.Species.Id).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var ordered = group.OrderBy(c => c.Index).ToArray();
                var ageLabels = AssignAges(ordered[0].Species, ordered.Length, request.Seed);
                for (int i = 0; i < ordered.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var candidate = ordered[i];
                    var poseRandom = new StableRandom(StableRandom.Key(request.Seed, candidate.Species.Id, candidate.Index, 6));
                    placements.Add(new Placement(candidate.Species.Id, candidate.Index, candidate.Position, candidate.Elevation,
                        poseRandom.Unit() * Math.PI * 2, ageLabels[i], candidate.Light));
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new GenerationResult(placements, counts, region.Area, attempts, checks);
        }

        private static int ComparePriority(Candidate a, Candidate b)
        {
            int order = a.Priority.CompareTo(b.Priority);
            if (order != 0) return order;
            order = StringComparer.Ordinal.Compare(a.Species.Id, b.Species.Id);
            return order != 0 ? order : a.Index.CompareTo(b.Index);
        }

        internal static string[] AssignAges(SpeciesProfile species, int count, ulong seed)
        {
            var ages = species.Ages.OrderBy(a => a.Id, StringComparer.Ordinal).ToArray();
            double sum = ages.Sum(a => a.Weight);
            var targets = ages.Select(a => count * (a.Weight / sum)).ToArray();
            var allocations = targets.Select(t => (int)Math.Floor(t)).ToArray();
            foreach (int i in Enumerable.Range(0, ages.Length).OrderByDescending(i => targets[i] - allocations[i])
                .ThenBy(i => ages[i].Id, StringComparer.Ordinal).Take(count - allocations.Sum())) allocations[i]++;
            var labels = new List<string>(count);
            for (int i = 0; i < ages.Length; i++) for (int j = 0; j < allocations[i]; j++) labels.Add(ages[i].Id);
            var random = new StableRandom(StableRandom.Key(seed, species.Id, 0, 7));
            for (int i = labels.Count - 1; i > 0; i--)
            {
                int j = (int)(random.Unit() * (i + 1));
                string swap = labels[i]; labels[i] = labels[j]; labels[j] = swap;
            }
            return labels.ToArray();
        }

        internal static void Validate(GenerationRequest request)
        {
            void Nonnegative(double value, string field)
            { if (!Polygon.Finite(value) || value < 0) throw new ArgumentException(field + " must be finite and nonnegative."); }
            void Fraction(double value, string field)
            { Nonnegative(value, field); if (value > 1) throw new ArgumentException(field + " must be <= 1."); }
            void Curve(SuitabilityCurve? curve)
            {
                if (curve != null && (!Polygon.Finite(curve.Minimum) || !Polygon.Finite(curve.Optimum)
                    || !Polygon.Finite(curve.Maximum) || curve.Minimum >= curve.Optimum || curve.Optimum >= curve.Maximum))
                    throw new ArgumentException("Suitability curves require finite minimum < optimum < maximum.");
            }
            Nonnegative(request.OverallDensityPercent, nameof(request.OverallDensityPercent));
            Nonnegative(request.MinimumShadeHeightDifferenceM, nameof(request.MinimumShadeHeightDifferenceM));
            if (request.Terrain == null || request.Species == null || request.ExistingPlants == null)
                throw new ArgumentException("Terrain rules and input collections are required.");
            if (request.MaxCandidates < 1 || request.MaxCandidates > 1000000 || request.MaxPlacements < 1
                || request.MaxPositionAttempts < 1 || request.MaxNeighbourChecks < 1)
                throw new ArgumentException("Positive workload limits required; MaxCandidates must not exceed 1,000,000.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in request.Species)
            {
                if (p == null || string.IsNullOrWhiteSpace(p.Id) || !ids.Add(p.Id)) throw new ArgumentException("Species IDs must be unique and nonempty.");
                Nonnegative(p.CandidatesPerHectare, nameof(p.CandidatesPerHectare)); Nonnegative(p.DensityPercent, nameof(p.DensityPercent));
                if (p.PreferredSpacingRadiusM.HasValue && (!Polygon.Finite(p.PreferredSpacingRadiusM.Value)
                    || p.PreferredSpacingRadiusM.Value < .001 || p.PreferredSpacingRadiusM.Value > 1e6))
                    throw new ArgumentException("Preferred spacing radius must be finite and in [0.001,1000000].");
                Nonnegative(p.MatureHeightM, nameof(p.MatureHeightM)); Nonnegative(p.ExclusionRadiusM, nameof(p.ExclusionRadiusM));
                Nonnegative(p.CrownRadiusM, nameof(p.CrownRadiusM)); Fraction(p.CrownOpacity, nameof(p.CrownOpacity));
                if (p.MatureHeightM > 1e6 || p.ExclusionRadiusM > 1e6 || p.CrownRadiusM > 1e6)
                    throw new ArgumentException("Plant dimensions must not exceed one million metres.");
                Fraction(p.MinimumLight, nameof(p.MinimumLight)); Fraction(p.FullLight, nameof(p.FullLight));
                if (p.MinimumLight >= p.FullLight || p.Terrain == null) throw new ArgumentException("Light thresholds must increase; terrain rules required.");
                Curve(p.Temperature); Curve(p.Slope);
                if (p.Ages == null || p.Ages.Count == 0) throw new ArgumentException("At least one age required.");
                var ageIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var age in p.Ages)
                {
                    if (age == null || string.IsNullOrWhiteSpace(age.Id) || !ageIds.Add(age.Id)) throw new ArgumentException("Age IDs must be unique and nonempty.");
                    Nonnegative(age.Weight, nameof(age.Weight));
                }
                double weight = p.Ages.Sum(a => a.Weight);
                if (!Polygon.Finite(weight) || weight <= 0) throw new ArgumentException("Positive finite total age weight required.");
            }
            ids.Clear();
            foreach (var p in request.ExistingPlants)
            {
                if (p == null || string.IsNullOrWhiteSpace(p.Id) || !ids.Add(p.Id)) throw new ArgumentException("Existing plant IDs must be unique and nonempty.");
                Polygon.CheckPosition(p.Position);
                if (!Polygon.Finite(p.ElevationM)) throw new ArgumentException("Finite elevation required.");
                Nonnegative(p.HeightM, nameof(p.HeightM)); Nonnegative(p.ExclusionRadiusM, nameof(p.ExclusionRadiusM));
                Nonnegative(p.CrownRadiusM, nameof(p.CrownRadiusM)); Fraction(p.CrownOpacity, nameof(p.CrownOpacity));
                if (p.HeightM > 1e6 || p.ExclusionRadiusM > 1e6 || p.CrownRadiusM > 1e6)
                    throw new ArgumentException("Plant dimensions must not exceed one million metres.");
            }
        }
    }
}
