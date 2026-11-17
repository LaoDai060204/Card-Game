# Recovered English MDPro3

The owner-selected English Yu-Gi-Oh! baseline is
`backup/2026-11-15-before-card-review-publication`, at commit
`2c7fd2ee7e5d2100eb85cccff7fb8418ec52439c`. The next two commits on
`mdpro3-english`, `22314e5c1` and `fbffa9d14`, replace the active application with
Battle Spirits. This recovery uses the MDPro3 tree and its original interface,
card data, WindBot and native YGO engine. It does not add a game-mode toggle.

Work is on `fix/mdpro3-english-repair-20261117`. The original `main`,
`mdpro3-english` and Battle Spirits branches are preserved. Backups verified on
origin before the repair:

- `backup/2026-11-17-before-english-repair-180358`: `db9007d14536df540d8ccd8b9eb195981060d485`.
- `backup/2026-11-17-english-before-fixes-180358`: `2c7fd2ee7e5d2100eb85cccff7fb8418ec52439c`.

## Run the game

Open `Assets/Scenes/Boot.unity` with **Unity 6000.0.24f1**, using the Windows x64
target. Keep `Data`, `Picture`, `Sound`, `Platforms` and `Puzzle` beside `Assets`.
The interface, card-data and prerelease languages default to English. For source
prefabs, choose Addressables **Use Asset Database**. The existing URP and nine
texture import compatibility fixes are retained.

Windows IL2CPP export also requires Windows Build Support (IL2CPP), the Visual
Studio C++ toolchain and Windows SDK. This Linux development machine does not
have Unity Editor; production-source harnesses and native TCP tests are not
Unity Play, render, full duel or Windows build validation.

## Repair scope

- WindBot resolves the selected card-data language, preserves explicit database
  paths, tolerates an absent `Expansions` folder and reports missing CDB files
  without waiting for a console key or exiting the Unity process. Executor
  catalogs are published atomically; random selection cannot loop forever when
  there is no eligible Normal bot. Concurrent launches capture their own bot
  options, publish complete card catalogs and use independent random generators.
- Solo rejects overlapping launches, catches bot-worker errors and cleans up a
  server when the client cannot start. Native server shutdown runs off the
  Unity caller and retains session ownership until old workers finish. LAN and
  hand-test joins are bounded and their delayed cleanup cannot stop a new session.
- TCP reads require complete frames, writes preserve full payloads, and reconnects
  discard stale queues and readers. Fixed-width UTF-16 handshake fields retain
  their original sizes without splitting surrogate pairs or logging passwords.
- Optional network data and downloads are validated without disabling TLS
  certificate checks. Retired prerelease defaults are skipped while installed
  data and custom source settings remain available.
- The 104 official Puzzle files are restored byte-for-byte with a pinned source
  manifest. Missing files/cards, empty lists, deleted previous selections and
  nonliteral custom-card previews are handled before starting the duel core.

Five old WCS puzzles still require six legacy card IDs that are absent from the
bundled CDB/scripts. The validator reports them and the UI prevents starting an
incomplete puzzle; no IDs or card effects are invented or substituted.
See `Tools/RuntimeData/puzzle-source.json` for source attribution and checksums.
Original external Puzzle filenames and Lua text are preserved as data; English
application source and canonical localization keys remain separate from them.

## Standalone verification

Use Python 3 with PyYAML and .NET SDK 10. For the prepared cloud image, activate:

```bash
export DOTNET_ROOT=/workspace/.cloud-tools/dotnet-10.0.404
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_HOME=/workspace/.cloud-tools/dotnet-home
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export NUGET_PACKAGES=/workspace/.cloud-tools/nuget-packages
export XDG_DATA_HOME=/workspace/.cloud-tools/xdg-data
export XDG_CONFIG_HOME=/workspace/.cloud-tools/xdg-config
export XDG_RUNTIME_DIR=/workspace/.cloud-tools/runtime
mkdir -p "$DOTNET_CLI_HOME" "$NUGET_PACKAGES" "$XDG_DATA_HOME" "$XDG_CONFIG_HOME" "$XDG_RUNTIME_DIR"
chmod 700 "$XDG_RUNTIME_DIR"
cd /workspace/Card-Game
```

The XDG paths keep runtime mutexes and NuGet state in writable directories
without changing HOME. No background service is required: native test servers
start and stop inside their harnesses.

```bash
set -euo pipefail
python3 Tools/Translations/validate_english.py --base e9ba63fa6
dotnet run --project Tools/Translations/tests/EnglishMigration.Tests.csproj -c Release -- .
dotnet run --project Tools/Translations/tests/Audit/SourceAudit.csproj -c Release -- . e9ba63fa6
dotnet run --project Tools/Tests/RuntimeData/RuntimeData.Tests.csproj -c Release -- Data/cards_Lite.json
dotnet run --project Tools/Tests/RemoteImages/RemoteImages.Tests.csproj -c Release
python3 Tools/Tests/WindBotHandshake.py
python3 Tools/Tests/WindBotStartup.py
python3 Tools/Tests/WindBotConcurrency/run.py
python3 Tools/Tests/SoloLaunchRegression.py
python3 Tools/Tests/LocalServerCallers.py
python3 Tools/Tests/YgoServerLifecycle.py --native-linux
python3 Tools/Tests/TcpTransport/run.py
python3 Tools/Tests/PuzzleRegression.py
python3 Tools/Tests/DisplayLocalization.py
python3 Tools/RuntimeData/validate_puzzles.py --native-lua
python3 Tools/Tests/validate_network_metadata.py
python3 Tools/Tests/DownloadLifecycle.py
git diff HEAD --check -- . ':(exclude)Puzzle/**'
```

The source audit checks C# 9 syntax and source-language invariants; it does not
resolve Unity assemblies or compile the full application. Existing original
locale values, protocol identifiers, resource GUIDs and attribution are retained.
The whitespace check excludes the immutable upstream Puzzle files, whose
original trailing whitespace is verified by the source checksums instead.

The additional diagnostic `python3 Tools/Tests/NativePuzzles.py --report /tmp/puzzle-init.json`
uses the bundled Linux core and real CDB/scripts. It is deliberately separate
from the regression batch because it reports unresolved upstream compatibility
failures: 97 initializations pass, two fail, and five lack the legacy data above.
See [Puzzle compatibility details](Tools/RuntimeData/PUZZLES.md). A successful
syntax or data check does not override those native failures.
