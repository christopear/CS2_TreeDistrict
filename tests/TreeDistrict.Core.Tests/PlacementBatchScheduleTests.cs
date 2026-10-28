using TreeDistrict.Core.Foliage;
using Xunit;

namespace TreeDistrict.Core.Tests
{
    public class PlacementBatchScheduleTests
    {
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
