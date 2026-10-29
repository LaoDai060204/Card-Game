# Remaining Chinese display text

The 29 October 2026 update translates remaining user-facing Chinese text into English while retaining the existing Vietnamese interface.

- Translated 257 string occurrences in 12 Unity Editor tools: menu entries, window labels, dialogs, progress messages and Console output.
- Translated 48 runtime diagnostic, Inspector and generated configuration-description strings in 10 scripts.
- Added a complete English translation of the upstream release history, covering 21 versions from v1.4.8 through v1.3.0. Non-Chinese interfaces now select `UpdateContent_en`; Vietnamese continues to use `UpdateContent_vi`.
- Added 51 translations for the official Puzzle AI names and hints. Card names come from the bundled English database. Translation happens when displaying the received text, after consuming the original packet; all 104 official Lua files and their filenames remain unchanged.
- Corrected the MyCard news selector to use the English feed for non-Chinese interfaces. An unavailable English feed remains hidden. Vietnamese news remains hidden. Language/feed changes discard cached images and ignore late downloads from an earlier feed.
- Replaced three native-language names in the English language picker and two untranslated English item placeholders. The unknown item remains identified by its original ID rather than receiving an invented official name.
- Filled 15 missing English UI translations, including download, cloud Deck, collection-list and duel messages. The localization validator now checks English CSV/configuration parity and runtime-key coverage as well as Vietnamese.

Chinese localization keys, Chinese-language data, parsing tokens, contributor names and source comments are retained where they are functional identifiers or attribution. Serialized UI source labels are localization keys: all 507 audited Chinese `#Text` labels already have English translations, so their prefab names and source text were preserved.

## Validation

```bash
python Tools/Translations/validate_vietnamese.py
python Tools/Tests/DisplayLocalization.py --dotnet /path/to/dotnet
python Tools/Tests/NewsLocalization.py --dotnet /path/to/dotnet
python Tools/Tests/validate_network_metadata.py --dotnet /path/to/dotnet
python Tools/RuntimeData/validate_puzzles.py
```

The executable harnesses require .NET SDK 10 and compile production code as C# 9. Unity display, tweening and downloads are controlled stand-ins; they do not replace testing in the Unity Editor.

Passed: 823 English and 823 Vietnamese UI-key checks; 12 display-localization scenarios plus 102 Puzzle packet cases; 20 news scenarios; 46 network metadata checks; original hashes of all 104 official Puzzle files. All 28 changed production C# files passed syntax checks for Editor/Windows, Editor/Android and standalone Windows symbols. Literal-only edits also preserve non-string code tokens.

Unity **6000.0.24f1** remains the project version. Windows Unity Play, rendering and builds were not tested in this environment. In the Editor, check Settings → About → Update Content, a Puzzle preview's AI name/hint, the English news panel when a feed is available, and the translated tool menus.

Backup before this update: `backup/2026-10-29-before-remaining-chinese-ui` at `72acf52b678b55bfe2f6681a93ce1c46a16802a2`.
