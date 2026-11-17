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

            StartCoroutine(CheckUpdateAsync());
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
            StartCoroutine(LoadMyCardNewsAsync());

            EnsureButtonsWidth();
            Program.instance.ReadParams();
            if(!Program.exitOnReturn)
                VersionIntroduction.Show();
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

        private IEnumerator CheckUpdateAsync()
        {
            yield return new WaitForSeconds(3);
            using var www = UnityWebRequest.Get(Settings.Data.MDPro3VersionUrl);
            www.timeout = 15;
            www.SetRequestHeader("Cache-Control", "max-age=0, no-cache, no-store");
            www.SetRequestHeader("Pragma", "no-cache");
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                if (Settings.TryReadPrereleaseVersion(www.downloadHandler.text, out var version)
                    && Application.version != version)
                    MessageManager.Cast(InterString.Get("A new version \"[?]\" has been detected.", version));
            }
            else
                MessageManager.Cast(InterString.Get("Failed to check for updates!"));

            var packageUrl = Settings.GetPrereleasePackUrl();
            var versionUrl = Settings.GetPrereleasePackVersionUrl();
            if (Settings.IsRetiredPrereleaseUrl(packageUrl) || Settings.IsRetiredPrereleaseUrl(versionUrl))
            {
                Debug.LogWarning("The configured TransSuperpre source has been retired; keeping installed pre-release cards and skipping its optional update check.");
                yield break;
            }
            if (!NetUtil.TryGetDownloadFileName(packageUrl, new[] { ".zip", ".ypk" }, out var fileName))
                yield break;
            var filePath = Path.Combine(Program.PATH_EXPANSIONS, fileName);
            if (!File.Exists(filePath))
            {
                Config.Set("Prerelease", "0");
                Config.Save();
            }

            using var www2 = UnityWebRequest.Get(versionUrl);
            www2.timeout = 15;
            yield return www2.SendWebRequest();
            if (www2.result == UnityWebRequest.Result.Success)
            {
                if (Settings.TryReadPrereleaseVersion(www2.downloadHandler.text, out var version)
                    && Config.Get("Prerelease", "0") != version)
                    MessageManager.Cast(InterString.Get("A new version of the Pre-Release Cards was detected. Go to [SETTINGS] > [Expansions] > [Update Pre-Release Cards] to update them."));
            }
        }

        private IEnumerator LoadMyCardNewsAsync()
        {
            while (!OnlineService.MyCardNewsInitialized)
                yield return null;
            var news = OnlineService.myCardNews;
            if (news == null)
                yield break;
            GetUI<MainMenuUI>().News.news = news;
            GetUI<MainMenuUI>().News.LoadNews();
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
