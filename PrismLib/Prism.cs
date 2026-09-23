using System;
using System.Collections.Generic;

namespace PrismLib
{
    /* PrismLib — the shared layer under Bismuth, Sapphire and Quartz.

       It exists because those three mods fight over things the GAME owns exactly one of:
       autoplay, no-fail, the editor camera, the path lock, HUD visibility — and over the
       keyboard. Two mods writing the same global every frame is not a Harmony conflict a loader
       can detect; it just looks like "autoplay is broken". So the library is an ARBITER first and
       a UI toolkit second: claims say who owns a piece of game state, the key registry says who
       owns a hotkey, and the settings schema lets all three describe settings once and be
       rendered (and searched) together.

       Deliberately NOT a framework the mods must live inside. Every call degrades: a mod that
       cannot load PrismLib, or is denied a claim, must still work on its own. */
    public static class Prism
    {
        /* Bump the MINOR for additive API, the MAJOR for a break. The bootstrapper in each mod
           compares this against what it requires and loads the newest copy it can find, so three
           mods shipping three different builds converge on one assembly at runtime. */
        public static readonly Version Version = new Version(0, 2, 0);

        private static readonly Dictionary<string, ModHandle> _mods = new Dictionary<string, ModHandle>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new object();

        /// Where PrismLib writes its own diagnostics. Each mod points this at its own log; the
        /// last one to register wins, which is fine — it is one process-wide library.
        public static Action<string> Log = _ => { };

        /// Register (or fetch) this mod's handle. Id is stable and human-readable: "Sapphire".
        public static ModHandle Register(string id, Version version)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("mod id required", nameof(id));
            lock (_lock)
            {
                ModHandle h;
                if (_mods.TryGetValue(id, out h)) return h;
                h = new ModHandle(id, version ?? new Version(0, 0, 0));
                _mods[id] = h;
                Log("PrismLib: " + id + " " + h.Version + " registered (lib " + Version + ")");
                return h;
            }
        }

        /// Is another Prism mod present, and which version? Replaces each mod's own reflection probe.
        public static bool IsLoaded(string id, out Version version)
        {
            lock (_lock)
            {
                ModHandle h;
                if (_mods.TryGetValue(id, out h)) { version = h.Version; return true; }
                version = null;
                return false;
            }
        }

        public static IEnumerable<string> LoadedMods
        {
            get { lock (_lock) return new List<string>(_mods.Keys); }
        }
    }

    /// A registered mod. Claims and key bindings are taken through this so every entry has an owner.
    public sealed class ModHandle
    {
        public string Id { get; }
        public Version Version { get; }

        internal ModHandle(string id, Version version) { Id = id; Version = version; }

        public Claim ClaimState(StateKey key, string reason = null) => Claims.Take(this, key, reason);
        public bool OwnsState(StateKey key) => Claims.OwnerOf(key) == Id;
        public KeyBinding BindKey(string id, int keyCode, KeyMods mods, string label) => Keys.Register(this, id, keyCode, mods, label);
        public void DescribeSettings(IEnumerable<SettingEntry> entries) => Settings.Register(this, entries);

        public override string ToString() => Id + " " + Version;
    }
}
