using System.Text.Json;
using TreeDistrict.Core;
using TreeDistrict.Core.Foliage;

// File-format adapter only. Core receives typed solver inputs, never botanical prose.
internal static class VegetationCatalogAdapter
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    internal sealed class Catalog
    {
        public int SchemaVersion { get; set; }
        public Asset[] Assets { get; set; } = [];
        public Botanical[] Profiles { get; set; } = [];
        public Dictionary<string, KindDefaults> Kinds { get; set; } = [];
    }
    internal sealed class Asset
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = "";
        public PlacementDefaults PlacementDefaults { get; set; } = new();
        public Measurement[] BaseGameMeasurements { get; set; } = [];
    }
    internal sealed class PlacementDefaults { public double? PreferredSpacingRadiusM { get; set; } }
    internal sealed class Measurement
    {
        public string Stage { get; set; } = "";
        public double HeightM { get; set; }
        public double WidthM { get; set; }
        public double DepthM { get; set; }
    }
    internal sealed class Botanical
    {
        public string Id { get; set; } = "";
        public QuantitativeInputs QuantitativeModelInputs { get; set; } = new();
    }
    internal sealed class QuantitativeInputs
    {
        public SuitabilityCurve? TemperatureSuitabilityCurveC { get; set; }
        public SuitabilityCurve? SlopeSuitabilityCurveDegrees { get; set; }
        public double[]? ElevationSurvivalLimitsM { get; set; }
        public double? MinimumTrunkSpacingM { get; set; }
        public double? CanopyOpacityPercent { get; set; }
        public double? ShadeTolerancePercent { get; set; }
    }
    internal sealed class KindDefaults { public SuitabilityCurve? Slope { get; set; } }
    internal sealed record Resolved(SpeciesProfile Profile, string[] Decisions, JsonElement AssetSource, JsonElement? BotanicalSource);

    internal static IReadOnlyList<Resolved> Load(string assetsPath, string botanicalPath, string policyPath)
    {
        Catalog Read(string path)
        {
            var data = JsonSerializer.Deserialize<Catalog>(File.ReadAllText(path), Json)
                ?? throw new ArgumentException("Missing catalogue: " + path);
            if (data.SchemaVersion != 1) throw new ArgumentException("Unsupported adapter schema: " + path);
            return data;
        }
        var assets = Read(assetsPath); var botany = Read(botanicalPath); var policy = Read(policyPath);
        var botanicalById = botany.Profiles.ToDictionary(p => p.Id, StringComparer.Ordinal);
        using var assetDocument = JsonDocument.Parse(File.ReadAllText(assetsPath));
        using var botanicalDocument = JsonDocument.Parse(File.ReadAllText(botanicalPath));
        var rawAssets = assetDocument.RootElement.GetProperty("assets").EnumerateArray()
            .ToDictionary(p => p.GetProperty("id").GetString()!, p => p.Clone());
        var rawBotany = botanicalDocument.RootElement.GetProperty("profiles").EnumerateArray()
            .ToDictionary(p => p.GetProperty("id").GetString()!, p => p.Clone());
        var resolved = new List<Resolved>();
        foreach (var asset in assets.Assets)
        {
            if (!policy.Kinds.TryGetValue(asset.Kind, out var defaults))
                throw new ArgumentException("No explicit kind policy for " + asset.Id);
            botanicalById.TryGetValue(asset.Id, out var botanical);
            var input = botanical?.QuantitativeModelInputs ?? new QuantitativeInputs();
            var measurements = asset.BaseGameMeasurements.Where(m => m.Stage == "mature").ToArray();
            if (measurements.Length == 0) measurements = asset.BaseGameMeasurements.Where(m => m.Stage == "static").ToArray();
            if (measurements.Length == 0) throw new ArgumentException("No mature/static measurements for " + asset.Id);
            if (measurements.Any(m => !double.IsFinite(m.HeightM) || m.HeightM <= 0
                || !double.IsFinite(m.WidthM) || m.WidthM <= 0 || !double.IsFinite(m.DepthM) || m.DepthM <= 0))
                throw new ArgumentException("Invalid dimensions for " + asset.Id);
            if (asset.PlacementDefaults.PreferredSpacingRadiusM == null)
                throw new ArgumentException("Missing preferred spacing for " + asset.Id);
            var limits = input.ElevationSurvivalLimitsM;
            if (limits != null && limits.Length != 2) throw new ArgumentException("Elevation limits require [min,max].");
            var profile = new SpeciesProfile
            {
                Id = asset.Id,
                CandidatesPerHectare = 100,
                PreferredSpacingRadiusM = asset.PlacementDefaults.PreferredSpacingRadiusM,
                MatureHeightM = measurements.Max(m => m.HeightM),
                CrownRadiusM = measurements.Max(m => Math.Max(m.WidthM, m.DepthM) / 2),
                Temperature = input.TemperatureSuitabilityCurveC,
                Slope = input.SlopeSuitabilityCurveDegrees ?? defaults.Slope,
                Terrain = new TerrainRules(limits?[0] ?? -10000, limits?[1] ?? 10000, 90),
                ExclusionRadiusM = (input.MinimumTrunkSpacingM ?? 0) / 2,
                CrownOpacity = (input.CanopyOpacityPercent ?? 0) / 100,
                Ages = new[] { new AgeWeight { Id = measurements[0].Stage } }
            };
            var decisions = new List<string> {
                "Equal proposal weight 100; mature/static measured footprint and catalogue preferred radius.",
                profile.Temperature == null ? "Temperature: unknown; neutral response 1 (not evidence of universal tolerance)." : "Temperature: authored quantitative curve.",
                input.SlopeSuitabilityCurveDegrees == null ? "Slope: prototype " + asset.Kind + " policy." : "Slope: authored quantitative curve.",
                "Hard elevation limits: " + (limits == null ? "broad solver defaults, not inferred biology." : "authored quantitative limits."),
                "Cold hardiness, FAO observations, soil, moisture, wind and qualitative light are inspection-only; no compatible calibrated response is assumed.",
                "Shade tolerance is retained for inspection; no automatic mortality conversion. Missing opacity/trunk spacing use 0 (effect disabled)."
            };
            if (botanical == null) decisions.Add("No botanical record matched this asset ID.");
            resolved.Add(new Resolved(profile, decisions.ToArray(), rawAssets[asset.Id],
                rawBotany.TryGetValue(asset.Id, out var raw) ? raw : null));
        }
        return resolved;
    }
}
