using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* Mouse cursors for the parts of a panel that are not obviously draggable.

       UI Toolkit's USS `cursor` keywords are editor-only, and a runtime mod has no cursor assets,
       so these are drawn in code and swapped through UnityEngine.Cursor.SetCursor on enter and
       exit. That is a GLOBAL swap — there is no per-element cursor at runtime — which is why every
       element that sets one must also clear it, and why leaving the window has to restore the
       system arrow rather than whatever was set last.

       Deliberately only the ambiguous ones. A resize edge and a text field look like anything else
       until the cursor says otherwise; a button already looks like a button, and the pointing hand
       is a web convention rather than a desktop one. */
    public static class Cursors
    {
        public enum Kind { Default, ResizeHorizontal, ResizeCorner, Text }

        private static Texture2D _horizontal, _corner, _text;
        private static Kind _current = Kind.Default;

        /// Swap while the pointer is over this element, and restore when it leaves.
        public static void Set(VisualElement e, Kind kind)
        {
            if (e == null) return;
            e.RegisterCallback<MouseEnterEvent>(_ => Apply(kind));
            e.RegisterCallback<MouseLeaveEvent>(_ => Apply(Kind.Default));
            // A panel can be hidden with the pointer still over it, which would otherwise strand
            // the cursor in whatever shape it last took.
            e.RegisterCallback<DetachFromPanelEvent>(_ => Apply(Kind.Default));
        }

        public static void Apply(Kind kind)
        {
            if (kind == _current) return;
            _current = kind;
            try
            {
                switch (kind)
                {
                    case Kind.ResizeHorizontal:
                        UnityEngine.Cursor.SetCursor(Horizontal(), new Vector2(11f, 11f), CursorMode.Auto);
                        break;
                    case Kind.ResizeCorner:
                        UnityEngine.Cursor.SetCursor(Corner(), new Vector2(11f, 11f), CursorMode.Auto);
                        break;
                    case Kind.Text:
                        UnityEngine.Cursor.SetCursor(TextBeam(), new Vector2(11f, 11f), CursorMode.Auto);
                        break;
                    default:
                        UnityEngine.Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                        break;
                }
            }
            catch (Exception e) { Ui.Log("Cursors: " + e.Message); }
        }

        private const int S = 23;   // odd, so the shapes have a true centre to point from

        private static Texture2D Horizontal()
        {
            if (_horizontal != null) return _horizontal;
            _horizontal = Draw((x, y) =>
            {
                int cx = S / 2, cy = S / 2;
                if (y == cy && x >= 4 && x <= S - 5) return true;              // shaft
                int d = Mathf.Abs(y - cy);
                if (d <= 4 && (x == 4 + d || x == S - 5 - d)) return true;     // heads
                return false;
            });
            return _horizontal;
        }

        private static Texture2D Corner()
        {
            if (_corner != null) return _corner;
            _corner = Draw((x, y) =>
            {
                if (x == y && x >= 5 && x <= S - 6) return true;               // shaft, top-left to bottom-right
                for (int i = 0; i <= 4; i++)
                {
                    if (x == 5 && y == 5 + i) return true;                     // upper head
                    if (y == 5 && x == 5 + i) return true;
                    if (x == S - 6 && y == S - 6 - i) return true;             // lower head
                    if (y == S - 6 && x == S - 6 - i) return true;
                }
                return false;
            });
            return _corner;
        }

        private static Texture2D TextBeam()
        {
            if (_text != null) return _text;
            _text = Draw((x, y) =>
            {
                int cx = S / 2;
                if (x == cx && y >= 4 && y <= S - 5) return true;              // stem
                if ((y == 4 || y == S - 5) && Mathf.Abs(x - cx) <= 3) return true;   // serifs
                return false;
            });
            return _text;
        }

        /* White with a black halo, so the cursor stays visible over a dark panel and over the
           bright game behind it — a plain white shape disappears against pale tiles. */
        private static Texture2D Draw(Func<int, int, bool> ink)
        {
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    bool on = ink(x, y);
                    bool near = false;
                    for (int dy = -1; dy <= 1 && !near; dy++)
                        for (int dx = -1; dx <= 1 && !near; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && ny >= 0 && nx < S && ny < S && ink(nx, ny)) near = true;
                        }
                    // Texture rows run bottom-up; the shapes here are symmetric, so no flip needed.
                    px[y * S + x] = on ? Color.white : near ? new Color(0f, 0f, 0f, 0.85f) : Color.clear;
                }
            t.SetPixels(px);
            t.Apply(false, false);
            return t;
        }
    }
}
