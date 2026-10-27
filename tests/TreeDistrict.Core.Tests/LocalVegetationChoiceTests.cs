using System.Text.Json;
using TreeDistrict.Core.Foliage;

namespace TreeDistrict.Core.Tests;

public class LocalVegetationChoiceTests
{
    private static GenerationRequest Request() => new() {
        Seed = 42,
        Boundary = new[] { new Position2(0,0), new Position2(160,0), new Position2(160,100), new Position2(0,100) },
        Species = new[] {
            new SpeciesProfile { Id = "bush", CandidatesPerHectare = 100, PreferredSpacingRadiusM = 2,
                Slope = new SuitabilityCurve { Minimum = -1, Optimum = 1, Maximum = 91 } },
            new SpeciesProfile { Id = "tree", CandidatesPerHectare = 100, PreferredSpacingRadiusM = 5,
                Slope = new SuitabilityCurve { Minimum = -1, Optimum = 1, Maximum = 41 } }
        }, ResolveShade = false };
    private static GenerationResult Run(GenerationRequest request, Func<Position2, GroundSample> terrain) =>
        new ProtonFoliageGenerator().Generate(request, terrain, new ProtonScatterSettings { EnableLocalAbundance = true });

    [Fact]
    public void DefaultSettingsUseCompositionWithoutAbundance()
    {
        var settings = new ProtonScatterSettings();
        Assert.True(settings.EnableLocalComposition);
        Assert.False(settings.EnableLocalAbundance);
        var generator = new ProtonFoliageGenerator();
        var request = Request();
        var defaults = generator.Generate(request, _ => new GroundSample(0,20), settings);
        var explicitMode = generator.Generate(request, _ => new GroundSample(0,20),
            new ProtonScatterSettings { EnableLocalComposition=true, EnableLocalAbundance=false });
        Assert.Equal(JsonSerializer.Serialize(explicitMode), JsonSerializer.Serialize(defaults));
    }

    [Fact]
    public void UnsuitableTreesNeverReserveSpaceAndBushesUseMoreOfTheSlope()
    {
        var r = Request();
        // Bush retains full suitability; tree cannot grow on 60-degree ground.
        r.Species[0].Slope = null;
        var flat = Run(r, _ => new GroundSample(0));
        var steep = Run(r, _ => new GroundSample(0,60));
        Assert.DoesNotContain(steep.Placements, p => p.SpeciesId == "tree");
        Assert.True(steep.Placements.Count > flat.Placements.Count(p => p.SpeciesId == "bush"));
        Assert.Equal(0, steep.EmptyEnvironmentSamples); // abundance changes spacing, not rejection probability
        Assert.All(steep.Counts, c => Assert.Equal(0,c.EnvironmentRejected));
        Assert.True(steep.SamplingOpportunities > 0);
        Assert.Equal(steep.SamplingOpportunities, steep.EmptyEnvironmentSamples + steep.SpacingRejectedSamples + steep.Counts.Sum(c => c.Candidates));
    }

    [Fact]
    public void UniformSuitabilityControlsAbundanceAndIsNotAppliedTwice()
    {
        var r = Request(); r.Species = new[] { r.Species[0] };
        var full = Run(r, _ => new GroundSample(0,1));
        var half = Run(r, _ => new GroundSample(0,46));
        // Finite boundaries mean the final count is approximate, not an exact quota.
        Assert.Equal(0, half.EmptyEnvironmentSamples);
        Assert.InRange((double)half.Placements.Count / full.Placements.Count, .4, .65);
        Assert.True(half.Placements.Count < full.Placements.Count);
        Assert.Equal(0,half.Counts[0].EnvironmentRejected);
        var empty = Run(r, _ => new GroundSample(0,90, temperatureC: double.NaN));
        Assert.Empty(empty.Placements);
        Assert.Equal(empty.SamplingOpportunities,empty.EmptyEnvironmentSamples);
    }

