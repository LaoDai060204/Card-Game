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
            SetLabel(Find(root, suffix), value);
        }
        internal static void SetLabel(Transform node, string value)
        {
            if (node == null) return;
            // Toggle faces contain separate On/Off labels; update both through
            // the existing template API so the old face cannot reappear.
            var selection = node.GetComponent<SelectionButton>();
            if (selection != null) selection.SetButtonText(value ?? string.Empty);
            var text = node.Find("BS_Caption")?.GetComponent<TextMeshProUGUI>()
                ?? node.GetComponent<TextMeshProUGUI>() ?? node.GetComponentInChildren<TextMeshProUGUI>(true);
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
            if (field != null)
            {
                field.enabled = true;
                field.interactable = true;
                field.readOnly = false;
                field.richText = false;
                // These template fields intentionally have no targetGraphic and
                // every normal background/text graphic has raycasts disabled.
                // Their old controller supplied focus; the native controller
                // must restore a full-field pointer target before it can type.
                var background = field.targetGraphic;
                if (background == null)
                    background = field.transform.Find("Out")?.GetComponent<Graphic>()
                        ?? field.GetComponent<Graphic>();
                if (background == null)
                    background = field.GetComponentsInChildren<Image>(true)
                        .FirstOrDefault(image => image.GetComponentInParent<Button>() == null);
                if (background == null)
                {
                    // Minimal fields without template chrome still need a hit
                    // surface. Keep it on the existing field, not another input.
                    var surface = field.gameObject.AddComponent<Image>();
                    surface.color = new Color(0, 0, 0, 0.001f);
                    background = surface;
                }
                background.enabled = true;
                background.raycastTarget = true;
                field.targetGraphic = background;
                if (field.textComponent != null) field.textComponent.raycastTarget = false;
                if (field.placeholder != null) field.placeholder.raycastTarget = false;
                if (value != null) field.SetTextWithoutNotify(value);
            }
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

        // The original card form is expensive to clone. Deck screens keep these
        // handles and change only the identity/callback when a visible slot changes.
        internal sealed class CardSlot
        {
            internal readonly Button Button;
            internal readonly RawImage Image;
            private readonly TextMeshProUGUI label;
            private readonly IBattleNativeSession session;
            private Action click;
            internal string Id { get; private set; }
            internal int Index;

            internal CardSlot(Button button, RawImage image, TextMeshProUGUI label, IBattleNativeSession session)
            {
                Button = button; Image = image; this.label = label; this.session = session;
                // Keep template hover/chrome but let one Unity Button own click
                // and Submit. A nested legacy selection must not dispatch twice.
                foreach (var selection in button.GetComponentsInChildren<SelectionButton>(true))
                    selection.SetClickEvent(() => { });
                button.onClick.RemoveAllListeners(); button.onClick.AddListener(Invoke);
            }

            private void Invoke()
            {
                if (Button != null && Button.gameObject.activeInHierarchy && Button.interactable)
                    click?.Invoke();
            }

            internal void Bind(string id, Action action = null)
            {
                if (action != null) click = action;
                bool changed = !string.Equals(Id, id, StringComparison.Ordinal);
                Id = id;
                Button.gameObject.name = "BS_Card_" + id;
                if (changed)
                {
                    label.text = id;
                    Image.texture = null;
                    Image.uvRect = new Rect(0, 0, 1, 1);
                    Image.GetComponent<AspectRatioFitter>().aspectRatio = 675f / 983;
                }
                Button.gameObject.SetActive(true);
                // A hidden card's texture may have been evicted by the session.
                // LoadArt also updates its own target-generation guard, so a
                // late result for a previous ID cannot paint the reused slot.
                if (changed || Image.texture == null) session.LoadArt(id, Image);
            }

            internal void Hide()
            {
                Button.gameObject.name = "BS_PooledCard";
                Button.gameObject.SetActive(false);
            }
        }

        internal static Button Card(Transform parent, string id, Action click, IBattleNativeSession session, RectTransform template = null)
        {
            var slot = CreateCardSlot(parent, session, template);
            slot.Bind(id, click);
            return slot.Button;
        }

        internal static CardSlot CreateCardSlot(Transform parent, IBattleNativeSession session, RectTransform template = null)
        {
            GameObject go;
            if (template != null)
            {
                go = UnityEngine.Object.Instantiate(template.gameObject, parent, false); go.name = "BS_PooledCard";
                go.SetActive(true); SetLayer(go.transform, parent.gameObject.layer);
                foreach (var node in go.GetComponentsInChildren<Transform>(true))
                    if (node.name.StartsWith("Icon", StringComparison.Ordinal) || node.name == "PickupCursor" || node.name == "CardPoint") node.gameObject.SetActive(false);
            }
            else go = Rect(parent, "BS_PooledCard").gameObject;
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
            var button = go.GetComponent<Button>() ?? go.AddComponent<Button>();
            button.targetGraphic = image; image.raycastTarget = true;
            var label = Text(go.transform, "BS_CardCode", string.Empty, 14); Place(label.rectTransform, 0, 0, 1, 0.12f);
            return new CardSlot(button, image, label, session);
        }
        internal static void Grid(RectTransform content, int columns, float width = 110f, float height = 166f)
        {
            if (content == null) return;
            var grid = content.GetComponent<GridLayoutGroup>() ?? content.gameObject.AddComponent<GridLayoutGroup>();
            // The original virtualized collection serializes both components
            // disabled. Reusing them without enabling them stacks all cards at
            // the template's original position instead of laying out a grid.
            grid.enabled = true;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = Math.Max(1, columns);
            grid.cellSize = new Vector2(Math.Max(1f, width), Math.Max(1f, height)); grid.spacing = new Vector2(5, 6);
            grid.childAlignment = TextAnchor.UpperLeft;
            var fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.enabled = true;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            content.pivot = new Vector2(content.pivot.x, 1f);
        }
    }
}
