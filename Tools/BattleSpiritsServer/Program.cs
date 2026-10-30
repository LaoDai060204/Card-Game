using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LaoDai.BattleSpirits.Core;
using LaoDai.BattleSpirits.Networking;
using Newtonsoft.Json;

internal static class Entry
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        string catalogPath = "Data/BattleSpirits/cards.json";
        int port = Protocol.DefaultPort;
        for (int index = 0; index < args.Length; index++)
        {
            string option = args[index];
            if (option == "--help" || option == "-h")
            {
                Console.WriteLine("Battle Spirits Japanese Eternal server");
                Console.WriteLine("Usage: dotnet run --project Tools/BattleSpiritsServer -c Release -- [--catalog PATH] [--port PORT]");
                Console.WriteLine("Defaults: --catalog Data/BattleSpirits/cards.json --port 7777");
                Console.WriteLine("Run from the repository root, or specify an absolute catalog path.");
                Console.WriteLine("The server listens on all IPv4 interfaces. Port 0 selects a free port.");
                Console.WriteLine("Press Ctrl+C to stop. Both game clients need the same catalog and protocol version.");
                return 0;
            }
            if (option == "--catalog" && index + 1 < args.Length)
                catalogPath = args[++index];
            else if (option == "--port" && index + 1 < args.Length
                && int.TryParse(args[++index], out int selectedPort) && selectedPort >= 0 && selectedPort <= 65535)
                port = selectedPort;
            else
            {
                Console.Error.WriteLine("Invalid option or value: " + option + ". Use --help for usage.");
                return 2;
            }
        }

        try
        {
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 64 };
            var catalog = JsonConvert.DeserializeObject<CardsDatabase>(File.ReadAllText(catalogPath), settings)?.cards;
            if (catalog == null || catalog.Length == 0)
                throw new InvalidDataException("The card catalog is empty.");
            // Fail before accepting clients if the catalog cannot provide a legal
            // supported deck. Imported ability cards remain browse-only.
            StarterDecks.CreatePair(catalog);
            var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (var server = new BattleLanServer(catalog))
            {
                ConsoleCancelEventHandler cancel = (_, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    stopped.TrySetResult(true);
                };
                EventHandler exiting = (_, __) => server.Stop();
                Console.CancelKeyPress += cancel;
                AppDomain.CurrentDomain.ProcessExit += exiting;
                server.Diagnostic += text => Console.WriteLine(text);
                try
                {
                    server.Start(port);
                    Console.WriteLine("Ruleset: Japanese Battle Spirits Eternal");
                    Console.WriteLine("Catalog: " + Path.GetFullPath(catalogPath));
                    Console.WriteLine("Cards: " + catalog.Length + "; playable: " + catalog.Count(card => card != null && card.IsPlayable));
                    Console.WriteLine("Catalog hash: " + server.CatalogHash);
                    Console.WriteLine("Ready on TCP port " + server.Port + ". Press Ctrl+C to stop.");
                    await stopped.Task.ConfigureAwait(false);
                    server.Stop();
                    Console.WriteLine("Server stopped.");
                    return 0;
                }
                finally
                {
                    Console.CancelKeyPress -= cancel;
                    AppDomain.CurrentDomain.ProcessExit -= exiting;
                }
            }
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException
            || error is JsonException || error is ArgumentException || error is InvalidOperationException
            || error is System.Net.Sockets.SocketException)
        {
            Console.Error.WriteLine("Unable to start Battle Spirits server: " + error.Message);
            return 1;
        }
    }
}
