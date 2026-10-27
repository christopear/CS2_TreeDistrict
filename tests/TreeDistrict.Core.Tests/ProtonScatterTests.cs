using System.Text.Json;
using TreeDistrict.Core.Foliage;

namespace TreeDistrict.Core.Tests
{
    public class ProtonScatterTests
    {
        private static Position2[] Rectangle(double x = 200, double z = 80) => new[] {
            new Position2(0, 0), new Position2(x, 0), new Position2(x, z), new Position2(0, z) };
        private static GenerationRequest Request() => new()
        {
            Seed = 42,
            Boundary = Rectangle(),
            Species = new[] {
            new SpeciesProfile { Id = "tree", CandidatesPerHectare = 100, ExclusionRadiusM = 4,
                CrownRadiusM = 8, MatureHeightM = 20, CrownOpacity = .7 },
            new SpeciesProfile { Id = "shrub", CandidatesPerHectare = 100, ExclusionRadiusM = 1,
                CrownRadiusM = 2, MatureHeightM = 2, CrownOpacity = .1, MinimumLight = 0, FullLight = .2 }
        }
        };
        private static GenerationResult Run(GenerationRequest r, Func<Position2, GroundSample>? sample = null) =>
            new ProtonFoliageGenerator().Generate(r, sample ?? (_ => new GroundSample(0)), new ProtonScatterSettings { UsePrePlacementSuitability = false });

        [Theory]
        [InlineData(200, 80)]
        [InlineData(80, 200)]
        public void PortRespectsSpacingOnRectanglesAndReachesAllQuadrants(double width, double height)
        {
            var points = new ProtonPoissonSampler().Generate(Rectangle(width, height), 6.25, 42);
            Assert.True(points.Count > 150);
            Assert.Equal(4, points.Select(p => (p.X >= width / 2, p.Z >= height / 2)).Distinct().Count());
            foreach (var p in points) { Assert.InRange(p.X, 0, width); Assert.InRange(p.Z, 0, height); }
            for (int i = 0; i < points.Count; i++) for (int j = i + 1; j < points.Count; j++)
                Assert.True(Math.Pow(points[i].X - points[j].X, 2) + Math.Pow(points[i].Z - points[j].Z, 2) >= 6.25 * 6.25);
        }

        [Fact]
        public void ConcaveDomainNeverPlacesInMissingCorner()
        {
            var boundary = new[] { new Position2(0,0), new Position2(100,0), new Position2(100,20),
                new Position2(20,20), new Position2(20,100), new Position2(0,100) };
            var points = new ProtonPoissonSampler().Generate(boundary, 4, 42);
            Assert.NotEmpty(points);
            Assert.All(points, p => Assert.True(p.X <= 20 || p.Z <= 20));
        }

        [Fact]
        public void TinyDomainRetainsItsInitialPoint()
        {
            Assert.Single(new ProtonPoissonSampler().Generate(Rectangle(1, 1), 10, 42));
        }

        [Fact]
        public void MatureClearanceAgesAndDiagnosticsAreConsistent()
        {
            var request = Request(); var result = Run(request);
            Assert.Equal(ProtonFoliageGenerator.AlgorithmVersion, result.AlgorithmVersion);
            Assert.NotEmpty(result.Placements);
            foreach (var count in result.Counts) Assert.Equal(count.Candidates, count.Accepted + count.Rejected);
            var profiles = request.Species.ToDictionary(p => p.Id);
            for (int i = 0; i < result.Placements.Count; i++) for (int j = i + 1; j < result.Placements.Count; j++)
            {
                var a = result.Placements[i]; var b = result.Placements[j];
                double clearance = profiles[a.SpeciesId].ExclusionRadiusM + profiles[b.SpeciesId].ExclusionRadiusM;
                Assert.True(Math.Pow(a.Position.X - b.Position.X, 2) + Math.Pow(a.Position.Z - b.Position.Z, 2) >= clearance * clearance);
            }
            foreach (var p in request.Species) p.Ages = new[] { new AgeWeight { Id = "young" } };
            var young = Run(request);
            Assert.Equal(result.Placements.Select(p => p.Position), young.Placements.Select(p => p.Position));
            Assert.All(young.Placements, p => Assert.Equal("young", p.AgeId));
        }

        [Fact]
        public void DeterministicOrderIndependentAndDensityIsEffective()
        {
            var r = Request(); var baseline = JsonSerializer.Serialize(Run(r));
            Assert.Equal(baseline, JsonSerializer.Serialize(Run(r)));
            r.Species = r.Species.Reverse().ToArray();
            Assert.Equal(baseline, JsonSerializer.Serialize(Run(r)));
            r.Seed++; Assert.NotEqual(baseline, JsonSerializer.Serialize(Run(r)));
            r.ResolveShade = false; r.ResolveCompetition = false;
            int full = Run(r).Placements.Count;
            r.OverallDensityPercent = 25;
            Assert.InRange(Run(r).Placements.Count, full / 6, full / 2);
            r.OverallDensityPercent = 0;
            Assert.Empty(Run(r, _ => throw new Exception("Should not sample terrain")).Placements);
        }

