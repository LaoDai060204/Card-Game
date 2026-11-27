using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MDPro3.Duel.YGOSharp;
using UnityEngine.AddressableAssets;
using MDPro3.Net;
using MDPro3.Servant;
using Cysharp.Threading.Tasks;
using MDPro3.Utility;
using MDPro3.IO;
using MDPro3.BattleSpirits;



#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MDPro3
{
    public class Program : MonoBehaviour
    {
#if UNITY_EDITOR
        [Header("Debug")]
        public bool debugGameMessage;
        public bool debugCardAnimation;
        public AnimationCurve curve;
#endif

        [Header("Public References")]
        public Transform container_3D;
        public Transform container_2D;
        public CardRenderer cardRenderer;

        [Header("Manager")]
        public CameraManager camera_;
        public UIManager ui_;
        public BackgroundManager background_;
        public AudioManager audio_;
        public TextureManager texture_;
        public MessageManager message_;

        [Header("Servants")]
        public Servant.MainMenu menu;
        public SoloSelector solo;
        public OnlineServant online;
        public PuzzlePortal puzzlePortal;
        public PuzzleSelector puzzle;
        public ReplaySelector replay;
        public CutinViewer cutin;
        public MateViewer mate;
        public DeckSelector deckSelector;
        public SettingServant setting;
        public Appearance appearance;
        public CharacterSelector character;
        public OcgCore ocgcore;
        public RoomServant room;
        public DeckEditor deckEditor;
        public OnlineDeckViewer onlineDeckViewer;
        public DeckBrowser deckBrowser;

#if UNITY_EDITOR
        [Header("Test")]
        public float testFloat = 1f;
        public float testFloat2 = 1f;
        public float testFloat3 = 1f;
        public float testFloat4 = 1f;
        public float testFloat5 = 1f;
#endif

        [HideInInspector]
        public Servant.Servant currentServant;
        [HideInInspector]
        public Servant.Servant lastServant;
        [HideInInspector]
        public Servant.Servant currentSubServant;
        [HideInInspector]
        public int depth;

        #region Const
        public static bool Running = true;
        public const string PATH_ART = "Picture/Art/";
        public const string PATH_ALT_ART = "Picture/Art2/";
        public const string PATH_CARD_PIC = "Picture/CardGenerated/";
        public const string PATH_CLOSEUP = "Picture/Closeup/";
        public const string PATH_OVER_FRAME = "Picture/OverFrame/";
        public const string PATH_DATA = "Data/";
        public const string PATH_LOCALES = "Data/locales/";
        public const string PATH_CONFIG = "Data/config.conf";
        public const string PATH_DECK = "Deck/";
        public const string PATH_EXPANSIONS = "Expansions/";
        public const string PATH_PUZZLE = "Puzzle/";
        public const string PATH_REPLAY = "Replay/";
        public const string PATH_DIY = "Picture/DIY/";
        public const string PATH_VIDEO_ART = "Video/Art/";

        public const string EXPANSION_CONF = ".conf";
        public const string EXPANSION_YDK = ".ydk";
        public const string EXPANSION_PNG = ".png";
        public const string EXPANSION_JPG = ".jpg";
        public const string EXPANSION_YRP = ".yrp";
        public const string EXPANSION_YRP3D = ".yrp3d";
        public const string EXPANSION_LUA = ".lua";
        public const string EXPANSION_MP4 = ".mp4";
        public const string STRING_SLASH = "/";
        public const string STRING_LINE_BREAK = "\r\n";
        public const string SCRIPT_ZIP = "script.zip";

        #endregion

        #region Initializement

        public static Program instance;
        public static Items items;

        private readonly List<Manager> managers = new();
        private readonly List<Servant.Servant> servants = new();
        public static bool exitOnReturn = false;

        private async UniTask Initialize()
        {
            if (!Directory.Exists(PATH_DATA))
                Directory.CreateDirectory(PATH_DATA);
            Config.Initialize(PATH_CONFIG);
            BattleCatalog.Initialize(Application.streamingAssetsPath, Application.persistentDataPath);

            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            await ResourceManager.InitializeAsync();
            if(!ABLoader.mdCached)
                await ABLoader.CacheInterfaceBundles();

            if(items == null)
            {
                var handle = Addressables.LoadAssetAsync<Items>("ScriptableObjects/Items.asset");
                await handle.Task;
                items = handle.Result;
            }

            items.Initialize();
            await CardImageLoader.InitializeAsync();

            InitializeAllManagers();
            BattleIntegration.Initialize(this);
            InitializeAllServants();
        }

        public void ReadParams()
        {
            BattleIntegration.ReadCommandLine(Environment.GetCommandLineArgs());
        }

        public void InitializeForDataChange(Action delegation = null)
        {
            UIManager.ShowLoading();
            _ = InitializeForDataChangeAsync(delegation);
        }

        private async UniTask InitializeForDataChangeAsync(Action delegation)
        {
            await ResourceManager.InitializeAsync();
            await CardImageLoader.InitializeAsync();

            await UniTask.RunOnThreadPool(() =>
            {
                StringHelper.Initialize();
                CardsManager.Initialize();
                delegation?.Invoke();
            });

            UIManager.HideLoading();
        }

        private void InitializeAllManagers()
        {
            managers.Add(texture_);
            managers.Add(ui_);
            managers.Add(camera_);
            managers.Add(audio_);
            managers.Add(background_);
            managers.Add(message_);

            foreach (Manager manager in managers)
                manager.Initialize();
        }

        private void InitializeAllServants()
        {
            servants.Add(setting);
            servants.Add(menu);
            servants.Add(solo);
            servants.Add(online);
            servants.Add(puzzlePortal);
            servants.Add(puzzle);
            servants.Add(replay);
            servants.Add(cutin);
            servants.Add(mate);
            servants.Add(deckSelector);
            servants.Add(appearance);
            servants.Add(deckEditor);
            servants.Add(deckBrowser);
            foreach (Servant.Servant servant in servants)
                servant.Initialize();
        }

        #endregion

        #region MonoBehaviors

        public const string PATH_ROOT_EDITOR = "Platforms/";
        public const string PATH_ROOT_WINDOWS64 = "StandaloneWindows64/";
        public const string PATH_ROOT_LINUX = "StandaloneLinux64/";
        public const string PATH_ROOT_ANDROID = "Android/";
        public const string PATH_ROOT_IOS = "iOS/";
        public const string PATH_ROOT_MAC = "StandaloneOSX/";
        public const string PATH_TEMP_FOLDER = "TempFolder/";
        public static string root = PATH_ROOT_WINDOWS64;

        private void Awake()
        {
            SetRoot();
            instance = this;
            _ = Initialize();

            //ABLoader.LoadFromFolder("Robber/fxp_somldg_Advance_S1imp_001");
            //ABLoader.LoadFromFolder("Robber/fxp_somldg_Advance_S2imp_001");
            //ABLoader.LoadFromFolder("Robber/fxp_somldg_Fusion_S1imp_001");
            //ABLoader.LoadFromFolder("Robber/fxp_somldg_Fusion_S2imp_001");
        }

        public static void SetRoot()
        {
            root = PATH_ROOT_WINDOWS64;

#if UNITY_ANDROID
            root = PATH_ROOT_ANDROID;
#elif UNITY_IOS
            root = PATH_ROOT_IOS;
#elif UNITY_STANDALONE_OSX
            root = PATH_ROOT_MAC;
#elif UNITY_STANDALONE_LINUX
            root = PATH_ROOT_LINUX;
#endif

#if UNITY_EDITOR
            root = PATH_ROOT_EDITOR + root;
#endif
        }

        public float TimeScale
        {
            get 
            { 
                return m_TimeScale;
            }
            set 
            {
                m_TimeScale = value;
                Time.timeScale = value;
            }
        }
        private float m_TimeScale = 1f;
#if UNITY_EDITOR
        public float timeScaleForEditor = 1;
#endif

        private void Update()
        {
            foreach (Manager manager in managers) 
                manager.PerFrameFunction();
            foreach (Servant.Servant servant in servants.ToArray())
                servant.PerFrameFunction();

#if UNITY_EDITOR
            TimeScale = timeScaleForEditor;
#endif
        }

        public void UnloadUnusedAssets()
        {
            _ = UnloadUnusedAssetsAsync();
        }

        private async UniTask UnloadUnusedAssetsAsync()
        {
            await Resources.UnloadUnusedAssets();
        }

        public static bool noAccess = false;

        #endregion

        #region Tools

        public static void Debug(string text)
        {
#if UNITY_EDITOR
            UnityEngine.Debug.Log(text);
#endif
        }

        public void RegisterServant(Servant.Servant servant)
        {
            if (servant == null || servants.Contains(servant)) return;
            servants.Add(servant);
            servant.Initialize();
        }

        public void ShiftToServant(Servant.Servant servant)
        {
            lastServant = currentServant;
            currentServant = servant;
            foreach (var ser in servants)
                if (ser != servant)
                    ser.Hide(servant.Depth);
            servant.Show(depth);
            depth = servant.Depth;
        }

        public void ShowSubServant(Servant.Servant servant)
        {
            if (currentSubServant == null)
            {
                servant.Show(0);
                currentSubServant = servant;
            }
            else
            {
                currentSubServant.Hide(servant.Depth);
                servant.Show(currentSubServant.Depth);
                currentSubServant = servant;
            }
        }

        public void ExitCurrentServant()
        {
            if (currentSubServant != null)
                currentSubServant.OnReturn();
            else
            {
                if (currentServant == null)
                {
                    foreach (var servant in servants)
                        if (servant.showing)
                        {
                            currentServant = servant;
                            break;
                        }
                    if(currentServant == null)
                        currentServant = online;
                }
                currentServant.OnReturn();
            }
        }

        public void ExitDuel()
        {
            currentSubServant.OnReturn();
            currentServant.OnReturn();
        }

        #endregion

        #region System

        private void OnApplicationQuit()
        {
            Running = false;
            Config.Save();
            ClearCache();
            BattleIntegration.Shutdown();
            FileManager.Dispose();
        }

        private void ClearCache()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaObject cacheDir = currentActivity.Call<AndroidJavaObject>("getCacheDir");
            string cachePath = cacheDir.Call<string>("getAbsolutePath");
            Tools.ClearDirectoryRecursively(new DirectoryInfo(cachePath));
#else
            if (Directory.Exists(PATH_TEMP_FOLDER))
                Directory.Delete(PATH_TEMP_FOLDER, true);
#endif
        }

        public static void GameQuit()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        #endregion
    }
}
