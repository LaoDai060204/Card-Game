using System;
using UnityEngine;
using UnityEngine.UI;

namespace LaoDai.BattleSpirits.Integration
{
    /// <summary>
    /// The title lives on Boot's existing Canvas. Main supplies only the chosen
    /// wallpaper beneath it until the player accepts the start prompt.
    /// </summary>
    public sealed class BattleTitleScreen : MonoBehaviour
    {
        private static readonly Color Cyan = new Color(0.25f, 0.88f, 1f, 1f);
        private static readonly Color Magenta = new Color(0.84f, 0.33f, 0.72f, 1f);
        private static readonly Color Silver = new Color(0.86f, 0.90f, 0.96f, 1f);
        private static readonly Color Background = new Color(0.015f, 0.022f, 0.04f, 1f);

        private CanvasGroup titleGroup;
        private Image backdrop;
        private Text status;
        private Slider progress;
        private Text prompt;
        private Text inputHint;
        private Image leftLight;
        private Image rightLight;
        private RectTransform wordmark;
        private float elapsed;
        private float readyElapsed;
        private float revealProgress;
        private bool ready;
        private bool failed;

        public static BattleTitleScreen Create(Canvas canvas, Text status, Slider progress)
        {
            if (canvas == null) throw new ArgumentNullException(nameof(canvas));
            if (status == null || status.font == null)
                throw new ArgumentException("Boot needs its existing Vietnamese-capable font.", nameof(status));
            if (progress == null) throw new ArgumentNullException(nameof(progress));

            var existing = canvas.GetComponentInChildren<BattleTitleScreen>(true);
            if (existing != null) return existing;

            var owner = new GameObject("BattleSpiritsTitle", typeof(RectTransform), typeof(CanvasGroup));
            owner.layer = canvas.gameObject.layer;
            var rect = owner.GetComponent<RectTransform>();
            rect.SetParent(canvas.transform, false);
            Stretch(rect);
            var view = owner.AddComponent<BattleTitleScreen>();
            view.Build(canvas, status, progress);
            return view;
        }

        private void Build(Canvas canvas, Text loadingStatus, Slider loadingProgress)
        {
            titleGroup = GetComponent<CanvasGroup>();
            titleGroup.interactable = false;
            // The original full-screen Image owns the input curtain, even while
            // its color fades. Decorative title children never intercept input.
            titleGroup.blocksRaycasts = false;
            if (canvas.transform.childCount == 0)
                throw new InvalidOperationException("Boot's full-screen backdrop is missing.");
            backdrop = canvas.transform.GetChild(0).GetComponent<Image>();
            if (backdrop == null)
                throw new InvalidOperationException("Boot's first Canvas child must be its full-screen Image.");
            backdrop.sprite = null;
            backdrop.raycastTarget = true;
            backdrop.color = Background;

            status = loadingStatus;
            progress = loadingProgress;
            Font font = status.font;

            // These quiet angular strokes frame the wallpaper instead of using
            // a second application screen or a large panel over the main menu.
            CreateLine("UpperCyanRail", transform, new Vector2(0.08f, 0.90f), new Vector2(0.39f, 0.90f),
                1f, Tint(Cyan, 0.30f));
            CreateLine("UpperSilverRail", transform, new Vector2(0.40f, 0.90f), new Vector2(0.58f, 0.90f),
                1f, Tint(Silver, 0.16f));
            CreateLine("UpperMagentaRail", transform, new Vector2(0.59f, 0.90f), new Vector2(0.92f, 0.90f),
                1f, Tint(Magenta, 0.30f));
            CreateLine("FooterRail", transform, new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.10f),
                1f, Tint(Silver, 0.18f));

