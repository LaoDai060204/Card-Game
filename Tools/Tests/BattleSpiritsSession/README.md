# Battle Spirits session integration checks

Run `python3 Tools/Tests/BattleSpiritsSession/run.py` from the repository. The
runner compiles the production Core, AI, Puzzles and three session integration
sources directly with the installed .NET SDK's Roslyn compiler. It uses the real
shipped catalog and all four starter decks without overriding support flags.

The checks exercise complete Solo matches driven by the production AI, all ten
puzzles through the session wrapper, presentation input gating, completed-only
replay storage and deterministic seek, replay path/catalog validation, a complete
two-player loopback TCP match, owner-filtered hands and draw events, socket-bound
actors, invalid protocol/deck/catalog/version/frame input and socket cleanup.
Each run uses a temporary private storage directory and deletes its recordings.

This is a pure C# production-source integration harness, not a Unity Play or build
test. Set `DOTNET_ROOT` if the SDK is installed outside `/tmp/codex-dotnet`.
