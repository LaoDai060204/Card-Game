# Repository instructions

## Backups required by the owner

Before every new batch of edits or updates, preserve the current working version
on a uniquely named `backup/YYYY-MM-DD-description` branch and push it to origin.
Verify the remote commit before changing project files and report the backup.
Preserve existing changes first, including relevant ignored runtime assets in
`Picture`, `Sound`, and `Platforms`. Never overwrite backups or force-push history.
Do not commit generated Unity caches, local configuration, decks or credentials.

## Battle Spirits application

- Work on `mdpro3-english`. The owner has explicitly authorized complete
  replacement of the previous game's content, rules, cards, effects, Solo,
  opponent AI and puzzles. Keep the established interface layout, with Battle
  Spirits content and branding. This is a direct Battle Spirits application;
  do not add a game-mode switch or a route back to the previous duel engine.
- Target Unity **6000.0.24f1**, Windows x64. Start at `Assets/Scenes/Boot.unity`.
- English is the default UI and card language. Application code and translation
  lookup keys use canonical English. Keep original locale values and the
  explicitly documented legacy text input adapters in
  `Data/locales/legacy-ui-keys.json` and `legacy-text-contracts.json`.
- After localization/ref changes, run `Tools/Translations/validate_english.py`
  and the production-source harness described in `Tools/Translations/README.md`.
  Preserve existing Unity GUIDs/fileIDs, archived source card data and exact
  creator credits. Use the tested explicit Battle Spirits catalog/ID map;
  never infer card effects, symbols or boss status from artwork, cost or rarity.
- Preserve the compatibility fixes removing the unsupported URP editor reference
  and disabling compression on nine UI textures. See `RESTORE_ENGLISH.md`.
- Native gameplay enters through `BattleIntegration` and `BattleGameSession`.
  Bootstrap must not initialize the previous CDB/banlist/game protocol, server
  or WindBot. AI uses the Battle Spirits legal-command dispatcher and filtered
  snapshots. Puzzle commands must pass through `PuzzleSession.TryExecute`.
- Keep `main`, `battle-spirits-mdpro3-ui`, and the reviewed artwork branch intact.
  Reuse reviewed assets selectively; never merge the obsolete conversion into
  this branch. Preserve the original prefab/scene layouts and replace their
  controllers/content on private runtime instances as necessary.
- Device decks/replays belong under `Application.persistentDataPath/BattleSpirits`.
  Do not overwrite or silently show legacy user decks, replays or configuration.
- Preserve upstream attribution, copyright notices, asset GUIDs and third-party
  licenses. Retained interface framework namespaces are compatibility details;
  they do not permit an active legacy gameplay path. This is an unofficial fan
  project, not an official Bandai application.
- State exactly which checks ran. Static/C# smoke checks are not Unity Play/build.
- Run the relevant production-source harnesses in `Tools/Tests/BattleSpirits*`,
  the native deck harness, catalog validator, and strict integration audit.
  See `BATTLE_SPIRITS_INTEGRATION.md` for architecture and verification scope.
