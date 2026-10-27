using System;

namespace TreeDistrict.Core.Foliage
{
    /// <summary>Authoring lapse-rate model; not a weather or atmospheric simulation.</summary>
    public sealed class ClimateProfile
    {
        public ClimateProfile(double referenceTemperatureC = 18, double referenceElevationM = 0, double coolingCPerKm = 6.5)
        {
            if (!Polygon.Finite(referenceTemperatureC) || !Polygon.Finite(referenceElevationM)
                || !Polygon.Finite(coolingCPerKm) || coolingCPerKm < 0)
                throw new ArgumentException("Climate inputs must be finite; cooling per kilometre must be nonnegative.");
            ReferenceTemperatureC = referenceTemperatureC;
            ReferenceElevationM = referenceElevationM;
            CoolingCPerKm = coolingCPerKm;
        }
        public double ReferenceTemperatureC { get; }
        public double ReferenceElevationM { get; }
        public double CoolingCPerKm { get; }
        public double TemperatureAt(double elevationM)
        {
            if (!Polygon.Finite(elevationM)) throw new ArgumentException("Finite elevation required.");
            double temperature = ReferenceTemperatureC - (elevationM - ReferenceElevationM) / 1000 * CoolingCPerKm;
            if (!Polygon.Finite(temperature)) throw new ArgumentException("Climate calculation overflowed.");
            return temperature;
        }
    }
}
