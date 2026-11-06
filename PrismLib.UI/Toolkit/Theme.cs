using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* A mod's colour scheme over a Facet.Theme (Twin): Facet paints by role, so recolouring a
       scheme re-themes what is already on screen — see Facet.Theme. Every scheme made here wears the
       suite (Suite: neutrals, Sizes, Looks); a scheme another host made is wrapped as it is.

       Default's neutrals are the suite's. Of a mod's Tokens push only Accent reaches it: its getter
       reads Tokens.Accent, and its twin takes it when a surface is made and whenever Use or For hands
       Default out. Setting a colour writes through without a live re-theme; Recolour re-themes live. */
    public sealed class Theme
    {
        // Each Facet scheme back to its PrismLib one, so For() hands out the object a mod made.
        // Declared before Default, whose constructor adds to it.
        private static readonly ConditionalWeakTable<Facet.Theme, Theme> _of = new ConditionalWeakTable<Facet.Theme, Theme>();

        public static readonly Theme Default = new Theme(true, true, Suite.Wear(new Facet.Theme()));

        /// The Facet scheme this one reads and writes. Not named Facet: that would hide the namespace here.
        internal readonly Facet.Theme Twin;
        private readonly bool _tokens, _suite;

        private Theme(bool tokens, bool suite, Facet.Theme twin)
        {
            _tokens = tokens; _suite = suite; Twin = twin;
            _of.Add(twin, this);
        }

        /// The suite with Default's colours, to adjust.
        public Theme() : this(false, true, Suite.Wear(new Facet.Theme())) { CopyFrom(Default); }

        /// Take every colour from `o`, in place, so whatever already holds this scheme sees them.
        public void CopyFrom(Theme o)
        {
            if (o == null) return;
            Panel = o.Panel; PanelBorder = o.PanelBorder; TitleBar = o.TitleBar;
            Row = o.Row; RowAlt = o.RowAlt; RowHover = o.RowHover;
            Text = o.Text; TextMuted = o.TextMuted; Accent = o.Accent; Danger = o.Danger;
        }

        public Color Panel       { get { return Twin[Facet.Role.Panel]; } set { if (_tokens) Tokens.Panel = value; Twin[Facet.Role.Panel] = value; } }
        public Color PanelBorder { get { return Twin[Facet.Role.PanelBorder]; } set { if (_tokens) Tokens.PanelBorder = value; Twin[Facet.Role.PanelBorder] = value; } }
        public Color TitleBar    { get { return Twin[Facet.Role.TitleBar]; } set { if (_tokens) Tokens.TitleBar = value; Twin[Facet.Role.TitleBar] = value; } }
        public Color Row         { get { return Twin[Facet.Role.Row]; } set { if (_tokens) Tokens.Row = value; Twin[Facet.Role.Row] = value; } }
        public Color RowAlt      { get { return Twin[Facet.Role.RowAlt]; } set { if (_tokens) Tokens.RowAlt = value; Twin[Facet.Role.RowAlt] = value; } }
        public Color RowHover    { get { return Twin[Facet.Role.RowHover]; } set { if (_tokens) Tokens.RowHover = value; Twin[Facet.Role.RowHover] = value; } }
        public Color Text        { get { return Twin[Facet.Role.Text]; } set { if (_tokens) Tokens.Text = value; Twin[Facet.Role.Text] = value; } }
        public Color TextMuted   { get { return Twin[Facet.Role.TextMuted]; } set { if (_tokens) Tokens.TextMuted = value; Twin[Facet.Role.TextMuted] = value; } }
        public Color Danger      { get { return Twin[Facet.Role.Danger]; } set { if (_tokens) Tokens.Danger = value; Twin[Facet.Role.Danger] = value; } }
        /// The focus ring follows the accent, as in Facet's own derivation.
        public Color Accent
        {
            get { return _tokens ? Tokens.Accent : Twin[Facet.Role.Accent]; }
            set { if (_tokens) Tokens.Accent = value; Twin[Facet.Role.Accent] = value; Twin[Facet.Role.Focus] = value; }
        }

        /// Default's twin takes Tokens.Accent as it is now: the one colour a mod's push reaches.
        internal void Refresh()
        {
            if (!_tokens) return;
            Twin[Facet.Role.Accent] = Tokens.Accent; Twin[Facet.Role.Focus] = Tokens.Accent;
        }

        /// A whole scheme from one colour (Quartz's derivation; see Facet.Theme.FromAccent), in the suite's sizes and looks.
        public static Theme FromAccent(Color accent, float tint = 1f)
        {
            var t = Facet.Theme.FromAccent(accent, tint);
            Suite.Shape(t);
            return new Theme(false, false, t);
        }

        /// Re-theme every window wearing this scheme around `accent`, in place. A suite scheme keeps the
        /// suite's neutrals (tint unused); one from FromAccent or another host re-derives every colour.
        public void Recolour(Color accent, float tint = 1f)
        {
            if (!_suite) { Twin.Recolour(accent, tint); return; }
            // Tokens first: a handler of the re-theme below may ask For(), which refreshes from it.
            if (_tokens) Tokens.Accent = accent;
            Suite.Wear(Twin);
            Twin[Facet.Role.Accent] = accent; Twin[Facet.Role.Focus] = accent;
            Twin.Notify();
        }

        /// Give `e` and everything under it this scheme. Null clears it.
        public static void Set(VisualElement e, Theme t) => Facet.Theme.Set(e, t != null ? t.Twin : null);

        /// The scheme `e` should draw with. Null-safe; never returns null.
        public static Theme For(VisualElement e) => Of(Facet.Theme.For(e));

        /// The PrismLib scheme over a Facet one: Default for the host's own and for Facet's frozen
        /// Dark (what an element under no host finds), a wrapper for a scheme another host made,
        /// which is never dressed in the suite.
        internal static Theme Of(Facet.Theme t)
        {
            if (t == null || t == Default.Twin || t == Facet.Theme.Dark) { Default.Refresh(); return Default; }
            Theme p;
            return _of.TryGetValue(t, out p) ? p : new Theme(false, false, t);   // the constructor registers it
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
