using System;
using System.Collections.Generic;
using System.Linq;

namespace MDPro3.BattleSpirits.Core
{
    /// <summary>Deterministic Standard rules, independent from Unity and presentation.</summary>
    public sealed partial class BattleMatch
    {
        private readonly Dictionary<string, CardDefinition> cards;
        private readonly Random random;
        private readonly int firstPlayer;
        private readonly int[] originalDeckSizes = new int[2];
        private int openingChoices, nextSpiritId = 1, nextChoiceId = 1, consecutiveFlashPasses;
        private readonly LinkedList<Action> effects = new LinkedList<Action>();
        private List<Action> nestedEffects;
        private bool processingEffects;
        private Action<int[]> choiceResolved;
        public MatchState State { get; }
        public BattleRulesFormat RulesFormat { get; }
        public List<BattleEvent> LastEvents { get; } = new List<BattleEvent>();
        public List<string> Log { get; } = new List<string>();
        public event Action<BattleEvent> EventEmitted;
        public int DecisionPlayerIndex => State.Phase == MatchPhase.GameOver ? -1 : State.PendingChoice != null ? State.PendingChoice.Owner
            : State.Phase == MatchPhase.Opening ? (openingChoices == 0 ? firstPlayer : 1 - firstPlayer)
            : IsFlashPhase(State.Phase) ? State.FlashPlayerIndex : State.Phase == MatchPhase.Block ? 1 - State.ActivePlayerIndex : State.ActivePlayerIndex;

