using System;
using System.Collections.Generic;

namespace PrismLib.UI.Toolkit
{
    /* What the search box searches: the index of the last Nav built or searched, which is Facet's.

       Facet keeps one index per Nav, made on its first search by building each page once into a
       throwaway container; rows note themselves as Facet builds them. So nothing here records any
       more: BeginPage, EndPage, Note and Clear are kept for callers built against them and do
       nothing, and Find reads that Nav's index (empty until its first search). A hit knows its page,
       which is what lets a result navigate rather than just report. */
    public static class SearchIndex
    {
        public sealed class Hit
        {
            public string Label;
            public string Page;
            public object PageRef;      // the NavItem, held loosely so this file stays generic
            public string Group;
        }

        /// Unused: Facet tracks the page and section being indexed.
        public static object CurrentPage;
        public static string CurrentPageName;
        public static string CurrentGroup;

        public static void Clear() { }

        public static void BeginPage(object page, string name) { }

        public static void EndPage() { }

        public static void Note(string label) { }

        /// Ignored: matching is Facet.Search's now. Kept because bridges built before it set this.
        public static Func<string, string, int> Scorer;

        public static List<Hit> Find(string query, int limit = 40)
        {
            var outp = new List<Hit>();
            var nav = Nav.Last;
            var index = nav != null ? nav.Index : null;
            if (string.IsNullOrEmpty(query) || index == null) return outp;
            foreach (var h in index.Find(query, limit))
                outp.Add(new Hit { Label = h.Label, Page = h.Page, PageRef = nav.Source(h.PageRef), Group = h.Section });
            return outp;
        }
    }
}
