using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /// One stop. The host keeps its own model and maps to this — PrismLib.UI knows nothing about
    /// Topaz's ColorStop or anyone else's.
    public sealed class GradientStop
    {
        public float Position;
        public Color Colour = Color.white;
    }

    /* The gradient editor: Facet's, over the caller's own stops.

       A baked strip with a draggable marker per stop, and the selected stop's colour and position
       edited below it; clicking empty strip adds a stop there. Facet's editor edits Facet stops in
       place, so it works on Stops, a list that mirrors the caller's (see there). */
    public sealed class GradientEditor
    {
        private readonly Facet.GradientEditor _f;

        public VisualElement Root { get; private set; }

        public GradientEditor(VisualElement parent, string label, IList<GradientStop> stops,
                              Action onChange, bool hasAlpha = true)
        {
            var mirror = new Stops(stops ?? new List<GradientStop>());
            // Settled once more before the caller hears of a change, whatever Facet read last.
            Action changed = () => { mirror.Settle(); if (onChange != null) onChange(); };
            using (PrismHost.Use()) _f = new Facet.GradientEditor(parent, label, mirror, changed, hasAlpha);
            Root = _f.Root;
        }

        /// Clamp outside the ends, lerp between the two stops either side.
        public Color Evaluate(float t) => _f.Evaluate(t);

        private sealed class Pair
        {
            public GradientStop P;
            public Facet.GradientStop F;
            public float Pos;       // both sides as of the last settle
            public Color Col;
        }

        /* One Facet stop per caller stop, for the editor's life. Every read settles each pair: a field
           the editor changed is written to the caller's stop, one the caller changed is read from it
           (the editor's wins if both moved). Which stops exist and their order is the caller's list's;
           the editor's adds, removes and re-sorts are made on it as they happen. */
        private sealed class Stops : IList<Facet.GradientStop>
        {
            private readonly IList<GradientStop> _p;
            private readonly List<Pair> _pairs = new List<Pair>();   // in the caller's order
            private readonly Dictionary<GradientStop, Pair> _byP = new Dictionary<GradientStop, Pair>();
            private readonly Dictionary<Facet.GradientStop, Pair> _byF = new Dictionary<Facet.GradientStop, Pair>();

            public Stops(IList<GradientStop> p) { _p = p; Settle(); }

            private Pair Of(GradientStop p)
            {
                Pair x;
                if (_byP.TryGetValue(p, out x)) return x;
                x = new Pair { P = p, F = new Facet.GradientStop { Position = p.Position, Colour = p.Colour }, Pos = p.Position, Col = p.Colour };
                _byP[p] = x; _byF[x.F] = x;
                return x;
            }

            private Pair Of(Facet.GradientStop f)
            {
                Pair x;
                if (_byF.TryGetValue(f, out x)) return x;
                x = new Pair { F = f, P = new GradientStop { Position = f.Position, Colour = f.Colour }, Pos = f.Position, Col = f.Colour };
                _byP[x.P] = x; _byF[f] = x;
                return x;
            }

            internal void Settle()
            {
                bool same = _pairs.Count == _p.Count;
                for (int i = 0; same && i < _pairs.Count; i++) same = _pairs[i].P == _p[i];
                if (!same) { _pairs.Clear(); foreach (var p in _p) _pairs.Add(Of(p)); }
                foreach (var x in _pairs)
                {
                    if (x.F.Position != x.Pos || x.F.Colour != x.Col) { x.P.Position = x.F.Position; x.P.Colour = x.F.Colour; }
                    else if (x.P.Position != x.Pos || x.P.Colour != x.Col) { x.F.Position = x.P.Position; x.F.Colour = x.P.Colour; }
                    x.Pos = x.F.Position; x.Col = x.F.Colour;
                }
            }

            // Every reader asks Count first (Evaluate, the List copy in Sort), so the indexer need not settle.
            public int Count { get { Settle(); return _pairs.Count; } }
            public bool IsReadOnly => false;

            public Facet.GradientStop this[int i]
            {
                get { return _pairs[i].F; }
                set { var x = Of(value); _p[i] = x.P; _pairs[i] = x; }
            }

            public void Add(Facet.GradientStop f) { var x = Of(f); _p.Add(x.P); _pairs.Add(x); }
            public void Insert(int i, Facet.GradientStop f) { var x = Of(f); _p.Insert(i, x.P); _pairs.Insert(i, x); }
            public void RemoveAt(int i) { _p.RemoveAt(i); _pairs.RemoveAt(i); }
            public void Clear() { _p.Clear(); _pairs.Clear(); }

            public bool Remove(Facet.GradientStop f)
            {
                int i = IndexOf(f);
                if (i < 0) return false;
                RemoveAt(i);
                return true;
            }

            public int IndexOf(Facet.GradientStop f)
            {
                Settle();
                for (int i = 0; i < _pairs.Count; i++) if (_pairs[i].F == f) return i;
                return -1;
            }

            public bool Contains(Facet.GradientStop f) => IndexOf(f) >= 0;

            public void CopyTo(Facet.GradientStop[] a, int at)
            {
                Settle();
                for (int i = 0; i < _pairs.Count; i++) a[at + i] = _pairs[i].F;
            }

            public IEnumerator<Facet.GradientStop> GetEnumerator()
            {
                Settle();
                for (int i = 0; i < _pairs.Count; i++) yield return _pairs[i].F;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
