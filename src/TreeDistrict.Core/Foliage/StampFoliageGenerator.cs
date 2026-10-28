using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace TreeDistrict.Core.Foliage
{
    /// <summary>Session cache of neutral spatial templates. Caller keys must cover all effective set
    /// and sampler settings, but exclude area geometry and area seed. Not thread safe.</summary>
    public sealed class StampFoliageGenerator
    {
        public const string AlgorithmVersion = "foliage-stamps-8x200-margin1-v1";
        public const double TileSizeM = 200;
        public const double MarginM = 1;
        public const int StampCount = 8;
        private readonly Dictionary<string, IReadOnlyList<Placement>[]> cache = new Dictionary<string, IReadOnlyList<Placement>[]>(StringComparer.Ordinal);
        public int LibraryBuildCount { get; private set; }
        public bool LastCacheHit { get; private set; }
        public double LastBuildMilliseconds { get; private set; }
        public double LastTileMilliseconds { get; private set; }

        public GenerationResult Generate(GenerationRequest request, Func<Position2, GroundSample> terrain,
            ProtonScatterSettings settings, string settingsKey, CancellationToken cancellationToken = default)
        {
            FoliageGenerator.Validate(request);
            if (terrain == null || settings == null || string.IsNullOrEmpty(settingsKey)) throw new ArgumentException("Missing stamp inputs.");
            if (request.ExistingPlants.Count != 0 || request.ResolveCompetition || request.ResolveShade)
                throw new ArgumentException("Stamp mode does not support external plants, competition or shade passes.");
            cancellationToken.ThrowIfCancellationRequested();
            var polygon = new Polygon(request.Boundary);
            var counts = request.Species.Select(s => new SpeciesCounts { SpeciesId = s.Id }).ToList();
            var result = new List<Placement>();
            LastBuildMilliseconds = 0; LastTileMilliseconds = 0; LastCacheHit = false;
            if (request.OverallDensityPercent == 0 || request.Species.Count == 0)
                return new GenerationResult(result, counts, polygon.Area, 0, 0, AlgorithmVersion);
            string key = AlgorithmVersion + ":" + settingsKey;
            LastCacheHit = cache.TryGetValue(key, out var stamps);
            if (!LastCacheHit)
            {
                var timer = Stopwatch.StartNew();
                var built = new IReadOnlyList<Placement>[StampCount];
                // No local climate/slope biases baked into reusable stamps: filter at actual world positions.
                var neutral = request.Species.Select(s => new SpeciesProfile {
                    Id = s.Id, CandidatesPerHectare = s.CandidatesPerHectare, DensityPercent = s.DensityPercent,
                    PreferredSpacingRadiusM = s.PreferredSpacingRadiusM, MatureHeightM = s.MatureHeightM,
                    CrownRadiusM = s.CrownRadiusM, Ages = s.Ages,
                    Terrain = new TerrainRules(-10000, 10000, 90)
                }).ToArray();
                for (int i = 0; i < StampCount; i++)
                {
                    var stampRequest = new GenerationRequest {
                        Seed = (ulong)(100 + i), Species = neutral, OverallDensityPercent = request.OverallDensityPercent,
                        Boundary = new[] { new Position2(1,1), new Position2(199,1), new Position2(199,199), new Position2(1,199) },
                        ResolveCompetition = false, ResolveShade = false,
                        MaxCandidates = request.MaxCandidates, MaxPlacements = request.MaxPlacements,
                        MaxPositionAttempts = request.MaxPositionAttempts, MaxNeighbourChecks = request.MaxNeighbourChecks
                    };
                    built[i] = new ProtonFoliageGenerator().Generate(stampRequest, p => new GroundSample(0), settings, cancellationToken).Placements;
                }
                // Publish only a complete library; a failed/cancelled build cannot poison the cache.
                stamps = built; cache.Add(key, built); LibraryBuildCount++;
                timer.Stop(); LastBuildMilliseconds = timer.Elapsed.TotalMilliseconds;
            }
            var tiling = Stopwatch.StartNew();
            var profiles = request.Species.ToDictionary(s => s.Id, StringComparer.Ordinal);
            var countById = counts.ToDictionary(c => c.SpeciesId, StringComparer.Ordinal);
            var evaluator = new VegetationSuitabilityEvaluator();
            int attempts = 0;
            int minX = (int)Math.Floor(polygon.MinX / TileSizeM), maxX = (int)Math.Floor(polygon.MaxX / TileSizeM);
            int minZ = (int)Math.Floor(polygon.MinZ / TileSizeM), maxZ = (int)Math.Floor(polygon.MaxZ / TileSizeM);
            if ((long)(maxX - minX + 1) * (maxZ - minZ + 1) > 1000000)
                throw new InvalidOperationException("Stamp tile workload exceeds one million boxes.");
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // World-anchored choices stay identical when a boundary expands or its minimum changes.
                var random = new StableRandom(StableRandom.Key(request.Seed, AlgorithmVersion, x, unchecked((ulong)(long)z)));
                var stamp = stamps![(int)(random.Unit() * StampCount)];
                int turns = (int)(random.Unit() * 4);
                foreach (var p in stamp)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (++attempts > request.MaxPositionAttempts) throw new InvalidOperationException("Stamp placement workload exceeds MaxPositionAttempts.");
                    double px = p.Position.X, pz = p.Position.Z;
                    for (int t = 0; t < turns; t++) { double old = px; px = TileSizeM - pz; pz = old; }
                    var position = new Position2(x * TileSizeM + px, z * TileSizeM + pz);
                    if (!polygon.Contains(position)) continue;
                    var count = countById[p.SpeciesId]; count.Candidates++;
                    var ground = terrain(position);
                    double temperature = request.Climate?.TemperatureAt(ground.ElevationM) ?? ground.TemperatureC;
                    var suitability = evaluator.Evaluate(profiles[p.SpeciesId],
                        new PlacementEnvironment(ground, temperature, ground.SlopeDegrees), request.Terrain);
                    if (!suitability.Allowed) { count.TerrainRejected++; continue; }
                    if (suitability.Score <= 0) { count.EnvironmentRejected++; continue; }
                    if (result.Count >= request.MaxPlacements) throw new InvalidOperationException("Population exceeds MaxPlacements; no partial plan returned.");
                    // x'=-z,z'=x is a negative yaw in Unity's X/Z convention.
                    double rotation = (p.RotationRadians - turns * Math.PI / 2 + 2 * Math.PI) % (2 * Math.PI);
                    result.Add(new Placement(p.SpeciesId, result.Count, position, ground.ElevationM, rotation, p.AgeId, 1));
                    count.Accepted++;
                }
            }
            tiling.Stop(); LastTileMilliseconds = tiling.Elapsed.TotalMilliseconds;
            return new GenerationResult(result, counts, polygon.Area, attempts, 0, AlgorithmVersion);
        }
    }
}
