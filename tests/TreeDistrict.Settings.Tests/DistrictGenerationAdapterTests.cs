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
                var a = coordinator.Generate(new ProtonScatterSettings());
                var b = coordinator.Generate(new ProtonScatterSettings());
                Assert.NotEmpty(a.Placements);
                Assert.Equal(a.Placements.Select(p => (p.SpeciesId, p.Position.X, p.Position.Z)),
                    b.Placements.Select(p => (p.SpeciesId, p.Position.X, p.Position.Z)));
                Assert.Equal(before, record.ToString());
                Assert.False(request.ResolveShade);
            }
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
            Assert.Equal((double)record["working"]["assets"][0]["profile"]["matureSpacingM"] / 2, shrub.PreferredSpacingRadiusM);
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
    }
}
