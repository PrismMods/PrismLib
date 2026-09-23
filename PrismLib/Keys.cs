using System;
using System.Collections.Generic;

namespace PrismLib
{
    [Flags]
    public enum KeyMods { None = 0, Shift = 1, Ctrl = 2, Alt = 4, Cmd = 8 }

    /* Declared hotkeys, so a collision is reported instead of silently swallowed.

       This is the other half of the autoplay report: Sapphire binds pan to A, the game binds A to
       autoplay, and nothing anywhere knew both wanted it. A mod registers what it binds — including
       keys it TAKES FROM THE GAME — and can ask what else wants a key before claiming it.

       Registration never blocks: two mods may hold the same key (the user might want that). What
       it guarantees is that the conflict is visible, to the log and to a settings page. */
    public static class Keys
    {
        private static readonly List<KeyBinding> _binds = new List<KeyBinding>();
        private static readonly object _lock = new object();

        /// Raised when a registration overlaps an existing one: (newcomer, incumbent).
        public static event Action<KeyBinding, KeyBinding> Conflict;

        internal static KeyBinding Register(ModHandle mod, string id, int keyCode, KeyMods mods, string label)
        {
            var b = new KeyBinding(mod != null ? mod.Id : "?", id, keyCode, mods, label);
            KeyBinding clash = null;
            lock (_lock)
            {
                foreach (var o in _binds)
                    if (o.KeyCode == keyCode && o.Mods == mods && o.Owner != b.Owner) { clash = o; break; }
                _binds.RemoveAll(o => o.Owner == b.Owner && o.Id == b.Id);
                _binds.Add(b);
            }
            if (clash != null)
            {
                Prism.Log("PrismLib: key conflict — " + b + " vs " + clash);
                var c = Conflict;
                if (c != null) { try { c(b, clash); } catch { } }
            }
            return b;
        }

        /// What already wants this key, ignoring one owner (usually the asker).
        public static IEnumerable<KeyBinding> Wanting(int keyCode, KeyMods mods, string ignoreOwner = null)
        {
            lock (_lock)
            {
                var outp = new List<KeyBinding>();
                foreach (var o in _binds)
                    if (o.KeyCode == keyCode && o.Mods == mods && o.Owner != ignoreOwner) outp.Add(o);
                return outp;
            }
        }

        public static IEnumerable<KeyBinding> All
        {
            get { lock (_lock) return new List<KeyBinding>(_binds); }
        }

        /// Every pair that shares a key+modifier — what a settings page lists as warnings.
        public static IEnumerable<KeyValuePair<KeyBinding, KeyBinding>> Conflicts()
        {
            lock (_lock)
            {
                var outp = new List<KeyValuePair<KeyBinding, KeyBinding>>();
                for (int i = 0; i < _binds.Count; i++)
                    for (int j = i + 1; j < _binds.Count; j++)
                        if (_binds[i].KeyCode == _binds[j].KeyCode && _binds[i].Mods == _binds[j].Mods)
                            outp.Add(new KeyValuePair<KeyBinding, KeyBinding>(_binds[i], _binds[j]));
                return outp;
            }
        }

        public static void Unregister(string owner)
        {
            lock (_lock) _binds.RemoveAll(o => o.Owner == owner);
        }

        /// How a key code prints in logs and conflict warnings. PrismLib has no Unity reference, so
        /// a mod that does hands over the names: Keys.KeyName = i => ((KeyCode)i).ToString().
        public static Func<int, string> KeyName = i => "Key" + i;
    }

    /// One declared hotkey. KeyCode is UnityEngine.KeyCode as an int, so the library stays usable
    /// from code that has no Unity reference (and from a loader that hasn't initialised Unity yet).
    public sealed class KeyBinding
    {
        public string Owner { get; }
        public string Id { get; }
        public int KeyCode { get; }
        public KeyMods Mods { get; }
        public string Label { get; }

        internal KeyBinding(string owner, string id, int keyCode, KeyMods mods, string label)
        { Owner = owner; Id = id; KeyCode = keyCode; Mods = mods; Label = label; }

        public override string ToString()
        {
            string m = (Mods.HasFlag(KeyMods.Ctrl) ? "Ctrl+" : "") + (Mods.HasFlag(KeyMods.Cmd) ? "Cmd+" : "")
                     + (Mods.HasFlag(KeyMods.Alt) ? "Alt+" : "") + (Mods.HasFlag(KeyMods.Shift) ? "Shift+" : "");
            string k;
            try { k = Keys.KeyName(KeyCode); } catch { k = "Key" + KeyCode; }
            return Owner + ":" + Id + " [" + m + k + "]";
        }
    }
}
