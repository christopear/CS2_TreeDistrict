using System;
using System.Collections.Generic;
using System.Diagnostics;
using Game;
using Game.Common;
using Game.Notifications;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;

namespace TreeDistrict
{
    // Read native preview icons, not validation queues or generated vegetation.
    // Icon priority is the displayed severity, not the original ErrorSeverity enum.
    public sealed partial class AreaOverlapDiagnosticsSystem : GameSystemBase
    {
        private ToolSystem tools;
        private PrefabSystem prefabs;
        private EntityQuery icons;
        private double nextSample, nextSummary;
        private string lastContext;
        private readonly HashSet<string> seen = new HashSet<string>();

        protected override void OnCreate()
        {
            base.OnCreate();
            tools = World.GetOrCreateSystemManaged<ToolSystem>();
            prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
            icons = GetEntityQuery(ComponentType.ReadOnly<Icon>(), ComponentType.ReadOnly<Temp>(),
                ComponentType.ReadOnly<PrefabRef>(), ComponentType.Exclude<Deleted>());
        }

        protected override void OnUpdate()
        {
            double now = (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
            if (now < nextSample) return;
            nextSample = now + 1;
            var tool = tools.activeTool;
            if (!(tool is AreaToolSystem) && !(tool is NetToolSystem) && !(tool is ObjectToolSystem))
            {
                lastContext = null; seen.Clear(); return;
            }
            try { Sample(now); }
            catch (Exception ex)
            {
                // Diagnostics must never interrupt placement, even if the native API changes.
                Mod.Log.Warn("AreaOverlap diagnostics disabled: " + ex);
                Enabled = false;
            }
        }

        private void Sample(double now)
        {
            var active = tools.activeTool.GetPrefab();
            string context = "tool=" + tools.activeTool.GetType().Name + " actionMode=" + tools.actionMode
                + " editorValidation=" + tools.actionMode.IsEditor()
                + " selectedPrefab=" + (active == null ? "none" : active.name)
                + " tree=" + Geometry(Mod.Area) + " canopy=" + Geometry(Mod.CanopyArea);
            if (context != lastContext)
            {
                Mod.Log.Info("AreaOverlap context " + context);
                lastContext = context; seen.Clear(); nextSummary = 0;
            }
            int inspected = 0, errors = 0, emitted = 0;
            bool capped = false;
            var entityType = GetEntityTypeHandle();
            using (var chunks = icons.ToArchetypeChunkArray(Allocator.Temp))
            {
                foreach (var chunk in chunks)
                {
                    var entities = chunk.GetNativeArray(entityType);
                    foreach (var entity in entities)
                    {
                        if (inspected >= 256) { capped = true; break; }
                        inspected++;
                        var prefab = EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab;
                        if (!EntityManager.HasComponent<ToolErrorData>(prefab)) continue;
                        errors++;
                        if (emitted >= 8 || seen.Count >= 128) continue;
                        var error = EntityManager.GetComponentData<ToolErrorData>(prefab);
                        var icon = EntityManager.GetComponentData<Icon>(entity);
                        Entity owner = EntityManager.HasComponent<Owner>(entity)
                            ? EntityManager.GetComponentData<Owner>(entity).m_Owner : Entity.Null;
                        Entity target = EntityManager.HasComponent<Target>(entity)
                            ? EntityManager.GetComponentData<Target>(entity).m_Target : Entity.Null;
                        string details = "error=" + error.m_Error + " iconPriority=" + icon.m_Priority
                            + " errorFlags=" + error.m_Flags + " preview={" + Describe(owner, false)
                            + "} target={" + Describe(target, true) + "}";
                        if (seen.Add(details))
                        {
                            Mod.Log.Info("AreaOverlap " + details + " position=" + icon.m_Location);
                            emitted++;
                        }
                    }
                    if (capped) break;
                }
            }
            if (now >= nextSummary)
            {
                Mod.Log.Info("AreaOverlap sample previewIconsInspected=" + inspected + " toolErrorsInSample=" + errors
                    + " capped=" + capped + " uniqueLogged=" + seen.Count
                    + "; limits: 256 icons/sample, 8 new lines/sample, 128 unique lines/tool context; no tree scan");
                nextSummary = now + 10;
            }
        }

        private string Geometry(PrefabBase prefab)
        {
            if (prefab == null || !prefabs.TryGetEntity(prefab, out var entity)) return "unavailable";
            return Geometry(entity);
        }

        private string Geometry(Entity prefab)
        {
            if (!EntityManager.HasComponent<AreaGeometryData>(prefab)) return "not-area";
            var data = EntityManager.GetComponentData<AreaGeometryData>(prefab);
            return "type=" + data.m_Type + ",storedFlags=" + data.m_Flags;
        }

        private string Describe(Entity entity, bool includeId)
        {
            if (entity == Entity.Null || !EntityManager.Exists(entity)) return "none-or-gone";
            string result = includeId ? "entity=" + entity + " " : "";
            Entity prefab = Entity.Null;
            if (EntityManager.HasComponent<PrefabRef>(entity))
            {
                prefab = EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab;
                result += "prefab=" + prefabs.GetPrefab<PrefabBase>(prefab).name + " ";
            }
            result += "building=" + EntityManager.HasComponent<Game.Buildings.Building>(entity)
                + " netEdge=" + EntityManager.HasComponent<Game.Net.Edge>(entity)
                + " area=" + EntityManager.HasComponent<Game.Areas.Area>(entity);
            if (EntityManager.HasComponent<Temp>(entity))
                result += " tempFlags=" + EntityManager.GetComponentData<Temp>(entity).m_Flags;
            bool owned = EntityManager.HasComponent<Owner>(entity);
            if (owned)
            {
                var owner = EntityManager.GetComponentData<Owner>(entity).m_Owner;
                result += " owner=" + owner + " ownerIsBuilding=" + EntityManager.HasComponent<Game.Buildings.Building>(owner);
            }
            if (prefab != Entity.Null && EntityManager.HasComponent<AreaGeometryData>(prefab))
            {
                var data = EntityManager.GetComponentData<AreaGeometryData>(prefab);
                result += " " + Geometry(prefab);
                // Mirror the native helper on a local copy only. Never write altered flags.
                Game.Areas.AreaUtils.SetCollisionFlags(ref data, !tools.actionMode.IsEditor() || owned);
                result += " effectiveFlags=" + data.m_Flags;
            }
            return result;
        }
    }
}
