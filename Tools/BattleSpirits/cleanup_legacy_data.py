#!/usr/bin/env python3
"""Remove versioned legacy game data; preserve BS, UI translations and folders.

Default is a dry run. This never deletes personal ignored/untracked files. Run
--apply only after the backup required by AGENTS.md has been verified remotely.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / "Documentation/master-duel-data-cleanup.json"


def retained(relative):
    path = Path(relative)
    return (relative.startswith("Data/BattleSpirits/") or relative == "Data/Settings.json"
            or len(path.parts) == 4 and path.parts[:2] == ("Data", "locales")
            and path.name == "translation.conf")


def safe_legacy_path(relative):
    path = Path(relative)
    return (not path.is_absolute() and path.parts and path.parts[0] == "Data"
            and ".." not in path.parts and path.name != ".gitkeep" and not retained(relative))


def plan():
    files = subprocess.check_output(["git", "ls-files", "-z", "--", "Data"], cwd=ROOT).decode().split("\0")
    records = []
    for relative in sorted(filter(None, files)):
        path = ROOT / relative
        if not path.is_file() or not safe_legacy_path(relative): continue
        records.append({"path": relative, "bytes": path.stat().st_size,
                        "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    folders = sorted(str(path.relative_to(ROOT)) for path in (ROOT / "Data").rglob("*") if path.is_dir())
    return {"schemaVersion": 1, "baseCommit": "c70747a026ff558a80ddb7f0b6705be549f5d0aa",
            "purpose": "Remove unused Yu-Gi-Oh/Master Duel metadata and databases after standalone BS bootstrap",
            "scope": "Versioned Data files only; ignored and untracked personal files are excluded",
            "preserved": ["Data/BattleSpirits/**", "Data/Settings.json", "Data/locales/*/translation.conf"],
            "removedFileCount": len(records), "removedBytes": sum(item["bytes"] for item in records),
            "removedPathListSha256": hashlib.sha256("\n".join(item["path"] for item in records).encode()).hexdigest(),
            "preservedFolders": folders, "removedFiles": records}


def apply(manifest):
    tracked = set(subprocess.check_output(["git", "ls-files", "-z", "--", "Data"], cwd=ROOT).decode().split("\0"))
    for item in manifest["removedFiles"]:
        relative = item["path"]
        if not safe_legacy_path(relative) or relative not in tracked:
            raise ValueError("Outside approved versioned legacy-data scope: " + relative)
        path = ROOT / relative
        if path.exists() and hashlib.sha256(path.read_bytes()).hexdigest() != item["sha256"]:
            raise ValueError("File changed since inventory; refusing to delete: " + relative)
    for item in manifest["removedFiles"]:
        (ROOT / item["path"]).unlink(missing_ok=True)
    keeps = []
    for relative in manifest["preservedFolders"]:
        path = ROOT / relative
        path.mkdir(parents=True, exist_ok=True)
        if not any(child.is_file() and child.name != ".gitkeep" for child in path.iterdir()):
            (path / ".gitkeep").touch()
            keeps.append(relative + "/.gitkeep")
    manifest["folderKeepFiles"] = keeps
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    manifest = json.loads(MANIFEST.read_text()) if MANIFEST.is_file() else plan()
    if args.apply: apply(manifest)
    print(json.dumps({"apply": args.apply, "removedFiles": manifest["removedFileCount"],
                      "removedBytes": manifest["removedBytes"], "preservedFolders": len(manifest["preservedFolders"]),
                      "manifest": str(MANIFEST.relative_to(ROOT))}, indent=2))


if __name__ == "__main__": main()
