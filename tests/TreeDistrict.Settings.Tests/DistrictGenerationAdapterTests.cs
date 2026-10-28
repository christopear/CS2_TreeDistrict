using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TreeDistrict.Core.Foliage;
using Xunit;

namespace TreeDistrict.Tests
{
    public class DistrictGenerationAdapterTests
    {
        private static JObject Catalog() => SettingsModel.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "editor-catalog.json")));
        private static ProtonScatterSettings Scatter() => Newtonsoft.Json.JsonConvert.DeserializeObject<ProtonScatterSettings>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "proton-scatter.json")));
        private static JObject District(JToken set) => new JObject
        {
            ["id"] = "test",
            ["name"] = "Test area",
            ["seed"] = 42,
            ["revision"] = 0,
            ["generationMode"] = "onDemand",
            ["libraryId"] = null,
            ["baseline"] = null,
            ["working"] = set.DeepClone(),
            ["boundary"] = JArray.Parse("[{x:0,z:0},{x:100,z:0},{x:100,z:100},{x:0,z:100}]")
        };
        [Fact]
        public void EveryPresetProducesDeterministicPlanWithoutMutatingEditorState()
        {
            var catalog = Catalog();
            Assert.Equal(7, catalog["sets"].Count());
            Assert.Equal(26, catalog["assets"].Count());
            foreach (var preset in catalog["sets"])
            {
                var record = District(preset["set"]);
                var before = record.ToString();
                var request = DistrictGenerationAdapter.Create(record, (JObject)catalog["cityDefaults"]);
                var coordinator = new FoliageGenerationCoordinator(request, p => new GroundSample(0));
                var a = coordinator.Generate(Scatter());
                var b = coordinator.Generate(Scatter());
                Assert.NotEmpty(a.Placements);
                Assert.Equal(a.Placements.Select(p => (p.SpeciesId, p.Position.X, p.Position.Z)),
                    b.Placements.Select(p => (p.SpeciesId, p.Position.X, p.Position.Z)));
                Assert.Equal(before, record.ToString());
                Assert.False(request.ResolveShade);
                Assert.False(request.ResolveCompetition);
            }
        }
        [Fact]
        public void ShippedPresetsUseTerrainHeightAndAvoidWaterOnThirtyPercentGrade()
        {
            var catalog = Catalog();
            foreach (var preset in catalog["sets"])
            {
                var request = DistrictGenerationAdapter.Create(District(preset["set"]), (JObject)catalog["cityDefaults"]);
                request.MaxPlacements = VegetationSpawnPolicy.MaxOwnedPlants;
                var result = new FoliageGenerationCoordinator(request,
                    p => new GroundSample(p.X * .3, Math.Atan(.3) * 180 / Math.PI, isWater: p.Z < 30)).Generate(Scatter());
                Assert.NotEmpty(result.Placements);
                Assert.All(result.Placements, p => {
                    Assert.Equal(p.Position.X * .3, p.ElevationM, 8);
                    Assert.True(p.Position.Z >= 30);
                    Assert.Contains(p.AgeId, request.Species.Single(s => s.Id == p.SpeciesId).Ages.Select(a => a.Id));
                });
            }
        }
        [Fact]
        public void DisabledMixProducesEmptyReplacementPlan()
        {
            var catalog = Catalog();
            var record = District(catalog["sets"][0]["set"]);
            foreach (var asset in record["working"]["assets"]) asset["densityPercent"] = 0;
            var request = DistrictGenerationAdapter.Create(record, (JObject)catalog["cityDefaults"]);
            Assert.Empty(new FoliageGenerationCoordinator(request, p => new GroundSample(0)).Generate(Scatter()).Placements);
        }
        [Fact]
        public void ClimateInheritsCityOrGameBaselineWithPerSetOverrides()
        {
            var catalog = Catalog(); var city = (JObject)catalog["cityDefaults"];
            var record = District(catalog["sets"][0]["set"]);
            city["source"] = "game";
            Assert.Equal(15, DistrictGenerationAdapter.Create(record, city).Climate.ReferenceTemperatureC);
            Assert.Equal(8, DistrictGenerationAdapter.Create(record, city, 8).Climate.ReferenceTemperatureC);
            record["working"]["climate"] = JObject.Parse("{referenceTemperatureC:12,referenceElevationM:500,coolingCPerKm:5}");
            var climate = DistrictGenerationAdapter.Create(record, city, 8).Climate;
            Assert.Equal(12, climate.ReferenceTemperatureC);
            Assert.Equal(9.5, climate.TemperatureAt(1000));
        }
        [Fact]
        public void NzOverridesAreLocalAndSpacingRetainsMatureRadius()
        {
            var catalog = Catalog();
            var nz = catalog["sets"].Single(s => (string)s["id"] == "builtin:nz-regenerating-bush");
            var record = District(nz["set"]);
            var request = DistrictGenerationAdapter.Create(record, (JObject)catalog["cityDefaults"]);
            var shrub = request.Species.Single(p => p.Id == "cs2:GreenBushWild01");
            Assert.Equal(12, shrub.Temperature.Optimum);
            Assert.Equal(60, shrub.Terrain.MaximumSlopeDegrees);
            Assert.Equal(55, shrub.DensityPercent);
            Assert.Equal((double)record["working"]["assets"][0]["profile"]["canopyRadiusM"], shrub.PreferredSpacingRadiusM);
            Assert.Equal(45, request.Species.Single(p => p.Id == "cs2:NA_LindenTree01").Terrain.MaximumSlopeDegrees);
            var coordinator = new FoliageGenerationCoordinator(request, p => new GroundSample(0, 50));
            var result = coordinator.Generate(new ProtonScatterSettings());
            Assert.NotEmpty(result.Placements);
            Assert.DoesNotContain(result.Placements, p => p.SpeciesId == "cs2:NA_LindenTree01");
            record["working"]["assets"][0]["profile"]["temperature"]["optimum"] = 13;
            Assert.Equal(12, shrub.Temperature.Optimum);
        }
        [Fact]
        public void UnsupportedClumpingAndInvalidAgesFailBeforePlanning()
        {
            var catalog = Catalog(); var city = (JObject)catalog["cityDefaults"];
            var record = District(catalog["sets"][0]["set"]);
            record["working"]["clumpingPercent"] = 10;
            Assert.Throws<ArgumentException>(() => DistrictGenerationAdapter.Create(record, city));
            record["working"]["clumpingPercent"] = 0;
            record["working"]["assets"][0]["ages"][0]["proportionPercent"] = 0;
            Assert.Throws<ArgumentException>(() => DistrictGenerationAdapter.Create(record, city));
        }

        [Theory]
        [InlineData(1, 0)]
        [InlineData(2, 0)]
        [InlineData(1, 1)]
        [InlineData(2, 1)]
        [InlineData(1, -1)]
        public void DensityScalesCrownPlusGapSpacing(double multiplier, double gap)
        {
            var catalog = Catalog();
            var preset = catalog["sets"].Single(s => (string)s["id"] == "builtin:nz-regenerating-bush");
            var record = District(preset["set"]);
            record["working"]["overallDensityPercent"] = multiplier * 100;
            foreach (var asset in record["working"]["assets"]) asset["profile"]["spacingAdjustmentM"] = gap;
            var request = DistrictGenerationAdapter.Create(record, (JObject)catalog["cityDefaults"]);
            foreach (var profile in request.Species)
                Assert.Equal(profile.CrownRadiusM + gap / 2, profile.PreferredSpacingRadiusM);
            var plan = new FoliageGenerationCoordinator(request, p => new GroundSample(0)).Generate(Scatter()).Placements;
            Assert.NotEmpty(plan);
            var radii = request.Species.ToDictionary(s => s.Id, s => s.PreferredSpacingRadiusM.Value);
            for (int i = 0; i < plan.Count; i++)
                for (int j = 0; j < i; j++)
                {
                    double dx = plan[i].Position.X - plan[j].Position.X, dz = plan[i].Position.Z - plan[j].Position.Z;
                    double required = (radii[plan[i].SpeciesId] + radii[plan[j].SpeciesId]) / Math.Sqrt(multiplier);
                    Assert.True(dx * dx + dz * dz >= required * required - 1e-8);
                }
            record["working"]["overallDensityPercent"] = 0;
            request = DistrictGenerationAdapter.Create(record, (JObject)catalog["cityDefaults"]);
            Assert.Empty(new FoliageGenerationCoordinator(request, p => new GroundSample(0)).Generate(Scatter()).Placements);
        }

        [Fact]
        public void MissingGapDefaultsToZeroAndNegativeGapCannotProduceNonpositiveSpacing()
        {
            var catalog = Catalog(); var record = District(catalog["sets"][0]["set"]);
            var profile = (JObject)record["working"]["assets"][0]["profile"];
            profile.Remove("spacingAdjustmentM");
            var request = DistrictGenerationAdapter.Create(record, (JObject)catalog["cityDefaults"]);
            Assert.Equal((double)profile["canopyRadiusM"], request.Species[0].PreferredSpacingRadiusM);
            profile["canopyRadiusM"] = .2; profile["spacingAdjustmentM"] = -1;
            Assert.Equal(.05, DistrictGenerationAdapter.Create(record, (JObject)catalog["cityDefaults"]).Species[0].PreferredSpacingRadiusM);
            profile["spacingAdjustmentM"] = 1.1;
            Assert.Throws<ArgumentException>(() => DistrictGenerationAdapter.Create(record, (JObject)catalog["cityDefaults"]));
            profile["spacingAdjustmentM"] = 0; record["working"]["overallDensityPercent"] = 201;
            Assert.Throws<ArgumentException>(() => DistrictGenerationAdapter.Create(record, (JObject)catalog["cityDefaults"]));
        }
    }
}
