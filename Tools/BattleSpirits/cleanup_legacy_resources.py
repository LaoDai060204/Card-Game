#!/usr/bin/env python3
"""Bounded initial resource cleanup for the Battle Spirits conversion.

The default is a dry run. --apply removes only versioned, unmodified resources
in a fixed cleanup phase and preserves their directory structure. Pet/wallpaper
resources and their shared shader bundle are protected. This does not claim
that the wider source-code/UI cleanup is complete.
"""

import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import subprocess
import sys


ROOT = Path(__file__).resolve().parents[2]
REMOVAL_ROOTS = (
    "Platforms/StandaloneWindows64/MonsterCutin",
    "Platforms/StandaloneWindows64/MonsterCutin2",
    "Sound/Voice",
)
PRESERVED_ROOTS = (
    "Platforms/StandaloneWindows64/MasterDuel/Mate",
    "Platforms/StandaloneWindows64/MasterDuel/Wallpaper",
)
MANIFEST = "Documentation/master-duel-initial-cleanup.json"
BRANCH = "battle-spirits-mdpro3-ui"
PHASES = {
    "initial": {
        "name": "initial-known-unused-resources",
        "roots": REMOVAL_ROOTS,
        "preservedDirectories": PRESERVED_ROOTS,
        "preservedFiles": (),
        "manifest": MANIFEST,
        "previousManifests": (),
    },
    "external": {
        "name": "external-resources-except-pet-wallpaper",
        "roots": ("Platforms", "Sound"),
        "preservedDirectories": (*PRESERVED_ROOTS, "Sound/BattleSpirits"),
        "preservedFiles": ("Platforms/StandaloneWindows64/MasterDuel/Built-in/shaders",),
        "manifest": "Documentation/master-duel-external-cleanup.json",
        "previousManifests": (MANIFEST,),
    },
}


def git(*arguments):
    return subprocess.check_output(["git", "-C", str(ROOT), *arguments])


def protected(relative, phase="initial"):
    config = PHASES[phase]
    value = PurePosixPath(relative)
    return (relative in config["preservedFiles"]
            or any(value.is_relative_to(PurePosixPath(base))
                   for base in config["preservedDirectories"]))


def allowed(relative, phase="initial"):
    """Only descendants, never a parent/root or a lexical path escape."""
    value = PurePosixPath(relative)
    return (not value.is_absolute() and ".." not in value.parts
            and str(value) == relative
            and not protected(relative, phase)
            and any(value.is_relative_to(PurePosixPath(base))
                    and value != PurePosixPath(base) for base in PHASES[phase]["roots"]))


def checked_file(relative, phase="initial"):
    if not allowed(relative, phase):
        raise ValueError("Refusing path outside fixed removal roots: " + relative)
    path = ROOT / relative
    if path.is_symlink() or any(parent.is_symlink() for parent in path.parents
                               if parent != ROOT and ROOT in parent.parents):
        raise ValueError("Refusing symbolic-link resource path: " + relative)
    if not path.is_file():
        raise ValueError("Expected a regular resource file: " + relative)
    return path


def inventory(phase="initial"):
    config = PHASES[phase]
    indexed = {}
    for record in git("ls-files", "--stage", "-z", "--", *config["roots"]).split(b"\0"):
        if not record:
            continue
        info, name = record.split(b"\t", 1)
        mode, blob, stage = info.decode("ascii").split()
        relative = name.decode("utf-8")
        if mode not in ("100644", "100755") or stage != "0":
            raise ValueError("Refusing non-regular/conflicted indexed file: " + relative)
        if protected(relative, phase):
            continue
        if not allowed(relative, phase):
            raise ValueError("Indexed path escaped cleanup scope: " + relative)
        indexed[relative] = blob

    files, directories = [], []
    for base in config["roots"]:
        directory = ROOT / base
        if directory.is_symlink() or not directory.is_dir():
            raise ValueError("Missing or linked cleanup directory: " + base)
        directories.append(base)
        for path in sorted(directory.rglob("*")):
            relative = path.relative_to(ROOT).as_posix()
            if path.is_symlink():
                raise ValueError("Refusing symbolic link: " + relative)
            if path.is_dir():
                directories.append(relative)
                continue
            if path.name == ".gitkeep" or protected(relative, phase):
                continue
            # No Cross Duel resources exist in the current Windows package.
            # A future package containing them needs an explicit pet review.
            if any("crossduel" in part.casefold() for part in path.parts):
                raise ValueError("Cross Duel resource requires cosmetic dependency review: " + relative)
            path = checked_file(relative, phase)
            if relative not in indexed:
                raise ValueError("Untracked resource must be backed up separately: " + relative)
            files.append({"path": relative, "bytes": path.stat().st_size})

    files.sort(key=lambda item: item["path"])
    directories.sort()
    file_names = "".join(item["path"] + "\n" for item in files).encode("utf-8")
    path_sizes = "".join(item["path"] + "\0" + str(item["bytes"]) + "\n"
                         for item in files).encode("utf-8")
    return {
        "version": 0,
        "phase": config["name"],
        "masterDuelCleanupComplete": False,
        "sourceCommit": git("rev-parse", "HEAD").decode().strip(),
        "branch": BRANCH,
        "removalRoots": list(config["roots"]),
        "preservedResourceRoots": list(config["preservedDirectories"]),
        "preservedResourceFiles": list(config["preservedFiles"]),
        "removedFileCount": len(files),
        "removedBytes": sum(item["bytes"] for item in files),
        "fileListSha256": hashlib.sha256(file_names).hexdigest(),
        "fileListHashEncoding": "UTF-8 sorted relative paths, each followed by LF",
        "pathSizeListSha256": hashlib.sha256(path_sizes).hexdigest(),
        "pathSizeHashEncoding": "UTF-8 sorted relative path, NUL, decimal byte count, LF",
        "preservedExistingDirectories": directories,
        "files": files,
    }


