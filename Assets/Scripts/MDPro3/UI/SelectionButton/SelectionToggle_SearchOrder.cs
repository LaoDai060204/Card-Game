using MDPro3.BattleSpirits;
using MDPro3.UI.ServantUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static MDPro3.UI.CardCollectionView;

namespace MDPro3.UI
{
    public class SelectionToggle_SearchOrder : SelectionToggle
    {
        [Header("SelectionToggle SearchOrder")]
        [SerializeField] private SortOrder sortOrder = SortOrder.ByType;

        protected override void Awake()
        {
            base.Awake();

            exclusiveToggle = true;
            canToggleOffSelf = false;
        }

        private void Start()
        {
            SetButtonText(GetSortText());
            if (sortOrder == _SortOrder)
            {
                SetToggleOn();
                EventSystem.current.SetSelectedGameObject(gameObject);
            }
        }

        protected override void CallHoverOnEvent()
        {
            base.CallHoverOnEvent();
            ((Popup.PopupSearchOrder)Program.instance.ui_.currentPopupB).lastSelectedToggle = this;
        }

        protected override void CallSubmitEvent()
        {
            _SortOrder = sortOrder;
            var view = Program.instance.deckEditor.GetUI<DeckEditorUI>().CardCollectionView;
            view.SetSortIcon(GetIconSprite());
            view.SetSortText(GetSortText());
            if(view.area == Area.Collection)
                view.PrintSearchCards();
            else
                view.PrintBookmarkCards();
            Program.instance.ui_.currentPopupB.Hide();
        }

        protected string GetSortText()
        {
            return sortOrder switch
            {
                SortOrder.ByType => InterString.Get("Type"),
                SortOrder.ByTypeReverse => InterString.Get("Type"),
                SortOrder.ByLevelUp => "Cost",
                SortOrder.ByLevelDown => "Cost",
                SortOrder.ByAttackUp => "BP",
                SortOrder.ByAttackDown => "BP",
                SortOrder.ByDefenceUp => "Symbols",
                SortOrder.ByDefenceDown => "Symbols",
                SortOrder.ByRarityUp => InterString.Get("Rarity"),
                SortOrder.ByRarityDown => InterString.Get("Rarity"),
                SortOrder.ByGPUp => "Color",
                SortOrder.ByGPDown => "Color",
                _ => string.Empty
            };
        }
        protected override void OnClick()
        {
            AudioManager.PlaySE(SoundLabelClick);
            SetToggleOn();
            CallSubmitEvent();
        }

        protected override void OnSubmit()
        {
            base.OnSubmit();
            OnClick();
        }
    }
}
