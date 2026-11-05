using System;
using System.Collections.Generic;

namespace PrismLib
{
    /* Shared matching and ranking.

       Three mods each grew their own settings search, and each one was a different `Contains` —
       which means the same query finds a setting in one mod and misses it in another. This is the
       one implementation, and it lives in PrismLib.dll rather than PrismLib.UI because it is pure
       logic: no Unity, testable offline, and usable by the settings registry and the dashboard as
       well as by anything that draws.

       The matcher is Quartz's (its settings search was the most worked-over of the three):

         - Every query WORD has to land somewhere — the label, the section or page it sits in, or
           its tooltip — so "key rain" finds "Note Rain" on the Key Viewer page. A word that lands
           only in context counts half; tooltip text is a last resort.
         - Spaces and punctuation do not count: "keyviewer" finds "Key Viewer".
         - A word answers to its aliases ("kv", "colour", "judgment", "size"/"scale"…).
         - Typos are a SECOND pass, run only when the exact one finds nothing, so a typo match
           never sits among real ones. That is a list-level decision, which is why only `Rank` (and
           a caller doing its own two passes with `Rate`) gets typos — a per-item filter cannot know
           whether anything else matched.

       The older per-string API keeps its contract — lower is better, `NoMatch` is -1, an exact
       label is 0 — and keeps the subsequence fallback it had, so an abbreviation like "efbpm"
       still finds "Effective BPM" when nothing matches properly. */
    public static class Search
    {
        public const int NoMatch = -1;

        /* Lower-is-better bands for the per-string API. Real matches sit under RealCeiling and a
           subsequence at Subsequence or above, so callers that add their own offsets for weaker
           fields (SearchIndex adds 500 for a group hit and 800 for a page hit) still rank a real
           hit anywhere above a scattered one. */
        private const int RealCeiling = 500;
        private const int Subsequence = 2000;

        /// One searchable string, normalised once: whole with spaces and punctuation dropped
        /// ("Key Viewer" → "keyviewer"), and per word. Build these once per index, not per query.
        public sealed class Text
        {
            public readonly string Joined;
            public readonly string[] Words;

            public Text(string raw)
            {
                raw = raw ?? "";
                Joined = Normalize(raw);
                var words = new List<string>();
                int start = -1;
                for (int i = 0; i <= raw.Length; i++)
                {
                    bool inWord = i < raw.Length && char.IsLetterOrDigit(raw[i]);
                    if (inWord && start < 0) start = i;
                    else if (!inWord && start >= 0)
                    {
                        string w = Normalize(raw.Substring(start, i - start));
                        if (w.Length > 0) words.Add(w);
                        start = -1;
                    }
                }
                Words = words.ToArray();
            }
        }

        /// A query parsed once: each word with its aliases, and the whole query joined.
        public sealed class Query
        {
            public readonly string Raw;
            public readonly string Phrase;
            public readonly string[][] Tokens;

            public Query(string raw)
            {
                Raw = raw ?? "";
                Phrase = Normalize(Raw);
                string[] words = new Text(Raw).Words;
                Tokens = new string[words.Length][];
                for (int i = 0; i < words.Length; i++)
                {
                    string[] alt;
                    if (Aliases.TryGetValue(words[i], out alt))
                    {
                        var all = new string[alt.Length + 1];
                        all[0] = words[i];
                        Array.Copy(alt, 0, all, 1, alt.Length);
                        Tokens[i] = all;
                    }
                    else Tokens[i] = new[] { words[i] };
                }
            }

            /// No letters or digits at all ("", "  ", "->").
            public bool IsEmpty { get { return Tokens.Length == 0; } }
        }

        // What people type for what the menus call it. Normalised keys; a query word also answers to these.
        private static readonly Dictionary<string, string[]> Aliases = new Dictionary<string, string[]>
        {
            { "kv", new[] { "keyviewer" } },
            { "colour", new[] { "color" } },
            { "judgment", new[] { "judgement" } },
            { "size", new[] { "scale" } },
            { "scale", new[] { "size" } },
            { "transparency", new[] { "opacity" } },
            { "alpha", new[] { "opacity" } },
        };

