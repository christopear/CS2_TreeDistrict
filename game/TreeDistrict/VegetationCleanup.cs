using System;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.Tools;
using Newtonsoft.Json.Linq;
using Unity.Collections;
using Unity.Entities;

namespace TreeDistrict
{
    public partial class TreeAreaSettingsSystem
    {
        private NativeArray<Entity> _cleanupPlants;
        private int _cleanupIndex;
        private double _cleanupNext;
        private string _cleanupStatus = "";
        private void ResetCleanup()
        {
            if (_cleanupPlants.IsCreated) _cleanupPlants.Dispose();
            _cleanupIndex = 0; _cleanupStatus = "";
        }
        private void StartCleanup(JObject command)
        {
            if (command["confirmed"]?.Type != JTokenType.Boolean || !(bool)command["confirmed"])
                throw new ArgumentException("Confirm deletion of all vegetation and Tree/Canopy Areas first.");
            if (_cleanupPlants.IsCreated) throw new ArgumentException("Cleanup is already running.");
            if (_placement != null) CancelPlacement(_placement.Area, "Placement stopped for map cleanup.");
            using (var query = EntityManager.CreateEntityQuery(new EntityQueryDesc {
                Any = new[] { ComponentType.ReadOnly<Tree>(), ComponentType.ReadOnly<Plant>() },
                None = new[] { ComponentType.ReadOnly<Prefab>(), ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Deleted>() }
            })) _cleanupPlants = query.ToEntityArray(Allocator.Persistent);
            // Release ownership before deleting wrappers; vegetation is handled by the batched snapshot.
            _trees.Clear(); _orphanTrees.Clear(); _areaIds.Clear();
            ((JArray)_state["districts"]).Clear();
            _selectedId = null; SelectNative(Entity.Null);
            using (var areas = _areas.ToEntityArray(Allocator.Temp))
                foreach (var area in areas)
                {
                    var prefab = EntityManager.GetComponentData<PrefabRef>(area).m_Prefab;
                    if (prefab == _prefabs.GetEntity(Mod.Area) || prefab == _prefabs.GetEntity(Mod.CanopyArea))
                        EntityManager.AddComponent<Deleted>(area);
                }
            _cleanupIndex = 0; _cleanupNext = 0; _visible = true;
            _cleanupStatus = "Removing " + _cleanupPlants.Length + " trees and shrubs. Do not save until cleanup finishes.";
            _uiDirty = true;
            Mod.Log.Info("Vegetation cleanup started: " + _cleanupPlants.Length + " plants; all Tree/Canopy wrappers removed.");
        }
        private void ProcessCleanup()
        {
            if (!_cleanupPlants.IsCreated || PlacementTime < _cleanupNext) return;
            int end = Math.Min(_cleanupIndex + 50000, _cleanupPlants.Length);
            using (var batch = new NativeList<Entity>(end - _cleanupIndex, Allocator.Temp))
            {
                for (; _cleanupIndex < end; _cleanupIndex++)
                {
                    var entity = _cleanupPlants[_cleanupIndex];
                    if (LivingReference(entity)) batch.Add(entity);
                }
                if (batch.Length > 0) EntityManager.AddComponent<Deleted>(batch.AsArray());
            }
            _cleanupStatus = "Cleanup: " + _cleanupIndex + " / " + _cleanupPlants.Length + " vegetation references processed.";
            if (_cleanupIndex == _cleanupPlants.Length)
            {
                _cleanupPlants.Dispose();
                _cleanupStatus = "Cleanup complete: all targeted trees, shrubs and Tree/Canopy Areas marked for native deletion. Let the game settle, then save a new copy.";
                Mod.Log.Info(_cleanupStatus);
            }
            _cleanupNext = PlacementTime + .5; _uiDirty = true;
        }
        protected override void OnDestroy() { ResetCleanup(); base.OnDestroy(); }
    }
}
