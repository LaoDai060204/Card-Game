using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LaoDai.BattleSpirits.Networking
{
    /// <summary>TCP transport only. Call Pump from Unity Update to process messages on its main thread.</summary>
    public sealed class BattleLanClient : IDisposable
    {
        private sealed class Connection
        {
            public readonly TcpClient Client = new TcpClient();
            public readonly CancellationTokenSource Stop = new CancellationTokenSource();
            public readonly SemaphoreSlim SendGate = new SemaphoreSlim(1, 1);
            public int Generation;

            public void Close()
            {
                try { Stop.Cancel(); } catch (ObjectDisposedException) { }
                try { Client.Close(); } catch (ObjectDisposedException) { }
            }
        }

        private sealed class Incoming
        {
            public int Generation;
            public string Line;
            public WireMessage Local;
        }

        private readonly object gate = new object();
        private readonly ConcurrentQueue<Incoming> incoming = new ConcurrentQueue<Incoming>();
        private Connection current;
        private int generation;
        private int queued;

        public event Action<WireMessage> MessageReceived;

        public bool IsConnected
        {
            get { lock (gate) return current != null && current.Client.Connected; }
        }

        public async Task ConnectAsync(string host, int port, string name, string catalogHash)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("A server address is required.", nameof(host));
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            Disconnect();
            Connection connection;
            lock (gate)
            {
                connection = new Connection { Generation = ++generation };
                current = connection;
            }
            try
            {
                connection.Client.NoDelay = true;
                var connect = connection.Client.ConnectAsync(host, port);
                var completed = await Task.WhenAny(connect, Task.Delay(5000, connection.Stop.Token)).ConfigureAwait(false);
                if (completed != connect)
                {
                    bool cancelled = connection.Stop.IsCancellationRequested;
                    connection.Close();
                    // Observe the eventual socket failure without blocking the caller.
                    _ = ObserveFailureAsync(connect);
                    if (cancelled) throw new OperationCanceledException();
                    throw new TimeoutException("The server did not respond within five seconds.");
                }
                await connect.ConfigureAwait(false);
                connection.Stop.Token.ThrowIfCancellationRequested();
                await SendOnConnectionAsync(connection, new WireMessage
                {
                    type = "hello", version = Protocol.Version, name = name,
                    catalogHash = catalogHash
                }).ConfigureAwait(false);
                _ = ReceiveAsync(connection);
            }
            catch
            {
                CloseCurrent(connection, "Không thể kết nối tới máy chủ.");
                throw;
            }
        }

        public Task SendAsync(WireMessage message)
        {
            Connection connection;
            lock (gate) connection = current;
            if (connection == null) throw new InvalidOperationException("Not connected to a Battle Spirits server.");
            return SendOnConnectionAsync(connection, message);
        }

        private async Task SendOnConnectionAsync(Connection connection, WireMessage message)
        {
            string encoded = Protocol.Encode(message);
            await connection.SendGate.WaitAsync(connection.Stop.Token).ConfigureAwait(false);
            try
            {
                lock (gate)
                    if (!ReferenceEquals(current, connection)) throw new OperationCanceledException();
                await WireWriter.WriteAsync(connection.Client.GetStream(), encoded, connection.Stop.Token).ConfigureAwait(false);
            }
            catch
            {
                CloseCurrent(connection, "Kết nối đã đóng khi gửi dữ liệu.");
                throw;
            }
            finally { connection.SendGate.Release(); }
        }

        private async Task ReceiveAsync(Connection connection)
        {
            try
            {
                var reader = new WireReader(connection.Client.GetStream());
                while (!connection.Stop.IsCancellationRequested)
                {
                    string line = await reader.ReadLineAsync(connection.Stop.Token).ConfigureAwait(false);
                    if (line == null) break;
                    if (Interlocked.Increment(ref queued) > 256)
                    {
                        Interlocked.Decrement(ref queued);
                        throw new InvalidOperationException("Too many unprocessed server messages.");
                    }
                    incoming.Enqueue(new Incoming { Generation = connection.Generation, Line = line });
                }
                CloseCurrent(connection, "Máy chủ đã đóng kết nối.");
            }
            catch (Exception)
            {
                CloseCurrent(connection, "Mất kết nối tới máy chủ.");
            }
        }

        public void Pump(int maxMessages = 128)
        {
            for (int count = 0; count < maxMessages && incoming.TryDequeue(out var item); count++)
            {
                Interlocked.Decrement(ref queued);
                lock (gate)
                    if (item.Generation != generation) continue;
                WireMessage message;
                try { message = item.Local ?? Protocol.Decode(item.Line); }
                catch (Exception)
                {
                    Disconnect();
                    MessageReceived?.Invoke(new WireMessage { type = "error", text = "Máy chủ gửi dữ liệu không hợp lệ." });
                    continue;
                }
                MessageReceived?.Invoke(message);
            }
        }

        public void Disconnect()
        {
            Connection previous;
            int next;
            lock (gate)
            {
                previous = current;
                current = null;
                next = ++generation;
            }
            if (previous == null) return;
            previous.Close();
            Interlocked.Increment(ref queued);
            incoming.Enqueue(new Incoming
            {
                Generation = next,
                Local = new WireMessage { type = "disconnected", text = "Đã ngắt kết nối." }
            });
        }

        private void CloseCurrent(Connection connection, string reason)
        {
            lock (gate)
            {
                if (!ReferenceEquals(current, connection)) return;
                current = null;
            }
            connection.Close();
            Interlocked.Increment(ref queued);
            incoming.Enqueue(new Incoming
            {
                Generation = connection.Generation,
                Local = new WireMessage { type = "disconnected", text = reason }
            });
        }

        private static async Task ObserveFailureAsync(Task task)
        {
            try { await task.ConfigureAwait(false); } catch (Exception) { }
        }

        public void Dispose() { Disconnect(); }
    }
}
