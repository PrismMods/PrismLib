using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* A mod's colour scheme over a Facet.Theme (Twin): Facet paints by role, so recolouring a
       scheme re-themes what is already on screen — see Facet.Theme. Sizes come from Facet's Theme.Sizes (the Suite skin sets them); Tokens' size fields only serve Prism-only elements.

       Default IS the Tokens fields, so a mod that pushes its palette into Tokens keeps working: its
       twin is the PrismLib host's own theme (never Facet's frozen Dark), refreshed from Tokens when a
       surface is made and whenever Use or For hands Default out, so windows built after a push wear
       it. Setting a colour writes through without a live re-theme, as pushing Tokens always did;
       Recolour re-themes live. */
    public sealed class Theme
    {
        // Each Facet scheme back to its PrismLib one, so For() hands out the object a mod made.
        // Declared before Default, whose constructor adds to it.
        private static readonly ConditionalWeakTable<Facet.Theme, Theme> _of = new ConditionalWeakTable<Facet.Theme, Theme>();

        public static readonly Theme Default = new Theme(true, new Facet.Theme());

        /// The Facet scheme this one reads and writes. Not named Facet: that would hide the namespace here.
        internal readonly Facet.Theme Twin;
        private readonly bool _tokens;

        private Theme(bool tokens, Facet.Theme twin)
        {
            _tokens = tokens; Twin = twin;
            _of.Add(twin, this);
        }

        /// A copy of the current Default, to adjust.
        public Theme() : this(false, new Facet.Theme()) { CopyFrom(Default); }

        /// Take every colour from `o`, in place, so whatever already holds this scheme sees them.
        public void CopyFrom(Theme o)
        {
            if (o == null) return;
            Panel = o.Panel; PanelBorder = o.PanelBorder; TitleBar = o.TitleBar;
            Row = o.Row; RowAlt = o.RowAlt; RowHover = o.RowHover;
            Text = o.Text; TextMuted = o.TextMuted; Accent = o.Accent; Danger = o.Danger;
        }

        public Color Panel       { get { return Get(Facet.Role.Panel, Tokens.Panel); }             set { if (_tokens) Tokens.Panel = value; Twin[Facet.Role.Panel] = value; } }
        public Color PanelBorder { get { return Get(Facet.Role.PanelBorder, Tokens.PanelBorder); } set { if (_tokens) Tokens.PanelBorder = value; Twin[Facet.Role.PanelBorder] = value; } }
        public Color TitleBar    { get { return Get(Facet.Role.TitleBar, Tokens.TitleBar); }       set { if (_tokens) Tokens.TitleBar = value; Twin[Facet.Role.TitleBar] = value; } }
        public Color Row         { get { return Get(Facet.Role.Row, Tokens.Row); }                 set { if (_tokens) Tokens.Row = value; Twin[Facet.Role.Row] = value; } }
        public Color RowAlt      { get { return Get(Facet.Role.RowAlt, Tokens.RowAlt); }           set { if (_tokens) Tokens.RowAlt = value; Twin[Facet.Role.RowAlt] = value; } }
        public Color RowHover    { get { return Get(Facet.Role.RowHover, Tokens.RowHover); }       set { if (_tokens) Tokens.RowHover = value; Twin[Facet.Role.RowHover] = value; } }
        public Color Text        { get { return Get(Facet.Role.Text, Tokens.Text); }               set { if (_tokens) Tokens.Text = value; Twin[Facet.Role.Text] = value; } }
        public Color TextMuted   { get { return Get(Facet.Role.TextMuted, Tokens.TextMuted); }     set { if (_tokens) Tokens.TextMuted = value; Twin[Facet.Role.TextMuted] = value; } }
        public Color Danger      { get { return Get(Facet.Role.Danger, Tokens.Danger); }           set { if (_tokens) Tokens.Danger = value; Twin[Facet.Role.Danger] = value; } }
        /// The focus ring follows the accent, as in Facet's own derivation.
        public Color Accent
        {
            get { return Get(Facet.Role.Accent, Tokens.Accent); }
            set { if (_tokens) Tokens.Accent = value; Twin[Facet.Role.Accent] = value; Twin[Facet.Role.Focus] = value; }
        }

        private Color Get(Facet.Role r, Color token) { return _tokens ? token : Twin[r]; }

        /// Default's twin takes the Tokens fields as they are now.
        internal void Refresh()
        {
            if (!_tokens) return;
            Twin[Facet.Role.Panel] = Tokens.Panel; Twin[Facet.Role.PanelBorder] = Tokens.PanelBorder;
            Twin[Facet.Role.TitleBar] = Tokens.TitleBar; Twin[Facet.Role.Row] = Tokens.Row;
            Twin[Facet.Role.RowAlt] = Tokens.RowAlt; Twin[Facet.Role.RowHover] = Tokens.RowHover;
            Twin[Facet.Role.Text] = Tokens.Text; Twin[Facet.Role.TextMuted] = Tokens.TextMuted;
            Twin[Facet.Role.Accent] = Tokens.Accent; Twin[Facet.Role.Focus] = Tokens.Accent;
            Twin[Facet.Role.Danger] = Tokens.Danger;
        }

        /// A whole scheme from one colour (Quartz's derivation; see Facet.Theme.FromAccent).
        public static Theme FromAccent(Color accent, float tint = 1f)
            => new Theme(false, Facet.Theme.FromAccent(accent, tint));

        /// Re-derive every colour from `accent`, in place, and re-theme every window wearing it.
        public void Recolour(Color accent, float tint = 1f)
        {
            if (_tokens)
            {
                // Tokens first: a handler of the re-theme below may ask For(), which refreshes from them.
                var d = Facet.Theme.FromAccent(accent, tint);
                Tokens.Panel = d[Facet.Role.Panel]; Tokens.PanelBorder = d[Facet.Role.PanelBorder];
                Tokens.TitleBar = d[Facet.Role.TitleBar]; Tokens.Row = d[Facet.Role.Row];
                Tokens.RowAlt = d[Facet.Role.RowAlt]; Tokens.RowHover = d[Facet.Role.RowHover];
                Tokens.Text = d[Facet.Role.Text]; Tokens.TextMuted = d[Facet.Role.TextMuted];
                Tokens.Accent = d[Facet.Role.Accent]; Tokens.Danger = d[Facet.Role.Danger];
            }
            Twin.Recolour(accent, tint);
        }

        /// Give `e` and everything under it this scheme. Null clears it.
        public static void Set(VisualElement e, Theme t) => Facet.Theme.Set(e, t != null ? t.Twin : null);

        /// The scheme `e` should draw with. Null-safe; never returns null.
        public static Theme For(VisualElement e) => Of(Facet.Theme.For(e));

        /// The PrismLib scheme over a Facet one: Default for the host's own and for Facet's frozen
        /// Dark (what an element under no host finds), a wrapper for a scheme another host made.
        internal static Theme Of(Facet.Theme t)
        {
            if (t == null || t == Default.Twin || t == Facet.Theme.Dark) { Default.Refresh(); return Default; }
            Theme p;
            return _of.TryGetValue(t, out p) ? p : new Theme(false, t);   // the constructor registers it
        }

        /// For a build that happens before its elements join the tree: `using (Theme.Use(t)) { … }`.
        /// A Surface created inside the scope keeps the scheme — how a panel built in its constructor
        /// gets one: `using (Theme.Use(mine)) panel = new SettingsPanel(…);`.
        public static IDisposable Use(Theme t)
        {
            if (t == Default) Default.Refresh();
            return Facet.Theme.Use(t != null ? t.Twin : null);
        }
    }
}
