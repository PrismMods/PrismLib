using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* A mod's colour scheme. Sizes stay global in Tokens; colours are per window, so a Quartz
       window can be red while a Sapphire one beside it is blue.

       An element finds its scheme with For(): the nearest ancestor that was given one (a Surface,
       or a popup card that took its anchor's), else the scheme of the build in progress (Use), else
       Default. Default IS the Tokens fields, so a mod that pushes its palette into Tokens keeps
       working unchanged and its colours show everywhere, as before.

       Elements copy colours into inline styles when they are built, so changing a scheme recolours
       what is built afterwards and any hover that reads it — not what is already drawn. Pushing
       Tokens always behaved the same way. */
    public sealed class Theme
    {
        public static readonly Theme Default = new Theme(true);

        private readonly bool _tokens;
        private Color _panel, _panelBorder, _titleBar, _row, _rowAlt, _rowHover, _text, _textMuted, _accent, _danger;

        private Theme(bool tokens) { _tokens = tokens; }

        /// A copy of the current Default, to adjust.
        public Theme() : this(false) { CopyFrom(Default); }

        /// Take every colour from `o`, in place, so whatever already holds this scheme sees them.
        public void CopyFrom(Theme o)
        {
            if (o == null) return;
            Panel = o.Panel; PanelBorder = o.PanelBorder; TitleBar = o.TitleBar;
            Row = o.Row; RowAlt = o.RowAlt; RowHover = o.RowHover;
            Text = o.Text; TextMuted = o.TextMuted; Accent = o.Accent; Danger = o.Danger;
        }

        public Color Panel       { get { return _tokens ? Tokens.Panel : _panel; }             set { if (_tokens) Tokens.Panel = value; else _panel = value; } }
        public Color PanelBorder { get { return _tokens ? Tokens.PanelBorder : _panelBorder; } set { if (_tokens) Tokens.PanelBorder = value; else _panelBorder = value; } }
        public Color TitleBar    { get { return _tokens ? Tokens.TitleBar : _titleBar; }       set { if (_tokens) Tokens.TitleBar = value; else _titleBar = value; } }
        public Color Row         { get { return _tokens ? Tokens.Row : _row; }                 set { if (_tokens) Tokens.Row = value; else _row = value; } }
        public Color RowAlt      { get { return _tokens ? Tokens.RowAlt : _rowAlt; }           set { if (_tokens) Tokens.RowAlt = value; else _rowAlt = value; } }
        public Color RowHover    { get { return _tokens ? Tokens.RowHover : _rowHover; }       set { if (_tokens) Tokens.RowHover = value; else _rowHover = value; } }
        public Color Text        { get { return _tokens ? Tokens.Text : _text; }               set { if (_tokens) Tokens.Text = value; else _text = value; } }
        public Color TextMuted   { get { return _tokens ? Tokens.TextMuted : _textMuted; }     set { if (_tokens) Tokens.TextMuted = value; else _textMuted = value; } }
        public Color Accent      { get { return _tokens ? Tokens.Accent : _accent; }           set { if (_tokens) Tokens.Accent = value; else _accent = value; } }
        public Color Danger      { get { return _tokens ? Tokens.Danger : _danger; }           set { if (_tokens) Tokens.Danger = value; else _danger = value; } }

        /// A whole scheme from one colour.
        public static Theme FromAccent(Color accent, float tint = 1f)
        {
            var t = new Theme(false);
            t.Recolour(accent, tint);
            return t;
        }

        /* Quartz's derivation (its UIColors.ApplyAccent): surfaces are near-black greys leaning
           toward the accent, controls a lighter grey leaning further, muted text the accent washed
           out. `tint` scales the lean — 0 is plain grey, 1 is Quartz's warm look. In place, so
           everything already holding this scheme sees the change. */
        public void Recolour(Color accent, float tint = 1f)
        {
            accent = new Color(Mathf.Clamp01(accent.r), Mathf.Clamp01(accent.g), Mathf.Clamp01(accent.b), 1f);
            Accent = accent;
            Color panel = Lean(0.037f, accent, 0.059f * tint);
            Panel = new Color(panel.r, panel.g, panel.b, 0.98f);
            TitleBar = Lean(0.061f, accent, 0.102f * tint);
            Color control = Lean(0.173f, accent, 0.146f * tint);
            RowAlt = control;
            RowHover = Color.Lerp(control, Color.white, 0.08f);
            Row = Color.Lerp(TitleBar, control, 0.5f);
            PanelBorder = new Color(1f, 1f, 1f, 0.09f);
            Text = Lean(0.92f, accent, 0.04f * tint);
            TextMuted = Lean(0.745f, accent, 0.17f * tint);
            // A red accent cannot also mean "destructive", so near red the warning turns orange.
            float h, s, v;
            Color.RGBToHSV(accent, out h, out s, out v);
            bool reddish = s > 0.25f && (h < 0.06f || h > 0.94f);
            Danger = reddish ? new Color(1f, 0.478f, 0.271f, 1f) : new Color(0.886f, 0.404f, 0.427f, 1f);
        }

        private static Color Lean(float grey, Color accent, float amount)
            => Color.Lerp(new Color(grey, grey, grey, 1f), accent, amount);

        private static readonly ConditionalWeakTable<VisualElement, Theme> _of = new ConditionalWeakTable<VisualElement, Theme>();
        private static Theme _building;
        internal static Theme Building { get { return _building; } }

        /// Give `e` and everything under it this scheme. Null clears it.
        public static void Set(VisualElement e, Theme t)
        {
            if (e == null) return;
            _of.Remove(e);
            if (t != null) _of.Add(e, t);
        }

        /// The scheme `e` should draw with. Null-safe; never returns null.
        public static Theme For(VisualElement e)
        {
            for (var x = e; x != null; x = x.hierarchy.parent)
            {
                Theme t;
                if (_of.TryGetValue(x, out t)) return t;
            }
            return _building ?? Default;
        }

        /// For a build that happens before its elements join the tree: `using (Theme.Use(t)) { … }`.
        /// A Surface created inside the scope keeps the scheme — how a panel built in its constructor
        /// gets one: `using (Theme.Use(mine)) panel = new SettingsPanel(…);`.
        public static IDisposable Use(Theme t) { return new Scope(t); }

        private sealed class Scope : IDisposable
        {
            private readonly Theme _prev;
            public Scope(Theme t) { _prev = _building; _building = t; }
            public void Dispose() { _building = _prev; }
        }
    }
}
