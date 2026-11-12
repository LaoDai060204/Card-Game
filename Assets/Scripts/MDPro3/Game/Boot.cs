using System;
using System.IO;
using UnityEngine;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
using LaoDai.BattleSpirits.Integration;

namespace MDPro3
{
    public class Boot : MonoBehaviour
    {
        public Slider progressBar;
        public Text text;

        private string title;
        private string dots;
        private float time;
        private bool extracting;
        private int totalNum;
        private int nowNum;
        private int startupToken;
        private GameObject startupCanvasRoot;
        private AsyncOperationHandle startupInitialization;
        private AsyncOperationHandle<Items> startupItems;
        private AsyncOperationHandle<SceneInstance> startupScene;
        private bool ownsStartupInitialization;
        private bool ownsStartupItems;
        private bool ownsStartupScene;

        private readonly List<string> zips = new();

        private void Start()
        {
            Application.targetFrameRate = 0;
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
            if (LaoDai.BattleSpirits.BattleSpiritsMode.Enabled)
                Directory.SetCurrentDirectory(Path.GetDirectoryName(Application.dataPath));
#endif

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            Environment.CurrentDirectory = Application.persistentDataPath;
            Directory.SetCurrentDirectory(Application.persistentDataPath);
            if (VersionCheck())
            {
                BetterStreamingAssets.Initialize();
                var paths = BetterStreamingAssets.GetFiles("\\", "*.zip");
                foreach (var zip in paths)
                    zips.Add(Path.GetFileName(zip).Replace(".zip", ""));
                StartCoroutine(CheckFile());
            }
#else
            InitializeLanguage();
            StartCoroutine(LoadMainSceneAsync());
#endif
        }

        private void Update()
        {
            if (startupToken != 0)
            {
                if (BattleStartupState.CurrentToken != startupToken || !BattleStartupState.IsActive)
                {
                    DestroyBattleStartupScreen();
                    return;
                }
                if (BattleStartupState.HasFailed)
                {
                    text.text = BattleStartupState.FailureMessage;
                    return;
                }
            }
            time += Time.deltaTime;
            if (time > 0.33f)
            {
                time = 0;
                dots += ".";
                if (dots == "....")
                    dots = "";
            }
            if (extracting && totalNum != 0)
            {
                float progress = (float)nowNum / totalNum;
                progressBar.value = progress;
            }
            if (totalNum == 0)
                text.text = title + dots;
            else
                text.text = title + "(" + nowNum + Program.STRING_SLASH + totalNum + ")";
        }

        private bool InitializeLanguage()
        {
            if (!Directory.Exists(Program.PATH_DATA))
            {
                Directory.CreateDirectory(Program.PATH_DATA);
                Config.Initialize(Program.PATH_CONFIG);
                InterString.Initialize();
                return true;
            }
            else
            {
                Config.Initialize(Program.PATH_CONFIG);
                InterString.Initialize();
                if(Config.Get("Version", "Version") == "Version")
                    return true;
                return false;
            }
        }

        private IEnumerator CheckFile()
        {
            IEnumerator enumerator;
            foreach (string zip in zips)
            {
                if (!Config.GetBool(zip + "_install", false))
                {
                    enumerator = Check(zip);
                    StartCoroutine(enumerator);
                    while (enumerator.MoveNext())
                        yield return enumerator.Current;
                    Config.Set(zip + "_install", "1");
                    Config.Save();
                    GC.Collect();
                }
            }
            yield return null;

            string cacheDir = Application.temporaryCachePath;
            if (Directory.Exists(cacheDir))
                foreach (string file in Directory.GetFiles(cacheDir, "*_temp.zip"))
                    SafeDeleteFile(file);

            StartCoroutine(LoadMainSceneAsync());
        }

