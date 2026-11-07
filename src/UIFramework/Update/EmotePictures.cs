using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// Pictures for emote cells. Kick emotes carry their id in the code (<c>[emote:ID:name]</c>, or just
    /// <c>ID:name</c>), so their picture is a URL away. Twitch emotes are typed by name: the picture is looked up
    /// once per window in the channel's and the global Twitch, 7TV, BTTV and FFZ emotes, with what the host hands
    /// over in <see cref="IHostHooks.EmoteSources"/> (the broadcaster's Twitch user id for the channel sets, client
    /// id and token for the Twitch API). Without a token, Twitch's global emotes come from the list embedded from
    /// conventions/emotes/twitch.json; without a host, that list and the global 7TV, BTTV and FFZ sets. YouTube's
    /// own emojis are typed as <c>:name:</c> and found in the table embedded from conventions/emotes/youtube.json,
    /// which the host's <c>youtube</c> object in the emote sources may extend; a Unicode emoji shows itself.
    /// </summary>
    public static class EmotePictures
    {
        private static readonly ConcurrentDictionary<string, Task<ImageSource>> Pictures = new ConcurrentDictionary<string, Task<ImageSource>>(StringComparer.Ordinal);
        private static readonly object Gate = new object();
        private static Task<Dictionary<string, (string Url, string Source)>> _twitch;
        private static DateTime _twitchAsked;
        private static string _twitchSources;
        // The host's answer per window (one IHostHooks each): asking the game costs a trip to Streamer.bot's thread
        // and up to five seconds when it hangs, so it is asked once, not once per emote cell.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IHostHooks, SourcesBox> Sources =
            new System.Runtime.CompilerServices.ConditionalWeakTable<IHostHooks, SourcesBox>();

        private sealed class SourcesBox { public string Json; }

        /// <summary>The host's emote sources, asked once per hooks object (per window); null without a host or an answer.</summary>
        public static string SourcesOf(IHostHooks hooks)
        {
            if (hooks == null) return null;
            return Sources.GetValue(hooks, h =>
            {
                string json = null;
                try { json = h.EmoteSources(); } catch (Exception) { }
                return new SourcesBox { Json = json };
            }).Json;
        }

        /// <summary>A Kick emote as chat renders it, <c>[emote:ID:name]</c>, or typed short as <c>ID:name</c>: group 1
        /// is the id, group 2 the name.</summary>
        public static readonly Regex KickCode = new Regex(@"^(?:\[emote:)?([0-9]+):([^\[\]\s:]+)\]?$", RegexOptions.Compiled);
        /// <summary>One of YouTube's own emojis as chat types it, <c>:name:</c>: group 1 is the name.</summary>
        public static readonly Regex YouTubeCode = new Regex(@"^:([A-Za-z0-9_-]+):$", RegexOptions.Compiled);
        private static Dictionary<string, string> _youtubeTable, _twitchTable;

        /// <summary>The name shown beneath an emote's picture: a Kick code's name, a YouTube shortcode without its
        /// colons, anything else as it is.</summary>
        public static string NameOf(string code)
        {
            code = (code ?? "").Trim();
            var kick = KickCode.Match(code);
            if (kick.Success) return kick.Groups[2].Value;
            var youtube = YouTubeCode.Match(code);
            return youtube.Success ? youtube.Groups[1].Value : code;
        }

        /// <summary>The picture of one of YouTube's own emojis, or null for anything the table doesn't know (a
        /// Unicode emoji, a BTTV emote, a typo). The host's <c>youtube</c> object (<c>{ ":name:": url }</c>) wins
        /// over the embedded table.</summary>
        public static string YouTubeUrl(string code, IHostHooks hooks, int size = 48)
        {
            code = (code ?? "").Trim();
            if (!YouTubeCode.IsMatch(code))
                return null;
            string url = null;
            try
            {
                string sources = SourcesOf(hooks);
                if (!string.IsNullOrWhiteSpace(sources) && JObject.Parse(sources)["youtube"] is JObject own)
                    url = (string)own[code];
            }
            catch (Exception) { }
            if (string.IsNullOrEmpty(url) && !YouTubeTable().TryGetValue(code, out url))
                return null;
            return url.Contains("=") ? url : url + "=w" + size + "-h" + size + "-c-k-nd";
        }

        private static Dictionary<string, string> YouTubeTable()
        {
            lock (Gate)
                return _youtubeTable ?? (_youtubeTable = EmbeddedTable("conventions.emotes.youtube.json", "emojis"));
        }

        /// <summary>Twitch's global emotes by name and their id, from conventions/emotes/twitch.json: the pictures
        /// of Kappa, LUL and the like when the game hands over no Twitch token (no Twitch login yet).</summary>
        public static Dictionary<string, string> TwitchGlobalTable()
        {
            lock (Gate)
                return _twitchTable ?? (_twitchTable = EmbeddedTable("conventions.emotes.twitch.json", "emotes"));
        }

        private static Dictionary<string, string> EmbeddedTable(string resource, string section)
        {
            var table = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                var assembly = typeof(EmotePictures).Assembly;
                string name = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(resource, StringComparison.OrdinalIgnoreCase));
                if (name != null)
                {
                    string json;
                    using (var stream = assembly.GetManifestResourceStream(name))
                    using (var reader = new StreamReader(stream))
                        json = reader.ReadToEnd();
                    foreach (var p in (JObject.Parse(json)[section] as JObject)?.Properties() ?? Enumerable.Empty<JProperty>())
                        if (!string.IsNullOrEmpty((string)p.Value)) table[p.Name] = (string)p.Value;
                }
            }
            catch (Exception) { }
            return table;
        }

        /// <summary>The picture URL of a Twitch emote by its id, with <c>{theme}</c> for dark or light.</summary>
        private static string TwitchUrl(string id) =>
            "https://static-cdn.jtvnw.net/emoticons/v2/" + Uri.EscapeDataString(id) + "/default/{theme}/2.0";

        /// <summary>A 22-px image beside an emote cell, filled in once the picture is known. The name of the extension
        /// a Twitch emote comes from (7TV, BTTV, FFZ) goes to <paramref name="onSource"/>, for a hint.</summary>
        public static Image Picture(string platform, string code, IHostHooks hooks, Action<string> onSource = null)
        {
            var image = new Image { Width = 22, Height = 22, Margin = new Thickness(0, 4, 6, 0), VerticalAlignment = VerticalAlignment.Top };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            Show(image, platform, code, hooks, onSource);
            return image;
        }

        /// <summary>Points the image at the picture of the code, or clears it when there is none.</summary>
        public static void Show(Image image, string platform, string code, IHostHooks hooks, Action<string> onSource = null)
        {
            image.Source = null;
            image.ToolTip = null;
            code = (code ?? "").Trim();
            if (code.Length == 0)
                return;
            string theme = Theme.IsDark ? "dark" : "light";
            if (string.Equals(platform, "kick", StringComparison.OrdinalIgnoreCase))
            {
                var m = KickCode.Match(code);
                if (m.Success)
                    Load(image, "https://files.kick.com/emotes/" + m.Groups[1].Value + "/fullsize");
                return;
            }
            if (string.Equals(platform, "youtube", StringComparison.OrdinalIgnoreCase))
            {
                string url = YouTubeUrl(code, hooks);
                if (url != null)
                    Load(image, url);
                return;
            }
            if (!string.Equals(platform, "twitch", StringComparison.OrdinalIgnoreCase))
                return;
            var dispatcher = image.Dispatcher;
            TwitchLookup(hooks).ContinueWith(lookup =>
            {
                if (lookup.Status != TaskStatus.RanToCompletion || !lookup.Result.TryGetValue(code, out var found))
                    return;
                dispatcher.BeginInvoke(new Action(() =>
                {
                    Load(image, found.Url.Replace("{theme}", theme));
                    if (found.Source != null)
                    {
                        image.ToolTip = "A " + found.Source + " emote: chat only shows it as one to viewers with the " + found.Source + " extension.";
                        onSource?.Invoke(found.Source);
                    }
                }));
            }, TaskScheduler.Default);
        }

        private static void Load(Image image, string url)
        {
            var dispatcher = image.Dispatcher;
            var cached = Pictures.GetOrAdd(url, u => Task.Run(() => Download(u)));
            if (cached.Status == TaskStatus.RanToCompletion && cached.Result != null)
            {
                image.Source = cached.Result;
                return;
            }
            cached.ContinueWith(download =>
            {
                var picture = download.Status == TaskStatus.RanToCompletion ? download.Result : null;
                if (picture == null)
                {
                    Pictures.TryRemove(url, out _);
                    return;
                }
                dispatcher.BeginInvoke(new Action(() => image.Source = picture));
            }, TaskScheduler.Default);
        }

        private static ImageSource Download(string url)
        {
            try
            {
                byte[] bytes;
                using (var client = new WebClient())
                    bytes = client.DownloadData(url);
                using (var ms = new MemoryStream(bytes))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = ms;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch (Exception) { return null; }
        }

        /// <summary>Emote name to picture URL (with <c>{theme}</c> for Twitch's own) and the extension it comes from.
        /// Asked once and kept ten minutes, or until a window brings other sources (asked once per window).</summary>
        public static Task<Dictionary<string, (string Url, string Source)>> TwitchLookup(IHostHooks hooks)
        {
            string sources = SourcesOf(hooks);
            lock (Gate)
            {
                if (_twitch == null || DateTime.UtcNow - _twitchAsked > TimeSpan.FromMinutes(10) || _twitchSources != sources)
                {
                    _twitchAsked = DateTime.UtcNow;
                    _twitchSources = sources;
                    string captured = sources;
                    _twitch = Task.Run(() => LookUp(captured));
                }
                return _twitch;
            }
        }

        private static Dictionary<string, (string Url, string Source)> LookUp(string sourcesJson)
        {
            var pictures = new Dictionary<string, (string Url, string Source)>(StringComparer.Ordinal);
            string channel = null, clientId = null, token = null;
            try
            {
                var o = string.IsNullOrWhiteSpace(sourcesJson) ? new JObject() : JObject.Parse(sourcesJson);
                channel = (string)o["twitchUserId"];
                clientId = (string)o["clientId"];
                token = (string)o["token"];
            }
            catch (Exception) { }
            if (token != null && token.StartsWith("oauth:", StringComparison.OrdinalIgnoreCase))
                token = token.Substring("oauth:".Length);
            bool twitch = !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(token);
            bool ofChannel = !string.IsNullOrEmpty(channel);
            string id = Uri.EscapeDataString(channel ?? "");

            var asks = new List<(string Source, Task<List<string>> Answer)>();
            void Ask(bool when, string source, string url)
            {
                if (when) asks.Add((source, Task.Run(() => FetchPages(url, source == "twitch" ? clientId : null, token))));
            }
            Ask(twitch && ofChannel, "twitch", "https://api.twitch.tv/helix/chat/emotes?broadcaster_id=" + id);
            Ask(twitch && ofChannel, "twitch", "https://api.twitch.tv/helix/chat/emotes/user?user_id=" + id);
            Ask(twitch, "twitch", "https://api.twitch.tv/helix/chat/emotes/global");
            Ask(ofChannel, "7tv", "https://7tv.io/v3/users/twitch/" + id);
            Ask(ofChannel, "bttv", "https://api.betterttv.net/3/cached/users/twitch/" + id);
            Ask(ofChannel, "ffz", "https://api.betterttv.net/3/cached/frankerfacez/users/twitch/" + id);
            Ask(true, "7tv", "https://7tv.io/v3/emote-sets/global");
            Ask(true, "bttv", "https://api.betterttv.net/3/cached/emotes/global");
            Ask(true, "ffz", "https://api.betterttv.net/3/cached/frankerfacez/emotes/global");

            // What Twitch itself answers comes first; then Twitch's global emotes from the embedded list, so Kappa
            // shows without a Twitch login (and when the API is down); then 7TV, BTTV and FFZ.
            bool globals = false;
            void AddGlobals()
            {
                if (globals) return;
                globals = true;
                foreach (var e in TwitchGlobalTable())
                    if (!pictures.ContainsKey(e.Key))
                        pictures[e.Key] = (TwitchUrl(e.Value), null);
            }
            foreach (var (source, answer) in asks)
            {
                if (source != "twitch") AddGlobals();
                List<string> pages;
                try { pages = answer.GetAwaiter().GetResult(); }
                catch (Exception) { continue; }   // offline, refused or too slow: the other sources still count
                foreach (var page in pages)
                {
                    try { Read(source, page, pictures); }
                    catch (Exception) { }         // an answer in a shape this doesn't know adds nothing
                }
            }
            AddGlobals();
            return pictures;
        }

        private static List<string> FetchPages(string url, string clientId, string token)
        {
            var pages = new List<string>();
            string after = null;
            do
            {
                string pageUrl = after == null ? url : url + (url.Contains("?") ? "&" : "?") + "after=" + Uri.EscapeDataString(after);
                var request = (HttpWebRequest)WebRequest.Create(pageUrl);
                request.Timeout = 5000;
                request.UserAgent = "UIFrameworkForStreamerbot/1.0";
                if (clientId != null)
                {
                    request.Headers["Client-Id"] = clientId;
                    request.Headers["Authorization"] = "Bearer " + token;
                }
                string json;
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                    json = reader.ReadToEnd();
                pages.Add(json);
                after = clientId == null ? null : (string)(JObject.Parse(json)["pagination"] as JObject)?["cursor"];
            }
            while (!string.IsNullOrEmpty(after) && pages.Count < 20);
            return pages;
        }

        private static void Read(string source, string json, IDictionary<string, (string Url, string Source)> into)
        {
            void Add(string code, string id, Func<string, string> url, string from)
            {
                if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(id) && !into.ContainsKey(code))
                    into[code] = (url(id), from);
            }

            var answer = JToken.Parse(json);
            switch (source)
            {
                case "twitch":
                    foreach (var e in answer["data"] as JArray ?? new JArray())
                        Add((string)e["name"], (string)e["id"], TwitchUrl, null);
                    break;
                case "7tv":
                    var set = answer["emote_set"] as JObject ?? answer as JObject;
                    foreach (var e in set?["emotes"] as JArray ?? new JArray())
                    {
                        bool animated = (bool?)(e["data"] as JObject)?["animated"] == true;
                        Add((string)e["name"], (string)e["id"], i => "https://cdn.7tv.app/emote/" + i + (animated ? "/2x_static.png" : "/2x.png"), "7TV");
                    }
                    break;
                case "bttv":
                    var lists = answer is JArray ? new[] { answer } : new[] { answer["channelEmotes"], answer["sharedEmotes"] };
                    foreach (var list in lists.OfType<JArray>())
                        foreach (var e in list)
                            Add((string)e["code"], (string)e["id"], i => "https://cdn.betterttv.net/emote/" + i + "/2x", "BTTV");
                    break;
                case "ffz":
                    foreach (var e in answer as JArray ?? new JArray())
                    {
                        var images = e["images"] as JObject;
                        Add((string)e["code"], (string)(images?["2x"] ?? images?["1x"]), i => i, "FFZ");
                    }
                    break;
            }
        }
    }
}
