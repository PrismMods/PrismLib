using UnityEngine;

namespace PrismLib.UI.Toolkit
{
    /* Theme values as code, because USS cannot be authored at runtime.

       Mutable and global on purpose: a mod sets these and PrismLib.UI picks them up. Of the colours,
       only Accent reaches Prism windows (Theme.Default reads it live); the rest are kept for the mods
       that still push their whole palette, and the suite's neutrals win (Suite). The sizes are read
       by Suite when a theme is made (Theme.Default at load, new Theme(), Theme.FromAccent), so a
       change reaches schemes made after it. */
    public static class Tokens
    {
        /// Page and section titles. Bigger than body text, and the one place weight is used.
        public static float FontSizeTitle = 17f;

        public static Color Panel       = new Color(0.102f, 0.102f, 0.122f, 0.98f);
        public static Color PanelBorder = new Color(1f, 1f, 1f, 0.10f);
        public static Color TitleBar    = new Color(0.078f, 0.078f, 0.094f, 1f);
        public static Color Row         = new Color(1f, 1f, 1f, 0.025f);
        public static Color RowAlt      = new Color(1f, 1f, 1f, 0.05f);
        public static Color RowHover    = new Color(1f, 1f, 1f, 0.08f);
        public static Color Text        = new Color(0.92f, 0.92f, 0.94f, 1f);
        public static Color TextMuted   = new Color(0.58f, 0.58f, 0.62f, 1f);
        public static Color Accent      = new Color(0.604f, 0.706f, 1f, 1f);
        public static Color Danger      = new Color(0.886f, 0.404f, 0.427f, 1f);
        /// The translucent wash a hotkey window draws over the whole screen behind it (HubLook).
        public static Color Wash        = new Color(0f, 0f, 0f, 0.45f);

        /* Sharp, not rounded: the suite's corners are 2px everywhere, switch knobs included, so the
           framework looks like the mods' square-edged panels rather than like a third product. */
        public static float Radius      = 2f;
        public static float Pad         = 12f;
        public static float Gap         = 8f;
        public static float FontSize    = 13f;
        public static float FontSizeSmall = 12f;
        /// A list or menu row (Sizes.ListRowHeight).
        public static float RowHeight   = 24f;
    }
}
