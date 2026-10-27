using System;
using System.Threading;

namespace TreeDistrict.Core.Foliage
{
    /// <summary>Prepared solver inputs, reusable for diagnostics and alternate sampling settings.
    /// Request/profiles and the terrain snapshot must remain stable during generation.</summary>
    public sealed class FoliageGenerationCoordinator
    {
        private readonly GenerationRequest request;
        private readonly Func<Position2, GroundSample> terrain;
        private readonly IVegetationSuitabilityEvaluator evaluator;
        private readonly PlacementEnvironmentProvider provider;
        public FoliageGenerationCoordinator(GenerationRequest request, Func<Position2, GroundSample> terrain,
            double cellSizeM = LocalEnvironmentGrid.DefaultCellSizeM, int maxEnvironmentSamples = 1000000,
            IVegetationSuitabilityEvaluator? evaluator = null, CancellationToken cancellationToken = default)
        {
            this.request = request ?? throw new ArgumentNullException(nameof(request));
            this.terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            FoliageGenerator.Validate(request);
            this.evaluator = evaluator ?? new VegetationSuitabilityEvaluator();
            Environment = LocalEnvironmentGrid.FromTerrain(request.Boundary, terrain, request.Climate,
                cellSizeM, maxEnvironmentSamples, cancellationToken);
            provider = new PlacementEnvironmentProvider(terrain, request.Climate, Environment);
        }
        public LocalEnvironmentGrid Environment { get; }
        public SuitabilityResult Evaluate(SpeciesProfile profile, Position2 position) => evaluator.Evaluate(profile,
            provider.Sample(position), request.Terrain);
        public GenerationResult Generate(ProtonScatterSettings settings, CancellationToken cancellationToken = default) =>
            new ProtonFoliageGenerator(evaluator).Generate(request, terrain, settings, cancellationToken, Environment);
    }
}
