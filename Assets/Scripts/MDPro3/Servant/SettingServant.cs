using MDPro3.Net;
using MDPro3.UI;
using MDPro3.UI.ServantUI;
using MDPro3.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using YgomSystem.LocalFileSystem;
using static MDPro3.CardRenderer;

namespace MDPro3.Servant
{
    public class SettingServant : Servant
    {
        [Header("Setting Servant")]
        [HideInInspector] public SelectionToggle_Setting lastSelectedToggle;
        [HideInInspector] public SelectionButton_Setting lastSelectedButton;


        #region Servant

        public override int Depth => 1;
        protected override bool ShowLine => false;
        protected override float BlackAlpha => 0.6f;
        protected override float SubBlackAlpha => 0.9f;

        public override void Initialize()
        {
            returnServant = Program.instance.menu;
            base.Initialize();

            InitializeSettings();
        }

        protected override void ApplyShowArrangement(int preDepth)
        {
            base.ApplyShowArrangement(preDepth);
            RefreshCharacterName();

            if (preDepth <= Depth)
            {
                servantUI.SelectDefaultSelectable();
            }

            if (Program.instance.currentServant is MDPro3.BattleSpirits.BattleDuelServant)
            {
                Program.instance.currentSubServant = this;
                UIManager.ShowFPSRight();
            }
        }

        public override void Select(bool forced = false)
        {
            if (!forced && !UserInput.NeedDefaultSelect())
                return;

            if (lastSelectable != null)
            {
                if (lastSelectable.TryGetComponent<SelectionButton_Setting>(out _))
                    EventSystem.current.SetSelectedGameObject(lastSelectable.gameObject);
                else if (lastSelectable.TryGetComponent<SelectionToggle_Setting>(out _))
                    EventSystem.current.SetSelectedGameObject(lastSelectable.gameObject);
                else
                    servantUI.SelectDefaultSelectable();
            }
            else
                servantUI.SelectDefaultSelectable();
        }

        public override void OnReturn()
        {
            if (inTransition) return;
            if(returnAction != null)
            {
                returnAction.Invoke();
                return;
            }
            AudioManager.PlaySE("SE_MENU_CANCEL");
            GameObject selected = EventSystem.current.currentSelectedGameObject;

            if (selected == null)
                OnExit();
            else if (Cursor.lockState == CursorLockMode.None)
                OnExit();
            else if (selected.TryGetComponent<SelectionButton_Setting>(out _))
            {
                if (lastSelectedToggle != null)
                    lastSelectedToggle.GetSelectable().Select();
                else
                    servantUI.SelectDefaultSelectable();
            }
            else
                OnExit();
        }

        #endregion

        #region Common
        private void InitializeSettings()
        {
            QualitySettings.vSyncCount = 0;

            AudioManager.SetBGMVol(Config.GetFloat("BgmVol", 0.7f));
            AudioManager.SetSeVol(Config.GetFloat("SEVol", 0.7f));
            AudioManager.SetVoiceVol(Config.GetFloat("VoiceVol", 0.7f));

#if UNITY_ANDROID || UNITY_IOS
            if (Config.Have("Resolution"))
            {
                var resolution = Config.Get("Resolution", "1920 x 1080");
                Screen.SetResolution(int.Parse(Regex.Split(resolution, " x ")[0]), int.Parse(Regex.Split(resolution, " x ")[1]), FullScreenMode.FullScreenWindow);
            }
#endif

            Program.instance.camera_.urpAsset.renderScale = SettingServantUI.GetScale();
            // TODO FAA
            SettingServantUI.ChangeAAA(Config.GetFloat("AAA", 0f));
            SettingServantUI.ChangeShadow(Config.GetFloat("Shadow", 0f));
            SetFpsToConfig();
            SettingServantUI.ChangeShowFPS();
            Program.instance.background_.Change(int.Parse(Config.Get("Background", "0")));
        }

        public static void SetFpsToConfig()
        {
            SettingServantUI.ChangeFPS(SettingServantUI.GetFPS());
        }

