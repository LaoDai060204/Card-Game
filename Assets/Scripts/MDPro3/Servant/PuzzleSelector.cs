using MDPro3.UI;
using MDPro3.UI.ServantUI;
using UnityEngine;

namespace MDPro3.Servant
{
    public class PuzzleSelector : Servant
    {

        [HideInInspector] public string currentPuzzle;
        [HideInInspector] public SelectionToggle_Puzzle lastPuzzleItem;


        #region Servant

        public override int Depth => 2;
        protected override bool ShowLine => false;

        public override void Initialize()
        {
            returnServant = Program.instance.puzzlePortal;
            base.Initialize();
        }

        protected override void FirstLoadEvent()
        {
            base.FirstLoadEvent();
            _ = Program.instance.texture_.SetCommonShopButtonMaterial(GetUI<PuzzleSelectorUI>().ImageOut, false);
            _ = Program.instance.texture_.SetCommonShopButtonMaterial(GetUI<PuzzleSelectorUI>().ImageHover, true);
        }

        public override void Select(bool forced = false)
        {
            if (!forced && !UserInput.NeedDefaultSelect())
                return;

            if (lastPuzzleItem != null) lastPuzzleItem.GetSelectable().Select();
            else servantUI?.SelectDefaultSelectable();
        }

        public override void OnExit()
        {
            if(Program.exitOnReturn)
                Program.GameQuit();
            else
                Program.instance.ShiftToServant(returnServant);
        }

        #endregion

        public struct PuzzleGroup
        {
            public string pathPrefix;
            public string namePattern;
        }

        public PuzzleGroup puzzleGroup = new()
        {
            pathPrefix = string.Empty,
            namePattern = string.Empty
        };

        public void StartCurrentPuzzle(bool check = true)
        {
            //Prevent repeated clicks.
            if (check && Program.instance.currentServant != Program.instance.puzzle)
                return;

            StartPuzzle(currentPuzzle);
        }

        public void StartPuzzle(string puzzle)
        {
            Config.Set("LastPlayPuzzle", puzzle);
            Config.Save();

            if (MDPro3.BattleSpirits.BattleIntegration.StartPuzzle(puzzle))
            {
                Config.Set("BattleSpirits/Puzzles/" + puzzle + "_Enter", Config.STRING_YES);
                Config.Save();
            }
        }

        public void SelectLastPuzzleItem()
        {
            UserInput.NextSelectionIsAxis = true;
            Select();
        }
    }
}
