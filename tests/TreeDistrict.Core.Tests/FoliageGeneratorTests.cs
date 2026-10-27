using System.Text.Json;
using TreeDistrict.Core.Foliage;
using Xunit.Abstractions;

namespace TreeDistrict.Core.Tests
{
    public class FoliageGeneratorTests
    {
        private readonly ITestOutputHelper output;
        public FoliageGeneratorTests(ITestOutputHelper output) { this.output = output; }
        private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
        private static SpeciesProfile[] Profiles() => JsonSerializer.Deserialize<SpeciesProfile[]>(
            JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "foliage-model.v1.json")))
                .RootElement.GetProperty("profiles").GetRawText(), Json)!;
        private static Position2[] Square(double side = 200) => new[] {
        new Position2(0, 0), new Position2(side, 0), new Position2(side, side), new Position2(0, side) };
        private static GenerationRequest Request(ulong seed = 1) => new() { Seed = seed, Boundary = Square(), Species = Profiles() };
        private static GenerationResult Run(GenerationRequest request, Func<Position2, GroundSample>? sample = null) =>
            new FoliageGenerator().Generate(request, sample ?? (_ => new GroundSample(0)));
        private static string Signature(GenerationResult result) => JsonSerializer.Serialize(result.Placements);
        private static void Reconciles(GenerationResult result)
        {
            foreach (var count in result.Counts)
            {
                Assert.Equal(count.Candidates, count.Accepted + count.Rejected);
                Assert.Equal(count.Accepted, result.Placements.Count(p => p.SpeciesId == count.SpeciesId));
            }
        }

        public static IEnumerable<object[]> PopulationScenarios() => JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "set-population.v1.json"))).RootElement.GetProperty("scenarios")
            .EnumerateArray().Select(c => new object[] { c.GetRawText() }).ToArray();

        [Theory]
        [MemberData(nameof(PopulationScenarios))]
        public void AllPopulationContractsRunAgainstStandaloneGenerator(string json)
        {
            var scenario = JsonDocument.Parse(json).RootElement;
            var results = new List<GenerationResult>();
            for (ulong seed = 1; seed <= 32; seed++)
            {
                var request = Request(seed);
                request.Boundary = Square(Math.Sqrt(scenario.GetProperty("areaM2").GetDouble()));
                request.OverallDensityPercent = scenario.GetProperty("overallDensityPercent").GetDouble();
                request.ResolveCompetition = false; request.ResolveShade = false;
                var mix = scenario.GetProperty("speciesDensityPercent");
                request.Species = request.Species.Where(p => mix.TryGetProperty(p.Id, out _)).ToArray();
                foreach (var p in request.Species)
                {
                    p.DensityPercent = mix.GetProperty(p.Id).GetDouble();
                    p.Temperature = new SuitabilityCurve { Minimum = 0, Optimum = 1, Maximum = 2 };
                }
                double side = Math.Sqrt(scenario.GetProperty("areaM2").GetDouble());
                double eligible = scenario.GetProperty("eligibleAreaFraction").GetDouble();
                double suitability = scenario.GetProperty("uniformSuitability").GetDouble();
                var result = Run(request, p => new GroundSample(0, temperatureC: suitability, isBlocked: p.X >= side * eligible));
                Reconciles(result); results.Add(result);
                foreach (var expectation in scenario.GetProperty("expected").EnumerateObject())
                {
                    var counts = result.Counts.Single(c => c.SpeciesId == expectation.Name);
                    int actual = scenario.GetProperty("stage").GetString() == "candidateBudget" ? counts.Candidates : counts.Accepted;
                    Assert.InRange(actual, expectation.Value.GetProperty("minimum").GetInt32(), expectation.Value.GetProperty("maximum").GetInt32());
                }
            }
            foreach (var expectation in scenario.GetProperty("expected").EnumerateObject())
            {
                double mean = expectation.Value.GetProperty("mean").GetDouble();
                double actual = results.Average(r =>
                {
                    var c = r.Counts.Single(c => c.SpeciesId == expectation.Name);
                    return scenario.GetProperty("stage").GetString() == "candidateBudget" ? c.Candidates : c.Accepted;
                });
                double tolerance = Math.Max(mean * .03, 4 * Math.Sqrt(mean / results.Count));
                Assert.InRange(actual, Math.Max(0, mean - tolerance), mean + tolerance);
            }
        }

        [Fact]
        public void MixedCompetitionMatchesAnalyticalThinningAndSpacing()
        {
            // Independent Poisson approximation; finite fixed counts and boundaries cause small deviations.
            var profiles = Profiles();
            var actual = profiles.ToDictionary(p => p.Id, _ => new List<int>());
            for (ulong seed = 1; seed <= 64; seed++)
            {
                var request = Request(seed); request.ResolveShade = false;
                var result = Run(request); Reconciles(result);
                foreach (var c in result.Counts) actual[c.SpeciesId].Add(c.Accepted);
                if (seed <= 4)
                    for (int i = 0; i < result.Placements.Count; i++)
                        for (int j = i + 1; j < result.Placements.Count; j++)
                        {
                            var a = result.Placements[i]; var b = result.Placements[j];
                            double distance = Math.Sqrt(Math.Pow(a.Position.X - b.Position.X, 2) + Math.Pow(a.Position.Z - b.Position.Z, 2));
                            double minimum = profiles.Single(p => p.Id == a.SpeciesId).ExclusionRadiusM
                                + profiles.Single(p => p.Id == b.SpeciesId).ExclusionRadiusM;
                            Assert.True(distance >= minimum);
                        }
            }
            foreach (var p in profiles)
            {
                double load = profiles.Sum(q => q.CandidatesPerHectare / 10000 * Math.PI * Math.Pow(p.ExclusionRadiusM + q.ExclusionRadiusM, 2));
                double expected = p.CandidatesPerHectare * 4 * (1 - Math.Exp(-load)) / load;
                double mean = actual[p.Id].Average();
                output.WriteLine($"{p.Id}: analytical={expected:F2}, mean={mean:F2}, range={actual[p.Id].Min()}–{actual[p.Id].Max()}");
                Assert.InRange(mean, expected * .95, expected * 1.05);
            }
        }

        [Fact]
        public void SeedAndInputOrderAreStableAndDisabledSpeciesDoesNotPerturbOthers()
        {
            var request = Request(); var baseline = Run(request);
            Assert.Equal(Signature(baseline), Signature(Run(request)));
            request.Species = request.Species.Reverse().ToArray();
            request.Boundary = request.Boundary.Reverse().ToArray();
            Assert.Equal(Signature(baseline), Signature(Run(request)));
            request.Species = request.Species.Concat(new[] { new SpeciesProfile { Id = "disabled", CandidatesPerHectare = 100, DensityPercent = 0 } }).ToArray();
            Assert.Equal(Signature(baseline), Signature(Run(request)));
            request.Seed++;
            Assert.NotEqual(Signature(baseline), Signature(Run(request)));
        }

        [Fact]
        public void AgesAreApportionedWithoutChangingTheForest()
        {
            var request = Request(); var before = Run(request);
            foreach (var species in request.Species)
            {
                int total = before.Placements.Count(p => p.SpeciesId == species.Id);
                foreach (var age in species.Ages)
                    Assert.InRange(Math.Abs(before.Placements.Count(p => p.SpeciesId == species.Id && p.AgeId == age.Id) - total * age.Weight / 100), 0, 1);
                species.Ages = new[] { new AgeWeight { Id = "young" } };
            }
            var after = Run(request);
            Assert.Equal(before.Placements.Select(p => (p.SpeciesId, p.CandidateIndex, p.Position.X, p.Position.Z, p.AvailableLight)),
                after.Placements.Select(p => (p.SpeciesId, p.CandidateIndex, p.Position.X, p.Position.Z, p.AvailableLight)));
            Assert.All(after.Placements, p => Assert.Equal("young", p.AgeId));
        }

        [Fact]
        public void UniformCanopyGivesPredictableSpeciesSpecificShadeThinning()
        {
            var means = new Dictionary<string, List<int>>();
            for (ulong seed = 1; seed <= 64; seed++)
            {
                var request = Request(seed); request.ResolveCompetition = false;
                // Prevent mutual canopy effects: isolate known overhead light of 0.5 everywhere.
                foreach (var p in request.Species) p.CrownOpacity = 0;
                request.ExistingPlants = new[] { new ExistingPlant { Id = "test-canopy", Position = new Position2(100, 100),
                HeightM = 100, CrownRadiusM = 200, CrownOpacity = .5 } };
                var result = Run(request); Reconciles(result);
                foreach (var count in result.Counts)
                {
                    if (!means.ContainsKey(count.SpeciesId)) means[count.SpeciesId] = new List<int>();
                    means[count.SpeciesId].Add(count.Accepted);
                }
                Assert.All(result.Placements, p => Assert.Equal(.5, p.AvailableLight));
            }
            foreach (var p in Profiles())
            {
                double acceptance = Math.Clamp((.5 - p.MinimumLight) / (p.FullLight - p.MinimumLight), 0, 1);
                double expected = 4 * p.CandidatesPerHectare * acceptance;
                output.WriteLine($"{p.Id}: shade analytical={expected:F2}, observed={means[p.Id].Average():F2}");
                Assert.InRange(means[p.Id].Average(), expected * .95, expected * 1.05);
            }
        }

        [Fact]
        public void TallerAcceptedCrownsShadeButRejectedCrownsDoNot()
        {
            var request = Request();
            request.Species = new[] {
            new SpeciesProfile { Id = "tall", CandidatesPerHectare = .25, MatureHeightM = 30, CrownRadiusM = 1000, CrownOpacity = 1, RequiresShade = true },
            new SpeciesProfile { Id = "short", CandidatesPerHectare = 25, MatureHeightM = 5 } };
            var result = Run(request);
            Assert.Equal(1, result.Counts.Single(c => c.SpeciesId == "tall").ShadeRejected);
            Assert.Equal(100, result.Counts.Single(c => c.SpeciesId == "short").Accepted);
            request.Species[0].RequiresShade = false;
            result = Run(request);
            Assert.Equal(100, result.Counts.Single(c => c.SpeciesId == "short").ShadeRejected);
            Assert.Single(result.Placements);
        }

        [Fact]
        public void ExistingTreesOutsideBoundaryBlockAndShadeWithoutBeingGenerated()
        {
            var request = Request();
            request.ExistingPlants = new[] { new ExistingPlant { Id = "outside", Position = new Position2(-1, 100), HeightM = 100,
            ExclusionRadiusM = 500, CrownRadiusM = 500, CrownOpacity = 1 } };
            var result = Run(request); Assert.Empty(result.Placements); Reconciles(result);
            Assert.All(result.Counts, c => Assert.Equal(c.Candidates, c.ExistingPlantRejected));
            request.ExistingPlants[0].ExclusionRadiusM = 0;
            foreach (var p in request.Species) p.ExclusionRadiusM = 0;
            result = Run(request); Assert.Empty(result.Placements); Reconciles(result);
            Assert.All(result.Counts, c => Assert.Equal(c.Candidates, c.ShadeRejected));
        }

        [Fact]
        public void ConcaveBoundaryAndNegativeWorldCoordinatesAreRespected()
        {
            var request = Request();
            request.Boundary = new[] { new Position2(-200, -200), new Position2(0, -200), new Position2(0, -100),
            new Position2(-100, -100), new Position2(-100, 0), new Position2(-200, 0) };
            var result = Run(request); Assert.Equal(30000, result.AreaM2);
            Assert.All(result.Placements, p =>
            {
                Assert.InRange(p.Position.X, -200, 0); Assert.InRange(p.Position.Z, -200, 0);
                Assert.True(p.Position.X < -100 || p.Position.Z < -100);
            });
            Reconciles(result);
        }

        [Fact]
        public void InvalidInputsAndBudgetsFailBeforeSamplingOrReturningPartialPlans()
        {
            var request = Request(); request.Species[0].CrownOpacity = double.NaN;
            Assert.Throws<ArgumentException>(() => Run(request));
            request = Request(); request.Species = new[] { request.Species[0], request.Species[0] };
            Assert.Throws<ArgumentException>(() => Run(request));
            request = Request(); request.Boundary = new[] { new Position2(0, 0), new Position2(200, 200), new Position2(0, 200), new Position2(200, 0) };
            Assert.Throws<ArgumentException>(() => Run(request));
            request = Request(); request.MaxCandidates = 10;
            int calls = 0;
            Assert.Throws<InvalidOperationException>(() => Run(request, _ => { calls++; return new GroundSample(0); }));
            Assert.Equal(0, calls);
            request = Request(); request.MaxPlacements = 1;
            Assert.Throws<InvalidOperationException>(() => Run(request));
            request = Request(); request.MaxPositionAttempts = 1;
            Assert.Throws<InvalidOperationException>(() => Run(request));
            request = Request(); request.MaxNeighbourChecks = 1;
            Assert.Throws<InvalidOperationException>(() => Run(request));
            request = new GenerationRequest { Boundary = Square(), OverallDensityPercent = -1 };
            Assert.Throws<ArgumentException>(() => Run(request, _ => throw new Xunit.Sdk.XunitException("Invalid input must fail before sampling")));
        }

        [Fact]
        public void FullMixedForestIsConsistentWithIndependentCoverageEstimate()
        {
            var profiles = Profiles();
            var estimated = new Dictionary<string, double>();
            foreach (var species in profiles.OrderByDescending(p => p.MatureHeightM))
            {
                double load = profiles.Sum(p => p.CandidatesPerHectare / 10000 * Math.PI * Math.Pow(species.ExclusionRadiusM + p.ExclusionRadiusM, 2));
                double physical = species.CandidatesPerHectare * 4 * (1 - Math.Exp(-load)) / load;
                // Independent homogeneous Poisson canopy-cover approximation. The real pipeline uses
                // spatially correlated hard-core trees, so this is a broad model check, not an exact oracle.
                var lightDistribution = new List<(double Light, double Probability)> { (1, 1) };
                foreach (var taller in profiles.Where(p => p.MatureHeightM > species.MatureHeightM))
                {
                    double coverage = estimated[taller.Id] / 40000 * Math.PI * taller.CrownRadiusM * taller.CrownRadiusM;
                    var next = new List<(double, double)>();
                    foreach (var state in lightDistribution)
                    {
                        double probability = Math.Exp(-coverage);
                        for (int k = 0; k <= 12; k++)
                        {
                            next.Add((state.Light * Math.Pow(1 - taller.CrownOpacity, k), state.Probability * probability));
                            probability *= coverage / (k + 1);
                        }
                    }
                    lightDistribution = next;
                }
                estimated[species.Id] = physical * lightDistribution.Sum(s => s.Probability
                    * Math.Clamp((s.Light - species.MinimumLight) / (species.FullLight - species.MinimumLight), 0, 1));
            }
            var runs = Enumerable.Range(1, 64).Select(seed => Run(Request((ulong)seed))).ToArray();
            foreach (var species in profiles)
            {
                var counts = runs.Select(r => r.Counts.Single(c => c.SpeciesId == species.Id).Accepted).ToArray();
                output.WriteLine($"Full pipeline {species.Id}: coverage estimate={estimated[species.Id]:F1}, observed mean={counts.Average():F1}, range={counts.Min()}–{counts.Max()}");
                Assert.InRange(counts.Average(), estimated[species.Id] * .8, estimated[species.Id] * 1.2);
            }
            foreach (var result in runs) Reconciles(result);
        }

        [Fact]
        public void TerrainAndEnvironmentUseIntersectedLimitsAndMinimumSuitability()
        {
            var request = Request(); request.ResolveCompetition = false; request.ResolveShade = false;
            request.Species = new[] { request.Species[0] };
            request.Species[0].Temperature = new SuitabilityCurve { Minimum = 0, Optimum = 10, Maximum = 20 };
            request.Species[0].Slope = new SuitabilityCurve { Minimum = 0, Optimum = 10, Maximum = 30 };
            // Both scores are 0.5: accepted probability must be 0.5, not product 0.25.
            var runs = Enumerable.Range(1, 32).Select(seed =>
            {
                request.Seed = (ulong)seed;
                return Run(request, _ => new GroundSample(0, 20, 5));
            }).ToArray();
            Assert.InRange(runs.Average(r => r.Placements.Count), 185, 215);
            request.Terrain = new TerrainRules(-10, 10, 5);
            var blocked = Run(request, _ => new GroundSample(0, 6, 10));
            Assert.Empty(blocked.Placements); Assert.Equal(400, blocked.Counts[0].TerrainRejected);
            request.Terrain = new TerrainRules(-100, 100, 90);
            request.Species[0].Terrain = new TerrainRules(-10, 10, 90);
            blocked = Run(request, _ => new GroundSample(11, 0, 10));
            Assert.Empty(blocked.Placements); Assert.Equal(400, blocked.Counts[0].TerrainRejected);
        }

        [Fact]
        public void UnsuitableSpeciesDoesNotSuppressOtherSpeciesAndInputIsNotMutated()
        {
            var request = Request();
            var baseline = Run(request);
            string input = JsonSerializer.Serialize(request);
            request.Species = request.Species.Concat(new[] { new SpeciesProfile { Id = "unsuitable", CandidatesPerHectare = 1000,
            ExclusionRadiusM = 100, MatureHeightM = 100, CrownRadiusM = 100,
            Temperature = new SuitabilityCurve { Minimum = 30, Optimum = 40, Maximum = 50 } } }).ToArray();
            Assert.Equal(Signature(baseline), Signature(Run(request)));
            request.Species = request.Species.Where(p => p.Id != "unsuitable").ToArray();
            Assert.Equal(input, JsonSerializer.Serialize(request));
        }

        [Fact]
        public void CancellationAndInvalidTerrainAreHandled()
        {
            var request = Request();
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => new FoliageGenerator().Generate(request, _ => new GroundSample(0), cancelled.Token));
            using var during = new CancellationTokenSource();
            Assert.Throws<OperationCanceledException>(() => new FoliageGenerator().Generate(request, _ => { during.Cancel(); return new GroundSample(0); }, during.Token));
            var result = Run(request, _ => new GroundSample(double.NaN));
            Assert.Empty(result.Placements); Reconciles(result);
            Assert.All(result.Counts, c => Assert.Equal(c.Candidates, c.TerrainRejected));
            foreach (var p in request.Species) p.DensityPercent = 0;
            result = Run(request, _ => throw new InvalidOperationException("No candidates should sample terrain"));
            Assert.Empty(result.Placements); Reconciles(result);
        }
    }
}
