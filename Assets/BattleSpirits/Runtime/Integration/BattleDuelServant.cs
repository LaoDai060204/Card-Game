using System;
using MDPro3.BattleSpirits.Core;
using MDPro3.UI.ServantUI;
using UnityEngine;

namespace MDPro3.BattleSpirits
{
    /// <summary>Uses a private instance of the original duel prefab, with a BS controller.</summary>
    public sealed class BattleDuelServant : MDPro3.Servant.Servant
    {
        protected override string Label_UI => "ServantUI/OcgCoreUI.prefab";
        public override int Depth => -1;
        protected override bool NeedExitButton => false;
        private BattleGameSession session;
        private string pendingPreview;

        public void Bind(BattleGameSession value)
        {
            session = value;
            if (servantUI is BattleDuelUI ui) ui.Bind(value);
        }

        protected override void FirstLoadEvent()
        {
            var instance = servantUI.gameObject;
            // Do not edit the shared prefab or invoke OcgCoreUI.Initialize/ShowEvent.
            BattleDuelUI.DisableLegacyInstance(instance);
            servantUI = instance.AddComponent<BattleDuelUI>();
            servantUI.Initialize(this);
            ((BattleDuelUI)servantUI).Bind(session);
            if (!string.IsNullOrEmpty(pendingPreview))
            {
                ((BattleDuelUI)servantUI).PreviewBoss(pendingPreview);
                pendingPreview = null;
            }
        }

        public void PreviewBoss(string cardId)
        {
            if (servantUI is BattleDuelUI ui) ui.PreviewBoss(cardId);
            else pendingPreview = cardId;
        }

        public override void OnReturn()
        {
            if (servantUI is BattleDuelUI ui) ui.ReturnOrClose();
            else LeaveDuel();
        }

        public override bool NeedResponseInput() => base.NeedResponseInput()
            && Program.instance.currentServant == this && Program.instance.currentSubServant == null;

        public void LeaveDuel()
        {
            if (session != null) session.PresentationBusy = false;
            BattleIntegration.Shutdown();
            Program.instance.ShiftToServant(Program.instance.menu);
        }

        protected override void ApplyShowArrangement(int preDepth)
        {
            // The native duel has no menu cover/push animation. Keep its existing roots.
            inTransition = false;
            servantUI.gameObject.SetActive(true);
            servantUI.ResetUI();
            servantUI.ShowEvent();
            UIManager.HideExitButton(0);
            UIManager.HideLine(0);
            UIManager.HideWallpaper(TransitionTime);
            UIManager.ShowBlackBack(0, TransitionTime);
            AfterShowingEvent();
        }

        protected override void ApplyHideArrangement(int nextDepth)
        {
            if (servantUI is BattleDuelUI ui) ui.Suspend();
            servantUI.ShutDown();
            inTransition = false;
        }

        protected override void OnMouseCursorHide()
        {
            if (NeedResponseInput()) Select();
        }
    }
}
