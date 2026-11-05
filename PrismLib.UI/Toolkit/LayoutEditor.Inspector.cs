using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* The selected target's exact values, and undo/redo. Fields are plain TextFields, not
       Widgets.Text: that one records History keyed by its label, which would undo into whichever
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
            undoRow.style.marginTop = Tokens.Gap;
            Ui.Btn("Undo", () => History.Undo(), undoRow).name = "layout-undo";
            Ui.Btn("Redo", () => History.Redo(), undoRow).name = "layout-redo";
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

            Ui.Text(string.IsNullOrEmpty(f.T.Label) ? f.T.Id : f.T.Label, _inspectorBody, Tokens.FontSize, Tokens.Text);
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
            tf.style.fontSize = Tokens.FontSizeSmall;
            Action commit = () =>
            {
                var f = _selected;
                float v;
                if (f == null || !float.TryParse(tf.value, NumberStyles.Float, Inv, out v)) return;
                BeginGesture(f);
                Guard(f, () => { apply(v); return true; }, false);
                EndGesture(f);
            };
            tf.RegisterCallback<BlurEvent>(_ => commit());
            tf.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) commit(); });
            row.Add(tf);
            return tf;
        }

        private void BuildAnchorGrid(Frame f)
        {
            var row = Ui.Row(_inspectorBody);
            row.style.marginTop = 6f;
            row.style.justifyContent = Justify.SpaceBetween;
            Ui.Muted("Anchor", row);
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.width = 3 * 18f;
            row.Add(grid);
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
                grid.Add(cell);
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
                for (int i = 0; i < 9; i++) _anchorCells[i].style.backgroundColor = i == a ? Tokens.Accent : Tokens.RowHover;
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
        private void TickUndoKeys()
        {
            bool mod = Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand)
                       || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (!mod || !Input.GetKeyDown(KeyCode.Z)) return;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) History.Redo();
            else History.Undo();
        }
    }
}
