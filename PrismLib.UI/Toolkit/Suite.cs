using UnityEngine;
using Facet;

namespace PrismLib.UI.Toolkit
{
    /* The Prism suite's look (spec "The suite look"): Topaz's neutral greys, 2px corners, 1px lines,
       rows on a barely visible fill, the accent only on state. Every theme PrismLib.UI makes wears it,
       and a Facet-native host (Quartz) dresses its own theme with Wear. Each mod keeps its accent. */
    public static class Suite
    {
        internal static readonly Color Window = new Color32(0x1A, 0x1A, 0x1F, 0xFF), TitleBar = new Color32(0x14, 0x14, 0x18, 0xFF),
            Sidebar = new Color32(0x11, 0x11, 0x15, 0xFF), RowFill = new Color(1f, 1f, 1f, 0.025f), RowHover = new Color(1f, 1f, 1f, 0.05f),
            ButtonFill = new Color(1f, 1f, 1f, 0.08f), Line = new Color(1f, 1f, 1f, 0.07f), Border = new Color(1f, 1f, 1f, 0.10f),
            Text = new Color32(0xEB, 0xEB, 0xF0, 0xFF), Muted = new Color32(0x94, 0x94, 0x9E, 0xFF), SwitchOff = new Color(1f, 1f, 1f, 0.18f),
            Danger = new Color32(0xE2, 0x67, 0x6D, 0xFF);

        /// Every neutral role, Sizes and Looks to the suite's, in place; Accent, Focus and AccentText
        /// stay the caller's. Facet's shared Dark refuses writes, so it comes back as it was.
        public static Facet.Theme Wear(Facet.Theme t)
        {
            if (t == null || ReferenceEquals(t, Facet.Theme.Dark)) return t;
            t[Role.Panel] = Window; t[Role.TitleBar] = TitleBar; t[Role.Rail] = Sidebar;
            t[Role.Row] = RowFill; t[Role.RowHover] = RowHover; t[Role.RowAlt] = ButtonFill;
            t[Role.Line] = Line; t[Role.PanelBorder] = Border; t[Role.Off] = SwitchOff;
            t[Role.Text] = Text; t[Role.TextMuted] = Muted; t[Role.Danger] = Danger;
            Shape(t);
            return t;
        }

        /* Every field of Sizes and Looks, none left to Facet's defaults (Dark's Rule is invisible, its
           cards sit on the window fill). Sizes Tokens names are read here, when a theme is made. */
        internal static void Shape(Facet.Theme t)
        {
            var s = t.Sizes;
            s.Radius = s.SwitchRadius = s.KnobRadius = Tokens.Radius;
            s.Line = 1f;
            s.Pad = Tokens.Pad; s.Gap = Tokens.Gap;
            s.ControlPad = 5f; s.FieldPad = 4f;
            s.RowHeight = 32f; s.ListRowHeight = Tokens.RowHeight; s.NavRowHeight = 28f; s.TitleBarHeight = 36f; s.ItemHeight = 38f;
            s.Title = Tokens.FontSizeTitle; s.Body = Tokens.FontSize; s.Small = Tokens.FontSizeSmall; s.Caps = 11f; s.CapsSpacing = 1f;
            s.SwitchWidth = 30f; s.SwitchHeight = 16f;
            s.Track = 3f; s.ThumbWidth = 4f; s.ThumbHeight = 14f;
            s.SliderWidth = 160f; s.NumberWidth = 52f; s.DropdownWidth = 140f;
            s.Indicator = 2f;

            var k = t.Looks;
            k.Button = new Look { Bg = Role.RowHover, Text = Role.Text, Border = Role.Line, HoverBg = Role.RowAlt, PressedBg = Role.Row,
                SelectedBg = Role.Accent, SelectedText = Role.AccentText, FocusBorder = Role.Focus };
            k.DangerButton = new Look { Bg = Role.RowHover, Text = Role.Danger, Border = Role.Line, HoverBg = Role.RowAlt, PressedBg = Role.Row,
                SelectedBg = Role.Danger, SelectedText = Role.AccentText, FocusBorder = Role.Focus };
            k.FlatButton = new Look { Text = Role.Text, FocusBorder = Role.Focus };
            k.AccentButton = new Look { Bg = Role.Accent, Text = Role.AccentText, FocusBorder = Role.Focus };
            // The card (Group) carries the row fill; a row adds only its hover.
            k.Row = new Look { HoverBg = Role.RowHover };
            k.RailItem = new Look { Text = Role.TextMuted, HoverBg = Role.Row, HoverText = Role.Text, SelectedBg = Role.RowHover, SelectedText = Role.Text };
            k.Field = new Look { Bg = Role.RowHover, Text = Role.Text, Border = Role.Line, HoverBg = Role.RowAlt, FocusBorder = Role.Focus };
            k.Ring = new Look { FocusBorder = Role.Focus };
            k.Chrome = new Look { Bg = Role.TitleBar };
            k.Card = new Look { Bg = Role.Panel, Border = Role.PanelBorder };
            k.Group = new Look { Bg = Role.Row, Border = Role.Line };
            // The preview stage's look (ruling S21).
            k.Stage = new Look { Bg = Role.Row, Border = Role.Line };
            k.Switch = new Look { Bg = Role.Off, SelectedBg = Role.Accent, FocusBorder = Role.Focus };
            k.Knob = new Look { Bg = Role.Text };
            k.Track = new Look { Bg = Role.Off };
            k.Fill = new Look { Bg = Role.Accent };
            k.Thumb = new Look { Bg = Role.Text };
            k.Tab = new Look { Text = Role.TextMuted, HoverText = Role.Text, SelectedText = Role.Text, FocusBorder = Role.Focus };
            k.Indicator = new Look { SelectedBg = Role.Accent };
            k.Tile = new Look { Bg = Role.Row, Border = Role.Line, Text = Role.TextMuted, HoverBg = Role.RowHover, SelectedText = Role.Text, FocusBorder = Role.Focus };
            k.Link = new Look { Text = Role.Accent, HoverText = Role.Text, FocusBorder = Role.Focus };
            k.Rule = new Look { Bg = Role.Line };
            k.Caps = new Look { Text = Role.TextMuted };
        }
    }
}