            wordmark = CreateRect("Wordmark", transform, new Vector2(0.12f, 0.40f), new Vector2(0.88f, 0.79f));
            CreateLabel("Brand", wordmark, font, "L A O D A I", 19,
                new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.94f), Tint(Silver, 0.86f));
            CreateLabel("TitleBattle", wordmark, font, "B A T T L E", 66,
                new Vector2(0.05f, 0.53f), new Vector2(0.95f, 0.84f), Color.white, true);
            CreateLabel("TitleSpirits", wordmark, font, "SPIRITS", 126,
                new Vector2(0.02f, 0.14f), new Vector2(0.98f, 0.59f), Silver, true);
            CreateLabel("Ruleset", wordmark, font, "S T A N D A R D", 17,
                new Vector2(0.10f, 0.01f), new Vector2(0.90f, 0.12f), Tint(Silver, 0.80f));

            var emblem = CreateRect("CoreEmblem", transform, new Vector2(0.5f, 0.40f), new Vector2(0.5f, 0.40f));
            emblem.sizeDelta = new Vector2(48, 48);
            CreateDiamondEdge("CyanUpper", emblem, new Vector2(-9, 9), 45, Cyan);
            CreateDiamondEdge("CyanLower", emblem, new Vector2(-9, -9), -45, Cyan);
            CreateDiamondEdge("MagentaUpper", emblem, new Vector2(9, 9), -45, Magenta);
            CreateDiamondEdge("MagentaLower", emblem, new Vector2(9, -9), 45, Magenta);
            leftLight = CreateLine("LeftTitleLight", transform,
                new Vector2(0.27f, 0.40f), new Vector2(0.465f, 0.40f), 2f, Tint(Cyan, 0.70f));
            rightLight = CreateLine("RightTitleLight", transform,
                new Vector2(0.535f, 0.40f), new Vector2(0.73f, 0.40f), 2f, Tint(Magenta, 0.70f));

            prompt = CreateLabel("StartPrompt", transform, font, "NHẤN ĐỂ BẮT ĐẦU", 28,
                new Vector2(0.12f, 0.205f), new Vector2(0.88f, 0.27f), Color.white);
            inputHint = CreateLabel("InputHint", transform, font, "Nhấp chuột · Phím bất kỳ · Tay cầm A / Start", 17,
                new Vector2(0.12f, 0.16f), new Vector2(0.88f, 0.205f), Tint(Silver, 0.68f));
            prompt.gameObject.SetActive(false);
            inputHint.gameObject.SetActive(false);

            var credit = CreateLabel("IndependentProject", transform, font,
                "Dự án cộng đồng độc lập · LaoDai060204", 16,
                new Vector2(0.05f, 0.035f), new Vector2(0.76f, 0.08f), Tint(Silver, 0.64f));
            credit.alignment = TextAnchor.MiddleLeft;
            var version = CreateLabel("Version", transform, font, "v" + Application.version, 16,
                new Vector2(0.78f, 0.035f), new Vector2(0.95f, 0.08f), Tint(Silver, 0.64f));
            version.alignment = TextAnchor.MiddleRight;

