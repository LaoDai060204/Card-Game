Run `python3 Tools/Tests/BattleSpiritsDuelUI/run.py` from the repository root.

The harness compiles the actual native duel servant/controller, core sources and
presentation DTO with C# 9. Narrow doubles model the existing ServantUI/native
widget signatures, Addressables, renderer and backend events. Checks exercise
ordered multi-choice bounds and source positions, full command parameter labels,
all action pages, unmodified backend sends, acting-seat/replay/presentation input
locks, private-instance persistent callback removal, HUD life, committed summons,
session rebind and disposal. A second executable compiles the actual application
bridge and frame driver together with the native duel, catalog, deck storage,
session, replay, LAN, AI and puzzle production sources. It loads the shipped 62
card profiles and four decks without implementation overrides, then exercises
launch/reuse, failed-start isolation, concession/retry/replay, puzzle solve/reset,
one-time completion persistence, command-line puzzle launch, boss preview, native
leave disposal and driver teardown. A real loopback listening socket verifies
that leaving releases a host's port for immediate rehosting, while menu frame
updates leave the disposed session untouched. Only the unavailable Unity/native framework APIs are doubled in
this executable. Decks and replays are written to a unique temporary directory and
removed afterwards. Compiler output also stays in a temporary directory.

Current results: 47 native controller checks and 41 actual bridge/driver checks.

This is a source/lifecycle smoke test. It does not run the Unity Editor, load real
Addressables, render images, verify prefab pixels or replace Unity Play Mode and
Windows build checks on Unity 6000.0.24f1 / URP 17.
