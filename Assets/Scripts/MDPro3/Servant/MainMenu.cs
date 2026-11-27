using MDPro3.Net;
using MDPro3.UI;
using MDPro3.UI.ServantUI;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace MDPro3.Servant
{
    public class MainMenu : Servant
    {
        [HideInInspector] public SelectionButton_MainMenu lastSelectedButton;

        public override int Depth => 0;
        protected override bool ShowLine => false;
        protected override bool NeedExitButton => false;

        public override void Initialize()
        {
            base.Initialize();
            
            showing = true;
            UIManager.HideExitButton(0f);
            UIManager.HideLine(0f);
            Program.instance.currentServant = this;
            Program.instance.depth = 0;


            LoadUI();
        }

        public override void OnExit()
        {
            if (Program.exitOnReturn)
                return;

            List<string> selections = new()
            {
                InterString.Get("Confirm Exit"),
                InterString.Get("Are you sure you want to exit the game?"),
                InterString.Get("Confirm"),
                InterString.Get("Cancel"),
                Config.STRING_YES
            };
            UIManager.ShowPopupYesOrNo(selections, Program.GameQuit, null);
        }

        protected override void FirstLoadEvent()
        {
            base.FirstLoadEvent();
            servantUI.ResetUI();
            StartCoroutine(LoadBattleSpiritsNews());

            EnsureButtonsWidth();
            Program.instance.ReadParams();

        }

        protected override void ApplyShowArrangement(int preDepth)
        {
            base.ApplyShowArrangement(preDepth);

            UIManager.ShowWallpaper(TransitionTime);
        }

        protected override void ApplyHideArrangement(int preDepth)
        {
            base.ApplyHideArrangement(preDepth);

            UIManager.HideWallpaper(TransitionTime);
        }

        public override void PerFrameFunction()
        {
            if (NeedResponseInput())
            {
                if (UserInput.WasCancelPressed)
                    OnReturn();

                if (GetUI<MainMenuUI>().News.showing)
                {
                    if (UserInput.WasLeftPressed)
                        GetUI<MainMenuUI>().News.OnLeft();
                    else if (UserInput.WasRightPressed)
                        GetUI<MainMenuUI>().News.OnRight();

                    if (UserInput.WasGamepadButtonWestPressed)
                        GetUI<MainMenuUI>().News.OnNewsClick();
                    if (UserInput.WasGamepadButtonNorthPressed)
                        GetUI<MainMenuUI>().News.OnClose();
                }
            }
        }

        public override void Select(bool forced = false)
        {
            if (!forced && !UserInput.NeedDefaultSelect())
                return;

            if (lastSelectedButton != null)
                lastSelectedButton.GetSelectable().Select();
            else
                GetUI<MainMenuUI>().SelectDefaultSelectable();
        }

        private IEnumerator LoadBattleSpiritsNews()
        {
            yield return null;
            var entries = new[]
            {
                new News { title = "Battle Spirits — four Standard decks in Solo", url = "https://www.battlespirits.com/feature/bsleague/2026/season3/" },
                new News { title = "Ten Battle Spirits puzzles — cores, battle and card effects", url = "https://www.battlespirits.com/rule/" },
                new News { title = "Ritenia — summon cutscene preview", url = "https://www.battlespirits.com/" }
            };
            var pictures = new List<Texture2D>();
            foreach (var id in new[] { "26RBS03-X01", "26RBS03-004", "26RSD03-X02" })
            {
                string path = MDPro3.BattleSpirits.BattleCatalog.CardImagePath(id);
                Texture2D texture = null;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!texture.LoadImage(File.ReadAllBytes(path))) { Destroy(texture); texture = null; }
                }
                pictures.Add(texture != null ? texture : Texture2D.blackTexture);
            }
            GetUI<MainMenuUI>().News.LoadLocalNews(entries, pictures);
        }

        private SelectionButton_MainMenu[] buttons;
        private SelectionButton_MainMenu[] Buttons
            => buttons ??= GetButtons();

        private SelectionButton_MainMenu[] GetButtons()
        {
            if (servantUI == null)
                return null;
            return transform.GetComponentsInChildren<SelectionButton_MainMenu>(true);
        }

        public void EnsureButtonsWidth()
        {
            if (Buttons == null)
                return;

            var widths = new float[Buttons.Length];
            for (int i = 0; i < Buttons.Length; i++)
                widths[i] = buttons[i].GetPreferredWidth();
            var maxWidth = Mathf.Max(widths);
            foreach(var button in Buttons)
                button.SetWidth(maxWidth);
        }

    }
}
