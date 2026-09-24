using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PrismLib.UI
{
    /// Colours and font for a toast. Mutable: change Accent at runtime and the card follows.
    public sealed class ToastTheme
    {
        public Color Background     = new Color(0.10f, 0.10f, 0.13f, 0.96f);
        public Color BackgroundHover = new Color(0.16f, 0.16f, 0.19f, 0.98f);
        public Color Border         = new Color(1f, 1f, 1f, 0.14f);
        public Color Text           = Color.white;
        public Color TextMuted      = new Color(1f, 1f, 1f, 0.55f);
        public Color Accent         = new Color(0.604f, 0.706f, 1f, 1f);
        public Color CloseHover     = new Color(0.910f, 0.067f, 0.137f, 1f);
        public TMP_FontAsset Font;      // null = TMP's default
    }

    /// What the card should say right now. The host rebuilds this every tick; it is a
    /// description, not a command, so there is nothing to keep in sync.
    public sealed class ToastContent
    {
        /// Identity of this message. Dismissing one Key does not suppress the next, so
        /// "update available" and "update installed" are separate cards for the same version.
        public string Key;
        public string Title;
        public string Hint;
        /// 0..1 shows a progress bar; anything negative hides it.
        public float Progress = -1f;
        /// Whether this one times out on its own. Off for anything in flight.
        public bool AutoHide;
        public float Width = 380f;
        public Action OnClick;
    }

    /* A top-right toast card: slides in, shows optional progress, closes on × or by timing out.

       This is Sapphire's update toast with the Sapphire taken out. Bismuth wanted the same card,
       and copying 350 lines of Unity layout into a second mod — then a third — is how three mods
       end up with three slightly different toasts and three separate bugs.

       The host owns the MEANING (what the message says, what clicking does) and pushes it in via
       Set(); this owns the CARD (building, slotting, animating, hovering, dismissing). State is
       pushed rather than subscribed because update checks run on worker threads and Unity objects
       may only be touched from the main one — the host polls its own service and hands over plain
       strings.

       One instance per mod. The canvas is named "<Mod>UpdateToastCanvas" and the card
       "UpdateToast", which is what ToastStack looks for: the naming is load-bearing, and it is how
       two mods' toasts stack instead of overlapping. */
    public sealed class Toast : IDisposable
    {
        private const float H = 66f;
        private const float Inset = 24f;          // margin from the screen corner
        private const float AutoHideSeconds = 12f;
        private const float SlideSpeed = 7f;      // exponential approach, per second

        private readonly string _canvasName;
        private readonly ToastTheme _theme;

        private GameObject _canvasGo, _cardGo;
        private RectTransform _card;
        private CanvasGroup _group, _closeGroup;
        private RoundedRectGraphic _bg, _pip, _barFill;
        private TextMeshProUGUI _title, _hint, _closeX;
        private GameObject _barGo;
        private RectTransform _barFillRect;

        private ToastContent _content;
        private bool _visible;
        private string _shownKey;
        private string _dismissedKey;
        private float _hideAt;                    // Time.unscaledTime deadline, 0 = no auto-hide
        private float _slotY;                     // smoothed screen-px offset from ToastStack
        private bool _hovered;

        /// modName is the bare mod name ("Sapphire"); the canvas name is derived from it.
        public Toast(string modName, ToastTheme theme = null)
        {
            _canvasName = (modName ?? "Mod") + ToastStack.CanvasSuffix;
            _theme = theme ?? new ToastTheme();
        }

        public ToastTheme Theme => _theme;

        /// The message that should be on screen, or null for none. Safe to call every frame.
        public void Set(ToastContent content)
        {
            _content = content;
            string key = content != null ? content.Key : null;
            if (key == null || key == _dismissedKey)
            {
                if (_visible) Hide();
            }
            else if (!_visible || key != _shownKey)
            {
                Show(content);
            }
        }

        public void Tick()
        {
            if (_canvasGo == null) return;
            if (_visible) RefreshText();
            Animate();
        }

        private void Show(ToastContent c)
        {
            Build();
            _shownKey = c.Key;
            _visible = true;
            _cardGo.SetActive(true);
            _card.sizeDelta = new Vector2(c.Width, H);
            _hideAt = c.AutoHide ? Time.unscaledTime + AutoHideSeconds : 0f;
            RefreshText();
        }

        /* Marking the key dismissed is what stops an auto-hide from looping. The host re-derives
           the desired content EVERY frame, so without this the timeout would hide the card and the
           next frame would see the same still-valid key with _visible false and slide it straight
           back in, forever. A timed-out toast counts as seen, same as pressing ×. */
        private void Hide()
        {
            if (_shownKey != null) _dismissedKey = _shownKey;
            _visible = false;
            _hideAt = 0f;
        }

        private void RefreshText()
        {
            if (_title == null || _content == null) return;
            _title.text = _content.Title ?? "";
            _hint.text = _content.Hint ?? "";
            _title.color = _theme.Text;
            _hint.color = _theme.TextMuted;
            if (_pip != null) _pip.color = _theme.Accent;
            if (_barFill != null) _barFill.color = _theme.Accent;
        }

        private void Animate()
        {
            float w = _card.sizeDelta.x;
            /* ToastStack measures in SCREEN pixels (it has to — foreign canvases may scale
               differently). anchoredPosition is in OUR canvas's units, so divide by our scale
               factor or the gap is wrong at every resolution except the reference one. */
            float target = ToastStack.OffsetFor(_canvasName) / CanvasScale();
            float t = 1f - Mathf.Exp(-SlideSpeed * Time.unscaledDeltaTime);
            _slotY = _visible ? Mathf.Lerp(_slotY, target, t) : target;
            float x = _visible ? -Inset : w + Inset;   // off the right edge when hidden
            Vector2 want = new Vector2(x, -(Inset + _slotY));

            _card.anchoredPosition = Vector2.Lerp(_card.anchoredPosition, want, t);
            _group.alpha = Mathf.Lerp(_group.alpha, _visible ? 1f : 0f, t);
            if (!_visible && _group.alpha < 0.02f && _cardGo.activeSelf) _cardGo.SetActive(false);

            if (_bg != null)
            {
                _bg.color = _hovered ? _theme.BackgroundHover : _theme.Background;
                _bg.BorderColor = _theme.Border;
            }

            /* × only while the pointer is on the card. Raycasts follow the alpha, or an invisible
               button would still eat the click meant for the card itself. interactable stays true:
               turning it off kills the click outright. */
            if (_closeGroup != null)
            {
                _closeGroup.alpha = Mathf.Lerp(_closeGroup.alpha, _hovered ? 1f : 0f, t);
                _closeGroup.blocksRaycasts = _closeGroup.alpha > 0.5f;
            }

            if (_hideAt > 0f && !_hovered && Time.unscaledTime >= _hideAt) Hide();

            if (_barGo != null)
            {
                float p = _content != null ? _content.Progress : -1f;
                bool showBar = p >= 0f;
                if (_barGo.activeSelf != showBar) _barGo.SetActive(showBar);
                if (showBar) _barFillRect.anchorMax = new Vector2(Mathf.Clamp01(p), 1f);
            }
        }

        private float CanvasScale()
        {
            if (_canvasGo == null) return 1f;
            var c = _canvasGo.GetComponent<Canvas>();
            float f = c != null ? c.scaleFactor : 1f;
            return f > 0.0001f ? f : 1f;
        }

        private void OnCardClick()
        {
            var c = _content;
            if (c != null && c.OnClick != null)
            {
                c.OnClick();
                _hideAt = 0f;
                _shownKey = null;   // force a re-Show, so the card re-sizes for whatever comes next
            }
        }

        // ── build ────────────────────────────────────────────────────────────

        private void Build()
        {
            if (_canvasGo != null) return;

            _canvasGo = new GameObject(_canvasName, typeof(RectTransform));
            UnityEngine.Object.DontDestroyOnLoad(_canvasGo);
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32700;   // above the mods' own panels
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _canvasGo.AddComponent<GraphicRaycaster>();

            _cardGo = new GameObject(ToastStack.CardName, typeof(RectTransform));
            _cardGo.transform.SetParent(_canvasGo.transform, false);
            _card = (RectTransform)_cardGo.transform;
            _card.anchorMin = _card.anchorMax = new Vector2(1f, 1f);
            _card.pivot = new Vector2(1f, 1f);
            _card.sizeDelta = new Vector2(380f, H);
            _card.anchoredPosition = new Vector2(380f + Inset, -Inset);
            _group = _cardGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _bg = _cardGo.AddComponent<RoundedRectGraphic>();
            _bg.Radius = 10f;
            _bg.color = _theme.Background;
            _bg.BorderWidth = 1f;
            _bg.BorderColor = _theme.Border;
            _bg.raycastTarget = true;
            Click.Attach(_cardGo, OnCardClick);
            var hov = _cardGo.AddComponent<Hover>();
            hov.OnEnter = () => _hovered = true;
            hov.OnExit = () => _hovered = false;

            // accent pip down the left edge — cheap identity marker, no glyph needed
            var pipGo = new GameObject("Pip", typeof(RectTransform));
            pipGo.transform.SetParent(_cardGo.transform, false);
            var pr = (RectTransform)pipGo.transform;
            pr.anchorMin = new Vector2(0f, 0f); pr.anchorMax = new Vector2(0f, 1f);
            pr.pivot = new Vector2(0f, 0.5f);
            pr.offsetMin = new Vector2(10f, 12f); pr.offsetMax = new Vector2(14f, -12f);
            _pip = pipGo.AddComponent<RoundedRectGraphic>();
            _pip.Radius = 2f;
            _pip.color = _theme.Accent;
            _pip.raycastTarget = false;

            _title = Label(-32f, -10f, 15f, _theme.Text);
            _hint = Label(-52f, -32f, 12.5f, _theme.TextMuted);

            BuildProgressBar();
            BuildClose();
            _cardGo.SetActive(false);
        }

        private TextMeshProUGUI Label(float bottom, float top, float size, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(_cardGo.transform, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.offsetMin = new Vector2(26f, bottom); r.offsetMax = new Vector2(-40f, top);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (_theme.Font != null) t.font = _theme.Font;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.color = color;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            return t;
        }

        private void BuildProgressBar()
        {
            _barGo = new GameObject("Bar", typeof(RectTransform));
            _barGo.transform.SetParent(_cardGo.transform, false);
            var br = (RectTransform)_barGo.transform;
            br.anchorMin = new Vector2(0f, 0f); br.anchorMax = new Vector2(1f, 0f);
            br.pivot = new Vector2(0.5f, 0f);
            br.offsetMin = new Vector2(26f, 8f); br.offsetMax = new Vector2(-16f, 11f);
            var track = _barGo.AddComponent<RoundedRectGraphic>();
            track.Radius = 1.5f;
            track.color = new Color(1f, 1f, 1f, 0.10f);
            track.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(_barGo.transform, false);
            _barFillRect = (RectTransform)fillGo.transform;
            _barFillRect.anchorMin = new Vector2(0f, 0f); _barFillRect.anchorMax = new Vector2(0f, 1f);
            _barFillRect.pivot = new Vector2(0f, 0.5f);
            _barFillRect.offsetMin = Vector2.zero; _barFillRect.offsetMax = Vector2.zero;
            _barFill = fillGo.AddComponent<RoundedRectGraphic>();
            _barFill.Radius = 1.5f;
            _barFill.color = _theme.Accent;
            _barFill.raycastTarget = false;
            _barGo.SetActive(false);
        }

        private void BuildClose()
        {
            var go = new GameObject("Close", typeof(RectTransform));
            go.transform.SetParent(_cardGo.transform, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(1f, 1f);
            r.anchoredPosition = new Vector2(-8f, -8f);
            r.sizeDelta = new Vector2(22f, 22f);
            _closeGroup = go.AddComponent<CanvasGroup>();
            _closeGroup.alpha = 0f;
            _closeGroup.blocksRaycasts = false;
            var bg = go.AddComponent<RoundedRectGraphic>();
            bg.Radius = 4f;
            bg.color = new Color(1f, 1f, 1f, 0f);
            bg.raycastTarget = true;
            _closeX = go.AddComponent<TextMeshProUGUI>();
            if (_theme.Font != null) _closeX.font = _theme.Font;
            _closeX.text = "×";
            _closeX.fontSize = 16f;
            _closeX.alignment = TextAlignmentOptions.Center;
            _closeX.color = _theme.TextMuted;
            _closeX.raycastTarget = false;
            Click.Attach(go, Hide);
            var hov = go.AddComponent<Hover>();
            hov.OnEnter = () => { bg.color = _theme.CloseHover; _closeX.color = Color.white; };
            hov.OnExit = () => { bg.color = new Color(1f, 1f, 1f, 0f); _closeX.color = _theme.TextMuted; };
        }

        public void Dispose()
        {
            if (_canvasGo != null) UnityEngine.Object.Destroy(_canvasGo);
            _canvasGo = null; _cardGo = null; _card = null; _group = null; _closeGroup = null;
            _bg = null; _pip = null; _barFill = null; _title = null; _hint = null; _closeX = null;
            _barGo = null; _barFillRect = null;
            _visible = false; _shownKey = null; _hovered = false; _slotY = 0f;
        }

        private class Click : MonoBehaviour, IPointerClickHandler
        {
            public Action OnClick;
            public static void Attach(GameObject go, Action a) => go.AddComponent<Click>().OnClick = a;
            public void OnPointerClick(PointerEventData e) { if (OnClick != null) OnClick(); }
        }

        private class Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public Action OnEnter, OnExit;
            public void OnPointerEnter(PointerEventData e) { if (OnEnter != null) OnEnter(); }
            public void OnPointerExit(PointerEventData e) { if (OnExit != null) OnExit(); }
        }
    }
}
