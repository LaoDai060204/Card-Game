Run from the repository root:

```sh
python3 Tools/Tests/BattleSpiritsPresentation/run.py
python3 Tools/Tests/BattleSpiritsPresentation/validate_assets.py
python3 Tools/Tests/BattleSpiritsPresentation/settings_smoke.py
```

`run.py` uses the .NET SDK at `DOTNET_ROOT` (default `/tmp/codex-dotnet`). It compiles the actual production presentation sources with narrow Unity API and catalog fixtures. The smoke checks cover upward-facing silver geometry, the approved 15.6 by 15.2 arena and mirrored resource zones, 42-degree perspective and viewport fit, UI layer inheritance, actual pixel sizing through inherited UI scale up to 4K, camera visibility scheduling, the fixed particle budget, copied snapshots, queued committed events, event deduplication, both explicitly approved bosses, Skip, reduced motion, crowded fields above five cards, exact resource count labels, removed cards and idempotent disposal. Temporary compiler output stays outside the repository; NuGet is not required.

`validate_assets.py` requires Pillow. It reads the shipped PNG alpha channels, verifies the 43 silhouette bounds and the explicit Ritenia/Pisketona boss assignments, and checks shader properties referenced by C#.

The presentation checks also verify selected Battle Spirits field artwork, field changes, the None fallback, central-art crop bounds and stable Random selection. `settings_smoke.py` extracts the actual native settings action bodies and exercises Solo/Puzzle/Replay/LAN visibility, retry, surrender locking, and viewer-filtered card inspection using UI doubles.

These checks do not run Unity, render pixels, compile GPU shaders, perform real asynchronous texture requests, or replace Play Mode and Windows build verification in Unity 6000.0.24f1 with URP 17.
