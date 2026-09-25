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
        public const float RowHeight = 32f;

        /* Wraps a setter so every change is undoable. The previous value is snapshotted INSIDE the
           returned action, not captured from the enclosing variable, or the undo closure would read
           whatever the value had become by the time it ran. */
        private static Action<T> Track<T>(string label, T initial, Action<T> apply)
        {
            T last = initial;
            return v =>
            {
                T prev = last;
                History.Record(label, label, () => apply(prev), () => apply(v));
                apply(v);
                last = v;
            };
        }

        /// Label on the left, whatever the caller adds on the right.
        public static VisualElement Row(VisualElement parent, string label, string tooltip = null)
        {
            var row = Ui.Row(parent);
            row.style.minHeight = RowHeight;
            row.style.marginBottom = 2f;
            row.userData = label;          // how a search result finds this row again
            SearchIndex.Note(label);
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.paddingLeft = 4f;
            row.style.paddingRight = 4f;
            if (!string.IsNullOrEmpty(tooltip)) row.tooltip = tooltip;

            var l = Ui.Text(label, row);
            l.style.flexGrow = 1f;
            l.style.flexShrink = 1f;
            l.pickingMode = PickingMode.Ignore;

            row.RegisterCallback<MouseEnterEvent>(_ => row.style.backgroundColor = Tokens.Row);
            row.RegisterCallback<MouseLeaveEvent>(_ => row.style.backgroundColor = Color.clear);

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
            return BuildSwitch(host, label, value, onChange);
        }

        private static VisualElement BuildSwitch(VisualElement host, string label, bool value,
                                                 Action<bool> onChange)
        {
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
            var set = Track(label, value, v =>
            {
                state = v;
                track.style.backgroundColor = v ? Tokens.Accent : Tokens.RowAlt;
                Anim.Move(knob, v ? 20f : 2f, Anim.Fast);
                try { if (onChange != null) onChange(v); } catch (Exception e) { Ui.Log("Toggle handler threw: " + e.Message); }
            });
            track.RegisterCallback<ClickEvent>(e =>
            {
                set(!state);
                // Or the row underneath would also take the click and navigate.
                e.StopPropagation();
            });
            host.Add(track);
            return track;
        }

        /* A toggle and the rows it governs. Showing controls that the mod is currently ignoring is
           the single most common way these settings screens confuse people — the accent picker
           under a disabled "use a custom colour" was one, and Hide UI has several more. The body is
           only built once; the toggle shows and hides it. */
        public static VisualElement ToggleGroup(VisualElement parent, string label, bool value,
                                                Action<bool> onChange, string tooltip = null)
        {
            var body = Ui.Box(parent);
            Toggle(parent, label, value, v =>
            {
                body.style.display = v ? DisplayStyle.Flex : DisplayStyle.None;
                if (onChange != null) onChange(v);
            }, tooltip);
            // Built before the toggle so the toggle reads above it, then moved back under it.
            parent.Remove(body);
            parent.Add(body);
            body.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            body.style.paddingLeft = Tokens.Pad;
            return body;
        }

        public static Slider Slider(VisualElement parent, string label, float value, float min, float max,
                                    Action<float> onChange, string tooltip = null, string format = "0.##")
        {
            var host = Row(parent, label, tooltip);
            var s = new Slider(min, max) { value = value };
            s.style.width = 180f;
            Skin.When<Slider>(s, Skin.Slider);

            // Typed, not just dragged: a slider cannot hit an exact value and some of these want one.
            var field = Number(host, value.ToString(format), typed =>
            {
                float v;
                if (!float.TryParse(typed, out v)) return false;
                s.value = Mathf.Clamp(v, min, max);
                return true;
            });
            host.Insert(0, s);

            var set = Track(label, value, v =>
            {
                field.SetValueWithoutNotify(v.ToString(format));
                s.SetValueWithoutNotify(v);
                try { if (onChange != null) onChange(v); } catch (Exception ex) { Ui.Log("Slider handler threw: " + ex.Message); }
            });
            s.RegisterValueChangedCallback(e => set(e.newValue));
            return s;
        }

        /* The number beside a slider, editable. Commits on Enter or focus loss and puts the old
           text back when what was typed is not a number — silently keeping a bad value is worse
           than refusing it. */
        private static TextField Number(VisualElement parent, string initial, Func<string, bool> commit)
        {
            var f = new TextField { value = initial };
            f.style.width = 62f;
            f.style.marginLeft = Tokens.Gap;
            f.style.fontSize = Tokens.FontSizeSmall;
            Skin.When<TextField>(f, Skin.Field);
            Cursors.Set(f, Cursors.Kind.Text);
            Action apply = () => { if (!commit(f.value)) f.SetValueWithoutNotify(initial); };
            f.RegisterCallback<BlurEvent>(_ => apply());
            f.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) apply();
            });
            parent.Add(f);
            return f;
        }

        public static SliderInt IntSlider(VisualElement parent, string label, int value, int min, int max,
                                          Action<int> onChange, string tooltip = null)
        {
            var host = Row(parent, label, tooltip);
            var s = new SliderInt(min, max) { value = value };
            s.style.width = 180f;
            Skin.When<SliderInt>(s, Skin.Slider);

            var field = Number(host, value.ToString(), typed =>
            {
                int v;
                if (!int.TryParse(typed, out v)) return false;
                s.value = Mathf.Clamp(v, min, max);
                return true;
            });
            host.Insert(0, s);

            var set = Track(label, value, v =>
            {
                field.SetValueWithoutNotify(v.ToString());
                s.SetValueWithoutNotify(v);
                try { if (onChange != null) onChange(v); } catch (Exception ex) { Ui.Log("IntSlider handler threw: " + ex.Message); }
            });
            s.RegisterValueChangedCallback(e => set(e.newValue));
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

            var set = Track(label, index, v =>
            {
                current = v;
                for (int j = 0; j < buttons.Count; j++) Ui.Highlight(buttons[j], j == current);
                if (v >= 0 && v < buttons.Count) Anim.Pop(buttons[v]);
                try { if (onChange != null) onChange(v); } catch (Exception e) { Ui.Log("Choice handler threw: " + e.Message); }
            });
            for (int i = 0; i < (options != null ? options.Count : 0); i++)
            {
                int idx = i;
                buttons.Add(Ui.Btn(options[i], () => { if (idx != current) set(idx); }, host));
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
            Skin.When<TextField>(f, Skin.Field);
            f.style.fontSize = Tokens.FontSizeSmall;
            // On commit, not per keystroke: a setter that persists to disk should not run once per
            // character typed.
            var set = Track(label, value ?? "", v =>
            {
                f.SetValueWithoutNotify(v);
                try { if (onChange != null) onChange(v); } catch (Exception e) { Ui.Log("Text handler threw: " + e.Message); }
            });
            f.RegisterCallback<BlurEvent>(_ => set(f.value));
            f.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) set(f.value); });
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

        /* Colour. The picker itself is ColourPicker — Bismuth's model, the best of the three the
           mods had — and this is just the row that hosts it. */
        public static VisualElement Colour(VisualElement parent, string label, Color value,
                                           Action<Color> onChange, string tooltip = null,
                                           bool hasAlpha = false)
        {
            var p = new ColourPicker(parent, label, value, hasAlpha, Track(label, value, c =>
            {
                try { if (onChange != null) onChange(c); } catch (Exception e) { Ui.Log("Colour handler threw: " + e.Message); }
            }));
            if (!string.IsNullOrEmpty(tooltip)) p.Root.tooltip = tooltip;
            return p.Root;
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
            SearchIndex.CurrentGroup = title;
            bool open = _folded.ContainsKey(title) ? _folded[title] : defaultOpen;

            var head = Ui.Row(parent);
            head.style.marginTop = Tokens.Pad;
            head.pickingMode = PickingMode.Position;
            var arrow = Ui.Muted(open ? "▾" : "▸", head);
            arrow.style.width = 18f;
            arrow.style.fontSize = Tokens.FontSize + 3f;   // it was a speck at body size
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

        /* A list entry: a name, an optional note under it, and buttons on the right.

           Profiles, font packs and the key viewer's rows are all this shape, and each of them had
           grown its own layout. Not a settings ROW — those are label-plus-one-control — this is one
           THING with actions, which is why it gets a taller line and a second text style. */
        public static VisualElement Item(VisualElement parent, string title, string note = null)
        {
            var row = Ui.Row(parent);
            row.style.minHeight = 38f;
            row.style.paddingLeft = 8f;
            row.style.paddingRight = 4f;
            row.style.marginBottom = 2f;
            Ui.SetRadius(row, Tokens.Radius);
            row.style.backgroundColor = Tokens.Row;

            var text = Ui.Box(row);
            text.style.flexGrow = 1f;
            var t = Ui.Text(title, text);
            t.pickingMode = PickingMode.Ignore;
            if (!string.IsNullOrEmpty(note))
            {
                var n = Ui.Muted(note, text);
                n.pickingMode = PickingMode.Ignore;
            }

            var actions = Ui.Row(row);
            actions.style.flexShrink = 0f;
            actions.name = "actions";
            return actions;
        }

        /// Marks a list entry as the active one, the way a profile list shows which is loaded.
        public static void MarkActive(VisualElement actions, string label = "Active")
        {
            var tag = Ui.Text(label, null, Tokens.FontSizeSmall, Tokens.Accent);
            tag.style.marginRight = Tokens.Gap;
            actions.Insert(0, tag);
        }

        /* A destructive button that is its own label. "All positions [Reset]" reads as a setting
           called "All positions"; "[Reset all positions]" reads as the action it is. */
        public static Button DangerButton(VisualElement parent, string text, Action onConfirm,
                                          string tooltip = null)
        {
            var row = Ui.Row(parent);
            row.style.minHeight = RowHeight;
            row.style.marginBottom = 2f;
            if (!string.IsNullOrEmpty(tooltip)) row.tooltip = tooltip;
            SearchIndex.Note(text);

            Button b = null;
            bool armed = false;
            b = Ui.Btn(text, () =>
            {
                if (!armed) { armed = true; b.text = "Sure?"; b.style.backgroundColor = Tokens.Danger; Anim.Pop(b); return; }
                armed = false;
                b.text = text;
                b.style.backgroundColor = Tokens.RowAlt;
                try { if (onConfirm != null) onConfirm(); } catch (Exception e) { Ui.Log("Danger handler threw: " + e.Message); }
            }, row);
            b.style.color = Tokens.Danger;
            return b;
        }

        /* A row that opens a page of its own. This is what the mods' menus use for anything with
           more than one setting behind it — a stat's colours, a key's layout — and it belongs to
           the row rather than to the rail, which is why it pushes instead of nesting. */
        public static VisualElement SubPage(VisualElement parent, string label,
                                            Action<VisualElement> build, string tooltip = null)
        {
            var host = Row(parent, label, tooltip);
            var arrow = Ui.Text("\u203A", host, Tokens.FontSize, Tokens.TextMuted);
            arrow.pickingMode = PickingMode.Ignore;

            var row = host.parent;
            row.pickingMode = PickingMode.Position;
            row.RegisterCallback<ClickEvent>(_ =>
            {
                var nav = Nav.Active;
                if (nav != null) nav.Push(label, build);
                else Ui.Log("SubPage '" + label + "': no Nav is building, nothing to push onto");
            });
            return host;
        }

        /* A switch AND a page behind it, on one row.

           The alternative — a toggle row followed by an "X options" row — says the thing's name
           twice and doubles the length of every list it appears in. The arrow marks the row as
           having more behind it; the switch still switches, because a click that lands on it must
           not navigate away. */
        public static VisualElement ToggleSubPage(VisualElement parent, string label, bool value,
                                                  Action<bool> onChange, Action<VisualElement> build,
                                                  string tooltip = null)
        {
            var host = Row(parent, label, tooltip);
            var row = host.parent;

            var track = BuildSwitch(host, label, value, onChange);
            track.pickingMode = PickingMode.Position;

            var arrow = Ui.Text("\u203A", host, Tokens.FontSize, Tokens.TextMuted);
            arrow.style.marginLeft = Tokens.Gap;
            arrow.pickingMode = PickingMode.Ignore;

            row.pickingMode = PickingMode.Position;
            row.RegisterCallback<ClickEvent>(e =>
            {
                // The switch handles its own clicks and stops them here; anything else opens.
                var nav = Nav.Active;
                if (nav != null && build != null) nav.Push(label, build);
            });
            return row;
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
