using TreeDistrict.Core.Canopy;
using TreeDistrict.Core.Foliage;

namespace TreeDistrict.Core.Tests;

public class CanopyGeometryTests
{
    private static Position2 P(double x, double z) => new(x, z);
    private static double Cross(Position2 a, Position2 b, Position2 c) =>
        (b.X - a.X) * (c.Z - a.Z) - (b.Z - a.Z) * (c.X - a.X);
    private static double Area(CanopyGeometry.Tile tile)
    {
        double sum = 0;
        for (int i = 0; i < tile.Indices.Count; i += 3)
        {
            double area = Cross(tile.Points[tile.Indices[i]], tile.Points[tile.Indices[i + 1]], tile.Points[tile.Indices[i + 2]]) / 2;
            Assert.True(area > 0, "Faces must be non-degenerate and consistently wound.");
            sum += area;
        }
        return sum;
    }
    private static IEnumerable<CanopyGeometry.Tile> Tiles(CanopyGeometry model)
    {
        for (int z = model.MinTileZ; z <= model.MaxTileZ; z++)
            for (int x = model.MinTileX; x <= model.MaxTileX; x++) yield return model.BuildTile(x, z);
    }
    [Fact]
    public void SquareIsCoveredExactlyWithNoSkirtsOnInternalSeams()
    {
        var g = new CanopyGeometry(new[] { P(0, 0), P(192, 0), P(192, 192), P(0, 192) });
        var tiles = Tiles(g).ToArray();
        Assert.Equal(192 * 192, tiles.Sum(Area), 5);
        double perimeter = tiles.SelectMany(t => t.Edges).Sum(e => Math.Sqrt(Math.Pow(e.A.X - e.B.X, 2) + Math.Pow(e.A.Z - e.B.Z, 2)));
        Assert.Equal(192 * 4, perimeter, 5);
        foreach (var e in tiles.SelectMany(t => t.Edges))
            Assert.True(e.A.X == 0 && e.B.X == 0 || e.A.X == 192 && e.B.X == 192 ||
                e.A.Z == 0 && e.B.Z == 0 || e.A.Z == 192 && e.B.Z == 192);
    }
    [Fact]
    public void DeepConcavityRemainsEmptyAndAreaIsPreserved()
    {
        // U shape; its intersection with an upper tile has disconnected pieces.
        var g = new CanopyGeometry(new[] { P(0, 0), P(180, 0), P(180, 180), P(120, 180), P(120, 40), P(60, 40), P(60, 180), P(0, 180) });
        var tiles = Tiles(g).ToArray();
        Assert.Equal(180 * 180 - 60 * 140, tiles.Sum(Area), 5);
        foreach (var t in tiles)
            for (int i = 0; i < t.Indices.Count; i += 3)
            {
                var a = t.Points[t.Indices[i]]; var b = t.Points[t.Indices[i + 1]]; var c = t.Points[t.Indices[i + 2]];
                double x = (a.X + b.X + c.X) / 3, z = (a.Z + b.Z + c.Z) / 3;
                Assert.False(x > 60 + 1e-6 && x < 120 - 1e-6 && z > 40 + 1e-6);
            }
    }
    [Fact]
    public void NegativeCoordinatesAndClockwiseClosedCollinearNodesWork()
    {
        var g = new CanopyGeometry(new[] { P(-150, -160), P(-150, -40), P(-90, -40), P(-30, -40), P(-30, -160), P(-150, -160) });
        Assert.Equal(120 * 120, Tiles(g).Sum(Area), 5);
    }
    [Fact]
    public void TinyAndNarrowAreasAreNotLostBetweenScatterPoints()
    {
        var g = new CanopyGeometry(new[] { P(.2, .3), P(.21, .3), P(.21, 180), P(.2, 180) });
        Assert.Equal(.01 * 179.7, Tiles(g).Sum(Area), 5);
    }
    [Fact]
    public void SharedTileRimsHaveIdenticalVertices()
    {
        var g = new CanopyGeometry(new[] { P(5, 13), P(185, 27), P(180, 182), P(10, 170) });
        var left = g.BuildTile(0, 0); var right = g.BuildTile(1, 0);
        double[] Rim(CanopyGeometry.Tile t) => t.Points.Where(p => Math.Abs(p.X - CanopyGeometry.TileSize) < 1e-6)
            .Select(p => Math.Round(p.Z, 6)).Distinct().OrderBy(z => z).ToArray();
        Assert.Equal(Rim(left), Rim(right));
        Assert.NotEmpty(Rim(left));
    }
    [Fact]
    public void GenerationIsRepeatableAndSurfaceIsContinuousAcrossWorldOrigin()
    {
        var g = new CanopyGeometry(new[] { P(0, 0), P(96, 0), P(96, 96), P(0, 96) });
        var a = g.BuildTile(0, 0); var b = g.BuildTile(0, 0);
        Assert.Equal(a.Points, b.Points); Assert.Equal(a.Indices, b.Indices);
        Assert.InRange(Math.Abs(CanopyGeometry.Height(-1e-6, 0) - CanopyGeometry.Height(1e-6, 0)), 0, 1e-4);
        foreach (var p in a.Points)
        { Assert.InRange(CanopyGeometry.Height(p.X, p.Z), 3, 20.25); Assert.InRange(CanopyGeometry.Shade(p.X, p.Z), .32, 1); }
    }
    [Fact]
    public void TwoHundredMetrePlotHasFineGeometryAndBoundedColour()
    {
        var g = new CanopyGeometry(new[] { P(0, 0), P(200, 0), P(200, 200), P(0, 200) });
        var tiles = Tiles(g).ToArray();
        Assert.Equal(40000, tiles.Sum(Area), 5);
        Assert.InRange(tiles.Sum(t => t.Indices.Count / 3), 80000, 90000);
        foreach (var p in tiles.SelectMany(t => t.Points))
        {
            var colour = CanopyGeometry.Colour(p.X, p.Z);
            Assert.InRange(colour.R, .05, .36);
            Assert.InRange(colour.G, .07, .365);
            Assert.InRange(colour.B, .04, .235);
        }
    }
    [Fact]
    public void BoundaryTapersAndInteriorRetainsFullCanopyHeight()
    {
        var g = new CanopyGeometry(new[] { P(0, 0), P(200, 0), P(200, 200), P(0, 200) });
        Assert.Equal(.5, g.SurfaceHeight(0, 40), 8);
        Assert.Equal(CanopyGeometry.Height(50, 50), g.SurfaceHeight(50, 50), 8);
        Assert.InRange(g.SurfaceHeight(.1, 40), .5, CanopyGeometry.Height(.1, 40));
        Assert.Equal(50, g.BoundaryDistance(50, 50), 8);
    }
    [Fact]
    public void DetailNormalsAreFiniteUnitVectorsAndClumpsHaveHeightContrast()
    {
        var heights = new List<double>();
        for (double z = -20; z <= 20; z += .7)
            for (double x = -20; x <= 20; x += .7)
            {
                var n = CanopyGeometry.DetailNormal(x, z);
                Assert.Equal(1, n.X * n.X + n.Y * n.Y + n.Z * n.Z, 8);
                Assert.True(n.Z > 0);
                heights.Add(CanopyGeometry.Height(x, z));
            }
        Assert.True(heights.Max() - heights.Min() > 9, "Canopy must have distinct peaks and hollows.");
    }
    [Fact]
    public void InvalidBoundaryFailsWithoutProducingNonFiniteGeometry()
    {
        Assert.Throws<ArgumentException>(() => new CanopyGeometry(new[] { P(0, 0), P(1, 1), P(2, 2) }));
        Assert.Throws<ArgumentException>(() => new CanopyGeometry(new[] { P(0, 0), P(double.NaN, 1), P(1, 0) }));
    }
}
