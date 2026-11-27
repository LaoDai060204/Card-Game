#!/usr/bin/env python3
"""Execute actual native settings action bodies against narrow UI test doubles."""
import json
import os
from pathlib import Path
import subprocess
import tempfile

root = Path(__file__).resolve().parents[3]
source = (root / "Assets/Scripts/MDPro3/UI/ServantUI/SettingServantUI.cs").read_text()

def method(name):
    start = source.index("public override void " + name + "()") if name == "ShowEvent" else source.index("public void " + name + "()")
    brace = source.index("{", start)
    depth = 1
    end = brace + 1
    while depth:
        depth += (source[end] == "{") - (source[end] == "}")
        end += 1
    return source[start:end]

sdk = Path(os.environ.get("DOTNET_ROOT", "/tmp/codex-dotnet"))
version = sorted((sdk / "sdk").iterdir())[-1]
reference = sorted((sdk / "packs/Microsoft.NETCore.App.Ref").glob("*/ref/net10.0"))[-1]
runtime = sorted((sdk / "shared/Microsoft.NETCore.App").iterdir())[-1].name
with tempfile.TemporaryDirectory(prefix="bs-settings-") as work:
    target = Path(work) / "SettingsSmoke.dll"
    actual = Path(work) / "ActualSettingsMethods.cs"
    actual.write_text("using System;using System.Collections.Generic;public partial class SettingsUI:SettingsBase{"
                      + "\n".join(method(name) for name in ("ShowEvent", "OnSurrender", "OnRetry", "OnCheckDecks")) + "}")
    sources = list((Path(__file__).parent / "Settings").glob("*.cs")) + [actual]
    options = ["-nologo", "-langversion:9", "-target:exe", '-out:"' + str(target) + '"']
    options += ['-r:"' + str(p) + '"' for p in reference.glob("*.dll")]
    options += ['"' + str(p) + '"' for p in sources]
    response = Path(work) / "compile.rsp"
    response.write_text("\n".join(options))
    env = dict(os.environ, DOTNET_CLI_HOME=str(Path(work) / "dotnet"), DOTNET_CLI_TELEMETRY_OPTOUT="1")
    subprocess.run([str(sdk / "dotnet"), "exec", str(version / "Roslyn/bincore/csc.dll"), "@" + str(response)], env=env, check=True)
    target.with_suffix(".runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {"tfm": "net10.0", "framework": {"name": "Microsoft.NETCore.App", "version": runtime}}}))
    subprocess.run([str(sdk / "dotnet"), "exec", str(target)], env=env, check=True)
