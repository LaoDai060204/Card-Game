#!/usr/bin/env python3
"""Compile actual production sources with the SDK Roslyn compiler, without NuGet."""
import glob
import json
import os
from pathlib import Path
import subprocess
import tempfile

root = Path(__file__).resolve().parents[3]
sdk = Path(os.environ.get("DOTNET_ROOT", "/tmp/codex-dotnet"))
version = sorted((sdk / "sdk").iterdir())[-1]
refs = sorted(glob.glob(str(sdk / "packs/Microsoft.NETCore.App.Ref/*/ref/net10.0/*.dll")))
runtime = sorted((sdk / "shared/Microsoft.NETCore.App").iterdir())[-1].name
with tempfile.TemporaryDirectory(prefix="bs-effects-") as work:
    target = Path(work) / "EffectsTests.dll"
    response = Path(work) / "compile.rsp"
    sources = sorted((root / "Assets/BattleSpirits/Runtime/Core").glob("*.cs")) + sorted(Path(__file__).parent.glob("*.cs"))
    response.write_text("\n".join(["-nologo", "-langversion:9.0", "-target:exe", '-out:"' + str(target) + '"'] + ['-r:"' + p + '"' for p in refs] + ['"' + str(p) + '"' for p in sources]))
    env = dict(os.environ, DOTNET_CLI_HOME=str(Path(work) / "dotnet"), DOTNET_CLI_TELEMETRY_OPTOUT="1")
    subprocess.run([str(sdk / "dotnet"), "exec", str(version / "Roslyn/bincore/csc.dll"), "@" + str(response)], env=env, check=True)
    target.with_suffix(".runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {"tfm": "net10.0", "framework": {"name": "Microsoft.NETCore.App", "version": runtime}}}))
    subprocess.run([str(sdk / "dotnet"), "exec", str(target), str(root)], env=env, check=True)
