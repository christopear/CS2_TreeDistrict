using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Game.UI;
using Game.UI.InGame;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Collections;
using Unity.Entities;

namespace TreeDistrict
{
    // Native area entities own settings; saved Entity references are remapped by CS2.
    // Boundary geometry remains exclusively owned/serialized by the native area systems.
    public partial class TreeAreaSettingsSystem : UISystemBase, IDefaultSerializable
    {
        private readonly ConcurrentQueue<string> _commands = new ConcurrentQueue<string>();
        private readonly Dictionary<Entity, string> _areaIds = new Dictionary<Entity, string>();
        private JObject _catalog, _state;
        private PrefabSystem _prefabs;
        private TreeDistrict.Core.Foliage.ProtonScatterSettings _scatterSettings;
        private readonly TreeDistrict.Core.Foliage.StampFoliageGenerator _stampGenerator = new TreeDistrict.Core.Foliage.StampFoliageGenerator();
        private ToolSystem _tools;
        private SelectedInfoUISystem _selection;
        private EntityQuery _areas;
        private ValueBinding<string> _binding;
        private string _selectedId, _error, _lastEnvelope, _libraryError;
        private Entity _lastSelection;
        private bool _ready, _visible, _initialScan = true, _wasAreaTool, _editorMode;
        private bool _uiDirty = true;
        private int _areaOrderVersion;
        private string LibraryPath => Path.Combine(UnityEngine.Application.persistentDataPath, "ModsSettings", "TreeDistrict", "sets.v1.json");
        public override GameMode gameMode => GameMode.GameOrEditor;