        public float GetBGMVolum()
        {
            if (servantUI == null)
                return Config.GetFloat("BgmVol", 0.7f);
            else
                return GetUI<SettingServantUI>().GetBGMVolum();
        }

        public void RefreshCharacterName()
        {
            if (servantUI == null)
                return;

            GetUI<SettingServantUI>().RefreshCharacterName();
        }

        #endregion

        #region Download Prerelease

        private bool checkingPrereleaseUpdate;

        private struct UpdatePreleaseHints
        {
            public readonly string CheckingUpdate => InterString.Get("Checking for Updates");
            public readonly string CheckFailure => InterString.Get("Failed to check for updates!");
            public readonly string Updating => InterString.Get("Update in progress. Please wait for it to finish before performing any other actions.");
            public readonly string UpdateSuccess => InterString.Get("Pre-release cards updated successfully.");
            public readonly string UpdateFailure => InterString.Get("Failed to update pre-release cards.");
            public readonly string Updated => InterString.Get("The pre-release cards are already up to date.");
            public readonly string Progress(float progress) 
                => (progress * 100f).ToString("0.##") + "%";
        }

        private readonly UpdatePreleaseHints pHints = new();

        public void UpdatePrerelease()
        {
            if (MDPro3.BattleSpirits.BattleSpiritsMode.IsEnabled)
            { MessageManager.Cast("Battle Spirits card content is included with this application."); return; }
            if (!checkingPrereleaseUpdate)
            {
                if (Settings.IsRetiredPrereleaseUrl(Settings.GetPrereleasePackUrl()))
                {
                    Debug.LogWarning("The configured TransSuperpre package source has been retired. Keeping installed cards; configure an available package URL before updating.");
                    MessageManager.Cast(pHints.CheckFailure);
                    return;
                }
                checkingPrereleaseUpdate = true;
                StartCoroutine(UpdatePrereleaseChunkedCoroutine());
            }
        }

