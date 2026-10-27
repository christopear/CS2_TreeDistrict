using System;
using System.Linq;

namespace TreeDistrict.Core.Foliage
{
    /// <summary>Independent local species weights and abundance. No random draws or reservations.</summary>
    internal sealed class LocalVegetationResponse
    {
        private readonly double[] weights;
        private readonly double abundance;
        internal LocalVegetationResponse(double[] weights, double abundance)
        { this.weights = weights; this.abundance = abundance; }

        // Area density scales with inverse squared distance. Zero abundance is barren,
        // and must be rejected by Choose before asking for a spacing radius.
        internal double SpacingMultiplier => 1 / Math.Sqrt(abundance);

        internal int Choose(double unitTicket)
        {
            double total = weights.Sum();
            if (abundance <= 0 || total <= 0) return -2;
            double ticket = unitTicket * total;
            int last = -2;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0) continue;
                last = i;
                if (ticket < weights[i]) return i;
                ticket -= weights[i];
            }
            return last; // floating-point endpoint
        }
    }
}
