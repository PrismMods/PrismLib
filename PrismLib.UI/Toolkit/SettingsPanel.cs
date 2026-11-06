using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /// Info: read-only text that may be long (a status, a path); it wraps instead of running out of
    /// the window. Append new kinds at the end: mods compiled against an older build pass the numbers.
    public enum SettingControl { Bool, Int, Float, Choice, Text, Action, Colour, Key, Info }

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

       This is the screen the widget port exists for. Topaz's and Sapphire's settings panels were
       described as "almost identical" and they are: the same rows, drawn by two copies of the same
       UIBuilder. Here a mod DESCRIBES its settings and this draws them, so the layout is written
       once and a new setting is a line of data rather than a screenful of layout code.

       Search spans mods on purpose. The complaint that started this was Quartz having so many
       settings nobody could find one; the answer is one box that looks everywhere, not three. */
    public sealed class SettingsPanel : IDisposable
    {
        private readonly Func<IEnumerable<SettingRow>> _source;
        private Surface _surface;
        /* The scheme this panel draws in — the owning mod's; null is Theme.Default, the Tokens a mod
           pushed. The window is built in the constructor, so build it inside `Theme.Use` to colour the
           chrome too; setting this afterwards reaches only what is built or hovered later. */
        public Theme Theme
        {
            get { return _surface != null ? _surface.Theme : null; }
            set { if (_surface != null) _surface.Theme = value; }
        }
        private Window _window;
        private Tabs _tabs;
        private ScrollView _body;
        private TextField _search;
        private Label _status;
        private readonly List<Entry> _rows = new List<Entry>();
        private readonly List<string> _pages = new List<string>();

        /* A row as the search sees it: as the player reads it (translated by its owner) and in
           English, so a query in either language finds it. Built once per Reload, not per keystroke. */
        private sealed class Entry
        {
            public SettingRow Row, Shown;
            public string Page;
            public Facet.Search.Text[] Label, Context;
            public Facet.Search.Text Tip;
        }

        private static readonly Func<SettingRow, string, bool> Contains =
            (r, q) => (r.Label ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                   || (r.Group ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;

        /// Left alone, search is Facet's (label, group, page and tooltip in both languages, Korean
        /// initials). A host that sets its own is asked instead, with the row as the player reads it.
        public Func<SettingRow, string, bool> Matcher = Contains;

        private Func<IEnumerable<NavItem>> _navPages;
        private Nav _nav;

        public SettingsPanel(string title, Func<IEnumerable<SettingRow>> source,
                             UnityEngine.TextCore.Text.FontAsset font = null)
        {
            _source = source ?? (() => new SettingRow[0]);
            Build(title, font);
        }

        /* A mod's OWN menu: it hands over nav pages it built with Widgets, and this supplies the
           window, rail, breadcrumb and scrolling. That is the shape the settings screens are being
           rebuilt in — the library provides the framework, not a place to dump every setting. */
        public SettingsPanel(string title, Func<IEnumerable<NavItem>> pages,
                             UnityEngine.TextCore.Text.FontAsset font = null)
        {
            _navPages = pages;
            _source = () => new SettingRow[0];
            Build(title, font);
        }

        /// While open, keep keys and the wheel from the game (InputGate). On by default.
        public bool BlocksGameInput
        {
            get { return _surface == null || _surface.BlocksGameInput; }
            set { if (_surface != null) _surface.BlocksGameInput = value; }
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
                    if (_window == null) return;
                    _window.Reclamp();
                    Anim.SlideIn(_window.Root, -10f, Anim.Fast);
                    Reload();
                }
            }
        }

        public void Toggle() => Visible = !Visible;

        /// Hint on the left, version on the right — the shape both mods' own panels already use.
        public void SetFooter(string hint, string right)
        {
            if (_window != null) _window.SetFooter(hint, right);
        }

        /// Called every frame by the host. Schema rows need nothing: Facet re-reads them while the
        /// window is open, so a value changed behind it shows in place.
        public void Tick()
        {
            if (Visible && _nav != null) _nav.Tick();
        }

        public void SetFont(Font font) { if (_surface != null) _surface.SetFont(font); }

        private static string Slug(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in s ?? "") if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.Length > 0 ? sb.ToString() : "Settings";
        }

        private void Build(string title, UnityEngine.TextCore.Text.FontAsset font)
        {
            // Named from the title, so a mod's own menu and the library's window are distinct
            // GameObjects with distinct saved geometry rather than two things called the same.
            _surface = new Surface(Slug(title) + "Panel", Layer.Window, font);
            if (_surface.Root == null) return;

            _window = new Window(_surface.Root, Slug(title), title,
                                 new Rect(120f, 80f, 760f, 560f), () => Visible = false);
            _window.OnRaise = () => _surface.BringToFront();

            if (_navPages != null)
            {
                _nav = new Nav(_window.Body, _navPages());
                _window.ShowSearch(Words.Own("Search settings…"), q => _nav.Filter(q));
                _nav.OnInsetChanged = inset => _window.SetContentInset(inset);
                // Undo writes straight into the mod's settings; the page has to be re-read or it
                // keeps showing what the control was set to before.
                History.AfterApply = () => { if (_nav != null) _nav.Refresh(); };
                _status = Ui.Muted("", _window.Footer);
                _status.style.flexGrow = 1f;
                _surface.Visible = false;
                return;
            }

            // Schema mode keeps its own search box: it has tabs rather than a rail, so the header
            // toggle and its alignment do not apply.
            _search = new TextField { value = "" };
            _search.style.marginBottom = Tokens.Gap;
            _search.style.fontSize = Tokens.FontSizeSmall;
            _search.style.flexShrink = 0f;
            Skin.When<TextField>(_search, Skin.Field);
            _search.RegisterValueChangedCallback(_ => Paint());
            _window.Body.Add(_search);

            _tabs = new Tabs(_window.Body, _ => Paint());

            _body = new ScrollView(ScrollViewMode.Vertical);
            _body.style.flexGrow = 1f;
            /* The flexbox rule that breaks every scroll container: a flex item's minimum size is
               its CONTENT, so a list taller than the window makes the container taller than the
               window too, and the window simply clips it — no scrollbar, no wheel. Every ancestor
               between here and the fixed-height window needs the same zero. */
            _body.style.minHeight = 0f;
            // The scrolling column must size to its content; letting it shrink is what makes rows
            // pile up instead of scrolling.
            _body.contentContainer.style.flexShrink = 0f;
            Skin.When<ScrollView>(_body, Skin.Scroll);
            _window.Body.Add(_body);

            _status = Ui.Muted("", _window.Footer);
            _status.style.flexGrow = 1f;

            _surface.Visible = false;
        }

        /// Re-read the schema. Cheap enough to do on every open; a mod can add settings at any time.
        public void Reload()
        {
            if (_nav != null) { _nav.Refresh(); return; }
            _rows.Clear();
            _pages.Clear();
            foreach (var r in _source())
            {
                if (r == null) continue;
                var e = new Entry { Row = r };
                As(r.Owner, () => e.Shown = Localised(r));
                e.Page = string.IsNullOrEmpty(r.Page) ? Words.Own("General") : e.Shown.Page;
                e.Label = Texts(r.Label, e.Shown.Label);
                e.Context = Texts(r.Group, e.Shown.Group, r.Page, e.Page, r.Owner);
                e.Tip = new Facet.Search.Text(r.Tooltip + " " + e.Shown.Tooltip);
                _rows.Add(e);
                if (!_pages.Contains(e.Page)) _pages.Add(e.Page);
            }
            _tabs.Rebuild(_pages);
            Paint();
        }

        private void Paint()
        {
            if (_body == null) return;
            _body.Clear();

            string q = (_search.value ?? "").Trim();
            string page = _tabs.SelectedName;
            // A search looks everywhere: filtering by the open tab as well would hide the hit the
            // user is looking for and leave them thinking the setting does not exist.
            var shown = q.Length > 0 ? Find(q) : _rows.FindAll(e => e.Page == page);

            /* One form per run of rows with one owner: the run builds under that owner's words, so
               its section titles come from its own table too. A search is a flat list under
               "owner · page · group" headers, because folding a section shut would hide a hit. */
            for (int i = 0; i < shown.Count;)
            {
                var first = shown[i];
                string head = q.Length > 0 ? Head(first) : null;
                var run = new List<Facet.Field>();
                for (; i < shown.Count && shown[i].Row.Owner == first.Row.Owner && (head == null || Head(shown[i]) == head); i++)
                    run.Add(Field(shown[i].Row, head == null));
                As(first.Row.Owner, () =>
                {
                    if (head != null) Widgets.Header(_body, head);
                    Facet.Form.Build(_body, run);
                });
            }

            if (shown.Count == 0) Ui.Muted(q.Length > 0 ? Words.OwnF("No setting matches '{0}'", q) : Words.Own("Nothing here yet"), _body);
            if (_status != null)
                _status.text = shown.Count + " of " + _rows.Count + " setting(s)"
                             + (q.Length > 0 ? " matching" : " on " + page);
        }

        private static string Head(Entry e)
            => e.Row.Owner + " · " + e.Page + (string.IsNullOrEmpty(e.Shown.Group) ? "" : " · " + e.Shown.Group);

        private List<Entry> Find(string q)
        {
            if (Matcher != null && Matcher != Contains) return _rows.FindAll(e => Match(e.Shown, q));
            var query = new Facet.Search.Query(q);
            var hits = new List<Entry>();
            // Typos only when nothing matches exactly, so a near miss never sits among real hits.
            for (int pass = 0; pass < 2 && hits.Count == 0; pass++)
                foreach (var e in _rows)
                    if (Facet.Search.Rate(query, e.Label, e.Context, e.Tip, pass == 1) > 0) hits.Add(e);
            return hits;
        }

        private bool Match(SettingRow r, string q)
        {
            try { return Matcher(r, q); }
            catch { return false; }
        }

        private static Facet.Search.Text[] Texts(params string[] s) => Array.ConvertAll(s, x => new Facet.Search.Text(x));

        /* SettingControl's nine members are FieldKind's first nine, in order. English goes in: the
           row's owner host translates it, now and on any relabel. Every Get is somebody else's
           delegate into a live mod; Facet turns one that throws into a dimmed row, not a dead window. */
        private static Facet.Field Field(SettingRow r, bool grouped)
        {
            var get = r.Get;
            bool text = r.Kind == SettingControl.Text || r.Kind == SettingControl.Key || r.Kind == SettingControl.Info;
            return new Facet.Field
            {
                Owner = PrismHost.Owner(r.Owner), Page = r.Page, Group = grouped ? r.Group : null,
                Label = r.Label, Tooltip = r.Tooltip, Kind = (Facet.FieldKind)r.Kind,
                // Facet reads null as a broken getter; text with nothing in it is only empty.
                Get = text && get != null ? () => get() ?? "" : get,
                // A key's Capture sets the binding itself and calls back only with what to show.
                Set = r.Kind == SettingControl.Key ? null : r.Set,
                Min = r.Min, Max = r.Max, Options = r.Options,
                ActionLabel = r.ActionLabel ?? Words.Own("Run"), Capture = r.Capture,
            };
        }

        /// Builds with only the row's own mod translating it (see Words).
        private static void As(string owner, Action build)
        {
            var was = Words.Owner;
            Words.Owner = owner;
            try { build(); } finally { Words.Owner = was; }
        }

        /* Rows arrive in English (PrismLib's own always do). Translated on the way in, into a copy,
           so the search matches what the player reads and the English stays searchable too. */
        private static SettingRow Localised(SettingRow r) => new SettingRow
        {
            Owner = r.Owner, Page = Words.T(r.Page), Group = Words.T(r.Group), Label = Words.T(r.Label),
            Tooltip = Words.T(r.Tooltip), ActionLabel = Words.T(r.ActionLabel),
            Options = r.Options == null ? null : Array.ConvertAll(r.Options, Words.T),
            Kind = r.Kind, Get = r.Get, Set = r.Set, Min = r.Min, Max = r.Max, Capture = r.Capture,
        };

        /* The first window drawn in a session costs ~85 ms more than any after it, so the first
           surface draws a throwaway one off screen at load — see Facet.WarmUp. */
        internal static bool WarmedUp => Facet.WarmUp.Done;

        internal static void WarmUp() => Facet.WarmUp.Run();

        public void Dispose()
        {
            if (_surface != null) _surface.Dispose();
            _surface = null; _window = null; _tabs = null; _body = null; _search = null; _status = null;
        }
    }
}
