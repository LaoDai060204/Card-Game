# Restored MDPro3 English baseline

This document records the initial restore at `e9ba63fa6`. The subsequent
[deep English migration](ENGLISH_MIGRATION.md) adds English source/lookup keys
and authored UI text while preserving the restored gameplay baseline. The
[CID mapping startup fix](Tools/RuntimeData/README.md) subsequently restores the
missing compact data separately; other historical gameplay fixes remain deferred.

Restored on 2026-11-14 from **`47c3adb6e1bc966356d2585aa67d86f5868cb7bd`**,
the snapshot immediately before the Vietnamese interface/branding commit.
The application is **MDPro3 1.4.7**, Unity **6000.0.24f1**, Windows x64.
Original source, menus, Yu-Gi-Oh rules, WindBot and imported Master Duel resources
come from that snapshot. This is MDPro3, not the official Master Duel client.

The Vietnamese locale, customized LaoDai branding and Battle Spirits conversion
are absent from this branch. English UI, card and prerelease languages are the
defaults. Existing `vi-VN` preferences migrate to `en-US`; other original language
choices, selected decks and unrelated settings remain. Original Chinese source
comments and localization lookup keys are intentionally retained.

Only these compatibility adjustments are applied over the historical snapshot:

- Remove the unsupported `UniversalRenderPipelineEditorAssets` managed reference
  `98082254862942208`, including its list entry and matching registry block.
- Keep `Compression = None` for the nine UI textures previously reported broken.
  Other importer settings, GUIDs and source image bytes are unchanged.
- Default/migrate the three language settings to English and load translations
  before the first Boot loading labels. A new config starts without preferences
  left in memory from a previous initialization.

No optimization or new Battle Spirits work is included. Later fixes for missing
card mapping data, Puzzle, AI startup/long bot names and the package registry are
not part of this historical baseline. A successful restore does not establish
that every original game mode works; those areas need review in the next task.

## Open and update

Close Unity before switching branches. Preserve local edits with a commit or
stash; ignored personal data needs its own backup. Then:

```bash
git fetch origin
git switch mdpro3-english
git pull --ff-only origin mdpro3-english
```

Open `Assets/Scenes/Boot.unity` in Unity 6000.0.24f1, Windows x64. Keep `Data`,
`Picture`, `Sound`, and `Platforms` beside `Assets`. When using source prefabs,
select Addressables **Use Asset Database**. If old Battle Spirits/Vietnamese
files were edited locally, stash them before switching so they do not remain
as untracked project content.

## Preservation and verification

The existing conversion remains at `6a7899527f2139dd0e552f7492d74b358e7bc493`
on `battle-spirits-mdpro3-ui`. Backup before this restore:
`backup/2026-11-14-before-mdpro3-english-restore`, verified on origin at that
same commit. `main` is preserved at `db9007d14536df540d8ccd8b9eb195981060d485`.

Eight smoke scenarios compiled the actual Config/Language C# 9 source with
controlled stubs: fresh defaults across OS languages, migration, saved original
choices, isolated reinitialization, missing keys, mixed preferences, persistence
and getter fallback. Three changed production files passed all three C# 9 syntax
profiles without errors. Source/asset checks verify the restored tree and narrowly
scoped compatibility changes. **Unity Editor Play, shader rendering and Windows
build have not been run in this environment.**
