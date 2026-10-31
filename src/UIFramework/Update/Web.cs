using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>Opening links, loading logos, asking GitHub for the newest release.</summary>
    public static class Web
    {
        public static void OpenUrl(string url, IHostLog log, string prefix)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                log?.Warn((prefix ?? "[UIFramework]") + " Failed to open URL " + ex.Message);
            }
        }

        public static void OpenFolder(string folder, IHostLog log, string prefix)
        {
            try
            {
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                log?.Warn((prefix ?? "[UIFramework]") + " Could not open folder " + ex.Message);
            }
        }

        public static void LoadImageAsync(Image target, string url, Action<ImageSource> onLoaded = null)
        {
            if (string.IsNullOrWhiteSpace(url) || target == null)
                return;
            var dispatcher = target.Dispatcher;
            Task.Run(() =>
            {
                try
                {
                    byte[] bytes;
                    using (var client = new System.Net.WebClient())
                        bytes = client.DownloadData(url);

                    using (var ms = new MemoryStream(bytes))
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.StreamSource = ms;
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();

                        dispatcher.BeginInvoke(new Action(() =>
                        {
                            target.Source = bmp;
                            onLoaded?.Invoke(bmp);
                        }));
                    }
                }
                catch { }
            });
        }

        /// <summary>Asks GitHub for the newest release and returns its tag only when it is genuinely newer than
        /// the running version. Comparing the strings instead would announce an "update" for every tag that is
        /// merely different, an older one included.</summary>
        public static string LatestRelease(string gitHubRepo, string currentVersion, IHostLog log, string prefix)
        {
            if (string.IsNullOrWhiteSpace(gitHubRepo))
                return null;
            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) })
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "StreamerbotVersionChecker/1.0");
                    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
                    string json = client.GetStringAsync("https://api.github.com/repos/" + gitHubRepo + "/releases/latest").GetAwaiter().GetResult();
                    string latestTag = JObject.Parse(json)["tag_name"]?.ToString();
                    if (!IsNewerVersion(latestTag, currentVersion))
                        return null;
                    log?.Info(prefix + " Update available " + latestTag);
                    return latestTag;
                }
            }
            catch (Exception ex)
            {
                log?.Warn(prefix + " Version check failed " + ex.Message);
                return null;
            }
        }

        public static bool IsNewerVersion(string latestTag, string currentTag)
        {
            var latest = ParseVersion(latestTag);
            return latest != null && latest > ParseVersion(currentTag);
        }

        public static Version ParseVersion(string tag)
        {
            string text = (tag ?? "").Trim().TrimStart('v', 'V');
            int suffix = text.IndexOfAny(new[] { '-', '+', ' ' });
            if (suffix >= 0)
                text = text.Substring(0, suffix);
            if (!text.Contains("."))
                text += ".0";
            if (!Version.TryParse(text, out var parsed))
                return null;
            return new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build), Math.Max(0, parsed.Revision));
        }
    }
}
