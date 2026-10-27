using System;

namespace TreeDistrict.Core.Foliage
{
    /// <summary>Exact placement constraints plus sampled growing conditions; no plant instance required.</summary>
    public readonly struct PlacementEnvironment
    {
        public PlacementEnvironment(GroundSample ground, double temperatureC, double slopeDegrees)
        { Ground = ground; TemperatureC = temperatureC; SlopeDegrees = slopeDegrees; }
        public GroundSample Ground { get; }
        public double TemperatureC { get; }
        public double SlopeDegrees { get; }
    }

    public readonly struct SuitabilityResult
    {
        public SuitabilityResult(bool allowed, double score, double temperatureScore, double slopeScore)
        {
            if (!Polygon.Finite(score) || score < 0 || score > 1
                || !Polygon.Finite(temperatureScore) || temperatureScore < 0 || temperatureScore > 1
                || !Polygon.Finite(slopeScore) || slopeScore < 0 || slopeScore > 1)
                throw new ArgumentOutOfRangeException(nameof(temperatureScore), "Response scores must be in [0,1].");
            Allowed = allowed; Score = score; TemperatureScore = temperatureScore; SlopeScore = slopeScore;
        }
        public bool Allowed { get; }
        public double TemperatureScore { get; }
        public double SlopeScore { get; }
        // A hard exclusion does not masquerade as a soft response. Consumers must check Allowed.
        public double Score { get; }
    }

    public interface IVegetationSuitabilityEvaluator
    {
        SuitabilityResult Evaluate(SpeciesProfile profile, PlacementEnvironment environment, TerrainRules districtRules);
    }

    /// <summary>Pure profile/environment calculation shared by local selection and optional later filtering.</summary>
    public sealed class VegetationSuitabilityEvaluator : IVegetationSuitabilityEvaluator
    {
        public SuitabilityResult Evaluate(SpeciesProfile profile, PlacementEnvironment environment, TerrainRules districtRules)
        {
            if (profile == null || districtRules == null) throw new ArgumentNullException();
            var ground = environment.Ground;
            bool valid = Polygon.Finite(environment.TemperatureC) && Polygon.Finite(environment.SlopeDegrees)
                && environment.SlopeDegrees >= 0 && environment.SlopeDegrees <= 90;
            bool allowed = valid
                && districtRules.Allows(ground.ElevationM, ground.SlopeDegrees, ground.IsWater, ground.IsBlocked)
                && profile.Terrain.Allows(ground.ElevationM, ground.SlopeDegrees, ground.IsWater, ground.IsBlocked);
            double temperatureScore = valid ? profile.Temperature?.Evaluate(environment.TemperatureC) ?? 1 : 0;
            double slopeScore = valid ? profile.Slope?.Evaluate(environment.SlopeDegrees, true) ?? 1 : 0;
            return new SuitabilityResult(allowed, Math.Min(temperatureScore, slopeScore), temperatureScore, slopeScore);
        }
    }
}
