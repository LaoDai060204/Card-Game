using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MDPro3.Servant;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using MDPro3.UI.PropertyOverride;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MDPro3.UI.ServantUI
{
    public class SoloSelectorUI : ServantUI
    {

        #region Elements

        private const string LABEL_SR = "ScrollRect";
        private ScrollRect m_ScrollRect;
        public ScrollRect ScrollRect =>
            m_ScrollRect = m_ScrollRect != null ? m_ScrollRect
            : Manager.GetElement<ScrollRect>(LABEL_SR);

        private const string LABEL_TXT_OVERVIEW = "TextOverview";
        private TextMeshProUGUI m_TextOverview;
        protected TextMeshProUGUI TextOverview =>
            m_TextOverview = m_TextOverview != null ? m_TextOverview
            : Manager.GetElement<TextMeshProUGUI>(LABEL_TXT_OVERVIEW);

        private const string LABEL_SBN_DECK = "ButtonDeck";
        private SelectionButton m_ButtonDeck;
        protected SelectionButton ButtonDeck =>
            m_ButtonDeck = m_ButtonDeck != null ? m_ButtonDeck
            : Manager.GetElement<SelectionButton>(LABEL_SBN_DECK);

        private const string LABEL_STG_LOCKHAND = "Settings/ToggleLockHand";
        private SelectionToggle m_ToggleLockHand;
        public SelectionToggle ToggleLockHand =>
            m_ToggleLockHand = m_ToggleLockHand != null ? m_ToggleLockHand
            : Manager.GetNestedElement<SelectionToggle>(LABEL_STG_LOCKHAND);

        private const string LABEL_STG_NOCHECK = "Settings/ToggleNoCheck";
        private SelectionToggle m_ToggleNoCheck;
        protected SelectionToggle ToggleNoCheck =>
            m_ToggleNoCheck = m_ToggleNoCheck != null ? m_ToggleNoCheck
            : Manager.GetNestedElement<SelectionToggle>(LABEL_STG_NOCHECK);

        private const string LABEL_STG_NOSHUFFLE = "Settings/ToggleNoShuffle";
        private SelectionToggle m_ToggleNoShuffle;
        protected SelectionToggle ToggleNoShuffle =>
            m_ToggleNoShuffle = m_ToggleNoShuffle != null ? m_ToggleNoShuffle
            : Manager.GetNestedElement<SelectionToggle>(LABEL_STG_NOSHUFFLE);

        private const string LABEL_IPT_PORT = "Settings/InputFieldPort";
        private TMP_InputField m_InputPort;
        protected TMP_InputField InputPort =>
            m_InputPort = m_InputPort != null ? m_InputPort
            : Manager.GetNestedElement<TMP_InputField>(LABEL_IPT_PORT);

        private const string LABEL_IPT_LP = "Settings/InputFieldLP";
        private TMP_InputField m_InputLP;
        protected TMP_InputField InputLP =>
            m_InputLP = m_InputLP != null ? m_InputLP
            : Manager.GetNestedElement<TMP_InputField>(LABEL_IPT_LP);

        private const string LABEL_IPT_HAND = "Settings/InputFieldHand";
        private TMP_InputField m_InputHand;
        protected TMP_InputField InputHand =>
            m_InputHand = m_InputHand != null ? m_InputHand
            : Manager.GetNestedElement<TMP_InputField>(LABEL_IPT_HAND);

        private const string LABEL_IPT_DRAW = "Settings/InputFieldDraw";
        private TMP_InputField m_InputDraw;
        protected TMP_InputField InputDraw =>
            m_InputDraw = m_InputDraw != null ? m_InputDraw
            : Manager.GetNestedElement<TMP_InputField>(LABEL_IPT_DRAW);

        #endregion

        private const int DEFAULT_PORT = 7911;
        private const int DEFAULT_LP = 5;
        private const int DEFAULT_HAND = 4;
        private const int DEFAULT_DRAW = 1;

        public SuperScrollView superScrollView;
        private GameObject itemPrefab;
        private AsyncOperationHandle<GameObject> itemHandle;
        private bool loadingItems;
        private bool disposed;
        private List<string[]> pendingTasks = new();

        public override void Initialize(Servant.Servant servant)
        {            
            base.Initialize(servant);
            ButtonDeck.gameObject.SetActive(false);
        }

        public override void ShowEvent()
        {
            base.ShowEvent();

            // Standard Battle Spirits fixes life, opening hand, draw and shuffle rules.
            // Preserve the settings panel and mark its old custom-rule inputs unavailable.
            ToggleLockHand.GetSelectable().interactable = false;
            ToggleNoCheck.GetSelectable().interactable = false;
            ToggleNoShuffle.GetSelectable().interactable = false;
            InputPort.interactable = false;
            InputLP.interactable = false; InputLP.text = "5";
            InputHand.interactable = false; InputHand.text = "4";
            InputDraw.interactable = false; InputDraw.text = "1";
            ButtonDeck.SetButtonText(Config.GetConfigDeckName());
        }

        public void Print()
        {
            pendingTasks = new List<string[]>();
            for (int i = 0; i < SoloSelector.bots.Count; i++)
                pendingTasks.Add(new[] { i.ToString() });
            if (itemPrefab != null) { PrintReadyItems(); return; }
            if (loadingItems) return;
            loadingItems = true;
            itemHandle = Addressables.LoadAssetAsync<GameObject>("UI/ItemSolo.prefab");
            itemHandle.Completed += result =>
            {
                if (disposed || this == null) return;
                loadingItems = false;
                if (result.Status != AsyncOperationStatus.Succeeded || result.Result == null)
                {
                    TextOverview.text = "The Solo opponent list could not load. Return and try again.";
                    Debug.LogError("Battle Spirits Solo: UI/ItemSolo.prefab could not load.");
                    if (itemHandle.IsValid()) Addressables.Release(itemHandle);
                    return;
                }
                itemPrefab = result.Result;
                PrintReadyItems();
            };
        }

        private void PrintReadyItems()
        {
            // One list owns the content and scrollbar. Two asynchronous Print
            // calls must not leave overlapping rows or a second unselected list.
            if (superScrollView == null)
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
                    itemPrefab,
                    ItemOnListRefresh,
                    ScrollRect);
            }
            foreach (var row in superScrollView.gameObjects)
                if (row != null) row.SetActive(false);
            int selected = Program.instance.solo.lastSoloItem != null ? Program.instance.solo.lastSoloItem.index : 0;
            superScrollView.selected = pendingTasks.Count == 0 ? -1 : Mathf.Clamp(selected, 0, pendingTasks.Count - 1);
            superScrollView.Print(pendingTasks);
            if (superScrollView.selected >= 0)
            {
                var item = superScrollView.GetItemByIndex(superScrollView.selected) as SelectionToggle_Solo;
                Program.instance.solo.lastSoloItem = item;
                SetOverview(SoloSelector.bots[superScrollView.selected].desc, false);
                if (item != null)
                {
                    item.SetToggleOn();
                    if (Cursor.lockState == CursorLockMode.Locked) item.GetSelectable().Select();
                }
            }
            else
            {
                Program.instance.solo.lastSoloItem = null;
                SetOverview("No Battle Spirits opponents are installed.", false);
            }
        }

        private void ItemOnListRefresh(string[] task, GameObject item)
        {
            var handler = item.GetComponent<SelectionToggle_Solo>();
            if (handler == null || !int.TryParse(task[0], out int index)
                || index < 0 || index >= SoloSelector.bots.Count) return;
            handler.index = index;
            handler.botInfo = SoloSelector.bots[handler.index];
            handler.Refresh();
        }

        public bool IsLockHand()
        {
            return ToggleLockHand.isOn;
        }

        public bool IsNoCheck()
        {
            return ToggleNoCheck.isOn;
        }

        public bool IsNoShuffle()
        {
            return ToggleNoShuffle.isOn;
        }

        public int GetPort()
        {
            if (int.TryParse(InputPort.text, out var result) 
                && result > 0 
                && result <= 65535)
            { return result; }
            InputPort.text = DEFAULT_PORT.ToString();
            return DEFAULT_PORT;
        }

        public int GetLP()
        {
            if(int.TryParse(InputLP.text, out var result)
                && result > 0)
                { return result; }
            InputLP.text = DEFAULT_LP.ToString();
            return DEFAULT_LP;
        }

        public int GetHand()
        {
            if(int.TryParse(InputHand.text, out var result)
                && result > 0)
            { return result; }
            InputHand.text = DEFAULT_HAND.ToString();
            return DEFAULT_HAND;
        }

        public int GetDraw()
        {
            if(int.TryParse(InputDraw.text, out var result)
                && result >= 0)
            { return result; }
            InputDraw.text = DEFAULT_DRAW.ToString();
            return DEFAULT_DRAW;
        }

        public void SetOverview(string text, bool activateDeckButton)
        {
            TextOverview.text = text;
            ButtonDeck.gameObject.SetActive(activateDeckButton);
        }

        public string GetAIDeck()
        {
            return ButtonDeck.GetButtonText();
        }

        public void OnPlay()
        {
            if (superScrollView == null || superScrollView.items.Count == 0
                || superScrollView.selected < 0 || superScrollView.selected >= superScrollView.items.Count)
                return;

            if (SoloSelector.condition == SoloSelector.Condition.ForSolo)
                Program.instance.solo.ShowSoloPreview(superScrollView.selected, ButtonDeck.gameObject.activeSelf);
            else
                Program.instance.solo.StartAIForRoom(superScrollView.selected, ButtonDeck.gameObject.activeSelf);
        }

        public void SelectLastSoloItem()
        {
            UserInput.NextSelectionIsAxis = true;
            Program.instance.solo.Select();
        }

        public void SelectOnRight()
        {
            // ButtonPlay is a named object in the preserved prefab, not an
            // ElementObjectManager label. Resolve the existing button directly.
            foreach (var button in GetComponentsInChildren<SelectionButton>(true))
                if (button.gameObject.name == "ButtonPlay")
                {
                    button.GetSelectable().Select();
                    return;
                }
        }

        public void OnSelectAIDeck()
        {
            Program.instance.deckSelector.SwitchCondition(DeckSelector.Condition.ForSolo);
            Program.instance.ShiftToServant(Program.instance.deckSelector);
        }

        protected override void OnDestroy()
        {
            disposed = true;
            if (superScrollView?.scrollRect != null && superScrollView.scrollRect.verticalScrollbar != null)
                superScrollView.scrollRect.verticalScrollbar.onValueChanged.RemoveListener(superScrollView.OnScrollBarChange);
            if (itemHandle.IsValid()) Addressables.Release(itemHandle);
            base.OnDestroy();
        }
    }
}
