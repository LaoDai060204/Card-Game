using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LaoDai.BattleSpirits.Core;
using LaoDai.BattleSpirits.Networking;
using MDPro3;
using MDPro3.UI.ServantUI;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LaoDai.BattleSpirits.Presentation
{
    // Lives beside the original MDPro3 Canvas. The original menu, wallpaper,
    // cosmetic viewers and settings are retained; duel data never uses YGO CDBs.
    public sealed class BattleSpiritsApp : MonoBehaviour
    {
        private static BattleSpiritsApp instance;
        public static bool Active => BattleSpiritsMode.Enabled;
        public static bool IsOpen => instance != null && instance.surface != null;
        private MainMenuUI menu;
        private RectTransform overlay;
        private RectTransform surface;
        private TextMeshProUGUI status;
        private CardsDatabase database;
        private Dictionary<string, CardDefinition> cards;
        private string rootPath;
        private DeckDefinition selectedDeck;
        private readonly Dictionary<string, Texture2D> art = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, List<RawImage>> pendingArt = new Dictionary<string, List<RawImage>>();
        private readonly Dictionary<RawImage, string> requestedArt = new Dictionary<RawImage, string>();
        private readonly Dictionary<string, long> artLastUsed = new Dictionary<string, long>();
        private long artClock;
        private const int ArtCacheLimit = 64;
        private BattleBoardPanel board;
        private BattleFieldBackdrop backdrop;
        private BattleMatch localMatch;
        private FilteredMatchView pendingLocalView;
        private RectTransform handover;
        private BattleLanServer server;
        private BattleLanClient client;
        private LobbyPlayerView[] lobby = Array.Empty<LobbyPlayerView>();
        private int seat = -1;
        private bool connecting;
        private TextMeshProUGUI lobbyText;
        private TextMeshProUGUI chatText;
        private Button readyButton;
        private Button startButton;
        private readonly List<string> chat = new List<string>();
        private MatchRecord record;
        private MatchRecord replay;
        private int replayStep;
        private bool recordSaved;

        [Serializable] private sealed class RecordedAction { public int actor; public BattleCommand command; }
        [Serializable] private sealed class MatchRecord
        {
            public string catalogHash;
            public int seed;
            public string[] names;
            public DeckDefinition[] decks;
            public List<RecordedAction> actions = new List<RecordedAction>();
        }

        public static void ConfigureMenu(MainMenuUI source)
        {
            if (!Active || source == null) return;
            var app = Ensure(source);
            app.SetMenuLabel("ButtonSolo", "CHƠI CÙNG MÁY");
            app.SetMenuLabel("ButtonPuzzle", "LUẬT CHƠI");
            app.SetMenuLabel("ButtonReplay", "LỊCH SỬ TRẬN");
        }

        private static BattleSpiritsApp Ensure(MainMenuUI source = null)
        {
            if (instance != null) { if (source != null) instance.menu = source; return instance; }
            var go = new GameObject("BattleSpirits_App");
            instance = go.AddComponent<BattleSpiritsApp>();
            instance.menu = source ?? MDPro3.Program.instance.menu.GetUI<MainMenuUI>();
            instance.rootPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return instance;
        }

        private void SetMenuLabel(string buttonName, string value)
        {
            foreach (var transform in menu.GetComponentsInChildren<Transform>(true))
                if (transform.name == buttonName)
                    foreach (var label in transform.GetComponentsInChildren<TextMeshProUGUI>(true))
                        if (label.name.EndsWith("_Menu", StringComparison.Ordinal)) label.text = value;
        }

        public static void ShowDeckBuilder() => Ensure().OpenDeckBuilder();
        public static void ShowLocal() => Ensure().OpenLocalSetup();
        public static void ShowOnline() => Ensure().OpenOnline();
        public static void ShowHistory() => Ensure().OpenHistory();
        public static void ShowRules() => Ensure().OpenRules();

        private bool LoadCatalog()
        {
            if (database != null) return true;
            try
            {
                string path = Path.Combine(rootPath, BattleSpiritsMode.CatalogPath);
                database = JsonConvert.DeserializeObject<CardsDatabase>(File.ReadAllText(path));
                if (database?.cards == null || database.cards.Length == 0) throw new InvalidDataException("Danh mục thẻ trống.");
                cards = database.cards.ToDictionary(card => card.id, StringComparer.Ordinal);
                selectedDeck = StarterDecks.Create(database.cards);
                return true;
            }
            catch (Exception error)
            {
                database = null;
                Debug.LogError("Battle Spirits catalog: " + error.Message);
                MDPro3.MessageManager.Cast("Không đọc được dữ liệu Battle Spirits: " + error.Message);
                return false;
            }
        }

        private RectTransform OpenSurface(string title)
        {
            board?.Dispose();
            if (surface != null) Destroy(surface.gameObject);
            board = null;
            backdrop = null;
            handover = null;
            status = null;
            lobbyText = null;
            chatText = null;
            readyButton = null;
            startButton = null;
            if (overlay == null)
            {
                var canvas = MDPro3.Program.instance.ui_.GetComponentInParent<Canvas>();
                if (canvas == null) throw new InvalidOperationException("Không tìm thấy Canvas MDPro3.");
                overlay = BattleUiTheme.Rect(canvas.transform, "BattleSpirits_Overlay", typeof(Canvas), typeof(GraphicRaycaster));
                BattleUiTheme.Stretch(overlay);
                var ownCanvas = overlay.GetComponent<Canvas>();
                ownCanvas.worldCamera = canvas.worldCamera;
                ownCanvas.additionalShaderChannels = canvas.additionalShaderChannels;
                ownCanvas.sortingLayerID = canvas.sortingLayerID;
                ownCanvas.overrideSorting = true;
                ownCanvas.sortingOrder = 40;
            }
            BattleUiTheme.Capture(menu.transform);
            menu.gameObject.SetActive(false);
            UIManager.InputBlocker = this;
            surface = BattleUiTheme.Panel(overlay, "BattleSpirits_Surface", Vector2.zero, Vector2.one, new Color(0.01f, 0.025f, 0.05f, 0.72f));
            var label = BattleUiTheme.Text(surface, "Title", title, 34, TextAlignmentOptions.MidlineLeft);
            Place(label.rectTransform, 0.09f, 0.91f, 0.91f, 0.99f);
            var back = BattleUiTheme.Button(surface, "‹", Close);
            Place(back.transform as RectTransform, 0.015f, 0.925f, 0.065f, 0.985f);
            status = BattleUiTheme.Text(surface, "Status", string.Empty, 18, TextAlignmentOptions.MidlineLeft);
            status.richText = false;
            Place(status.rectTransform, 0.02f, 0.01f, 0.98f, 0.065f);
            return surface;
        }

        internal static void Place(RectTransform rect, float x0, float y0, float x1, float y1)
            => BattleUiTheme.SetRect(rect, new Vector2(x0, y0), new Vector2(x1, y1), Vector2.zero, Vector2.zero);

        private void SetStatus(string message)
        {
            if (status != null) status.text = message ?? string.Empty;
        }

        private void Close()
        {
            Disconnect();
            localMatch = null;
            replay = null;
            record = null;
            board?.Dispose();
            board = null;
            if (surface != null) Destroy(surface.gameObject);
            surface = null;
            if (UIManager.InputBlocker == this) UIManager.InputBlocker = null;
            if (menu != null) { menu.gameObject.SetActive(true); ConfigureMenu(menu); }
        }

        private void Update()
        {
            client?.Pump();
            PruneArtCache();
            if (surface != null && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        }

        private void OnDestroy()
        {
            Disconnect();
            foreach (var texture in art.Values) if (texture != null) Destroy(texture);
            art.Clear();
            pendingArt.Clear();
            requestedArt.Clear();
            artLastUsed.Clear();
            if (overlay != null) Destroy(overlay.gameObject);
            if (UIManager.InputBlocker == this) UIManager.InputBlocker = null;
            if (instance == this) instance = null;
        }

        private CardDefinition FindCard(string id) => id != null && cards.TryGetValue(id, out var card) ? card : null;

        private async void LoadArt(string id, RawImage image)
        {
            if (image == null || string.IsNullOrEmpty(id)) return;
            requestedArt[image] = id;
            artLastUsed[id] = ++artClock;
            if (art.TryGetValue(id, out var existing)) { image.texture = existing; return; }
            image.texture = Texture2D.blackTexture;
            if (pendingArt.TryGetValue(id, out var waiting)) { if (!waiting.Contains(image)) waiting.Add(image); return; }
            pendingArt[id] = new List<RawImage> { image };
            Texture2D texture = null;
            try
            {
                var card = FindCard(id);
                string relative = string.IsNullOrEmpty(card?.artPath) ? "Picture/BattleSpirits/" + id + ".png" : card.artPath;
                string path = Path.GetFullPath(Path.Combine(rootPath, relative));
                if (!path.StartsWith(rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return;
                byte[] bytes = await Task.Run(() => File.ReadAllBytes(path));
                if (this == null) return;
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes, true)) { Destroy(texture); texture = null; return; }
                texture.name = "BS_Art_" + id;
                art[id] = texture;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            finally
            {
                ApplyArtToTargets(id, texture);
            }
        }

        private void ApplyArtToTargets(string id, Texture2D texture)
        {
            if (!pendingArt.TryGetValue(id, out var targets)) return;
            foreach (var target in targets)
                if (target != null && requestedArt.TryGetValue(target, out var requested) && requested == id)
                    target.texture = texture;
            pendingArt.Remove(id);
        }

        private void PruneArtCache()
        {
            foreach (var target in requestedArt.Keys.Where(target => target == null).ToArray())
                requestedArt.Remove(target);
            if (art.Count <= ArtCacheLimit) return;
            var visible = new HashSet<string>(requestedArt.Where(pair => pair.Key != null
                && pair.Key.gameObject.activeInHierarchy).Select(pair => pair.Value), StringComparer.Ordinal);
            foreach (var id in art.Keys.Where(id => !visible.Contains(id))
                .OrderBy(id => artLastUsed.TryGetValue(id, out var used) ? used : 0).ToArray())
            {
                if (art.Count <= ArtCacheLimit) break;
                Destroy(art[id]);
                art.Remove(id);
                artLastUsed.Remove(id);
            }
        }

        private void OpenDeckBuilder()
        {
            if (!LoadCatalog()) return;
            Disconnect(); localMatch = null; replay = null;
            var root = OpenSurface("BATTLE SPIRITS — BỘ BÀI / TRA CỨU THẺ");
            var panel = new BattleDeckPanel(root, rootPath, database.cards, selectedDeck, LoadArt, deck => selectedDeck = deck, SetStatus);
            panel.Build();
        }

        private List<DeckDefinition> SavedDecks()
        {
            var decks = StarterDecks.CreatePair(database.cards).ToList();
            string directory = Path.Combine(rootPath, BattleSpiritsMode.DeckDirectory);
            if (Directory.Exists(directory))
                foreach (string path in Directory.GetFiles(directory, "*.json").OrderBy(p => p, StringComparer.Ordinal))
                    try
                    {
                        var deck = JsonConvert.DeserializeObject<DeckDefinition>(File.ReadAllText(path));
                        if (deck?.cards != null) decks.Add(deck);
                    }
                    catch (Exception error) when (error is IOException || error is JsonException)
                    { Debug.LogWarning("Skipped Battle Spirits deck: " + error.Message); }
            if (selectedDeck != null && !decks.Any(deck => deck.name == selectedDeck.name && deck.cards.SequenceEqual(selectedDeck.cards)))
                decks.Insert(0, selectedDeck);
            return decks;
        }

        private void OpenLocalSetup()
        {
            if (!LoadCatalog()) return;
            Disconnect(); replay = null; localMatch = null;
            var root = OpenSurface("BATTLE SPIRITS — HAI NGƯỜI CÙNG MÁY");
            var explanation = BattleUiTheme.Text(root, "Instructions", "Hai người thay phiên trên cùng máy. Khi chuyển người quyết định, bài trên tay được che cho đến khi người tiếp theo xác nhận.", 24);
            Place(explanation.rectTransform, 0.14f, 0.72f, 0.86f, 0.86f);
            var names = new[] { BattleUiTheme.Input(root, "Người chơi 1", "Người chơi 1"), BattleUiTheme.Input(root, "Người chơi 2", "Người chơi 2") };
            var decks = SavedDecks();
            var selectors = new TMP_Dropdown[2];
            for (int i = 0; i < 2; i++)
            {
                Place(names[i].transform as RectTransform, 0.15f, 0.61f - i * 0.19f, 0.44f, 0.69f - i * 0.19f);
                selectors[i] = BattleUiTheme.Dropdown(root, "Deck" + i, decks.Select(deck => deck.name).ToArray());
                Place(selectors[i].transform as RectTransform, 0.47f, 0.61f - i * 0.19f, 0.85f, 0.69f - i * 0.19f);
            }
            var start = BattleUiTheme.Button(root, "BẮT ĐẦU", () => StartLocal(decks[selectors[0].value], decks[selectors[1].value], names[0].text, names[1].text));
            Place(start.transform as RectTransform, 0.34f, 0.21f, 0.66f, 0.31f);
            SetStatus("Dùng bộ luyện tập để chơi; thẻ chưa hỗ trợ hiệu ứng vẫn có thể tra cứu trong Bộ bài.");
        }

        private void StartLocal(DeckDefinition first, DeckDefinition second, string name0, string name1)
        {
            var a = DeckValidator.Validate(database.cards, first.cards);
            var b = DeckValidator.Validate(database.cards, second.cards);
            if (!a.IsValid || !b.IsValid) { SetStatus(a.Summary + "\n" + b.Summary); return; }
            try
            {
                int seed = Environment.TickCount;
                localMatch = BattleMatch.Create(database.cards, first.cards, second.cards, seed);
                localMatch.State.Players[0].Name = string.IsNullOrWhiteSpace(name0) ? "Người chơi 1" : name0.Trim();
                localMatch.State.Players[1].Name = string.IsNullOrWhiteSpace(name1) ? "Người chơi 2" : name1.Trim();
                record = new MatchRecord { catalogHash = CatalogFingerprint.Compute(database.cards), seed = seed, names = new[] { localMatch.State.Players[0].Name, localMatch.State.Players[1].Name }, decks = new[] { first, second } };
                recordSaved = false;
                OpenBoard();
                RenderLocal(true);
            }
            catch (Exception error) { SetStatus(error.Message); }
        }

        private void OpenBoard()
        {
            var root = OpenSurface("BATTLE SPIRITS — ETERNAL");
            board = new BattleBoardPanel(root, SendAction, Close, FindCard, LoadArt);
            Place(board.Root, 0.015f, 0.07f, 0.985f, 0.91f);
            backdrop = board.Root.gameObject.AddComponent<BattleFieldBackdrop>();
            backdrop.Load(board.Background);
            board.BattleEventPlayed += evt => { if (backdrop != null) backdrop.PlayEvent(evt, pendingLocalView?.viewer ?? seat); };
        }

        private void RenderLocal(bool forceHandover = false)
        {
            int viewer = localMatch.DecisionPlayerIndex;
            var next = ViewFactory.Create(localMatch, viewer);
            bool changed = pendingLocalView == null || pendingLocalView.viewer != viewer;
            pendingLocalView = next;
            if ((forceHandover || changed) && next.winner < 0)
            {
                board.Root.gameObject.SetActive(false);
                if (handover != null) Destroy(handover.gameObject);
                handover = BattleUiTheme.Panel(surface, "BattleSpirits_Handover", new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.88f), new Color(0.01f, 0.025f, 0.05f, 1f));
                var label = BattleUiTheme.Text(handover, "Player", "Đến lượt " + localMatch.State.Players[viewer].Name + "\nChuyển máy cho người này trước khi mở bài.", 32, TextAlignmentOptions.Center);
                Place(label.rectTransform, 0.15f, 0.45f, 0.85f, 0.85f);
                var show = BattleUiTheme.Button(handover, "TÔI ĐÃ SẴN SÀNG", () =>
                {
                    if (handover != null) Destroy(handover.gameObject);
                    handover = null;
                    board.Root.gameObject.SetActive(true);
                    board.Render(pendingLocalView);
                });
                Place(show.transform as RectTransform, 0.32f, 0.2f, 0.68f, 0.35f);
            }
            else { board.Root.gameObject.SetActive(true); board.Render(next); }
            if (next.winner >= 0) SaveRecord();
        }

        private async void SendAction(WireMessage message)
        {
            if (replay != null) return;
            if (localMatch != null)
            {
                int actor = pendingLocalView.viewer;
                var command = new BattleCommand { action = message.action, index = message.index, source = message.source, target = message.target, soul = message.soul, redraw = message.redraw };
                if (!BattleCommandDispatcher.TryExecute(localMatch, actor, command, out string error)) { SetStatus(error); return; }
                record.actions.Add(new RecordedAction { actor = actor, command = command });
                SetStatus(string.Join("  •  ", localMatch.Log.Skip(Math.Max(0, localMatch.Log.Count - 2))));
                RenderLocal();
                return;
            }
            try { if (client != null && client.IsConnected) await client.SendAsync(message); else SetStatus("Không còn kết nối tới phòng."); }
            catch (Exception error) { SetStatus(error.Message); }
        }

        private void OpenOnline()
        {
            if (!LoadCatalog()) return;
            Disconnect(); localMatch = null; replay = null;
            var root = OpenSurface("BATTLE SPIRITS — LAN / ONLINE");
            var left = BattleUiTheme.Panel(root, "RoomSettings", new Vector2(0.04f, 0.13f), new Vector2(0.46f, 0.88f), BattleUiTheme.PanelColor);
            var instructions = BattleUiTheme.Text(left, "Instructions", "Tạo phòng trên máy này, hoặc nhập IP/domain của máy chủ để tham gia. LAN và Internet dùng cùng phòng Battle Spirits.", 22);
            Place(instructions.rectTransform, 0.06f, 0.79f, 0.94f, 0.96f);
            var name = BattleUiTheme.Input(left, "Tên người chơi", Config.Get("BattleSpiritsPlayerName", "Người chơi"));
            Place(name.transform as RectTransform, 0.06f, 0.67f, 0.94f, 0.76f);
            var host = BattleUiTheme.Input(left, "IP / domain", "127.0.0.1");
            Place(host.transform as RectTransform, 0.06f, 0.55f, 0.65f, 0.64f);
            var port = BattleUiTheme.Input(left, "Cổng", BattleSpiritsMode.DefaultPort.ToString());
            port.contentType = TMP_InputField.ContentType.IntegerNumber;
            Place(port.transform as RectTransform, 0.68f, 0.55f, 0.94f, 0.64f);
            var decks = SavedDecks();
            var deck = BattleUiTheme.Dropdown(left, "SelectedDeck", decks.Select(item => item.name).ToArray(), i => { selectedDeck = decks[i]; if (client?.IsConnected == true) SendNetwork(new WireMessage { type = "deck", deck = selectedDeck.cards }); });
            Place(deck.transform as RectTransform, 0.06f, 0.42f, 0.94f, 0.51f);
            selectedDeck = decks[0];
            var create = BattleUiTheme.Button(left, "TẠO PHÒNG LAN", () => ConnectRoom(true, host.text, port.text, name.text));
            Place(create.transform as RectTransform, 0.06f, 0.26f, 0.48f, 0.37f);
            var join = BattleUiTheme.Button(left, "THAM GIA", () => ConnectRoom(false, host.text, port.text, name.text));
            Place(join.transform as RectTransform, 0.52f, 0.26f, 0.94f, 0.37f);
            var note = BattleUiTheme.Text(left, "ConnectionNote", "Máy khác dùng IP LAN của người tạo phòng. Chơi Internet cần địa chỉ máy chủ truy cập được từ Internet.", 18);
            Place(note.rectTransform, 0.06f, 0.04f, 0.94f, 0.22f);
            lobbyText = BattleUiTheme.Text(root, "LobbyPlayers", "Chưa vào phòng.", 26);
            lobbyText.richText = false;
            Place(lobbyText.rectTransform, 0.51f, 0.59f, 0.96f, 0.86f);
            readyButton = BattleUiTheme.Button(root, "SẴN SÀNG", () => SendNetwork(new WireMessage { type = "ready", ready = !lobby.Any(player => player.playerIndex == seat && player.ready) }));
            Place(readyButton.transform as RectTransform, 0.51f, 0.49f, 0.72f, 0.57f);
            startButton = BattleUiTheme.Button(root, "BẮT ĐẦU", () => SendNetwork(new WireMessage { type = "start" }));
            Place(startButton.transform as RectTransform, 0.75f, 0.49f, 0.96f, 0.57f);
            readyButton.interactable = startButton.interactable = false;
            chatText = BattleUiTheme.Text(root, "Chat", string.Empty, 19);
            chatText.richText = false;
            Place(chatText.rectTransform, 0.51f, 0.24f, 0.96f, 0.46f);
            var input = BattleUiTheme.Input(root, "Tin nhắn", string.Empty);
            input.characterLimit = 300;
            Place(input.transform as RectTransform, 0.51f, 0.14f, 0.84f, 0.21f);
            var send = BattleUiTheme.Button(root, "GỬI", () => { SendNetwork(new WireMessage { type = "chat", text = input.text }); input.text = string.Empty; });
            Place(send.transform as RectTransform, 0.86f, 0.14f, 0.96f, 0.21f);
        }

        private async void ConnectRoom(bool hostHere, string address, string portText, string name)
        {
            if (connecting) return;
            if (!int.TryParse(portText, out int port) || port < 1 || port > 65535) { SetStatus("Cổng phải từ 1 đến 65535."); return; }
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > Protocol.MaxNameLength) { SetStatus("Tên người chơi cần từ 1 đến 24 ký tự."); return; }
            var validation = DeckValidator.Validate(database.cards, selectedDeck.cards);
            if (!validation.IsValid) { SetStatus(validation.Summary); return; }
            Disconnect(); connecting = true;
            var connection = new BattleLanClient();
            client = connection;
            connection.MessageReceived += OnNetworkMessage;
            try
            {
                if (hostHere) { server = new BattleLanServer(database.cards); server.Start(port); address = "127.0.0.1"; }
                SetStatus("Đang kết nối...");
                await connection.ConnectAsync(address.Trim(), port, name.Trim(), CatalogFingerprint.Compute(database.cards));
                if (client != connection) return;
                Config.Set("BattleSpiritsPlayerName", name.Trim()); Config.Save();
                SetStatus(hostHere ? "Đã mở phòng trên cổng " + port + ". Người khác nhập IP của máy này để tham gia." : "Đã kết nối, đang vào phòng.");
            }
            catch (Exception error) { if (client == connection) { Disconnect(); SetStatus(error.Message); } }
            finally { if (client == connection) connecting = false; }
        }

        private void OnNetworkMessage(WireMessage message)
        {
            switch (message.type)
            {
                case "welcome": seat = message.playerIndex; SendNetwork(new WireMessage { type = "deck", deck = selectedDeck.cards }); break;
                case "lobby":
                    lobby = message.players ?? Array.Empty<LobbyPlayerView>();
                    if (lobbyText != null) lobbyText.text = string.Join("\n\n", lobby.Select(player => (player.playerIndex == seat ? "▶ " : "") + player.name + "\n" + player.deckCount + " lá — " + (player.ready ? "Đã sẵn sàng" : "Chưa sẵn sàng")));
                    if (readyButton != null) readyButton.interactable = seat >= 0;
                    if (startButton != null) startButton.interactable = seat == 0 && lobby.Length == 2 && lobby.All(player => player.ready);
                    break;
                case "state":
                    if (message.view == null) break;
                    if (board == null) OpenBoard();
                    pendingLocalView = message.view;
                    board.Render(message.view);
                    break;
                case "chat":
                    chat.Add(message.name + ": " + message.text); if (chat.Count > 8) chat.RemoveAt(0);
                    if (chatText != null) chatText.text = string.Join("\n", chat);
                    break;
                case "error": SetStatus(message.text); break;
                case "disconnected":
                    if (readyButton != null) readyButton.interactable = false;
                    if (startButton != null) startButton.interactable = false;
                    board?.SetDisconnected();
                    SetStatus(message.text);
                    break;
            }
        }

        private async void SendNetwork(WireMessage message)
        {
            try { if (client?.IsConnected == true) await client.SendAsync(message); else SetStatus("Hãy vào phòng trước."); }
            catch (Exception error) { SetStatus(error.Message); }
        }

        private void Disconnect()
        {
            if (client != null) { client.MessageReceived -= OnNetworkMessage; client.Dispose(); client = null; }
            server?.Dispose(); server = null;
            seat = -1; connecting = false; lobby = Array.Empty<LobbyPlayerView>(); chat.Clear();
        }

        private void SaveRecord()
        {
            if (recordSaved || record == null) return;
            try
            {
                string directory = Path.Combine(rootPath, "Data/BattleSpirits/Replays"); Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".json");
                File.WriteAllText(path, JsonConvert.SerializeObject(record, Formatting.Indented)); recordSaved = true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { SetStatus("Không lưu được lịch sử: " + error.Message); }
        }

        private void OpenHistory()
        {
            if (!LoadCatalog()) return;
            Disconnect(); localMatch = null; replay = null;
            var root = OpenSurface("BATTLE SPIRITS — LỊCH SỬ TRẬN CÙNG MÁY");
            var scroll = BattleUiTheme.Scroll(root, "History", out var content); Place(scroll.transform as RectTransform, 0.12f, 0.16f, 0.88f, 0.86f);
            string directory = Path.Combine(rootPath, "Data/BattleSpirits/Replays");
            var files = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.json").OrderByDescending(path => path, StringComparer.Ordinal).ToArray() : Array.Empty<string>();
            foreach (string file in files)
            {
                string path = file;
                BattleUiTheme.Button(content, Path.GetFileNameWithoutExtension(path), () => LoadReplay(path));
            }
            SetStatus(files.Length == 0 ? "Chưa có trận hoàn tất. Trận chơi cùng máy được lưu tự động khi kết thúc." : "Chọn trận để xem lại từng thao tác.");
        }

        private void LoadReplay(string path)
        {
            try
            {
                var saved = JsonConvert.DeserializeObject<MatchRecord>(File.ReadAllText(path));
                if (saved?.decks?.Length != 2 || saved.actions == null || saved.catalogHash != CatalogFingerprint.Compute(database.cards)) throw new InvalidDataException("Lịch sử không phù hợp với bản dữ liệu thẻ này.");
                replay = saved; localMatch = null; replayStep = 0; OpenBoard(); RenderReplay();
                var previous = BattleUiTheme.Button(surface, "‹ TRƯỚC", () => { replayStep = Math.Max(0, replayStep - 1); RenderReplay(); });
                var next = BattleUiTheme.Button(surface, "TIẾP ›", () => { replayStep = Math.Min(replay.actions.Count, replayStep + 1); RenderReplay(); });
                Place(previous.transform as RectTransform, 0.57f, 0.925f, 0.73f, 0.985f); Place(next.transform as RectTransform, 0.76f, 0.925f, 0.92f, 0.985f);
            }
            catch (Exception error) { SetStatus("Không mở được lịch sử: " + error.Message); }
        }

        private void RenderReplay()
        {
            try
            {
                var match = BattleMatch.Create(database.cards, replay.decks[0].cards, replay.decks[1].cards, replay.seed);
                for (int p = 0; p < 2; p++) match.State.Players[p].Name = replay.names[p];
                for (int i = 0; i < replayStep; i++) if (!BattleCommandDispatcher.TryExecute(match, replay.actions[i].actor, replay.actions[i].command, out string error)) throw new InvalidDataException(error);
                pendingLocalView = ViewFactory.Create(match, match.DecisionPlayerIndex);
                board.Render(pendingLocalView, false);
                SetStatus("Xem lại thao tác " + replayStep + " / " + replay.actions.Count);
            }
            catch (Exception error) { SetStatus(error.Message); }
        }

        private void OpenRules()
        {
            var root = OpenSurface("BATTLE SPIRITS — LUẬT ETERNAL");
            var scroll = BattleUiTheme.Scroll(root, "Rules", out var content); Place(scroll.transform as RectTransform, 0.1f, 0.15f, 0.9f, 0.87f);
            var text = BattleUiTheme.Text(content, "RuleText", "Mỗi bộ bài có ít nhất 40 lá; mặc định tối đa 3 lá cùng tên, trừ ngoại lệ đã xác minh và danh sách giới hạn.\n\nBắt đầu với 5 Life, 3 core thường và 1 Soul Core ở Reserve, rút 4 lá. Mỗi người được đổi toàn bộ bài mở đầu một lần.\n\nLượt Eternal: Start → Core → Draw → Refresh → Main → Attack → End. Người đi trước bỏ qua Core và Attack ở lượt đầu nhưng vẫn Draw.\n\nTrong Main, trả Cost sau khi giảm theo symbol cùng màu và đặt đủ core duy trì Lv1. Chuyển core để thay đổi Level/BP; Spirit không đủ core Lv1 sẽ tiêu biến.\n\nTấn công làm Spirit mệt. Bên phòng thủ có quyền Flash trước. Sau hai lần bỏ qua, chọn chặn hoặc nhận sát thương Life. Nếu chặn, có cửa sổ Flash thứ hai trước khi so BP; BP bằng nhau thì cả hai bị phá hủy.\n\nMất Life theo Symbol; core Life bị mất chuyển về Reserve. Life về 0 thì thua. Hết Deck được kiểm tra ở Start Step.\n\nBản này có bộ luyện tập gồm Spirit không hiệu ứng. Các thẻ mới vẫn có ảnh, chỉ số và văn bản gốc để tra cứu; chỉ thẻ được ghi Đã hỗ trợ mới dùng được trong trận tự động.\n\nLuật gốc: battlespirits.com/rule/official-rule-manual-eternal/", 25);
            var layout = text.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight = 1300;
        }
    }
}
