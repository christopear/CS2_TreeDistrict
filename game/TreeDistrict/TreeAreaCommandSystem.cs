using Game;

namespace TreeDistrict
{
    public sealed partial class TreeAreaCommandSystem : GameSystemBase
    {
        private TreeAreaSettingsSystem _settings;
        protected override void OnCreate()
        {
            base.OnCreate();
            _settings = World.GetOrCreateSystemManaged<TreeAreaSettingsSystem>();
        }
        protected override void OnUpdate() => _settings.ProcessCommands();
    }
}
