using System;
using System.Collections.Generic;

namespace PrismLib.UI.Toolkit
{
    /* Text in the player's language. English is the key, as in the mods' own Loc tables, so
       anything untranslated shows in English. Ui.Text and Ui.Btn look every string up, so page code
       never has to — the convention the mods' uGUI builders already follow.

       Two sources, in order: translators the mods register (each mod's own table, so its labels
       translate in any Prism window), then PrismLib.UI's table for its own words — buttons, empty
       states, the restart prompt — so three mods do not each carry a copy. */
    public static class Words
    {
        /// "ko" for Korean; anything else (or null) is English. Each bridge points this at its own
        /// language setting; they all follow the game by default, so the last one set agrees.
        public static Func<string> Language;

        private static readonly List<Func<string, string>> _translators = new List<Func<string, string>>();

        /// A mod's own table: return the translation, or null when it has none.
        public static void AddTranslator(Func<string, string> t) { if (t != null) _translators.Add(t); }

        public static string T(string english)
        {
            if (string.IsNullOrEmpty(english)) return english;
            for (int i = 0; i < _translators.Count; i++)
            {
                string s = null;
                try { s = _translators[i](english); } catch { }
                if (!string.IsNullOrEmpty(s) && s != english) return s;
            }
            string ko;
            return IsKorean() && Korean.TryGetValue(english, out ko) ? ko : english;
        }

        /// T for a sentence with a value in it: translate the pattern, then fill it.
        public static string F(string englishFormat, params object[] args)
        {
            try { return string.Format(T(englishFormat), args); }
            catch (FormatException) { return string.Format(englishFormat, args); }
        }

        private static bool IsKorean()
        {
            try { return Language != null && Language() == "ko"; } catch { return false; }
        }

        // Terms follow the mods' tables (스톱 for a gradient stop, 실행 취소, 사용 중).
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
