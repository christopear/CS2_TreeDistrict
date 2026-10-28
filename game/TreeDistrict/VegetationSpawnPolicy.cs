using System;

namespace TreeDistrict
{
    // Native TreeState values; kept independent of game assemblies for adapter tests.
    public static class VegetationSpawnPolicy
    {
        public static void ConfigureStampGeneration(TreeDistrict.Core.Foliage.GenerationRequest request)
        {
            // No product population cap; collections and native entity indices use Int32.
            request.MaxPlacements = int.MaxValue;
            // Candidate budget applies to each 198m template, not the tiled area.
            request.MaxCandidates = 1000000;
            request.MaxPositionAttempts = int.MaxValue;
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
