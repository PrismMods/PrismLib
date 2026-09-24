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
                if (font != null) Root.style.unityFontDefinition = FontDefinition.FromSDFFont(font);
            }
        }

        /// Fallback for hosts that only have a legacy Font (what FontLoader hands back before it
        /// builds the TMP asset). Lower quality than an SDF font, but it renders.
        public void SetFont(Font font)
        {
            if (Root == null || font == null) { Ui.Log("Surface: SetFont ignored (root or font null)"); return; }
            Root.style.unityFontDefinition = FontDefinition.FromFont(font);
            Ui.Log("Surface: font set to " + font.name);
        }

        public bool Visible
        {
            get { return _go != null && _go.activeSelf; }
            set { if (_go != null) _go.SetActive(value); }
        }

        public void Dispose()
        {
            if (_go != null) UnityEngine.Object.Destroy(_go);
            if (_settings != null) UnityEngine.Object.Destroy(_settings);
            _go = null; _settings = null; Root = null;
        }
    }
}
