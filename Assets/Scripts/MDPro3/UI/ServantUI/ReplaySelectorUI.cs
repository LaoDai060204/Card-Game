using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using TMPro;
using MDPro3.Servant;
using MDPro3.UI.PropertyOverride;

namespace MDPro3.UI.ServantUI
{
    public class ReplaySelectorUI : ServantUI
    {

        #region Elements

        private const string LABEL_SR = "ScrollRect";
        private ScrollRect m_ScrollRect;
        private ScrollRect ScrollRect =>
            m_ScrollRect = m_ScrollRect != null ? m_ScrollRect
            : Manager.GetElement<ScrollRect>(LABEL_SR);

        private const string LABEL_TXT_OVERVIEW = "TextOverview";
        private TextMeshProUGUI m_TextOverview;
        public TextMeshProUGUI TextOverview =>
            m_TextOverview = m_TextOverview != null ? m_TextOverview
            : Manager.GetElement<TextMeshProUGUI>(LABEL_TXT_OVERVIEW);

        private const string LABEL_SBN_PLAYER0 = "ButtonPlayer0";
        private SelectionButton m_ButtonPlayer0;
        public SelectionButton ButtonPlayer0 =>
            m_ButtonPlayer0 = m_ButtonPlayer0 != null ? m_ButtonPlayer0
            : Manager.GetElement<SelectionButton>(LABEL_SBN_PLAYER0);

        private const string LABEL_SBN_PLAYER1 = "ButtonPlayer1";
        private SelectionButton m_ButtonPlayer1;
        public SelectionButton ButtonPlayer1 =>
            m_ButtonPlayer1 = m_ButtonPlayer1 != null ? m_ButtonPlayer1
            : Manager.GetElement<SelectionButton>(LABEL_SBN_PLAYER1);

        private const string LABEL_SBN_PLAYER2 = "ButtonPlayer2";
        private SelectionButton m_ButtonPlayer2;
        public SelectionButton ButtonPlayer2 =>
            m_ButtonPlayer2 = m_ButtonPlayer2 != null ? m_ButtonPlayer2
            : Manager.GetElement<SelectionButton>(LABEL_SBN_PLAYER2);

        private const string LABEL_SBN_PLAYER3 = "ButtonPlayer3";
        private SelectionButton m_ButtonPlayer3;
        public SelectionButton ButtonPlayer3 =>
            m_ButtonPlayer3 = m_ButtonPlayer3 != null ? m_ButtonPlayer3
            : Manager.GetElement<SelectionButton>(LABEL_SBN_PLAYER3);

        private const string LABEL_SBN_SORT = "ButtonSort";
        private SelectionButton m_ButtonSort;
        private SelectionButton ButtonSort =>
            m_ButtonSort = m_ButtonSort != null ? m_ButtonSort
            : Manager.GetElement<SelectionButton>(LABEL_SBN_SORT);

        private const string LABEL_SBN_GODVIEW = "ButtonGodView";
        private SelectionButton m_ButtonGodView;
        public SelectionButton ButtonGodView =>
            m_ButtonGodView = m_ButtonGodView != null ? m_ButtonGodView
            : Manager.GetElement<SelectionButton>(LABEL_SBN_GODVIEW);

        private const string LABEL_IMG_HOVER = "ButtonHover";
        private Image m_ImageHover;
        public Image ImageHover =>
            m_ImageHover = m_ImageHover != null ? m_ImageHover
            : Manager.GetElement<Image>(LABEL_IMG_HOVER);

        private const string LABEL_IMG_OUT = "ButtonOut";
        private Image m_ImageOut;
        public Image ImageOut =>
            m_ImageOut = m_ImageOut != null ? m_ImageOut
            : Manager.GetElement<Image>(LABEL_IMG_OUT);

        #endregion

        public SuperScrollView superScrollView;
        private bool sortByName = true;
        private string Selected => superScrollView != null && superScrollView.selected >= 0 && superScrollView.selected < superScrollView.items.Count
            ? superScrollView.items[superScrollView.selected].args[1] : null;
        public void KF_Replay(string path, bool freeView = false)
        { if (!string.IsNullOrEmpty(path)) MDPro3.BattleSpirits.BattleIntegration.PlayReplay(path); }
        private void SelectZero()
        {
            if (superScrollView == null || superScrollView.items.Count == 0) return;
            var item = superScrollView.items[0].gameObject.GetComponent<SelectionToggle_Replay>();
            item.SetToggleOn(); Program.instance.replay.lastSelectedReplayItem = item;
        }
        public void Print()
        {
            superScrollView?.Clear();
            var paths = MDPro3.BattleSpirits.BattleIntegration.GetReplayFiles();
            if (sortByName) Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
            else Array.Sort(paths, (a,b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
            var tasks = new List<string[]>();
            for (int i = 0; i < paths.Length; i++) tasks.Add(new[] { i.ToString(), paths[i] });
            TextOverview.text = paths.Length == 0 ? "No Battle Spirits replays saved." : "";
            ButtonPlayer0.gameObject.SetActive(false); ButtonPlayer1.gameObject.SetActive(false);
            ButtonPlayer2.gameObject.SetActive(false); ButtonPlayer3.gameObject.SetActive(false);
            var handle = Addressables.LoadAssetAsync<GameObject>("UI/ItemReplay.prefab");
            handle.Completed += (result) =>
            {
                var itemHeight = PropertyOverrider.NeedMobileLayout() ? 180f : 150f;
                float topPadding = PropertyOverrider.NeedMobileLayout() ? 148f : 134f;
                float space = itemHeight - (PropertyOverrider.NeedMobileLayout() ? 152f : 122f);
                float bottomPadding = (PropertyOverrider.NeedMobileLayout() ? 64f : 54f) - space;
                superScrollView = new SuperScrollView(
                        1,
                        700,
                        itemHeight,
                        topPadding,
                        bottomPadding,
                        result.Result,
                        ItemOnListRefresh,
                        Manager.GetElement<ScrollRect>("ScrollRect"));
                superScrollView.Print(tasks);
                if (tasks.Count > 0)
                    SelectZero();
            };
        }

        private void ItemOnListRefresh(string[] task, GameObject item)
        {
            var handler = item.GetComponent<SelectionToggle_Replay>();
            handler.index = int.Parse(task[0]);
            handler.replayName = task[1];
            handler.Refresh();
        }

        public void OnRename()
        {
            if (Selected == null) return;
            UIManager.ShowPopupInput(new List<string> { "Replay name", Path.GetFileNameWithoutExtension(Selected) },
                ReplayRename, null, TmpInputValidation.ValidationType.Path);
        }
        private void ReplayRename(string name)
        {
            string selected = Selected;
            if (selected == null || string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name) return;
            if (name.EndsWith(".bsreplay.json", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - ".bsreplay.json".Length);
            else if (name.EndsWith(".bsreplay", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - ".bsreplay".Length);
            if (string.IsNullOrWhiteSpace(name)) return;
            string next = Path.Combine(Path.GetDirectoryName(selected), name + ".bsreplay.json");
            if (!File.Exists(next)) File.Move(selected, next);
            Print();
        }
        public void OnPlay() => KF_Replay(Selected);
        public void OnGodView() => KF_Replay(Selected);
        public void OnDelete() { if (Selected != null && File.Exists(Selected)) { File.Delete(Selected); Print(); } }
        public void OnSort() { sortByName = !sortByName; Print(); }
        public void OnDeck(int player) { }
        public void SelectLastReplayItem() { UserInput.NextSelectionIsAxis = true; Program.instance.replay.Select(); }
    }
}
