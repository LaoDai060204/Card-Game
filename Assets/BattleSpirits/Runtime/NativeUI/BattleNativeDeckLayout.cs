using System;
using LaoDai.BattleSpirits.Visuals;
using MDPro3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LaoDai.BattleSpirits.NativeUI
{
    // Adapt the existing MDPro3 form after its old virtual-list/layout controllers
    // have been removed. No extra Canvas or replacement application screen.
    internal sealed class BattleNativeDeckLayout
    {
        private const string Collection = "CardCollectionView/Root/Window";
        private const string Deck = "DeckView/Root/Window";
        private const string Detail = "CardDetailView/Root/Window/Content";
        internal const string DensityConfigKey = "BattleSpiritsDeckDensity";
        private static readonly string[] DensityValues = { "Compact", "Medium", "Large" };
        private static readonly string[] DensityLabels = { "GỌN", "VỪA", "LỚN" };
        private static readonly float[] DensityWidths = { 126, 150, 180 };
        private readonly Transform root;
        private readonly RectTransform area, collection, deck, outerDeck, deckSection;
        private readonly GridLayoutGroup collectionGrid, deckGrid;
        private readonly TextMeshProUGUI pageLabel;
        private readonly TextMeshProUGUI densityCaption;
        private readonly Transform[] tabOn = new Transform[3], tabOff = new Transform[3];
        private readonly GameObject[] tabUnderline = new GameObject[3];
        private readonly TextMeshProUGUI[] tabCaptions = new TextMeshProUGUI[3];
        private readonly Button[] tabButtons = new Button[3];
        private float lastWidth, lastHeight;
        private int deckCount, density;

        internal BattleNativeDeckLayout(Transform root, RectTransform collection, RectTransform deck)
        {
            this.root = root; this.collection = collection; this.deck = deck;
            density = ReadDensity(Config.Get(DensityConfigKey, "Medium"));
            area = Rect("SafeArea") ?? root as RectTransform;
            foreach (string panel in new[] { "CardDetailView", "DeckView", "CardCollectionView", "DeckEditHeader" })
            { Frame(panel, 0, 0, 1, 1); Frame(panel + "/Root", 0, 0, 1, 1); }
            Frame("CardDetailView/Root/Window", .012f, .055f, .245f, .89f);
            Frame(Deck, .256f, .055f, .63f, .89f);
            Frame(Collection, .642f, .055f, .988f, .89f);
            Hide("DeckEditOverHeader"); Hide("Footer");
            BuildHeader(); BuildCollection(); BuildDetail(); BuildDeckHeader();

            // The deck's old controller sized the outer ScrollRect content.
            // Keep that hierarchy, but size it explicitly from the active grid.
            outerDeck = Rect(Deck + "/Scroll View/Viewport/Content");
            deckSection = Rect(Deck + "/Scroll View/Viewport/Content/MainDeckView");
            Top(outerDeck, 0); Top(deckSection, 0); Top(deck, -44);
            collectionGrid = collection.GetComponent<GridLayoutGroup>();
            deckGrid = deck.GetComponent<GridLayoutGroup>();
            pageLabel = BattleNativeWidgets.Text(Rect(Collection), "BS_PageLabel", string.Empty, 22);
            BattleNativeWidgets.Place(pageLabel.rectTransform, .025f, .005f, .47f, .075f);
            pageLabel.alignment = TextAlignmentOptions.MidlineLeft;
            pageLabel.enableWordWrapping = false; pageLabel.overflowMode = TextOverflowModes.Ellipsis;
            var sizeButton = BattleNativeWidgets.Button(Rect(Collection), "BS_CardSize", "THẺ: " + DensityLabels[density], CycleDensity);
            BattleNativeWidgets.Place(sizeButton.transform as RectTransform, .49f, .005f, .76f, .075f);
            densityCaption = sizeButton.GetComponentInChildren<TextMeshProUGUI>(true);
            densityCaption.fontSize = 20; densityCaption.enableWordWrapping = false;
            Refresh();
        }

        private static int ReadDensity(string value)
        {
            for (int i = 0; i < DensityValues.Length; i++)
                if (string.Equals(value, DensityValues[i], StringComparison.OrdinalIgnoreCase)) return i;
            return 1;
        }
        private void CycleDensity()
        {
            density = (density + 1) % DensityValues.Length;
            Config.Set(DensityConfigKey, DensityValues[density]); Config.Save();
            densityCaption.text = "THẺ: " + DensityLabels[density];
            lastWidth = 0; // Reflow the existing slots; do not rebuild or reload them.
            Refresh();
        }

        private RectTransform Rect(string path) => BattleNativeWidgets.Find(root, path) as RectTransform;
        private void Hide(string path) => BattleNativeWidgets.Active(root, path, false);
        private static void StopLayout(RectTransform node)
        {
            if (node == null) return;
            foreach (var layout in node.GetComponents<LayoutGroup>()) layout.enabled = false;
            var fitter = node.GetComponent<ContentSizeFitter>(); if (fitter != null) fitter.enabled = false;
        }
        private void Frame(string path, float left, float bottom, float right, float top)
        {
            var node = Rect(path); if (node == null) return;
            StopLayout(node); node.pivot = new Vector2(.5f, .5f); node.localScale = Vector3.one;
            BattleNativeWidgets.Place(node, left, bottom, right, top);
        }
        private static void Top(RectTransform node, float y)
        {
            if (node == null) return;
            node.anchorMin = new Vector2(0, 1); node.anchorMax = Vector2.one;
            node.pivot = new Vector2(.5f, 1); node.anchoredPosition = new Vector2(0, y);
            node.sizeDelta = new Vector2(0, node.sizeDelta.y);
        }
        private TextMeshProUGUI Caption(string path, string text, int size = 24)
        {
            var node = Rect(path); if (node == null) return null;
            StopLayout(node);
            var button = node.GetComponent<Button>();
            if (button != null)
            {
                var face = node.GetComponent<Image>() ?? node.gameObject.AddComponent<Image>();
                face.enabled = true; face.raycastTarget = true;
                BattleTheme.StyleButton(button, face);
            }
            foreach (var old in node.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (old.name != "BS_Caption") old.enabled = false;
            foreach (var child in node.GetComponentsInChildren<Transform>(true))
                if (child.name == "IconGroup" || child.name == "ShortcutIconGroup" || child.name.StartsWith("Icon", StringComparison.Ordinal))
                    child.gameObject.SetActive(false);
            var caption = node.Find("BS_Caption")?.GetComponent<TextMeshProUGUI>()
                ?? BattleNativeWidgets.Text(node, "BS_Caption", text, size);
            caption.text = text; caption.fontSize = size; caption.enabled = true;
            caption.enableWordWrapping = false; caption.overflowMode = TextOverflowModes.Ellipsis;
            BattleNativeWidgets.Place(caption.rectTransform, .04f, .05f, .96f, .95f);
            return caption;
        }
        private void BuildHeader()
        {
            const string header = "DeckEditHeader/Root/Header";
            Frame(header, .012f, .905f, .988f, .985f);
            Frame(header + "/ButtonBack", 0, .12f, .04f, .88f);
            Hide(header + "/ButtonInfoSwitching");
            var title = BattleNativeWidgets.Text(Rect(header), "BS_DeckTitle", "BỘ BÀI", 32);
            BattleNativeWidgets.Place(title.rectTransform, .05f, 0, .19f, 1);
            title.alignment = TextAlignmentOptions.MidlineLeft;
            string[] buttons = { "ButtonMenu", "ButtonRegulation", "ButtonTest", "ButtonSort", "ButtonSave", "ButtonChangeSide" };
            string[] captions = { "MỞ BỘ BÀI", "STANDARD", "ĐẤU THỬ", "SẮP XẾP", "LƯU", "XÓA BỘ BÀI" };
            for (int i = 0; i < buttons.Length; i++)
            {
                float x = .20f + i * .125f;
                Frame(header + "/" + buttons[i], x, .12f, x + .115f, .88f);
                Caption(header + "/" + buttons[i], captions[i]);
            }
        }
        private void BuildCollection()
        {
            foreach (string face in new[] { "Base", "Frame", "Line" }) Frame(Collection + "/" + face, 0, 0, 1, 1);
            Frame(Collection + "/TabArea", .01f, .925f, .99f, 1);
            string[] tabs = { "CardListToggle", "BookmarkToggle", "HistotyToggle" };
            string[] text = { "TẤT CẢ", "CHƠI ĐƯỢC", "STANDARD" };
            for (int i = 0; i < tabs.Length; i++)
            {
                string path = Collection + "/TabArea/" + tabs[i];
                Frame(path, i / 3f + .008f, 0, (i + 1) / 3f - .008f, 1);
                foreach (string face in new[] { "ImageOn", "ImageOff" })
                {
                    Frame(path + "/" + face, 0, 0, 1, 1);
                    var legacy = Rect(path + "/" + face);
                    if (legacy != null) foreach (var graphic in legacy.GetComponentsInChildren<Graphic>(true)) graphic.enabled = false;
                }
                tabCaptions[i] = Caption(path, text[i], 22);
                tabOn[i] = Rect(path + "/ImageOn"); tabOff[i] = Rect(path + "/ImageOff");
                tabButtons[i] = Rect(path)?.GetComponent<Button>();
                BuildTabSurface(Rect(path), tabCaptions[i], tabButtons[i], i);
            }
            const string filters = Collection + "/FilterAndSortArea";
            Frame(filters, 0, 0, 1, 1);
            Frame(filters + "/UpperArea", .02f, .845f, .98f, .915f);
            Frame(filters + "/UpperArea/InputField", 0, 0, .73f, 1);
            Frame(filters + "/UpperArea/SearchButton", .75f, 0, 1, 1);
            Caption(filters + "/UpperArea/SearchButton", "TÌM", 23);
            Field(filters + "/UpperArea/InputField", "Tên hoặc mã thẻ · cost:6 · bp:6000");
            Frame(filters + "/BottomArea", .02f, .765f, .98f, .835f);
            string[] names = { "FilterToggle", "SortButton", "ClearButton" };
            string[] labels = { "TẤT CẢ MÀU", "MỚI NHẤT", "XÓA LỌC" };
            for (int i = 0; i < names.Length; i++)
            {
                string path = filters + "/BottomArea/" + names[i];
                Frame(path, i / 3f + .005f, 0, (i + 1) / 3f - .005f, 1); Caption(path, labels[i], 22);
            }
            Frame(Collection + "/CardListArea", .015f, .085f, .985f, .75f);
            Frame(Collection + "/CardListArea/CollectionAreaCenter", 0, 0, 1, 1);
            string scroll = Collection + "/CardListArea/CollectionAreaCenter/ScrollRect";
            Frame(scroll, 0, 0, 1, 1); Frame(scroll + "/Viewport", 0, 0, 1, 1);
            Top(collection, 0); SetupScroll(scroll, collection);
            foreach (string button in new[] { "BS_Previous", "BS_Next" })
            {
                var node = Rect(button); if (node == null) continue;
                node.SetParent(Rect(Collection), false);
                BattleNativeWidgets.Place(node, button == "BS_Previous" ? .78f : .89f, .005f,
                    button == "BS_Previous" ? .87f : .98f, .075f);
            }
        }
        private void Field(string path, string hint)
        {
            var input = BattleNativeWidgets.Component<TMP_InputField>(root, path); if (input == null) return;
            // The old Out image is still driven by cached template color
            // containers. Give the field its own surface so hover/focus cannot
            // repaint a white panel underneath white input text.
            if (input.targetGraphic != null)
            {
                input.targetGraphic.enabled = false; input.targetGraphic.raycastTarget = false;
            }
            var surfaceRect = input.transform.Find("BS_InputSurface") as RectTransform
                ?? BattleNativeWidgets.Rect(input.transform, "BS_InputSurface");
            BattleNativeWidgets.Place(surfaceRect, 0, 0, 1, 1); surfaceRect.SetAsFirstSibling();
            var background = surfaceRect.GetComponent<Image>() ?? surfaceRect.gameObject.AddComponent<Image>();
            background.enabled = true; background.raycastTarget = true; background.material = null;
            background.sprite = BattleTheme.PanelSprite; background.type = Image.Type.Sliced;
            input.targetGraphic = background; input.transition = Selectable.Transition.ColorTint;
            var inputColors = input.colors;
            inputColors.normalColor = new Color(.13f, .16f, .20f, 1);
            inputColors.highlightedColor = new Color(.17f, .22f, .26f, 1);
            inputColors.selectedColor = new Color(.18f, .24f, .28f, 1);
            inputColors.pressedColor = new Color(.10f, .14f, .18f, 1);
            inputColors.disabledColor = new Color(.08f, .10f, .12f, .8f);
            inputColors.colorMultiplier = 1; inputColors.fadeDuration = .10f;
            input.colors = inputColors; background.color = inputColors.normalColor;
            input.customCaretColor = true; input.caretColor = Color.white;
            input.selectionColor = new Color(.45f, .68f, .14f, .5f);
            foreach (string overlay in new[] { "Hover", "ButtonHover", "SelectCursor" })
            { var legacy = input.transform.Find(overlay); if (legacy != null) legacy.gameObject.SetActive(false); }
            if (input.textViewport != null)
            { StopLayout(input.textViewport); BattleNativeWidgets.Place(input.textViewport, .08f, .08f, .9f, .92f); }
            if (input.textComponent != null)
            {
                BattleNativeWidgets.Place(input.textComponent.rectTransform, 0, 0, 1, 1);
                input.textComponent.fontSize = 25; input.textComponent.color = Color.white;
                input.textComponent.enableWordWrapping = false;
            }
            if (input.placeholder != null && input.placeholder.name != "BS_InputPlaceholder")
            {
                // Legacy ColorContainerGraphic can multiply the old hint's
                // alpha several times during hover/selection. Leave its
                // serialized object in place, but render an owned hint instead.
                input.placeholder.enabled = false; input.placeholder.raycastTarget = false;
            }
            if (input.textViewport != null)
            {
                var placeholder = input.textViewport.Find("BS_InputPlaceholder")?.GetComponent<TextMeshProUGUI>()
                    ?? BattleNativeWidgets.Text(input.textViewport, "BS_InputPlaceholder", hint, 22);
                BattleNativeWidgets.Place(placeholder.rectTransform, 0, 0, 1, 1);
                placeholder.text = hint; placeholder.fontSize = 22; placeholder.font = Program.instance.ui_.tmpFont;
                placeholder.color = new Color(.65f, .71f, .78f, 1); placeholder.enabled = true;
                placeholder.alignment = TextAlignmentOptions.MidlineLeft; placeholder.raycastTarget = false;
                placeholder.enableWordWrapping = false; placeholder.overflowMode = TextOverflowModes.Ellipsis;
                input.placeholder = placeholder;
                // Keep the object active and let TMP own Graphic visibility.
                // It must be able to show the hint again after clearing a name.
                placeholder.gameObject.SetActive(true);
                input.ForceLabelUpdate();
            }
            var icon = input.transform.Find("SearchIcon"); if (icon != null) icon.gameObject.SetActive(false);
            var clear = BattleNativeWidgets.Find(input.transform, "ClearButton") as RectTransform;
            if (clear != null) BattleNativeWidgets.Place(clear, .91f, .08f, .99f, .92f);
        }
        private void BuildTabSurface(RectTransform node, TextMeshProUGUI caption, Button button, int index)
        {
            if (node == null || button == null) return;
            foreach (var old in node.GetComponentsInChildren<Graphic>(true))
                if (old != caption) { old.enabled = false; old.raycastTarget = false; }
            var surface = BattleNativeWidgets.Rect(node, "BS_TabSurface");
            BattleNativeWidgets.Place(surface, 0, 0, 1, 1); surface.SetAsFirstSibling();
            var face = surface.gameObject.AddComponent<Image>();
            face.sprite = BattleTheme.ButtonSprite; face.type = Image.Type.Sliced;
            face.raycastTarget = true; button.targetGraphic = face;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = TabColors(button.colors, false); face.color = button.colors.normalColor;
            var underline = BattleNativeWidgets.Rect(node, "BS_TabSelectedUnderline");
            BattleNativeWidgets.Place(underline, .08f, .025f, .92f, .075f);
            var accent = underline.gameObject.AddComponent<Image>(); accent.color = BattleTheme.Accent; accent.raycastTarget = false;
            tabUnderline[index] = underline.gameObject; tabUnderline[index].SetActive(false);
            caption.color = Color.white;
        }
        private static ColorBlock TabColors(ColorBlock colors, bool chosen)
        {
            colors.normalColor = chosen ? new Color(.16f, .23f, .19f, 1) : new Color(.12f, .15f, .18f, 1);
            colors.highlightedColor = new Color(.21f, .28f, .25f, 1);
            colors.selectedColor = new Color(.23f, .31f, .27f, 1);
            colors.pressedColor = new Color(.10f, .15f, .12f, 1);
            colors.disabledColor = new Color(.08f, .10f, .12f, .8f);
            colors.colorMultiplier = 1; colors.fadeDuration = .10f;
            return colors;
        }
        private void BuildDeckHeader()
        {
            Frame(Deck + "/HeaderArea", 0, .93f, 1, 1); Hide(Deck + "/HeaderArea/IconDeck");
            Frame(Deck + "/HeaderArea/NameAreaGroup", .02f, .1f, .98f, .9f);
            Frame(Deck + "/HeaderArea/NameAreaGroup/InputField", 0, 0, .74f, 1);
            Frame(Deck + "/HeaderArea/NameAreaGroup/ButtonDeck", .76f, 0, 1, 1);
            Caption(Deck + "/HeaderArea/NameAreaGroup/ButtonDeck", "LƯU TÊN", 22);
            Hide(Deck + "/HeaderArea/NameAreaGroup/InputField/Button");
            Field(Deck + "/HeaderArea/NameAreaGroup/InputField", "Tên bộ bài");
            Frame(Deck + "/Scroll View", .01f, .015f, .99f, .92f);
            Frame(Deck + "/Scroll View/Viewport", 0, 0, 1, 1);
            SetupScroll(Deck + "/Scroll View", Rect(Deck + "/Scroll View/Viewport/Content"));
            string heading = Deck + "/Scroll View/Viewport/Content/MainDeckView/Subhedding";
            var row = Rect(heading); if (row != null) { StopLayout(row); Top(row, 0); row.sizeDelta = new Vector2(0, 42); }
            Frame(heading + "/Left", 0, 0, 1, 1);
            Frame(heading + "/Left/#TextMainDeckTMP", .02f, 0, .48f, 1);
            Frame(heading + "/Left/TextMainDeckCardNumTMP", .5f, 0, .98f, 1);
            BattleNativeWidgets.Label(root, heading + "/Left/#TextMainDeckTMP", "BỘ BÀI");
            foreach (string field in new[] { "#TextMainDeckTMP", "TextMainDeckCardNumTMP" })
            {
                var label = BattleNativeWidgets.Component<TextMeshProUGUI>(root, heading + "/Left/" + field);
                if (label != null) { label.font = Program.instance.ui_.tmpFont; label.fontSize = field == "TextMainDeckCardNumTMP" ? 20 : 23; label.enableWordWrapping = false; }
            }
        }
        private void SetupScroll(string path, RectTransform content)
        {
            var scroll = BattleNativeWidgets.Component<ScrollRect>(root, path); if (scroll == null) return;
            scroll.enabled = true; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.content = content;
            var viewport = Rect(path + "/Viewport"); scroll.viewport = viewport;
            if (viewport != null && viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
            var surface = viewport != null ? viewport.GetComponent<Image>() ?? viewport.gameObject.AddComponent<Image>() : null;
            if (surface != null) { surface.color = new Color(0, 0, 0, .01f); surface.raycastTarget = true; }
        }
        private void BuildDetail()
        {
            Frame(Detail, 0, 0, 1, 1);
            Frame(Detail + "/TitleArea", 0, .935f, 1, 1);
            Frame(Detail + "/TitleArea/PlateTitle", .01f, 0, .99f, 1);
            string nameArea = Detail + "/TitleArea/PlateTitle/NameArea";
            Frame(nameArea, .02f, 0, .85f, 1); Frame(nameArea + "/Viewport", 0, 0, 1, 1);
            Frame(nameArea + "/Viewport/TextCardName", 0, 0, 1, 1);
            var nameScroll = BattleNativeWidgets.Component<ScrollRect>(root, nameArea); if (nameScroll != null) nameScroll.enabled = false;
            var cardName = BattleNativeWidgets.Component<TextMeshProUGUI>(root, nameArea + "/Viewport/TextCardName");
            if (cardName != null) { cardName.enableWordWrapping = false; cardName.overflowMode = TextOverflowModes.Ellipsis; }
            Frame(Detail + "/CardArea", .06f, .42f, .94f, .925f);
            var art = BattleNativeWidgets.Component<RawImage>(root, Detail + "/CardArea/ImageCard");
            if (art != null)
            {
                var fit = art.GetComponent<AspectRatioFitter>() ?? art.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 675f / 983;
            }
            Hide(Detail + "/CardArea/IconLimit");
            var code = Rect(Detail + "/CardArea/CardNum");
            if (code != null) { code.SetParent(Rect(Detail), false); StopLayout(code); BattleNativeWidgets.Place(code, .05f, .38f, .95f, .415f); }
            Frame(Detail + "/CardNum/TextCardNumValueTMP", 0, 0, 1, 1);
            var stats = BattleNativeWidgets.Rect(Rect(Detail), "BS_Stats");
            BattleNativeWidgets.Place(stats, .04f, .305f, .96f, .375f);
            string[] labels = { "TextLevelTMP", "TextAtkTMP", "TextDefTMP" };
            for (int i = 0; i < labels.Length; i++)
            {
                var label = BattleNativeWidgets.Find(Rect(Detail), labels[i]) as RectTransform; if (label == null) continue;
                label.SetParent(stats, false); StopLayout(label);
                BattleNativeWidgets.Place(label, i == 2 ? .65f : 0, i == 1 ? 0 : .5f, i == 0 ? .65f : 1, i == 1 ? .5f : 1);
            }
            Hide(Detail + "/ParameterArea");
            Frame(Detail + "/DescriptionArea", 0, 0, 1, 1);
            Frame(Detail + "/DescriptionArea/PlateDescription", .02f, .26f, .98f, .30f);
            Frame(Detail + "/DescriptionArea/TextArea", .025f, .09f, .975f, .255f);
            string textArea = Detail + "/DescriptionArea/TextArea";
            Frame(textArea + "/Viewport", 0, 0, .97f, 1);
            var text = BattleNativeWidgets.Component<TextMeshProUGUI>(root, textArea + "/Viewport/TextDescriptionValueTMP");
            if (text != null)
            {
                Top(text.rectTransform, 0); text.alignment = TextAlignmentOptions.TopLeft; text.enableWordWrapping = true;
                var fitter = text.GetComponent<ContentSizeFitter>() ?? text.gameObject.AddComponent<ContentSizeFitter>();
                fitter.enabled = true; fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                SetupScroll(textArea, text.rectTransform);
            }
            Frame(Detail + "/DescriptionArea/MenuAreaRoot", .025f, .01f, .975f, .08f);
            Frame(Detail + "/DescriptionArea/MenuAreaRoot/MenuArea", 0, 0, 1, 1);
            string buttons = Detail + "/DescriptionArea/MenuAreaRoot/MenuArea/CardGroup";
            Frame(buttons, 0, 0, 1, 1); Frame(buttons + "/AddCardButton", 0, 0, .48f, 1); Frame(buttons + "/RemoveCardButton", .52f, 0, 1, 1);
            Caption(buttons + "/AddCardButton", "THÊM", 24); Caption(buttons + "/RemoveCardButton", "BỚT", 24);
            foreach (var label in Rect(Detail).GetComponentsInChildren<TextMeshProUGUI>(true))
                if (label.name != "BS_Caption") { label.font = Program.instance.ui_.tmpFont; label.fontSize = 23; }
        }

        internal void Refresh(int count = -1)
        {
            bool changedCount = count >= 0 && count != deckCount;
            if (count >= 0) deckCount = count;
            float width = area != null ? area.rect.width : 1920, height = area != null ? area.rect.height : 1080;
            if (width < 600 || height < 300) { width = 1920; height = 1080; }
            if (!changedCount && width == lastWidth && height == lastHeight) return;
            if (width != lastWidth || height != lastHeight)
            {
                lastWidth = width; lastHeight = height;
                ResizeGrid(collectionGrid, width * .346f * .97f, density == 2 ? 2 : 3, density == 0 ? 6 : density == 1 ? 5 : 4);
                ResizeGrid(deckGrid, width * .374f * .98f, density == 2 ? 2 : 3, density == 0 ? 8 : density == 1 ? 6 : 5);
            }
            int rows = deckCount == 0 ? 0 : (deckCount + deckGrid.constraintCount - 1) / deckGrid.constraintCount;
            float total = 44 + rows * (deckGrid.cellSize.y + deckGrid.spacing.y) + 12;
            if (outerDeck != null) outerDeck.sizeDelta = new Vector2(0, total);
            if (deckSection != null) deckSection.sizeDelta = new Vector2(0, total);
        }
        private void ResizeGrid(GridLayoutGroup grid, float width, int minimum, int maximum)
        {
            int columns = Math.Max(minimum, Math.Min(maximum, (int)(width / DensityWidths[density])));
            grid.constraintCount = columns; grid.spacing = new Vector2(8, 8); grid.padding = new RectOffset(8, 8, 8, 8);
            float cardWidth = Math.Max(1, (width - 16 - (columns - 1) * 8) / columns);
            grid.cellSize = new Vector2(cardWidth, cardWidth * 1.62f);
        }
        internal void Results(int count, int page, int pages, int support)
        {
            pageLabel.text = count + " thẻ · Trang " + (page + 1) + "/" + pages;
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabOn[i] != null) tabOn[i].gameObject.SetActive(i == support);
                if (tabOff[i] != null) tabOff[i].gameObject.SetActive(i != support);
                var caption = tabCaptions[i];
                if (caption != null) caption.color = Color.white;
                if (tabUnderline[i] != null) tabUnderline[i].SetActive(i == support);
                var button = tabButtons[i];
                if (button != null)
                {
                    var colors = TabColors(button.colors, i == support);
                    button.colors = colors;
                    if (button.targetGraphic != null) button.targetGraphic.color = colors.normalColor;
                }
            }
        }
    }
}
