using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MDPro3.BattleSpirits.AI;
using MDPro3.BattleSpirits.Core;
using MDPro3.BattleSpirits.Puzzles;

namespace MDPro3.BattleSpirits
{
    /// <summary>Main-thread coordinator shared by Solo, puzzles, authoritative LAN and replay.</summary>
    public sealed class BattleGameSession : IDisposable
    {
        private readonly IReadOnlyList<CardDefinition> cards;
        private readonly string storageRoot, catalogHash;
        private readonly int ownerThread;
        private readonly BattleSpiritsBot bot;
        private readonly List<string> log = new List<string>();
        private BattleMatch match;
        private BattleReplayDocument recording, replay;
        private BattleLanTransport transport;
        private DeckDefinition localDeck, retryPlayer, retryOpponent;
        private string localName = "Player", retryName;
        private int retrySeed, commandNumber, revision;
        private bool ready, clientPending, disposed, replaySaved;
        private float nextDecisionTime, clock;
        private IReadOnlyList<BattleCommand> legal = Array.Empty<BattleCommand>();
        public BattleSnapshot View { get; private set; }
        public IReadOnlyList<BattleCommand> LegalCommands => PresentationBusy || IsReplay || clientPending || IsOnline && !IsConnected ? Array.Empty<BattleCommand>() : legal;
        public int Seat { get; private set; }
        public bool IsReplay { get; private set; }
        public bool IsOnline { get; private set; }
        public bool IsConnected => IsOnline && ready && transport != null && transport.IsConnected;
        public bool IsHost { get; private set; }
        public bool PresentationBusy { get; set; }
        public string Status { get; private set; } = "Choose a Battle Spirits match.";
        public IReadOnlyList<string> Log => log.AsReadOnly();
        public PuzzleSession Puzzle { get; private set; }
        public int ReplayPosition { get; private set; }
        public int ReplayCount => replay?.commands.Count ?? 0;
        public event Action Changed;
        public event Action<BattleEvent> CommittedEvent;

