# Deep English migration

The `mdpro3-english` branch keeps MDPro3 1.4.7 and Unity **6000.0.24f1**, Windows
x64. This change translates the application and its references, without resuming
the Battle Spirits conversion. Baseline: `e9ba63fa6`; verified remote backup:
`backup/2026-11-14-before-deep-english-migration`.

## Changes

- **752 canonical English lookup aliases**, with no key collisions. Chinese
  lookup keys in code and all ten `translation.conf` files become English.
  Every original locale value, row order, duplicate and formatting control is
  preserved. The translation CSV gains an explicit `key` column; all eleven
  original language columns are retained.
- Chinese comments, regions, logs, Editor commands, tooltips and authored source
  text are translated. The source audit covers **122 changed C# files** in three
  C# 9 profiles and checks decoded string literals, not just raw text. Namespaces
  and component class names remain stable. The unused decompiled enum member is
  renamed to `Unused`, preserving its serialized integer value **2**.
- **988 text fields across 91 prefab/scene/resource files** use English text.
  This includes static lookup labels, sample player/card text, filters and TMP
  text stored with YAML Unicode escapes. GUIDs, fileIDs, hierarchy, method names
  and UnityEvent targets are preserved. No tracked filename or directory name
  contained Han characters, so there was no speculative file/folder rename.
- English news uses one coherent `en-US` feed for count, captions, images and
  links. An absent English feed stays hidden. English patch notes are registered
  as `UpdateContent_en` and selected by the English interface.
- Four GB18030 shader sources, missed by a UTF-8-only search, become UTF-8 with
  English comments. All **11 changed shader/compute files** retain identical
  executable tokens. The shader-inspection Python tool is translated too.

## Compatibility and remaining original-language data

This is not a claim that every byte in a multilingual resource repository is
English. The owner's optional data-scope question was not answered; the migration
preserves original translated/card data rather than inventing replacement data.
The reviewed exceptions are recorded in
[`english-migration-manifest.json`](Tools/Translations/english-migration-manifest.json):

- `Data/locales/legacy-ui-keys.json` maps externally supplied old lookup keys to
  English canonical keys. `InterString` normalizes both requests and old/downloaded
  translation files, so original upstream files still work. JSON escaping is only
  the representation of this explicit compatibility table; source strings were
  actually translated, not merely escaped.
- `Data/locales/legacy-text-contracts.json` retains exact CDB grammar and original
  downloaded puzzle filename prefixes behind English semantic keys. Translating
  those input tokens would break parsing. The same rule applies to the Chinese
  race suffix. `LegacyTextContracts` loads this data once; keep both JSON adapters
  in `Data/locales` when copying external runtime files into a Windows build.
- Official card databases, card/character identities, voice/dialogue data, banlist
  data, original locale values and third-party resources keep their original
  meanings and bytes. English preferences select the English datasets. This does
  not translate arbitrary future server messages or text baked into images.
- Chinese About/Update documents remain as original locale resources, alongside
  English counterparts. Japanese audio-event labels, font data, vendor encodings
  and binary assets are not application UI identifiers and are retained.
- Ten prefab creator/contributor text values and names on the original English
  attribution page keep their original identities and URLs. Sample player
  names become `Player`; actual attribution is not replaced with an invented name.

The UI/card/prerelease language choices remain independent. Missing translations
fall back to English; `[?]`, `@n`, `@ui`, locale IDs and original protocol values
remain compatible. Existing `No Shuffle` duplicate rows retain the original
first-match behavior, including the differing Italian/Portuguese values.

## Update and run

Close Unity and preserve local edits, then update `mdpro3-english` with a normal
fast-forward pull. Open **Assets/Scenes/Boot.unity** in Unity **6000.0.24f1**.
Use Addressables **Use Asset Database** to test the changed source prefabs rather
than a cached older Addressables build. Rebuild Addressables content before
shipping changed assets; the imported external bundles have not been rebuilt.

Check English menus, settings, deck filters, Solo/Online/Puzzle labels, patch
notes and news. Check several Editor commands and validation menu entries. If
using another original locale, check both canonical and legacy translations;
unknown external data must remain distinguishable from application labels.

## Validation limits

The production-source harness compiles Config, Language, InterString,
LegacyTextContracts and NewsManager with controlled Unity/HTTP stubs. Its twelve
scenarios exercise all ten locale tables (over 7,000 canonical/legacy lookups),
independent language targets, reinitialization, fallback, old downloaded files,
placeholders, reverse lookup, grammar and coherent/empty English news.

The source audit checks syntax for **122 changed C# files × three C# 9 profiles**,
including decoded string literals and namespace preservation. This is not Unity
API compilation. The resource validator compares serialized structure, GUIDs,
fileIDs, original locale values, CSV language columns and protected resource data
against the saved baseline. Shader-token and Python-AST checks preserve behavior.

**Unity Editor Play, shader compilation/rendering, device input, live news-server
content and Windows player builds have not been verified here.** No FPS or visual
quality claims are derived from these checks. The old gameplay/Puzzle/AI issues
of the restored baseline are not addressed by this translation-only migration.
