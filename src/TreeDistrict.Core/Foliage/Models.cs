using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TreeDistrict.Core.Foliage
{
    public readonly struct Position2
    {
        public Position2(double x, double z) { X = x; Z = z; }
        public double X { get; }
        public double Z { get; }
    }

    public readonly struct GroundSample
    {
        public GroundSample(double elevationM, double slopeDegrees = 0, double temperatureC = 18,
            bool isWater = false, bool isBlocked = false)
        {
            ElevationM = elevationM; SlopeDegrees = slopeDegrees; TemperatureC = temperatureC;
            IsWater = isWater; IsBlocked = isBlocked;
        }
        public double ElevationM { get; }
        public double SlopeDegrees { get; }
        public double TemperatureC { get; }
        public bool IsWater { get; }
        public bool IsBlocked { get; }
    }

    public sealed class SuitabilityCurve
    {
        public double Minimum { get; set; }
        public double Optimum { get; set; }
        public double Maximum { get; set; }
        public double Evaluate(double value, bool flatSlopeIsSuitable = false)
        {
            if (flatSlopeIsSuitable && value <= Optimum) return 1;
            return Math.Max(0, Math.Min(1, value <= Optimum
                ? (value - Minimum) / (Optimum - Minimum) : (Maximum - value) / (Maximum - Optimum)));
        }
    }

    public sealed class AgeWeight
    {
        public string Id { get; set; } = "mature";
        public double Weight { get; set; } = 1;
    }

    /// <summary>All distances are metres. Numerical ecology values are authoring inputs, not inferred biology.</summary>
    public sealed class SpeciesProfile
    {
        public string Id { get; set; } = "";
        public double CandidatesPerHectare { get; set; }
        public double DensityPercent { get; set; } = 100;
        public double? PreferredSpacingRadiusM { get; set; }
        public double MatureHeightM { get; set; }
        public double ExclusionRadiusM { get; set; }
        public double CrownRadiusM { get; set; }
        public double CrownOpacity { get; set; }
        public double MinimumLight { get; set; } = 0;
        public double FullLight { get; set; } = 1;
        public bool RequiresShade { get; set; }
        public TerrainRules Terrain { get; set; } = new TerrainRules(-10000, 10000, 90);
        public SuitabilityCurve? Temperature { get; set; }
        public SuitabilityCurve? Slope { get; set; }
        public IReadOnlyList<AgeWeight> Ages { get; set; } = new[] { new AgeWeight() };
    }

    /// <summary>Immutable world constraints supplied by the caller; never changed or removed by this library.</summary>
    public sealed class ExistingPlant
    {
        public string Id { get; set; } = "";
        public Position2 Position { get; set; }
        public double ElevationM { get; set; }
        public double HeightM { get; set; }
        public double ExclusionRadiusM { get; set; }
        public double CrownRadiusM { get; set; }
        public double CrownOpacity { get; set; }
    }

    public sealed class GenerationRequest
    {
        public ulong Seed { get; set; } = 1;
        public IReadOnlyList<Position2> Boundary { get; set; } = Array.Empty<Position2>();
        public IReadOnlyList<SpeciesProfile> Species { get; set; } = Array.Empty<SpeciesProfile>();
        public IReadOnlyList<ExistingPlant> ExistingPlants { get; set; } = Array.Empty<ExistingPlant>();
        public double OverallDensityPercent { get; set; } = 100;
        /// <summary>If present, derive temperature from sampled elevation; otherwise use GroundSample.TemperatureC.</summary>
        public ClimateProfile? Climate { get; set; }
        public TerrainRules Terrain { get; set; } = new TerrainRules(-10000, 10000, 90);
        public bool ResolveCompetition { get; set; } = true;
        public bool ResolveShade { get; set; } = true;
        /// <summary>A vertical canopy-layer allowance, avoiding shade changes from tiny terrain-height differences.</summary>
        public double MinimumShadeHeightDifferenceM { get; set; } = 2;
        public int MaxCandidates { get; set; } = 100000;
        public int MaxPlacements { get; set; } = 100000;
        public int MaxPositionAttempts { get; set; } = 2000000;
        public long MaxNeighbourChecks { get; set; } = 20000000;
    }

    public sealed class Placement
    {
        internal Placement(string speciesId, int candidateIndex, Position2 position, double elevation,
            double rotation, string age, double light)
        {
            SpeciesId = speciesId; CandidateIndex = candidateIndex; Position = position;
            ElevationM = elevation; RotationRadians = rotation; AgeId = age; AvailableLight = light;
        }
        public string SpeciesId { get; }
        public int CandidateIndex { get; }
        public Position2 Position { get; }
        public double ElevationM { get; }
        public double RotationRadians { get; }
        public string AgeId { get; }
        public double AvailableLight { get; }
    }

    public sealed class SpeciesCounts
    {
        public string SpeciesId { get; internal set; } = "";
        public double ExpectedCandidates { get; internal set; }
        public int Candidates { get; internal set; }
        public int TerrainRejected { get; internal set; }
        public int EnvironmentRejected { get; internal set; }
        public int ExistingPlantRejected { get; internal set; }
        public int CompetitionRejected { get; internal set; }
        public int ShadeRejected { get; internal set; }
        public int Accepted { get; internal set; }
        public int Rejected => TerrainRejected + EnvironmentRejected + ExistingPlantRejected + CompetitionRejected + ShadeRejected;
    }

    public sealed class GenerationResult
    {
        internal GenerationResult(List<Placement> placements, List<SpeciesCounts> counts, double area,
            int attempts, long checks, string algorithmVersion = FoliageGenerator.AlgorithmVersion,
            int samplingOpportunities = 0, int emptyEnvironmentSamples = 0, int spacingRejectedSamples = 0)
        {
            SamplingOpportunities = samplingOpportunities; EmptyEnvironmentSamples = emptyEnvironmentSamples;
            SpacingRejectedSamples = spacingRejectedSamples;
            Placements = new ReadOnlyCollection<Placement>(placements);
            Counts = new ReadOnlyCollection<SpeciesCounts>(counts);
            AreaM2 = area; PositionAttempts = attempts; NeighbourChecks = checks; AlgorithmVersion = algorithmVersion;
        }
        public int SamplingOpportunities { get; }
        public int EmptyEnvironmentSamples { get; }
        public int SpacingRejectedSamples { get; }
        public string AlgorithmVersion { get; }
        public IReadOnlyList<Placement> Placements { get; }
        public IReadOnlyList<SpeciesCounts> Counts { get; }
        public double AreaM2 { get; }
        public int PositionAttempts { get; }
        public long NeighbourChecks { get; }
    }
}
