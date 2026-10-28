using System.Text.Json;
using TreeDistrict.Core.Foliage;

namespace TreeDistrict.Core.Tests
{
    public class VegetationSetTests
    {
        [Theory]
        [InlineData("nz-regenerating-bush", 3)]
        [InlineData("low-cover", 4)]
        [InlineData("european-woodland", 8)]
        [InlineData("european-trees", 6)]
        [InlineData("american-woodland", 5)]
        [InlineData("american-trees", 3)]
        [InlineData("london-plane", 3)]
        [InlineData("palms-woodland", 6)]
        [InlineData("palms-trees", 4)]
        [InlineData("european-02-woodland", 8)]
        public void PresetsResolveAndGenerateWithoutMutatingSourceProfiles(string id, int count)
        {
            string Data(string name) => Path.Combine(AppContext.BaseDirectory, name);
            var assets = VegetationCatalogAdapter.Load(Data("base-game-vegetation.v1.json"), Data("real-world-vegetation.v1.json"), Data("vegetation-adapter.v1.json"));
            var source = assets.Select(a => a.Profile).ToArray();
            string before = JsonSerializer.Serialize(source);
            var set = VegetationSetCatalog.Load(Data("vegetation-sets.v1.json"), id);
            var profiles = set.Apply(source);
            Assert.Equal(count, profiles.Length); Assert.Equal(before, JsonSerializer.Serialize(source));
            foreach (var profile in profiles)
            {
                var kind = assets.Single(a => a.Profile.Id == profile.Id).AssetSource.GetProperty("kind").GetString();
                if (id == "low-cover") Assert.Equal("shrub", kind);
                if (id.EndsWith("-trees")) Assert.Equal("tree", kind);
            }
            var request = new GenerationRequest
            {
                Species = profiles,
                OverallDensityPercent = set.OverallDensityPercent,
                ResolveShade = false,
                Boundary = new[] { new Position2(0, 0), new Position2(80, 0), new Position2(80, 80), new Position2(0, 80) }
            };
            Assert.NotEmpty(new FoliageGenerationCoordinator(request, _ => new GroundSample(0)).Generate(new ProtonScatterSettings()).Placements);
        }
        [Fact]
        public void TreeWeightsMatchWithAndWithoutBushes()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "vegetation-sets.v1.json");
            foreach (string region in new[] { "european", "american", "palms" })
            {
                var trees = VegetationSetCatalog.Load(path, region + "-trees");
                var woodland = VegetationSetCatalog.Load(path, region + "-woodland");
                foreach (var tree in trees.Members)
                    Assert.Equal(tree.Weight, woodland.Members.Single(m => m.AssetId == tree.AssetId).Weight);
            }
            Assert.Throws<ArgumentException>(() => VegetationSetCatalog.Load(path, "missing"));
        }
        [Fact]
        public void NzOverridesAreLocalAndPreservePhysicalAssetData()
        {
            string Data(string name) => Path.Combine(AppContext.BaseDirectory, name);
            var source = VegetationCatalogAdapter.Load(Data("base-game-vegetation.v1.json"), Data("real-world-vegetation.v1.json"), Data("vegetation-adapter.v1.json"))
                .Select(a => a.Profile).ToArray();
            var set = VegetationSetCatalog.Load(Data("vegetation-sets.v1.json"), "nz-regenerating-bush");
            var before = JsonSerializer.Serialize(source);
            var nz = set.Apply(source);
            Assert.Equal(new double[] { 55, 30, 15 }, nz.Select(p => p.CandidatesPerHectare));
            Assert.Equal(new double[] { 60, 60, 45 }, nz.Select(p => p.Terrain.MaximumSlopeDegrees));
            foreach (var profile in nz)
            {
                var original = source.Single(p => p.Id == profile.Id);
                Assert.Equal(original.PreferredSpacingRadiusM, profile.PreferredSpacingRadiusM);
                Assert.Equal(original.CrownRadiusM, profile.CrownRadiusM);
                Assert.Equal(original.MatureHeightM, profile.MatureHeightM);
            }
            var evaluator = new VegetationSuitabilityEvaluator();
            var cold = new PlacementEnvironment(new GroundSample(0), 0, 0);
            Assert.True(evaluator.Evaluate(nz[1], cold, new TerrainRules(-10000, 10000, 90)).Score > 0);
            Assert.Equal(0, evaluator.Evaluate(nz[2], cold, new TerrainRules(-10000, 10000, 90)).Score);
            nz[0].Temperature!.Optimum = 99;
            Assert.Equal(12, set.Apply(source)[0].Temperature!.Optimum);
            Assert.Equal(before, JsonSerializer.Serialize(source));
            Assert.Null(VegetationSetCatalog.Load(Data("vegetation-sets.v1.json"), "european-woodland").Apply(source)[0].Temperature);
        }

    }
}
