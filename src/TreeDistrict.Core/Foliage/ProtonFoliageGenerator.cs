using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace TreeDistrict.Core.Foliage
{
    public sealed class ProtonScatterSettings
    {
        // Centre-to-centre distance, NOT a crown radius. Authoring value at 100% density.
        public double SpacingM { get; set; } = 6.25;
        public int AttemptsPerPoint { get; set; } = 15;
        // Alternative modes: never apply the same suitability probability twice by default.
        public bool UsePrePlacementSuitability { get; set; } = true;
        public bool EnableLocalComposition { get; set; } = true;
        // Experimental spacing-based abundance; composition remains enabled by default.
        public bool EnableLocalAbundance { get; set; } = false;
    }

    /// <summary>Poisson placement -> weighted assets -> terrain -> clearance/shade.
    /// No simulation or growth. All clearance and canopy dimensions are mature dimensions.</summary>
    public sealed class ProtonFoliageGenerator
    {
        public const string AlgorithmVersion = "foliage-proton-poisson-v1";
        private sealed class Item
        {
            internal SpeciesProfile Profile = null!;
            internal SpeciesCounts Count = null!;
            internal Position2 Position;
            internal int Index;
            internal double Elevation, Light = 1;
            internal double Top => Elevation + Profile.MatureHeightM;
        }
        public GenerationResult Generate(GenerationRequest request, Func<Position2, GroundSample> sample,
            ProtonScatterSettings settings, CancellationToken cancellationToken = default,
            ILocalEnvironmentField? environment = null)
        {
            if (request == null || sample == null || settings == null) throw new ArgumentNullException();
            cancellationToken.ThrowIfCancellationRequested();
            FoliageGenerator.Validate(request);
            if (!Polygon.Finite(settings.SpacingM) || settings.SpacingM < .001
                || settings.AttemptsPerPoint < 1 || settings.AttemptsPerPoint > 1000)
                throw new ArgumentException("Invalid ProtonScatter spacing or attempt limit.");
            var region = new Polygon(request.Boundary);
            var profiles = request.Species.OrderBy(p => p.Id, StringComparer.Ordinal).ToArray();
            var counts = profiles.Select(p => new SpeciesCounts { SpeciesId = p.Id }).ToList();
            var placements = new List<Placement>();
            double[] weights = profiles.Select(p => p.CandidatesPerHectare * (p.DensityPercent / 100)).ToArray();
            double totalWeight = weights.Sum();
            if (!Polygon.Finite(totalWeight)) throw new ArgumentException("Species weights overflow.");
            if (request.OverallDensityPercent == 0 || totalWeight == 0)
                return new GenerationResult(placements, counts, region.Area, 0, 0, AlgorithmVersion);
            bool variableSpacing = profiles.Any(p => p.PreferredSpacingRadiusM.HasValue);
            if (variableSpacing && profiles.Any(p => !p.PreferredSpacingRadiusM.HasValue))
                throw new ArgumentException("Provide preferred spacing radii for every selected profile or none.");
            // Preserve the fixed-radius baseline for legacy fixture files.
            double spacing = settings.SpacingM / Math.Sqrt(request.OverallDensityPercent / 100);
            IReadOnlyList<Position2> points;
            IReadOnlyList<VegetationPoint>? vegetationPoints = null;
            int positionAttempts; long samplingChecks = 0;
            int opportunities = 0, empty = 0, spacingRejected = 0;
            if (settings.UsePrePlacementSuitability)
            {
                LocalVegetationResponse Conditions(Position2 position)
                {
                    var ground = sample(position);
                    if (!Polygon.Finite(ground.ElevationM)) return new LocalVegetationResponse(new double[profiles.Length], 0);
                    double temperature = request.Climate?.TemperatureAt(ground.ElevationM) ?? ground.TemperatureC;
                    double slope = ground.SlopeDegrees;
                    if (environment != null)
                    {
                        if (!environment.TrySample(position, out var local))
                            throw new InvalidOperationException("Environment field does not cover a proposed placement.");
                        temperature = local.TemperatureC; slope = local.SlopeDegrees;
                    }
                    var choiceWeights = new double[profiles.Length];
                    double suitableWeight = 0;
                    for (int i = 0; i < profiles.Length; i++)
                    {
                        double suitability = VegetationSuitability.Evaluate(request, profiles[i], ground, temperature, slope);
                        suitableWeight += weights[i] * suitability;
                        bool eligible = Polygon.Finite(temperature) && Polygon.Finite(slope)
                            && request.Terrain.Allows(ground.ElevationM, ground.SlopeDegrees, ground.IsWater, ground.IsBlocked)
                            && profiles[i].Terrain.Allows(ground.ElevationM, ground.SlopeDegrees, ground.IsWater, ground.IsBlocked);
                        choiceWeights[i] = eligible ? weights[i] * (settings.EnableLocalComposition ? suitability : 1) : 0;
                    }
                    return new LocalVegetationResponse(choiceWeights,
                        settings.EnableLocalAbundance ? suitableWeight / totalWeight : 1);
                }
                int SelectLocally(Position2 position, double ticket) => Conditions(position).Choose(ticket / totalWeight);
                vegetationPoints = ProtonVegetationSampler.Generate(request, profiles, weights, settings.AttemptsPerPoint,
                    cancellationToken, out positionAttempts, out samplingChecks, SelectLocally, settings.SpacingM / 2,
                    isEmpty => { opportunities++; if (isEmpty) empty++; },
                    position => Conditions(position).SpacingMultiplier);
                points = vegetationPoints.Select(p => p.Position).ToArray();
                spacingRejected = opportunities - empty - points.Count;
            }
            else if (variableSpacing)
            {
                vegetationPoints = ProtonVegetationSampler.Generate(request, profiles, weights, settings.AttemptsPerPoint,
                    cancellationToken, out positionAttempts, out samplingChecks);
                points = vegetationPoints.Select(p => p.Position).ToArray();
            }
            else
                points = new ProtonPoissonSampler().GenerateWithDiagnostics(request.Boundary, spacing, request.Seed,
                    settings.AttemptsPerPoint, request.MaxCandidates, request.MaxPositionAttempts, cancellationToken, out positionAttempts);
            // These are proposal weights, not a promise of the final species proportions.
            for (int i = 0; i < profiles.Length; i++) counts[i].ExpectedCandidates = points.Count * weights[i] / totalWeight;
            var candidates = new List<Item>();
            for (int i = 0; i < points.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var random = new StableRandom(StableRandom.Key(request.Seed, "proton-assets", i, 1));
                double ticket = random.Unit() * totalWeight;
                int species = 0;
                while (species < profiles.Length - 1 && ticket >= weights[species]) ticket -= weights[species++];
                if (vegetationPoints != null) species = vegetationPoints[i].Species;
                var p = profiles[species]; var count = counts[species]; count.Candidates++;
                var ground = sample(points[i]);
                if (!request.Terrain.Allows(ground.ElevationM, ground.SlopeDegrees, ground.IsWater, ground.IsBlocked)
                    || !p.Terrain.Allows(ground.ElevationM, ground.SlopeDegrees, ground.IsWater, ground.IsBlocked))
                { count.TerrainRejected++; continue; }
                double temperature = request.Climate?.TemperatureAt(ground.ElevationM) ?? ground.TemperatureC;
                double slope = ground.SlopeDegrees;
                if (environment != null)
                {
                    if (!environment.TrySample(points[i], out var local))
                        throw new InvalidOperationException("Environment field does not cover a proposed placement.");
                    temperature = local.TemperatureC;
                    slope = local.SlopeDegrees;
                }
                double suitable = VegetationSuitability.Evaluate(request, p, ground, temperature, slope);
                if (!settings.UsePrePlacementSuitability && (!Polygon.Finite(temperature) || random.Unit() >= suitable)) { count.EnvironmentRejected++; continue; }
                candidates.Add(new Item { Profile = p, Count = count, Position = points[i], Index = i, Elevation = ground.ElevationM });
            }
            double maxRadius = Math.Max(profiles.Select(p => Math.Max(p.CrownRadiusM, p.ExclusionRadiusM)).Max(),
                request.ExistingPlants.Select(p => Math.Max(p.CrownRadiusM, p.ExclusionRadiusM)).DefaultIfEmpty(0).Max());
            var accepted = new SpatialIndex<Item>(Math.Max(1, maxRadius));
            var existing = new SpatialIndex<ExistingPlant>(Math.Max(1, maxRadius));
            foreach (var plant in request.ExistingPlants.OrderBy(p => p.Id, StringComparer.Ordinal)) existing.Add(plant.Position, plant);
            var survivors = new List<Item>(); long checks = samplingChecks;
            void Work()
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++checks > request.MaxNeighbourChecks) throw new InvalidOperationException("Neighbour workload exceeds MaxNeighbourChecks; no partial plan returned.");
            }
            foreach (var item in candidates.OrderByDescending(c => c.Top).ThenBy(c => c.Index))
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool blocked = false; double light = 1;
                double search = maxRadius + item.Profile.ExclusionRadiusM;
                foreach (var other in existing.Around(item.Position, search))
                {
                    Work(); double d2 = Polygon.DistanceSquared(item.Position, other.Position);
                    double clearance = item.Profile.ExclusionRadiusM + other.ExclusionRadiusM;
                    if (d2 < clearance * clearance) { blocked = true; break; }
                    if (request.ResolveShade && other.ElevationM + other.HeightM - item.Top > request.MinimumShadeHeightDifferenceM
                        && d2 < other.CrownRadiusM * other.CrownRadiusM) light *= 1 - other.CrownOpacity;
                }
                if (blocked) { item.Count.ExistingPlantRejected++; continue; }
                foreach (var other in accepted.Around(item.Position, search))
                {
                    Work(); double d2 = Polygon.DistanceSquared(item.Position, other.Position);
                    double clearance = item.Profile.ExclusionRadiusM + other.Profile.ExclusionRadiusM;
                    if (request.ResolveCompetition && d2 < clearance * clearance) { blocked = true; break; }
                    if (request.ResolveShade && other.Top - item.Top > request.MinimumShadeHeightDifferenceM
                        && d2 < other.Profile.CrownRadiusM * other.Profile.CrownRadiusM) light *= 1 - other.Profile.CrownOpacity;
                }
                if (blocked) { item.Count.CompetitionRejected++; continue; }
                if (request.ResolveShade)
                {
                    double chance = Math.Max(0, Math.Min(1, (light - item.Profile.MinimumLight) / (item.Profile.FullLight - item.Profile.MinimumLight)));
                    if (item.Profile.RequiresShade && light >= 1) chance = 0;
                    var random = new StableRandom(StableRandom.Key(request.Seed, "proton-shade", item.Index, 2));
                    if (random.Unit() >= chance) { item.Count.ShadeRejected++; continue; }
                }
                if (survivors.Count >= request.MaxPlacements) throw new InvalidOperationException("Population exceeds MaxPlacements; no partial plan returned.");
                item.Light = light; item.Count.Accepted++; survivors.Add(item); accepted.Add(item.Position, item);
            }
            foreach (var group in survivors.GroupBy(c => c.Profile.Id).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var ordered = group.OrderBy(c => c.Index).ToArray();
                var ages = FoliageGenerator.AssignAges(ordered[0].Profile, ordered.Length, request.Seed);
                for (int i = 0; i < ordered.Length; i++)
                {
                    var item = ordered[i];
                    var random = new StableRandom(StableRandom.Key(request.Seed, item.Profile.Id, item.Index, 6));
                    placements.Add(new Placement(item.Profile.Id, item.Index, item.Position, item.Elevation,
                        random.Unit() * Math.PI * 2, ages[i], item.Light));
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new GenerationResult(placements, counts, region.Area, positionAttempts, checks, settings.UsePrePlacementSuitability ? "foliage-proton-frontier-local-policies-v2" : variableSpacing ? "foliage-proton-variable-spacing-v1" : AlgorithmVersion,
                opportunities, empty, spacingRejected);
        }
    }
}
