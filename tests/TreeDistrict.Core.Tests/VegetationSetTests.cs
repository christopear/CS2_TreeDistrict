using System.Text.Json;
using TreeDistrict.Core.Foliage;

namespace TreeDistrict.Core.Tests;

public class VegetationSetTests
{
    [Theory]
    [InlineData("low-cover",4)]
    [InlineData("european-woodland",8)]
    [InlineData("european-trees",6)]
    [InlineData("american-woodland",5)]
    [InlineData("american-trees",3)]
    public void PresetsResolveAndGenerateWithoutMutatingSourceProfiles(string id,int count)
    {
        string Data(string name)=>Path.Combine(AppContext.BaseDirectory,name);
        var assets=VegetationCatalogAdapter.Load(Data("base-game-vegetation.v1.json"),Data("real-world-vegetation.v1.json"),Data("vegetation-adapter.v1.json"));
        var source=assets.Select(a=>a.Profile).ToArray();
        string before=JsonSerializer.Serialize(source);
        var set=VegetationSetCatalog.Load(Data("vegetation-sets.v1.json"),id);
        var profiles=set.Apply(source);
        Assert.Equal(count,profiles.Length); Assert.Equal(before,JsonSerializer.Serialize(source));
        foreach(var profile in profiles)
        {
            var kind=assets.Single(a=>a.Profile.Id==profile.Id).AssetSource.GetProperty("kind").GetString();
            if(id=="low-cover") Assert.Equal("shrub",kind);
            if(id.EndsWith("-trees")) Assert.Equal("tree",kind);
        }
        var request=new GenerationRequest { Species=profiles,OverallDensityPercent=set.OverallDensityPercent,
            ResolveShade=false,Boundary=new[] {new Position2(0,0),new Position2(80,0),new Position2(80,80),new Position2(0,80)} };
        Assert.NotEmpty(new FoliageGenerationCoordinator(request,_=>new GroundSample(0)).Generate(new ProtonScatterSettings()).Placements);
    }
    [Fact]
    public void TreeWeightsMatchWithAndWithoutBushes()
    {
        string path=Path.Combine(AppContext.BaseDirectory,"vegetation-sets.v1.json");
        foreach(string region in new[]{"european","american"})
        {
            var trees=VegetationSetCatalog.Load(path,region+"-trees");
            var woodland=VegetationSetCatalog.Load(path,region+"-woodland");
            foreach(var tree in trees.Members)
                Assert.Equal(tree.Weight,woodland.Members.Single(m=>m.AssetId==tree.AssetId).Weight);
        }
        Assert.Throws<ArgumentException>(()=>VegetationSetCatalog.Load(path,"missing"));
    }
}
