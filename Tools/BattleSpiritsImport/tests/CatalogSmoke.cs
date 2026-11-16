using System;
using System.IO;
using System.Linq;
using MDPro3.BattleSpirits;
using MDPro3.BattleSpirits.Core;

// Catalog-only test dependency: gameplay registry behavior is tested by its owner.
namespace MDPro3.BattleSpirits.Core { public static class AbilityRegistry { public static bool Contains(string id) => false; } }

static class CatalogSmoke
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Main()
    {
        string root = Path.Combine(Directory.GetCurrentDirectory(), "Assets/StreamingAssets");
        BattleCatalog.Initialize(root, Path.Combine(Path.GetTempPath(), "bs-catalog-user"));
        Check(BattleCatalog.Cards.Count == 62, "All 62 cards must deserialize through the actual public API.");
        Check(BattleCatalog.StarterDecks.Count == 4, "Four starter decks must load.");
        Check(BattleCatalog.Cards.Count(c => c.IsSpirit) == 43, "Spirit type checks must match.");
        Check(BattleCatalog.Cards.Count(c => c.IsNexus) == 3, "Nexus type checks must match.");
        Check(BattleCatalog.Cards.Count(c => c.IsMagic) == 16, "Magic type checks must match.");
        foreach (var card in BattleCatalog.Cards)
        {
            Check(BattleCatalog.TryGet(card.numericId, out var numericCard) && ReferenceEquals(card, numericCard), "Numeric lookup must preserve identity.");
            Check(BattleCatalog.GetNumericId(card.id) == card.numericId, "Explicit mapping must round trip.");
            Check(BattleCatalog.GetCardId(card.numericId) == card.id, "Printed ID must round trip.");
            Check(File.Exists(BattleCatalog.CardImagePath(card.id)), "Original or English face must exist.");
            Check((BattleCatalog.StandeePath(card.id) != null) == card.IsSpirit, "Only Spirits receive standees.");
        }
        foreach (var deck in BattleCatalog.StarterDecks)
        {
            Check(deck.cards.Length == 40, "Every recipe must contain exactly 40 cards.");
            var validation = DeckValidator.Validate(BattleCatalog.Cards, deck.cards, requireImplemented: false);
            Check(validation.IsValid, "Typed Standard legality and copy-limit validation: " + validation.Summary);
        }
        Check(BattleCatalog.GetVisual("ritenia").isBoss, "Ritenia alias must use the approved boss assignment.");
        Check(!BattleCatalog.GetVisual("pisketona").isBoss, "Pisketona must remain normal presentation.");
        Check(BattleCatalog.GetVisual("pisketona").width == 1414, "Pisketona alpha bounds must retain source dimensions.");
        Check(BattleCatalog.CardImagePath("pisketona").EndsWith(".jpg"), "The original JPEG must retain its extension and bytes.");
        Check(BattleCatalog.GetNumericId("not-a-card") == 0 && BattleCatalog.GetCardId(42) == null, "Unknown IDs must not fall back to another card.");
        var snapshot = BattleCatalog.Cards;
        BattleCatalog.Initialize(Path.Combine(root, "BattleSpirits"));
        Check(ReferenceEquals(snapshot, BattleCatalog.Cards), "Repeated initialization must be idempotent.");
        Console.WriteLine("Catalog API smoke passed: 62 typed cards, 4 valid Standard recipes, numeric round trips, exact image paths, bounds and boss assignment.");
    }
}
