using System;

namespace PrismLib.UI.Toolkit
{
    /* PrismLib's own Facet host: every Prism window is a Facet surface made for it. Log, language
       and geometry go through Ui.Log, Words.Language and Window.Read/Write, fields the mods' bridges
       set at any time, so each is read at use rather than captured. Made with the first surface,
       which is also when Tokens' sizes are copied into Facet's (sizes stay global, as before). */
    internal static class PrismHost
    {
        private static Facet.Host _instance;

        internal static Facet.Host Instance
        {
            get
            {
                if (_instance != null) return _instance;
                Facet.Tokens.FontSizeTitle = Tokens.FontSizeTitle;
                Facet.Tokens.Radius = Tokens.Radius;
                Facet.Tokens.Pad = Tokens.Pad;
                Facet.Tokens.Gap = Tokens.Gap;
                Facet.Tokens.FontSize = Tokens.FontSize;
                Facet.Tokens.FontSizeSmall = Tokens.FontSizeSmall;
                Facet.Tokens.RowHeight = Tokens.RowHeight;
                _instance = Facet.Host.Create(Words.Self, o =>
                {
                    o.Log = m => Ui.Log(m);
                    o.Translate = Words.T;
                    o.Language = () => Words.Language != null ? Words.Language() : null;
                    o.Read = k => Window.Read != null ? Window.Read(k) : null;
                    o.Write = (k, v) => { if (Window.Write != null) Window.Write(k, v); };
                    o.Theme = Theme.Default.Twin;
                });
                return _instance;
            }
        }

        /* Elements built before they have a parent resolve to this host, so Facet translates them
           now, with whatever Words.Owner the build pinned, and not again when they join the panel. */
        internal static IDisposable Use() => Facet.Host.Use(Instance);
    }
}
