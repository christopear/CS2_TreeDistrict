using System.Text.Json;
using TreeDistrict.Core.Foliage;

/// <summary>Standalone terrain adapter: bilinear heights and interpolated node gradients.
/// Gradients use centred differences, second-order one-sided differences at borders.
/// Matches the 1m diagnostic terrain; no game data or solver policy lives here.</summary>
internal sealed class HeightmapTerrain
{
    public double[] OriginM { get; set; } = [0, 0];
    public double SampleSpacingM { get; set; }
    public double[][] HeightsM { get; set; } = [];
    private double[][] dx = [], dz = [];
    internal static HeightmapTerrain Load(string path)
    {
        var map = JsonSerializer.Deserialize<HeightmapTerrain>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new ArgumentException("Missing heightmap.");
        if (map.OriginM.Length != 2 || map.OriginM.Any(v => !double.IsFinite(v))
            || !double.IsFinite(map.SampleSpacingM) || map.SampleSpacingM <= 0
            || map.HeightsM.Length < 3 || map.HeightsM[0] == null || map.HeightsM[0].Length < 3)
            throw new ArgumentException("Heightmap needs a finite origin, positive spacing and at least 3x3 samples.");
        int rows = map.HeightsM.Length, cols = map.HeightsM[0].Length;
        if (map.HeightsM.Any(row => row == null || row.Length != cols || row.Any(v => !double.IsFinite(v))))
            throw new ArgumentException("Heightmap must be a finite rectangular array.");
        map.dx = new double[rows][]; map.dz = new double[rows][];
        double Derivative(Func<int, double> value, int index, int length) => index == 0
            ? (-3 * value(0) + 4 * value(1) - value(2)) / (2 * map.SampleSpacingM)
            : index == length - 1 ? (3 * value(index) - 4 * value(index - 1) + value(index - 2)) / (2 * map.SampleSpacingM)
            : (value(index + 1) - value(index - 1)) / (2 * map.SampleSpacingM);
        for (int z = 0; z < rows; z++)
        {
            map.dx[z] = new double[cols]; map.dz[z] = new double[cols];
            for (int x = 0; x < cols; x++)
            {
                map.dx[z][x] = Derivative(i => map.HeightsM[z][i], x, cols);
                map.dz[z][x] = Derivative(i => map.HeightsM[i][x], z, rows);
            }
        }
        return map;
    }
    internal GroundSample Sample(Position2 position)
    {
        double x = (position.X - OriginM[0]) / SampleSpacingM, z = (position.Z - OriginM[1]) / SampleSpacingM;
        if (!double.IsFinite(x) || !double.IsFinite(z) || x < 0 || z < 0 || x > HeightsM[0].Length - 1 || z > HeightsM.Length - 1)
            throw new ArgumentException("Heightmap does not cover the requested terrain position.");
        int ix = Math.Min((int)Math.Floor(x), HeightsM[0].Length - 2), iz = Math.Min((int)Math.Floor(z), HeightsM.Length - 2);
        double tx = x - ix, tz = z - iz;
        double Blend(double[][] values) => (values[iz][ix] * (1 - tx) + values[iz][ix + 1] * tx) * (1 - tz)
            + (values[iz + 1][ix] * (1 - tx) + values[iz + 1][ix + 1] * tx) * tz;
        double gx = Blend(dx), gz = Blend(dz);
        return new GroundSample(Blend(HeightsM), Math.Atan(Math.Sqrt(gx * gx + gz * gz)) * 180 / Math.PI);
    }
}
