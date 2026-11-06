using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* Settings rows: Facet's Controls, under the names and return types the mods were built against.

       Every widget is a labelled ROW: label on the left, control on the right, one consistent height.
       Each takes a current value and a setter rather than binding to a field, because the mods store
       settings in their own serialised classes; the pair becomes Facet's Bind.Value, so every change
       is undoable and the row shows whatever an undo puts back. Built under PrismLib's host, so a row
       made before it joins a panel still translates through Words. */
    public static class Widgets
    {
        public const float RowHeight = Facet.Controls.RowHeight;

        /// Label on the left, whatever the caller adds on the right.
        public static VisualElement Row(VisualElement parent, string label, string tooltip = null)
        {
            using (PrismHost.Use()) return Facet.Controls.Row(parent, label, tooltip);
        }

        /// A switch, not a checkbox. Returns the switch itself.
        public static VisualElement Toggle(VisualElement parent, string label, bool value,
                                           Action<bool> onChange, string tooltip = null)
        {
            using (PrismHost.Use())
                return Facet.Controls.Toggle(parent, label, Facet.Bind.Value(value, onChange), o => o.Tooltip = tooltip).Input;
        }

        /// A toggle and the rows it governs: returns the body, shown only while the switch is on.
        public static VisualElement ToggleGroup(VisualElement parent, string label, bool value,
                                                Action<bool> onChange, string tooltip = null)
        {
            using (PrismHost.Use())
                return Facet.Controls.ToggleGroup(parent, label, Facet.Bind.Value(value, onChange), o => o.Tooltip = tooltip);
        }

        /// The slider runs 0..1 and Facet maps it onto min..max; the typed number beside it is the value.
        public static Slider Slider(VisualElement parent, string label, float value, float min, float max,
                                    Action<float> onChange, string tooltip = null, string format = "0.##")
        {
            using (PrismHost.Use())
                return (Slider)Facet.Controls.Slider(parent, label, Facet.Bind.Value(value, onChange), o =>
                {
                    o.Tooltip = tooltip; o.Min = min; o.Max = max; o.Format = format;
                }).Input;
        }

        public static SliderInt IntSlider(VisualElement parent, string label, int value, int min, int max,
                                          Action<int> onChange, string tooltip = null)
        {
            using (PrismHost.Use())
                return (SliderInt)Facet.Controls.IntSlider(parent, label, Facet.Bind.Value(value, onChange), o =>
                {
                    o.Tooltip = tooltip; o.Min = min; o.Max = max;
                }).Input;
        }

        /// Segmented buttons; returns the container that holds them.
        public static VisualElement Choice(VisualElement parent, string label, IList<string> options,
                                           int index, Action<int> onChange, string tooltip = null)
        {
            using (PrismHost.Use())
                return Facet.Controls.Choice(parent, label, Facet.Bind.Value(index, onChange), o =>
                {
                    o.Tooltip = tooltip; o.Items = options;
                }).Input;
        }

        /// Commits on Enter or focus loss, not per keystroke.
        public static TextField Text(VisualElement parent, string label, string value,
                                     Action<string> onChange, string tooltip = null)
        {
            using (PrismHost.Use())
                return (TextField)Facet.Controls.Text(parent, label, Facet.Bind.Value(value ?? "", onChange), o => o.Tooltip = tooltip).Input;
        }

        /// Read-only text beside a label that wraps when long, instead of a TextField that cuts it off.
        public static Label Info(VisualElement parent, string label, string value, string tooltip = null)
        {
            using (PrismHost.Use())
                return (Label)Facet.Controls.Info(parent, label, () => value, o => o.Tooltip = tooltip).Input;
        }

        public static Button Action(VisualElement parent, string label, string buttonText,
                                    Action onClick, string tooltip = null)
        {
            using (PrismHost.Use()) return Facet.Controls.Action(parent, label, buttonText, onClick, o => o.Tooltip = tooltip);
        }

        /// Destructive action: reads as dangerous and asks once.
        public static Button Danger(VisualElement parent, string label, string buttonText,
                                    Action onConfirm, string tooltip = null)
        {
            using (PrismHost.Use()) return Facet.Controls.Danger(parent, label, buttonText, onConfirm, o => o.Tooltip = tooltip);
        }

        /// The colour picker's row (see ColourPicker).
        public static VisualElement Colour(VisualElement parent, string label, UnityEngine.Color value,
                                           Action<UnityEngine.Color> onChange, string tooltip = null,
                                           bool hasAlpha = false)
        {
            using (PrismHost.Use())
                return Facet.Controls.Colour(parent, label, Facet.Bind.Value(value, onChange), o =>
                {
                    o.Tooltip = tooltip; o.Alpha = hasAlpha;
                }).Root;
        }

        /* Keybind capture: click, then the host polls for the next key and calls back with its name.
           The host sets the binding itself, so the bind only remembers what was last shown and writes
           nothing — a capture must not leave an undo step that would undo nothing. */
        public static VisualElement Key(VisualElement parent, string label, string current,
                                        Action<Action<string>> beginCapture, string tooltip = null)
        {
            string shown = current ?? "";
            using (PrismHost.Use())
                return Facet.Controls.Key(parent, label, Facet.Bind.To<string>(() => shown, null), o =>
                {
                    o.Tooltip = tooltip;
                    if (beginCapture != null) o.Capture = done => beginCapture(name => { shown = name ?? ""; done(name); });
                }).Input;
        }

        /// A section that folds away; its state is kept per page and title.
        public static VisualElement Section(VisualElement parent, string title, bool defaultOpen = true)
        {
            using (PrismHost.Use()) return Facet.Controls.Section(parent, title, defaultOpen);
        }

        /// A list entry: a name, an optional note under it, and the returned row for its buttons.
        public static VisualElement Item(VisualElement parent, string title, string note = null)
        {
            using (PrismHost.Use()) return Facet.Controls.Item(parent, title, note);
        }

        /// Marks a list entry as the active one, the way a profile list shows which is loaded.
        public static void MarkActive(VisualElement actions, string label = null)
        {
            using (PrismHost.Use()) Facet.Controls.MarkActive(actions, label);
        }

        /// A destructive button that is its own label.
        public static Button DangerButton(VisualElement parent, string text, Action onConfirm,
                                          string tooltip = null)
        {
            using (PrismHost.Use()) return Facet.Controls.DangerButton(parent, text, onConfirm, o => o.Tooltip = tooltip);
        }

        /// A row that opens a page of its own, pushed onto the Nav building this one.
        public static VisualElement SubPage(VisualElement parent, string label,
                                            Action<VisualElement> build, string tooltip = null)
        {
            using (PrismHost.Use()) return Facet.Controls.SubPage(parent, label, build, o => o.Tooltip = tooltip);
        }

        /// A switch AND a page behind it, on one row; returns the row.
        public static VisualElement ToggleSubPage(VisualElement parent, string label, bool value,
                                                  Action<bool> onChange, Action<VisualElement> build,
                                                  string tooltip = null)
        {
            using (PrismHost.Use())
                return Facet.Controls.ToggleSubPage(parent, label, Facet.Bind.Value(value, onChange), build, o => o.Tooltip = tooltip).Root;
        }

        public static Label Header(VisualElement parent, string text)
        {
            using (PrismHost.Use()) return Facet.Controls.Header(parent, text);
        }

        public static VisualElement Spacer(VisualElement parent, float height = 8f)
        {
            using (PrismHost.Use()) return Facet.Controls.Spacer(parent, height);
        }
    }
}
