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

        /* REPLACES everything this mod described, rather than appending. A mod builds its list
           and hands it over once; two calls means the second wins, which is what a caller
           splitting "features" and "keybinds" into two calls discovers the hard way. */
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

        /* Ranked match over label, id, group, page and keywords, across every mod. The scoring
           lives in Search so the settings screen, the dashboard and each mod's own filters all
           agree on what "bpm" finds — three different Contains() implementations was the problem
           this replaces. Ties keep declaration order, then Order. */
        public static IEnumerable<KeyValuePair<string, SettingEntry>> Search(string query)
        {
            List<Owned> pool;
            lock (_lock) pool = new List<Owned>(_all);
            pool.Sort((a, b) => a.Entry.Order.CompareTo(b.Entry.Order));

            var hits = PrismLib.Search.Rank(pool, query, o => Fields(o.Entry));
            var res = new List<KeyValuePair<string, SettingEntry>>(hits.Count);
            foreach (var o in hits) res.Add(new KeyValuePair<string, SettingEntry>(o.Owner, o.Entry));
            return res;
        }

        private static string[] Fields(SettingEntry e)
        {
            var f = new List<string> { e.Label, e.Id, e.Group, e.Page };
            if (e.Keywords != null) f.AddRange(e.Keywords);
            return f.ToArray();
        }
    }
}
