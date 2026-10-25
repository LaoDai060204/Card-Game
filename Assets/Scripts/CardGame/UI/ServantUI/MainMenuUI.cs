using CardGame.Net;
using CardGame;
using UnityEngine;
using UnityEngine.Networking;
using YgomSystem.ElementSystem;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using CardGame.Servant;

namespace CardGame.UI.ServantUI
{
    public class MainMenuUI : ServantUI
    {
        #region Elements

        private const string LABEL_MYCARD_NEWS = "News";
        private NewsManager m_News;
        public NewsManager News =>
            m_News = m_News != null ? m_News
            : Manager.GetElement<NewsManager>(LABEL_MYCARD_NEWS);

        #endregion

        public override void Initialize(Servant.Servant servant)
        {
            base.Initialize(servant);

            Title.text = Application.productName + " v" + Application.version;
        }

        #region Button Function

        public void OnSolo()
        {
            if (Program.exitOnReturn)
                return;

            Program.instance.solo.SwitchCondition(SoloSelector.Condition.ForSolo);
            Program.instance.ShiftToServant(Program.instance.solo);
        }

        public void OnOnline()
        {
            if (Program.exitOnReturn)
                return;

            Program.instance.ShiftToServant(Program.instance.online);
        }

        public void OnPuzzle()
        {
            if (Program.exitOnReturn)
                return;

            Program.instance.ShiftToServant(Program.instance.puzzlePortal);
        }

        public void OnReplay()
        {
            if (Program.exitOnReturn)
                return;

            Program.instance.ShiftToServant(Program.instance.replay);
        }

        public void OnCutin()
        {
            if (Program.exitOnReturn)
                return;

            Program.instance.ShiftToServant(Program.instance.cutin);
        }
        public void OnMate()
        {
            if (Program.exitOnReturn)
                return;

            Program.instance.ShiftToServant(Program.instance.mate);
        }

        public void OnDeck()
        {
            if (Program.exitOnReturn)
                return;

            Program.instance.deckSelector.SwitchCondition(DeckSelector.Condition.ForEdit);
            Program.instance.ShiftToServant(Program.instance.deckSelector);
        }

        public void OnSetting()
        {
            if (Program.exitOnReturn)
                return;

            Program.instance.ShiftToServant(Program.instance.setting);
        }

        public void OnExit()
        {
            if (Program.exitOnReturn)
                return;

            List<string> selections = new()
            {
                InterString.Get("Confirm Exit"),
                InterString.Get("Are you sure you want to exit the game?"),
                InterString.Get("Confirm"),
                InterString.Get("Cancel"),
                Config.STRING_YES
            };
            UIManager.ShowPopupYesOrNo(selections, Program.GameQuit, null);
        }

        #endregion
    }
}
