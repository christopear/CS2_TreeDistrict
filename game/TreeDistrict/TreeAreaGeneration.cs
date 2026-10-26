using System;
using System.Collections.Generic;
using System.Linq;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Newtonsoft.Json.Linq;
using Unity.Entities;
using Unity.Mathematics;

namespace TreeDistrict
{
    public partial class TreeAreaSettingsSystem
    {
        // No custom components on trees. Entity includes its version; a recycled index is not ownership.
        private readonly Dictionary<Entity, List<Entity>> _trees = new Dictionary<Entity, List<Entity>>();
        private readonly List<Entity> _orphanTrees = new List<Entity>();
        private bool LivingReference(Entity entity) => entity != Entity.Null && EntityManager.Exists(entity)
            && EntityManager.HasComponent<Tree>(entity) && !EntityManager.HasComponent<Deleted>(entity);
        private void DeleteTrees(IEnumerable<Entity> trees)
        {
            foreach (var tree in trees) if (LivingReference(tree)) EntityManager.AddComponent<Deleted>(tree);
        }
        private void RemoveOwned(Entity area)
        {
            if (_trees.TryGetValue(area, out var trees)) { DeleteTrees(trees); _trees.Remove(area); }
        }
        private void RefreshTreeCounts()
        {
            DeleteTrees(_orphanTrees); _orphanTrees.Clear();
            foreach (var entry in _trees)
            {
                entry.Value.RemoveAll(e => !LivingReference(e));
                if (_areaIds.TryGetValue(entry.Key, out var id)) SettingsModel.Target(_state, id)["generatedCount"] = entry.Value.Count;
            }
        }
        private void GenerationCommand(string type, JObject command)
        {
            string id = SettingsModel.Text(command["districtId"], 160);
            Entity area = EntityFor(id);
            var record = SettingsModel.Target(_state, id);
            if (type == "generate") { Generate(area, record); return; }
            if (type == "bakeDelete")
            {
                if (command["confirmed"]?.Type != JTokenType.Boolean || !(bool)command["confirmed"])
                    throw new ArgumentException("Confirm Bake + delete first.");
                _trees.Remove(area); // Release ownership BEFORE deleting the wrapper.
            }
            else RemoveOwned(area);
            record["generatedCount"] = 0;
            record["generatedRevision"] = null;
            record["generationStatus"] = "Generated trees removed.";
            if (type == "deleteDistrict" || type == "bakeDelete")
            {
                EntityManager.AddComponent<Deleted>(area);
                _areaIds.Remove(area); record.Remove();
                if (_selectedId == id) { _selectedId = null; _selection.SetSelection(Entity.Null); _tools.selected = Entity.Null; }
            }
        }
        private void Generate(Entity area, JObject record)
        {
            var set = (JObject)record["working"];
            SettingsModel.ValidateSet(set);
            string prefabName = (string)_catalog["assets"][0]["prefabName"];
            if (!_prefabs.TryGetPrefab(new PrefabID("StaticObjectPrefab", prefabName), out var prefab)
                || !_prefabs.TryGetEntity(prefab, out var prefabEntity)
                || !EntityManager.HasComponent<TreeData>(prefabEntity) || !EntityManager.HasComponent<ObjectData>(prefabEntity))
                throw new ArgumentException("London Plane prefab is unavailable: " + prefabName + ". Existing trees were retained.");
            var nodes = EntityManager.GetBuffer<Game.Areas.Node>(area, true);
            var boundary = new List<PlacementPlanner.Point>();
            foreach (var node in nodes) boundary.Add(new PlacementPlanner.Point(node.m_Position.x, node.m_Position.z));
            var terrain = World.GetOrCreateSystemManaged<TerrainSystem>().GetHeightData(true);
            var plan = PlacementPlanner.Plan(boundary, set, (JObject)_state["city"], (int)record["seed"], (x, z) => {
                var position = new float3((float)x, 0, (float)z);
                float height = TerrainUtils.SampleHeight(ref terrain, position);
                // Sample world-space rise over run rather than unscaled heightmap normals.
                float dx = TerrainUtils.SampleHeight(ref terrain, position + new float3(1, 0, 0)) - TerrainUtils.SampleHeight(ref terrain, position - new float3(1, 0, 0));
                float dz = TerrainUtils.SampleHeight(ref terrain, position + new float3(0, 0, 1)) - TerrainUtils.SampleHeight(ref terrain, position - new float3(0, 0, 1));
                return new PlacementPlanner.Ground { Height = height, Slope = Math.Atan(Math.Sqrt(dx * dx + dz * dz) / 2) * 180 / Math.PI };
            });
            var created = new List<Entity>(plan.Count);
            var archetype = EntityManager.GetComponentData<ObjectData>(prefabEntity).m_Archetype;
            try
            {
                foreach (var placement in plan)
                {
                    Entity tree = EntityManager.CreateEntity(archetype: archetype); created.Add(tree);
                    EntityManager.SetComponentData(tree, new PrefabRef(prefabEntity));
                    EntityManager.SetComponentData(tree, new Transform(new float3((float)placement.X, (float)placement.Y, (float)placement.Z), quaternion.RotateY((float)placement.Rotation)));
                    TreeState age = placement.Age == "young" ? (TreeState)0 : placement.Age == "teen" ? TreeState.Teen : placement.Age == "mature" ? TreeState.Adult : TreeState.Elderly;
                    EntityManager.SetComponentData(tree, new Tree { m_State = age, m_Growth = 0 });
                    if (EntityManager.HasComponent<PseudoRandomSeed>(tree))
                        EntityManager.SetComponentData(tree, new PseudoRandomSeed((ushort)(created.Count * 7919 % 65535 + 1)));
                }
            }
            catch
            {
                // These entities have not left this update; roll back an incomplete creation batch.
                foreach (var tree in created) if (EntityManager.Exists(tree)) EntityManager.DestroyEntity(tree);
                throw;
            }
            RemoveOwned(area);
            _trees[area] = created;
            record["generatedCount"] = created.Count;
            record["generatedRevision"] = record["revision"].DeepClone();
            record["generationStatus"] = "Placed " + created.Count + " London Plane trees. Aging is controlled by the game.";
            Mod.Log.Info("Generated " + created.Count + " London Plane trees for area " + (string)record["id"]);
        }
    }
}