def check_local_changes(phase):
    """Allow only deletions/keepers already documented by an earlier phase."""
    config = PHASES[phase]
    deleted, keepers = set(), set()
    for relative in config["previousManifests"]:
        previous = json.loads((ROOT / relative).read_text(encoding="utf-8"))
        if not previous.get("applied"):
            raise ValueError("Earlier cleanup phase has not been applied: " + relative)
        deleted.update(item["path"] for item in previous["files"])
        keepers.update(previous["createdGitkeepFiles"])
    for record in git("status", "--porcelain=v1", "-z", "--untracked-files=all", "--", *config["roots"]).split(b"\0"):
        if not record:
            continue
        status, relative = record[:2].decode(), record[3:].decode("utf-8")
        if status in (" D", "D ") and relative in deleted and not (ROOT / relative).exists():
            continue
        if status in ("??", "A ") and relative in keepers:
            path = ROOT / relative
            if path.is_file() and not path.is_symlink() and path.stat().st_size == 0:
                continue
        raise ValueError("Cleanup scope has undocumented local changes: " + relative)


def apply(plan, phase="initial"):
    config = PHASES[phase]
    if git("branch", "--show-current").decode().strip() != BRANCH:
        raise ValueError("--apply is restricted to the Battle Spirits conversion branch")
    # Do not discard the owner's modifications, even within the fixed roots.
    check_local_changes(phase)
    target = ROOT / config["manifest"]
    if target.exists():
        raise ValueError("A cleanup manifest already exists; refusing to overwrite history")
    if not plan["files"]:
        raise ValueError("There are no indexed resources to remove")
    # Finish every check before the first mutation. Pet and wallpaper roots are
    # outside the fixed removal roots and are never passed to unlink().
    paths = []
    for item in plan["files"]:
        path = checked_file(item["path"], phase)
        if path.stat().st_size != item["bytes"]:
            raise ValueError("Resource changed while planning: " + item["path"])
        paths.append(path)
    for path in paths:
        path.unlink()
    keepers = []
    for relative in sorted(plan["preservedExistingDirectories"],
                           key=lambda item: (-len(PurePosixPath(item).parts), item)):
        directory = ROOT / relative
        if not any(directory.iterdir()):
            keeper = directory / ".gitkeep"
            keeper.touch(exist_ok=False)
            keepers.append(keeper.relative_to(ROOT).as_posix())
    plan["createdGitkeepFiles"] = sorted(keepers)
    plan["applied"] = True
    plan["previousManifests"] = list(config["previousManifests"])
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(plan, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--phase", choices=sorted(PHASES), default="initial",
                        help="initial legacy cutins/voices, or remaining external bundles/audio")
    parser.add_argument("--apply", action="store_true",
                        help="apply the selected fixed phase; default only reports a dry run")
    options = parser.parse_args()
    try:
        plan = inventory(options.phase)
        if options.apply:
            apply(plan, options.phase)
        print(json.dumps({"mode": "applied" if options.apply else "dry-run",
                          "phase": plan["phase"],
                          "files": plan["removedFileCount"],
                          "bytes": plan["removedBytes"],
                          "directoriesPreserved": len(plan["preservedExistingDirectories"]),
                          "fileListSha256": plan["fileListSha256"],
                          "masterDuelCleanupComplete": False}, indent=2))
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        print("FAIL: " + str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
