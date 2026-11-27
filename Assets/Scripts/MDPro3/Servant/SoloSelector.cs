using MDPro3.BattleSpirits;
using MDPro3.UI;
using MDPro3.UI.ServantUI;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MDPro3.Servant
{
    public class SoloSelector : Servant
    {
        public class BotInfo
        {
            public string name, command, desc;
            public string[] flags;
            public int main0;
        }
        public static readonly List<BotInfo> bots = new();
        public static string port = "7911";
        public enum Condition { ForSolo, ForRoom }
        public static Condition condition;
        [HideInInspector] public SelectionToggle_Solo lastSoloItem;
        private string lastOpponent;
        private string lastDeck;
        private bool retry;
        private bool previewOpen;
        public override int Depth => 3;
        protected override bool ShowLine => false;
        public override void Initialize()
        {
            returnServant = Program.instance.menu;
            base.Initialize();
            LoadBots();
        }
        public void SwitchCondition(Condition value)
        {
            condition = value;
            returnServant = value == Condition.ForSolo ? Program.instance.menu : Program.instance.online;
        }
        protected override void FirstLoadEvent() { base.FirstLoadEvent(); Print(); }
        protected override void ApplyShowArrangement(int depth)
        {
            if (bots.Count == 0) LoadBots();
            base.ApplyShowArrangement(depth);
            if (retry) { retry = false; BattleIntegration.StartSolo(lastDeck, lastOpponent); }
        }
        public override void Select(bool forced = false)
        {
            if (!forced && !UserInput.NeedDefaultSelect()) return;
            if (lastSoloItem != null) lastSoloItem.GetSelectable().Select();
            else servantUI?.SelectDefaultSelectable();
        }
        public void LoadBots()
        {
            bots.Clear();
            string[] descriptions = {
                "Infernowing — red pressure, BP removal and Soul Core combinations. Standard difficulty.",
                "Dark Purple — core control, discard recovery and graveyard combinations. Expert difficulty.",
                "Armored Fish — green core growth, exhaustion and efficient attacks. Standard difficulty.",
                "Kamen Rider Ex-Aid — multicolor Rider combinations and resilient finishers. Expert difficulty."
            };
            int i = 0;
            foreach (var deck in BattleCatalog.StarterDecks)
            {
                if (i >= 4) break;
                string first = deck.cards.Length > 0 ? deck.cards[0] : "";
                bots.Add(new BotInfo { name = deck.name, command = deck.name, desc = descriptions[i],
                    flags = new[] { i == 1 || i == 3 ? "Expert" : "Standard" }, main0 = BattleCatalog.GetNumericId(first) });
                i++;
            }
            Print();
        }
        private void Print() { if (servantUI != null) GetUI<SoloSelectorUI>().Print(); }

        public void ShowSoloPreview(int aiCode, bool customDeck)
        {
            if (previewOpen || aiCode < 0 || aiCode >= bots.Count) return;

            var bot = bots[aiCode];
            string opponent = customDeck ? GetUI<SoloSelectorUI>().GetAIDeck() : bot.command;
            string deck = Config.GetConfigDeckName();
            string player = MatchLabel(Config.Get("DuelPlayerName0", "Player"), "Player");
            string deckName = MatchLabel(Path.GetFileName(deck), "Default deck");
            string opponentName = MatchLabel(customDeck ? opponent : bot.name, "Opponent");
            string summary = "<color=#8CE7BF>PLAYER</color>\n<b>" + player + "</b>\nDeck · " + deckName
                + "\n\n<color=#F5BC78>CPU OPPONENT</color>\n<b>" + opponentName + "</b>"
                + "\n\n5 Life per side   ·   4 opening cards";
            bool approved = false;
            previewOpen = true;
            UIManager.ShowPopupYesOrNo(new List<string> { "Standard · Solo Match", summary, "Start Duel", "Back" },
                () => approved = true, null, () =>
                {
                    previewOpen = false;
                    bool start = approved;
                    approved = false;
                    if (start && this != null && Program.instance?.currentServant == this)
                        StartSelectedSolo(deck, opponent);
                });
        }

        private static string MatchLabel(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback
                : value.Replace('<', '‹').Replace('>', '›').Replace('\r', ' ').Replace('\n', ' ');
        }

        public void StartAIForSolo(int aiCode, bool customDeck)
        {
            if (aiCode < 0 || aiCode >= bots.Count) return;
            string opponent = customDeck ? GetUI<SoloSelectorUI>().GetAIDeck() : bots[aiCode].command;
            StartSelectedSolo(Config.GetConfigDeckName(), opponent);
        }

        private void StartSelectedSolo(string deck, string opponent)
        {
            lastDeck = deck;
            lastOpponent = opponent;
            BattleIntegration.StartSolo(deck, opponent);
        }
        public void StartAIForRoom(int aiCode, bool customDeck)
        { MessageManager.Cast("Battle Spirits AI opponents are available in Solo."); }
        public void StartAIForHandTest(int ignoredPort) { StartAIForSolo(0, false); }
        public void StartWindBot(string command, string ip, string remotePort, string password, bool lockHand, int delay)
        { MessageManager.Cast("Choose a Battle Spirits opponent in Solo."); }
        public void Launch(string command, bool lockHand, bool noCheck, bool noShuffle)
        { BattleIntegration.StartSolo(Config.GetConfigDeckName(), command); }
        public bool CanRetryLastSoloDuel() => !string.IsNullOrEmpty(lastOpponent);
        public void QueueRetryLastSoloDuel() { retry = CanRetryLastSoloDuel(); }
    }
}
