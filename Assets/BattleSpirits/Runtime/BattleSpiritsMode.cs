namespace LaoDai.BattleSpirits
{
    // This branch keeps the MDPro3 presentation assets while routing gameplay
    // to the Battle Spirits rules, catalog and network protocol.
    public static class BattleSpiritsMode
    {
        public static bool Enabled => true;
        public const string Ruleset = "Japanese Eternal";
        public const string CatalogPath = "Data/BattleSpirits/cards.json";
        public const string DeckDirectory = "Data/BattleSpirits/Decks";
        public const int DefaultPort = 7777;
    }
}
