using MDPro3.Duel.YGOSharp;
using MDPro3.Servant;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using MDPro3.UI.PropertyOverride;

namespace MDPro3.UI.ServantUI
{
    public class MateViewerUI : ServantUI
    {

        #region Elements

        private const string LABEL_SR = "ScrollRect";
        private ScrollRect m_ScrollRect;
        private ScrollRect ScrollRect =>
            m_ScrollRect = m_ScrollRect != null ? m_ScrollRect
            : Manager.GetElement<ScrollRect>(LABEL_SR);

        private const string LABEL_SBN_INTERACTION = "ButtonInteraction";
        private SelectionButton m_ButtonInteraction;
        private SelectionButton ButtonInteraction =>
            m_ButtonInteraction = m_ButtonInteraction != null ? m_ButtonInteraction
            : Manager.GetElement<SelectionButton>(LABEL_SBN_INTERACTION);

        private const string LABEL_IPT = "InputField";
        private TMP_InputField m_Input;
        private TMP_InputField Input =>
            m_Input = m_Input != null ? m_Input
            : Manager.GetElement<TMP_InputField>(LABEL_IPT);

        #endregion

        private static readonly List<int> crossDuelMates = new();
        private static readonly List<Card> cards = new();
        public SuperScrollView superScrollView;

        private void Awake() { Load(); }
        public void Load() { cards.Clear(); crossDuelMates.Clear(); Print(); }

        public void Print(string search = "")
        {
            superScrollView?.Clear();
            var tasks = new List<string[]>();
            foreach (var card in cards)
            {
                if (card.Name.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string[] task = new string[] { card.Id.ToString(), card.Name };
                    tasks.Add(task);
                }
            }
            var handle = Addressables.LoadAssetAsync<GameObject>("UI/ItemMate.prefab");
            handle.Completed += (result) =>
            {
                var itemWidth = PropertyOverrider.NeedMobileLayout() ? 460f : 360f;
                var itemHeight = PropertyOverrider.NeedMobileLayout() ? 80f : 40f;

                superScrollView = new SuperScrollView(
                    1,
                    itemWidth,
                    itemHeight,
                    0,
                    0,
                    result.Result,
                    ItemOnListRefresh,
                    ScrollRect, 4);
                superScrollView.Print(tasks);
                if (superScrollView.items.Count > 0)
                    Program.instance.mate.lastSelectedMateItem 
                    = superScrollView.items[0].gameObject.GetComponent<SelectionToggle_Mate>();
            };
        }

        private void ItemOnListRefresh(string[] task, GameObject item)
        {
            var handler = item.GetComponent<SelectionToggle_Mate>();
            handler.code = int.Parse(task[0]);
            handler.mateName = task[1];
            handler.Refresh();
        }

        public static string GetRushDuelMateName(int code)
        {
            return code switch
            {
                120105001 => InterString.Get("Sevens Road Magician"),
                120105010 => InterString.Get("Straynge Cat"),
                120110001 => InterString.Get("Multistrike Dragon Dragias"),
                120110006 => InterString.Get("Twin Edge Dragon"),
                120110010 => InterString.Get("Draco the Tiny"),
                120115001 => InterString.Get("Sevens Road Witch"),
                120120018 => InterString.Get("Whispering Fairy"),
                120120025 => InterString.Get("Sportsdragon Slugger"),
                120120024 => InterString.Get("Sportsdragon Pitcher"),
                120120029 => InterString.Get("Yamiruler the Dark Delayer"),
                120120003 => InterString.Get("Ancient Turtle Protector"),
                120130016 => InterString.Get("Sevens Road Mage"),
                120130026 => InterString.Get("Nandes the Fire Awakener"),
                120140023 => InterString.Get("Royal Rebel's Fanatic"),
                120145014 => InterString.Get("Sparkhearts Girl"),
                120150002 => InterString.Get("Supreme Machine Magnum Overlord"),
                120155019 => InterString.Get("Modorina the Light Retriever"),
                _ => string.Empty
            };
        }

        public void OnMateTap()
        {
            if (Program.instance.mate.mate == null)
                return;
            Program.instance.mate.mate.Play(Mate.MateAction.Tap);
        }

        public void FocusOnInputField()
        {
            Input.ActivateInputField();
        }

        public void SelectButtonInteract()
        {
            ButtonInteraction.GetSelectable().Select();
        }

        public void SelectLastMateItem()
        {
            if (Program.instance.mate.lastSelectedMateItem != null)
                Program.instance.mate.lastSelectedMateItem.GetSelectable().Select();
        }
    }
}
