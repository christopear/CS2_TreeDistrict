using Game;
using Colossal;
using Colossal.IO.AssetDatabase;
using Colossal.IO.AssetDatabase.VirtualTexturing;
using Game.Achievements;
using Game.Areas;
using Game.Audio;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Creatures;
using Game.Debug;
using Game.Effects;
using Game.Events;
using Game.Net;
using Game.Notifications;
using Game.Objects;
using Game.PSI;
using Game.Pathfind;
using Game.Policies;
using Game.Prefabs;
using Game.Prefabs.Modes;
using Game.Rendering;
using Game.Routes;
using Game.Serialization;
using Game.Serialization.DataMigration;
using Game.Simulation;
using Game.Tools;
using Game.Triggers;
using Game.Tutorials;
using Game.UI;
using Game.UI.Debug;
using Game.UI.Editor;
using Game.UI.InGame;
using Game.UI.Menu;
using Game.UI.Tooltip;
using Game.Vehicles;
using Game.Zones;
using Unity.Entities;


namespace TreeDistrict {
// Inclusive scheduler spans; no forced completion of worker jobs. Dormant outside placement diagnostics.
internal static partial class NativePlacementTimings { internal static void RegisterRenderingDetails(UpdateSystem update) {
update.UpdateBefore<DetailBatchInstanceSystemGroupsBefore, BatchInstanceSystem.Groups>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailBatchInstanceSystemGroupsAfter, BatchInstanceSystem.Groups>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailInitializeAnimatedSystemBefore, InitializeAnimatedSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailInitializeAnimatedSystemAfter, InitializeAnimatedSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailInitializeBonesSystemBefore, InitializeBonesSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailInitializeBonesSystemAfter, InitializeBonesSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailInitializeBoneHistoriesSystemBefore, InitializeBoneHistoriesSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailInitializeBoneHistoriesSystemAfter, InitializeBoneHistoriesSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailInitializeLightsSystemBefore, InitializeLightsSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailInitializeLightsSystemAfter, InitializeLightsSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailObjectInterpolateSystemBefore, ObjectInterpolateSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailObjectInterpolateSystemAfter, ObjectInterpolateSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailEventInterpolateSystemBefore, EventInterpolateSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailEventInterpolateSystemAfter, EventInterpolateSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailRelativeObjectSystemBefore, RelativeObjectSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailRelativeObjectSystemAfter, RelativeObjectSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailAnimatedSystemPrepareBefore, AnimatedSystem.Prepare>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailAnimatedSystemPrepareAfter, AnimatedSystem.Prepare>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailEffectTransformSystemBefore, EffectTransformSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailEffectTransformSystemAfter, EffectTransformSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailSFXCullingSystemBefore, SFXCullingSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailSFXCullingSystemAfter, SFXCullingSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailLightCullingSystemBefore, LightCullingSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailLightCullingSystemAfter, LightCullingSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailObjectColorSystemBefore, ObjectColorSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailObjectColorSystemAfter, ObjectColorSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailNetColorSystemBefore, NetColorSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailNetColorSystemAfter, NetColorSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailAreaColorSystemBefore, AreaColorSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailAreaColorSystemAfter, AreaColorSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailPhotoModeRenderSystemBefore, PhotoModeRenderSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailPhotoModeRenderSystemAfter, PhotoModeRenderSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailClimateRenderSystemBefore, ClimateRenderSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailClimateRenderSystemAfter, ClimateRenderSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailNotificationIconLocationSystemBefore, NotificationIconLocationSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailNotificationIconLocationSystemAfter, NotificationIconLocationSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailMarkerIconSystemBefore, MarkerIconSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailMarkerIconSystemAfter, MarkerIconSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailNotificationIconBufferSystemBefore, NotificationIconBufferSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailNotificationIconBufferSystemAfter, NotificationIconBufferSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailProceduralSkeletonSystemBefore, ProceduralSkeletonSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailProceduralSkeletonSystemAfter, ProceduralSkeletonSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailProceduralEmissiveSystemBefore, ProceduralEmissiveSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailProceduralEmissiveSystemAfter, ProceduralEmissiveSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailProceduralUploadSystemPrepareBefore, ProceduralUploadSystem.Prepare>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailProceduralUploadSystemPrepareAfter, ProceduralUploadSystem.Prepare>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailAggregateRenderSystemBefore, AggregateRenderSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailAggregateRenderSystemAfter, AggregateRenderSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailRouteRenderSystemBefore, RouteRenderSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailRouteRenderSystemAfter, RouteRenderSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailBuildingLotRenderSystemBefore, BuildingLotRenderSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailBuildingLotRenderSystemAfter, BuildingLotRenderSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailAreaBorderRenderSystemBefore, AreaBorderRenderSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailAreaBorderRenderSystemAfter, AreaBorderRenderSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailGuideLinesSystemBefore, GuideLinesSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailGuideLinesSystemAfter, GuideLinesSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailBrushRenderSystemBefore, BrushRenderSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailBrushRenderSystemAfter, BrushRenderSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailEditorGizmoSystemBefore, EditorGizmoSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailEditorGizmoSystemAfter, EditorGizmoSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailEffectRangeRenderSystemBefore, EffectRangeRenderSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailEffectRangeRenderSystemAfter, EffectRangeRenderSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailBatchDataSystemBefore, BatchDataSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailBatchDataSystemAfter, BatchDataSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailBatchRendererSystemBefore, BatchRendererSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailBatchRendererSystemAfter, BatchRendererSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailAreaRenderSystemBefore, AreaRenderSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailAreaRenderSystemAfter, AreaRenderSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailOverlayRenderSystemBefore, OverlayRenderSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailOverlayRenderSystemAfter, OverlayRenderSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailVFXSystemBefore, VFXSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailVFXSystemAfter, VFXSystem>(SystemUpdatePhase.Rendering);
update.UpdateBefore<DetailProceduralUploadSystemBefore, ProceduralUploadSystem>(SystemUpdatePhase.Rendering);
update.UpdateAfter<DetailProceduralUploadSystemAfter, ProceduralUploadSystem>(SystemUpdatePhase.Rendering);
} }
public sealed partial class DetailBatchInstanceSystemGroupsBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(BatchInstanceSystem.Groups).FullName); }
public sealed partial class DetailBatchInstanceSystemGroupsAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(BatchInstanceSystem.Groups).FullName); }
public sealed partial class DetailInitializeAnimatedSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(InitializeAnimatedSystem).FullName); }
public sealed partial class DetailInitializeAnimatedSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(InitializeAnimatedSystem).FullName); }
public sealed partial class DetailInitializeBonesSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(InitializeBonesSystem).FullName); }
public sealed partial class DetailInitializeBonesSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(InitializeBonesSystem).FullName); }
public sealed partial class DetailInitializeBoneHistoriesSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(InitializeBoneHistoriesSystem).FullName); }
public sealed partial class DetailInitializeBoneHistoriesSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(InitializeBoneHistoriesSystem).FullName); }
public sealed partial class DetailInitializeLightsSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(InitializeLightsSystem).FullName); }
public sealed partial class DetailInitializeLightsSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(InitializeLightsSystem).FullName); }
public sealed partial class DetailObjectInterpolateSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(ObjectInterpolateSystem).FullName); }
public sealed partial class DetailObjectInterpolateSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(ObjectInterpolateSystem).FullName); }
public sealed partial class DetailEventInterpolateSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(EventInterpolateSystem).FullName); }
public sealed partial class DetailEventInterpolateSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(EventInterpolateSystem).FullName); }
public sealed partial class DetailRelativeObjectSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(RelativeObjectSystem).FullName); }
public sealed partial class DetailRelativeObjectSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(RelativeObjectSystem).FullName); }
public sealed partial class DetailAnimatedSystemPrepareBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(AnimatedSystem.Prepare).FullName); }
public sealed partial class DetailAnimatedSystemPrepareAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(AnimatedSystem.Prepare).FullName); }
public sealed partial class DetailEffectTransformSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(EffectTransformSystem).FullName); }
public sealed partial class DetailEffectTransformSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(EffectTransformSystem).FullName); }
public sealed partial class DetailSFXCullingSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(SFXCullingSystem).FullName); }
public sealed partial class DetailSFXCullingSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(SFXCullingSystem).FullName); }
public sealed partial class DetailLightCullingSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(LightCullingSystem).FullName); }
public sealed partial class DetailLightCullingSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(LightCullingSystem).FullName); }
public sealed partial class DetailObjectColorSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(ObjectColorSystem).FullName); }
public sealed partial class DetailObjectColorSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(ObjectColorSystem).FullName); }
public sealed partial class DetailNetColorSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(NetColorSystem).FullName); }
public sealed partial class DetailNetColorSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(NetColorSystem).FullName); }
public sealed partial class DetailAreaColorSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(AreaColorSystem).FullName); }
public sealed partial class DetailAreaColorSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(AreaColorSystem).FullName); }
public sealed partial class DetailPhotoModeRenderSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(PhotoModeRenderSystem).FullName); }
public sealed partial class DetailPhotoModeRenderSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(PhotoModeRenderSystem).FullName); }
public sealed partial class DetailClimateRenderSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(ClimateRenderSystem).FullName); }
public sealed partial class DetailClimateRenderSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(ClimateRenderSystem).FullName); }
public sealed partial class DetailNotificationIconLocationSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(NotificationIconLocationSystem).FullName); }
public sealed partial class DetailNotificationIconLocationSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(NotificationIconLocationSystem).FullName); }
public sealed partial class DetailMarkerIconSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(MarkerIconSystem).FullName); }
public sealed partial class DetailMarkerIconSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(MarkerIconSystem).FullName); }
public sealed partial class DetailNotificationIconBufferSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(NotificationIconBufferSystem).FullName); }
public sealed partial class DetailNotificationIconBufferSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(NotificationIconBufferSystem).FullName); }
public sealed partial class DetailProceduralSkeletonSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(ProceduralSkeletonSystem).FullName); }
public sealed partial class DetailProceduralSkeletonSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(ProceduralSkeletonSystem).FullName); }
public sealed partial class DetailProceduralEmissiveSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(ProceduralEmissiveSystem).FullName); }
public sealed partial class DetailProceduralEmissiveSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(ProceduralEmissiveSystem).FullName); }
public sealed partial class DetailProceduralUploadSystemPrepareBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(ProceduralUploadSystem.Prepare).FullName); }
public sealed partial class DetailProceduralUploadSystemPrepareAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(ProceduralUploadSystem.Prepare).FullName); }
public sealed partial class DetailAggregateRenderSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(AggregateRenderSystem).FullName); }
public sealed partial class DetailAggregateRenderSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(AggregateRenderSystem).FullName); }
public sealed partial class DetailRouteRenderSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(RouteRenderSystem).FullName); }
public sealed partial class DetailRouteRenderSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(RouteRenderSystem).FullName); }
public sealed partial class DetailBuildingLotRenderSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(BuildingLotRenderSystem).FullName); }
public sealed partial class DetailBuildingLotRenderSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(BuildingLotRenderSystem).FullName); }
public sealed partial class DetailAreaBorderRenderSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(AreaBorderRenderSystem).FullName); }
public sealed partial class DetailAreaBorderRenderSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(AreaBorderRenderSystem).FullName); }
public sealed partial class DetailGuideLinesSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(GuideLinesSystem).FullName); }
public sealed partial class DetailGuideLinesSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(GuideLinesSystem).FullName); }
public sealed partial class DetailBrushRenderSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(BrushRenderSystem).FullName); }
public sealed partial class DetailBrushRenderSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(BrushRenderSystem).FullName); }
public sealed partial class DetailEditorGizmoSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(EditorGizmoSystem).FullName); }
public sealed partial class DetailEditorGizmoSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(EditorGizmoSystem).FullName); }
public sealed partial class DetailEffectRangeRenderSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(EffectRangeRenderSystem).FullName); }
public sealed partial class DetailEffectRangeRenderSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(EffectRangeRenderSystem).FullName); }
public sealed partial class DetailBatchDataSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(BatchDataSystem).FullName); }
public sealed partial class DetailBatchDataSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(BatchDataSystem).FullName); }
public sealed partial class DetailBatchRendererSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(BatchRendererSystem).FullName); }
public sealed partial class DetailBatchRendererSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(BatchRendererSystem).FullName); }
public sealed partial class DetailAreaRenderSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(AreaRenderSystem).FullName); }
public sealed partial class DetailAreaRenderSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(AreaRenderSystem).FullName); }
public sealed partial class DetailOverlayRenderSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(OverlayRenderSystem).FullName); }
public sealed partial class DetailOverlayRenderSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(OverlayRenderSystem).FullName); }
public sealed partial class DetailVFXSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(VFXSystem).FullName); }
public sealed partial class DetailVFXSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(VFXSystem).FullName); }
public sealed partial class DetailProceduralUploadSystemBefore : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.Start(typeof(ProceduralUploadSystem).FullName); }
public sealed partial class DetailProceduralUploadSystemAfter : Game.GameSystemBase { protected override void OnUpdate() => NativePlacementTimings.End(typeof(ProceduralUploadSystem).FullName); }
}