        public BattleGameSession(IReadOnlyList<CardDefinition> cards, string storageRoot)
        {
            this.cards = cards ?? throw new ArgumentNullException(nameof(cards));
            if (string.IsNullOrWhiteSpace(storageRoot)) throw new ArgumentException("Battle Spirits storage is required.");
            this.storageRoot = Path.GetFullPath(storageRoot); catalogHash = BattleReplay.CatalogHash(cards);
            ownerThread = Thread.CurrentThread.ManagedThreadId; bot = new BattleSpiritsBot(cards);
        }
        private void MainThread()
        {
            if (disposed) throw new ObjectDisposedException(nameof(BattleGameSession));
            if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("Battle Spirits gameplay must run on the session thread.");
        }
        private void Clear()
        {
            transport?.Dispose(); transport = null; match = null; recording = replay = null; Puzzle = null;
            View = null; legal = Array.Empty<BattleCommand>(); log.Clear(); Seat = 0; IsReplay = IsOnline = IsHost = false;
            ready = clientPending = replaySaved = false; commandNumber = revision = ReplayPosition = 0;
            nextDecisionTime = clock; PresentationBusy = false;
        }
        public void StartSolo(DeckDefinition player, DeckDefinition opponent, int seed = 2026, string playerName = "Player")
        {
            MainThread();
            var document = BattleReplay.Create(cards, catalogHash, player, opponent, seed, playerName, "Battle Spirits AI");
            var initial = BattleReplay.Simulate(cards, document, 0);
            Clear(); recording = document; match = initial;
            retryPlayer = BattleReplay.Copy(player); retryOpponent = BattleReplay.Copy(opponent); retrySeed = seed; retryName = playerName;
            Refresh();
        }
        public void StartPuzzle(PuzzleDefinition definition)
        {
            MainThread(); var puzzle = PuzzleSession.Create(definition, cards);
            Clear(); Puzzle = puzzle; match = puzzle.Match; Refresh();
        }
        public bool ResetPuzzle()
        {
            MainThread(); if (Puzzle == null || PresentationBusy) return false;
            Puzzle.Reset(); match = Puzzle.Match; commandNumber = 0; Refresh(); return true;
        }
        public bool Retry()
        {
            MainThread(); if (PresentationBusy || IsOnline || IsReplay) return false;
            if (Puzzle != null) return ResetPuzzle();
            if (retryPlayer == null || retryOpponent == null) return false;
            StartSolo(retryPlayer, retryOpponent, retrySeed, retryName); return true;
        }
        public bool Send(BattleCommand command, out string error)
        {
            MainThread(); error = "";
            if (IsReplay) { error = "Replay input is closed. Use previous or next."; return false; }
            if (PresentationBusy) { error = "Wait for the current presentation."; return false; }
            try { BattleReplay.ValidateCommand(command); }
            catch (Exception e) { error = e.Message; return false; }
            if (IsOnline && !IsConnected) { error = "Wait for the LAN peer."; return false; }
            if (IsOnline && !IsHost)
            {
                if (View == null || View.WinnerIndex >= 0 || clientPending || (View.DecisionPlayerIndex != Seat && command.action != "concede"))
                { error = "Wait for your decision."; return false; }
                try { transport.Send(Packet("command", command: BattleReplay.Copy(command))); clientPending = true; Changed?.Invoke(); return true; }
                catch (Exception e) { Disconnect(e.Message); error = e.Message; return false; }
            }
            if (match == null) { error = "No Battle Spirits match is active."; return false; }
            if (match.DecisionPlayerIndex != Seat && command.action != "concede") { error = "The other player must decide first."; return false; }
            return Execute(Seat, command, out error);
        }
        public void Concede() { Send(new BattleCommand { action = "concede" }, out _); }
        private bool Execute(int actor, BattleCommand command, out string error)
        {
            if (recording != null && recording.commands.Count >= BattleReplay.MaxCommands)
            { error = "The match command limit has been reached."; return false; }
            var detached = BattleReplay.Copy(command);
            bool success = Puzzle != null ? Puzzle.TryExecute(actor, detached, out error) : match.TryExecute(actor, detached, out error);
            if (!success) return false;
            recording?.commands.Add(new BattleRecordedCommand { actor = actor, command = detached });
            commandNumber++; revision++; nextDecisionTime = clock + .35f;
            var events = match.LastEvents.Select(e => FilterEvent(e, Seat)).ToArray();
            Refresh(false);
            if (IsHost && ready) SendState(match.LastEvents);
            SaveCompleted();
            Changed?.Invoke();
            foreach (var e in events) CommittedEvent?.Invoke(e);
            return true;
        }
        private void SaveCompleted()
        {
            if (recording == null || replaySaved || match.State.WinnerIndex < 0) return;
            recording.winner = match.State.WinnerIndex;
            try { BattleReplay.Save(storageRoot, recording); replaySaved = true; }
            catch (Exception e) { Status += " Replay could not be saved: " + e.Message; }
        }
        private void Refresh(bool notify = true)
        {
            if (match != null)
            {
                View = match.GetSnapshot(Seat);
                bool puzzleActive = Puzzle == null || Puzzle.Status == PuzzleStatus.InProgress;
                legal = !IsReplay && puzzleActive ? match.GetLegalCommands(Seat).ToArray() : Array.Empty<BattleCommand>();
                log.Clear(); log.AddRange(match.Log);
                Status = Puzzle != null ? Puzzle.ResultMessage : View.WinnerIndex >= 0 ? View.Players[View.WinnerIndex].Name + " wins."
                    : IsReplay ? "Replay " + ReplayPosition + "/" + ReplayCount
                    : View.DecisionPlayerIndex == Seat ? "Your " + View.Phase + " decision." : IsOnline ? "Waiting for opponent." : "Battle Spirits AI is thinking.";
                if (IsReplay) Status = "Replay " + ReplayPosition + "/" + ReplayCount + (View.WinnerIndex >= 0 ? ": " + View.Players[View.WinnerIndex].Name + " wins." : " — " + View.Phase);
            }
            if (notify) Changed?.Invoke();
        }
        public void Tick(float unscaledTime)
        {
            MainThread();
            if (float.IsNaN(unscaledTime) || float.IsInfinity(unscaledTime)) return;
            clock = unscaledTime;
            if (PresentationBusy || IsReplay) return;
            if (transport != null)
            {
                // Process a bounded number per frame. Every rules mutation is performed here.
                for (int i = 0; i < 8 && transport != null && transport.Receive(out var notice); i++)
                {
                    if (notice.type == "closed") { Disconnect(notice.payload); break; }
                    if (notice.type == "connected")
                    {
                        if (!IsHost) try { transport.Send(Packet("hello", deck: localDeck, name: localName)); }
                            catch (Exception e) { Disconnect(e.Message); }
                    }
                    else if (notice.type == "frame") ProcessPacket(notice.payload);
                    if (PresentationBusy) return;
                }
                if (!IsHost || !IsConnected) return;
            }
            if (IsOnline && !IsConnected || match == null || match.State.WinnerIndex >= 0 || Puzzle != null && Puzzle.Status != PuzzleStatus.InProgress || clock < nextDecisionTime) return;
            int actor = match.DecisionPlayerIndex;
            if (actor < 0) return;
            var candidates = match.GetLegalCommands(actor);
            // Only these system steps advance without a player choice. Opening/Main/Attack/Flash/Block remain decisions.
            bool automatic = match.State.PendingChoice == null && candidates.Count == 1 && candidates[0].action == "advance" &&
                (match.State.Phase == MatchPhase.Start || match.State.Phase == MatchPhase.Core || match.State.Phase == MatchPhase.Draw || match.State.Phase == MatchPhase.Refresh || match.State.Phase == MatchPhase.End);
            BattleCommand decision = automatic ? candidates[0] : !IsOnline && actor != Seat
                ? bot.Choose(match.GetSnapshot(actor), candidates, BotDifficulty.Expert, (recording?.seed ?? Puzzle?.Definition.seed ?? 2026) + commandNumber) : null;
            if (decision != null && !Execute(actor, decision, out var error)) { Status = "Battle Spirits decision failed: " + error; Changed?.Invoke(); }
        }
        public void StartHost(int port, DeckDefinition deck, string playerName)
        {
            MainThread(); var validated = BattleReplay.ValidateDeck(cards, deck); string name = BattleReplay.Name(playerName, "Host");
            Clear(); localDeck = validated; localName = name; IsOnline = IsHost = true; transport = new BattleLanTransport();
            try { transport.Host(port); Status = "Hosting Battle Spirits on LAN port " + port + ". Waiting for a player."; Changed?.Invoke(); }
            catch { transport.Dispose(); transport = null; IsOnline = IsHost = false; throw; }
        }
        public void Join(string host, int port, DeckDefinition deck, string playerName)
        {
            MainThread(); var validated = BattleReplay.ValidateDeck(cards, deck); string name = BattleReplay.Name(playerName, "Guest");
            Clear(); Seat = 1; localDeck = validated; localName = name; IsOnline = true; transport = new BattleLanTransport();
            try { transport.Join(host, port); Status = "Connecting to Battle Spirits LAN host…"; Changed?.Invoke(); }
            catch { transport.Dispose(); transport = null; IsOnline = false; throw; }
        }
        private BattleLanPacket Packet(string type, BattleCommand command = null, DeckDefinition deck = null, string name = null)
            => new BattleLanPacket { type = type, catalogHash = catalogHash, revision = revision, command = command, deck = deck, name = name };
        private void ProcessPacket(string json)
        {
            try
            {
                var packet = BattleReplay.Decode<BattleLanPacket>(json);
                if (packet.protocol != BattleReplay.Protocol || packet.version != BattleReplay.Version || packet.catalogHash != catalogHash)
                    throw new InvalidDataException("Battle Spirits protocol, version or card catalog differs.");
                if (IsHost)
                {
                    if (!ready)
                    {
                        if (packet.type != "hello" || packet.command != null || packet.snapshot != null) throw new InvalidDataException("A deck handshake is required first.");
                        var remote = BattleReplay.ValidateDeck(cards, packet.deck);
                        recording = BattleReplay.Create(cards, catalogHash, localDeck, remote, 2026, localName, BattleReplay.Name(packet.name, "Guest"));
                        match = BattleReplay.Simulate(cards, recording, 0); ready = true; Refresh(); SendState(Array.Empty<BattleEvent>());
                        return;
                    }
                    if (packet.type != "command" || packet.deck != null || packet.snapshot != null) throw new InvalidDataException("Only player commands may be sent to the host.");
                    BattleReplay.ValidateCommand(packet.command);
                    // The only remote socket always owns seat 1. The packet cannot choose an actor.
                    if (packet.revision != revision) { SendError("The match advanced. Choose from the current state."); SendState(Array.Empty<BattleEvent>()); return; }
                    if (!Execute(1, packet.command, out var error)) { SendError(error); SendState(Array.Empty<BattleEvent>()); }
                }
                else
                {
                    if (packet.type == "error") { clientPending = false; Status = packet.error ?? "LAN command rejected."; Changed?.Invoke(); return; }
                    if (packet.type != "state" || packet.snapshot == null || packet.snapshot.ViewerIndex != Seat || packet.snapshot.Players == null || packet.snapshot.Players.Length != 2 ||
                        packet.snapshot.Players[0] == null || packet.snapshot.Players[1] == null || packet.snapshot.Players[0].Hand == null || packet.snapshot.Players[0].Hand.Length != 0 ||
                        packet.snapshot.PendingChoice != null && packet.snapshot.PendingChoice.Owner != Seat || packet.revision < revision ||
                        packet.snapshot.DecisionPlayerIndex != Seat && (packet.legal?.Length ?? 0) != 0)
                        throw new InvalidDataException("Invalid owner-filtered LAN state.");
                    ValidateRemoteState(packet);
                    ready = true; revision = packet.revision; View = packet.snapshot; legal = packet.legal ?? Array.Empty<BattleCommand>(); clientPending = false;
                    log.Clear(); if (packet.log != null) log.AddRange(packet.log.Take(300));
                    Status = View.WinnerIndex >= 0 ? View.Players[View.WinnerIndex].Name + " wins." : View.DecisionPlayerIndex == Seat ? "Your " + View.Phase + " decision." : "Waiting for opponent.";
                    Changed?.Invoke();
                    if (packet.events != null) foreach (var e in packet.events) if (e != null) CommittedEvent?.Invoke(FilterEvent(e, Seat));
                }
            }
            catch (Exception e) { Disconnect("Battle Spirits LAN rejected: " + e.Message); }
        }
        private void SendError(string error) { var packet = Packet("error"); packet.error = error; transport.Send(packet); }
        private static void ValidateRemoteState(BattleLanPacket packet)
        {
            var view = packet.snapshot;
            if (view.WinnerIndex < -1 || view.WinnerIndex > 1 || view.ActivePlayerIndex < 0 || view.ActivePlayerIndex > 1 ||
                view.DecisionPlayerIndex < -1 || view.DecisionPlayerIndex > 1 || !Enum.IsDefined(typeof(MatchPhase), view.Phase) ||
                (packet.legal?.Length ?? 0) > 20000 || (packet.events?.Length ?? 0) > 20000 || (packet.log?.Length ?? 0) > 300)
                throw new InvalidDataException("Invalid LAN state bounds.");
            foreach (var p in view.Players)
            {
                if (p.Index < 0 || p.Index > 1 || p.Life < 0 || p.ReserveNormal < 0 || p.TrashNormal < 0 || p.HandCount < 0 || p.DeckCount < 0 ||
                    p.Hand == null || p.Discard == null || p.Removed == null || p.Revealed == null || p.Field == null ||
                    p.Hand.Length > 500 || p.Discard.Length > 500 || p.Removed.Length > 500 || p.Revealed.Length > 500 || p.Field.Length > 500 ||
                    p.Field.Any(s => s == null || s.Owner != p.Index || string.IsNullOrEmpty(s.CardId)))
                    throw new InvalidDataException("Invalid LAN player data.");
            }
            if (view.Players[0].Index != 0 || view.Players[1].Index != 1 || view.Players[1].Hand.Length != view.Players[1].HandCount)
                throw new InvalidDataException("Invalid LAN player seats.");
            if (packet.legal != null) foreach (var command in packet.legal) BattleReplay.ValidateCommand(command);
            var choice = view.PendingChoice;
            if (choice != null && (choice.Options == null || choice.Options.Length > 2000 || choice.Min < 0 || choice.Max < choice.Min || choice.Max > choice.Options.Length || choice.Options.Any(o => o == null)))
                throw new InvalidDataException("Invalid LAN pending choice.");
        }
        private void SendState(IEnumerable<BattleEvent> events)
        {
            try
            {
                var packet = Packet("state"); packet.snapshot = match.GetSnapshot(1); packet.legal = match.GetLegalCommands(1).ToArray();
                packet.events = events.Select(e => FilterEvent(e, 1)).ToArray(); packet.log = match.Log.ToArray(); transport.Send(packet);
            }
            catch (Exception e) { Disconnect(e.Message); }
        }
        private static BattleEvent FilterEvent(BattleEvent e, int viewer)
        {
            // Even if a future effect annotates a draw event, another player's card identity stays private.
            bool hidden = e.Actor != viewer && (e.Kind.IndexOf("draw", StringComparison.OrdinalIgnoreCase) >= 0 || e.Kind == "OpeningRedrawn");
            return new BattleEvent { Kind = e.Kind, Actor = e.Actor, CardId = hidden ? "" : e.CardId, SpiritId = hidden ? -1 : e.SpiritId, TargetId = hidden ? -1 : e.TargetId, Amount = e.Amount };
        }
        private void Disconnect(string message)
        {
            transport?.Dispose(); transport = null; ready = clientPending = false; legal = Array.Empty<BattleCommand>();
            Status = message; Changed?.Invoke();
        }
        public void StartReplay(string path)
        {
            MainThread(); var document = BattleReplay.Load(storageRoot, path, cards, catalogHash); var initial = BattleReplay.Simulate(cards, document, 0);
            Clear(); replay = document; match = initial; IsReplay = true; Refresh();
        }
        public void StepReplay(int delta)
        {
            MainThread(); if (!IsReplay || replay == null || PresentationBusy || delta == 0) return;
            int next = (int)Math.Max(0L, Math.Min(replay.commands.Count, (long)ReplayPosition + delta));
            if (next == ReplayPosition) return;
            match = BattleReplay.Simulate(cards, replay, next); ReplayPosition = next; Refresh();
            if (delta == 1) foreach (var e in match.LastEvents) CommittedEvent?.Invoke(FilterEvent(e, Seat));
        }
        public static string[] ReplayFiles(string storageRoot) => BattleReplay.Files(storageRoot);
        public void Dispose()
        {
            if (disposed) return; MainThread(); transport?.Dispose(); transport = null; disposed = true;
            ready = clientPending = false; legal = Array.Empty<BattleCommand>(); Changed = null; CommittedEvent = null;
        }
    }
}
