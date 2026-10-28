using System;
using System.Collections.Generic;
using System.Linq;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Newtonsoft.Json.Linq;
using TreeDistrict.Core.Foliage;
using Unity.Entities;
using Unity.Mathematics;

namespace TreeDistrict
{
    public partial class TreeAreaSettingsSystem
    {
        // Area-owned tree AND static-plant references; version-2 save layout remains unchanged.
        // No custom components on plants. A recycled entity index is not ownership.
        private readonly Dictionary<Entity, List<Entity>> _trees = new Dictionary<Entity, List<Entity>>();
        private readonly List<Entity> _orphanTrees = new List<Entity>();
        private PlacementJob _placement;
        private static double PlacementTime => (double)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency;
        private sealed class PlacementJob
        {
            internal Entity Area;
            internal string Id;
            internal int Revision;
            internal IReadOnlyList<Placement> Plan;
            internal Dictionary<string, NativeVegetation> Native;
            internal PlacementBatchSchedule Schedule;
        }
        private void CancelPlacement(Entity area, string reason)
        {
            if (_placement == null || _placement.Area != area) return;
            var job = _placement;
            job.Schedule.Cancel();
            _placement = null; // Release the unplaced plan; keep already placed plants owned.
            if (_areaIds.ContainsKey(area))
            {
                var record = SettingsModel.Target(_state, job.Id);
                record["placementActive"] = false;
                record["generationStatus"] = reason + " " + job.Schedule.Completed + " of " + job.Schedule.Total + " plants placed.";
                _uiDirty = true;
            }
        }
        private bool LivingReference(Entity entity) => entity != Entity.Null && EntityManager.Exists(entity)
            && (EntityManager.HasComponent<Tree>(entity) || EntityManager.HasComponent<Plant>(entity))
            && !EntityManager.HasComponent<Deleted>(entity);
        private void DeleteTrees(IEnumerable<Entity> trees)
        {
            foreach (var tree in trees) if (LivingReference(tree)) EntityManager.AddComponent<Deleted>(tree);
        }
        private void RemoveOwned(Entity area)
        {
            CancelPlacement(area, "Placement stopped.");
            if (_trees.TryGetValue(area, out var trees)) { DeleteTrees(trees); _trees.Remove(area); }
        }
        private void GenerationCommand(string type, JObject command)
        {
            string id = SettingsModel.Text(command["districtId"], 160);
            Entity area = EntityFor(id);
            var record = SettingsModel.Target(_state, id);
            if (type == "generate") { Generate(area, record); return; }
            if (type == "cancelGeneration") { CancelPlacement(area, "Placement cancelled."); return; }
            if (type == "bakeDelete")
            {
                if (command["confirmed"]?.Type != JTokenType.Boolean || !(bool)command["confirmed"])
                    throw new ArgumentException("Confirm Bake + delete first.");
                CancelPlacement(area, "Placement stopped for baking.");
                _trees.Remove(area); // Release ownership BEFORE deleting the wrapper.
            }
            else RemoveOwned(area);
            record["generatedCount"] = 0;
            record["generatedRevision"] = null;
            record["generationStatus"] = "Generated vegetation removed.";
            if (type == "deleteDistrict" || type == "bakeDelete")
            {
                EntityManager.AddComponent<Deleted>(area);
                _areaIds.Remove(area); record.Remove();
                if (_selectedId == id) { _selectedId = null; SelectNative(Entity.Null); }
            }
        }
        private sealed class NativeVegetation
        {
            internal Entity Prefab;
            internal EntityArchetype Archetype;
            internal bool IsTree;
            internal string Name;
        }

