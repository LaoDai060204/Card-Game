# Native Battle Spirits card and deck checks

Run from the project root with .NET SDK 10:

```sh
dotnet run --project Tools/BattleSpiritsNativeTests/BattleSpiritsNative.Tests.csproj -c Release -- "$PWD"
python3 Tools/BattleSpiritsNativeTests/audit_api.py
```

The executable compiles the production catalog, native card/deck models, search adapter,
and device-local deck storage. Unity image/widget APIs have small stubs. It reads the
actual shipped catalog and all four starter decks; it never changes `implemented` flags.
Temporary device storage is deleted after each run, and original user decks are untouched.

The 496 assertions cover 62-card identity/clone/image paths, shipped rule availability,
English effects and cost/BP/color filtering, all four Standard starters, same-name copy
limits, empty Extra/Side, the 40-card minimum, decks larger than 60 cards, root/group save
paths, zero deck-case defaults, cosmetic round trips, traversal rejection, caller-owned
lists, and a deleted/empty device deck library.

`audit_api.py` scans every Assets C# source for native CardsManager/PacksManager calls,
while distinguishing the separate engine/bot manager types. This checks retained APIs
after removing the old database/search implementation.

These are production-source checks, not Unity Play, shader/render, prefab, or player-build
tests. Image aspect fitting, raw material behavior, widget navigation, and asynchronous
Unity object lifetimes still require Unity verification.
