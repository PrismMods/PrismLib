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

        public static float Radius      = 8f;
        public static float Pad         = 10f;
        public static float Gap         = 6f;
        public static float FontSize    = 13f;
        public static float FontSizeSmall = 11.5f;
        public static float RowHeight   = 20f;
    }
}