        private void Generate(Entity area, JObject record)
        {
            if (_placement != null) throw new ArgumentException("Wait for placement to finish or cancel it before generating another area.");
            // Capture authoritative native nodes immediately before planning, without mutating saved settings.
            var snapshot = (JObject)record.DeepClone();
            var nodes = EntityManager.GetBuffer<Game.Areas.Node>(area, true);
            var boundary = new JArray();
            foreach (var node in nodes) boundary.Add(new JObject { ["x"] = node.m_Position.x, ["z"] = node.m_Position.z });
            if (boundary.Count > 3 && JToken.DeepEquals(boundary[0], boundary[boundary.Count - 1])) boundary.RemoveAt(boundary.Count - 1);
            snapshot["boundary"] = boundary;
            var request = DistrictGenerationAdapter.Create(snapshot, (JObject)_state["city"]);
            VegetationSpawnPolicy.ConfigureStampGeneration(request);
            // No existing-vegetation query: all interaction with manually placed/baked/other-area plants is deferred.
            var native = new Dictionary<string, NativeVegetation>(StringComparer.Ordinal);
            var assets = snapshot["working"]["assets"].ToDictionary(a => (string)a["id"], StringComparer.Ordinal);
            foreach (var species in request.Species)
            {
                var asset = assets[species.Id];
                string prefabName = (string)asset["prefabName"];
                if (!_prefabs.TryGetPrefab(new PrefabID("StaticObjectPrefab", prefabName), out var prefab)
                    || !_prefabs.TryGetEntity(prefab, out var prefabEntity)
                    || !EntityManager.HasComponent<PlantData>(prefabEntity)
                    || !EntityManager.HasComponent<StaticObjectData>(prefabEntity)
                    || !EntityManager.HasComponent<ObjectData>(prefabEntity))
                    throw new ArgumentException("Vegetation prefab is unavailable: " + (string)asset["name"] + " (" + prefabName + "). Existing plants were retained.");
                bool isTree = EntityManager.HasComponent<TreeData>(prefabEntity);
                foreach (var age in species.Ages.Where(a => a.Weight > 0)) VegetationSpawnPolicy.TreeStateCode(isTree, age.Id);
                native.Add(species.Id, new NativeVegetation { Prefab = prefabEntity, IsTree = isTree,
                    Archetype = EntityManager.GetComponentData<ObjectData>(prefabEntity).m_Archetype, Name = (string)asset["name"] });
            }
            var terrain = World.GetOrCreateSystemManaged<TerrainSystem>().GetHeightData(true);
            var water = World.GetOrCreateSystemManaged<WaterSystem>().GetSurfaceData(out var waterDependency);
            waterDependency.Complete(); // Synchronous snapshot read, no reader job remains after this command.
            var planningTimer = System.Diagnostics.Stopwatch.StartNew();
            var result = _stampGenerator.Generate(request, point => {
                var position = new float3((float)point.X, 0, (float)point.Z);
                float height = TerrainUtils.SampleHeight(ref terrain, position);
                // Sample world-space rise over run rather than unscaled heightmap normals.
                float dx = TerrainUtils.SampleHeight(ref terrain, position + new float3(1, 0, 0)) - TerrainUtils.SampleHeight(ref terrain, position - new float3(1, 0, 0));
                float dz = TerrainUtils.SampleHeight(ref terrain, position + new float3(0, 0, 1)) - TerrainUtils.SampleHeight(ref terrain, position - new float3(0, 0, 1));
                double slope = Math.Atan(Math.Sqrt(dx * dx + dz * dz) / 2) * 180 / Math.PI;
                // Ignore centimetre-scale surface noise when excluding submerged ground.
                return new GroundSample(height, slope, isWater: WaterUtils.SampleDepth(ref water, position) > 0.01f);
            }, _scatterSettings, DistrictGenerationAdapter.StampSettingsKey(request, _scatterSettings));
            planningTimer.Stop();
            var plan = result.Placements;
            RemoveOwned(area);
            _trees[area] = new List<Entity>();
            record["generatedCount"] = 0;
            record["generatedRevision"] = plan.Count == 0 ? record["revision"].DeepClone() : JValue.CreateNull();
            record["placementActive"] = plan.Count > 0;
            record["generationStatus"] = "Placing 0 of " + plan.Count + " plants (50,000 per batch, 0.5 seconds between batches).";
            if (plan.Count > 0)
                _placement = new PlacementJob { Area = area, Id = (string)record["id"], Revision = (int)record["revision"],
                    Plan = plan, Native = native, Schedule = new PlacementBatchSchedule(plan.Count) };
            else record["generationStatus"] = "Placed 0 plants.";
            Mod.Log.Info("Planned " + plan.Count + " plants in " + planningTimer.Elapsed.TotalMilliseconds.ToString("F1")
                + "ms; stamp cache=" + (_stampGenerator.LastCacheHit ? "hit" : "miss") + "; placement batches=50000, interval=0.5s");
        }

        // Called once per MainLoop, before native initialization. Never catch up multiple batches.
        private void ProcessPlacement()
        {
            var job = _placement;
            if (job == null) return;
            if (!IsTreeArea(job.Area)) { RemoveOwned(job.Area); return; }
            int batchCount = job.Schedule.NextCount(PlacementTime);
            if (batchCount == 0) return;
            var created = new List<Entity>(batchCount);
            try
            {
                for (int i = 0; i < batchCount; i++)
                {
                    var placement = job.Plan[job.Schedule.Completed + i];
                    var resolved = job.Native[placement.SpeciesId];
                    Entity plant = EntityManager.CreateEntity(archetype: resolved.Archetype); created.Add(plant);
                    if (!EntityManager.HasComponent<Plant>(plant) || EntityManager.HasComponent<Tree>(plant) != resolved.IsTree)
                        throw new InvalidOperationException("Unexpected native vegetation archetype for " + resolved.Name);
                    EntityManager.SetComponentData(plant, new PrefabRef(resolved.Prefab));
                    EntityManager.SetComponentData(plant, new Transform(new float3((float)placement.Position.X, (float)placement.ElevationM, (float)placement.Position.Z), quaternion.RotateY((float)placement.RotationRadians)));
                    var state = VegetationSpawnPolicy.TreeStateCode(resolved.IsTree, placement.AgeId);
                    if (state.HasValue) EntityManager.SetComponentData(plant, new Tree { m_State = (TreeState)state.Value, m_Growth = 0 });
                    if (EntityManager.HasComponent<PseudoRandomSeed>(plant))
                        EntityManager.SetComponentData(plant, new PseudoRandomSeed((ushort)(((long)job.Schedule.Completed + created.Count) * 7919 % 65535 + 1)));
                }
            }
            catch (Exception ex)
            {
                // These entities have not left this update; roll back an incomplete creation batch.
                foreach (var tree in created) if (EntityManager.Exists(tree)) EntityManager.DestroyEntity(tree);
                CancelPlacement(job.Area, "Placement failed; completed batches retained.");
                _error = ex.Message;
                Mod.Log.Warn("Placement batch failed: " + ex);
                return;
            }
            _trees[job.Area].AddRange(created);
            job.Schedule.CompleteBatch(batchCount, PlacementTime);
            // Settings edits can replace _state; resolve the current record for every batch.
            var current = SettingsModel.Target(_state, job.Id);
            current["generatedCount"] = job.Schedule.Completed;
            bool finished = job.Schedule.Completed == job.Schedule.Total;
            current["placementActive"] = !finished;
            current["generationStatus"] = (finished ? "Placed " : "Placing ") + job.Schedule.Completed + " of " + job.Schedule.Total + " plants.";
            if (finished)
            {
                current["generatedRevision"] = job.Revision;
                _placement = null;
                Mod.Log.Info("Finished placing " + job.Schedule.Completed + " plants for area " + job.Id);
            }
            _uiDirty = true;
        }
    }
}
