using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.TextCore.Text;

namespace PrismLib.UI.Toolkit
{
    /* Lets an OS font whose regular weight is not called "Regular" load at all.

       TextCore turns a dynamic OS Font (no embedded data) into a FontAsset by asking for its family
       with the style hard-coded to "Regular". fontconfig tags DejaVu's regular weight "Book", and in
       the Steam Linux runtime DejaVu is often the only family there is — so UnityModManager's
       window (IMGUI, font "DejaVu Sans") got null back, logged "Unable to find a font file" for
       every label on every frame (~10,000 lines a second), and, having nothing of its own to draw,
       showed whatever text TextCore generated last: a Prism label, the FPS counter.

       A prefix + postfix on FontAsset.CreateFontAsset(family, style, size): a "Regular" miss is
       retried with the family's other names for that weight, and every outcome — a miss included —
       is cached, so a family that really is absent costs one lookup instead of one per label per
       frame. The mod applies the patches (PrismLib.UI has no Harmony), like InputGate. */
    public static class FontFix
    {
        private static string _installedBy;
        private static readonly Dictionary<string, FontAsset> _cache = new Dictionary<string, FontAsset>();
        private static readonly string[] _regularNames = { "Book", "Normal", "Medium", "Roman", "" };
        private static MethodInfo _internal;
        private static bool _retrying;

        public static void EnsureInstalled(string owner, Action<MethodBase, MethodInfo, string> patch)
        {
            if (_installedBy != null || patch == null) return;
            _installedBy = owner ?? "?";
            var t = typeof(FontAsset);
            var target = t.GetMethod("CreateFontAsset", BindingFlags.Public | BindingFlags.Static, null,
                                     new[] { typeof(string), typeof(string), typeof(int) }, null);
            _internal = t.GetMethod("CreateFontAssetInternal", BindingFlags.NonPublic | BindingFlags.Static, null,
                                    new[] { typeof(string), typeof(string), typeof(int) }, null);
            if (target == null) { Ui.Log("FontFix: FontAsset.CreateFontAsset(family, style, size) not found"); return; }
            try
            {
                patch(target, typeof(FontFix).GetMethod(nameof(Prefix), BindingFlags.NonPublic | BindingFlags.Static), "prefix");
                patch(target, typeof(FontFix).GetMethod(nameof(Postfix), BindingFlags.NonPublic | BindingFlags.Static), "postfix");
                Ui.Log("FontFix: installed by " + _installedBy + (_internal == null ? " (no internal creator — retries will log)" : ""));
            }
            catch (Exception e) { Ui.Log("FontFix: could not patch CreateFontAsset — " + e.Message); }
        }

        public static void Uninstalled(string owner) { if (_installedBy == owner) _installedBy = null; }

        private static string Key(string family, string style, int size) => family + "\n" + style + "\n" + size;

        // A cached answer skips the lookup — and the error it logs on a miss.
        private static bool Prefix(string familyName, string styleName, int pointSize, ref FontAsset __result)
        {
            if (_retrying) return true;
            FontAsset cached;
            if (!_cache.TryGetValue(Key(familyName, styleName, pointSize), out cached)) return true;
            if (cached == null && !ReferenceEquals(cached, null)) return true;   // destroyed since: look again
            __result = cached;
            return false;
        }

        private static void Postfix(string familyName, string styleName, int pointSize, ref FontAsset __result)
        {
            if (_retrying) return;
            string key = Key(familyName, styleName, pointSize);
            if (_cache.ContainsKey(key) && (__result == null || ReferenceEquals(_cache[key], __result))) return;
            if (__result == null && string.Equals(styleName, "Regular", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var style in _regularNames)
                {
                    var fa = Create(familyName, style, pointSize);
                    if (fa == null) continue;
                    __result = fa;
                    Ui.Log("FontFix: '" + familyName + "' has no \"Regular\" — using style \"" + style + "\"");
                    break;
                }
            }
            _cache[key] = __result;
        }

        private static FontAsset Create(string family, string style, int size)
        {
            _retrying = true;
            try
            {
                return _internal != null
                    ? _internal.Invoke(null, new object[] { family, style, size }) as FontAsset
                    : FontAsset.CreateFontAsset(family, style, size);
            }
            catch { return null; }
            finally { _retrying = false; }
        }
    }
}
