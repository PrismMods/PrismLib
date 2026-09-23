using System;
using System.Collections.Generic;

namespace PrismLib
{
    public enum SettingKind { Bool, Int, Float, Enum, Key, Colour, Text, Action }

    /* One setting, described rather than drawn.

       Bismuth's and Sapphire's settings panels are nearly the same screen written twice, and
       Quartz's problem is that it has so many settings nobody can find one. Both are the same
       missing piece: a mod should DECLARE its settings — type, label, group, keywords, how to
       read and write the value — and let a shared renderer lay them out, search them and report
       key conflicts. The mod keeps its own look through theme tokens (and can override the
       renderer for one row), so this is not a demand that three mods look alike.

       The renderer lands in stage 2. This model, its storage and cross-mod search come first,
       because they are what the three actually duplicate. */
    public sealed class SettingEntry
    {
        public string Id;                 // stable within the mod: "editor.effectiveBpm"
        public SettingKind Kind;
        public string Label;              // already localised by the owning mod
        public string Tooltip;
        public string Page;               // top-level tab: "Features", "Keybinds", …
        public string Group;              // section within the page
        public string[] Keywords;         // extra search terms (synonyms, old names)
        public int Order;

        public Func<object> Get;          // current value, boxed per Kind
        public Action<object> Set;        // write + persist; the mod owns storage
        public Func<bool> Visible;        // optional: hide when another setting is off
        public Func<bool> Enabled;        // optional: show greyed instead of hiding

        public double Min, Max, Step;     // Int / Float
        public string[] Options;          // Enum: display names, index = value
        public string ActionLabel;        // Action: button text

        public override string ToString() => Id + " (" + Kind + ")";
    }

    /// Everything every mod has declared, searchable in one place.
    public static class Settings
    {
        private sealed class Owned { public string Owner; public SettingEntry Entry; }

        private static readonly List<Owned> _all = new List<Owned>();
        private static readonly object _lock = new object();

        /// Raised when any mod's schema changes, so an open settings page can rebuild.
        public static event Action Changed;

        internal static void Register(ModHandle mod, IEnumerable<SettingEntry> entries)
        {
            if (mod == null || entries == null) return;
            lock (_lock)
            {
                _all.RemoveAll(o => o.Owner == mod.Id);
                foreach (var e in entries)
                {
                    if (e == null || string.IsNullOrEmpty(e.Id) || e.Get == null) continue;
                    _all.Add(new Owned { Owner = mod.Id, Entry = e });
                }
                Prism.Log("PrismLib: " + mod.Id + " described " + _all.FindAll(o => o.Owner == mod.Id).Count + " setting(s)");
            }
            var c = Changed;
            if (c != null) { try { c(); } catch { } }
        }

        public static void Unregister(string owner)
        {
            lock (_lock) _all.RemoveAll(o => o.Owner == owner);
            var c = Changed;
            if (c != null) { try { c(); } catch { } }
        }

        public static IEnumerable<KeyValuePair<string, SettingEntry>> Of(string owner)
        {
            lock (_lock)
            {
                var outp = new List<KeyValuePair<string, SettingEntry>>();
                foreach (var o in _all) if (o.Owner == owner) outp.Add(new KeyValuePair<string, SettingEntry>(o.Owner, o.Entry));
                return outp;
            }
        }

        /* Substring match over label, id, group, page and keywords, across every mod. Ranked so
           a label hit beats a keyword hit — the point is finding one setting among hundreds
           without knowing which mod owns it. */
        public static IEnumerable<KeyValuePair<string, SettingEntry>> Search(string query)
        {
            var hits = new List<KeyValuePair<int, Owned>>();
            if (string.IsNullOrEmpty(query))
            {
                lock (_lock) foreach (var o in _all) hits.Add(new KeyValuePair<int, Owned>(0, o));
            }
            else
            {
                string q = query.Trim().ToLowerInvariant();
                lock (_lock)
                    foreach (var o in _all)
                    {
                        var e = o.Entry;
                        int score = -1;
                        if (!string.IsNullOrEmpty(e.Label) && e.Label.ToLowerInvariant().Contains(q)) score = 0;
                        else if (e.Id.ToLowerInvariant().Contains(q)) score = 1;
                        else if (!string.IsNullOrEmpty(e.Group) && e.Group.ToLowerInvariant().Contains(q)) score = 2;
                        else if (!string.IsNullOrEmpty(e.Page) && e.Page.ToLowerInvariant().Contains(q)) score = 2;
                        else if (e.Keywords != null)
                            foreach (var k in e.Keywords)
                                if (!string.IsNullOrEmpty(k) && k.ToLowerInvariant().Contains(q)) { score = 3; break; }
                        if (score >= 0) hits.Add(new KeyValuePair<int, Owned>(score, o));
                    }
            }
            hits.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.Value.Entry.Order.CompareTo(b.Value.Entry.Order));
            var res = new List<KeyValuePair<string, SettingEntry>>();
            foreach (var h in hits) res.Add(new KeyValuePair<string, SettingEntry>(h.Value.Owner, h.Value.Entry));
            return res;
        }
    }
}
