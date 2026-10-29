using System.Diagnostics;
using System.Text.Json;
using TreeDistrict.Core.Canopy;
using TreeDistrict.Core.Foliage;

// Standalone look-development fixture. No game, Unity or individual tree assets.
string output = args.Length > 0 ? args[0] : "artifacts/canopy-preview.json";
const int size = 200, resolution = 401;
var geometry = new CanopyGeometry(new[] { new Position2(0, 0), new Position2(size, 0),
    new Position2(size, size), new Position2(0, size) });
var watch = Stopwatch.StartNew();
int vertices = 0, triangles = 0, chunks = 0;
for (int z = geometry.MinTileZ; z <= geometry.MaxTileZ; z++)
    for (int x = geometry.MinTileX; x <= geometry.MaxTileX; x++)
    {
        var tile = geometry.BuildTile(x, z);
        vertices += tile.Points.Count; triangles += tile.Indices.Count / 3; chunks++;
    }
double geometryMs = watch.Elapsed.TotalMilliseconds;
watch.Restart();
var height = new double[resolution * resolution];
var colour = new double[height.Length * 3];
var normal = new double[height.Length * 3];
for (int z = 0; z < resolution; z++)
    for (int x = 0; x < resolution; x++)
    {
        double px = x * size / (double)(resolution - 1), pz = z * size / (double)(resolution - 1);
        int i = z * resolution + x;
        height[i] = geometry.SurfaceHeight(px, pz);
        var c = CanopyGeometry.Colour(px, pz); var n = CanopyGeometry.DetailNormal(px, pz);
        colour[i * 3] = c.R; colour[i * 3 + 1] = c.G; colour[i * 3 + 2] = c.B;
        normal[i * 3] = n.X; normal[i * 3 + 1] = n.Y; normal[i * 3 + 2] = n.Z;
    }
double samplingMs = watch.Elapsed.TotalMilliseconds;
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
File.WriteAllText(output, JsonSerializer.Serialize(new { size, resolution, height, colour, normal,
    geometryMs, samplingMs, vertices, triangles, chunks }));
Console.WriteLine($"200 m patch: {triangles:N0} triangles, {chunks} chunks; geometry {geometryMs:F0} ms; preview field samples {samplingMs:F0} ms. {output}");
