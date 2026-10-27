using System.Text.Json;
using TreeDistrict.Core.Foliage;
namespace TreeDistrict.Core.Tests
{
    public class HeightmapTerrainTests
    {
        [Fact]
        public void InterpolatesPlaneAndBothGradientComponentsIncludingEdges()
        {
            string path = Path.GetTempFileName();
            try
            {
                var heights = Enumerable.Range(0, 4).Select(z => Enumerable.Range(0, 5).Select(x => 10.0 + 2 * x + 4 * z).ToArray()).ToArray();
                File.WriteAllText(path, JsonSerializer.Serialize(new { originM = new[] { 10, 20 }, sampleSpacingM = 2, heightsM = heights }));
                var map = HeightmapTerrain.Load(path);
                foreach (var p in new[] { new Position2(10, 20), new Position2(12.3, 24.7), new Position2(18, 26) })
                {
                    var sample = map.Sample(p);
                    Assert.Equal(10 + (p.X - 10) + 2 * (p.Z - 20), sample.ElevationM, 10);
                    Assert.Equal(Math.Atan(Math.Sqrt(5)) * 180 / Math.PI, sample.SlopeDegrees, 10);
                }
                Assert.Throws<ArgumentException>(() => map.Sample(new Position2(9, 20)));
                File.WriteAllText(path, "{\"sampleSpacingM\":1,\"heightsM\":[[0,0,0],[0,0],[0,0,0]]}");
                Assert.Throws<ArgumentException>(() => HeightmapTerrain.Load(path));
            }
            finally { File.Delete(path); }
        }
    }
}
