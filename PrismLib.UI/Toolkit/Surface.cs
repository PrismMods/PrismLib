using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* A UI Toolkit panel created entirely at runtime.

       The game is Unity 6000.3 and ships UIElementsModule, so UI Toolkit is available to mods:
       retained-mode elements, flexbox layout, and ListView's row recycling, none of which the uGUI
       framework in Bismuth and Sapphire gets without hand-rolling it.

       What is NOT available is the authoring half. USS and UXML are imported by the editor, so a
       mod cannot compile a stylesheet at runtime — every style here is set in C# and every theme
       value is a token (see Tokens). That is also why themeStyleSheet gets an empty instance and
       disableNoThemeWarning is set: a panel with no theme renders fine, it just has no built-in
       control styling, which we do not want anyway since we style our own elements. It does log one
       "no theme style sheet" warning per panel — disableNoThemeWarning exists but is not public.

       One Surface per window. Sorting order decides what sits above what; the update toast's uGUI
       canvas is at 32700, so panels stay well below it. */
    public sealed class Surface : IDisposable
    {
        private GameObject _go;
        private PanelSettings _settings;

        public VisualElement Root { get; private set; }

        /// name is used for the GameObject, so it shows up in a hierarchy dump as "<Mod>Prism…".
        public Surface(string name, float sortingOrder = 100f, UnityEngine.TextCore.Text.FontAsset font = null)
        {
            Ui.Log("Surface '" + name + "': creating UI Toolkit panel");
            _settings = ScriptableObject.CreateInstance<PanelSettings>();
            _settings.name = name + "PanelSettings";
            // Empty rather than null: the panel works either way, but a null theme logs a warning
            // every time and there is no asset to point at from inside a mod.
            _settings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            _settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _settings.referenceResolution = new Vector2Int(1920, 1080);
            _settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            _settings.match = 0.5f;
            _settings.sortingOrder = sortingOrder;

            Ui.Stack.Add(this);
            _go = new GameObject(name);
            UnityEngine.Object.DontDestroyOnLoad(_go);
            var doc = _go.AddComponent<UIDocument>();
            doc.panelSettings = _settings;
            Root = doc.rootVisualElement;
            /* Null here is the failure that looks like nothing happening: the panel exists, the
               hotkey "works", and not one element is ever drawn. Worth its own line. */
            Ui.Log("Surface '" + name + "': root " + (Root == null ? "NULL — nothing will render" : "ok")
                   + ", font " + (font == null ? "none (text may be invisible)" : font.name));
            if (Root != null)
            {
                Root.style.flexGrow = 1f;
                // The panel covers the screen; only what we draw inside it should catch the mouse,
                // or the root would swallow every click in the game.
                Root.pickingMode = PickingMode.Ignore;
                // UI Toolkit draws through TextCore, not TMP: FontDefinition wants a
                // TextCore FontAsset, and TMP_FontAsset is NOT one. A mod that only has the TMP
                // asset should pass the plain Font it was built from instead — see SetFont.
                if (font != null)
                {
                    Root.style.unityFontDefinition = FontDefinition.FromSDFFont(font);
                    Ui.Log("Surface '" + name + "': SDF font " + font.name);
                }
                else SetFont(null);   // never leave a panel fontless; see Fallback
            }
        }

        /* Fallback for hosts that only have a legacy Font. Passing null is fine and normal: a mod
           holding a TMP_FontAsset usually CANNOT produce one, because TMP_FontAsset.sourceFontFile
           is an editor-time reference and comes back null in a player build. That is what happened
           the first time this panel opened, and a panel with no font draws no text at all. */
        public void SetFont(Font font)
        {
            if (Root == null) { Ui.Log("Surface: SetFont ignored, no root"); return; }
            var f = font ?? Fallback();
            if (f == null) { Ui.Log("Surface: NO FONT AVAILABLE — labels will be blank"); return; }
            Root.style.unityFontDefinition = FontDefinition.FromFont(f);
            Ui.Log("Surface: font " + f.name + (font == null ? " (OS fallback)" : ""));
        }

        private static Font _fallback, _mono;

        /* A monospace face for anything columnar. A log is the case that matters: timestamps and
           level tags only line up in a fixed pitch, and the game's own display font — which is what
           a mod's TMP asset is built from — is the worst possible choice for reading one. */
        public static Font Mono()
        {
            if (_mono != null) return _mono;
            try
            {
                _mono = Font.CreateDynamicFontFromOSFont(
                    new[] { "Menlo", "SF Mono", "Consolas", "DejaVu Sans Mono", "Courier New", "monospace" }, 14);
            }
            catch (Exception e) { Ui.Log("Surface: mono font unavailable: " + e.Message); }
            return _mono ?? Fallback();
        }

        /* An OS font, built once. Cheap insurance: every mod has a different font pipeline and any
           of them can hand over null, but a debug window that renders nothing is worse than one in
           the wrong typeface. Not safe during the loader's static-ctor window (Time.frameCount 0),
           which is why it is built on demand rather than at load. */
        private static Font Fallback()
        {
            if (_fallback != null) return _fallback;
            try
            {
                _fallback = Font.CreateDynamicFontFromOSFont(
                    new[] { "Helvetica Neue", "Helvetica", "Arial", "Segoe UI", "Noto Sans", "DejaVu Sans" }, 16);
            }
            catch (Exception e) { Ui.Log("Surface: OS font fallback failed: " + e.Message); }
            return _fallback;
        }

        /* Shown and hidden with display, NOT by deactivating the GameObject.

           UIDocument rebuilds its visual tree in OnEnable, so a SetActive(false)/SetActive(true)
           cycle hands back a DIFFERENT rootVisualElement and everything built into the old one is
           orphaned — the panel reappears empty, with no error anywhere. Keeping the document
           enabled and flipping display costs nothing: a hidden subtree is not laid out or drawn. */
        private bool _visible = true;

        public bool Visible
        {
            // Backed by a field rather than read back from style.display: an unset StyleEnum
            // compares unequal to None, which would report a brand-new panel as visible.
            get { return _visible; }
            set
            {
                if (_visible == value) return;
                _visible = value;
                Ui.OpenWindows += value ? 1 : -1;
                if (value) BringToFront();
                if (Root != null) Root.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// One-shot geometry report: proves layout actually ran and where the element landed.
        /// Resolved styles are NaN until the first layout pass, so this waits for the event.
        public static void LogGeometryOnce(VisualElement e, string label)
        {
            if (e == null) return;
            EventCallback<GeometryChangedEvent> cb = null;
            cb = _ =>
            {
                var r = e.worldBound;
                Ui.Log(label + ": laid out at " + Mathf.RoundToInt(r.x) + "," + Mathf.RoundToInt(r.y)
                       + " size " + Mathf.RoundToInt(r.width) + "x" + Mathf.RoundToInt(r.height)
                       + " (screen " + Screen.width + "x" + Screen.height + ")");
                e.UnregisterCallback(cb);
            };
            e.RegisterCallback(cb);
        }

        /* Raise above every other Prism window. sortingOrder is what orders separate UIDocument
           panels, so raising means taking a number above the highest anyone has used — a counter
           rather than a swap, because two panels sharing a number order arbitrarily. */
        public void BringToFront()
        {
            Ui.Stack.Remove(this);
            Ui.Stack.Add(this);
            if (_settings != null) _settings.sortingOrder = ++_topOrder;
        }

        private static float _topOrder = 30000f;

        public void Dispose()
        {
            Ui.Stack.Remove(this);
            if (_visible) { _visible = false; Ui.OpenWindows--; }
            if (_go != null) UnityEngine.Object.Destroy(_go);
            if (_settings != null) UnityEngine.Object.Destroy(_settings);
            _go = null; _settings = null; Root = null;
        }
    }
}
