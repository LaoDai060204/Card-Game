using MDPro3.UI.ServantUI;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MDPro3.UI
{
    public class SelectionToggle_Replay : SelectionToggle_ScrollRectItem
    {

        #region Elements

        private const string LABEL_TXT_TITLE = "Title";
        private TextMeshProUGUI m_Title;
        private TextMeshProUGUI Title =>
            m_Title = m_Title != null ? m_Title
            : Manager.GetElement<TextMeshProUGUI>(LABEL_TXT_TITLE);

        private const string LABEL_ART = "Image";
        private ArtRawImageHandler m_Art;
        private ArtRawImageHandler Art =>
            m_Art = m_Art != null ? m_Art
            : Manager.GetElement<ArtRawImageHandler>(LABEL_ART);

        private const string LABEL_GO_NUMBADGE = "NumBadge";
        private GameObject m_NumBadge;
        private GameObject NumBadge =>
            m_NumBadge = m_NumBadge != null ? m_NumBadge
            : Manager.GetElement(LABEL_GO_NUMBADGE);

        private const string LABEL_GO_TEXTCLEAR = "TextClear";
        private GameObject m_TextClear;
        private GameObject TextClear =>
            m_TextClear = m_TextClear != null ? m_TextClear
            : Manager.GetElement(LABEL_GO_TEXTCLEAR);

        #endregion

        public string replayName;
        public override void Refresh()
        {
            base.Refresh();
            Title.text = System.IO.Path.GetFileNameWithoutExtension(replayName);
            NumBadge.SetActive(false); TextClear.SetActive(false);
            int preview = MDPro3.BattleSpirits.BattleCatalog.GetNumericId("26RBS03-X01");
            Art.SetArt(preview);
        }
        protected override void CallToggleOnEvent()
        {
            base.CallToggleOnEvent();
            Program.instance.replay.lastSelectedReplayItem = this;
            var ui = Program.instance.replay.GetUI<ReplaySelectorUI>();
            ui.superScrollView.selected = index;
            ui.TextOverview.text = "Battle Spirits — recorded rules commands and events\n" + System.IO.Path.GetFileName(replayName);
        }

        protected override void CallSubmitEvent()
        {
            Program.instance.replay.GetUI<ReplaySelectorUI>().KF_Replay(replayName);
        }

        protected override void OnNavigation(AxisEventData eventData)
        {
            base.OnNavigation(eventData);

            if (eventData.moveDir == MoveDirection.Right)
            {
                UserInput.NextSelectionIsAxis = true;
                var deckButton = Program.instance.replay.GetUI<ReplaySelectorUI>().ButtonPlayer0;
                if (deckButton.gameObject.activeSelf)
                    deckButton.GetSelectable().Select();
                else
                    Program.instance.replay.GetUI<ReplaySelectorUI>().ButtonGodView.GetSelectable().Select();
            }
        }
    }
}