        [Fact]
        public void TerrainAndExistingObstaclesAreRespected()
        {
            var r = Request();
            Assert.Empty(Run(r, _ => new GroundSample(0, isBlocked: true)).Placements);
            Assert.Empty(Run(r, _ => new GroundSample(double.NaN)).Placements);
            r.ExistingPlants = new[] { new ExistingPlant { Id = "obstacle", Position = new Position2(100, 40), ExclusionRadiusM = 1000 } };
            Assert.Empty(Run(r).Placements);
        }

        [Fact]
        public void ShadeActsWithoutChangingSamplerAndYoungTreesReserveMatureShade()
        {
            var r = Request(); r.ResolveCompetition = false;
            r.ExistingPlants = new[] { new ExistingPlant { Id = "canopy", Position = new Position2(100,40),
                HeightM = 100, CrownRadiusM = 1000, CrownOpacity = 1 } };
            Assert.Empty(Run(r).Placements);
            r.ResolveShade = false; Assert.NotEmpty(Run(r).Placements);
        }

        [Fact]
        public void WorkLimitsAndCancellationFailWithoutPartialResults()
        {
            var r = Request(); r.MaxCandidates = 1;
            Assert.Throws<InvalidOperationException>(() => Run(r));
            r = Request(); r.MaxPositionAttempts = 1;
            Assert.Throws<InvalidOperationException>(() => Run(r));
            r = Request(); r.MaxNeighbourChecks = 1;
            Assert.Throws<InvalidOperationException>(() => Run(r));
            r = Request(); r.MaxPlacements = 1;
            Assert.Throws<InvalidOperationException>(() => Run(r));
            using var c = new CancellationTokenSource(); c.Cancel();
            Assert.Throws<OperationCanceledException>(() => new ProtonFoliageGenerator().Generate(Request(),
                _ => new GroundSample(0), new ProtonScatterSettings { UsePrePlacementSuitability = false }, c.Token));
            Assert.Throws<ArgumentException>(() => new ProtonPoissonSampler().Generate(Rectangle(), double.NaN, 1));
        }
        [Fact]
        public void PerVegetationSpacingUsesSumOfRadiiAndDensityScaling()
        {
            var request = Request();
            request.ResolveShade = false;
            request.Species[0].PreferredSpacingRadiusM = 4.64;
            request.Species[1].PreferredSpacingRadiusM = 2.313;
            request.Species[0].ExclusionRadiusM = 0;
            request.Species[1].ExclusionRadiusM = 0;
            var profiles = request.Species.ToDictionary(p => p.Id);
            int full = 0;
            foreach (double density in new[] { 100d, 25d })
            {
                request.OverallDensityPercent = density;
                var result = Run(request);
                Assert.Equal("foliage-proton-variable-spacing-v1", result.AlgorithmVersion);
                Assert.NotEmpty(result.Placements);
                Assert.Equal(2, result.Placements.Select(p => p.SpeciesId).Distinct().Count());
                Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Run(request)));
                for (int a = 0; a < result.Placements.Count; a++)
                    for (int b = a + 1; b < result.Placements.Count; b++)
                    {
                        var p = result.Placements[a]; var q = result.Placements[b];
                        double distance = (profiles[p.SpeciesId].PreferredSpacingRadiusM!.Value
                            + profiles[q.SpeciesId].PreferredSpacingRadiusM!.Value) / Math.Sqrt(density / 100);
                        Assert.True(Math.Pow(p.Position.X - q.Position.X, 2) + Math.Pow(p.Position.Z - q.Position.Z, 2) >= distance * distance - 1e-9);
                    }
                if (density == 100) full = result.Placements.Count;
                else Assert.InRange(result.Placements.Count, full / 6, full / 2);
                foreach (var c in result.Counts) Assert.Equal(c.Candidates, c.Accepted + c.Rejected);
            }
        }

        [Fact]
        public void VariableSpacingRejectsIncompleteProfilesAndHonoursWorkLimits()
        {
            var request = Request(); request.Species[0].PreferredSpacingRadiusM = 4;
            Assert.Throws<ArgumentException>(() => Run(request));
            request.Species[1].PreferredSpacingRadiusM = 2;
            request.MaxCandidates = 1;
            Assert.Throws<InvalidOperationException>(() => Run(request));
            request.MaxCandidates = 100000; request.MaxNeighbourChecks = 1;
            Assert.Throws<InvalidOperationException>(() => Run(request));
            request.MaxNeighbourChecks = 20000000; request.MaxPositionAttempts = 1;
            Assert.Throws<InvalidOperationException>(() => Run(request));
            request.Species[1].PreferredSpacingRadiusM = double.NaN;
            Assert.Throws<ArgumentException>(() => Run(request));
        }

    }
}
