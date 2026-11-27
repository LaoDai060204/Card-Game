using MDPro3.UI.PropertyOverride;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using static MDPro3.Servant.PuzzleSelector;
using static MDPro3.UI.ServantUI.PuzzleSelectorUI;

namespace MDPro3.UI.ServantUI
{
    public class PuzzleSelectorUI : ServantUI
    {
        #region Elements

        private const string LABEL_SBN_PLAY = "ButtonPlay";
        private SelectionButton m_ButtonPlay;
        public SelectionButton ButtonPlay =>
            m_ButtonPlay = m_ButtonPlay != null ? m_ButtonPlay
            : Manager.GetElement<SelectionButton>(LABEL_SBN_PLAY);

        private const string LABEL_SR = "ScrollRect";
        private ScrollRect m_ScrollRect;
        private ScrollRect ScrollRect =>
            m_ScrollRect = m_ScrollRect != null ? m_ScrollRect
            : Manager.GetElement<ScrollRect>(LABEL_SR);

        private const string LABEL_TXT_OVERVIEW = "TextOverview";
        private TextMeshProUGUI m_TextOverview;
        private TextMeshProUGUI TextOverview =>
            m_TextOverview = m_TextOverview != null ? m_TextOverview
            : Manager.GetElement<TextMeshProUGUI>(LABEL_TXT_OVERVIEW);

        private const string LABEL_MONO_ART = "ArtImage";
        private ArtRawImageHandler m_Art;
        public ArtRawImageHandler Art =>
            m_Art = m_Art != null ? m_Art
            : Manager.GetElement<ArtRawImageHandler>(LABEL_MONO_ART);

        private const string LABEL_IMG_HOVER = "ButtonHover";
        private Image m_ImageHover;
        public Image ImageHover =>
            m_ImageHover = m_ImageHover != null ? m_ImageHover
            : Manager.GetElement<Image>(LABEL_IMG_HOVER);

        private const string LABEL_IMG_OUT = "ButtonOut";
        private Image m_ImageOut;
        public Image ImageOut =>
            m_ImageOut = m_ImageOut != null ? m_ImageOut
            : Manager.GetElement<Image>(LABEL_IMG_OUT);

        #endregion

        public const int DEFAULT_CARD = 0;
        public struct Puzzle
        {
            public string id, name, firstCard, description, solution, pathPrefix, namePattern;
            public readonly string GetPath() => id;
            public readonly string GetEnterConfigKey() => "BattleSpirits/Puzzles/" + id + "_Enter";
            public readonly string GetClearConfigKey() => "BattleSpirits/Puzzles/" + id + "_Clear";
        }
        private static readonly List<Puzzle> puzzles = new();
        public SuperScrollView superScrollView;
        public override void ShowEvent()
        {
            base.ShowEvent();
            GetPuzzles(Program.instance.puzzle.puzzleGroup); Print();
            Title.text = PuzzlePortalUI.GetPuzzleGroupName(Program.instance.puzzle.puzzleGroup);
        }
        private static Puzzle Convert(MDPro3.BattleSpirits.Puzzles.PuzzleDefinition p) => new Puzzle
        {
            id = p.id, name = p.title, firstCard = MDPro3.BattleSpirits.BattleCatalog.GetNumericId(p.previewCardId).ToString(),
            description = p.description, solution = "Hint: " + p.hint, pathPrefix = p.category + "/", namePattern = ""
        };
        private static void GetPuzzles(PuzzleGroup group)
        {
            puzzles.Clear();
            foreach (var p in MDPro3.BattleSpirits.Puzzles.BattlePuzzleCatalog.All)
                if (string.IsNullOrEmpty(group.pathPrefix) || p.category + "/" == group.pathPrefix) puzzles.Add(Convert(p));
        }
        public void Print()
        {
            superScrollView?.Clear();
            var tasks = new List<string[]>();
            for (int i = 0; i < puzzles.Count; i++)
            {
                var p = puzzles[i];
                tasks.Add(new[] { i.ToString(), p.name, p.firstCard, p.description, p.solution, p.pathPrefix, p.namePattern, p.id });
            }
            var handle = Addressables.LoadAssetAsync<GameObject>("UI/ItemPuzzle.prefab");
            handle.Completed += result =>
            {
                float itemHeight = PropertyOverrider.NeedMobileLayout() ? 180f : 150f;
                float topPadding = PropertyOverrider.NeedMobileLayout() ? 148f : 134f;
                float space = itemHeight - (PropertyOverrider.NeedMobileLayout() ? 152f : 122f);
                float bottomPadding = (PropertyOverrider.NeedMobileLayout() ? 64f : 54f) - space;
                superScrollView = new SuperScrollView(1, 700, itemHeight, topPadding, bottomPadding,
                    result.Result, ItemOnListRefresh, ScrollRect);
                superScrollView.Print(tasks);
                if (superScrollView.items.Count > 0) superScrollView.items[0].gameObject.GetComponent<SelectionToggle_Puzzle>().SetToggleOn();
                else { SetOverview(""); Program.instance.puzzle.currentPuzzle = ""; }
            };
        }
        private void ItemOnListRefresh(string[] task, GameObject item)
        {
            var handler = item.GetComponent<SelectionToggle_Puzzle>();
            handler.puzzle = new Puzzle { id = task[7], name = task[1], firstCard = task[2], description = task[3],
                solution = task[4], pathPrefix = task[5], namePattern = task[6] };
            handler.Refresh();
        }
        public void SetOverview(string overview)
        { TextOverview.text = overview; TextOverview.GetComponent<RectTransform>().anchoredPosition = Vector2.zero; }
        public void OnPlayPuzzle() => Program.instance.puzzle.StartCurrentPuzzle();
        public void SelectLastPuzzleItem()
        { UserInput.NextSelectionIsAxis = true; Program.instance.puzzle.Select(); }
        public static Puzzle GetFirstPuzzle(PuzzleGroup group)
        { GetPuzzles(group); return puzzles.Count > 0 ? puzzles[0] : default; }
        public static List<Puzzle> GetRecommendPuzzles(int count) => MDPro3.BattleSpirits.Puzzles.BattlePuzzleCatalog.All
            .Select(Convert).OrderBy(p => Config.GetBool(p.GetClearConfigKey(), false)).Take(Math.Max(0, count)).ToList();
        public static Puzzle? GetLastPlayPuzzle()
        {
            return MDPro3.BattleSpirits.Puzzles.BattlePuzzleCatalog.TryGet(Config.Get("LastPlayPuzzle", ""), out var p)
                ? Convert(p) : (Puzzle?)null;
        }
        public static bool GroupHasNew(PuzzleGroup group)
        { GetPuzzles(group); return puzzles.Any(p => !Config.GetBool(p.GetEnterConfigKey(), false)); }
        public static bool GroupAllClear(PuzzleGroup group)
        { GetPuzzles(group); return puzzles.Count > 0 && puzzles.All(p => Config.GetBool(p.GetClearConfigKey(), false)); }
    }
}
