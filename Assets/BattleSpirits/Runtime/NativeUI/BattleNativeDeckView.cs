using System;
using System.Collections.Generic;
using System.Linq;
using LaoDai.BattleSpirits.Core;
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
        private TMP_InputField search, name;
        private CardDefinition selected;
        private int color = -1, sort, support, page;
        private string loadedDeck;
        private const int PageSize = 42;
        private const string Collection = "CardCollectionView/Root/Window";
        private const string Deck = "DeckView/Root/Window";
        private const string Detail = "CardDetailView/Root/Window/Content";
        private const string Header = "DeckEditHeader/Root/Header";

        internal BattleNativeDeckView(Transform root, IBattleNativeSession session)
        {
            this.root = root; this.session = session;
            cards = session.Cards.ToDictionary(card => card.id, StringComparer.Ordinal);
            deck = new List<string>(session.SelectedDeck?.cards ?? Array.Empty<string>());
            loadedDeck = Signature(session.SelectedDeck);
        }
        internal void Build()
        {
            collection = BattleNativeWidgets.Component<RectTransform>(root, Collection + "/CardListArea/CollectionAreaCenter/ScrollRect/Viewport/Content");
            deckContent = BattleNativeWidgets.Component<RectTransform>(root, Deck + "/Scroll View/Viewport/Content/MainDeckView/MainDeckContent");
            collectionTemplate = collection?.Find("Template") as RectTransform; deckTemplate = deckContent?.Find("Template") as RectTransform;
            if (collectionTemplate != null) collectionTemplate.gameObject.SetActive(false);
            if (deckTemplate != null) deckTemplate.gameObject.SetActive(false);
            BattleNativeWidgets.Grid(collection, 5); BattleNativeWidgets.Grid(deckContent, 6, 92, 138);
            search = BattleNativeWidgets.Input(root, Collection + "/FilterAndSortArea/UpperArea/InputField", string.Empty);
            name = BattleNativeWidgets.Input(root, Deck + "/HeaderArea/NameAreaGroup/InputField", session.SelectedDeck?.name ?? "Battle Spirits Standard");
            if (search != null) search.onValueChanged.AddListener(_ => { page = 0; RenderCollection(); });
            BattleNativeWidgets.Bind(root, Header + "/ButtonBack", BattleNativeNavigation.ReturnToMenu);
            BattleNativeWidgets.Bind(root, Header + "/ButtonSave", () => Save(), "LƯU");
            BattleNativeWidgets.Bind(root, Header + "/ButtonTest", () => { if (Save()) BattleNativeNavigation.Open(BattleNativeScreen.Solo); }, "ĐẤU THỬ");
            BattleNativeWidgets.Bind(root, Header + "/ButtonSort", () => { deck.Sort(CompareCards); RenderDeck(); }, "SẮP XẾP");
            BattleNativeWidgets.Bind(root, Header + "/ButtonChangeSide", () => { deck.Clear(); RenderDeck(); }, "XÓA BỘ BÀI");
            BattleNativeWidgets.Bind(root, Header + "/ButtonRegulation", () => { support = (support + 1) % 3; page = 0; RenderCollection(); }, "ETERNAL");
            BattleNativeWidgets.Bind(root, Header + "/ButtonMenu", NextDeck, "MỞ BỘ BÀI");
            BattleNativeWidgets.Bind(root, Collection + "/FilterAndSortArea/UpperArea/SearchButton", RenderCollection);
            BattleNativeWidgets.Bind(root, Collection + "/FilterAndSortArea/BottomArea/FilterToggle", CycleColor, "TẤT CẢ MÀU");
            BattleNativeWidgets.Bind(root, Collection + "/FilterAndSortArea/BottomArea/SortButton", () => { sort = (sort + 1) % 3; page = 0; RenderCollection(); }, "MỚI NHẤT");
            BattleNativeWidgets.Bind(root, Collection + "/FilterAndSortArea/BottomArea/ClearButton", () => { if (search != null) search.text = string.Empty; color = -1; support = 0; page = 0; RenderCollection(); });
            BattleNativeWidgets.Bind(root, Collection + "/TabArea/CardListToggle", () => { support = 0; RenderCollection(); }, "TẤT CẢ");
            BattleNativeWidgets.Bind(root, Collection + "/TabArea/BookmarkToggle", () => { support = 1; RenderCollection(); }, "CHƠI ĐƯỢC");
            BattleNativeWidgets.Bind(root, Collection + "/TabArea/HistotyToggle", () => { support = 2; RenderCollection(); }, "ETERNAL");
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
            BattleNativeWidgets.Label(root, Deck + "/Scroll View/Viewport/Content/MainDeckView/Subhedding/Left/#TextMainDeckTMP", "BỘ BÀI ETERNAL");
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
            string signature = Signature(session.SelectedDeck);
            if (signature == loadedDeck) return;
            loadedDeck = signature;
            deck.Clear(); deck.AddRange(session.SelectedDeck?.cards ?? Array.Empty<string>());
            if (name != null) name.SetTextWithoutNotify(session.SelectedDeck?.name ?? "Battle Spirits Standard");
            RenderDeck();
        }
        private void CycleColor()
        {
            color = color == 5 ? -1 : color + 1; page = 0;
            string[] names = { "ĐỎ", "TÍM", "LỤC", "TRẮNG", "VÀNG", "LAM" };
            BattleNativeWidgets.Label(root, Collection + "/FilterAndSortArea/BottomArea/FilterToggle", color < 0 ? "TẤT CẢ MÀU" : names[color]);
            RenderCollection();
        }
        private void RenderCollection()
        {
            if (collection == null) return;
            string query = (search?.text ?? string.Empty).Trim();
            IEnumerable<CardDefinition> values = session.Cards;
            if (query.Length > 0) values = values.Where(card => Matches(card, query));
            if (color >= 0) values = values.Where(card => card.color == (CardColor)color);
            if (support == 1) values = values.Where(card => card.IsPlayableFor(BattleSpiritsMode.RulesFormat));
            if (support == 2) values = values.Where(card => card.IsEligibleFor(BattleSpiritsMode.RulesFormat));
            values = sort == 1 ? values.OrderBy(card => card.cost).ThenBy(card => card.id, StringComparer.Ordinal)
                : sort == 2 ? values.OrderBy(card => card.DisplayName, StringComparer.Ordinal)
                : values.OrderByDescending(card => card.releaseDate, StringComparer.Ordinal).ThenBy(card => card.id, StringComparer.Ordinal);
            var all = values.ToArray(); page = Math.Max(0, Math.Min(page, Math.Max(0, (all.Length - 1) / PageSize)));
            BattleNativeWidgets.ClearGenerated(collection);
            foreach (var card in all.Skip(page * PageSize).Take(PageSize))
                BattleNativeWidgets.Card(collection, card.id, () => Select(card), session, collectionTemplate);
            BattleNativeWidgets.Label(root, Collection + "/FilterAndSortArea/BottomArea/SortButton", (sort == 1 ? "COST" : sort == 2 ? "TÊN" : "MỚI NHẤT") + " · " + all.Length + " · " + (page + 1));
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
            selected = card;
            var detail = BattleNativeWidgets.Find(root, Detail);
            if (detail == null) return;
            detail.gameObject.SetActive(true);
            BattleNativeWidgets.Label(detail, "TextCardName", card.DisplayName);
            BattleNativeWidgets.Label(detail, "TextCardNumValueTMP", card.id);
            BattleNativeWidgets.Label(detail, "TextDescriptionItemTMP", "HIỆU ỨNG / " + card.type);
            string status = !card.IsEligibleFor(BattleSpiritsMode.RulesFormat) ? "Ngoài pool Standard hoặc chưa xác minh hợp lệ"
                : card.IsPlayableFor(BattleSpiritsMode.RulesFormat) ? "Standard · đã hỗ trợ trong trận" : "Standard · chỉ tra cứu; hiệu ứng chưa triển khai";
            BattleNativeWidgets.Label(detail, "TextDescriptionValueTMP", card.effectTextJa + "\n\n" + card.setName + "\n" + status);
            BattleNativeWidgets.Label(detail, "TextLevelTMP", "Cost " + card.cost + " / ↓" + card.reductionSymbols);
            BattleNativeWidgets.Label(detail, "TextAtkTMP", string.Join(" / ", (card.levels ?? new List<CardLevel>()).Select(level => level.bp)) + " BP");
            BattleNativeWidgets.Label(detail, "TextDefTMP", "Symbol " + card.symbols);
            foreach (var suffix in new[] { "IconAttribute", "IconTuner", "IconRank", "IconLink", "IconPendulumScale", "SpellTrapType", "IconSpellTrapType", "IconLimit", "IconAtk", "IconDef", "IconLevel" }) BattleNativeWidgets.Active(detail, suffix, false);
            var art = BattleNativeWidgets.Component<RawImage>(detail, "CardArea/ImageCard"); if (art != null) session.LoadArt(card.id, art);
        }
        private void Add()
        {
            if (selected == null) return;
            if (!selected.IsEligibleFor(BattleSpiritsMode.RulesFormat))
            { MessageManager.Cast("Thẻ này không thuộc pool Standard đã xác minh."); return; }
            int limit = session.Cards.Where(card => card.nameJa == selected.nameJa && card.standardBlock == "A"
                && card.standardLegalityVerified && (card.format == "Standard" || card.format == "Both")).Min(card => card.EffectiveCopyLimit);
            if (deck.Count(id => cards.TryGetValue(id, out var card) && card.nameJa == selected.nameJa) >= limit)
            { MessageManager.Cast("Tối đa " + limit + " lá cùng tên."); return; }
            deck.Add(selected.id); RenderDeck();
        }
        private void RemoveSelected() { if (selected != null && deck.Remove(selected.id)) RenderDeck(); }
        private void RenderDeck()
        {
            if (deckContent == null) return;
            BattleNativeWidgets.ClearGenerated(deckContent);
            for (int i = 0; i < deck.Count; i++)
            {
                int index = i; string id = deck[i];
                BattleNativeWidgets.Card(deckContent, id, () => { if (cards.TryGetValue(id, out var card)) Select(card); deck.RemoveAt(index); RenderDeck(); }, session, deckTemplate);
            }
            BattleNativeWidgets.Label(root, Deck + "/Scroll View/Viewport/Content/MainDeckView/Subhedding/Left/TextMainDeckCardNumTMP", deck.Count + " / 40+");
            BattleNativeWidgets.Label(root, Deck + "/HeaderArea/DeckNameText", name?.text ?? session.SelectedDeck?.name ?? "Battle Spirits");
        }
        private bool Save()
        {
            var definition = new DeckDefinition { name = string.IsNullOrWhiteSpace(name?.text) ? "Battle Spirits Standard" : name.text.Trim(), format = BattleSpiritsMode.RulesFormat.ToString(), cards = deck.ToArray() };
            if (!session.SaveDeck(definition, out var error)) { MessageManager.Cast(error); return false; }
            session.SelectDeck(definition); loadedDeck = Signature(definition); MessageManager.Cast("Đã lưu " + definition.name); return true;
        }
    }
}
