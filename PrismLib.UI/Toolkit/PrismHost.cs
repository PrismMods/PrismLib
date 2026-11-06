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
                _instance = Make(Words.Self, Words.T);
                return _instance;
            }
        }

        /* The host a settings row of `owner` is built under, by name ("PrismLib/<owner>"), for
           Facet.Field.Owner. It translates with that owner's table alone, so a later relabel or an
           undo tooltip cannot borrow another mod's sense of a word (CamDisplay's "Undo"). Made once:
           Host.Named keeps the first host of a name. */
        internal static string Owner(string owner)
        {
            string name = Words.Self + "/" + owner;
            if (Facet.Host.Named(name) == null)
                Make(name, s =>
                {
                    var was = Words.Owner;
                    Words.Owner = owner;
                    try { return Words.T(s); } finally { Words.Owner = was; }
                });
            return name;
        }

        private static Facet.Host Make(string name, Func<string, string> translate) => Facet.Host.Create(name, o =>
        {
            o.Log = m => Ui.Log(m);
            o.Translate = translate;
            // A mod may never set Words.Language (CamDisplay): English, not null, which Facet keys indexes by.
            o.Language = () => (Words.Language != null ? Words.Language() : null) ?? "en";
            o.Read = k => Window.Read != null ? Window.Read(k) : null;
            o.Write = (k, v) => { if (Window.Write != null) Window.Write(k, v); };
            o.Theme = Theme.Default.Twin;
        });

        /* Elements built before they have a parent resolve to this host, so Facet translates them
           now, with whatever Words.Owner the build pinned, and not again when they join the panel. */
        internal static IDisposable Use() => Facet.Host.Use(Instance);
    }
}
