using UnityEngine;

namespace PrismLib.UI.Toolkit
{
    /* Theme values as code, because USS cannot be authored at runtime.

       Mutable and global on purpose: a mod sets these once at startup and every element built
       afterwards picks them up, which is the closest thing to a stylesheet this environment allows.
       Accent is the one that changes at runtime (both mods let the user re-skin it), so anything
       using it re-reads it rather than caching a colour. */
    public static class Tokens
    {
        /// Page and section titles. Bigger than body text, and the one place weight is used.
        public static float FontSizeTitle = 19f;

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

        /* Sharp, not rounded: the mods' own panels are square-edged and the framework should look
           like them rather than like a third product. 3px takes the hard edge off a border without
           reading as a pill. */
        public static float Radius      = 3f;
        public static float Pad         = 12f;
        public static float Gap         = 8f;
        // Sized up across the board: the first build was legible on a 2560-wide screen and no more.
        public static float FontSize    = 15f;
        public static float FontSizeSmall = 13f;
        public static float RowHeight   = 24f;
    }
}
