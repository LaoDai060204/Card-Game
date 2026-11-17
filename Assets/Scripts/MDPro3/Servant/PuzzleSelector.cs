using MDPro3.UI;
using MDPro3.UI.ServantUI;
using MDPro3.Duel.YGOSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MDPro3.Servant
{
    public class PuzzleSelector : Servant
    {

        [HideInInspector] public string currentPuzzle;
        [HideInInspector] public SelectionToggle_Puzzle lastPuzzleItem;
        private PercyOCG percy;

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

            if (lastPuzzleItem == null)
                return;

            lastPuzzleItem.GetSelectable().Select();
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

            if (string.IsNullOrEmpty(currentPuzzle) || !File.Exists(currentPuzzle + Program.EXPANSION_LUA))
                return;

            StartPuzzle(currentPuzzle);
        }

        public void StartPuzzle(string puzzle)
        {
            if (string.IsNullOrEmpty(puzzle))
                return;

            var path = puzzle + Program.EXPANSION_LUA;
            try
            {
                var missing = FindMissingPuzzleCardIds(File.ReadAllText(path),
                    id => CardsManager.GetCardRaw(id) != null);
                if (missing.Count > 0)
                {
                    MessageManager.Cast("This puzzle requires missing card data: " + string.Join(", ", missing));
                    Debug.LogWarning($"Puzzle {path} requires missing card data: {string.Join(", ", missing)}");
                    return;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                MessageManager.Cast(InterString.Get("Failed to start puzzle <#FF0000>[?]</color>.", Path.GetFileName(puzzle)));
                Debug.LogWarning($"Unable to read Puzzle {path}: {exception.Message}");
                return;
            }

            Config.Set("LastPlayPuzzle", puzzle);
            Config.Save();

            percy?.Dispose();
            percy = new PercyOCG();
            percy.StartPuzzle(puzzle + Program.EXPANSION_LUA);
        }

        internal static List<int> FindMissingPuzzleCardIds(string text, Predicate<int> hasCard)
        {
            if (hasCard == null)
                throw new ArgumentNullException(nameof(hasCard));
            var missing = new HashSet<int>();
            // Recognize literal setup calls without treating commented-out lines
            // or unrelated IDs in the description as required card data.
            const string pattern = @"(?m)^[\t ]*(?:(?:local[\t ]+)?[A-Za-z_]\w*[\t ]*=[\t ]*)?Debug\.AddCard\s*\(\s*(\d+)\s*,";
            foreach (Match match in Regex.Matches(text ?? string.Empty, pattern))
                if (int.TryParse(match.Groups[1].Value, out int id) && id > 0 && !hasCard(id))
                    missing.Add(id);
            var result = new List<int>(missing);
            result.Sort();
            return result;
        }

        public void SelectLastPuzzleItem()
        {
            UserInput.NextSelectionIsAxis = true;
            Select();
        }
    }
}
