using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LaoDai.BattleSpirits.Core;
using LaoDai.BattleSpirits.NativeUI;
using LaoDai.BattleSpirits.Networking;
using LaoDai.BattleSpirits.Visuals;
using MDPro3;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using WindBot.BattleSpirits;

namespace LaoDai.BattleSpirits.Integration
{
    // Session controller only. All views are the original template's ServantUI
    // prefabs; this class never creates a Canvas or places a menu overlay.
    public sealed class BattleNativeSession : MonoBehaviour, IBattleNativeSession, IBattleNativeReplaySession, IBattleNativePortraitSession, IBattleNativePreviewSession, IBattleNativePresentationSession, IBattleNativeCinematicSession
    {
        private static BattleNativeSession instance;
        private string root;
        private CardsDatabase database;
        private readonly List<DeckDefinition> decks = new List<DeckDefinition>();
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private readonly Dictionary<RawImage, string> requestedArt = new Dictionary<RawImage, string>();
        private readonly Dictionary<string, List<RawImage>> waitingArt = new Dictionary<string, List<RawImage>>();
        private readonly Dictionary<string, long> imageAges = new Dictionary<string, long>();
        private readonly List<BattleFieldVisuals> fields = new List<BattleFieldVisuals>();
        private long imageClock;
        private BattleMatch match;
        private BattleSpiritsBotSession bot;
        private BattleLanServer server;
        private BattleLanClient client;
        private bool connecting;
        private int connectionGeneration;
        private MatchRecord record;
        private bool recordSaved;
        private MatchRecord replay;
        private int replayStep;
        public bool Initialized { get; private set; }
        public IReadOnlyList<CardDefinition> Cards => database?.cards ?? Array.Empty<CardDefinition>();
        public IReadOnlyList<DeckDefinition> Decks => decks;
        public DeckDefinition SelectedDeck { get; private set; }
        public FilteredMatchView View { get; private set; }
        public IReadOnlyList<LobbyPlayerView> Players { get; private set; } = Array.Empty<LobbyPlayerView>();
        public string Status { get; private set; } = string.Empty;
        public int Seat { get; private set; } = -1;
        public bool IsConnected => client?.IsConnected == true;
        public bool CanSendCommands => !IsPresentationBusy && replay == null && View != null && View.winner < 0
            && (match != null || client?.IsConnected == true);
        public bool IsReady => Players.Any(player => player.playerIndex == Seat && player.ready);
        public bool IsPreview { get; private set; }
        public event Action Changed;
        public event Action<PublicBattleEvent> PresentationEvent;
        public event Action PresentationReset;
        public bool IsPresentationBusy { get; private set; }

        public void SetPresentationBusy(bool busy)
        {
            if (IsPresentationBusy == busy) return;
            IsPresentationBusy = busy;
            Notify();
        }

        public bool TryGetCardViewport(int instanceId, out Rect bounds)
        {
            foreach (var field in fields)
                if (field != null && !field.IsDisposed && field.TryGetCardViewport(instanceId, out bounds)) return true;
            bounds = default; return false;
        }

        public bool TryGetCardScreenViewport(int instanceId, out Rect bounds)
        {
            foreach (var field in fields)
                if (field != null && !field.IsDisposed && field.TryGetCardViewport(instanceId, out Rect local))
                {
                    // The native field uses a zero-offset RectTransform under
                    // the full-screen duel root, also used by its hit layers.
                    var area = field.transform as RectTransform;
                    if (area == null) { bounds = local; return true; }
                    float width = area.anchorMax.x - area.anchorMin.x, height = area.anchorMax.y - area.anchorMin.y;
                    bounds = new Rect(area.anchorMin.x + local.x * width, area.anchorMin.y + local.y * height,
                        local.width * width, local.height * height);
                    return true;
                }
            bounds = default; return false;
        }

        public void RevealSpiritModel(int instanceId)
        {
            foreach (var field in fields)
                if (field != null && !field.IsDisposed) field.RevealSpiritModel(instanceId);
        }

