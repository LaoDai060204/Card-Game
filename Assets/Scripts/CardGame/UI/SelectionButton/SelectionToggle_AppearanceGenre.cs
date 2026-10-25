using CardGame.UI;
using CardGame.UI.ServantUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardGame.UI
{
    public class SelectionToggle_AppearanceGenre : SelectionToggle
    {

        protected override void Awake()
        {
            base.Awake();

            exclusiveToggle = true;
            canToggleOffSelf = false;
            toggleWhenSelected = true;
        }

        public override void SetToggleOn(bool callEvent = true)
        {
            if (!isOn)
                Program.instance.appearance.GetUI<AppearanceUI>().ScrollRect.verticalScrollbar.value = 1f;
            base.SetToggleOn();
        }

        protected override void CallToggleOnEvent()
        {
            base.CallToggleOnEvent();
            Program.instance.appearance.GetUI<AppearanceUI>().ShowItems(name.Split(" ")[1]);
            Program.instance.appearance.lastSelectedToggle = this;
        }

        protected override void OnSubmit()
        {
            base.OnSubmit();

            UserInput.NextSelectionIsAxis = true;
            var target = Program.instance.appearance.GetUI<AppearanceUI>().GetCurrentContentItem();
            if (target == null)
                return;
            EventSystem.current.SetSelectedGameObject(target);
        }
    }
}
