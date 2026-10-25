using CardGame.UI.ServantUI;
using UnityEngine;

namespace CardGame.UI
{
    public class SelectionToggle_CardFilter : SelectionToggle
    {
        public static SelectionToggle_CardFilter Instance;

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
            SetClickEvent(() =>
            {
                Program.instance.deckEditor.GetUI<DeckEditorUI>().CardCollectionView.ShowFilters();
            });
        }

        protected override void OnClick()
        {
        }
    }
}
