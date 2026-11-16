Run from the repository root:

```sh
python3 Tools/Tests/BattleSpiritsCosmetics/run.py
```

This compiles production `BattleCosmetics.cs` and `Items.cs` as C# 9 against narrow Unity API test doubles with the .NET SDK at `/tmp/codex-dotnet` (or `DOTNET_ROOT`). The smoke harness reads shipped catalog metadata and PNG headers, checks all 43 wallpaper/avatar sources, stable IDs and list identities across reloads, source credits, alpha-bound cropping, stale appearance settings, empty categories, native wallpaper widgets and the supplied card back. Legacy Addressables/DIY image test doubles throw if called.

Temporary compiler output stays outside the repository. This is not Unity Play Mode, pixel rendering, a Unity assembly compile or a Windows build; Unity Editor verification remains necessary.