        [Serializable] private sealed class RecordedAction { public int actor; public BattleCommand command; }
        [Serializable] private sealed class MatchRecord
        {
            public int schema = Protocol.Version;
            public string catalogHash;
            public string rulesFormat = BattleSpiritsMode.RulesFormat.ToString();
            public int seed;
            public string[] names;
            public DeckDefinition[] decks;
            public List<RecordedAction> actions = new List<RecordedAction>();
        }

        public static BattleNativeSession Ensure()
        {
            if (instance != null) return instance;
            instance = new GameObject("BattleSpiritsSession").AddComponent<BattleNativeSession>();
            instance.root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            BattleNativeNavigation.Session = instance;
            instance.LoadCatalog();
            return instance;
        }

        private void LoadCatalog()
        {
            try
            {
                database = JsonConvert.DeserializeObject<CardsDatabase>(File.ReadAllText(Path.Combine(root, BattleSpiritsMode.CatalogPath)));
                if (database?.cards == null || database.cards.Length == 0) throw new InvalidDataException("Danh mục Battle Spirits trống.");
                if (database.cards.Any(card => card == null || string.IsNullOrWhiteSpace(card.id))
                    || database.cards.Select(card => card.id).Distinct(StringComparer.Ordinal).Count() != database.cards.Length)
                    throw new InvalidDataException("Danh mục có mã thẻ thiếu hoặc trùng.");
                ReloadDecks();
                SelectedDeck = decks[0];
                Initialized = true;
                SetStatus("Battle Spirits Standard — " + Cards.Count + " thẻ, " + Cards.Count(card => card.IsPlayableFor(BattleSpiritsMode.RulesFormat)) + " thẻ Standard đã hỗ trợ.");
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is JsonException || error is ArgumentException)
            {
                Initialized = false;
                Debug.LogError("Battle Spirits initialization: " + error.Message);
                SetStatus("Không đọc được dữ liệu Battle Spirits: " + error.Message);
            }
        }

        private void ReloadDecks()
        {
            decks.Clear();
            string supplied = Path.Combine(root, "Data/BattleSpirits/StarterDecks");
            foreach (string directory in new[] { supplied, Path.Combine(root, BattleSpiritsMode.DeckDirectory) })
                if (Directory.Exists(directory))
                    foreach (string path in Directory.GetFiles(directory, "*.json").OrderBy(path => path, StringComparer.Ordinal))
                        try
                        {
                            var deck = JsonConvert.DeserializeObject<DeckDefinition>(File.ReadAllText(path));
                            if (deck?.cards != null && deck.format == BattleSpiritsMode.RulesFormat.ToString()
                                && DeckValidator.Validate(Cards, deck.cards, false, BattleSpiritsMode.RulesFormat).IsValid) decks.Add(deck);
                        }
                        catch (Exception error) when (error is IOException || error is JsonException || error is UnauthorizedAccessException)
                        { Debug.LogWarning("Battle Spirits deck: " + error.Message); }
            if (decks.Count == 0) decks.AddRange(StarterDecks.CreatePair(Cards, BattleSpiritsMode.RulesFormat));
            foreach (var profile in BattleSpiritsBotProfiles.Create(Cards, BattleSpiritsMode.RulesFormat))
                if (!decks.Any(deck => deck.name == profile.Deck.name)) decks.Add(profile.Deck);
        }

        public void SelectDeck(DeckDefinition deck)
        {
            if (deck == null || deck.cards == null) return;
            if (IsConnected && (IsReady || View != null))
            { SetStatus("Hủy Sẵn sàng và kết thúc trận trước khi đổi bộ bài."); return; }
            SelectedDeck = Copy(deck);
            if (IsConnected) SendNetwork(new WireMessage { type = "deck", deck = SelectedDeck.cards });
            Notify();
        }

