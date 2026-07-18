using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;

namespace TestPluginV7;

public sealed class TestPlugin : GenericPlugin
{
    private readonly TestSettings settings;
    private string EventPath => Path.Combine(GetPluginUserDataPath(), "events.txt");

    public override Guid Id { get; } = Guid.Parse("8134f4eb-556e-4e01-936f-1bf5a808cb10");

    public TestPlugin(IPlayniteAPI api)
        : base(api)
    {
        settings = LoadPluginSettings<TestSettings>() ?? new TestSettings();
        settings.Plugin = this;
        Properties = new GenericPluginProperties { HasSettings = true };
        File.AppendAllLines(EventPath, ["constructed:" + api.ApplicationInfo.Mode]);
        api.Notifications.Add("test-v7-loaded", "SDK v7 plugin constructed", NotificationType.Info);
    }

    public override ISettings GetSettings(bool firstRunSettings) => settings;

    public override void OnApplicationStarted(OnApplicationStartedEventArgs args) =>
        File.AppendAllLines(EventPath, ["started"]);

    public override void OnApplicationStopped(OnApplicationStoppedEventArgs args) =>
        File.AppendAllLines(EventPath, ["stopped"]);

    public sealed class TestSettings : ObservableObject, ISettings
    {
        private int launchCount;
        private TestSettings? editClone;

        [DontSerialize]
        public TestPlugin? Plugin { get; set; }

        public int LaunchCount
        {
            get => launchCount;
            set => SetValue(ref launchCount, value);
        }

        public void BeginEdit() => editClone = Serialization.GetClone(this);
        public void CancelEdit()
        {
            if (editClone != null)
            {
                LaunchCount = editClone.LaunchCount;
            }
        }
        public void EndEdit() => Plugin?.SavePluginSettings(this);
        public bool VerifySettings(out List<string> errors)
        {
            errors = [];
            return true;
        }
    }
}
