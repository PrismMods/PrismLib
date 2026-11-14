using System;
using System.Collections.Generic;

namespace PrismLib.UI.Toolkit
{
    /// What one frame of Edit layout's wait decides: keep waiting, bring the menu back, or bring it back after the
    /// editor never appeared.
    public enum LayoutReturn { Wait, Back, BackLate }

    /* The suite shell's rules that need no game: last page, tab lookup, sidebar mode, the compact rail and the
       layout-return wait. BCL only, so tools/PrismLibTest.cs compiles this file directly and checks it offline. */
    public static class SuiteModel
    {
        public const int LayoutWaitFrames = 120;

        /// The stored page when it is set and exists, else home.
        public static string LastPage(string stored, Func<string, bool> exists, string home)
        {
            return !string.IsNullOrEmpty(stored) && exists(stored) ? stored : home;
        }

        /// The tab for the open page: by page key, then by owning feature (a stub finds its feature's tab), else -1
        /// (Facet keeps its own).
        public static int TabIndex(IList<KeyValuePair<string, string>> pages, string currentKey, string currentFeature)
        {
            for (int i = 0; i < pages.Count; i++) if (pages[i].Key == currentKey) return i;
            if (currentFeature != null)
                for (int i = 0; i < pages.Count; i++) if (pages[i].Value == currentFeature) return i;
            return -1;
        }

        /// Sidebar modes: 0 can be folded (default), 1 always shown, 2 icons only.
        public static int ParseSidebar(string stored)
        {
            return stored == "1" ? 1 : stored == "2" ? 2 : 0;
        }

        /// 1 and 2 as stored; anything else null, which removes the key.
        public static string FormatSidebar(int mode)
        {
            return mode == 1 ? "1" : mode == 2 ? "2" : null;
        }

        /// Icons only always; can be folded when "1" is stored; never when always shown.
        public static bool Compact(int mode, string storedCompact)
        {
            return mode == 2 || mode == 0 && storedCompact == "1";
        }

        /// One frame of Edit layout's wait; frames and seen start at 0 and false. The editor showing marks it seen;
        /// gone after that, the menu comes back. Never seen, the menu comes back late on the LayoutWaitFrames-th call.
        public static LayoutReturn StepLayout(ref int frames, ref bool seen, bool showing)
        {
            if (showing)
            {
                seen = true;
                return LayoutReturn.Wait;
            }
            if (seen) return LayoutReturn.Back;
            return ++frames < LayoutWaitFrames ? LayoutReturn.Wait : LayoutReturn.BackLate;
        }
    }
}
