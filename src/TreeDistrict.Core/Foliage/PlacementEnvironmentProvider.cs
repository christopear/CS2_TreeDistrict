using System;

namespace TreeDistrict.Core.Foliage
{
    /// <summary>Joins exact terrain with optional gridded conditions for every solver query.</summary>
    public sealed class PlacementEnvironmentProvider
    {
        private readonly Func<Position2, GroundSample> terrain;
        private readonly ClimateProfile? climate;
        private readonly ILocalEnvironmentField? field;
        public PlacementEnvironmentProvider(Func<Position2, GroundSample> terrain,
            ClimateProfile? climate = null, ILocalEnvironmentField? field = null)
        {
            this.terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            this.climate = climate; this.field = field;
        }
        public PlacementEnvironment Sample(Position2 position)
        {
            var ground = terrain(position);
            double temperature = climate?.TemperatureAt(ground.ElevationM) ?? ground.TemperatureC;
            double slope = ground.SlopeDegrees;
            if (field != null)
            {
                if (!field.TrySample(position, out var local))
                    throw new InvalidOperationException("Environment field does not cover a proposed placement.");
                temperature = local.TemperatureC; slope = local.SlopeDegrees;
            }
            return new PlacementEnvironment(ground, temperature, slope);
        }
    }
}
