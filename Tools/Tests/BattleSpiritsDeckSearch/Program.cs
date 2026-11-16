using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MDPro3.BattleSpirits;
using MDPro3.Duel.YGOSharp;

public sealed class SearchInput
{
    public readonly SearchText InputField = new SearchText();
}
public sealed class SearchText { public string text = ""; }
public sealed class SearchButton
{
    public string Caption;
    public void SetButtonText(string caption) { Caption = caption; }
}
public static class DeckEditor { public static Banlist Banlist => null; }

// Only unavailable widget surfaces are doubled. Start, ShowArea, PrintSearchCards
// and RefreshCurrentArea below are extracted from the current production source.
public partial class CollectionFixture
{
    public enum Area { Collection, Bookmark, History }
    public Area area = Area.Collection, defaultArea = Area.Collection;
    public bool showingRelatedCards;
    public Card relatedCardData;
    public readonly SearchInput InputSearch = new SearchInput();
    public readonly SearchButton ButtonSearch = new SearchButton();
    public readonly List<long> filters = new List<long>();
    public string packName = "";
    public List<int> Printed = new List<int>();
    public int Prints, ToolsRefreshed, DropAreasSet;
    public void StartNow() { Start(); }
    public void RefreshNow() { RefreshCurrentArea(); }
    private void RefreshToolArea() { ToolsRefreshed++; }
    private void HideRelatedCardArea() { }
    private void SetDropArea() { DropAreasSet++; }
    private void SortCards(List<Card> cards) { cards.Sort(CardsManager.ComparisonOfCard()); }
    private void PrintCards(List<int> cards) { Printed = cards; Prints++; }
    private void ShowRelatedCard(Card card) { PrintRelatedCards(card); }
    private void PrintRelatedCards(Card card) { PrintCards(CardsManager.RelatedSearch(card.Id).Select(c => c.Id).ToList()); }
    private void PrintBookmarkCards() { PrintCards(new List<int>()); }
    private void PrintHistoryCards() { PrintCards(new List<int>()); }
}

public static class DeckSearchTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    public static int Main(string[] args)
    {
        var root = Path.GetFullPath(args[0]);
        UnityEngine.Application.streamingAssetsPath = Path.Combine(root, "Assets/StreamingAssets");
        UnityEngine.Application.persistentDataPath = Path.Combine(Path.GetTempPath(), "bs-search-" + Guid.NewGuid().ToString("N"));
        CardsManager.Initialize();
        var catalogIds = new HashSet<int>(BattleCatalog.Cards.Select(card => card.numericId));
        Check(catalogIds.Count == 62, "The shipped catalog has 62 distinct explicit numeric IDs.");
        var view = new CollectionFixture();
        Check(view.area == view.defaultArea, "The initial native collection enum is already selected.");
        view.StartNow();
        Check(view.Prints == 1 && view.Printed.Count == 62, "Start must populate the initially selected tab with all 62 shipped cards.");
        Check(catalogIds.SetEquals(view.Printed) && view.Printed.Distinct().Count() == 62,
            "Initial results contain exactly the catalog IDs with no duplicates or unknown cards.");
        Check(view.ButtonSearch.Caption == "62", "The search button reports the current result count.");
        view.ShowArea(CollectionFixture.Area.Collection);
        Check(view.Prints == 2 && view.Printed.Count == 62, "Native tab Start may repeat ShowArea(Collection) without leaving the grid empty.");
        Check(view.DropAreasSet == 2, "The default selected tab still configures its native drop target.");
        foreach (var definition in BattleCatalog.Cards)
        {
            view.InputSearch.InputField.text = definition.id.ToLowerInvariant();
            view.RefreshNow();
            Check(view.Printed.SequenceEqual(new[] { definition.numericId }), "Printed-ID search returns its exact card: " + definition.id);
            view.InputSearch.InputField.text = definition.DisplayName;
            view.RefreshNow();
            Check(view.Printed.Contains(definition.numericId), "English name search finds its card: " + definition.id);
        }
        foreach (int cost in BattleCatalog.Cards.Select(card => card.cost).Distinct())
        {
            view.InputSearch.InputField.text = "cost:" + cost;
            view.RefreshNow();
            var expected = BattleCatalog.Cards.Where(card => card.cost == cost).Select(card => card.numericId);
            Check(new HashSet<int>(view.Printed).SetEquals(expected), "Live cost search returns every matching catalog card at cost " + cost + ".");
        }
        foreach (int bp in BattleCatalog.Cards.SelectMany(card => card.levels).Select(level => level.bp).Distinct())
        {
            view.InputSearch.InputField.text = "bp:" + bp;
            view.RefreshNow();
            var expected = BattleCatalog.Cards.Where(card => card.levels.Any(level => level.bp >= bp)).Select(card => card.numericId);
            Check(new HashSet<int>(view.Printed).SetEquals(expected), "Live BP search returns every card with a level at or above " + bp + ".");
        }
        view.InputSearch.InputField.text = "draw";
        view.RefreshNow();
        Check(view.Printed.Count > 0 && view.Printed.All(id => BattleCatalog.TryGet(id, out var c) && (c.effectTextEn + c.DisplayName).IndexOf("draw", StringComparison.OrdinalIgnoreCase) >= 0), "Effect search uses English printed effects.");
        var drawMatches = BattleCatalog.Cards.Where(card => (card.effectTextEn + card.DisplayName).IndexOf("draw", StringComparison.OrdinalIgnoreCase) >= 0).Select(card => card.numericId);
        Check(new HashSet<int>(view.Printed).SetEquals(drawMatches), "Effect search includes every matching printed English effect.");
        view.InputSearch.InputField.text = "DRAW";
        view.RefreshNow();
        Check(new HashSet<int>(view.Printed).SetEquals(drawMatches), "English effect search is case insensitive.");
        view.InputSearch.InputField.text = "COST:4 BP:5000";
        view.RefreshNow();
        var combined = BattleCatalog.Cards.Where(card => card.cost == 4 && card.levels.Any(level => level.bp >= 5000)).Select(card => card.numericId);
        Check(new HashSet<int>(view.Printed).SetEquals(combined), "Cost and BP terms combine without excluding valid matches.");
        view.InputSearch.InputField.text = "cost:invalid";
        view.RefreshNow();
        Check(view.Printed.Count == 0 && view.ButtonSearch.Caption == "Search", "Malformed search bounds return no results without throwing.");
        view.InputSearch.InputField.text = "bp:invalid";
        view.RefreshNow();
        Check(view.Printed.Count == 0, "Malformed BP bounds return no results without throwing.");
        view.InputSearch.InputField.text = "no-such-battle-spirits-card";
        view.RefreshNow();
        Check(view.Printed.Count == 0, "An unmatched query clears previous results.");
        view.InputSearch.InputField.text = "";
        view.RefreshNow();
        Check(view.Printed.Count == 62 && catalogIds.SetEquals(view.Printed), "Clearing the text immediately restores the complete catalog.");
        view.InputSearch.InputField.text = "   \u3000 ";
        view.RefreshNow();
        Check(view.Printed.Count == 62 && catalogIds.SetEquals(view.Printed), "Whitespace-only input behaves as an empty search.");
        view.ShowArea(CollectionFixture.Area.History);
        Check(view.Printed.Count == 0, "History retains its own content.");
        view.ShowArea(CollectionFixture.Area.Collection);
        Check(view.Printed.Count == 62, "Returning to Card List restores real results.");
        Console.WriteLine("PASS: " + checks + " production collection initialization/search assertions using the shipped 62-card catalog; Unity rendering not run.");
        return 0;
    }
}
