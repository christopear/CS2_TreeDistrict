using TreeDistrict.Core.Foliage;

namespace TreeDistrict.Core.Tests
{
    public class StampFoliageGeneratorTests
    {
        private static GenerationRequest Request(double min = -200, double max = 400) => new() {
            Seed = 42, ResolveCompetition = false, ResolveShade = false,
            Boundary = new[] { new Position2(min,min), new Position2(max,min), new Position2(max,max), new Position2(min,max) },
            Species = new[] { new SpeciesProfile { Id="shrub", CandidatesPerHectare=100, PreferredSpacingRadiusM=3,
                CrownRadiusM=2, MatureHeightM=3, Terrain=new TerrainRules(-100,1000,30),
                Ages=new[] {new AgeWeight{Id="young",Weight=1},new AgeWeight{Id="mature",Weight=3}} } }
        };
        private static object Identity(Placement p) => (p.SpeciesId,p.Position.X,p.Position.Z,p.AgeId,p.RotationRadians);
        [Fact]
        public void InsetsAndWorldAnchoredTilesSurviveBoundaryExpansionAndReuseLibrary()
        {
            var generator=new StampFoliageGenerator();var settings=new ProtonScatterSettings();
            var request=Request(0,200);
            var initial=generator.Generate(request,p=>new GroundSample(p.X*.2),settings,"set");
            Assert.False(generator.LastCacheHit);Assert.Equal(1,generator.LibraryBuildCount);
            Assert.NotEmpty(initial.Placements);
            var expanded=generator.Generate(Request(),p=>new GroundSample(p.X*.2),settings,"set");
            Assert.True(generator.LastCacheHit);Assert.Equal(1,generator.LibraryBuildCount);
            var sameTile=expanded.Placements.Where(p=>p.Position.X>=0&&p.Position.X<200&&p.Position.Z>=0&&p.Position.Z<200);
            Assert.Equal(initial.Placements.Select(Identity),sameTile.Select(Identity));
            foreach(var p in expanded.Placements)
            {
                Assert.InRange(p.Position.X-Math.Floor(p.Position.X/200)*200,1,199);
                Assert.InRange(p.Position.Z-Math.Floor(p.Position.Z/200)*200,1,199);
                Assert.Equal(p.Position.X*.2,p.ElevationM,8);
                Assert.InRange(p.RotationRadians,0,2*Math.PI);
            }
            generator.Generate(request,p=>new GroundSample(0),settings,"changed-set");
            Assert.False(generator.LastCacheHit);Assert.Equal(2,generator.LibraryBuildCount);
        }
        [Fact]
        public void ClipsConcaveBoundaryAndRechecksTerrainOnCacheHit()
        {
            var generator=new StampFoliageGenerator();var request=Request(0,400);var settings=new ProtonScatterSettings();
            request.Boundary=new[]{new Position2(0,0),new Position2(400,0),new Position2(400,100),
                new Position2(100,100),new Position2(100,400),new Position2(0,400)};
            var result=generator.Generate(request,p=>new GroundSample(0),settings,"set");
            Assert.NotEmpty(result.Placements);
            Assert.All(result.Placements,p=>Assert.True(p.Position.X<=100||p.Position.Z<=100));
            Assert.Empty(generator.Generate(request,p=>new GroundSample(0,31),settings,"set").Placements);
            Assert.True(generator.LastCacheHit);
            Assert.Empty(generator.Generate(request,p=>new GroundSample(0,isWater:true),settings,"set").Placements);
            request.Terrain=new TerrainRules(-100,1000,10);
            Assert.Empty(generator.Generate(request,p=>new GroundSample(0,11),settings,"stricter-set").Placements);
        }
        [Fact]
        public void ChecksAltitudeTemperatureOnActualGroundAndHonoursPopulationLimit()
        {
            var generator=new StampFoliageGenerator();var request=Request(0,200);var settings=new ProtonScatterSettings();
            request.Species[0].Temperature=new SuitabilityCurve{Minimum=5,Optimum=15,Maximum=25};
            request.Climate=new ClimateProfile(15,0,20);
            Assert.NotEmpty(generator.Generate(request,p=>new GroundSample(0),settings,"set").Placements);
            Assert.Empty(generator.Generate(request,p=>new GroundSample(600),settings,"set").Placements);
            request.MaxPlacements=1;
            Assert.Throws<InvalidOperationException>(()=>generator.Generate(request,p=>new GroundSample(0),settings,"set"));
            request.OverallDensityPercent=0;
            Assert.Empty(generator.Generate(request,p=>new GroundSample(0),settings,"zero").Placements);
        }
    }
}