    [Fact]
    public void ExplorationCrossesCompletelyBarrenStrips()
    {
        var r = Request();
        var result = Run(r, p => new GroundSample(0, p.X > 50 && p.X < 110 ? 90 : 0, isBlocked: p.X > 50 && p.X < 110));
        Assert.Contains(result.Placements,p => p.Position.X < 50);
        Assert.Contains(result.Placements,p => p.Position.X > 110);
        Assert.DoesNotContain(result.Placements,p => p.Position.X > 50 && p.Position.X < 110);
    }

    [Fact]
    public void LocalChoiceIsDeterministicAndRespectsMixedSpacing()
    {
        var r = Request(); var result = Run(r,_ => new GroundSample(0,20));
        Assert.Equal(JsonSerializer.Serialize(result),JsonSerializer.Serialize(Run(r,_ => new GroundSample(0,20))));
        r.Species = r.Species.Reverse().ToArray();
        Assert.Equal(JsonSerializer.Serialize(result),JsonSerializer.Serialize(Run(r,_ => new GroundSample(0,20))));
        var radii = r.Species.ToDictionary(p => p.Id,p => p.PreferredSpacingRadiusM!.Value);
        for (int i=0;i<result.Placements.Count;i++) for(int j=i+1;j<result.Placements.Count;j++)
        {
            var a=result.Placements[i]; var b=result.Placements[j];
            Assert.True(Math.Pow(a.Position.X-b.Position.X,2)+Math.Pow(a.Position.Z-b.Position.Z,2)
                >= Math.Pow(radii[a.SpeciesId]+radii[b.SpeciesId],2)-1e-9);
        }
    }

    [Fact]
    public void HardConstraintsAndWorkBudgetsAreAppliedBeforeReservations()
    {
        var r=Request(); r.ExistingPlants = new[] { new ExistingPlant { Id="block", Position=new Position2(80,50),ExclusionRadiusM=1000 } };
        Assert.Empty(Run(r,_=>new GroundSample(0)).Placements);
        r=Request(); r.MaxNeighbourChecks=1;
        Assert.Throws<InvalidOperationException>(()=>Run(r,_=>new GroundSample(0)));
        r=Request(); r.MaxCandidates=1;
        Assert.Throws<InvalidOperationException>(()=>Run(r,_=>new GroundSample(0)));
        using var cancelled=new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(()=>new ProtonFoliageGenerator().Generate(Request(),_=>new GroundSample(0),new ProtonScatterSettings(),cancelled.Token));
    }
    [Fact]
    public void AnImpossibleTreesRadiusCannotLeaveAGhostReservation()
    {
        var request=Request(); request.Species[0].Slope=null;
        var before=Run(request,_=>new GroundSample(0,60));
        request.Species[1].PreferredSpacingRadiusM=50;
        var after=Run(request,_=>new GroundSample(0,60));
        Assert.Equal(JsonSerializer.Serialize(before.Placements),JsonSerializer.Serialize(after.Placements));
    }

    [Fact]
    public void CachedEnvironmentDrivesChoiceAndPostFilterRemainsAvailable()
    {
        var request=Request(); request.Species[0].Slope=null;
        var field=LocalEnvironmentGrid.Build(request.Boundary,_=>new LocalEnvironmentSample(0,60,18));
        var generator=new ProtonFoliageGenerator();
        var pre=generator.Generate(request,_=>new GroundSample(0),new ProtonScatterSettings(),environment:field);
        Assert.DoesNotContain(pre.Placements,p=>p.SpeciesId=="tree");
        Assert.All(pre.Counts,c=>Assert.Equal(0,c.EnvironmentRejected));
        var post=generator.Generate(request,_=>new GroundSample(0),
            new ProtonScatterSettings { UsePrePlacementSuitability=false },environment:field);
        Assert.True(post.Counts.Single(c=>c.SpeciesId=="tree").EnvironmentRejected>0);
        Assert.NotEqual(pre.AlgorithmVersion,post.AlgorithmVersion);
    }

