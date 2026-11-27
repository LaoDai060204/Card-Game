# Native deck and inspector layout check

```sh
python3 Tools/Tests/BattleSpiritsDeckLayout/run.py
```

Requires a .NET 10 SDK at `/tmp/codex-dotnet`, or `DOTNET_ROOT` set to its location.
The runner reads the actual desktop `CardDetailView` and `DeckView` prefab YAML,
resolves RectTransform sizes through their serialized hierarchy, and verifies the
old inspector face, grid cell, template Root, and disabled/unbound ScrollRect.

It extracts and compiles the current production inspector configuration, deck
configuration/refresh/resize methods, and `BattleCardAdapter.FitFace`. Narrow Unity
API doubles support anchor-based sizing and component lookup; assertions cover
larger centered faces, readable description space, seven columns, matching card
Roots, clipping, native scrolling, 40/50/empty decks, empty Extra/Side sections,
resizing, detached animated Roots, deferred old-button destruction, null card
lists during Awake, mobile guards, and source texture aspect/full UVs/default material.
Source contracts also check direct full-resolution decoding and sampling settings.

This is a C# 9 production-method check with serialized asset inputs. It does not
run Unity layout scheduling, rendering, Play mode, or a Windows build.
