using TreeDistrict.Core.Foliage;

namespace TreeDistrict.Core.Tests
{
    public class LocalEnvironmentGridTests
    {
        private static Position2[] Bounds(double width = 12, double height = 8, double origin = -10) => new[] {
            new Position2(origin, origin), new Position2(origin + width, origin),
            new Position2(origin + width, origin + height), new Position2(origin, origin + height) };

        [Fact]
        public void DefaultResolutionSamplesCornersAndClampsLastCellsToBounds()
        {
            var visited = new List<Position2>();
            var field = LocalEnvironmentGrid.Build(Bounds(), p =>
            {
                visited.Add(p); return new LocalEnvironmentSample(100, 10, 18);
            });
            Assert.Equal(5, field.CellSizeM);
            Assert.Equal(4, field.Columns); Assert.Equal(3, field.Rows);
            Assert.Equal(12, field.SampleCount); Assert.Equal(12, visited.Count);
            Assert.All(visited, p => { Assert.InRange(p.X, -10, 2); Assert.InRange(p.Z, -10, -2); });
            Assert.True(field.TrySample(new Position2(2, -2), out var result));
            Assert.Equal(100, result.ElevationM);
            // Queries never call the source again: the result is a snapshot.
            Assert.Equal(12, visited.Count);
        }

        [Theory]
        [InlineData(5)]
        [InlineData(2)]
        [InlineData(50)]
        public void InterpolationReconstructsLinearFieldsIncludingNarrowEdgeCells(double resolution)
        {
            var field = LocalEnvironmentGrid.Build(Bounds(), p => new LocalEnvironmentSample(
                100 + p.X + 2 * p.Z, 30 + p.X, 18 - .01 * p.X), resolution);
            foreach (var p in new[] { new Position2(-10, -10), new Position2(2, -2), new Position2(1, -3), new Position2(-6.3, -8.1) })
            {
                Assert.True(field.TrySample(p, out var result));
                Assert.Equal(100 + p.X + 2 * p.Z, result.ElevationM, 10);
                Assert.Equal(30 + p.X, result.SlopeDegrees, 10);
                Assert.Equal(18 - .01 * p.X, result.TemperatureC, 10);
            }
        }

        [Fact]
        public void TerrainAdapterUsesClimateWhenPresentOtherwiseProviderTemperature()
        {
            GroundSample Terrain(Position2 p) => new(1000 + 100 * p.X, 20, temperatureC: 7, isBlocked: true);
            var field = LocalEnvironmentGrid.FromTerrain(Bounds(), Terrain, new ClimateProfile(20, 0, 6));
            Assert.True(field.TrySample(new Position2(0, -5), out var result));
            Assert.Equal(14, result.TemperatureC, 10);
            var fallback = LocalEnvironmentGrid.FromTerrain(Bounds(), Terrain);
            Assert.True(fallback.TrySample(new Position2(0, -5), out result));
            Assert.Equal(7, result.TemperatureC);
        }

        [Fact]
        public void InvalidOrOutsideQueriesDoNotInventConditions()
        {
            var field = LocalEnvironmentGrid.Build(Bounds(), _ => new LocalEnvironmentSample(0, 0, 18));
            Assert.False(field.TrySample(new Position2(3, -5), out _));
            Assert.False(field.TrySample(new Position2(-11, -5), out _));
            Assert.False(field.TrySample(new Position2(double.NaN, 0), out _));
            Assert.Throws<ArgumentException>(() => new LocalEnvironmentSample(double.NaN, 0, 18));
            Assert.Throws<ArgumentException>(() => new LocalEnvironmentSample(0, 91, 18));
            Assert.Throws<ArgumentException>(() => LocalEnvironmentGrid.Build(Bounds(), _ => default, 0));
            Assert.Throws<InvalidOperationException>(() => LocalEnvironmentGrid.Build(Bounds(), _ => default, maxSamples: 4));
            Assert.Throws<ArgumentException>(() => LocalEnvironmentGrid.FromTerrain(Bounds(), _ => new GroundSample(double.NaN)));
        }

        [Fact]
        public void CancellationBeforeAndDuringBuildIsHonoured()
        {
            using var token = new CancellationTokenSource(); token.Cancel();
            Assert.Throws<OperationCanceledException>(() => LocalEnvironmentGrid.Build(Bounds(),
                _ => throw new Exception("Must not call provider"), cancellationToken: token.Token));
            using var during = new CancellationTokenSource();
            Assert.Throws<OperationCanceledException>(() => LocalEnvironmentGrid.Build(Bounds(),
                _ => { during.Cancel(); return new LocalEnvironmentSample(0, 0, 18); }, cancellationToken: during.Token));
        }

        private static GenerationRequest Request() => new()
        {
            Boundary = Bounds(40, 40, 0),
            Seed = 42,
            Species = new[] { new SpeciesProfile { Id = "placeholder", CandidatesPerHectare = 100,
                Temperature = new SuitabilityCurve { Minimum = 0, Optimum = 18, Maximum = 36 },
                Slope = new SuitabilityCurve { Minimum = 0, Optimum = 10, Maximum = 60 } } }
        };

        [Fact]
        public void GeneratorConsumesFieldButKeepsExactPlacementHeightAndHardLimits()
        {
            var request = Request(); var generator = new ProtonFoliageGenerator(); var settings = new ProtonScatterSettings { UsePrePlacementSuitability = false };
            var bad = LocalEnvironmentGrid.Build(request.Boundary, _ => new LocalEnvironmentSample(999, 60, 18));
            var rejected = generator.Generate(request, _ => new GroundSample(7), settings, environment: bad);
            Assert.Empty(rejected.Placements);
            Assert.True(rejected.Counts[0].EnvironmentRejected > 0);
            var good = LocalEnvironmentGrid.Build(request.Boundary, _ => new LocalEnvironmentSample(999, 0, 18));
            var accepted = generator.Generate(request, _ => new GroundSample(7), settings, environment: good);
            Assert.NotEmpty(accepted.Placements);
            Assert.All(accepted.Placements, p => Assert.Equal(7, p.ElevationM));
            Assert.Empty(generator.Generate(request, _ => new GroundSample(7, isBlocked: true), settings, environment: good).Placements);
            request.Species[0].Terrain = new TreeDistrict.Core.TerrainRules(-100, 100, 30);
            Assert.Empty(generator.Generate(request, _ => new GroundSample(7, 70), settings, environment: good).Placements);
            Assert.Throws<InvalidOperationException>(() => generator.Generate(Request(), _ => new GroundSample(0), settings,
                environment: LocalEnvironmentGrid.Build(Bounds(), _ => default)));
        }

        [Fact]
        public void ResolutionDoesNotAffectPoissonLocationsOrFlatTerrainOutput()
        {
            var request = Request(); var generator = new ProtonFoliageGenerator(); var settings = new ProtonScatterSettings { UsePrePlacementSuitability = false };
            GroundSample Terrain(Position2 _) => new(0);
            var baseline = generator.Generate(request, Terrain, settings);
            foreach (double cellSize in new[] { 1d, 5d, 20d })
            {
                var field = LocalEnvironmentGrid.FromTerrain(request.Boundary, Terrain, cellSizeM: cellSize);
                var result = generator.Generate(request, Terrain, settings, environment: field);
                Assert.Equal(baseline.Placements.Select(p => p.Position), result.Placements.Select(p => p.Position));
                Assert.Equal(baseline.PositionAttempts, result.PositionAttempts);
            }
        }
    }
}
