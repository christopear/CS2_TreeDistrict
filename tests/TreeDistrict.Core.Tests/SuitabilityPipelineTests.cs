using System.Text.Json;
using TreeDistrict.Core.Foliage;

namespace TreeDistrict.Core.Tests
{
    public class SuitabilityPipelineTests
    {
        private static readonly TerrainRules Rules = new(-10000, 10000, 90);
        private static SpeciesProfile Profile() => new()
        {
            Id = "bush",
            CandidatesPerHectare = 100,
            PreferredSpacingRadiusM = 2,
            Temperature = new SuitabilityCurve { Minimum = 0, Optimum = 20, Maximum = 40 },
            Slope = new SuitabilityCurve { Minimum = -1, Optimum = 10, Maximum = 70 }
        };
        private static GenerationRequest Request(SpeciesProfile profile) => new()
        {
            Species = new[] { profile },
            ResolveShade = false,
            Boundary = new[] { new Position2(0, 0), new Position2(50, 0), new Position2(50, 50), new Position2(0, 50) }
        };

        [Fact]
        public void HardExclusionDoesNotChangeSoftScore()
        {
            var evaluator = new VegetationSuitabilityEvaluator();
            var allowed = evaluator.Evaluate(Profile(), new PlacementEnvironment(new GroundSample(0), 20, 0), Rules);
            var blocked = evaluator.Evaluate(Profile(), new PlacementEnvironment(new GroundSample(0, isBlocked: true), 20, 0), Rules);
            Assert.True(allowed.Allowed); Assert.False(blocked.Allowed);
            Assert.Equal(1, blocked.Score); Assert.Equal(allowed.Score, blocked.Score);
            var unsuitable = evaluator.Evaluate(Profile(), new PlacementEnvironment(new GroundSample(0), 40, 0), Rules);
            Assert.True(unsuitable.Allowed); Assert.Equal(0, unsuitable.Score);
        }

        [Fact]
        public void FormulaKeepsMinimumResponseAndDiagnostics()
        {
            var score = new VegetationSuitabilityEvaluator().Evaluate(Profile(),
                new PlacementEnvironment(new GroundSample(0), 10, 55), Rules);
            Assert.True(score.Allowed);
            Assert.Equal(.5, score.TemperatureScore); Assert.Equal(.25, score.SlopeScore);
            Assert.Equal(.25, score.Score);
        }

        [Fact]
        public void ExactGroundLimitsAreNotSmoothedByEnvironmentGrid()
        {
            var request = Request(Profile());
            var field = LocalEnvironmentGrid.Build(request.Boundary, _ => new LocalEnvironmentSample(0, 0, 20));
            var provider = new PlacementEnvironmentProvider(_ => new GroundSample(20, 60), field: field);
            var profile = Profile(); profile.Terrain = new TerrainRules(0, 100, 30);
            var result = new VegetationSuitabilityEvaluator().Evaluate(profile, provider.Sample(new Position2(20, 20)), Rules);
            Assert.False(result.Allowed); Assert.Equal(1, result.Score);
            Assert.Throws<InvalidOperationException>(() => provider.Sample(new Position2(51, 20)));
        }

        [Fact]
        public void InvalidEnvironmentIsDisallowedAndScoresStayFinite()
        {
            var result = new VegetationSuitabilityEvaluator().Evaluate(Profile(),
                new PlacementEnvironment(new GroundSample(0), double.NaN, 0), Rules);
            Assert.False(result.Allowed); Assert.Equal(0, result.Score);
            Assert.Throws<ArgumentOutOfRangeException>(() => new SuitabilityResult(true, double.NaN, 1, 1));
        }

        [Fact]
        public void JsonProfileAndTerrainClimateReachCoordinatorAndSolver()
        {
            var profile = JsonSerializer.Deserialize<SpeciesProfile>("""
            {"id":"bush","candidatesPerHectare":100,"preferredSpacingRadiusM":2,
             "temperature":{"minimum":0,"optimum":20,"maximum":40}}
            """, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            var request = Request(profile); request.Climate = new ClimateProfile(20, 0, 10);
            GroundSample Terrain(Position2 p) => new(1000, temperatureC: 99);
            var coordinator = new FoliageGenerationCoordinator(request, Terrain);
            Assert.Equal(.5, coordinator.Evaluate(profile, new Position2(25, 25)).Score);
            var result = coordinator.Generate(new ProtonScatterSettings());
            Assert.NotEmpty(result.Placements);
            var direct = new ProtonFoliageGenerator().Generate(request, Terrain, new ProtonScatterSettings(), environment: coordinator.Environment);
            Assert.Equal(JsonSerializer.Serialize(direct), JsonSerializer.Serialize(result));
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => coordinator.Generate(new ProtonScatterSettings(), cancelled.Token));
        }

        private sealed class ZeroResponse : IVegetationSuitabilityEvaluator
        {
            public int Calls;
            public SuitabilityResult Evaluate(SpeciesProfile profile, PlacementEnvironment environment, TerrainRules rules)
            { Calls++; return new SuitabilityResult(true, 0, 1, 1); }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void InjectedEvaluatorDrivesBothSelectionAndLegacyFiltering(bool pre)
        {
            var evaluator = new ZeroResponse();
            var coordinator = new FoliageGenerationCoordinator(Request(Profile()), _ => new GroundSample(0), evaluator: evaluator);
            Assert.Empty(coordinator.Generate(new ProtonScatterSettings { UsePrePlacementSuitability = pre }).Placements);
            Assert.True(evaluator.Calls > 0);
            Assert.NotEmpty(coordinator.Generate(new ProtonScatterSettings { EnableLocalComposition = false }).Placements);
        }
    }
}
