Run `python3 Tools/Tests/BattleSpiritsEffects/run.py` with .NET 10 at
`/tmp/codex-dotnet`, or set `DOTNET_ROOT`. The harness compiles the shipped
`Runtime/Core/*.cs`; it loads the shipped catalog without changing card flags.

`Program.cs` checks the outcomes, target restrictions, level conditions, core
ownership, continuations, ordered returns, once-per-turn limits and simultaneous
effects of all 62 explicit profiles. `PublicCommandChecks.cs` independently
enters through `GetLegalCommands` and `BattleCommandDispatcher.TryExecute`:

| Public route | Cases |
| --- | ---: |
| Every card from Hand, in Main and Second Main | 124 |
| Every Spirit attacking | 43 |
| Every Spirit blocking after an actual attack and Flash passes | 43 |
| Every Magic in both Flash windows, plus both Main windows where printed | 40 |
| Every Soul Magic alternate payment | 10 |
| Every activated ability in both permitted windows | 8 |
| Every printed Succession removing an actual matching EX card | 14 |

Fixtures arrange cards and cores before dispatch. They do not call effect
handlers to activate the public-route cases. The older outcome tests call
internal effect hooks to isolate printed clauses; the public matrix verifies
that those handlers can also be reached by production commands. This is not an
exhaustive enumeration of card interactions, historical FAQs, or Unity UI input.

One printed interaction cannot occur with this 62-card pool: Para-DX's optional
F Change cost of 5 has an isolated handler test, but the only shipped F Change
source, Maximum Gamer, requires cost 7 and "Ex-Aid" in the replacement's name.
Para-DX does not have that name. The command tests verify that Maximum Gamer
correctly excludes it; they do not invent a compatible F Change source.

The printed-clause review uses the shipped English wording and the archived
official Japanese detail pages in `Data/BattleSpirits/provenance/official-details`.
On 2026-11-16, parsing all 62 archived detail pages with the importer's
`parse_official` produced exact matches for all 62 shipped `effectTextJa` values.
The profile checks cover these clauses:

