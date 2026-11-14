// A generic suite adapter for the shell checks: a few categories and pages over an in-memory store, with
// switches the checks flip (key capture, the layout editor showing, a throwing member). With Shell set, its pages
// also use the shell's page frames and Settings rows.
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
        public bool HasMaster = true, MasterOn, AlphaOn, BetaOn, Smooth, PrismShown;
        public float Speed = 100f;
        public SuiteShell Shell;
        // What the shell's rows gave back, for the checks.
        public Facet.Control<int> SidebarControl;
        public Facet.Control<float> SpeedControl;
        public VisualElement StartLayout;
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
        public override Facet.Bind<bool> Master => HasMaster ? Facet.Bind.To(() => MasterOn, v => MasterOn = v) : null;
        public override string MasterLabel => "Demo on";
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
            if (Shell != null) ShellRows(panel, page.Key);
        }

        void ShellRows(VisualElement p, string key)
        {
            var s = Shell;
            switch (key)
            {
                case "start.home":
                    s.LayoutRow(Facet.Controls.Card(p, null), "start-layout");
                    if (p.panel != null) StartLayout = p.Q("start-layout");
                    break;
                case "options.main":
                    var sidebar = s.SidebarRow(s.SettingsCard(p, "Look"));
                    var motion = s.SettingsCard(p, "Motion");
                    s.SmoothRow(motion, Facet.Bind.To(() => Smooth, v => Smooth = v));
                    s.PrismLinkRow(motion, () => Calls.Add("prism"), () => PrismShown);
                    var speed = s.ScrollSpeedRow(s.SettingsCard(p, "Behaviour"), Facet.Bind.To(() => Speed, v => Speed = v));
                    s.ClassicMenuRow(s.SettingsCard(p, "Other"), () => Calls.Add("classic"));
                    if (p.panel != null) { SidebarControl = sidebar; SpeedControl = speed; }   // not the search index's copy
                    break;
                case "things.alpha":
                    s.FeatureFrame(p, "Alpha", Facet.Bind.To(() => AlphaOn, v => AlphaOn = v), "About alpha",
                                   c => c.Add(new Label("cards")), r => r.Add(new Label("preview")));
                    break;
                case "things.off":
                    s.Header(p, "Beta", Facet.Bind.To(() => BetaOn, v => BetaOn = v), () => "off");
                    s.ClassicCard(p, () => Calls.Add("open"), true);
                    break;
            }
        }

        public override IEnumerable<Facet.SearchIndex.Entry> SearchRows()
        {
            yield return new Facet.SearchIndex.Entry { Label = "Gizmo", Open = () => Calls.Add("gizmo") };
        }
    }
}
