using MDPro3.BattleSpirits;
using MDPro3.UI;
using MDPro3.UI.ServantUI;
using System.Collections.Generic;
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
            base.ApplyShowArrangement(depth);
            LoadBots();
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
        public void StartAIForSolo(int aiCode, bool customDeck)
        {
            if (aiCode < 0 || aiCode >= bots.Count) return;
            lastOpponent = customDeck ? GetUI<SoloSelectorUI>().GetAIDeck() : bots[aiCode].command;
            lastDeck = Config.GetConfigDeckName();
            BattleIntegration.StartSolo(lastDeck, lastOpponent);
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
