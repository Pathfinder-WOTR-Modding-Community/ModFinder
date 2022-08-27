namespace ModFinder.Infrastructure
{
    public class AppSettingsData
    {
        public string AutoWrathPath { get; set; }
        public string WrathPath { get; set; }

        private static AppSettingsData _Instance;
        public static AppSettingsData Load()
        {
            if (_Instance == null)
            {
                if (Main.TryReadFile("Settings.json", out var settingsRaw))
                    _Instance = ModFinderIO.FromString<AppSettingsData>(settingsRaw);
                else
                    _Instance = new();
            }

            return _Instance;
        }

        public void Save()
        {
            Main.Safe(() =>
            {
                ModFinderIO.Write(this, Main.AppPath("Settings.json"));
            });
        }
    }
}