        private IEnumerator UpdatePrereleaseChunkedCoroutine()
        {
            var url = Settings.GetPrereleasePackUrl();
            if (!NetUtil.TryGetDownloadFileName(url, new[] { ".zip", ".ypk" }, out var fileName))
            {
                checkingPrereleaseUpdate = false;
                MessageManager.Cast(pHints.CheckFailure);
                yield break;
            }
            var targetPath = Path.Combine(Program.PATH_EXPANSIONS, fileName);
            var filePath = targetPath + "." + Guid.NewGuid().ToString("N") + ".download";
            var etagConfigKey = "Prerelease";
            const long chunkSize = 10 * 1024 * 1024;

            var ui = GetUI<SettingServantUI>();
            var activeRequests = new List<UnityWebRequest>();
            ui.ButtonUpdatePrerelease.SetModeText(pHints.CheckingUpdate);

            try
            {
                Directory.CreateDirectory(Program.PATH_EXPANSIONS);
                using var headRequest = UnityWebRequest.Head(url);
                headRequest.timeout = 15;
                yield return headRequest.SendWebRequest();

                if (headRequest.result != UnityWebRequest.Result.Success)
                {
                    OnlineService.LogRequestFailure(headRequest, "prerelease package HEAD");
                    MessageManager.Cast(pHints.CheckFailure);
                    yield break;
                }

                string remoteEtag = headRequest.GetResponseHeader("ETag");
                string contentLengthHeader = headRequest.GetResponseHeader("Content-Length");
                if (!long.TryParse(contentLengthHeader, out long totalSize) || totalSize <= 0)
                {
                    Debug.LogError("Invalid or missing Content-Length, cannot proceed.");
                    MessageManager.Cast(pHints.CheckFailure);
                    yield break;
                }

                if (!string.IsNullOrEmpty(remoteEtag))
                {
                    string localEtag = Config.Get(etagConfigKey, Config.STRING_NO);
                    if (localEtag == remoteEtag && File.Exists(targetPath)
                        && Language.GetConfig() == Language.GetPrereleaseConfig())
                    {
                        MessageManager.Cast(pHints.Updated);
                        yield break;
                    }
                }

                bool supportsRange = false;
                using var probeRequest = UnityWebRequest.Get(url);
                probeRequest.timeout = 15;
                probeRequest.SetRequestHeader("Range", "bytes=0-0");
                yield return probeRequest.SendWebRequest();

                if (probeRequest.result == UnityWebRequest.Result.Success && probeRequest.responseCode == 206)
                {
                    supportsRange = true;
                }
                else
                {
                    Debug.LogWarning("Server does not support Range requests, falling back to full download.");
                }

                if (supportsRange)
                {
                    using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read, 81920);
                    long totalDownloadedBytes = 0;
                    int maxConcurrent = 5;

                    long chunks = (totalSize + chunkSize - 1) / chunkSize;
                    long[] chunkStart = new long[chunks];
                    long[] chunkEnd = new long[chunks];
                    for (long i = 0; i < chunks; i++)
                    {
                        chunkStart[i] = i * chunkSize;
                        chunkEnd[i] = Math.Min(chunkStart[i] + chunkSize - 1, totalSize - 1);
                    }

                    int nextChunkIndex = 0;
                    List<int> activeChunkIndices = new List<int>();

                    while (totalDownloadedBytes < totalSize)
                    {
                        long currentDownloaded = totalDownloadedBytes;
                        foreach (var req in activeRequests)
                        {
                            currentDownloaded += (long)req.downloadedBytes;
                        }
                        float progress = Mathf.Clamp01((float)currentDownloaded / totalSize);
                        ui.ButtonUpdatePrerelease.SetModeText(pHints.Progress(progress));

                        while (activeRequests.Count < maxConcurrent && nextChunkIndex < chunks)
                        {
                            int idx = nextChunkIndex++;
                            var req = UnityWebRequest.Get(url);
                            req.timeout = 120;
                            req.SetRequestHeader("Range", $"bytes={chunkStart[idx]}-{chunkEnd[idx]}");
                            req.SendWebRequest();
                            activeRequests.Add(req);
                            activeChunkIndices.Add(idx);
                        }

                        yield return null;

                        for (int i = activeRequests.Count - 1; i >= 0; i--)
                        {
                            var req = activeRequests[i];
                            if (req.isDone)
                            {
                                int chunkIdx = activeChunkIndices[i];
                                byte[] data = req.downloadHandler.data;
                                if (req.result != UnityWebRequest.Result.Success || req.responseCode != 206
                                    || !NetUtil.IsExpectedRange(req.GetResponseHeader("Content-Range"),
                                        chunkStart[chunkIdx], chunkEnd[chunkIdx], totalSize, data?.LongLength ?? 0))
                                {
                                    OnlineService.LogRequestFailure(req, "prerelease package range");
                                    MessageManager.Cast(pHints.UpdateFailure);
                                    yield break;
                                }

                                fs.Seek(chunkStart[chunkIdx], SeekOrigin.Begin);
                                fs.Write(data, 0, data.Length);
                                totalDownloadedBytes += data.Length;

                                req.Dispose();
                                activeRequests.RemoveAt(i);
                                activeChunkIndices.RemoveAt(i);
                            }
                        }
                    }

                    ui.ButtonUpdatePrerelease.SetModeText(pHints.Progress(1f));

                    fs.Flush();
                    if (fs.Length != totalSize)
                    {
                        Debug.LogError("Downloaded file size mismatch.");
                        MessageManager.Cast(pHints.UpdateFailure);
                        yield break;
                    }
                }
                else
                {
                    using var fullRequest = UnityWebRequest.Get(url);
                    fullRequest.timeout = 120;
                    fullRequest.downloadHandler = new DownloadHandlerBuffer();
                    var operation = fullRequest.SendWebRequest();

                    using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read, 81920);
                    long writtenBytes = 0;

                    while (!operation.isDone)
                    {
                        ulong downloaded = fullRequest.downloadedBytes;
                        if (downloaded > (ulong)writtenBytes)
                        {
                            byte[] data = fullRequest.downloadHandler.data;
                            int newDataLength = (int)(downloaded - (ulong)writtenBytes);
                            if (data != null && newDataLength > 0)
                            {
                                fs.Write(data, (int)writtenBytes, newDataLength);
                                writtenBytes += newDataLength;
                            }
                        }

                        float progress = fullRequest.downloadProgress;
                        ui.ButtonUpdatePrerelease.SetModeText(pHints.Progress(progress));
                        yield return null;
                    }

                    if (fullRequest.result != UnityWebRequest.Result.Success)
                    {
                        OnlineService.LogRequestFailure(fullRequest, "prerelease package download");
                        MessageManager.Cast(pHints.UpdateFailure);
                        yield break;
                    }

                    byte[] finalData = fullRequest.downloadHandler.data;
                    if (finalData != null && finalData.Length > writtenBytes)
                    {
                        fs.Write(finalData, (int)writtenBytes, finalData.Length - (int)writtenBytes);
                    }

                    fs.Flush();
                    if (fs.Length != totalSize)
                    {
                        Debug.LogError("Downloaded file size mismatch.");
                        MessageManager.Cast(pHints.UpdateFailure);
                        yield break;
                    }
                }

