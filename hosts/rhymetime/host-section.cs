    // ================================================================================================
    // HOST: RhymeTime
    // ================================================================================================

    private const string GameActionName = "RhymeTime Game";             // answers uiframework.describe, writes the overlay page
    private const string LogPrefix = "[RhymeTime/Settings]";
    private const string RhymeCommandId = "15e49f3a-389a-4190-ac15-c50b1fd4eee6";
    private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Chat Games", "RhymeTime", "RT-settings.json");
    private static readonly string OverlayDirectory = Path.Combine(Path.GetDirectoryName(SettingsPath), "Overlay");
    private static readonly string OverlayFile = Path.Combine(OverlayDirectory, "rhymetime-overlay.html");
    private static readonly string OverlayDemoFile = Path.Combine(OverlayDirectory, "rhymetime-overlay-demo.html");
    private const string ObsTransparentCss = "body { background-color: rgba(0, 0, 0, 0); margin: 0px auto; overflow: hidden; }";

    /// <summary>What the DLL calls. Public methods, string arguments, no framework types: found by reflection.
    /// Everything that needs CPH is queued as a job and run by Pump on Streamer.bot's thread.</summary>
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

        // The two cooldowns live on the !Rhyme command, where Streamer.bot reads them.
        public void OnSaved(string settingsJson)
        {
            var settings = JObject.Parse(settingsJson);
            _owner.Enqueue(() =>
            {
                _owner.CPH.CommandSetGlobalCooldownDuration(RhymeCommandId, Math.Max(0, (int?)settings["globalCooldown"] ?? 0));
                _owner.CPH.CommandSetUserCooldownDuration(RhymeCommandId, Math.Max(0, (int?)settings["userCooldown"] ?? 0));
            });
        }

        public string Test(string name, string settingsJson)
        {
            var settings = JObject.Parse(settingsJson);
            switch (name)
            {
                case "setupObsOverlay":
                    _owner.Enqueue(() => _owner.SetupObsOverlay(settings));
                    return "OBS setup queued; see the Streamer.bot log for the result.";
                case "copyOverlayUrl":
                {
                    string url = OverlayUrl(settings);
                    _owner.Enqueue(() => _owner.EnsureOverlayPage());
                    return CopyToClipboard(url)
                        ? "Copied. Paste it into the URL of a Browser source in OBS (1920 × 1080, 'Local file' unticked)."
                        : "The clipboard was busy; the URL is " + url;
                }
                case "openOverlayDemo":
                    _owner.Enqueue(() => { _owner.EnsureOverlayPage(); _owner.OpenPath(OverlayDemoFile); });
                    return "The demo opens in your browser. In OBS, add &demo=1 to the URL to place the source the same way.";
                case "openOverlayFolder":
                    _owner.Enqueue(() => { Directory.CreateDirectory(OverlayDirectory); _owner.OpenPath(OverlayDirectory); });
                    return "Opening " + OverlayDirectory;
                default:
                    return null;
            }
        }

        public string ReadTable(string key) => null;

        // What would not do what it looks like, one problem per line; the window asks before saving anyway.
        public string Validate(string settingsJson)
        {
            var s = JObject.Parse(settingsJson);
            var problems = new List<string>();
            long min = (long?)s["minBet"] ?? 0, max = (long?)s["maxBet"] ?? 0, fallback = (long?)s["defaultBet"] ?? 0;
            if (min > 0 && max > 0 && min > max)
                problems.Add("The minimum bet (" + min + ") is above the maximum (" + max + "), so every stake is refused.");
            if (fallback > 0 && ((min > 0 && fallback < min) || (max > 0 && fallback > max)))
                problems.Add("The default bet (" + fallback + ") is outside the minimum and maximum, so !Rhyme without an amount is refused.");
            if (fallback > 0 && string.IsNullOrWhiteSpace((string)s["pointsVariable"]))
                problems.Add("The default bet is above 0 but the points variable is empty, so no stake can be taken.");
            if (((int?)s["timeLimit"] ?? 30) < 5)
                problems.Add("Less than 5 seconds to answer leaves no time to type a rhyme.");
            return problems.Count == 0 ? null : string.Join("\n", problems);
        }
        public IEnumerable<string> ReadLogLines() => null;
    }

    private static string OverlayUrl(JObject settings)
    {
        int port = (int?)settings["overlayPort"] ?? 8080;
        if (port < 1 || port > 65535) port = 8080;
        return new Uri(OverlayFile).AbsoluteUri + "?port=" + port.ToString(CultureInfo.InvariantCulture);
    }

    // The clipboard wants an STA thread; the window's thread is one, but this runs where the DLL calls it.
    private static bool CopyToClipboard(string text)
    {
        bool ok = false;
        var thread = new Thread(() =>
        {
            try { System.Windows.Clipboard.SetText(text); ok = true; }
            catch (Exception) { ok = false; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(3000);
        return ok;
    }

    // The game action carries the overlay page and writes it when asked; the demo page beside it adds ?demo=1
    // itself, because Windows drops a query string when it hands a file:// address to the browser.
    private void EnsureOverlayPage()
    {
        try
        {
            CPH.SetArgument("rhymetime.writeOverlay", true);
            CPH.RunAction(GameActionName, true);
            if (!File.Exists(OverlayFile))
                CPH.LogWarn(LogPrefix + " The overlay page was not written to " + OverlayFile + "; is the '" + GameActionName + "' action enabled?");
        }
        catch (Exception ex)
        {
            CPH.LogWarn(LogPrefix + " Could not write the overlay page: " + ex.Message);
        }
    }

    private void OpenPath(string path)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { CPH.LogWarn(LogPrefix + " Could not open " + path + ": " + ex.Message); }
    }

    private void SetupObsOverlay(JObject settings)
    {
        int connectionId = Math.Max(0, (int?)settings["obsConnectionId"] ?? 0);
        string sceneName = ((string)settings["obsSceneName"] ?? "").Trim();
        string sourceName = ((string)settings["obsSourceName"] ?? "").Trim();
        if (sceneName.Length == 0) sceneName = "RhymeTime";
        if (sourceName.Length == 0) sourceName = "RhymeTime Overlay";
        try
        {
            if (!CPH.ObsIsConnected(connectionId))
            {
                CPH.LogWarn(LogPrefix + " OBS is not connected on connection #" + connectionId + "; nothing was set up. Check Stream Apps > OBS Studio in Streamer.bot.");
                return;
            }
            EnsureOverlayPage();
            string url = OverlayUrl(settings);

            // Source names are global in OBS, not per scene, so a name already taken anywhere is left alone.
            var inputs = ObsNames(CPH.ObsSendRaw("GetInputList", "{}", connectionId), "inputs", "inputName");
            if (inputs.Any(n => string.Equals(n, sourceName, StringComparison.OrdinalIgnoreCase)))
            {
                CPH.LogInfo(LogPrefix + " OBS already has a source called '" + sourceName + "'; nothing was changed.");
                return;
            }
            var scenes = ObsNames(CPH.ObsSendRaw("GetSceneList", "{}", connectionId), "scenes", "sceneName");
            if (!scenes.Any(n => string.Equals(n, sceneName, StringComparison.OrdinalIgnoreCase)))
                CPH.ObsSendRaw("CreateScene", new JObject { ["sceneName"] = sceneName }.ToString(Newtonsoft.Json.Formatting.None), connectionId);

            int width = 1920, height = 1080;
            try
            {
                var video = JObject.Parse(CPH.ObsSendRaw("GetVideoSettings", "{}", connectionId) ?? "{}");
                int w = video.SelectTokens("..baseWidth").Select(t => (int?)t).FirstOrDefault() ?? 0;
                int h = video.SelectTokens("..baseHeight").Select(t => (int?)t).FirstOrDefault() ?? 0;
                if (w > 0 && h > 0) { width = w; height = h; }
            }
            catch (Exception ex) { CPH.LogWarn(LogPrefix + " Could not read the OBS canvas size, using 1920x1080: " + ex.Message); }

            var payload = new JObject
            {
                ["sceneName"] = sceneName,
                ["inputName"] = sourceName,
                ["inputKind"] = "browser_source",
                ["inputSettings"] = new JObject
                {
                    ["is_local_file"] = false,
                    ["url"] = url,
                    ["width"] = width,
                    ["height"] = height,
                    ["css"] = ObsTransparentCss
                }
            };
            CPH.ObsSendRaw("CreateInput", payload.ToString(Newtonsoft.Json.Formatting.None), connectionId);
            CPH.LogInfo(LogPrefix + " '" + sourceName + "' is in scene '" + sceneName + "' at " + width + "x" + height + ", pointing at " + url);
        }
        catch (Exception ex)
        {
            CPH.LogError(LogPrefix + " OBS overlay setup failed: " + ex.Message + " (usually the connection # not matching what Streamer.bot has configured)");
        }
    }

    private List<string> ObsNames(string rawJson, string arrayKey, string nameKey)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(rawJson)) return result;
        try
        {
            if (JObject.Parse(rawJson).SelectToken(".." + arrayKey) is JArray array)
                foreach (var item in array)
                {
                    string name = item[nameKey]?.ToString();
                    if (!string.IsNullOrEmpty(name)) result.Add(name);
                }
        }
        catch (Exception ex) { CPH.LogError(LogPrefix + " Could not read the OBS reply: " + ex.Message); }
        return result;
    }
