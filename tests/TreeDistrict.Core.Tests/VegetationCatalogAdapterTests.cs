using TreeDistrict.Core.Foliage;

namespace TreeDistrict.Core.Tests
{
    public class VegetationCatalogAdapterTests
    {
        [Fact]
        public void RealCataloguesJoinByIdAndFeedTheSolverWithoutInventingTemperatureCurves()
        {
            string Data(string name) => Path.Combine(AppContext.BaseDirectory, name);
            var resolved = VegetationCatalogAdapter.Load(Data("base-game-vegetation.v1.json"),
                Data("real-world-vegetation.v1.json"), Data("vegetation-adapter.v1.json"));
            Assert.Equal(26, resolved.Count);
            Assert.All(resolved, p =>
            {
                Assert.Null(p.Profile.Temperature);
                Assert.True(p.Profile.PreferredSpacingRadiusM > 0);
                Assert.True(p.Profile.MatureHeightM > 0);
                Assert.True(p.Profile.CrownRadiusM > 0);
            });
            Assert.Single(resolved, p => p.BotanicalSource == null);
            var tree = resolved.Single(p => p.Profile.Id == "cs2:NA_LondonPlaneTree01");
            var shrub = resolved.Single(p => p.Profile.Id == "cs2:GreenBushWild01");
            var evaluator = new VegetationSuitabilityEvaluator();
            var environment = new PlacementEnvironment(new GroundSample(0, 60), 18, 60);
            var rules = new TerrainRules(-10000, 10000, 90);
            Assert.True(evaluator.Evaluate(tree.Profile, environment, rules).Score
                < evaluator.Evaluate(shrub.Profile, environment, rules).Score);
            var request = new GenerationRequest
            {
                Species = new[] { tree.Profile, shrub.Profile },
                ResolveShade = false,
                Boundary = new[] { new Position2(0, 0), new Position2(100, 0), new Position2(100, 100), new Position2(0, 100) }
            };
            var result = new FoliageGenerationCoordinator(request, _ => new GroundSample(0, 60)).Generate(new ProtonScatterSettings());
            Assert.NotEmpty(result.Placements);
            Assert.All(result.Counts, c => Assert.Equal(0, c.EnvironmentRejected));
        }

        [Fact]
        public void AuthoredQuantitativeInputsOverrideKindDefaults()
        {
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "assets.json"), """
              {"schemaVersion":1,"assets":[{"id":"a","kind":"tree",
              "placementDefaults":{"preferredSpacingRadiusM":3},
              "baseGameMeasurements":[{"stage":"mature","heightM":5,"widthM":4,"depthM":6}]}]}
              """);
                File.WriteAllText(Path.Combine(dir, "botany.json"), """
              {"schemaVersion":1,"profiles":[{"id":"a","quantitativeModelInputs":{
              "temperatureSuitabilityCurveC":{"minimum":0,"optimum":10,"maximum":20},
              "slopeSuitabilityCurveDegrees":{"minimum":-1,"optimum":5,"maximum":30},
              "elevationSurvivalLimitsM":[0,1000],"minimumTrunkSpacingM":2,"canopyOpacityPercent":50}}]}
              """);
                var profile = VegetationCatalogAdapter.Load(Path.Combine(dir, "assets.json"), Path.Combine(dir, "botany.json"),
                    Path.Combine(AppContext.BaseDirectory, "vegetation-adapter.v1.json")).Single().Profile;
                Assert.Equal(10, profile.Temperature!.Optimum); Assert.Equal(30, profile.Slope!.Maximum);
                Assert.Equal(1, profile.ExclusionRadiusM); Assert.Equal(.5, profile.CrownOpacity);
                Assert.Equal(1000, profile.Terrain.MaximumElevation);
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
