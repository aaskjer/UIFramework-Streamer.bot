    // ================================================================================================
    // HOST: High-Low Game
    // ================================================================================================

    private const string GameActionName = "[HLG] - High-Low Game";
    private const string LogPrefix = "[High-Low Game/Settings]";
    private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Chat Games", "High-Low Game", "HLG-Settings.json");

    // Command id, then the settings keys of its global and per-viewer cooldown.
    private static readonly string[][] CommandCooldowns =
    {
        new[] { "dc2d4f17-d5f1-4228-923e-5c03652bb30f", "highGlobalCooldown", "highUserCooldown" },
        new[] { "2b7f7ba1-b8a6-4b45-b2ef-31b1ee61f7be", "lowGlobalCooldown", "lowUserCooldown" },
        new[] { "36a22b8f-5e63-4df7-ae8f-3439dd7d6475", "equalGlobalCooldown", "equalUserCooldown" },
        new[] { "68f97444-4f77-4bab-8277-2a920fcdfddf", "alltimeGlobalCooldown", "alltimeUserCooldown" },
        new[] { "5911cfbc-32cb-4749-bb40-3fce8eb76a1a", "sessionGlobalCooldown", "sessionUserCooldown" },
        new[] { "df94341a-8b15-4c6c-9e1a-6af383915f8b", "personalGlobalCooldown", "personalUserCooldown" }
    };

    public sealed class Host
    {
        private readonly CPHInline _owner;
        public Host(CPHInline owner) { _owner = owner; }

        public void Log(string level, string message)
        {
            if (level == "error") _owner.CPH.LogError(message);
            else if (level == "warn") _owner.CPH.LogWarn(message);
            else _owner.CPH.LogInfo(message);
        }

        public void OnSaved(string settingsJson)
        {
            var settings = JObject.Parse(settingsJson);
            _owner.Enqueue(() =>
            {
                foreach (var command in CommandCooldowns)
                {
                    _owner.CPH.CommandSetGlobalCooldownDuration(command[0], Math.Max(0, (int?)settings[command[1]] ?? 0));
                    _owner.CPH.CommandSetUserCooldownDuration(command[0], Math.Max(0, (int?)settings[command[2]] ?? 0));
                }
            });
        }

        public string Test(string name, string settingsJson) => null;
        public string ReadTable(string key) => null;
        public IEnumerable<string> ReadLogLines() => null;
    }