        private IEnumerator Check(string type)
        {
            title = InterString.Get("正在读取[?]", type + ".zip");
            nowNum = 0;
            totalNum = 0;

            string tempZipPath = Path.Combine(Application.temporaryCachePath, type + "_temp.zip");

            string filePath = Path.Combine(Application.streamingAssetsPath, type + ".zip");
            using UnityWebRequest request = UnityWebRequest.Get(filePath);
            request.downloadHandler = new DownloadHandlerFile(tempZipPath);
            request.SendWebRequest();
            while (!request.isDone)
            {
                float progress = Mathf.Clamp01(request.downloadProgress / 0.9f);
                progressBar.value = progress;
                yield return null;
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                title = InterString.Get("正在解压[?]", type + ".zip");
                string outPath = "";
                if (type.Contains("_"))
                    outPath = type.Split('_')[0];
                if (outPath.Length > 0 && !Directory.Exists(outPath))
                    Directory.CreateDirectory(outPath);

                IEnumerator extractEnumerator = ExtractZipFileFromFile(tempZipPath, outPath);
                while (extractEnumerator.MoveNext())
                    yield return extractEnumerator.Current;
            }
            else
                title = $"Decompression failed: {request.error}";

            SafeDeleteFile(tempZipPath);
        }

        private IEnumerator LoadMainSceneAsync()
        {
            if (LaoDai.BattleSpirits.BattleSpiritsMode.Enabled)
            {
                yield return LoadBattleMainSceneAsync();
                yield break;
            }

            nowNum = 0;
            totalNum = 0;
            progressBar.value = 0;

            title = InterString.Get("正在初始化");

            Program.SetRoot();
            bool battle = LaoDai.BattleSpirits.BattleSpiritsMode.Enabled;
            if (battle) _ = ABLoader.CacheBattleCosmeticsAsync();
            else _ = ABLoader.CacheMasterDuelOutDuelBundles();
            while (battle ? !ABLoader.battleCosmeticsCached : !ABLoader.mdCached)
            {
                progressBar.value = battle ? ABLoader.battleCosmeticsCacheProgress : ABLoader.mdCachedProgress;
                yield return null;
            }

            Config.Initialize(Program.PATH_CONFIG);
            Config.Set("Version", Application.version[..5]);
            Config.Save();

            var ini = Addressables.InitializeAsync();
            while (!ini.IsDone)
            {
                progressBar.value = ini.PercentComplete;
                yield return null;
            }

            title = InterString.Get("正在读取数据");
            var handle = Addressables.LoadAssetAsync<Items>("ScriptableObjects/Items.asset");
            while (!handle.IsDone)
            {
                progressBar.value = handle.PercentComplete;
                yield return null;
            }
            Program.items = handle.Result;

            title = InterString.Get("正在进入游戏");
            var load = Addressables.LoadSceneAsync("SceneMain");
            while (!load.IsDone)
            {
                yield return null;
                progressBar.value = load.PercentComplete;
            }
        }

