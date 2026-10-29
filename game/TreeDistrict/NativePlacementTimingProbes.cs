using Game;
namespace TreeDistrict {
 internal static partial class NativePlacementTimings {
 internal static void Register(UpdateSystem update) {
update.UpdateBefore<ProbeModificationBefore, Game.Common.ModificationSystem>(SystemUpdatePhase.MainLoop);
update.UpdateAfter<ProbeModificationAfter, Game.Common.ModificationSystem>(SystemUpdatePhase.MainLoop);
update.UpdateBefore<ProbeToolBefore, Game.Tools.ToolSystem>(SystemUpdatePhase.MainLoop);
update.UpdateAfter<ProbeToolAfter, Game.Tools.ToolSystem>(SystemUpdatePhase.MainLoop);
update.UpdateBefore<ProbePreRenderBefore, Game.Rendering.PreRenderSystem>(SystemUpdatePhase.MainLoop);
update.UpdateAfter<ProbePreRenderAfter, Game.Rendering.PreRenderSystem>(SystemUpdatePhase.MainLoop);
update.UpdateBefore<ProbeRenderingBefore, Game.Rendering.RenderingSystem>(SystemUpdatePhase.MainLoop);
update.UpdateAfter<ProbeRenderingAfter, Game.Rendering.RenderingSystem>(SystemUpdatePhase.MainLoop);
update.UpdateBefore<ProbeUIBefore, Game.UI.UIUpdateSystem>(SystemUpdatePhase.MainLoop);
update.UpdateAfter<ProbeUIAfter, Game.UI.UIUpdateSystem>(SystemUpdatePhase.MainLoop);
update.UpdateBefore<ProbeEndFrameBefore, Game.EndFrameBarrier>(SystemUpdatePhase.MainLoop);
update.UpdateAfter<ProbeEndFrameAfter, Game.EndFrameBarrier>(SystemUpdatePhase.MainLoop);
update.UpdateBefore<ProbeSimulationBefore, Game.Simulation.SimulationSystem>(SystemUpdatePhase.LateUpdate);
update.UpdateAfter<ProbeSimulationAfter, Game.Simulation.SimulationSystem>(SystemUpdatePhase.LateUpdate);
update.UpdateBefore<ProbeCompleteRenderingBefore, Game.Rendering.CompleteRenderingSystem>(SystemUpdatePhase.LateUpdate);
update.UpdateAfter<ProbeCompleteRenderingAfter, Game.Rendering.CompleteRenderingSystem>(SystemUpdatePhase.LateUpdate);
update.UpdateBefore<ProbeObjectCollectBefore, Game.Objects.UpdateCollectSystem>(SystemUpdatePhase.Modification5);
update.UpdateAfter<ProbeObjectCollectAfter, Game.Objects.UpdateCollectSystem>(SystemUpdatePhase.Modification5);
update.UpdateBefore<ProbeObjectSearchBefore, Game.Objects.SearchSystem>(SystemUpdatePhase.Modification5);
update.UpdateAfter<ProbeObjectSearchAfter, Game.Objects.SearchSystem>(SystemUpdatePhase.Modification5);
update.UpdateBefore<ProbeObjectOverrideBefore, Game.Objects.OverrideSystem>(SystemUpdatePhase.Modification5);
update.UpdateAfter<ProbeObjectOverrideAfter, Game.Objects.OverrideSystem>(SystemUpdatePhase.Modification5);
update.UpdateBefore<ProbeBatchInstanceBefore, Game.Rendering.BatchInstanceSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<ProbeBatchInstanceAfter, Game.Rendering.BatchInstanceSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<ProbeBatchManagerBefore, Game.Rendering.BatchManagerSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<ProbeBatchManagerAfter, Game.Rendering.BatchManagerSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<ProbeManagedBatchBefore, Game.Rendering.ManagedBatchSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<ProbeManagedBatchAfter, Game.Rendering.ManagedBatchSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<ProbeVegetationRenderBefore, Game.Rendering.VegetationRenderSystem>(SystemUpdatePhase.PreCulling);
update.UpdateAfter<ProbeVegetationRenderAfter, Game.Rendering.VegetationRenderSystem>(SystemUpdatePhase.PreCulling);
update.UpdateBefore<ProbeBatchUploadBefore, Game.Rendering.BatchUploadSystem>(SystemUpdatePhase.Cleanup);
update.UpdateAfter<ProbeBatchUploadAfter, Game.Rendering.BatchUploadSystem>(SystemUpdatePhase.Cleanup);
update.UpdateBefore<ProbeBarrier1Before, Game.Common.ModificationBarrier1>(SystemUpdatePhase.Modification1);
update.UpdateAfter<ProbeBarrier1After, Game.Common.ModificationBarrier1>(SystemUpdatePhase.Modification1);
update.UpdateBefore<ProbeBarrier2Before, Game.Common.ModificationBarrier2>(SystemUpdatePhase.Modification2);
update.UpdateAfter<ProbeBarrier2After, Game.Common.ModificationBarrier2>(SystemUpdatePhase.Modification2);
update.UpdateBefore<ProbeBarrier2BBefore, Game.Common.ModificationBarrier2B>(SystemUpdatePhase.Modification2B);
update.UpdateAfter<ProbeBarrier2BAfter, Game.Common.ModificationBarrier2B>(SystemUpdatePhase.Modification2B);
update.UpdateBefore<ProbeBarrier3Before, Game.Common.ModificationBarrier3>(SystemUpdatePhase.Modification3);
update.UpdateAfter<ProbeBarrier3After, Game.Common.ModificationBarrier3>(SystemUpdatePhase.Modification3);
update.UpdateBefore<ProbeBarrier4Before, Game.Common.ModificationBarrier4>(SystemUpdatePhase.Modification4);
update.UpdateAfter<ProbeBarrier4After, Game.Common.ModificationBarrier4>(SystemUpdatePhase.Modification4);
update.UpdateBefore<ProbeBarrier4BBefore, Game.Common.ModificationBarrier4B>(SystemUpdatePhase.Modification4B);
update.UpdateAfter<ProbeBarrier4BAfter, Game.Common.ModificationBarrier4B>(SystemUpdatePhase.Modification4B);
update.UpdateBefore<ProbeBarrier5Before, Game.Common.ModificationBarrier5>(SystemUpdatePhase.Modification5);
update.UpdateAfter<ProbeBarrier5After, Game.Common.ModificationBarrier5>(SystemUpdatePhase.Modification5);
}
}
public sealed partial class ProbeModificationBefore : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start("Game.Common.ModificationSystem"); }
public sealed partial class ProbeModificationAfter : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End("Game.Common.ModificationSystem"); }
public sealed partial class ProbeToolBefore : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start("Game.Tools.ToolSystem"); }
public sealed partial class ProbeToolAfter : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End("Game.Tools.ToolSystem"); }
public sealed partial class ProbePreRenderBefore : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start("Game.Rendering.PreRenderSystem"); }
public sealed partial class ProbePreRenderAfter : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End("Game.Rendering.PreRenderSystem"); }
public sealed partial class ProbeRenderingBefore : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start("Game.Rendering.RenderingSystem"); }
public sealed partial class ProbeRenderingAfter : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End("Game.Rendering.RenderingSystem"); }
public sealed partial class ProbeUIBefore : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start("Game.UI.UIUpdateSystem"); }
public sealed partial class ProbeUIAfter : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End("Game.UI.UIUpdateSystem"); }
public sealed partial class ProbeEndFrameBefore : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start("Game.EndFrameBarrier"); }
public sealed partial class ProbeEndFrameAfter : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End("Game.EndFrameBarrier"); }
public sealed partial class ProbeSimulationBefore : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start("Game.Simulation.SimulationSystem"); }
public sealed partial class ProbeSimulationAfter : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End("Game.Simulation.SimulationSystem"); }
public sealed partial class ProbeCompleteRenderingBefore : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start("Game.Rendering.CompleteRenderingSystem"); }
public sealed partial class ProbeCompleteRenderingAfter : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End("Game.Rendering.CompleteRenderingSystem"); }
public sealed partial class ProbeObjectCollectBefore : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start("Game.Objects.UpdateCollectSystem"); }
public sealed partial class ProbeObjectCollectAfter : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End("Game.Objects.UpdateCollectSystem"); }
public sealed partial class ProbeObjectSearchBefore : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start("Game.Objects.SearchSystem"); }
public sealed partial class ProbeObjectSearchAfter : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End("Game.Objects.SearchSystem"); }
public sealed partial class ProbeObjectOverrideBefore : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start("Game.Objects.OverrideSystem"); }
public sealed partial class ProbeObjectOverrideAfter : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End("Game.Objects.OverrideSystem"); }
public sealed partial class ProbeBatchInstanceBefore : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start("Game.Rendering.BatchInstanceSystem"); }
public sealed partial class ProbeBatchInstanceAfter : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End("Game.Rendering.BatchInstanceSystem"); }
public sealed partial class ProbeBatchManagerBefore : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start("Game.Rendering.BatchManagerSystem"); }
public sealed partial class ProbeBatchManagerAfter : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End("Game.Rendering.BatchManagerSystem"); }
public sealed partial class ProbeManagedBatchBefore : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start("Game.Rendering.ManagedBatchSystem"); }
public sealed partial class ProbeManagedBatchAfter : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End("Game.Rendering.ManagedBatchSystem"); }
public sealed partial class ProbeVegetationRenderBefore : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start("Game.Rendering.VegetationRenderSystem"); }
public sealed partial class ProbeVegetationRenderAfter : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End("Game.Rendering.VegetationRenderSystem"); }
public sealed partial class ProbeBatchUploadBefore : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start("Game.Rendering.BatchUploadSystem"); }
public sealed partial class ProbeBatchUploadAfter : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End("Game.Rendering.BatchUploadSystem"); }
public sealed partial class ProbeBarrier1Before : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start("Game.Common.ModificationBarrier1"); }
public sealed partial class ProbeBarrier1After : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End("Game.Common.ModificationBarrier1"); }
public sealed partial class ProbeBarrier2Before : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start("Game.Common.ModificationBarrier2"); }
public sealed partial class ProbeBarrier2After : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End("Game.Common.ModificationBarrier2"); }
public sealed partial class ProbeBarrier2BBefore : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start("Game.Common.ModificationBarrier2B"); }
public sealed partial class ProbeBarrier2BAfter : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End("Game.Common.ModificationBarrier2B"); }
public sealed partial class ProbeBarrier3Before : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start("Game.Common.ModificationBarrier3"); }
public sealed partial class ProbeBarrier3After : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End("Game.Common.ModificationBarrier3"); }
public sealed partial class ProbeBarrier4Before : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start("Game.Common.ModificationBarrier4"); }
public sealed partial class ProbeBarrier4After : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End("Game.Common.ModificationBarrier4"); }
public sealed partial class ProbeBarrier4BBefore : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start("Game.Common.ModificationBarrier4B"); }
public sealed partial class ProbeBarrier4BAfter : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End("Game.Common.ModificationBarrier4B"); }
public sealed partial class ProbeBarrier5Before : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start("Game.Common.ModificationBarrier5"); }
public sealed partial class ProbeBarrier5After : GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End("Game.Common.ModificationBarrier5"); }
}
