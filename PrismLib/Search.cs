using System;
using System.Collections.Generic;

namespace PrismLib
{
    /* Shared matching and ranking.

       Three mods each grew their own settings search, and each one is a different `Contains` —
       which means the same query finds a setting in one mod and misses it in another. This is the
       one implementation, and it lives in PrismLib.dll rather than PrismLib.UI because it is pure
       logic: no Unity, testable offline, and usable by the settings registry and the dashboard as
       well as by anything that draws.

       The matcher is subsequence-based, so "efbpm" finds "Effective BPM", with the ranking doing
       the real work: an exact hit beats a prefix, a prefix beats a word-start run, and a scattered
       subsequence comes last. Deliberately not a fuzzy-distance algorithm — typo correction on a
       list of forty settings costs more than it gives, and a wrong-but-confident reorder is worse
       than no match. */
    public static class Search
    {
        public const int NoMatch = -1;

        /// True when the query matches at all. An empty query matches everything.
        public static bool Matches(string haystack, string query) => Score(haystack, query) != NoMatch;

        /* Lower is better. The bands are wide apart on purpose so a better KIND of match always
           wins regardless of length: callers sort on this and expect "Effective BPM" to come above
           "Buffer Padding Mode" for "bpm", not to depend on a tie-break. */
        public static int Score(string haystack, string query)
        {
            if (string.IsNullOrEmpty(query)) return 0;
            if (string.IsNullOrEmpty(haystack)) return NoMatch;

            string h = haystack.ToLowerInvariant(), q = query.Trim().ToLowerInvariant();
            if (q.Length == 0) return 0;

            if (h == q) return 0;
            int idx = h.IndexOf(q, StringComparison.Ordinal);
            if (idx == 0) return 10;                       // prefix
            if (idx > 0) return IsWordStart(h, idx) ? 100 : 200;   // inside, better at a word start

            // Subsequence: every query character in order. Cost grows with how scattered it is, so
            // "efbpm" over "Effective BPM" beats the same letters strewn across a long sentence.
            int at = 0, gaps = 0, runs = 0;
            bool inRun = false;
            for (int i = 0; i < q.Length; i++)
            {
                int found = h.IndexOf(q[i], at);
                if (found < 0) return NoMatch;
                if (found == at && inRun) { /* still in a run */ }
                else { if (inRun) runs++; inRun = true; gaps += found - at; }
                at = found + 1;
            }
            return 1000 + gaps + runs * 5;
        }

        private static bool IsWordStart(string s, int i)
        {
            if (i <= 0) return true;
            char p = s[i - 1];
            return p == ' ' || p == '.' || p == '_' || p == '-' || p == '/' || p == ':';
        }

        /// Best score across several fields — a label, an id, keywords. Ignores null entries.
        public static int ScoreAny(string query, params string[] fields)
        {
            int best = NoMatch;
            if (fields == null) return best;
            foreach (var f in fields)
            {
                int s = Score(f, query);
                if (s != NoMatch && (best == NoMatch || s < best)) best = s;
            }
            return best;
        }

        /// Matching items, best first. A stable sort: equal scores keep their original order, so a
        /// list the caller already ordered deliberately is not shuffled by an empty query.
        public static List<T> Rank<T>(IEnumerable<T> items, string query, Func<T, string[]> fields)
        {
            var scored = new List<KeyValuePair<int, T>>();
            if (items == null || fields == null) return new List<T>();
            foreach (var it in items)
            {
                int s = ScoreAny(query, fields(it));
                if (s != NoMatch) scored.Add(new KeyValuePair<int, T>(s, it));
            }
            var order = new int[scored.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) =>
            {
                int c = scored[a].Key.CompareTo(scored[b].Key);
                return c != 0 ? c : a.CompareTo(b);     // stable
            });
            var outp = new List<T>(scored.Count);
            foreach (var i in order) outp.Add(scored[i].Value);
            return outp;
        }

        /// Plain-text filter that keeps input order — for logs, where "best match first" would
        /// scramble the one thing a log is good for.
        public static List<string> Filter(IEnumerable<string> lines, string query)
        {
            var outp = new List<string>();
            if (lines == null) return outp;
            foreach (var l in lines) if (Matches(l, query)) outp.Add(l);
            return outp;
        }
    }
}