        public bool SaveDeck(DeckDefinition deck, out string error)
        {
            error = string.Empty;
            if (deck?.cards == null) { error = "Bộ bài trống."; return false; }
            var validation = DeckValidator.Validate(Cards, deck.cards, false, BattleSpiritsMode.RulesFormat);
            if (!validation.IsValid) { error = validation.Summary; return false; }
            try
            {
                string title = string.IsNullOrWhiteSpace(deck.name) ? "Bộ bài Battle Spirits" : deck.name.Trim();
                if (title.Length > 80) title = title.Substring(0, 80);
                deck = new DeckDefinition { name = title, format = BattleSpiritsMode.RulesFormat.ToString(), cards = deck.cards.ToArray() };
                string safe = string.Concat(title.Select(c => c < 32 || "<>:\"/\\|?*".IndexOf(c) >= 0 ? '_' : c)).Trim(' ', '.');
                if (safe.Length == 0) safe = "deck";
                // Prefix also prevents Windows reserved names such as CON.
                string directory = Path.Combine(root, BattleSpiritsMode.DeckDirectory);
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "deck-" + safe + ".json"), JsonConvert.SerializeObject(deck, Formatting.Indented));
                ReloadDecks(); SelectedDeck = Copy(deck); SetStatus("Đã lưu " + title + ".");
                return true;
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            { error = failure.Message; return false; }
        }

        public void StartSolo(DeckDefinition playerDeck, DeckDefinition opponentDeck)
        {
            if (!Initialized || !ValidDeck(playerDeck) || !ValidDeck(opponentDeck)) return;
            Disconnect();
            try
            {
                int seed = Environment.TickCount;
                var profile = BattleSpiritsBotProfiles.Create(Cards, BattleSpiritsMode.RulesFormat).FirstOrDefault(item => item.Deck.name == opponentDeck.name);
                match = BattleMatch.Create(Cards, playerDeck.cards, opponentDeck.cards, seed, format: BattleSpiritsMode.RulesFormat);
                match.State.Players[0].Name = Config.Get("BattleSpiritsPlayerName", "Người chơi");
                match.State.Players[1].Name = profile?.Name ?? "WindBot Battle Spirits";
                bot = new BattleSpiritsBotSession(1, new BattleSpiritsExecutor(Cards, profile?.Difficulty ?? BotDifficulty.Normal, seed ^ 7911), 0.4);
                record = new MatchRecord { catalogHash = CatalogFingerprint.Compute(Cards), seed = seed,
                    decks = new[] { Copy(playerDeck), Copy(opponentDeck) }, names = match.State.Players.Select(player => player.Name).ToArray() };
                recordSaved = false; Seat = 0;
                SelectedDeck = Copy(playerDeck);
                BattleNativeNavigation.Open(BattleNativeScreen.Duel);
                ApplyView(ViewFactory.Create(match, 0));
                SetStatus("Đấu với " + match.State.Players[1].Name + ".");
            }
            catch (Exception error) { Disconnect(); SetStatus("Không bắt đầu được trận: " + error.Message); }
        }

        private bool ValidDeck(DeckDefinition deck)
        {
            var result = DeckValidator.Validate(Cards, deck?.cards, true, BattleSpiritsMode.RulesFormat);
            if (!result.IsValid) SetStatus(result.Summary);
            return result.IsValid;
        }

        public void Host(int port, string playerName) => Connect(true, "127.0.0.1", port, playerName);
        public void Join(string host, int port, string playerName) => Connect(false, host, port, playerName);