        private IEnumerator LoadBattleMainSceneAsync()
        {
            startupToken = BattleStartupState.Begin();
            nowNum = 0;
            totalNum = 0;
            progressBar.value = 0;
            title = InterString.Get("正在初始化");

            if (!TryBattleAction(KeepBattleStartupScreen,
                "Không thể mở màn hình tải. Hãy mở lại trò chơi.")) yield break;
            if (!TryBattleAction(Program.SetRoot,
                "Không thể khởi tạo dữ liệu. Hãy mở lại trò chơi.")) yield break;

            UniTask cosmetics = default;
            if (!TryBattleAction(() => cosmetics = ABLoader.CacheBattleCosmeticsAsync(),
                "Không thể tải tài nguyên. Hãy mở lại trò chơi.")) yield break;
            while (ContinueBattleStartup() && cosmetics.Status == UniTaskStatus.Pending)
            {
                progressBar.value = ABLoader.battleCosmeticsCacheProgress;
                yield return null;
            }
            if (!ContinueBattleStartup()) yield break;
            if (!TryBattleAction(() => cosmetics.GetAwaiter().GetResult(),
                "Không thể tải tài nguyên. Hãy mở lại trò chơi.")) yield break;

            if (!TryBattleAction(() =>
            {
                Config.Initialize(Program.PATH_CONFIG);
                Config.Set("Version", Application.version[..5]);
                Config.Save();
            }, "Không thể lưu cấu hình. Hãy mở lại trò chơi.")) yield break;

            if (!TryBattleOperation(() => Addressables.InitializeAsync(false),
                "Không thể tải dữ liệu. Hãy mở lại trò chơi.", out var initialize)) yield break;
            startupInitialization = initialize;
            ownsStartupInitialization = true;
            while (ContinueBattleStartup() && !initialize.IsDone)
            {
                progressBar.value = initialize.PercentComplete;
                yield return null;
            }
            if (!ContinueBattleStartup()) yield break;
            bool initialized = BattleOperationSucceeded(initialize,
                "Không thể tải dữ liệu. Hãy mở lại trò chơi.");
            ReleaseBattleLoadingHandles();
            if (!initialized) yield break;

            title = InterString.Get("正在读取数据");
            if (!TryBattleOperation(() => Addressables.LoadAssetAsync<Items>("ScriptableObjects/Items.asset"),
                "Không thể tải dữ liệu vật phẩm. Hãy mở lại trò chơi.", out var items)) yield break;
            startupItems = items;
            ownsStartupItems = true;
            while (ContinueBattleStartup() && !items.IsDone)
            {
                progressBar.value = items.PercentComplete;
                yield return null;
            }
            if (!ContinueBattleStartup()) yield break;
            if (!BattleOperationSucceeded(items,
                "Không thể tải dữ liệu vật phẩm. Hãy mở lại trò chơi.")) yield break;
            if (items.Result == null)
            {
                FailBattleStartup("Không thể tải dữ liệu vật phẩm. Hãy mở lại trò chơi.");
                yield break;
            }
            Program.items = items.Result;
            // Program retains this successful asset for the lifetime of Main.
            ownsStartupItems = false;

            title = InterString.Get("正在进入游戏");
            // Keep the normal Single scene load: newly instantiated Main UI must
            // belong to Main, while only this existing loading screen persists.
            if (!TryBattleOperation(() => Addressables.LoadSceneAsync("SceneMain"),
                "Không thể mở giao diện. Hãy mở lại trò chơi.", out var scene)) yield break;
            startupScene = scene;
            ownsStartupScene = true;
            while (ContinueBattleStartup() && !scene.IsDone)
            {
                progressBar.value = scene.PercentComplete;
                yield return null;
            }
            if (!ContinueBattleStartup()) yield break;
            if (!BattleOperationSucceeded(scene,
                "Không thể mở giao diện. Hãy mở lại trò chơi.")) yield break;
            // Keep the loaded Single scene's Addressables reference alive.
            ownsStartupScene = false;

            while (ContinueBattleStartup() && !BattleStartupState.IsReady)
                yield return null;
            if (!ContinueBattleStartup()) yield break;

            progressBar.value = 1;
            // MDPro3 opens its menu directly. Keep the opaque loading curtain
            // until Main has finished its first layout and wallpaper load.
            yield return null;
            if (!ContinueBattleStartup() || !BattleStartupState.IsReady) yield break;
            BattleStartupState.Complete(startupToken);
            DestroyBattleStartupScreen();
        }

        private void KeepBattleStartupScreen()
        {
            var canvas = text != null ? text.canvas : progressBar.GetComponentInParent<Canvas>();
            if (canvas == null) throw new InvalidOperationException("Boot Canvas is missing.");
            canvas = canvas.rootCanvas;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32000;
            startupCanvasRoot = canvas.transform.root.gameObject;
            DontDestroyOnLoad(startupCanvasRoot);
            DontDestroyOnLoad(gameObject);
        }

        private bool ContinueBattleStartup()
        {
            if (BattleStartupState.CurrentToken == startupToken && BattleStartupState.IsActive)
            {
                if (!BattleStartupState.HasFailed) return true;
                ReleaseBattleLoadingHandles();
                return false;
            }
            DestroyBattleStartupScreen();
            return false;
        }

