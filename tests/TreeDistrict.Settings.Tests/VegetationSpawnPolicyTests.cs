using System;
using Xunit;

namespace TreeDistrict.Tests
{
    public class VegetationSpawnPolicyTests
    {
        [Theory]
        [InlineData("young", 0)]
        [InlineData("teen", 1)]
        [InlineData("mature", 2)]
        [InlineData("old", 4)]
        [InlineData("static", 2)]
        public void TreeAgesMapToNativeStatesButStaticPlantsNeverReceiveTreeState(string age, int expected)
        {
            Assert.Equal((byte)expected, VegetationSpawnPolicy.TreeStateCode(true, age));
            Assert.Null(VegetationSpawnPolicy.TreeStateCode(false, age));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void UnsupportedAgeFailsBeforeSpawning(bool nativeTree)
        {
            Assert.Throws<ArgumentException>(() => VegetationSpawnPolicy.TreeStateCode(nativeTree, "unknown"));
        }
    }
}
