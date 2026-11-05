using System;
using System.Collections.Generic;
using System.Linq;

namespace LaoDai.BattleSpirits.Core
{
    /// <summary>
    /// A deterministic, Unity-independent Japanese Battle Spirits rules engine. The
    /// explicit AbilityRegistry is the boundary for reviewed card implementations.
    /// Official rule references are recorded in Documentation/RULES_AND_SCOPE.md.
    /// </summary>
    public sealed class BattleMatch
    {
        private readonly Dictionary<string, CardDefinition> cards;
        private readonly Random random;
        private readonly int firstPlayer;
        private int openingChoices;
        private int nextSpiritId = 1;
        private int consecutiveFlashPasses;
        private readonly int[] originalDeckSizes = new int[2];

        public MatchState State { get; }
        public BattleRulesFormat RulesFormat { get; }
        public List<BattleEvent> LastEvents { get; } = new List<BattleEvent>();
        public List<string> Log { get; } = new List<string>();
        public event Action<BattleEvent> EventEmitted;

        public int DecisionPlayerIndex
        {
            get
            {
                switch (State.Phase)
                {
                    case MatchPhase.Opening: return openingChoices == 0 ? firstPlayer : 1 - firstPlayer;
                    case MatchPhase.FlashBeforeBlock:
                    case MatchPhase.FlashAfterBlock: return State.FlashPlayerIndex;
                    case MatchPhase.Block: return 1 - State.ActivePlayerIndex;
                    default: return State.ActivePlayerIndex;
                }
            }
        }

        private BattleMatch(IReadOnlyList<CardDefinition> definitions,
            IReadOnlyList<string> deck0, IReadOnlyList<string> deck1,
            int seed, int firstPlayerIndex, bool validateDecks, BattleRulesFormat format)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (format != BattleRulesFormat.Eternal && format != BattleRulesFormat.Standard)
                throw new ArgumentOutOfRangeException(nameof(format));
            RulesFormat = format;
            if (firstPlayerIndex < 0 || firstPlayerIndex > 1)
                throw new ArgumentOutOfRangeException(nameof(firstPlayerIndex));
            cards = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                ValidateDefinition(definition);
                if (cards.ContainsKey(definition.id))
                    throw new ArgumentException("Card IDs must be unique: " + definition.id);
                cards.Add(definition.id, definition);
            }

