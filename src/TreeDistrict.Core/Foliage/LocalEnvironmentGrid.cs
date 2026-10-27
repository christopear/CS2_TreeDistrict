using System;
using System.Collections.Generic;
using System.Threading;

namespace TreeDistrict.Core.Foliage
{
    /// <summary>Continuous local growing conditions. No vegetation, quotas, or placement decisions.</summary>
    public readonly struct LocalEnvironmentSample
    {
        public LocalEnvironmentSample(double elevationM, double slopeDegrees, double temperatureC)
        {
            if (!Polygon.Finite(elevationM) || !Polygon.Finite(temperatureC)
                || !Polygon.Finite(slopeDegrees) || slopeDegrees < 0 || slopeDegrees > 90)
                throw new ArgumentException("Environment requires finite elevation/temperature and slope in [0,90].");
            ElevationM = elevationM; SlopeDegrees = slopeDegrees; TemperatureC = temperatureC;
        }
        public double ElevationM { get; }
        public double SlopeDegrees { get; }
        public double TemperatureC { get; }
    }

    /// <summary>Read-only contract for vegetation choice/suitability, independent of grid storage.</summary>
    public interface ILocalEnvironmentField
    {
        bool TrySample(Position2 position, out LocalEnvironmentSample environment);
    }

    /// <summary>
    /// Immutable environmental snapshot. Cell size controls environmental resolution, never plant spacing.
    /// Samples live at cell corners and interpolate bilinearly across the district's bounding rectangle.
    /// Hard terrain limits and occupied ground must still be checked at the actual placement position.
    /// </summary>
    public sealed class LocalEnvironmentGrid : ILocalEnvironmentField
    {
        private readonly LocalEnvironmentSample[] samples;
        private LocalEnvironmentGrid(Position2 minimum, Position2 maximum, double cellSizeM,
            int columns, int rows, LocalEnvironmentSample[] samples)
        {
            Minimum = minimum; Maximum = maximum; CellSizeM = cellSizeM;
            Columns = columns; Rows = rows; this.samples = samples;
        }
        public const double DefaultCellSizeM = 5;
        public Position2 Minimum { get; }
        public Position2 Maximum { get; }
        public double CellSizeM { get; }
        public int Columns { get; }
        public int Rows { get; }
        public int SampleCount => samples.Length;

        /// <summary>
        /// Provider boundary for map integration: supply topology-derived conditions in world metres.
        /// TODO(game adapter): capture map heights/normals and climate on a safe game thread, then build
        /// from that stable snapshot. The core never calls game APIs. Missing/invalid samples fail the build.
        /// Boundary is used for extent only; the Poisson sampler retains responsibility for polygon containment.
        /// </summary>
        public static LocalEnvironmentGrid Build(IReadOnlyList<Position2> boundary,
            Func<Position2, LocalEnvironmentSample> provider, double cellSizeM = DefaultCellSizeM,
            int maxSamples = 1000000, CancellationToken cancellationToken = default)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (!Polygon.Finite(cellSizeM) || cellSizeM < .001 || maxSamples < 4)
                throw new ArgumentException("Cell size must be finite and >= 0.001m; sample budget must be >= 4.");
            cancellationToken.ThrowIfCancellationRequested();
            var polygon = new Polygon(boundary);
            double columns = Math.Ceiling((polygon.MaxX - polygon.MinX) / cellSizeM) + 1;
            double rows = Math.Ceiling((polygon.MaxZ - polygon.MinZ) / cellSizeM) + 1;
            if (!Polygon.Finite(columns * rows) || columns * rows > maxSamples || columns * rows > int.MaxValue)
                throw new InvalidOperationException("Environment grid exceeds sample budget; choose a coarser resolution.");
            int width = (int)columns, height = (int)rows;
            var values = new LocalEnvironmentSample[width * height];
            for (int z = 0; z < height; z++)
                for (int x = 0; x < width; x++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // Last cells may be narrower; never query outside the map/district bounds rectangle.
                    var position = new Position2(Math.Min(polygon.MaxX, polygon.MinX + x * cellSizeM),
                        Math.Min(polygon.MaxZ, polygon.MinZ + z * cellSizeM));
                    values[z * width + x] = provider(position);
                }
            cancellationToken.ThrowIfCancellationRequested();
            return new LocalEnvironmentGrid(new Position2(polygon.MinX, polygon.MinZ),
                new Position2(polygon.MaxX, polygon.MaxZ), cellSizeM, width, height, values);
        }

        /// <summary>Adapter for the existing topology callback and city/set lapse-rate climate.</summary>
        public static LocalEnvironmentGrid FromTerrain(IReadOnlyList<Position2> boundary,
            Func<Position2, GroundSample> sampleTerrain, ClimateProfile? climate = null,
            double cellSizeM = DefaultCellSizeM, int maxSamples = 1000000,
            CancellationToken cancellationToken = default)
        {
            if (sampleTerrain == null) throw new ArgumentNullException(nameof(sampleTerrain));
            return Build(boundary, position =>
            {
                var ground = sampleTerrain(position);
                return new LocalEnvironmentSample(ground.ElevationM, ground.SlopeDegrees,
                    climate?.TemperatureAt(ground.ElevationM) ?? ground.TemperatureC);
            }, cellSizeM, maxSamples, cancellationToken);
        }

        public bool TrySample(Position2 position, out LocalEnvironmentSample environment)
        {
            environment = default;
            if (!Polygon.Finite(position.X) || !Polygon.Finite(position.Z)
                || position.X < Minimum.X || position.X > Maximum.X
                || position.Z < Minimum.Z || position.Z > Maximum.Z) return false;
            int x = Math.Min(Columns - 2, (int)Math.Floor((position.X - Minimum.X) / CellSizeM));
            int z = Math.Min(Rows - 2, (int)Math.Floor((position.Z - Minimum.Z) / CellSizeM));
            double startX = Minimum.X + x * CellSizeM, startZ = Minimum.Z + z * CellSizeM;
            double tx = (position.X - startX) / (Math.Min(Maximum.X, startX + CellSizeM) - startX);
            double tz = (position.Z - startZ) / (Math.Min(Maximum.Z, startZ + CellSizeM) - startZ);
            var a = samples[z * Columns + x]; var b = samples[z * Columns + x + 1];
            var c = samples[(z + 1) * Columns + x]; var d = samples[(z + 1) * Columns + x + 1];
            double Blend(double av, double bv, double cv, double dv) =>
                (av * (1 - tx) + bv * tx) * (1 - tz) + (cv * (1 - tx) + dv * tx) * tz;
            environment = new LocalEnvironmentSample(Blend(a.ElevationM, b.ElevationM, c.ElevationM, d.ElevationM),
                Math.Max(0, Math.Min(90, Blend(a.SlopeDegrees, b.SlopeDegrees, c.SlopeDegrees, d.SlopeDegrees))),
                Blend(a.TemperatureC, b.TemperatureC, c.TemperatureC, d.TemperatureC));
            return true;
        }
    }
}
