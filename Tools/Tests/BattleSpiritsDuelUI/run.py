#!/usr/bin/env python3
"""Compile production native duel sources as C#9 and run lifecycle/command checks."""
import json
import os
from pathlib import Path
import subprocess
import shutil
import tempfile
from test_real_prefabs import validate

root = Path(__file__).resolve().parents[3]
validate(root)
sdk = Path(os.environ.get("DOTNET_ROOT", "/tmp/codex-dotnet"))
version = sorted((sdk / "sdk").iterdir())[-1]
reference = sorted((sdk / "packs/Microsoft.NETCore.App.Ref").glob("*/ref/net10.0"))[-1]
runtime = sorted((sdk / "shared/Microsoft.NETCore.App").iterdir())[-1].name
with tempfile.TemporaryDirectory(prefix="bs-native-duel-") as work:
    target = Path(work) / "NativeDuelSmoke.dll"
    sources = [p for p in Path(__file__).parent.glob("*.cs") if p.name != "BridgeProgram.cs"]
    production = root / "Assets/BattleSpirits/Runtime"
    sources += [production / "Integration/BattleDuelServant.cs", production / "Integration/BattleDuelUI.cs", production / "Integration/BattleDuelHud.cs",
                production / "Visuals/BattlePresentationState.cs"]
    sources += list((production / "Core").glob("*.cs"))
    response = Path(work) / "compile.rsp"
    options = ["-nologo", "-langversion:9", "-target:exe", "-nowarn:0649", '-out:"' + str(target) + '"']
    options += ['-r:"' + str(p) + '"' for p in reference.glob("*.dll")]
    options += ['"' + str(p) + '"' for p in sources]
    response.write_text("\n".join(options))
    env = dict(os.environ, DOTNET_CLI_HOME=str(Path(work) / "dotnet"), DOTNET_CLI_TELEMETRY_OPTOUT="1")
    subprocess.run([str(sdk / "dotnet"), "exec", str(version / "Roslyn/bincore/csc.dll"), "@" + str(response)], env=env, check=True)
    target.with_suffix(".runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {"tfm": "net10.0", "framework": {"name": "Microsoft.NETCore.App", "version": runtime}}}))
    subprocess.run([str(sdk / "dotnet"), "exec", str(target)], env=env, check=True)
    # Also compile against the real session, replay, transport, AI and puzzle APIs;
    # fixture events are only used in the executable lifecycle tests above.
    integration_sources = [p for p in sources if p.name != "Program.cs"]
    integration_sources.append(Path(__file__).parent / "BridgeProgram.cs")
    integration_sources += list((production / "AI").glob("*.cs"))
    integration_sources += list((production / "Puzzles").glob("*.cs"))
    integration_sources += [production / "Integration" / name for name in
                            ("BattleGameSession.cs", "BattleReplay.cs", "BattleLanTransport.cs", "BattleIntegration.cs", "BattleRuntimeDriver.cs")]
    integration_sources += [production / "Data/BattleCatalog.cs", production / "NativeAdapters/BattleDeckStorage.cs"]
    integration_sources += sorted((production / "Interface").glob("*.cs"))
    integration_target = Path(work) / "NativeSessionIntegration.dll"
    options = ["-nologo", "-langversion:9", "-define:REAL_SESSION", "-target:exe", "-nowarn:0649",
               '-out:"' + str(integration_target) + '"']
    options += ['-r:"' + str(p) + '"' for p in reference.glob("*.dll")]
    options += ['-r:"' + str(version / "Newtonsoft.Json.dll") + '"']
    options += ['"' + str(p) + '"' for p in integration_sources]
    response.write_text("\n".join(options))
    subprocess.run([str(sdk / "dotnet"), "exec", str(version / "Roslyn/bincore/csc.dll"), "@" + str(response)], env=env, check=True)
    integration_target.with_suffix(".runtimeconfig.json").write_text(target.with_suffix(".runtimeconfig.json").read_text())
    shutil.copy2(version / "Newtonsoft.Json.dll", Path(work) / "Newtonsoft.Json.dll")
    subprocess.run([str(sdk / "dotnet"), "exec", str(integration_target), str(root)], env=env, check=True)
    print("PASS native duel + actual bridge/driver/catalog/deck/session/replay/LAN/AI/puzzle C#9 integration compile and startup checks.")
