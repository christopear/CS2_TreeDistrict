using Colossal;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.Prefabs;
using Game.SceneFlow;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using System.IO;
using Game.UI;
using Colossal.UI;

namespace TreeDistrict
{
    public sealed class Mod : IMod
    {
        public const string PrefabName = "TreeDistrict.Area.v1";
        public const string CanopyPrefabName = "TreeDistrict.CanopyArea.v1";
        internal static readonly ILog Log = LogManager.GetLogger("TreeDistrict").SetShowsErrorsInUI(false);
        internal static SpacePrefab Area;
        internal static SpacePrefab CanopyArea;

        public void OnLoad(UpdateSystem updateSystem)
        {
            // Nonphysical area: no policies, surface rendering, clearing or plants.
            Area = ScriptableObject.CreateInstance<SpacePrefab>();
            Area.name = PrefabName;
            Area.m_Color = new Color(0.12f, 0.65f, 0.25f, 0.25f);
            Area.m_EdgeColor = new Color(0.2f, 0.9f, 0.35f, 1f);
            Area.m_SelectionColor = new Color(0.3f, 0.9f, 0.4f, 0.4f);
            Area.m_SelectionEdgeColor = new Color(0.65f, 1f, 0.55f, 1f);
            Area.AddComponent<UIObject>().m_Icon = "coui://treedistrict/TreeArea.svg";
            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                UIManager.defaultUISystem.AddHostLocation("treedistrict", Path.Combine(Path.GetDirectoryName(asset.path), "Icons"), true);
            var prefabs = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<PrefabSystem>();
            if (!prefabs.AddPrefab(Area))
                throw new System.InvalidOperationException("Could not register TreeDistrict area prefab.");
            CanopyArea = ScriptableObject.CreateInstance<SpacePrefab>();
            CanopyArea.name = CanopyPrefabName;
            CanopyArea.m_Color = new Color(.13f, .4f, .19f, .2f);
            CanopyArea.m_EdgeColor = new Color(.35f, .8f, .3f, 1);
            CanopyArea.m_SelectionColor = new Color(.2f, .65f, .3f, .3f);
            CanopyArea.m_SelectionEdgeColor = new Color(.65f, 1, .55f, 1);
            CanopyArea.AddComponent<UIObject>().m_Icon = "coui://treedistrict/TreeCanopy.svg";
            if (!prefabs.AddPrefab(CanopyArea))
                throw new System.InvalidOperationException("Could not register canopy area prefab.");
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN());
            updateSystem.UpdateAt<TreeDistrictRegistrationSystem>(SystemUpdatePhase.MainLoop);
            updateSystem.UpdateAt<TreeAreaSettingsSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<AreaOverlapDiagnosticsSystem>(SystemUpdatePhase.UIUpdate);
            NativePlacementTimings.Register(updateSystem);
            LightCullingTimings.Install();
            // Register last so our creation work precedes the ToolSystem timing probe.
            updateSystem.UpdateBefore<TreeAreaCommandSystem, Game.Tools.ToolSystem>(SystemUpdatePhase.MainLoop);
            updateSystem.UpdateBefore<TreeCanopySystem, Game.Tools.ToolSystem>(SystemUpdatePhase.MainLoop);
            Log.Info("Registered TreeDistrict.Area.v1 with manual mixed-vegetation generation (Proton solver).");
        }
        public void OnDispose()
        {
            NativePlacementTimings.Stop();
            LightCullingTimings.Uninstall();
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                var overlapDiagnostics = world.GetExistingSystemManaged<AreaOverlapDiagnosticsSystem>();
                if (overlapDiagnostics != null) overlapDiagnostics.Enabled = false;
                var canopy = world.GetExistingSystemManaged<TreeCanopySystem>();
                if (canopy != null) { canopy.Enabled = false; canopy.Clear(); }
            }
            Log.Info("TreeDistrict disposed.");
        }
    }
    public sealed class LocaleEN : IDictionarySource
    {
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { "Assets.NAME[" + Mod.PrefabName + "]", "Tree district" },
                { "Assets.DESCRIPTION[" + Mod.PrefabName + "]", "Draw an empty vegetation planning area using the native area tool. No trees or district policies are applied." },
                { "Assets.NAME[" + Mod.CanopyPrefabName + "]", "Tree Canopy Area" },
                { "Assets.DESCRIPTION[" + Mod.CanopyPrefabName + "]", "Draw a continuous forest canopy. The surface automatically follows completed boundary edits. Experimental; no individual trees are placed." }
            };
        }
        public void Unload() { }
    }
}
