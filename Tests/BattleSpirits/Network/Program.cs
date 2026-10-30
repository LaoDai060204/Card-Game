using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using LaoDai.BattleSpirits.Core;
using LaoDai.BattleSpirits.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal static class Entry
{
    private sealed class Peer : IDisposable
    {
        public readonly BattleLanClient Client = new BattleLanClient();
        public readonly List<WireMessage> Messages = new List<WireMessage>();
        public int Seat = -1;
        public FilteredMatchView State;
        public LobbyPlayerView[] Lobby;
        public bool Pumping;
        public Peer()
        {
            Client.MessageReceived += message =>
            {
                Require(Pumping, "Callback ran outside Pump.");
                Messages.Add(message);
                if (message.type == "welcome") Seat = message.playerIndex;
                if (message.type == "state") State = message.view;
                if (message.type == "lobby") Lobby = message.players;
            };
        }
        public int Count(string type) => Messages.Count(message => message.type == type);
        public void Pump() { Pumping = true; try { Client.Pump(); } finally { Pumping = false; } }
        public void Dispose() { Client.Dispose(); }
    }

    private static int scenarios, networkActions;
    private static CardDefinition[] cards;
    private static string hash;
    private static string[][] decks;
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Pass(string name) { scenarios++; Console.WriteLine("PASS " + name); }

    private static async Task Until(Func<bool> condition, params Peer[] peers)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < 6000)
        {
            foreach (var peer in peers) peer.Pump();
            if (condition()) return;
            await Task.Delay(2);
        }
        throw new TimeoutException("Timed out waiting for a network test condition.");
    }
    private static async Task DelayPumping(int milliseconds, params Peer[] peers)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < milliseconds)
        { foreach (var peer in peers) peer.Pump(); await Task.Delay(2); }
    }
    private static async Task Join(Peer peer, int port, string name, params Peer[] others)
    {
        await peer.Client.ConnectAsync("127.0.0.1", port, name, hash);
        await Until(() => peer.Seat >= 0, new[] { peer }.Concat(others).ToArray());
    }
    private static async Task ExpectError(Peer peer, WireMessage command, params Peer[] others)
    {
        int previous = peer.Count("error");
        await peer.Client.SendAsync(command);
        await Until(() => peer.Count("error") > previous, new[] { peer }.Concat(others).ToArray());
    }
    private static async Task StartMatch(Peer a, Peer b)
    {
        await a.Client.SendAsync(new WireMessage { type = "deck", deck = decks[0] });
        await b.Client.SendAsync(new WireMessage { type = "deck", deck = decks[1] });
        await Until(() => a.Lobby != null && a.Lobby.All(player => player.deckCount == 40), a, b);
        await a.Client.SendAsync(new WireMessage { type = "ready", ready = true });
        await b.Client.SendAsync(new WireMessage { type = "ready", ready = true });
        await Until(() => a.Lobby.All(player => player.ready), a, b);
        int states = a.Count("state"), otherStates = b.Count("state");
        await a.Client.SendAsync(new WireMessage { type = "start" });
        await Until(() => a.Count("state") > states && b.Count("state") > otherStates
            && a.State.phase == "Opening" && b.State.phase == "Opening", a, b);
    }
    private static async Task<bool> Action(Peer sender, Peer other, WireMessage action)
    {
        int states = sender.Count("state"), otherStates = other.Count("state"), errors = sender.Count("error");
        action.type = "action";
        await sender.Client.SendAsync(action);
        await Until(() => sender.Count("state") > states || sender.Count("error") > errors, sender, other);
        bool accepted = sender.Count("state") > states;
        if (accepted) await Until(() => other.Count("state") > otherStates, sender, other);
        networkActions++;
        return accepted;
    }
    private static async Task PlayCompleteMatch(Peer a, Peer b)
    {
        var peers = new[] { a, b };
        int actions = 0;
        while (a.State.phase != "GameOver" && actions++ < 600)
        {
            var view = a.State;
            var peer = peers[view.decisionPlayer];
            var other = peers[1 - view.decisionPlayer];
            WireMessage command;
            switch (view.phase)
            {
                case "Opening": command = new WireMessage { action = "opening" }; break;
                case "Main":
                    for (int i = peer.State.ownHand.Length - 1; i >= 0; i--)
                        if (await Action(peer, other, new WireMessage { action = "summon", index = i })) break;
                    Require(await Action(peer, other, new WireMessage { action = view.turn == 1 ? "endTurn" : "attackStep" }), "Main transition rejected.");
                    continue;
                case "Attack":
                    var attacker = peer.State.players[peer.Seat].field.FirstOrDefault(spirit => !spirit.exhausted);
                    command = attacker == null ? new WireMessage { action = "endTurn" }
                        : new WireMessage { action = "attack", index = attacker.instanceId };
                    break;
                case "FlashBeforeBlock":
                case "FlashAfterBlock": command = new WireMessage { action = "passFlash" }; break;
                case "Block": command = new WireMessage { action = "takeLife" }; break;
                default: throw new Exception("Unknown phase: " + view.phase);
            }
            Require(await Action(peer, other, command), "Valid action rejected: " + command.action);
        }
        Require(a.State.phase == "GameOver" && b.State.phase == "GameOver", "TCP match did not finish.");
        Require(a.State.winner == b.State.winner && a.State.winner >= 0, "Clients disagree about winner.");
        var events = a.Messages.Where(message => message.view != null).SelectMany(message => message.view.events).ToArray();
        Require(events.Any(item => item.kind == "Summoned") && events.Any(item => item.kind == "Attacked")
            && events.Any(item => item.kind == "LifeLost"), "Actual battle events did not traverse TCP.");
    }
    private static async Task RawWrite(NetworkStream stream, string line)
    { byte[] bytes = Encoding.UTF8.GetBytes(line + "\n"); await stream.WriteAsync(bytes, 0, bytes.Length); }
    private static async Task<string> ReadLine(StreamReader reader, int milliseconds = 6000)
    {
        var read = reader.ReadLineAsync();
        if (await Task.WhenAny(read, Task.Delay(milliseconds)) != read) throw new TimeoutException("Raw TCP read timed out.");
        return await read;
    }

    public static async Task<int> Main(string[] args)
    {
        string fixture = args.Length > 0 ? args[0] : "Tests/BattleSpirits/Network/Fixtures/vanilla-cards.json";
        cards = JsonConvert.DeserializeObject<CardsDatabase>(File.ReadAllText(fixture)).cards;
        hash = CatalogFingerprint.Compute(cards);
        decks = StarterDecks.CreatePair(cards).Select(deck => deck.cards).ToArray();
        Require(hash == CatalogFingerprint.Compute(cards.Reverse().ToArray()), "Catalog hash depends on order.");
        Pass("Catalog fingerprint is order independent");
        using (var server = new BattleLanServer(cards))
        using (var a = new Peer())
        using (var b = new Peer())
        {
            server.Start(0);
            Require(server.IsRunning && server.Port > 0, "Server did not bind.");
            await Join(a, server.Port, "Người chơi Đỏ");
            await Join(b, server.Port, "Người chơi Xanh", a);
            Require(a.Seat == 0 && b.Seat == 1, "Assigned seats were not stable.");
            Pass("Two real TCP clients join with Vietnamese names and main-thread Pump callbacks");
            await ExpectError(a, new WireMessage { type = "ready", ready = true }, b);
            await ExpectError(a, new WireMessage { type = "deck", deck = new[] { "missing-card" } }, b);
            await ExpectError(a, new WireMessage { type = "action", action = "endTurn" }, b);
            Pass("Server rejects invalid decks and actions outside a match");
            int chats = b.Count("chat");
            await a.Client.SendAsync(new WireMessage { type = "chat", text = "Chào bạn, đấu Battle Spirits nhé!" });
            await Until(() => b.Count("chat") > chats, a, b);
            Require(b.Messages.Last(message => message.type == "chat").name == "Người chơi Đỏ", "Chat identity was not server assigned.");
            Pass("UTF-8 chat preserves the server-assigned sender name");
            using (var third = new Peer())
            {
                await third.Client.ConnectAsync("127.0.0.1", server.Port, "Người thứ ba", hash);
                await Until(() => third.Count("error") > 0 && third.Count("disconnected") > 0, a, b, third);
                Require(third.Seat == -1, "Third player entered a full room.");
            }
            Pass("Third participant is rejected without replacing either player");
            await StartMatch(a, b);
            foreach (var peer in new[] { a, b })
            {
                Require(peer.State.viewer == peer.Seat && peer.State.ownHand.Length == 4, "Opening hand filtering incorrect.");
                var json = JObject.FromObject(peer.Messages.Last(message => message.type == "state"));
                foreach (var player in (JArray)json["view"]["players"])
                    Require(player["hand"] == null && player["Hand"] == null && player["deck"] == null && player["Deck"] == null,
                        "Public player view leaks private card lists.");
                Require(!json.Descendants().OfType<JProperty>().Any(property => property.Name.IndexOf("seed", StringComparison.OrdinalIgnoreCase) >= 0), "State leaks shuffle seed.");
            }
            Pass("Views reveal own hand only, never opponent hand, deck order or seed");
            int wrongActor = 1 - a.State.decisionPlayer;
            await ExpectError(new[] { a, b }[wrongActor], new WireMessage { type = "action", action = "opening", playerIndex = 1 - wrongActor }, new[] { a, b }[1 - wrongActor]);
            Require(a.State.phase == "Opening", "Forged actor changed the match.");
            await ExpectError(b, new WireMessage { type = "start" }, a);
            await ExpectError(a, new WireMessage { type = "deck", deck = decks[1] }, b);
            Pass("Connection identity overrides forged actor fields; live deck/restart changes are rejected");
            await PlayCompleteMatch(a, b);
            Pass("A complete match over TCP covers summon, attack, Flash, Life loss and winner");
            await Until(() => a.Lobby != null && a.Lobby.All(player => !player.ready), a, b);
            await StartMatch(a, b);
            b.Client.Disconnect();
            await Until(() => a.State.phase == "GameOver" && a.State.winner == 0 && !a.Lobby[1].connected, a, b);
            Pass("Disconnect concedes on the server and clears ready state");
            using (var replacement = new Peer())
            {
                await Join(replacement, server.Port, "Người mới", a);
                await DelayPumping(150, a, replacement);
                Require(replacement.Seat == 1 && replacement.Count("state") == 0, "Replacement inherited the previous occupant's hand.");
            }
            Pass("Replacement enters a fresh lobby without old private state");
            var stopping = Stopwatch.StartNew();
            int previousPort = server.Port;
            server.Stop();
            Require(stopping.ElapsedMilliseconds < 500 && !server.IsRunning, "Stop blocked its caller.");
            await Until(() => a.Count("disconnected") > 0, a);
            server.Start(previousPort);
            Require(server.IsRunning && server.Port == previousPort, "Same-port restart failed.");
            Pass("Server stop is nonblocking and permits same-port restart");
            using (var mismatch = new Peer())
            {
                await mismatch.Client.ConnectAsync("127.0.0.1", server.Port, "Sai dữ liệu", "different-hash");
                await Until(() => mismatch.Count("error") > 0 && mismatch.Count("disconnected") > 0, mismatch);
                Require(mismatch.Seat < 0, "Mismatched catalog entered room.");
            }
            await Task.Delay(20);
            Pass("Catalog mismatch is rejected before welcome");
            using (var raw = new TcpClient())
            {
                await raw.ConnectAsync("127.0.0.1", server.Port);
                var stream = raw.GetStream();
                using (var reader = new StreamReader(stream, new UTF8Encoding(false), false, 4096, true))
                {
                    await RawWrite(stream, "{broken-json");
                    Require(Protocol.Decode(await ReadLine(reader)).type == "error", "Malformed JSON was not rejected.");
                    await RawWrite(stream, Protocol.Encode(new WireMessage { type = "hello", name = "Raw", catalogHash = hash, version = 999 }));
                    Require(Protocol.Decode(await ReadLine(reader)).type == "error", "Wrong version was accepted.");
                    Require(await ReadLine(reader) == null, "Wrong version did not close connection.");
                }
            }
            await Task.Delay(20);
            Pass("Malformed JSON is recoverable and incompatible protocol is disconnected");
            using (var raw = new TcpClient())
            {
                await raw.ConnectAsync("127.0.0.1", server.Port);
                var stream = raw.GetStream();
                byte[] excess = Encoding.UTF8.GetBytes(new string('X', Protocol.MaxLineBytes + 1));
                await stream.WriteAsync(excess, 0, excess.Length);
                var read = stream.ReadAsync(new byte[1], 0, 1);
                Require(await Task.WhenAny(read, Task.Delay(6000)) == read, "Overlong line left the connection hanging.");
                try { Require(await read == 0, "Overlong line was accepted."); } catch (IOException) { }
            }
            Pass("Overlong protocol line closes its connection");
            server.Stop();
        }
        Console.WriteLine(JsonConvert.SerializeObject(new { status = "PASS", scenarios, networkActions, actualTcp = true,
            unityPlayTested = false, windowsLanTested = false, scope = "Production code on Linux loopback; no Unity or Internet deployment" }));
        return 0;
    }
}
