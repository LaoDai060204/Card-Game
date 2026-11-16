using System;
using System.IO;
using System.Linq;
using MDPro3.BattleSpirits.Core;
using MDPro3.BattleSpirits.Puzzles;
using UnityEngine;

namespace MDPro3.BattleSpirits
{
    /// <summary>The application's game entry points. No previous duel protocol is used.</summary>
    public static class BattleIntegration
    {
        private static Program program;
        private static BattleDuelServant duel;
        private static string awardedPuzzle;
        public static BattleGameSession Current { get; private set; }
        public static bool IsActive => Current != null;
        public static string StorageRoot => Path.Combine(Application.persistentDataPath, "BattleSpirits");

        public static void Initialize(Program owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (program == owner && owner.GetComponent<BattleRuntimeDriver>() != null) return;
            program = owner;
            BattleCatalog.Initialize(Application.streamingAssetsPath, Application.persistentDataPath);
            Directory.CreateDirectory(StorageRoot);
            BattleDeckStorage.EnsureInitialized();
            if (owner.GetComponent<BattleRuntimeDriver>() == null) owner.gameObject.AddComponent<BattleRuntimeDriver>();
        }

        private static void EnsureReady()
        {
            if (program == null) Initialize(Program.instance);
        }

        private static void StartSession(Action<BattleGameSession> start)
        {
            EnsureReady();
            var next = new BattleGameSession(BattleCatalog.Cards, StorageRoot);
            try { start(next); }
            catch { next.Dispose(); throw; }
            // Validate and initialize the candidate before releasing a live match or
            // LAN listener. Failed replay/deck/connection input must not replace it.
            if (duel != null) duel.Bind(null);
            var previous = Current;
            Current = next;
            awardedPuzzle = null;
            previous?.Dispose();
            OpenDuel();
        }

        private static DeckDefinition PlayerDeck(string nameOrPath)
        {
            if (string.IsNullOrWhiteSpace(nameOrPath)) nameOrPath = BattleDeckStorage.DefaultDeckName;
            var deck = BattleDeckStorage.LoadDefinition(nameOrPath);
            var validation = DeckValidator.Validate(BattleCatalog.Cards, deck.cards);
            if (!validation.IsValid) throw new InvalidDataException(validation.Summary);
            return deck;
        }

        private static void OpenDuel()
        {
            if (duel == null)
            {
                var gameObject = new GameObject("Battle Spirits Duel");
                gameObject.transform.SetParent(program.transform, false);
                duel = gameObject.AddComponent<BattleDuelServant>();
                program.RegisterServant(duel);
            }
            if (program.currentServant != duel)
                duel.returnServant = program.currentServant ?? program.menu;
            duel.Bind(Current);
            program.ShiftToServant(duel);
        }

        public static bool StartSolo(string deckPathOrName, string opponentName = null)
        {
            return TryStart(() =>
            {
                EnsureReady();
                var player = PlayerDeck(deckPathOrName);
                var opponent = BattleCatalog.StarterDecks.FirstOrDefault(d => string.Equals(d.name, opponentName, StringComparison.OrdinalIgnoreCase))
                    ?? BattleCatalog.StarterDecks.FirstOrDefault()
                    ?? throw new InvalidDataException("The Battle Spirits opponent decks are missing.");
                StartSession(session => session.StartSolo(player, opponent, Environment.TickCount & int.MaxValue, Config.Get("DuelPlayerName0", "Player")));
            });
        }

        public static bool StartPuzzle(string puzzleId)
        {
            return TryStart(() =>
            {
                EnsureReady();
                puzzleId = Path.GetFileNameWithoutExtension(puzzleId ?? string.Empty);
                if (!BattlePuzzleCatalog.TryGet(puzzleId, out var definition)) throw new ArgumentException("Unknown Battle Spirits puzzle: " + puzzleId);
                StartSession(session => session.StartPuzzle(definition));
            });
        }

        public static bool Host(int port, string playerName)
        {
            return TryStart(() =>
            {
                EnsureReady();
                var deck = PlayerDeck(Config.GetConfigDeckName());
                StartSession(session => session.StartHost(port, deck, playerName));
            });
        }

        public static bool Join(string host, int port, string playerName)
        {
            return TryStart(() =>
            {
                EnsureReady();
                var deck = PlayerDeck(Config.GetConfigDeckName());
                StartSession(session => session.Join(host, port, deck, playerName));
            });
        }

        public static bool PlayReplay(string path)
        {
            return TryStart(() => StartSession(session => session.StartReplay(path)));
        }

        public static string[] GetReplayFiles()
        {
            EnsureReady();
            return BattleGameSession.ReplayFiles(StorageRoot);
        }

        public static bool PreviewCutscene(string cardId = "26RBS03-X01")
        {
            return TryStart(() =>
            {
                EnsureReady();
                if (!BattleCatalog.IsBoss(cardId)) throw new ArgumentException("No boss presentation is available for this card.");
                StartSession(_ => { });
                duel.PreviewBoss(cardId);
            });
        }

        public static void Concede()
        {
            Current?.Concede();
        }

        public static bool Retry() => Current != null && Current.Retry();
        public static bool ResetPuzzle() => Current != null && Current.ResetPuzzle();

        internal static void Tick(float unscaledTime)
        {
            Current?.Tick(unscaledTime);
            if (Current?.Puzzle != null && Current.Puzzle.Status == PuzzleStatus.Solved && awardedPuzzle != Current.Puzzle.Definition.id)
            {
                awardedPuzzle = Current.Puzzle.Definition.id;
                Config.Set("BattleSpirits/Puzzles/" + awardedPuzzle + "_Clear", Config.STRING_YES);
                Config.Save();
            }
        }

        public static void Shutdown()
        {
            if (duel != null) duel.Bind(null);
            Current?.Dispose();
            Current = null;
        }

        public static void ReadCommandLine(string[] args)
        {
            if (args == null) return;
            string Value(string key)
            {
                int index = Array.IndexOf(args, key);
                return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
            }
            if (Value("-r") is string replay) PlayReplay(replay);
            else if (Value("-s") is string puzzle) StartPuzzle(puzzle);
            else if (Value("-h") is string host && int.TryParse(Value("-p") ?? "7911", out int port)) Join(host, port, Value("-n") ?? "Player");
            else if (Value("-d") is string deck) StartSolo(deck);
        }

        private static bool TryStart(Action action)
        {
            try { action(); return true; }
            catch (Exception error)
            {
                UnityEngine.Debug.LogError("Battle Spirits: " + error.Message);
                MessageManager.Cast(error.Message);
                return false;
            }
        }
    }
}
