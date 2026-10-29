using System;

namespace TreeDistrict.Core.Foliage
{
    // Wall-clock pacing: never catch up missed intervals with multiple batches.
    public sealed class PlacementBatchSchedule
    {
        public const int DefaultBatchSize = 50000;
        public const double DefaultIntervalSeconds = 0.5;
        public int Total { get; }
        public int BatchSize { get; }
        public double IntervalSeconds { get; }
        public int Completed { get; private set; }
        public bool Cancelled { get; private set; }
        private double nextBatchAt;
        public PlacementBatchSchedule(int total, int batchSize = DefaultBatchSize, double intervalSeconds = DefaultIntervalSeconds)
        {
            if (total < 0) throw new ArgumentOutOfRangeException(nameof(total));
            if (batchSize < 1) throw new ArgumentOutOfRangeException(nameof(batchSize));
            if (double.IsNaN(intervalSeconds) || double.IsInfinity(intervalSeconds) || intervalSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
            Total = total;
            BatchSize = batchSize;
            IntervalSeconds = intervalSeconds;
        }
        public int NextCount(double now) => Cancelled || now < nextBatchAt
            ? 0 : Math.Min(BatchSize, Total - Completed);
        public void CompleteBatch(int count, double now)
        {
            if (count <= 0 || count != NextCount(now)) throw new InvalidOperationException("Invalid placement batch.");
            Completed += count;
            nextBatchAt = now + IntervalSeconds;
        }
        public void Cancel() => Cancelled = true;
    }
}
