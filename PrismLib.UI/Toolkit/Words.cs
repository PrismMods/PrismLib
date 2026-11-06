using System;
using System.Collections.Generic;

namespace PrismLib.UI.Toolkit
{
    /* Text in the player's language. English is the key, as in the mods' own Loc tables, so
       anything untranslated shows in English. Ui.Text and Ui.Btn look every string up, so page code
       never has to — the convention the mods' uGUI builders already follow.

       A string is translated only by the table of whoever wrote it. English keys carry no context,
       so a lookup in someone else's table finds the same word in another sense: CamDisplay's
       "Undo" (remove the camera patch) came out as the editor's 실행 취소. So:
       - PrismLib's own table (below) answers only Own(), which PrismLib's code calls for its own
         words, and the rows PrismLib registers itself (owner "PrismLib").
       - A settings row is built with Owner set to its mod's name; then only that mod's
         translators answer, and a mod with no table shows English.
       - Elsewhere (a mod's own menu) every registered translator answers, in order. */
    public static class Words
    {
        /// "ko" for Korean; anything else (or null) is English. Each bridge points this at its own
        /// language setting; they all follow the game by default, so the last one set agrees.
        public static Func<string> Language;

        private static readonly List<Func<string, string>> _translators = new List<Func<string, string>>();
        private static readonly List<string> _assemblies = new List<string>();

        /// Whose strings are being built: a settings row's mod (its Prism name), or null.
        internal static string Owner;

        internal const string Self = "PrismLib";

        /// Titles every bridge passes for the shared windows: PrismLib's words whoever builds them.
        private static readonly HashSet<string> Shared = new HashSet<string> { "Prism · Settings", "Prism · Debug" };

        /// A mod's own table: return the translation, or null when it has none.
        public static void AddTranslator(Func<string, string> t)
        {
            if (t == null) return;
            _translators.Add(t);
            _assemblies.Add(AssemblyOf(t));
        }

        public static string T(string english)
        {
            if (string.IsNullOrEmpty(english)) return english;
            string owner = Owner;
            if (owner == Self || Shared.Contains(english)) return Own(english);
            for (int i = 0; i < _translators.Count; i++)
            {
                if (owner != null && !Owns(_assemblies[i], owner)) continue;
                string s = null;
                try { s = _translators[i](english); } catch { }
                if (!string.IsNullOrEmpty(s) && s != english) return s;
            }
            return english;
        }

        /// T for a sentence with a value in it: translate the pattern, then fill it.
        public static string F(string englishFormat, params object[] args)
        {
            try { return string.Format(T(englishFormat), args); }
            catch (FormatException) { return string.Format(englishFormat, args); }
        }

        /// PrismLib's own words, from its own table only.
        internal static string Own(string english)
        {
            string ko;
            return !string.IsNullOrEmpty(english) && IsKorean() && Korean.TryGetValue(english, out ko) ? ko : english;
        }

        internal static string OwnF(string englishFormat, params object[] args)
        {
            try { return string.Format(Own(englishFormat), args); }
            catch (FormatException) { return string.Format(englishFormat, args); }
        }

        /* A translator belongs to the mod whose code it is: the bridges register a lambda over
           their own Loc table, compiled into the mod's assembly, and every Prism mod's assembly is
           named after its Prism name (Topaz, Sapphire, CamDisplay or CamDisplayUmm). This works
           with mods already released, which register with no owner.
           ponytail: name match; an AddTranslator(owner, t) overload if a mod's assembly is ever
           named otherwise. */
        private static string AssemblyOf(Func<string, string> t)
        {
            try { return t.Method.DeclaringType.Assembly.GetName().Name; } catch { return null; }
        }

        private static bool Owns(string assembly, string owner)
            => assembly != null && assembly.StartsWith(owner, StringComparison.OrdinalIgnoreCase);

        private static bool IsKorean()
        {
            try { return Language != null && Language() == "ko"; } catch { return false; }
        }

        // PrismLib's own words, read through Own(). Terms follow the mods' tables (스톱 for a gradient stop, 실행 취소, 사용 중).
        private static readonly Dictionary<string, string> Korean = new Dictionary<string, string>
        {
            { "Prism · Settings", "Prism · 설정" },
            { "Prism · Debug", "Prism · 디버그" },
            { "Settings", "설정" },
            { "General", "일반" },
            { "Search settings…", "설정 검색…" },
            { "Nothing matches.", "일치하는 항목이 없습니다." },
            { "No setting matches '{0}'", "'{0}'에 맞는 설정이 없습니다" },
            { "Nothing here yet", "아직 아무것도 없습니다" },
            { "This page failed to build — see the log.", "이 페이지를 만들지 못했습니다 — 로그를 확인하세요." },
            { "{0} — not rebindable here", "{0} — 여기서는 바꿀 수 없습니다" },
            { "Run", "실행" },
            { "Undo", "실행 취소" },
            { "Redo", "다시 실행" },
            { "Undo {0}", "실행 취소: {0}" },
            { "Redo {0}", "다시 실행: {0}" },
            { "Sure?", "정말요?" },
            { "Active", "사용 중" },
            { "unbound", "없음" },
            { "press a key…", "키를 누르세요…" },
            { "Refresh", "새로고침" },
            { "Clear", "지우기" },
            { "Open folder", "폴더 열기" },
            { "Line numbers", "줄 번호" },
            { "Show [dbg] and [perf] lines", "[dbg] · [perf] 줄 보기" },
            { "Click the strip to add a stop.", "띠를 클릭하면 스톱이 추가됩니다." },
            { "Stop colour", "스톱 색" },
            { "Position", "위치" },
            { "Add stop", "스톱 추가" },
            { "Remove stop", "스톱 제거" },
            { "Restart to finish updating", "재시작하면 업데이트가 끝납니다" },
            { "{0} updated. Restart the game now to load the new version?", "{0} 업데이트를 설치했습니다. 새 버전을 불러오도록 지금 게임을 다시 시작할까요?" },
            { " and ", ", " },
            { "Remember my choice", "선택 기억하기" },
            { "Change it later in Prism · Settings", "나중에 Prism · 설정에서 바꿀 수 있습니다" },
            { "Later", "나중에" },
            { "Restart now", "지금 다시 시작" },
            // PrismLib's own settings (Prism.DescribeOwnSettings), shown in Prism · Settings.
            { "Show FPS", "FPS 표시" },
            { "Animation duration (ms)", "애니메이션 길이 (ms)" },
            { "Length of a standard transition in every Prism window. 0 turns animation off.", "모든 Prism 창의 기본 전환 길이입니다. 0이면 애니메이션이 꺼집니다." },
            { "Frame rate in the bottom-right corner, for every Prism mod", "오른쪽 아래에 프레임 레이트를 표시합니다 (모든 Prism 모드 공통)" },
            { "Display", "표시" },
            { "After a mod update", "모드 업데이트 후" },
            { "What to do once a Prism mod has installed an update", "Prism 모드가 업데이트를 설치한 뒤 할 일" },
            { "Updates", "업데이트" },
            { "Ask", "묻기" },
            { "Restart later", "나중에 다시 시작" },
        };
    }
}
