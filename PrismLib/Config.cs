using System;
using System.Collections.Generic;
using System.IO;

namespace PrismLib
{
    /* PrismLib's own settings — things that belong to the suite rather than to any one mod (the FPS
       display first; Bismuth and Quartz each used to draw their own).

       A flat key=value file beside PrismLib.dll, in the shared folder every Prism mod already uses,
       so one switch serves every mod. Plain text and written whole on each change: it holds a
       handful of values, and a mod's XML settings must never be the thing a crash here corrupts. */
    public static class Config
    {
        private static Dictionary<string, string> _values;
        private static readonly object _lock = new object();

        /// Where the values live, or null when PrismLib was loaded from memory and has no folder.
        public static string FilePath
        {
            get
            {
                try
                {
                    string dir = Path.GetDirectoryName(typeof(Config).Assembly.Location);
                    return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, "settings.txt");
                }
                catch { return null; }
            }
        }

        public static bool Has(string key)
        {
            lock (_lock) { Load(); return _values.ContainsKey(key); }
        }

        public static string Get(string key, string fallback = null)
        {
            lock (_lock)
            {
                Load();
                string v;
                return _values.TryGetValue(key, out v) ? v : fallback;
            }
        }

        public static bool GetBool(string key, bool fallback = false)
        {
            string v = Get(key);
            return v == null ? fallback : v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public static void Set(string key, string value)
        {
            if (string.IsNullOrEmpty(key) || key.IndexOf('=') >= 0 || key.IndexOf('\n') >= 0) return;
            lock (_lock)
            {
                Load();
                string old;
                if (_values.TryGetValue(key, out old) && old == value) return;
                _values[key] = (value ?? "").Replace("\n", " ");
                Save();
            }
        }

        public static void SetBool(string key, bool value) => Set(key, value ? "1" : "0");

        private static void Load()
        {
            if (_values != null) return;
            _values = new Dictionary<string, string>(StringComparer.Ordinal);
            string path = FilePath;
            if (path == null || !File.Exists(path)) return;
            try
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0) _values[line.Substring(0, eq)] = line.Substring(eq + 1);
                }
            }
            catch (Exception e) { Prism.Log("PrismLib: settings unreadable: " + e.Message); }
        }

        private static void Save()
        {
            string path = FilePath;
            if (path == null) return;
            try
            {
                var lines = new List<string>();
                foreach (var kv in _values) lines.Add(kv.Key + "=" + kv.Value);
                File.WriteAllLines(path, lines.ToArray());
            }
            catch (Exception e) { Prism.Log("PrismLib: settings unwritable: " + e.Message); }
        }
    }
}
