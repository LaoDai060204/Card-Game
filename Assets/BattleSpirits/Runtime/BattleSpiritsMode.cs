namespace LaoDai.BattleSpirits
{
    // Template screen layout, original Battle Spirits UI art and isolated
    // retained pet/wallpaper cosmetics; gameplay follows the supplied Standard rules.
    public static class BattleSpiritsMode
    {
        public static bool Enabled => true;
        public const string Ruleset = "Japanese Standard";
        public const Core.BattleRulesFormat RulesFormat = Core.BattleRulesFormat.Standard;
        public const string CatalogPath = "Data/BattleSpirits/cards.json";
        public const string DeckDirectory = "Data/BattleSpirits/Decks";
        public const int DefaultPort = 7777;
    }
}
