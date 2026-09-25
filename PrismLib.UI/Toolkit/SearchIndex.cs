using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* What the search box searches.

       Ported from Bismuth's SettingsSearch: widgets register themselves as they are built, so the
       index is whatever the menu actually contains rather than a list somebody has to remember to
       update. The difference here is that pages are built lazily, so Nav builds each one once into
       a throwaway container at startup purely to harvest its labels — the same thing Bismuth gets
       for free by building every tab up front.

       A hit knows its page, which is what lets a result navigate rather than just report. */
    public static class SearchIndex
    {
        public sealed class Hit
        {
            public string Label;
            public string Page;
            public object PageRef;      // the NavItem, held loosely so this file stays generic
            public string Group;
        }

        private static readonly List<Hit> _all = new List<Hit>();

        /// Page currently being indexed. Null means nothing is recording.
        public static object CurrentPage;
        public static string CurrentPageName;
        public static string CurrentGroup;

        public static void Clear() => _all.Clear();

        public static void BeginPage(object page, string name)
        {
            CurrentPage = page;
            CurrentPageName = name;
            CurrentGroup = null;
        }

        public static void EndPage()
        {
            CurrentPage = null;
            CurrentPageName = null;
            CurrentGroup = null;
        }

        /* Two characters minimum, copied from Bismuth's rule and for its reason: a colour picker's
           R, G and B rows are labels too, and a search that offers "R" as a result is noise. */
        public static void Note(string label)
        {
            if (CurrentPage == null || string.IsNullOrEmpty(label) || label.Trim().Length < 2) return;
            _all.Add(new Hit { Label = label, Page = CurrentPageName, PageRef = CurrentPage, Group = CurrentGroup });
        }

        /// Ranked matches. The matcher is injected for the same reason DebugPanel's is: PrismLib.UI
        /// must not reference PrismLib.dll, where the shared scorer lives.
        public static Func<string, string, int> Scorer;

        public static List<Hit> Find(string query, int limit = 40)
        {
            var outp = new List<Hit>();
            if (string.IsNullOrEmpty(query)) return outp;
            var scored = new List<KeyValuePair<int, Hit>>();
            foreach (var h in _all)
            {
                int best = Score(h.Label, query);
                int viaGroup = Score(h.Group, query);
                int viaPage = Score(h.Page, query);
                // A hit on the page or group name still counts, ranked below a hit on the setting.
                if (viaGroup >= 0 && (best < 0 || viaGroup + 500 < best)) best = viaGroup + 500;
                if (viaPage >= 0 && (best < 0 || viaPage + 800 < best)) best = viaPage + 800;
                if (best >= 0) scored.Add(new KeyValuePair<int, Hit>(best, h));
            }
            scored.Sort((a, b) => a.Key.CompareTo(b.Key));
            for (int i = 0; i < scored.Count && i < limit; i++) outp.Add(scored[i].Value);
            return outp;
        }

        private static int Score(string text, string query)
        {
            if (string.IsNullOrEmpty(text)) return -1;
            if (Scorer != null) return Scorer(text, query);
            return text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : -1;
        }
    }
}
