// stub/HostReader.cs: the UIFramework host reader, for a project with several actions. One action carries the host
// block (stub/HostSchema.cs) with the table and answers the settings window; every other action that only reads the
// settings carries this instead. It knows no table: a reader names the keys it reads, with what holds while the
// file has none.
// See docs/Getting-Started.md, "A project with several actions".

// --- UIFramework host reader (the same in every action of this project that only reads the settings) ---
// From UIFramework/stub/HostReader.cs (tools/update-host-block.ps1 -Reader refreshes it). Needs no usings and no
// reference beyond Newtonsoft.Json, which every action has: paste it as it is, right below the action's using lines.
// An action carries the host block or this, never both: each declares UiSettings.
//
// In Execute():
//     var settings = UiSettings.Load("My Project/settings.json", CPH.LogWarn).With(args, "webhookUrl", "dryRun");
//     string url = settings.String("webhookUrl");
//     bool dryRun = settings.Bool("dryRun");
//     int perHour = settings.Int("postsPerHour", 6);
//
// The keys are the ones the host action's table declares, in its spelling: the window writes those. A key is not
// renamed in a project with readers, since a reader would go on looking for the old name.

/// <summary>The settings file as an action without the table reads it. A key the file does not hold reads as the
/// fallback the call names.</summary>
public sealed class UiSettings
{
    private readonly Newtonsoft.Json.Linq.JObject _root;

    private UiSettings(Newtonsoft.Json.Linq.JObject root) { _root = root; }

