#!/usr/bin/env python3
"""Compile the production audio manager against narrow Unity API test doubles."""
import json
import os
from pathlib import Path
import subprocess
import tempfile

root = Path(__file__).resolve().parents[3]
sdk = Path(os.environ.get("DOTNET_ROOT", "/tmp/codex-dotnet"))
version = sorted((sdk / "sdk").iterdir())[-1]
reference = sorted((sdk / "packs/Microsoft.NETCore.App.Ref").glob("*/ref/net10.0"))[-1]
runtime = sorted((sdk / "shared/Microsoft.NETCore.App").iterdir())[-1].name
with tempfile.TemporaryDirectory(prefix="bs-audio-") as work:
    target = Path(work) / "AudioSmoke.dll"
    response = Path(work) / "compile.rsp"
    sources = [root / "Assets/Scripts/MDPro3/Managers/AudioManager.cs"]
    sources += sorted(Path(__file__).parent.glob("*.cs"))
    options = ["-nologo", "-target:exe", "-langversion:9.0", "-nowarn:0649", '-out:"' + str(target) + '"']
    options += ['-r:"' + str(p) + '"' for p in sorted(reference.glob("*.dll"))]
    options += ['"' + str(p) + '"' for p in sources]
    response.write_text("\n".join(options))
    env = dict(os.environ, DOTNET_CLI_HOME=str(Path(work) / "dotnet"), DOTNET_CLI_TELEMETRY_OPTOUT="1")
    subprocess.run([str(sdk / "dotnet"), "exec", str(version / "Roslyn/bincore/csc.dll"), "@" + str(response)], env=env, check=True)
    target.with_suffix(".runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {"tfm": "net10.0", "framework": {"name": "Microsoft.NETCore.App", "version": runtime}}}))
    subprocess.run([str(sdk / "dotnet"), "exec", str(target)], env=env, check=True)