            LayoutLoadingControls();
            ApplyVisuals();
        }

        private void LayoutLoadingControls()
        {
            var statusRect = status.GetComponent<RectTransform>();
            statusRect.SetParent(transform, false);
            SetBounds(statusRect, new Vector2(0.12f, 0.225f), new Vector2(0.88f, 0.28f));
            status.fontStyle = FontStyle.Normal;
            status.fontSize = 23;
            status.resizeTextForBestFit = true;
            status.resizeTextMinSize = 16;
            status.resizeTextMaxSize = 23;
            status.alignment = TextAnchor.MiddleCenter;
            status.supportRichText = false;
            status.color = Silver;
            status.raycastTarget = false;

            var progressRect = progress.GetComponent<RectTransform>();
            progressRect.SetParent(transform, false);
            SetBounds(progressRect, new Vector2(0.35f, 0.21f), new Vector2(0.65f, 0.21f));
            progressRect.sizeDelta = new Vector2(0, 5);
            progress.interactable = false;
            progress.transition = Selectable.Transition.None;
            if (progress.handleRect != null) progress.handleRect.gameObject.SetActive(false);
            foreach (var graphic in progress.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
                if (graphic is Image image)
                {
                    image.sprite = null;
                    image.color = progress.fillRect != null && image.transform == progress.fillRect
                        ? Tint(Cyan, 0.90f) : Tint(Silver, 0.14f);
                }
            }
        }

        public void ShowReady()
        {
            if (ready || failed) return;
            ready = true;
            readyElapsed = 0;
            status.gameObject.SetActive(false);
            progress.gameObject.SetActive(false);
            prompt.gameObject.SetActive(true);
            inputHint.gameObject.SetActive(true);
            ApplyVisuals();
        }

        public void SetRevealProgress(float value)
        {
            if (failed) return;
            revealProgress = float.IsNaN(value) ? 0 : Mathf.Clamp01(value);
            ApplyVisuals();
        }

        public void ShowFailure(string message)
        {
            failed = true;
            ready = false;
            revealProgress = 0;
            prompt.gameObject.SetActive(false);
            inputHint.gameObject.SetActive(false);
            progress.gameObject.SetActive(false);
            status.gameObject.SetActive(true);
            status.text = string.IsNullOrWhiteSpace(message)
                ? "Không thể khởi động. Hãy mở lại trò chơi."
                : message;
            SetBounds(status.GetComponent<RectTransform>(), new Vector2(0.12f, 0.16f), new Vector2(0.88f, 0.29f));
            ApplyVisuals();
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (float.IsNaN(unscaledDeltaTime) || float.IsInfinity(unscaledDeltaTime) || unscaledDeltaTime < 0)
                return;
            elapsed += unscaledDeltaTime;
            if (ready) readyElapsed += unscaledDeltaTime;
            ApplyVisuals();
        }

        private void ApplyVisuals()
        {
            if (titleGroup == null || backdrop == null) return;
            float visible = 1f - revealProgress;
            titleGroup.alpha = visible;
            float wallpaperReveal = ready && !failed ? Mathf.Clamp01(readyElapsed / 0.75f) : 0;
            // Keep an imperceptible nonzero surface until Boot destroys the
            // curtain. Its CanvasRenderer must not cull the pointer blocker on
            // the last frame of the reveal.
            float backdropAlpha = (1f - wallpaperReveal * 0.55f) * visible;
            backdrop.color = Tint(Background, Mathf.Max(0.001f, backdropAlpha));
            backdrop.raycastTarget = true;

            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 1.8f);
            if (prompt != null) prompt.color = Tint(Color.white, 0.65f + 0.35f * pulse);
            if (leftLight != null) leftLight.color = Tint(Cyan, 0.35f + 0.40f * pulse);
            if (rightLight != null) rightLight.color = Tint(Magenta, 0.35f + 0.40f * (1f - pulse));
            if (wordmark != null)
                wordmark.localScale = Vector3.one * (1f + 0.003f * Mathf.Sin(elapsed * 0.4f));
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var owner = new GameObject(name, typeof(RectTransform));
            owner.layer = parent.gameObject.layer;
            var rect = owner.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetBounds(rect, min, max);
            return rect;
        }

        private static Text CreateLabel(string name, Transform parent, Font font, string value, int fontSize,
            Vector2 min, Vector2 max, Color color, bool bold = false)
        {
            var rect = CreateRect(name, parent, min, max);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.text = value;
            label.fontSize = fontSize;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Math.Max(12, fontSize / 2);
            label.resizeTextMaxSize = fontSize;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.color = color;
            return label;
        }

        private static Image CreateLine(string name, Transform parent, Vector2 min, Vector2 max,
            float thickness, Color color)
        {
            var rect = CreateRect(name, parent, min, max);
            rect.sizeDelta = new Vector2(0, thickness);
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = color;
            return image;
        }

        private static void CreateDiamondEdge(string name, Transform parent, Vector2 position, float angle, Color color)
        {
            var image = CreateLine(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 2f, color);
            var rect = image.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(25, 2);
            rect.anchoredPosition = position;
            rect.localRotation = Quaternion.Euler(0, 0, angle);
        }

        private static void Stretch(RectTransform rect) => SetBounds(rect, Vector2.zero, Vector2.one);

        private static void SetBounds(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static Color Tint(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);
    }
}