        /// Letters and digits only, lower case.
        public static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var c = new char[s.Length];
            int n = 0;
            foreach (char ch in s) if (char.IsLetterOrDigit(ch)) c[n++] = char.ToLowerInvariant(ch);
            return new string(c, 0, n);
        }

        /* Higher is better, 0 is no match. `label` is what the item IS (several texts allowed — a
           translated label and its English, say), `context` is where it lives (section, page,
           owner, keywords) and counts half, `detail` is a tooltip and only ever scores 10. Pass
           `typos` only on a second pass after an exact one found nothing. */
        public static int Rate(Query q, Text[] label, Text[] context, Text detail, bool typos)
        {
            if (q == null || q.Tokens.Length == 0) return 0;
            int total = 0;
            foreach (string[] alts in q.Tokens)
            {
                int best = 0;
                foreach (string tok in alts)
                {
                    best = Math.Max(best, Best(tok, label, typos));
                    best = Math.Max(best, Best(tok, context, typos) / 2);
                    if (detail != null && tok.Length >= 3 && detail.Joined.Contains(tok)) best = Math.Max(best, 10);
                }
                if (best == 0) return 0;
                total += best;
            }
            int bonus = 0;
            if (q.Phrase.Length > 0 && label != null)
                foreach (Text l in label)
                {
                    if (l == null) continue;
                    if (l.Joined == q.Phrase) bonus = Math.Max(bonus, 200);
                    else if (l.Joined.StartsWith(q.Phrase, StringComparison.Ordinal)) bonus = Math.Max(bonus, 100);
                    else if (l.Joined.Contains(q.Phrase)) bonus = Math.Max(bonus, 50);
                }
            return total + bonus;
        }

        private static int Best(string tok, Text[] texts, bool typos)
        {
            int best = 0;
            if (texts == null) return best;
            foreach (Text t in texts) if (t != null) best = Math.Max(best, Match(tok, t, typos));
            return best;
        }

        private static int Match(string tok, Text t, bool typos)
        {
            int best = 0;
            foreach (string w in t.Words)
            {
                if (w == tok) return 100;
                if (w.StartsWith(tok, StringComparison.Ordinal)) best = 80;
            }
            if (best == 0 && t.Joined.Contains(tok)) best = 60;
            if (best == 0 && typos && WithinTypos(tok, t.Joined)) best = 30;
            return best;
        }

        // None under 4 letters (short words are all one edit apart), then 1, then 2.
        private static int AllowedTypos(int length) { return length < 4 ? 0 : length < 7 ? 1 : 2; }

        // Some stretch of `text` is within the allowed edits of `tok` (insert, delete, substitute, swap neighbours).
        private static bool WithinTypos(string tok, string text)
        {
            int k = AllowedTypos(tok.Length);
            if (k == 0 || text.Length == 0) return false;
            int n = text.Length;
            // Row 0 is all zeros, so the match may start anywhere in text.
            int[] prev2 = new int[n + 1], prev = new int[n + 1], cur = new int[n + 1];
            for (int i = 1; i <= tok.Length; i++)
            {
                cur[0] = i;
                for (int j = 1; j <= n; j++)
                {
                    int v = Math.Min(Math.Min(prev[j], cur[j - 1]) + 1, prev[j - 1] + (tok[i - 1] == text[j - 1] ? 0 : 1));
                    if (i > 1 && j > 1 && tok[i - 1] == text[j - 2] && tok[i - 2] == text[j - 1])
                        v = Math.Min(v, prev2[j - 2] + 1);
                    cur[j] = v;
                }
                int[] spare = prev2; prev2 = prev; prev = cur; cur = spare;
            }
            for (int j = 0; j <= n; j++)
                if (prev[j] <= k) return true;
            return false;
        }

