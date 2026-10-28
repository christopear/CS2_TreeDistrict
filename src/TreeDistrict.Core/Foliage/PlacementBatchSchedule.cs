using System;

namespace TreeDistrict.Core.Foliage
{
    // Wall-clock pacing: never catch up missed intervals with multiple batches.
    public sealed class PlacementBatchSchedule
    {
        public const int DefaultBatchSize = 50000;
        public const double DefaultIntervalSeconds = 0.5;
        public int Total { get; }
        public int Completed { get; private set; }
        public bool Cancelled { get; private set; }
        private double nextBatchAt;
        public PlacementBatchSchedule(int total)
        {
            if (total < 0) throw new ArgumentOutOfRangeException(nameof(total));
            Total = total;
        }
        public int NextCount(double now) => Cancelled || now < nextBatchAt
            ? 0 : Math.Min(DefaultBatchSize, Total - Completed);
        public void CompleteBatch(int count, double now)
        {
            if (count <= 0 || count != NextCount(now)) throw new InvalidOperationException("Invalid placement batch.");
            Completed += count;
            nextBatchAt = now + DefaultIntervalSeconds;
        }
        public void Cancel() => Cancelled = true;
    }
}
