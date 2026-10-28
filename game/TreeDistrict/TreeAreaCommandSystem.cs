using Game;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;

namespace TreeDistrict
{
    public sealed partial class TreeAreaCommandSystem : GameSystemBase
    {
        private TreeAreaSettingsSystem _settings;
        private EntityQuery _changedAreas;
        protected override void OnCreate()
        {
            base.OnCreate();
            _settings = World.GetOrCreateSystemManaged<TreeAreaSettingsSystem>();
            _changedAreas = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<Game.Areas.Area>(), ComponentType.ReadOnly<PrefabRef>(), ComponentType.ReadOnly<Game.Areas.Node>() },
                None = new[] { ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Deleted>() }
            });
            // SystemBase advances the change filter to this system's LastSystemVersion.
            // This observes edits since our previous MainLoop update, including while UI is hidden.
            _changedAreas.SetChangedVersionFilter(ComponentType.ReadOnly<Game.Areas.Node>());
        }
        protected override void OnUpdate() => _settings.ProcessCommands(!_changedAreas.IsEmpty);
    }
}
