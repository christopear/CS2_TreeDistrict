using TreeDistrict.Core.Foliage;
using Xunit;

namespace TreeDistrict.Core.Tests
{
    public class PlacementBatchScheduleTests
    {
        [Theory]
        [InlineData(1000000, 3)]
        [InlineData(20000, 0.25)]
        public void CustomBatchSizeAndDelayControlPacing(int size, double delay)
        {
            var schedule = new PlacementBatchSchedule(size + 7, size, delay);
            Assert.Equal(size, schedule.NextCount(0));
            schedule.CompleteBatch(size, 1);
            Assert.Equal(0, schedule.NextCount(1 + delay - 0.001));
            Assert.Equal(7, schedule.NextCount(1 + delay));
            schedule.CompleteBatch(7, 1 + delay);
            Assert.Equal(0, schedule.NextCount(100));
        }
        [Fact]
        public void InvalidSettingsAreRejectedAndZeroDelayIsAllowed()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new PlacementBatchSchedule(1, 0));
            foreach (double delay in new[] { -1d, double.NaN, double.PositiveInfinity })
                Assert.Throws<System.ArgumentOutOfRangeException>(() => new PlacementBatchSchedule(1, 1, delay));
            var schedule = new PlacementBatchSchedule(2, 1, 0);
            schedule.CompleteBatch(1, 0);
            Assert.Equal(1, schedule.NextCount(0));
        }
        [Fact]
        public void PlacesFiftyThousandThenWaitsHalfSecondAfterCompletion()
        {
            var schedule = new PlacementBatchSchedule(120001);
            Assert.Equal(50000, schedule.NextCount(0));
            schedule.CompleteBatch(50000, 0.2);
            Assert.Equal(0, schedule.NextCount(0.699));
            Assert.Equal(50000, schedule.NextCount(0.7));
            schedule.CompleteBatch(50000, 10); // A stall must not cause a catch-up burst.
            Assert.Equal(0, schedule.NextCount(10));
            Assert.Equal(20001, schedule.NextCount(10.5));
            schedule.CompleteBatch(20001, 10.5);
            Assert.Equal(120001, schedule.Completed);
            Assert.Equal(0, schedule.NextCount(100));
        }
        [Fact]
        public void CancellationRetainsProgressAndStopsFutureBatches()
        {
            var schedule = new PlacementBatchSchedule(10000000);
            schedule.CompleteBatch(50000, 0);
            schedule.Cancel();
            Assert.Equal(50000, schedule.Completed);
            Assert.Equal(0, schedule.NextCount(999));
            Assert.Equal(0, new PlacementBatchSchedule(0).NextCount(0));
        }
    }
}
