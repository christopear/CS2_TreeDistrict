using System;

namespace TreeDistrict
{
    // Native TreeState values; kept independent of game assemblies for adapter tests.
    public static class VegetationSpawnPolicy
    {
        public const int MaxOwnedPlants = 1000000;

        public static void ConfigureLargeAreaTest(TreeDistrict.Core.Foliage.GenerationRequest request)
        {
            request.MaxPlacements = MaxOwnedPlants;
            request.MaxCandidates = MaxOwnedPlants;
            request.MaxPositionAttempts = 100000000;
            request.MaxNeighbourChecks = 1000000000L;
        }

        public static byte? TreeStateCode(bool nativeTree, string ageId)
        {
            byte state;
            switch (ageId)
            {
                case "young": state = 0; break;
                case "teen": state = 1; break;
                case "mature":
                case "static": state = 2; break;
                case "old": state = 4; break;
                default: throw new ArgumentException("Unsupported vegetation age: " + ageId);
            }
            // Static shrubs are normal plants, never trees with an invented growth component.
            return nativeTree ? state : (byte?)null;
        }
    }
}
