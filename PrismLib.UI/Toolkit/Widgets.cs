using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* Settings rows.

       This is the replacement for the widget half of UIBuilder, which both mods carry in copies
       that differ by 615 lines out of 4503 — the same toggle, slider and dropdown written twice and
       then drifted. Every widget here is a labelled ROW: label on the left, control on the right,
       one consistent height. That shape is the reason the two mods' settings screens looked alike
       in the first place, so it is the thing worth keeping.

       Each takes a current value and a setter rather than binding to a field, because the mods
       store settings in their own serialised classes and none of that belongs here. A widget reads
       once at build and writes on change; a host that mutates a value behind the UI's back calls
       Refresh on the page, exactly as the uGUI version did. */
    public static class Widgets
    {
        public const float RowHeight = 28f;

        /// Label on the left, whatever the caller adds on the right.
        public static VisualElement Row(VisualElement parent, string label, string tooltip = null)
        {
            var row = Ui.Row(parent);
            row.style.minHeight = RowHeight;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.paddingLeft = 4f;
            row.style.paddingRight = 4f;
            if (!string.IsNullOrEmpty(tooltip)) row.tooltip = tooltip;

            var l = Ui.Text(label, row);
            l.style.flexGrow = 1f;
            l.style.flexShrink = 1f;
            l.pickingMode = PickingMode.Ignore;

            var right = Ui.Row(row);
            right.style.flexShrink = 0f;
            right.name = "control";
            return right;
        }

        /* A switch, not a checkbox: UI Toolkit's Toggle is a tick box whose styling lives in the
           theme style sheet we do not have, so it would come out unstyled. Drawn from two elements
           instead — a track and a knob — which also lets it animate. */
        public static VisualElement Toggle(VisualElement parent, string label, bool value,
                                           Action<bool> onChange, string tooltip = null)
        {
            var host = Row(parent, label, tooltip);
            var track = new VisualElement();
            track.style.width = 36f;
            track.style.height = 18f;
            Ui.SetRadius(track, 9f);
            track.style.backgroundColor = value ? Tokens.Accent : Tokens.RowAlt;
            track.pickingMode = PickingMode.Position;

            var knob = new VisualElement();
            knob.style.position = Position.Absolute;
            knob.style.top = 2f;
            knob.style.width = 14f;
            knob.style.height = 14f;
            Ui.SetRadius(knob, 7f);
            knob.style.backgroundColor = Color.white;
            knob.style.left = value ? 20f : 2f;
            knob.pickingMode = PickingMode.Ignore;
            track.Add(knob);

            bool state = value;
            track.RegisterCallback<ClickEvent>(_ =>
            {
                state = !state;
                track.style.backgroundColor = state ? Tokens.Accent : Tokens.RowAlt;
                Anim.Move(knob, state ? 20f : 2f, Anim.Fast);
                try { if (onChange != null) onChange(state); } catch (Exception e) { Ui.Log("Toggle handler threw: " + e.Message); }
            });
            host.Add(track);
            return track;
        }

        public static Slider Slider(VisualElement parent, string label, float value, float min, float max,
                                    Action<float> onChange, string tooltip = null, string format = "0.##")
        {
            var host = Row(parent, label, tooltip);
            var readout = Ui.Muted(value.ToString(format), host);
            readout.style.minWidth = 44f;
            readout.style.unityTextAlign = TextAnchor.MiddleRight;
            readout.style.marginRight = Tokens.Gap;

            var s = new Slider(min, max) { value = value };
            s.style.width = 160f;
            s.RegisterValueChangedCallback(e =>
            {
                readout.text = e.newValue.ToString(format);
                try { if (onChange != null) onChange(e.newValue); } catch (Exception ex) { Ui.Log("Slider handler threw: " + ex.Message); }
            });
            host.Add(s);
            return s;
        }

        public static SliderInt IntSlider(VisualElement parent, string label, int value, int min, int max,
                                          Action<int> onChange, string tooltip = null)
        {
            var host = Row(parent, label, tooltip);
            var readout = Ui.Muted(value.ToString(), host);
            readout.style.minWidth = 44f;
            readout.style.unityTextAlign = TextAnchor.MiddleRight;
            readout.style.marginRight = Tokens.Gap;

            var s = new SliderInt(min, max) { value = value };
            s.style.width = 160f;
            s.RegisterValueChangedCallback(e =>
            {
                readout.text = e.newValue.ToString();
                try { if (onChange != null) onChange(e.newValue); } catch (Exception ex) { Ui.Log("IntSlider handler threw: " + ex.Message); }
            });
            host.Add(s);
            return s;
        }

        /* Segmented buttons rather than a popup list. Same reason as the toggle — DropdownField's
           menu is themed by the style sheet we do not have — and for the handful of options a
           setting usually has, seeing them all beats opening a list to find out. */
        public static VisualElement Choice(VisualElement parent, string label, IList<string> options,
                                           int index, Action<int> onChange, string tooltip = null)
        {
            var host = Row(parent, label, tooltip);
            var buttons = new List<Button>();
            int current = index;

            for (int i = 0; i < (options != null ? options.Count : 0); i++)
            {
                int idx = i;
                var b = Ui.Btn(options[i], () =>
                {
                    if (idx == current) return;
                    current = idx;
                    for (int j = 0; j < buttons.Count; j++) Ui.Highlight(buttons[j], j == current);
                    Anim.Pop(buttons[idx]);
                    try { if (onChange != null) onChange(idx); } catch (Exception e) { Ui.Log("Choice handler threw: " + e.Message); }
                }, host);
                buttons.Add(b);
            }
            for (int j = 0; j < buttons.Count; j++) Ui.Highlight(buttons[j], j == current);
            return host;
        }

        public static TextField Text(VisualElement parent, string label, string value,
                                     Action<string> onChange, string tooltip = null)
        {
            var host = Row(parent, label, tooltip);
            var f = new TextField { value = value ?? "" };
            f.style.width = 200f;
            f.style.fontSize = Tokens.FontSizeSmall;
            // On commit, not per keystroke: a setter that persists to disk should not run once per
            // character typed.
            f.RegisterCallback<BlurEvent>(_ => Commit(f, onChange));
            f.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) Commit(f, onChange); });
            host.Add(f);
            return f;
        }

        private static void Commit(TextField f, Action<string> onChange)
        {
            try { if (onChange != null) onChange(f.value); }
            catch (Exception e) { Ui.Log("Text handler threw: " + e.Message); }
        }

        public static Button Action(VisualElement parent, string label, string buttonText,
                                    Action onClick, string tooltip = null)
        {
            var host = Row(parent, label, tooltip);
            return Ui.Btn(buttonText, onClick, host);
        }

        /// Destructive action: reads as dangerous and asks once. The confirm is the point — every
        /// mod had grown its own armed-button dance for this.
        public static Button Danger(VisualElement parent, string label, string buttonText,
                                    Action onConfirm, string tooltip = null)
        {
            var host = Row(parent, label, tooltip);
            Button b = null;
            bool armed = false;
            b = Ui.Btn(buttonText, () =>
            {
                if (!armed)
                {
                    armed = true;
                    b.text = "Sure?";
                    b.style.backgroundColor = Tokens.Danger;
                    Anim.Pop(b);
                    return;
                }
                armed = false;
                b.text = buttonText;
                b.style.backgroundColor = Tokens.RowAlt;
                try { if (onConfirm != null) onConfirm(); } catch (Exception e) { Ui.Log("Danger handler threw: " + e.Message); }
            }, host);
            b.style.color = Tokens.Danger;
            return b;
        }

        /* Colour: a swatch that opens an inline HSV-free picker — three sliders and a preview.
           Deliberately not a colour wheel. Sapphire's ColorWheel is 485 lines of procedural mesh
           for a surface people mostly use to nudge an accent, and three labelled channels are
           easier to type an exact value into anyway. */
        public static VisualElement Colour(VisualElement parent, string label, Color value,
                                           Action<Color> onChange, string tooltip = null)
        {
            var host = Row(parent, label, tooltip);
            var swatch = new VisualElement();
            swatch.style.width = 40f;
            swatch.style.height = 18f;
            Ui.SetRadius(swatch, 4f);
            Ui.SetBorderWidth(swatch, 1f);
            Ui.SetBorderColor(swatch, Tokens.PanelBorder);
            swatch.style.backgroundColor = value;
            host.Add(swatch);

            // The editor hangs under the row, so opening it pushes the rest of the page down
            // rather than covering it — a popup would need a layer and a dismiss rule.
            var editor = Ui.Box(parent);
            editor.style.display = DisplayStyle.None;
            Ui.SetPadding(editor, Tokens.Gap);

            Color current = value;
            Action<int, float> set = (channel, v) =>
            {
                if (channel == 0) current.r = v; else if (channel == 1) current.g = v; else current.b = v;
                swatch.style.backgroundColor = current;
                try { if (onChange != null) onChange(current); }
                catch (Exception e) { Ui.Log("Colour handler threw: " + e.Message); }
            };
            Channel(editor, "R", current.r, v => set(0, v));
            Channel(editor, "G", current.g, v => set(1, v));
            Channel(editor, "B", current.b, v => set(2, v));

            swatch.pickingMode = PickingMode.Position;
            swatch.RegisterCallback<ClickEvent>(_ =>
            {
                bool open = editor.style.display == DisplayStyle.None;
                if (open) Anim.SlideIn(editor, -6f, Anim.Fast); else editor.style.display = DisplayStyle.None;
            });
            return swatch;
        }

        private static void Channel(VisualElement parent, string name, float value, Action<float> onChange)
        {
            var row = Ui.Row(parent);
            var l = Ui.Muted(name, row);
            l.style.width = 16f;
            var s = new Slider(0f, 1f) { value = value };
            s.style.flexGrow = 1f;
            var readout = Ui.Muted(Mathf.RoundToInt(value * 255f).ToString(), row);
            readout.style.minWidth = 32f;
            readout.style.unityTextAlign = TextAnchor.MiddleRight;
            s.RegisterValueChangedCallback(e =>
            {
                readout.text = Mathf.RoundToInt(e.newValue * 255f).ToString();
                onChange(e.newValue);
            });
            row.Insert(1, s);
        }

        /* Keybind capture. Click, then press — the next key wins, Escape cancels, and the row says
           which it is waiting for. The capture has to be polled by the host rather than read from a
           UI Toolkit event, because the keys worth binding include ones the panel would otherwise
           treat as navigation (Tab, arrows, Escape). */
        public static VisualElement Key(VisualElement parent, string label, string current,
                                        Action<Action<string>> beginCapture, string tooltip = null)
        {
            var host = Row(parent, label, tooltip);
            Button b = null;
            b = Ui.Btn(string.IsNullOrEmpty(current) ? "unbound" : current, () =>
            {
                b.text = "press a key…";
                Ui.Highlight(b, true);
                try
                {
                    beginCapture(name =>
                    {
                        b.text = string.IsNullOrEmpty(name) ? "unbound" : name;
                        Ui.Highlight(b, false);
                        Anim.Pop(b);
                    });
                }
                catch (Exception e)
                {
                    Ui.Log("Key capture threw: " + e.Message);
                    b.text = current; Ui.Highlight(b, false);
                }
            }, host);
            b.style.minWidth = 110f;
            return b;
        }

        /* A section that folds away. Long settings pages are mostly headings you are not reading;
           this is the one piece of UIBuilder's ExpandSection/Collapsible pair worth keeping, and
           the state is remembered per title so a page does not refold on every rebuild. */
        private static readonly Dictionary<string, bool> _folded = new Dictionary<string, bool>();

        public static VisualElement Section(VisualElement parent, string title, bool defaultOpen = true)
        {
            bool open = _folded.ContainsKey(title) ? _folded[title] : defaultOpen;

            var head = Ui.Row(parent);
            head.style.marginTop = Tokens.Pad;
            head.pickingMode = PickingMode.Position;
            var arrow = Ui.Muted(open ? "▾" : "▸", head);
            arrow.style.width = 14f;
            var l = Ui.Text(title, head, Tokens.FontSize);
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.color = Tokens.TextMuted;
            l.pickingMode = PickingMode.Ignore;

            var body = Ui.Box(parent);
            body.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            body.style.marginBottom = Tokens.Gap;

            head.RegisterCallback<ClickEvent>(_ =>
            {
                open = !open;
                _folded[title] = open;
                arrow.text = open ? "▾" : "▸";
                if (open) Anim.SlideIn(body, -6f, Anim.Fast);
                else body.style.display = DisplayStyle.None;
            });
            return body;
        }

        public static Label Header(VisualElement parent, string text)
        {
            var l = Ui.Text(text, parent, Tokens.FontSize);
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.color = Tokens.TextMuted;
            l.style.marginTop = Tokens.Pad;
            l.style.marginBottom = Tokens.Gap;
            l.style.flexShrink = 0f;
            return l;
        }

        public static VisualElement Spacer(VisualElement parent, float height = 8f)
        {
            var e = Ui.Box(parent);
            e.style.height = height;
            e.style.flexShrink = 0f;
            return e;
        }
    }
}
