using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LaoDai.BattleSpirits.Core;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LaoDai.BattleSpirits.Presentation
{
    // Preserves MDPro3's card-detail / collection / deck editing arrangement,
    // binding each area to Battle Spirits IDs rather than integer YGO records.
    internal sealed class BattleDeckPanel
    {
        private readonly RectTransform root;
        private readonly string rootPath;
        private readonly IReadOnlyList<CardDefinition> catalog;
        private readonly Dictionary<string, CardDefinition> lookup;
        private readonly Action<string, RawImage> loadArt;
        private readonly Action<DeckDefinition> selected;
        private readonly Action<string> status;
        private readonly List<string> deck;
        private RectTransform grid;
        private RectTransform deckRows;
        private RawImage preview;
        private TextMeshProUGUI description;
        private TextMeshProUGUI resultCount;
        private TextMeshProUGUI deckCount;
        private TMP_InputField query;
        private TMP_InputField name;
        private TMP_InputField minimumCost;
        private TMP_InputField maximumCost;
        private TMP_InputField minimumBp;
        private TMP_Dropdown colors;
        private TMP_Dropdown types;
        private TMP_Dropdown sets;
        private TMP_Dropdown support;
        private TMP_Dropdown sorting;
        private string[] typeOptions;
        private string[] setOptions;
        private CardDefinition current;
        private Button add;
        private int page;
        private const int PageSize = 42;
        private readonly HashSet<string> chosenNames = new HashSet<string>(StringComparer.Ordinal);

        internal BattleDeckPanel(RectTransform root, string rootPath, IReadOnlyList<CardDefinition> catalog,
            DeckDefinition selectedDeck, Action<string, RawImage> loadArt, Action<DeckDefinition> selected, Action<string> status)
        {
            this.root = root; this.rootPath = rootPath; this.catalog = catalog;
            this.loadArt = loadArt; this.selected = selected; this.status = status;
            lookup = catalog.ToDictionary(card => card.id, StringComparer.Ordinal);
            deck = new List<string>(selectedDeck.cards);
            initialName = selectedDeck.name;
        }

        private readonly string initialName;

        internal void Build()
        {
            _ = BattleTemplateVisuals.CopyDeckFrameAsync(root);
            var left = BattleUiTheme.Panel(root, "BS_CardDetail", new Vector2(0.012f, 0.13f), new Vector2(0.285f, 0.9f), new Color(0.01f, 0.025f, 0.045f, 0.78f));
            var previewFrame = BattleUiTheme.Rect(left, "CardArtFrame");
            BattleSpiritsApp.Place(previewFrame, 0.1f, 0.49f, 0.9f, 0.97f);
            var image = BattleUiTheme.Rect(previewFrame, "CardArt", typeof(RawImage), typeof(AspectRatioFitter));
            preview = image.GetComponent<RawImage>(); preview.color = Color.white; preview.raycastTarget = false;
            var aspect = image.GetComponent<AspectRatioFitter>(); aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent; aspect.aspectRatio = 0.715f;
            var detailsScroll = BattleUiTheme.Scroll(left, "CardDescription", out var details);
            BattleSpiritsApp.Place(detailsScroll.transform as RectTransform, 0.02f, 0.09f, 0.98f, 0.48f);
            description = BattleUiTheme.Text(details, "Description", "Chọn một lá bài để xem thông tin.", 19);
            var descriptionLayout = description.gameObject.AddComponent<LayoutElement>(); descriptionLayout.preferredHeight = 850f;
            add = BattleUiTheme.Button(left, "THÊM VÀO BỘ BÀI", AddCurrent);
            BattleSpiritsApp.Place(add.transform as RectTransform, 0.05f, 0.01f, 0.95f, 0.08f); add.interactable = false;

            query = BattleUiTheme.Input(root, "Tìm tên / mã / hệ / nội dung hiệu ứng", string.Empty, _ => Refresh(true));
            BattleSpiritsApp.Place(query.transform as RectTransform, 0.30f, 0.835f, 0.69f, 0.895f);
            sorting = BattleUiTheme.Dropdown(root, "Sort", new[] { "Mới nhất", "Cost tăng dần", "Tên", "Mã thẻ" }, _ => Refresh(true));
            BattleSpiritsApp.Place(sorting.transform as RectTransform, 0.70f, 0.835f, 0.98f, 0.895f);
            colors = BattleUiTheme.Dropdown(root, "Color", new[] { "Tất cả màu", "Đỏ", "Tím", "Lục", "Trắng", "Vàng", "Lam" }, _ => Refresh(true));
            typeOptions = new[] { "Tất cả loại" }.Concat(catalog.Select(card => card.type).Where(value => !string.IsNullOrEmpty(value)).Distinct().OrderBy(value => value)).ToArray();
            types = BattleUiTheme.Dropdown(root, "Type", typeOptions, _ => Refresh(true));
            setOptions = new[] { "Tất cả bộ" }.Concat(catalog.OrderByDescending(card => card.releaseDate, StringComparer.Ordinal).Select(card => card.setId).Where(value => !string.IsNullOrEmpty(value)).Distinct()).ToArray();
            sets = BattleUiTheme.Dropdown(root, "Set", setOptions, _ => Refresh(true));
            support = BattleUiTheme.Dropdown(root, "Support", new[] { "Tất cả thẻ", "Đã hỗ trợ trong trận", "Eternal đã xác nhận", "Chỉ tra cứu" }, _ => Refresh(true));
            BattleSpiritsApp.Place(colors.transform as RectTransform, 0.30f, 0.765f, 0.46f, 0.825f);
            BattleSpiritsApp.Place(types.transform as RectTransform, 0.47f, 0.765f, 0.63f, 0.825f);
            BattleSpiritsApp.Place(sets.transform as RectTransform, 0.64f, 0.765f, 0.80f, 0.825f);
            BattleSpiritsApp.Place(support.transform as RectTransform, 0.81f, 0.765f, 0.98f, 0.825f);
            minimumCost = BattleUiTheme.Input(root, "Cost từ", string.Empty, _ => Refresh(true));
            maximumCost = BattleUiTheme.Input(root, "Cost đến", string.Empty, _ => Refresh(true));
            minimumBp = BattleUiTheme.Input(root, "BP tối thiểu", string.Empty, _ => Refresh(true));
            minimumCost.contentType = maximumCost.contentType = minimumBp.contentType = TMP_InputField.ContentType.IntegerNumber;
            BattleSpiritsApp.Place(minimumCost.transform as RectTransform, 0.30f, 0.70f, 0.42f, 0.755f);
            BattleSpiritsApp.Place(maximumCost.transform as RectTransform, 0.43f, 0.70f, 0.55f, 0.755f);
            BattleSpiritsApp.Place(minimumBp.transform as RectTransform, 0.56f, 0.70f, 0.72f, 0.755f);
            resultCount = BattleUiTheme.Text(root, "Results", string.Empty, 18, TextAlignmentOptions.MidlineRight);
            BattleSpiritsApp.Place(resultCount.rectTransform, 0.73f, 0.70f, 0.98f, 0.755f);

            var collection = BattleUiTheme.Panel(root, "BS_CardCollection", new Vector2(0.30f, 0.35f), new Vector2(0.98f, 0.69f), new Color(0.01f, 0.035f, 0.055f, 0.7f));
            var scroll = BattleUiTheme.Scroll(collection, "CardGrid", out var gridContent);
            grid = BattleUiTheme.Rect(gridContent, "Grid");
            grid.anchorMin = new Vector2(0, 1); grid.anchorMax = Vector2.one;
            grid.pivot = new Vector2(0.5f, 1); grid.sizeDelta = Vector2.zero;
            var gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(105, 164); gridLayout.spacing = new Vector2(8, 9);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gridLayout.constraintCount = 7;
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            var fitter = grid.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var previous = BattleUiTheme.Button(root, "‹", () => { page = Math.Max(0, page - 1); Refresh(false); });
            var next = BattleUiTheme.Button(root, "›", () => { page++; Refresh(false); });
            BattleSpiritsApp.Place(previous.transform as RectTransform, 0.915f, 0.355f, 0.944f, 0.405f);
            BattleSpiritsApp.Place(next.transform as RectTransform, 0.949f, 0.355f, 0.978f, 0.405f);

            name = BattleUiTheme.Input(root, "Tên bộ bài", initialName);
            BattleSpiritsApp.Place(name.transform as RectTransform, 0.30f, 0.275f, 0.62f, 0.335f);
            deckCount = BattleUiTheme.Text(root, "DeckCount", string.Empty, 22, TextAlignmentOptions.MidlineLeft);
            BattleSpiritsApp.Place(deckCount.rectTransform, 0.635f, 0.275f, 0.78f, 0.335f);
            var save = BattleUiTheme.Button(root, "LƯU", Save);
            BattleSpiritsApp.Place(save.transform as RectTransform, 0.80f, 0.275f, 0.885f, 0.335f);
            var load = BattleUiTheme.Button(root, "MỞ", ShowSaved);
            BattleSpiritsApp.Place(load.transform as RectTransform, 0.895f, 0.275f, 0.98f, 0.335f);
            var deckScroll = BattleUiTheme.Scroll(root, "DeckCards", out deckRows);
            BattleSpiritsApp.Place(deckScroll.transform as RectTransform, 0.30f, 0.085f, 0.98f, 0.265f);
            var clear = BattleUiTheme.Button(root, "XÓA BỘ BÀI ĐANG SỬA", () => { deck.Clear(); RefreshDeck(); });
            BattleSpiritsApp.Place(clear.transform as RectTransform, 0.025f, 0.085f, 0.27f, 0.13f);
            Refresh(true); RefreshDeck();
            if (catalog.Count > 0) Select(catalog[0]);
        }

        private void Refresh(bool resetPage)
        {
            if (grid == null || sorting == null || support == null || colors == null || types == null || sets == null || minimumBp == null) return;
            if (resetPage) page = 0;
            string search = (query.text ?? string.Empty).Trim();
            int minCost = int.TryParse(minimumCost.text, out int a) ? Math.Max(0, a) : 0;
            int maxCost = int.TryParse(maximumCost.text, out int b) ? Math.Max(0, b) : int.MaxValue;
            int minBp = int.TryParse(minimumBp.text, out int c) ? Math.Max(0, c) : 0;
            IEnumerable<CardDefinition> result = catalog.Where(card => card.cost >= minCost && card.cost <= maxCost
                && (card.levels == null || card.levels.Count == 0 ? 0 : card.levels.Max(level => level.bp)) >= minBp);
            if (search.Length > 0) result = result.Where(card => Contains(card.id, search) || Contains(card.nameJa, search) || Contains(card.nameVi, search) || Contains(card.effectTextJa, search) || (card.families ?? Array.Empty<string>()).Any(family => Contains(family, search)));
            if (colors.value > 0)
            {
                string color = ((CardColor)(colors.value - 1)).ToString();
                result = result.Where(card => (card.colors?.Length ?? 0) > 0 ? card.colors.Any(item => string.Equals(item, color, StringComparison.OrdinalIgnoreCase)) : card.color == (CardColor)(colors.value - 1));
            }
            if (types.value > 0) result = result.Where(card => card.type == typeOptions[types.value]);
            if (sets.value > 0) result = result.Where(card => card.setId == setOptions[sets.value]);
            if (support.value == 1) result = result.Where(card => card.IsPlayable);
            if (support.value == 2) result = result.Where(card => card.IsEternalEligible);
            if (support.value == 3) result = result.Where(card => !card.IsPlayable);
            switch (sorting.value)
            {
                case 1: result = result.OrderBy(card => card.cost).ThenBy(card => card.id, StringComparer.Ordinal); break;
                case 2: result = result.OrderBy(card => card.DisplayName, StringComparer.Ordinal); break;
                case 3: result = result.OrderBy(card => card.id, StringComparer.Ordinal); break;
                default: result = result.OrderByDescending(card => card.releaseDate, StringComparer.Ordinal).ThenBy(card => card.id, StringComparer.Ordinal); break;
            }
            var all = result.ToArray();
            page = Math.Max(0, Math.Min(page, Math.Max(0, (all.Length - 1) / PageSize)));
            ClearChildren(grid);
            foreach (var card in all.Skip(page * PageSize).Take(PageSize))
            {
                var button = BattleUiTheme.Button(grid, string.Empty, () => Select(card));
                var imageGo = BattleUiTheme.Rect(button.transform, "Artwork", typeof(RawImage));
                var image = imageGo.GetComponent<RawImage>(); image.raycastTarget = false;
                BattleSpiritsApp.Place(image.rectTransform, 0.04f, 0.17f, 0.96f, 0.98f); loadArt(card.id, image);
                var label = BattleUiTheme.Text(button.transform, "CardName", card.id + (card.IsPlayable ? " ✓" : ""), 15, TextAlignmentOptions.Center);
                BattleSpiritsApp.Place(label.rectTransform, 0.02f, 0, 0.98f, 0.16f);
            }
            resultCount.text = all.Length + " thẻ — " + (page + 1) + "/" + Math.Max(1, (all.Length + PageSize - 1) / PageSize);
        }

        private static bool Contains(string value, string query) => !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        private void Select(CardDefinition card)
        {
            current = card; loadArt(card.id, preview);
            string levels = string.Join("\n", (card.levels ?? new List<CardLevel>()).Select((level, index) => "Lv" + (index + 1) + ": " + level.cores + " core / " + level.bp + " BP"));
            string availability = card.IsPlayable ? "<color=#91E8B0>Đã hỗ trợ trong trận</color>" : "<color=#FFD280>Chỉ tra cứu — hiệu ứng chưa được hỗ trợ trong trận</color>";
            string format = card.IsEternalEligible ? "Eternal đã xác nhận" : "Chưa xác nhận dùng trong Eternal";
            description.text = "<b>" + card.DisplayName + "</b>\n" + card.id + " • " + card.type + "\n" + card.setId + " • " + card.releaseDate + "\n" + availability + "\n" + format + "\n\nMàu: " + string.Join(" / ", card.colors?.Length > 0 ? card.colors : new[] { card.color.ToString() }) + "\nCost: " + card.cost + " • Giảm phí: " + card.reductionSymbols + "\nSymbol: " + (card.symbolsVerified ? card.symbols.ToString() : "Chưa xác minh") + "\nHệ: " + string.Join(" / ", card.families ?? Array.Empty<string>()) + "\n" + levels + "\n\n" + card.effectTextJa;
            add.interactable = card.IsEternalEligible;
            status(card.IsPlayable ? "Bấm Thêm để đưa vào bộ bài." : "Ảnh, chỉ số và hiệu ứng gốc dùng để tra cứu. Trận tự động chỉ nhận các thẻ đã hỗ trợ.");
        }

        private void AddCurrent()
        {
            if (current == null || !current.IsEternalEligible) return;
            int count = deck.Count(id => lookup.TryGetValue(id, out var card) && card.nameJa == current.nameJa);
            int limit = catalog.Where(card => card.nameJa == current.nameJa).Min(card => card.EffectiveCopyLimit);
            if (count >= limit) { status("Đã đạt giới hạn số lá cùng tên."); return; }
            deck.Add(current.id); RefreshDeck();
        }

        private void RefreshDeck()
        {
            ClearChildren(deckRows); chosenNames.Clear();
            foreach (var grouping in deck.GroupBy(id => id).OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                string id = grouping.Key; int count = grouping.Count();
                string title = lookup.TryGetValue(id, out var card) ? card.DisplayName : id;
                var row = BattleUiTheme.Button(deckRows, count + " × " + title + "  [" + id + "]  −", () => { deck.Remove(id); RefreshDeck(); if (card != null) Select(card); });
                row.GetComponentInChildren<TextMeshProUGUI>().fontSize = 18;
            }
            var validation = DeckValidator.Validate(catalog, deck);
            deckCount.text = deck.Count + " LÁ" + (validation.IsValid ? " ✓" : "");
            selected(new DeckDefinition { name = name.text, cards = deck.ToArray() });
        }

        private void Save()
        {
            string title = (name.text ?? string.Empty).Trim();
            if (title.Length == 0 || title.Length > 80 || title.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || title.Contains("/") || title.Contains("\\")) { status("Tên bộ bài cần từ 1 đến 80 ký tự và không chứa ký tự đường dẫn."); return; }
            var validation = DeckValidator.Validate(catalog, deck, false);
            if (!validation.IsValid) { status(validation.Summary); return; }
            try
            {
                string directory = Path.Combine(rootPath, BattleSpiritsMode.DeckDirectory); Directory.CreateDirectory(directory);
                var definition = new DeckDefinition { name = title, cards = deck.ToArray() };
                File.WriteAllText(Path.Combine(directory, title + ".json"), JsonConvert.SerializeObject(definition, Formatting.Indented));
                selected(definition);
                var playable = DeckValidator.Validate(catalog, deck);
                status(playable.IsValid ? "Đã lưu bộ bài. Có thể chọn để chơi cùng máy hoặc qua mạng." : "Đã lưu bộ bài để chỉnh sửa. Còn thẻ chưa hỗ trợ hiệu ứng trong trận.");
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { status("Không lưu được bộ bài: " + error.Message); }
        }

        private void ShowSaved()
        {
            string directory = Path.Combine(rootPath, BattleSpiritsMode.DeckDirectory);
            var files = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.json").OrderBy(path => path, StringComparer.Ordinal).ToArray() : Array.Empty<string>();
            if (files.Length == 0) { status("Chưa có bộ bài đã lưu."); return; }
            var picker = BattleUiTheme.Panel(root, "BS_DeckPicker", new Vector2(0.2f, 0.18f), new Vector2(0.8f, 0.82f), new Color(0.01f, 0.03f, 0.055f, 1));
            var scroll = BattleUiTheme.Scroll(picker, "SavedDecks", out var content); BattleSpiritsApp.Place(scroll.transform as RectTransform, 0.04f, 0.15f, 0.96f, 0.95f);
            foreach (string file in files)
            {
                string path = file;
                BattleUiTheme.Button(content, Path.GetFileNameWithoutExtension(path), () =>
                {
                    try
                    {
                        var saved = JsonConvert.DeserializeObject<DeckDefinition>(File.ReadAllText(path));
                        if (saved?.cards == null) throw new InvalidDataException("Tệp bộ bài trống.");
                        deck.Clear(); deck.AddRange(saved.cards); name.text = saved.name; RefreshDeck();
                        UnityEngine.Object.Destroy(picker.gameObject); status("Đã mở bộ bài.");
                    }
                    catch (Exception error) { status(error.Message); }
                });
            }
            var close = BattleUiTheme.Button(picker, "ĐÓNG", () => UnityEngine.Object.Destroy(picker.gameObject));
            BattleSpiritsApp.Place(close.transform as RectTransform, 0.35f, 0.02f, 0.65f, 0.12f);
        }

        private static void ClearChildren(Transform parent)
        {
            foreach (Transform child in parent) { child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject); }
        }
    }
}