        private async void Connect(bool hostHere, string address, int port, string name)
        {
            if (connecting || !Initialized) return;
            if (port < 1 || port > 65535 || string.IsNullOrWhiteSpace(address)) { SetStatus("Địa chỉ hoặc cổng không hợp lệ."); return; }
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > Protocol.MaxNameLength) { SetStatus("Tên cần từ 1 đến 24 ký tự."); return; }
            if (!ValidDeck(SelectedDeck)) return;
            Disconnect(); connecting = true;
            int generation = connectionGeneration;
            var connection = new BattleLanClient(); client = connection;
            connection.MessageReceived += Receive;
            try
            {
                if (hostHere) { server = new BattleLanServer(Cards, BattleSpiritsMode.RulesFormat); server.Start(port); }
                SetStatus("Đang kết nối Battle Spirits...");
                await connection.ConnectAsync(address.Trim(), port, name.Trim(), CatalogFingerprint.Compute(Cards), BattleSpiritsMode.RulesFormat);
                if (this == null || generation != connectionGeneration || client != connection) return;
                Config.Set("BattleSpiritsPlayerName", name.Trim()); Config.Save();
                SetStatus(hostHere ? "Đã tạo phòng Battle Spirits TCP " + port + "." : "Đã kết nối phòng.");
            }
            catch (Exception error) { if (generation == connectionGeneration && client == connection) { Disconnect(); SetStatus(error.Message); } }
            finally { if (generation == connectionGeneration && client == connection) connecting = false; }
        }

        public void SetReady(bool ready) => SendNetwork(new WireMessage { type = "ready", ready = ready });
        public void StartOnline() => SendNetwork(new WireMessage { type = "start" });

        public void Send(WireMessage message)
        {
            if (message == null) return;
            if (IsPresentationBusy && message.action != "concede")
            { SetStatus("Đợi hoạt ảnh triệu hồi hoàn tất."); return; }
            if (replay != null) { SetStatus("Đang xem lại, không gửi thao tác trận đấu."); return; }
            if (match != null)
            {
                var command = new BattleCommand { action = message.action, index = message.index,
                    source = message.source, target = message.target, soul = message.soul,
                    redraw = message.redraw, transfers = message.transfers };
                Execute(0, command, false);
            }
            else SendNetwork(message);
        }

        private void Execute(int actor, BattleCommand command, bool fromBot)
        {
            if (!BattleCommandDispatcher.TryExecute(match, actor, command, out string error))
            {
                if (fromBot) bot?.ReportRejectedAction();
                SetStatus(error); return;
            }
            record?.actions.Add(new RecordedAction { actor = actor, command = JsonConvert.DeserializeObject<BattleCommand>(JsonConvert.SerializeObject(command)) });
            match.AssertInvariants();
            ApplyView(ViewFactory.Create(match, 0));
            Status = string.Join(" • ", match.Log.Skip(Math.Max(0, match.Log.Count - 2)));
            if (View.winner >= 0) { bot?.Cancel(); SaveRecord(); }
            Notify();
        }

        private async void SendNetwork(WireMessage message)
        {
            try
            {
                if (client?.IsConnected != true) { SetStatus("Chưa kết nối tới phòng Battle Spirits."); return; }
                await client.SendAsync(message);
            }
            catch (Exception error) { if (this != null) SetStatus(error.Message); }
        }

        private void Receive(WireMessage message)
        {
            if ((message.type == "welcome" || message.type == "lobby" || message.type == "state")
                && (message.version != Protocol.Version || message.rulesFormat != BattleSpiritsMode.RulesFormat.ToString()
                    || message.type == "state" && message.view?.rulesFormat != BattleSpiritsMode.RulesFormat.ToString()))
            { Disconnect(); SetStatus("Máy chủ gửi thể thức hoặc phiên bản không phù hợp với Standard hiện tại."); return; }
            switch (message.type)
            {
                case "welcome":
                    Seat = message.playerIndex;
                    SendNetwork(new WireMessage { type = "deck", deck = SelectedDeck.cards });
                    BattleNativeNavigation.Open(BattleNativeScreen.Room); break;
                case "lobby": Players = message.players ?? Array.Empty<LobbyPlayerView>(); Notify(); break;
                case "state":
                    if (message.view == null) return;
                    if (View == null) BattleNativeNavigation.Open(BattleNativeScreen.Duel);
                    ApplyView(message.view); break;
                case "chat": SetStatus(message.name + ": " + message.text); break;
                case "error": SetStatus(message.text); break;
                case "disconnected":
                    PresentationReset?.Invoke(); SetPresentationBusy(false);
                    SetStatus(message.text); Notify(); break;
            }
        }

        private void ApplyView(FilteredMatchView view, bool animatePresentation = true)
        {
            View = view;
            fields.RemoveAll(field => field == null);
            foreach (var field in fields)
            {
                field.Render(view);
                foreach (var item in view.events ?? Array.Empty<PublicBattleEvent>()) field.PlayEvent(item);
            }
            Notify();
            if (animatePresentation)
                foreach (var item in view.events ?? Array.Empty<PublicBattleEvent>()) PresentationEvent?.Invoke(item);
        }

        public void AttachField(RawImage image)
        {
            if (image == null) return;
            var field = fields.FirstOrDefault(item => item != null && !item.IsDisposed && item.gameObject == image.gameObject);
            if (field == null)
            {
                var previous = image.GetComponent<BattleFieldVisuals>();
                if (previous != null && previous.IsDisposed) Destroy(previous);
                field = previous != null && !previous.IsDisposed ? previous : image.gameObject.AddComponent<BattleFieldVisuals>();
            }
            if (!fields.Contains(field)) fields.Add(field);
            field.SetArtworkProvider(LoadArt);
            field.Load(image);
            field.SetCatalog(Cards);
            if (View != null) field.Render(View);
        }

        private void Update()
        {
            client?.Pump();
            if (!IsPresentationBusy && match != null && bot != null && !bot.IsCancelled && match.State.WinnerIndex < 0
                && bot.TryPoll(ViewFactory.Create(match, 1), Time.unscaledTime, out var action)) Execute(1, action, true);
            PruneArt();
        }

        public void LoadArt(string id, RawImage image) => LoadImage(id, image, false);

        public void LoadPortrait(string id, RawImage image)
        {
            bool available = !string.IsNullOrEmpty(id) && Cards.Any(card => card.id == id)
                && File.Exists(Path.Combine(root, "Picture/Closeup", id + ".png"));
            LoadImage(id, image, available);
        }

        private async void LoadImage(string cardId, RawImage image, bool portrait)
        {
            if (image == null || string.IsNullOrEmpty(cardId)) return;
            string id = portrait ? "closeup:" + cardId : cardId;
            requestedArt[image] = id; imageAges[id] = ++imageClock;
            if (textures.TryGetValue(id, out var texture)) { AssignArtwork(image, texture); return; }
            image.texture = Texture2D.blackTexture;
            if (waitingArt.TryGetValue(id, out var targets)) { if (!targets.Contains(image)) targets.Add(image); return; }
            waitingArt[id] = new List<RawImage> { image };
            texture = null;
            try
            {
                var card = Cards.FirstOrDefault(item => item.id == cardId);
                if (card == null) return;
                string relative = portrait ? "Picture/Closeup/" + cardId + ".png"
                    : string.IsNullOrEmpty(card.artPath) ? "Picture/Art/" + cardId + ".png" : card.artPath;
                string path = Path.GetFullPath(Path.Combine(root, relative));
                if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return;
                byte[] bytes = await Task.Run(() => File.ReadAllBytes(path));
                if (this == null) return;
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "BattleSpirits_" + id,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    anisoLevel = 8
                };
                if (!texture.LoadImage(bytes, true)) { Destroy(texture); texture = null; return; }
                textures[id] = texture;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { Debug.LogWarning("Battle Spirits artwork: " + error.Message); }
            finally
            {
                if (waitingArt.TryGetValue(id, out var waiting))
                    foreach (var target in waiting)
                        if (target != null && requestedArt.TryGetValue(target, out string current) && current == id) AssignArtwork(target, texture);
                waitingArt.Remove(id);
            }
        }

        private static void AssignArtwork(RawImage image, Texture2D texture)
        {
            image.texture = texture;
            var fitter = image.GetComponent<AspectRatioFitter>();
            if (fitter == null || texture == null || texture == Texture2D.blackTexture
                || texture.width <= 0 || texture.height <= 0) return;
            Rect uv = image.uvRect;
            if (uv.width > 0 && uv.height > 0)
                fitter.aspectRatio = texture.width * uv.width / (texture.height * uv.height);
        }

        private void PruneArt()
        {
            foreach (var image in requestedArt.Keys.Where(image => image == null).ToArray()) requestedArt.Remove(image);
            if (textures.Count <= 64) return;
            var visible = new HashSet<string>(requestedArt.Where(pair => pair.Key != null && pair.Key.gameObject.activeInHierarchy).Select(pair => pair.Value), StringComparer.Ordinal);
            foreach (string id in textures.Keys.Where(id => !visible.Contains(id)).OrderBy(id => imageAges[id]).ToArray())
            {
                if (textures.Count <= 64) break;
                Destroy(textures[id]); textures.Remove(id); imageAges.Remove(id);
            }
        }

        public void Disconnect()
        {
            IsPresentationBusy = false;
            PresentationReset?.Invoke();
            connectionGeneration++; connecting = false;
            IsPreview = false;
            bot?.Cancel(); bot = null; match = null; replay = null; record = null;
            if (client != null) { client.MessageReceived -= Receive; client.Dispose(); client = null; }
            server?.Dispose(); server = null;
            View = null; Seat = -1; Players = Array.Empty<LobbyPlayerView>();
            foreach (var field in fields) if (field != null) field.Dispose();
            fields.Clear(); Notify();
        }

        private void SaveRecord()
        {
            if (recordSaved || record == null) return;
            try
            {
                string directory = Path.Combine(root, "Data/BattleSpirits/Replays"); Directory.CreateDirectory(directory);
                string file = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".json";
                File.WriteAllText(Path.Combine(directory, file), JsonConvert.SerializeObject(record, Formatting.Indented)); recordSaved = true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { SetStatus("Không lưu được lịch sử: " + error.Message); }
        }

        public string[] ReplayFiles()
        {
            string directory = Path.Combine(root, "Data/BattleSpirits/Replays");
            return Directory.Exists(directory) ? Directory.GetFiles(directory, "*.json").OrderByDescending(path => path, StringComparer.Ordinal).ToArray() : Array.Empty<string>();
        }

        public void LoadReplay(string path, int step = int.MaxValue)
        {
            try
            {
                if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new InvalidDataException("Tệp lịch sử quá lớn.");
                var saved = JsonConvert.DeserializeObject<MatchRecord>(File.ReadAllText(path));
                if (saved?.decks?.Length != 2 || saved.names?.Length != 2 || saved.actions == null
                    || saved.actions.Count > 10000
                    || saved.schema != Protocol.Version || saved.catalogHash != CatalogFingerprint.Compute(Cards)
                    || saved.rulesFormat != BattleSpiritsMode.RulesFormat.ToString())
                    throw new InvalidDataException("Lịch sử không phù hợp phiên bản luật/danh mục hiện tại.");
                // Reject an invalid later action before discarding the current
                // session, even when playback is requested from step zero.
                RebuildReplay(saved, saved.actions.Count);
                Disconnect(); replay = saved; replayStep = Math.Max(0, Math.Min(step, saved.actions.Count)); Seat = 0;
                RenderReplay(); BattleNativeNavigation.Open(BattleNativeScreen.Duel);
            }
            catch (Exception error) { SetStatus("Không mở được lịch sử: " + error.Message); }
        }

        public void StepReplay(int delta)
        {
            if (replay == null) return;
            int previous = replayStep;
            replayStep = (int)Math.Max(0L, Math.Min(replay.actions.Count, (long)replayStep + delta));
            try { RenderReplay(delta > 0 && replayStep == previous + 1); }
            catch (Exception error) { replayStep = previous; SetStatus("Không đọc được thao tác lịch sử: " + error.Message); }
        }

        public bool IsReplay => replay != null;

        public void StartBossPreview(string id)
        {
            var card = Cards.FirstOrDefault(item => item.id == id && item.IsSpirit
                && (item.IsPlayableFor(BattleSpiritsMode.RulesFormat) || item.IsPlayable));
            if (card == null) { SetStatus("Chưa có tài nguyên xem thử cho thẻ này."); return; }
            Disconnect(); IsPreview = true; Seat = 0;
            var level = card.levels[card.levels.Count - 1];
            View = new FilteredMatchView { viewer = 0, activePlayer = 0, decisionPlayer = -1, phase = "XEM HOẠT CẢNH",
                rulesFormat = card.IsPlayableFor(BattleSpiritsMode.RulesFormat) ? "Standard" : "Eternal",
                players = new[] {
                    new PublicPlayerView { playerIndex = 0, name = card.DisplayName, life = 5, reserveNormal = 3, reserveSoul = true,
                        field = new[] { new SpiritView { instanceId = 1, cardId = card.id, level = card.levels.Count, bp = level.bp, normalCores = level.cores } } },
                    new PublicPlayerView { playerIndex = 1, name = "Xem trước hoạt ảnh thẻ", life = 5, reserveNormal = 3 }
                } };
            BattleNativeNavigation.Open(BattleNativeScreen.Duel);
            ApplyView(View);
            SetStatus("Bấm TRIỆU HỒI để xem thẻ xuất hiện. Nút 3D/ẢNH đổi cách hiện quái; các thẻ BS01 cũ chỉ có artwork.");
        }

        public void PlayPreview(string eventKind)
        {
            if (!IsPreview || View == null || !(eventKind == "Summoned" || eventKind == "Attacked" || eventKind == "Blocked" || eventKind == "GameWon")) return;
            var card = View.players[0].field[0];
            foreach (var field in fields.Where(item => item != null && !item.IsDisposed))
                field.PlayEvent(new PublicBattleEvent { kind = eventKind, actor = 0, spiritId = 1, cardId = card.cardId });
            PresentationEvent?.Invoke(new PublicBattleEvent { kind = eventKind, actor = 0, spiritId = 1, cardId = card.cardId });
        }

        private void RenderReplay(bool animatePresentation = false)
        {
            var replayMatch = RebuildReplay(replay, replayStep);
            ApplyView(ViewFactory.Create(replayMatch, 0), animatePresentation);
            SetStatus("Xem lại thao tác " + replayStep + "/" + replay.actions.Count + ".");
        }

        private BattleMatch RebuildReplay(MatchRecord saved, int count)
        {
            var result = BattleMatch.Create(Cards, saved.decks[0].cards, saved.decks[1].cards, saved.seed, format: BattleSpiritsMode.RulesFormat);
            for (int i = 0; i < 2; i++) result.State.Players[i].Name = saved.names[i];
            for (int i = 0; i < count; i++)
            {
                var action = saved.actions[i];
                if (action == null || !BattleCommandDispatcher.TryExecute(result, action.actor, action.command, out string error))
                    throw new InvalidDataException("Lệnh lịch sử không hợp lệ tại " + i + ".");
            }
            return result;
        }

        private static DeckDefinition Copy(DeckDefinition deck) => new DeckDefinition { name = deck.name, format = deck.format, cards = deck.cards.ToArray() };
        private void SetStatus(string text) { Status = text ?? string.Empty; Notify(); }
        private void Notify() => Changed?.Invoke();
        private void OnDestroy()
        {
            Disconnect();
            foreach (var texture in textures.Values) if (texture != null) Destroy(texture);
            textures.Clear(); requestedArt.Clear(); waitingArt.Clear(); imageAges.Clear();
            if (ReferenceEquals(BattleNativeNavigation.Session, this)) BattleNativeNavigation.Session = null;
            if (instance == this) instance = null;
        }
    }
}
