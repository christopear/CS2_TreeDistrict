using System;

namespace TreeDistrict.Core.Foliage
{
    /// <summary>Shared environmental response for pre-placement choice and optional later filtering.</summary>
    internal static class VegetationSuitability
    {
        internal static double Evaluate(GenerationRequest request, SpeciesProfile profile,
            GroundSample ground, double temperature, double slope)
        {
            if (!Polygon.Finite(temperature) || !Polygon.Finite(slope)
                || !request.Terrain.Allows(ground.ElevationM, ground.SlopeDegrees, ground.IsWater, ground.IsBlocked)
                || !profile.Terrain.Allows(ground.ElevationM, ground.SlopeDegrees, ground.IsWater, ground.IsBlocked)) return 0;
            return Math.Min(profile.Temperature?.Evaluate(temperature) ?? 1, profile.Slope?.Evaluate(slope, true) ?? 1);
        }
    }
}
