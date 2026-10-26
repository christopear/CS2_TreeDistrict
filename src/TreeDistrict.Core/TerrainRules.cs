using System;

namespace TreeDistrict.Core
{
    /// <summary>
    /// Terrain eligibility for a vegetation area. Heights are world-space metres;
    /// slope is degrees from horizontal. All limits are inclusive.
    /// </summary>
    public sealed class TerrainRules
    {
        public TerrainRules(double minimumElevation, double maximumElevation, double maximumSlopeDegrees)
        {
            if (!IsFinite(minimumElevation))
                throw new ArgumentOutOfRangeException(nameof(minimumElevation));
            if (!IsFinite(maximumElevation) || maximumElevation < minimumElevation)
                throw new ArgumentOutOfRangeException(nameof(maximumElevation));
            if (!IsFinite(maximumSlopeDegrees) || maximumSlopeDegrees < 0 || maximumSlopeDegrees > 90)
                throw new ArgumentOutOfRangeException(nameof(maximumSlopeDegrees));

            MinimumElevation = minimumElevation;
            MaximumElevation = maximumElevation;
            MaximumSlopeDegrees = maximumSlopeDegrees;
        }

        public double MinimumElevation { get; }
        public double MaximumElevation { get; }
        public double MaximumSlopeDegrees { get; }

        /// <summary>Invalid terrain samples, water and blocked land cannot receive vegetation.</summary>
        public bool Allows(double elevation, double slopeDegrees, bool isWater, bool isBlocked)
        {
            return !isWater && !isBlocked
                && IsFinite(elevation) && IsFinite(slopeDegrees)
                && elevation >= MinimumElevation && elevation <= MaximumElevation
                && slopeDegrees >= 0 && slopeDegrees <= MaximumSlopeDegrees;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
