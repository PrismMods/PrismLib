using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* The look of the small window a mod's hotkey opens, the same for every mod: a translucent wash over the whole
       screen behind it, a window with no fill and no border (its logo and buttons float on the wash), and button
       labels centred. Clicks on the wash pass through it, as nothing on it answers them. Wear once, after the window's
       buttons are built; FadeIn on each show fades the wash and the window in together, and every hide fades them out
       (Wear hooks the surface's Hidden). After the first fade, showing and hiding allocate nothing. */
    public static class HubLook
    {
        /// The wash's name: the first child of the surface's root, under the window.
        public const string WashName = "hub-wash";
        /// Each fade's length at the standard motion speed (Anim.Duration scales it; 0 shows at once).
        public const int FadeMs = 200;
        /// A hub button, as the old hotkey hub drew it: 240 wide, at least 56 tall, corners 6, 24-unit text, 8 apart.
        public const float ButtonWidth = 240f, ButtonHeight = 56f, ButtonRadius = 6f, ButtonTextSize = 24f, ButtonGap = 8f;
        /// The logo leading the hub, and the wordmark line under it.
        public const float LogoSize = 120f, WordmarkSize = 28f, WordmarkHeight = 40f;
        /// The wordmark's name: the logo's child.
        public const string WordmarkName = "hub-wordmark";
        /// The wordmark's colour: white, barely translucent.
        public static readonly Color Wordmark = new Color(1f, 1f, 1f, 0.92f);
        // The old hub: the wordmark 8 under the logo, the buttons 12 under the wordmark; its letters 0.06 em apart.
        private const float WordmarkGap = 8f, LogoGap = 12f, WordmarkSpacing = 0.06f * WordmarkSize;
        // How much white lies over a button's fill: none at rest on the accent; the neutral fill is the theme's title bar
        // lifted a fifth toward white (the old hub's close button sat that far from its panel); hover and press lift more.
        private const float NeutralLift = 0.2f, HoverLift = 0.06f, PressLift = 0.12f;
        private const string NeutralClass = "hub-button-neutral";
        private static readonly Facet.Look AccentLook = new Facet.Look { Bg = Facet.Role.Accent, Text = Facet.Role.AccentText, FocusBorder = Facet.Role.Text };
        private static readonly Facet.Look NeutralLook = new Facet.Look { Bg = Facet.Role.TitleBar, Text = Facet.Role.Text, FocusBorder = Facet.Role.Text };

        /// Puts the wash under `window` on `surface`, strips the window's fill and border and centres every button
        /// label in it. Wearing it again adds no second wash. Returns the wash, or null without a surface root.
        public static VisualElement Wear(Facet.Surface surface, VisualElement window)
        {
            if (surface == null || surface.Root == null) return null;
            VisualElement root = surface.Root;
            VisualElement wash = Wash(root);
            if (wash == null)
            {
                wash = new VisualElement { name = WashName, pickingMode = PickingMode.Ignore };
                wash.style.position = Position.Absolute;
                wash.style.left = 0f; wash.style.top = 0f; wash.style.right = 0f; wash.style.bottom = 0f;
                wash.style.backgroundColor = Tokens.Wash;
                root.Insert(0, wash);
                surface.Hidden += () => FadeOut(surface);
            }
            if (window != null)
            {
                Bare(window);
                window.Query<Button>().ForEach(Centre);
            }
            return wash;
        }

        /// No fill and no border on `e`, kept through re-themes.
        public static void Bare(VisualElement e)
        {
            if (e == null) return;
            Facet.Paint.Forget(e);
            e.style.backgroundColor = Color.clear;
            Facet.Ui.SetBorderWidth(e, 0f);
        }

        /// The button's label, and whatever it holds, centred both ways.
        public static void Centre(VisualElement button)
        {
            if (button == null) return;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.justifyContent = Justify.Center;
            button.style.alignItems = Align.Center;
        }

        /// Dresses every Button directly in `container` as a hub button: the old hub's size, a solid accent fill (the last
        /// one, Close, a neutral fill), large centred text, and hover, press and focus that show. In a column they stand
        /// ButtonGap apart. Their text children take the hub's text size too. Once, after the buttons are built.
        public static void Buttons(VisualElement container)
        {
            if (container == null) return;
            bool row = container.style.flexDirection.keyword == StyleKeyword.Undefined && container.style.flexDirection.value == FlexDirection.Row;
            int last = -1, first = -1;
            for (int i = 0; i < container.childCount; i++)
                if (container[i] is Button) { if (first < 0) first = i; last = i; }
            for (int i = first; i >= 0 && i <= last; i++)
            {
                var b = container[i] as Button;
                if (b == null) continue;
                bool neutral = i == last;
                Facet.Paint.Look(b, neutral ? NeutralLook : AccentLook);
                b.style.width = ButtonWidth;
                b.style.minHeight = ButtonHeight;
                b.style.fontSize = ButtonTextSize;
                b.Query<TextElement>().ForEach(SizeText);
                Facet.Ui.SetRadius(b, ButtonRadius);
                Facet.Ui.SetBorderWidth(b, 2f);   // the focus ring; clear until focused
                if (!row) { b.style.marginLeft = 0f; b.style.marginRight = 0f; b.style.marginTop = i == first ? 0f : ButtonGap; }
                // The lift is white laid over the fill as a tinted image, so the fill keeps following the theme.
                b.style.backgroundImage = Background.FromTexture2D(Texture2D.whiteTexture);
                if (neutral) b.AddToClassList(NeutralClass);
                Lift(b, 0f);
                Facet.Events.On(b, Enter);
                Facet.Events.On(b, Leave);
                Facet.Events.On(b, Down, TrickleDown.TrickleDown);   // a Button's clickable stops a bubbling pointer-down
                Facet.Events.On(b, Up, TrickleDown.TrickleDown);
                Centre(b);
            }
        }

        /// Sizes `logo` as the hub's logo, centred, with `wordmark` (when given) as a line under it; the next element
        /// stands clear of both. Returns the wordmark's label, or null.
        public static Label Logo(VisualElement logo, string wordmark)
        {
            if (logo == null) return null;
            logo.style.width = LogoSize;
            logo.style.height = LogoSize;
            logo.style.alignSelf = Align.Center;
            logo.style.flexShrink = 0f;
            logo.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            logo.style.marginBottom = string.IsNullOrEmpty(wordmark) ? LogoGap : WordmarkGap + WordmarkHeight + LogoGap;
            if (string.IsNullOrEmpty(wordmark)) return null;
            // Hung under the logo, as wide as a hub button, so the logo stays the window's first element.
            var l = new Label(wordmark) { name = WordmarkName, pickingMode = PickingMode.Ignore };
            l.style.position = Position.Absolute;
            l.style.top = LogoSize + WordmarkGap;
            l.style.height = WordmarkHeight;
            l.style.left = (LogoSize - ButtonWidth) / 2f;
            l.style.right = (LogoSize - ButtonWidth) / 2f;
            l.style.fontSize = WordmarkSize;
            l.style.letterSpacing = WordmarkSpacing;
            l.style.color = Wordmark;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            logo.Add(l);
            return l;
        }

        private static void SizeText(TextElement t) { t.style.fontSize = ButtonTextSize; }

        // White over the fill: the neutral fill's own lift, plus hover or press.
        private static void Lift(VisualElement b, float more)
        {
            float a = (b.ClassListContains(NeutralClass) ? NeutralLift : 0f) + more;
            b.style.unityBackgroundImageTintColor = new Color(1f, 1f, 1f, a);
        }

        // Static, so dressing a button allocates no callback and hovering allocates nothing.
        private static readonly EventCallback<MouseEnterEvent> Enter = e => Lift((VisualElement)e.currentTarget, HoverLift);
        private static readonly EventCallback<MouseLeaveEvent> Leave = e => Lift((VisualElement)e.currentTarget, 0f);
        private static readonly EventCallback<PointerDownEvent> Down = e => { if (e.button == 0) Lift((VisualElement)e.currentTarget, PressLift); };
        private static readonly EventCallback<PointerUpEvent> Up = e => { if (e.button == 0) Lift((VisualElement)e.currentTarget, HoverLift); };

        /// Fades the surface's wash and window in from transparent, as the surface shows. Nothing without a wash.
        public static void FadeIn(Facet.Surface surface)
        {
            VisualElement root = surface == null ? null : surface.Root;
            if (Wash(root) == null) return;
            Facet.Data.Get<Fader>(root).Start(surface, true);
        }

        /// Fades a hidden surface's wash and window out, then hides them. The surface already counts as hidden: it
        /// holds no input and no Escape. Showing it again stops the fade.
        // ponytail: a click on a fading button still runs it (SetEnabled would also stop a synthetic Submit, which
        // tests send to hidden windows); add a full-screen catcher if a stray click in those 200 ms ever matters.
        public static void FadeOut(Facet.Surface surface)
        {
            VisualElement root = surface == null ? null : surface.Root;
            if (Wash(root) == null || surface.Visible) return;
            Facet.Data.Get<Fader>(root).Start(surface, false);
        }

        private static VisualElement Wash(VisualElement root)
        {
            return root != null && root.childCount > 0 && root[0].name == WashName ? root[0] : null;
        }

        // One scheduled step per surface, made on the first fade and paused between fades.
        private sealed class Fader
        {
            private Facet.Surface _surface;
            private VisualElement _root;
            private IVisualElementScheduledItem _item;
            private float _start, _length, _from;
            private bool _in;

            internal void Start(Facet.Surface surface, bool fadeIn)
            {
                VisualElement root = surface.Root;
                _surface = surface;
                _in = fadeIn;
                _length = FadeMs / 1000f * Facet.Anim.Speed;
                if (_length <= 0f || root.panel == null)
                {
                    if (_item != null) _item.Pause();
                    Finish(root);
                    return;
                }
                _start = Time.unscaledTime;
                if (fadeIn) root.style.opacity = 0f;
                else
                {
                    // From wherever a fade-in had got to; the surface set display None as it hid.
                    _from = root.resolvedStyle.opacity;
                    root.style.display = DisplayStyle.Flex;
                }
                if (_item == null || _root != root)
                {
                    _root = root;
                    _item = root.schedule.Execute(Step).Every(0);
                }
                else _item.Resume();
            }

            // Ends a fade: shown at full opacity, or hidden and reset so a show without FadeIn is not left transparent.
            private void Finish(VisualElement root)
            {
                root.style.opacity = 1f;
                if (!_in && !_surface.Visible) root.style.display = DisplayStyle.None;
            }

            // Sine ease out both ways, as the hotkey window's fade always has.
            private void Step(TimerState _)
            {
                float t = (Time.unscaledTime - _start) / _length;
                if (t >= 1f || !_in && _surface.Visible)   // done, or shown again without a FadeIn
                {
                    Finish(_root);
                    _item.Pause();
                    return;
                }
                float e = Mathf.Sin(t * Mathf.PI / 2f);
                _root.style.opacity = _in ? e : _from * (1f - e);
            }
        }
    }
}
