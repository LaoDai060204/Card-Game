using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using LaoDai.BattleSpirits.Core;
using LaoDai.BattleSpirits.Integration;
using LaoDai.BattleSpirits.NativeUI;
using LaoDai.BattleSpirits.Networking;
using LaoDai.BattleSpirits.Presentation;
using LaoDai.BattleSpirits.Visuals;
using MDPro3.UI;
using MDPro3.UI.ServantUI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Debug=UnityEngine.Debug;
using Object=UnityEngine.Object;

internal static class Entry
{
    private sealed class MainContext:SynchronizationContext
    {
        private readonly ConcurrentQueue<Action> work=new ConcurrentQueue<Action>();
        public int Pending=>work.Count;
        public override void Post(SendOrPostCallback callback,object state)=>work.Enqueue(()=>callback(state));
        public void Drain(){while(work.TryDequeue(out var action))action();}
    }
    private sealed class FixtureNode {public string path="";public string[] components=Array.Empty<string>();public JObject[] states=Array.Empty<JObject>();public JObject rect=null;public int layer=5;public bool active=true;}
    private sealed class CountingCards:IReadOnlyList<CardDefinition>
    {
        private readonly IReadOnlyList<CardDefinition> cards;
        public int Enumerations;
        public CountingCards(IReadOnlyList<CardDefinition> cards){this.cards=cards;}
        public int Count=>cards.Count;
        public CardDefinition this[int index]=>cards[index];
        public IEnumerator<CardDefinition> GetEnumerator(){Enumerations++;return cards.GetEnumerator();}
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
    }
    private sealed class FakeSession:IBattleNativeSession,IBattleNativeReplaySession,IBattleNativePresentationSession
    {
        public IReadOnlyList<CardDefinition> Cards{get;set;}
        public IReadOnlyList<DeckDefinition> Decks{get;set;}
        public DeckDefinition SelectedDeck{get;private set;}
        public FilteredMatchView View{get;set;}
        public IReadOnlyList<LobbyPlayerView> Players{get;set;}=Array.Empty<LobbyPlayerView>();
        public string Status{get;set;}="Controlled native session";
        public int Seat{get;set;}=-1;
        public bool IsConnected{get;set;}
        public bool IsReady{get;set;}
        private bool commandsAllowed=true;public bool CanSendCommands{get=>commandsAllowed&&!IsPresentationBusy;set=>commandsAllowed=value;}
        public bool IsPresentationBusy{get;private set;}
        public readonly Dictionary<int,Rect> Bounds=new Dictionary<int,Rect>();
        public readonly Dictionary<string,Texture2D> ArtTextures=new Dictionary<string,Texture2D>();
        public bool MissingArtwork;
        public bool IsReplay{get;set;}
        public int ReplayDelta,Hosts,Joins,Starts,ReadyRequests,SoloStarts,Disconnected;
        public string HostName,JoinAddress;public int Port;
        public readonly List<string> RequestedArt=new List<string>();
        public readonly List<WireMessage> Commands=new List<WireMessage>();
        public DeckDefinition Saved;
        public event Action Changed;
        public event Action<PublicBattleEvent> PresentationEvent;
        public event Action PresentationReset;
        public void SetPresentationBusy(bool busy){if(IsPresentationBusy==busy)return;IsPresentationBusy=busy;Changed?.Invoke();}
        public bool TryGetCardViewport(int id,out Rect bounds)=>Bounds.TryGetValue(id,out bounds);
        public void Emit(PublicBattleEvent value)=>PresentationEvent?.Invoke(value);
        public FakeSession(IReadOnlyList<CardDefinition> cards){Cards=cards;Decks=StarterDecks.CreatePair(cards, BattleRulesFormat.Standard);SelectedDeck=Decks[0];}
        public void SelectDeck(DeckDefinition deck){SelectedDeck=deck;Changed?.Invoke();}
        public bool SaveDeck(DeckDefinition deck,out string error){Saved=deck;error="";return true;}
        public void StartSolo(DeckDefinition player,DeckDefinition opponent){SoloStarts++;}
        public void Host(int port,string name){Hosts++;Port=port;HostName=name;}
        public void Join(string host,int port,string name){Joins++;JoinAddress=host;Port=port;HostName=name;}
        public void SetReady(bool ready){ReadyRequests++;IsReady=ready;}
        public void StartOnline(){Starts++;}
        public void Send(WireMessage command)=>Commands.Add(command);
        public void LoadArt(string id,RawImage image){RequestedArt.Add(id);if(MissingArtwork){image.texture=Texture2D.blackTexture;return;}if(!ArtTextures.TryGetValue(id,out var texture)||texture==null){texture=new Texture2D(675,983,TextureFormat.RGBA32,false){name="Face:"+id};ArtTextures[id]=texture;}image.texture=texture;}
        public void AttachField(RawImage image){image.texture=Texture2D.blackTexture;}
        public void Disconnect(){Disconnected++;IsConnected=false;View=null;PresentationReset?.Invoke();SetPresentationBusy(false);}
        public void StepReplay(int delta)=>ReplayDelta+=delta;
        public void Refresh()=>Changed?.Invoke();
    }
    private static readonly MainContext Context=new MainContext();
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static string sourceRoot,temporary;
    private static CardsDatabase catalog;
    private static Dictionary<string,FixtureNode[]> fixtures;
    private static MDPro3.Program program;
    private static FakeSession fake;
    private static int passed;
    private static void Assert(bool value,string message){if(!value)throw new Exception(message);}
    private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Private).GetValue(owner);
    private static object Call(object owner,string name,params object[] args)
    {
        try{return owner.GetType().GetMethod(name,Private).Invoke(owner,args);}
        catch(TargetInvocationException error){throw error.InnerException;}
    }
    private static Transform Find(Transform root,string suffix)=>BattleNativeWidgets.Find(root,suffix);
    private static void Click(Transform root,string suffix)
    {
        var node=Find(root,suffix);Assert(node!=null,"Missing actual prefab button: "+suffix);
        Assert(node.gameObject.activeInHierarchy,"Button inactive: "+suffix);
        var selectable=node.GetComponent<Selectable>();
        Assert(selectable==null||selectable.interactable,"Button disabled: "+suffix);
        if(node.GetComponent<SelectionToggle>() is SelectionToggle toggle)toggle.Submit();
        else if(node.GetComponent<SelectionButton>() is SelectionButton selection)selection.Click();
        else {var button=node.GetComponent<Button>();Assert(button!=null,"Button has no event API: "+suffix);button.onClick.Invoke();}
    }
    private static void Input(Transform root,string suffix,string value)
    {
        var input=Find(root,suffix)?.GetComponent<TMP_InputField>();Assert(input!=null,"Missing actual prefab input: "+suffix);
        if(suffix==DeckSearch&&root.gameObject.activeInHierarchy)
            Assert(input.enabled&&input.interactable&&!input.readOnly&&input.targetGraphic!=null
                &&input.targetGraphic.enabled&&input.targetGraphic.raycastTarget&&input.targetGraphic.gameObject.activeInHierarchy
                &&input.textViewport!=null&&input.textComponent!=null,
                "The actual search field has no visible pointer target or serialized text references.");
        input.text=value;input.ForceLabelUpdate();input.onValueChanged.Invoke(value);
    }
    private const string DeckSearch="CardCollectionView/Root/Window/FilterAndSortArea/UpperArea/InputField";
    private const string DeckSearchButton="CardCollectionView/Root/Window/FilterAndSortArea/UpperArea/SearchButton";
    private const string DeckAdd="CardDetailView/Root/Window/Content/DescriptionArea/MenuAreaRoot/MenuArea/CardGroup/AddCardButton";
    private static Transform[] VisibleCards(RectTransform content)=>content.children.Where(child=>child.name.StartsWith("BS_Card_",StringComparison.Ordinal)&&child.gameObject.activeSelf).ToArray();
    private static string[] VisibleIds(RectTransform content)=>VisibleCards(content).Select(child=>child.name.Substring("BS_Card_".Length)).ToArray();
    private static string CardId(Transform card)=>card.name.Substring("BS_Card_".Length);
    private static int CardQuantity(Transform card)
    {
        var badge=card.Find("BS_CardQuantity");Assert(badge!=null,"An editor card has no quantity badge.");
        if(!badge.gameObject.activeSelf)return 0;
        var text=badge.GetComponentInChildren<TextMeshProUGUI>(true)?.text??string.Empty;
        Assert(int.TryParse(text.TrimStart('×','x','X',' '),out int quantity),"Quantity badge has no numerical count: "+text);
        return quantity;
    }
    private static double Luminance(Color color)
    {
        double Linear(float value)=>value<=.04045f?value/12.92:Math.Pow((value+.055)/1.055,2.4);
        return .2126*Linear(color.r)+.7152*Linear(color.g)+.0722*Linear(color.b);
    }
    private static double Contrast(Color first,Color second)
    {
        double a=Luminance(first),b=Luminance(second);return (Math.Max(a,b)+.05)/(Math.Min(a,b)+.05);
    }
    private static void SearchDeck(BattleNativeScreenUI screen,string query){Input(screen.transform,DeckSearch,query);Click(screen.transform,DeckSearchButton);}
    private static void TickDeck(BattleNativeDeckView view,float now){Time.unscaledTime=now;Call(view,"Tick",now);}
    private static Type FixtureType(string kind)=>kind switch {
        "CanvasGroup"=>typeof(CanvasGroup),"SelectionButton"=>typeof(SelectionButton),"SelectionToggle"=>typeof(SelectionToggle),
        "TMP_InputField"=>typeof(TMP_InputField),"TMP_Dropdown"=>typeof(TMP_Dropdown),"TextMeshProUGUI"=>typeof(TextMeshProUGUI),
        "RawImage"=>typeof(RawImage),"Image"=>typeof(Image),"Button"=>typeof(Button),"Toggle"=>typeof(Toggle),
        "GridLayoutGroup"=>typeof(GridLayoutGroup),"HorizontalLayoutGroup"=>typeof(HorizontalLayoutGroup),
        "VerticalLayoutGroup"=>typeof(VerticalLayoutGroup),"ContentSizeFitter"=>typeof(ContentSizeFitter),"LayoutElement"=>typeof(LayoutElement),
        "ScrollRect"=>typeof(ScrollRect),"ElementObjectManager"=>typeof(YgomSystem.ElementSystem.ElementObjectManager),_=>null};
    private static Vector2 FixtureVector(JToken value)=>value==null||value.Type==JTokenType.Null?Vector2.zero:new Vector2((float)value["x"],(float)value["y"]);
    private static T FixtureReference<T>(Dictionary<string,GameObject> objects,JToken value)where T:Component
        =>value!=null&&value.Type!=JTokenType.Null&&objects.TryGetValue((string)value,out var owner)?owner.GetComponent<T>():null;
    private static void ApplyFixtureState(GameObject go,JObject state,Dictionary<string,GameObject> objects)
    {
        var type=FixtureType((string)state["type"]);if(type==null)return;
        var component=go.GetComponents<Component>().FirstOrDefault(type.IsInstanceOfType);if(component==null)return;
        if(component is Behaviour behaviour&&state["enabled"]!=null)behaviour.enabled=(float)state["enabled"]!=0;
        if(component is Graphic graphic&&state["raycastTarget"]!=null)graphic.raycastTarget=(float)state["raycastTarget"]!=0;
        if(component is Selectable selectable&&state["interactable"]!=null)selectable.interactable=(float)state["interactable"]!=0;
        if(component is CanvasGroup group)
        {
            if(state["alpha"]!=null)group.alpha=(float)state["alpha"];
            if(state["interactable"]!=null)group.interactable=(float)state["interactable"]!=0;
            if(state["blocksRaycasts"]!=null)group.blocksRaycasts=(float)state["blocksRaycasts"]!=0;
            if(state["ignoreParentGroups"]!=null)group.ignoreParentGroups=(float)state["ignoreParentGroups"]!=0;
        }
        if(component is TMP_InputField input)
        {
            input.textViewport=FixtureReference<RectTransform>(objects,state["TextViewport"]);
            input.textComponent=FixtureReference<TextMeshProUGUI>(objects,state["TextComponent"]);
            input.placeholder=FixtureReference<Graphic>(objects,state["Placeholder"]);
            input.targetGraphic=FixtureReference<Graphic>(objects,state["TargetGraphic"]);
            if(state["readOnly"]!=null)input.readOnly=(float)state["readOnly"]!=0;
        }
        if(component is TextMeshProUGUI label)
        {
            if(state["text"]!=null)label.text=(string)state["text"];
            if(state["fontSize"]!=null)label.fontSize=(int)(float)state["fontSize"];
        }
        if(component is GridLayoutGroup grid)
        {
            if(state["constraintCount"]!=null)grid.constraintCount=(int)(float)state["constraintCount"];
            if(state["cellSize"]!=null)grid.cellSize=FixtureVector(state["cellSize"]);
            if(state["spacing"]!=null)grid.spacing=FixtureVector(state["spacing"]);
        }
        if(component is HorizontalLayoutGroup horizontal)
        {
            if(state["spacingScalar"]!=null)horizontal.spacing=(float)state["spacingScalar"];
            if(state["childControlWidth"]!=null)horizontal.childControlWidth=(float)state["childControlWidth"]!=0;
            if(state["childControlHeight"]!=null)horizontal.childControlHeight=(float)state["childControlHeight"]!=0;
            if(state["childForceExpandWidth"]!=null)horizontal.childForceExpandWidth=(float)state["childForceExpandWidth"]!=0;
            if(state["childForceExpandHeight"]!=null)horizontal.childForceExpandHeight=(float)state["childForceExpandHeight"]!=0;
        }
        if(component is ContentSizeFitter fitter)
        {
            if(state["horizontalFit"]!=null)fitter.horizontalFit=(ContentSizeFitter.FitMode)(int)(float)state["horizontalFit"];
            if(state["verticalFit"]!=null)fitter.verticalFit=(ContentSizeFitter.FitMode)(int)(float)state["verticalFit"];
        }
        if(component is LayoutElement element)
        {
            if(state["minWidth"]!=null)element.minWidth=(float)state["minWidth"];
            if(state["minHeight"]!=null)element.minHeight=(float)state["minHeight"];
            if(state["preferredWidth"]!=null)element.preferredWidth=(float)state["preferredWidth"];
            if(state["preferredHeight"]!=null)element.preferredHeight=(float)state["preferredHeight"];
            if(state["flexibleWidth"]!=null)element.flexibleWidth=(float)state["flexibleWidth"];
            if(state["flexibleHeight"]!=null)element.flexibleHeight=(float)state["flexibleHeight"];
        }
        if(component is ScrollRect scroll)
        {
            scroll.viewport=FixtureReference<RectTransform>(objects,state["Viewport"]);
            scroll.content=FixtureReference<RectTransform>(objects,state["Content"]);
        }
    }
    private static GameObject Fixture(string name)
    {
        var objects=new Dictionary<string,GameObject>(StringComparer.Ordinal);
        foreach(var row in fixtures[name].OrderBy(row=>row.path.Count(c=>c=='/')))
        {
            if(objects.ContainsKey(row.path))continue;
            var go=new GameObject(row.path.Split('/').Last(),typeof(RectTransform)){layer=row.layer};objects.Add(row.path,go);
            int slash=row.path.LastIndexOf('/');if(slash>=0)go.transform.SetParent(objects[row.path.Substring(0,slash)].transform,false);
            foreach(string kind in row.components??Array.Empty<string>())
            {
                Type type=FixtureType(kind);
                if(type!=null)go.AddComponent(type);
            }
            if(row.rect!=null)
            {
                var rect=(RectTransform)go.transform;
                rect.anchorMin=FixtureVector(row.rect["AnchorMin"]);rect.anchorMax=FixtureVector(row.rect["AnchorMax"]);
                rect.anchoredPosition=FixtureVector(row.rect["AnchoredPosition"]);rect.sizeDelta=FixtureVector(row.rect["SizeDelta"]);
                rect.pivot=FixtureVector(row.rect["Pivot"]);
                if(row.rect["LocalScale"] is JToken scale)rect.localScale=new Vector3((float)scale["x"],(float)scale["y"],(float)scale["z"]);
            }
            if(go.GetComponent<Image>() is Image image)image.sprite=new Sprite();
            go.SetActive(row.active);
        }
        foreach(var row in fixtures[name])
            foreach(var state in row.states??Array.Empty<JObject>())ApplyFixtureState(objects[row.path],state,objects);
        var result=objects.Values.First(go=>go.transform.parent==null);
        result.SetActive(true);return result;
    }
    private static BattleNativeScreenUI Screen(BattleNativeScreen screen,IBattleNativeSession session=null)
    {
        BattleNativeNavigation.Session=session??fake;
        var go=Fixture(BattleNativeNavigation.PrefabName(screen));go.transform.SetParent(program.ui_.transform,false);
        var controller=go.AddComponent<BattleNativeScreenUI>();controller.screen=screen;
        controller.Initialize(new GameObject("ControlledServant").AddComponent<MDPro3.Servant.Servant>());
        return controller;
    }
    private static void Cleanup()
    {
        Context.Drain();
        foreach(var root in GameObject.All.Where(go=>!go.destroyed&&go.transform.parent==null).ToArray())Object.Destroy(root);
        Context.Drain();BattleNativeNavigation.Session=null;MDPro3.Program.instance=null;MDPro3.UIManager.Popup=null;MDPro3.UIManager.PopupSelected=null;
    }
    private static void Setup()
    {
        Cleanup();Time.unscaledTime=0;MDPro3.Config.Values.Clear();
        UnityEngine.EventSystems.EventSystem.current=new UnityEngine.EventSystems.EventSystem();
        Application.dataPath=Path.Combine(temporary,"Assets");
        var canvas=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas)){layer=5};
        canvas.GetComponent<Canvas>().worldCamera=new GameObject("Camera_UI").AddComponent<Camera>();
        canvas.GetComponent<Canvas>().worldCamera.cullingMask=1073741856;
        program=new GameObject("Program").AddComponent<MDPro3.Program>();MDPro3.Program.instance=program;
        program.ui_=new GameObject("UI",typeof(RectTransform)).AddComponent<MDPro3.UIManager>();program.ui_.gameObject.layer=5;
        program.ui_.transform.SetParent(canvas.transform,false);program.ui_.tmpFont=new TMP_FontAsset();
        program.menu=new GameObject("Menu").AddComponent<MDPro3.Servant.Servant>();program.currentServant=program.menu;
        program.UiFactory=servant=>{
            if (!(servant is BattleNativeServant native)) return null;
            var view=Screen(native.Screen,BattleNativeNavigation.Session);servant.servantUI=view;return view;
        };
        MDPro3.Program.items=new MDPro3.Items();
        fake=new FakeSession(catalog.cards);BattleNativeNavigation.Session=fake;
        BattleAudio.Configure(temporary);
    }
    private static void TickPresentation(float delta)
    {
        Time.unscaledTime+=delta;Time.unscaledDeltaTime=delta;
        foreach(var component in GameObject.All.Where(go=>!go.destroyed&&go.activeInHierarchy).SelectMany(go=>go.GetComponents<BattleSummonPresentation>()).ToArray()) component.Tick(delta);
    }
    private static void Case(string name,Action test){Setup();test();Context.Drain();passed++;Console.WriteLine("PASS "+name);}
    private sealed class TitleFixture
    {
        public Canvas Canvas;
        public Image Backdrop;
        public Text Status;
        public Slider Progress;
        public Font Font;
        public BattleTitleScreen View;
    }
    private static TitleFixture Title()
    {
        var fixture=new TitleFixture();
        var canvas=new GameObject("BootCanvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster)){layer=5};
        fixture.Canvas=canvas.GetComponent<Canvas>();
        var backdrop=new GameObject("Backdrop",typeof(RectTransform),typeof(Image)){layer=5};
        backdrop.transform.SetParent(canvas.transform,false);fixture.Backdrop=backdrop.GetComponent<Image>();
        fixture.Backdrop.sprite=new Sprite();
        var status=new GameObject("LoadingStatus",typeof(RectTransform),typeof(Text)){layer=5};
        status.transform.SetParent(canvas.transform,false);fixture.Status=status.GetComponent<Text>();
        fixture.Font=new Font{name="Boot Vietnamese font"};fixture.Status.font=fixture.Font;fixture.Status.text="Đang tải dữ liệu";
        var progress=new GameObject("LoadingProgress",typeof(RectTransform),typeof(Slider)){layer=5};
        progress.transform.SetParent(canvas.transform,false);fixture.Progress=progress.GetComponent<Slider>();
        foreach(string name in new[]{"Background","Fill","Handle"})
        {
            var graphic=new GameObject(name,typeof(RectTransform),typeof(Image)){layer=5};graphic.transform.SetParent(progress.transform,false);
            if(name=="Fill")fixture.Progress.fillRect=graphic.GetComponent<RectTransform>();
            if(name=="Handle")fixture.Progress.handleRect=graphic.GetComponent<RectTransform>();
        }
        fixture.View=BattleTitleScreen.Create(fixture.Canvas,fixture.Status,fixture.Progress);
        return fixture;
    }
    private static void Pump(Func<bool> done,Action tick=null)
    {
        var watch=Stopwatch.StartNew();while(!done()&&watch.Elapsed.TotalSeconds<10){Context.Drain();tick?.Invoke();Thread.Sleep(2);}Context.Drain();Assert(done(),"Timed out waiting for actual production continuation.");
    }
    private static void InstallWallpaperFixture()
    {
        string target=Path.Combine(temporary,"Picture/Art/bs-menu.png");
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        File.Copy(Path.Combine(sourceRoot,"Picture/Art/bs-menu.png"),target,true);
    }
    private static Task<bool> PendingWallpaper(BattleMenuWallpaper wallpaper,RawImage image,Func<bool> canAssign=null)
    {
        // Keep the real file-read continuation queued on the captured main context,
        // so destruction and replacement races do not depend on Thread.Sleep timing.
        for(int attempt=0;attempt<20;attempt++)
        {
            Task<bool> loading=wallpaper.LoadAsync(image,canAssign);
            if(loading.IsCompleted){loading.GetAwaiter().GetResult();continue;}
            var watch=Stopwatch.StartNew();
            while(Context.Pending==0&&!loading.IsCompleted&&watch.Elapsed.TotalSeconds<10)Thread.Sleep(2);
            Assert(Context.Pending>0&&!loading.IsCompleted,"Could not hold the actual wallpaper continuation for the lifecycle fixture.");
            return loading;
        }
        throw new Exception("All wallpaper reads completed synchronously; the deferred-read fixture could not be established.");
    }
    private static bool FinishWallpaper(Task<bool> loading)
    {
        Pump(()=>loading.IsCompleted);
        return loading.GetAwaiter().GetResult();
    }
    private static void AssertGeneratedLayers(Transform root)
    {
        foreach(var item in root.GetComponentsInChildren<Transform>(true).Where(item=>item.name.StartsWith("BS_",StringComparison.Ordinal)))
            Assert(item.gameObject.layer==5,"Generated widget not visible to actual UI camera: "+item.name);
    }
    private static MainMenuUI Menu()
    {
        var go=Fixture("MainMenuUI");go.transform.SetParent(program.ui_.transform,false);
        var menu=go.AddComponent<MainMenuUI>();menu.Manager=go.GetComponent<YgomSystem.ElementSystem.ElementObjectManager>()??go.AddComponent<YgomSystem.ElementSystem.ElementObjectManager>();
        menu.Title=new GameObject("ControlledTitle",typeof(RectTransform)).AddComponent<TextMeshProUGUI>();menu.Initialize(program.menu);return menu;
    }
    private static void LoadFixtures()
    {
        var start=new ProcessStartInfo("python"){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        start.ArgumentList.Add(Path.Combine(sourceRoot,"Tests/BattleSpirits/Presentation/prefab_fixture.py"));start.ArgumentList.Add(sourceRoot);
        using var child=Process.Start(start);string output=child.StandardOutput.ReadToEnd(),error=child.StandardError.ReadToEnd();child.WaitForExit();
        Assert(child.ExitCode==0,"Prefab fixture export failed: "+error);fixtures=JsonConvert.DeserializeObject<Dictionary<string,FixtureNode[]>>(output);
    }
    private sealed class FixtureAction{public int actor;public BattleCommand command;}
    private static BattleMatch SummonedFixture(out int seed,out DeckDefinition[] decks,out List<FixtureAction> actions)
    {
        decks=StarterDecks.CreatePair(catalog.cards,BattleRulesFormat.Standard);
        var definitions=catalog.cards.ToDictionary(card=>card.id,StringComparer.Ordinal);
        for(seed=1;seed<=100;seed++)
        {
            var match=BattleMatch.Create(catalog.cards,decks[0].cards,decks[1].cards,seed,format:BattleRulesFormat.Standard);
            actions=new List<FixtureAction>{new FixtureAction{actor=0,command=new BattleCommand{action="opening"}},new FixtureAction{actor=1,command=new BattleCommand{action="opening"}}};
            foreach(var action in actions)Assert(BattleCommandDispatcher.TryExecute(match,action.actor,action.command,out _),"Deterministic fixture opening failed.");
            var player=match.State.Players[0];int available=player.Reserve.Normal+(player.Reserve.Soul?1:0);
            int index=player.Hand.FindIndex(id=>definitions[id].IsSpirit&&definitions[id].cost+definitions[id].levels[0].cores<=available);
            if(index<0)continue;
            var summon=new FixtureAction{actor=0,command=new BattleCommand{action="summon",index=index}};
            Assert(BattleCommandDispatcher.TryExecute(match,0,summon.command,out string error),"Legal fixture summon failed: "+error);actions.Add(summon);return match;
        }
        throw new Exception("No legal deterministic summon fixture could be created.");
    }
    public static int Main(string[] args)
    {
        SynchronizationContext.SetSynchronizationContext(Context);
        sourceRoot=Path.GetFullPath(args.Length>0?args[0]:".");Resources.ProjectRoot=sourceRoot;LoadFixtures();
        catalog=JsonConvert.DeserializeObject<CardsDatabase>(File.ReadAllText(Path.Combine(sourceRoot,"Data/BattleSpirits/cards.json")));
        temporary=Path.Combine(Path.GetTempPath(),"bs-native-presentation-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(temporary,"Assets"));Directory.CreateDirectory(Path.Combine(temporary,"Data/BattleSpirits"));
        File.WriteAllText(Path.Combine(temporary,"Data/BattleSpirits/cards.json"),JsonConvert.SerializeObject(catalog));
        Directory.CreateDirectory(Path.Combine(temporary,"Sound/BattleSpirits"));
        foreach(string wave in Directory.GetFiles(Path.Combine(sourceRoot,"Sound/BattleSpirits"),"*.wav"))File.Copy(wave,Path.Combine(temporary,"Sound/BattleSpirits",Path.GetFileName(wave)));
        foreach(var card in catalog.cards.Where(card=>card.IsPlayableFor(BattleRulesFormat.Standard)))
        {
            string target=Path.Combine(temporary,card.artPath);Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(Path.Combine(sourceRoot,card.artPath),target,true);
        }
        try
        {
            Case("Skin resources resolve the reviewed PNG and JPEG files with their original dimensions",()=>{
                var manifest=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(sourceRoot,"Data/BattleSpirits/skin-sources.json")));
                int count=0;
                foreach(var entry in manifest["assets"])
                {
                    string path=(string)entry["resourcePath"];var resource=Resources.Load<Texture2D>(path);
                    Assert(resource!=null,"A reviewed skin resource cannot be loaded: "+path);
                    Assert(resource.width==(int)entry["width"]&&resource.height==(int)entry["height"],"Resource dimensions differ from the original file: "+path);
                    string original=Path.GetFullPath(Path.Combine(sourceRoot,(string)entry["path"]));
                    Assert(resource.sourcePath==original,"Resource resolved to an unrelated or fabricated image: "+path);
                    using var sha=SHA256.Create();string hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(original))).Replace("-","").ToLowerInvariant();
                    Assert(hash==(string)entry["sha256"],"Imported skin bytes differ from reviewed source: "+path);count++;
                }
                Assert(count==14&&Resources.Load<Texture2D>("BattleSpirits/Skin/NotInstalled")==null,"Missing resources were fabricated or the reviewed bank is incomplete.");
            });
            Case("Generated Battle Spirits buttons keep original bevels and lime selection colors",()=>{
                var parent=new GameObject("ButtonHost",typeof(RectTransform));var button=BattleNativeWidgets.Button(parent.transform,"TestButton","BỘ BÀI",()=>{});var face=button.GetComponent<Image>();
                Assert(face.sprite!=null&&face.sprite.texture==Resources.Load<Texture2D>("BattleSpirits/Skin/ChromeButton"),"Generated button did not use the original template chrome.");
                Assert(face.sprite.border.x==22&&face.sprite.border.y==22&&face.type==Image.Type.Sliced,"Original border was stretched instead of nine-sliced.");
                Assert(button.transition==Selectable.Transition.ColorTint&&Math.Abs(button.colors.highlightedColor.r-0.7529412f)<0.000001f&&button.colors.highlightedColor.g==1&&button.colors.highlightedColor.b==0,"Template lime selection palette was not restored.");
                Assert(face.color.r==1&&face.color.g==1&&face.color.b==1&&button.colors.normalColor.r==1,"Original button artwork is still tinted dark navy.");
                Assert(Math.Abs(BattleTheme.PanelBackground.r-0.15294118f)<0.000001f&&Math.Abs(BattleTheme.Red.r-0.6117647f)<0.000001f,"Template panel or duel-red palette changed.");
            });
            Case("Native navigation registers original prefab Servants without another Canvas",()=>{
                BattleNativeNavigation.Open(BattleNativeScreen.Solo);
                var first=program.currentServant;Assert(first is BattleNativeServant&&program.Registered.Count==1,"Native servant was not registered.");
                Assert(first.servantUI is BattleNativeScreenUI,"Original prefab did not receive native controller.");
                BattleNativeNavigation.Open(BattleNativeScreen.Solo);Assert(program.Registered.Count==1,"Duplicate native screen registration.");
                Assert(GameObject.All.Count(go=>!go.destroyed&&go.GetComponent<Canvas>()!=null)==1,"Separate overlay Canvas created.");
                BattleNativeNavigation.Open(BattleNativeScreen.Deck);Assert(!first.servantUI.gameObject.activeSelf,"Old form did not hide on navigation.");
            });
            Case("Native deck uses original collection, deck and detail form with Battle Spirits images",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");
                var collection=Field<RectTransform>(view,"collection");var deck=Field<RectTransform>(view,"deckContent");
                Assert(collection!=null&&deck!=null,"Original nested deck form paths not resolved.");
                Assert(collection.children.Count(child=>child.name.StartsWith("BS_Card_"))==42,"Collection first page missing.");
                Assert(VisibleIds(deck).SequenceEqual(fake.SelectedDeck.cards.Distinct(StringComparer.Ordinal))
                    &&VisibleCards(deck).Sum(CardQuantity)==fake.SelectedDeck.cards.Length,"Grouped deck cards or their logical quantities were not rebound.");
                Assert(fake.RequestedArt.All(id=>catalog.cards.Any(card=>card.id==id)),"Legacy YGO card images requested.");AssertGeneratedLayers(screen.transform);
            });
            Case("Native search filters actual catalog by card ID, type, cost and BP",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");var grid=Field<RectTransform>(view,"collection");
                foreach(string query in new[]{"BS01-X01","Magic","cost:4","bp:10000"})
                {
                    SearchDeck(screen,query);
                    var cards=grid.children.Where(child=>child.name.StartsWith("BS_Card_")).ToArray();Assert(cards.Length>0,"Search empty unexpectedly: "+query);
                    foreach(var node in cards){string id=node.name.Substring("BS_Card_".Length);var card=catalog.cards.Single(card=>card.id==id);
                        Assert(query=="Magic"?card.type=="Magic":query=="cost:4"?card.cost==4:query=="bp:10000"?card.levels.Any(level=>level.bp>=10000):id==query,"Native BS search returned incompatible card.");}
                }
            });
            Case("Real card loading preserves original resolution and fits hand artwork without stretching",()=>{
                const string id="26RBS02-002";
                var session=BattleNativeSession.Ensure();var host=new GameObject("FullResolutionHand",typeof(RectTransform));
                var button=BattleNativeWidgets.Card(host.transform,id,()=>{},session);
                var image=button.GetComponentsInChildren<RawImage>(true).Single();var fitter=image.GetComponent<AspectRatioFitter>();
                Assert(image.transform.parent.name=="BS_CardArtworkSlot"&&fitter!=null&&fitter.aspectMode==AspectRatioFitter.AspectMode.FitInParent,"Hand card still fills a wide button rectangle instead of preserving its face ratio.");
                Pump(()=>image.texture!=null&&image.texture!=Texture2D.blackTexture);
                var texture=(Texture2D)image.texture;
                Assert(texture.width==813&&texture.height==1184,"Runtime artwork was reduced below the verified original PNG dimensions.");
                Assert(texture.filterMode==FilterMode.Bilinear&&texture.wrapMode==TextureWrapMode.Clamp&&texture.anisoLevel==8,"Artwork filtering can pixelate the source or bleed neighboring edges.");
                Assert(Math.Abs(fitter.aspectRatio-813f/1184)<0.000001f,"The fitted hand image uses a guessed or stretched aspect rather than the loaded original.");
                Assert(button.targetGraphic==image&&image.raycastTarget,"Preserving the face ratio removed card click input.");
            });
            Case("Cached artwork refreshes full-face and illustration aspect ratios from the same original texture",()=>{
                const string id="26RBS02-002";var session=BattleNativeSession.Ensure();
                var first=new GameObject("WarmArtwork",typeof(RectTransform)).AddComponent<RawImage>();session.LoadArt(id,first);
                Pump(()=>first.texture!=null&&first.texture!=Texture2D.blackTexture);
                var cached=new GameObject("CachedCrop",typeof(RectTransform)).AddComponent<RawImage>();cached.uvRect=new Rect(0.13f,0.51f,0.79f,0.44f);
                var cropFit=cached.gameObject.AddComponent<AspectRatioFitter>();cropFit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;cropFit.aspectRatio=1;
                session.LoadArt(id,cached);
                Assert(cached.texture==first.texture&&Math.Abs(cropFit.aspectRatio-813f*0.79f/(1184*0.44f))<0.000001f,"Cache reuse kept a stale full-card aspect for the cropped illustration.");
                var fullHost=new GameObject("CachedHand",typeof(RectTransform));var button=BattleNativeWidgets.Card(fullHost.transform,id,()=>{},session);
                var full=button.GetComponentsInChildren<RawImage>(true).Single();
                Assert(full.texture==first.texture&&Math.Abs(full.GetComponent<AspectRatioFitter>().aspectRatio-813f/1184)<0.000001f,"An immediate cache hit arrives before hand layout knows the original dimensions.");
            });
            Case("Deck details and filters show the matching Battle Spirits color symbol and Standard labels",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");
                var card=catalog.cards.First(value=>value.color==CardColor.Blue);Call(view,"Select",card);
                var detail=Field<RawImage>(view,"detailColorSymbol");Assert(detail!=null&&detail.gameObject.activeInHierarchy&&detail.texture==BattleTheme.ColorIcon(CardColor.Blue),"Card detail retained a Yu-Gi-Oh attribute or another color's icon.");
                var filter=Field<RawImage>(view,"filterColorSymbol");Assert(filter!=null&&!filter.gameObject.activeSelf,"All-colors filter shows one misleading color.");
                Click(screen.transform,"CardCollectionView/Root/Window/FilterAndSortArea/BottomArea/FilterToggle");
                Assert(filter.gameObject.activeInHierarchy&&filter.texture==BattleTheme.ColorIcon(CardColor.Red),"Red filter does not show the red symbol.");
                Click(screen.transform,"CardCollectionView/Root/Window/FilterAndSortArea/BottomArea/ClearButton");Assert(!filter.gameObject.activeSelf,"Clearing a color filter leaves its icon behind.");
                foreach(string path in new[]{"DeckEditHeader/Root/Header/ButtonRegulation","CardCollectionView/Root/Window/TabArea/HistotyToggle"})
                    Assert(Find(screen.transform,path).GetComponentsInChildren<TextMeshProUGUI>(true).Any(label=>label.text=="STANDARD"),"Active Standard filter is mislabeled Eternal.");
            });
            Case("Prefab fixture retains the actual disabled grid and unfocusable search preconditions",()=>{
                var raw=Fixture("DeckEditorUI");var field=Find(raw.transform,DeckSearch).GetComponent<TMP_InputField>();
                Assert(field.targetGraphic==null&&field.textComponent!=null&&field.textViewport!=null&&field.placeholder!=null,
                    "Fixture lost the serialized TMP references or silently invented a pointer target.");
                Assert(!field.transform.Find("Out").GetComponent<Image>().raycastTarget,"Fixture discarded the source background's disabled raycast flag.");
                var content=Find(raw.transform,"CardCollectionView/Root/Window/CardListArea/CollectionAreaCenter/ScrollRect/Viewport/Content");
                Assert(content.GetComponent<GridLayoutGroup>()!=null&&!content.GetComponent<GridLayoutGroup>().enabled,
                    "Fixture replaced the actual disabled GridLayoutGroup with an enabled default.");
                Assert(Find(raw.transform,"CardCollectionView/Root/Window/CardListArea/CollectionAreaCenter/#Text_NoItemMessageTMP").gameObject.activeSelf,
                    "Fixture omitted the independent serialized empty-results label.");
            });
            Case("Native search repairs its hit surface while retaining the original text and viewport",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var field=Find(screen.transform,DeckSearch).GetComponent<TMP_InputField>();
                var surface=field.targetGraphic;
                Assert(surface is Image&&surface.transform==field.transform.Find("BS_InputSurface")&&surface.enabled&&surface.raycastTarget&&surface.color.a>0,
                    "The repaired input has no owned surface for a GraphicRaycaster.");
                Assert(field.textViewport==field.transform.Find("Mask")&&field.textComponent.transform.parent==field.textViewport
                    &&field.placeholder.transform.parent==field.textViewport,"Search repair broke the serialized viewport/text hierarchy.");
                Assert(!field.textComponent.raycastTarget&&!field.placeholder.raycastTarget,"Search text or placeholder competes with its focus surface.");
                SearchDeck(screen,"cost:4");var view=Field<BattleNativeDeckView>(screen,"deck");
                Assert(VisibleIds(Field<RectTransform>(view,"collection")).All(id=>catalog.cards.Single(card=>card.id==id).cost==4),"A repaired search did not filter the actual catalog.");
            });
            Case("Existing disabled grid and fitter are enabled and linked to a vertical viewport",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");
                foreach(string key in new[]{"collection","deckContent"}){
                    var content=Field<RectTransform>(view,key);var grid=content.GetComponent<GridLayoutGroup>();var fit=content.GetComponent<ContentSizeFitter>();
                    Assert(grid.enabled&&fit.enabled&&fit.horizontalFit==ContentSizeFitter.FitMode.Unconstrained&&fit.verticalFit==ContentSizeFitter.FitMode.PreferredSize,
                        "The actual reused grid/fitter stayed disabled or tried to drive the content width.");
                    Assert(content.anchorMin.x==0&&content.anchorMax.x==1&&content.pivot.y==1&&content.sizeDelta.x==0,"Grid content is still a centered zero-width virtual-list rect.");
                }
                var scroll=Find(screen.transform,"CardCollectionView/Root/Window/CardListArea/CollectionAreaCenter/ScrollRect").GetComponent<ScrollRect>();
                Assert(scroll.enabled&&scroll.vertical&&!scroll.horizontal&&scroll.content==Field<RectTransform>(view,"collection")&&scroll.viewport.GetComponent<RectMask2D>()!=null,
                    "Collection scrolling or clipping is not connected to its actual card content.");
            });
            Case("Empty-results label follows actual search results instead of covering a populated grid",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");
                var empty=Find(screen.transform,"CardCollectionView/Root/Window/CardListArea/CollectionAreaCenter/#Text_NoItemMessageTMP");
                Assert(!empty.gameObject.activeSelf,"Empty label covers the initial populated collection.");
                SearchDeck(screen,"__no_card_can_match_this__");
                Assert(empty.gameObject.activeInHierarchy&&VisibleIds(Field<RectTransform>(view,"collection")).Length==0,"No-result search left old cards or hid its feedback.");
                Click(screen.transform,"CardCollectionView/Root/Window/FilterAndSortArea/BottomArea/ClearButton");
                Assert(!empty.gameObject.activeSelf&&VisibleCards(Field<RectTransform>(view,"collection")).Length==42,"Clearing search did not restore cards and dismiss the empty label.");
            });
            Case("Submitting the actual TMP field applies a pending query immediately",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");
                Input(screen.transform,DeckSearch,"bp:6000");
                var field=Find(screen.transform,DeckSearch).GetComponent<TMP_InputField>();field.onSubmit.Invoke(field.text);
                var ids=VisibleIds(Field<RectTransform>(view,"collection"));
                Assert(ids.Length>0&&ids.All(id=>catalog.cards.Single(card=>card.id==id).levels.Any(level=>level.bp>=6000)),"Enter/Submit left the pending search unapplied.");
                int requests=fake.RequestedArt.Count;TickDeck(view,1);Assert(fake.RequestedArt.Count==requests,"Submit caused a duplicate delayed render.");
            });
            Case("Three existing panels stretch across normal, wide and four-three windows with fitting grids",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");
                var area=(RectTransform)Find(screen.transform,"SafeArea");
                foreach(float width in new[]{1920f,2680f,1440f}){
                    area.rect=new Rect(0,0,width,1080);TickDeck(view,0);
                    var detail=(RectTransform)Find(screen.transform,"CardDetailView/Root/Window");
                    var middle=(RectTransform)Find(screen.transform,"DeckView/Root/Window");
                    var right=(RectTransform)Find(screen.transform,"CardCollectionView/Root/Window");
                    Assert(detail.anchorMin.x<.02f&&right.anchorMax.x>.98f&&detail.anchorMax.x<middle.anchorMin.x&&middle.anchorMax.x<right.anchorMin.x,
                        "Panel anchors overlap or leave the original centered 1920-wide dead space.");
                    foreach(string panel in new[]{"CardDetailView","DeckView","CardCollectionView"}){
                        var frame=(RectTransform)Find(screen.transform,panel);Assert(frame.anchorMin.x==0&&frame.anchorMax.x==1&&frame.sizeDelta.x==0,"An outer panel container remains fixed-width.");
                    }
                    var grid=Field<RectTransform>(view,"collection").GetComponent<GridLayoutGroup>();
                    float occupied=grid.cellSize.x*grid.constraintCount+grid.spacing.x*(grid.constraintCount-1)+grid.padding.left+grid.padding.right;
                    Assert(grid.constraintCount>=3&&grid.constraintCount<=6&&grid.cellSize.y>grid.cellSize.x&&occupied<=width*.346f*.97f+.01f,
                        "The active collection grid spills out of its responsive viewport.");
                }
            });
            Case("Deck preview and scroll height replace the old tiny portrait and fixed outer content",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");
                var card=(RectTransform)Find(screen.transform,"CardDetailView/Root/Window/Content/CardArea");
                Assert(card.anchorMax.y-card.anchorMin.y>.5f&&card.sizeDelta.y==0,"Selected artwork still uses the original 143-by-208 corner slot.");
                var image=card.Find("ImageCard").GetComponent<RawImage>();Assert(image.GetComponent<AspectRatioFitter>().aspectMode==AspectRatioFitter.AspectMode.FitInParent,"The enlarged portrait is stretched.");
                var outer=(RectTransform)Find(screen.transform,"DeckView/Root/Window/Scroll View/Viewport/Content");
                var grid=Field<RectTransform>(view,"deckContent").GetComponent<GridLayoutGroup>();int count=VisibleCards(Field<RectTransform>(view,"deckContent")).Length;
                Assert(outer.sizeDelta.y>=44+((count+grid.constraintCount-1)/grid.constraintCount)*grid.cellSize.y,"The deck ScrollRect content cannot reach its final card rows.");
            });
            Case("Deck chrome uses readable captions and hides unused legacy appearance controls",()=>{
                var screen=Screen(BattleNativeScreen.Deck);
                Assert(!Find(screen.transform,"DeckEditOverHeader").gameObject.activeSelf&&!Find(screen.transform,"DeckEditHeader/Root/Header/ButtonInfoSwitching").gameObject.activeSelf,
                    "Unused six-icon appearance row or the old 100-point control remains visible.");
                Assert(Find(screen.transform,"BS_DeckTitle").GetComponent<TextMeshProUGUI>().text=="BỘ BÀI","Deck header has no readable title.");
                foreach(string button in new[]{"CardListToggle","BookmarkToggle","HistotyToggle"}){
                    var node=Find(screen.transform,"CardCollectionView/Root/Window/TabArea/"+button);
                    Assert(node.Find("BS_Caption").GetComponent<TextMeshProUGUI>().enabled&&node.GetComponent<Button>().targetGraphic.raycastTarget,"A tab is still an icon-only or untargetable control.");
                }
                Click(screen.transform,"CardCollectionView/Root/Window/TabArea/BookmarkToggle");
                Assert(Find(screen.transform,"CardCollectionView/Root/Window/TabArea/BookmarkToggle/ImageOn").gameObject.activeSelf
                    &&!Find(screen.transform,"CardCollectionView/Root/Window/TabArea/CardListToggle/ImageOn").gameObject.activeSelf,"Selected-tab chrome still displays the all-cards state.");
            });
            Case("Native collection paging rebinds visible card identities",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");var grid=Field<RectTransform>(view,"collection");
                string first=grid.children.First(child=>child.name.StartsWith("BS_Card_")).name;Click(screen.transform,"BS_Next");
                Assert(first!=grid.children.First(child=>child.name.StartsWith("BS_Card_")).name,"Next page reused first page.");AssertGeneratedLayers(screen.transform);
            });
            Case("Repeated collection refreshes reuse cached results and bound artwork",()=>{
                var measured=new CountingCards(catalog.cards);fake.Cards=measured;
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");var grid=Field<RectTransform>(view,"collection");
                var before=VisibleCards(grid);int scans=measured.Enumerations,requests=fake.RequestedArt.Count,objects=GameObject.All.Count;
                for(int repeat=0;repeat<8;repeat++)Call(view,"RenderCollection");
                Assert(measured.Enumerations==scans,"Unchanged collection criteria rescanned the entire catalog.");
                Assert(before.SequenceEqual(VisibleCards(grid))&&GameObject.All.Count==objects,"Unchanged collection criteria allocated replacement card widgets.");
                Assert(fake.RequestedArt.Count==requests,"Unchanged visible cards requested their artwork again.");
            });
            Case("Rapid search input is coalesced until its quiet interval",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");var grid=Field<RectTransform>(view,"collection");
                var initial=VisibleIds(grid);int requests=fake.RequestedArt.Count;
                foreach(string query in new[]{"M","Ma","Mag","Magic"})Input(screen.transform,DeckSearch,query);
                Assert(initial.SequenceEqual(VisibleIds(grid))&&fake.RequestedArt.Count==requests,"Typing rebuilt a collection for every intermediate search value.");
                TickDeck(view,0.149f);Assert(initial.SequenceEqual(VisibleIds(grid)),"The search quiet interval was not respected.");
                TickDeck(view,0.151f);
                Assert(VisibleIds(grid).Length>0&&VisibleIds(grid).All(id=>catalog.cards.Single(card=>card.id==id).type=="Magic"),"The final search value was not applied after the quiet interval.");
            });
            Case("Explicit search flushes a pending value without waiting for debounce",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");var grid=Field<RectTransform>(view,"collection");
                SearchDeck(screen,"cost:4");
                Assert(VisibleIds(grid).Length>0&&VisibleIds(grid).All(id=>catalog.cards.Single(card=>card.id==id).cost==4),"The original Search button did not apply its current query immediately.");
                int requests=fake.RequestedArt.Count;TickDeck(view,1);
                Assert(fake.RequestedArt.Count==requests,"A flushed pending query ran twice.");
            });
            Case("Repeated paging and filtering keep the collection pool bounded",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");var grid=Field<RectTransform>(view,"collection");
                var original=VisibleCards(grid);string[] initial=VisibleIds(grid);int objects=GameObject.All.Count;
                for(int repeat=0;repeat<12;repeat++){Click(screen.transform,"BS_Next");Click(screen.transform,"BS_Previous");}
                Assert(initial.SequenceEqual(VisibleIds(grid))&&original.All(node=>VisibleCards(grid).Contains(node)),"Returning to the first page lost the reusable collection widgets.");
                Assert(GameObject.All.Count==objects,"Paging allocated new widget hierarchies instead of rebinding the existing page.");
                SearchDeck(screen,catalog.cards[0].id);Assert(VisibleCards(grid).Length==1,"A one-card search left pooled cards visible.");
                Click(screen.transform,"CardCollectionView/Root/Window/FilterAndSortArea/BottomArea/ClearButton");
                Assert(VisibleCards(grid).Length==42&&GameObject.All.Count==objects,"Restoring the full collection grew the page pool.");
            });
            Case("Collection tabs reset later pages and pooled click handlers follow the current card",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");var grid=Field<RectTransform>(view,"collection");
                Click(screen.transform,"BS_Next");Click(screen.transform,"BS_Next");
                Click(screen.transform,"CardCollectionView/Root/Window/TabArea/BookmarkToggle");
                Assert(VisibleIds(grid).Length==catalog.cards.Count(card=>card.IsPlayableFor(BattleRulesFormat.Standard)),"Changing tabs retained an out-of-range page or omitted playable cards.");
                var target=VisibleCards(grid).First(node=>node.name.Substring("BS_Card_".Length)!=Field<CardDefinition>(view,"selected").id);
                string expected=target.name.Substring("BS_Card_".Length);int requests=fake.RequestedArt.Count;
                target.GetComponent<Button>().onClick.Invoke();
                Assert(Field<CardDefinition>(view,"selected").id==expected,"A reused collection slot selected its old card identity.");
                Assert(fake.RequestedArt.Count==requests+1,"A pooled card accumulated duplicate click listeners.");
            });
            Case("Adding, removing and sorting deck cards preserve unaffected artwork widgets",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");var content=Field<RectTransform>(view,"deckContent");
                var candidates=catalog.cards.Where(card=>card.IsPlayableFor(BattleRulesFormat.Standard)).Take(3).ToArray();
                Click(screen.transform,"DeckEditHeader/Root/Header/ButtonChangeSide");
                foreach(var card in candidates.Take(2)){Call(view,"Select",card);Click(screen.transform,DeckAdd);}
                var first=VisibleCards(content);int objects=GameObject.All.Count;Call(view,"Select",candidates[2]);int requests=fake.RequestedArt.Count;
                Click(screen.transform,DeckAdd);
                Assert(first.All(node=>VisibleCards(content).Contains(node)),"Appending a card replaced existing deck widgets.");
                Assert(fake.RequestedArt.Count<=requests+1,"Appending one card reloaded artwork for the entire deck.");
                requests=fake.RequestedArt.Count;
                Click(screen.transform,"DeckEditHeader/Root/Header/ButtonSort");
                Assert(fake.RequestedArt.Count==requests&&GameObject.All.Count==objects,"Sorting a deck allocated widgets or reloaded their unchanged artwork.");
                string removed=VisibleIds(content)[0];VisibleCards(content)[0].GetComponent<Button>().onClick.Invoke();
                Assert(Field<CardDefinition>(view,"selected").id==removed&&Field<List<string>>(view,"deck").Count==2,"A deck slot click did not select and remove its current card exactly once.");
                string next=VisibleIds(content)[0];VisibleCards(content)[0].GetComponent<Button>().onClick.Invoke();
                Assert(Field<CardDefinition>(view,"selected").id==next&&Field<List<string>>(view,"deck").Count==1,"A moved pooled deck slot retained its former removal index or card.");
                Assert(VisibleIds(content).SequenceEqual(Field<List<string>>(view,"deck").Distinct(StringComparer.Ordinal)),"The pooled deck widgets no longer match the first occurrence order of the saved cards.");
            });
            Case("A pooled slot reloads an evicted texture without replacing its widget",()=>{
                var parent=new GameObject("PooledArtwork",typeof(RectTransform));var slot=BattleNativeWidgets.CreateCardSlot(parent.transform,fake);
                string id=catalog.cards.First(card=>card.IsPlayableFor(BattleRulesFormat.Standard)).id;slot.Bind(id);
                var image=slot.Button.GetComponentsInChildren<RawImage>(true).Single();var node=slot.Button.gameObject;int requests=fake.RequestedArt.Count;
                slot.Hide();Object.Destroy(fake.ArtTextures[id]);slot.Bind(id);
                Assert(ReferenceEquals(node,slot.Button.gameObject)&&fake.RequestedArt.Count==requests+1&&image.texture!=null,"Reactivating a pooled slot kept an evicted texture or recreated its widget.");
            });
            Case("Grouped duplicate cards show their logical quantity and remove one current occurrence",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");var content=Field<RectTransform>(view,"deckContent");
                var choices=catalog.cards.Where(card=>card.IsPlayableFor(BattleRulesFormat.Standard)).GroupBy(card=>card.nameJa).Select(group=>group.First()).OrderBy(card=>card.cost).ToArray();
                var first=choices[choices.Length-1];var second=choices.First(card=>card.nameJa!=first.nameJa);
                Click(screen.transform,"DeckEditHeader/Root/Header/ButtonChangeSide");
                Call(view,"Select",first);for(int copy=0;copy<3;copy++)Click(screen.transform,DeckAdd);
                Call(view,"Select",second);for(int copy=0;copy<2;copy++)Click(screen.transform,DeckAdd);
                var firstNode=VisibleCards(content).Single(node=>CardId(node)==first.id);
                Assert(VisibleIds(content).SequenceEqual(new[]{first.id,second.id})&&CardQuantity(firstNode)==3
                    &&CardQuantity(VisibleCards(content).Single(node=>CardId(node)==second.id))==2,
                    "Grouped deck lost first occurrence order, quantity badges, or displayed duplicate card faces.");
                firstNode.GetComponent<Button>().onClick.Invoke();
                Assert(Field<List<string>>(view,"deck").SequenceEqual(new[]{first.id,first.id,second.id,second.id})
                    &&CardQuantity(firstNode)==2,"Clicking a grouped face removed the wrong occurrence or more than one copy.");
                VisibleCards(content).Single(node=>CardId(node)==second.id).GetComponent<Button>().onClick.Invoke();
                Assert(Field<List<string>>(view,"deck").SequenceEqual(new[]{first.id,first.id,second.id}),"A later grouped card retained a stale logical removal index.");
                Click(screen.transform,"DeckEditHeader/Root/Header/ButtonSort");
                Assert(VisibleIds(content).SequenceEqual(Field<List<string>>(view,"deck").Distinct(StringComparer.Ordinal))
                    &&VisibleCards(content).Sum(CardQuantity)==Field<List<string>>(view,"deck").Count,
                    "Sorting grouped cards changed logical quantities or the displayed order.");
                firstNode.GetComponent<Button>().onClick.Invoke();
                Assert(Field<List<string>>(view,"deck").Count(id=>id==first.id)==1&&CardQuantity(firstNode)==1,
                    "A grouped card retained its pre-sort removal index.");
            });
            Case("Grouping the initial deck keeps all logical copies and saves the exact original list",()=>{
                var original=fake.SelectedDeck.cards.ToArray();var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");
                var content=Field<RectTransform>(view,"deckContent");
                Assert(original.Length>=40&&VisibleIds(content).SequenceEqual(original.Distinct(StringComparer.Ordinal)),"The grouped form omitted an ID or failed to collapse duplicate faces.");
                Assert(VisibleCards(content).Sum(CardQuantity)==original.Length&&Field<List<string>>(view,"deck").SequenceEqual(original),
                    "A compact card grid changed the underlying deck copy count or order.");
                Click(screen.transform,"DeckEditHeader/Root/Header/ButtonSave");
                Assert(fake.Saved!=null&&fake.Saved.cards.SequenceEqual(original),"Saving the compact form wrote one copy per group instead of the full deck.");
            });
            Case("Collection quantity badges follow logical edits while copy limits remain enforced",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");
                var content=Field<RectTransform>(view,"deckContent");var grid=Field<RectTransform>(view,"collection");
                var card=catalog.cards.First(value=>value.IsPlayableFor(BattleRulesFormat.Standard));
                Click(screen.transform,"DeckEditHeader/Root/Header/ButtonChangeSide");SearchDeck(screen,card.id);
                var tile=VisibleCards(grid).Single();Assert(CardQuantity(tile)==0,"An empty deck left an in-deck quantity on its collection tile.");
                Call(view,"Select",card);for(int copy=0;copy<4;copy++)Click(screen.transform,DeckAdd);
                Assert(Field<List<string>>(view,"deck").Count==3&&VisibleCards(content).Length==1&&CardQuantity(tile)==3
                    &&CardQuantity(VisibleCards(content).Single())==3,"Grouping bypassed the copy cap or displayed a stale collection count.");
                VisibleCards(content).Single().GetComponent<Button>().onClick.Invoke();
                Assert(CardQuantity(tile)==2&&CardQuantity(VisibleCards(content).Single())==2&&Field<List<string>>(view,"deck").Count==2,
                    "Removing one copy did not update both views immediately.");
                Assert(ReferenceEquals(tile,VisibleCards(grid).Single()),"A quantity-only edit replaced the collection's pooled tile.");
            });
            Case("Card selection border follows rebound identities without tinting the official face",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");var grid=Field<RectTransform>(view,"collection");
                var chosen=VisibleCards(grid).First(node=>CardId(node)!=Field<CardDefinition>(view,"selected").id);string id=CardId(chosen);
                chosen.GetComponent<Button>().onClick.Invoke();
                Assert(VisibleCards(grid).Single(node=>node.Find("BS_CardSelection").gameObject.activeSelf)==chosen,
                    "The selected card has no unique visible editor border.");
                foreach(var node in VisibleCards(grid)){
                    Assert(node.GetComponent<Button>().transition==Selectable.Transition.None,"Editor selection can tint the printed card face.");
                    Assert(node.Find("BS_CardSelection").GetComponentsInChildren<Graphic>(true).All(graphic=>!graphic.raycastTarget),
                        "A decorative selection border can intercept a card click.");
                }
                Click(screen.transform,"BS_Next");
                Assert(VisibleCards(grid).All(node=>!node.Find("BS_CardSelection").gameObject.activeSelf),"A rebound page retained the selected border on another card ID.");
                Click(screen.transform,"BS_Previous");
                Assert(VisibleCards(grid).Single(node=>CardId(node)==id).Find("BS_CardSelection").gameObject.activeSelf,
                    "Returning to the selected card's page lost its selection border.");
            });
            Case("Card status badges reset when pooled collection slots change eligibility",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");var grid=Field<RectTransform>(view,"collection");
                var outside=catalog.cards.First(card=>!card.IsEligibleFor(BattleRulesFormat.Standard));
                var playable=catalog.cards.First(card=>card.IsPlayableFor(BattleRulesFormat.Standard));
                SearchDeck(screen,outside.id);var tile=VisibleCards(grid).Single();var status=tile.Find("BS_CardStatus");
                Assert(status.gameObject.activeSelf&&status.GetComponentInChildren<TextMeshProUGUI>(true).text=="NGOÀI POOL",
                    "An out-of-pool card has no clear eligibility marker.");
                SearchDeck(screen,playable.id);
                Assert(ReferenceEquals(tile,VisibleCards(grid).Single())&&!tile.Find("BS_CardStatus").gameObject.activeSelf,
                    "A playable card inherited another card's out-of-pool badge.");
            });
            Case("Editor badges and selection chrome are absent from ordinary gameplay cards",()=>{
                var parent=new GameObject("GameplayHandCard",typeof(RectTransform));
                var button=BattleNativeWidgets.Card(parent.transform,catalog.cards[0].id,()=>{},fake);
                foreach(string decoration in new[]{"BS_CardQuantity","BS_CardStatus","BS_CardSelection","BS_CardCodeBackdrop"})
                    Assert(button.transform.Find(decoration)==null,"Deck-editor decorations leaked into the game hand: "+decoration);
            });
            Case("Search field keeps readable dark colors for every selectable state",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var field=Find(screen.transform,DeckSearch).GetComponent<TMP_InputField>();
                foreach(var background in new[]{field.colors.normalColor,field.colors.highlightedColor,field.colors.selectedColor,field.colors.pressedColor})
                    Assert(background.a>.9f&&Contrast(field.textComponent.color,background)>=4.5,
                        "Hover, focus or press can make search text white on a light background.");
                var placeholder=field.placeholder as TextMeshProUGUI;
                Assert(placeholder!=null&&placeholder.color.a>=.9f&&Contrast(placeholder.color,field.colors.normalColor)>=4.5,
                    "Search placeholder has insufficient contrast on the normal field.");
                Assert(placeholder.name=="BS_InputPlaceholder"&&placeholder.transform.parent==field.textViewport
                    &&placeholder.enabled==string.IsNullOrEmpty(field.text)&&!placeholder.raycastTarget
                    &&placeholder.gameObject.activeSelf,
                    "The owned input hint has broken viewport ownership, initial visibility or pointer behavior.");
                var legacyHint=field.textViewport.Find("#Text_Placeholder").GetComponent<TextMeshProUGUI>();
                Assert(legacyHint!=placeholder&&!legacyHint.enabled&&!legacyHint.raycastTarget,"The old hint can still draw or intercept input.");
                legacyHint.color=new Color(1,1,1,.125f);
                Assert(placeholder.color.a==1,"A legacy hint tint changed the owned placeholder's alpha.");
                Assert(field.colors.colorMultiplier==1&&field.customCaretColor&&Contrast(field.caretColor,field.colors.selectedColor)>=4.5,
                    "The focused input has an invisible caret or uncontrolled tint multiplier.");
                var owned=field.targetGraphic;var original=Find(field.transform,"Out").GetComponent<Image>();original.color=Color.white;
                Assert(owned!=original&&!original.enabled&&owned.enabled,"Legacy field chrome can still overwrite the owned dark hit surface.");
            });
            Case("Active collection tabs keep light captions and a dedicated selection underline",()=>{
                var screen=Screen(BattleNativeScreen.Deck);
                foreach(string selected in new[]{"CardListToggle","BookmarkToggle","HistotyToggle"}){
                    Click(screen.transform,"CardCollectionView/Root/Window/TabArea/"+selected);
                    foreach(string name in new[]{"CardListToggle","BookmarkToggle","HistotyToggle"}){
                        var tab=Find(screen.transform,"CardCollectionView/Root/Window/TabArea/"+name);
                        var caption=tab.Find("BS_Caption").GetComponent<TextMeshProUGUI>();
                        Assert(caption.color.r>.8f&&caption.color.g>.8f&&caption.color.b>.8f&&caption.color.a>.9f,
                            "A tab caption becomes dark against its dark face.");
                        Assert(tab.Find("BS_TabSelectedUnderline").gameObject.activeSelf==(name==selected),
                            "The active tab is not distinguished by its dedicated underline.");
                    }
                }
            });
            Case("Owned hints can return after clearing either search text or the deck name",()=>{
                var screen=Screen(BattleNativeScreen.Deck);
                foreach(string path in new[]{DeckSearch,"DeckView/Root/Window/HeaderArea/NameAreaGroup/InputField"}){
                    var field=Find(screen.transform,path).GetComponent<TMP_InputField>();
                    field.SetTextWithoutNotify("A visible value");
                    Assert(!field.placeholder.enabled&&field.placeholder.gameObject.activeSelf,
                        "A nonempty field kept its hint visible or deactivated its owned hint object.");
                    field.SetTextWithoutNotify(string.Empty);
                    Assert(field.placeholder.enabled&&field.placeholder.gameObject.activeInHierarchy,
                        "Clearing a field cannot show its owned hint again.");
                }
            });
            Case("Card density cycles and persists without changing the query, artwork or logical deck",()=>{
                var measured=new CountingCards(catalog.cards);fake.Cards=measured;
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");var grid=Field<RectTransform>(view,"collection");
                var area=(RectTransform)Find(screen.transform,"SafeArea");area.rect=new Rect(0,0,2680,1080);TickDeck(view,0);
                var ids=VisibleIds(grid);var logical=Field<List<string>>(view,"deck").ToArray();int scans=measured.Enumerations,requests=fake.RequestedArt.Count,objects=GameObject.All.Count;
                var widths=new List<float>();
                for(int step=0;step<3;step++){
                    Click(screen.transform,"BS_CardSize");widths.Add(grid.GetComponent<GridLayoutGroup>().cellSize.x);
                    Assert(VisibleIds(grid).SequenceEqual(ids)&&Field<List<string>>(view,"deck").SequenceEqual(logical),"A density choice changed the current page or deck.");
                }
                Assert(widths.Distinct().Count()>1&&measured.Enumerations==scans&&fake.RequestedArt.Count==requests&&GameObject.All.Count==objects,
                    "Density does not reflow the cards, or it reallocates tiles/reloads artwork/rescans the catalog.");
                Assert(MDPro3.Config.Values.TryGetValue("BattleSpiritsDeckDensity",out string saved)&&new[]{"Compact","Medium","Large"}.Contains(saved),
                    "The card density selection was not saved as a cosmetic preference.");
                var other=Screen(BattleNativeScreen.Deck);var otherView=Field<BattleNativeDeckView>(other,"deck");
                var otherArea=(RectTransform)Find(other.transform,"SafeArea");otherArea.rect=new Rect(0,0,2680,1080);TickDeck(otherView,0);
                Assert(Field<RectTransform>(otherView,"collection").GetComponent<GridLayoutGroup>().cellSize.x==widths.Last(),
                    "Reopening the deck form lost its saved card density.");
            });
            Case("Returning to an unchanged deck reloads evicted visible images and preserves unsaved edits",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");var grid=Field<RectTransform>(view,"collection");var content=Field<RectTransform>(view,"deckContent");
                Click(screen.transform,"DeckEditHeader/Root/Header/ButtonChangeSide");var card=catalog.cards.First(value=>value.IsPlayableFor(BattleRulesFormat.Standard));
                Call(view,"Select",card);Click(screen.transform,DeckAdd);
                var before=VisibleCards(content);var targets=VisibleCards(grid).Take(1).Concat(before).SelectMany(node=>node.GetComponentsInChildren<RawImage>(true)).ToList();
                targets.Add(Find(screen.transform,"CardDetailView/Root/Window/Content/CardArea/ImageCard").GetComponent<RawImage>());
                screen.gameObject.SetActive(false);foreach(var texture in targets.Select(image=>image.texture).Distinct().ToArray())Object.Destroy(texture);
                Input(screen.transform,DeckSearch,"Magic");Time.unscaledTime=1;
                screen.gameObject.SetActive(true);screen.ShowEvent();
                Assert(Field<List<string>>(view,"deck").SequenceEqual(new[]{card.id})&&before.SequenceEqual(VisibleCards(content)),"Reopening an unchanged session deck discarded unsaved edits or replaced its widgets.");
                Assert(before.SelectMany(node=>node.GetComponentsInChildren<RawImage>(true)).All(image=>image.texture!=null),"A hidden deck returned with Unity-destroyed artwork still assigned.");
                Assert(Find(screen.transform,"CardDetailView/Root/Window/Content/CardArea/ImageCard").GetComponent<RawImage>().texture!=null,"Reopening did not refresh the selected card's evicted detail image.");
                Assert(VisibleIds(grid).Length>0&&VisibleIds(grid).All(id=>catalog.cards.Single(value=>value.id==id).type=="Magic"),"Reopening did not flush the latest pending search.");
            });
            Case("A rebound slot uses the production loader ownership guard for delayed artwork",()=>{
                var session=BattleNativeSession.Ensure();var parent=new GameObject("ProductionPooledArt",typeof(RectTransform));var slot=BattleNativeWidgets.CreateCardSlot(parent.transform,session);
                string[] ids=catalog.cards.Where(card=>card.IsPlayableFor(BattleRulesFormat.Standard)).Take(2).Select(card=>card.id).ToArray();
                slot.Bind(ids[0]);slot.Bind(ids[1]);var image=slot.Button.GetComponentsInChildren<RawImage>(true).Single();
                Pump(()=>image.texture!=null&&image.texture!=Texture2D.blackTexture&&Field<Dictionary<string,List<RawImage>>>(session,"waitingArt").Count==0);
                Assert(image.texture.name=="BattleSpirits_"+ids[1]&&Field<Dictionary<RawImage,string>>(session,"requestedArt")[image]==ids[1],"An earlier file read replaced the current pooled card's artwork.");
            });
            Case("Original deck add action enforces same-name copy cap",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");
                Click(screen.transform,"DeckEditHeader/Root/Header/ButtonChangeSide");Call(view,"Select",catalog.cards.First(card=>card.IsPlayableFor(BattleRulesFormat.Standard)));
                for(int i=0;i<4;i++)Click(screen.transform,"CardDetailView/Root/Window/Content/DescriptionArea/MenuAreaRoot/MenuArea/CardGroup/AddCardButton");
                Assert(Field<List<string>>(view,"deck").Count==3,"Copy limit not enforced.");
            });
            Case("Native Solo starts a real selected Battle Spirits deck pair",()=>{
                var screen=Screen(BattleNativeScreen.Solo);
                Assert(screen.GetComponentsInChildren<Transform>(true).Any(node=>node.name.StartsWith("BS_Row_")),"No original Solo list rows.");
                Click(screen.transform,"ButtonDeck");Click(screen.transform,"ButtonPlay");Assert(fake.SoloStarts==1,"Solo button did not call BS session.");
                AssertGeneratedLayers(screen.transform);
            });
            Case("Standard deck form refuses Eternal-only BS01 cards",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");
                Click(screen.transform,"DeckEditHeader/Root/Header/ButtonChangeSide");
                Call(view,"Select",catalog.cards.Single(card=>card.id=="BS01-X01"));
                Click(screen.transform,"CardDetailView/Root/Window/Content/DescriptionArea/MenuAreaRoot/MenuArea/CardGroup/AddCardButton");
                Assert(Field<List<string>>(view,"deck").Count==0,"Eternal-only boss added to Standard deck.");
            });
            Case("Original online inputs send direct IP and port to Battle Spirits",()=>{
                var screen=Screen(BattleNativeScreen.Online);
                Input(screen.transform,"PageLegacy/Right/InputFieldName","Tester");Input(screen.transform,"PageLegacy/Right/InputFieldHost","192.168.1.20");Input(screen.transform,"PageLegacy/Right/InputFieldPort","7788");
                Click(screen.transform,"PageLegacy/Right/ButtonJoin");Assert(fake.Joins==1&&fake.JoinAddress=="192.168.1.20"&&fake.Port==7788&&fake.HostName=="Tester","Original IP/port bindings lost.");
                Click(screen.transform,"ToggleHost");Input(screen.transform,"PageHost/InputFieldTime","7799");Click(screen.transform,"PageHost/ButtonCreate");Assert(fake.Hosts==1&&fake.Port==7799,"Host form did not call BS server.");
            });
            Case("Native room ready/start follows connection and server lobby authority",()=>{
                fake.IsConnected=true;fake.Seat=0;fake.Players=new[]{new LobbyPlayerView{playerIndex=0,ready=true},new LobbyPlayerView{playerIndex=1,ready=true}};
                var screen=Screen(BattleNativeScreen.Room);Click(screen.transform,"ButtonReady");Click(screen.transform,"ButtonStart");Assert(fake.ReadyRequests==1&&fake.Starts==1,"Room actions not rebound.");
                fake.IsConnected=false;fake.Refresh();Assert(!Find(screen.transform,"ButtonReady").GetComponent<Selectable>().interactable,"Ready remains active after disconnect.");
            });
            Case("Duel uses own hand snapshot and dispatches opening choices",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards),0);var screen=Screen(BattleNativeScreen.Duel);
                var view=Field<BattleNativeDuelView>(screen,"duel");var hand=Field<RectTransform>(view,"hand");Assert(hand.childCount==4,"Own hand not displayed.");
                Click(screen.transform,"Buttons/ButtonInfo");Assert(fake.Commands.Single().action=="opening","Keep opening did not send BS command.");AssertGeneratedLayers(screen.transform);
            });
            Case("Opponent hand displays bounded backs using only the public count",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);
                string[] own=catalog.cards.Where(card=>card.IsPlayableFor(BattleRulesFormat.Standard)).Take(2).Select(card=>card.id).ToArray();fake.View.ownHand=own;fake.View.players[1].handCount=17;
                var screen=Screen(BattleNativeScreen.Duel);var duel=Field<BattleNativeDuelView>(screen,"duel");var hand=Field<RectTransform>(duel,"opponentHand");
                var backs=hand.GetComponentsInChildren<RawImage>(true);Assert(backs.Length==10&&backs.All(back=>back.texture==BattleTheme.CardBack&&!back.raycastTarget),"Opponent hand exposes a face, captures input, or creates unbounded back widgets.");
                Assert(hand.GetComponentsInChildren<TextMeshProUGUI>(true).Any(label=>label.name=="BS_HandOverflow"&&label.text=="+7"),"Opponent public count overflow is lost.");
                Assert(fake.RequestedArt.All(id=>own.Contains(id)),"Opponent hand requested a private face identity.");
                fake.View.players[1].handCount=3;fake.Refresh();Assert(hand.GetComponentsInChildren<RawImage>(true).Length==3,"Old opponent backs survive the next snapshot.");
                Assert(fake.RequestedArt.All(id=>own.Contains(id)),"A changed hidden hand requested its cards' faces.");
            });
            Case("Native Magic chooser permits Awaken draw-only without an owned Spirit",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards),0);
                fake.View.phase="Main";fake.View.decisionPlayer=0;fake.View.ownHand=new[]{AbilityRegistry.AwakenMagic};
                var screen=Screen(BattleNativeScreen.Duel);var duel=Field<BattleNativeDuelView>(screen,"duel");
                Call(duel,"PlayHand",0,AbilityRegistry.AwakenMagic);
                string option="Không chuyển Core · rút 1 lá";
                Assert(MDPro3.UIManager.Popup.Contains(option),"Draw-only option missing when the field is empty.");
                var go=new GameObject("PopupChoice",typeof(RectTransform));go.AddComponent<SelectionButton>();go.AddComponent<TextMeshProUGUI>().text=option;
                UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject=go;MDPro3.UIManager.PopupSelected.Invoke();
                var command=fake.Commands.Single();Assert(command.action=="playMagic"&&command.target==-1&&command.transfers?.Length==0,"Draw-only choice requested a nonexistent target or core.");
            });
            Case("Standard Second Main permits summon and end without another Attack",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);
                fake.View.phase="SecondMain";fake.View.rulesFormat="Standard";fake.View.turn=3;fake.View.decisionPlayer=0;
                string id=catalog.cards.First(card=>card.IsPlayableFor(BattleRulesFormat.Standard)).id;fake.View.ownHand=new[]{id};
                var screen=Screen(BattleNativeScreen.Duel);var duel=Field<BattleNativeDuelView>(screen,"duel");
                Assert(!Find(screen.transform,"Buttons/ButtonInfo").gameObject.activeSelf,"Second Main exposes Attack button.");
                Call(duel,"PlayHand",0,id);Assert(fake.Commands.Last().action=="summon","Second Main did not allow summon.");
                Click(screen.transform,"Buttons/ButtonLog");Assert(fake.Commands.Last().action=="endTurn","Second Main did not end turn.");
            });
            Case("Native duel controls close on disconnected state and replay uses step controls",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards),0);var screen=Screen(BattleNativeScreen.Duel);fake.CanSendCommands=false;fake.Refresh();
                Assert(Find(screen.transform,"Buttons/ButtonInfo").GetComponentInChildren<TextMeshProUGUI>(true).text=="QUAY LẠI",
                    "Disconnected state did not replace commands with a return route.");
                Assert(!Find(screen.transform,"Buttons/ButtonTiming").gameObject.activeSelf,"Disconnected opening action remains enabled.");
                fake.IsReplay=true;fake.Refresh();Click(screen.transform,"Buttons/ButtonInfo");Assert(fake.ReplayDelta==1,"Replay step controls not wired.");
            });
            Case("Native servant return disconnects an active room and restores original menu",()=>{
                BattleNativeNavigation.Open(BattleNativeScreen.Room);((BattleNativeServant)program.currentServant).OnReturn();Assert(fake.Disconnected==1&&program.currentServant==program.menu,"Native return lifecycle lost.");
            });
            Case("Actual menu facade initializes native session and routes original handlers",()=>{
                var menu=Menu();Assert(BattleNativeSession.Ensure().Initialized,"Catalog/backend initialization failed.");
                menu.OnSolo();Assert(((BattleNativeServant)program.currentServant).Screen==BattleNativeScreen.Solo,"Solo menu uses old overlay.");
                menu.OnDeck();Assert(((BattleNativeServant)program.currentServant).Screen==BattleNativeScreen.Deck,"Deck route not native.");
                menu.OnOnline();Assert(((BattleNativeServant)program.currentServant).Screen==BattleNativeScreen.Online,"Online route not native.");
                menu.OnPuzzle();Assert(MDPro3.UIManager.Popup?.First()=="BATTLE SPIRITS — STANDARD","Rules popup not native.");
            });
            Case("Session save/load preserves deck IDs and rejects path/format mistakes",()=>{
                var session=BattleNativeSession.Ensure();var deck=StarterDecks.Create(catalog.cards,"../CON:Battle/Deck", BattleRulesFormat.Standard);
                Assert(session.SaveDeck(deck,out string error),"Valid BS deck save failed: "+error);
                Assert(Directory.GetFiles(Path.Combine(temporary,"Data/BattleSpirits/Decks"),"*.json").Length>0,"No saved BS deck.");
                Assert(!session.SaveDeck(new DeckDefinition{name="Short",cards=new[]{"BS01-X01"}},out error),"Undersized deck accepted.");
            });
            Case("Actual Solo session runs WindBot on filtered views and records complete match",()=>{
                var session=BattleNativeSession.Ensure();var pair=StarterDecks.CreatePair(catalog.cards, BattleRulesFormat.Standard);session.StartSolo(pair[0],pair[1]);Assert(session.View?.viewer==0&&session.Seat==0,"Human own-view not fixed.");
                session.Send(new WireMessage{type="action",action="opening"});
                Pump(()=>session.View.phase=="Main",()=>{TickPresentation(0.5f);Call(session,"Update");});
                Assert(session.View.players[1].handCount==4,"AI opening did not complete.");
                session.Send(new WireMessage{type="action",action="concede"});Assert(session.View.winner==1,"Solo concession did not complete.");
                Assert(session.ReplayFiles().Length>0,"Completed Solo not saved.");
            });
            Case("Actual replay reconstructs commands and disables live action authority",()=>{
                var session=BattleNativeSession.Ensure();var existing=session.ReplayFiles();var pair=StarterDecks.CreatePair(catalog.cards, BattleRulesFormat.Standard);session.StartSolo(pair[0],pair[1]);session.Send(new WireMessage{type="action",action="concede"});
                string file=session.ReplayFiles().Except(existing).Single();session.LoadReplay(file,0);Assert(session.IsReplay&&!session.CanSendCommands,"Replay retained command authority.");
                session.StepReplay(1);Assert(session.View.winner==1,"Recorded concession replay diverged.");
            });
            Case("Actual field allocates isolated stage and disposes render targets on disconnect",()=>{
                var session=BattleNativeSession.Ensure();var go=new GameObject("ControlledField",typeof(RectTransform));go.layer=5;var image=go.AddComponent<RawImage>();
                session.AttachField(image);var field=go.GetComponent<BattleFieldVisuals>();Assert(field.IsLoaded,"Own field failed to load: "+field.LastError);
                var texture=(RenderTexture)image.texture;var stage=Field<GameObject>(field,"stage");Assert(stage.layer==31,"Own field leaked into UI layer.");
                session.Disconnect();Assert(texture.released&&field.IsDisposed&&image.texture==null,"Field render target not cleaned.");
                session.AttachField(image);Assert(go.GetComponents<BattleFieldVisuals>().Any(value=>!value.IsDisposed&&value.IsLoaded),"Cached native form cannot create next field.");
            });
            Case("Summon presentation uses the exact card face and matching illustration once",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);
                var screen=Screen(BattleNativeScreen.Duel);var presentation=screen.GetComponentInChildren<BattleSummonPresentation>(true);
                string id=catalog.cards.First(card=>card.IsPlayableFor(BattleRulesFormat.Standard)).id;
                var action=new PublicBattleEvent{kind="Summoned",actor=0,spiritId=101,cardId=id};fake.Emit(action);
                Assert(presentation.IsPlaying&&fake.IsPresentationBusy&&!fake.CanSendCommands,"Presentation did not hold local controls.");
                var face=Field<RawImage>(presentation,"cardArt");var illustration=Field<RawImage>(presentation,"illustrationArt");
                Assert(face.texture==fake.ArtTextures[id]&&illustration.texture==face.texture,"Summon substituted a different card/artwork.");
                var crop=BattleFieldVisuals.GetIllustrationUv(id);Assert(illustration.uvRect.x==crop.x&&illustration.uvRect.y==crop.y&&illustration.uvRect.width==crop.width&&illustration.uvRect.height==crop.height,"Illustration included the wrong printed region.");
                fake.Emit(action);fake.Refresh();Assert(presentation.PendingCount==0,"Duplicate event/status redraw queued a second summon.");
                TickPresentation(0.25f);TickPresentation(0.25f);
                float expected=face.texture.width*crop.width/(face.texture.height*crop.height);
                Assert(Math.Abs(illustration.GetComponent<AspectRatioFitter>().aspectRatio-expected)<0.00001f,"Illustration is stretched to the full-card aspect.");
                for(int frame=0;frame<10;frame++)TickPresentation(0.25f);
                Assert(!presentation.IsPlaying&&!fake.IsPresentationBusy&&fake.CanSendCommands,"Summon did not release local controls.");
                Assert(GameObject.All.Count(go=>!go.destroyed&&go.GetComponent<Canvas>()!=null)==1,"Summon created another Canvas.");
            });
            Case("Summon queue is bounded and never issues gameplay commands",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);
                var screen=Screen(BattleNativeScreen.Duel);var presentation=screen.GetComponentInChildren<BattleSummonPresentation>(true);
                string id=catalog.cards.First(card=>card.IsPlayableFor(BattleRulesFormat.Standard)).id;
                fake.Emit(new PublicBattleEvent{kind="GameWon",cardId=id});fake.Emit(new PublicBattleEvent{kind="Summoned",cardId="unknown-id"});Assert(!presentation.IsPlaying,"Non-summon or unknown card started a presentation.");
                for(int index=1;index<=20;index++)fake.Emit(new PublicBattleEvent{kind="Summoned",actor=index%2,spiritId=index,cardId=id});
                Assert(presentation.IsPlaying&&presentation.PendingCount<=4,"A burst of network events grew an unbounded queue.");
                for(int frame=0;frame<60;frame++)TickPresentation(0.25f);
                Assert(!presentation.IsPlaying&&presentation.PendingCount==0&&!fake.IsPresentationBusy,"A bounded queue could not drain.");
                Assert(fake.Commands.Count==0,"Visual queue sent a rules/network action.");
            });
            Case("Actual received summon snapshot emits once while status rerenders remain silent",()=>{
                var match=SummonedFixture(out _,out _,out _);var session=BattleNativeSession.Ensure();int count=0;
                session.PresentationEvent+=item=>{if(item.kind=="Summoned")count++;};
                var snapshot=ViewFactory.Create(match,0);Call(session,"Receive",new WireMessage{type="state",rulesFormat="Standard",view=snapshot});
                var presentation=program.currentServant.servantUI.GetComponentInChildren<BattleSummonPresentation>(true);
                Assert(count==1&&presentation.IsPlaying&&session.IsPresentationBusy,"Actual network snapshot did not forward its summon.");
                Call(session,"Notify");Call(session,"Notify");Assert(count==1&&presentation.PendingCount==0,"Status-only notifications replayed the previous summon.");
                string id=snapshot.events.Single(item=>item.kind=="Summoned").cardId;
                Pump(()=>Field<RawImage>(presentation,"cardArt").texture!=null&&Field<RawImage>(presentation,"cardArt").texture!=Texture2D.blackTexture);
                Assert(Field<RawImage>(presentation,"cardArt").texture==Field<Dictionary<string,Texture2D>>(session,"textures")[id],"Actual session loaded a different face for its summon.");
                presentation.Clear();Assert(!session.IsPresentationBusy,"Actual session retained busy state after clearing presentation.");
            });
            Case("Replay seeking is silent and stepping forward presents its accepted summon once",()=>{
                SummonedFixture(out int seed,out DeckDefinition[] decks,out List<FixtureAction> actions);
                var session=BattleNativeSession.Ensure();int count=0;session.PresentationEvent+=item=>{if(item.kind=="Summoned")count++;};
                string path=Path.Combine(temporary,"summoned-fixture-replay.json");
                File.WriteAllText(path,JsonConvert.SerializeObject(new{schema=Protocol.Version,catalogHash=CatalogFingerprint.Compute(catalog.cards),rulesFormat="Standard",seed,names=new[]{"Fixture0","Fixture1"},decks,actions}));
                session.LoadReplay(path,actions.Count);var presentation=program.currentServant.servantUI.GetComponentInChildren<BattleSummonPresentation>(true);
                Assert(session.IsReplay&&count==0&&!presentation.IsPlaying,"Seeking to an old summon started a new cinematic.");
                session.StepReplay(-1);Assert(count==0,"Backward seek replayed an old summon.");session.StepReplay(1);
                Assert(count==1&&presentation.IsPlaying&&!session.CanSendCommands,"Forward replay either lost its summon or obtained live gameplay authority.");
                session.Disconnect();Assert(!presentation.IsPlaying&&!session.IsPresentationBusy,"Leaving replay retained queued presentation.");
            });
            Case("Missing artwork, skip, hiding and disconnect release summon ownership",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);
                fake.MissingArtwork=true;var screen=Screen(BattleNativeScreen.Duel);var presentation=screen.GetComponentInChildren<BattleSummonPresentation>(true);
                string id=catalog.cards.First(card=>card.IsPlayableFor(BattleRulesFormat.Standard)).id;
                var action=new PublicBattleEvent{kind="Summoned",actor=0,spiritId=9,cardId=id};fake.Emit(action);TickPresentation(0.25f);
                Assert(Field<RawImage>(presentation,"cardArt").color.a==0,"Black loading sentinel was presented as card art.");
                for(int frame=0;frame<8;frame++)TickPresentation(0.25f);
                Assert(!presentation.IsPlaying&&!fake.IsPresentationBusy,"Missing artwork held the UI indefinitely.");
                fake.MissingArtwork=false;fake.Emit(action);Click(screen.transform,"BS_SummonPresentation/BS_SkipSummon");Assert(!presentation.IsPlaying&&fake.CanSendCommands,"Skip did not release controls.");
                fake.Emit(action);screen.gameObject.SetActive(false);Assert(!presentation.IsPlaying&&!fake.IsPresentationBusy,"Hiding the native form retained summon ownership.");
                screen.gameObject.SetActive(true);fake.Emit(action);fake.Disconnect();Assert(!presentation.IsPlaying&&!fake.IsPresentationBusy,"Disconnected room retained a summon.");
                var borrowed=fake.ArtTextures[id];presentation.Dispose();Assert(borrowed!=null,"Disposing a presentation destroyed cached artwork.");
            });
            Case("Field card and floating illustration share each public card's own artwork",()=>{
                var output=new GameObject("CardField",typeof(RectTransform)).AddComponent<RawImage>();var field=output.gameObject.AddComponent<BattleFieldVisuals>();
                field.SetCatalog(catalog.cards);field.SetArtworkProvider(fake.LoadArt);field.Load(output);
                var ids=catalog.cards.Where(card=>card.IsPlayableFor(BattleRulesFormat.Standard)).Take(3).Select(card=>card.id).ToArray();
                var snapshot=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);snapshot.ownHand=new[]{ids[2]};
                snapshot.players[0].field=new[]{new SpiritView{instanceId=10,cardId=ids[0],normalCores=1}};
                snapshot.players[1].field=new[]{new SpiritView{instanceId=20,cardId=ids[1],normalCores=1}};field.Render(snapshot);
                var units=(System.Collections.IDictionary)Field<object>(field,"units");Assert(units.Count==2,"Public field did not create both players' figures.");
                foreach(int instance in new[]{10,20})
                {
                    string id=instance==10?ids[0]:ids[1];var figure=Field<object>(units[instance],"figure");
                    Assert(figure.GetType().Name=="BattleCardFigure","Generic monster model still used for a field card.");
                    Assert(Field<Material>(figure,"cardMaterial").mainTexture==fake.ArtTextures[id]&&Field<Material>(figure,"illustrationMaterial").mainTexture==fake.ArtTextures[id],"Floor card and floating artwork use the wrong card ID.");
                    var uv=Field<Material>(figure,"illustrationMaterial").Vectors["_UvRect"];var expected=BattleFieldVisuals.GetIllustrationUv(id);
                    Assert(uv.x==expected.x&&uv.y==expected.y&&uv.z==expected.width&&uv.w==expected.height,"Field illustration has incorrect texture coordinates.");
                }
                Assert(fake.RequestedArt.SequenceEqual(ids.Take(2)),"Field requested a hand/private card rather than public units.");
            });
            Case("Field skin uses six proper color symbols and public deck backs without private card requests",()=>{
                var output=new GameObject("SkinnedField",typeof(RectTransform)).AddComponent<RawImage>();var field=output.gameObject.AddComponent<BattleFieldVisuals>();field.SetArtworkProvider(fake.LoadArt);field.Load(output);
                var snapshot=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);field.Render(snapshot);
                var stage=Field<GameObject>(field,"stage");
                foreach(CardColor color in Enum.GetValues(typeof(CardColor)))
                {
                    var symbol=stage.GetComponentsInChildren<MeshRenderer>(true).Single(renderer=>renderer.name=="ColorSymbol_"+color);
                    Assert(symbol.sharedMaterial.mainTexture==Resources.Load<Texture2D>("BattleSpirits/Skin/Color"+color),"Field color symbol has another color's artwork: "+color);
                }
                var backs=stage.GetComponentsInChildren<MeshRenderer>(true).Where(renderer=>renderer.name=="DeckCardBack").ToArray();
                Assert(backs.Length==2&&backs.All(renderer=>renderer.sharedMaterial.mainTexture==BattleTheme.CardBack),"Decks expose a face or lack the Battle Spirits back.");
                Assert(backs.All(renderer=>Math.Abs(renderer.transform.localScale.z-1.2f*BattleTheme.CardBack.height/BattleTheme.CardBack.width)<0.00001f),"Deck back artwork has a stretched aspect.");
                Assert(fake.RequestedArt.Count==0,"Decorative symbols/deck stacks requested a private hand/deck identity.");
                var decoration=stage.GetComponentsInChildren<MeshRenderer>(true).Where(renderer=>renderer.name.StartsWith("Playmat",StringComparison.Ordinal)||renderer.name=="BattleSpiritsPlaymatLogo").ToArray();
                Assert(decoration.Length==9&&decoration.All(renderer=>renderer.sharedMaterial.mainTexture==BattleTheme.PlaymatStandard),"Field uses the old Eternal sheet or omits Standard ornament.");
                Assert(decoration.All(renderer=>{var uv=renderer.sharedMaterial.Vectors["_UvRect"];return uv.z<1&&uv.w<1;}),"A whole printed one-player sheet duplicates Japanese controls over the live field.");
            });
            Case("Repeated Core-pool updates reuse skin materials and release only field-owned resources",()=>{
                var output=new GameObject("PooledSkinField",typeof(RectTransform)).AddComponent<RawImage>();var field=output.gameObject.AddComponent<BattleFieldVisuals>();field.SetArtworkProvider(fake.LoadArt);field.Load(output);
                var snapshot=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);field.Render(snapshot);
                var materials=Field<List<Material>>(field,"materials");var meshes=Field<List<Mesh>>(field,"meshes");int materialCount=materials.Count,meshCount=meshes.Count;
                var deckMaterial=Field<Material>(field,"cardBackMaterial");var skinMesh=Field<Mesh>(field,"skinQuad");var back=BattleTheme.CardBack;var mat=BattleTheme.PlaymatStandard;
                for(int i=0;i<30;i++){snapshot.players[0].reserveNormal=i;snapshot.players[1].trashNormal=i;field.Render(snapshot);}
                Assert(materials.Count==materialCount&&meshes.Count==meshCount&&Field<Material>(field,"cardBackMaterial")==deckMaterial&&Field<Mesh>(field,"skinQuad")==skinMesh,"A public Core-count update allocates new decorative meshes/materials.");
                var ownedMaterials=materials.ToArray();var ownedMeshes=meshes.ToArray();field.Dispose();
                Assert(ownedMaterials.All(material=>material==null)&&ownedMeshes.All(mesh=>mesh==null),"Field-owned style resources leaked after disposal.");
                Assert(back!=null&&mat!=null&&BattleTheme.CardBack==back&&BattleTheme.PlaymatStandard==mat,"Closing a field destroyed shared Resources artwork.");
            });
            Case("Life, Reserve, Trash and Spirit Cores display camera-facing blue and red gem artwork",()=>{
                var output=new GameObject("CoreGemField",typeof(RectTransform)).AddComponent<RawImage>();
                var field=output.gameObject.AddComponent<BattleFieldVisuals>();field.SetArtworkProvider(fake.LoadArt);field.Load(output);
                var snapshot=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);
                string id=catalog.cards.First(card=>card.IsPlayableFor(BattleRulesFormat.Standard)).id;
                snapshot.players[0].life=5;snapshot.players[0].reserveNormal=3;snapshot.players[0].reserveSoul=true;snapshot.players[0].trashNormal=2;
                snapshot.players[0].field=new[]{new SpiritView{instanceId=81,cardId=id,normalCores=2}};
                snapshot.players[1].life=4;snapshot.players[1].reserveNormal=4;snapshot.players[1].reserveSoul=false;snapshot.players[1].trashNormal=1;
                snapshot.players[1].field=new[]{new SpiritView{instanceId=82,cardId=id,normalCores=1,soulCore=true}};
                string before=JsonConvert.SerializeObject(snapshot);field.Render(snapshot);
                var stage=Field<GameObject>(field,"stage");var normal=Field<Material>(field,"normalGemMaterial");var soul=Field<Material>(field,"soulGemMaterial");
                var gems=stage.GetComponentsInChildren<MeshRenderer>(true).Where(item=>new[]{"Core","SoulCore","SpiritCore","SpiritSoulCore"}.Contains(item.name)).ToArray();
                Assert(gems.Count(item=>item.name=="Core"||item.name=="SpiritCore")==22&&gems.Count(item=>item.name=="SoulCore"||item.name=="SpiritSoulCore")==2,"Gem rendering changed public Core quantities.");
                Assert(normal.mainTexture==BattleTheme.ColorIcon(CardColor.Blue)&&soul.mainTexture==BattleTheme.ColorIcon(CardColor.Red),"Default Core and Soul use chips or another card color instead of the provided gem artwork.");
                var mesh=Field<Mesh>(field,"skinQuad");var camera=Field<Camera>(field,"camera");
                foreach(var gem in gems)
                {
                    bool isSoul=gem.name=="SoulCore"||gem.name=="SpiritSoulCore";
                    Assert(gem.sharedMaterial==(isSoul?soul:normal)&&gem.GetComponent<MeshFilter>().sharedMesh==mesh,"Core marker is still an untextured sphere or creates a per-gem mesh/material.");
                    var center=camera.WorldToViewportPoint(gem.transform.TransformPoint(Vector3.zero));
                    var right=camera.WorldToViewportPoint(gem.transform.TransformPoint(new Vector3(0.5f,0,0)));
                    var top=camera.WorldToViewportPoint(gem.transform.TransformPoint(new Vector3(0,0,0.5f)));
                    Assert(right.x>center.x&&top.y>center.y&&Math.Abs(right.y-center.y)<0.002f&&Math.Abs(top.x-center.x)<0.002f,"Gem artwork is mirrored, flat on the table, or faces away from the camera.");
                }
                Assert(JsonConvert.SerializeObject(snapshot)==before,"Displaying gems mutated public match data.");
            });
            Case("Core skins change only normal gem artwork while Soul and match state remain unchanged",()=>{
                var output=new GameObject("CosmeticCoreField",typeof(RectTransform)).AddComponent<RawImage>();
                var field=output.gameObject.AddComponent<BattleFieldVisuals>();field.SetArtworkProvider(fake.LoadArt);field.Load(output);
                var snapshot=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);field.Render(snapshot);
                string state=JsonConvert.SerializeObject(snapshot);var normal=Field<Material>(field,"normalGemMaterial");var soul=Field<Material>(field,"soulGemMaterial");
                var materials=Field<List<Material>>(field,"materials");var meshes=Field<List<Mesh>>(field,"meshes");int materialCount=materials.Count,meshCount=meshes.Count;
                var soulTexture=soul.mainTexture;
                foreach(string name in new[]{"Blue","Purple","Green","Yellow","White","purple","Red","not-a-skin",""})
                {
                    MDPro3.Config.Set(BattleTheme.CoreSkinConfig,name);Call(field,"Update");field.Render(snapshot);
                    string canonical=BattleTheme.NormalizeCoreSkin(name);
                    Assert(normal.mainTexture==Resources.Load<Texture2D>("BattleSpirits/Skin/Color"+canonical),"Cosmetic Core selection did not update existing markers.");
                    Assert(soul.mainTexture==soulTexture&&soulTexture==BattleTheme.ColorIcon(CardColor.Red),"Changing ordinary Core skin recolored the unique Soul Core.");
                    Assert(materials.Count==materialCount&&meshes.Count==meshCount,"Changing Core appearance allocated unbounded field resources.");
                    Assert(JsonConvert.SerializeObject(snapshot)==state&&fake.Commands.Count==0,"A local skin setting changed match state or sent a command.");
                }
                Assert(BattleTheme.NormalizeCoreSkin("Red")=="Blue"&&BattleTheme.NormalizeCoreSkin(null)=="Blue","Invalid/default ordinary Core style can impersonate the red Soul Core.");
                var owned=materials.ToArray();var blue=BattleTheme.ColorIcon(CardColor.Blue);field.Dispose();
                Assert(owned.All(item=>item==null)&&Field<Material>(field,"normalGemMaterial")==null&&Field<Material>(field,"soulGemMaterial")==null,"Core material ownership leaked on field disposal.");
                Assert(blue!=null&&soulTexture!=null,"Disposing Core markers destroyed shared source gem images.");
            });
            Case("Field viewport bounds follow the printed card and exhausted orientation",()=>{
                var output=new GameObject("BoundsField",typeof(RectTransform)).AddComponent<RawImage>();var field=output.gameObject.AddComponent<BattleFieldVisuals>();field.SetArtworkProvider(fake.LoadArt);field.Load(output);
                string id=catalog.cards.First(card=>card.IsPlayableFor(BattleRulesFormat.Standard)).id;
                var snapshot=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);snapshot.players[0].field=new[]{new SpiritView{instanceId=11,cardId=id}};field.Render(snapshot);
                Assert(field.TryGetCardViewport(11,out Rect ready)&&ready.width>0&&ready.height>0&&ready.xMin>=0&&ready.yMin>=0&&ready.xMax<=1&&ready.yMax<=1,"Projected card bounds are invalid.");
                snapshot.players[0].field[0].exhausted=true;field.Render(snapshot);Assert(field.TryGetCardViewport(11,out Rect exhausted)&&exhausted.width>ready.width&&exhausted.height<ready.height,"Exhausted card bounds did not turn with the card.");
                snapshot.players[0].field=Array.Empty<SpiritView>();field.Render(snapshot);Assert(!field.TryGetCardViewport(11,out _),"Removed card remains clickable.");
            });
            Case("Native field selection uses transparent projected targets without duplicate faces",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);fake.View.phase="Main";fake.View.decisionPlayer=0;
                string id=catalog.cards.First(card=>card.IsPlayableFor(BattleRulesFormat.Standard)).id;fake.View.players[0].field=new[]{new SpiritView{instanceId=21,cardId=id}};fake.Bounds[21]=new Rect(0.2f,0.3f,0.15f,0.12f);
                var screen=Screen(BattleNativeScreen.Duel);var target=Find(screen.transform,"BS_FieldCard_21");var rect=(RectTransform)target;
                Assert(rect.anchorMin.x==0.2f&&rect.anchorMin.y==0.3f&&Math.Abs(rect.anchorMax.x-0.35f)<0.00001f&&Math.Abs(rect.anchorMax.y-0.42f)<0.00001f,"Click target does not match the rendered card.");
                Assert(target.GetComponent<Image>().color.a==0&&target.GetComponentsInChildren<RawImage>(true).Length==0,"Another opaque card face obscures the field artwork.");
                Click(screen.transform,"BS_FieldCard_21");Assert(Field<int>(Field<BattleNativeDuelView>(screen,"duel"),"selectedSpirit")==21,"Projected card target did not select the correct Spirit.");
            });
            Case("Figure disposal retains shared textures and a cached card can reload evicted artwork",()=>{
                var output=new GameObject("OwnershipField",typeof(RectTransform)).AddComponent<RawImage>();var field=output.gameObject.AddComponent<BattleFieldVisuals>();field.SetArtworkProvider(fake.LoadArt);field.Load(output);
                string id=catalog.cards.First(card=>card.IsPlayableFor(BattleRulesFormat.Standard)).id;var snapshot=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);
                snapshot.players[0].field=new[]{new SpiritView{instanceId=31,cardId=id},new SpiritView{instanceId=32,cardId=id}};field.Render(snapshot);
                var units=(System.Collections.IDictionary)Field<object>(field,"units");var first=Field<object>(units[31],"figure");var second=Field<object>(units[32],"figure");var texture=fake.ArtTextures[id];
                snapshot.players[0].field=snapshot.players[0].field.Skip(1).ToArray();field.Render(snapshot);Time.unscaledTime+=2;Call(field,"Update");
                Assert(Field<GameObject>(first,"root")==null&&Field<RawImage>(first,"artwork")==null&&texture!=null,"Removing one copy leaked its resources or destroyed shared artwork.");
                int requests=fake.RequestedArt.Count;Object.Destroy(texture);Call(field,"Update");
                Assert(fake.RequestedArt.Count==requests+1&&fake.ArtTextures[id]!=null&&Field<Material>(second,"cardMaterial").mainTexture==fake.ArtTextures[id],"A returned field did not reload evicted borrowed artwork exactly once.");
                field.Dispose();Assert(fake.ArtTextures[id]!=null&&Field<GameObject>(second,"root")==null,"Disposing field destroyed the session texture or leaked the last figure.");
            });
            Case("Delayed artwork completion cannot overwrite a newer card selection",()=>{
                var session=BattleNativeSession.Ensure();var image=new GameObject("Preview",typeof(RectTransform)).AddComponent<RawImage>();
                var newer=new Texture2D(1,1,TextureFormat.RGBA32,false);image.texture=newer;
                Field<Dictionary<RawImage,string>>(session,"requestedArt")[image]="new";
                // Actual async loader race is covered by target ownership: a subsequent
                // request overwrites the old ID before any read completes.
                session.LoadArt(catalog.cards[0].id,image);session.LoadArt("unknown-id",image);Context.Drain();
                Assert(Field<Dictionary<RawImage,string>>(session,"requestedArt")[image]=="unknown-id","Old artwork retained ownership.");
            });
            Case("Boss preview uses the native duel form without live match authority",()=>{
                var session=BattleNativeSession.Ensure();session.StartBossPreview("BS01-X01");
                Assert(session.IsPreview&&!session.CanSendCommands&&session.View.players[0].field.Single().cardId=="BS01-X01","Boss preview entered a live match.");
                var screen=(BattleNativeScreenUI)program.currentServant.servantUI;
                Click(screen.transform,"Buttons/ButtonInfo");for(int frame=0;frame<10;frame++)TickPresentation(0.25f);Click(screen.transform,"Buttons/ButtonTiming");
                Assert(session.IsPreview&&session.View.phase=="XEM HOẠT CẢNH","Preview playback changed game rules.");
                Click(screen.transform,"Buttons/ButtonLog");Assert(!session.IsPreview&&program.currentServant==program.menu,"Preview did not release its native screen.");
            });
            Case("Official card faces and generated closeups retain separate cache ownership",()=>{
                string face=Path.Combine(temporary,"Picture/Art/BS01-X01.png"),portrait=Path.Combine(temporary,"Picture/Closeup/BS01-X01.png");
                Directory.CreateDirectory(Path.GetDirectoryName(face));Directory.CreateDirectory(Path.GetDirectoryName(portrait));
                File.Copy(Path.Combine(sourceRoot,"Picture/Art/BS01-X01.png"),face,true);File.Copy(Path.Combine(sourceRoot,"Picture/Closeup/BS01-X01.png"),portrait,true);
                var session=BattleNativeSession.Ensure();var image=new GameObject("CardPortrait",typeof(RectTransform)).AddComponent<RawImage>();
                session.LoadPortrait("BS01-X01",image);
                var textures=Field<Dictionary<string,Texture2D>>(session,"textures");Pump(()=>textures.ContainsKey("closeup:BS01-X01"));
                Assert(image.texture==textures["closeup:BS01-X01"],"Generated closeup not used.");
                session.LoadArt("BS01-X01",image);Pump(()=>textures.ContainsKey("BS01-X01"));
                Assert(image.texture==textures["BS01-X01"]&&image.texture!=textures["closeup:BS01-X01"],"Official card face replaced by a cached closeup.");
            });
            Case("Corrupt future replay commands are rejected before replacing a live Solo",()=>{
                var session=BattleNativeSession.Ensure();var pair=StarterDecks.CreatePair(catalog.cards, BattleRulesFormat.Standard);session.StartSolo(pair[0],pair[1]);session.Send(new WireMessage{type="action",action="concede"});
                var saved=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(session.ReplayFiles().First()));
                saved["actions"]=new Newtonsoft.Json.Linq.JArray(new Newtonsoft.Json.Linq.JObject { ["actor"]=0,["command"]=new Newtonsoft.Json.Linq.JObject { ["action"]="invented-command" } });
                string bad=Path.Combine(temporary,"bad-replay.json");File.WriteAllText(bad,saved.ToString());
                session.StartSolo(pair[0],pair[1]);var before=session.View;session.LoadReplay(bad,0);
                Assert(!session.IsReplay&&session.CanSendCommands&&ReferenceEquals(before,session.View),"Invalid replay discarded the active game.");
            });
            Case("Actual wallpaper read owns its full-resolution texture and releases it with its component",()=>{
                InstallWallpaperFixture();
                var root=new GameObject("WallpaperOwner");var wallpaper=root.AddComponent<BattleMenuWallpaper>();
                var image=new GameObject("WallpaperImage",typeof(RectTransform)).AddComponent<RawImage>();
                Assert(FinishWallpaper(wallpaper.LoadAsync(image)),"The real installed wallpaper did not load.");
                var texture=image.texture as Texture2D;
                byte[] png=File.ReadAllBytes(Path.Combine(sourceRoot,"Picture/Art/bs-menu.png"));
                int width=(png[16]<<24)|(png[17]<<16)|(png[18]<<8)|png[19],height=(png[20]<<24)|(png[21]<<16)|(png[22]<<8)|png[23];
                Assert(texture!=null&&texture.width==width&&texture.height==height&&!image.raycastTarget&&image.color.a==1&&image.color.r==1,
                    "The completed wallpaper remained a placeholder, intercepted clicks or did not become visible.");
                Object.Destroy(root);
                Assert(image.texture==null&&texture==null,"Destroying the wallpaper owner leaked its texture or left the image referencing it.");
            });
            Case("A queued wallpaper read cannot assign after its component is destroyed",()=>{
                InstallWallpaperFixture();
                var root=new GameObject("DestroyedWallpaper");var wallpaper=root.AddComponent<BattleMenuWallpaper>();
                var image=new GameObject("PersistentImage",typeof(RectTransform)).AddComponent<RawImage>();
                int created=Texture2D.Created.Count;
                var loading=PendingWallpaper(wallpaper,image);
                Object.Destroy(root);
                Assert(!FinishWallpaper(loading)&&image.texture==null,"A destroyed wallpaper owner assigned its late file-read result.");
                Assert(Texture2D.Created.Skip(created).All(texture=>texture==null),"The destroyed owner leaked a late texture.");
            });
            Case("A queued wallpaper read rejects a destroyed image without allocating a texture",()=>{
                InstallWallpaperFixture();
                var wallpaper=new GameObject("SurvivingWallpaper").AddComponent<BattleMenuWallpaper>();
                var image=new GameObject("RemovedImage",typeof(RectTransform)).AddComponent<RawImage>();
                var loading=PendingWallpaper(wallpaper,image);int created=Texture2D.Created.Count;
                Object.Destroy(image.gameObject);
                Assert(!FinishWallpaper(loading)&&Texture2D.Created.Count==created,
                    "A removed image received a late texture or leaked its candidate allocation.");
            });
            Case("Only the newest wallpaper generation may assign its image",()=>{
                InstallWallpaperFixture();
                var wallpaper=new GameObject("ReusedWallpaper").AddComponent<BattleMenuWallpaper>();
                var firstImage=new GameObject("OldImage",typeof(RectTransform)).AddComponent<RawImage>();
                var secondImage=new GameObject("CurrentImage",typeof(RectTransform)).AddComponent<RawImage>();
                var old=PendingWallpaper(wallpaper,firstImage);
                var current=wallpaper.LoadAsync(secondImage);
                Assert(!FinishWallpaper(old)&&FinishWallpaper(current),"A stale wallpaper generation completed or the newest one could not load.");
                Assert(firstImage.texture==null&&secondImage.texture!=null,"A superseded read wrote into its old target.");
            });
            Case("The wallpaper assignment guard rejects a superseded manager request",()=>{
                InstallWallpaperFixture();bool current=true;
                var wallpaper=new GameObject("GuardedWallpaper").AddComponent<BattleMenuWallpaper>();
                var image=new GameObject("GuardedImage",typeof(RectTransform)).AddComponent<RawImage>();
                var loading=PendingWallpaper(wallpaper,image,()=>current);
                current=false;
                Assert(!FinishWallpaper(loading)&&image.texture==null,"A superseded manager request painted an obsolete wallpaper.");
            });
            Case("Replacing a wallpaper releases only its own image and texture",()=>{
                InstallWallpaperFixture();
                var root=new GameObject("ReplacingWallpaper");var wallpaper=root.AddComponent<BattleMenuWallpaper>();
                var firstImage=new GameObject("FirstWallpaper",typeof(RectTransform)).AddComponent<RawImage>();
                var secondImage=new GameObject("SecondWallpaper",typeof(RectTransform)).AddComponent<RawImage>();
                Assert(FinishWallpaper(wallpaper.LoadAsync(firstImage)),"Initial wallpaper load failed.");
                var old=firstImage.texture;
                Assert(FinishWallpaper(wallpaper.LoadAsync(secondImage)),"Replacement wallpaper load failed.");
                Assert(old==null&&firstImage.texture==null&&secondImage.texture!=null,"Replacement leaked the previous texture or left it assigned.");
                var owned=secondImage.texture;var external=new Texture2D(8,8,TextureFormat.RGBA32,false);secondImage.texture=external;
                Object.Destroy(root);
                Assert(owned==null&&external!=null&&secondImage.texture==external,"Cleanup destroyed or cleared a newer texture owned by another loader.");
            });
            Case("Missing and invalid wallpaper files fail visibly without assigning a texture",()=>{
                string path=Path.Combine(temporary,"Picture/Art/bs-menu.png");if(File.Exists(path))File.Delete(path);
                var wallpaper=new GameObject("UnavailableWallpaper").AddComponent<BattleMenuWallpaper>();
                var image=new GameObject("NeutralWallpaper",typeof(RectTransform)).AddComponent<RawImage>();
                int warnings=Debug.Logs.Count;
                Assert(!FinishWallpaper(wallpaper.LoadAsync(image))&&image.texture==null&&image.color.a==1&&Debug.Logs.Count>warnings,
                    "A missing optional wallpaper silently reported success or left a transparent background.");
                Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,new byte[]{1,2,3,4});warnings=Debug.Logs.Count;
                int created=Texture2D.Created.Count;
                Assert(!FinishWallpaper(wallpaper.LoadAsync(image))&&image.texture==null&&Debug.Logs.Count>warnings,
                    "An invalid wallpaper was assigned or swallowed without a useful warning.");
                Assert(Texture2D.Created.Skip(created).Any()&&Texture2D.Created.Skip(created).All(texture=>texture==null),
                    "The undecodable candidate texture leaked after LoadImage rejected it.");
            });
            Case("Title is owned by the existing Boot Canvas and repeated creation does not duplicate it",()=>{
                int before=GameObject.All.Count(go=>go!=null&&go.GetComponent<Canvas>()!=null);
                var title=Title();
                Assert(GameObject.All.Count(go=>go!=null&&go.GetComponent<Canvas>()!=null)==before+1,
                    "The title created another Canvas beyond its provided Boot fixture.");
                var again=BattleTitleScreen.Create(title.Canvas,title.Status,title.Progress);
                Assert(again==title.View&&title.Canvas.GetComponentsInChildren<BattleTitleScreen>(true).Length==1,
                    "Repeated title setup stacked another application view.");
                Assert(title.View.transform.parent==title.Canvas.transform&&title.View.GetComponentsInChildren<Transform>(true).All(node=>node.gameObject.layer==5),
                    "The title was mounted outside the existing Boot Canvas or lost the UI layer.");
            });
            Case("Loading title keeps an opaque input curtain and reuses the original status and progress",()=>{
                var title=Title();
                Assert(title.Backdrop.sprite==null&&title.Backdrop.color.a==1&&title.Backdrop.raycastTarget,
                    "The loading title still uses a transparent sprite or permits pointer input behind it.");
                Assert(title.Status.transform.parent==title.View.transform&&title.Progress.transform.parent==title.View.transform&&
                    title.Status.gameObject.activeSelf&&title.Progress.gameObject.activeSelf,
                    "Loading cloned or discarded the existing Boot controls.");
                Assert(!title.Progress.interactable&&!title.Progress.handleRect.gameObject.activeSelf&&
                    title.Progress.GetComponentsInChildren<Graphic>(true).All(graphic=>!graphic.raycastTarget),
                    "Decorative loading controls are interactive or intercept the input curtain.");
                Assert(!title.View.transform.Find("StartPrompt").gameObject.activeSelf,
                    "The title offered Start before Main was ready.");
            });
            Case("Title labels retain a Vietnamese-capable font and the actual game version",()=>{
                string original=Application.version;
                try
                {
                    Application.version="0.6.0-test";
                    var title=Title();var labels=title.View.GetComponentsInChildren<Text>(true);
                    Assert(labels.All(label=>label.font==title.Font&&!label.supportRichText&&!label.raycastTarget),
                        "A title label used a missing font, parsed text as markup or intercepted input.");
                    Assert(labels.Count(label=>label.text=="NHẤN ĐỂ BẮT ĐẦU")==1&&labels.Any(label=>label.text=="SPIRITS")&&
                        labels.Any(label=>label.text=="v0.6.0-test")&&labels.Any(label=>label.text.Contains("Dự án cộng đồng độc lập")),
                        "The title omitted its single Vietnamese prompt, Battle Spirits identity, game version or attribution.");
                }
                finally{Application.version=original;}
            });
            Case("Ready title hides loading controls and reveals only the already-selected wallpaper beneath it",()=>{
                var title=Title();var prompt=title.View.transform.Find("StartPrompt").GetComponent<Text>();
                title.View.ShowReady();
                Assert(!title.Status.gameObject.activeSelf&&!title.Progress.gameObject.activeSelf&&prompt.gameObject.activeSelf,
                    "Ready title left the loading controls stacked with its start prompt.");
                title.View.Tick(0.25f);float initial=title.Backdrop.color.a;
                title.View.Tick(0.5f);float ready=title.Backdrop.color.a;
                Assert(initial<1&&ready<initial&&ready>=0.4f&&ready<=0.5f&&title.Backdrop.raycastTarget,
                    "Ready title failed to fade its solid backdrop into a readable scrim over the wallpaper.");
                title.View.ShowReady();title.View.Tick(0);
                Assert(Math.Abs(title.Backdrop.color.a-ready)<0.0001f,
                    "A duplicate ready notification restarted the wallpaper fade.");
            });
            Case("Title reveal fades all title chrome while retaining its pointer curtain until destruction",()=>{
                var title=Title();title.View.ShowReady();title.View.Tick(1);
                var group=title.View.GetComponent<CanvasGroup>();
                title.View.SetRevealProgress(0.5f);
                Assert(Math.Abs(group.alpha-0.5f)<0.0001f&&title.Backdrop.color.a>0&&title.Backdrop.color.a<0.3f&&title.Backdrop.raycastTarget,
                    "Partial reveal did not fade title chrome and scrim together.");
                title.View.SetRevealProgress(1);
                Assert(group.alpha==0&&title.Backdrop.color.a>0&&title.Backdrop.color.a<=0.002f&&title.Backdrop.raycastTarget,
                    "The final retained curtain became fully transparent/cullable before Boot removed it.");
            });
            Case("A title failure replaces the prompt with a stable readable error and opaque backdrop",()=>{
                var title=Title();title.View.ShowReady();title.View.Tick(1);title.View.SetRevealProgress(0.6f);
                const string message="Không thể mở giao diện. Hãy mở lại trò chơi.";
                int count=GameObject.All.Count;
                title.View.ShowFailure(message);title.View.ShowFailure(message);title.View.ShowReady();title.View.Tick(1);
                Assert(title.Backdrop.color.a==1&&title.View.GetComponent<CanvasGroup>().alpha==1&&
                    title.Status.gameObject.activeSelf&&title.Status.text==message&&!title.Progress.gameObject.activeSelf&&
                    !title.View.transform.Find("StartPrompt").gameObject.activeSelf&&!title.View.transform.Find("InputHint").gameObject.activeSelf,
                    "An initialization failure remained faded, hid the status or restored an actionable start prompt.");
                Assert(GameObject.All.Count==count,"Repeated error updates allocated duplicate labels or views.");
            });
            Case("Title animations pulse in unscaled ticks and reject non-finite or negative durations",()=>{
                var title=Title();title.View.ShowReady();var prompt=title.View.transform.Find("StartPrompt").GetComponent<Text>();
                float initial=prompt.color.a;title.View.Tick(0.25f);float animated=prompt.color.a;
                Assert(animated!=initial&&animated>=0.65f&&animated<=1,"A normal unscaled tick did not animate the prompt.");
                float backdrop=title.Backdrop.color.a;
                foreach(float delta in new[]{-1f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})title.View.Tick(delta);
                Assert(prompt.color.a==animated&&title.Backdrop.color.a==backdrop,
                    "Invalid frame time corrupted the title alpha or backdrop.");
                title.View.SetRevealProgress(float.NaN);
                Assert(title.View.GetComponent<CanvasGroup>().alpha==1&&!float.IsNaN(title.Backdrop.color.a),
                    "Invalid reveal progress made the retained title invisible or non-finite.");
            });
            Case("Removing the Boot Canvas releases its title hierarchy and preserves externally owned resources",()=>{
                var title=Title();var owner=title.Canvas.gameObject;
                var outside=new GameObject("SelectedWallpaperOutsideBoot",typeof(RectTransform)).AddComponent<RawImage>();
                var shared=new Texture2D(8,8,TextureFormat.RGBA32,false);outside.texture=shared;
                var children=owner.GetComponentsInChildren<Component>(true);int textures=Texture2D.Created.Count;
                Object.Destroy(owner);
                Assert(children.All(component=>component==null)&&title.View==null&&title.Status==null&&title.Progress==null,
                    "Destroying Boot retained title controls or a duplicate overlay hierarchy.");
                Assert(title.Font!=null&&outside!=null&&outside.texture==shared&&shared!=null&&Texture2D.Created.Count==textures,
                    "Title cleanup destroyed external font/wallpaper ownership or allocated a texture.");
            });
            Console.WriteLine("PASS "+passed+" native production flow scenarios; actual prefab metadata, controlled Unity APIs, no Editor/render/device test.");return 0;
        }
        finally{Cleanup();Directory.Delete(temporary,true);}
    }
}
