using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MDPro3;
using MDPro3.Duel.YGOSharp;

internal static class TransportTests
{
    private static int passed;
    private static readonly BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static T Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T error) { return error; }
        throw new Exception("Expected " + typeof(T).Name);
    }
    private static void Until(Func<bool> predicate, string message)
        => Require(SpinWait.SpinUntil(predicate, 3000), message);
    private static object Field(string name) => typeof(TcpHelper).GetField(name, PrivateStatic).GetValue(null);
    private static bool CloseExpected(TcpClient expected, bool onlyIfDisconnected)
        => (bool)typeof(TcpHelper).GetMethod("TryCloseConnection", PrivateStatic).Invoke(null, new object[] { expected, onlyIfDisconnected });
    private static void CloseCurrent() => CloseExpected(TcpHelper.tcpClient, false);
    private static void WaitLink() => Until(() => TcpHelper.canJoin &&
        (Field("linkThread") is not Thread thread || !thread.IsAlive), "Link worker did not release its gate");
    private static void Case(string name, Action action)
    {
        MDPro3.Program.Running = true;
        MDPro3.Program.instance.room.duelEnded = false;
        CloseCurrent(); WaitLink();
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        finally { CloseCurrent(); WaitLink(); }
    }

    private sealed class SocketPair : IDisposable
    {
        public readonly TcpClient Client, Peer;
        private readonly TcpListener listener;
        public SocketPair()
        {
            listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Client = new TcpClientWithTimeout("127.0.0.1", port, 1000).Connect();
            Peer = listener.AcceptTcpClient();
            Client.ReceiveTimeout = Peer.ReceiveTimeout = 2000;
        }
        public void Dispose() { Client.Close(); Peer.Close(); listener.Stop(); }
    }

    private sealed class Session : IDisposable
    {
        public readonly TcpClient Client, Peer;
        private readonly TcpListener listener;
        public bool Background;
        public Session(string name = "Player", string password = "", Action callback = null)
        {
            listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            var ready = new ManualResetEventSlim();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Require(TcpHelper.LinkStart("127.0.0.1", name, port.ToString(), password, false, () =>
            {
                Background = Thread.CurrentThread.IsBackground;
                ready.Set(); callback?.Invoke();
            }), "Session was not accepted");
            var accept = listener.AcceptTcpClientAsync();
            Require(accept.Wait(3000), "Loopback server did not accept");
            Peer = accept.Result; Peer.ReceiveTimeout = 3000;
            Require(ready.Wait(3000), "Success callback missing");
            Client = TcpHelper.tcpClient;
            if (callback == null) WaitLink();
        }
        public byte[][] Handshake()
            => Enumerable.Range(0, 3).Select(_ => SocketMaster.ReadPacket(Peer.GetStream())).ToArray();
        public void Dispose()
        {
            if (ReferenceEquals(TcpHelper.tcpClient, Client)) CloseCurrent();
            else Client.Close();
            Peer.Close(); listener.Stop();
        }
    }

    private sealed class Pending : IAsyncResult
    {
        public readonly ManualResetEvent Signal = new ManualResetEvent(false);
        public object AsyncState => null;
        public WaitHandle AsyncWaitHandle => Signal;
        public bool CompletedSynchronously => false;
        public bool IsCompleted => false;
    }
    private sealed class PendingConnector : TcpClientWithTimeout
    {
        public readonly Pending Pending = new Pending();
        public Socket Candidate;
        public PendingConnector() : base("127.0.0.1", 12345, 40) { }
        protected override IAsyncResult BeginConnect(TcpClient client)
        { Candidate = client.Client; return Pending; }
    }
    private sealed class DelayedEofStream : NetworkStream
    {
        public readonly ManualResetEventSlim Reading = new ManualResetEventSlim();
        public readonly ManualResetEventSlim Release = new ManualResetEventSlim();
        public DelayedEofStream(Socket socket) : base(socket, false) { }
        public override int Read(byte[] buffer, int offset, int count)
        { Reading.Set(); Require(Release.Wait(3000), "Delayed reader not released"); return 0; }
    }
    private static byte[] Unicode(string text, int count = 20)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.WriteUnicode(text, count); return stream.ToArray();
    }
    private static string Visible(byte[] bytes)
    {
        string value = Encoding.Unicode.GetString(bytes);
        int end = value.IndexOf('\0');
        Require(end >= 0, "Protocol text lacks its NUL terminator");
        return value.Substring(0, end);
    }
    private static void SendChat(TcpClient peer, int value)
    {
        byte[] frame = { 5, 0, (byte)StocMessage.Chat, 0, 0, 0, 0 };
        Array.Copy(BitConverter.GetBytes(value), 0, frame, 3, 4);
        peer.GetStream().Write(frame, 0, frame.Length);
    }
    private static int QueuedIncoming()
    {
        var packets = (List<byte[]>)Field("datas");
        lock (packets) return packets.Count;
    }

    private static void Main()
    {
        Case("A deferred disconnect cannot close a newly published connection", () =>
        {
            using var previous = new Session(); previous.Handshake();
            var staleClient = previous.Client;
            TcpHelper.onDisConnected = true;
            CloseCurrent();
            using var replacement = new Session(); replacement.Handshake();
            Require(!CloseExpected(staleClient, true), "Stale cleanup accepted a replacement connection");
            Require(!CloseExpected(null, true), "Failed-attempt cleanup accepted a replacement connection");
            Require(ReferenceEquals(TcpHelper.tcpClient, replacement.Client) && !TcpHelper.onDisConnected,
                "Stale disconnect damaged the active session");
            SendChat(replacement.Peer, 4321);
            Until(() => QueuedIncoming() > 0, "Replacement stopped receiving after stale cleanup");
            TcpHelper.PerFrameFunction();
            Require(MDPro3.Program.instance.room.LastChat == 4321, "Replacement message not delivered");
        });
        Case("An unrelated failed connector cannot mark an active session disconnected", () =>
        {
            using var session = new Session(); session.Handshake();
            Throws<ArgumentOutOfRangeException>(() => new TcpClientWithTimeout("127.0.0.1", 65536, 100).Connect());
            Require(!TcpHelper.onDisConnected && ReferenceEquals(TcpHelper.tcpClient, session.Client),
                "A connector without session ownership changed global transport state");
        });
        Case("A failed top-level join releases its gate and reports disconnection", () =>
        {
            Require(TcpHelper.LinkStart("127.0.0.1", "Player", "65536", "", false, null), "Join request rejected");
            WaitLink();
            Require(TcpHelper.onDisConnected, "Failed join did not report failure");
            TcpHelper.PerFrameFunction();
            Require(!TcpHelper.onDisConnected && TcpHelper.canJoin, "Failed join cleanup kept the gate busy");
        });
        Case("Async connector returns a working real loopback socket without Thread.Abort", () =>
        {
            using var pair = new SocketPair();
            pair.Peer.GetStream().WriteByte(0x7A);
            Require(pair.Client.GetStream().ReadByte() == 0x7A, "Returned socket cannot receive");
        });
        Case("Refused and invalid connections preserve their original error types", () =>
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
            Throws<SocketException>(() => new TcpClientWithTimeout("127.0.0.1", port, 500).Connect());
            Throws<ArgumentOutOfRangeException>(() => new TcpClientWithTimeout("127.0.0.1", 65536, 500).Connect());
        });
        Case("A controlled pending connection times out promptly and closes its socket", () =>
        {
            var connector = new PendingConnector(); var clock = Stopwatch.StartNew();
            Throws<TimeoutException>(() => connector.Connect());
            Require(clock.ElapsedMilliseconds >= 25 && clock.ElapsedMilliseconds < 1000, "Timeout was not bounded");
            Require(connector.Candidate.SafeHandle.IsClosed, "Timed-out socket leaked");
        });
        Case("Fragmented TCP header and payload are read exactly", () =>
        {
            using var pair = new SocketPair();
            var send = Task.Run(() =>
            {
                foreach (byte value in new byte[] { 4, 0, 0x42, 1, 2, 3 })
                { pair.Peer.GetStream().WriteByte(value); Thread.Sleep(2); }
            });
            Require(SocketMaster.ReadPacket(pair.Client.GetStream()).SequenceEqual(new byte[] { 0x42, 1, 2, 3 }), "Fragmented frame corrupted");
            send.GetAwaiter().GetResult();
        });
        Case("Clean EOF, truncated header and truncated payload never produce padded packets", () =>
        {
            foreach (byte[] bytes in new[] { Array.Empty<byte>(), new byte[] { 4 }, new byte[] { 4, 0, 0x42 } })
            {
                using var pair = new SocketPair();
                pair.Peer.GetStream().Write(bytes, 0, bytes.Length); pair.Peer.Close();
                Throws<EndOfStreamException>(() => SocketMaster.ReadPacket(pair.Client.GetStream()));
            }
        });
        Case("A zero-length frame is rejected", () =>
        {
            using var pair = new SocketPair(); pair.Peer.GetStream().Write(new byte[2], 0, 2);
            Throws<InvalidDataException>(() => SocketMaster.ReadPacket(pair.Client.GetStream()));
        });
        Case("Fixed UTF-16 fields retain widths, NUL terminators and 0xCC padding", () =>
        {
            foreach (string value in new[] { null, "", "Player", new string('a', 19), new string('a', 20),
                new string('ấ', 40), new string('a', 18) + "😀", new string('a', 17) + "😀", "abc\0hidden" })
            {
                byte[] field = Unicode(value); string visible = Visible(field);
                Require(field.Length == 40 && visible.Length <= 19 && !visible.Contains('\uFFFD'), "Invalid fixed-width field");
                Require(visible.Length == 0 || !char.IsHighSurrogate(visible[visible.Length - 1]), "Split UTF-16 pair");
                int end = (visible.Length + 1) * 2;
                Require(field.Skip(end).All(value => value == 0xCC), "Padding changed");
            }
            Require(Visible(Unicode("abc\0hidden")) == "abc", "Embedded NUL not respected");
            Require(Visible(Unicode(new string('a', 18) + "😀")) == new string('a', 18), "Boundary emoji not safely shortened");
            Throws<ArgumentOutOfRangeException>(() => Unicode("a", 0));
            Throws<ArgumentOutOfRangeException>(() => Unicode("a", -1));
        });
        Case("Real client PlayerInfo and JoinGame handshakes preserve protocol sizes", () =>
        {
            const string secret = "DoNotLog-PrivatePassword-123456";
            using var session = new Session(new string('ấ', 20) + "😀", secret);
            var frames = session.Handshake();
            Require(session.Background, "Join worker is a foreground thread");
            Require(frames[0][0] == (byte)CtosMessage.ExternalAddress, "External address missing");
            Require(frames[1][0] == (byte)CtosMessage.PlayerInfo && frames[1].Length == 41, "Malformed PlayerInfo width");
            Require(Visible(frames[1].Skip(1).ToArray()) == new string('ấ', 19), "Long player name not fitted");
            Require(frames[2][0] == (byte)CtosMessage.JoinGame && frames[2].Length == 49, "Malformed JoinGame width");
            Require(Visible(frames[2].Skip(9).ToArray()) == secret.Substring(0, 19), "Long password damaged frame");
            Require(!UnityEngine.Debug.Messages.Any(message => message.Contains(secret)), "Password leaked into log");
        });
        Case("Duplicate join/sender requests preserve the active connection", () =>
        {
            using var session = new Session(); session.Handshake();
            object sender = Field("senderThread"); TcpHelper.InitializeSender();
            Require(ReferenceEquals(sender, Field("senderThread")), "Duplicate sender worker created");
            Require(!TcpHelper.LinkStart("127.0.0.1", "Other", "1", "", false, null), "Active session replaced");
        });
        Case("Invalid join returns its gate and does not strand canJoin", () =>
        {
            Require(TcpHelper.LinkStart("127.0.0.1", "Player", "not-a-port", "", false, null), "Invalid request not scheduled");
            WaitLink(); Require(TcpHelper.tcpClient == null, "Invalid join published a client");
        });
        Case("An in-flight callback blocks duplicate joins without aborting its worker", () =>
        {
            using var release = new ManualResetEventSlim();
            using var session = new Session(callback: () => release.Wait(3000));
            try
            {
                session.Handshake();
                Require(!TcpHelper.LinkStart("127.0.0.1", "Other", "1", "", false, null), "Concurrent link accepted");
                Require(!TcpHelper.canJoin, "Worker gate released before callback ended");
            }
            finally { release.Set(); WaitLink(); }
        });
        Case("A large queued frame is transmitted fully with a small socket send buffer", () =>
        {
            using var session = new Session(); session.Handshake(); session.Client.SendBufferSize = 512;
            byte[] payload = Enumerable.Range(0, 60000).Select(index => (byte)index).ToArray();
            var message = new Package { Function = 0x42 }; message.Data.writer.Write(payload); TcpHelper.Send(message);
            byte[] received = SocketMaster.ReadPacket(session.Peer.GetStream());
            Require(received.Length == 60001 && received[0] == 0x42 && received.Skip(1).SequenceEqual(payload), "Short send lost data");
        });
        Case("Oversize outgoing frames fail without wrapping the length prefix", () =>
        {
            using var session = new Session(); session.Handshake();
            var message = new Package(); message.Data.writer.Write(new byte[65535]); TcpHelper.Send(message);
            Until(() => TcpHelper.onDisConnected, "Oversize frame did not report failure");
            TcpHelper.PerFrameFunction(); Require(TcpHelper.tcpClient == null, "Failed sender not cleaned up");
        });
        Case("EOF receiver stops once and main-frame cleanup releases the session", () =>
        {
            using var session = new Session(); session.Handshake(); session.Peer.Close();
            Until(() => TcpHelper.onDisConnected, "EOF not reported");
            Thread.Sleep(30); Require(QueuedIncoming() == 0, "EOF flooded incoming queue");
            TcpHelper.PerFrameFunction(); Require(TcpHelper.tcpClient == null && TcpHelper.canJoin, "Closed session not released");
        });
        Case("Reconnect discards old queues and ignores a delayed old reader's EOF", () =>
        {
            using var first = new Session(); first.Handshake();
            using var delayed = new DelayedEofStream(first.Client.Client);
            var receiver = typeof(TcpHelper).GetMethod("Receiver", PrivateStatic, null,
                new[] { typeof(TcpClient), typeof(NetworkStream) }, null);
            Task old = Task.Run(() => receiver.Invoke(null, new object[] { first.Client, delayed }));
            Require(delayed.Reading.Wait(1000), "Old receiver did not block");
            TcpHelper.AddDateJumoLine(new byte[] { 0xFF, 1 });
            TcpHelper.tcpClient = null; first.Client.Close();
            using var second = new Session(); second.Handshake();
            Require(QueuedIncoming() == 0, "Old received queue carried into next session");
            delayed.Release.Set(); Require(old.Wait(1000), "Old receiver did not finish");
            Require(!TcpHelper.onDisConnected, "Old receiver disconnected new session");
            SendChat(second.Peer, 8675309); Until(() => QueuedIncoming() == 1, "New session packet not received");
            TcpHelper.PerFrameFunction();
            Require(MDPro3.Program.instance.room.LastChat == 8675309, "New receiver failed after reconnect");
            var message = new Package { Function = 0x43 }; message.Data.writer.Write(1234); TcpHelper.Send(message);
            byte[] response = SocketMaster.ReadPacket(second.Peer.GetStream());
            Require(response.Length == 5 && response[0] == 0x43 && BitConverter.ToInt32(response, 1) == 1234, "New sender failed after reconnect");
        });
        Console.WriteLine("PASS: " + passed + " TCP transport scenarios using production methods, real loopback sockets and controlled timeout/stale-reader fixtures. No Unity Play test.");
    }
}
