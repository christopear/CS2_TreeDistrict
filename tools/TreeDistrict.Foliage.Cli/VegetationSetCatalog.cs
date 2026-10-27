using System.Text.Json;
using TreeDistrict.Core.Foliage;

internal sealed class VegetationSetCatalog
{
    public int SchemaVersion { get; set; }
    public VegetationSet[] Sets { get; set; } = [];
    internal static VegetationSet Load(string path, string id)
    {
        var catalog=JsonSerializer.Deserialize<VegetationSetCatalog>(File.ReadAllText(path),
            new JsonSerializerOptions {PropertyNameCaseInsensitive=true}) ?? throw new ArgumentException("Missing vegetation sets.");
        if (catalog.SchemaVersion!=1) throw new ArgumentException("Unsupported vegetation-set schema.");
        if (catalog.Sets.Select(s=>s.Id).Distinct(StringComparer.Ordinal).Count()!=catalog.Sets.Length)
            throw new ArgumentException("Duplicate vegetation-set ID.");
        return catalog.Sets.SingleOrDefault(s=>s.Id==id)
            ?? throw new ArgumentException("Unknown vegetation set: " + id + ". Available: " + string.Join(", ",catalog.Sets.Select(s=>s.Id)));
    }
}
internal sealed class VegetationSet
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public double OverallDensityPercent { get; set; } = 100;
    public VegetationSetMember[] Members { get; set; } = [];
    internal SpeciesProfile[] Apply(SpeciesProfile[] profiles)
    {
        if (Members.Length==0 || Members.Select(m=>m.AssetId).Distinct(StringComparer.Ordinal).Count()!=Members.Length)
            throw new ArgumentException("Vegetation-set members must be nonempty and unique.");
        if (!double.IsFinite(OverallDensityPercent) || OverallDensityPercent<0 || OverallDensityPercent>100)
            throw new ArgumentException("Invalid set density.");
        var lookup=profiles.ToDictionary(p=>p.Id,StringComparer.Ordinal);
        return Members.Select(member=> {
            if (!double.IsFinite(member.Weight) || member.Weight<=0) throw new ArgumentException("Set weights must be positive and finite.");
            if (!lookup.TryGetValue(member.AssetId,out var source)) throw new ArgumentException("Unknown set asset: " + member.AssetId);
            // Apply weights to a copy: reusing the catalogue for another set must not mutate it.
            var profile=JsonSerializer.Deserialize<SpeciesProfile>(JsonSerializer.Serialize(source))!;
            profile.CandidatesPerHectare=member.Weight;
            return profile;
        }).ToArray();
    }
}
internal sealed class VegetationSetMember
{
    public string AssetId { get; set; } = "";
    public double Weight { get; set; }
}
