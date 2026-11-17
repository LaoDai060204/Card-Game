using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WindBot;
using YGOSharp.OCGWrapper;

internal static class WindBotConcurrencyTests
{
    private static int passed;
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Case(string name, Action test)
    {
        ConcurrencyFixtures.BeforeDeckInit = ConcurrencyFixtures.BeforeClientStart = null;
        ConcurrencyFixtures.Started.Clear(); ConcurrencyFixtures.Randoms.Clear();
        test(); passed++; Console.WriteLine("PASS " + name);
    }
    private static Task OnWorker(string name, Action action) => Task.Factory.StartNew(() =>
    {
        Thread.CurrentThread.Name = name; action();
    }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    private static string[] Options(string database, string id, int port)
        => new[] { "DbPath=" + database, "Name=Bot " + id, "Deck=Deck=" + id,
            "DeckFile=decks/" + id + " deck.ydk", "Dialog=" + id, "Host=" + id + ".invalid",
            "Port=" + port, "HostInfo=password=" + id, "Version=0x1362", "Hand=7", "Debug=true", "Chat=false" };
    private static void CheckOptions(string worker, string id, int port)
    {
        Require(ConcurrencyFixtures.Started.TryGetValue(worker, out var info), "Bot never started: " + worker);
        Require(info.Name == "Bot " + id && info.Deck == "Deck=" + id && info.DeckFile == "decks/" + id + " deck.ydk"
            && info.Dialog == id && info.Host == id + ".invalid" && info.Port == port && info.HostInfo == "password=" + id
            && info.Version == 0x1362 && info.Hand == 7 && info.Debug && !info.Chat, "Startup options crossed bot workers: " + worker);
    }

    private static void CheckPublication(bool single)
    {
        string original = ConcurrencyFixtures.Register("original.cdb", () => ConcurrencyFixtures.Rows("original", 2));
        NamedCardsManager.Init(original);
        NamedCard retained = NamedCardsManager.GetCard(1);
        using var paused = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        string replacement = ConcurrencyFixtures.Register("replacement.cdb", () => ConcurrencyFixtures.Rows("replacement", 3, read =>
        {
            if (read == 2) { paused.Set(); Require(release.Wait(3000), "Catalog fixture not released"); }
        }));
        var loading = Task.Run(() =>
        {
            if (single) NamedCardsManager.Init(replacement);
            else NamedCardsManager.InitForMulti(new List<string> { replacement });
        });
        try
        {
            Require(paused.Wait(3000), "Loader did not reach a partially built catalog");
            for (int repeat = 0; repeat < 100; repeat++)
            {
                Require(ReferenceEquals(retained, NamedCardsManager.GetCard(1)), "Reader observed an in-progress replacement");
                var cards = NamedCardsManager.GetAllCards();
                Require(cards.Count == 2 && cards.All(card => card.Name.StartsWith("original-")), "Partial/empty card catalog escaped");
            }
        }
        finally { release.Set(); }
        loading.GetAwaiter().GetResult();
        Require(NamedCardsManager.GetAllCards().Count == 3 && NamedCardsManager.GetCard(1).Name == "replacement-1", "Completed replacement not published");
        Require(retained.Name == "original-1", "Published card object mutated");
    }

    private static void Main()
    {
        Case("An uninitialized card lookup returns an empty snapshot safely", () =>
        {
            Require(NamedCardsManager.GetCard(1) == null && NamedCardsManager.GetAllCards().Count == 0, "Unsafe initial catalog");
        });
        Case("Two actual Main calls retain every bot option while database startup overlaps", () =>
        {
            string dbA = ConcurrencyFixtures.Register("alpha.cdb", () => ConcurrencyFixtures.Rows("alpha", 2));
            string dbB = ConcurrencyFixtures.Register("beta.cdb", () => ConcurrencyFixtures.Rows("beta", 3));
            using var paused = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
            ConcurrencyFixtures.BeforeDeckInit = () =>
            {
                if (Thread.CurrentThread.Name == "bot-a") { paused.Set(); Require(release.Wait(3000), "Startup fixture not released"); }
            };
            var first = OnWorker("bot-a", () => WindBot.Program.Main(Options(dbA, "alpha", 7111)));
            try
            {
                Require(paused.Wait(3000), "First bot did not pause during initialization");
                var second = OnWorker("bot-b", () => WindBot.Program.Main(Options(dbB, "beta", 7222)));
                Require(second.Wait(2000), "Global option lock blocked expensive startup"); second.GetAwaiter().GetResult();
                CheckOptions("bot-b", "beta", 7222);
            }
            finally { release.Set(); }
            first.GetAwaiter().GetResult(); CheckOptions("bot-a", "alpha", 7111);
            Require(ConcurrencyFixtures.Opened["bot-a"] == dbA && ConcurrencyFixtures.Opened["bot-b"] == dbB, "Database options crossed workers");
            Require(!ReferenceEquals(ConcurrencyFixtures.Randoms["bot-a"], ConcurrencyFixtures.Randoms["bot-b"]), "Room bots share a mutable Random");
        });
        Case("A blocked running bot does not hold the startup option lock", () =>
        {
            string db = ConcurrencyFixtures.Register("blocking.cdb", () => ConcurrencyFixtures.Rows("base", 2));
            using var paused = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
            ConcurrencyFixtures.BeforeClientStart = () =>
            {
                if (Thread.CurrentThread.Name == "running-a") { paused.Set(); Require(release.Wait(3000), "Running fixture not released"); }
            };
            var first = OnWorker("running-a", () => WindBot.Program.Main(Options(db, "first", 7333)));
            try
            {
                Require(paused.Wait(3000), "First bot did not reach its client loop");
                var second = OnWorker("running-b", () => WindBot.Program.Main(Options(db, "second", 7444)));
                Require(second.Wait(2000), "Active bot prevented another bot starting"); second.GetAwaiter().GetResult();
                CheckOptions("running-b", "second", 7444);
            }
            finally { release.Set(); }
            first.GetAwaiter().GetResult(); CheckOptions("running-a", "first", 7333);
        });
        Case("Original config-file defaults and command-line precedence are preserved", () =>
        {
            string db = ConcurrencyFixtures.Register("configured.cdb", () => ConcurrencyFixtures.Rows("configured", 1));
            File.WriteAllText("bot.conf", "# fixture\nName=File name\nDeck=FileDeck\nPort=0x1F40\nChat=false\nDbPath=" + db);
            OnWorker("file-bot", () => WindBot.Program.Main(new[] { "Config=bot.conf", "Name=CLI name" })).GetAwaiter().GetResult();
            var info = ConcurrencyFixtures.Started["file-bot"];
            Require(info.Name == "CLI name" && info.Deck == "FileDeck" && info.Port == 8000 && !info.Chat
                && info.Dialog == "default" && info.Host == "127.0.0.1", "Config parsing/precedence changed");
        });
        Case("Single-database initialization publishes only a complete replacement", () => CheckPublication(true));
        Case("Multi-database initialization publishes only a complete replacement", () => CheckPublication(false));
        Case("Expansion precedence and missing-file skipping retain existing behavior", () =>
        {
            string original = ConcurrencyFixtures.Register("base.cdb", () => ConcurrencyFixtures.Rows("base", 2));
            string expansion = ConcurrencyFixtures.Register("expansion.cdb", () => ConcurrencyFixtures.Rows("expansion", 3));
            NamedCardsManager.InitForMulti(new List<string> { "does-not-exist.cdb", original, expansion });
            Require(NamedCardsManager.GetAllCards().Count == 3 && NamedCardsManager.GetCard(1).Name == "expansion-1", "Expansion no longer overrides base cards");
        });
        Case("Failed single/multi reloads preserve the last complete catalog", () =>
        {
            string good = ConcurrencyFixtures.Register("good.cdb", () => ConcurrencyFixtures.Rows("good", 2));
            string broken = ConcurrencyFixtures.Register("broken.cdb", () => ConcurrencyFixtures.Rows("broken", 3, read =>
            { if (read == 2) throw new IOException("Controlled database read failure"); }));
            NamedCardsManager.Init(good); var retained = NamedCardsManager.GetCard(1);
            foreach (Action reload in new Action[] { () => NamedCardsManager.Init(broken),
                () => NamedCardsManager.InitForMulti(new List<string> { good, broken }) })
            {
                bool failed = false; try { reload(); } catch (Exception error) { failed = error.InnerException is IOException; }
                Require(failed && ReferenceEquals(retained, NamedCardsManager.GetCard(1)) && NamedCardsManager.GetAllCards().Count == 2, "Failed reload damaged published cards");
            }
        });
        Case("Concurrent readers see whole catalogs during repeated overlapping reloads", () =>
        {
            string a = ConcurrencyFixtures.Register("stress-a.cdb", () => ConcurrencyFixtures.Rows("A", 2));
            string b = ConcurrencyFixtures.Register("stress-b.cdb", () => ConcurrencyFixtures.Rows("B", 3));
            NamedCardsManager.Init(a);
            var work = new List<Task>();
            for (int reader = 0; reader < 4; reader++)
                work.Add(Task.Run(() =>
                {
                    for (int repeat = 0; repeat < 1000; repeat++)
                    {
                        var cards = NamedCardsManager.GetAllCards();
                        Require((cards.Count == 2 || cards.Count == 3) && cards.Select(card => card.Name.Substring(0, 1)).Distinct().Count() == 1, "Mixed or partial read snapshot");
                        Require(NamedCardsManager.GetCard(1) != null, "Concurrent lookup lost a card");
                    }
                }));
            for (int writer = 0; writer < 2; writer++)
                work.Add(Task.Run(() => { for (int repeat = 0; repeat < 50; repeat++) NamedCardsManager.InitForMulti(new List<string> { repeat % 2 == 0 ? a : b }); }));
            Task.WaitAll(work.ToArray());
        });
        Case("Each bot/server worker lazily owns a stable independent Random", () =>
        {
            var randoms = new ConcurrentBag<Random>();
            var tasks = Enumerable.Range(0, 8).Select(worker => OnWorker("random-" + worker, () =>
            {
                var random = WindBot.Program.Rand;
                Require(ReferenceEquals(random, WindBot.Program.Rand), "Random changed within its worker");
                for (int repeat = 0; repeat < 1000; repeat++) Require(random.Next(100) >= 0, "Invalid random result");
                randoms.Add(random);
            })).ToArray();
            Task.WaitAll(tasks); Require(randoms.Distinct().Count() == 8, "Workers share Random state");
        });
        Console.WriteLine("PASS: " + passed + " production WindBot startup/config/catalog concurrency scenarios; controlled SQLite reader and client transport; no Unity Play test.");
    }
}