        /// True when the query matches at all. An empty query matches everything.
        public static bool Matches(string haystack, string query) { return Score(haystack, query) != NoMatch; }

        /// Lower is better: 0 for an exact label, `NoMatch` for none.
        public static int Score(string haystack, string query)
        {
            if (Blank(query)) return 0;
            if (string.IsNullOrEmpty(haystack)) return NoMatch;
            return ScoreAny(new Query(query), false, new[] { haystack });
        }

        /// The first field is the item's label; the rest are context (id, group, page, keywords)
        /// and count half. Query words may land in different fields. Ignores null entries.
        public static int ScoreAny(string query, params string[] fields)
        {
            if (fields == null) return NoMatch;
            if (Blank(query)) return 0;
            return ScoreAny(new Query(query), false, fields);
        }

        private static int ScoreAny(Query q, bool typos, string[] fields)
        {
            if (q.IsEmpty)
            {
                // Punctuation only ("->"): nothing to split into words, so a plain substring.
                foreach (string f in fields)
                    if (f != null && f.IndexOf(q.Raw.Trim(), StringComparison.OrdinalIgnoreCase) >= 0) return RealCeiling - 1;
                return NoMatch;
            }
            var texts = new Text[fields.Length];
            for (int i = 0; i < fields.Length; i++) texts[i] = fields[i] == null ? null : new Text(fields[i]);
            Text[] label = { texts.Length > 0 ? texts[0] : null };
            var context = new Text[Math.Max(0, texts.Length - 1)];
            Array.Copy(texts, 1, context, 0, context.Length);

            if (label[0] != null && label[0].Joined.Length > 0 && label[0].Joined == q.Phrase) return 0;
            int rate = Rate(q, label, context, null, typos);
            if (rate > 0) return Math.Max(1, RealCeiling - rate);

            int best = NoMatch;
            foreach (Text t in texts)
            {
                if (t == null) continue;
                int s = Scatter(t.Joined, q.Phrase);
                if (s >= 0 && (best == NoMatch || Subsequence + s < best)) best = Subsequence + s;
            }
            return best;
        }

        /* Every query character in order. Cost grows with how scattered it is, so "efbpm" over
           "Effective BPM" beats the same letters strewn across a long sentence. -1 when absent. */
        private static int Scatter(string h, string q)
        {
            if (q.Length == 0) return -1;
            int at = 0, gaps = 0, runs = 0;
            bool inRun = false;
            for (int i = 0; i < q.Length; i++)
            {
                int found = h.IndexOf(q[i], at);
                if (found < 0) return -1;
                if (found != at || !inRun) { if (inRun) runs++; inRun = true; gaps += found - at; }
                at = found + 1;
            }
            return gaps + runs * 5;
        }

        /// Matching items, best first. A stable sort: equal scores keep their original order, so a
        /// list the caller already ordered deliberately is not shuffled by an empty query. When
        /// nothing matches exactly, a second pass allows typos.
        public static List<T> Rank<T>(IEnumerable<T> items, string query, Func<T, string[]> fields)
        {
            if (items == null || fields == null) return new List<T>();
            var all = new List<T>(items);
            Query q = Blank(query) ? null : new Query(query);
            var outp = RankPass(all, q, fields, false);
            if (outp.Count == 0 && q != null && !q.IsEmpty) outp = RankPass(all, q, fields, true);
            return outp;
        }

        private static List<T> RankPass<T>(List<T> items, Query q, Func<T, string[]> fields, bool typos)
        {
            var scored = new List<KeyValuePair<int, T>>();
            foreach (var it in items)
            {
                string[] f = fields(it);
                int s = q == null ? (f == null ? NoMatch : 0) : (f == null ? NoMatch : ScoreAny(q, typos, f));
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

        private static bool Blank(string s) { return s == null || s.Trim().Length == 0; }
    }
}
