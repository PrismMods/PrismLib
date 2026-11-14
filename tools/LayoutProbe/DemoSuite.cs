// A generic suite adapter for the shell checks: a few categories and pages over an in-memory store, with
// switches the checks flip (key capture, the layout editor showing, a throwing member).
using System;
using System.Collections.Generic;
using PrismLib.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace LayoutProbe
{
    sealed class DemoSuite : SuiteAdapter
    {
        public readonly Dictionary<string, string> Store;
        public readonly List<string> Logged = new List<string>();
        public readonly List<string> Built = new List<string>();
        public readonly List<string> Calls = new List<string>();
        public string Lang = "en";
        public string Id = "probe.menu";
        public bool Extra, Capturing, Showing, ReturnOk = true, ThrowShowing, ThrowKeep, EditOk = true, SecretShown;
        readonly Facet.Host _host;
        readonly Background _logo;

        static readonly Dictionary<string, string> Korean = new Dictionary<string, string> { { "Search", "찾기" }, { "Find", "검색" }, { "Alpha", "알파" } };

        public DemoSuite(string hostName = "LayoutProbeSuite", Dictionary<string, string> store = null)
        {
            Store = store ?? new Dictionary<string, string>();
            _host = Facet.Host.Create(hostName, o =>
            {
                o.Read = k => { string v; return Store.TryGetValue(k, out v) ? v : null; };
                o.Write = (k, v) => { if (v == null) Store.Remove(k); else Store[k] = v; };
                o.Translate = s => { string t; return Lang == "ko" && s != null && Korean.TryGetValue(s, out t) ? t : s; };
                o.Language = () => Lang;
                o.Log = m => Logged.Add(m);
            });
            _logo = Background.FromTexture2D(new Texture2D(16, 16));
        }

        public override Facet.Host Host => _host;
        public override string WindowId => Id;
        public override string Title => "DEMO";
        public override string HomePage => "start.home";
        public override Background? Logo => _logo;
        public override bool KeepEscape { get { if (ThrowKeep) throw new InvalidOperationException("probe"); return Capturing; } }
        public override bool LayoutEditorShowing { get { if (ThrowShowing) throw new InvalidOperationException("probe"); return Showing; } }
        public override bool ReturnFromLayout => ReturnOk;
        public override bool CanEditLayout => EditOk;
        public override void OnLeaving() { Calls.Add("leaving"); }
        public override void OpenLayoutEditor() { Calls.Add("open-editor"); }
        public override void OnReturning() { Calls.Add("returning"); }
        // Not the English fallback: the shell's search word must come through Key.
        public override string Key(string id, string english) => id == "suite.search" ? "Find" : english;

        static SuitePage Page(string key, string label, string feature = null, Func<bool> visible = null) =>
            new SuitePage { Key = key, Label = label, Feature = feature, Visible = visible };

        static SuiteCategory Category(string key, string label, int group, bool tabs, params SuitePage[] pages)
        {
            IList<SuitePage> list = pages;
            return new SuiteCategory { Key = key, Label = label, Group = group, Tabs = tabs, Pages = () => list };
        }

        public override IList<SuiteCategory> Categories()
        {
            var l = new List<SuiteCategory>
            {
                Category("start", "Start", 0, false, Page("start.home", "Home")),
                Category("things", "Things", 1, true, Page("things.alpha", "Alpha", "alpha"), Page("things.beta", "Beta", "beta"), Page("things.off", "Off", "beta"),
                         Page("things.secret", "Secret", null, () => SecretShown)),
                Category("options", "Options", 2, false, Page("options.main", "Main")),
                Category("vault", "Vault", 2, false, Page("vault.page", "Vault", null, () => SecretShown)),
                Category("aside", "Aside", -1, false, Page("aside.page", "Aside")),
            };
            if (Extra) l.Add(Category("more", "More", 1, false, Page("more.page", "More")));
            return l;
        }

        public override void BuildPage(VisualElement panel, SuitePage page)
        {
            Built.Add(page.Key);
            panel.Add(new Label(page.Key));
            // A row named after its page: search indexes rows, not page titles.
            Facet.Controls.Toggle(panel, page.Label, Facet.Bind.Value(false), o => o.Id = page.Key + ".row");
        }

        public override IEnumerable<Facet.SearchIndex.Entry> SearchRows()
        {
            yield return new Facet.SearchIndex.Entry { Label = "Gizmo", Open = () => Calls.Add("gizmo") };
        }
    }
}
