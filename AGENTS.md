# Repository instructions

## Backups required by the owner

Before every new batch of edits or updates, preserve the current working version
on a uniquely named `backup/YYYY-MM-DD-description` branch and push it to origin.
Verify the remote commit before changing project files and report the backup.
Preserve existing changes first, including relevant ignored runtime assets in
`Picture`, `Sound`, and `Platforms`. Never overwrite backups or force-push history.
Do not commit generated Unity caches, local configuration, decks or credentials.

## Restored MDPro3 English baseline

- Current baseline: original MDPro3 1.4.7 from `47c3adb6e`, before the Vietnamese
  interface and LaoDai branding were introduced. Work on `mdpro3-english`.
- Target Unity **6000.0.24f1**, Windows x64. Start at `Assets/Scenes/Boot.unity`.
- English is the default UI, card and prerelease language. Keep other original
  language choices and upstream Chinese translation keys/protocol identifiers.
- Preserve the compatibility fixes removing the unsupported URP editor reference
  and disabling compression on nine UI textures. See `RESTORE_ENGLISH.md`.
- The owner will request optimization and a fresh Battle Spirits conversion
  later. Do not resume the previous conversion without that instruction.
- Keep `main` and `battle-spirits-mdpro3-ui` intact while this baseline is reviewed.
- Preserve upstream attribution, copyright notices, asset GUIDs and third-party
  licenses. This project is an MDPro3 customization, not the official Master Duel.
- State exactly which checks ran. Static/C# smoke checks are not Unity Play/build.