        private bool TryBattleAction(Action action, string failure)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception error)
            {
                FailBattleStartup(failure, error);
                return false;
            }
        }

        private bool TryBattleOperation<T>(Func<AsyncOperationHandle<T>> start, string failure,
            out AsyncOperationHandle<T> operation)
        {
            operation = default;
            try
            {
                operation = start();
                return true;
            }
            catch (Exception error)
            {
                FailBattleStartup(failure, error);
                return false;
            }
        }

        private bool BattleOperationSucceeded<T>(AsyncOperationHandle<T> operation, string failure)
        {
            if (operation.Status == AsyncOperationStatus.Succeeded) return true;
            FailBattleStartup(failure, operation.OperationException);
            return false;
        }

        private void FailBattleStartup(string message, Exception error = null)
        {
            BattleStartupState.Fail(startupToken, message);
            ReleaseBattleLoadingHandles();
            if (error != null) Debug.LogException(error);
        }

        private void ReleaseBattleLoadingHandles()
        {
            if (ownsStartupInitialization)
            {
                ownsStartupInitialization = false;
                if (startupInitialization.IsValid()) Addressables.Release(startupInitialization);
            }
            if (ownsStartupItems)
            {
                ownsStartupItems = false;
                if (startupItems.IsValid()) Addressables.Release(startupItems);
            }
            if (ownsStartupScene)
            {
                ownsStartupScene = false;
                if (!startupScene.IsValid()) return;
                if (startupScene.IsDone)
                {
                    if (startupScene.Status != AsyncOperationStatus.Succeeded)
                        Addressables.Release(startupScene);
                }
                else
                {
                    // A Single scene already loading must finish. Release a
                    // failed operation, while preserving a successfully loaded
                    // Main scene even if its old curtain was cancelled.
                    startupScene.Completed += operation =>
                    {
                        if (operation.IsValid() && operation.Status != AsyncOperationStatus.Succeeded)
                            Addressables.Release(operation);
                    };
                }
            }
        }

        private void DestroyBattleStartupScreen()
        {
            ReleaseBattleLoadingHandles();
            if (startupCanvasRoot != null && startupCanvasRoot != gameObject)
                Destroy(startupCanvasRoot);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (startupToken == 0) return;
            BattleStartupState.Cancel(startupToken);
            ReleaseBattleLoadingHandles();
            if (startupCanvasRoot != null && startupCanvasRoot != gameObject)
                Destroy(startupCanvasRoot);
        }

        public static void FastExtractZipFile(string file, string dir, string password = "")
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            FastZip zip = new FastZip();
            zip.Password = password;
            zip.ExtractZip(file, dir, "");
        }

        private IEnumerator ExtractZipFile(byte[] data, string outFolder)
        {
            ZipFile zf = null;
            using MemoryStream mstrm = new(data);
            zf = new ZipFile(mstrm);
            int count = 0;
            foreach (ZipEntry zipEntry in zf)
                count++;
            totalNum = count;
            nowNum = 0;
            extracting = true;
            foreach (ZipEntry zipEntry in zf)
            {
                nowNum++;
                if (!zipEntry.IsFile)
                {
                    continue;
                }
                string entryFileName = zipEntry.Name;
                byte[] buffer = new byte[4096];
                Stream zipStream = zf.GetInputStream(zipEntry);
                string fullZipToPath = Path.Combine(outFolder, entryFileName);
                string directoryName = Path.GetDirectoryName(fullZipToPath);
                if (directoryName.Length > 0)
                    Directory.CreateDirectory(directoryName);
                using (FileStream streamWriter = File.Create(fullZipToPath))
                {
                    StreamUtils.Copy(zipStream, streamWriter, buffer);
                }
                yield return null;
            }
            if (zf != null)
            {
                zf.IsStreamOwner = true;
                zf.Close();
            }
        }

        private bool VersionCheck()
        {
            var firstInstall = InitializeLanguage();

            var installVersion = Application.version;
            var installedVersion = Config.Get("Version", "Version");
            if (installedVersion == "Version")
                firstInstall = true;

            if(firstInstall)
            {
                if(installVersion.Length > 5 || !installVersion.EndsWith("0"))
                {
                    title = "An update APK cannot be installed directly.";
                    Directory.Delete(Program.PATH_DATA);
                    return false;
                }
                else
                    return true;
            }
            else //firstInstall
            {
                if(installVersion == installedVersion)
                    return true;

                if (installVersion.Length > 5)
                {
                    if(installVersion.EndsWith("0"))
                    {
                        if (InstallNext(installedVersion, installVersion))
                            return true;
                        else if (installVersion[..5] == installedVersion)
                            return true;
                        else
                        {
                            title = InterString.Get("当前更新包需要的版本：「[?]」。", VersionPre(installVersion));
                            title += InterString.Get("已安装版本：「[?]」。", installedVersion);
                            return false;
                        }
                    }
                    else
                    {
                        if (installVersion.Substring(0, 5) == installedVersion)
                            return true;
                        else
                        {
                            title = InterString.Get("当前更新包需要的版本：「[?]」。", installVersion.Substring(0, 5));
                            title += InterString.Get("已安装版本：「[?]」。", installedVersion);
                            return false;
                        }
                    }
                }
                else
                {
                    if (installVersion.EndsWith("0"))
                        return true;
                    else
                    {
                        if(VersionPre(installVersion) == installedVersion.Substring(0, 5))
                            return true;
                        else
                        {
                            title = InterString.Get("当前更新包需要的版本：「[?]」。", VersionPre(installVersion));
                            title += InterString.Get("已安装版本：「[?]」。", installedVersion);
                            return false;
                        }
                    }
                }
            }
        }

        private bool InstallNext(string installedVersion, string installVersion)
        {
            var installedInt = GetVersionInt(installedVersion);
            var installInt = GetVersionInt(installVersion);
            if(installInt -  installedInt == 1)
                return true;
            else
                return false;
        }

        private string VersionPre(string version)
        {
            var versionInt = GetVersionInt(version);
            string returnValue = (versionInt - 1).ToString("D3");
            return returnValue.Substring(0, 1) + "." + returnValue.Substring(1, 1) + "." + returnValue.Substring(2, 1);
        }

        private int GetVersionInt(string version)
        {
            string textInt = version.Substring(0, 1) + version.Substring(2, 1) + version.Substring(4, 1);
            return int.Parse(textInt);
        }

        private IEnumerator ExtractZipFileFromFile(string zipPath, string outFolder)
        {
            ZipFile zf = null;
            try
            {
                zf = new ZipFile(zipPath);
                int count = 0;
                foreach (ZipEntry zipEntry in zf)
                    count++;
                totalNum = count;
                nowNum = 0;
                extracting = true;

                foreach (ZipEntry zipEntry in zf)
                {
                    nowNum++;
                    if (!zipEntry.IsFile)
                        continue;

                    string entryFileName = zipEntry.Name;
                    byte[] buffer = new byte[4096];
                    Stream zipStream = zf.GetInputStream(zipEntry);
                    string fullZipToPath = Path.Combine(outFolder, entryFileName);
                    string directoryName = Path.GetDirectoryName(fullZipToPath);
                    if (directoryName.Length > 0)
                        Directory.CreateDirectory(directoryName);

                    using (FileStream streamWriter = File.Create(fullZipToPath))
                    {
                        StreamUtils.Copy(zipStream, streamWriter, buffer);
                    }

                    yield return null;
                }
            }
            finally
            {
                if (zf != null)
                {
                    zf.IsStreamOwner = true;
                    zf.Close();
                }
                extracting = false;
            }
        }

        private static void SafeDeleteFile(string path)
        {
            if (File.Exists(path))
            {
                try { File.Delete(path); }
                catch (Exception e) { Debug.LogWarning($"Failed to delete file {path}: {e.Message}"); }
            }
        }
    }
}