    /// <summary>Settings from JSON text, for tests and callers that already hold the values; empty text is empty settings.</summary>
    public static UiSettings Parse(string json)
    {
        try
        {
            using (var reader = new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(string.IsNullOrWhiteSpace(json) ? "{}" : json)) { DateParseHandling = Newtonsoft.Json.DateParseHandling.None })
                return new UiSettings(Newtonsoft.Json.Linq.JObject.Load(reader));
        }
        catch (System.Exception)
        {
            return new UiSettings(new Newtonsoft.Json.Linq.JObject());
        }
    }

    /// <summary>The values as they are, for an action that reads them its own way.</summary>
    public Newtonsoft.Json.Linq.JObject Raw => _root;

    /// <summary>Why the settings did not come from the file, or null: it could not be read, and the backup of the
    /// window's last save (FromBackup) or nothing stands in for it.</summary>
    public string Problem { get; private set; }
    public bool FromBackup { get; private set; }

    /// <summary>Never throws: an unreadable file gives the .bak the window's last save kept, else nothing (every key
    /// then reads as its fallback), and the action keeps running; Problem says so, and log (CPH.LogWarn, say) hears
    /// it. A file that is missing while a save is under way is waited for a moment, and one still missing beside its
    /// .bak is read from that; only a file never saved gives nothing without a word. A relative path starts in the
    /// Streamer.bot folder, whatever the current directory.</summary>
    public static UiSettings Load(string path, System.Action<string> log = null)
    {
        if (string.IsNullOrEmpty(path)) return new UiSettings(new Newtonsoft.Json.Linq.JObject());
        try
        {
            if (!System.IO.Path.IsPathRooted(path)) path = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, path);
            // The window saves beside the file and swaps it in (File.Replace, the old one becoming the .bak): for a
            // moment there is no file, and a swap that failed halfway leaves only the .bak.
            for (int attempt = 1; !System.IO.File.Exists(path); attempt++)
            {
                bool backup = System.IO.File.Exists(path + ".bak"), saving = System.IO.File.Exists(path + ".tmp");
                if (!backup && (attempt >= 5 || !saving)) return new UiSettings(new Newtonsoft.Json.Linq.JObject());
                // Only a save under way (its .tmp) is waited for. Without one the file is looked for once more, as a
                // swap may have ended between the looks; still missing, it is read from the .bak at once, not after
                // 160 ms on every load.
                if (attempt >= 5 || (!saving && !System.IO.File.Exists(path))) throw new System.IO.FileNotFoundException("it is missing", path);
                if (saving) System.Threading.Thread.Sleep(40);
            }
            return new UiSettings(Read(path));
        }
        catch (System.Exception ex)
        {
            string file;
            try { file = System.IO.Path.GetFileName(path); } catch (System.Exception) { file = path; }
            string why = file + " could not be read (" + ex.Message + ")";
            UiSettings stand;
            try { stand = System.IO.File.Exists(path + ".bak") ? new UiSettings(Read(path + ".bak")) { FromBackup = true, Problem = why + "; the backup of the last save is used." } : null; }
            catch (System.Exception) { stand = null; }
            if (stand == null) stand = new UiSettings(new Newtonsoft.Json.Linq.JObject()) { Problem = why + "; the defaults are used." };
            if (log != null) try { log(stand.Problem); } catch (System.Exception) { }
            return stand;
        }
    }

    private static Newtonsoft.Json.Linq.JObject Read(string path)
    {
        using (var reader = new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(ReadWithRetry(path))) { DateParseHandling = Newtonsoft.Json.DateParseHandling.None })
            return Newtonsoft.Json.Linq.JObject.Load(reader);
    }

    private static string ReadWithRetry(string path)
    {
        for (int attempt = 1; ; attempt++)
        {
            try { return System.IO.File.ReadAllText(path); }
            catch (System.IO.IOException) when (attempt < 5) { System.Threading.Thread.Sleep(40); }
        }
    }

    /// <summary>The settings with this run's arguments over them, for the keys named: a Set Argument sub-action of
    /// that name wins over the file while it holds something (an empty one does not). So a value can differ per
    /// action. The argument is kept as it came; Bool, Int and the others read text as the host action reads it
    /// ("yes", "1", "on" for a switch; "2,5" for a number).</summary>
    public UiSettings With(System.Collections.Generic.IDictionary<string, object> args, params string[] keys)
    {
        if (args == null || args.Count == 0 || keys == null) return this;
        Newtonsoft.Json.Linq.JObject root = null;
        foreach (var key in keys)
        {
            object raw;
            if (key == null || !args.TryGetValue(key, out raw) || raw == null) continue;
            if (raw is string && ((string)raw).Trim().Length == 0) continue;
            if (root == null) root = (Newtonsoft.Json.Linq.JObject)_root.DeepClone();
            root[key] = raw is string ? (Newtonsoft.Json.Linq.JToken)((string)raw).Trim() : Newtonsoft.Json.Linq.JToken.FromObject(raw);
        }
        return root == null ? this : new UiSettings(root) { Problem = Problem, FromBackup = FromBackup };
    }

    /// <summary>The value as the file holds it, or null: nothing is filled in here.</summary>
    public Newtonsoft.Json.Linq.JToken Token(string key)
    {
        var token = _root[key];
        return token != null && token.Type != Newtonsoft.Json.Linq.JTokenType.Null ? token : null;
    }

    /// <summary>Whether the file (or an argument) holds the key at all.</summary>
    public bool Has(string key) => Token(key) != null;

    public bool Bool(string key, bool fallback = false)
    {
        var t = Token(key);
        if (t == null) return fallback;
        if (t.Type == Newtonsoft.Json.Linq.JTokenType.Boolean) return (bool)t;
        switch (t.ToString().Trim().ToLowerInvariant())
        {
            case "true": case "yes": case "on": case "1": case "ja": return true;
            case "false": case "no": case "off": case "0": case "nein": return false;
            default: return fallback;
        }
    }

    public long Long(string key, long fallback = 0)
    {
        // A whole number as it is: through a double, one past 2^53 would lose its last digits.
        var t = Token(key);
        if (t == null) return fallback;
        if (t.Type == Newtonsoft.Json.Linq.JTokenType.Integer)
        {
            try { return (long)t; }
            catch (System.Exception) { return t.ToString().TrimStart().StartsWith("-") ? long.MinValue : long.MaxValue; }
        }
        double d;
        if (!Number(t, out d)) return fallback;
        if (d >= long.MaxValue) return long.MaxValue;
        if (d <= long.MinValue) return long.MinValue;
        return (long)System.Math.Round(d);
    }

    public int Int(string key, int fallback = 0) { long l = Long(key, fallback); return l > int.MaxValue ? int.MaxValue : l < int.MinValue ? int.MinValue : (int)l; }

    public double Double(string key, double fallback = 0)
    {
        var t = Token(key);
        double d;
        return t != null && Number(t, out d) ? d : fallback;
    }

    private static bool Number(Newtonsoft.Json.Linq.JToken t, out double d)
    {
        // A whole number beyond long is a BigInteger to Json.NET, which Convert cannot take: read from its digits.
        var number = (t.Type == Newtonsoft.Json.Linq.JTokenType.Integer || t.Type == Newtonsoft.Json.Linq.JTokenType.Float) ? ((Newtonsoft.Json.Linq.JValue)t).Value : null;
        if (number is System.IConvertible) d = System.Convert.ToDouble(number, System.Globalization.CultureInfo.InvariantCulture);
        else if (!double.TryParse(t.ToString().Trim().Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) return false;
        return !double.IsNaN(d) && !double.IsInfinity(d);
    }

    /// <summary>The value as text, numbers with a point whatever the system's culture ("0.5", not "0,5").</summary>
    public string String(string key, string fallback = "")
    {
        var t = Token(key);
        if (t == null) return fallback;
        if (t.Type == Newtonsoft.Json.Linq.JTokenType.String) return (string)t;
        if (t.Type == Newtonsoft.Json.Linq.JTokenType.Float) return System.Convert.ToDouble(((Newtonsoft.Json.Linq.JValue)t).Value, System.Globalization.CultureInfo.InvariantCulture).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        if (t.Type == Newtonsoft.Json.Linq.JTokenType.Integer) return System.Convert.ToString(((Newtonsoft.Json.Linq.JValue)t).Value, System.Globalization.CultureInfo.InvariantCulture);
        if (t.Type == Newtonsoft.Json.Linq.JTokenType.Boolean) return (bool)t ? "true" : "false";
        return t.ToString(Newtonsoft.Json.Formatting.None);
    }

    /// <summary>The values of a setting that holds several, stored as text with commas between them: the ticked
    /// options of a multi field ("steam,gog" is steam and gog), the names of a namelist or a grouplist. Without the
    /// blanks around a value, without empty and repeated ones; none is an empty array. The fallback is such a text
    /// too, as the host action's table has it for a default. The window writes an option's value in the option's own
    /// spelling; a file written by hand may hold another case, which is not put right here without the table.</summary>
    public string[] Values(string key, string fallback = "")
    {
        var values = new System.Collections.Generic.List<string>();
        var t = Token(key);
        // An array in the file (written by hand) is read as its values.
        var parts = new System.Collections.Generic.List<string>();
        if (t is Newtonsoft.Json.Linq.JArray)
        {
            foreach (var item in (Newtonsoft.Json.Linq.JArray)t)
                if (item is Newtonsoft.Json.Linq.JValue && item.Type != Newtonsoft.Json.Linq.JTokenType.Null)
                    parts.Add(item.Type == Newtonsoft.Json.Linq.JTokenType.String ? (string)item : item.ToString(Newtonsoft.Json.Formatting.None));
        }
        else if (!(t is Newtonsoft.Json.Linq.JContainer)) parts.AddRange((t == null ? fallback ?? "" : String(key)).Split(','));
        foreach (var part in parts)
        {
            string value = part.Trim();
            bool have = value.Length == 0;
            foreach (var other in values)
                if (string.Equals(other, value, System.StringComparison.OrdinalIgnoreCase)) have = true;
            if (!have) values.Add(value);
        }
        return values.ToArray();
    }

    public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    public static long Clamp(long value, long min, long max) => value < min ? min : value > max ? max : value;
    public static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;
}
// --- end UIFramework host reader ---
