using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using LaoDai.BattleSpirits.Core;

namespace LaoDai.BattleSpirits.Networking
{
    /// <summary>A two-player authoritative room. LAN and Internet use the same protocol.</summary>
    public sealed class BattleLanServer : IDisposable
    {
        private sealed class Outgoing
        {
            public string Data;
            public bool CloseAfter;
        }

        private sealed class Session
        {
            public readonly TcpClient Client;
            public readonly CancellationTokenSource Stop;
            public readonly int Index;
            public readonly ConcurrentQueue<Outgoing> Outgoing = new ConcurrentQueue<Outgoing>();
            public readonly SemaphoreSlim Signal = new SemaphoreSlim(0);
            public bool Hello;
            public string Name = string.Empty;
            public string[] Deck = Array.Empty<string>();
            public bool Ready;
            private int queued;
            private int closed;
            private int finalQueued;

            public bool Closed => Volatile.Read(ref closed) != 0;

            public Session(TcpClient client, int index, CancellationToken token)
            {
                Client = client; Index = index;
                Stop = CancellationTokenSource.CreateLinkedTokenSource(token);
                Client.NoDelay = true;
            }

            public void Queue(WireMessage message, bool closeAfter = false)
            {
                if (Closed || Volatile.Read(ref finalQueued) != 0) return;
                if (closeAfter) Interlocked.Exchange(ref finalQueued, 1);
                if (Interlocked.Increment(ref queued) > 64) { Close(); return; }
                Outgoing.Enqueue(new Outgoing { Data = Protocol.Encode(message), CloseAfter = closeAfter });
                Signal.Release();
            }

            public async Task SendLoopAsync()
            {
                try
                {
                    while (!Stop.IsCancellationRequested)
                    {
                        await Signal.WaitAsync(Stop.Token).ConfigureAwait(false);
                        if (!Outgoing.TryDequeue(out var item)) continue;
                        Interlocked.Decrement(ref queued);
                        await WireWriter.WriteAsync(Client.GetStream(), item.Data, Stop.Token).ConfigureAwait(false);
                        if (item.CloseAfter) break;
                    }
                }
                catch (Exception) { }
                finally { Close(); }
            }

            public void Close()
            {
                if (Interlocked.Exchange(ref closed, 1) != 0) return;
                try { Stop.Cancel(); } catch (ObjectDisposedException) { }
                try { Client.Close(); } catch (ObjectDisposedException) { }
            }
        }

        private sealed class Run
        {
            public readonly object Gate = new object();
            public readonly CancellationTokenSource Stop = new CancellationTokenSource();
            public readonly Session[] Sessions = new Session[2];
            public readonly TcpListener Listener;
            public BattleMatch Match;
            public Session[] MatchParticipants;
            public Run(int port) { Listener = new TcpListener(IPAddress.Any, port); }
        }

        private readonly object lifecycleGate = new object();
        private readonly IReadOnlyList<CardDefinition> catalog;
        private readonly string catalogHash;
        private Run current;

        public event Action<string> Diagnostic;
        public int Port { get; private set; }
        public string CatalogHash => catalogHash;
        public bool IsRunning { get { lock (lifecycleGate) return current != null; } }

        public BattleLanServer(IReadOnlyList<CardDefinition> catalog)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            catalogHash = CatalogFingerprint.Compute(catalog);
        }

