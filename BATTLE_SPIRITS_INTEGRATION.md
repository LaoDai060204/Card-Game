# Direct Battle Spirits integration

The owner's final instruction is to retain the established interface layout and
replace the previous game's content, rules and branding. Boot and Program now
enter Battle Spirits directly. The old duel protocol, CDB/banlist startup,
WindBot and room engine are not initialized by active native entry points.
Retained interface types and serialized references preserve existing layouts
and GUIDs; they are not an alternate gameplay mode.

## Runtime

`Assets/BattleSpirits/Runtime` contains the pure C# rules core, legal-command AI,
puzzle runner, catalog, device deck storage, native adapters, session/replay/LAN
transport, and Unity presentation. `BattleIntegration` is the sole game launch
bridge. It validates a candidate session before replacing a current one.
`BattleRuntimeDriver` ticks the active session; leaving the duel closes it.

The original duel prefab is instantiated privately. Its old controller and
persistent gameplay callbacks are disconnected before the Battle Spirits
controller binds the original HUD, inspector and action-dialog structures.
The field renders into its own texture/camera layer. No scene or existing prefab
hierarchy is rewritten.

Solo uses the four shipped Standard recipes and the native legal-command AI.
Only filtered snapshots and legal commands reach the bot. Puzzle moves pass
through the puzzle wrapper so goals, failure conditions and reset state stay
consistent. Ten source puzzle fixtures are copied byte-for-byte into the player.

Online is a new Battle Spirits LAN host/client protocol. The host validates
catalog/rules versions, deck legality and the connection's assigned seat, then
publishes viewer-filtered snapshots. It is not compatible with old game servers.
Completed-match replays use `.bsreplay.json`, validated command resimulation and
isolated local storage. No old replay or deck directory is consumed.

## Cards and presentation

The shipped catalog has 62 explicit profiles and four 40-card example decks.
`Data/BattleSpirits/implemented-cards.json` governs availability; images alone
never mark an effect implemented. Persistent numeric IDs do not use alternate-art
arithmetic. Printed symbols, Standard block/legality and reference hashes are
recorded separately from effect code.

English printed faces are the 61 reviewed unofficial images. Pisketona retains
its supplied original printed face with English inspection text. This is a
limited reviewed roster, not the entire Battle Spirits card database.

The presentation includes 43 alpha paper silhouettes on flat printed card bases.
These are 2.5D standees, not rigged 3D creature meshes. Ritenia is the only card
explicitly assigned the boss cutscene; ordinary summons do not infer boss status
from cost or rarity. Cutscenes support skip and reduced motion. Three selected
field appearances provide subdued mirrored art beneath readable native zones.
Wallpaper, avatar and protector widgets use the supplied Battle Spirits assets.
Unsupported cosmetic libraries remain empty.

Sound effects use small original synthesized UI tones. Optional music/voice
resources must live under `Resources/BattleSpirits/Audio`; absent resources are
silent. No legacy soundtrack or character voice is loaded as a fallback.

## Checks

Use Python 3 and .NET SDK 10 for the source harnesses. Set `DOTNET_ROOT` if the SDK
is not at `/tmp/codex-dotnet`. The game code remains C# 9 compatible.

```sh
python3 Tools/Tests/BattleSpiritsCore/run.py
python3 Tools/Tests/BattleSpiritsCompatibility/run.py
python3 Tools/Tests/BattleSpiritsEffects/run.py
python3 Tools/Tests/BattleSpiritsSoloPuzzles/run.py
python3 Tools/Tests/BattleSpiritsSession/run.py
python3 Tools/Tests/BattleSpiritsDuelUI/run.py
python3 Tools/Tests/BattleSpiritsPresentation/run.py
python3 Tools/Tests/BattleSpiritsPresentation/validate_assets.py
python3 Tools/Tests/BattleSpiritsPresentation/settings_smoke.py
python3 Tools/Tests/BattleSpiritsCosmetics/run.py
python3 Tools/Tests/BattleSpiritsAudio/run.py
python3 Tools/BattleSpiritsImport/validate_catalog.py
python3 Tools/BattleSpiritsImport/run_smoke.py
python3 Tools/BattleSpiritsNativeTests/audit_api.py
python3 Tools/Tests/BattleSpiritsIntegrationAudit/run.py
python3 Tools/Translations/validate_english.py
```

The native deck and English compatibility harness instructions are in their
respective READMEs. `Data/BattleSpirits/integration-verification.json` records the
final source check results and limits. Compiler output stays outside Unity
Assets and is not published.

The compatibility runner separately compiles Core, AI, Puzzles, backend and
native adapters against .NET Standard 2.1 reference assemblies. This checks the
real assembly boundaries and target BCL without .NET 10 library references.

The cloud workspace has no Unity Editor. These checks compile selected actual
production sources against narrow Unity/native API doubles and test real rules,
session, network-loopback, catalog and command behavior. Static entry audits
cover named native routes, not arbitrary reflection or asset-bundle contents.
They do not prove Unity assembly compilation, Play Mode, pixel rendering, shader
compilation, controller navigation or a Windows build. Those require the target
Unity Editor with the retained interface assets.
