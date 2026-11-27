using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MDPro3.BattleSpirits
{
    public sealed partial class StandardFrontend
    {
        private static RectTransform Node(Transform parent, string name, float left, float bottom, float right, float top)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.layer = parent.gameObject.layer;
            var rect = item.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Place(rect, left, bottom, right, top);
            return rect;
        }

        private static void Place(RectTransform rect, float left, float bottom, float right, float top, int padding = 0)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        private static RectTransform Panel(Transform parent, string name, float left, float bottom, float right, float top, Color color)
        {
            var rect = Node(parent, name, left, bottom, right, top);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private Text Label(Transform parent, string value, float left, float bottom, float right, float top,
            int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var rect = Node(parent, "Text", left, bottom, right, top);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.supportRichText = false;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private Button Button(Transform parent, string title, float left, float bottom, float right, float top,
            Action click, bool primary = false)
        {
            var rect = Panel(parent, title, left, bottom, right, top, primary ? Glacier : ControlColor);
            var background = rect.GetComponent<Image>();
            background.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.interactable = click != null;
            var colors = button.colors;
            colors.highlightedColor = new Color32(207, 233, 246, 255);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color32(155, 188, 205, 255);
            button.colors = colors;
            if (click != null)
                button.onClick.AddListener(() =>
                {
                    if (!interfaceMuted && audioSource != null && clickTone != null)
                        audioSource.PlayOneShot(clickTone);
                    click();
                });
            Label(rect, title, .04f, .05f, .96f, .95f, 25, primary ? CanvasColor : Ink, TextAnchor.MiddleCenter);
            return button;
        }

        private InputField Input(Transform parent, string value, float left, float bottom, float right, float top)
        {
            var rect = Panel(parent, "Input", left, bottom, right, top, PanelColor);
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            var input = rect.gameObject.AddComponent<InputField>();
            input.targetGraphic = image;
            input.textComponent = Label(rect, "", .035f, .05f, .965f, .95f, 27, Ink);
            input.characterLimit = 100;
            input.text = value;
            return input;
        }

        private static RectTransform Scroll(Transform parent, string title, float left, float bottom, float right, float top)
        {
            var area = Panel(parent, title, left, bottom, right, top, PanelColor);
            area.GetComponent<Image>().raycastTarget = true;
            var scroll = area.gameObject.AddComponent<ScrollRect>();
            var viewport = Node(area, "Viewport", .016f, .015f, .984f, .985f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Node(viewport, "Rows", 0, 1, 1, 1);
            content.pivot = new Vector2(.5f, 1);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlWidth = layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            var size = content.gameObject.AddComponent<ContentSizeFitter>();
            size.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 38;
            return content;
        }

        private Button Row(Transform column, string text, Action click, int height = 58)
        {
            var item = Button(column, text, 0, 0, 1, 1, click);
            item.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            var label = item.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            return item;
        }

        private static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        private static void Focus(Button button)
        {
            if (button != null && EventSystem.current != null) button.Select();
        }

        private void Confirm(string title, string accept, Action confirmed)
        {
            var controls = screen.gameObject.AddComponent<CanvasGroup>();
            controls.interactable = false;
            controls.blocksRaycasts = false;
            var overlay = Panel(surface, "Confirmation", 0, 0, 1, 1, new Color(0, 0, 0, .70f));
            overlay.GetComponent<Image>().raycastTarget = true;
            var dialog = Panel(overlay, title, .29f, .34f, .71f, .66f, PanelColor);
            Label(dialog, title, .08f, .52f, .92f, .91f, 35, Ink);
            var previousBack = back;
            var previousFocus = EventSystem.current?.currentSelectedGameObject;
            void Close()
            {
                overlay.gameObject.SetActive(false);
                Destroy(overlay.gameObject);
                controls.interactable = true;
                controls.blocksRaycasts = true;
                Destroy(controls);
                back = previousBack;
                EventSystem.current?.SetSelectedGameObject(previousFocus);
            }
            back = Close;
            Button(dialog, accept, .08f, .13f, .52f, .39f, () => { Close(); confirmed(); }, true);
            Focus(Button(dialog, "Cancel", .57f, .13f, .92f, .39f, Close));
        }

        private static void Picture(Transform parent, Texture texture, float left, float bottom, float right, float top)
        {
            if (texture == null) return;
            var slot = Node(parent, "Picture bounds", left, bottom, right, top);
            var image = Node(slot, "Picture", 0, 0, 1, 1).gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = texture.width / (float)texture.height;
        }
    }
}
