using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TextCore.Text;

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
    /* Hud draws under every window and is not a window: never in Ui.Stack, never counted as open,
       never closed by Escape, never withholding input — a readout like the FPS counter. */
    public enum Layer { Hud, Window, Popup, Modal, Tooltip }

    public sealed class Surface : IDisposable
    {
        private GameObject _go;
        private PanelSettings _settings;

        public VisualElement Root { get; private set; }

        public Layer Layer { get; }

        public string Name { get; }

        /// While visible, keep keys and the wheel away from the game (InputGate). On by default:
        /// a window you are typing or scrolling in should not also drive the world behind it.
        public bool BlocksGameInput { get; set; } = true;

        /* Called instead of hiding when Escape reaches this surface. The popup host uses it to
           close only its newest popup rather than every one at once. Return true when consumed. */
        internal Func<bool> EscapeHandler;

        /// Raised when the surface hides or is disposed — a popup closes with the window it came from.
        internal event Action Hidden;

        /// The old signature. sortingOrder is ignored now: a window's order comes from its band.
        public Surface(string name, float sortingOrder = 100f, UnityEngine.TextCore.Text.FontAsset font = null)
            : this(name, Layer.Window, font) { }

        /// name is used for the GameObject, so it shows up in a hierarchy dump as "<Mod>Prism…".
        public Surface(string name, Layer layer, UnityEngine.TextCore.Text.FontAsset font = null)
        {
            Layer = layer;
            Name = name;
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
            _settings.textSettings = TextSettings(font);

            // Starts visible (as the GameObject does), so it counts as open until hidden — the
            // count used to start one short, and the first hide took OpenWindows negative.
            if (Layer != Layer.Hud) Ui.OpenWindows++;
            else BlocksGameInput = false;
            BringToFront();
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
                Skin.WatchWheel(Root);
                if (font != null)
                {
                    Root.style.unityFontDefinition = FontDefinition.FromSDFFont(font);
                    Ui.Log("Surface '" + name + "': SDF font " + font.name);
                }
                else SetFont(null);   // never leave a panel fontless; see Fallback
                if (!FontsInherit) Root.schedule.Execute(() => HandDownFonts(Root, default(StyleFontDefinition))).Every(200);
            }
        }

        /* Fallback for hosts that only have a legacy Font. Passing null is fine and normal: a mod
           holding a TMP_FontAsset usually CANNOT produce one, because TMP_FontAsset.sourceFontFile
           is an editor-time reference and comes back null in a player build. That is what happened
           the first time this panel opened, and a panel with no font draws no text at all. */
        public void SetFont(Font font)
        {
            if (Root == null) { Ui.Log("Surface: SetFont ignored, no root"); return; }
            if (font != null)
            {
                Root.style.unityFontDefinition = FontDefinition.FromFont(font);
                Ui.Log("Surface: font " + font.name);
                return;
            }
            var fa = Fallback();
            if (fa == null) { Ui.Log("Surface: NO FONT AVAILABLE — labels will be blank"); return; }
            Root.style.unityFontDefinition = FontDefinition.FromSDFFont(fa);
            Ui.Log("Surface: font " + fa.name + " (OS fallback)");
        }

        /* Before Unity 6 (ADOFAI 2.9 runs Unity 2022) a font set on an element does not reach its
           descendants: in game every label under the root resolved no font and laid out 0px tall,
           so no text drew at all; set on the label itself, it draws. There each surface hands its
           font down by hand on a short schedule — list rows are recycled and popups add elements
           later — giving every text element the nearest font set above it, as inheritance would.
           Stamped elements are marked so a later font change above them still reaches them. */
        private static readonly bool FontsInherit = UnityMajor() >= 6000;
        private const string Stamped = "prism-font-handed-down";

        private static int UnityMajor()
        {
            int n = 0;
            foreach (char c in Application.unityVersion) { if (!char.IsDigit(c)) break; n = n * 10 + (c - '0'); }
            return n;
        }

        private static void HandDownFonts(VisualElement e, StyleFontDefinition inherited)
        {
            var own = e.style.unityFontDefinition;
            if (own.keyword == StyleKeyword.Undefined && !e.ClassListContains(Stamped)) inherited = own;
            else if (e is UnityEngine.UIElements.TextElement && inherited.keyword == StyleKeyword.Undefined)
            {
                e.style.unityFontDefinition = inherited;
                e.AddToClassList(Stamped);
            }
            for (int i = 0; i < e.hierarchy.childCount; i++) HandDownFonts(e.hierarchy[i], inherited);
        }

        private static FontAsset _fallback, _mono;
        private static PanelTextSettings _textSettings;

        /* Every panel's text settings, with OUR face as the default. Left null, UI Toolkit falls
           back to a built-in default that names an OS "DejaVu Sans" / "Regular" — which the Steam
           runtime cannot resolve — and retries it for every label on every frame: ~10,000 "Unable
           to load font face" lines a second, blank labels, and (once a real font was supplied)
           every label drawing the same string. One shared instance; the fallback list is emptied
           so nothing reaches for an OS font behind our back. */
        private static PanelTextSettings TextSettings(FontAsset preferred)
        {
            if (_textSettings != null) return _textSettings;
            _textSettings = ScriptableObject.CreateInstance<PanelTextSettings>();
            _textSettings.name = "PrismPanelTextSettings";
            _textSettings.defaultFontAsset = preferred ?? Fallback();
            _textSettings.fallbackFontAssets = new System.Collections.Generic.List<FontAsset>();
            Ui.Log("Surface: panel text settings, default font "
                   + (_textSettings.defaultFontAsset != null ? _textSettings.defaultFontAsset.name : "NONE"));
            return _textSettings;
        }

        /* Regular, in that order, because a name match alone is not enough — see OsFontAsset. */
        private static readonly string[] _styles = { "Regular", "Book", "Normal", "Medium", "" };

        /* A monospace face for anything columnar. A log is the case that matters: timestamps and
           level tags only line up in a fixed pitch, and the game's own display font — which is what
           a mod's TMP asset is built from — is the worst possible choice for reading one. */
        public static FontAsset Mono()
        {
            if (_mono != null) return _mono;
            return MonoOnly() ?? Fallback();
        }

        private static FontAsset MonoOnly()
        {
            if (_mono != null) return _mono;
            _mono = FileFontAsset(_monoFiles, 14)
                 ?? (_osSdf == null ? FileFontAsset(_macMonoFiles, 14) : null)
                 ?? OsFontAsset(new[] { "Menlo", "SF Mono", "Consolas", "DejaVu Sans Mono", "Noto Sans Mono", "Courier New", "monospace" }, 14);
            return _mono;
        }

        /* Font FILES, tried before any family-name lookup. A family+style asset only works while
           TextCore can find that family again: it reloads the face by name whenever it adds glyphs,
           and on Steam Linux Runtime that reload asks for style "Regular" — which fontconfig's
           "Book"-tagged DejaVu does not answer to. The asset was created fine and then every
           frame logged "Unable to load font face" and drew nothing (232k lines in one session).
           A path-built asset reloads by path, which cannot miss.

           Pairs of regular + bold: the bold file becomes the asset's real 700 weight (see
           AttachBold). Inside the runtime container the game sees its own DejaVu under
           /usr/share/fonts and the host's fonts under /run/host. */
        private static readonly string[][] _sansFiles =
        {
            new[] { "/run/host/usr/share/fonts/noto/NotoSans-Regular.ttf", "/run/host/usr/share/fonts/noto/NotoSans-Bold.ttf" },
            new[] { "/usr/share/fonts/noto/NotoSans-Regular.ttf", "/usr/share/fonts/noto/NotoSans-Bold.ttf" },
            new[] { "/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf", "/usr/share/fonts/truetype/noto/NotoSans-Bold.ttf" },
            new[] { "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf" },
            new[] { "/usr/share/fonts/TTF/DejaVuSans.ttf", "/usr/share/fonts/TTF/DejaVuSans-Bold.ttf" },
            new[] { "/run/host/usr/share/fonts/TTF/DejaVuSans.ttf", "/run/host/usr/share/fonts/TTF/DejaVuSans-Bold.ttf" },
            new[] { @"C:\Windows\Fonts\segoeui.ttf", @"C:\Windows\Fonts\segoeuib.ttf" },
            new[] { @"C:\Windows\Fonts\arial.ttf", @"C:\Windows\Fonts\arialbd.ttf" },
        };

        private static readonly string[][] _monoFiles =
        {
            new[] { "/run/host/usr/share/fonts/noto/NotoSansMono-Regular.ttf", "/run/host/usr/share/fonts/noto/NotoSansMono-Bold.ttf" },
            new[] { "/usr/share/fonts/noto/NotoSansMono-Regular.ttf", "/usr/share/fonts/noto/NotoSansMono-Bold.ttf" },
            new[] { "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSansMono-Bold.ttf" },
            new[] { "/usr/share/fonts/TTF/DejaVuSansMono.ttf", "/usr/share/fonts/TTF/DejaVuSansMono-Bold.ttf" },
            new[] { "/run/host/usr/share/fonts/TTF/DejaVuSansMono.ttf", "/run/host/usr/share/fonts/TTF/DejaVuSansMono-Bold.ttf" },
            new[] { @"C:\Windows\Fonts\consola.ttf", @"C:\Windows\Fonts\consolab.ttf" },
        };

        /* macOS's own files, used FIRST only where there is no SDF family lookup (Unity 2022, ADOFAI
           2.9): there the family path builds a raster atlas and every Prism label drew nothing.
           On Unity 6 the family lookup keeps Helvetica Neue / Menlo, so 3.x looks unchanged.
           Menlo.ttc's face 0 is its regular weight. */
        private static readonly string[][] _macSansFiles =
        {
            new[] { "/System/Library/Fonts/Supplemental/Arial.ttf", "/System/Library/Fonts/Supplemental/Arial Bold.ttf" },
            new[] { "/Library/Fonts/Arial.ttf", "/Library/Fonts/Arial Bold.ttf" },
        };

        private static readonly string[][] _macMonoFiles =
        {
            new[] { "/System/Library/Fonts/Menlo.ttc" },
            new[] { "/System/Library/Fonts/Supplemental/Courier New.ttf", "/System/Library/Fonts/Supplemental/Courier New Bold.ttf" },
        };

        // Printable ASCII plus the symbols the panels draw (arrows, ×, ✕, ▾, ↶ ↷, …, •).
        private static readonly string _preload = BuildPreload();

        private static string BuildPreload()
        {
            var sb = new System.Text.StringBuilder();
            for (char c = ' '; c <= '~'; c++) sb.Append(c);
            sb.Append("×✕▾▸◂▴←→↑↓↶↷…•—–·°");
            return sb.ToString();
        }

        private static FontAsset FileFontAsset(string[][] candidates, int pointSize)
        {
            foreach (var pair in candidates)
            {
                if (!System.IO.File.Exists(pair[0])) continue;
                var fa = FromFile(pair[0], pointSize);
                if (fa == null) continue;
                Ui.Log("Surface: font file " + pair[0]);
                if (pair.Length > 1 && System.IO.File.Exists(pair[1])) AttachBold(fa, FromFile(pair[1], pointSize), pair[1]);
                return fa;
            }
            return null;
        }

        /* The 5-arg overload that asks for SDF is Unity 6 only; ADOFAI 2.9's Unity 2022 has just the
           3-arg one (a raster atlas — see OsFontAsset for what that costs bold text). Found by
           reflection: naming it would bind the dll to Unity 6. */
        private static readonly System.Reflection.MethodInfo _osSdf = typeof(FontAsset).GetMethod("CreateFontAsset",
            new[] { typeof(string), typeof(string), typeof(int), typeof(int), typeof(UnityEngine.TextCore.LowLevel.GlyphRenderMode) });

        private static FontAsset OsFont(string family, string style, int pointSize)
        {
            if (_osSdf != null)
                return (FontAsset)_osSdf.Invoke(null, new object[] { family, style, pointSize, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA });
            return FontAsset.CreateFontAsset(family, style, pointSize);
        }

        private static FontAsset FromFile(string path, int pointSize)
        {
            try
            {
                var fa = FontAsset.CreateFontAsset(path, 0, pointSize, 9,
                    UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
                if (fa != null)
                {
                    fa.name = System.IO.Path.GetFileNameWithoutExtension(path);
                    string missing;
                    bool all = fa.TryAddCharacters(_preload, out missing);
                    Ui.Log("Surface: preloaded " + fa.name + (all ? "" : " (missing: " + missing + ")"));
                }
                return fa;
            }
            catch (Exception e) { Ui.Log("Surface: font file '" + path + "' failed: " + e.Message); return null; }
        }

        /* A real bold face for FontStyle.Bold. Without one UI Toolkit fakes bold by dilating the
           regular glyphs' SDF edge, which at panel sizes comes out smeared and blurry — every header
           and title. Weight 700 is slot 7 of the weight table; the text generator uses a typeface
           found there instead of faking it. */
        private static void AttachBold(FontAsset regular, FontAsset bold, string what)
        {
            if (regular == null || bold == null) return;
            var table = regular.fontWeightTable;
            if (table == null || table.Length < 8) { Ui.Log("Surface: no weight table — bold stays synthesised"); return; }
            table[7].regularTypeface = bold;
            Ui.Log("Surface: bold face " + what);
        }

        /* An OS font, built once. Cheap insurance: every mod has a different font pipeline and any
           of them can hand over null, but a debug window that renders nothing is worse than one in
           the wrong typeface. Not safe during the loader's static-ctor window (Time.frameCount 0),
           which is why it is built on demand rather than at load.

           Goes through FontAsset.CreateFontAsset(family, style, size) rather than
           Font.CreateDynamicFontFromOSFont: the legacy call matches a family NAME against the OS
           list and hands back a non-null Font even when nothing can actually be rendered with it,
           deferring the failure to glyph generation — which runs every frame a label is on screen.
           Measured on Steam Linux Runtime: only "DejaVu Sans" was visible to the sandboxed process
           at all, and fontconfig tags its regular weight style "Book", not "Regular" — a mismatch
           CreateDynamicFontFromOSFont never surfaces, so the panel opened with every frame logging
           "Unable to load font face" and not one visible character. CreateFontAsset checks the
           family+style pair up front and returns null on a miss, so trying several style strings
           per family here actually finds a working one instead of silently failing per frame. */
        private static FontAsset Fallback()
        {
            if (_fallback != null) return _fallback;
            _fallback = FileFontAsset(_sansFiles, 16)
                     ?? (_osSdf == null ? FileFontAsset(_macSansFiles, 16) : null)
                     ?? OsFontAsset(new[] { "Helvetica Neue", "Helvetica", "Arial", "Segoe UI", "Noto Sans", "DejaVu Sans", "Liberation Sans", "Cantarell", "FreeSans" }, 16);
            /* The monospace face as the sans face's fallback: Arial (macOS on 2.9) lacks the panel
               icons (✕ ▾ ▸ ◂ ▴ ↶ ↷) and drew boxes; Menlo preloads every one of them. */
            var mono = MonoOnly();
            if (_fallback != null && mono != null && mono != _fallback)
            {
                if (_fallback.fallbackFontAssetTable == null) _fallback.fallbackFontAssetTable = new System.Collections.Generic.List<FontAsset>();
                if (!_fallback.fallbackFontAssetTable.Contains(mono)) _fallback.fallbackFontAssetTable.Add(mono);
            }
            return _fallback;
        }

        private static FontAsset OsFontAsset(string[] families, int pointSize)
        {
            foreach (string family in families)
                foreach (string style in _styles)
                {
                    FontAsset fa;
                    try
                    {
                        /* The 3-arg CreateFontAsset(family, style, size) passes GlyphRenderMode
                           .DEFAULT (a raster atlas, not SDF) — confirmed by disassembling
                           UnityEngine.TextCoreTextEngineModule.dll. UI Toolkit's faux-bold
                           (FontStyle.Bold on a face with no real bold weight, which is all this
                           fallback ever has) works by dilating the SDF glyph edge; on a raster
                           atlas that dilation has nothing to act on and instead doubles and
                           offsets the glyph. Headers and titles (the only bold text) rendered as
                           a garbled double-strike while plain-weight labels were fine — request
                           SDF explicitly so faux-bold has an edge to dilate. */
                        fa = OsFont(family, style, pointSize);
                    }
                    catch (Exception e) { Ui.Log("Surface: OS font '" + family + "' (" + style + ") failed: " + e.Message); continue; }
                    if (fa != null)
                    {
                        Ui.Log("Surface: OS font asset '" + family + "' style '" + style + "'");
                        // The family's own bold, where the OS has one (macOS's Helvetica Neue does).
                        FontAsset bold = null;
                        try { bold = OsFont(family, "Bold", pointSize); }
                        catch { }
                        AttachBold(fa, bold, family + " Bold");
                        return fa;
                    }
                }
            Ui.Log("Surface: no OS font asset matched any candidate");
            return null;
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
                if (Layer != Layer.Hud) Ui.OpenWindows += value ? 1 : -1;
                if (value) BringToFront();
                if (Root != null) Root.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
                if (!value)
                {
                    if (BlocksGameInput) InputGate.HoldThisFrame();
                    RaiseHidden();
                }
            }
        }

        private void RaiseHidden()
        {
            var h = Hidden;
            if (h == null) return;
            try { h(); } catch (Exception e) { Ui.Log("Surface Hidden handler threw: " + e.Message); }
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

        /* Raise above everything else in this surface's layer. sortingOrder is what orders separate
           UIDocument panels, so raising means taking a number above the highest in use — a counter
           rather than a swap, because two panels sharing a number order arbitrarily.

           Each layer owns a band of numbers, so a window raised a thousand times still sits under
           an open popup, and nothing climbs into the update toast's canvas at 32700. A band that
           runs out renumbers its surfaces in stacking order. Ui.Stack is kept in the same order
           (layer, then raise), so walking it backwards always meets the topmost surface first. */
        public void BringToFront()
        {
            if (Layer == Layer.Hud) { if (_settings != null) _settings.sortingOrder = _bandBase[0]; return; }
            Ui.Stack.Remove(this);
            int at = Ui.Stack.Count;
            while (at > 0 && Ui.Stack[at - 1].Layer > Layer) at--;
            Ui.Stack.Insert(at, this);

            int b = (int)Layer;
            if (_bandNext[b] + 1f >= _bandBase[b] + _bandSpan[b]) Renumber(Layer);
            else if (_settings != null) _settings.sortingOrder = ++_bandNext[b];
        }

        private static readonly float[] _bandBase = { 29000f, 30000f, 31000f, 31500f, 32000f };
        private static readonly float[] _bandSpan = { 1000f, 1000f, 500f, 500f, 500f };
        private static readonly float[] _bandNext = { 29000f, 30000f, 31000f, 31500f, 32000f };

        private static void Renumber(Layer layer)
        {
            int b = (int)layer;
            float n = _bandBase[b];
            foreach (var s in Ui.Stack)
                if (s.Layer == layer && s._settings != null) s._settings.sortingOrder = ++n;
            _bandNext[b] = n;
        }

        public void Dispose()
        {
            Ui.Stack.Remove(this);
            if (_visible) { _visible = false; if (Layer != Layer.Hud) Ui.OpenWindows--; RaiseHidden(); }
            if (_go != null) UnityEngine.Object.Destroy(_go);
            if (_settings != null) UnityEngine.Object.Destroy(_settings);
            _go = null; _settings = null; Root = null;
        }
    }
}
