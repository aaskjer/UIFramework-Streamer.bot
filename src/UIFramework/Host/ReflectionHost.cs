using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// Lets a host talk to the framework without referencing the DLL at compile time: the stub passes any
    /// object, and the methods it happens to have are used. Every method is optional, every value is a string,
    /// so the stub compiles inside Streamer.bot with no reference and the import stays self-contained.
    /// <code>
    ///   void   Log(string level, string message)     // "info" | "warn" | "error"
    ///   void   OnSaved(string settingsJson)
    ///   string Test(string name, string settingsJson)
    ///   string ReadTable(string key)                 // JSON array of row objects
    ///   IEnumerable&lt;string&gt; ReadLogLines()          // or string[] / List&lt;string&gt;; null = no Log page
    /// </code>
    /// </summary>
    public sealed class ReflectionHost : IHostLog, IHostHooks
    {
        private readonly object _host;
        private readonly MethodInfo _log, _onSaved, _test, _readTable, _readLog;

        public ReflectionHost(object host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            var type = host.GetType();
            _log = Find(type, "Log", typeof(string), typeof(string));
            _onSaved = Find(type, "OnSaved", typeof(string));
            _test = Find(type, "Test", typeof(string), typeof(string));
            _readTable = Find(type, "ReadTable", typeof(string));
            _readLog = Find(type, "ReadLogLines");
        }

        private static MethodInfo Find(Type type, string name, params Type[] parameters) =>
            type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, parameters, null);

        public void Info(string message) => Log("info", message);
        public void Warn(string message) => Log("warn", message);
        public void Error(string message) => Log("error", message);

        private void Log(string level, string message)
        {
            if (_log == null) return;
            try { _log.Invoke(_host, new object[] { level, message }); } catch (Exception) { }
        }

        public void OnSaved(JObject settings)
        {
            if (_onSaved == null) return;
            _onSaved.Invoke(_host, new object[] { settings.ToString(Newtonsoft.Json.Formatting.None) });
        }

        public string Test(string name, JObject settings)
        {
            if (_test == null) return null;
            return _test.Invoke(_host, new object[] { name, settings.ToString(Newtonsoft.Json.Formatting.None) }) as string;
        }

        public FrameworkElement BuildPage(string pageId, PageContext context) => null;

        public IEnumerable<LogEntry> ReadLog()
        {
            if (_readLog == null) return null;
            var lines = _readLog.Invoke(_host, null) as IEnumerable;
            if (lines == null) return null;
            return lines.Cast<object>().Select(l => l?.ToString()).Where(l => !string.IsNullOrWhiteSpace(l)).Select(LogEntry.Parse).ToList();
        }

        public string ReadTable(string key)
        {
            if (_readTable == null) return null;
            return _readTable.Invoke(_host, new object[] { key }) as string;
        }
    }
}