    [Fact]
    public void FullySuitableGroundPreservesOriginalActiveFrontierLayoutExactly()
    {
        var request = Request();
        var generator = new ProtonFoliageGenerator();
        var restored = generator.Generate(request, _ => new GroundSample(0), new ProtonScatterSettings());
        var original = generator.Generate(request, _ => new GroundSample(0),
            new ProtonScatterSettings { UsePrePlacementSuitability = false });
        Assert.Equal(JsonSerializer.Serialize(original.Placements), JsonSerializer.Serialize(restored.Placements));
        Assert.Equal(original.PositionAttempts,restored.PositionAttempts);
    }

    [Fact]
    public void CompositionAndAbundanceCanBeDisabledIndependently()
    {
        var request = Request(); request.Species[0].Slope = null;
        var generator = new ProtonFoliageGenerator();
        GenerationResult Mode(bool composition, bool abundance) => generator.Generate(request,
            _ => new GroundSample(0,60), new ProtonScatterSettings {
                EnableLocalComposition = composition, EnableLocalAbundance = abundance });
        var both = Mode(true,true); var compositionOnly = Mode(true,false);
        var abundanceOnly = Mode(false,true); var neither = Mode(false,false);
        Assert.DoesNotContain(both.Placements, p => p.SpeciesId == "tree");
        Assert.DoesNotContain(compositionOnly.Placements, p => p.SpeciesId == "tree");
        Assert.Contains(abundanceOnly.Placements, p => p.SpeciesId == "tree");
        Assert.Contains(neither.Placements, p => p.SpeciesId == "tree");
        Assert.True(both.Placements.Count < compositionOnly.Placements.Count);
        Assert.True(abundanceOnly.Placements.Count < neither.Placements.Count);
        var flat = generator.Generate(request, _ => new GroundSample(0), new ProtonScatterSettings());
        Assert.Equal(JsonSerializer.Serialize(flat.Placements), JsonSerializer.Serialize(neither.Placements));
    }

    [Fact]
    public void DisablingPoliciesDoesNotDisableHardTerrainExclusions()
    {
        var result = new ProtonFoliageGenerator().Generate(Request(), _ => new GroundSample(0,isBlocked:true),
            new ProtonScatterSettings { EnableLocalComposition=false, EnableLocalAbundance=false });
        Assert.Empty(result.Placements);
    }

    [Fact]
    public void SpatialAbundanceUsesBothEndpointsRadiiIncludingOverallDensity()
    {
        var request = Request(); request.OverallDensityPercent = 50;
        double Slope(Position2 p) => p.X / 160 * 80;
        var result = Run(request,p => new GroundSample(0,Slope(p)));
        var profiles = request.Species.ToDictionary(p => p.Id);
        double Radius(Placement p)
        {
            double slope = Slope(p.Position);
            double abundance = request.Species.Average(s => s.Slope!.Evaluate(slope,true));
            return profiles[p.SpeciesId].PreferredSpacingRadiusM!.Value / Math.Sqrt(.5 * abundance);
        }
        Assert.NotEmpty(result.Placements);
        for (int i=0;i<result.Placements.Count;i++) for(int j=i+1;j<result.Placements.Count;j++)
        {
            var a=result.Placements[i]; var b=result.Placements[j];
            Assert.True(Math.Pow(a.Position.X-b.Position.X,2)+Math.Pow(a.Position.Z-b.Position.Z,2)
                >= Math.Pow(Radius(a)+Radius(b),2)-1e-9);
        }
    }

    [Fact]
    public void NearZeroAbundanceRemainsBoundedAndZeroIsBarren()
    {
        var request=Request(); request.Species=new[] {request.Species[0]};
        request.Species[0].Slope!.Maximum = 90;
        var sparse=Run(request,_=>new GroundSample(0,89.99999999));
        Assert.Single(sparse.Placements);
        Assert.Empty(Run(request,_=>new GroundSample(0,90)).Placements);
    }

}
