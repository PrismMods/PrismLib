using System;
using System.Collections.Generic;

namespace PrismLib
{
    /// A stream of text a mod is willing to show in the shared debug view — usually its log file.
    public sealed class LogSource
    {
        public string Owner;
        public string Name;               // "SapphireLog", "BismuthLog"
        public string Path;               // on disk, for an "open folder" button; may be null
        public Func<IEnumerable<string>> Tail;   // most recent lines, newest last
        public Action Clear;              // optional: wipes the file, for a "Clear" button
    }

    /// Named values a mod is willing to expose — player state, save data, whatever it already knows.
    public sealed class FieldGroup
    {
        public string Owner;
        public string Name;               // "Level", "Player", "Save"
        public Func<IEnumerable<KeyValuePair<string, string>>> Read;
    }

    /* The registry behind the debug view.

       Each mod already has a log and already knows things about the level, the player and the save
       that the others cannot see. Registering them here is what turns three separate log files and
       three separate debug screens into one place to look — and it is the first piece of the
       dashboard, since "central management for logs, player data, save data and fields" is exactly
       this registry plus a view over it.

       Everything is pulled, never pushed: a source is a delegate the viewer calls when it refreshes,
       so a mod costs nothing while nobody is looking, and nothing here has to be thread-safe beyond
       the list itself. */
    public static class Diagnostics
    {
        private static readonly List<LogSource> _logs = new List<LogSource>();
        private static readonly List<FieldGroup> _fields = new List<FieldGroup>();
        private static readonly object _lock = new object();

        /// Raised when a mod adds or drops a source, so an open view can rebuild its tabs.
        public static event Action Changed;

        /* The debug window is owned by whichever mod won StateKey.DebugPanel, but any mod may want
           to open it — its own hotkey, a "View log" button on its settings page. Without this, the
           button in the mod that lost the claim would silently do nothing. The owner subscribes;
           everyone else asks. */
        public static event Action ToggleRequested;

        public static void RequestToggle()
        {
            var t = ToggleRequested;
            if (t == null) { Prism.Log("PrismLib: debug window requested, but no mod owns one"); return; }
            try { t(); } catch (Exception e) { Prism.Log("PrismLib: debug toggle failed: " + e.Message); }
        }

        internal static void AddLog(ModHandle mod, string name, Func<IEnumerable<string>> tail, string path, Action clear)
        {
            if (mod == null || tail == null || string.IsNullOrEmpty(name)) return;
            lock (_lock)
            {
                _logs.RemoveAll(l => l.Owner == mod.Id && l.Name == name);
                _logs.Add(new LogSource { Owner = mod.Id, Name = name, Tail = tail, Path = path, Clear = clear });
            }
            Prism.Log("PrismLib: " + mod.Id + " registered log '" + name + "'");
            Fire();
        }

        internal static void AddFields(ModHandle mod, string name, Func<IEnumerable<KeyValuePair<string, string>>> read)
        {
            if (mod == null || read == null || string.IsNullOrEmpty(name)) return;
            lock (_lock)
            {
                _fields.RemoveAll(f => f.Owner == mod.Id && f.Name == name);
                _fields.Add(new FieldGroup { Owner = mod.Id, Name = name, Read = read });
            }
            Prism.Log("PrismLib: " + mod.Id + " registered fields '" + name + "'");
            Fire();
        }

        public static IEnumerable<LogSource> Logs
        {
            get { lock (_lock) return new List<LogSource>(_logs); }
        }

        public static IEnumerable<FieldGroup> Fields
        {
            get { lock (_lock) return new List<FieldGroup>(_fields); }
        }

        /// A mod going away takes its sources with it — the delegates point into an assembly that
        /// may be about to be replaced by a hot reload.
        public static void Unregister(string owner)
        {
            int n;
            lock (_lock)
            {
                n = _logs.RemoveAll(l => l.Owner == owner) + _fields.RemoveAll(f => f.Owner == owner);
            }
            if (n > 0) Fire();
        }

        private static void Fire()
        {
            var c = Changed;
            if (c != null) { try { c(); } catch { } }
        }
    }
}
