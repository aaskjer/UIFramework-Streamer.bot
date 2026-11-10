using System;
using System.IO;
using System.Linq;
using System.Net;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>Whether a channel is live, asked on the web for the host block's UiLive: the block must compile in an
    /// action that references nothing but mscorlib, so the requests are made here. Twitch through Helix with the token
    /// Streamer.bot holds, Kick through its public channel page. Every answer is "Live", "Offline" or "Unknown";
    /// nothing here throws, and a request that is not answered within six seconds is Unknown.</summary>
    public static class Live
    {
        public const string IsLive = "Live", IsOffline = "Offline", IsUnknown = "Unknown";

        /// <summary>The fetch behind the two questions: the body of a successful answer or null. Tests put their own in.</summary>
        public static Func<string, string, string, string> Fetch = Body;

        /// <param name="userId">The broadcaster's Twitch user id.</param>
        /// <param name="clientId">Streamer.bot's client id (CPH.TwitchClientId).</param>
        /// <param name="token">Its token (CPH.TwitchOAuthToken), with or without "oauth:".</param>
        public static string Twitch(string userId, string clientId, string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(token)) return IsUnknown;
                token = token.Trim();
                if (token.StartsWith("oauth:", StringComparison.OrdinalIgnoreCase)) token = token.Substring(6);
                return TwitchFromApi(Fetch("https://api.twitch.tv/helix/streams?user_id=" + Uri.EscapeDataString(userId.Trim()), clientId.Trim(), token));
            }
            catch (Exception) { return IsUnknown; }
        }

        /// <param name="login">The broadcaster's Kick login. Kick names a channel by its slug: the login in lower
        /// case, and where that is not found, with every underscore turned into a hyphen.</param>
        public static string Kick(string login)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(login)) return IsUnknown;
                string slug = login.Trim().ToLowerInvariant();
                string state = KickFromApi(Fetch("https://kick.com/api/v2/channels/" + Uri.EscapeDataString(slug), null, null));
                if (state == IsUnknown && slug.Contains("_"))
                    state = KickFromApi(Fetch("https://kick.com/api/v2/channels/" + Uri.EscapeDataString(slug.Replace('_', '-')), null, null));
                return state;
            }
            catch (Exception) { return IsUnknown; }
        }

        /// <summary>Helix /streams lists the channel while it is live and nothing while it is not.</summary>
        public static string TwitchFromApi(string body)
        {
            var data = Parse(body)?["data"] as JArray;
            if (data == null) return IsUnknown;
            return data.OfType<JObject>().Any(s => string.Equals((string)s["type"] ?? "live", "live", StringComparison.OrdinalIgnoreCase)) ? IsLive : IsOffline;
        }

        /// <summary>The channel's livestream is null while it is offline.</summary>
        public static string KickFromApi(string body)
        {
            var json = Parse(body);
            if (json == null || !json.TryGetValue("livestream", out JToken stream)) return IsUnknown;
            if (!(stream is JObject live)) return IsOffline;
            return live["is_live"] != null && live["is_live"].Type == JTokenType.Boolean && !(bool)live["is_live"] ? IsOffline : IsLive;
        }

        private static JObject Parse(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try { return JObject.Parse(body); }
            catch (Exception) { return null; }
        }

        // Short, since a timed action waits for it.
        private static string Body(string url, string clientId, string token)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = 6000;
                request.ReadWriteTimeout = 6000;
                request.UserAgent = "UIFramework-Streamer.bot/" + Entry.FrameworkVersion;
                request.Accept = "application/json";
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                if (clientId != null) request.Headers["Client-Id"] = clientId;
                if (token != null) request.Headers["Authorization"] = "Bearer " + token;
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                    return (int)response.StatusCode >= 200 && (int)response.StatusCode < 300 ? reader.ReadToEnd() : null;
            }
            catch (Exception) { return null; }
        }
    }
}
