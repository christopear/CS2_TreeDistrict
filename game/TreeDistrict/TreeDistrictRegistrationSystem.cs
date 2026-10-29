using Game;
using Colossal.Serialization.Entities;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;

namespace TreeDistrict
{
    // Discover the installed game's category instead of hardcoding its ID.
    public partial class TreeDistrictRegistrationSystem : GameSystemBase
    {
        private PrefabSystem _prefabs;
        private EntityQuery _districtPrefabs;
        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            // Native UI group buffers are rebuilt during city loading.
            Enabled = true;
            OnUpdate();
            foreach (var areaPrefab in new[] { Mod.Area, Mod.CanopyArea })
            if (areaPrefab != null && _prefabs.TryGetEntity(areaPrefab, out var areaEntity)
                && EntityManager.HasComponent<AreaGeometryData>(areaEntity))
            {
                // Native districts use this flag to permit boundaries crossing water.
                // Tree placement still filters submerged ground independently.
                var geometry = EntityManager.GetComponentData<AreaGeometryData>(areaEntity);
                if ((mode & GameMode.Editor) != 0)
                    geometry.m_Flags |= Game.Areas.GeometryFlags.OnWaterSurface;
                else
                    geometry.m_Flags &= ~Game.Areas.GeometryFlags.OnWaterSurface;
                EntityManager.SetComponentData(areaEntity, geometry);
            }
        }
        protected override void OnCreate()
        {
            base.OnCreate();
            _prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
            _districtPrefabs = GetEntityQuery(ComponentType.ReadOnly<DistrictData>(), ComponentType.ReadOnly<PrefabData>());
        }
        protected override void OnUpdate()
        {
            if (Mod.Area == null) return;
            using (var entities = _districtPrefabs.ToEntityArray(Allocator.Temp))
            {
                foreach (var entity in entities)
                {
                    var district = _prefabs.GetPrefab<DistrictPrefab>(entity);
                    if (district == null || !district.TryGet<UIObject>(out var source) || source.m_Group == null) continue;
                    var ui = Mod.Area.GetComponent<UIObject>();
                    ui.m_Group = source.m_Group;
                    ui.m_Priority = source.m_Priority + 1;
                    ui.m_Icon = "coui://treedistrict/TreeArea.svg";
                    var areaEntity = _prefabs.GetEntity(Mod.Area);
                    var groupEntity = _prefabs.GetEntity(source.m_Group);
                    EntityManager.SetComponentData(areaEntity, new UIObjectData
                    {
                        m_Group = groupEntity,
                        m_Priority = ui.m_Priority
                    });
                    var elements = EntityManager.GetBuffer<UIGroupElement>(groupEntity);
                    bool present = false;
                    foreach (var element in elements)
                        if (element.m_Prefab == areaEntity) present = true;
                    if (!present) source.m_Group.AddElement(EntityManager, areaEntity);
                    var canopyUi = Mod.CanopyArea.GetComponent<UIObject>();
                    canopyUi.m_Group = source.m_Group; canopyUi.m_Priority = ui.m_Priority + 1;
                    var canopyEntity = _prefabs.GetEntity(Mod.CanopyArea);
                    EntityManager.SetComponentData(canopyEntity, new UIObjectData {
                        m_Group = groupEntity, m_Priority = canopyUi.m_Priority
                    });
                    bool canopyPresent = false;
                    // AddElement can invalidate an existing DynamicBuffer; reacquire it.
                    var canopyElements = EntityManager.GetBuffer<UIGroupElement>(groupEntity);
                    foreach (var element in canopyElements) if (element.m_Prefab == canopyEntity) canopyPresent = true;
                    if (!canopyPresent) source.m_Group.AddElement(EntityManager, canopyEntity);
                    Mod.Log.Info("Tree district added to native area menu: " + source.m_Group.name);
                    Enabled = false;
                    return;
                }
            }
        }
    }
}
