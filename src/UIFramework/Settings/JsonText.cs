using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>JSON a game answers with, read the way the settings file is: a text that looks like a date stays the
    /// text it is. Json.NET's own Parse turns "2026-11-10T20:15:00" into a date, which a table cell or a field then
    /// showed as "11/10/2026 20:15:00".</summary>
    public static class JsonText
    {
        public static JToken Parse(string json)
        {
            using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
                return JToken.Load(reader);
        }
    }
}
