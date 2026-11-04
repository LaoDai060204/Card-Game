# Pure visual-curve and audio-data checks

Run from the repository root with a .NET 10 SDK:

```bash
dotnet run --project Tests/BattleSpirits/Visuals/BattleSpirits.Visuals.Tests.csproj -c Release -- .
```

The executable compiles the production `BattleVisualMotion` and `PcmWave`
classes directly, with C# 9 and no Unity stubs or engine dependency. It checks
all eight animation curves, endpoint behavior, invalid/extreme clocks, signed
PCM decoding, unknown WAV chunks, rejected malformed/unsupported WAVs, and all
nine original generated sound-bank files against their provenance hashes,
durations, sample format, headroom, DC offset and zero endpoints.

These checks do not run Unity rendering, model animation in the Editor,
AudioSource playback, or a Windows build.
