using System;
using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using TreeDistrict.Core.Canopy;
using TreeDistrict.Core.Foliage;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TreeDistrict
{
    // The native area is the saved source of truth. Meshes are disposable derived data.
    // No native tree entities, collisions, simulation, or tree-count polling.
    public partial class TreeCanopySystem : GameSystemBase
    {
        private sealed class Record
        {
            public Entity Area;
            public float3[] Nodes;
            public CanopyGeometry Geometry;
            public int X, Z, Chunks;
            public bool Queued, Cancelled;
            public readonly List<GameObject> Objects = new List<GameObject>();
            public readonly List<Mesh> Meshes = new List<Mesh>();
            public readonly List<Material> Materials = new List<Material>();
            public readonly List<Texture2D> Textures = new List<Texture2D>();
            public void Clear()
            {
                foreach (var obj in Objects) { if (obj != null) obj.SetActive(false); Object.Destroy(obj); }
                foreach (var mesh in Meshes) Object.Destroy(mesh);
                foreach (var material in Materials) Object.Destroy(material);
                foreach (var texture in Textures) Object.Destroy(texture);
                Objects.Clear(); Meshes.Clear(); Materials.Clear(); Textures.Clear(); Chunks = 0;
            }
        }
        private readonly Dictionary<Entity, Record> _records = new Dictionary<Entity, Record>();
        private readonly Queue<Record> _pending = new Queue<Record>();
        private PrefabSystem _prefabs;
        private EntityQuery _areas, _changed;
        private bool _ready, _initial;
        private int _orderVersion;
        private Shader _shader;
        private bool _shaderChecked;
        protected override void OnCreate()
        {
            base.OnCreate();
            _prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
            var desc = new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<Game.Areas.Area>(), ComponentType.ReadOnly<PrefabRef>(), ComponentType.ReadOnly<Game.Areas.Node>() },
                None = new[] { ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Deleted>() }
            };
            // Keep an unfiltered instance for reconciliation. The system-owned query
            // automatically receives LastSystemVersion from SystemBase each update.
            _areas = EntityManager.CreateEntityQuery(desc);
            _changed = GetEntityQuery(desc);
            _changed.SetChangedVersionFilter(ComponentType.ReadOnly<Game.Areas.Node>());
        }
        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode); _ready = false; Clear();
        }
        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            Clear(); _ready = (mode & GameMode.GameOrEditor) != 0; _initial = true;
        }
        protected override void OnDestroy()
        {
            Clear(); _areas.Dispose(); base.OnDestroy();
        }
        internal void Clear()
        {
            foreach (var record in _records.Values) { record.Cancelled = true; record.Clear(); }
            _records.Clear(); _pending.Clear(); _shader = null; _shaderChecked = false;
        }
        protected override void OnUpdate()
        {
            if (!_ready || Mod.CanopyArea == null) return;
            int order = EntityManager.GetComponentOrderVersion<Game.Areas.Area>();
            if (_initial || order != _orderVersion || !_changed.IsEmpty)
            {
                Scan(); _initial = false; _orderVersion = order;
            }
            if (_pending.Count == 0) return;
            var job = _pending.Dequeue(); job.Queued = false;
            if (job.Cancelled || job.Geometry == null) return;
            try
            {
                // Bounded per-frame work, also when the simulation is paused or in the editor.
                var tile = job.Geometry.BuildTile(job.X, job.Z);
                if (tile.Indices.Count > 0) CreateMesh(job, tile, job.X, job.Z);
                if (++job.X > job.Geometry.MaxTileX) { job.X = job.Geometry.MinTileX; job.Z++; }
                if (job.Z <= job.Geometry.MaxTileZ) Enqueue(job);
                else Mod.Log.Info($"Canopy complete: area {job.Area.Index}, {job.Chunks} mesh chunks.");
            }
            catch (Exception ex)
            {
                job.Clear(); job.Geometry = null;
                Mod.Log.Error(ex, "Canopy generation failed. Edit its boundary to retry.");
            }
        }
        private void Enqueue(Record record)
        {
            if (record.Queued) return;
            record.Queued = true; _pending.Enqueue(record);
        }
        private void Scan()
        {
            if (!_prefabs.TryGetEntity(Mod.CanopyArea, out var prefab)) return;
            var live = new HashSet<Entity>();
            using (var entities = _areas.ToEntityArray(Allocator.Temp))
                foreach (var entity in entities)
                {
                    if (EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab != prefab) continue;
                    live.Add(entity);
                    var nodes = EntityManager.GetBuffer<Game.Areas.Node>(entity, true);
                    _records.TryGetValue(entity, out var record);
                    bool same = record?.Nodes != null && record.Nodes.Length == nodes.Length;
                    for (int i = 0; same && i < nodes.Length; i++) same = math.all(record.Nodes[i] == nodes[i].m_Position);
                    if (same) continue;
                    if (record == null) _records.Add(entity, record = new Record { Area = entity });
                    record.Clear(); record.Geometry = null;
                    record.Nodes = new float3[nodes.Length];
                    var boundary = new Position2[nodes.Length];
                    for (int i = 0; i < nodes.Length; i++)
                    { record.Nodes[i] = nodes[i].m_Position; boundary[i] = new Position2(nodes[i].m_Position.x, nodes[i].m_Position.z); }
                    if (nodes.Length < 3) continue;
                    try
                    {
                        record.Geometry = new CanopyGeometry(boundary);
                        record.X = record.Geometry.MinTileX; record.Z = record.Geometry.MinTileZ;
                        Enqueue(record);
                        Mod.Log.Info($"Canopy queued: area {entity.Index}, {nodes.Length} boundary nodes.");
                    }
                    catch (ArgumentException ex) { Mod.Log.Warn("Canopy boundary not ready: " + ex.Message); }
                }
            var removed = new List<Entity>();
            foreach (var pair in _records)
                if (!live.Contains(pair.Key)) { pair.Value.Cancelled = true; pair.Value.Clear(); removed.Add(pair.Key); }
            foreach (var entity in removed) _records.Remove(entity);
        }
        private Shader GetShader()
        {
            if (!_shaderChecked)
            {
                _shaderChecked = true;
                _shader = Shader.Find("HDRP/Lit");
                if (_shader == null || !_shader.isSupported) _shader = Shader.Find("HDRP/Unlit");
                if (_shader == null || !_shader.isSupported)
                    throw new InvalidOperationException("No supported HDRP canopy shader is loaded.");
                Mod.Log.Info("Canopy shader: " + _shader.name);
            }
            if (_shader == null || !_shader.isSupported) throw new InvalidOperationException("Canopy shader unavailable.");
            return _shader;
        }
        private void CreateMesh(Record owner, CanopyGeometry.Tile tile, int tx, int tz)
        {
            var shader = GetShader();
            var terrain = World.GetOrCreateSystemManaged<TerrainSystem>().GetHeightData(true);
            float Ground(double x, double z) => TerrainUtils.SampleHeight(ref terrain, new float3((float)x, 0, (float)z));
            float Top(double x, double z) => Ground(x, z) + (float)CanopyGeometry.Height(x, z);
            double x0 = tx * CanopyGeometry.TileSize, z0 = tz * CanopyGeometry.TileSize;
            var origin = new Vector3((float)x0, Ground(x0, z0), (float)z0);
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>();
            int Vertex(Position2 p, bool bottom, Vector3? normal = null)
            {
                int index = vertices.Count;
                vertices.Add(new Vector3((float)(p.X - x0), (bottom ? Ground(p.X, p.Z) - .2f : Top(p.X, p.Z)) - origin.y, (float)(p.Z - z0)));
                normals.Add(normal ?? new Vector3(Top(p.X - .5, p.Z) - Top(p.X + .5, p.Z), 1,
                    Top(p.X, p.Z - .5) - Top(p.X, p.Z + .5)).normalized);
                uv.Add(new Vector2((float)((p.X - x0) / CanopyGeometry.TileSize), (float)((p.Z - z0) / CanopyGeometry.TileSize)));
                return index;
            }
            foreach (var p in tile.Points) Vertex(p, false);
            var indices = new List<int>();
            for (int i = 0; i < tile.Indices.Count; i += 3)
            { indices.Add(tile.Indices[i]); indices.Add(tile.Indices[i + 2]); indices.Add(tile.Indices[i + 1]); }
            foreach (var edge in tile.Edges)
            {
                var outward = new Vector3((float)(edge.B.Z - edge.A.Z), 0, (float)(edge.A.X - edge.B.X)).normalized;
                int a = Vertex(edge.A, false, outward), b = Vertex(edge.B, false, outward);
                int c = Vertex(edge.A, true, outward), d = Vertex(edge.B, true, outward);
                indices.Add(a); indices.Add(b); indices.Add(c); indices.Add(c); indices.Add(b); indices.Add(d);
            }
            var mesh = new Mesh { name = "TreeCanopy mesh", indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.HideAndDontSave };
            owner.Meshes.Add(mesh);
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0); mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            // Bake cheap hollow shading/colour into a texture; HDRP/Lit doesn't consume vertex colours.
            const int size = 97;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) {
                name = "TreeCanopy colour", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
            };
            owner.Textures.Add(texture);
            var pixels = new Color[size * size];
            for (int z = 0; z < size; z++)
                for (int x = 0; x < size; x++)
                {
                    float shade = (float)CanopyGeometry.Shade(x0 + x, z0 + z);
                    pixels[z * size + x] = new Color(.22f * shade, .37f * shade, .13f * shade, 1);
                }
            texture.SetPixels(pixels); texture.Apply(true, true);
            var material = new Material(shader) { name = "TreeCanopy material", hideFlags = HideFlags.HideAndDontSave };
            owner.Materials.Add(material);
            material.SetColor("_BaseColor", Color.white); material.SetTexture("_BaseColorMap", texture);
            if (material.HasProperty("_UnlitColor")) material.SetColor("_UnlitColor", Color.white);
            if (material.HasProperty("_UnlitColorMap")) material.SetTexture("_UnlitColorMap", texture);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .15f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0);
            material.renderQueue = (int)RenderQueue.Geometry;
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(material);
            var obj = new GameObject($"TreeCanopy {owner.Area.Index} [{tx},{tz}]") { hideFlags = HideFlags.HideAndDontSave };
            owner.Objects.Add(obj);
            obj.transform.position = origin;
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            owner.Chunks++;
        }
    }
}
