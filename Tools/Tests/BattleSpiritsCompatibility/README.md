Run from the repository root:

```sh
python3 Tools/Tests/BattleSpiritsCompatibility/run.py
```

This compiles production sources with C# 9 against **.NET Standard 2.1 reference assemblies**, rather than the .NET 10 references used by executable smoke tests. Roslyn runs through the installed SDK (`DOTNET_ROOT`, default `/tmp/codex-dotnet`); none of that SDK's .NET 10 BCL assemblies are compilation references.

The check compiles Core, AI and Puzzles as three separate libraries matching their Unity assembly definitions. AI and Puzzles reference the compiled Core library, which exposes internal-access errors that a single combined source compilation can hide. It then compiles the actual session, replay, LAN transport and catalog against these libraries, followed by every native adapter and the production native card/deck/configuration classes.

The first run downloads `NETStandard.Library.Ref 2.1.0` and `Newtonsoft.Json 13.0.3` from public NuGet and verifies pinned SHA256 hashes. The packages are cached outside the repository at `/tmp/bs-netstandard21` (`BS_COMPATIBILITY_CACHE` overrides it). To supply existing reference files instead, set `NETSTANDARD_REF_DIR` to the `ref/netstandard2.1` directory and `NEWTONSOFT_JSON_DLL` to a .NET Standard compatible Newtonsoft DLL. The Newtonsoft netstandard2.0 assembly can produce Roslyn's expected CS1701 reference-version warning when checked against netstandard2.1; JSON-populated fields can also produce CS0649 warnings.

The JSON report is written to `/tmp/battle-spirits-netstandard-compatibility.json` (`BS_COMPATIBILITY_REPORT` overrides it). Compiler outputs are temporary and are removed after completion.

Core, AI, Puzzles and backend use no Unity doubles. The native adapter compilation reuses the existing native harness doubles for unavailable Unity, TMP and UniTask types; actual BCL references remain .NET Standard 2.1. This is a compile compatibility check, not Unity Editor Play, a player build, shader compilation, rendering or Unity package/asset validation.
