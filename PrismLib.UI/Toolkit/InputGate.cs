using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PrismLib.UI.Toolkit
{
    /* Keeps keys and the wheel away from the game while a Prism window wants them.

       A panel only catches what UI Toolkit routes to it; the game reads the keyboard and wheel
       straight from UnityEngine.Input and RDInput, so typing into a search box placed tiles, Escape
       opened the pause menu behind the window, and scrolling a list zoomed the editor. While
       Ui.BlocksGameInput holds (a visible surface with BlocksGameInput, the default), these read as
       "nothing pressed" / "no scroll":

         UnityEngine.Input  GetKey / GetKeyDown / GetKeyUp (KeyCode and string),
                            mouseScrollDelta, GetAxis / GetAxisRaw for scroll axes
         the game's calls   to GetMouseButton / GetMouseButtonDown / GetMouseButtonUp — see below
         RDInput            GetMain, WentDown, IsDown, GetState — the game's own entry points,
                            which carry the async (SkyHook) keyboard as well

       UI Toolkit reads the same Input calls to drive the windows themselves, but only through one
       wrapper (DefaultEventSystem.LegacyInputProcessor.Input), so every method of that wrapper runs
       exempt: Prism windows keep their clicks and wheel while the game's own reads come back empty.
       Typed text arrives through Event.PopEvent and was never gated. A mod whose own uGUI panels
       must stay clickable beside an open window registers AddMouseExemption.

       Engine calls (icalls) cannot be patched on the game's Mono + UMM Harmony 2.3.6: the wrapper
       fails to compile ("Invalid IL code"). GetKey(KeyCode) and friends are managed wrappers and
       patch fine; GetMouseButton* and anyKeyDown ARE the icalls. So the mouse is gated at the
       CALLERS: every Assembly-CSharp method that calls one is transpiled (by the mod — "redirect")
       to call MouseButton / MouseButtonDown / MouseButtonUp here instead. That includes Rewired's
       UnityInputSource, which feeds uGUI, so the game's menus stop taking clicks too. anyKeyDown
       stays ungated: the game's press-any-key prompts still see a key.

       PrismLib.UI does not reference Harmony, so the MOD applies the patches: EnsureInstalled takes
       a delegate that adds one prefix / postfix / finalizer with the mod's own Harmony. One install per process is
       enough (every copy of this assembly shares these statics); a mod that unpatches itself calls
       Uninstalled so another mod's next EnsureInstalled takes over. */
    public static class InputGate
    {
        private static string _installedBy;
        private static int _exempt;
        private static readonly List<Func<bool>> _exemptProbes = new List<Func<bool>>();
        private static readonly List<Func<bool>> _bypass = new List<Func<bool>>();
        private static readonly List<Func<bool>> _mouseExempt = new List<Func<bool>>();

        /// True while game input is being withheld right now.
        public static bool Blocking
        {
            get
            {
                if (_exempt > 0) return false;
                if (!Ui.BlocksGameInput && Time.frameCount != _holdFrame) return false;
                if (Any(_exemptProbes) || Any(_bypass)) return false;
                return true;
            }
        }

        /* A blocking window that hides keeps the gate shut for the rest of that frame. The key that
           closed it — Escape, read by a mod's Update — is still "down" for every script that runs
           later in the same frame, and the game read it there and opened the pause menu behind the
           window it had just closed. */
        private static int _holdFrame = -1;

        internal static void HoldThisFrame() => _holdFrame = Time.frameCount;

        /// Run reads that belong to a mod or to Prism itself — its menu hotkeys, the Escape and
        /// chord polls, the wheel feed — so they still see real input while a window blocks.
        public static void Exempt(Action reads)
        {
            _exempt++;
            try { reads(); }
            finally { _exempt--; }
        }

        public static T Exempt<T>(Func<T> read)
        {
            _exempt++;
            try { return read(); }
            finally { _exempt--; }
        }

        /// A mod's own "I am reading input for myself" flag (Topaz's RawReadExempt).
        public static void AddExemption(Func<bool> probe) { if (probe != null) _exemptProbes.Add(probe); }

        /// The pointer is on this mod's own (non-Prism) UI, which must stay clickable.
        public static void AddMouseExemption(Func<bool> probe) { if (probe != null) _mouseExempt.Add(probe); }

        /// Conditions under which the game must keep its input — autoplay drives hits through
        /// the same path, and starving it empties the hit tracker.
        public static void AddBypass(Func<bool> probe) { if (probe != null) _bypass.Add(probe); }

        /// Patch the gate in through the caller's Harmony: patch(target, patchMethod, kind), kind
        /// being "prefix", "postfix" or "finalizer". Cheap to call every frame — it does nothing
        /// once installed.
        public static void EnsureInstalled(string owner, Action<MethodBase, MethodInfo, string> postfix)
        {
            if (_installedBy != null || postfix == null) return;
            _installedBy = owner ?? "?";
            int n = 0;
            var input = typeof(Input);
            n += Patch(postfix, Method(input, "GetKey", typeof(KeyCode)), "Bool");
            n += Patch(postfix, Method(input, "GetKeyDown", typeof(KeyCode)), "Bool");
            n += Patch(postfix, Method(input, "GetKeyUp", typeof(KeyCode)), "Bool");
            n += Patch(postfix, Method(input, "GetKey", typeof(string)), "Bool");
            n += Patch(postfix, Method(input, "GetKeyDown", typeof(string)), "Bool");
            n += Patch(postfix, Method(input, "GetKeyUp", typeof(string)), "Bool");
            n += Patch(postfix, input.GetProperty("mouseScrollDelta")?.GetGetMethod(), "Scroll");
            n += Patch(postfix, Method(input, "GetAxis", typeof(string)), "Axis");
            n += Patch(postfix, Method(input, "GetAxisRaw", typeof(string)), "Axis");

            var rd = Type.GetType("RDInput, Assembly-CSharp");
            if (rd != null)
                foreach (var m in rd.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                {
                    if (m.Name != "GetMain" && m.Name != "WentDown" && m.Name != "IsDown" && m.Name != "GetState") continue;
                    if (m.ReturnType == typeof(bool)) n += Patch(postfix, m, "Bool");
                    else if (m.ReturnType == typeof(int)) n += Patch(postfix, m, "Int");
                }
            // Mouse buttons, at the callers (see the header).
            int redirected = 0;
            if (rd != null)
                foreach (var caller in CallersOfMouseButtons(rd.Assembly))
                {
                    try { postfix(caller, null, "redirect"); redirected++; }
                    catch (Exception e) { Ui.Log("InputGate: could not redirect " + caller.DeclaringType?.Name + "." + caller.Name + " — " + e.Message); }
                }

            // UI Toolkit's own reads go through this wrapper — exempt all of it, so the windows the
            // gate protects still receive their clicks and wheel.
            int exempted = 0;
            var uitk = Type.GetType("UnityEngine.UIElements.DefaultEventSystem+LegacyInputProcessor+Input, UnityEngine.UIElementsModule");
            if (uitk != null)
                foreach (var m in uitk.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (m.IsAbstract || m.GetMethodBody() == null) continue;
                    try
                    {
                        postfix(m, typeof(InputGate).GetMethod("ExemptPrefix", BindingFlags.NonPublic | BindingFlags.Static), "prefix");
                        postfix(m, typeof(InputGate).GetMethod("ExemptFinalizer", BindingFlags.NonPublic | BindingFlags.Static), "finalizer");
                        exempted++;
                    }
                    catch (Exception e) { Ui.Log("InputGate: could not exempt UI Toolkit's " + m.Name + " — " + e.Message); }
                }
            Ui.Log("InputGate: " + n + " input read(s) gated, " + redirected + " game mouse caller(s) redirected, "
                   + exempted + " UI Toolkit read(s) exempt"
                   + (uitk == null ? " (UI Toolkit input wrapper NOT FOUND — windows may lose clicks)" : "")
                   + ", installed by " + _installedBy);
        }

        /// The installing mod removed its patches (master switch off); let another mod reinstall.
        /// Ignored from any other mod — its unpatching did not touch the gate.
        public static void Uninstalled(string owner) { if (_installedBy == owner) _installedBy = null; }

        private static MethodInfo Method(Type t, string name, Type arg)
            => t.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, new[] { arg }, null);

        private static int Patch(Action<MethodBase, MethodInfo, string> postfix, MethodBase target, string kind)
        {
            if (target == null) return 0;
            try
            {
                postfix(target, typeof(InputGate).GetMethod(kind + "Postfix", BindingFlags.NonPublic | BindingFlags.Static), "postfix");
                return 1;
            }
            catch (Exception e)
            {
                Ui.Log("InputGate: could not gate " + target.DeclaringType?.Name + "." + target.Name + " — " + e.Message);
                return 0;
            }
        }

        private static bool Any(List<Func<bool>> probes)
        {
            for (int i = 0; i < probes.Count; i++)
            {
                try { if (probes[i]()) return true; }
                catch { }
            }
            return false;
        }

        // Postfixes — resolved by name in Patch.
        private static void BoolPostfix(ref bool __result) { if (__result && Blocking) __result = false; }
        private static void IntPostfix(ref int __result) { if (__result != 0 && Blocking) __result = 0; }
        // ── mouse buttons, called in place of UnityEngine.Input's by redirected game code ──

        public static bool MouseButton(int button) => Input.GetMouseButton(button) && !MouseBlocked;
        public static bool MouseButtonDown(int button) => Input.GetMouseButtonDown(button) && !MouseBlocked;
        public static bool MouseButtonUp(int button) => Input.GetMouseButtonUp(button) && !MouseBlocked;

        private static bool MouseBlocked => Blocking && !Any(_mouseExempt);

        private static MethodInfo[] _mouseFrom, _mouseTo;

        /// For the mod's transpiler: the gated replacement for a call target, or null to leave it.
        public static MethodInfo RedirectFor(MethodBase called)
        {
            EnsureMouseMaps();
            for (int i = 0; i < _mouseFrom.Length; i++)
                if (_mouseFrom[i] == called) return _mouseTo[i];
            return null;
        }

        private static void EnsureMouseMaps()
        {
            if (_mouseFrom != null) return;
            var names = new[] { "GetMouseButton", "GetMouseButtonDown", "GetMouseButtonUp" };
            var to = new[] { "MouseButton", "MouseButtonDown", "MouseButtonUp" };
            _mouseFrom = new MethodInfo[3];
            _mouseTo = new MethodInfo[3];
            for (int i = 0; i < 3; i++)
            {
                _mouseFrom[i] = Method(typeof(Input), names[i], typeof(int));
                _mouseTo[i] = typeof(InputGate).GetMethod(to[i], BindingFlags.Public | BindingFlags.Static);
            }
        }

        /* Every method in the game's assembly whose IL calls a mouse-button read. A plain byte scan:
           any 0x28 (call) followed by a MemberRef token, resolved once per token and cached. A byte
           pattern that happens to look like a call can only resolve to the wrong method or fail —
           it is checked, and the transpiler rewrites real call instructions only. */
        private static List<MethodBase> CallersOfMouseButtons(Assembly asm)
        {
            EnsureMouseMaps();
            var found = new List<MethodBase>();
            var resolved = new Dictionary<int, bool>();
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types; }
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                                   | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var t in types)
            {
                if (t == null || t.ContainsGenericParameters) continue;
                var methods = new List<MethodBase>();
                try { methods.AddRange(t.GetMethods(all)); methods.AddRange(t.GetConstructors(all)); }
                catch { continue; }
                foreach (var m in methods)
                {
                    if (m.ContainsGenericParameters) continue;
                    byte[] il;
                    try { var body = m.GetMethodBody(); il = body?.GetILAsByteArray(); }
                    catch { continue; }
                    if (il == null) continue;
                    for (int i = 0; i + 4 < il.Length; i++)
                    {
                        if (il[i] != 0x28) continue;
                        int token = BitConverter.ToInt32(il, i + 1);
                        if ((token >> 24) != 0x0A) continue;   // MemberRef — Input lives in another assembly
                        bool hit;
                        if (!resolved.TryGetValue(token, out hit))
                        {
                            try { hit = Array.IndexOf(_mouseFrom, m.Module.ResolveMethod(token)) >= 0; }
                            catch { hit = false; }
                            resolved[token] = hit;
                        }
                        if (hit) { found.Add(m); break; }
                    }
                }
            }
            return found;
        }

        private static void ExemptPrefix() { _exempt++; }

        private static Exception ExemptFinalizer(Exception __exception)
        {
            _exempt--;
            return __exception;
        }

        private static void ScrollPostfix(ref Vector2 __result) { if (__result != Vector2.zero && Blocking) __result = Vector2.zero; }

        private static void AxisPostfix(string axisName, ref float __result)
        {
            if (__result != 0f && axisName != null && axisName.IndexOf("Scroll", StringComparison.OrdinalIgnoreCase) >= 0 && Blocking)
                __result = 0f;
        }
    }
}