            random = new Random(seed);
            firstPlayer = firstPlayerIndex;
            State = new MatchState
            {
                Format = format.ToString(),
                ActivePlayerIndex = firstPlayer,
                Players = new[]
                {
                    new PlayerState { Index = 0, Name = "Người chơi 1" },
                    new PlayerState { Index = 1, Name = "Người chơi 2" }
                }
            };
            var decks = new[] { deck0, deck1 };
            for (int player = 0; player < 2; player++)
            {
                ValidateDeck(decks[player], validateDecks);
                State.Players[player].Deck.AddRange(decks[player]);
                originalDeckSizes[player] = decks[player].Count;
                Shuffle(State.Players[player].Deck);
                for (int i = 0; i < 4; i++) Draw(player);
            }
            AddLog((format == BattleRulesFormat.Standard ? "Standard v1.0 · Block A (2026/27)" : "Eternal v17.1")
                + " · Chỉ sử dụng thẻ có hồ sơ luật đã kiểm chứng.");
            AddLog("Chọn giữ hoặc đổi toàn bộ 4 lá bài mở đầu, mỗi người một lần.");
        }

        public static BattleMatch CreateDefault(IReadOnlyList<CardDefinition> cards, int seed = 2026, int firstPlayer = 0,
            BattleRulesFormat format = BattleRulesFormat.Eternal)
        {
            var decks = StarterDecks.CreatePair(cards, format);
            return Create(cards, decks[0].cards, decks[1].cards, seed, firstPlayer, format: format);
        }

        public static BattleMatch Create(IReadOnlyList<CardDefinition> cards,
            IReadOnlyList<string> deck0, IReadOnlyList<string> deck1,
            int seed = 2026, int firstPlayer = 0, bool validateDecks = true,
            BattleRulesFormat format = BattleRulesFormat.Eternal)
        {
            return new BattleMatch(cards, deck0, deck1, seed, firstPlayer, validateDecks, format);
        }

        public CardDefinition GetCard(string id)
        {
            if (id == null || !cards.TryGetValue(id, out var card))
                throw new ArgumentException("Không có dữ liệu cho thẻ: " + id);
            return card;
        }

        public SpiritState FindSpirit(int instanceId)
        {
            foreach (var player in State.Players)
                foreach (var spirit in player.Field)
                    if (spirit.InstanceId == instanceId) return spirit;
            return null;
        }

        public int GetSummonCost(int player, CardDefinition card)
        {
            if (player < 0 || player > 1) throw new ArgumentOutOfRangeException(nameof(player));
            if (card == null) throw new ArgumentNullException(nameof(card));
            long symbols = 0;
            foreach (var spirit in State.Players[player].Field)
                if (spirit.Card.color == card.color) symbols += spirit.Card.symbols;
            return Math.Max(0, card.cost - (int)Math.Min(symbols, card.reductionSymbols));
        }

        public bool TryConfirmOpening(int player, bool redraw, out string error)
        {
            BeginAction();
            if (State.Phase != MatchPhase.Opening || player != DecisionPlayerIndex)
                return Fail("Chưa đến lượt bạn chọn bài mở đầu.", out error);
            var owner = State.Players[player];
            if (redraw)
            {
                owner.Deck.AddRange(owner.Hand);
                owner.Hand.Clear();
                Shuffle(owner.Deck);
                for (int i = 0; i < 4; i++) Draw(player);
                owner.MulliganUsed = true;
                AddLog(owner.Name + " đổi toàn bộ bài mở đầu.");
            }
            else AddLog(owner.Name + " giữ bài mở đầu.");
            openingChoices++;
            if (openingChoices == 2) StartTurn(firstPlayer);
            return Success(out error);
        }

        public bool TrySummon(int player, int handIndex, out string error)
        {
            BeginAction();
            if (!RequireMainTurn(player, out error)) return false;
            var owner = State.Players[player];
            if (handIndex < 0 || handIndex >= owner.Hand.Count)
                return Fail("Lá bài đã chọn không còn trên tay.", out error);
            var card = GetCard(owner.Hand[handIndex]);
            if (!IsSupportedVanilla(card) || (!card.IsSpirit && !card.IsNexus))
                return Fail("Chỉ triệu hồi Spirit hoặc bố trí Nexus đã hỗ trợ; Magic dùng lệnh kích hoạt.", out error);
            int cost = GetSummonCost(player, card);
            int required = card.levels[0].cores;
            int usableCores = owner.Reserve.Total + owner.Field.Sum(s => s.Cores.Total);
            if ((long)usableCores < (long)cost + required)
                return Fail("Cần " + cost + " core trả phí và " + required +
                    " core duy trì Lv1 từ Reserve hoặc các Spirit trên sân.", out error);

            // Reduction is fixed BEFORE payment. A symbol-providing Spirit can
            // contribute its last core to this payment without losing that
            // already-calculated discount. Moving its core earlier is different.
            // Prefer spare cores so the default UI avoids unnecessary vanish.
            int payment = cost;
            int fromReserve = Math.Min(payment, Math.Max(0, owner.Reserve.Total - required));
            Transfer(owner.Reserve, owner.Trash, fromReserve);
            payment -= fromReserve;
            payment = TakeFieldCores(owner, owner.Trash, payment, true);
            payment = TakeFieldCores(owner, owner.Trash, payment, false);
            if (payment > 0) Transfer(owner.Reserve, owner.Trash, payment);
            var spirit = new SpiritState { InstanceId = nextSpiritId++, Card = card };
            int allocation = Math.Min(required, owner.Reserve.Total);
            Transfer(owner.Reserve, spirit.Cores, allocation);
            allocation = TakeFieldCores(owner, spirit.Cores, required - allocation, true);
            allocation = TakeFieldCores(owner, spirit.Cores, allocation, false);
            if (allocation > 0) Transfer(owner.Reserve, spirit.Cores, allocation);
            owner.Hand.RemoveAt(handIndex);
            owner.Field.Add(spirit);
            AddLog(owner.Name + " triệu hồi " + card.DisplayName + " (phí " + cost + ").");
            Emit(card.IsNexus ? "NexusPlaced" : "Summoned", player, spirit.InstanceId, -1, card.id, cost);
            return Success(out error);
        }

        public bool TryPlayMagic(int player, int handIndex, int targetId, CoreTransfer[] transfers, out string error)
        {
            BeginAction();
            if (!RequireAbilityWindow(player, out error)) return false;
            var owner = State.Players[player];
            if (handIndex < 0 || handIndex >= owner.Hand.Count)
                return Fail("Lá Magic không còn trên tay.", out error);
            var card = GetCard(owner.Hand[handIndex]);
            if (!card.IsMagic || !card.IsPlayableFor(RulesFormat))
                return Fail("Hiệu ứng Magic chưa được triển khai.", out error);
            bool flashWindow = IsFlashPhase(State.Phase);
            var target = targetId < 0 ? null : FindSpirit(targetId);
            if (targetId >= 0 && (target == null || !target.Card.IsSpirit))
                return Fail("Mục tiêu phải là một Spirit đang trên sân.", out error);
            if (card.id == AbilityRegistry.DoubleDraw)
            {
                if (targetId < 0 && flashWindow)
                    return Fail("Flash của Double Draw cần chọn Spirit nhận BP; rút 2 lá chỉ dùng trong Main.", out error);
                if (transfers != null && transfers.Length > 0)
                    return Fail("Double Draw không chuyển core.", out error);
            }
            else if (card.id == AbilityRegistry.AwakenMagic)
            {
                if (target != null && !owner.Field.Contains(target))
                    return Fail("Awaken chỉ chuyển core tới Spirit của mình.", out error);
                if (transfers != null && (transfers.Length > 1 || transfers.Any(t => t == null || t.source != -1 || t.normal < 0 || (long)t.normal + (t.soul ? 1 : 0) > 3)))
                    return Fail("Awaken chỉ chuyển tối đa 3 core từ Reserve.", out error);
            }
            else return Fail("Magic chưa có hồ sơ hiệu ứng.", out error);
            int cost = GetSummonCost(player, card);
            if (AvailableCores(owner) < cost)
                return Fail("Không đủ core trả phí Magic.", out error);
            var afterPayment = PreviewReserveAfterPayment(owner, cost);
            CoreTransfer chosen = null;
            if (card.id == AbilityRegistry.AwakenMagic)
            {
                chosen = transfers == null
                    ? new CoreTransfer { normal = target == null ? 0 : Math.Min(3, afterPayment.Normal) }
                    : transfers.Length == 0 ? new CoreTransfer() : transfers[0];
                // Official BS01-115 FAQ Q24490/Q24491 permits choosing zero
                // cores and resolving only the draw. Without a Spirit, skip the
                // transfer per Eternal manual page14 Q2's partial-resolution rule.
                if (target == null && (chosen.normal > 0 || chosen.soul))
                    return Fail("Awaken cần chọn Spirit của mình khi chuyển core.", out error);
                if (chosen.normal > afterPayment.Normal || chosen.soul && !afterPayment.Soul)
                    return Fail("Core được chọn không còn ở Reserve sau khi trả phí Magic.", out error);
            }

            // Every rejecting path is above the mutation boundary. A target can
            // leave the field while paying the cost; its effect then has no
            // target to change, but the paid Magic still resolves/discards.
            PayMagicCost(owner, cost);
            owner.Hand.RemoveAt(handIndex);
            owner.Discard.Add(card.id);
            Emit("MagicPlayed", player, -1, targetId, card.id, cost);
            AddLog(owner.Name + " sử dụng " + card.DisplayName + " (phí " + cost + ").");
            bool targetRemains = target != null && FindSpirit(targetId) == target;
            if (card.id == AbilityRegistry.DoubleDraw)
            {
                if (targetId < 0) { Draw(player); Draw(player); }
                else if (targetRemains)
                {
                    target.TurnBpBonus = (int)Math.Min(int.MaxValue, (long)target.TurnBpBonus + 2000);
                    Emit("BpChanged", player, targetId, -1, card.id, 2000);
                }
            }
            else
            {
                if (targetRemains)
                {
                    for (int i=0;i<chosen.normal;i++) MoveOne(owner.Reserve, target.Cores, false);
                    if (chosen.soul) MoveOne(owner.Reserve, target.Cores, true);
                    Emit("CoreMoved", player, -1, targetId, card.id, chosen.normal + (chosen.soul ? 1 : 0));
                }
                Draw(player);
            }
            CompleteAbility(player, flashWindow);
            return Success(out error);
        }

        public bool TryAwaken(int player, int sourceId, int targetId, int ordinaryCount, bool soul,
            CoreTransfer[] transfers, out string error)
        {
            BeginAction();
            if (!RequireAbilityWindow(player, out error)) return false;
            var owner = State.Players[player];
            var target = owner.Field.Find(s => s.InstanceId == targetId);
            if (target == null || !AbilityRegistry.HasAwaken(target.Card))
                return Fail("Mục tiêu phải là Spirit có kỹ năng Giác tỉnh đã hỗ trợ của mình.", out error);
            if (transfers == null)
                transfers = new[] { new CoreTransfer { source = sourceId, normal = ordinaryCount == 0 && !soul ? 1 : ordinaryCount, soul = soul } };
            if (transfers.Length == 0 || transfers.Length > owner.Field.Count)
                return Fail("Chọn các Spirit cấp core cho Giác tỉnh.", out error);
            var used = new HashSet<int>();
            var sources = new List<SpiritState>();
            foreach (var transfer in transfers)
            {
                if (transfer == null || transfer.normal < 0 || !used.Add(transfer.source))
                    return Fail("Nguồn core Giác tỉnh không hợp lệ hoặc bị trùng.", out error);
                var source = owner.Field.Find(s => s.InstanceId == transfer.source);
                if (source == null || !source.Card.IsSpirit || source == target
                    || transfer.normal > source.Cores.Normal || transfer.soul && !source.Cores.Soul
                    || (long)transfer.normal + (transfer.soul ? 1 : 0) == 0)
                    return Fail("Giác tỉnh chỉ lấy core hiện có trên các Spirit khác của mình.", out error);
                sources.Add(source);
            }
            bool flashWindow = IsFlashPhase(State.Phase);
            // All selected sources move in this ONE Flash activation, before
            // checking vanish. The opponent cannot interrupt between sources.
            for (int i=0;i<transfers.Length;i++)
            {
                var transfer = transfers[i]; var source = sources[i];
                for (int n=0;n<transfer.normal;n++) MoveOne(source.Cores, target.Cores, false);
                if (transfer.soul) MoveOne(source.Cores, target.Cores, true);
                Emit("CoreMoved", player, source.InstanceId, targetId, target.Card.id, transfer.normal + (transfer.soul ? 1 : 0));
            }
            foreach (var source in sources)
                if (source.Level == 0) RemoveSpirit(player, source, "Vanished");
            Emit("Awakened", player, targetId, -1, target.Card.id);
            AddLog(target.Card.DisplayName + " Giác tỉnh: chuyển core từ các Spirit của mình.");
            CompleteAbility(player, flashWindow);
            return Success(out error);
        }

        private bool RequireAbilityWindow(int player, out string error)
        {
            if (player < 0 || player > 1 || !((IsMainPhase(State.Phase) && player == State.ActivePlayerIndex)
                || (IsFlashPhase(State.Phase) && player == State.FlashPlayerIndex)))
                return Fail("Hiệu ứng chỉ được sử dụng trong Main của mình hoặc khi có quyền Flash.", out error);
            error = string.Empty;
            return true;
        }

        private static bool IsFlashPhase(MatchPhase phase) => phase == MatchPhase.FlashBeforeBlock || phase == MatchPhase.FlashAfterBlock;
        private bool IsMainPhase(MatchPhase phase) => phase == MatchPhase.Main
            || RulesFormat == BattleRulesFormat.Standard && phase == MatchPhase.SecondMain;
        private static bool IsAttackPhase(MatchPhase phase) => phase == MatchPhase.Attack || phase == MatchPhase.Block || IsFlashPhase(phase);
        private static long AvailableCores(PlayerState owner) => owner.Reserve.Total + owner.Field.Sum(s => (long)s.Cores.Total);

        private void CompleteAbility(int player, bool flashWindow)
        {
            // An attacker or blocker leaving the field does not close Flash.
            // Finish the remaining priority window before ending that battle.
            // Official Eternal FAQ Q1015/Q1125/Q1197 explicitly retains Flash
            // when the attacking Spirit is no longer present.
            if (flashWindow)
            {
                consecutiveFlashPasses = 0;
                State.FlashPlayerIndex = 1 - player;
            }
        }

        private void PayMagicCost(PlayerState owner, int cost)
        {
            int fromReserve = Math.Min(cost, owner.Reserve.Total);
            Transfer(owner.Reserve, owner.Trash, fromReserve);
            int remaining = TakeFieldCores(owner, owner.Trash, cost - fromReserve, true);
            remaining = TakeFieldCores(owner, owner.Trash, remaining, false);
            if (remaining != 0) throw new InvalidOperationException("Validated Magic payment was not available.");
        }

        private static CorePool PreviewReserveAfterPayment(PlayerState owner, int cost)
        {
            var reserve = new CorePool { Normal = owner.Reserve.Normal, Soul = owner.Reserve.Soul };
            var paid = new CorePool();
            int initial = Math.Min(cost, reserve.Total);
            Transfer(reserve, paid, initial);
            int remaining = cost - initial;
            var pools = owner.Field.Select(s => new CorePool { Normal=s.Cores.Normal, Soul=s.Cores.Soul }).ToArray();
            var vanished = new bool[pools.Length];
            for (int pass=0;pass<2;pass++)
                for (int i=0;i<pools.Length && remaining>0;i++)
                {
                    if (vanished[i]) continue;
                    int minimum = owner.Field[i].Card.levels[0].cores;
                    int available = pass == 0 ? Math.Max(0,pools[i].Total - minimum) : pools[i].Total;
                    int take = Math.Min(remaining,available);
                    Transfer(pools[i],paid,take); remaining-=take;
                    if (pools[i].Total < minimum)
                    { Transfer(pools[i],reserve,pools[i].Total); vanished[i]=true; }
                }
            return reserve;
        }

        public bool TryMoveCore(int player, int fromSpiritId, int toSpiritId, bool soul, out string error)
        {
            BeginAction();
            if (!RequireMainTurn(player, out error)) return false;
            if (fromSpiritId == toSpiritId)
                return Fail("Chọn hai vị trí core khác nhau.", out error);
            var owner = State.Players[player];
            var sourceSpirit = owner.Field.Find(s => s.InstanceId == fromSpiritId);
            var targetSpirit = owner.Field.Find(s => s.InstanceId == toSpiritId);
            if ((fromSpiritId != -1 && sourceSpirit == null) || (toSpiritId != -1 && targetSpirit == null))
                return Fail("Chỉ được chuyển core giữa Reserve và các Spirit của mình.", out error);
            var source = sourceSpirit == null ? owner.Reserve : sourceSpirit.Cores;
            var target = targetSpirit == null ? owner.Reserve : targetSpirit.Cores;
            if (soul ? !source.Soul : source.Normal == 0)
                return Fail(soul ? "Vị trí này không có Soul Core." : "Vị trí này không có core thường.", out error);

            MoveOne(source, target, soul);
            Emit("CoreMoved", player, fromSpiritId, toSpiritId, string.Empty, 1);
            if (sourceSpirit != null && sourceSpirit.Level == 0)
                RemoveSpirit(player, sourceSpirit, "Vanished");
            return Success(out error);
        }

        // Main-step allocation can exchange a Soul Core and an ordinary core
        // atomically. Moving either core first would incorrectly vanish a
        // one-core Spirit; the official Eternal Main-step FAQ permits the swap.
        public bool TrySwapCore(int player, int fromSpiritId, int toSpiritId, bool sourceSoul, out string error)
        {
            BeginAction();
            if (!RequireMainTurn(player, out error)) return false;
            if (fromSpiritId == toSpiritId)
                return Fail("Chọn hai vị trí core khác nhau.", out error);
            var owner = State.Players[player];
            var from = owner.Field.Find(spirit => spirit.InstanceId == fromSpiritId);
            var to = owner.Field.Find(spirit => spirit.InstanceId == toSpiritId);
            if ((fromSpiritId != -1 && from == null) || (toSpiritId != -1 && to == null))
                return Fail("Chỉ đổi core giữa các vị trí của mình.", out error);
            var source = from == null ? owner.Reserve : from.Cores;
            var target = to == null ? owner.Reserve : to.Cores;
            if (sourceSoul ? !source.Soul || target.Soul || target.Normal == 0
                : source.Normal == 0 || source.Soul || !target.Soul)
                return Fail("Chọn Soul Core ở một bên và core thường ở bên còn lại.", out error);

            var soulPool = sourceSoul ? source : target;
            var ordinaryPool = sourceSoul ? target : source;
            soulPool.Soul = false;
            soulPool.Normal++;
            ordinaryPool.Normal--;
            ordinaryPool.Soul = true;
            Emit("CoreSwapped", player, fromSpiritId, toSpiritId, string.Empty, 1);
            AddLog(owner.Name + " đổi Soul Core và core thường đồng thời.");
            return Success(out error);
        }

        public bool TryBeginAttackStep(int player, out string error)
        {
            BeginAction();
            if (!RequireTurn(player, MatchPhase.Main, out error)) return false;
            if (State.TurnNumber == 1)
                return Fail("Người đi trước bỏ qua Attack Step ở lượt đầu.", out error);
            EnterAttackStep(player);
            return Success(out error);
        }

        private void EnterAttackStep(int player)
        {
            State.Phase = MatchPhase.Attack;
            var defender = State.Players[1 - player];
            if (defender.Trash.Total > 0 && defender.Field.Any(n => n.Card.id == AbilityRegistry.SilverWorld && n.Level > 0))
            {
                int recycled = defender.Trash.Total;
                Transfer(defender.Trash, defender.Reserve, recycled);
                Emit("CoresRecycled", 1 - player, -1, -1, AbilityRegistry.SilverWorld, recycled);
                AddLog("侵食されゆく銀世界: lấy core từ Trash về Reserve ở đầu Attack Step của đối phương.");
            }
            AddLog(State.Players[player].Name + " vào Attack Step.");
        }

        // Standard comprehensive rules 7-8: after Attack, repeat the allowed
        // Main actions once. Eternal has no Second Main, and Turn1 skips it.
        public bool TryBeginSecondMain(int player, out string error)
        {
            BeginAction();
            if (RulesFormat != BattleRulesFormat.Standard || State.TurnNumber == 1)
                return Fail("Second Main chỉ có trong Standard sau lượt đầu của người đi trước.", out error);
            if (!RequireTurn(player, MatchPhase.Attack, out error)) return false;
            EnterSecondMain(player);
            return Success(out error);
        }

        private void EnterSecondMain(int player)
        {
            ClearBattle();
            State.Phase = MatchPhase.SecondMain;
            AddLog(State.Players[player].Name + " vào Second Main Step.");
            Emit("SecondMainStarted", player);
        }

        public bool TryAttack(int player, int spiritId, out string error)
        {
            BeginAction();
            if (!RequireTurn(player, MatchPhase.Attack, out error)) return false;
            var attacker = State.Players[player].Field.Find(s => s.InstanceId == spiritId);
            if (attacker == null || !attacker.Card.IsSpirit || attacker.Exhausted)
                return Fail("Chọn một Spirit đang hồi phục của mình để tấn công.", out error);
            attacker.Exhausted = true;
            State.PendingAttackerId = spiritId;
            State.PendingBlockerId = -1;
            BeginFlash(MatchPhase.FlashBeforeBlock);
            AddLog(attacker.Card.DisplayName + " tấn công; bên phòng thủ có quyền Flash trước.");
            Emit("Attacked", player, spiritId, -1, attacker.Card.id);
            return Success(out error);
        }

        public bool TryPassFlash(int player, out string error)
        {
            BeginAction();
            if ((State.Phase != MatchPhase.FlashBeforeBlock && State.Phase != MatchPhase.FlashAfterBlock)
                || player != State.FlashPlayerIndex)
                return Fail("Chưa đến lượt bạn bỏ qua Flash.", out error);
            consecutiveFlashPasses++;
            AddLog(State.Players[player].Name + " bỏ qua Flash.");
            if (consecutiveFlashPasses == 2)
            {
                State.FlashPlayerIndex = -1;
                if (FindSpirit(State.PendingAttackerId) == null) EndBattle();
                else if (State.Phase == MatchPhase.FlashBeforeBlock) State.Phase = MatchPhase.Block;
                else ResolveBlockedBattle();
            }
            else State.FlashPlayerIndex = 1 - player;
            return Success(out error);
        }

        public bool TryBlock(int player, int spiritId, out string error)
        {
            BeginAction();
            if (State.Phase != MatchPhase.Block || player != 1 - State.ActivePlayerIndex)
                return Fail("Chưa đến bước chọn chặn.", out error);
            var blocker = State.Players[player].Field.Find(s => s.InstanceId == spiritId);
            if (blocker == null || !blocker.Card.IsSpirit || blocker.Exhausted)
                return Fail("Chỉ Spirit đang hồi phục của bên phòng thủ mới chặn được.", out error);
            blocker.Exhausted = true;
            if (blocker.Card.id == AbilityRegistry.Valkyrius)
            {
                blocker.BattleBpBonus = 6000;
                Emit("BpChanged", player, spiritId, -1, blocker.Card.id, 6000);
            }
            State.PendingBlockerId = spiritId;
            BeginFlash(MatchPhase.FlashAfterBlock);
            AddLog(blocker.Card.DisplayName + " chặn; bắt đầu Flash sau khi chặn.");
            Emit("Blocked", player, spiritId, State.PendingAttackerId, blocker.Card.id);
            return Success(out error);
        }

        public bool TryTakeLife(int player, out string error)
        {
            BeginAction();
            if (State.Phase != MatchPhase.Block || player != 1 - State.ActivePlayerIndex)
                return Fail("Chưa đến bước quyết định nhận sát thương.", out error);
            var attacker = FindSpirit(State.PendingAttackerId);
            if (attacker == null) return Fail("Spirit tấn công không còn trên sân.", out error);
            var defender = State.Players[player];
            int damage = Math.Min(defender.Life, attacker.Card.symbols);
            defender.Life -= damage;
            defender.Reserve.Normal += damage;
            AddLog(defender.Name + " nhận " + damage + " sát thương; Life chuyển thành core Reserve.");
            Emit("LifeLost", player, attacker.InstanceId, -1, attacker.Card.id, damage);
            if (defender.Life == 0) Win(State.ActivePlayerIndex, "Life của đối phương về 0.");
            else EndBattle();
            return Success(out error);
        }

        public bool TryEndTurn(int player, out string error)
        {
            BeginAction();
            if (player != State.ActivePlayerIndex || (State.Phase != MatchPhase.Main && State.Phase != MatchPhase.Attack
                && !(RulesFormat == BattleRulesFormat.Standard && State.Phase == MatchPhase.SecondMain)))
                return Fail("Chỉ kết thúc lượt sau khi xử lý xong trận đánh hiện tại.", out error);
            if (RulesFormat == BattleRulesFormat.Standard && State.TurnNumber > 1 && State.Phase != MatchPhase.SecondMain)
            {
                // Passing Main also passes through Attack's start triggers;
                // never skip them when the UI requests an early end of turn.
                if (State.Phase == MatchPhase.Main) EnterAttackStep(player);
                EnterSecondMain(player);
                return Success(out error);
            }
            AddLog(State.Players[player].Name + " kết thúc lượt.");
            StartTurn(1 - player);
            return Success(out error);
        }

        public bool TryConcede(int player, out string error)
        {
            BeginAction();
            if (player < 0 || player > 1)
                return Fail("Người chơi không hợp lệ.", out error);
            if (State.Phase == MatchPhase.GameOver)
                return Fail("Trận đấu đã kết thúc.", out error);
            Win(1 - player, "Đối phương đầu hàng.");
            return Success(out error);
        }

        /// <summary>Independent invariants for tests and development builds.</summary>
        public void AssertInvariants()
        {
            for (int player = 0; player < 2; player++)
            {
                var owner = State.Players[player];
                int total = owner.Life + owner.Reserve.Total + owner.Trash.Total;
                int souls = (owner.Reserve.Soul ? 1 : 0) + (owner.Trash.Soul ? 1 : 0);
                CheckPool(owner.Reserve);
                CheckPool(owner.Trash);
                var ids = new HashSet<int>();
                foreach (var spirit in owner.Field)
                {
                    CheckPool(spirit.Cores);
                    if (!ids.Add(spirit.InstanceId) || spirit.Level == 0 || (!spirit.Card.IsSpirit && !spirit.Card.IsNexus))
                        throw new InvalidOperationException("Invalid or vanished Spirit on field.");
                    total += spirit.Cores.Total;
                    souls += spirit.Cores.Soul ? 1 : 0;
                }
                if (souls != 1 || total != 9 + owner.CoreGains || owner.Life < 0)
                    throw new InvalidOperationException("Core conservation or Soul Core uniqueness violated.");
                if (owner.Deck.Count + owner.Hand.Count + owner.Field.Count + owner.Discard.Count != originalDeckSizes[player])
                    throw new InvalidOperationException("Card conservation violated.");
            }
        }

        private void StartTurn(int player)
        {
            State.ActivePlayerIndex = player;
            State.TurnNumber++;
            foreach (var ownerToReset in State.Players)
                foreach (var permanent in ownerToReset.Field) permanent.TurnBpBonus = 0;
            ClearBattle();
            var owner = State.Players[player];
            AddLog("Lượt " + State.TurnNumber + " · " + owner.Name + " · Start Step.");
            if (owner.Deck.Count == 0)
            {
                Win(1 - player, "Đối phương không còn bài trong Deck ở Start Step.");
                return;
            }
            if (State.TurnNumber != 1)
            {
                owner.Reserve.Normal++;
                owner.CoreGains++;
                AddLog("Core Step: nhận 1 core từ Void.");
            }
            else AddLog("Lượt đầu của người đi trước: bỏ qua Core Step, Attack Step"
                + (RulesFormat == BattleRulesFormat.Standard ? " và Second Main Step." : "."));
            Draw(player);
            AddLog("Draw Step: rút 1 lá.");
            Transfer(owner.Trash, owner.Reserve, owner.Trash.Total);
            foreach (var spirit in owner.Field) spirit.Exhausted = false;
            State.Phase = MatchPhase.Main;
            AddLog("Refresh Step: hồi phục Spirit và lấy core từ Trash; vào Main Step.");
            Emit("TurnStarted", player, -1, -1, string.Empty, State.TurnNumber);
        }

        private void BeginFlash(MatchPhase phase)
        {
            State.Phase = phase;
            State.FlashPlayerIndex = 1 - State.ActivePlayerIndex;
            consecutiveFlashPasses = 0;
        }

        private void ResolveBlockedBattle()
        {
            var attacker = FindSpirit(State.PendingAttackerId);
            var blocker = FindSpirit(State.PendingBlockerId);
            // A declared block still prevents Life damage if either participant
            // left during Flash. No BP comparison is possible in that case.
            if (attacker == null || blocker == null) { EndBattle(); return; }
            int attackBp = attacker.Bp;
            int blockBp = blocker.Bp;
            AddLog("So sánh BP: " + attackBp + " / " + blockBp + ".");
            if (attackBp <= blockBp) RemoveSpirit(State.ActivePlayerIndex, attacker, "Destroyed");
            if (blockBp <= attackBp) RemoveSpirit(1 - State.ActivePlayerIndex, blocker, "Destroyed");
            EndBattle();
        }

        private void RemoveSpirit(int player, SpiritState spirit, string reason)
        {
            var owner = State.Players[player];
            int levelAtDestruction = spirit.Level;
            int nexusTriggers = reason == "Destroyed" && spirit.Card.IsSpirit && player != State.ActivePlayerIndex
                && IsAttackPhase(State.Phase)
                ? owner.Field.Count(n => n.Card.id == AbilityRegistry.SilverWorld && n.Level >= 2) : 0;
            owner.Field.Remove(spirit);
            owner.Discard.Add(spirit.Card.id);
            Transfer(spirit.Cores, owner.Reserve, spirit.Cores.Total);
            AddLog(spirit.Card.DisplayName + (reason == "Vanished" ? " tiêu biến vì không đủ core Lv1." : " bị phá hủy; core về Reserve."));
            Emit(reason, player, spirit.InstanceId, -1, spirit.Card.id);
            if (reason == "Destroyed" && spirit.Card.id == AbilityRegistry.Siegfried && levelAtDestruction >= 3)
            {
                owner.Life++; owner.CoreGains++;
                Emit("LifeGained", player, spirit.InstanceId, -1, spirit.Card.id, 1);
                AddLog("龍皇ジークフリード Lv3: thêm 1 core từ Void vào Life khi bị phá hủy.");
            }
            if (nexusTriggers > 0)
            {
                owner.Reserve.Normal += nexusTriggers; owner.CoreGains += nexusTriggers;
                Emit("CoreGained", player, spirit.InstanceId, -1, AbilityRegistry.SilverWorld, nexusTriggers);
            }
        }

        private void EndBattle()
        {
            ClearBattle();
            State.Phase = MatchPhase.Attack;
        }

        private void ClearBattle()
        {
            foreach (var player in State.Players)
                foreach (var permanent in player.Field) permanent.BattleBpBonus = 0;
            State.PendingAttackerId = -1;
            State.PendingBlockerId = -1;
            State.FlashPlayerIndex = -1;
            consecutiveFlashPasses = 0;
        }

        private void Win(int player, string reason)
        {
            State.WinnerIndex = player;
            State.Phase = MatchPhase.GameOver;
            ClearBattle();
            AddLog(State.Players[player].Name + " thắng. " + reason);
            Emit("GameWon", player);
        }

        private bool RequireTurn(int player, MatchPhase phase, out string error)
        {
            if (player < 0 || player > 1 || State.ActivePlayerIndex != player || State.Phase != phase)
                return Fail("Hành động này chỉ thực hiện được ở đúng bước và lượt của mình.", out error);
            error = string.Empty;
            return true;
        }

        private bool RequireMainTurn(int player, out string error)
        {
            if (player < 0 || player > 1 || State.ActivePlayerIndex != player || !IsMainPhase(State.Phase))
                return Fail("Hành động này chỉ thực hiện được trong Main Step của mình.", out error);
            error = string.Empty;
            return true;
        }

        private void Draw(int player)
        {
            var owner = State.Players[player];
            if (owner.Deck.Count == 0) return;
            owner.Hand.Add(owner.Deck[owner.Deck.Count - 1]);
            owner.Deck.RemoveAt(owner.Deck.Count - 1);
        }

        private void Shuffle(List<string> deck)
        {
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                string temporary = deck[i];
                deck[i] = deck[other];
                deck[other] = temporary;
            }
        }

        private static void Transfer(CorePool source, CorePool target, int amount)
        {
            if (amount < 0 || source.Total < amount)
                throw new InvalidOperationException("Invalid core transfer.");
            int normal = Math.Min(amount, source.Normal);
            source.Normal -= normal;
            target.Normal += normal;
            if (amount > normal) MoveOne(source, target, true);
        }

        private int TakeFieldCores(PlayerState owner, CorePool target, int amount, bool spareOnly)
        {
            if (amount == 0) return 0;
            foreach (var source in owner.Field.ToArray())
            {
                int available = spareOnly
                    ? Math.Max(0, source.Cores.Total - source.Card.levels[0].cores)
                    : source.Cores.Total;
                int taken = Math.Min(amount, available);
                if (taken == 0) continue;
                Transfer(source.Cores, target, taken);
                amount -= taken;
                if (source.Level == 0) RemoveSpirit(owner.Index, source, "Vanished");
                if (amount == 0) break;
            }
            return amount;
        }

        private static void MoveOne(CorePool source, CorePool target, bool soul)
        {
            if (soul)
            {
                if (!source.Soul || target.Soul) throw new InvalidOperationException("Invalid Soul Core transfer.");
                source.Soul = false;
                target.Soul = true;
            }
            else
            {
                if (source.Normal <= 0) throw new InvalidOperationException("No ordinary core to transfer.");
                source.Normal--;
                target.Normal++;
            }
        }

        private void ValidateDeck(IReadOnlyList<string> deck, bool validateSizeAndCopies)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            if (deck.Count < 4 || (validateSizeAndCopies && deck.Count < 40))
                throw new ArgumentException("Bộ bài " + RulesFormat + " cần ít nhất 40 lá.");
            if (validateSizeAndCopies)
            {
                var validation = DeckValidator.Validate(cards.Values.ToArray(), deck, format: RulesFormat);
                if (!validation.IsValid)
                    throw new ArgumentException(string.Join("\n", validation.Errors));
                return;
            }
            // Small rule fixtures can bypass size/copy restrictions, never
            // the boundary that rejects unsupported card implementations.
            foreach (string id in deck)
                if (!IsSupportedVanilla(GetCard(id)))
                    throw new ArgumentException("Bộ bài chứa thẻ chưa hỗ trợ: " + id);
        }

        private static void ValidateDefinition(CardDefinition card)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.id) || string.IsNullOrWhiteSpace(card.nameJa))
                throw new ArgumentException("Card ID and original Japanese name are required.");
            // Browse-only catalog entries are not Spirit rules definitions.
            // Deck validation separately requires a fully supported card.
        }

        private bool IsSupportedVanilla(CardDefinition card)
        {
            return card != null && card.IsPlayableFor(RulesFormat);
        }

        private static void CheckPool(CorePool pool)
        {
            if (pool == null || pool.Normal < 0) throw new InvalidOperationException("Invalid core pool.");
        }

        private void BeginAction() => LastEvents.Clear();

        private bool Success(out string error)
        {
            error = string.Empty;
            AssertInvariants();
            return true;
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }

        private void AddLog(string message)
        {
            Log.Add(message);
            if (Log.Count > 180) Log.RemoveAt(0);
        }

        private void Emit(string kind, int actor, int spiritId = -1, int targetId = -1,
            string cardId = "", int amount = 0)
        {
            var battleEvent = new BattleEvent
            {
                Kind = kind, Actor = actor, SpiritId = spiritId,
                TargetId = targetId, CardId = cardId, Amount = amount
            };
            LastEvents.Add(battleEvent);
            EventEmitted?.Invoke(battleEvent);
        }
    }
}
