using System;
using System.Collections.Generic;

namespace PrismLib
{
    /// The pieces of game state the game owns exactly one of. Two mods writing one of these is
    /// the bug class PrismLib exists for.
    public enum StateKey
    {
        Autoplay,        // RDC.auto
        NoFail,          // GCS.useNoFail / scrController.noFail
        EditorCamera,    // the editor camera's transform — panning, framing, follow
        PathLock,        // scnEditor.lockPathEditing
        EditorHud,       // the game's corner icons / control tip / placement rings
        GamePanels,      // the game's own event + settings panels (hidden by a native replacement)
    }

    /* Who owns a piece of game state right now.

       One owner at a time, first come first served, and the claim is a lease the owner releases
       (or disposes) when its feature switches off. A denied claim is NOT an error: the caller
       carries on without touching that state, which is exactly what a second editor suite should
       do rather than fighting the first every frame.

       Claims describe intent, they do not enforce it — nothing can stop a mod writing RDC.auto
       directly. What they buy is that a mod can ASK, and that when the user reports "autoplay is
       broken" the log names the owner instead of leaving three suspects. */
    public static class Claims
    {
        private sealed class Entry { public string Owner; public string Reason; public DateTime TakenUtc; }

        private static readonly Dictionary<StateKey, Entry> _held = new Dictionary<StateKey, Entry>();
        private static readonly object _lock = new object();

        /// Raised when a mod asks for state another mod holds — the hook a diagnostics panel uses.
        public static event Action<string, StateKey, string> Denied;   // asker, key, current owner

        internal static Claim Take(ModHandle mod, StateKey key, string reason)
        {
            if (mod == null) return null;
            lock (_lock)
            {
                Entry e;
                if (_held.TryGetValue(key, out e) && e.Owner != mod.Id)
                {
                    Prism.Log("PrismLib: " + mod.Id + " was denied " + key + " (held by " + e.Owner
                              + (e.Reason != null ? " for " + e.Reason : "") + ")");
                    var d = Denied;
                    if (d != null) { try { d(mod.Id, key, e.Owner); } catch { } }
                    return null;
                }
                if (e == null) Prism.Log("PrismLib: " + mod.Id + " claimed " + key + (reason != null ? " for " + reason : ""));
                _held[key] = new Entry { Owner = mod.Id, Reason = reason, TakenUtc = DateTime.UtcNow };
                return new Claim(mod.Id, key);
            }
        }

        public static string OwnerOf(StateKey key)
        {
            lock (_lock)
            {
                Entry e;
                return _held.TryGetValue(key, out e) ? e.Owner : null;
            }
        }

        internal static void Release(string owner, StateKey key)
        {
            lock (_lock)
            {
                Entry e;
                if (!_held.TryGetValue(key, out e) || e.Owner != owner) return;
                _held.Remove(key);
                Prism.Log("PrismLib: " + owner + " released " + key);
            }
        }

        /// Every held key and its owner — for a "who owns what" diagnostics view.
        public static IEnumerable<KeyValuePair<StateKey, string>> Held
        {
            get
            {
                lock (_lock)
                {
                    var outp = new List<KeyValuePair<StateKey, string>>();
                    foreach (var kv in _held) outp.Add(new KeyValuePair<StateKey, string>(kv.Key, kv.Value.Owner));
                    return outp;
                }
            }
        }

        /// A mod going away (StopMod / master switch off) drops everything it held.
        public static void ReleaseAll(string owner)
        {
            lock (_lock)
            {
                var keys = new List<StateKey>();
                foreach (var kv in _held) if (kv.Value.Owner == owner) keys.Add(kv.Key);
                foreach (var k in keys) _held.Remove(k);
                if (keys.Count > 0) Prism.Log("PrismLib: " + owner + " released " + keys.Count + " claim(s)");
            }
        }
    }

    /// A held claim. Dispose (or Release) when the feature that needed the state switches off.
    public sealed class Claim : IDisposable
    {
        public string Owner { get; }
        public StateKey Key { get; }
        private bool _released;

        internal Claim(string owner, StateKey key) { Owner = owner; Key = key; }

        public void Release()
        {
            if (_released) return;
            _released = true;
            Claims.Release(Owner, Key);
        }

        public void Dispose() => Release();
    }
}