        protected override void OnCreate()
        {
            base.OnCreate();
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TreeDistrict.Catalog"))
            using (var reader = new StreamReader(stream)) _catalog = SettingsModel.Parse(reader.ReadToEnd());
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TreeDistrict.ScatterSettings"))
            using (var reader = new StreamReader(stream))
                _scatterSettings = JsonConvert.DeserializeObject<TreeDistrict.Core.Foliage.ProtonScatterSettings>(reader.ReadToEnd());

            _prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
            _tools = World.GetOrCreateSystemManaged<ToolSystem>();
            _selection = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();
            var areaQuery = new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<Game.Areas.Area>(), ComponentType.ReadOnly<PrefabRef>(), ComponentType.ReadOnly<Game.Areas.Node>() },
                None = new[] { ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Deleted>() }
            };
            _areas = GetEntityQuery(areaQuery);
            AddBinding(_binding = new ValueBinding<string>("TreeDistrict", "editor", ""));
            AddBinding(new TriggerBinding<string>("TreeDistrict", "command", json => {
                if (_commands.Count < 128 && json != null && json.Length <= 1000000) _commands.Enqueue(json);
            }));
            ResetState();
        }
        private void ResetState()
        {
            _state = SettingsModel.Initial(_catalog); _areaIds.Clear(); _selectedId = null; _error = null;
            _trees.Clear(); _orphanTrees.Clear();
            _placement = null;
            _initialScan = true; _lastSelection = Entity.Null; _visible = false; _wasAreaTool = false;
            _uiDirty = true;
            while (_commands.TryDequeue(out _)) { }
            LoadLibrary();
        }
        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            _ready = false; _visible = false; _binding.Update(""); _lastEnvelope = ""; _uiDirty = true;
            if (_placement != null) CancelPlacement(_placement.Area, "Placement stopped for loading.");
            while (_commands.TryDequeue(out _)) { }
        }
        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            _ready = (mode & GameMode.GameOrEditor) != 0; _editorMode = (mode & GameMode.Editor) != 0; _initialScan = true;
            _selectedId = null; _lastSelection = Entity.Null; _wasAreaTool = false;
            _uiDirty = true;
        }
        private bool IsTreeArea(Entity entity)
        {
            return entity != Entity.Null && EntityManager.Exists(entity) && !EntityManager.HasComponent<Deleted>(entity)
                && !EntityManager.HasComponent<Temp>(entity) && EntityManager.HasComponent<PrefabRef>(entity)
                && EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab == _prefabs.GetEntity(Mod.Area);
        }
        private void ScanAreas()
        {
            var live = new HashSet<Entity>();
            using (var entities = _areas.ToEntityArray(Allocator.Temp))
            {
                foreach (var entity in entities)
                {
                    if (!IsTreeArea(entity)) continue;
                    var nodes = EntityManager.GetBuffer<Game.Areas.Node>(entity, true);
                    if (nodes.Length < 3 || nodes.Length > 2049) continue;
                    live.Add(entity);
                    if (!_areaIds.TryGetValue(entity, out string id))
                    {
                        if (_areaIds.Count >= 2048) { _error = "Tree Area settings limit reached (2048)."; _uiDirty = true; continue; }
                        id = Guid.NewGuid().ToString("N"); _areaIds.Add(entity, id);
                        var record = (JObject)_state["draft"].DeepClone();
                        record["id"] = id; record["name"] = "Tree Area " + _areaIds.Count;
                        record["seed"] = 1; record["generationMode"] = "onDemand"; record["revision"] = 0;
                        ((JArray)_state["districts"]).Add(record);
                        _uiDirty = true;
                        if (!_initialScan) { _selectedId = id; _visible = true; }
                        Mod.Log.Info("Attached settings to Tree Area " + id);
                    }
                    var target = SettingsModel.Target(_state, id);
                    int nodeCount = nodes.Length;
                    if (nodeCount > 3 && nodes[0].m_Position.x == nodes[nodeCount - 1].m_Position.x
                        && nodes[0].m_Position.z == nodes[nodeCount - 1].m_Position.z) nodeCount--;
                    var previous = target["boundary"] as JArray;
                    bool same = previous != null && previous.Count == nodeCount;
                    for (int i = 0; same && i < nodeCount; i++)
                        same = (double)previous[i]["x"] == nodes[i].m_Position.x
                            && (double)previous[i]["z"] == nodes[i].m_Position.z;
                    if (same) continue;
                    var boundary = new JArray();
                    for (int i = 0; i < nodeCount; i++) boundary.Add(new JObject { ["x"] = nodes[i].m_Position.x, ["z"] = nodes[i].m_Position.z });
                    if (target["boundary"] != null && !JToken.DeepEquals(target["boundary"], boundary))
                    {
                        target["revision"] = checked((int)target["revision"] + 1);
                        if (!_initialScan && _tools.activePrefab == Mod.Area) { _selectedId = id; _visible = true; }
                    }
                    target["boundary"] = boundary;
                    _uiDirty = true;
                }
            }
            foreach (var entity in _areaIds.Keys.Where(e => !live.Contains(e)).ToArray())
            {
                RemoveOwned(entity);
                string id = _areaIds[entity]; _areaIds.Remove(entity);
                SettingsModel.Target(_state, id).Remove();
                if (_selectedId == id) _selectedId = null;
                _uiDirty = true;
            }
            _initialScan = false;
            _areaOrderVersion = EntityManager.GetComponentOrderVersion<Game.Areas.Area>();
            // Loaded references whose area no longer exists need one-time cleanup.
            // Live areas retain a passive registry; never poll their plants for counts.
            if (_orphanTrees.Count > 0)
            {
                DeleteTrees(_orphanTrees);
                _orphanTrees.Clear();
            }
        }
        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (!_ready) return;
            if (_visible || _tools.activePrefab == Mod.Area)
            {
                if (_uiDirty || _lastEnvelope == "") { Publish(); _uiDirty = false; }
            }
            else if (_lastEnvelope != "") { _binding.Update(""); _lastEnvelope = ""; }
        }
        // Invoked before native tools and modification systems. UIUpdate is too late:
        // cleanup would strip Created/Updated before native object initialization runs.
        internal void ProcessCommands(bool areaNodesChanged)
        {
            if (!_ready) return;
            bool active = _tools.activePrefab == Mod.Area;
            if (active && !_wasAreaTool) { _visible = true; _uiDirty = true; }
            _wasAreaTool = active;
            // Chunk version/order checks detect native edits and structural deletions without
            // materializing all areas or touching any plants while the world is unchanged.
            if (_initialScan || EntityManager.GetComponentOrderVersion<Game.Areas.Area>() != _areaOrderVersion
                || areaNodesChanged) ScanAreas();
            var selected = _editorMode ? _tools.selected : _selection.selectedEntity;
            if (selected == Entity.Null) selected = _tools.selected;
            if (selected != _lastSelection)
            {
                _lastSelection = selected;
                _uiDirty = true;
                if (_areaIds.TryGetValue(selected, out string id)) { _selectedId = id; _visible = true; }
                else if (selected != Entity.Null) { _selectedId = null; _visible = false; }
            }
            while (_commands.TryDequeue(out string json))
            {
                _uiDirty = true;
                try { Execute(SettingsModel.Parse(json)); _error = null; }
                catch (Exception ex) { _error = ex.Message; Mod.Log.Warn("Settings command rejected: " + ex.Message); }
            }
            ProcessPlacement();
        }
        private void SelectNative(Entity entity)
        {
            // SelectedInfoUISystem is gameplay-only; editor selection belongs to ToolSystem.
            if (!_editorMode) _selection.SetSelection(entity);
            _tools.selected = entity;
        }
        private Entity EntityFor(string id)
        {
            foreach (var pair in _areaIds) if (pair.Value == id && IsTreeArea(pair.Key)) return pair.Key;
            throw new ArgumentException("Tree Area no longer exists.");
        }
        private void Execute(JObject command)
        {
            string type = SettingsModel.Text(command["type"], 40);
            switch (type)
            {
                case "beginCanopy":
                    _selectedId = null; SelectNative(Entity.Null); _visible = false;
                    _tools.ActivatePrefabTool(Mod.CanopyArea); return;
                case "togglePanel": _visible = !_visible; return;
                case "selectDistrict":
                    string requestedId = command["id"]?.Type == JTokenType.Null ? null : SettingsModel.Text(command["id"], 160);
                    Entity requestedEntity = requestedId == null ? Entity.Null : EntityFor(requestedId);
                    _selectedId = requestedId;
                    SelectNative(requestedEntity);
                    _visible = true; return;
                case "beginDrawing":
                    if (command["edit"]?.Type != JTokenType.Boolean) throw new ArgumentException("Invalid drawing mode.");
                    if ((bool)command["edit"] && _selectedId == null) throw new ArgumentException("Select a Tree Area first.");
                    if (!(bool)command["edit"]) { _selectedId = null; SelectNative(Entity.Null); }
                    _tools.ActivatePrefabTool(Mod.Area); _visible = true; return;
                case "deleteDistrict":
                case "generate":
                case "cancelGeneration":
                case "removeGenerated":
                case "bakeDelete":
                    GenerationCommand(type, command); return;
            }
            if (type != "city" && command["districtId"]?.Type != JTokenType.Null)
                EntityFor(SettingsModel.Text(command["districtId"], 160));
            var next = SettingsModel.Apply(_state, command);
            // Persist the library before publishing an atomic Save-as-and-replace result.
            if (type == "saveSet") SaveLibrary(next);
            _state = next;
        }
        private void Publish()
        {
            var envelope = new JObject {
                ["protocolVersion"] = 1, ["visible"] = _visible, ["workspace"] = _state,
                ["selectedId"] = _selectedId, ["drawing"] = false, ["error"] = _error ?? _libraryError
            }.ToString(Formatting.None);
            if (envelope != _lastEnvelope) { _binding.Update(envelope); _lastEnvelope = envelope; }
        }
        private void LoadLibrary()
        {
            _libraryError = null;
            if (!File.Exists(LibraryPath)) return;
            try
            {
                var doc = SettingsModel.Parse(File.ReadAllText(LibraryPath));
                if ((int?)doc["schemaVersion"] != 1 || !(doc["sets"] is JArray sets) || sets.Count > 250) throw new ArgumentException("Unsupported library document.");
                var library = (JArray)_state["library"];
                foreach (var entry in sets)
                {
                    var id = SettingsModel.Text(entry["id"], 160);
                    if (library.Any(e => (string)e["id"] == id)) throw new ArgumentException("Duplicate library identifier.");
                    ProfileMigration.Set(entry["set"]); SettingsModel.ValidateSet(entry["set"]);
                    library.Add(new JObject { ["id"] = id, ["builtIn"] = false, ["set"] = entry["set"].DeepClone() });
                }
            }
            catch (Exception ex)
            {
                _state["library"] = _catalog["sets"].DeepClone();
                _libraryError = "Custom library could not be read; the original file is preserved. " + ex.Message;
                Mod.Log.Warn(_libraryError);
            }
        }
        private void SaveLibrary(JObject next)
        {
            if (_libraryError != null) throw new IOException("Repair or recover the custom library file before saving sets. " + LibraryPath);
            var doc = new JObject { ["schemaVersion"] = 1, ["sets"] = new JArray(next["library"].Where(e => !(bool)e["builtIn"]).Select(e => e.DeepClone())) };
            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
            string temporary = LibraryPath + ".tmp";
            File.WriteAllText(temporary, doc.ToString(Formatting.Indented));
            if (File.Exists(LibraryPath)) File.Replace(temporary, LibraryPath, LibraryPath + ".bak");
            else File.Move(temporary, LibraryPath);
        }
        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            // Only save records for live areas, not cached deleted entities.
            var records = _areaIds.Where(p => IsTreeArea(p.Key)).ToArray();
            writer.Write(2);
            writer.Write(new JObject { ["city"] = _state["city"].DeepClone(), ["draft"] = _state["draft"].DeepClone() }.ToString(Formatting.None));
            writer.Write(records.Length);
            foreach (var pair in records)
            {
                writer.Write(pair.Key);
                var record = (JObject)SettingsModel.Target(_state, pair.Value).DeepClone();
                if ((bool?)record["placementActive"] == true)
                {
                    record["placementActive"] = false;
                    record["generationStatus"] = "Partial planting saved. Pending placement is not resumed after loading; regenerate to start again.";
                }
                record.Remove("boundary"); // Native area nodes are authoritative.
                writer.Write(record.ToString(Formatting.None));
                var trees = _trees.TryGetValue(pair.Key, out var owned) ? owned.Where(LivingReference).ToArray() : new Entity[0];
                writer.Write(trees.Length);
                foreach (var tree in trees) writer.Write(tree);
            }
            // Preserve pending cleanup if a save occurs between native deletion and our next scan.
            var orphaned = _trees.Where(p => !IsTreeArea(p.Key)).SelectMany(p => p.Value).Concat(_orphanTrees).Where(LivingReference).Distinct().ToArray();
            writer.Write(orphaned.Length);
            foreach (var tree in orphaned) writer.Write(tree);
        }
        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            ResetState();
            reader.Read(out int version);
            if (version != 1 && version != 2) throw new InvalidDataException("Unsupported TreeDistrict save version: " + version);
            reader.Read(out string headerJson); var header = SettingsModel.Parse(headerJson);
            ProfileMigration.Working(header["draft"]);
            SettingsModel.ValidateClimate(header["city"], false); SettingsModel.ValidateWorking((JObject)header["draft"]);
            _state["city"] = header["city"]; _state["draft"] = header["draft"];
            reader.Read(out int count);
            if (count < 0 || count > 2048) throw new InvalidDataException("Invalid TreeDistrict area count.");
            var ids = new HashSet<string>();
            for (int i = 0; i < count; i++)
            {
                reader.Read(out Entity entity); reader.Read(out string json);
                var record = SettingsModel.Parse(json); ProfileMigration.Working(record); SettingsModel.ValidateRecord(record);
                record["placementActive"] = false;
                string id = (string)record["id"];
                if (!ids.Add(id)) throw new InvalidDataException("Duplicate Tree Area ID in save.");
                var trees = new List<Entity>();
                if (version >= 2)
                {
                    reader.Read(out int treeCount);
                    if (treeCount < 0) throw new InvalidDataException("Invalid owned vegetation count.");
                    for (int j = 0; j < treeCount; j++) { reader.Read(out Entity tree); if (tree != Entity.Null) trees.Add(tree); }
                }
                if (entity == Entity.Null) { _orphanTrees.AddRange(trees); continue; }
                _trees.Add(entity, trees);
                _areaIds.Add(entity, id); ((JArray)_state["districts"]).Add(record);
            }
            if (version >= 2)
            {
                reader.Read(out int orphanCount);
                if (orphanCount < 0) throw new InvalidDataException("Invalid pending cleanup count.");
                for (int j = 0; j < orphanCount; j++) { reader.Read(out Entity tree); if (tree != Entity.Null) _orphanTrees.Add(tree); }
            }
            Mod.Log.Info("Loaded settings for " + _areaIds.Count + " Tree Areas.");
        }
        public void SetDefaults(Context context) { ResetState(); }
    }
}
