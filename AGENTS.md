# Repository instructions

## Backups required by the owner

Before every new batch of edits or updates, preserve the current working version
on a uniquely named `backup/YYYY-MM-DD-description` branch and push it to origin.
Verify the remote commit before changing project files and report the backup.
Preserve existing changes first, including relevant ignored runtime assets in
`Picture`, `Sound`, and `Platforms`. Never overwrite backups or force-push history.
Do not commit generated Unity caches, local configuration, decks or credentials.

## Battle Spirits Standard on the recovered English branch

- The owner selected Battle Spirits Standard on `fix/mdpro3-english-repair-20261117`.
  The original recovered Yu-Gi-Oh! state is saved on
  `backup/2026-11-27-before-standard-ui-conversion` at `cb251af31`.
- Target Unity **6000.0.24f1**, Windows x64. Start at `Assets/Scenes/Boot.unity`.
- English is the default UI, card and prerelease language. Application code and
  translation lookup keys use canonical English. Keep original locale values,
  protocol IDs and the explicitly documented legacy input adapters in
  `Data/locales/legacy-ui-keys.json` and `legacy-text-contracts.json`.
- After localization/ref changes, run `Tools/Translations/validate_english.py`
  and the production-source harness described in `Tools/Translations/README.md`.
  Preserve Unity GUIDs/fileIDs, original card data and exact creator credits.
- Preserve the compatibility fixes removing the unsupported URP editor reference
  and disabling compression on nine UI textures. See `RESTORE_ENGLISH.md`.
- Use the existing Battle Spirits rules, legal commands and card catalog.
  See `BATTLE_SPIRITS_INTEGRATION.md` for source checks and supported features.
- Keep changes scoped to the selected branch. The owner runs Unity and the game.
- Preserve upstream attribution, copyright notices, asset GUIDs and third-party
  licenses. This project is an MDPro3 customization, not the official Master Duel.
- State exactly which checks ran. Static/C# smoke checks are not Unity Play/build.
