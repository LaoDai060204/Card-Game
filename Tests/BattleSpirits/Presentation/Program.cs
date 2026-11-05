using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using LaoDai.BattleSpirits.Core;
using LaoDai.BattleSpirits.Integration;
using LaoDai.BattleSpirits.NativeUI;
using LaoDai.BattleSpirits.Networking;
using LaoDai.BattleSpirits.Presentation;
using LaoDai.BattleSpirits.Visuals;
using MDPro3.UI;
using MDPro3.UI.ServantUI;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

internal static class Entry
{
    private sealed class MainContext:SynchronizationContext
    {
        private readonly ConcurrentQueue<Action> work=new ConcurrentQueue<Action>();
        public override void Post(SendOrPostCallback callback,object state)=>work.Enqueue(()=>callback(state));
        public void Drain(){while(work.TryDequeue(out var action))action();}
    }
    private sealed class FixtureNode {public string path="";public string[] components=Array.Empty<string>();public int layer=5;public bool active=true;}
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
        input.text=value;input.onValueChanged.Invoke(value);
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
                Type type=kind switch {
                    "CanvasGroup"=>typeof(CanvasGroup),"SelectionButton"=>typeof(SelectionButton),"SelectionToggle"=>typeof(SelectionToggle),
                    "TMP_InputField"=>typeof(TMP_InputField),"TMP_Dropdown"=>typeof(TMP_Dropdown),"TextMeshProUGUI"=>typeof(TextMeshProUGUI),
                    "RawImage"=>typeof(RawImage),"Image"=>typeof(Image),"Button"=>typeof(Button),"Toggle"=>typeof(Toggle),
                    "ScrollRect"=>typeof(ScrollRect),"ElementObjectManager"=>typeof(YgomSystem.ElementSystem.ElementObjectManager),_=>null};
                if(type!=null)go.AddComponent(type);
            }
            if(go.GetComponent<Image>() is Image image)image.sprite=new Sprite();
            go.SetActive(row.active);
        }
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
        Cleanup();Time.unscaledTime=0;
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
    private static void Pump(Func<bool> done,Action tick=null)
    {
        var watch=Stopwatch.StartNew();while(!done()&&watch.Elapsed.TotalSeconds<10){Context.Drain();tick?.Invoke();Thread.Sleep(2);}Context.Drain();Assert(done(),"Timed out waiting for actual production continuation.");
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
        sourceRoot=Path.GetFullPath(args.Length>0?args[0]:".");LoadFixtures();
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
                Assert(deck.children.Count(child=>child.name.StartsWith("BS_Card_"))==fake.SelectedDeck.cards.Length,"Deck cards not rebound.");
                Assert(fake.RequestedArt.All(id=>catalog.cards.Any(card=>card.id==id)),"Legacy YGO card images requested.");AssertGeneratedLayers(screen.transform);
            });
            Case("Native search filters actual catalog by card ID, type, cost and BP",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");var grid=Field<RectTransform>(view,"collection");
                foreach(string query in new[]{"BS01-X01","Magic","cost:4","bp:10000"})
                {
                    Input(screen.transform,"CardCollectionView/Root/Window/FilterAndSortArea/UpperArea/InputField",query);
                    var cards=grid.children.Where(child=>child.name.StartsWith("BS_Card_")).ToArray();Assert(cards.Length>0,"Search empty unexpectedly: "+query);
                    foreach(var node in cards){string id=node.name.Substring("BS_Card_".Length);var card=catalog.cards.Single(card=>card.id==id);
                        Assert(query=="Magic"?card.type=="Magic":query=="cost:4"?card.cost==4:query=="bp:10000"?card.levels.Any(level=>level.bp>=10000):id==query,"Native BS search returned incompatible card.");}
                }
            });
            Case("Native collection paging creates fresh visible card widgets",()=>{
                var screen=Screen(BattleNativeScreen.Deck);var view=Field<BattleNativeDeckView>(screen,"deck");var grid=Field<RectTransform>(view,"collection");
                string first=grid.children.First(child=>child.name.StartsWith("BS_Card_")).name;Click(screen.transform,"BS_Next");
                Assert(first!=grid.children.First(child=>child.name.StartsWith("BS_Card_")).name,"Next page reused first page.");AssertGeneratedLayers(screen.transform);
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
            Console.WriteLine("PASS "+passed+" native production flow scenarios; actual prefab metadata, controlled Unity APIs, no Editor/render/device test.");return 0;
        }
        finally{Cleanup();Directory.Delete(temporary,true);}
    }
}
