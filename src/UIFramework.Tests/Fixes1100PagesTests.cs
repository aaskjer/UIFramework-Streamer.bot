using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>1.10.0, the standard pages: the Log page's folder button opens the folder beside Streamer.bot.</summary>
    public class Fixes1100PagesTests
    {
        private static void Sta(Action body)
        {
            Exception failure = null;
            var thread = new Thread(() => { try { body(); } catch (Exception ex) { failure = ex; } });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private static IEnumerable<T> Find<T>(object node) where T : class
        {
            if (node is T hit) yield return hit;
            if (node is DependencyObject d)
                foreach (var child in LogicalTreeHelper.GetChildren(d))
                    foreach (var found in Find<T>(child))
                        yield return found;
        }

        private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        // ---- The Log page ------------------------------------------------------------------------------------

        private sealed class LogHooks : NoHooks
        {
            public List<string> Lines = new List<string>();
            public override IEnumerable<LogEntry> ReadLog() => Lines.Select(LogEntry.Parse).ToList();
        }

        private sealed class QuietLog : IHostLog
        {
            public readonly List<string> Lines = new List<string>();
            public void Info(string message) => Lines.Add(message);
            public void Warn(string message) => Lines.Add(message);
            public void Error(string message) => Lines.Add(message);
        }

        private static FrameworkElement LogPageOf(LogHooks hooks, WindowSpec spec = null)
        {
            var ctx = new PageContext();
            typeof(PageContext).GetProperty("Spec").SetValue(ctx, spec ?? new WindowSpec());
            typeof(PageContext).GetProperty("Hooks").SetValue(ctx, hooks);
            typeof(PageContext).GetProperty("Log").SetValue(ctx, new QuietLog());
            return (FrameworkElement)LogPage.Build(ctx);
        }

        [Fact]
        public void The_folder_button_opens_the_log_folder_beside_Streamer_bot_wherever_the_process_stands() => Sta(() =>
        {
            string folder = "uiframework-1100-logs-" + Guid.NewGuid().ToString("N");
            string full = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder);
            string elsewhere = Path.Combine(Path.GetTempPath(), "uiframework-1100-cwd-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(elsewhere);
            var opened = new List<string>();
            var show = Web.ShowFolder;
            string current = Environment.CurrentDirectory;
            try
            {
                Web.ShowFolder = opened.Add;
                // A file dialog (the window's own Export or Import) left the process in another folder.
                Environment.CurrentDirectory = elsewhere;
                var spec = new WindowSpec();
                spec.Log.Folder = folder;
                var page = LogPageOf(new LogHooks(), spec);
                Click(Find<Button>(page).Single(b => AutomationProperties.GetName(b) == Text.T("log.openFolder")));
                Assert.Equal(new[] { full }, opened);
                Assert.True(Directory.Exists(full));
                Assert.False(Directory.Exists(Path.Combine(elsewhere, folder)));   // no empty folder where the dialog was
            }
            finally
            {
                Web.ShowFolder = show;
                Environment.CurrentDirectory = current;
                if (Directory.Exists(full)) Directory.Delete(full, true);
                Directory.Delete(elsewhere, true);
            }
        });
    }
}
