using System;
using System.Linq;
using LaoDai.BattleSpirits.Visuals;
using MDPro3;
using MDPro3.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LaoDai.BattleSpirits.NativeUI
{
    internal static class BattleNativeWidgets
    {
        internal static Transform Find(Transform root, string suffix)
        {
            if (root == null) return null;
            var direct = root.Find(suffix);
            if (direct != null) return direct;
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => Path(item, root).EndsWith("/" + suffix, StringComparison.Ordinal)
                || Path(item, root) == suffix);
        }
        private static string Path(Transform item, Transform root)
        {
            string value = item.name;
            while (item.parent != null && item.parent != root) { item = item.parent; value = item.name + "/" + value; }
            return value;
        }
        internal static T Component<T>(Transform root, string suffix) where T : UnityEngine.Component
            => Find(root, suffix)?.GetComponent<T>();
        internal static void Active(Transform root, string suffix, bool value)
        { var target = Find(root, suffix); if (target != null) target.gameObject.SetActive(value); }
        internal static void Label(Transform root, string suffix, string value)
        {
            var node = Find(root, suffix); if (node == null) return;
            // Toggle faces contain separate On/Off labels; update both through
            // the existing template API so the old face cannot reappear.
            var selection = node.GetComponent<SelectionButton>();
            if (selection != null) selection.SetButtonText(value ?? string.Empty);
            var text = node.GetComponent<TextMeshProUGUI>() ?? node.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null) { text.text = value ?? string.Empty; text.richText = false; return; }
            var old = node.GetComponent<Text>() ?? node.GetComponentInChildren<Text>(true);
            if (old != null) { old.text = value ?? string.Empty; old.supportRichText = false; }
        }
        internal static void Bind(Transform root, string suffix, Action click, string label = null)
        {
            var node = Find(root, suffix); if (node == null) return;
            if (label != null) Label(root, suffix, label);
            var toggle = node.GetComponent<SelectionToggle>();
            if (toggle != null)
            {
                toggle.SetToggleOnEvent(() => click());
                toggle.SetToggleOffEvent(() => click());
                toggle.SetSubmitEvent(() => click());
                return;
            }
            var selection = node.GetComponent<SelectionButton>();
            if (selection != null) { selection.SetClickEvent(() => click()); return; }
            var button = node.GetComponent<Button>() ?? node.gameObject.AddComponent<Button>();
            button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => click());
        }
        internal static void Interactable(Transform root, string suffix, bool value)
        { var select = Component<Selectable>(root, suffix); if (select != null) select.interactable = value; }
        internal static TMP_InputField Input(Transform root, string suffix, string value = null)
        {
            var field = Component<TMP_InputField>(root, suffix);
            if (field != null) { field.richText = false; if (value != null) field.SetTextWithoutNotify(value); }
            return field;
        }
        internal static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = parent.gameObject.layer;
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.localScale = Vector3.one; return rect;
        }
        internal static void Place(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom); rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        internal static TextMeshProUGUI Text(Transform parent, string name, string text, int size = 22)
        {
            var rect = Rect(parent, name); Place(rect, 0, 0, 1, 1);
            var result = rect.gameObject.AddComponent<TextMeshProUGUI>();
            result.font = Program.instance.ui_.tmpFont; result.fontSize = size;
            result.color = Color.white; result.text = text; result.richText = false;
            result.alignment = TextAlignmentOptions.Center; result.enableWordWrapping = true; result.raycastTarget = false;
            return result;
        }
        internal static Button Button(Transform parent, string name, string text, Action click)
        {
            var rect = Rect(parent, name); var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>(); BattleTheme.StyleButton(button, image);
            if (click != null) button.onClick.AddListener(() => click());
            Text(rect, "Label", text); return button;
        }
        internal static void ClearGenerated(Transform parent)
        {
            if (parent == null) return;
            foreach (Transform child in parent)
                if (child.name.StartsWith("BS_", StringComparison.Ordinal)) { child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject); }
        }
        internal static void SetLayer(Transform root, int layer)
        { foreach (var node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = layer; }

        internal static Button Card(Transform parent, string id, Action click, IBattleNativeSession session, RectTransform template = null)
        {
            GameObject go;
            if (template != null)
            {
                go = UnityEngine.Object.Instantiate(template.gameObject, parent, false); go.name = "BS_Card_" + id;
                go.SetActive(true); SetLayer(go.transform, parent.gameObject.layer);
                foreach (var selection in go.GetComponentsInChildren<SelectionButton>(true)) selection.SetClickEvent(() => click());
                foreach (var node in go.GetComponentsInChildren<Transform>(true))
                    if (node.name.StartsWith("Icon", StringComparison.Ordinal) || node.name == "PickupCursor" || node.name == "CardPoint") node.gameObject.SetActive(false);
            }
            else go = Rect(parent, "BS_Card_" + id).gameObject;
            var image = go.GetComponentsInChildren<RawImage>(true).FirstOrDefault();
            // Card slots follow screen/layout sizes, while the official face
            // retains its printed aspect. A separate art area also keeps the
            // fitter from overlapping the card-code label underneath it.
            var artArea = Rect(go.transform, "BS_CardArtworkSlot"); Place(artArea, 0, 0.12f, 1, 1);
            if (image == null) image = Rect(artArea, "CardArtwork").gameObject.AddComponent<RawImage>();
            else image.rectTransform.SetParent(artArea, false);
            Place(image.rectTransform, 0, 0, 1, 1);
            var aspect = image.GetComponent<AspectRatioFitter>() ?? image.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 675f / 983;
            image.color = Color.white; image.raycastTarget = false;
            session.LoadArt(id, image);
            var button = go.GetComponent<Button>() ?? go.AddComponent<Button>();
            button.targetGraphic = image; image.raycastTarget = true;
            button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => click());
            var label = Text(go.transform, "BS_CardCode", id, 14); Place(label.rectTransform, 0, 0, 1, 0.12f);
            return button;
        }
        internal static void Grid(RectTransform content, int columns, float width = 110f, float height = 166f)
        {
            if (content == null) return;
            var grid = content.GetComponent<GridLayoutGroup>() ?? content.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = columns;
            grid.cellSize = new Vector2(width, height); grid.spacing = new Vector2(5, 6);
            grid.childAlignment = TextAnchor.UpperLeft;
            var fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }
}