| Card ID(s) | Printed behavior checked |
| --- | --- |
| 26RBS01-003, 26RBS01-019, 26RBS02-002, 26RCB01-040, 26RCB01-044, 26RSD02-005, 26RSD03-005 | No printed effects; printed EX symbol remains usable |
| 26RBS01-024 | Paid Dark Purple revival; destroy exhausted cost 3 or less |
| 26RBS01-126 | Succession; cost 5 destruction and conditional cost 3 continuation |
| 26RBS01-X03 | Succession; Clash; Lv2 refresh after a low-BP block |
| 26RBS01-X04 | Reveal/discard purple; ordered remainder; activate Spirit Manipulation |
| 26RBS01-X06 | Succession; remove up to three cores; extra attack Life loss after depletion |
| 26RBS02-033 | Exhaust/heavy exhaust; opponent-owned End choice |
| 26RBS02-034 | Succession; Void core to self; Lv2 destruction exhaust |
| 26RBS02-087 | Soul Magic; exhausted-count BP; granted Life-loss Nexus destruction |
| 26RBS02-X09 | Succession; exhaust/refresh; once-per-turn heavy exhaust |
| 26RBS03-004 | Summon destruction; Soul-paid higher BP limit |
| 26RBS03-009 | Lv2 destruction of its cost-limited blocker |
| 26RBS03-010 | Summon draw |
| 26RBS03-012 | Core-count target restriction; leave one; opponent core choice |
| 26RBS03-014 | Destruction draw; Dark Purple attack core removal with one-core floor |
| 26RBS03-022 | Purple Trash reduction; core removal followed by Reserve-dependent draw |
| 26RBS03-087 | Soul Magic; combined-BP groups; increased limit after Life reduction |
| 26RBS03-089 | Soul Magic; exact core count; two targets after Life reduction |
| 26RBS03-091 | Soul Magic; exhaust and opposing Attack Step continuation |
| 26RBS03-097 | Soul Magic; cost limit changes after Life reduction |
| 26RBS03-X01 | Succession family scaling; End Trash-core recovery |
| 26RBS03-X03 | Succession; Soul Awakening; name-wide once-per-turn battle-end Life loss |
| 26RBS03-X04 | Optional reveal; Spirit discard; Dark Purple continuation; physical Trash-copy EX conversion |
| 26RBS03-X05 | Succession; free revival; normal-core removal when blocking |
| 26RBS03-X06 | Succession; three-owner core removal; one Void core after depletion; leave one normal core |
| 26RCB01-039, 26RCB01-043 | First attack/block Level Up and battle duration |
| 26RCB01-041 | Reveal; different-name EX-AID add; ordered remainder |
| 26RCB01-046 | Other EX-AID attack Life loss; unstackable once-per-turn draw/return |
| 26RCB01-047 | Level Up before the Lv2 draw/ordered return clause |
| 26RCB01-051 | Attack/block support; exhaust-self condition; two destructions |
| 26RCB01-052 | Required cost-7 different-name Ex-Aid F Change; Lv2 destruction |
| 26RCB01-060 | Reveal/return up to two EX-AID cards; equal draw; End refresh |
| 26RCB01-071 | Soul Magic; all-EX-AID restriction; permanent-count cost scaling |
| 26RCB01-X06 | Succession; conditional F Change cost; Level Up; cost-5 destruction |
| 26RCB01-X07 | Succession; conditional opponent-Magic protection; exhaust-to-refresh |
| 26RSD01-012 | Main Nexus restriction and core recovery; Flash BP |
| 26RSD01-014 | Soul Magic; BP limit changes after Life reduction |
| 26RSD02-001 | Lv2 destruction normal-core removal |
| 26RSD02-014 | Soul Magic; two-core removal; opponent normal/Soul selection |
| 26RSD02-X01 | Succession; leave-one summon removal; normal-core attack removal and depletion draw |
| 26RSD03-002 | Void core into Trash |
| 26RSD03-003 | Optional cost-limited exhaustion; Lv2 attack BP |
| 26RSD03-010 | Attacking Armored Fish BP; Main reveal/return/draw |
| 26RSD03-012 | Main ordered whole-Hand return and conditional draw; Flash BP |
| 26RSD03-013 | Exhaust opposing Spirit |
| 26RSD03-014 | Soul Magic; heavy exhaustion |
| 26RSD03-X02 | Succession; Armored Fish summon BP; own-Attack-Step Flash refresh |
| 26RSD06-012 | Main draw/ordered return; Flash BP |
| 26RSD06-013 | Soul Magic; optional own-Nexus destruction increases target cost limit |
| 26RSD09-001 | Soul Core required to attack |
| 26RSD09-004 | Attack BP; Lv2 draw only with at most five Hand cards |
| 26RSD09-011 | Attacking red BP; Lv2 destruction target BP limit |
| 26RSD09-012 | Succession; Main draw; Flash BP |
| 26RSD10-003 | Choice of Void-core destination |
| 26RSD10-004 | Lv2 attack BP |

The concrete rules defect fixed in profile `standard-effects-20261116-r2` was
Level Up preserving Lv2 after every physical core was removed. The official
[Standard manual](https://asia-en.battlespirits.com/pdf/official-rule-manual_standard.pdf?ver.1.0),
page 24 of the PDF retrieved on 2026-11-16, states that a Spirit unable to maintain
its minimum level is Depleted immediately and distinguishes this from destruction.
The new regression removes the last core from all four Level Up cards, while
attacking and blocking, through Soul Bite and public target choices. It also
spends the attacking Spirit's last core on a Magic payment. It checks removal,
zero level/BP, Trash placement and Depletion bookkeeping. Compiling the same
regression with the previous Level getter fails on `26RCB01-039`.

The checked run passes 2,754 assertions. Core separately passes 116 checks;
Solo/Puzzles passes 3,698 assertions, ten puzzles and all sixteen completed AI
deck pairings. These are C# production-source checks. Unity Play Mode, Windows
player builds and rendered input remain separate checks.
