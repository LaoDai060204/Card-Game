using Cysharp.Threading.Tasks;
using MDPro3.BattleSpirits;
using MDPro3.Duel.YGOSharp;
using MDPro3.UI;
using MDPro3.UI.ServantUI;
using System.Collections.Generic;
using UnityEngine;
namespace MDPro3.Servant
{
    public class CutinViewer : Servant
    {
        public const float CUTIN_PLAY_TIME = 1.6f;
        public static int controller;
        public static readonly List<Card> cards = new();
        public static readonly List<int> codes = new();
        public static readonly List<int> codes2 = new();
        [HideInInspector] public SelectionToggle_Cutin lastSelectedCutinItem;
        public override int Depth => 1;
        protected override bool ShowLine => false;
        public override void Initialize() { returnServant = Program.instance.menu; base.Initialize(); LoadCutins(); }
        protected override void ApplyShowArrangement(int previous) { base.ApplyShowArrangement(previous); LoadCutins(); }
        public override void Select(bool forced = false)
        {
            if (!forced && !UserInput.NeedDefaultSelect()) return;
            if (lastSelectedCutinItem != null) lastSelectedCutinItem.GetSelectable().Select();
            else servantUI?.SelectDefaultSelectable();
        }
        public void LoadCutins()
        {
            cards.Clear(); codes.Clear(); codes2.Clear();
            foreach (var definition in BattleCatalog.Cards)
                if (BattleCatalog.IsBoss(definition.id))
                { var card = CardsManager.Get(definition.numericId); if (card != null) { cards.Add(card); codes.Add(card.Id); } }
            if (servantUI != null) GetUI<CutinViewerUI>().Print();
        }
        public override void PerFrameFunction()
        {
            if (!NeedResponseInput()) return;
            if (UserInput.MouseRightDown || UserInput.WasCancelPressed) OnReturn();
            if (UserInput.WasGamepadButtonWestPressed) GetUI<CutinViewerUI>().FocusOnInputField();
            if (UserInput.WasGamepadButtonNorthPressed) AutoPlay();
        }
        public void SelectLastCutinItem() { UserInput.NextSelectionIsAxis = true; Select(); }
        public static bool CutinExist(int code) => BattleCatalog.IsBoss(BattleCatalog.GetCardId(code));
        public static UniTask Play(int code, int owner)
        { if (CutinExist(code)) BattleIntegration.PreviewCutscene(BattleCatalog.GetCardId(code)); return UniTask.CompletedTask; }
        public static UniTask CacheCutin(int code) => UniTask.CompletedTask;
        public void AutoPlay() { BattleIntegration.PreviewCutscene(); }
    }
}