        private BattleMatch(IReadOnlyList<CardDefinition> definitions, IReadOnlyList<string> deck0, IReadOnlyList<string> deck1,
            int seed, int firstPlayerIndex, bool validateDecks, BattleRulesFormat format)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (firstPlayerIndex < 0 || firstPlayerIndex > 1) throw new ArgumentOutOfRangeException(nameof(firstPlayerIndex));
            if (format != BattleRulesFormat.Standard && format != BattleRulesFormat.Eternal) throw new ArgumentOutOfRangeException(nameof(format));
            RulesFormat = format; firstPlayer = firstPlayerIndex; random = new Random(seed);
            cards = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);
            foreach (var card in definitions)
            {
                if (card == null || string.IsNullOrWhiteSpace(card.id) || string.IsNullOrWhiteSpace(card.DisplayName)) throw new ArgumentException("Card ID and name are required.");
                if (cards.ContainsKey(card.id)) throw new ArgumentException("Duplicate card ID: " + card.id);
                cards.Add(card.id, card);
            }
            State = new MatchState { Format = format.ToString(), ActivePlayerIndex = firstPlayer, Players = new[] {
                new PlayerState { Index = 0, Name = "Player 1" }, new PlayerState { Index = 1, Name = "Player 2" } } };
            var decks = new[] { deck0, deck1 };
            for (int owner = 0; owner < 2; owner++)
            {
                ValidateDeck(decks[owner], validateDecks);
                State.Players[owner].Deck.AddRange(decks[owner]); originalDeckSizes[owner] = decks[owner].Count;
                Shuffle(State.Players[owner].Deck); Draw(owner, 4);
            }
            AddLog("Battle Spirits " + format + ". Each player may redraw their opening four cards once.");
            AssertInvariants();
        }
        public static BattleMatch CreateDefault(IReadOnlyList<CardDefinition> cards, int seed = 2026, int firstPlayer = 0, BattleRulesFormat format = BattleRulesFormat.Standard)
        { var decks = StarterDecks.CreatePair(cards, format); return Create(cards, decks[0].cards, decks[1].cards, seed, firstPlayer, true, format); }
        public static BattleMatch Create(IReadOnlyList<CardDefinition> cards, IReadOnlyList<string> deck0, IReadOnlyList<string> deck1,
            int seed = 2026, int firstPlayer = 0, bool validateDecks = true, BattleRulesFormat format = BattleRulesFormat.Standard)
            => new BattleMatch(cards, deck0, deck1, seed, firstPlayer, validateDecks, format);
        public CardDefinition GetCard(string id) => id != null && cards.TryGetValue(id, out var card) ? card : throw new ArgumentException("Unknown card: " + id);
        public SpiritState FindSpirit(int id) => State.Players.SelectMany(p => p.Field).FirstOrDefault(s => s.InstanceId == id);
        private static bool IsFlashPhase(MatchPhase phase) => phase == MatchPhase.FlashBeforeBlock || phase == MatchPhase.FlashAfterBlock;
        private bool IsMainPhase(MatchPhase phase) => phase == MatchPhase.Main || RulesFormat == BattleRulesFormat.Standard && phase == MatchPhase.SecondMain;
        private static bool IsAttackPhase(MatchPhase phase) => phase == MatchPhase.Attack || phase == MatchPhase.Block || IsFlashPhase(phase);
        private static long AvailableCores(PlayerState player) => player.Reserve.Total + player.Field.Sum(s => (long)s.Cores.Total);
        private bool RequireMainTurn(int player, out string error) => Require(State.PendingChoice == null && player == State.ActivePlayerIndex && IsMainPhase(State.Phase), "This action requires your Main Step.", out error);
        private bool RequireAbilityWindow(int player, out string error) => Require(State.PendingChoice == null && (player == State.ActivePlayerIndex && IsMainPhase(State.Phase) || IsFlashPhase(State.Phase) && player == State.FlashPlayerIndex), "This action requires your Main Step or Flash priority.", out error);
        private static bool Require(bool condition, string message, out string error) { error = condition ? "" : message; return condition; }
        private static bool Fail(string message, out string error) { error = message; return false; }
        private bool Success(out string error) { ProcessEffects(); AssertInvariants(); error = ""; return true; }
        private void BeginAction() => LastEvents.Clear();
        private void AddLog(string text) { Log.Add(text); if (Log.Count > 300) Log.RemoveAt(0); }
        internal void Emit(string kind, int actor, int spiritId = -1, int targetId = -1, string cardId = "", int amount = 0)
        { var e = new BattleEvent { Kind = kind, Actor = actor, SpiritId = spiritId, TargetId = targetId, CardId = cardId, Amount = amount }; LastEvents.Add(e); EventEmitted?.Invoke(e); }

        // Effects added by a running effect resolve before its pre-existing continuation.
        internal void QueueEffect(Action action) { if (action == null) return; if (nestedEffects != null) nestedEffects.Add(action); else effects.AddLast(action); }
        internal void ProcessEffects()
        {
            if (processingEffects) return;
            processingEffects = true;
            try
            {
                int guard = 0;
                while (State.PendingChoice == null && effects.Count > 0 && State.Phase != MatchPhase.GameOver)
                {
                    if (++guard > 10000) throw new InvalidOperationException("Effect resolution did not terminate.");
                    var action = effects.First.Value; effects.RemoveFirst(); nestedEffects = new List<Action>();
                    try { action(); }
                    finally { for (int i = nestedEffects.Count - 1; i >= 0; i--) effects.AddFirst(nestedEffects[i]); nestedEffects = null; }
                }
            }
            finally { processingEffects = false; }
        }
        internal void QueueChoice(int owner, string prompt, int min, int max, IEnumerable<ChoiceOption> options, Action<int[]> resolved, bool ordered = false)
        {
            var array = options?.ToArray() ?? Array.Empty<ChoiceOption>();
            if (min < 0 || max < min || min > array.Length) throw new ArgumentException("Invalid choice bounds.");
            max = Math.Min(max, array.Length);
            if (max == 0) { QueueEffect(() => resolved(Array.Empty<int>())); return; }
            Action show = () => { State.PendingChoice = new PendingChoice { Id = nextChoiceId++, Owner = owner, Prompt = prompt, Min = min, Max = max, Ordered = ordered, Options = array }; choiceResolved = resolved; Emit("ChoiceRequested", owner); };
            if (State.PendingChoice != null) QueueEffect(show); else show();
        }
        private bool TryChoose(int actor, BattleCommand command, out string error)
        {
            var choice = State.PendingChoice;
            if (choice == null || choice.Owner != actor) return Fail("There is no choice for this player.", out error);
            var chosen = command.selection ?? Array.Empty<int>();
            if (chosen.Length < choice.Min || chosen.Length > choice.Max || chosen.Distinct().Count() != chosen.Length || chosen.Any(i => i < 0 || i >= choice.Options.Length)) return Fail("Select the required number of distinct listed options.", out error);
            var callback = choiceResolved; State.PendingChoice = null; choiceResolved = null;
            effects.AddFirst(() => callback(chosen)); Emit("ChoiceResolved", actor, amount: chosen.Length);
            return Success(out error);
        }
        public bool TryExecute(int actor, BattleCommand command, out string error)
        {
            BeginAction();
            if (command == null || actor < 0 || actor > 1) return Fail("Invalid match command.", out error);
            if (State.Phase == MatchPhase.GameOver) return Fail("The match has ended.", out error);
            string action = (command.action ?? "").ToLowerInvariant();
            if (action == "concede") return TryConcede(actor, out error);
            if (actor != DecisionPlayerIndex) return Fail("The other player must decide first.", out error);
            if (State.PendingChoice != null) return action == "choose" ? TryChoose(actor, command, out error) : Fail("Resolve the pending choice first.", out error);
            switch (action)
            {
                case "opening": case "mulligan": return TryConfirmOpening(actor, command.redraw, out error);
                case "advance": case "next": return TryAdvance(actor, out error);
                case "summon": case "place": return TrySummon(actor, command, out error);
                case "magic": case "playmagic": return TryPlayMagic(actor, command, out error);
                case "attack": return TryAttack(actor, command.source >= 0 ? command.source : command.index, out error);
                case "block": return TryBlock(actor, command.source >= 0 ? command.source : command.index, out error);
                case "take-life": case "takelife": return TryTakeLife(actor, out error);
                case "pass": return IsFlashPhase(State.Phase) ? TryPassFlash(actor, out error) : State.Phase == MatchPhase.Block ? TryTakeLife(actor, out error) : TryAdvance(actor, out error);
                case "transfer": case "movecore": return TryTransfer(actor, command, out error);
                case "swap": return TrySwapCore(actor, command.source, command.target, command.soul, out error);
                case "attackstep": return TryBeginAttackStep(actor, out error);
                case "secondmain": return TryBeginSecondMain(actor, out error);
                case "end": case "endturn": return TryEndTurn(actor, out error);
                case "activate": if (!RequireAbilityWindow(actor, out error)) return false; if (!EffectActivate(actor, command, out error)) return false; bool wasFlash = IsFlashPhase(State.Phase); QueueEffect(() => CompleteAbility(actor, wasFlash)); return Success(out error);
                default: return Fail("Unknown Battle Spirits command: " + command.action, out error);
            }
        }
        public bool TryConfirmOpening(int player, bool redraw, out string error)
        {
            BeginAction();
            if (State.Phase != MatchPhase.Opening || player != DecisionPlayerIndex) return Fail("Wait for your opening-hand decision.", out error);
            var owner = State.Players[player];
            if (redraw) { owner.Deck.AddRange(owner.Hand); owner.Hand.Clear(); Shuffle(owner.Deck); Draw(player, 4); owner.MulliganUsed = true; }
            openingChoices++; Emit(redraw ? "OpeningRedrawn" : "OpeningKept", player);
            if (openingChoices == 2) StartTurn(firstPlayer);
            return Success(out error);
        }
        private void StartTurn(int owner)
        {
            State.ActivePlayerIndex = owner; State.TurnNumber++; ClearBattle();
            foreach (var p in State.Players)
            {
                p.LifeReducedThisTurn = false; p.OpponentDepletedThisTurn = false; p.AttacksOrBlocksThisTurn = 0;
                foreach (var s in p.Field) { s.TurnBpBonus = 0; s.BattleLevelOverride = 0; }
            }
            if (State.Players[owner].Deck.Count == 0) { Win(1 - owner, "The opposing deck was empty at the Start Step."); return; }
            EnterStep(MatchPhase.Start); Emit("TurnStarted", owner, amount: State.TurnNumber);
        }
        private void EnterStep(MatchPhase phase)
        { State.Phase = phase; Emit("StepStarted", State.ActivePlayerIndex, amount: (int)phase); QueueEffect(() => EffectStep(phase, State.ActivePlayerIndex)); }
        public bool TryAdvance(int player, out string error)
        {
            BeginAction();
            if (player != DecisionPlayerIndex || State.PendingChoice != null) return Fail("A decision is pending.", out error);
            var owner = State.Players[player];
            switch (State.Phase)
            {
                case MatchPhase.Start:
                    if (State.TurnNumber == 1) { EnterStep(MatchPhase.Draw); Draw(player); } else { EnterStep(MatchPhase.Core); AddReserve(player, 1); } break;
                case MatchPhase.Core: EnterStep(MatchPhase.Draw); Draw(player); break;
                case MatchPhase.Draw:
                    EnterStep(MatchPhase.Refresh); Transfer(owner.Trash, owner.Reserve, owner.Trash.Total);
                    foreach (var s in owner.Field) { if (s.HeavyExhausted) { s.HeavyExhausted = false; s.Exhausted = true; } else s.Exhausted = false; }
                    Emit("Refreshed", player); break;
                case MatchPhase.Refresh: EnterStep(MatchPhase.Main); break;
                case MatchPhase.Main: EnterStep(State.TurnNumber == 1 ? MatchPhase.End : MatchPhase.Attack); break;
                case MatchPhase.Attack: EnterStep(RulesFormat == BattleRulesFormat.Standard ? MatchPhase.SecondMain : MatchPhase.End); break;
                case MatchPhase.SecondMain: EnterStep(MatchPhase.End); break;
                case MatchPhase.End: StartTurn(1 - player); break;
                default: return Fail("Resolve this battle before advancing the step.", out error);
            }
            return Success(out error);
        }
        public bool TryBeginAttackStep(int player, out string error)
        { if (State.Phase != MatchPhase.Main || State.TurnNumber == 1) return Fail("The first player skips the first Attack Step.", out error); return TryAdvance(player, out error); }
        public bool TryBeginSecondMain(int player, out string error)
        { if (State.Phase != MatchPhase.Attack || RulesFormat != BattleRulesFormat.Standard) return Fail("Second Main follows the Standard Attack Step.", out error); return TryAdvance(player, out error); }
        public bool TryEndTurn(int player, out string error)
        { if (player != State.ActivePlayerIndex || !(IsMainPhase(State.Phase) || State.Phase == MatchPhase.Attack || State.Phase == MatchPhase.End)) return Fail("Resolve this step before ending the turn.", out error); return TryAdvance(player, out error); }
        public bool TryConcede(int player, out string error)
        { BeginAction(); if (player < 0 || player > 1 || State.Phase == MatchPhase.GameOver) return Fail("The match has ended or player is invalid.", out error); Win(1 - player, "The opponent conceded."); return Success(out error); }
        private void Win(int player, string reason)
        { State.WinnerIndex = player; State.Phase = MatchPhase.GameOver; State.PendingChoice = null; choiceResolved = null; effects.Clear(); ClearBattle(); AddLog(State.Players[player].Name + " wins. " + reason); Emit("GameWon", player); }

        public int GetSummonCost(int owner, CardDefinition card) => GetCost(owner, card, null, out _, out _);
        private int GetCost(int owner, CardDefinition card, string[] succession, out PaymentContext context, out string error, int[] successionIndices = null)
        {
            context = new PaymentContext(); error = "";
            if (successionIndices != null && successionIndices.Length != (succession?.Length ?? 0)) { error = "Select the physical Trash copy for each Succession card."; return -1; }
            var reductions = card.reductions?.Length > 0 ? card.reductions : new[] { new Reduction { color = card.color.ToString(), count = card.reductionSymbols } };
            var slots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in reductions) { slots.TryGetValue(r.color, out int n); slots[r.color] = n + r.count; }
            if (succession != null && succession.Length > 0)
            {
                if (!AbilityRegistry.HasSuccession(card.id)) { error = "This card has no Succession."; return -1; }
                if (succession.Length > slots.Values.Sum()) { error = "Succession cannot exceed the printed reduction slots."; return -1; }
                var used = new HashSet<int>(); var trash = State.Players[owner].Discard;
                for (int selection = 0; selection < succession.Length; selection++)
                {
                    string id = succession[selection];
                    int index = successionIndices == null ? -1 : successionIndices[selection];
                    if (successionIndices == null)
                        for (int i = 0; i < trash.Count; i++) if (trash[i] == id && !used.Contains(i) && EffectExSymbols(owner, id, i) > 0) { index = i; break; }
                    if (index >= trash.Count || index >= 0 && (trash[index] != id || used.Contains(index))) { error = "The selected physical Succession copy is unavailable."; return -1; }
                    if (index < 0) { error = "A selected Succession card is not in your Trash."; return -1; }
                    int ex = EffectExSymbols(owner, id, index);
                    if (ex <= 0) { error = "Succession requires a card bearing an EX symbol."; return -1; }
                    var removed = GetCard(id); int matched = 0;
                    foreach (var symbol in removed.symbolDetails ?? Array.Empty<CardSymbol>())
                    {
                        if (!symbol.isEx && !(string.Equals(symbol.color, "Purple", StringComparison.OrdinalIgnoreCase) && ex > removed.ExSymbols)) continue;
                        slots.TryGetValue(symbol.color, out int left); int use = Math.Min(left, Math.Min(ex - matched, symbol.count));
                        if (use > 0) { slots[symbol.color] -= use; matched += use; }
                    }
                    if (matched == 0) { error = "The EX symbol must match an unfilled printed reduction slot."; return -1; }
                    used.Add(index); context.SuccessionEx += matched; context.RemovedCards.Add(id); context.RemovedCardIndices.Add(index);
                    if (removed.HasFamily("Crimson Cloud") || removed.HasFamily("Infernowing")) context.FamilyEx += matched;
                }
            }
            var available = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var permanent in State.Players[owner].Field.Where(v => v.Level > 0))
                foreach (var symbol in permanent.Card.symbolDetails ?? Array.Empty<CardSymbol>()) { available.TryGetValue(symbol.color, out int n); available[symbol.color] = n + symbol.count; }
            int discount = context.SuccessionEx;
            foreach (var slot in slots) { available.TryGetValue(slot.Key, out int symbols); discount += Math.Min(slot.Value, symbols + EffectExtraReduction(owner, card)); }
            return Math.Max(0, card.cost - discount);
        }
        private CorePool Pool(int owner, int source) => source == -1 ? State.Players[owner].Reserve : State.Players[owner].Field.FirstOrDefault(s => s.InstanceId == source)?.Cores;
        private CoreTransfer[] DefaultTransfers(int owner, int amount, int reserveForPlacement = 0)
        {
            var result = new List<CoreTransfer>(); var p = State.Players[owner];
            var pools = new List<KeyValuePair<int, CorePool>> { new KeyValuePair<int, CorePool>(-1, p.Reserve) };
            pools.AddRange(p.Field.Select(s => new KeyValuePair<int, CorePool>(s.InstanceId, s.Cores)));
            // Ordinary cores before Soul; preserve field minimums where possible.
            foreach (var pair in pools)
            {
                int keep = pair.Key == -1 ? Math.Min(reserveForPlacement, pair.Value.Total) : Math.Max(0, FindSpirit(pair.Key).Card.levels[0].cores - (pair.Value.Soul ? 1 : 0));
                int count = Math.Min(amount, Math.Max(0, pair.Value.Normal - keep));
                if (count > 0) { result.Add(new CoreTransfer { source = pair.Key, normal = count }); amount -= count; }
            }
            foreach (var pair in pools)
            {
                int taken = result.Where(t => t.source == pair.Key).Sum(t => t.normal);
                int count = Math.Min(amount, pair.Value.Normal - taken);
                if (count > 0) { var existing = result.FirstOrDefault(t => t.source == pair.Key); if (existing != null) existing.normal += count; else result.Add(new CoreTransfer { source = pair.Key, normal = count }); amount -= count; }
            }
            foreach (var pair in pools) if (amount > 0 && pair.Value.Soul) { var transfer = result.FirstOrDefault(t => t.source == pair.Key); if (transfer == null) { transfer = new CoreTransfer { source = pair.Key }; result.Add(transfer); } transfer.soul = true; amount--; }
            if (amount != 0) return null;
            return result.ToArray();
        }
        private bool ValidateTransfers(int owner, CoreTransfer[] transfers, int amount, out string error)
        {
            if (transfers == null) return Fail("Not enough cores for this payment.", out error);
            long sum = 0; var used = new HashSet<int>();
            foreach (var t in transfers)
            {
                var pool = t == null ? null : Pool(owner, t.source);
                if (pool == null || t.normal < 0 || t.normal > pool.Normal || t.soul && !pool.Soul || !used.Add(t.source)) return Fail("Invalid payment core source.", out error);
                sum += t.normal + (t.soul ? 1 : 0);
            }
            return Require(sum == amount, "Payment must specify exactly " + amount + " cores.", out error);
        }
        private void Pay(int owner, CoreTransfer[] transfers, PaymentContext context)
        {
            foreach (var t in transfers) { var pool = Pool(owner, t.source); pool.Normal -= t.normal; State.Players[owner].Trash.Normal += t.normal; if (t.soul) { MoveOne(pool, State.Players[owner].Trash, true); context.SoulPaid = true; } }
            foreach (var trashIndex in context.RemovedCardIndices.OrderByDescending(i => i)) { string id = State.Players[owner].Discard[trashIndex]; EffectTrashRemoved(owner, trashIndex); State.Players[owner].Discard.RemoveAt(trashIndex); State.Players[owner].Removed.Add(id); Emit("SuccessionRemoved", owner, cardId: id); }
            foreach (var s in State.Players[owner].Field.ToArray()) if (s.Level == 0) DepleteSpirit(s);
        }
        public bool TrySummon(int owner, int handIndex, out string error) => TrySummon(owner, new BattleCommand { action = "summon", index = handIndex }, out error);
        private bool TrySummon(int owner, BattleCommand command, out string error)
        {
            BeginAction(); if (!RequireMainTurn(owner, out error)) return false;
            var p = State.Players[owner]; if (command.index < 0 || command.index >= p.Hand.Count) return Fail("The selected hand card is unavailable.", out error);
            var card = GetCard(p.Hand[command.index]);
            if ((!card.IsSpirit && !card.IsNexus) || !card.IsPlayableFor(RulesFormat)) return Fail("Select a supported Spirit or Nexus.", out error);
            if (command.chooseSuccession) return ConfigureSuccession(owner, card, command, out error);
            int cost = GetCost(owner, card, command.succession, out var context, out error, command.successionIndices); if (cost < 0) return false;
            int minimum = card.levels[0].cores;
            if (AvailableCores(p) < (long)cost + minimum) return Fail("Not enough cores to pay and maintain this card.", out error);
            if (command.chooseCores)
            {
                ChooseCoreTransfers(owner, cost, "Choose cores to pay for " + card.DisplayName, payment =>
                    ChooseCoreTransfers(owner, minimum, "Choose cores to place onto " + card.DisplayName, placement =>
                    {
                        var configured = CopyCommand(command); configured.chooseCores = false; configured.payment = payment; configured.transfers = placement;
                        if (!TrySummon(owner, configured, out var failure)) throw new InvalidOperationException(failure);
                    }, payment));
                return Success(out error);
            }
            var payment = command.payment ?? DefaultTransfers(owner, cost, minimum); if (!ValidateTransfers(owner, payment, cost, out error)) return false;
            // Placement may be explicit; validate against pools after payment before mutating.
            var placement = command.transfers;
            if (placement != null && !ValidatePlacement(owner, payment, placement, minimum, out error)) return false;
            string id = card.id; p.Hand.RemoveAt(command.index); Pay(owner, payment, context);
            var spirit = CreatePermanent(owner, card);
            if (placement == null) AllocateDefault(owner, spirit, minimum); else ApplyPlacement(owner, spirit, placement);
            p.Field.Add(spirit); Emit(card.IsNexus ? "NexusPlaced" : "Summoned", owner, spirit.InstanceId, cardId: id, amount: cost);
            QueueEffect(() => EffectSummoned(spirit, context)); return Success(out error);
        }
        private SpiritState CreatePermanent(int owner, CardDefinition card) => new SpiritState { InstanceId = nextSpiritId++, Owner = owner, Card = card, ContinuousBp = EffectContinuousBp };
        private bool ValidatePlacement(int owner, CoreTransfer[] payment, CoreTransfer[] placement, int minimum, out string error)
        {
            var used = new HashSet<int>(); long total = 0;
            foreach (var t in placement)
            {
                var p = t == null ? null : Pool(owner, t.source); var paid = t == null ? null : payment.FirstOrDefault(v => v.source == t.source);
                if (p == null || t.normal < 0 || !used.Add(t.source) || t.normal > p.Normal - (paid?.normal ?? 0) || t.soul && (!p.Soul || paid?.soul == true)) return Fail("Invalid placement core source after payment.", out error);
                total += t.normal + (t.soul ? 1 : 0);
            }
            return Require(total >= minimum, "A summoned card must have enough cores for Lv1.", out error);
        }
        private void ApplyPlacement(int owner, SpiritState target, CoreTransfer[] placement)
        {
            foreach (var t in placement) { var pool = Pool(owner, t.source); if (pool == null) pool = State.Players[owner].Reserve; pool.Normal -= t.normal; target.Cores.Normal += t.normal; if (t.soul) MoveOne(pool, target.Cores, true); }
            foreach (var s in State.Players[owner].Field.ToArray()) if (s.Level == 0) DepleteSpirit(s);
        }
        private void AllocateDefault(int owner, SpiritState target, int amount)
        {
            var transfers = DefaultTransfers(owner, amount);
            if (transfers == null) throw new InvalidOperationException("Validated allocation is unavailable.");
            ApplyPlacement(owner, target, transfers);
        }
        public bool TryPlayMagic(int owner, int handIndex, int targetId, CoreTransfer[] transfers, out string error)
            => TryPlayMagic(owner, new BattleCommand { action = "magic", index = handIndex, target = targetId, transfers = transfers }, out error);
        private bool TryPlayMagic(int owner, BattleCommand command, out string error)
        {
            BeginAction(); if (!RequireAbilityWindow(owner, out error)) return false;
            var p = State.Players[owner]; if (command.index < 0 || command.index >= p.Hand.Count) return Fail("The selected hand card is unavailable.", out error);
            var card = GetCard(p.Hand[command.index]); if (!card.IsMagic || !card.IsPlayableFor(RulesFormat)) return Fail("Select a supported Magic card.", out error);
            if (!EffectCanPlayMagic(owner, card, command, out error)) return false;
            if (command.target >= 0 && FindSpirit(command.target) is SpiritState target && !EffectCanBeMagicTarget(owner, target)) return Fail("This Spirit cannot be selected by this Magic.", out error);
            if (command.chooseSuccession) return ConfigureSuccession(owner, card, command, out error);
            int cost = GetCost(owner, card, command.succession, out var context, out error, command.successionIndices); if (cost < 0) return false;
            CoreTransfer[] alternatePayment = null;
            if (command.alternateCost)
            {
                if (!AbilityRegistry.SoulMagicColor(card.id, out var color) || !HasReductionSymbol(owner, color) || !HasAvailableSoul(owner) || command.succession?.Length > 0)
                    return Fail("Soul Magic requires its matching Field symbol and an available Soul Core.", out error);
                cost = 1; alternatePayment = SoulPayment(owner, 1);
                if (command.payment != null && (command.payment.Sum(t => t?.normal ?? 0) != 0 || command.payment.Count(t => t != null && t.soul) != 1)) return Fail("Soul Magic must be paid with only the Soul Core.", out error);
            }
            if (command.chooseCores && !command.alternateCost)
            {
                if (AvailableCores(p) < cost) return Fail("Not enough cores for this payment.", out error);
                ChooseCoreTransfers(owner, cost, "Choose cores to pay for " + card.DisplayName, payment =>
                {
                    var configured = CopyCommand(command); configured.chooseCores = false; configured.payment = payment;
                    if (!TryPlayMagic(owner, configured, out var failure)) throw new InvalidOperationException(failure);
                });
                return Success(out error);
            }
            var payment = command.payment ?? alternatePayment ?? DefaultTransfers(owner, cost); if (!ValidateTransfers(owner, payment, cost, out error)) return false;
            bool flash = IsFlashPhase(State.Phase); p.Hand.RemoveAt(command.index); Pay(owner, payment, context); p.Discard.Add(card.id);
            Emit("MagicPlayed", owner, targetId: command.target, cardId: card.id, amount: cost);
            QueueEffect(() => EffectPlayMagic(owner, card, command, context)); QueueEffect(() => CompleteAbility(owner, flash)); return Success(out error);
        }
        private void CompleteAbility(int player, bool flash) { if (flash && IsFlashPhase(State.Phase)) { consecutiveFlashPasses = 0; State.FlashPlayerIndex = 1 - player; } }
        public bool TryMoveCore(int owner, int source, int target, bool soul, out string error)
            => TryTransfer(owner, new BattleCommand { action = "transfer", source = source, target = target, soul = soul }, out error);
        private bool TryTransfer(int owner, BattleCommand command, out string error)
        {
            BeginAction(); if (!RequireMainTurn(owner, out error)) return false;
            var from = Pool(owner, command.source); var to = Pool(owner, command.target);
            int amount = command.mode <= 0 ? 1 : command.mode;
            if (from == null || to == null || from == to || command.soul && (!from.Soul || to.Soul) || !command.soul && from.Normal < amount) return Fail("Select available cores in two different friendly pools.", out error);
            if (command.soul) MoveOne(from, to, true); else { from.Normal -= amount; to.Normal += amount; }
            foreach (var s in State.Players[owner].Field.ToArray()) if (s.Level == 0) DepleteSpirit(s);
            Emit("CoreMoved", owner, command.source, command.target, amount: command.soul ? 1 : amount); return Success(out error);
        }
        public bool TrySwapCore(int owner, int source, int target, bool sourceSoul, out string error)
        {
            BeginAction(); if (!RequireMainTurn(owner, out error)) return false;
            var a = Pool(owner, source); var b = Pool(owner, target); var soul = sourceSoul ? a : b; var normal = sourceSoul ? b : a;
            if (a == null || b == null || a == b || !soul.Soul || normal.Soul || normal.Normal < 1) return Fail("Select a Soul Core and an ordinary core in different friendly pools.", out error);
            soul.Soul = false; soul.Normal++; normal.Normal--; normal.Soul = true; Emit("CoreSwapped", owner, source, target); return Success(out error);
        }
        public bool TryAttack(int owner, int spiritId, out string error)
        {
            BeginAction(); if (State.PendingChoice != null || State.ActivePlayerIndex != owner || State.Phase != MatchPhase.Attack) return Fail("Attacks require your Attack Step with no pending choice.", out error);
            var attacker = FindSpirit(spiritId);
            if (attacker == null || attacker.Owner != owner || !attacker.Card.IsSpirit || attacker.Exhausted || attacker.HeavyExhausted || !EffectCanAttack(attacker)) return Fail("Choose one of your refreshed Spirits that can attack.", out error);
            attacker.Exhausted = true; State.PendingAttackerId = spiritId; State.PendingBlockerId = -1;
            State.Players[owner].AttacksOrBlocksThisTurn++; Emit("Attacked", owner, spiritId, cardId: attacker.Card.id);
            QueueEffect(() => EffectAttacked(attacker)); QueueEffect(() => BeginFlash(MatchPhase.FlashBeforeBlock)); return Success(out error);
        }
        private void BeginFlash(MatchPhase phase)
        { if (State.Phase == MatchPhase.GameOver) return; State.Phase = phase; State.FlashPlayerIndex = 1 - State.ActivePlayerIndex; consecutiveFlashPasses = 0; Emit("FlashStarted", State.FlashPlayerIndex); }
        public bool TryPassFlash(int owner, out string error)
        {
            BeginAction(); if (State.PendingChoice != null || !IsFlashPhase(State.Phase) || State.FlashPlayerIndex != owner) return Fail("Wait for your Flash priority with no pending choice.", out error);
            consecutiveFlashPasses++; Emit("FlashPassed", owner);
            if (consecutiveFlashPasses < 2) State.FlashPlayerIndex = 1 - owner;
            else
            {
                State.FlashPlayerIndex = -1;
                if (FindSpirit(State.PendingAttackerId) == null) EndBattle();
                else if (State.Phase == MatchPhase.FlashBeforeBlock) State.Phase = MatchPhase.Block;
                else ResolveBlockedBattle();
            }
            return Success(out error);
        }
        public bool TryBlock(int owner, int spiritId, out string error)
        {
            BeginAction(); if (State.PendingChoice != null || State.Phase != MatchPhase.Block || owner != 1 - State.ActivePlayerIndex) return Fail("Wait for the block decision.", out error);
            var blocker = FindSpirit(spiritId);
            if (blocker == null || blocker.Owner != owner || !blocker.Card.IsSpirit || blocker.Exhausted || blocker.HeavyExhausted) return Fail("Only a refreshed defending Spirit can block.", out error);
            blocker.Exhausted = true; State.PendingBlockerId = spiritId; State.Players[owner].AttacksOrBlocksThisTurn++;
            Emit("Blocked", owner, spiritId, State.PendingAttackerId, blocker.Card.id);
            QueueEffect(() => EffectBlocked(blocker)); QueueEffect(() => BeginFlash(MatchPhase.FlashAfterBlock)); return Success(out error);
        }
        public bool TryTakeLife(int owner, out string error)
        {
            BeginAction(); if (State.PendingChoice != null || State.Phase != MatchPhase.Block || owner != 1 - State.ActivePlayerIndex) return Fail("Wait for the block decision.", out error);
            var attacker = FindSpirit(State.PendingAttackerId);
            if (attacker == null) { EndBattle(); return Success(out error); }
            if (EffectMustBlock(attacker) && State.Players[owner].Field.Any(s => s.Card.IsSpirit && !s.Exhausted && !s.HeavyExhausted)) return Fail("This attacking Spirit must be blocked if possible.", out error);
            int symbols = (attacker.Card.symbolDetails ?? Array.Empty<CardSymbol>()).Sum(s => Math.Max(0, s.count));
            int damage = EffectAttackDamage(attacker, symbols);
            int actualDamage = Math.Min(damage, State.Players[owner].Life); DamageLife(owner, damage);
            QueueEffect(() => EffectAttackLifeLost(attacker, actualDamage));
            QueueEffect(EndBattle); return Success(out error);
        }
        private int LastLifeDamage;
        internal void DamageLife(int owner, int count)
        {
            var p = State.Players[owner]; count = Math.Max(0, Math.Min(count, p.Life)); LastLifeDamage = count;
            if (count == 0) return;
            p.Life -= count; p.Reserve.Normal += count; p.LifeReducedThisTurn = true;
            Emit("LifeLost", owner, amount: count); QueueEffect(() => EffectLifeLost(owner, count));
            if (p.Life == 0) Win(1 - owner, "The opponent has no Life remaining.");
        }
        private void ResolveBlockedBattle()
        {
            var attacker = FindSpirit(State.PendingAttackerId); var blocker = FindSpirit(State.PendingBlockerId);
            if (attacker != null && blocker != null)
            {
                int attackBp = attacker.Bp, blockBp = blocker.Bp; Emit("BpCompared", State.ActivePlayerIndex, attacker.InstanceId, blocker.InstanceId, amount: attackBp - blockBp);
                // Determine both outcomes before resolving destruction triggers.
                var destroyed = new List<SpiritState>();
                if (attackBp <= blockBp) destroyed.Add(attacker);
                if (blockBp <= attackBp) destroyed.Add(blocker);
                DestroySpirits(destroyed);
            }
            QueueEffect(EndBattle);
        }
        private void EndBattle()
        {
            var attacker = FindSpirit(State.PendingAttackerId);
            QueueEffect(() => { if (attacker != null) EffectBattleEnded(attacker); });
            QueueEffect(() => { ClearBattle(); if (State.Phase != MatchPhase.GameOver) State.Phase = MatchPhase.Attack; Emit("BattleEnded", State.ActivePlayerIndex); });
        }
        private void ClearBattle()
        {
            foreach (var p in State.Players) foreach (var s in p.Field) { s.BattleBpBonus = 0; s.BattleLevelOverride = 0; }
            State.PendingAttackerId = State.PendingBlockerId = State.FlashPlayerIndex = -1; consecutiveFlashPasses = 0;
        }
        internal void ExhaustSpirit(SpiritState target, int effectOwner)
        {
            if (target == null || FindSpirit(target.InstanceId) == null || target.Exhausted || target.HeavyExhausted) return;
            target.Exhausted = true; Emit("Exhausted", effectOwner, target.InstanceId, cardId: target.Card.id); QueueEffect(() => EffectExhausted(target, effectOwner));
        }
        internal void AttachCores(SpiritState target, int normal, bool soul = false)
        {
            if (target == null || FindSpirit(target.InstanceId) == null || normal < 0) return;
            var reserve = State.Players[target.Owner].Reserve;
            normal = Math.Min(normal, reserve.Normal); reserve.Normal -= normal; target.Cores.Normal += normal;
            if (soul && reserve.Soul && !target.Cores.Soul) MoveOne(reserve, target.Cores, true);
            Emit("CoreMoved", target.Owner, -1, target.InstanceId, amount: normal + (soul ? 1 : 0));
        }
        internal void AddReserve(int owner, int count)
        { if (count <= 0) return; State.Players[owner].Reserve.Normal += count; State.Players[owner].CoreGains += count; Emit("CoreGained", owner, amount: count); }
        internal void Draw(int owner, int count = 1)
        {
            var p = State.Players[owner]; int drawn = 0;
            for (int i = 0; i < count && p.Deck.Count > 0; i++) { p.Hand.Add(p.Deck[0]); p.Deck.RemoveAt(0); drawn++; }
            if (drawn > 0) Emit("Drawn", owner, amount: drawn);
        }
        internal void DestroySpirit(SpiritState target, string reason = "Destroyed", bool allowReplace = true)
            => DestroySpirits(new[] { target }, reason);
        internal void DestroySpirits(IEnumerable<SpiritState> targets, string reason = "Destroyed")
        {
            var pending = targets.Where(s => s != null && !s.PendingRemoval && State.Players[s.Owner].Field.Contains(s)).Distinct().ToArray();
            var triggers = new List<TriggeredEffect>();
            foreach (var target in pending)
            {
                target.PendingRemoval = true; int level = target.Level;
                Emit("Destroyed", target.Owner, target.InstanceId, cardId: target.Card.id);
                if (EffectHasDestructionTrigger(target, level)) AddTrigger(triggers, target, "when destroyed", () => EffectDestroyed(target, level, reason));
            }
            QueueTriggers(triggers);
            QueueEffect(() => { foreach (var target in pending) FinishRemoval(target); });
        }
        private void FinishRemoval(SpiritState target)
        {
            var p = State.Players[target.Owner]; if (!p.Field.Remove(target)) return;
            p.Discard.Add(target.Card.id); Transfer(target.Cores, p.Reserve, target.Cores.Total); target.PendingRemoval = false;
        }
        internal void DepleteSpirit(SpiritState target, int effectOwner = -1)
        {
            if (target == null || target.PendingRemoval || !State.Players[target.Owner].Field.Contains(target)) return;
            target.PendingRemoval = true;
            if (effectOwner >= 0 && effectOwner != target.Owner) State.Players[effectOwner].OpponentDepletedThisTurn = true;
            Emit("Depleted", target.Owner, target.InstanceId, cardId: target.Card.id);
            QueueEffect(() => EffectDepleted(target, effectOwner)); QueueEffect(() => FinishRemoval(target));
        }
        internal void ReturnSpirit(SpiritState target, string destination = "hand")
        {
            if (target == null || !State.Players[target.Owner].Field.Remove(target)) return;
            var p = State.Players[target.Owner]; Transfer(target.Cores, p.Reserve, target.Cores.Total);
            if (destination == "deck" || destination == "bottom") p.Deck.Add(target.Card.id);
            else if (destination == "top") p.Deck.Insert(0, target.Card.id);
            else if (destination == "trash" || destination == "discard") p.Discard.Add(target.Card.id);
            else p.Hand.Add(target.Card.id);
            Emit("Returned", target.Owner, target.InstanceId, cardId: target.Card.id);
        }
        private static SpiritState Capture(SpiritState s) => new SpiritState { InstanceId = s.InstanceId, Owner = s.Owner, Card = s.Card,
            Cores = new CorePool { Normal = s.Cores.Normal, Soul = s.Cores.Soul }, Exhausted = s.Exhausted, HeavyExhausted = s.HeavyExhausted,
            TurnBpBonus = s.TurnBpBonus, BattleBpBonus = s.BattleBpBonus, BattleLevelOverride = s.BattleLevelOverride, ContinuousBp = s.ContinuousBp };
        internal void EffectSummonFromZone(int owner, string cardId, string zone, bool free, Action<SpiritState> completed = null)
        {
            var p = State.Players[owner]; var source = zone == "hand" ? p.Hand : p.Discard; var card = GetCard(cardId);
            if (!source.Contains(cardId) || !card.IsSpirit) { completed?.Invoke(null); return; }
            int cost = free ? 0 : GetSummonCost(owner, card); int minimum = card.levels[0].cores;
            if (AvailableCores(p) < (long)cost + minimum) { completed?.Invoke(null); return; }
            ChooseCoreTransfers(owner, cost, "Choose a core to pay for " + card.DisplayName, payment =>
            {
                if (!source.Contains(cardId)) { completed?.Invoke(null); return; }
                var context = new PaymentContext();
                if (source == p.Discard) EffectTrashRemoved(owner, source.IndexOf(cardId)); source.Remove(cardId);
                // Keep the card conserved while payment-trigger choices resolve.
                p.Revealed.Add(cardId); Pay(owner, payment, context);
                QueueEffect(() => ChooseCoreTransfers(owner, minimum, "Choose a core to place onto " + card.DisplayName, placement =>
                {
                    var summoned = CreatePermanent(owner, card); ApplyPlacement(owner, summoned, placement);
                    p.Revealed.Remove(cardId); p.Field.Add(summoned);
                    Emit("Summoned", owner, summoned.InstanceId, cardId: cardId, amount: cost);
                    QueueEffect(() => EffectSummoned(summoned, context)); QueueEffect(() => completed?.Invoke(summoned));
                }));
            });
        }
        private void ChooseCoreTransfers(int owner, int amount, string prompt, Action<CoreTransfer[]> completed, CoreTransfer[] unavailable = null)
        {
            var chosen = new Dictionary<int, CoreTransfer>();
            Action<int> next = null;
            next = remaining =>
            {
                if (remaining == 0) { completed(chosen.Values.ToArray()); return; }
                var ids = new[] { -1 }.Concat(State.Players[owner].Field.Select(s => s.InstanceId)).ToArray();
                var options = new List<ChoiceOption>(); var sources = new List<int>(); var souls = new List<bool>();
                foreach (int id in ids)
                {
                    var pool = Pool(owner, id); chosen.TryGetValue(id, out var prior);
                    var reserved = unavailable?.FirstOrDefault(t => t.source == id);
                    string name = id == -1 ? "Reserve" : FindSpirit(id).Card.DisplayName;
                    if (pool.Normal - (reserved?.normal ?? 0) > (prior?.normal ?? 0)) { options.Add(new ChoiceOption { key = "normal:" + id, label = name + ": normal core", instanceId = id }); sources.Add(id); souls.Add(false); }
                    if (pool.Soul && prior?.soul != true && reserved?.soul != true) { options.Add(new ChoiceOption { key = "soul:" + id, label = name + ": Soul Core", instanceId = id }); sources.Add(id); souls.Add(true); }
                }
                if (options.Count == 0) throw new InvalidOperationException("A validated effect core allocation became unavailable.");
                QueueChoice(owner, prompt + " (" + remaining + " remaining)", 1, 1, options, selection =>
                {
                    int index = selection[0], id = sources[index];
                    if (!chosen.TryGetValue(id, out var item)) { item = new CoreTransfer { source = id }; chosen.Add(id, item); }
                    if (souls[index]) item.soul = true; else item.normal++;
                    next(remaining - 1);
                });
            };
            next(amount);
        }
        private static BattleCommand CopyCommand(BattleCommand c) => new BattleCommand {
            action = c.action, index = c.index, source = c.source, target = c.target, mode = c.mode, soul = c.soul,
            redraw = c.redraw, alternateCost = c.alternateCost, chooseCores = c.chooseCores, chooseSuccession = c.chooseSuccession,
            transfers = c.transfers, payment = c.payment, selection = c.selection, targets = c.targets,
            succession = c.succession, successionIndices = c.successionIndices };
        private bool CanFinishSuccession(int owner, CardDefinition card, List<int> selected)
        {
            var p = State.Players[owner];
            int minimum = card.IsMagic ? 0 : card.levels[0].cores;
            int cost = GetCost(owner, card, selected.Select(i => p.Discard[i]).ToArray(), out _, out _, selected.ToArray());
            if (cost < 0) return false;
            if (AvailableCores(p) >= (long)cost + minimum) return true;
            int slots = card.reductions?.Length > 0 ? card.reductions.Sum(r => r.count) : card.reductionSymbols;
            if (selected.Count >= slots || AvailableCores(p) < (long)Math.Max(0, card.cost - slots) + minimum) return false;
            var seen = new HashSet<string>();
            for (int i = 0; i < p.Discard.Count; i++)
            {
                int ex = EffectExSymbols(owner, p.Discard[i], i);
                if (selected.Contains(i) || ex <= 0 || !seen.Add(p.Discard[i] + ":" + ex)) continue;
                selected.Add(i); bool possible = CanFinishSuccession(owner, card, selected); selected.RemoveAt(selected.Count - 1);
                if (possible) return true;
            }
            return false;
        }
        private bool ConfigureSuccession(int owner, CardDefinition card, BattleCommand command, out string error)
        {
            if (!AbilityRegistry.HasSuccession(card.id) || command.alternateCost) return Fail("This cost cannot use Succession.", out error);
            var p = State.Players[owner]; var selected = new List<int>();
            if (!CanFinishSuccession(owner, card, selected)) return Fail("Not enough cores after the available Succession reductions.", out error);
            Action next = null;
            next = () =>
            {
                var options = new List<ChoiceOption>(); var indices = new List<int>();
                int cost = GetCost(owner, card, selected.Select(i => p.Discard[i]).ToArray(), out _, out _, selected.ToArray());
                int minimum = card.IsMagic ? 0 : card.levels[0].cores;
                if (AvailableCores(p) >= (long)cost + minimum)
                { options.Add(new ChoiceOption { key = "finish", label = "Finish Succession (pay " + cost + " cores)" }); indices.Add(-1); }
                for (int i = 0; i < p.Discard.Count; i++)
                {
                    if (selected.Contains(i) || EffectExSymbols(owner, p.Discard[i], i) <= 0) continue;
                    selected.Add(i); bool possible = CanFinishSuccession(owner, card, selected); selected.RemoveAt(selected.Count - 1);
                    if (!possible) continue;
                    options.Add(new ChoiceOption { key = "trash:" + i, index = i, cardId = p.Discard[i], label = "Remove " + GetCard(p.Discard[i]).DisplayName + " (Trash copy " + (i + 1) + ")" }); indices.Add(i);
                }
                QueueChoice(owner, "Choose EX cards for Succession (" + selected.Count + " selected)", 1, 1, options, choice =>
                {
                    int index = indices[choice[0]];
                    if (index >= 0) { selected.Add(index); next(); return; }
                    var configured = CopyCommand(command); configured.chooseSuccession = false;
                    configured.successionIndices = selected.ToArray(); configured.succession = selected.Select(i => p.Discard[i]).ToArray();
                    bool accepted = card.IsMagic ? TryPlayMagic(owner, configured, out var failure) : TrySummon(owner, configured, out failure);
                    if (!accepted) throw new InvalidOperationException(failure);
                });
            };
            next(); return Success(out error);
        }
        private static void Transfer(CorePool source, CorePool target, int amount)
        {
            if (amount < 0 || source.Total < amount || source.Soul && target.Soul && amount > source.Normal) throw new InvalidOperationException("Invalid core transfer.");
            int ordinary = Math.Min(amount, source.Normal); source.Normal -= ordinary; target.Normal += ordinary;
            if (amount > ordinary) MoveOne(source, target, true);
        }
        private static void MoveOne(CorePool source, CorePool target, bool soul)
        {
            if (soul) { if (!source.Soul || target.Soul) throw new InvalidOperationException("Invalid Soul Core transfer."); source.Soul = false; target.Soul = true; }
            else { if (source.Normal <= 0) throw new InvalidOperationException("No ordinary core is available."); source.Normal--; target.Normal++; }
        }
        private void Shuffle(List<string> deck)
        { for (int i = deck.Count - 1; i > 0; i--) { int j = random.Next(i + 1); string old = deck[i]; deck[i] = deck[j]; deck[j] = old; } }
        private void ValidateDeck(IReadOnlyList<string> deck, bool validate)
        {
            if (deck == null || deck.Count < 4) throw new ArgumentException("A rules fixture requires at least four cards.");
            if (validate) { var v = DeckValidator.Validate(cards.Values.ToArray(), deck, true, RulesFormat); if (!v.IsValid) throw new ArgumentException(v.Summary); }
            else foreach (var id in deck) if (!GetCard(id).IsPlayableFor(RulesFormat)) throw new ArgumentException("Unsupported card in rules fixture: " + id);
        }
        public void AssertInvariants()
        {
            var instances = new HashSet<int>();
            foreach (var p in State.Players)
            {
                long total = p.Life + p.Reserve.Total + p.Trash.Total; int souls = (p.Reserve.Soul ? 1 : 0) + (p.Trash.Soul ? 1 : 0);
                if (p.Reserve.Normal < 0 || p.Trash.Normal < 0 || p.Life < 0) throw new InvalidOperationException("Negative core pool.");
                foreach (var s in p.Field)
                {
                    if (s.Owner != p.Index || !instances.Add(s.InstanceId) || s.Level <= 0 && !s.PendingRemoval || s.Cores.Normal < 0 || (!s.Card.IsSpirit && !s.Card.IsNexus)) throw new InvalidOperationException("Invalid permanent on field.");
                    total += s.Cores.Total; souls += s.Cores.Soul ? 1 : 0;
                }
                if (total != 9L + p.CoreGains || souls != 1) throw new InvalidOperationException("Core conservation or Soul Core uniqueness violated for player " + p.Index + ".");
                if (p.Deck.Count + p.Hand.Count + p.Discard.Count + p.Removed.Count + p.Revealed.Count + p.Field.Count != originalDeckSizes[p.Index]) throw new InvalidOperationException("Card conservation violated for player " + p.Index + ".");
            }
        }
        /// <summary>Rebinds deterministic engine delegates after a puzzle has moved existing cards/cores into its fixture.</summary>
        public void RebasePuzzleState()
        {
            if (processingEffects || State.PendingChoice != null) throw new InvalidOperationException("Cannot rebase during effect resolution.");
            effects.Clear(); choiceResolved = null; openingChoices = 2;
            foreach (var p in State.Players) foreach (var s in p.Field) { s.Owner = p.Index; s.ContinuousBp = EffectContinuousBp; }
            nextSpiritId = Math.Max(1, State.Players.SelectMany(p => p.Field).Select(s => s.InstanceId).DefaultIfEmpty(0).Max() + 1);
            AssertInvariants(); LastEvents.Clear();
        }
        public BattleSnapshot GetSnapshot(int viewer)
        {
            if (viewer < -1 || viewer > 1) throw new ArgumentOutOfRangeException(nameof(viewer));
            var view = new BattleSnapshot { ViewerIndex = viewer, ActivePlayerIndex = State.ActivePlayerIndex, DecisionPlayerIndex = DecisionPlayerIndex,
                TurnNumber = State.TurnNumber, WinnerIndex = State.WinnerIndex, Phase = State.Phase, PendingAttackerId = State.PendingAttackerId,
                PendingBlockerId = State.PendingBlockerId, FlashPlayerIndex = State.FlashPlayerIndex };
            view.Players = State.Players.Select(p => new PlayerSnapshot { Index = p.Index, Name = p.Name, Life = p.Life, ReserveNormal = p.Reserve.Normal,
                ReserveSoul = p.Reserve.Soul, TrashNormal = p.Trash.Normal, TrashSoul = p.Trash.Soul, DeckCount = p.Deck.Count, HandCount = p.Hand.Count,
                Hand = viewer == p.Index ? p.Hand.ToArray() : Array.Empty<string>(), Discard = p.Discard.ToArray(), Removed = p.Removed.ToArray(), Revealed = p.Revealed.ToArray(),
                Field = p.Field.Select(s => new SpiritSnapshot { InstanceId = s.InstanceId, Owner = s.Owner, CardId = s.Card.id, NormalCores = s.Cores.Normal,
                    Soul = s.Cores.Soul, Exhausted = s.Exhausted, HeavyExhausted = s.HeavyExhausted, Level = s.Level, Bp = s.Bp }).ToArray() }).ToArray();
            if (State.PendingChoice != null && State.PendingChoice.Owner == viewer)
            {
                var c = State.PendingChoice; view.PendingChoice = new PendingChoice { Id = c.Id, Owner = c.Owner, Prompt = c.Prompt, Min = c.Min, Max = c.Max, Ordered = c.Ordered,
                    Options = c.Options.Select(o => new ChoiceOption { key = o.key, label = o.label, cardId = o.cardId, instanceId = o.instanceId, index = o.index }).ToArray() };
            }
            return view;
        }
        /// <summary>Legal deterministic action candidates. Core counts can also be supplied explicitly through the command API.</summary>
        public IReadOnlyList<BattleCommand> GetLegalCommands(int actor)
        {
            var result = new List<BattleCommand>(); if (actor != DecisionPlayerIndex || actor < 0 || actor > 1) return result;
            var p = State.Players[actor];
            if (State.PendingChoice != null)
            {
                var c = State.PendingChoice; var chosen = new List<int>(); var used = new HashSet<int>();
                Action<int> expand = null;
                expand = start =>
                {
                    if (result.Count >= 512) return;
                    if (chosen.Count >= c.Min) result.Add(new BattleCommand { action = "choose", selection = chosen.ToArray() });
                    if (chosen.Count == c.Max) return;
                    for (int i = c.Ordered ? 0 : start; i < c.Options.Length && result.Count < 512; i++)
                    { if (!used.Add(i)) continue; chosen.Add(i); expand(i + 1); chosen.RemoveAt(chosen.Count - 1); used.Remove(i); }
                };
                expand(0); return result;
            }
            if (State.Phase == MatchPhase.Opening) return new[] { new BattleCommand { action = "opening" }, new BattleCommand { action = "opening", redraw = true } };
            if (IsMainPhase(State.Phase) || IsFlashPhase(State.Phase))
            {
                for (int i = 0; i < p.Hand.Count; i++)
                {
                    var card = GetCard(p.Hand[i]);
                    if (!card.IsPlayableFor(RulesFormat) || !card.IsMagic && !IsMainPhase(State.Phase)) continue;
                    var variants = new List<string[]> { null };
                    if (AbilityRegistry.HasSuccession(card.id))
                    {
                        var ex = p.Discard.Select((id, n) => new { id, n }).Where(x => EffectExSymbols(actor, x.id, x.n) > 0).Select(x => x.id).ToArray();
                        foreach (var id in ex.Distinct()) variants.Add(new[] { id });
                        if (ex.Length > 1) variants.Add(ex.Take(Math.Min(ex.Length, card.cost)).ToArray());
                    }
                    foreach (var succession in variants)
                    {
                        int cost = GetCost(actor, card, succession, out _, out var err); int minimum = card.IsMagic ? 0 : card.levels[0].cores;
                        if (cost < 0 || AvailableCores(p) < (long)cost + minimum) continue;
                        var candidate = new BattleCommand { action = card.IsMagic ? "magic" : "summon", index = i, succession = succession };
                        if (card.IsMagic && !EffectCanPlayMagic(actor, card, candidate, out _)) continue;
                        result.Add(candidate);
                        // The owner can choose every payment pool and the cores placed on a summoned card.
                        // Represent this as a sequence of choices instead of exponentially many command variants.
                        var manual = CopyCommand(candidate); manual.chooseCores = true; result.Add(manual);
                        if (card.IsMagic && IsMainPhase(State.Phase) && AbilityRegistry.HasMainMagic(card.id)) result.Add(new BattleCommand { action = "magic", index = i, succession = succession, mode = 1 });
                        if (card.IsMagic && IsMainPhase(State.Phase) && AbilityRegistry.HasMainMagic(card.id)) result.Add(new BattleCommand { action = "magic", index = i, succession = succession, mode = 1, chooseCores = true });
                        // A normal payment and an explicit Soul payment are distinct effect contexts.
                        if (cost > 0)
                        {
                            var soulPayment = SoulPayment(actor, cost);
                            if (soulPayment != null)
                            {
                                result.Add(new BattleCommand { action = candidate.action, index = i, succession = succession, payment = soulPayment });
                                if (card.IsMagic && IsMainPhase(State.Phase) && AbilityRegistry.HasMainMagic(card.id)) result.Add(new BattleCommand { action = "magic", index = i, succession = succession, payment = soulPayment, mode = 1 });
                            }
                        }
                    }
                    if (AbilityRegistry.HasSuccession(card.id) && CanFinishSuccession(actor, card, new List<int>()))
                    {
                        result.Add(new BattleCommand { action = card.IsMagic ? "magic" : "summon", index = i, chooseSuccession = true, chooseCores = true });
                        if (card.IsMagic && IsMainPhase(State.Phase) && AbilityRegistry.HasMainMagic(card.id)) result.Add(new BattleCommand { action = "magic", index = i, mode = 1, chooseSuccession = true, chooseCores = true });
                    }
                    if (card.IsMagic && AbilityRegistry.SoulMagicColor(card.id, out var magicColor) && HasReductionSymbol(actor, magicColor) && HasAvailableSoul(actor))
                    {
                        var candidate = new BattleCommand { action = "magic", index = i, alternateCost = true };
                        if (EffectCanPlayMagic(actor, card, candidate, out _)) result.Add(candidate);
                    }
                }
                foreach (var s in p.Field)
                {
                    var command = new BattleCommand { action = "activate", source = s.InstanceId };
                    if (EffectCanActivate(actor, command)) result.Add(command);
                }
            }
            if (IsMainPhase(State.Phase))
            {
                var sources = new[] { -1 }.Concat(p.Field.Select(s => s.InstanceId)).ToArray();
                foreach (int from in sources) foreach (int to in sources)
                {
                    if (from == to) continue; var pool = Pool(actor, from);
                    if (pool.Normal > 0) result.Add(new BattleCommand { action = "transfer", source = from, target = to });
                    if (pool.Normal > 1) result.Add(new BattleCommand { action = "transfer", source = from, target = to, mode = pool.Normal });
                    if (pool.Soul) result.Add(new BattleCommand { action = "transfer", source = from, target = to, soul = true });
                    if (pool.Soul && Pool(actor, to).Normal > 0) result.Add(new BattleCommand { action = "swap", source = from, target = to, soul = true });
                }
            }
            if (State.Phase == MatchPhase.Attack)
                foreach (var s in p.Field.Where(s => s.Card.IsSpirit && !s.Exhausted && !s.HeavyExhausted && EffectCanAttack(s))) result.Add(new BattleCommand { action = "attack", source = s.InstanceId });
            if (State.Phase == MatchPhase.Block)
            {
                foreach (var s in p.Field.Where(s => s.Card.IsSpirit && !s.Exhausted && !s.HeavyExhausted)) result.Add(new BattleCommand { action = "block", source = s.InstanceId });
                var attacker = FindSpirit(State.PendingAttackerId);
                if (attacker == null || !EffectMustBlock(attacker) || result.Count == 0) result.Add(new BattleCommand { action = "take-life" });
            }
            else if (IsFlashPhase(State.Phase)) result.Add(new BattleCommand { action = "pass" });
            else result.Add(new BattleCommand { action = "advance" });
            return result;
        }
        private bool HasAvailableSoul(int owner) => State.Players[owner].Reserve.Soul || State.Players[owner].Field.Any(s => s.Cores.Soul);
        private bool HasReductionSymbol(int owner, CardColor color) => State.Players[owner].Field.Any(s => s.Level > 0 && (s.Card.symbolDetails ?? Array.Empty<CardSymbol>()).Any(v => string.Equals(v.color, color.ToString(), StringComparison.OrdinalIgnoreCase) && v.count > 0));
        private CoreTransfer[] SoulPayment(int owner, int cost)
        {
            if (cost < 1 || !HasAvailableSoul(owner)) return null;
            int source = State.Players[owner].Reserve.Soul ? -1 : State.Players[owner].Field.First(s => s.Cores.Soul).InstanceId;
            var basePayment = DefaultTransfers(owner, cost - 1); if (basePayment == null || basePayment.Any(t => t.soul)) return null;
            var item = basePayment.FirstOrDefault(t => t.source == source);
            if (item == null) return basePayment.Concat(new[] { new CoreTransfer { source = source, soul = true } }).ToArray();
            item.soul = true; return basePayment;
        }
    }
}