                if (!NetUtil.TryCommitDownload(filePath, targetPath))
                {
                    MessageManager.Cast(pHints.UpdateFailure);
                    yield break;
                }
                Config.Set(etagConfigKey, string.IsNullOrEmpty(remoteEtag) ? Config.STRING_NO : remoteEtag);
                Language.SetPrereleaseConfig(Language.GetConfig());
                Config.Save();
                MessageManager.Cast(pHints.UpdateSuccess);
                Program.instance.InitializeForDataChange();
            }
            finally
            {
                foreach (var request in activeRequests)
                    request.Dispose();
                NetUtil.DeleteTemporaryDownload(filePath);
                ui.ButtonUpdatePrerelease.SetModeText(string.Empty);
                checkingPrereleaseUpdate = false;
            }
        }
        #endregion

        #region Download YPK

        private bool downloadingYPK;

        public void DownloadYPK()
        {
            if (MDPro3.BattleSpirits.BattleSpiritsMode.IsEnabled)
            { MessageManager.Cast("Battle Spirits card content is included with this application."); return; }
            if (!downloadingYPK)
            {
                var selections = new List<string>()
                {
                    InterString.Get("Download expansion@nEnter the expansion download URL"),
                    string.Empty
                };
                UIManager.ShowPopupInput(selections, DownloadYPK, null, TmpInputValidation.ValidationType.None);
            }
            else
                MessageManager.Toast(InterString.Get("A download is already in progress. Please try again later."));
        }

        private void DownloadYPK(string url)
        {
            if (downloadingYPK)
                return;
            string[] supportedExts = new string[] { ".zip", ".ypk" };
            if (!NetUtil.TryGetDownloadFileName(url, supportedExts, out var fileName))
            {
                MessageManager.Toast(InterString.Get("Invalid Download URL."));
                return;
            }
            StartCoroutine(DownloadYpkCoroutine(url, fileName));
        }

        private IEnumerator DownloadYpkCoroutine(string url, string fileName)
        {
            downloadingYPK = true;
            try
            {
                using var request = UnityWebRequest.Get(url);
                request.timeout = 120;
                request.SendWebRequest();
                while (!request.isDone)
                {
                    GetUI<SettingServantUI>().ButtonDownloadYPK.SetModeText((request.downloadProgress * 100f).ToString("0.##") + "%");
                    yield return new WaitForSeconds(0.3f);
                }

                var filePath = Path.Combine(Program.PATH_EXPANSIONS, fileName);
                if (request.result == UnityWebRequest.Result.Success
                    && NetUtil.TrySaveDownload(request.downloadHandler.data, filePath))
                {
                    MessageManager.Cast(InterString.Get("Download successful."));
                    Program.instance.InitializeForDataChange();
                }
                else
                {
                    OnlineService.LogRequestFailure(request, "expansion download");
                    MessageManager.Cast(InterString.Get("Download failed."));
                }
            }
            finally
            {
                GetUI<SettingServantUI>().ButtonDownloadYPK.SetModeText(string.Empty);
                downloadingYPK = false;
            }
        }

        #endregion

    }
}
