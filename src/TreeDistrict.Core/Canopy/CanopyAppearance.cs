using System;

namespace TreeDistrict.Core.Canopy
{
    // Sample once with a halo, then compute the study's 7.5 m local-concavity
    // shading using a summed-area table. No repeated lobe evaluation per neighbour.
    public sealed class CanopyAppearance
    {
        public const double Step = .5;
        public const int Size = 97;
        private const int Radius = 7;
        private readonly double[,] heights, sums;
        public CanopyAppearance(CanopyGeometry geometry, double x0, double z0)
        {
            int n = Size + 2 * Radius;
            heights = new double[n, n]; sums = new double[n + 1, n + 1];
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    double h = geometry.SurfaceHeight(x0 + (x - Radius) * Step, z0 + (z - Radius) * Step);
                    heights[z, x] = h;
                    sums[z + 1, x + 1] = h + sums[z, x + 1] + sums[z + 1, x] - sums[z, x];
                }
        }
        public double Occlusion(int x, int z)
        {
            int width = Radius * 2 + 1;
            double mean = (sums[z + width, x + width] - sums[z, x + width]
                - sums[z + width, x] + sums[z, x]) / (width * width);
            return Math.Exp(-.24 * Math.Max(0, mean - heights[z + Radius, x + Radius]));
        }
    }
}
