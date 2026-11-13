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
    private sealed class FakeSession:IBattleNativeSession,IBattleNativeReplaySession,IBattleNativePortraitSession,IBattleNativeStandeeSession,IBattleNativePresentationSession,IBattleNativeCinematicSession
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
        public readonly Dictionary<int,Rect> ScreenBounds=new Dictionary<int,Rect>();
        public readonly List<int> RevealedModels=new List<int>();
        public readonly Dictionary<string,Texture2D> ArtTextures=new Dictionary<string,Texture2D>();
        public readonly Dictionary<string,Texture2D> StandeeTextures=new Dictionary<string,Texture2D>();
        public bool MissingArtwork;
        public bool MissingStandee;
        public bool IsReplay{get;set;}
        public int ReplayDelta,Hosts,Joins,Starts,ReadyRequests,SoloStarts,Disconnected;
        public string HostName,JoinAddress;public int Port;
        public readonly List<string> RequestedArt=new List<string>();
        public readonly List<string> RequestedPortraits=new List<string>();
        public readonly List<string> RequestedStandees=new List<string>();
        public readonly List<WireMessage> Commands=new List<WireMessage>();
        public DeckDefinition Saved;
        public event Action Changed;
        public event Action<PublicBattleEvent> PresentationEvent;
        public event Action PresentationReset;
        public void SetPresentationBusy(bool busy){if(IsPresentationBusy==busy)return;IsPresentationBusy=busy;Changed?.Invoke();}
        public bool TryGetCardViewport(int id,out Rect bounds)=>Bounds.TryGetValue(id,out bounds);
        public bool TryGetCardScreenViewport(int id,out Rect bounds)=>ScreenBounds.TryGetValue(id,out bounds);
        public void RevealSpiritModel(int id)=>RevealedModels.Add(id);
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
        public void LoadPortrait(string id,RawImage image){RequestedPortraits.Add(id);LoadArt(id,image);}
        public bool HasStandee(string id)=>File.Exists(Path.Combine(sourceRoot,"Picture/Standee",id+".png"));
        public void LoadStandee(string id,RawImage image){RequestedStandees.Add(id);if(MissingStandee){image.texture=null;return;}if(!StandeeTextures.TryGetValue(id,out var texture)||texture==null){texture=MaskTexture("Cutout:"+id);StandeeTextures[id]=texture;}image.texture=texture;}
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
    private static void Pointer(Transform node,UnityEngine.EventSystems.EventTriggerType kind)
    {
        var trigger=node.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        var entry=trigger?.triggers?.SingleOrDefault(item=>item.eventID==kind);
        Assert(entry!=null,"A hand card has no single pointer binding for "+kind);
        entry.callback.Invoke(new UnityEngine.EventSystems.BaseEventData());
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
    private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    private static bool Finite(Vector3 value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z);
    private static float Distance(Vector3 a,Vector3 b)=>(a-b).magnitude;
    private static float[] VectorArray(Vector3 value)=>new[]{value.x,value.y,value.z};
    private static Texture2D MaskTexture(string name="HoledCharacter",int size=24)
    {
        var pixels=new Color32[size*size];
        for(int y=3;y<size-3;y++)for(int x=4;x<size-4;x++)
            if(!(x>=size/2-2&&x<size/2+2&&y>=size/2-2&&y<size/2+2))pixels[y*size+x]=new Color32((byte)(x*9),(byte)(y*9),200,255);
        // A thin blade/feather remains connected to the silhouette.
        for(int x=2;x<size/2;x++)pixels[(size-4)*size+x]=new Color32(220,150,60,255);
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name=name};texture.SetPixels32(pixels);return texture;
    }
    private static Texture2D RealCutout(string id)
    {
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false){name="RealCutout:"+id};
        Assert(texture.LoadImage(File.ReadAllBytes(Path.Combine(sourceRoot,"Picture/Standee",id+".png")),false),"Real transparent cutout failed PNG load.");return texture;
    }
    private static object MeshData(Mesh mesh)=>new {
        vertices=mesh.vertices.Select(VectorArray).ToArray(),normals=mesh.normals.Select(VectorArray).ToArray(),
        uv=mesh.uv.Select(value=>new[]{value.x,value.y}).ToArray(),colors=mesh.colors.Select(value=>new[]{value.r,value.g,value.b,value.a}).ToArray(),
        triangles=mesh.triangles,bounds=new{center=VectorArray(mesh.bounds.center),size=VectorArray(mesh.bounds.size)}
    };
    private static void ExportCharacterStandees(string directory)
    {
        Directory.CreateDirectory(directory);var host=new GameObject("ProductionPaperExport");
        var library=new BattleStandeeLibrary(Resources.Load<Shader>("BattleSpirits/PaperStandee"));var rows=new List<object>();
        try
        {
            foreach(var card in catalog.cards.Where(card=>card.IsSpirit&&File.Exists(Path.Combine(sourceRoot,"Picture/Standee",card.id+".png"))).OrderBy(card=>card.id,StringComparer.Ordinal))
            {
                var texture=RealCutout(card.id);
                // Match BattleFieldVisuals.Spirit's existing card-color accents.
                Color accent=card.color==CardColor.White?new Color(.8f,.92f,1):card.color==CardColor.Blue?new Color(.1f,.5f,1)
                    :card.color==CardColor.Green?new Color(.15f,1,.4f):card.color==CardColor.Purple?new Color(.7f,.25f,1)
                    :card.color==CardColor.Yellow?new Color(1,.78f,.12f):new Color(1,.2f,.12f);
                Assert(library.TryCreate(card.id,texture,host.transform,accent,out var standee),"Could not export real cutout "+card.id);
                rows.Add(new{id=card.id,cutoutPath=Path.Combine(sourceRoot,"Picture/Standee",card.id+".png"),
                    textureWidth=texture.width,textureHeight=texture.height,thickness=BattleStandeeLibrary.Thickness,
                    paper=MeshData(standee.template.mesh),plinth=MeshData(library.baseMesh),paperOffset=VectorArray(standee.paper.localPosition),
                    paperRotation=new[]{standee.paper.localRotation.x,standee.paper.localRotation.y,standee.paper.localRotation.z,standee.paper.localRotation.w},
                    accent=new[]{accent.r,accent.g,accent.b},edgeSegments=standee.template.edgeSegments});
                standee.Dispose();
            }
            string path=Path.Combine(directory,"production-standees.json");
            File.WriteAllText(path,JsonConvert.SerializeObject(new{schema=1,
                source="Production BattleStandeeLibrary geometry and exact cutout paths exported by the controlled native harness",
                coordinates="Unity Y-up, front toward -Z; mesh coordinates and source PNG are unedited",
                unityRenderingVerified=false,standees=rows}));Console.WriteLine("EXPORTED "+rows.Count+" production paper standees: "+path);
        }
        finally{Object.Destroy(host);library.Dispose();}
    }
    private static void ExportSpiritModels(string directory)
    {
        Directory.CreateDirectory(directory);
        var host=new GameObject("ProductionModelExport");
        var library=new BattleSpiritModelLibrary(Resources.Load<Shader>("BattleSpirits/SpiritModel"));
        var rows=new List<object>();
        try
        {
            foreach(string id in BattleSpiritModelLibrary.SupportedIds.OrderBy(id=>id,StringComparer.Ordinal))
            {
                Assert(library.TryCreate(id,host.transform,out var model),"Export could not create production model "+id);
                var mesh=model.Renderer.sharedMesh;
                rows.Add(new {
                    id, profile=model.template.profileName,
                    vertices=mesh.vertices.Select(VectorArray).ToArray(),
                    normals=mesh.normals.Select(VectorArray).ToArray(),
                    colors=mesh.colors.Select(color=>new[]{color.r,color.g,color.b,color.a}).ToArray(),
                    triangles=mesh.triangles,
                    joints=model.template.joints.Select(VectorArray).ToArray(),
                    weights=mesh.boneWeights.Select(weight=>new[]{(float)weight.boneIndex0,weight.weight0,(float)weight.boneIndex1,weight.weight1,(float)weight.boneIndex2,weight.weight2,(float)weight.boneIndex3,weight.weight3}).ToArray(),
                    bounds=new{center=VectorArray(mesh.bounds.center),size=VectorArray(mesh.bounds.size)}
                });
                model.Dispose();
            }
            string path=Path.Combine(directory,"production-models.json");
            File.WriteAllText(path,JsonConvert.SerializeObject(new {
                schema=1,source="Production BattleSpiritModelLibrary sharedMesh exported by the controlled native harness",
                coordinates="Unity Y-up; these are real mesh vertices, not card planes or rendered Unity pixels",
                projectColorSpace=File.ReadAllText(Path.Combine(sourceRoot,"ProjectSettings/ProjectSettings.asset")).Contains("m_ActiveColorSpace: 0")?"Gamma":"Linear",
                unityRenderingVerified=false,models=rows
            }));
            Console.WriteLine("EXPORTED "+rows.Count+" production skinned meshes: "+path);
        }
        finally{Object.Destroy(host);library.Dispose();}
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
        string standeeDirectory=Path.Combine(sourceRoot,"Picture/Standee");
        if(Directory.Exists(standeeDirectory))foreach(string path in Directory.GetFiles(standeeDirectory,"*.png"))
        {string target=Path.Combine(temporary,"Picture/Standee",Path.GetFileName(path));Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(path,target,true);}
        try
        {
            string standeeExport=Environment.GetEnvironmentVariable("BATTLE_SPIRITS_STANDEE_EXPORT");
            if(!string.IsNullOrEmpty(standeeExport))
            {ExportCharacterStandees(standeeExport);if(args.Contains("--export-standees-only"))return 0;}
            string exportDirectory=Environment.GetEnvironmentVariable("BATTLE_SPIRITS_MODEL_EXPORT");
            if(!string.IsNullOrEmpty(exportDirectory))
            {
                ExportSpiritModels(exportDirectory);
                if(args.Contains("--export-models-only"))return 0;
            }
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
                fake.View.players[1].handCount=3;fake.Refresh();Assert(hand.GetComponentsInChildren<RawImage>(true).Count(back=>back.gameObject.activeInHierarchy)==3,"Old opponent backs survive the next snapshot.");
                Assert(fake.RequestedArt.All(id=>own.Contains(id)),"A changed hidden hand requested its cards' faces.");
            });
            Case("Opponent card backs fit inside separate positioned slots instead of stacking their layout anchors",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);fake.View.players[1].handCount=4;
                var screen=Screen(BattleNativeScreen.Duel);var duel=Field<BattleNativeDuelView>(screen,"duel");var hand=Field<RectTransform>(duel,"opponentHand");
                var backs=hand.GetComponentsInChildren<RawImage>(true).Where(back=>back.gameObject.activeInHierarchy).ToArray();Assert(backs.Length==4,"The public opponent hand lost visible backs.");
                float previousRight=-1;
                foreach(var back in backs)
                {
                    var outer=back.transform.parent as RectTransform;var fit=back.GetComponent<AspectRatioFitter>();
                    Assert(outer!=null&&outer.parent==hand&&outer.GetComponent<AspectRatioFitter>()==null&&outer.gameObject.activeInHierarchy
                        &&outer.anchorMin.x>=previousRight&&outer.anchorMax.x>outer.anchorMin.x&&outer.anchorMax.x<=1,
                        "A FitInParent component owns positioned hand anchors or opponent slots overlap.");
                    Assert(fit!=null&&fit.aspectMode==AspectRatioFitter.AspectMode.FitInParent&&back.rectTransform.anchorMin.x==0&&back.rectTransform.anchorMin.y==0
                        &&back.rectTransform.anchorMax.x==1&&back.rectTransform.anchorMax.y==1&&back.texture==BattleTheme.CardBack&&!back.raycastTarget,
                        "Opponent artwork does not fit within its own independent public-back slot.");
                    previousRight=outer.anchorMax.x;
                }
                var saved=fake.View;fake.View=null;fake.Refresh();Assert(backs.All(back=>!back.gameObject.activeInHierarchy),"Disconnected opponent slots remain visible.");
                fake.View=saved;fake.Refresh();Assert(backs.All(back=>back.gameObject.activeInHierarchy),"Re-entering a match leaves pooled back-art children disabled.");
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
            Case("A delayed Magic choice cannot play another card after its inspected hand entry changes",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards),0);fake.View.phase="Main";fake.View.decisionPlayer=0;fake.View.ownHand=new[]{AbilityRegistry.AwakenMagic};
                var screen=Screen(BattleNativeScreen.Duel);var duel=Field<BattleNativeDuelView>(screen,"duel");Call(duel,"PlayHand",0,AbilityRegistry.AwakenMagic);
                var choice=new GameObject("DelayedMagicChoice",typeof(RectTransform));choice.AddComponent<SelectionButton>();choice.AddComponent<TextMeshProUGUI>().text="Không chuyển Core · rút 1 lá";
                UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject=choice;var delayed=MDPro3.UIManager.PopupSelected;
                fake.View.ownHand=new[]{catalog.cards.First(item=>item.IsPlayableFor(BattleRulesFormat.Standard)&&item.IsSpirit).id};delayed.Invoke();
                Assert(fake.Commands.Count==0,"An old Magic popup submitted the replacement hand card at its remembered index.");
                fake.View.ownHand=Array.Empty<string>();delayed.Invoke();Assert(fake.Commands.Count==0,"An old Magic popup submitted an index no longer in the hand.");
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
            Case("Paper library rejects opaque framed art empty alpha and unreadable images",()=>{
                var host=new GameObject("InvalidPaperSources");var library=new BattleStandeeLibrary(Resources.Load<Shader>("BattleSpirits/PaperStandee"));
                var opaque=new Texture2D(24,24,TextureFormat.RGBA32,false);opaque.SetPixels32(Enumerable.Repeat(new Color32(255,255,255,255),24*24).ToArray());var empty=new Texture2D(24,24,TextureFormat.RGBA32,false);empty.SetPixels32(new Color32[24*24]);
                var unreadable=MaskTexture();unreadable.Apply(false,true);
                Assert(!library.TryCreate("framed-card",opaque,host.transform,BattleTheme.Accent,out _)
                    &&!library.TryCreate("empty",empty,host.transform,BattleTheme.Accent,out _)
                    &&!library.TryCreate("gpu-only",unreadable,host.transform,BattleTheme.Accent,out _)
                    &&!library.TryCreate("",MaskTexture(),host.transform,BattleTheme.Accent,out _)
                    &&library.MeshCount==0,"Non-cutout source became a paper character or entered the mesh cache.");
                library.Dispose();
            });
            Case("Paper mesh preserves trimmed artwork UVs holes silhouette thickness and an oval 3D base",()=>{
                var host=new GameObject("HoledPaperGeometry");var texture=MaskTexture();var library=new BattleStandeeLibrary(Resources.Load<Shader>("BattleSpirits/PaperStandee"));
                Assert(library.TryCreate("character-with-hole",texture,host.transform,BattleTheme.Accent,out var standee),"Holed alpha fixture was rejected.");
                var mesh=standee.template.mesh;var pixels=texture.GetPixels32();int size=texture.width;
                int x0=size,y0=size,x1=-1,y1=-1;
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)if(pixels[y*size+x].a>=32){x0=Math.Min(x0,x);x1=Math.Max(x1,x);y0=Math.Min(y0,y);y1=Math.Max(y1,y);}
                Assert(Math.Abs(mesh.uv[0].x-x0/(float)size)<1e-6&&Math.Abs(mesh.uv[0].y-y0/(float)size)<1e-6
                    &&Math.Abs(mesh.uv[2].x-(x1+1)/(float)size)<1e-6&&Math.Abs(mesh.uv[2].y-(y1+1)/(float)size)<1e-6,"Paper face lost source UV alignment while trimming transparent margins.");
                Assert(mesh.uv.Take(4).SequenceEqual(mesh.uv.Skip(4).Take(4))&&mesh.normals.Take(4).All(n=>n.z==-1)&&mesh.normals.Skip(4).Take(4).All(n=>n.z==1),"Physical reverse has another silhouette or incorrect facing.");
                Assert(Math.Abs(mesh.bounds.size.z-BattleStandeeLibrary.Thickness)<1e-6&&mesh.colors.Take(8).All(c=>c.a==1)&&mesh.colors.Skip(8).All(c=>c.a==0),"Paper is a thick sculpted character or lacks distinct alpha-clipped face/stock edge flags.");
                float innerX=standee.template.width*2f/(x1-x0+1),innerY=standee.template.height*(size/2f-y0)/(y1-y0+1);
                Assert(mesh.vertices.Skip(8).Any(p=>Math.Abs(p.x)<innerX+.001f&&Math.Abs(p.y-innerY)<standee.template.height*.15f),"Alpha hole has no physical inner sidewalls; only the outer card rectangle was extruded.");
                foreach(var geometry in new[]{mesh,library.baseMesh})
                {
                    Assert(geometry.vertices.All(Finite)&&geometry.normals.All(n=>Finite(n)&&Math.Abs(n.magnitude-1)<.001f),"Paper/base has invalid positions or normals.");
                    for(int i=0;i<geometry.triangles.Length;i+=3)
                    {int a=geometry.triangles[i],b=geometry.triangles[i+1],c=geometry.triangles[i+2];var u=geometry.vertices[b]-geometry.vertices[a];var v=geometry.vertices[c]-geometry.vertices[a];var n=new Vector3(u.y*v.z-u.z*v.y,u.z*v.x-u.x*v.z,u.x*v.y-u.y*v.x);var avg=geometry.normals[a]+geometry.normals[b]+geometry.normals[c];Assert(n.magnitude>1e-9&&n.x*avg.x+n.y*avg.y+n.z*avg.z>=-1e-8,"Paper/base has a degenerate or inward-wound triangle.");}
                }
                var bounds=library.baseMesh.bounds;Assert(bounds.size.x>bounds.size.z*1.5f&&bounds.size.y>.15f&&bounds.size.y<.25f&&bounds.min.y>=0,"Character base is not a thin physical oval.");
                Assert(standee.root.GetComponentsInChildren<MeshRenderer>(true).Length==2&&standee.root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length==0,"Paper creates toy bones or one renderer per contour segment.");
                standee.Dispose();library.Dispose();
            });
            Case("The generated real cutout retains transparent background and high resolution in production geometry",()=>{
                const string id="26RBS01-064";string source=Path.Combine(sourceRoot,catalog.cards.Single(card=>card.id==id).artPath);byte[] before=SHA256.HashData(File.ReadAllBytes(source));
                var texture=RealCutout(id);var pixels=texture.GetPixels32();int transparent=pixels.Count(p=>p.a==0),visible=pixels.Count(p=>p.a>=32);
                Assert(texture.width>=1000&&texture.height>=1000&&transparent>pixels.Length*.4&&visible>pixels.Length*.1&&visible<pixels.Length*.8
                    &&pixels.Any(p=>p.a>0&&p.a<255),"Actual cutout was reduced, opaque, empty or lost antialiased transparency.");
                var library=new BattleStandeeLibrary(Resources.Load<Shader>("BattleSpirits/PaperStandee"));var host=new GameObject("ActualCharacterPaper");
                Assert(library.TryCreate(id,texture,host.transform,BattleTheme.Accent,out var standee)&&standee.template.texture==texture&&standee.template.edgeSegments>100,"Actual transparent character failed its alpha silhouette mesh.");
                Assert(standee.template.mesh.uv[0].x>0&&standee.template.mesh.uv[2].x<1&&standee.template.mesh.uv[0].y>=0&&standee.template.mesh.uv[2].y<=1,"Actual transparent margins are not trimmed around the visible character.");
                Assert(standee.paperRenderer.propertyBlock.Textures["_MainTex"]==texture&&standee.paperRenderer.propertyBlock.Colors["_BaseColor"].r==1,"Paper recolors or substitutes the actual cutout texture.");
                standee.Dispose();library.Dispose();Assert(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))),"Original full card artwork was rewritten while building its standee.");
            });
            Case("Every available character asset has an exact catalog Spirit ID and valid transparent paper geometry",()=>{
                var paths=Directory.GetFiles(Path.Combine(sourceRoot,"Picture/Standee"),"*.png");Assert(paths.Length>0,"There are no actual character cutouts.");
                var host=new GameObject("AllAuthoredPaperAssets");var library=new BattleStandeeLibrary(Resources.Load<Shader>("BattleSpirits/PaperStandee"));
                foreach(string path in paths)
                {
                    string id=Path.GetFileNameWithoutExtension(path);Assert(catalog.cards.Any(card=>card.id==id&&card.IsSpirit),"Cutout filename has no exact Spirit identity: "+id);
                    var texture=RealCutout(id);var pixels=texture.GetPixels32();
                    Assert(pixels.Any(pixel=>pixel.a==0)&&pixels.Any(pixel=>pixel.a>=32)&&texture.width>=600&&texture.height>=600,"Actual cutout is opaque, empty or reduced below the reviewed art size: "+id);
                    Assert(library.TryCreate(id,texture,host.transform,BattleTheme.Accent,out var standee)&&standee.template.mesh.vertices.All(Finite)
                        &&standee.template.mesh.triangles.Length/3<20000&&standee.template.mesh.bounds.size.z<=.056f,"An actual cutout cannot produce bounded finite physical paper: "+id);
                    standee.Dispose();Object.Destroy(texture);
                }
                Assert(library.MeshCount==paths.Length,"Different authored card identities shared an unrelated silhouette mesh.");library.Dispose();
            });
            Case("Paper instances share exact-ID geometry and material but retain independent tint and borrowed texture ownership",()=>{
                var library=new BattleStandeeLibrary(Resources.Load<Shader>("BattleSpirits/PaperStandee"));var host=new GameObject("SharedPaper");var texture=MaskTexture();
                library.TryCreate("exact-id",texture,host.transform,new Color(.9f,.2f,.3f),out var first);library.TryCreate("exact-id",texture,host.transform,new Color(.2f,.6f,.9f),out var second);
                Assert(first.template==second.template&&first.paperRenderer.sharedMaterial==second.paperRenderer.sharedMaterial&&first.baseRenderer.GetComponent<MeshFilter>().sharedMesh==library.baseMesh&&library.MeshCount==1&&texture.PixelReadCalls==1,"Copies reread alpha or allocate another shared mesh/material.");
                Assert(first.baseRenderer.propertyBlock.Colors["_BaseColor"].r!=second.baseRenderer.propertyBlock.Colors["_BaseColor"].r&&first.paperRenderer.propertyBlock.Textures["_MainTex"]==texture,"Instance tint leaked into shared material or lost the artwork.");
                library.TryCreate("another-id",texture,host.transform,BattleTheme.Accent,out var differentId);var replacement=MaskTexture("Replacement");library.TryCreate("exact-id",replacement,host.transform,BattleTheme.Accent,out var replaced);
                Assert(library.MeshCount==3&&differentId.template!=first.template&&replaced.template!=first.template,"Mesh cache confuses distinct IDs or same-ID replacement textures.");
                var mesh=first.template.mesh;var material=library.Material;first.Dispose();Assert(mesh!=null&&material!=null&&texture!=null&&second.Visible,"Disposing one copy destroyed shared resources used by another.");
                library.Dispose();Assert(mesh==null&&material==null&&texture!=null&&replacement!=null,"Library disposal leaks owned geometry/material or destroys borrowed session textures.");
            });
            Case("Paper motion uses finite lightweight transforms without alpha reads or geometry rebuilding",()=>{
                var library=new BattleStandeeLibrary(Resources.Load<Shader>("BattleSpirits/PaperStandee"));var host=new GameObject("AnimatedPaper");var texture=MaskTexture();library.TryCreate("animated",texture,host.transform,BattleTheme.Accent,out var standee);
                var mesh=standee.template.mesh;int objects=GameObject.All.Count,reads=texture.PixelReadCalls;
                foreach(BattleVisualPose pose in Enum.GetValues(typeof(BattleVisualPose)))foreach(float value in new[]{-1f,0f,.2f,.8f,4f,float.NaN,float.PositiveInfinity})
                {standee.Animate(pose,value,value,-1,true);Assert(Finite(standee.root.transform.localPosition)&&Finite(standee.root.transform.localScale)&&Finite(standee.paper.localPosition),"Paper pose produced nonfinite transforms.");}
                Assert(GameObject.All.Count==objects&&texture.PixelReadCalls==reads&&library.MeshCount==1&&standee.template.mesh==mesh,"Motion rebuilt silhouette geometry/material or reread image pixels.");
                standee.Visible=false;Assert(!standee.paperRenderer.enabled&&!standee.baseRenderer.enabled,"Hidden mode retains half the physical standee.");
                standee.Dispose();standee.Animate(BattleVisualPose.Idle,0,0,1,false);library.Dispose();
            });
            Case("A pathological noisy alpha contour stays within the shared paper geometry budget",()=>{
                const int size=96;var pixels=new Color32[size*size];for(int y=0;y<size;y++)for(int x=0;x<size;x++)if((x+y)%2==0)pixels[y*size+x]=new Color32(255,255,255,255);
                var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);texture.SetPixels32(pixels);var library=new BattleStandeeLibrary(Resources.Load<Shader>("BattleSpirits/PaperStandee"));var host=new GameObject("NoisyPaper");
                Assert(library.TryCreate("noisy",texture,host.transform,BattleTheme.Accent,out var standee)&&standee.template.mesh.vertices.Length<65535&&standee.template.mesh.triangles.Length/3<20000,"Noisy alpha overflowed 16-bit indices or the paper triangle budget.");library.Dispose();
            });
            Case("Actual session separates readable paper PNGs from GPU-only faces and invalidates stale cutout targets",()=>{
                const string id="26RBS01-064";var session=BattleNativeSession.Ensure();var face=new GameObject("PaperFullFace",typeof(RectTransform)).AddComponent<RawImage>();var image=new GameObject("ActualPaperCutout",typeof(RectTransform)).AddComponent<RawImage>();
                Assert(session.HasStandee(id)&&!session.HasStandee("../26RBS01-064")&&!session.HasStandee("unknown"),"Standee availability accepts unrelated or escaping IDs.");session.LoadArt(id,face);session.LoadStandee(id,image);Pump(()=>face.texture!=null&&face.texture!=Texture2D.blackTexture&&image.texture!=null&&image.texture!=Texture2D.blackTexture);
                var cutout=(Texture2D)image.texture;var textures=Field<Dictionary<string,Texture2D>>(session,"textures");
                Assert(((Texture2D)face.texture).isReadable==false&&cutout.isReadable&&face.texture!=cutout&&textures[id]==face.texture&&textures["standee:"+id]==cutout&&cutout.GetPixels32().Any(pixel=>pixel.a==0),"Readable alpha cutout cache aliases the full card face.");
                var target=new GameObject("SupersededCutoutTarget",typeof(RectTransform)).AddComponent<RawImage>();
                string delayedId=catalog.cards.Where(card=>card.IsSpirit&&card.id!=id&&session.HasStandee(card.id)).Select(card=>card.id).FirstOrDefault();
                session.LoadStandee(delayedId??id,target);session.LoadStandee("unknown",target);
                if(delayedId!=null)Pump(()=>textures.ContainsKey("standee:"+delayedId));else Context.Drain();
                Assert(target.texture==null&&Field<Dictionary<RawImage,string>>(session,"requestedArt")[target]=="standee:unknown","A missing newer cutout selection retains the previous character.");
                Object.Destroy(face.gameObject);Object.Destroy(image.gameObject);Object.Destroy(target.gameObject);
            });
            Case("Real model profiles cover exactly the nineteen playable Standard Spirits without claiming other cards",()=>{
                var expected=catalog.cards.Where(card=>card.IsPlayableFor(BattleRulesFormat.Standard)&&card.IsSpirit).Select(card=>card.id).OrderBy(id=>id).ToArray();
                Assert(expected.Length==19&&BattleSpiritModelLibrary.SupportedIds.OrderBy(id=>id).SequenceEqual(expected),"Model profiles are missing a playable Spirit or substitute an unsupported identity.");
                var library=new BattleSpiritModelLibrary(Resources.Load<Shader>("BattleSpirits/SpiritModel"));var host=new GameObject("ModelCatalog");
                Assert(!library.TryCreate("BS01-X01",host.transform,out _)&&!library.TryCreate("unknown-card",host.transform,out _)
                    &&library.MeshCount==0,"Unmapped artwork acquired a generic model or allocated a cached mesh.");library.Dispose();
            });
            Case("Production model meshes are volumetric bounded finite and correctly wound with valid bone weights",()=>{
                var library=new BattleSpiritModelLibrary(Resources.Load<Shader>("BattleSpirits/SpiritModel"));var host=new GameObject("ModelGeometry");var signatures=new HashSet<string>();
                foreach(string id in BattleSpiritModelLibrary.SupportedIds)
                {
                    Assert(library.TryCreate(id,host.transform,out var model),"Model could not be built: "+id);var mesh=model.Renderer.sharedMesh;
                    Assert(mesh.vertices.Length>1000&&mesh.triangles.Length/3<12000&&mesh.normals.Length==mesh.vertices.Length
                        &&mesh.colors.Length==mesh.vertices.Length&&mesh.boneWeights.Length==mesh.vertices.Length,"A model is a plane, exceeds its geometry budget or lacks normal/color/skin streams: "+id);
                    Assert(mesh.bounds.size.x>0.5f&&mesh.bounds.size.y>.5f&&mesh.bounds.size.z>.4f&&mesh.bounds.size.x<=2.801f
                        &&mesh.bounds.size.y<=3.601f&&mesh.bounds.size.z<=2.801f&&mesh.bounds.min.y>=-.001f,"Model bounds leave the field budget: "+id);
                    Assert(mesh.vertices.All(Finite)&&mesh.normals.All(normal=>Finite(normal)&&Math.Abs(normal.magnitude-1)<.001f),"A model has nonfinite vertices or invalid normals: "+id);
                    foreach(var weight in mesh.boneWeights)Assert(weight.boneIndex0>=0&&weight.boneIndex0<10&&weight.weight0==1
                        &&weight.weight1==0&&weight.weight2==0&&weight.weight3==0,"A vertex lacks its normalized valid rig joint: "+id);
                    for(int i=0;i<mesh.triangles.Length;i+=3)
                    {
                        int a=mesh.triangles[i],b=mesh.triangles[i+1],c=mesh.triangles[i+2];
                        Assert(a>=0&&b>=0&&c>=0&&a<mesh.vertices.Length&&b<mesh.vertices.Length&&c<mesh.vertices.Length,"Model face references an invalid vertex: "+id);
                        var u=mesh.vertices[b]-mesh.vertices[a];var v=mesh.vertices[c]-mesh.vertices[a];
                        var n=new Vector3(u.y*v.z-u.z*v.y,u.z*v.x-u.x*v.z,u.x*v.y-u.y*v.x);var average=mesh.normals[a]+mesh.normals[b]+mesh.normals[c];
                        Assert(n.magnitude>1e-8f&&n.x*average.x+n.y*average.y+n.z*average.z>=-1e-8f,"Degenerate or inward-facing model triangle: "+id);
                    }
                    Assert(model.root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length==1&&model.root.GetComponentsInChildren<Collider>(true).Length==0,"Model creates per-part renderers or unwanted colliders.");
                    signatures.Add(Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new{mesh.vertices,mesh.colors,mesh.triangles})))));model.Dispose();
                }
                Assert(signatures.Count==19,"Distinct card IDs were assigned identical generic monster geometry.");library.Dispose();
            });
            Case("Skinned rest bind poses retain the model under nontrivial parent translation rotation and scale",()=>{
                var host=new GameObject("TransformedModelHost");host.transform.localPosition=new Vector3(27,4,-19);
                host.transform.localRotation=Quaternion.Euler(13,31,-7);host.transform.localScale=new Vector3(1.6f,.8f,1.3f);
                var library=new BattleSpiritModelLibrary(Resources.Load<Shader>("BattleSpirits/SpiritModel"));
                foreach(string id in BattleSpiritModelLibrary.SupportedIds)
                {
                    library.TryCreate(id,host.transform,out var model);var mesh=model.Renderer.sharedMesh;
                    Assert(mesh.bindposes.Length==10&&model.Renderer.bones.Length==10,"Model rig and bind pose counts differ.");
                    for(int i=0;i<mesh.vertices.Length;i+=23)
                    {
                        int joint=mesh.boneWeights[i].boneIndex0;var matrix=model.Renderer.bones[joint].localToWorldMatrix*mesh.bindposes[joint];
                        var skinned=matrix.MultiplyPoint3x4(mesh.vertices[i]);var expected=model.root.transform.TransformPoint(mesh.vertices[i]);
                        Assert(Distance(skinned,expected)<.002f,"The initial rig collapses or moves vertices under a transformed parent: "+id);
                    }
                    model.Dispose();
                }
                library.Dispose();
            });
            Case("Copies share one model mesh and material while instance disposal preserves other rigs",()=>{
                var library=new BattleSpiritModelLibrary(Resources.Load<Shader>("BattleSpirits/SpiritModel"));var host=new GameObject("SharedModels");string id=BattleSpiritModelLibrary.SupportedIds[0];
                library.TryCreate(id,host.transform,out var first);library.TryCreate(id,host.transform,out var second);
                var mesh=first.Renderer.sharedMesh;var material=first.Renderer.sharedMaterial;
                Assert(second.Renderer.sharedMesh==mesh&&second.Renderer.sharedMaterial==material&&first.Renderer.bones[0]!=second.Renderer.bones[0]
                    &&library.MeshCount==1,"Copies duplicate meshes/materials or incorrectly share animated transforms.");
                first.Dispose();Assert(mesh!=null&&material!=null&&second.root!=null,"Removing one model destroyed shared resources.");
                second.Animate(BattleVisualPose.Attack,.2f,1,1,false);second.Dispose();library.Dispose();
                Assert(mesh==null&&material==null,"Field library leaked its owned mesh or material.");
            });
            Case("All model poses sanitize numeric extremes without creating new rigs or replacing geometry",()=>{
                var library=new BattleSpiritModelLibrary(Resources.Load<Shader>("BattleSpirits/SpiritModel"));var host=new GameObject("AnimatedModels");
                foreach(string id in BattleSpiritModelLibrary.SupportedIds)
                {
                    library.TryCreate(id,host.transform,out var model);int objects=GameObject.All.Count;var mesh=model.Renderer.sharedMesh;var material=model.Renderer.sharedMaterial;
                    foreach(BattleVisualPose pose in Enum.GetValues(typeof(BattleVisualPose)))foreach(float time in new[]{0f,.3f,2f,-1f,float.MaxValue,float.NaN,float.PositiveInfinity})
                    {
                        model.Animate(pose,time,time,-1,true);
                        Assert(Finite(model.root.transform.localPosition)&&Finite(model.root.transform.localScale)&&model.root.transform.localScale.x>0,"Model animation generated an invalid transform.");
                        foreach(var joint in model.bones)Assert(Finite(joint.localRotation.x)&&Finite(joint.localRotation.y)&&Finite(joint.localRotation.z)&&Finite(joint.localRotation.w),"Model bone rotation is nonfinite.");
                        Assert(model.Renderer.propertyBlock.Floats.Values.All(Finite),"Model shader properties are nonfinite.");
                    }
                    Assert(GameObject.All.Count==objects&&model.Renderer.sharedMesh==mesh&&model.Renderer.sharedMaterial==material,"An animation tick rebuilds rig/mesh/material resources.");model.Dispose();
                }
                library.Dispose();
            });
            Case("Paper and artwork modes switch cached geometry without changing public state or borrowed textures",()=>{
                var output=new GameObject("PaperField",typeof(RectTransform)).AddComponent<RawImage>();var field=output.gameObject.AddComponent<BattleFieldVisuals>();field.SetArtworkProvider(fake.LoadArt);field.SetStandeeProvider(fake.LoadStandee);field.SetCatalog(catalog.cards);field.Load(output);
                const string id="26RBS01-064";var snapshot=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);
                snapshot.players[0].field=new[]{new SpiritView{instanceId=501,cardId=id}};field.Render(snapshot);Time.unscaledTime=2;Call(field,"Update");
                var units=(System.Collections.IDictionary)Field<object>(field,"units");var figure=Field<BattleCardFigure>(units[501],"figure");var standee=Field<BattleCharacterStandee>(figure,"standee");
                Assert(standee!=null&&standee.Visible&&!Field<Renderer>(figure,"illustrationRenderer").enabled&&Field<Renderer>(figure,"cardRenderer").enabled,"Default paper mode does not show the actual cutout with its floor card.");
                Assert(standee.root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length==0,"Default presentation creates a rounded skinned replacement character.");
                int objects=GameObject.All.Count,requests=fake.RequestedArt.Count,cutoutRequests=fake.RequestedStandees.Count;string state=JsonConvert.SerializeObject(snapshot);var mesh=standee.template.mesh;var borrowed=standee.template.texture;
                MDPro3.Config.Set(BattleTheme.StandeeModeConfig,"0");Call(field,"Update");
                Assert(!standee.Visible&&Field<Renderer>(figure,"illustrationRenderer").enabled,"Explicit artwork mode did not restore the old floating illustration.");
                MDPro3.Config.Set(BattleTheme.StandeeModeConfig,"1");Call(field,"Update");
                Assert(standee.Visible&&standee.template.mesh==mesh&&GameObject.All.Count==objects&&fake.RequestedArt.Count==requests&&fake.RequestedStandees.Count==cutoutRequests
                    &&JsonConvert.SerializeObject(snapshot)==state,"A cosmetic mode recreated geometry, reloaded artwork or changed public state.");
                Assert(field.RevealSpiritModel(501),"An active paper character cannot reveal after the card cinematic.");var floor=Field<Transform>(figure,"card");var scale=floor.localScale;
                Call(field,"Update");Assert(floor.localScale.x==scale.x&&floor.localScale.y==scale.y,"Standee reveal shrank or lifted the already-landed printed card.");
                field.Dispose();Assert(mesh==null&&borrowed!=null,"Field disposal leaked owned paper geometry or destroyed borrowed cutout artwork.");
            });
            Case("Cutout loading is independent from floor art and missing paper stays a printed card",()=>{
                fake.MissingArtwork=true;var output=new GameObject("PaperMissingArt",typeof(RectTransform)).AddComponent<RawImage>();var field=output.gameObject.AddComponent<BattleFieldVisuals>();field.SetArtworkProvider(fake.LoadArt);field.SetStandeeProvider(fake.LoadStandee);field.SetCatalog(catalog.cards);field.Load(output);
                const string id="26RBS01-064";var snapshot=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);
                snapshot.players[0].field=new[]{new SpiritView{instanceId=601,cardId=id}};field.Render(snapshot);var units=(System.Collections.IDictionary)Field<object>(field,"units");
                var mapped=Field<BattleCardFigure>(units[601],"figure");Assert(Field<BattleCharacterStandee>(mapped,"standee")?.Visible==true,"A floor-art delay unnecessarily hides the independent cutout.");
                fake.MissingArtwork=false;fake.MissingStandee=true;snapshot.players[0].field=new[]{new SpiritView{instanceId=602,cardId="BS01-X01"}};field.Render(snapshot);
                var missing=Field<BattleCardFigure>(units[602],"figure");Assert(Field<BattleCharacterStandee>(missing,"standee")==null&&!Field<Renderer>(missing,"illustrationRenderer").enabled&&Field<Renderer>(missing,"cardRenderer").enabled,"Missing cutout shows a framed rectangular character or rounded substitute.");
                int requests=fake.RequestedStandees.Count;for(int i=0;i<20;i++)Call(field,"Update");Assert(fake.RequestedStandees.Count==requests,"Missing cutout polls the filesystem every frame.");
                MDPro3.Config.Set(BattleTheme.StandeeModeConfig,"0");Call(field,"Update");Assert(Field<Renderer>(missing,"illustrationRenderer").enabled,"Artwork mode cannot explicitly opt into the old illustration.");field.Dispose();
            });
            Case("The field builds paper only for public units and never requests hidden hand identities",()=>{
                var output=new GameObject("PublicOnlyPaper",typeof(RectTransform)).AddComponent<RawImage>();var field=output.gameObject.AddComponent<BattleFieldVisuals>();field.SetArtworkProvider(fake.LoadArt);field.SetStandeeProvider(fake.LoadStandee);field.SetCatalog(catalog.cards);field.Load(output);
                var snapshot=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);const string id="26RBS01-064";
                snapshot.ownHand=new[]{"not-a-public-field-card"};snapshot.players[1].handCount=9;snapshot.players[1].field=new[]{new SpiritView{instanceId=603,cardId=id}};field.Render(snapshot);
                Assert(Field<BattleStandeeLibrary>(field,"standeeLibrary").MeshCount==1&&fake.RequestedArt.All(card=>card==id)&&fake.RequestedStandees.SequenceEqual(new[]{id}),"Paper meshes or cutout requests depend on private hand identities.");field.Dispose();
            });
            Case("Duel visual controls persist cosmetic choices and block hidden modal or presentation callbacks",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);var screen=Screen(BattleNativeScreen.Duel);
                string state=JsonConvert.SerializeObject(fake.View);Click(screen.transform,"BS_ModelMode");Click(screen.transform,"BS_DuelQuality");
                Assert(MDPro3.Config.Get(BattleTheme.StandeeModeConfig,"1")=="0"&&MDPro3.Config.Get(BattleTheme.DuelQualityConfig,"Balanced")=="High"
                    &&fake.Commands.Count==0&&JsonConvert.SerializeObject(fake.View)==state,"Visual settings issue gameplay commands or fail to persist.");
                var mode=Find(screen.transform,"BS_ModelMode").GetComponent<Button>();var quality=Find(screen.transform,"BS_DuelQuality").GetComponent<Button>();
                Click(screen.transform,"BS_Hand/BS_Card_"+fake.View.ownHand[0]);Click(screen.transform,"BS_InspectZoom");mode.onClick.Invoke();quality.onClick.Invoke();
                Assert(MDPro3.Config.Get(BattleTheme.StandeeModeConfig,"1")=="0"&&MDPro3.Config.Get(BattleTheme.DuelQualityConfig,"Balanced")=="High","Modal settings callbacks remain active behind zoom.");
                Click(screen.transform,"BS_ZoomClose");fake.SetPresentationBusy(true);mode.onClick.Invoke();quality.onClick.Invoke();
                Assert(MDPro3.Config.Get(BattleTheme.StandeeModeConfig,"1")=="0"&&MDPro3.Config.Get(BattleTheme.DuelQualityConfig,"Balanced")=="High","Presentation guard permits disruptive cosmetic changes.");
            });
            Case("Low balanced and high render budgets resize once preserve aspect and release old targets",()=>{
                var output=new GameObject("QualityBudgetField",typeof(RectTransform)).AddComponent<RawImage>();output.rectTransform.rect=new Rect(0,0,3200,1800);
                var field=output.gameObject.AddComponent<BattleFieldVisuals>();field.Load(output);Assert(output.texture.width==1920,"Balanced quality is not the default bounded render budget.");
                foreach(var pair in new[]{new KeyValuePair<string,int>("Low",1280),new KeyValuePair<string,int>("High",2560),new KeyValuePair<string,int>("Balanced",1920)})
                {
                    var old=(RenderTexture)output.texture;MDPro3.Config.Set(BattleTheme.DuelQualityConfig,pair.Key);Call(field,"Update");Time.unscaledTime+=.13f;Call(field,"Update");
                    var current=(RenderTexture)output.texture;Assert(current.width==pair.Value&&Math.Abs(current.width/(float)current.height-16f/9)<.005f&&old.released&&old==null,"Quality resize loses its budget/aspect or leaks its previous target.");
                    Call(field,"Update");Assert(output.texture==current,"A stable quality setting recreates the target every frame.");
                }
                output.rectTransform.rect=new Rect(0,0,912,518);var sizes=new List<int>();
                foreach(string quality in new[]{"Low","Balanced","High"})
                { MDPro3.Config.Set(BattleTheme.DuelQualityConfig,quality);Call(field,"Update");Time.unscaledTime+=.13f;Call(field,"Update");sizes.Add(output.texture.width); }
                Assert(sizes[0]<sizes[1]&&sizes[1]<sizes[2],"Quality choices do nothing on a normal-sized window below all caps.");
                field.Dispose();
            });
            Case("Cinematic card landing uses the screen-mapped field rectangle and reveals a model only once",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);var screen=Screen(BattleNativeScreen.Duel);
                string id=BattleSpiritModelLibrary.SupportedIds[0];fake.Bounds[701]=new Rect(.2f,.3f,.1f,.1f);fake.ScreenBounds[701]=new Rect(.392f,.396f,.076f,.072f);
                var e=new PublicBattleEvent{kind="Summoned",actor=0,spiritId=701,cardId=id};fake.Emit(e);fake.Emit(e);
                for(int i=0;i<7;i++)TickPresentation(.25f);var presenter=Field<BattleSummonPresentation>(Field<BattleNativeDuelView>(screen,"duel"),"summonPresentation");
                var slot=Field<RectTransform>(presenter,"cardSlot");float landing=BattleSummonMotion.Sample(1.75f).landing;
                float x=.31f+(.392f+.076f*.5f-.31f)*landing;float y=.53f+(.396f+.072f*.5f-.53f)*landing;
                Assert(Math.Abs((slot.anchorMin.x+slot.anchorMax.x)*.5f-x)<.0001f&&Math.Abs((slot.anchorMin.y+slot.anchorMax.y)*.5f-y)<.0001f,"Card cinematic lands in field-local coordinates rather than the visible root.");
                for(int i=0;i<5;i++)TickPresentation(.25f);Assert(fake.RevealedModels.SequenceEqual(new[]{701})&&fake.Commands.Count==0,"Model reveal duplicates public events or issues a gameplay command.");
            });
            Case("Actual session maps viewport through its field anchors and model reveal does not alter the snapshot",()=>{
                var session=BattleNativeSession.Ensure();var output=new GameObject("CinematicMappedField",typeof(RectTransform)).AddComponent<RawImage>();
                BattleNativeWidgets.Place(output.rectTransform,.24f,.18f,1,.90f);output.rectTransform.rect=new Rect(0,0,1459,778);
                var field=output.gameObject.AddComponent<BattleFieldVisuals>();field.SetArtworkProvider(fake.LoadArt);field.SetCatalog(catalog.cards);field.Load(output);
                var snapshot=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);snapshot.players[0].field=new[]{new SpiritView{instanceId=702,cardId=BattleSpiritModelLibrary.SupportedIds[0]}};
                Field<List<BattleFieldVisuals>>(session,"fields").Add(field);Call(session,"ApplyView",snapshot,false);string state=JsonConvert.SerializeObject(snapshot);
                Assert(field.TryGetCardViewport(702,out var local)&&session.TryGetCardScreenViewport(702,out var mapped)
                    &&Math.Abs(mapped.x-(.24f+local.x*.76f))<.0001f&&Math.Abs(mapped.y-(.18f+local.y*.72f))<.0001f
                    &&Math.Abs(mapped.width-local.width*.76f)<.0001f,"Production session did not convert the native field viewport to screen coordinates.");
                session.RevealSpiritModel(702);Assert(JsonConvert.SerializeObject(snapshot)==state,"A purely visual reveal mutates the authoritative view.");field.Dispose();
            });
            Case("Duel status occupies the sidebar footer and does not overlay the hand faces",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);var screen=Screen(BattleNativeScreen.Duel);
                var status=(RectTransform)Find(screen.transform,"BS_Status");var hand=(RectTransform)Find(screen.transform,"BS_Hand");
                Assert(status.anchorMax.x<hand.anchorMin.x&&status.anchorMin.y>=0&&status.anchorMax.y<=.06f,"Long status text still draws across the player's hand.");
            });
            Case("Field BP is emphasized over compact Lv and Core while the complete state remains in inspection",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);
                fake.View.players[0].field=new[]{new SpiritView{instanceId=703,cardId=BattleSpiritModelLibrary.SupportedIds[0],bp=7000,level=2,normalCores=3,soulCore=true,exhausted=true}};
                var screen=Screen(BattleNativeScreen.Duel);var card=Find(screen.transform,"BS_FieldCard_703");var stats=Find(card,"BS_Stats").GetComponent<TextMeshProUGUI>();
                Assert(stats.richText&&stats.text.Contains("<b>7000</b> BP")&&stats.text.Contains("Lv2")&&stats.text.Contains("C3")&&stats.text.Contains("◆")
                    &&((RectTransform)stats.transform.parent).anchorMax.y>=.5f,"Field stats still bury BP inside a tiny two-line strip.");
                Click(screen.transform,"BS_FieldCard_703");var detail=Find(screen.transform,"BS_InspectState").GetComponent<TextMeshProUGUI>();
                Assert(detail.text.Contains("7000")&&detail.text.Contains("Soul Core")&&detail.text.Contains("ĐÃ MỆT"),"Compact field stats removed complete inspection information.");
            });
            Case("Animation gallery lists existing transparent cutouts and opens a command-free paper preview",()=>{
                MDPro3.Config.Set(BattleTheme.StandeeModeConfig,"0");
                BattleSpiritsApp.ShowBossGallery();const string id="26RBS01-064";
                string choice=MDPro3.UIManager.Popup.Single(item=>item.StartsWith("GIẤY · "+id+" · ",StringComparison.Ordinal));
                var session=BattleNativeSession.Ensure();int available=catalog.cards.Count(card=>card.IsSpirit&&session.HasStandee(card.id));
                Assert(MDPro3.UIManager.Popup.Count(item=>item.StartsWith("GIẤY · ",StringComparison.Ordinal))==available,"Gallery claims paper models for missing or unrelated cutout images.");
                var selected=new GameObject("PreviewPaperChoice",typeof(RectTransform));selected.AddComponent<SelectionButton>();selected.AddComponent<TextMeshProUGUI>().text=choice;
                UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject=selected;MDPro3.UIManager.PopupSelected.Invoke();
                Assert(session.IsPreview&&!session.CanSendCommands&&session.View.rulesFormat=="Standard"&&session.View.players[0].field.Single().cardId==id
                    &&MDPro3.Config.Get(BattleTheme.StandeeModeConfig,"0")=="1",
                    "Paper preview was rejected or entered authoritative gameplay.");
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
            Case("Duel inspection repairs the actual offscreen native window and shows a full card with readable metadata",()=>{
                var imported=Fixture("OcgCoreUI");var hidden=(RectTransform)Find(imported.transform,"CardDescription/Window");
                Assert(hidden.anchoredPosition.x< -400,"The native fixture lost the inspector's actual offscreen starting position.");Object.Destroy(imported);
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);
                var card=catalog.cards.First(item=>item.IsPlayableFor(BattleRulesFormat.Standard));
                fake.View.players[0].field=new[]{new SpiritView{instanceId=70,cardId=card.id,normalCores=2,level=2,bp=6000}};
                var screen=Screen(BattleNativeScreen.Duel);Click(screen.transform,"BS_FieldCard_70");
                var window=(RectTransform)Find(screen.transform,"CardDescription/Window");
                Assert(window.gameObject.activeInHierarchy&&window.anchorMin.x>=0&&window.anchorMin.y>=0&&window.anchorMax.x<=.24f&&window.anchorMax.y<=1
                    &&window.anchorMax.x-window.anchorMin.x>=.20f&&window.anchorMax.y-window.anchorMin.y>=.8f
                    &&Math.Abs(window.offsetMin.x)<.01f&&Math.Abs(window.offsetMax.x)<.01f,"Native inspector remains outside the visible left rail or retains its hidden pixel offsets.");
                var image=Find(window,"Card").GetComponent<RawImage>();
                Assert(image!=null&&image.texture==fake.ArtTextures[card.id]&&image.uvRect.width==1&&image.uvRect.height==1
                    &&image.GetComponent<AspectRatioFitter>()?.aspectMode==AspectRatioFitter.AspectMode.FitInParent
                    &&fake.RequestedPortraits.Count==0,"Inspection must display the complete original card, not an artwork-only portrait or cropped face.");
                var name=Find(window,"BS_InspectName")?.GetComponent<TextMeshProUGUI>();
                var meta=Find(window,"BS_InspectMeta")?.GetComponent<TextMeshProUGUI>();
                Assert(name?.text==card.DisplayName&&meta!=null&&meta.text.Contains(card.id)&&meta.color.a>.9f&&!name.raycastTarget&&!meta.raycastTarget,
                    "Card identity, readable metadata or nonblocking labels are missing from inspection.");
                var scroll=Find(window,"Description Scroll View").GetComponent<ScrollRect>();
                Assert(scroll!=null&&scroll.enabled&&scroll.vertical&&!scroll.horizontal&&scroll.viewport!=null&&scroll.content!=null
                    &&scroll.viewport.anchorMax.x-scroll.viewport.anchorMin.x>.9f
                    &&scroll.viewport.anchorMax.y-scroll.viewport.anchorMin.y>.9f,"The original zero-size inspector viewport was not normalized into a scrollable region.");
                Assert(screen.GetComponentsInChildren<Canvas>(true).Length==0,"Inspection created another Canvas over the native UI.");
            });
            Case("Native duel panel tints the actual white alpha mask dark and keeps inspection labels readable",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);
                string id=catalog.cards.First(item=>item.IsPlayableFor(BattleRulesFormat.Standard)).id;fake.View.ownHand=new[]{id};
                var screen=Screen(BattleNativeScreen.Duel);Click(screen.transform,"BS_Hand/BS_Card_"+id);
                var window=Find(screen.transform,"CardDescription/Window");var face=window.GetComponent<Image>();
                Assert(face.sprite==BattleTheme.PanelSprite&&face.sprite.texture==Resources.Load<Texture2D>("BattleSpirits/Skin/ChromePanel")
                    &&face.color.a>=.9f&&Luminance(face.color)<.025,
                    "The reviewed white alpha-mask panel is still rendered with a white or translucent background.");
                foreach(string name in new[]{"BS_InspectName","BS_InspectMeta","BS_InspectState","Description Scroll View/Viewport/Content"})
                {
                    var label=Find(window,name)?.GetComponent<TextMeshProUGUI>();
                    Assert(label!=null&&label.gameObject.activeInHierarchy&&label.color.a>.9f&&Contrast(label.color,face.color)>=4.5,
                        "Inspector foreground loses declared contrast against its actual panel tint: "+name);
                }
                var button=Find(window,"BS_InspectZoom").GetComponent<Image>();
                Assert(button.sprite==BattleTheme.ButtonSprite&&button.color.r==1&&button.color.g==1&&button.color.b==1,
                    "Fixing the white panel also dark-tinted the original black-centered button artwork.");
            });
            Case("Duel hand uses complete card faces in a restrained fan and selected card is raised above its siblings",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);
                fake.View.ownHand=catalog.cards.Where(item=>item.IsPlayableFor(BattleRulesFormat.Standard)).Take(4).Select(item=>item.id).ToArray();
                var screen=Screen(BattleNativeScreen.Duel);var duel=Field<BattleNativeDuelView>(screen,"duel");var hand=Field<RectTransform>(duel,"hand");
                hand.rect=new Rect(0,0,1000,240);Call(duel,"Tick");
                var slots=Field<List<BattleNativeWidgets.CardSlot>>(duel,"handSlots");
                foreach(var slot in slots)
                {
                    var rect=(RectTransform)slot.Button.transform;var area=(RectTransform)Find(rect,"BS_CardArtworkSlot");
                    var visual=(RectTransform)Find(rect,"BS_HandVisual");
                    Assert(!Find(rect,"BS_CardCode").gameObject.activeSelf&&area.anchorMin.y==0&&area.anchorMax.y==1,
                        "A duel hand still reserves a tiny unreadable ID footer instead of the entire face.");
                    Assert(rect.anchorMin.x>=-.001f&&rect.anchorMax.x<=1.001f&&visual!=null&&Math.Abs(visual.localEulerAngles.z)<=10,
                        "The hand fan clips a whole slot horizontally or turns its official face too far.");
                }
                Assert(Math.Abs(Find(slots[0].Button.transform,"BS_HandVisual").localEulerAngles.z)>.1f
                    &&Math.Abs(Find(slots[3].Button.transform,"BS_HandVisual").localEulerAngles.z)>.1f,"The hand is still a rigid horizontal thumbnail grid.");
                var selected=slots[1];Click(screen.transform,"BS_Hand/BS_Card_"+selected.Id);var selectedRect=(RectTransform)Find(selected.Button.transform,"BS_HandVisual");
                Assert(selectedRect.anchorMin.y>((RectTransform)Find(slots[0].Button.transform,"BS_HandVisual")).anchorMin.y
                    &&selectedRect.localScale.x>1&&Math.Abs(selectedRect.localEulerAngles.z)<.001f
                    &&hand.GetChild(hand.childCount-1)==selected.Button.transform&&fake.Commands.Count==0,
                    "Selected fan card is not raised, straightened and placed in front without playing it.");
            });
            Case("Hand hover raises only the current pooled slot and never changes inspection selection or sends a command",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);fake.View.phase="Main";fake.View.decisionPlayer=0;
                fake.View.ownHand=catalog.cards.Where(item=>item.IsPlayableFor(BattleRulesFormat.Standard)&&item.IsSpirit).Take(3).Select(item=>item.id).ToArray();
                var screen=Screen(BattleNativeScreen.Duel);var duel=Field<BattleNativeDuelView>(screen,"duel");
                var hand=Field<RectTransform>(duel,"hand");hand.rect=new Rect(0,0,1000,240);Call(duel,"Tick");
                var slots=Field<List<BattleNativeWidgets.CardSlot>>(duel,"handSlots");Click(screen.transform,"BS_Hand/BS_Card_"+slots[0].Id);
                var first=slots[0].Button.transform;var hovered=slots[2].Button.transform;
                var visual=(RectTransform)Find(hovered,"BS_HandVisual");
                float baseY=visual.anchorMin.y;string title=Find(screen.transform,"BS_InspectName").GetComponent<TextMeshProUGUI>().text;
                int objects=GameObject.All.Count,requests=fake.RequestedArt.Count;
                Pointer(hovered,UnityEngine.EventSystems.EventTriggerType.PointerEnter);
                Assert(visual.anchorMin.y>baseY&&visual.localScale.x>1&&hand.GetChild(hand.childCount-1)==hovered
                    &&Field<int>(duel,"selectedHand")==0&&Find(screen.transform,"BS_InspectName").GetComponent<TextMeshProUGUI>().text==title
                    &&fake.Commands.Count==0&&fake.RequestedArt.Count==requests&&GameObject.All.Count==objects,
                    "Hover rebuilt artwork, changed the selected identity, played a card or failed to raise the intended slot.");
                Pointer(hovered,UnityEngine.EventSystems.EventTriggerType.PointerExit);
                Assert(Math.Abs(visual.anchorMin.y-baseY)<.001f&&visual.localScale.x==1&&hand.GetChild(hand.childCount-1)==first,
                    "Pointer exit failed to restore the selected card above its resting hand.");
                fake.View.ownHand=fake.View.ownHand.Reverse().ToArray();fake.Refresh();
                Pointer(slots[1].Button.transform,UnityEngine.EventSystems.EventTriggerType.PointerEnter);
                Assert(Field<int>(duel,"hoveredHand")==1&&fake.Commands.Count==0,"Rebound hover retains a previous slot's logical index.");
                slots[1].Button.onClick.Invoke();Click(screen.transform,"BS_InspectPlay");
                Assert(fake.Commands.Single().action=="summon"&&fake.Commands.Single().index==1,
                    "Hover or fan sibling order changed the logical index used for explicit play.");
            });
            Case("Hand hover animates only the visual under a stable raycast rectangle and respects modal presentation guards",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);
                fake.View.ownHand=catalog.cards.Where(item=>item.IsPlayableFor(BattleRulesFormat.Standard)).Take(3).Select(item=>item.id).ToArray();
                var screen=Screen(BattleNativeScreen.Duel);var duel=Field<BattleNativeDuelView>(screen,"duel");var hand=Field<RectTransform>(duel,"hand");
                hand.rect=new Rect(0,0,1000,240);Call(duel,"Tick");var slots=Field<List<BattleNativeWidgets.CardSlot>>(duel,"handSlots");
                var slot=slots[2];var hit=(RectTransform)slot.Button.transform;var visual=(RectTransform)Find(hit,"BS_HandVisual");
                var min=hit.anchorMin;var max=hit.anchorMax;float before=visual.anchorMin.y;
                void AssertStable()
                {
                    Assert(hit.anchorMin.x==min.x&&hit.anchorMin.y==min.y&&hit.anchorMax.x==max.x&&hit.anchorMax.y==max.y
                        &&hit.localScale.x==1&&hit.localScale.y==1&&hit.localEulerAngles.z==0,
                        "Raising a hovered card moves its hit rectangle and can cause pointer exit/reenter flicker.");
                }
                Assert(hit.GetComponent<Image>()?.raycastTarget==true&&!slot.Image.raycastTarget&&slot.Image.GetComponentInParent<Button>()==slot.Button,
                    "The moving artwork, rather than the stable root, owns the card's hover hit surface.");
                Pointer(hit,UnityEngine.EventSystems.EventTriggerType.PointerEnter);AssertStable();
                Assert(visual.anchorMin.y>before&&visual.anchorMax.y<=1.01f,
                    "The lifted card exceeds its stable vertical hit extent or never lifts its visual.");
                Pointer(hit,UnityEngine.EventSystems.EventTriggerType.PointerExit);AssertStable();
                Assert(Math.Abs(visual.anchorMin.y-before)<.001f,"Pointer exit did not restore the visual under its unchanged hit rectangle.");
                fake.SetPresentationBusy(true);Pointer(hit,UnityEngine.EventSystems.EventTriggerType.PointerEnter);
                Assert(Field<int>(duel,"hoveredHand")==-1,"Summon presentation permits another card to lift behind its input curtain.");
                fake.SetPresentationBusy(false);Click(screen.transform,"BS_Hand/BS_Card_"+slots[0].Id);Click(screen.transform,"BS_InspectZoom");
                Pointer(hit,UnityEngine.EventSystems.EventTriggerType.PointerEnter);
                Assert(Field<int>(duel,"hoveredHand")==-1&&Field<int>(duel,"selectedHand")==0&&fake.Commands.Count==0,
                    "Hover behind the card zoom changes its source selection or emits a command.");
            });
            Case("Dense duel hand reflows existing fan slots on resize without dropping identities or loading artwork",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);
                var ids=catalog.cards.Where(item=>item.IsPlayableFor(BattleRulesFormat.Standard)).Take(4).Select(item=>item.id).ToArray();
                fake.View.ownHand=Enumerable.Range(0,13).Select(i=>ids[i%ids.Length]).ToArray();string state=JsonConvert.SerializeObject(fake.View);
                var screen=Screen(BattleNativeScreen.Duel);var duel=Field<BattleNativeDuelView>(screen,"duel");var hand=Field<RectTransform>(duel,"hand");
                var slots=Field<List<BattleNativeWidgets.CardSlot>>(duel,"handSlots");int objects=GameObject.All.Count,requests=fake.RequestedArt.Count;
                foreach(var size in new[]{new Vector2(1000,240),new Vector2(650,230),new Vector2(1500,260)})
                {
                    hand.rect=new Rect(0,0,size.x,size.y);Call(duel,"Tick");
                    Assert(slots.Count==13&&slots.All(slot=>slot.Button.gameObject.activeSelf)
                        &&slots.Select(slot=>slot.Id).SequenceEqual(fake.View.ownHand),"Dense hand lost a copy or reordered its logical identities.");
                    foreach(var slot in slots)
                    {
                        var rect=(RectTransform)slot.Button.transform;
                        Assert(rect.anchorMin.x>=-.001f&&rect.anchorMax.x<=1.001f,"Resizing a dense hand moves a card slot entirely outside its horizontal area.");
                    }
                }
                Assert(GameObject.All.Count==objects&&fake.RequestedArt.Count==requests&&JsonConvert.SerializeObject(fake.View)==state,
                    "A cosmetic fan resize allocates card widgets, requests faces again or changes the filtered snapshot.");
            });
            Case("Duel phase and action hints remain readable on owned dark strips without covering central card zones",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);
                var screen=Screen(BattleNativeScreen.Duel);
                foreach(var names in new[]{new[]{"BS_Phase","BS_PhaseBar"},new[]{"BS_ActionHint","BS_ActionHintBar"}})
                {
                    var label=Find(screen.transform,names[0])?.GetComponent<TextMeshProUGUI>();var bar=Find(screen.transform,names[1])?.GetComponent<Image>();
                    Assert(label!=null&&bar!=null&&!label.raycastTarget&&!bar.raycastTarget&&bar.color.a>=.85f&&Contrast(label.color,bar.color)>=4.5,
                        "A duel prompt relies on tiny text over an unowned or bright battlefield surface.");
                }
                var hint=Find(screen.transform,"BS_ActionHint").GetComponent<TextMeshProUGUI>();
                var opponentHand=(RectTransform)Find(screen.transform,"BS_OpponentHand");
                Assert(hint.rectTransform.anchorMin.y>=.86f&&hint.rectTransform.anchorMax.y<=opponentHand.anchorMin.y,
                    "Action guidance overlaps the near Core pools or the opponent's public hand instead of the header gap.");
                Assert(!string.IsNullOrWhiteSpace(hint.text),"Opening offers buttons but no short action guidance.");
                fake.View.phase="Main";fake.View.decisionPlayer=0;fake.Refresh();
                Assert(hint.text.IndexOf("thẻ",StringComparison.OrdinalIgnoreCase)>=0&&fake.Commands.Count==0,
                    "Main-step cosmetic guidance is missing or executes a command while updating text.");
            });
            Case("Arena uses restrained navy insets and tinted unchanged Standard ornaments without state or resource churn",()=>{
                var output=new GameObject("StyledArena",typeof(RectTransform)).AddComponent<RawImage>();
                var field=output.gameObject.AddComponent<BattleFieldVisuals>();field.Load(output);var stage=Field<GameObject>(field,"stage");
                foreach(string name in new[]{"StandardArena","FieldInset","SPIRITOpenArea","NEXUSOpenArea"})
                {
                    var renderer=stage.GetComponentsInChildren<MeshRenderer>(true).First(item=>item.name==name);var color=renderer.sharedMaterial.Colors["_BaseColor"];
                    Assert(color.b>=color.r&&color.b>.02f&&Math.Max(color.r,Math.Max(color.g,color.b))<.2f,
                        "A field surface is still a bright gray mat or pure black slab: "+name);
                }
                var ornaments=stage.GetComponentsInChildren<MeshRenderer>(true).Where(item=>item.name.StartsWith("PlaymatBorder",StringComparison.Ordinal)
                    ||item.name=="PlaymatCorner"||item.name=="BattleSpiritsPlaymatLogo").ToArray();
                Assert(ornaments.Length==9&&ornaments.All(item=>item.sharedMaterial.mainTexture==BattleTheme.PlaymatStandard
                    &&Luminance(item.sharedMaterial.Colors["_BaseColor"])<.7),
                    "The oversized white ornament remains untoned or uses substituted source artwork.");
                var snapshot=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);string state=JsonConvert.SerializeObject(snapshot);
                field.Render(snapshot);var materials=Field<List<Material>>(field,"materials");var meshes=Field<List<Mesh>>(field,"meshes");
                int materialCount=materials.Count,meshCount=meshes.Count;field.Render(snapshot);Call(field,"Update");
                Assert(materials.Count==materialCount&&meshes.Count==meshCount&&JsonConvert.SerializeObject(snapshot)==state&&fake.Commands.Count==0,
                    "Arena chrome churns owned resources or alters gameplay on an unchanged snapshot.");
                field.Dispose();
            });
            Case("Hand selection inspects safely and only the explicit play button summons the selected index",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);fake.View.phase="Main";fake.View.decisionPlayer=0;
                var ids=catalog.cards.Where(item=>item.IsPlayableFor(BattleRulesFormat.Standard)&&item.IsSpirit).Take(2).Select(item=>item.id).ToArray();fake.View.ownHand=ids;
                var screen=Screen(BattleNativeScreen.Duel);Click(screen.transform,"BS_Hand/BS_Card_"+ids[1]);
                Assert(fake.Commands.Count==0,"Merely inspecting a card played it from the hand.");
                Assert(Find(screen.transform,"BS_InspectName").GetComponent<TextMeshProUGUI>().text==catalog.cards.Single(card=>card.id==ids[1]).DisplayName,"Hand inspection shows another card.");
                Click(screen.transform,"BS_InspectPlay");var command=fake.Commands.Single();
                Assert(command.action=="summon"&&command.index==1,"Explicit play lost the selected logical hand index.");
            });
            Case("Stale inspected hand indices cannot play a replacement card and a changed hand clears its selection",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);fake.View.phase="Main";fake.View.decisionPlayer=0;
                var ids=catalog.cards.Where(item=>item.IsPlayableFor(BattleRulesFormat.Standard)&&item.IsSpirit).Take(2).Select(item=>item.id).ToArray();fake.View.ownHand=new[]{ids[0]};
                var screen=Screen(BattleNativeScreen.Duel);Click(screen.transform,"BS_Hand/BS_Card_"+ids[0]);var play=Find(screen.transform,"BS_InspectPlay").GetComponent<Button>();
                fake.View.ownHand=new[]{ids[1]};play.onClick.Invoke();Assert(fake.Commands.Count==0,"Stale inspect button summoned a newly drawn replacement card.");
                fake.Refresh();Assert(Field<int>(Field<BattleNativeDuelView>(screen,"duel"),"selectedHand")==-1,"Hand replacement retained its old selection.");
                play.onClick.Invoke();Assert(fake.Commands.Count==0,"Repainting a changed hand rebound an old inspected card to the new card.");
                fake.View=null;fake.Refresh();Assert(!Find(screen.transform,"BS_CardZoom").gameObject.activeSelf,"Disconnected state retained a visible card zoom.");
            });
            Case("Inspection remains available without allowing commands during opening opponent turn replay or summon presentation",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);
                string id=catalog.cards.First(item=>item.IsPlayableFor(BattleRulesFormat.Standard)&&item.IsSpirit).id;fake.View.ownHand=new[]{id};
                var screen=Screen(BattleNativeScreen.Duel);Click(screen.transform,"BS_Hand/BS_Card_"+id);var play=Find(screen.transform,"BS_InspectPlay").GetComponent<Button>();
                play.onClick.Invoke();Assert(fake.Commands.Count==0,"Opening inspection offered a summon command.");
                fake.View.phase="Main";fake.View.decisionPlayer=1;fake.Refresh();play.onClick.Invoke();Assert(fake.Commands.Count==0,"Opponent turn inspection played a card.");
                fake.View.decisionPlayer=0;fake.IsReplay=true;fake.Refresh();play.onClick.Invoke();Assert(fake.Commands.Count==0,"Replay inspection can issue a gameplay command.");
                fake.IsReplay=false;fake.SetPresentationBusy(true);play.onClick.Invoke();Assert(fake.Commands.Count==0,"Summon presentation inspection can issue a gameplay command.");
                fake.SetPresentationBusy(false);fake.CanSendCommands=false;fake.Refresh();play.onClick.Invoke();Assert(fake.Commands.Count==0,"Disconnected inspection can issue a gameplay command.");
            });
            Case("Inspecting an opponent field card clears the previous action selection and public stats stay current",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);fake.View.phase="Attack";fake.View.decisionPlayer=0;
                var ids=catalog.cards.Where(item=>item.IsPlayableFor(BattleRulesFormat.Standard)&&item.IsSpirit).Take(2).Select(item=>item.id).ToArray();
                fake.View.players[0].field=new[]{new SpiritView{instanceId=71,cardId=ids[0],normalCores=1,level=1,bp=1000}};
                fake.View.players[1].field=new[]{new SpiritView{instanceId=72,cardId=ids[1],normalCores=2,soulCore=true,level=2,bp=6000}};
                var screen=Screen(BattleNativeScreen.Duel);var duel=Field<BattleNativeDuelView>(screen,"duel");Click(screen.transform,"BS_FieldCard_71");
                Assert(Field<int>(duel,"selectedSpirit")==71,"Own public field inspection cannot select an attacker.");
                Click(screen.transform,"BS_FieldCard_72");Assert(Field<int>(duel,"selectedSpirit")==-1&&fake.Commands.Count==0,"Opponent inspection retained an unrelated own attacker or sent a command.");
                var state=Find(screen.transform,"BS_InspectState").GetComponent<TextMeshProUGUI>();
                Assert(state.text.Contains("6000")&&state.text.Contains("2"),"Inspected opponent public BP/Core state is missing.");
                fake.View.players[1].field[0].bp=9000;fake.View.players[1].field[0].normalCores=3;fake.Refresh();
                Assert(state.text.Contains("9000")&&!state.text.Contains("6000"),"Open inspection retained stale battlefield BP after a snapshot.");
                fake.View.players[1].field=Array.Empty<SpiritView>();fake.Refresh();
                Assert(!Find(screen.transform,"BS_InspectPlay").GetComponent<Selectable>().interactable&&Field<int>(duel,"selectedSpirit")==-1,"Removed inspected field unit retained an actionable selection.");
            });
            Case("Full card zoom preserves original artwork identity and closes without issuing commands",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);
                string id=catalog.cards.First(item=>item.IsPlayableFor(BattleRulesFormat.Standard)).id;fake.View.ownHand=new[]{id};
                var screen=Screen(BattleNativeScreen.Duel);var zoom=Find(screen.transform,"BS_CardZoom");Assert(!zoom.gameObject.activeSelf,"Card zoom starts open.");
                Click(screen.transform,"BS_Hand/BS_Card_"+id);Click(screen.transform,"BS_InspectZoom");
                var image=Find(zoom,"BS_ZoomArt").GetComponent<RawImage>();
                Assert(zoom.gameObject.activeInHierarchy&&image.texture==fake.ArtTextures[id]&&image.uvRect.width==1&&image.uvRect.height==1
                    &&image.GetComponent<AspectRatioFitter>()?.aspectMode==AspectRatioFitter.AspectMode.FitInParent&&fake.RequestedPortraits.Count==0,
                    "Zoom cropped, distorted or replaced the inspected card face.");
                Click(screen.transform,"BS_ZoomClose");Assert(!zoom.gameObject.activeSelf&&fake.Commands.Count==0,"Closing full card inspection played a card or left its input shield visible.");
            });
            Case("Zoom owns focus and blocks underlying hand play field attack and replay callbacks until it closes",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);fake.View.phase="Main";fake.View.turn=3;fake.View.decisionPlayer=0;
                var ids=catalog.cards.Where(item=>item.IsPlayableFor(BattleRulesFormat.Standard)&&item.IsSpirit).Take(2).Select(item=>item.id).ToArray();fake.View.ownHand=ids;
                fake.View.players[0].field=new[]{new SpiritView{instanceId=75,cardId=ids[1],normalCores=1}};
                var screen=Screen(BattleNativeScreen.Duel);var duel=Field<BattleNativeDuelView>(screen,"duel");Click(screen.transform,"BS_Hand/BS_Card_"+ids[0]);
                var previous=Find(screen.transform,"BS_Hand/BS_Card_"+ids[0]).gameObject;UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(previous);
                Click(screen.transform,"BS_InspectZoom");var close=Find(screen.transform,"BS_ZoomClose").GetComponent<Button>();
                Assert(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject==close.gameObject&&close.navigation.mode==Navigation.Mode.None,
                    "Zoom failed to take Submit focus or permits navigation onto the obscured duel controls.");
                Find(screen.transform,"BS_Hand/BS_Card_"+ids[1]).GetComponent<Button>().onClick.Invoke();
                Find(screen.transform,"BS_FieldCard_75").GetComponent<Button>().onClick.Invoke();Find(screen.transform,"BS_InspectPlay").GetComponent<Button>().onClick.Invoke();
                Click(screen.transform,"Buttons/ButtonInfo");Click(screen.transform,"Buttons/ButtonSetting");
                Assert(fake.Commands.Count==0&&Field<int>(duel,"selectedHand")==0&&Field<int>(duel,"selectedSpirit")==-1,
                    "An obscured hand/field/play/attack/concede callback remained live behind zoom.");
                Click(screen.transform,"BS_ZoomClose");Assert(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject==previous,
                    "Closing zoom failed to restore its previous valid card focus.");
                Click(screen.transform,"BS_InspectPlay");Assert(fake.Commands.Single().action=="summon","Closing zoom failed to restore explicit hand play.");
                fake.Commands.Clear();fake.IsReplay=true;fake.Refresh();Click(screen.transform,"BS_InspectZoom");
                Click(screen.transform,"Buttons/ButtonInfo");Click(screen.transform,"Buttons/ButtonLog");
                Assert(fake.ReplayDelta==0&&fake.Disconnected==0,"An obscured replay or return callback ran behind zoom.");
                Click(screen.transform,"BS_ZoomClose");Click(screen.transform,"Buttons/ButtonInfo");Assert(fake.ReplayDelta==1,"Replay stepping did not return after zoom closes.");
            });
            Case("Inspector and battlefield labels declare bounded autosizing and truncation for short windows",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);
                var card=catalog.cards.First(item=>item.IsPlayableFor(BattleRulesFormat.Standard));fake.View.players[0].field=new[]{new SpiritView{instanceId=76,cardId=card.id,normalCores=4,bp=99999}};
                var screen=Screen(BattleNativeScreen.Duel);Click(screen.transform,"BS_FieldCard_76");
                foreach(string path in new[]{"BS_InspectName","BS_InspectMeta","BS_InspectState","BS_FieldCard_76/BS_StatsBackdrop/BS_Stats"})
                {
                    var label=Find(screen.transform,path).GetComponent<TextMeshProUGUI>();
                    Assert(label.enableAutoSizing&&label.fontSizeMin>=11&&label.fontSizeMax>=label.fontSizeMin&&label.fontSizeMax<=24
                        &&label.overflowMode==TextOverflowModes.Truncate&&!label.raycastTarget,
                        "A bounded inspector/status label can spill onto nearby controls in a short window: "+path);
                }
            });
            Case("Native return first closes card zoom and only a later return disconnects the match",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);fake.IsConnected=true;
                string id=catalog.cards.First(item=>item.IsPlayableFor(BattleRulesFormat.Standard)).id;fake.View.ownHand=new[]{id};
                BattleNativeNavigation.Open(BattleNativeScreen.Duel);var servant=(BattleNativeServant)program.currentServant;var screen=servant.GetUI<BattleNativeScreenUI>();
                Click(screen.transform,"BS_Hand/BS_Card_"+id);Click(screen.transform,"BS_InspectZoom");servant.OnReturn();
                Assert(!Find(screen.transform,"BS_CardZoom").gameObject.activeSelf&&fake.Disconnected==0&&fake.IsConnected&&program.currentServant==servant,
                    "Cancel/Escape through the actual native Servant exited the match instead of closing card zoom.");
                servant.OnReturn();Assert(fake.Disconnected==1&&program.currentServant==program.menu,"The normal return route was lost after closing zoom.");
            });
            Case("Status snapshots reuse duel card widgets and artwork while maintaining the selected card identity",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);fake.View.phase="Main";fake.View.decisionPlayer=0;
                var ids=catalog.cards.Where(item=>item.IsPlayableFor(BattleRulesFormat.Standard)).Take(2).Select(item=>item.id).ToArray();fake.View.ownHand=ids;
                fake.View.players[0].field=new[]{new SpiritView{instanceId=73,cardId=ids[0]}};
                var screen=Screen(BattleNativeScreen.Duel);Click(screen.transform,"BS_Hand/BS_Card_"+ids[1]);
                var handCard=Find(screen.transform,"BS_Hand/BS_Card_"+ids[1]);var fieldCard=Find(screen.transform,"BS_FieldCard_73");
                int objects=GameObject.All.Count,requests=fake.RequestedArt.Count;
                fake.Status="Another public status";fake.Refresh();fake.Refresh();
                Assert(Find(screen.transform,"BS_Hand/BS_Card_"+ids[1])==handCard&&Find(screen.transform,"BS_FieldCard_73")==fieldCard
                    &&GameObject.All.Count==objects&&fake.RequestedArt.Count==requests,"A status-only snapshot recloned card targets or reloaded unchanged inspection artwork.");
                Assert(Find(screen.transform,"BS_InspectName").GetComponent<TextMeshProUGUI>().text==catalog.cards.Single(item=>item.id==ids[1]).DisplayName,"Status repaint silently changed the selected card.");
                fake.View.ownHand=new[]{ids[1],ids[0]};fake.Refresh();Click(screen.transform,"BS_Hand/BS_Card_"+ids[0]);Click(screen.transform,"BS_InspectPlay");
                Assert(fake.Commands.Single().index==1,"A recycled hand callback targeted its old index or identity.");
            });
            Case("Long match field churn reuses retired targets and rebound clicks follow the current owner and instance",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);fake.View.phase="Attack";fake.View.decisionPlayer=0;
                var ids=catalog.cards.Where(item=>item.IsPlayableFor(BattleRulesFormat.Standard)&&item.IsSpirit).Take(2).Select(item=>item.id).ToArray();
                fake.View.players[0].field=new[]{new SpiritView{instanceId=1000,cardId=ids[0]},new SpiritView{instanceId=1001,cardId=ids[1]}};
                var screen=Screen(BattleNativeScreen.Duel);var duel=Field<BattleNativeDuelView>(screen,"duel");var stable=Find(screen.transform,"BS_FieldCard_1000");int widgets=GameObject.All.Count;
                for(int i=0;i<24;i++)
                {
                    int instance=2000+i;var dynamic=new SpiritView{instanceId=instance,cardId=ids[i%2],normalCores=i+1,bp=1000+i};bool own=i%2==0;
                    fake.View.players[0].field=own?new[]{new SpiritView{instanceId=1000,cardId=ids[0]},dynamic}:new[]{new SpiritView{instanceId=1000,cardId=ids[0]}};
                    fake.View.players[1].field=own?Array.Empty<SpiritView>():new[]{dynamic};fake.Refresh();
                    Assert(GameObject.All.Count==widgets&&Find(screen.transform,"BS_FieldCard_1000")==stable,"Dead instances accumulate hidden field widgets or replace an unchanged field target.");
                    var target=Find(screen.transform,"BS_FieldCard_"+instance);Assert(target!=null&&target.parent.name==(own?"BS_OwnField":"BS_OpponentField"),"Recycled target stayed under its previous owner.");
                    Click(screen.transform,"BS_FieldCard_"+instance);
                    Assert(Field<int>(duel,"selectedSpirit")== (own?instance:-1)
                        &&Find(screen.transform,"BS_InspectName").GetComponent<TextMeshProUGUI>().text==catalog.cards.Single(item=>item.id==dynamic.cardId).DisplayName,
                        "Recycled field click still inspects or selects the removed instance.");
                }
                Assert(fake.Commands.Count==0,"Public field inspection issued a command during pool reuse.");
            });
            Case("Duel card targets share the actual field rectangle and phase text does not cover the card zones",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);
                var screen=Screen(BattleNativeScreen.Duel);var image=(RectTransform)Find(screen.transform,"BS_Field");
                var own=(RectTransform)Find(screen.transform,"BS_OwnField");var opponent=(RectTransform)Find(screen.transform,"BS_OpponentField");var phase=(RectTransform)Find(screen.transform,"BS_Phase");
                Assert(image.anchorMin.x>=.24f&&image.anchorMin.y>=.17f&&image.anchorMax.x>=.99f&&image.anchorMax.y<=.91f,"Battlefield remains squeezed under the hand or the native inspector.");
                foreach(var target in new[]{own,opponent})Assert(target.anchorMin.x==image.anchorMin.x&&target.anchorMin.y==image.anchorMin.y
                    &&target.anchorMax.x==image.anchorMax.x&&target.anchorMax.y==image.anchorMax.y,"Card picking coordinates are stretched over another rectangle than the rendered field.");
                Assert(phase.anchorMin.y>=.90f,"The turn/phase text still overlays central battlefield cards.");
            });
            Case("A viewport resize reprojects existing field targets without rebuilding cards or loading artwork",()=>{
                fake.View=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);
                string id=catalog.cards.First(item=>item.IsPlayableFor(BattleRulesFormat.Standard)).id;fake.View.players[0].field=new[]{new SpiritView{instanceId=74,cardId=id}};
                fake.Bounds[74]=new Rect(.2f,.3f,.1f,.15f);var screen=Screen(BattleNativeScreen.Duel);var duel=Field<BattleNativeDuelView>(screen,"duel");
                var target=(RectTransform)Find(screen.transform,"BS_FieldCard_74");int objects=GameObject.All.Count,requests=fake.RequestedArt.Count;
                fake.Bounds[74]=new Rect(.35f,.24f,.17f,.12f);Call(duel,"Tick");
                Assert(target.anchorMin.x==.35f&&target.anchorMin.y==.24f&&Math.Abs(target.anchorMax.x-.52f)<.00001f&&Math.Abs(target.anchorMax.y-.36f)<.00001f,
                    "Existing inspect targets retain obsolete camera coordinates after resizing the field.");
                Assert(GameObject.All.Count==objects&&fake.RequestedArt.Count==requests,"Reprojecting hit areas rebuilds widgets or reloads card faces.");
            });
            Case("Field render target follows the visible area aspect and releases settled resizes without status churn",()=>{
                var host=new GameObject("SizedCanvas",typeof(RectTransform),typeof(Canvas));host.GetComponent<Canvas>().scaleFactor=1.5f;
                var output=new GameObject("SizedField",typeof(RectTransform)).AddComponent<RawImage>();output.transform.SetParent(host.transform,false);
                output.rectTransform.rect=new Rect(0,0,912,518);var field=output.gameObject.AddComponent<BattleFieldVisuals>();field.SetArtworkProvider(fake.LoadArt);field.Load(output);
                var original=(RenderTexture)output.texture;var camera=Field<Camera>(field,"camera");
                Assert(original.width<=1920&&original.height<=1920&&original.width>=1368&&Math.Abs(original.width/(float)original.height-912f/518)<.005f
                    &&Math.Abs(camera.aspect-original.width/(float)original.height)<.0001f,"Field target ignores its actual scaled area or distorts the board aspect.");
                Call(field,"Update");Call(field,"Update");Assert(output.texture==original&&!original.released,"Unchanged field updates recreate their GPU target.");
                output.rectTransform.rect=new Rect(0,0,730,650);Call(field,"Update");Assert(output.texture==original,"An unsettled window resize immediately reallocates its GPU target.");
                Time.unscaledTime+=.05f;output.rectTransform.rect=new Rect(0,0,731,651);Call(field,"Update");Assert(output.texture==original,"Active resize drag is not debounced.");
                Time.unscaledTime+=.13f;Call(field,"Update");var resized=(RenderTexture)output.texture;
                Assert(resized!=original&&original.released&&original==null&&Math.Abs(resized.width/(float)resized.height-731f/651)<.005f,"Settled resize failed to retain aspect and release the previous target.");
                Call(field,"Update");Assert(output.texture==resized,"Settled render target still churns after the resize.");
                field.Dispose();Assert(resized.released&&resized==null,"Duel disposal leaked the final GPU target.");
            });
            Case("Fitted field camera retains both players table pools at wide and narrow display aspects",()=>{
                foreach(var size in new[]{new Vector2(1459,778),new Vector2(778,622),new Vector2(640,720)})
                {
                    var output=new GameObject("FramedField",typeof(RectTransform)).AddComponent<RawImage>();output.rectTransform.rect=new Rect(0,0,size.x,size.y);
                    var field=output.gameObject.AddComponent<BattleFieldVisuals>();field.Load(output);var camera=Field<Camera>(field,"camera");var stage=Field<GameObject>(field,"stage");
                    foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})foreach(var point in new[]{new Vector3(x*17.5f,.3f,z*13.5f),new Vector3(x*12,5,z*8)})
                    {
                        var projected=camera.WorldToViewportPoint(stage.transform.TransformPoint(point));
                        Assert(projected.z>0&&projected.x>=0&&projected.x<=1&&projected.y>=0&&projected.y<=1,
                            "Field camera clips a table pool or standing illustration at aspect "+size.x+":"+size.y);
                    }
                    field.Dispose();
                }
            });
            Case("Field cards enlarge sparse formations and crowded exhausted units remain inside their open zones",()=>{
                var output=new GameObject("FormationField",typeof(RectTransform)).AddComponent<RawImage>();output.rectTransform.rect=new Rect(0,0,1280,860);
                var field=output.gameObject.AddComponent<BattleFieldVisuals>();field.SetArtworkProvider(fake.LoadArt);field.SetCatalog(catalog.cards);field.Load(output);
                string spiritId=catalog.cards.First(card=>card.IsPlayableFor(BattleRulesFormat.Standard)&&card.IsSpirit).id;
                string nexusId=catalog.cards.First(card=>card.IsNexus).id;
                var snapshot=ViewFactory.Create(BattleMatch.CreateDefault(catalog.cards,format:BattleRulesFormat.Standard),0);
                snapshot.players[0].field=new[]{new SpiritView{instanceId=100,cardId=spiritId}};field.Render(snapshot);
                var units=(System.Collections.IDictionary)Field<object>(field,"units");Assert(Field<float>(units[100],"layoutScale")>1,"A sparse field leaves printed cards at their old small size.");
                foreach(var owner in snapshot.players)
                    owner.field=Enumerable.Range(0,26).Select(i=>new SpiritView{instanceId=100+owner.playerIndex*100+i,cardId=spiritId,exhausted=i%2==0})
                        .Concat(Enumerable.Range(0,10).Select(i=>new SpiritView{instanceId=150+owner.playerIndex*100+i,cardId=nexusId,exhausted=i%2==1})).ToArray();
                string data=JsonConvert.SerializeObject(snapshot);field.Render(snapshot);
                foreach(var owner in snapshot.players)foreach(var item in owner.field)
                {
                    var unit=units[item.instanceId];float scale=Field<float>(unit,"layoutScale");var home=Field<Vector3>(unit,"home");
                    bool nexus=item.cardId==nexusId;float half=BattleCardFigure.CardHeight*scale*.5f;
                    float near=nexus?6.4f:1.4f,far=nexus?9.1f:6.2f,width=nexus?18f:23f;
                    Assert(Math.Abs(home.x)+half<=width*.5f+.02f&&Math.Abs(home.z)-half>=near-.03f&&Math.Abs(home.z)+half<=far+.03f,
                        "Crowded/exhausted printed card escapes its own Spirit or Nexus zone.");
                    Assert(field.TryGetCardViewport(item.instanceId,out var bounds)&&bounds.width>0&&bounds.height>0,"A crowded public field card loses its inspectable projected target.");
                }
                Assert(JsonConvert.SerializeObject(snapshot)==data,"Visual formation layout mutated authoritative match state.");field.Dispose();
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
