#nullable disable
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TreeDistrict.Core;
using TreeDistrict.Core.Foliage;

namespace TreeDistrict
{
    /// <summary>Copies an editor district snapshot into solver inputs. No game APIs or file I/O.</summary>
    public static class DistrictGenerationAdapter
    {
        public static GenerationRequest Create(JObject district, JObject city, double? gameTemperatureC = null)
        {
            SettingsModel.ValidateRecord(district);
            SettingsModel.ValidateClimate(city, false);
            if (gameTemperatureC.HasValue && (double.IsNaN(gameTemperatureC.Value)
                || double.IsInfinity(gameTemperatureC.Value) || Math.Abs(gameTemperatureC.Value) > 100))
                throw new ArgumentException("Invalid stable game temperature.");
            var set = district["working"];
            if ((double)set["clumpingPercent"] != 0)
                throw new ArgumentException("Clumping is not implemented; use 0 for this planner.");
            var climate = set["climate"];
            double temperature = (string)city["source"] == "game" && gameTemperatureC.HasValue
                ? gameTemperatureC.Value : (double)city["referenceTemperatureC"];
            var boundary = district["boundary"] as JArray;
            if (boundary == null || boundary.Count < 3 || boundary.Count > 2048)
                throw new ArgumentException("A district boundary needs 3–2048 nodes.");
            var species = set["assets"].Where(a => (double)a["densityPercent"] > 0).Select(a =>
            {
                var p = a["profile"];
                var ages = a["ages"].Select(age => new AgeWeight
                {
                    Id = (string)age["id"],
                    Weight = (double)age["proportionPercent"]
                }).ToArray();
                if (Math.Abs(ages.Sum(age => age.Weight) - 100) > .001)
                    throw new ArgumentException("Enabled asset age proportions must total 100%: " + (string)a["id"]);
                return new SpeciesProfile
                {
                    Id = (string)a["id"],
                    CandidatesPerHectare = 100,
                    DensityPercent = (double)a["densityPercent"],
                    PreferredSpacingRadiusM = (double)p["matureSpacingM"] / 2,
                    MatureHeightM = (double)p["heightM"],
                    CrownRadiusM = (double)p["canopyRadiusM"],
                    CrownOpacity = (double)p["canopyOpacityPercent"] / 100,
                    Temperature = Curve(p["temperature"]),
                    Slope = Curve(p["slope"]),
                    Terrain = Limits(p["limits"]),
                    Ages = ages
                };
            }).ToArray();
            return new GenerationRequest
            {
                Seed = (ulong)district["seed"],
                Boundary = boundary.Select(p => new Position2(
                    SettingsModel.Number(p["x"], -double.MaxValue, double.MaxValue),
                    SettingsModel.Number(p["z"], -double.MaxValue, double.MaxValue))).ToArray(),
                Species = species,
                OverallDensityPercent = (double)set["overallDensityPercent"],
                Climate = new ClimateProfile((double?)climate["referenceTemperatureC"] ?? temperature,
                    (double?)climate["referenceElevationM"] ?? (double)city["referenceElevationM"],
                    (double?)climate["coolingCPerKm"] ?? (double)city["coolingCPerKm"]),
                Terrain = Limits(set["limits"]),
                // Shade tolerance remains an authoring field; no unagreed mortality conversion.
                ResolveShade = false,
                ResolveCompetition = false
            };
        }
        private static TerrainRules Limits(JToken p) => new TerrainRules(
            (double)p["minElevationM"], (double)p["maxElevationM"], (double)p["maxSlopeDegrees"]);
        private static SuitabilityCurve Curve(JToken p) => new SuitabilityCurve
        {
            Minimum = (double)p["min"],
            Optimum = (double)p["optimum"],
            Maximum = (double)p["max"]
        };
    }
}
