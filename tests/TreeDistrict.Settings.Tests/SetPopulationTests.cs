using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace TreeDistrict.Tests
{
    // Population envelopes, not golden snapshots of a particular random layout.
    public class SetPopulationTests
    {
        private readonly ITestOutputHelper output;
        public SetPopulationTests(ITestOutputHelper output) { this.output = output; }
        private static JObject Read(string file) => JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, file)));
        private static JObject Fixtures => Read("set-population.v1.json");
        private static readonly int[] Seeds = { 1, 7, 19, 42, 101, 503, 1729, 8191 };

        public static IEnumerable<object[]> CurrentScenarios() => Fixtures["scenarios"]
            .Where(s => (string)s["status"] == "currentSingle").Select(s => new object[] { (string)s["id"] });

        [Theory]
        [MemberData(nameof(CurrentScenarios))]
        public void SingleSpeciesPopulationFallsWithinIndependentEnvelope(string scenarioId)
        {
            var scenario = Fixtures["scenarios"].Single(s => (string)s["id"] == scenarioId);
            var expected = scenario["expected"]["maple"];
            var counts = new List<int>();
            foreach (int seed in Seeds)
            {
                var catalog = ProfileMigration.Catalog(Read("catalog.json"), Read("london-plane.json"));
                var set = (JObject)catalog["sets"][0]["set"];
                var profile = set["assets"][0]["profile"];
                // Controlled environment: isolate density rather than accidentally testing the default climate.
                set["overallDensityPercent"] = scenario["overallDensityPercent"].DeepClone();
                set["clumpingPercent"] = 0;
                set["assets"][0]["densityPercent"] = scenario["speciesDensityPercent"]["maple"].DeepClone();
                profile["matureSpacingM"] = Fixtures["profiles"]["maple"]["spacingM"].DeepClone();
                double score = (double)scenario["uniformSuitability"];
                set["climate"]["referenceTemperatureC"] = (double)profile["temperature"]["min"]
                    + score * ((double)profile["temperature"]["optimum"] - (double)profile["temperature"]["min"]);
                set["climate"]["referenceElevationM"] = 0;
                set["climate"]["coolingCPerKm"] = 0;
                double side = Math.Sqrt((double)scenario["areaM2"]);
                var boundary = new[] { new PlacementPlanner.Point(0, 0), new PlacementPlanner.Point(side, 0),
                    new PlacementPlanner.Point(side, side), new PlacementPlanner.Point(0, side) };
                {
                    var plan = PlacementPlanner.Plan(boundary, set, (JObject)catalog["cityDefaults"], seed,
                        (x, z) => new PlacementPlanner.Ground { Height = x < side * (double)scenario["eligibleAreaFraction"] ? 0 : 10000, Slope = 0 });
                    counts.Add(plan.Count);
                    Assert.InRange(plan.Count, (int)expected["minimum"], Math.Min((int)expected["maximum"], PlacementPlanner.MaxTrees));
                    foreach (var age in set["assets"][0]["ages"])
                        Assert.InRange(Math.Abs(plan.Count(p => p.Age == (string)age["id"])
                            - plan.Count * (double)age["proportionPercent"] / 100), 0, 1);
                }
            }
            output.WriteLine($"{scenarioId}: counts=[{string.Join(", ", counts)}], expected mean={expected["mean"]}");
            {
                Assert.Equal(Seeds.Length, counts.Count);
                double mean = (double)expected["mean"];
                double tolerance = Math.Max(mean * 0.03, 4 * Math.Sqrt(mean / Seeds.Length));
                Assert.InRange(counts.Average(), Math.Max(0, mean - tolerance), mean + tolerance);
            }
        }

        [Fact]
        public void MixedSpeciesIsExplicitlyUnsupportedRatherThanSilentlyDropped()
        {
            var catalog = ProfileMigration.Catalog(Read("catalog.json"), Read("london-plane.json"));
            var set = (JObject)catalog["sets"][0]["set"];
            var birch = set["assets"][0].DeepClone();
            birch["id"] = "cs2:BirchTree01";
            ((JArray)set["assets"]).Add(birch);
            var boundary = new[] { new PlacementPlanner.Point(0, 0), new PlacementPlanner.Point(1000, 0), new PlacementPlanner.Point(1000, 1000) };
            Assert.Throws<ArgumentException>(() => PlacementPlanner.Plan(boundary, set, (JObject)catalog["cityDefaults"], 1,
                (x, z) => throw new InvalidOperationException("Unsupported sets must fail before terrain sampling.")));
        }

        [Fact(Skip = "Future mixed generator: expose candidate counts by asset ID, then run candidateBudget fixtures before competition. See docs/foliage-reference-design.md population contracts.")]
        public void MixedCandidateBudgetsMatchIndependentSpeciesRequests() => throw new NotImplementedException();

        [Fact(Skip = "Future mixed generator: add a test adapter that disables interactions, then run mixed uncontestedPlacement fixtures across Seeds. Final plans must report asset IDs.")]
        public void MixedUncontestedPopulationsMatchPerSpeciesEnvelopes() => throw new NotImplementedException();

        [Fact(Skip = "Known gap: all species at zero density currently throws. Contract expects an empty plan, including when other species are added at zero.")]
        public void AllDisabledSpeciesProducesAnEmptyPlan() => throw new NotImplementedException();
    }
}
