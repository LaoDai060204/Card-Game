using System;
using System.Collections.Generic;
using System.Linq;
using LaoDai.BattleSpirits.Core;
using LaoDai.BattleSpirits.Visuals;
using MDPro3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LaoDai.BattleSpirits.NativeUI
{
    // Binds Battle Spirits data to the original DeckEditorUI/DeckView/CardDetailView form.
    internal sealed class BattleNativeDeckView
    {
        private readonly Transform root;
        private readonly IBattleNativeSession session;
        private readonly List<string> deck;
        private readonly Dictionary<string, CardDefinition> cards;
        private RectTransform collection, deckContent, collectionTemplate, deckTemplate;
        private ScrollRect collectionScroll;
        private TMP_InputField search, name;
        private BattleNativeDeckLayout layout;
        private Transform emptyCollection;
        private RawImage detailColorSymbol, filterColorSymbol;
        private Transform detailRoot;
        private RawImage detailArtwork;
        private readonly Dictionary<string, Transform> detailNodes = new Dictionary<string, Transform>(StringComparer.Ordinal);
        private bool detailRendered;
        private CardDefinition selected;
        private int color = -1, sort, support, page;
        private string loadedDeck;
        private readonly List<BattleNativeWidgets.CardSlot> collectionSlots = new List<BattleNativeWidgets.CardSlot>();
        private readonly List<BattleNativeWidgets.CardSlot> deckSlots = new List<BattleNativeWidgets.CardSlot>();
        private readonly List<BattleNativeWidgets.CardSlot> orderedDeckSlots = new List<BattleNativeWidgets.CardSlot>();
        private readonly HashSet<BattleNativeWidgets.CardSlot> usedDeckSlots = new HashSet<BattleNativeWidgets.CardSlot>();
        private readonly CardDefinition[][] sortedCatalog;
        private readonly Dictionary<string, int> copyLimits;
        private CardDefinition[] collectionResults;
        private string renderedQuery;
        private int renderedColor, renderedSort, renderedSupport;
        private bool searchPending;
        private float searchDue;
        private int displayedPage = -1;
        internal const float SearchDelaySeconds = 0.15f;
        private const int PageSize = 42;
        private const string Collection = "CardCollectionView/Root/Window";
        private const string Deck = "DeckView/Root/Window";
        private const string Detail = "CardDetailView/Root/Window/Content";
        private const string Header = "DeckEditHeader/Root/Header";

        internal BattleNativeDeckView(Transform root, IBattleNativeSession session)
        {
            this.root = root; this.session = session;
            cards = session.Cards.ToDictionary(card => card.id, StringComparer.Ordinal);
            sortedCatalog = new[] {
                session.Cards.OrderByDescending(card => card.releaseDate, StringComparer.Ordinal).ThenBy(card => card.id, StringComparer.Ordinal).ToArray(),
                session.Cards.OrderBy(card => card.cost).ThenBy(card => card.id, StringComparer.Ordinal).ToArray(),
                session.Cards.OrderBy(card => card.DisplayName, StringComparer.Ordinal).ToArray()
            };
            copyLimits = session.Cards.Where(card => card.standardBlock == "A" && card.standardLegalityVerified
                    && (card.format == "Standard" || card.format == "Both"))
                .GroupBy(card => card.nameJa, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Min(card => card.EffectiveCopyLimit), StringComparer.Ordinal);
            deck = new List<string>(session.SelectedDeck?.cards ?? Array.Empty<string>());
            loadedDeck = Signature(session.SelectedDeck);
        }
        internal void Build()
        {
            collection = BattleNativeWidgets.Component<RectTransform>(root, Collection + "/CardListArea/CollectionAreaCenter/ScrollRect/Viewport/Content");
            deckContent = BattleNativeWidgets.Component<RectTransform>(root, Deck + "/Scroll View/Viewport/Content/MainDeckView/MainDeckContent");
            collectionTemplate = collection?.Find("Template") as RectTransform; deckTemplate = deckContent?.Find("Template") as RectTransform;
            collectionScroll = collection?.GetComponentInParent<ScrollRect>();
            if (collectionTemplate != null) collectionTemplate.gameObject.SetActive(false);
            if (deckTemplate != null) deckTemplate.gameObject.SetActive(false);
            BattleNativeWidgets.Grid(collection, 5); BattleNativeWidgets.Grid(deckContent, 6, 92, 138);
            search = BattleNativeWidgets.Input(root, Collection + "/FilterAndSortArea/UpperArea/InputField", string.Empty);
            name = BattleNativeWidgets.Input(root, Deck + "/HeaderArea/NameAreaGroup/InputField", session.SelectedDeck?.name ?? "Battle Spirits Standard");
            if (search != null) search.onValueChanged.AddListener(_ => { page = 0; searchPending = true; searchDue = Time.unscaledTime + SearchDelaySeconds; });
            if (search != null) search.onSubmit.AddListener(_ => { page = 0; RenderCollection(); });
            BattleNativeWidgets.Bind(root, Header + "/ButtonBack", BattleNativeNavigation.ReturnToMenu);
            BattleNativeWidgets.Bind(root, Header + "/ButtonSave", () => Save(), "LƯU");
            BattleNativeWidgets.Bind(root, Header + "/ButtonTest", () => { if (Save()) BattleNativeNavigation.Open(BattleNativeScreen.Solo); }, "ĐẤU THỬ");
            BattleNativeWidgets.Bind(root, Header + "/ButtonSort", () => { deck.Sort(CompareCards); RenderDeck(); }, "SẮP XẾP");
            BattleNativeWidgets.Bind(root, Header + "/ButtonChangeSide", () => { deck.Clear(); RenderDeck(); }, "XÓA BỘ BÀI");
            BattleNativeWidgets.Bind(root, Header + "/ButtonRegulation", () => { support = (support + 1) % 3; page = 0; RenderCollection(); }, "STANDARD");
            BattleNativeWidgets.Bind(root, Header + "/ButtonMenu", NextDeck, "MỞ BỘ BÀI");
            BattleNativeWidgets.Bind(root, Collection + "/FilterAndSortArea/UpperArea/SearchButton", RenderCollection);
            BattleNativeWidgets.Bind(root, Collection + "/FilterAndSortArea/BottomArea/FilterToggle", CycleColor, "TẤT CẢ MÀU");
            BattleNativeWidgets.Bind(root, Collection + "/FilterAndSortArea/BottomArea/SortButton", () => { sort = (sort + 1) % 3; page = 0; RenderCollection(); }, "MỚI NHẤT");
            BattleNativeWidgets.Bind(root, Collection + "/FilterAndSortArea/BottomArea/ClearButton", () => { if (search != null) search.text = string.Empty; color = -1; support = 0; page = 0; RenderCollection(); });
            BattleNativeWidgets.Bind(root, Collection + "/TabArea/CardListToggle", () => { support = 0; page = 0; RenderCollection(); }, "TẤT CẢ");
            BattleNativeWidgets.Bind(root, Collection + "/TabArea/BookmarkToggle", () => { support = 1; page = 0; RenderCollection(); }, "CHƠI ĐƯỢC");
            BattleNativeWidgets.Bind(root, Collection + "/TabArea/HistotyToggle", () => { support = 2; page = 0; RenderCollection(); }, "STANDARD");
            BattleNativeWidgets.Active(root, Deck + "/Scroll View/Viewport/Content/ExtraDeckView", false);
            BattleNativeWidgets.Active(root, Deck + "/Scroll View/Viewport/Content/SideDeckView", false);
            BattleNativeWidgets.Active(root, Deck + "/Scroll View/Loading", false);
            BattleNativeWidgets.Active(root, Deck + "/Scroll View/NoItemButton", false);
            BattleNativeWidgets.Active(root, Collection + "/RelatedArea", false);
            BattleNativeWidgets.Active(root, Collection + "/NoItemButton", false);
            BattleNativeWidgets.Active(root, Collection + "/CardListArea/CollectionAreaCenter/Loading", false);
            BattleNativeWidgets.Active(root, Deck + "/Scroll View/Viewport/Content/MainDeckView/Subhedding/Right", false);
            BattleNativeWidgets.Active(root, Detail + "/DescriptionArea/MenuAreaRoot/MenuArea/RarityGroup", false);
            BattleNativeWidgets.Active(root, Detail + "/DescriptionArea/MenuAreaRoot/MenuArea/MenuGroup", false);
            BattleNativeWidgets.Bind(root, Detail + "/DescriptionArea/MenuAreaRoot/MenuArea/CardGroup/AddCardButton", Add, "THÊM");
            BattleNativeWidgets.Bind(root, Detail + "/DescriptionArea/MenuAreaRoot/MenuArea/CardGroup/RemoveCardButton", RemoveSelected, "BỚT");
            BattleNativeWidgets.Bind(root, Deck + "/HeaderArea/NameAreaGroup/ButtonDeck", () => Save(), "LƯU TÊN");
            BattleNativeWidgets.Active(root, "CardActionMenu", false);
            BattleNativeWidgets.Label(root, Deck + "/Scroll View/Viewport/Content/MainDeckView/Subhedding/Left/#TextMainDeckTMP", "BỘ BÀI STANDARD");
            var filter = BattleNativeWidgets.Find(root, Collection + "/FilterAndSortArea/BottomArea/FilterToggle");
            if (filter != null)
            {
                var slot = BattleNativeWidgets.Rect(filter, "BS_FilterColorSymbol");
                BattleNativeWidgets.Place(slot, 0.02f, 0.15f, 0.16f, 0.85f);
                filterColorSymbol = slot.gameObject.AddComponent<RawImage>(); filterColorSymbol.raycastTarget = false;
                filterColorSymbol.gameObject.SetActive(false);
            }
            if (collection != null)
            {
                var scrollRoot = collection.parent?.parent;
                if (scrollRoot != null)
                {
                    var previous = BattleNativeWidgets.Button(scrollRoot, "BS_Previous", "‹", () => { page = Math.Max(0, page - 1); RenderCollection(); });
                    BattleNativeWidgets.Place(previous.transform as RectTransform, 0.78f, 0.01f, 0.87f, 0.08f);
                    var next = BattleNativeWidgets.Button(scrollRoot, "BS_Next", "›", () => { page++; RenderCollection(); });
                    BattleNativeWidgets.Place(next.transform as RectTransform, 0.88f, 0.01f, 0.97f, 0.08f);
                }
            }
            emptyCollection = BattleNativeWidgets.Find(root, Collection + "/CardListArea/CollectionAreaCenter/#Text_NoItemMessageTMP");
            if (emptyCollection != null)
                BattleNativeWidgets.SetLabel(emptyCollection, "Không tìm thấy thẻ phù hợp.\nThử tên, mã thẻ hoặc xóa bộ lọc.");
            layout = new BattleNativeDeckLayout(root, collection, deckContent);
            RenderCollection(); RenderDeck();
            if (session.Cards.Count > 0) Select(session.Cards[0]);
        }
        private int CompareCards(string first, string second) => cards.TryGetValue(first, out var a) && cards.TryGetValue(second, out var b) ? a.cost.CompareTo(b.cost) : string.CompareOrdinal(first, second);
        private void NextDeck()
        {
            if (session.Decks.Count == 0) return;
            int current = -1;
            for (int i = 0; i < session.Decks.Count; i++)
                if (Signature(session.Decks[i]) == loadedDeck) { current = i; break; }
            var next = session.Decks[(current + 1) % session.Decks.Count];
            session.SelectDeck(next); RefreshSelectedDeck();
        }
        private static string Signature(DeckDefinition definition)
            => definition == null ? string.Empty : definition.name + "\n" + string.Join("\n", definition.cards ?? Array.Empty<string>());
        internal void RefreshSelectedDeck()
        {
            // Cached screens can outlive hidden artwork in the session's LRU.
            // Rebinding checks destroyed textures without cloning any widgets.
            RenderCollection();
            if (selected != null) Select(selected);
            string signature = Signature(session.SelectedDeck);
            if (signature == loadedDeck) { RenderDeck(); return; }
            loadedDeck = signature;
            deck.Clear(); deck.AddRange(session.SelectedDeck?.cards ?? Array.Empty<string>());
            if (name != null) name.SetTextWithoutNotify(session.SelectedDeck?.name ?? "Battle Spirits Standard");
            RenderDeck();
        }
        private void CycleColor()
        {
            color = color == 5 ? -1 : color + 1; page = 0;
            RenderCollection();
        }
        internal void Tick(float now)
        {
            layout?.Refresh();
            if (searchPending && now >= searchDue) RenderCollection();
        }
        internal void FlushSearch()
        {
            if (searchPending) RenderCollection();
        }
        private void RenderCollection()
        {
            if (collection == null) return;
            searchPending = false;
            string[] names = { "ĐỎ", "TÍM", "LỤC", "TRẮNG", "VÀNG", "LAM" };
            BattleNativeWidgets.Label(root, Collection + "/FilterAndSortArea/BottomArea/FilterToggle", color < 0 ? "TẤT CẢ MÀU" : names[color]);
            if (filterColorSymbol != null)
            {
                filterColorSymbol.texture = color >= 0 ? BattleTheme.ColorIcon((CardColor)color) : null;
                filterColorSymbol.gameObject.SetActive(filterColorSymbol.texture != null);
            }
            string query = (search?.text ?? string.Empty).Trim();
            bool criteriaChanged = collectionResults == null || !string.Equals(renderedQuery, query, StringComparison.OrdinalIgnoreCase)
                || renderedColor != color || renderedSort != sort || renderedSupport != support;
            if (criteriaChanged)
            {
                IEnumerable<CardDefinition> values = sortedCatalog[sort];
                if (query.Length > 0) values = values.Where(card => Matches(card, query));
                if (color >= 0) values = values.Where(card => card.color == (CardColor)color);
                if (support == 1) values = values.Where(card => card.IsPlayableFor(BattleSpiritsMode.RulesFormat));
                if (support == 2) values = values.Where(card => card.IsEligibleFor(BattleSpiritsMode.RulesFormat));
                collectionResults = values.ToArray();
                renderedQuery = query; renderedColor = color; renderedSort = sort; renderedSupport = support;
            }
            var all = collectionResults;
            if (emptyCollection != null) emptyCollection.gameObject.SetActive(all.Length == 0);
            int pageCount = Math.Max(1, (all.Length + PageSize - 1) / PageSize);
            page = Math.Max(0, Math.Min(page, pageCount - 1));
            if (collectionScroll != null && (criteriaChanged || displayedPage != page))
                collectionScroll.verticalNormalizedPosition = 1f;
            displayedPage = page;
            int start = page * PageSize, count = Math.Min(PageSize, all.Length - start);
            for (int i = 0; i < count; i++)
            {
                if (i == collectionSlots.Count)
                {
                    var slot = BattleNativeWidgets.CreateCardSlot(collection, session, collectionTemplate);
                    collectionSlots.Add(slot);
                    slot.Bind(all[start + i].id, () => { if (cards.TryGetValue(slot.Id, out var card)) Select(card); });
                }
                else collectionSlots[i].Bind(all[start + i].id);
            }
            for (int i = count; i < collectionSlots.Count; i++) collectionSlots[i].Hide();
            BattleNativeWidgets.Label(root, Collection + "/FilterAndSortArea/BottomArea/SortButton", sort == 1 ? "COST" : sort == 2 ? "TÊN" : "MỚI NHẤT");
            layout?.Results(all.Length, page, pageCount, support);
        }
        private static bool Matches(CardDefinition card, string query)
        {
            bool Has(string value) => !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            if (Has(card.id) || Has(card.DisplayName) || Has(card.nameJa) || Has(card.effectTextJa) || Has(card.type) || Has(card.setId) || (card.families ?? Array.Empty<string>()).Any(Has)) return true;
            return query.StartsWith("cost:", StringComparison.OrdinalIgnoreCase) && int.TryParse(query.Substring(5), out var cost) && card.cost == cost
                || query.StartsWith("bp:", StringComparison.OrdinalIgnoreCase) && int.TryParse(query.Substring(3), out var bp) && (card.levels?.Any(level => level.bp >= bp) ?? false);
        }
        private void Select(CardDefinition card)
        {
            bool unchanged = ReferenceEquals(selected, card) && detailRendered;
            selected = card;
            var detail = detailRoot != null ? detailRoot : (detailRoot = BattleNativeWidgets.Find(root, Detail));
            if (detail == null) return;
            detail.gameObject.SetActive(true);
            if (unchanged)
            {
                if (detailArtwork != null && detailArtwork.texture == null) session.LoadArt(card.id, detailArtwork);
                return;
            }
            LabelDetail("TextCardName", card.DisplayName);
            LabelDetail("TextCardNumValueTMP", card.id);
            LabelDetail("TextDescriptionItemTMP", "HIỆU ỨNG / " + card.type);
            string status = !card.IsEligibleFor(BattleSpiritsMode.RulesFormat) ? "Ngoài pool Standard hoặc chưa xác minh hợp lệ"
                : card.IsPlayableFor(BattleSpiritsMode.RulesFormat) ? "Standard · đã hỗ trợ trong trận" : "Standard · chỉ tra cứu; hiệu ứng chưa triển khai";
            LabelDetail("TextDescriptionValueTMP", card.effectTextJa + "\n\n" + card.setName + "\n" + status);
            LabelDetail("TextLevelTMP", "Cost " + card.cost + " / ↓" + card.reductionSymbols);
            LabelDetail("TextAtkTMP", string.Join(" / ", (card.levels ?? new List<CardLevel>()).Select(level => level.bp)) + " BP");
            LabelDetail("TextDefTMP", "Symbol " + card.symbols);
            foreach (var suffix in new[] { "IconAttribute", "IconTuner", "IconRank", "IconLink", "IconPendulumScale", "SpellTrapType", "IconSpellTrapType", "IconLimit", "IconAtk", "IconDef", "IconLevel" })
            { var icon = DetailNode(suffix); if (icon != null) icon.gameObject.SetActive(false); }
            if (detailArtwork == null) detailArtwork = DetailNode("CardArea/ImageCard")?.GetComponent<RawImage>();
            if (detailArtwork != null) session.LoadArt(card.id, detailArtwork);
            var symbolSlot = DetailNode("IconAttribute");
            if (detailColorSymbol == null)
            {
                if (symbolSlot == null)
                {
                    var slot = BattleNativeWidgets.Rect(detail, "BS_ColorSymbolSlot");
                    BattleNativeWidgets.Place(slot, 0.87f, 0.88f, 0.96f, 0.96f); symbolSlot = slot;
                }
                foreach (var old in symbolSlot.GetComponentsInChildren<Image>(true)) old.enabled = false;
                var icon = BattleNativeWidgets.Rect(symbolSlot, "BS_ColorSymbol"); BattleNativeWidgets.Place(icon, 0, 0, 1, 1);
                detailColorSymbol = icon.gameObject.AddComponent<RawImage>(); detailColorSymbol.raycastTarget = false;
            }
            detailColorSymbol.texture = BattleTheme.ColorIcon(card.color);
            detailColorSymbol.transform.parent.gameObject.SetActive(detailColorSymbol.texture != null);
            detailRendered = true;
        }
        private Transform DetailNode(string suffix)
        {
            if (!detailNodes.TryGetValue(suffix, out var node))
            { node = BattleNativeWidgets.Find(detailRoot, suffix); detailNodes[suffix] = node; }
            return node;
        }
        private void LabelDetail(string suffix, string value) => BattleNativeWidgets.SetLabel(DetailNode(suffix), value);
        private void Add()
        {
            if (selected == null) return;
            if (!selected.IsEligibleFor(BattleSpiritsMode.RulesFormat))
            { MessageManager.Cast("Thẻ này không thuộc pool Standard đã xác minh."); return; }
            if (!copyLimits.TryGetValue(selected.nameJa, out int limit)) return;
            if (deck.Count(id => cards.TryGetValue(id, out var card) && card.nameJa == selected.nameJa) >= limit)
            { MessageManager.Cast("Tối đa " + limit + " lá cùng tên."); return; }
            deck.Add(selected.id); RenderDeck();
        }
        private void RemoveSelected() { if (selected != null && deck.Remove(selected.id)) RenderDeck(); }
        private void RenderDeck()
        {
            if (deckContent == null) return;
            // Match occurrences before allocating/rebinding. Sorting or removing
            // a card moves its existing widget instead of reloading every face.
            orderedDeckSlots.Clear(); usedDeckSlots.Clear();
            for (int i = 0; i < deck.Count; i++)
            {
                string id = deck[i];
                var existing = deckSlots.FirstOrDefault(slot => slot.Id == id && !usedDeckSlots.Contains(slot));
                orderedDeckSlots.Add(existing);
                if (existing != null) usedDeckSlots.Add(existing);
            }
            for (int i = 0; i < deck.Count; i++)
            {
                var slot = orderedDeckSlots[i];
                if (slot == null)
                {
                    slot = deckSlots.FirstOrDefault(candidate => !usedDeckSlots.Contains(candidate));
                    if (slot == null)
                    {
                        slot = BattleNativeWidgets.CreateCardSlot(deckContent, session, deckTemplate);
                        deckSlots.Add(slot);
                        var captured = slot;
                        slot.Bind(deck[i], () => RemoveSlot(captured));
                    }
                    orderedDeckSlots[i] = slot; usedDeckSlots.Add(slot);
                }
                slot.Index = i; slot.Bind(deck[i]);
                slot.Button.transform.SetSiblingIndex(i + (deckTemplate != null ? 1 : 0));
            }
            foreach (var slot in deckSlots) if (!usedDeckSlots.Contains(slot)) slot.Hide();
            BattleNativeWidgets.Label(root, Deck + "/Scroll View/Viewport/Content/MainDeckView/Subhedding/Left/TextMainDeckCardNumTMP", deck.Count + " / 40+");
            BattleNativeWidgets.Label(root, Deck + "/HeaderArea/DeckNameText", name?.text ?? session.SelectedDeck?.name ?? "Battle Spirits");
            layout?.Refresh(deck.Count);
        }
        private void RemoveSlot(BattleNativeWidgets.CardSlot slot)
        {
            int index = slot.Index;
            if (index < 0 || index >= deck.Count || deck[index] != slot.Id) return;
            if (cards.TryGetValue(slot.Id, out var card)) Select(card);
            deck.RemoveAt(index); RenderDeck();
        }
        private bool Save()
        {
            var definition = new DeckDefinition { name = string.IsNullOrWhiteSpace(name?.text) ? "Battle Spirits Standard" : name.text.Trim(), format = BattleSpiritsMode.RulesFormat.ToString(), cards = deck.ToArray() };
            if (!session.SaveDeck(definition, out var error)) { MessageManager.Cast(error); return false; }
            session.SelectDeck(definition); loadedDeck = Signature(definition); MessageManager.Cast("Đã lưu " + definition.name); return true;
        }
    }
}
