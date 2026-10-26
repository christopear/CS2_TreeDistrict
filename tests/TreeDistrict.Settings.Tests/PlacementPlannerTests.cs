using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TreeDistrict.Tests
{
    public class PlacementPlannerTests
    {
        private static JObject Read(string name) => SettingsModel.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, name)));
        private static JObject Catalog() => ProfileMigration.Catalog(Read("catalog.json"), Read("london-plane.json"));
        private static readonly PlacementPlanner.Point[] Boundary = {
            new PlacementPlanner.Point(0, 0), new PlacementPlanner.Point(400, 0), new PlacementPlanner.Point(400, 100),
            new PlacementPlanner.Point(100, 100), new PlacementPlanner.Point(100, 400), new PlacementPlanner.Point(0, 400)
        };
        private static PlacementPlanner.Ground Flat(double x, double z) => new PlacementPlanner.Ground { Height = 0, Slope = 0 };
        [Fact]
        public void ConcaveAreaContainsEveryPlacementAndMatureSpacingIsRespected()
        {
            var catalog = Catalog();
            var plan = PlacementPlanner.Plan(Boundary, (JObject)catalog["sets"][0]["set"], (JObject)catalog["cityDefaults"], 1, Flat);
            Assert.NotEmpty(plan);
            foreach (var p in plan) Assert.True(PlacementPlanner.Contains(Boundary, p.X, p.Z));
            for (int i = 0; i < plan.Count; i++) for (int j = i + 1; j < plan.Count; j++)
                Assert.True(Math.Pow(plan[i].X - plan[j].X, 2) + Math.Pow(plan[i].Z - plan[j].Z, 2) >= 400);
        }
        [Fact]
        public void AgesHaveCorrectProportionsAndDoNotChangePositionsOrCount()
        {
            var catalog = Catalog(); var set = (JObject)catalog["sets"][0]["set"];
            var a = PlacementPlanner.Plan(Boundary, set, (JObject)catalog["cityDefaults"], 7, Flat);
            foreach (var age in set["assets"][0]["ages"])
                Assert.InRange(Math.Abs(a.Count(p => p.Age == (string)age["id"]) - a.Count * (double)age["proportionPercent"] / 100), 0, 1);
            foreach (var age in set["assets"][0]["ages"]) age["proportionPercent"] = (string)age["id"] == "young" ? 100 : 0;
            var b = PlacementPlanner.Plan(Boundary, set, (JObject)catalog["cityDefaults"], 7, Flat);
            Assert.Equal(a.Select(p => (p.X, p.Z)), b.Select(p => (p.X, p.Z)));
            Assert.All(b, p => Assert.Equal("young", p.Age));
        }
        [Fact]
        public void InvalidAgeMixAndUnsupportedAssetsFailWithoutMutatingSettings()
        {
            var catalog = Catalog(); var set = (JObject)catalog["sets"][0]["set"];
            set["assets"][0]["ages"][0]["proportionPercent"] = 0;
            string original = set.ToString();
            Assert.Throws<ArgumentException>(() => PlacementPlanner.Plan(Boundary, set, (JObject)catalog["cityDefaults"], 1, Flat));
            Assert.Equal(original, set.ToString());
            set["assets"][0]["id"] = "unavailable";
            Assert.Throws<ArgumentException>(() => PlacementPlanner.Plan(Boundary, set, (JObject)catalog["cityDefaults"], 1, Flat));
        }
        [Fact]
        public void LimitsAndZeroDensityCanProduceAnEmptyReplacement()
        {
            var catalog = Catalog(); var set = (JObject)catalog["sets"][0]["set"];
            Assert.Empty(PlacementPlanner.Plan(Boundary, set, (JObject)catalog["cityDefaults"], 1,
                (x, z) => new PlacementPlanner.Ground { Height = 3000, Slope = 0 }));
            set["overallDensityPercent"] = 0;
            Assert.Empty(PlacementPlanner.Plan(Boundary, set, (JObject)catalog["cityDefaults"], 1, Flat));
        }
        [Fact]
        public void ExcessiveCandidateWorkIsRejectedBeforeSampling()
        {
            var catalog = Catalog(); var set = (JObject)catalog["sets"][0]["set"];
            set["assets"][0]["profile"]["matureSpacingM"] = 1;
            var large = new[] { new PlacementPlanner.Point(0, 0), new PlacementPlanner.Point(10000, 0), new PlacementPlanner.Point(10000, 10000) };
            Assert.Throws<ArgumentException>(() => PlacementPlanner.Plan(large, set, (JObject)catalog["cityDefaults"], 1, (x, z) => throw new Exception("Must not sample")));
        }
        [Fact]
        public void LegacyProfileMigrationUsesMatureSettingsAndNormalizesAges()
        {
            var set = Read("legacy.json")["sets"][0]["set"];
            ProfileMigration.Set(set); SettingsModel.ValidateSet(set);
            foreach (var asset in set["assets"])
            {
                Assert.NotNull(asset["profile"]);
                Assert.InRange(Math.Abs(asset["ages"].Sum(a => (double)a["proportionPercent"]) - 100), 0, 0.001);
                Assert.Null(asset["ages"][0]["temperature"]);
            }
            string once = set.ToString(); ProfileMigration.Set(set); Assert.Equal(once, set.ToString());
        }
    }
}
