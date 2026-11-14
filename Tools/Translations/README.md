# English localization and compatibility keys

Runtime code and the first column of each locale `translation.conf` use canonical
English keys. `translations.csv` has an explicit `key` column followed by its
unchanged eleven locale columns; an old splitter that assumes column 0 is the
Simplified Chinese key must be updated to use `key`. Do not substitute translated
values for canonical keys or discard duplicate rows without reviewing behavior.

`Data/locales/legacy-ui-keys.json` adapts known upstream/old keys at the input
boundary. `legacy-text-contracts.json` contains exact external CDB/puzzle grammar,
identified by English semantic names. Keep both files beside the locale folders
when packaging external Data resources. Original translated values remain valid.

From the repository root, with Python 3 and PyYAML:

```bash
python Tools/Translations/validate_english.py
python Tools/Translations/validate_english.py --base e9ba63fa6
```

The second command needs the saved commit in local Git history and additionally
compares the exact locale migration, serialized structure and protected data.
A shallow clone may need that history fetched before running it.

The optional test harness requires .NET SDK 10 outside Unity; the game still uses
Unity 6000.0.24f1/.NET Standard-compatible C# 9. It compiles actual production
source with deliberately limited Unity/network stubs:

```bash
dotnet run --project Tools/Translations/tests/EnglishMigration.Tests.csproj -c Release -- /path/to/Card-Game
dotnet run --project Tools/Translations/tests/Audit/SourceAudit.csproj -c Release -- /path/to/Card-Game e9ba63fa6
```

The audit creates an `mdpro3-deep-english` directory in the operating system
temporary folder for its detailed findings. These tools do not
run Unity, compile shaders, test asset bundles, or contact live news services.
See [migration scope](../../ENGLISH_MIGRATION.md) for exact exceptions and limits.
