using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MDPro3.BattleSpirits.Core;
using Newtonsoft.Json;

namespace MDPro3.BattleSpirits
{
    [Serializable] internal sealed class BattleLanPacket
    {
        [JsonProperty(Required = Required.Always)] public string protocol = BattleReplay.Protocol;
        [JsonProperty(Required = Required.Always)] public int version = BattleReplay.Version;
        [JsonProperty(Required = Required.Always)] public string catalogHash;
        [JsonProperty(Required = Required.Always)] public string type;
        public string name, error;
        public DeckDefinition deck;
        public BattleCommand command;
        public BattleSnapshot snapshot;
        public BattleCommand[] legal;
        public BattleEvent[] events;
        public string[] log;
        public int revision;
    }
    internal sealed class BattleLanNotice
    {
        internal string type, payload;
        internal BattleLanNotice(string kind, string text = "") { type = kind; payload = text; }
    }
    /// <summary>One peer, length-prefixed UTF8 JSON. Workers only move bounded bytes through queues.</summary>
    internal sealed class BattleLanTransport : IDisposable
    {
        internal const int MaxFrameBytes = 4 * 1024 * 1024, MaxQueue = 64;
        private readonly ConcurrentQueue<BattleLanNotice> incoming = new ConcurrentQueue<BattleLanNotice>();
        private readonly BlockingCollection<byte[]> outgoing = new BlockingCollection<byte[]>(MaxQueue);
        private TcpListener listener;
        private TcpClient client;
        private volatile bool disposed, connected;
        internal bool IsConnected => connected && !disposed;
        internal int Port { get; private set; }
        internal void Host(int port)
        {
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            listener = new TcpListener(IPAddress.Any, port); listener.Start(1); Port = port;
            Run(() => { var peer = listener.AcceptTcpClient(); listener.Stop(); Attach(peer); });
        }
        internal void Join(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host) || host.Length > 253 || port < 1 || port > 65535) throw new ArgumentException("Enter a valid LAN host and port.");
            Run(() =>
            {
                var peer = new TcpClient(); client = peer;
                var pending = peer.BeginConnect(host, port, null, null);
                using (pending.AsyncWaitHandle) { if (!pending.AsyncWaitHandle.WaitOne(10000)) throw new IOException("LAN connection timed out."); }
                peer.EndConnect(pending); Attach(peer);
            });
        }
        private void Run(Action action) => new Thread(() =>
        {
            try { action(); }
            catch (Exception e) { if (!disposed) Fail("LAN connection closed: " + e.Message); }
        }) { IsBackground = true, Name = "BattleSpirits LAN" }.Start();
        private void Attach(TcpClient peer)
        {
            if (disposed) { peer.Close(); return; }
            client = peer; peer.NoDelay = true; peer.ReceiveTimeout = 300000; peer.SendTimeout = 10000;
            connected = true; Enqueue(new BattleLanNotice("connected"));
            Run(() =>
            {
                var stream = peer.GetStream();
                foreach (var bytes in outgoing.GetConsumingEnumerable())
                {
                    var header = new[] { (byte)(bytes.Length >> 24), (byte)(bytes.Length >> 16), (byte)(bytes.Length >> 8), (byte)bytes.Length };
                    stream.Write(header, 0, 4); stream.Write(bytes, 0, bytes.Length);
                }
            });
            var input = peer.GetStream(); var prefix = new byte[4]; var utf8 = new UTF8Encoding(false, true);
            while (!disposed)
            {
                Read(input, prefix);
                int length = (prefix[0] << 24) | (prefix[1] << 16) | (prefix[2] << 8) | prefix[3];
                if (length <= 0 || length > MaxFrameBytes) throw new InvalidDataException("Invalid LAN frame length.");
                var bytes = new byte[length]; Read(input, bytes);
                Enqueue(new BattleLanNotice("frame", utf8.GetString(bytes)));
            }
        }
        private static void Read(Stream stream, byte[] bytes)
        {
            int offset = 0;
            while (offset < bytes.Length) { int count = stream.Read(bytes, offset, bytes.Length - offset); if (count <= 0) throw new EndOfStreamException("Peer disconnected."); offset += count; }
        }
        private void Enqueue(BattleLanNotice notice)
        {
            if (incoming.Count >= MaxQueue) throw new InvalidDataException("LAN input queue limit exceeded.");
            incoming.Enqueue(notice);
        }
        private void Fail(string message)
        {
            connected = false;
            if (incoming.Count < MaxQueue) incoming.Enqueue(new BattleLanNotice("closed", message));
            try { client?.Close(); } catch (SocketException) { }
            try { listener?.Stop(); } catch (SocketException) { }
            outgoing.CompleteAdding();
        }
        internal bool Receive(out BattleLanNotice notice) => incoming.TryDequeue(out notice);
        internal void Send(BattleLanPacket packet)
        {
            if (disposed || !connected) throw new IOException("LAN peer is disconnected.");
            byte[] bytes = Encoding.UTF8.GetBytes(BattleReplay.Encode(packet));
            if (bytes.Length == 0 || bytes.Length > MaxFrameBytes) throw new InvalidDataException("LAN state exceeds the frame limit.");
            if (!outgoing.TryAdd(bytes)) throw new IOException("LAN output queue limit exceeded.");
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; connected = false;
            outgoing.CompleteAdding();
            try { listener?.Stop(); } catch (SocketException) { }
            try { client?.Close(); } catch (SocketException) { }
            while (incoming.TryDequeue(out _)) { }
        }
    }
}
