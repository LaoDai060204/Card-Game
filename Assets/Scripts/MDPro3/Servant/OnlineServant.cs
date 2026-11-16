using MDPro3.BattleSpirits;
using MDPro3.Net;
using MDPro3.UI;
using MDPro3.UI.ServantUI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MDPro3.Servant
{
    public class OnlineServant : Servant
    {
        [HideInInspector] public SelectionToggle_Address lastSelectedAddressItem;
        [HideInInspector] public SelectionToggle_Watch lastSelectedWatchItem;
        public const int DEFAULT_TIME = 180, DEFAULT_PORT = 7911, DEFAULT_LP = 5, DEFAULT_HAND = 4, DEFAULT_DRAW = 1;
        public static IEnumerator entertainMatch, athleticMatch;
        public override int Depth => 1;
        protected override bool ShowLine => true;
        public override void Initialize() { returnServant = Program.instance.menu; base.Initialize(); }
        protected override void ApplyHideArrangement(int depth) { base.ApplyHideArrangement(depth); Config.Save(); }
        public override void Select(bool forced = false)
        { if ((forced || UserInput.NeedDefaultSelect()) && servantUI != null) GetUI<OnlineServantUI>().SelectLastSelectable(lastSelectable); }
        public override void PerFrameFunction()
        {
            if (!NeedResponseInput()) return;
            if (UserInput.MouseRightDown || UserInput.WasCancelPressed) OnReturn();
            if (UserInput.WasLeftShoulderPressed) GetUI<OnlineServantUI>().PageLeft();
            if (UserInput.WasRightShoulderPressed) GetUI<OnlineServantUI>().PageRight();
        }
        public void KF_OnlineGame(string name, string ip, string port, string password)
        {
            if (string.IsNullOrWhiteSpace(name)) { MessageManager.Cast("Enter a player name."); return; }
            if (!int.TryParse(port, out var number) || number < 1 || number > 65535) { MessageManager.Cast("Enter a valid port."); return; }
            BattleIntegration.Join(ip, number, name);
        }
        public void CreateServer(List<string> args)
        { BattleIntegration.Host(DEFAULT_PORT, Config.Get("DuelPlayerName0", "Player")); }
        private void Unavailable() { MessageManager.Cast("This online service is unavailable for Battle Spirits."); }
        public void LoginMyCard(string name, string password) { Unavailable(); }
        public void EntertainMatch() { Unavailable(); }
        public void AthleticMatch() { Unavailable(); }
        public void SetWatchRooms(List<MyCardRoom> rooms) { }
        public void CreateWatchRoom(MyCardRoom room) { }
        public void UpdateWatchRoom(MyCardRoom room) { }
        public void DeleteWatchRoom(string id) { }
        public void ClearWatchList() { }
    }
}
