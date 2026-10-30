using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using LaoDai.BattleSpirits.Core;
using LaoDai.BattleSpirits.Networking;
using LaoDai.BattleSpirits.Presentation;
using MDPro3.UI.ServantUI;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

internal static class Entry
{
    private sealed class MainThreadContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<Action> queue = new ConcurrentQueue<Action>();
        public override void Post(SendOrPostCallback callback, object state) => queue.Enqueue(() => callback(state));
        public void Drain() { while (queue.TryDequeue(out var action)) action(); }
    }
    private static readonly MainThreadContext Context = new MainThreadContext();
    private static string sourceRoot, temporary;
    private static CardsDatabase database;
    private sealed class SceneUiProfile
    {
        public int layer, cameraMask, renderMode, sortingLayer;
        public AdditionalCanvasShaderChannels shaderChannels;
    }
    private static SceneUiProfile sceneUi;
    private static MainMenuUI menu;
    private static int passed;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
    private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
    private static object Call(object owner, string name, params object[] arguments)
    {
        try { return owner.GetType().GetMethod(name, Private).Invoke(owner, arguments); }
        catch (TargetInvocationException error) { throw error.InnerException; }
    }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static BattleSpiritsApp App => (BattleSpiritsApp)typeof(BattleSpiritsApp).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
    private static RectTransform Rect(string name, Transform parent = null)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect;
    }
    private static void PumpUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (!condition() && DateTime.UtcNow < deadline) { Context.Drain(); Thread.Sleep(1); }
        Context.Drain(); Assert(condition(), "Timed out waiting for controlled continuation.");
    }
    private static void Cleanup()
    {
        if (App != null) Object.Destroy(App.gameObject);
        Context.Drain();
        foreach (var go in GameObject.All.Where(go => !go.destroyed && go.transform.parent == null).ToArray()) Object.Destroy(go);
        MDPro3.UIManager.InputBlocker = null;
    }
    private static void Setup()
    {
        Cleanup();
        Keyboard.current = null;
        Application.dataPath = Path.Combine(temporary, "Assets");
        var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        canvas.layer = sceneUi.layer;
        var uiCanvas = canvas.GetComponent<Canvas>();
        uiCanvas.sortingLayerID = sceneUi.sortingLayer;
        uiCanvas.additionalShaderChannels = sceneUi.shaderChannels;
        uiCanvas.worldCamera = new GameObject("Existing UI camera").AddComponent<Camera>();
        uiCanvas.worldCamera.cullingMask = sceneUi.cameraMask;
        var owner = new GameObject("Program").AddComponent<MDPro3.Program>(); MDPro3.Program.instance = owner;
        owner.ui_ = new GameObject("UI", typeof(RectTransform)).AddComponent<MDPro3.UIManager>(); owner.ui_.transform.SetParent(canvas.transform, false); owner.ui_.tmpFont = new TMP_FontAsset();
        owner.ui_.gameObject.layer = sceneUi.layer;
        owner.menu = new GameObject("MainMenuServant").AddComponent<MDPro3.Servant.Servant>();
        menu = new GameObject("MainMenuUI", typeof(RectTransform)).AddComponent<MainMenuUI>(); menu.transform.SetParent(owner.ui_.transform, false);
        owner.menu.servantUI = menu; menu.Manager = menu.gameObject.AddComponent<YgomSystem.ElementSystem.ElementObjectManager>();
        menu.Title = Rect("Title", menu.transform).gameObject.AddComponent<TextMeshProUGUI>();
        foreach (var name in new[] { "ButtonSolo", "ButtonPuzzle", "ButtonReplay" })
        {
            var button = Rect(name, menu.transform); var art = button.gameObject.AddComponent<Image>(); art.sprite = new Sprite(); art.material = new Material();
            button.gameObject.AddComponent<MDPro3.UI.SelectionButton>(); Rect("#Text_Menu", button).gameObject.AddComponent<TextMeshProUGUI>();
        }
        MDPro3.Program.items = new MDPro3.Items();
        MDPro3.Program.items.mats.Add(new MDPro3.Items.Item { id = 1090001, path = "Mat/Mat_002" });
        MDPro3.Program.items.mates.Add(new MDPro3.Items.Item { id = 1000001, path = "Mate/M09755_Model" });
        MDPro3.ABLoader.Field = path => Task.FromResult(new GameObject(path));
        MDPro3.ABLoader.Pet = id => Task.FromResult(new GameObject("Mate").AddComponent<MDPro3.Mate>());
        Addressables.Load = path => Task.FromResult<GameObject>(null); Addressables.Releases = 0;
        menu.Initialize(owner.menu);
    }
    private static void Case(string name, Action test)
    {
        Setup(); test(); Context.Drain(); passed++; Console.WriteLine("PASS " + name);
    }
    private static void Reveal()
    {
        var curtain = Field<RectTransform>(App, "handover");
        Assert(curtain != null, "Expected handover curtain.");
        curtain.GetComponentsInChildren<Button>(true).Single().onClick.Invoke();
    }
    private static void StartLocal()
    {
        menu.OnSolo(); var pair = StarterDecks.CreatePair(database.cards);
        Call(App, "StartLocal", pair[0], pair[1], "Người Một", "Người Hai");
    }
    private static GameObject Template()
    {
        var prefab = Rect("DeckEditorUI"); var root = Rect("Root", prefab); var safe = Rect("SafeArea", root);
        foreach (var parent in new[] { root, safe, safe, safe })
        {
            string name = parent == root ? "BG" : !parent.children.Any(x => x.name == "DeckEditHeader") ? "DeckEditHeader" : !parent.children.Any(x => x.name == "DeckEditOverHeader") ? "DeckEditOverHeader" : "Footer";
            var part = Rect(name, parent); part.anchorMin = new Vector2(0.1f, 0.2f); part.gameObject.AddComponent<Image>().sprite = new Sprite();
        }
        return prefab.gameObject;
    }
    private static string[] PrefabPaths(string path)
    {
        string source=File.ReadAllText(path);
        var names=new Dictionary<string,string>();var owners=new Dictionary<string,string>();var parents=new Dictionary<string,string>();
        foreach(Match block in Regex.Matches(source,@"(?ms)^--- !u!(?<kind>\d+) &(?<id>\d+)[^\n]*\n(?<body>.*?)(?=^---|\z)"))
        {
            string body=block.Groups["body"].Value,id=block.Groups["id"].Value,kind=block.Groups["kind"].Value;
            if(kind=="1"){var name=Regex.Match(body,@"(?m)^  m_Name: (.*)$");if(name.Success)names[id]=name.Groups[1].Value.Trim().Trim('\"','\'');}
            if(kind=="224"||kind=="4"){
                var owner=Regex.Match(body,@"m_GameObject: \{fileID: (\d+)\}");var parent=Regex.Match(body,@"m_Father: \{fileID: (\d+)\}");
                if(owner.Success)owners[id]=owner.Groups[1].Value;if(parent.Success)parents[id]=parent.Groups[1].Value;
            }
        }
        var result=new List<string>();
        foreach(string id in owners.Keys){var parts=new List<string>();var seen=new HashSet<string>();string current=id;
            while(seen.Add(current)&&owners.TryGetValue(current,out string owner)&&names.TryGetValue(owner,out string name)){
                parts.Add(name);if(!parents.TryGetValue(current,out current)||current=="0")break;
            }parts.Reverse();result.Add(string.Join("/",parts));}
        return result.ToArray();
    }
    private static SceneUiProfile ReadSceneUiProfile()
    {
        var blocks = Regex.Matches(File.ReadAllText(Path.Combine(sourceRoot, "Assets/Scenes/Main.unity")),
            @"(?ms)^--- !u!(?<kind>\d+) &(?<id>\d+)[^\n]*\n(?<body>.*?)(?=^---|\z)").Cast<Match>().ToArray();
        string Scalar(string body, string field)
        {
            var value = Regex.Match(body, @"(?m)^\s*" + Regex.Escape(field) + @": ([^\r\n]+)");
            Assert(value.Success, "Missing scene field: " + field);
            return value.Groups[1].Value.Trim();
        }
        string Owner(Match block) => Scalar(block.Groups["body"].Value, "m_GameObject");
        var ui = blocks.Single(block => block.Groups["kind"].Value == "1"
            && Regex.IsMatch(block.Groups["body"].Value, @"(?m)^  m_Name: UI\r?$"));
        string uiRef = "{fileID: " + ui.Groups["id"].Value + "}";
        var canvas = blocks.Single(block => block.Groups["kind"].Value == "223" && Owner(block) == uiRef);
        string canvasBody = canvas.Groups["body"].Value;
        string cameraRef = Scalar(canvasBody, "m_Camera");
        var camera = blocks.Single(block => block.Groups["kind"].Value == "20"
            && "{fileID: " + block.Groups["id"].Value + "}" == cameraRef);
        return new SceneUiProfile {
            layer = int.Parse(Scalar(ui.Groups["body"].Value, "m_Layer")),
            cameraMask = unchecked((int)uint.Parse(Scalar(camera.Groups["body"].Value, "m_Bits"))),
            renderMode = int.Parse(Scalar(canvasBody, "m_RenderMode")),
            sortingLayer = int.Parse(Scalar(canvasBody, "m_SortingLayerID")),
            shaderChannels = (AdditionalCanvasShaderChannels)int.Parse(Scalar(canvasBody, "m_AdditionalShaderChannelsFlag"))
        };
    }
    private static void AssertUiLayers()
    {
        var overlay = Field<RectTransform>(App, "overlay");
        var rootCanvas = MDPro3.Program.instance.ui_.GetComponentInParent<Canvas>();
        var overlayCanvas = overlay.GetComponent<Canvas>();
        Assert(overlay.parent == rootCanvas.transform && overlay.gameObject.activeInHierarchy, "Overlay is outside the active UI root.");
        Assert(overlayCanvas.worldCamera == rootCanvas.worldCamera, "Overlay lost its UI camera.");
        var widgets = overlay.GetComponentsInChildren<RectTransform>(true);
        Assert(widgets.Length > 3 && overlay.GetComponentsInChildren<Graphic>(true).Length > 0, "No runtime UI to inspect.");
        foreach (var widget in widgets)
            Assert(widget.gameObject.layer == sceneUi.layer
                && (rootCanvas.worldCamera.cullingMask & (1 << widget.gameObject.layer)) != 0,
                "Camera_UI excludes runtime widget " + widget.name + " on layer " + widget.gameObject.layer + ".");
        Assert(overlayCanvas.additionalShaderChannels == rootCanvas.additionalShaderChannels, "Overlay lost source shader vertex channels.");
        Assert(overlayCanvas.sortingLayerID == rootCanvas.sortingLayerID, "Overlay moved to another sorting layer.");
    }
    public static int Main(string[] args)
    {
        SynchronizationContext.SetSynchronizationContext(Context);
        sourceRoot = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
        sceneUi = ReadSceneUiProfile();
        temporary = Path.Combine(Path.GetTempPath(), "bs-presentation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(temporary, "Assets")); Directory.CreateDirectory(Path.Combine(temporary, "Data/BattleSpirits"));
        string catalog = File.ReadAllText(Path.Combine(sourceRoot, "Data/BattleSpirits/cards.json"));
        File.WriteAllText(Path.Combine(temporary, "Data/BattleSpirits/cards.json"), catalog); database = JsonConvert.DeserializeObject<CardsDatabase>(catalog);
        try
        {
            Case("Scene-derived UI camera excludes Default and includes the UI layer", () => {
                Assert(sceneUi.renderMode == 1, "Expected the actual Screen Space Camera UI canvas.");
                Assert(sceneUi.layer != 0 && (sceneUi.cameraMask & 1) == 0, "Fixture no longer reproduces Default-layer culling.");
                Assert((sceneUi.cameraMask & (1 << sceneUi.layer)) != 0, "Scene UI layer is excluded by Camera_UI.");
                menu.OnPuzzle();
                AssertUiLayers();
            });
            Case("Solo, network, deck, rules and history screens stay inside the UI camera mask", () => {
                foreach (var open in new Action[] { menu.OnSolo, menu.OnOnline, menu.OnDeck, menu.OnPuzzle, menu.OnReplay })
                {
                    open(); Context.Drain(); AssertUiLayers();
                    Assert(!menu.gameObject.activeSelf && Field<RectTransform>(App, "surface").GetComponentsInChildren<Graphic>().Length > 0,
                        "Menu hidden without an active replacement screen.");
                    Call(App, "Close");
                }
            });
            Case("Deferred original deck decorations inherit the visible target layer", () => {
                var completion = new TaskCompletionSource<GameObject>(TaskCreationOptions.RunContinuationsAsynchronously);
                Addressables.Load = path => completion.Task;
                menu.OnDeck();
                var surface = Field<RectTransform>(App, "surface");
                Assert(surface.Find("BS_MDPro3_OriginalDeckFrame") == null, "Deferred artwork fixture loaded too early.");
                completion.SetResult(Template());
                PumpUntil(() => surface.Find("BS_MDPro3_OriginalDeckFrame") != null);
                Assert(surface.Find("BS_MDPro3_OriginalDeckFrame").GetComponentsInChildren<Image>(true).Length == 4, "Missing copied decoration graphics.");
                AssertUiLayers();
            });
            Case("Deck paging and selection keep newly created artwork and rows visible", () => {
                menu.OnDeck();
                var surface = Field<RectTransform>(App, "surface");
                var grid = surface.GetComponentsInChildren<RectTransform>(true).Single(widget => widget.name == "Grid");
                string firstPageCard = grid.GetComponentsInChildren<TextMeshProUGUI>(true).First(label => label.name == "CardName").text;
                var next = surface.GetComponentsInChildren<Button>(true).Single(button => button.GetComponentInChildren<TextMeshProUGUI>(true)?.text == "›");
                next.onClick.Invoke();
                Assert(grid.GetComponentsInChildren<TextMeshProUGUI>(true).First(label => label.name == "CardName").text != firstPageCard,
                    "Paging did not create another set of card widgets.");
                grid.GetComponentsInChildren<Button>(true).First().onClick.Invoke();
                Assert(grid.GetComponentsInChildren<RawImage>(true).Length > 0, "No card artwork widgets after paging.");
                AssertUiLayers();
            });
            Case("Handovers and refreshed board artwork use UI while 3D cosmetics keep their stage layer", () => {
                StartLocal(); AssertUiLayers(); Reveal(); AssertUiLayers();
                Call(App, "SendAction", new WireMessage { type = "action", action = "opening" });
                AssertUiLayers(); Reveal(); AssertUiLayers();
                var board = Field<BattleBoardPanel>(App, "board");
                Assert(board.Root.GetComponentsInChildren<RawImage>(true).Length > 1, "No hand/card/background images on refreshed board.");
                var backdrop = Field<BattleFieldBackdrop>(App, "backdrop");
                var stage = Field<GameObject>(backdrop, "stage");
                var stageCamera = Field<Camera>(backdrop, "stageCamera");
                Assert(stage.layer == 31 && stageCamera.cullingMask == (1 << 31), "UI fix changed the isolated cosmetic stage mask.");
                Assert(stage.GetComponentsInChildren<Transform>(true).All(item => item.gameObject.layer == 31), "Stage children leaked onto UI layer.");
                Assert(board.Background.gameObject.layer == sceneUi.layer, "RenderTexture presenter is excluded from Camera_UI.");
            });
            Case("Actual main-menu handlers preserve labels and route to Battle Spirits", () => {
                Assert(menu.Title.text.Contains("0.1.0"), "Title.");
                Assert(menu.transform.Find("ButtonSolo/#Text_Menu").GetComponent<TextMeshProUGUI>().text == "CHƠI CÙNG MÁY", "Local label.");
                menu.OnPuzzle(); Assert(BattleSpiritsApp.IsOpen && MDPro3.Program.instance.Shifted == null, "Legacy Puzzle was invoked.");
                Assert(!menu.gameObject.activeSelf && MDPro3.UIManager.InputBlocker == App, "Overlay input ownership.");
                Assert(Field<RectTransform>(App,"overlay").GetComponent<Canvas>().worldCamera == MDPro3.Program.instance.ui_.GetComponentInParent<Canvas>().worldCamera,"NestedcanvasUIcamera.");
                Call(App, "Close"); Assert(menu.gameObject.activeSelf && MDPro3.UIManager.InputBlocker == null, "Menu restored.");
            });
            Case("Update is safe without a keyboard in Input System mode", () => {
                menu.OnPuzzle();
                Call(App, "Update");
                Assert(BattleSpiritsApp.IsOpen && MDPro3.UIManager.InputBlocker == App, "Missing keyboard changed the open surface.");
            });
            Case("Update keeps the overlay open until Escape is pressed", () => {
                Keyboard.current = new Keyboard();
                menu.OnPuzzle();
                Call(App, "Update");
                Assert(BattleSpiritsApp.IsOpen && !menu.gameObject.activeSelf, "Unpressed Escape closed the overlay.");
            });
            Case("Input System Escape closes the overlay and restores menu input", () => {
                Keyboard.current = new Keyboard();
                Keyboard.current.escapeKey.wasPressedThisFrame = true;
                menu.OnPuzzle();
                Call(App, "Update");
                Assert(!BattleSpiritsApp.IsOpen && menu.gameObject.activeSelf && MDPro3.UIManager.InputBlocker == null, "Escape did not restore the menu.");
                Call(App, "Update");
                Assert(menu.gameObject.activeSelf && MDPro3.UIManager.InputBlocker == null, "Escape with no overlay changed menu input.");
            });
            Case("Actual deck builder loads full mixed catalog and pages card artwork", () => {
                menu.OnDeck(); var surface = Field<RectTransform>(App, "surface");
                Assert(Field<CardsDatabase>(App,"database").cards.Length == database.cards.Length, "Catalog data lost.");
                Assert(surface.GetComponentsInChildren<TMP_Dropdown>(true).Length == 5, "Battle Spirits filters missing.");
                var grid = surface.GetComponentsInChildren<RectTransform>(true).Single(x => x.name == "Grid");
                Assert(grid.childCount == Math.Min(42,database.cards.Length), "Artwork page size.");
            });
            Case("Printed verified copy exceptions and name-wide limits match Core", () => {
                var card = JsonConvert.DeserializeObject<CardDefinition>(JsonConvert.SerializeObject(database.cards.First(x=>x.IsPlayable)));
                card.maxCopies=20;card.restrictionVerified=true;
                var root=Rect("DeckTest");var panel=new BattleDeckPanel(root,temporary,new[]{card},new DeckDefinition{cards=Array.Empty<string>()},(id,img)=>{},deck=>{},message=>{});panel.Build();Call(panel,"Select",card);
                for(int i=0;i<21;i++)Call(panel,"AddCurrent");Assert(Field<List<string>>(panel,"deck").Count==20,"Verified20-copylimit.");
                var other=JsonConvert.DeserializeObject<CardDefinition>(JsonConvert.SerializeObject(card));other.id+="-reprint";other.maxCopies=2;
                panel=new BattleDeckPanel(Rect("DeckTest2"),temporary,new[]{card,other},new DeckDefinition{cards=Array.Empty<string>()},(id,img)=>{},deck=>{},message=>{});panel.Build();Call(panel,"Select",card);
                for(int i=0;i<3;i++)Call(panel,"AddCurrent");Assert(Field<List<string>>(panel,"deck").Count==2,"Name-widecap.");
            });
            Case("Older artwork completion cannot overwrite the newly selected card", () => {
                var image=Rect("Preview").gameObject.AddComponent<RawImage>();var newer=new Texture2D(1,1,TextureFormat.RGBA32,false);image.texture=newer;
                Field<Dictionary<RawImage,string>>(App,"requestedArt")[image]="new";Field<Dictionary<string,List<RawImage>>>(App,"pendingArt")["old"]=new List<RawImage>{image};
                Call(App,"ApplyArtToTargets","old",new Texture2D(1,1,TextureFormat.RGBA32,false));Assert(image.texture==newer,"Late oldselection overwrotepreview.");
            });
            Case("Artwork cache evicts unused entries while keeping visible images", () => {
                var art=Field<Dictionary<string,Texture2D>>(App,"art");var ages=Field<Dictionary<string,long>>(App,"artLastUsed");
                for(int i=0;i<65;i++){art["art"+i]=new Texture2D(1,1,TextureFormat.RGBA32,false);ages["art"+i]=i;}
                var active=Rect("Visible").gameObject.AddComponent<RawImage>();active.texture=art["art0"];Field<Dictionary<RawImage,string>>(App,"requestedArt")[active]="art0";
                var removed=art["art1"];Call(App,"PruneArtCache");Assert(art.Count==64&&art.ContainsKey("art0")&&!art.ContainsKey("art1")&&removed.destroyed,"LRU inactive eviction.");
            });
            Case("Destroyed artwork targets are removed from cache tracking", () => {
                var image=Rect("OldPreview").gameObject.AddComponent<RawImage>();var requested=Field<Dictionary<RawImage,string>>(App,"requestedArt");requested[image]="old";Object.Destroy(image.gameObject);Call(App,"PruneArtCache");Assert(requested.Count==0,"Destroyedtargetretained.");
            });
            Case("Original template sprites stay leased until the visual clone is destroyed", () => {
                var prefab=Template();Addressables.Load=path=>Task.FromResult(prefab);var target=Rect("Target");BattleTemplateVisuals.CopyDeckFrameAsync(target).GetAwaiter().GetResult();
                Assert(Addressables.Releases==0,"Releasedwhilevisible.");Assert(target.GetComponentsInChildren<Image>(true).Length==4,"Originalframepartsnotcopied.");
                Object.Destroy(target.gameObject);Assert(Addressables.Releases==1,"Leasewasnotreleased.");Object.Destroy(target.gameObject);Assert(Addressables.Releases==1,"Doublelease release.");
            });
            Case("Closing a template target during load releases its handle without adding UI", () => {
                var completion=new TaskCompletionSource<GameObject>(TaskCreationOptions.RunContinuationsAsynchronously);Addressables.Load=path=>completion.Task;var target=Rect("ClosingTarget");var task=BattleTemplateVisuals.CopyDeckFrameAsync(target);Object.Destroy(target.gameObject);completion.SetResult(Template());PumpUntil(()=>task.IsCompleted);task.GetAwaiter().GetResult();Assert(Addressables.Releases==1,"Cancelledlease.");
            });
            Case("Local hotseat hides each hand until its owner confirms the handover", () => {
                StartLocal();var board=Field<BattleBoardPanel>(App,"board");Assert(!board.Root.gameObject.activeSelf,"Openinghand exposed.");Reveal();Assert(board.Root.gameObject.activeSelf,"Revealfailed.");
                Call(App,"SendAction",new WireMessage{type="action",action="opening"});Assert(!board.Root.gameObject.activeSelf&&Field<FilteredMatchView>(App,"pendingLocalView").viewer==1,"Otherhand exposed duringhandover.");Reveal();
                Call(App,"SendAction",new WireMessage{type="action",action="opening"});Assert(!board.Root.gameObject.activeSelf,"Returningownerhandovermissing.");Reveal();Assert(Field<BattleMatch>(App,"localMatch").State.Phase==MatchPhase.Main,"OpeningdidnotreachMain.");
            });
            Case("Closing a board disposes tweens, render target, and input ownership", () => {
                StartLocal();var board=Field<BattleBoardPanel>(App,"board");var target=(RenderTexture)board.Background.texture;Call(App,"Close");Assert(board.Root.gameObject.destroyed&&target.released&&MDPro3.UIManager.InputBlocker==null,"Boardcleanup.");
            });
            Case("Replacing a surface also disposes the old board", () => {
                StartLocal();var board=Field<BattleBoardPanel>(App,"board");var target=(RenderTexture)board.Background.texture;Call(App,"OpenRules");Assert(board.Root.gameObject.destroyed&&target.released,"Replacedsurfaceleakedboard.");
            });
            Case("A completed local match saves and replays actual engine commands", () => {
                StartLocal();Reveal();Call(App,"SendAction",new WireMessage{type="action",action="opening"});Reveal();Call(App,"SendAction",new WireMessage{type="action",action="opening"});Reveal();Call(App,"SendAction",new WireMessage{type="action",action="concede"});
                string file=Directory.GetFiles(Path.Combine(temporary,"Data/BattleSpirits/Replays"),"*.json").Single();Call(App,"OpenHistory");Call(App,"LoadReplay",file);Set(App,"replayStep",3);Call(App,"RenderReplay");var view=Field<BattleBoardPanel>(App,"board");Assert(Field<FilteredMatchView>(view,"current").winner==1,"Replayresultdiverged.");
            });
            Case("Network lobby and chat render untrusted text as plain text", () => {
                menu.OnOnline();var chat=Field<TextMeshProUGUI>(App,"chatText");var lobby=Field<TextMeshProUGUI>(App,"lobbyText");Assert(!chat.richText&&!lobby.richText&&!Field<TextMeshProUGUI>(App,"status").richText,"Richtext enabled.");Call(App,"OnNetworkMessage",new WireMessage{type="chat",name="Player",text="<size=300>message</size>"});Assert(chat.text.Contains("<size=300>"),"Chatcontentwasnotretainedasplaintext.");
            });
            Case("A disconnected lobby disables stale ready and start buttons", () => {
                menu.OnOnline();Call(App,"OnNetworkMessage",new WireMessage{type="welcome",playerIndex=0});Call(App,"OnNetworkMessage",new WireMessage{type="lobby",players=new[]{new LobbyPlayerView{playerIndex=0,ready=true},new LobbyPlayerView{playerIndex=1,ready=true}}});Assert(Field<Button>(App,"startButton").interactable,"Fixturelobbystart.");Call(App,"OnNetworkMessage",new WireMessage{type="disconnected",text="closed"});Assert(!Field<Button>(App,"readyButton").interactable&&!Field<Button>(App,"startButton").interactable,"Stalecontrols.");
            });
            Case("A disconnected board disables actions and offers a return route", () => {
                menu.OnOnline();var match=BattleMatch.CreateDefault(database.cards);Call(App,"OnNetworkMessage",new WireMessage{type="state",view=ViewFactory.Create(match,0)});var board=Field<BattleBoardPanel>(App,"board");Call(App,"OnNetworkMessage",new WireMessage{type="disconnected",text="closed"});Assert(!Field<bool>(board,"allowActions"),"Commandsstillenabled.");Assert(Field<TextMeshProUGUI>(board,"decision").text.StartsWith("KẾT NỐI ĐÃ NGẮT"),"Wrongdisconnectlabel.");Assert(!board.Root.Find("Header/Concede").GetComponent<Button>().interactable,"Concedestillenabled.");
            });
            Case("Late cosmetic asset loads are discarded after closing their stage", () => {
                var completion = new TaskCompletionSource<GameObject>(TaskCreationOptions.RunContinuationsAsynchronously);
                MDPro3.ABLoader.Field = path => completion.Task;
                var component = new GameObject("Backdrop").AddComponent<BattleFieldBackdrop>();
                component.Load(Rect("Target").gameObject.AddComponent<RawImage>());
                Object.Destroy(component.gameObject);var late = new GameObject("LateField");completion.SetResult(late);
                PumpUntil(() => late.destroyed);Assert(!component.IsLoaded,"Closedstagebecameactive.");
            });
            Case("Cosmetic events use the filtered view's own/opponent orientation", () => {
                var component = new GameObject("Backdrop").AddComponent<BattleFieldBackdrop>();
                component.Load(Rect("Target").gameObject.AddComponent<RawImage>());var mates=Field<MDPro3.Mate[]>(component,"mates");
                component.PlayEvent(new PublicBattleEvent{kind="Attacked",actor=1},1);Assert(mates[0].Played.Last()==MDPro3.Mate.MateAction.Attack,"Ownmateorientation.");
                component.PlayEvent(new PublicBattleEvent{kind="GameWon",actor=0},1);Assert(mates[1].Played.Last()==MDPro3.Mate.MateAction.Victory&&mates[0].Played.Last()==MDPro3.Mate.MateAction.Defeat,"Winnerorientation.");
            });
            Case("Decorative copy and menu-label paths match the actual MDPro3 prefabs", () => {
                var paths = PrefabPaths(Path.Combine(sourceRoot,"Assets/Prefabs/ServantUI/DeckEditorUI.prefab"));
                foreach(string path in new[]{"Root/BG","Root/SafeArea/DeckEditHeader","Root/SafeArea/DeckEditOverHeader","Root/SafeArea/Footer"})
                    Assert(paths.Any(value=>value.EndsWith("/"+path,StringComparison.Ordinal)),"Missingactualtemplatepath: "+path);
                paths=PrefabPaths(Path.Combine(sourceRoot,"Assets/Prefabs/ServantUI/MainMenuUI.prefab"));
                foreach(string button in new[]{"ButtonSolo","ButtonPuzzle","ButtonReplay"})
                    Assert(paths.Any(value=>value.Contains("/"+button+"/")&&value.EndsWith("_Menu",StringComparison.Ordinal)),"Missingactualmenulabel: "+button);
            });
            Console.WriteLine("PASS " + passed + " production presentation scenarios; controlled Unity/asset/tween stand-ins, no Unity Editor rendering.");
            return 0;
        }
        finally { Cleanup();Context.Drain();Directory.Delete(temporary,true); }
    }
}
