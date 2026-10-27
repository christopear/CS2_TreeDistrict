using System.Text.Json;
using TreeDistrict.Core.Foliage;
using Xunit.Abstractions;

namespace TreeDistrict.Core.Tests
{
    public sealed class FoliageTerrainTests
    {
        private readonly ITestOutputHelper output;
        public FoliageTerrainTests(ITestOutputHelper output) { this.output = output; }
        private static GenerationRequest Request(double side = 200) => new GenerationRequest
        {
            Boundary = new[] { new Position2(0, 0), new Position2(side, 0), new Position2(side, side), new Position2(0, side) },
            Species = new[] { new SpeciesProfile { Id = "maple", CandidatesPerHectare = 100, MatureHeightM = 10,
                ExclusionRadiusM = 1.5, CrownRadiusM = 4, CrownOpacity = .6,
                Terrain = new TerrainRules(-1000, 4000, 30),
                Slope = new SuitabilityCurve { Minimum = 0, Optimum = 20, Maximum = 45 } } },
            Climate = new ClimateProfile(18, 0, 6.5)
        };

        [Theory]
        [InlineData(0)]
        [InlineData(20)]
        [InlineData(30)]
        [InlineData(-30)]
        public void GentleGradeKeepsSuitableTreesAndGroundsEveryPlacement(double gradePercent)
        {
            var generator = new FoliageGenerator();
            var request = Request();
            double slope = Math.Atan(Math.Abs(gradePercent) / 100) * 180 / Math.PI;
            var flat = generator.Generate(request, _ => new GroundSample(0));
            var hill = generator.Generate(request, p => new GroundSample(p.X * gradePercent / 100, slope));
            Assert.NotEmpty(hill.Placements);
            // Mature same-species crowns don't suddenly shade one another on gentle grades.
            Assert.Equal(flat.Placements.Select(p => (p.CandidateIndex, p.Position.X, p.Position.Z)),
                hill.Placements.Select(p => (p.CandidateIndex, p.Position.X, p.Position.Z)));
            Assert.All(hill.Placements, p => Assert.Equal(p.Position.X * gradePercent / 100, p.ElevationM, 9));
            Assert.All(hill.Counts, c => Assert.Equal(0, c.TerrainRejected + c.EnvironmentRejected));
        }

        [Theory]
        [InlineData(20)]
        [InlineData(30)]
        public void MixedForestRemainsPopulatedOnSlopes(double gradePercent)
        {
            var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "foliage-model.v1.json")));
            var profiles = JsonSerializer.Deserialize<SpeciesProfile[]>(document.RootElement.GetProperty("profiles").GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            double slope = Math.Atan(gradePercent / 100) * 180 / Math.PI;
            var request = Request(); request.Species = profiles;
            var flat = new List<int>(); var hills = new List<int>();
            var generator = new FoliageGenerator();
            for (ulong seed = 1; seed <= 32; seed++)
            {
                request.Seed = seed;
                flat.Add(generator.Generate(request, _ => new GroundSample(0)).Placements.Count);
                var result = generator.Generate(request, p => new GroundSample(100 + p.X * gradePercent / 100, slope));
                hills.Add(result.Placements.Count);
                Assert.All(result.Placements, p => Assert.Equal(100 + p.Position.X * gradePercent / 100, p.ElevationM, 9));
                Assert.All(result.Counts, c => Assert.True(c.Accepted > 0));
            }
            output.WriteLine($"{gradePercent}% grade ({slope:F2} degrees): flat mean={flat.Average():F1}, slope mean={hills.Average():F1}");
            Assert.InRange(hills.Average(), flat.Average() * .9, flat.Average() * 1.1);
        }

        [Fact]
        public void AltitudeCoolingMatchesHandCalculatedGradientAcrossOneKilometre()
        {
            // 30% grade over 1000 m rises 300 m: 6.5 C/km gives 1.95 C cooling.
            var request = Request(1000); request.ResolveCompetition = false; request.ResolveShade = false;
            request.Species[0].CandidatesPerHectare = 10;
            request.Species[0].Temperature = new SuitabilityCurve { Minimum = 10, Optimum = 20, Maximum = 30 };
            request.Climate = new ClimateProfile(20, 0, 6.5);
            Assert.Equal(18.05, request.Climate.TemperatureAt(300), 8);
            // Thermal acceptance declines linearly from 1 to .805: spatial mean .9025.
            var counts = new List<int>();
            for (ulong seed = 1; seed <= 64; seed++)
            {
                request.Seed = seed;
                var result = new FoliageGenerator().Generate(request,
                    p => new GroundSample(p.X * .3, Math.Atan(.3) * 180 / Math.PI, temperatureC: -999));
                // Climate profile explicitly overrides the callback's temperature.
                counts.Add(result.Placements.Count);
                Assert.Equal(1000, result.Counts[0].Candidates);
                Assert.Equal(0, result.Counts[0].TerrainRejected);
                Assert.Equal(1000 - result.Placements.Count, result.Counts[0].EnvironmentRejected);
                Assert.All(result.Placements, p => Assert.Equal(p.Position.X * .3, p.ElevationM, 8));
            }
            output.WriteLine($"Altitude/temperature gradient: expected=902.5, observed={counts.Average():F2}");
            Assert.InRange(counts.Average(), 890, 915);
        }

        [Fact]
        public void ClimateReferenceAndTerrainTranslationAreConsistent()
        {
            var request = Request();
            request.Species[0].Temperature = new SuitabilityCurve { Minimum = 0, Optimum = 18, Maximum = 35 };
            var generator = new FoliageGenerator();
            var baseline = generator.Generate(request, p => new GroundSample(p.X * .2, Math.Atan(.2) * 180 / Math.PI));
            request.Climate = new ClimateProfile(18, 1000, 6.5);
            var raised = generator.Generate(request, p => new GroundSample(1000 + p.X * .2, Math.Atan(.2) * 180 / Math.PI));
            Assert.Equal(baseline.Placements.Select(p => p.CandidateIndex), raised.Placements.Select(p => p.CandidateIndex));
            Assert.All(raised.Placements, p => Assert.InRange(p.ElevationM, 1000, 1040));
            Assert.Equal(18, request.Climate.TemperatureAt(1000));
            Assert.Equal(24.5, request.Climate.TemperatureAt(0));
            Assert.Equal(18, new ClimateProfile(18, 0, 0).TemperatureAt(2000));
            Assert.Throws<ArgumentException>(() => new ClimateProfile(coolingCPerKm: -1));
            Assert.Throws<ArgumentException>(() => new ClimateProfile().TemperatureAt(double.NaN));
        }

        [Fact]
        public void ExplicitSlopeLimitRejectsSteepTerrainRatherThanCrashing()
        {
            var request = Request();
            request.Terrain = new TerrainRules(-1000, 10000, 15);
            double slope = Math.Atan(.3) * 180 / Math.PI; // 16.7 degrees: intentionally exceeds this limit.
            var result = new FoliageGenerator().Generate(request, p => new GroundSample(p.X * .3, slope));
            Assert.Empty(result.Placements);
            Assert.Equal(400, result.Counts[0].TerrainRejected);
        }
    }
}
