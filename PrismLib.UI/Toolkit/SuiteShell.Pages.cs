using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* What a page builds with: the page frames (a stub or classic page's header, a feature page's header and cards
       beside its preview, the classic-menu card), the Edit layout row and the Settings page's cards and shared rows.
       Each row's Id is the adapter's key for its word, so a mod's own checks find it by the name it always had; the
       shell's own element names (menu-header, menu-classic, menu-classic-layout, menu-settings-<title>) never change.
       Nothing here reads Surface, Window or Nav: the first page is built before the shell holds them. */
    public sealed partial class SuiteShell
    {
        /// A stub or classic page's header, "menu-header"; a given description is re-read on every refresh.
        public Facet.Control<bool> Header(VisualElement page, string title, Facet.Bind<bool> on, Func<string> description = null)
        {
            return Wraps(Facet.Controls.PageHeader(page, title, on, o =>
            {
                o.Id = "menu-header";
                if (description != null) o.DescriptionText = description;
            }));
        }

        // Facet draws a page's description on one line, cut off: a page's whole explanation goes there, so it wraps.
        private static Facet.Control<bool> Wraps(Facet.Control<bool> header)
        {
            if (header == null) return null;
            int i = 0;
            header.Root.Query<Label>().ForEach(l => { if (i++ > 0) l.style.whiteSpace = WhiteSpace.Normal; });   // the first is the title
            return header;
        }

        /// A feature page: its header (Id = title) and cards; with a preview, two columns, header and cards left and
        /// the preview right, stacked below 760.
        public void FeatureFrame(VisualElement page, string title, Facet.Bind<bool> on, string description,
                                 Action<VisualElement> cards, Action<VisualElement> preview = null)
        {
            VisualElement left = page, right = null;
            if (preview != null)
            {
                var cols = Facet.Controls.Columns(page, o =>
                {
                    o.Left = 1.25f;
                    o.Right = 1f;
                    o.StackBelow = 760f;
                });
                left = cols.Left;
                right = cols.Right;
            }
            Wraps(Facet.Controls.PageHeader(left, title, on, o =>
            {
                o.Id = title;
                o.Description = description;
            }));
            if (cards != null) cards(left);
            if (preview != null) preview(right);
        }

        /// "This page is still in the classic menu": an Open row ("menu-classic") and, with layout, the Edit layout
        /// row ("menu-classic-layout"). Not on the search index's detached build: the card is the same on every page.
        public void ClassicCard(VisualElement page, Action open, bool layout)
        {
            if (page.panel == null) return;
            var card = Facet.Controls.Card(page, null);
            Facet.Controls.Action(card, _adapter.Key("suite.classic.page", "This page is still in the classic menu"),
                                  _adapter.Key("suite.classic.open", "Open"), open, o => o.Id = "menu-classic");
            if (layout) LayoutRow(card, "menu-classic-layout");
        }

        /// The Edit layout row, shown only while the adapter can edit layout; a click is EditLayout().
        public void LayoutRow(VisualElement card, string id)
        {
            Facet.Controls.Action(card, _adapter.Key("suite.layout.hint", "Move this feature's on-screen elements"),
                                  _adapter.Key("suite.layout.edit", "Edit layout"), EditLayout, o =>
                                  {
                                      o.Id = id;
                                      o.VisibleWhen = () => _adapter.CanEditLayout;
                                  });
        }

        /// A card of the Settings page, "menu-settings-" + title.
        public VisualElement SettingsCard(VisualElement page, string title, Action<Facet.CardOptions> more = null)
        {
            return Facet.Controls.Card(page, title, o =>
            {
                o.Id = "menu-settings-" + title;
                if (more != null) more(o);
            });
        }

        /// The Sidebar setting (SidebarMode): Toggleable, Always visible, Always hidden.
        public Facet.Control<int> SidebarRow(VisualElement card)
        {
            string id = _adapter.Key("suite.sidebar", "Sidebar");
            return Facet.Controls.Dropdown(card, id, Facet.Bind.To(() => SidebarMode, v => SidebarMode = v), o =>
            {
                o.Id = id;
                o.Items = new[]
                {
                    _adapter.Key("suite.sidebar.toggle", "Toggleable"),
                    _adapter.Key("suite.sidebar.shown", "Always visible"),
                    _adapter.Key("suite.sidebar.hidden", "Always hidden"),
                };
                o.Tooltip = _adapter.Key("suite.sidebar.tip",
                    "Toggleable adds a button that folds the sidebar to its icons. Always hidden keeps it folded, "
                    + "and it opens over the page while the pointer rests on it");
            });
        }

        /// Smooth scrolling, over the mod's own bind.
        public Facet.Control<bool> SmoothRow(VisualElement card, Facet.Bind<bool> on)
        {
            string id = _adapter.Key("suite.smooth", "Smooth Scrolling");
            return Facet.Controls.Toggle(card, id, on, o =>
            {
                o.Id = id;
                o.Tooltip = _adapter.Key("suite.smooth.tip",
                    "Glides every Prism window to where the mouse wheel sends it instead of jumping. "
                    + "The same setting as in Prism · Settings.");
            });
        }

        /// The "Animation duration" row whose button opens Prism · Settings, shown while shown() is true.
        public void PrismLinkRow(VisualElement card, Action open, Func<bool> shown)
        {
            string id = _adapter.Key("suite.animation", "Animation duration");
            Facet.Controls.Action(card, id, _adapter.Key("suite.animation.open", "Open Prism · Settings"), open, o =>
            {
                o.Id = id;
                o.VisibleWhen = shown;
            });
        }

        /// Scroll speed, 20 to 300 px, over the mod's own bind. Typed values clamp; the bind is written as given.
        public Facet.Control<float> ScrollSpeedRow(VisualElement card, Facet.Bind<float> speed)
        {
            string id = _adapter.Key("suite.scroll", "Scroll speed");
            return Facet.Controls.Slider(card, id, speed, o =>
            {
                o.Id = id;
                o.Min = 20f;
                o.Max = 300f;
                o.Step = 1f;
                o.Format = "0 px";
            });
        }

        /// The window's link to the classic menu. The round trip is the mod's: open hides the shell if it should.
        public void ClassicMenuRow(VisualElement card, Action open)
        {
            string id = _adapter.Key("suite.classic.menu", "Classic menu");
            Facet.Controls.Action(card, id, _adapter.Key("suite.classic.open", "Open"), open, o => o.Id = id);
        }

        // The adapter's master switch, first in the title bar's right block (the left one is 110 px with a folded
        // rail), sized to its content. Not undoable: turning the mod on or off is no edit of the page.
        private void PinMaster(Facet.Window window)
        {
            var master = _adapter.Master;
            if (master == null) return;
            if (_adapter.MasterIcon.HasValue)
            {
                PinPower(window, master, _adapter.MasterIcon.Value);
                return;
            }
            var c = Facet.Controls.Toggle(window.TitleBarRight, _adapter.MasterLabel, master, o =>
            {
                o.Id = "menu-master";
                o.Undoable = false;
            });
            var row = c.Root;
            window.TitleBarRight.Insert(0, row);
            row.style.flexGrow = 0f;
            row.style.flexShrink = 0f;
            row.style.minHeight = 0f;
            row.style.marginBottom = 0f;
            row.style.alignSelf = Align.Center;
        }

        private const float PowerHeight = 48f;

        // The master switch as a power button pinned under the rail's list ("menu-master"), red while off. It sits in the
        // rail's own hierarchy, so the compact strip keeps it and a rail rebuild (which clears the list) leaves it.
        private static void PinPower(Facet.Window window, Facet.Bind<bool> master, Background icon)
        {
            var rail = window.Body.Q<ScrollView>();   // the Nav builds its rail before its page scroll
            if (rail == null) return;
            var b = new VisualElement { name = "menu-master" };
            b.style.position = Position.Absolute;
            b.style.left = 0f;
            b.style.right = 0f;
            b.style.bottom = 0f;
            b.style.height = PowerHeight;
            b.style.alignItems = Align.Center;
            b.style.justifyContent = Justify.Center;
            var glyph = new VisualElement { name = "menu-master-icon", pickingMode = PickingMode.Ignore };
            glyph.style.width = 22f;
            glyph.style.height = 22f;
            glyph.style.backgroundImage = new StyleBackground(icon);
            b.Add(glyph);
            rail.hierarchy.Add(b);
            rail.contentContainer.style.paddingBottom = PowerHeight;   // the list scrolls clear of the button
            bool hover = false;
            void Paint()
            {
                b.style.backgroundColor = master.Get() ? new Color(0f, 0f, 0f, hover ? 0.2f : 0.1f)
                    : new Color(1f, 0.478f, 0.271f, hover ? 1f : 0.85f);
                glyph.style.unityBackgroundImageTintColor = new Color(1f, 1f, 1f, hover ? 0.9f : 0.6f);
            }
            Paint();
            b.RegisterCallback<MouseEnterEvent>(_ => { hover = true; Paint(); });
            b.RegisterCallback<MouseLeaveEvent>(_ => { hover = false; Paint(); });
            b.RegisterCallback<ClickEvent>(_ => { master.Set(!master.Get()); Paint(); });
            b.schedule.Execute(Paint).Every(250);   // the mod may flip it elsewhere (a self-test, a refused press)
        }
    }
}
