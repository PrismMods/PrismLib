using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    public enum SettingControl { Bool, Int, Float, Choice, Text, Action, Colour, Key }

    /* One setting, flattened to what a widget needs.

       Deliberately NOT PrismLib.SettingEntry: PrismLib.UI must not reference PrismLib.dll, or
       shipping it inside a mod folder would drag in the assembly that may have failed to install.
       The bridge converts. That is the same contract DebugTab has, and it is why this half keeps
       working when the other one is missing. */
    public sealed class SettingRow
    {
        public string Owner, Page, Group, Label, Tooltip;
        public SettingControl Kind;
        public Func<object> Get;
        public Action<object> Set;
        public float Min, Max;
        public string[] Options;          // Choice
        public string ActionLabel;        // Action
        /// Key only. The host starts a capture and calls back with the new binding's display name;
        /// it owns that, because the keys worth binding are ones this panel would treat as
        /// navigation.
        public Action<Action<string>> Capture;
    }

    /* Every mod's settings in one window, grouped and searchable.

       This is the screen the widget port exists for. Bismuth's and Sapphire's settings panels were
       described as "almost identical" and they are: the same rows, drawn by two copies of the same
       UIBuilder. Here a mod DESCRIBES its settings and this draws them, so the layout is written
       once and a new setting is a line of data rather than a screenful of layout code.

       Search spans mods on purpose. The complaint that started this was Quartz having so many
       settings nobody could find one; the answer is one box that looks everywhere, not three. */
    public sealed class SettingsPanel : IDisposable
    {
        private readonly Func<IEnumerable<SettingRow>> _source;
        private Surface _surface;
        private Window _window;
        private Tabs _tabs;
        private ScrollView _body;
        private TextField _search;
        private Label _status;
        private List<SettingRow> _rows = new List<SettingRow>();
        private List<string> _pages = new List<string>();
        private VisualElement _section;

        /// Swapped for PrismLib's ranked matcher when the host has it. Same reason as DebugPanel.
        public Func<SettingRow, string, bool> Matcher =
            (r, q) => (r.Label ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                   || (r.Group ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;

        public SettingsPanel(string title, Func<IEnumerable<SettingRow>> source,
                             UnityEngine.TextCore.Text.FontAsset font = null)
        {
            _source = source ?? (() => new SettingRow[0]);
            Build(title, font);
        }

        public bool Visible
        {
            get { return _surface != null && _surface.Visible; }
            set
            {
                if (_surface == null) return;
                _surface.Visible = value;
                if (value)
                {
                    _window.Reclamp();
                    Anim.SlideIn(_window.Root, -10f, Anim.Fast);
                    Reload();
                }
            }
        }

        public void Toggle() => Visible = !Visible;

        /// Called every frame by the host. Nothing to do yet — settings change when the user
        /// changes them — but the call site exists so adding a live refresh later is one method.
        public void Tick() { }

        public void SetFont(Font font) { if (_surface != null) _surface.SetFont(font); }

        private void Build(string title, UnityEngine.TextCore.Text.FontAsset font)
        {
            _surface = new Surface("PrismSettingsPanel", 30000f, font);
            if (_surface.Root == null) return;

            _window = new Window(_surface.Root, "PrismSettings", title,
                                 new Rect(120f, 80f, 760f, 560f), () => Visible = false);

            _search = new TextField { value = "" };
            _search.style.marginBottom = Tokens.Gap;
            _search.style.fontSize = Tokens.FontSizeSmall;
            _search.style.flexShrink = 0f;
            _search.RegisterValueChangedCallback(_ => Paint());
            _window.Body.Add(_search);

            _tabs = new Tabs(_window.Body, _ => Paint());

            _body = new ScrollView(ScrollViewMode.Vertical);
            _body.style.flexGrow = 1f;
            // The scrolling column must size to its content; letting it shrink is what makes rows
            // pile up instead of scrolling.
            _body.contentContainer.style.flexShrink = 0f;
            _window.Body.Add(_body);

            _status = Ui.Muted("", _window.Footer);
            _status.style.flexGrow = 1f;

            _surface.Visible = false;
        }

        /// Re-read the schema. Cheap enough to do on every open; a mod can add settings at any time.
        public void Reload()
        {
            _rows = new List<SettingRow>(_source());
            _pages.Clear();
            foreach (var r in _rows)
            {
                string p = string.IsNullOrEmpty(r.Page) ? "General" : r.Page;
                if (!_pages.Contains(p)) _pages.Add(p);
            }
            _tabs.Rebuild(_pages);
            Paint();
        }

        private void Paint()
        {
            if (_body == null) return;
            _body.Clear();
            _section = _body;

            string q = (_search.value ?? "").Trim();
            string page = _tabs.SelectedName;
            string group = null;
            int shown = 0;

            foreach (var r in _rows)
            {
                string rp = string.IsNullOrEmpty(r.Page) ? "General" : r.Page;
                // A search looks everywhere: filtering by the open tab as well would hide the hit
                // the user is looking for and leave them thinking the setting does not exist.
                if (q.Length == 0 && rp != page) continue;
                if (q.Length > 0 && !Match(r, q)) continue;

                string g = (q.Length > 0 ? r.Owner + " · " + rp : "") +
                           (string.IsNullOrEmpty(r.Group) ? "" : (q.Length > 0 ? " · " : "") + r.Group);
                if (g != group)
                {
                    group = g;
                    // Searching shows a flat list: folding a section shut would hide a hit.
                    _section = string.IsNullOrEmpty(g) ? _body
                             : q.Length > 0 ? Widgets.Header(_body, g).parent
                             : Widgets.Section(_body, g);
                }

                Add(r);
                shown++;
            }

            if (shown == 0) Ui.Muted(q.Length > 0 ? "No setting matches '" + q + "'" : "Nothing here yet", _body);
            if (_status != null)
                _status.text = shown + " of " + _rows.Count + " setting(s)"
                             + (q.Length > 0 ? " matching" : " on " + page);
        }

        private bool Match(SettingRow r, string q)
        {
            try { return Matcher != null && Matcher(r, q); }
            catch { return false; }
        }

        /* One row per setting. Every Get is somebody else's delegate into a live mod, so a throw
           here must cost that row and not the window. */
        /// Rows go into the open section when there is one, and straight into the page when a
        /// search has flattened it.
        private VisualElement Target => _section ?? _body;

        private void Add(SettingRow r)
        {
            try
            {
                switch (r.Kind)
                {
                    case SettingControl.Bool:
                        Widgets.Toggle(Target, r.Label, Convert.ToBoolean(r.Get()),
                                       v => Write(r, v), r.Tooltip);
                        break;
                    case SettingControl.Int:
                        Widgets.IntSlider(Target, r.Label, Convert.ToInt32(r.Get()),
                                          (int)r.Min, (int)r.Max, v => Write(r, v), r.Tooltip);
                        break;
                    case SettingControl.Float:
                        Widgets.Slider(Target, r.Label, Convert.ToSingle(r.Get()),
                                       r.Min, r.Max, v => Write(r, v), r.Tooltip);
                        break;
                    case SettingControl.Choice:
                        Widgets.Choice(Target, r.Label, r.Options, Convert.ToInt32(r.Get()),
                                       v => Write(r, v), r.Tooltip);
                        break;
                    case SettingControl.Text:
                        Widgets.Text(Target, r.Label, Convert.ToString(r.Get()),
                                     v => Write(r, v), r.Tooltip);
                        break;
                    case SettingControl.Action:
                        Widgets.Action(Target, r.Label, r.ActionLabel ?? "Run",
                                       () => Write(r, null), r.Tooltip);
                        break;
                    case SettingControl.Colour:
                        Widgets.Colour(Target, r.Label, ToColour(r.Get()), v => Write(r, v), r.Tooltip);
                        break;
                    case SettingControl.Key:
                        if (r.Capture == null) { Ui.Muted(r.Label + " — not rebindable here", Target); break; }
                        Widgets.Key(Target, r.Label, Convert.ToString(r.Get()), r.Capture, r.Tooltip);
                        break;
                }
            }
            catch (Exception e)
            {
                Ui.Log("SettingsPanel: '" + r.Label + "' failed to build: " + e.Message);
                Ui.Muted(r.Label + " — unavailable", Target);
            }
        }

        /* A mod may hand a colour over as a Color or as the "#rrggbb" it serialises. Accept both:
           this is a schema filled in by three codebases, and refusing one form means a row that
           silently says "unavailable". */
        private static Color ToColour(object v)
        {
            if (v is Color) return (Color)v;
            Color c;
            if (v != null && ColorUtility.TryParseHtmlString(Convert.ToString(v), out c)) return c;
            return Tokens.Accent;
        }

        private void Write(SettingRow r, object v)
        {
            try { if (r.Set != null) r.Set(v); }
            catch (Exception e) { Ui.Log("SettingsPanel: '" + r.Label + "' setter threw: " + e.Message); }
        }

        public void Dispose()
        {
            if (_surface != null) _surface.Dispose();
            _surface = null; _window = null; _tabs = null; _body = null; _search = null; _status = null;
        }
    }
}
