Run from the repository root:

```sh
python3 Tools/Tests/BattleSpiritsAudio/run.py
```

The harness compiles the production `AudioManager.cs` as C# 9 with narrow Unity,
network and UniTask doubles, then executes 42 behavioral assertions. It checks
prefab clip clearing, six cached original procedural UI tones, repeat throttling,
one-shot mute, shuffle, volume preferences, approved resource/file playback,
legacy address rejection, absent-content silence, device changes and manager
replacement/disposal. The resource double rejects every lookup outside
`BattleSpirits/Audio/`. Output is built in a temporary directory.

The three serialized scene audio sources and original public entry points stay
compatible with the retained interface. Widget event names select newly generated
sounds; they no longer load old audio assets. Optional music resources can be added
at `Assets/.../Resources/BattleSpirits/Audio/Menu`, `Duel`, `Boss` and `Climax`.
Absent resources are silent. Voices must first be loaded through an explicit
`BattleSpirits/Audio/...` resource path or a file under the device's
`BattleSpirits/audio` directory. Old identifiers are not music aliases.

This verifies production-source behavior against doubles. It does not test Unity
Play Mode, audio hardware, mixing, asset import, or a player build. The test SDK
is `/tmp/codex-dotnet` by default, or the directory specified by `DOTNET_ROOT`.
