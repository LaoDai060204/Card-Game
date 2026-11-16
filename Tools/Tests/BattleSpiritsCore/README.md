Production-source deterministic Battle Spirits rules checks:

```
python3 Tools/Tests/BattleSpiritsCore/run.py
```

The harness compiles actual `Runtime/Core/*.cs` with the .NET SDK Roslyn compiler and reference assemblies, without NuGet or a copied engine. Set `DOTNET_ROOT` if the SDK is outside `/tmp/codex-dotnet`.

Covers opening resources/redraw, all eight Standard steps and first-player exceptions, detached owner-filtered views, invalid-command atomicity, explicit Soul payment, Soul Magic, Magic/Spirit EX Succession with color/slot restrictions, defender-first Flash, tied BP, Life-to-Reserve, Heavy Exhaust refresh, pending destruction, End Step choices, and core/card conservation. Reviewed card fixtures explicitly enable registry-supported profiles so they can also validate the importer before its publication support flag is set.

These are pure C# rules checks, not Unity Play Mode or a Windows build. The effects and Solo/Puzzle harnesses independently cover all 62 effect profiles, ten puzzles, and bot deck pairings.
