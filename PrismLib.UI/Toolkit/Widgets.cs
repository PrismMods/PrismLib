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
