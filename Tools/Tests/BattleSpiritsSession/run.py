#!/usr/bin/env python3
"""Compile and exercise the production session, LAN and replay sources without Unity/NuGet."""
import glob
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

root = Path(__file__).resolve().parents[3]
sdk = Path(os.environ.get("DOTNET_ROOT", "/tmp/codex-dotnet"))
version = sorted((sdk / "sdk").iterdir())[-1]
refs = sorted(glob.glob(str(sdk / "packs/Microsoft.NETCore.App.Ref/*/ref/net10.0/*.dll")))
runtime = sorted((sdk / "shared/Microsoft.NETCore.App").iterdir())[-1].name
with tempfile.TemporaryDirectory(prefix="bs-session-") as work:
    work = Path(work)
    target = work / "SessionTests.dll"
    sources = []
    for directory in ("Core", "AI", "Puzzles"):
        sources += sorted((root / "Assets/BattleSpirits/Runtime" / directory).glob("*.cs"))
    sources += [root / "Assets/BattleSpirits/Runtime/Integration" / name for name in ("BattleGameSession.cs", "BattleLanTransport.cs", "BattleReplay.cs")]
    sources += [Path(__file__).with_name("Program.cs")]
    newtonsoft = version / "Newtonsoft.Json.dll"
    response = work / "compile.rsp"
    response.write_text("\n".join(["-nologo", "-target:exe", '-out:"' + str(target) + '"'] + ['-r:"' + p + '"' for p in refs + [str(newtonsoft)]] + ['"' + str(p) + '"' for p in sources]))
    env = dict(os.environ, DOTNET_CLI_HOME=str(work / "dotnet"), DOTNET_CLI_TELEMETRY_OPTOUT="1")
    subprocess.run([str(sdk / "dotnet"), "exec", str(version / "Roslyn/bincore/csc.dll"), "@" + str(response)], env=env, check=True)
    shutil.copy2(newtonsoft, work / "Newtonsoft.Json.dll")
    target.with_suffix(".runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {"tfm": "net10.0", "framework": {"name": "Microsoft.NETCore.App", "version": runtime}}}))
    subprocess.run([str(sdk / "dotnet"), "exec", str(target), str(root)], env=env, check=True)
