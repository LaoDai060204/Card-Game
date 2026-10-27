# Runtime core API (C# 9 / .NET Standard2.1, no Unity dependency)

Namespace: `LaoDai.BattleSpirits.Core`.

`CardsDatabase` public JSON field `cards` (`CardDefinition[]`). Definition fields:
`id`, `nameJa`, `nameVi`, `color` (enum integer: Red0 Purple1 Green2 White3 Yellow4 Blue5),
`cost`, `reductionSymbols`, `symbols`, `levels` (`List<CardLevel>` with public fields `cores`, `bp`),
`effectTextJa`, `sourceUrl`, `imageUrl`, `implemented` (must true; nonvanilla text rejected).

`BattleMatch.CreateDefault(IReadOnlyList<CardDefinition> cards, int seed=2026, int firstPlayer=0)`
creates matching 40-card practice decks from at least14 distinct named definitions, max3copies/name; copies are distributed round-robin so all16 shipped definitions participate.
Opening hand has4cards. `State` is `MatchState` with public fields `Players` (`PlayerState[2]`),
`ActivePlayerIndex`, `TurnNumber`, `Phase` (`MatchPhase`), `WinnerIndex` (-1 until finish).
`DecisionPlayerIndex` query gives who must currentlyact.

`MatchPhase`: `Opening`, `Main`, `Attack`, `FlashBeforeBlock`, `Block`, `FlashAfterBlock`, `GameOver`.
Main includes automatic Start/Core/Draw/Refresh processing with events/logs; seven officialsteps not seven required clicks.

Player fields: `Index`, `Name`, `Hand` (`List<string>` cardIDs), `Deck`, `Field` (`List<SpiritState>`),
`Discard` (`List<string>`), `Reserve`, `Trash` (`CorePool`), `Life` (int), `CoreGains`.
Spirit fields: `InstanceId`, `Card`, `Cores`, `Exhausted`; computed properties `Level` and `Bp`.
CorePool fields `Normal`(int), `Soul`(bool), computed `Total`.

All actions returnbool and last argument `out string error` (empty on success):
- `TryConfirmOpening(int player, bool redraw, out string error)` once perplayer; firstplayer thenother.
- `TrySummon(int player, int handIndex, out string error)` calculates reduction before atomic Reserve+field→Trash payment, allocates minimumLv1 from remaining Reserve+field. Spare cores are preferred; donors below Lv1 vanish.
- `TryMoveCore(int player, int fromSpiritId, int toSpiritId, bool soul, out string error)` moves1core; `-1`meansReserve. Mainonly. FieldbelowLv1vanishes.
- `TryBeginAttackStep(int player, out string error)` firstplayerfirstturncannotattack.
- `TryAttack(int player, int spiritId, out string error)` readySpirit→exhaust; defenderhasFlashpriority.
- `TryPassFlash(int player, out string error)` alternating2passes; beforeBlock andafterBlockonly.
- `TryBlock(int defender, int spiritId, out string error)` readySpirit→exhaust andsecondFlash.
- `TryTakeLife(int defender, out string error)` unblocked resolvesdamageandreturnsAttackphase.
- `TryEndTurn(int player, out string error)` legal inMain/Attackwithnopendingbattle.

Queries: `GetSummonCost(int player, CardDefinition card)`, `GetCard(string id)`, `FindSpirit(int instanceId)`.
`LastEvents` list and `Log` list. `EventEmitted` event `Action<BattleEvent>`.
BattleEvent fields `Kind` (string), `Actor`, `SpiritId`, `TargetId`, `CardId`, `Amount`.
Kinds: `Summoned`, `Attacked`, `Blocked`, `Destroyed`, `Vanished`, `LifeLost`, `TurnStarted`, `GameWon`, `CoreMoved`.
Use `State.PendingAttackerId`, `State.PendingBlockerId` and `State.FlashPlayerIndex` forbattlecontrols.

Rules subset: current JP Eternal vanilla Spirits only. OneSoulCore accuratelytracked perplayer.
Refreshreturns ownTrash→Reserve, readies ownfield. Firstplayerfirstturn skips Core+Attack butdraws.
Deckout only at ownerStartStep. No Magic/Nexus/Brave/Contract/Burst/cardability support.
Hotseat, no network layer. Default payment is deterministic and automatic; explicit player-chosen payment plans are not yet exposed. Main core transfers permit rearrangement, but recalculated discounts reflect the current field.
