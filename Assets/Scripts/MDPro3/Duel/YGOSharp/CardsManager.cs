using MDPro3.BattleSpirits;
using MDPro3.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MDPro3.Duel.YGOSharp
{
    // Compatibility view models for the preserved native widgets. Rules use the BS core catalog.
    internal static class CardsManager
    {
        public static string nullName = "Unknown Battle Spirits Card";
        public static string nullString = string.Empty;
        public static string nameInSearch = string.Empty;
        public static Dictionary<int, Card> _cards = new();
        public static Dictionary<int, Card> _cardsForRender = new();
        public static List<string> setNameHead = new();
        public static List<string> setNameTail = new();

        internal static void Initialize()
        {
            BattleCatalog.Initialize(UnityEngine.Application.streamingAssetsPath, UnityEngine.Application.persistentDataPath);
            _cards = BattleCatalog.Cards.Select(BattleCardAdapter.ToNative).ToDictionary(card => card.Id);
            _cardsForRender = _cards;
            PacksManager.Initialize();
        }

        internal static void UpdateSetNames() { }
        internal static Card GetCard(int id) => _cards.TryGetValue(id, out var card) ? card.Clone() : null;
        internal static Card GetRenderCard(int id) => GetCard(id);
        internal static Card GetCardRaw(int id) => _cards.TryGetValue(id, out var card) ? card : null;
        internal static Card Get(int id, bool noneIsZero = false)
        {
            // Explicit keys only: an adjacent numeric key is always a different BS card.
            return GetCard(id) ?? new Card { Id = noneIsZero ? 0 : id, Desc = noneIsZero ? string.Empty : "Unknown Battle Spirits card" };
        }
        internal static List<Card> GetAllCards() => _cards.Values.ToList();
        internal static List<int> GetAllCardCodes() => _cards.Keys.ToList();
        internal static int GetAlias(int id) => 0;

        private static List<Card> SearchInternal(string text, List<long> filters, string pack, IEnumerable<Card> source)
        {
            nameInSearch = text ?? string.Empty;
            var terms = nameInSearch.Split(new[] { ' ', '\u3000' }, StringSplitOptions.RemoveEmptyEntries);
            return source.Where(card => card != null && card.IsBattleSpirits
                && BattleCatalog.TryGet(card.BattleSpiritsId, out var definition)
                && BattleCardAdapter.Matches(definition, terms)
                && BattleCardAdapter.MatchesFilters(definition, filters, pack)).ToList();
        }

        internal static List<Card> Search(string text, List<long> filters, Banlist banlist, string pack)
            => SearchInternal(text, filters, pack, _cards.Values);
        internal static List<Card> Search(string text, List<long> filters, Banlist banlist, string pack, IEnumerable<int> sourceIds)
            => SearchInternal(text, filters, pack, sourceIds.Distinct().Select(GetCardRaw));
        internal static List<Card> Search(string text, IEnumerable<int> sourceIds)
            => SearchInternal(text, null, string.Empty, sourceIds.Distinct().Select(GetCardRaw));
        internal static List<Card> AnnounceSearch(string text, List<int> searchCodes)
            => SearchInternal(text, null, string.Empty, _cards.Values);

        internal static List<Card> RelatedSearch(int id)
        {
            if (!BattleCatalog.TryGet(id, out var source)) return new List<Card>();
            var result = _cards.Values.Where(card => card.Id != id && BattleCatalog.TryGet(card.Id, out var definition)
                && (definition.color == source.color || (definition.families ?? Array.Empty<string>()).Any(source.HasFamily))).ToList();
            result.Sort(ComparisonOfCard());
            return result;
        }

        public static List<string> GetMiddleStrings(string text, string start, string end)
            => Regex.Matches(text, "(?<=(" + start + "))[.\\s\\S]*?(?=(" + end + "))").Cast<Match>().Select(match => match.Value).ToList();

        internal static Comparison<Card> ComparisonOfCard() => (a, b) =>
        {
            int result = string.Compare(TypeName(a), TypeName(b), StringComparison.Ordinal);
            if (result == 0) result = a.Level.CompareTo(b.Level);
            if (result == 0) result = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            if (result == 0) result = a.Id.CompareTo(b.Id);
            return result;
        };

        private static string TypeName(Card card) => BattleCatalog.TryGet(card.Id, out var definition) ? definition.type : string.Empty;

        internal static Comparison<Card> GetSort(CardCollectionView.SortOrder sortOrder)
        {
            return (a, b) =>
            {
                int result = sortOrder switch
                {
                    CardCollectionView.SortOrder.ByTypeReverse => -ComparisonOfCard()(a, b),
                    CardCollectionView.SortOrder.ByLevelUp => a.Level.CompareTo(b.Level),
                    CardCollectionView.SortOrder.ByLevelDown => b.Level.CompareTo(a.Level),
                    CardCollectionView.SortOrder.ByAttackUp => a.Attack.CompareTo(b.Attack),
                    CardCollectionView.SortOrder.ByAttackDown => b.Attack.CompareTo(a.Attack),
                    CardCollectionView.SortOrder.ByDefenceUp => a.Defense.CompareTo(b.Defense),
                    CardCollectionView.SortOrder.ByDefenceDown => b.Defense.CompareTo(a.Defense),
                    CardCollectionView.SortOrder.ByRarityUp => string.Compare(a.reality, b.reality, StringComparison.Ordinal),
                    CardCollectionView.SortOrder.ByRarityDown => string.Compare(b.reality, a.reality, StringComparison.Ordinal),
                    CardCollectionView.SortOrder.ByGPUp => a.Attribute.CompareTo(b.Attribute),
                    CardCollectionView.SortOrder.ByGPDown => b.Attribute.CompareTo(a.Attribute),
                    _ => ComparisonOfCard()(a, b)
                };
                return result == 0 ? ComparisonOfCard()(a, b) : result;
            };
        }
    }

    internal static class PacksManager
    {
        public class PackName
        {
            public string fullName, shortName;
            public int year, month, day;
        }
        public static List<PackName> packs = new();
        internal static void Initialize()
        {
            packs = BattleCatalog.Cards.Where(card => !string.IsNullOrWhiteSpace(card.setName)).GroupBy(card => card.setName)
                .Select(group => new PackName { fullName = group.Key, shortName = group.First().setId }).ToList();
        }
        internal static void InitializeSec() { }
    }
}
