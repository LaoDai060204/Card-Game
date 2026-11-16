# Native collection search check

Run from the repository root with a .NET 10 SDK installed at `/tmp/codex-dotnet`,
or set `DOTNET_ROOT` to its location:

```sh
python3 Tools/Tests/BattleSpiritsDeckSearch/run.py
```

The runner extracts the current `CardCollectionView.Start`, `ShowArea`,
`PrintSearchCards`, and `RefreshCurrentArea` method bodies and compiles them with
the production catalog, native card adapter, and `CardsManager`. Only unavailable
widget surfaces are doubled. The checks cover the initially selected collection
tab, the exact shipped 62-card ID set, every printed ID and English name, effect
text, every shipped cost and BP threshold, combined terms, invalid values,
clearing input, and returning from History.

The runner also checks the production input event wiring and collection cell
configuration. It does not run Unity lifecycle scheduling, layout, rendering,
Play mode, or a Windows build.
