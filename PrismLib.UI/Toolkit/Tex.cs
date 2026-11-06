using UnityEngine;

namespace PrismLib.UI.Toolkit
{
    /// Generated textures a colour picker needs, made once and kept — see Facet.Tex.
    public static class Tex
    {
        /// Vertical hue ramp, red at the bottom round to red at the top.
        public static Texture2D Hue() => Facet.Tex.Hue();

        /// White to transparent, left to right — saturation over a hue-coloured background.
        public static Texture2D Sat() => Facet.Tex.Sat();

        /// Transparent to black, top to bottom — value, laid over the saturation ramp.
        public static Texture2D Val() => Facet.Tex.Val();

        /// Grey checkerboard, so a low alpha reads as transparent rather than as dark.
        public static Texture2D Checker(int cell = 6) => Facet.Tex.Checker(cell);
    }
}
