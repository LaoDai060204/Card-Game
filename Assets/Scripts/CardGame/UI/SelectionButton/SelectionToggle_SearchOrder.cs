using CardGame.UI.ServantUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static CardGame.UI.CardCollectionView;

namespace CardGame.UI
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
                SortOrder.ByLevelUp => InterString.Get("Level/Rank/Link"),
                SortOrder.ByLevelDown => InterString.Get("Level/Rank/Link"),
                SortOrder.ByAttackUp => InterString.Get("ATK"),
                SortOrder.ByAttackDown => InterString.Get("ATK"),
                SortOrder.ByDefenceUp => InterString.Get("Defense"),
                SortOrder.ByDefenceDown => InterString.Get("Defense"),
                SortOrder.ByRarityUp => InterString.Get("Rarity"),
                SortOrder.ByRarityDown => InterString.Get("Rarity"),
                SortOrder.ByGPUp => InterString.Get("Genesys Points"),
                SortOrder.ByGPDown => InterString.Get("Genesys Points"),
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
