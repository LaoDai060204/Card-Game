# Repository instructions

## Backups required by the owner

Before each new batch of edits or updates, preserve the current working version
on a uniquely named `backup/YYYY-MM-DD-description` branch and push that branch
to `origin`. Verify the remote branch points to the saved commit before changing
project files. Report the backup branch to the user. Never overwrite an existing
backup or force-push history as part of an ordinary update.

If the working tree already contains changes, preserve those changes before
starting. Check relevant ignored runtime assets as well: `Picture`, `Sound`, and
`Platforms` are ignored even though their imported files are tracked. Do not
commit generated Unity caches such as `Library`, `Temp`, or `Logs`.

The owner currently requires testing/review before publishing new project changes.
Do not push new implementation commits until the owner explicitly requests it.
Pushing a backup of the previous saved version remains authorized.

## Project conventions

- Use Unity **6000.0.24f1** and the Windows x64 target for the imported assets.
- The game starts at `Assets/Scenes/Boot.unity`.
- Application code lives in `Assets/Scripts/CardGame` and uses the `CardGame` namespace.
- Use canonical English UI keys in code. Preserve localized values, the legacy
  key compatibility table, protocol identifiers, and asset GUIDs.
- Vietnamese UI translations belong in `Data/locales/vi-VN`. Keep the CSV
  explicit `key` column synchronized with the locale dictionaries.
- Preserve accurate upstream attribution and bundled third-party license notices
  when customizing names, documentation, or the interface.
- A verified Git transfer or static localization check is not a Unity Play/build
  test. Report which checks actually ran.
