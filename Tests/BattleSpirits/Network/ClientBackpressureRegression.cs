using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LaoDai.BattleSpirits.Core;
using LaoDai.BattleSpirits.Networking;

// Uses real sockets and the same client instance across two server connections.
// Pump is deliberately paused while the first server fills the receive queue.
internal static class ClientBackpressureRegression
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static async Task Wait(Func<bool> condition)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.ElapsedMilliseconds < 6000)
        {
            if (condition()) return;
            await Task.Delay(2);
        }
        throw new TimeoutException("Timed out in the client backpressure/reconnect regression.");
    }

    public static async Task Run(IReadOnlyList<CardDefinition> cards)
    {
        string hash = CatalogFingerprint.Compute(cards);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using (var stopFake = new CancellationTokenSource())
        using (var client = new BattleLanClient())
        {
            Task fake = SendOldGenerationMessages(listener, stopFake.Token);
            bool pumping = false;
            int callbacks = 0;
            int welcomes = 0;
            int staleMessages = 0;
            client.MessageReceived += message =>
            {
                Require(pumping, "A callback ran outside the caller's Pump.");
                callbacks++;
                if (message.type == "welcome") welcomes++;
                if (message.type == "chat" || message.type == "disconnected") staleMessages++;
            };
            try
            {
                await client.ConnectAsync("127.0.0.1", port, "Queued client", hash);
                await Wait(() => !client.IsConnected);
                Require(callbacks == 0, "Paused Pump delivered old server messages.");

                using (var room = new BattleLanServer(cards))
                {
                    room.Start(0);
                    await client.ConnectAsync("127.0.0.1", room.Port, "New room", hash);
                    // Give the fresh welcome/lobby messages time to arrive before
                    // old queued messages can be drained by the first new Pump.
                    await Task.Delay(250);
                    Require(client.IsConnected, "The old queue's backpressure closed the new connection.");
                    await Wait(() =>
                    {
                        pumping = true;
                        try { client.Pump(); }
                        finally { pumping = false; }
                        return welcomes == 1;
                    });
                    Require(staleMessages == 0, "An earlier connection leaked callbacks into the new room.");
                    Require(client.IsConnected, "The recovered client did not remain connected.");
                }
            }
            finally
            {
                stopFake.Cancel();
                listener.Stop();
                await fake;
            }
        }
    }

    private static async Task SendOldGenerationMessages(TcpListener listener, CancellationToken stop)
    {
        try
        {
            using (var peer = await listener.AcceptTcpClientAsync())
            using (var reader = new StreamReader(peer.GetStream(), Encoding.UTF8, false, 4096, true))
            using (var writer = new StreamWriter(peer.GetStream(), new UTF8Encoding(false), 4096, true))
            {
                await reader.ReadLineAsync();
                string line = Protocol.Encode(new WireMessage { type = "chat", text = "old generation" }) + "\n";
                await writer.WriteAsync(string.Concat(Enumerable.Repeat(line, 300)));
                await writer.FlushAsync();
                await Task.Delay(10000, stop);
            }
        }
        catch (Exception exception) when (exception is IOException || exception is SocketException
            || exception is OperationCanceledException || exception is ObjectDisposedException)
        {
            // Closing the first connection at its queue limit is expected.
        }
    }
}
