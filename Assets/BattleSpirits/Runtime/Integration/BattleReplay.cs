using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MDPro3.BattleSpirits.Core;
using Newtonsoft.Json;

namespace MDPro3.BattleSpirits
{
    [Serializable] internal sealed class BattleRecordedCommand
    {
        public int actor;
        public BattleCommand command;
    }
    [Serializable] internal sealed class BattleReplayDocument
    {
        [JsonProperty(Required = Required.Always)] public string protocol = BattleReplay.Protocol;
        [JsonProperty(Required = Required.Always)] public int version = BattleReplay.Version;
        [JsonProperty(Required = Required.Always)] public string catalogHash;
        public int seed, firstPlayer, winner = -1;
        public DeckDefinition[] decks;
        public string[] names;
        public List<BattleRecordedCommand> commands = new List<BattleRecordedCommand>();
    }

    /// <summary>Versioned command recording. Playback always re-simulates the production rules.</summary>
    internal static class BattleReplay
    {
        internal const string Protocol = "BattleSpirits";
        internal const int Version = 1, MaxCommands = 50000, MaxBytes = 16 * 1024 * 1024;
        internal const string Extension = ".bsreplay.json";
        internal static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None, MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Error, MaxDepth = 32,
            Culture = System.Globalization.CultureInfo.InvariantCulture
        };
        internal static string Encode(object value) => JsonConvert.SerializeObject(value, Formatting.None, Json);
        internal static T Decode<T>(string json) => JsonConvert.DeserializeObject<T>(json, Json) ?? throw new InvalidDataException("Empty Battle Spirits document.");
        internal static T Copy<T>(T value) => Decode<T>(Encode(value));
        internal static string CatalogHash(IReadOnlyList<CardDefinition> cards)
        {
            // Rules and data participate in the identity, independently of importer file order.
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Encode(cards.OrderBy(c => c.id, StringComparer.Ordinal).ToArray())))).Replace("-", "").ToLowerInvariant();
        }
        internal static DeckDefinition ValidateDeck(IReadOnlyList<CardDefinition> cards, DeckDefinition deck)
        {
            if (deck == null || deck.cards == null || deck.cards.Length > 500 || !string.Equals(deck.format, "Standard", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("A Standard deck containing 40–500 cards is required.");
            var result = DeckValidator.Validate(cards, deck.cards);
            if (!result.IsValid) throw new ArgumentException(result.Summary);
            return Copy(deck);
        }
        internal static string Name(string value, string fallback)
        {
            value = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            if (value.Length > 40 || value.Any(char.IsControl)) throw new ArgumentException("Player names must contain 1–40 printable characters.");
            return value;
        }
        internal static BattleReplayDocument Create(IReadOnlyList<CardDefinition> cards, string hash, DeckDefinition first, DeckDefinition second, int seed, string firstName, string secondName)
            => new BattleReplayDocument { catalogHash = hash, seed = seed, firstPlayer = 0,
                decks = new[] { ValidateDeck(cards, first), ValidateDeck(cards, second) },
                names = new[] { Name(firstName, "Player"), Name(secondName, "Opponent") } };
        internal static BattleMatch Simulate(IReadOnlyList<CardDefinition> cards, BattleReplayDocument replay, int count)
        {
            var match = BattleMatch.Create(cards, replay.decks[0].cards, replay.decks[1].cards, replay.seed, replay.firstPlayer);
            match.State.Players[0].Name = replay.names[0]; match.State.Players[1].Name = replay.names[1];
            for (int i = 0; i < count; i++)
            {
                var step = replay.commands[i];
                ValidateCommand(step?.command);
                if (step == null || !match.TryExecute(step.actor, step.command, out var error))
                    throw new InvalidDataException("Invalid replay command " + i + ".");
            }
            return match;
        }
        internal static void ValidateCommand(BattleCommand command)
        {
            if (command == null || string.IsNullOrEmpty(command.action) || command.action.Length > 32 ||
                (command.selection?.Length ?? 0) > 512 || (command.targets?.Length ?? 0) > 512 ||
                (command.transfers?.Length ?? 0) > 512 || (command.payment?.Length ?? 0) > 512 ||
                (command.succession?.Length ?? 0) > 500 || (command.successionIndices?.Length ?? 0) > 500 ||
                (command.succession != null && command.succession.Any(s => s == null || s.Length > 80)))
                throw new InvalidDataException("Invalid Battle Spirits command bounds.");
            if (command.payment != null && command.payment.Any(t => t == null) || command.transfers != null && command.transfers.Any(t => t == null))
                throw new InvalidDataException("Invalid core payment.");
        }
        private static string DirectoryPath(string storageRoot) => Path.Combine(Path.GetFullPath(storageRoot), "Replays");
        internal static string ValidPath(string storageRoot, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Select a Battle Spirits replay.");
            string directory = DirectoryPath(storageRoot), full = Path.GetFullPath(path);
            if (!string.Equals(Path.GetDirectoryName(full), directory, StringComparison.Ordinal) || !full.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Replays must be .bsreplay.json files inside the replay directory.");
            // Resolve neither directory links nor file links outside the configured storage root.
            if (Directory.Exists(directory) && (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0 ||
                File.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Replay links are not supported.");
            return full;
        }
        internal static string[] Files(string storageRoot)
        {
            string directory = DirectoryPath(storageRoot);
            if (!Directory.Exists(directory)) return Array.Empty<string>();
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) return Array.Empty<string>();
            return Directory.GetFiles(directory, "*" + Extension).Where(p => (File.GetAttributes(p) & FileAttributes.ReparsePoint) == 0).OrderByDescending(p => p, StringComparer.Ordinal).ToArray();
        }
        internal static string Save(string storageRoot, BattleReplayDocument replay)
        {
            if (replay == null || replay.winner < 0 || replay.winner > 1) throw new InvalidOperationException("Only completed matches can be recorded.");
            string directory = DirectoryPath(storageRoot);
            string path = ValidPath(storageRoot, Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + Extension));
            string json = Encode(replay);
            if (Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new InvalidDataException("Replay exceeds the storage limit.");
            Directory.CreateDirectory(directory);
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(file, new UTF8Encoding(false))) writer.Write(json);
            return path;
        }
        internal static BattleReplayDocument Load(string storageRoot, string path, IReadOnlyList<CardDefinition> cards, string hash)
        {
            path = ValidPath(storageRoot, path);
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= 0 || info.Length > MaxBytes) throw new InvalidDataException("Invalid replay size.");
            var replay = Decode<BattleReplayDocument>(File.ReadAllText(path));
            if (replay.protocol != Protocol || replay.version != Version || replay.catalogHash != hash || replay.firstPlayer < 0 || replay.firstPlayer > 1 ||
                replay.decks == null || replay.decks.Length != 2 || replay.names == null || replay.names.Length != 2 ||
                replay.commands == null || replay.commands.Count == 0 || replay.commands.Count > MaxCommands || replay.winner < 0 || replay.winner > 1)
                throw new InvalidDataException("Unsupported or incompatible Battle Spirits replay.");
            for (int i = 0; i < 2; i++) { ValidateDeck(cards, replay.decks[i]); Name(replay.names[i], "Player"); }
            var completed = Simulate(cards, replay, replay.commands.Count);
            if (completed.State.WinnerIndex != replay.winner) throw new InvalidDataException("Replay does not reproduce its completed result.");
            return replay;
        }
    }
}
