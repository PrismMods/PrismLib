using System;
using System.Collections.Generic;
using System.Linq;

namespace PrismLib.UI.Toolkit
{
    /* The key layout editor's arrange maths: align, spread and reading order over boxes. BCL only, so
       tools/PrismLibTest.cs compiles this file directly and checks it offline. A struct, not tuples:
       Mono's mcs cannot pass or return arrays of named tuples. */
    public static class KeyLayoutMath
    {
        public enum Edge { Left, Right, Top, Bottom }

        public struct Box
        {
            public float X, Y, W, H;
            public Box(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }
        }

        /// Moves every box so its chosen edge meets the extreme one among them; sizes kept. Under 2: unchanged.
        public static Box[] Align(IReadOnlyList<Box> boxes, Edge edge)
        {
            var o = boxes.ToArray();
            if (o.Length < 2) return o;
            switch (edge)
            {
                case Edge.Left:   { float v = o.Min(b => b.X);       for (int i = 0; i < o.Length; i++) o[i].X = v; break; }
                case Edge.Right:  { float v = o.Max(b => b.X + b.W); for (int i = 0; i < o.Length; i++) o[i].X = v - o[i].W; break; }
                case Edge.Top:    { float v = o.Min(b => b.Y);       for (int i = 0; i < o.Length; i++) o[i].Y = v; break; }
                case Edge.Bottom: { float v = o.Max(b => b.Y + b.H); for (int i = 0; i < o.Length; i++) o[i].Y = v - o[i].H; break; }
            }
            return o;
        }

        /// Keeps the outermost two along the axis (ties by input order) and makes every gap between
        /// neighbours equal; gaps go negative when the boxes overlap. Under 3: unchanged.
        public static Box[] Spread(IReadOnlyList<Box> boxes, bool horizontal)
        {
            var o = boxes.ToArray();
            if (o.Length < 3) return o;
            var order = Enumerable.Range(0, o.Length).OrderBy(i => horizontal ? o[i].X : o[i].Y).ToArray();
            Func<int, float> pos = i => horizontal ? o[i].X : o[i].Y;
            Func<int, float> size = i => horizontal ? o[i].W : o[i].H;
            int first = order[0], last = order[order.Length - 1];
            float inner = 0;
            for (int k = 1; k < order.Length - 1; k++) inner += size(order[k]);
            float gap = (pos(last) - pos(first) - size(first) - inner) / (order.Length - 1);
            float at = pos(first) + size(first) + gap;
            for (int k = 1; k < order.Length - 1; k++)
            {
                int i = order[k];
                if (horizontal) o[i].X = at; else o[i].Y = at;
                at += size(i) + gap;
            }
            return o;
        }

        /// Indices in reading order: rows top to bottom, each left to right. A box joins the current row
        /// when its centre Y is within half the smaller of its height and the row's first box's height.
        public static int[] ReadingOrder(IReadOnlyList<Box> boxes)
        {
            var byCentre = Enumerable.Range(0, boxes.Count)
                .OrderBy(i => boxes[i].Y + boxes[i].H / 2).ThenBy(i => boxes[i].X + boxes[i].W / 2).ToArray();
            var result = new List<int>(boxes.Count);
            var row = new List<int>();
            foreach (int i in byCentre)
            {
                if (row.Count > 0)
                {
                    Box f = boxes[row[0]], b = boxes[i];
                    if (Math.Abs((b.Y + b.H / 2) - (f.Y + f.H / 2)) > Math.Min(b.H, f.H) / 2)
                    {
                        result.AddRange(row.OrderBy(j => boxes[j].X));
                        row.Clear();
                    }
                }
                row.Add(i);
            }
            result.AddRange(row.OrderBy(j => boxes[j].X));
            return result.ToArray();
        }
    }
}