        public void Start(int port = Protocol.DefaultPort)
        {
            if (port < 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            lock (lifecycleGate)
            {
                if (current != null) throw new InvalidOperationException("This server is already running.");
                var run = new Run(port);
                run.Listener.Start();
                Port = ((IPEndPoint)run.Listener.LocalEndpoint).Port;
                current = run;
                _ = AcceptAsync(run);
            }
            Diagnostic?.Invoke("Battle Spirits server listening on TCP " + Port + ".");
        }

        public void Stop()
        {
            Run run;
            lock (lifecycleGate) { run = current; current = null; }
            if (run == null) return;
            run.Stop.Cancel();
            run.Listener.Stop();
            lock (run.Gate)
                foreach (var session in run.Sessions) session?.Close();
        }

        private async Task AcceptAsync(Run run)
        {
            try
            {
                while (!run.Stop.IsCancellationRequested)
                {
                    var client = await run.Listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    Session session = null;
                    lock (run.Gate)
                    {
                        if (!run.Stop.IsCancellationRequested)
                            for (int index = 0; index < run.Sessions.Length; index++)
                                if (run.Sessions[index] == null)
                                {
                                    session = new Session(client, index, run.Stop.Token);
                                    run.Sessions[index] = session;
                                    break;
                                }
                    }
                    if (session == null) _ = RejectAsync(client, "Phòng đã đủ hai người chơi.");
                    else _ = HandleSessionAsync(run, session);
                }
            }
            catch (Exception exception)
            {
                if (!run.Stop.IsCancellationRequested)
                    Diagnostic?.Invoke("Server listener stopped: " + exception.Message);
            }
        }

        private static async Task RejectAsync(TcpClient client, string text)
        {
            try
            {
                await WireWriter.WriteAsync(client.GetStream(), Protocol.Encode(new WireMessage
                { type = "error", text = text }), CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception) { }
            finally { client.Close(); }
        }

        private async Task HandleSessionAsync(Run run, Session session)
        {
            _ = session.SendLoopAsync();
            try
            {
                var reader = new WireReader(session.Client.GetStream());
                var helloDeadline = DateTime.UtcNow.AddSeconds(7);
                while (!session.Stop.IsCancellationRequested)
                {
                    var read = reader.ReadLineAsync(session.Stop.Token);
                    if (!session.Hello)
                    {
                        int remaining = Math.Max(0, (int)(helloDeadline - DateTime.UtcNow).TotalMilliseconds);
                        var completed = await Task.WhenAny(read, Task.Delay(remaining, session.Stop.Token)).ConfigureAwait(false);
                        if (completed != read || remaining == 0)
                        {
                            session.Close();
                            try { await read.ConfigureAwait(false); } catch (Exception) { }
                            return;
                        }
                    }
                    string line = await read.ConfigureAwait(false);
                    if (line == null) break;
                    lock (run.Gate)
                    {
                        if (run.Stop.IsCancellationRequested) return;
                        try { HandleMessage(run, session, Protocol.Decode(line)); }
                        catch (Exception exception) when (exception is Newtonsoft.Json.JsonException
                            || exception is ArgumentException || exception is InvalidOperationException)
                        {
                            Error(session, "Yêu cầu không hợp lệ.");
                        }
                    }
                }
            }
            catch (Exception) { }
            finally
            {
                session.Close();
                lock (run.Gate)
                {
                    if (ReferenceEquals(run.Sessions[session.Index], session))
                    {
                        run.Sessions[session.Index] = null;
                        if (!run.Stop.IsCancellationRequested)
                        {
                            if (run.Match != null && run.Match.State.Phase != MatchPhase.GameOver
                                && ReferenceEquals(run.MatchParticipants[session.Index], session))
                            {
                                run.Match.TryConcede(session.Index, out _);
                                foreach (var player in run.Sessions) if (player != null) player.Ready = false;
                                BroadcastState(run);
                            }
                            BroadcastLobby(run);
                        }
                    }
                }
            }
        }

        private void HandleMessage(Run run, Session session, WireMessage message)
        {
            if (!session.Hello)
            {
                if (message.type != "hello" || message.version != Protocol.Version
                    || !string.Equals(message.catalogHash, catalogHash, StringComparison.Ordinal))
                {
                    Error(session, "Phiên bản hoặc dữ liệu thẻ khác máy chủ. Hãy cập nhật cùng bản game.", true);
                    return;
                }
                var name = (message.name ?? string.Empty).Trim();
                if (name.Length == 0 || name.Length > Protocol.MaxNameLength
                    || name.Any(character => char.IsControl(character) || character == '<' || character == '>'))
                {
                    Error(session, "Tên người chơi cần từ 1 đến 24 ký tự, không chứa ký tự điều khiển.", true);
                    return;
                }
                if (run.Match != null && run.Match.State.Phase == MatchPhase.GameOver)
                {
                    // A replacement connection starts a fresh lobby; it must not
                    // inherit the previous occupant's private hand.
                    run.Match = null;
                    run.MatchParticipants = null;
                    foreach (var player in run.Sessions) if (player != null) player.Ready = false;
                }
                session.Name = name;
                session.Hello = true;
                session.Queue(new WireMessage { type = "welcome", playerIndex = session.Index, catalogHash = catalogHash });
                BroadcastLobby(run);
                return;
            }

            bool playing = run.Match != null && run.Match.State.Phase != MatchPhase.GameOver;
            switch (message.type)
            {
                case "deck":
                    if (playing) { Error(session, "Không thể đổi bộ bài khi trận đấu đang diễn ra."); return; }
                    var validation = DeckValidator.Validate(catalog, message.deck, true);
                    if (!validation.IsValid) { Error(session, string.Join("\n", validation.Errors)); return; }
                    session.Deck = (string[])message.deck.Clone();
                    session.Ready = false;
                    BroadcastLobby(run);
                    break;
                case "ready":
                    if (playing) { Error(session, "Trận đấu đã bắt đầu."); return; }
                    if (message.ready && !DeckValidator.Validate(catalog, session.Deck, true).IsValid)
                    { Error(session, "Chọn bộ bài hợp lệ trước khi sẵn sàng."); return; }
                    session.Ready = message.ready;
                    BroadcastLobby(run);
                    break;
                case "start":
                    if (playing) { Error(session, "Trận đấu đã bắt đầu."); return; }
                    if (session.Index != 0) { Error(session, "Người tạo phòng mới được bắt đầu trận."); return; }
                    if (run.Sessions.Any(player => player == null || !player.Hello || !player.Ready))
                    { Error(session, "Cần đủ hai người đã chọn bộ bài và sẵn sàng."); return; }
                    var bytes = new byte[8];
                    using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
                    run.Match = BattleMatch.Create(catalog, run.Sessions[0].Deck, run.Sessions[1].Deck,
                        BitConverter.ToInt32(bytes, 0), bytes[4] & 1);
                    run.MatchParticipants = (Session[])run.Sessions.Clone();
                    for (int i = 0; i < 2; i++) run.Match.State.Players[i].Name = run.Sessions[i].Name;
                    BroadcastState(run);
                    break;
                case "action":
                    if (!playing || !ReferenceEquals(run.MatchParticipants[session.Index], session))
                    { Error(session, "Không có trận đấu đang diễn ra."); return; }
                    var command = new BattleCommand
                    {
                        action = message.action, index = message.index, source = message.source,
                        target = message.target, soul = message.soul, redraw = message.redraw
                    };
                    // Actor identity comes exclusively from this TCP connection.
                    if (!BattleCommandDispatcher.TryExecute(run.Match, session.Index, command, out var error))
                    { Error(session, error); return; }
                    run.Match.AssertInvariants();
                    BroadcastState(run);
                    if (run.Match.State.Phase == MatchPhase.GameOver)
                    {
                        foreach (var player in run.Sessions) if (player != null) player.Ready = false;
                        BroadcastLobby(run);
                    }
                    break;
                case "chat":
                    if (string.IsNullOrWhiteSpace(message.text) || message.text.Length > Protocol.MaxChatLength
                        || message.text.IndexOf('\0') >= 0)
                    { Error(session, "Tin nhắn cần từ 1 đến 300 ký tự."); return; }
                    foreach (var player in run.Sessions)
                        if (player != null && player.Hello)
                            player.Queue(new WireMessage { type = "chat", name = session.Name, text = message.text });
                    break;
                default:
                    Error(session, "Loại yêu cầu không được hỗ trợ.");
                    break;
            }
        }

        private static void Error(Session session, string text, bool close = false)
        { session.Queue(new WireMessage { type = "error", text = text }, close); }

        private static void BroadcastLobby(Run run)
        {
            var players = run.Sessions.Select((session, index) => new LobbyPlayerView
            {
                playerIndex = index, connected = session != null && session.Hello,
                name = session?.Name ?? string.Empty, ready = session != null && session.Ready,
                deckCount = session?.Deck.Length ?? 0
            }).ToArray();
            foreach (var session in run.Sessions)
                if (session != null && session.Hello)
                    session.Queue(new WireMessage { type = "lobby", players = players });
        }

        private static void BroadcastState(Run run)
        {
            if (run.Match == null) return;
            for (int index = 0; index < 2; index++)
            {
                var session = run.Sessions[index];
                if (session != null && session.Hello && ReferenceEquals(run.MatchParticipants[index], session))
                    session.Queue(new WireMessage { type = "state", view = ViewFactory.Create(run.Match, index) });
            }
        }

        public void Dispose() { Stop(); }
    }
}
