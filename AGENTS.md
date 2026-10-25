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

## Project conventions

- Use Unity **6000.0.24f1** and the Windows x64 target for the imported assets.
- The game starts at `Assets/Scenes/Boot.unity`.
- Vietnamese UI translations belong in `Data/locales/vi-VN`. Keep Chinese
  translation keys, protocol identifiers, namespaces, and asset GUIDs intact.
- Preserve accurate upstream attribution and bundled third-party license notices
  when customizing names, documentation, or the interface.
- A verified Git transfer or static localization check is not a Unity Play/build
  test. Report which checks actually ran.
