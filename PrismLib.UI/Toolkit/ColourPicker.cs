using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* The colour picker: Facet's, under the name the mods know. Topaz's model — a collapsed row with
       the swatch and hex, opening into a saturation/value square, a hue strip, an optional alpha
       strip, numeric channels and a hex field. */
    public sealed class ColourPicker
    {
        public VisualElement Root { get; private set; }

        public ColourPicker(VisualElement parent, string label, Color initial, bool hasAlpha,
                            Action<Color> onChange)
        {
            using (PrismHost.Use()) Root = new Facet.ColourPicker(parent, label, initial, hasAlpha, onChange).Root;
        }
    }
}
