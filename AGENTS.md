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

## Battle Spirits conversion branch

- This branch retains the MDPro3 menu, appearance assets and Windows bundles,
  and targets **Unity 6000.0.24f1**, as selected by the owner.
- Gameplay is **Japanese Battle Spirits Eternal**, with Vietnamese UI. Do not
  silently substitute Saga or Standard rules, or imply that a Standard-only
  catalog entry is legal in Eternal.
- New rules live in `Assets/BattleSpirits/Runtime/Core`, without Unity objects.
  Keep C# 9 and Unity's .NET Standard 2.1 API compatibility.
- Network clients receive filtered player views. Never send the opponent's hand,
  deck order or random seed in an active match snapshot.
- Imported official card text is catalog data, not an implementation of an
  ability. Mark a card playable only when its rules behavior is implemented and
  tested. Preserve original IDs, Japanese text, sources and artwork checksums.
- Preserve the working Yu-Gi-Oh project on `main`; publish conversion changes on
  `battle-spirits-mdpro3-ui` until the converted game has been reviewed.
- Keep official card provenance and existing third-party notices accurate.
