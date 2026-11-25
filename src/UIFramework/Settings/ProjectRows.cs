using System;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// A list the project keeps itself (<c>rowsFrom</c> on a <c>list</c>): notes in a file per category that its chat
    /// commands write under a lock of their own, rows in a database. The window neither reads nor writes them. It
    /// asks the project for the rows when the page is built and again when the field named in <c>rowsFrom</c> changes
    /// (a dropdown that picks the category), shows them to edit like any list, counts an edit as an unsaved change,
    /// and on Save hands the project the rows as they are now together with the rows it was given, so the project
    /// can tell what the user changed from what its own side changed meanwhile. The project answers the rows as it
    /// keeps them from then on. Nothing of such a list is in the settings file, an export or a reset.
    /// <para>Both questions travel as a <c>test</c> request with a name of the framework's own in front of the list's
    /// key, as the Fire button of a trigger does, so every settings action carries them.</para>
    /// </summary>
    public static class ProjectRows
    {
        /// <summary>In front of the list's key: the request for its rows, and the one that hands them back.</summary>
        public const string RowsRequest = "uiframework.rows:";
        public const string SaveRequest = "uiframework.saveRows:";
        /// <summary>Behind the list's key in the values of a save: the rows the window was given.</summary>
        public const string LoadedSuffix = "@loaded";

        public static bool Applies(Setting setting) => setting != null && setting.Kind == "list" && setting.RowsFrom != null;

        /// <summary>The rows in the project's answer, <c>{ "rows": [ … ] }</c>; null with the reason when it holds none:
        /// its <c>error</c>, that it did not answer, or what it said instead.</summary>
        public static JArray Rows(string answer, out string why)
        {
            why = null;
            if (Patient.IsNoAnswer(answer))
            {
                why = Patient.Why(answer);
                if (string.IsNullOrWhiteSpace(why)) why = "the project did not answer";
                return null;
            }
            JObject said = null;
            try { said = string.IsNullOrWhiteSpace(answer) ? null : JsonText.Parse(answer) as JObject; }
            catch (Exception) { }
            if (said?["error"] is JToken error && error.Type == JTokenType.String && ((string)error).Length > 0)
            {
                why = (string)error;
                return null;
            }
            if (said?["rows"] is JArray rows)
                return rows;
            string text = (answer ?? "").Trim();
            why = text.Length == 0 ? "the project named no rows" : text.Length > 200 ? text.Substring(0, 200) + "…" : text;
            return null;
        }
    }
}
