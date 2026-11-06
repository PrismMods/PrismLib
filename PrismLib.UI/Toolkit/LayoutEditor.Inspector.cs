using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* The selected target's exact values, and undo/redo. Fields are plain TextFields, not
       Widgets.Text: that one records History keyed `host/page/label#n`, which would undo into whichever
       target happens to be selected later. Every gesture (drag, grip, knob, crop edge, field
       commit) records exactly one History step, keyed per gesture so two quick gestures on one
       target never coalesce into one. */
    public sealed partial class LayoutEditor
    {
        private VisualElement _inspector, _inspectorBody;
        private TextField _fx, _fy, _fsize, _frot;
        private VisualElement[] _anchorCells;
        private Button _closestBtn;
        private Snapshot _before;
        private int _gesture;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Snapshot
        {
            public float Cx, Cy, Scale, Rot;
            public int Anchor;
            public bool Closest;
            public float[] Crop;

            public bool Same(Snapshot o) =>
                o != null && Cx == o.Cx && Cy == o.Cy && Scale == o.Scale && Rot == o.Rot && Anchor == o.Anchor
                && Closest == o.Closest && (Crop == null ? o.Crop == null : o.Crop != null && Crop[0] == o.Crop[0]
                && Crop[1] == o.Crop[1] && Crop[2] == o.Crop[2] && Crop[3] == o.Crop[3]);
        }

        private Snapshot Snap(Frame f)
        {
            var b = Guard(f, () => f.T.GetBox(), null);
            if (b == null) return null;
            return new Snapshot
            {
                Cx = b[0], Cy = b[1],
                Scale = Guard(f, () => f.T.GetScale(), 1f),
                Rot = f.T.GetRotation != null ? Guard(f, () => f.T.GetRotation(), 0f) : 0f,
                Anchor = f.T.GetAnchor != null ? Guard(f, () => f.T.GetAnchor(), 4) : 4,
                Closest = f.T.GetAnchorClosest != null && Guard(f, () => f.T.GetAnchorClosest(), false),
                Crop = f.T.GetCrop != null ? Guard(f, () => (float[])f.T.GetCrop().Clone(), null) : null,
            };
        }

        // Anchor first (its setter keeps the on-screen spot), position last: size, rotation and
        // crop setters may re-derive the position from the anchor.
        private void Restore(Frame f, Snapshot s)
        {
            if (s == null || f.Broken) return;
            Guard(f, () =>
            {
                if (f.T.SetAnchorClosest != null) f.T.SetAnchorClosest(s.Closest);
                if (f.T.SetAnchor != null) f.T.SetAnchor(s.Anchor);
                f.T.SetScale(s.Scale);
                if (f.T.SetRotation != null) f.T.SetRotation(s.Rot);
                if (f.T.SetCrop != null && s.Crop != null) f.T.SetCrop((float[])s.Crop.Clone());
                f.T.MoveTo(s.Cx, s.Cy);
                return true;
            }, false);
        }

        partial void BeginGesture(Frame f) { _before = Snap(f); }

        partial void EndGesture(Frame f)
        {
            var before = _before;
            _before = null;
            var after = Snap(f);
            if (before == null || after == null || before.Same(after)) return;
            string label = "Arrange " + (string.IsNullOrEmpty(f.T.Label) ? f.T.Id : f.T.Label);
            History.Record(label, f.T.Key + "#" + (++_gesture), () => Restore(f, before), () => Restore(f, after));
        }

        // ── Panel ───────────────────────────────────────────────────────────

        partial void BuildInspector()
        {
            _inspector = Ui.Card(_layer);
            _inspector.pickingMode = PickingMode.Position;
            _inspector.style.position = Position.Absolute;
            _inspector.style.top = 80f;
            _inspector.style.width = 250f;
            _inspector.style.display = DisplayStyle.None;
            _inspectorBody = Ui.Box(_inspector);

            var undoRow = Ui.Row(_inspector);
            undoRow.style.marginTop = _gap;
            Ui.Btn(Words.Own("Undo"), () => History.Undo(), undoRow).name = "layout-undo";
            Ui.Btn(Words.Own("Redo"), () => History.Redo(), undoRow).name = "layout-redo";
        }

        partial void SelectionChangedExtras()
        {
            _cropMode = false;
            if (_inspectorBody == null) return;
            _inspectorBody.Clear();
            _fx = _fy = _fsize = _frot = null;
            _anchorCells = null; _closestBtn = null;
            var f = _selected;
            _inspector.style.display = f == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (f == null) return;

            Ui.Text(string.IsNullOrEmpty(f.T.Label) ? f.T.Id : f.T.Label, _inspectorBody, _body, _th.Text);
            if (!string.IsNullOrEmpty(f.T.Owner)) Ui.Muted(f.T.Owner, _inspectorBody);
            _fx = Field("X", v => { var s = Snap(f); if (s != null) f.T.MoveTo(v, s.Cy); });
            _fy = Field("Y", v => { var s = Snap(f); if (s != null) f.T.MoveTo(s.Cx, v); });
            _fsize = Field("Size", v => f.T.SetScale(Mathf.Clamp(v, f.T.MinScale, f.T.MaxScale)));
            if (f.T.SetRotation != null) _frot = Field("Rotation", v => f.T.SetRotation(LayoutMath.SnapRotation(v)));
            if (f.T.SetAnchor != null) BuildAnchorGrid(f);
            BuildCropRows(f);
        }

        partial void BuildCropRows(Frame f);   // LayoutEditor.Crop.cs

        private TextField Field(string label, Action<float> apply)
        {
            var row = Ui.Row(_inspectorBody);
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginTop = 4f;
            Ui.Muted(label, row);
            var tf = new TextField { name = "layout-" + label.ToLowerInvariant().Replace(' ', '-').Replace("%", "pct") };
            tf.style.width = 90f;
            Skin.When<TextField>(tf, Skin.Field);
            tf.style.fontSize = _small;
            // Commit only what was typed: the shown text is rounded, so committing it on every blur
            // would nudge the value and add an undo step for nothing. Escape puts the text back.
            string shown = null;
            bool cancelled = false;
            Action commit = () =>
            {
                var f = _selected;
                float v;
                if (f == null || tf.value == shown || !float.TryParse(tf.value, NumberStyles.Float, Inv, out v)) return;
                shown = tf.value;
                BeginGesture(f);
                Guard(f, () => { apply(v); return true; }, false);
                EndGesture(f);
            };
            tf.RegisterCallback<FocusInEvent>(_ => { shown = tf.value; cancelled = false; });
            // Ui.HandleEscape blurs the field itself, possibly before the Escape key event reaches
            // it, so a blur while Escape is down is a cancel too.
            tf.RegisterCallback<BlurEvent>(_ =>
            {
                if (!cancelled && !InputGate.Exempt(() => KeyHeld(KeyCode.Escape))) commit();
                else if (shown != null) tf.SetValueWithoutNotify(shown);
                cancelled = false;
            });
            tf.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) commit();
                else if (e.keyCode == KeyCode.Escape) { cancelled = true; if (shown != null) tf.SetValueWithoutNotify(shown); }
            });
            row.Add(tf);
            return tf;
        }

        private void BuildAnchorGrid(Frame f)
        {
            var row = Ui.Row(_inspectorBody);
            row.style.marginTop = 6f;
            row.style.justifyContent = Justify.SpaceBetween;
            Ui.Muted("Anchor", row);
            // Three explicit rows: a wrapping row of 9 cells relied on the cells' exact outer width
            // (the button skin's border made it 2 columns).
            var grid = new VisualElement();
            grid.style.flexShrink = 0f;
            row.Add(grid);
            VisualElement line = null;
            _anchorCells = new VisualElement[9];
            for (int i = 0; i < 9; i++)
            {
                int a = i;
                var cell = new Button(() =>
                {
                    BeginGesture(f);
                    Guard(f, () => { f.T.SetAnchorClosest?.Invoke(false); f.T.SetAnchor(a); return true; }, false);
                    EndGesture(f);
                }) { name = "layout-anchor-" + i, text = "" };
                cell.style.width = 14f; cell.style.height = 14f;
                cell.style.marginLeft = 2f; cell.style.marginRight = 2f; cell.style.marginTop = 2f; cell.style.marginBottom = 2f;
                Ui.SetRadius(cell, 2f);
                if (i % 3 == 0) { line = new VisualElement(); line.style.flexDirection = FlexDirection.Row; grid.Add(line); }
                line.Add(cell);
                _anchorCells[i] = cell;
            }
            if (f.T.SetAnchorClosest != null)
            {
                _closestBtn = Ui.Btn("Closest", () =>
                {
                    BeginGesture(f);
                    Guard(f, () => { f.T.SetAnchorClosest(!f.T.GetAnchorClosest()); return true; }, false);
                    EndGesture(f);
                }, _inspectorBody);
                _closestBtn.name = "layout-closest";
            }
        }

        partial void TickInspector(float k)
        {
            var f = _selected;
            if (_inspector != null && _visible) TickUndoKeys();
            if (_inspector == null || f == null || f.Broken || f.B == null)
            {
                if (_inspector != null) _inspector.style.display = DisplayStyle.None;
                return;
            }
            _inspector.style.display = DisplayStyle.Flex;
            // Docked on the side away from the selected box.
            bool right = f.B[0] < Screen.width / 2f;
            _inspector.style.left = right ? StyleKeyword.Auto : (StyleLength)24f;
            _inspector.style.right = right ? (StyleLength)24f : StyleKeyword.Auto;

            Show(_fx, f.B[0]); Show(_fy, f.B[1]);
            Show(_fsize, Guard(f, () => f.T.GetScale(), 1f));
            if (_frot != null) Show(_frot, Guard(f, () => f.T.GetRotation(), 0f));
            if (_anchorCells != null)
            {
                int a = Guard(f, () => f.T.GetAnchor(), 4);
                for (int i = 0; i < 9; i++) _anchorCells[i].style.backgroundColor = i == a ? _th.Accent : _th.RowHover;
            }
            if (_closestBtn != null) Ui.Highlight(_closestBtn, Guard(f, () => f.T.GetAnchorClosest(), false));
        }

        // Live values, except in the field being typed into.
        private static void Show(TextField tf, float v)
        {
            if (tf == null || tf.focusController?.focusedElement == tf) return;
            tf.SetValueWithoutNotify(v.ToString(Math.Abs(v) >= 100f ? "0" : "0.##", Inv));
        }

        // Cmd/Ctrl+Z undoes, with Shift redoes, while the editor is open.
        // The key reads go through these so LayoutProbe's autotest can drive and observe them.
        internal static Func<KeyCode, bool> KeyHeld = Input.GetKey;
        internal static Func<KeyCode, bool> KeyPressed = Input.GetKeyDown;

        // Exempt: the editor's own surface raises the input gate, which would otherwise read
        // every key as up and leave Ctrl+Z dead.
        private void TickUndoKeys()
        {
            bool mod = false, z = false, shift = false;
            InputGate.Exempt(() =>
            {
                mod = KeyHeld(KeyCode.LeftCommand) || KeyHeld(KeyCode.RightCommand)
                      || KeyHeld(KeyCode.LeftControl) || KeyHeld(KeyCode.RightControl);
                z = mod && KeyPressed(KeyCode.Z);
                shift = z && (KeyHeld(KeyCode.LeftShift) || KeyHeld(KeyCode.RightShift));
            });
            if (!z) return;
            if (shift) History.Redo();
            else History.Undo();
        }
    }
}
