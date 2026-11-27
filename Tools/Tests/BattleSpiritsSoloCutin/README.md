Run `python3 Tools/Tests/BattleSpiritsSoloCutin/run.py` from the repository root.
The runner compiles the production SoloSelector, CutinViewer, both UI controllers,
both row handlers and SuperScrollView against narrow Unity/framework boundary doubles.
It runs their real list refresh, toggle, click, Play and Auto Play handlers, including
multiple Print calls while Addressables is pending, latest search completion, selected
boss preview, empty results, repeat printing and scrollbar cleanup. The runner also
checks the shipped prefab bytes, GUID files, row geometry, content/scrollbar references
and serialized callbacks.

Addressables completion is explicitly controlled by the harness. Catalog input and
BattleIntegration calls are boundary fixtures/recorders; the test does not claim to
exercise asset import, canvas rendering, texture decoding or native duel execution.
These checks are C# production-source smoke/regression checks, not Unity Play Mode
or a Unity 6000.0.24f1 Windows build. Open the existing Boot scene in that Editor to
verify the final screen composition and visual clipping with real assets.
