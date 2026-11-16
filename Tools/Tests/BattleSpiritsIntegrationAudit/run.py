#!/usr/bin/env python3
"""Roslyn source/API/entry-flow checks. This does not run Unity or build the game."""
import argparse
import glob
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

from assets import validate


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--allow-incomplete", action="store_true", help="Report work in progress without requiring the final bridge/bootstrap.")
    parser.add_argument("--report", default="/tmp/battle-spirits-integration-audit.json")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[3]
    sdk = Path(os.environ.get("DOTNET_ROOT", "/tmp/codex-dotnet"))
    version = sorted((sdk / "sdk").iterdir())[-1]
    roslyn = version / "Roslyn/bincore"
    refs = sorted(glob.glob(str(sdk / "packs/Microsoft.NETCore.App.Ref/*/ref/net10.0/*.dll")))
    runtime = sorted((sdk / "shared/Microsoft.NETCore.App").iterdir())[-1].name
    tracked_changes = subprocess.check_output(["git", "diff", "--name-only", "HEAD", "--", "Assets"], cwd=root, text=True).splitlines()
    untracked = subprocess.check_output(["git", "ls-files", "--others", "--exclude-standard", "--", "Assets"], cwd=root, text=True).splitlines()
    changes = sorted({str(root / path) for path in tracked_changes + untracked if path.endswith(".cs")})
    with tempfile.TemporaryDirectory(prefix="bs-integration-audit-") as work:
        work = Path(work)
        target = work / "IntegrationAudit.dll"
        response = work / "compile.rsp"
        dependencies = [roslyn / "Microsoft.CodeAnalysis.dll", roslyn / "Microsoft.CodeAnalysis.CSharp.dll"]
        response.write_text("\n".join(["-nologo", "-target:exe", "-langversion:9", '-out:"' + str(target) + '"'] + ['-r:"' + str(p) + '"' for p in refs + dependencies] + ['"' + str(Path(__file__).with_name("Program.cs")) + '"']))
        env = dict(os.environ, DOTNET_CLI_HOME=str(work / "dotnet"), DOTNET_CLI_TELEMETRY_OPTOUT="1")
        subprocess.run([str(sdk / "dotnet"), "exec", str(roslyn / "csc.dll"), "@" + str(response)], env=env, check=True)
        for dependency in dependencies:
            shutil.copy2(dependency, work)
        target.with_suffix(".runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {"tfm": "net10.0", "framework": {"name": "Microsoft.NETCore.App", "version": runtime}}}))
        changed_file = work / "changed.json"
        changed_file.write_text(json.dumps(changes))
        result = subprocess.run([str(sdk / "dotnet"), "exec", str(target), str(root), str(changed_file), args.report, str(args.allow_incomplete)], env=env)
    report_path = Path(args.report)
    report = json.loads(report_path.read_text())
    assets = validate(root, args.allow_incomplete)
    report["assets"] = assets
    report["findings"].extend(assets["findings"])
    failed = result.returncode != 0 or any(finding["severity"] == "error" for finding in assets["findings"])
    report["status"] = "FAIL" if failed else "PASS"
    report_path.write_text(json.dumps(report, indent=2) + "\n")
    for finding in assets["findings"]:
        print(finding["severity"], finding["check"], finding["message"])
    print(report["status"] + ": shipped catalog/decks/puzzles, metadata GUIDs and preserved prefab/scene/meta checks; details in " + args.report)
    return int(failed)


if __name__ == "__main__":
    raise SystemExit(main())
