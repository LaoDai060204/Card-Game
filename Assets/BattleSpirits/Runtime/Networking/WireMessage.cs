using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LaoDai.BattleSpirits.Core;
using Newtonsoft.Json;

namespace LaoDai.BattleSpirits.Networking
{
    /// <summary>Versioned JSON-line protocol. Never serialize MatchState onto the wire.</summary>
    [Serializable]
    public sealed class WireMessage
    {
        public string type = string.Empty;
        public int version = Protocol.Version;
        public string catalogHash = string.Empty;
        public string name = string.Empty;
        public string text = string.Empty;
        public int playerIndex = -1;
        public string[] deck = Array.Empty<string>();
        public bool ready;
        public string action = string.Empty;
        public int index;
        public int source = -1;
        public int target = -1;
        public bool soul;
        public bool redraw;
        public LobbyPlayerView[] players = Array.Empty<LobbyPlayerView>();
        public FilteredMatchView view;
    }

    public static class Protocol
    {
        public const int Version = 1;
        public const int DefaultPort = 7777;
        public const int MaxLineBytes = 65536;
        public const int MaxNameLength = 24;
        public const int MaxChatLength = 300;

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            NullValueHandling = NullValueHandling.Ignore,
            MaxDepth = 16
        };

        public static string Encode(WireMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            return JsonConvert.SerializeObject(message, Formatting.None, Settings);
        }

        public static WireMessage Decode(string line)
        {
            if (line == null) throw new ArgumentNullException(nameof(line));
            var message = JsonConvert.DeserializeObject<WireMessage>(line, Settings);
            if (message == null || string.IsNullOrEmpty(message.type))
                throw new JsonSerializationException("A message type is required.");
            return message;
        }
    }

    public static class CatalogFingerprint
    {
        public static string Compute(IReadOnlyList<CardDefinition> catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var ordered = catalog.OrderBy(card => card.id, StringComparer.Ordinal).ToArray();
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(ordered, Formatting.None));
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    [Serializable]
    public sealed class LobbyPlayerView
    {
        public int playerIndex;
        public string name = string.Empty;
        public bool connected;
        public bool ready;
        public int deckCount;
    }

    [Serializable]
    public sealed class FilteredMatchView
    {
        public int viewer;
        public int activePlayer;
        public int decisionPlayer;
        public int turn;
        public string phase = string.Empty;
        public int winner = -1;
        public int attacker = -1;
        public int blocker = -1;
        public int flashPlayer = -1;
        public string[] ownHand = Array.Empty<string>();
        public PublicPlayerView[] players = Array.Empty<PublicPlayerView>();
        public PublicBattleEvent[] events = Array.Empty<PublicBattleEvent>();
    }

    [Serializable]
    public sealed class PublicPlayerView
    {
        public int playerIndex;
        public string name = string.Empty;
        public int handCount;
        public int deckCount;
        public int life;
        public int reserveNormal;
        public bool reserveSoul;
        public int trashNormal;
        public bool trashSoul;
        public SpiritView[] field = Array.Empty<SpiritView>();
        public string[] discard = Array.Empty<string>();
    }

    [Serializable]
    public sealed class SpiritView
    {
        public int instanceId;
        public string cardId = string.Empty;
        public int normalCores;
        public bool soulCore;
        public bool exhausted;
        public int level;
        public int bp;
    }

    [Serializable]
    public sealed class PublicBattleEvent
    {
        public string kind = string.Empty;
        public int actor;
        public int spiritId = -1;
        public int targetId = -1;
        public string cardId = string.Empty;
        public int amount;
    }

    public static class ViewFactory
    {
        private static readonly HashSet<string> PublicEvents = new HashSet<string>(StringComparer.Ordinal)
        {
            "Summoned", "CoreMoved", "Attacked", "Blocked", "LifeLost", "TurnStarted",
            "Destroyed", "Vanished", "GameWon"
        };

        public static FilteredMatchView Create(BattleMatch match, int viewer)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            if (viewer < 0 || viewer > 1) throw new ArgumentOutOfRangeException(nameof(viewer));
            var state = match.State;
            return new FilteredMatchView
            {
                viewer = viewer,
                activePlayer = state.ActivePlayerIndex,
                decisionPlayer = state.Phase == MatchPhase.GameOver ? -1 : match.DecisionPlayerIndex,
                turn = state.TurnNumber,
                phase = state.Phase.ToString(),
                winner = state.WinnerIndex,
                attacker = state.PendingAttackerId,
                blocker = state.PendingBlockerId,
                flashPlayer = state.FlashPlayerIndex,
                ownHand = state.Players[viewer].Hand.ToArray(),
                players = state.Players.Select(player => new PublicPlayerView
                {
                    playerIndex = player.Index,
                    name = player.Name,
                    handCount = player.Hand.Count,
                    deckCount = player.Deck.Count,
                    life = player.Life,
                    reserveNormal = player.Reserve.Normal,
                    reserveSoul = player.Reserve.Soul,
                    trashNormal = player.Trash.Normal,
                    trashSoul = player.Trash.Soul,
                    discard = player.Discard.ToArray(),
                    field = player.Field.Select(spirit => new SpiritView
                    {
                        instanceId = spirit.InstanceId,
                        cardId = spirit.Card.id,
                        normalCores = spirit.Cores.Normal,
                        soulCore = spirit.Cores.Soul,
                        exhausted = spirit.Exhausted,
                        level = spirit.Level,
                        bp = spirit.Bp
                    }).ToArray()
                }).ToArray(),
                // Future private events are excluded until explicitly reviewed.
                events = match.LastEvents.Where(item => PublicEvents.Contains(item.Kind))
                    .Select(item => new PublicBattleEvent
                    {
                        kind = item.Kind, actor = item.Actor, spiritId = item.SpiritId,
                        targetId = item.TargetId, cardId = item.CardId, amount = item.Amount
                    }).ToArray()
            };
        }
    }
}
