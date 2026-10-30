using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LaoDai.BattleSpirits.Presentation
{
    // Reuses the loaded MDPro3 font and button artwork without copying any of
    // the Yu-Gi-Oh control scripts or their serialized callbacks.
    internal static class BattleUiTheme
    {
        internal static readonly Color PanelColor = new Color(0.015f, 0.035f, 0.065f, 0.94f);
        internal static readonly Color Accent = new Color(0.55f, 0.9f, 1f, 1f);
        internal static readonly Color Muted = new Color(0.66f, 0.76f, 0.84f, 1f);
        private static Sprite buttonSprite;
        private static Material buttonMaterial;
        private static Image.Type buttonType = Image.Type.Simple;
        private static bool styleCaptured;

        internal static void Capture(Transform source)
        {
            if (source == null) return;
            Image selected = null;
            float best = float.MinValue;
            foreach (var image in source.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == null || IsBattleWidget(image.transform)) continue;
                string name = image.name.ToLowerInvariant();
                if (name.Contains("hover") || name.Contains("cursor") || name.Contains("icon") || name.Contains("arrow"))
                    continue;
                float width = Mathf.Abs(image.rectTransform.rect.width);
                float height = Mathf.Abs(image.rectTransform.rect.height);
                if (width < 90f || height < 15f || height > 180f) continue;
                float score = width / Mathf.Max(height, 1f);
                if (name.Contains("base") || name.Contains("background") || name.Contains("frame")) score += 10f;
                if (image.GetComponentInParent<MDPro3.UI.SelectionButton>() != null) score += 20f;
                if (score <= best) continue;
                selected = image;
                best = score;
            }
            if (selected != null)
            {
                buttonSprite = selected.sprite;
                buttonMaterial = selected.material;
                buttonType = selected.type;
            }
            styleCaptured = true;
        }

        private static bool IsBattleWidget(Transform transform)
        {
            while (transform != null)
            {
                if (transform.name.StartsWith("BattleSpirits_", StringComparison.Ordinal)
                    || transform.name.StartsWith("BS_", StringComparison.Ordinal)) return true;
                transform = transform.parent;
            }
            return false;
        }

        private static void EnsureStyle()
        {
            if (!styleCaptured && MDPro3.Program.instance != null && MDPro3.Program.instance.ui_ != null)
                Capture(MDPro3.Program.instance.ui_.transform);
        }

        private static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        internal static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        internal static void Stretch(RectTransform rect, float inset = 0f)
        {
            SetRect(rect, Vector2.zero, Vector2.one, new Vector2(inset, inset), new Vector2(-inset, -inset));
        }

        internal static RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var rect = Rect(parent, name);
            SetRect(rect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return rect;
        }

        internal static TextMeshProUGUI Text(Transform parent, string name, string text, int size,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var rect = Rect(parent, name);
            Stretch(rect);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (MDPro3.Program.instance != null && MDPro3.Program.instance.ui_ != null
                && MDPro3.Program.instance.ui_.tmpFont != null)
                label.font = MDPro3.Program.instance.ui_.tmpFont;
            else if (TMP_Settings.defaultFontAsset != null)
                label.font = TMP_Settings.defaultFontAsset;
            label.text = text ?? string.Empty;
            label.fontSize = size;
            label.color = Color.white;
            label.alignment = align;
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            label.richText = true;
            return label;
        }

        internal static Button Button(Transform parent, string label, UnityAction action)
        {
            EnsureStyle();
            var rect = Rect(parent, "BattleSpirits_Button");
            rect.sizeDelta = new Vector2(220f, 44f);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = buttonSprite != null ? Color.white : new Color(0.035f, 0.13f, 0.19f, 1f);
            if (buttonSprite != null)
            {
                image.sprite = buttonSprite;
                image.type = buttonType;
                image.material = buttonMaterial;
            }
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.65f, 0.95f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.4f, 0.75f, 0.85f, 1f);
            colors.disabledColor = new Color(0.4f, 0.45f, 0.5f, 0.65f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            if (action != null) button.onClick.AddListener(action);
            var text = Text(rect, "Label", label, 22, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, 6f);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 42f;
            layout.preferredHeight = 44f;
            return button;
        }

        internal static TMP_InputField Input(Transform parent, string placeholder, string value,
            UnityAction<string> onChanged = null)
        {
            var rect = Panel(parent, "BattleSpirits_Input", Vector2.zero, Vector2.one, new Color(0.025f, 0.08f, 0.12f, 1f));
            rect.sizeDelta = new Vector2(280f, 42f);
            var input = rect.gameObject.AddComponent<TMP_InputField>();
            var viewport = Rect(rect, "Viewport");
            Stretch(viewport, 7f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = Text(viewport, "Value", string.Empty, 22, TextAlignmentOptions.MidlineLeft);
            var hint = Text(viewport, "Placeholder", placeholder, 22, TextAlignmentOptions.MidlineLeft);
            hint.color = Muted;
            input.targetGraphic = rect.GetComponent<Image>();
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = hint;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.richText = false;
            input.text = value ?? string.Empty;
            if (onChanged != null) input.onValueChanged.AddListener(onChanged);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 40f;
            layout.preferredHeight = 42f;
            return input;
        }

        internal static ScrollRect Scroll(Transform parent, string name, out RectTransform content)
        {
            var root = Rect(parent, name);
            Stretch(root);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = Panel(root, "Viewport", Vector2.zero, Vector2.one, new Color(0, 0, 0, 0.01f));
            viewport.gameObject.AddComponent<RectMask2D>();
            content = Rect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 5f;
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35f;
            return scroll;
        }

        internal static TMP_Dropdown Dropdown(Transform parent, string name, IReadOnlyList<string> options,
            UnityAction<int> onChanged = null)
        {
            var root = Panel(parent, name, Vector2.zero, Vector2.one, new Color(0.025f, 0.08f, 0.12f, 1f));
            root.sizeDelta = new Vector2(230f, 42f);
            var dropdown = root.gameObject.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = root.GetComponent<Image>();
            dropdown.captionText = Text(root, "Caption", string.Empty, 20, TextAlignmentOptions.MidlineLeft);
            SetRect(dropdown.captionText.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 3), new Vector2(-30, -3));
            var arrow = Text(root, "Arrow", "▼", 16, TextAlignmentOptions.Center);
            SetRect(arrow.rectTransform, new Vector2(1, 0), Vector2.one, new Vector2(-27, 0), new Vector2(-3, 0));
            var template = Panel(root, "Template", new Vector2(0, 0), new Vector2(1, 0), PanelColor);
            template.pivot = new Vector2(0.5f, 1f);
            template.sizeDelta = new Vector2(0, 230f);
            var scroll = Scroll(template, "Options", out var content);
            var item = Rect(content, "Item");
            item.sizeDelta = new Vector2(0, 38f);
            var itemImage = item.gameObject.AddComponent<Image>();
            itemImage.color = new Color(0.03f, 0.1f, 0.15f, 1f);
            var toggle = item.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = itemImage;
            var itemLabel = Text(item, "Label", string.Empty, 20, TextAlignmentOptions.MidlineLeft);
            SetRect(itemLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 2), new Vector2(-6, -2));
            var layout = item.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 38f;
            dropdown.template = template;
            dropdown.itemText = itemLabel;
            dropdown.options.Clear();
            if (options != null)
                for (int i = 0; i < options.Count; i++) dropdown.options.Add(new TMP_Dropdown.OptionData(options[i]));
            template.gameObject.SetActive(false);
            dropdown.RefreshShownValue();
            if (onChanged != null) dropdown.onValueChanged.AddListener(onChanged);
            return dropdown;
        }
    }
}
