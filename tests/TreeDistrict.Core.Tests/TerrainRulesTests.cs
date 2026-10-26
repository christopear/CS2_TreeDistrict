namespace TreeDistrict.Core.Tests
{
    public sealed class TerrainRulesTests
    {
        private readonly TerrainRules rules = new TerrainRules(-20, 500, 35);

        [Theory]
        [InlineData(-20, 0)]
        [InlineData(500, 35)]
        [InlineData(100, 10)]
        public void AcceptsLandIncludingExactLimits(double elevation, double slope)
        {
            Assert.True(rules.Allows(elevation, slope, false, false));
        }

        [Theory]
        [InlineData(-20.01, 0)]
        [InlineData(500.01, 0)]
        [InlineData(100, 35.01)]
        [InlineData(100, -1)]
        [InlineData(double.NaN, 0)]
        [InlineData(double.PositiveInfinity, 0)]
        [InlineData(double.NegativeInfinity, 0)]
        [InlineData(100, double.NaN)]
        [InlineData(100, double.PositiveInfinity)]
        public void RejectsUnsuitableOrInvalidTerrain(double elevation, double slope)
        {
            Assert.False(rules.Allows(elevation, slope, false, false));
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void ExcludesWaterAndObstacles(bool water, bool blocked)
        {
            Assert.False(rules.Allows(100, 10, water, blocked));
        }

        [Theory]
        [InlineData(10, 0, 30)]
        [InlineData(double.NaN, 100, 30)]
        [InlineData(0, double.PositiveInfinity, 30)]
        [InlineData(0, 100, -1)]
        [InlineData(0, 100, 91)]
        [InlineData(0, 100, double.NaN)]
        public void RejectsInvalidConfiguration(double minimum, double maximum, double slope)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TerrainRules(minimum, maximum, slope));
        }
    }
}
