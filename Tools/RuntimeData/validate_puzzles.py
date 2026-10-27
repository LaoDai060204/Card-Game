#!/usr/bin/env python3
"""Verify the bundled, pinned official Puzzle files; optional Linux Lua syntax check."""
from pathlib import Path, PurePosixPath
import argparse
from contextlib import closing
import ctypes
import hashlib
import json
import re
import sqlite3
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]


def git_hash(kind, content):
    return hashlib.sha1(kind.encode() + b" " + str(len(content)).encode() + b"\0" + content).digest()


def tree_hash(files, prefix):
    names = {path[len(prefix) + 1:].split("/")[0] for path in files if path.startswith(prefix + "/")}
    entries = []
    for name in names:
        path = prefix + "/" + name
        if path in files:
            digest, mode, sort = git_hash("blob", files[path]), b"100644", name.encode("utf-8")
        else:
            digest, mode, sort = tree_hash(files, path), b"40000", name.encode("utf-8") + b"/"
        entries.append((sort, mode + b" " + name.encode("utf-8") + b"\0" + digest))
    return git_hash("tree", b"".join(entry for _, entry in sorted(entries)))


def native_syntax(root):
    """Use this checkout's Linux core. Compile text only; never run Lua chunks."""
    if not sys.platform.startswith("linux"):
        raise RuntimeError("--native-lua requires Linux and the bundled Linux ocgcore library")
    path = root / "Assets/Plugins/Linux/libocgcore.so"
    library = ctypes.CDLL(str(path))

    def function(name, result, arguments):
        value = getattr(library, name)
        value.restype, value.argtypes = result, arguments
        return value

    new = function("_Z13luaL_newstatev", ctypes.c_void_p, [])
    load = function("_Z16luaL_loadbufferxP9lua_StatePKcmS2_S2_", ctypes.c_int,
                    [ctypes.c_void_p, ctypes.c_char_p, ctypes.c_size_t, ctypes.c_char_p, ctypes.c_char_p])
    message = function("_Z13lua_tolstringP9lua_StateiPm", ctypes.c_char_p,
                       [ctypes.c_void_p, ctypes.c_int, ctypes.POINTER(ctypes.c_size_t)])
    clear = function("_Z10lua_settopP9lua_Statei", None, [ctypes.c_void_p, ctypes.c_int])
    close = function("_Z9lua_closeP9lua_State", None, [ctypes.c_void_p])
    version = function("_Z11lua_versionP9lua_State", ctypes.c_double, [ctypes.c_void_p])
    state = new()
    if not state:
        raise RuntimeError("The bundled Lua library could not allocate a compiler state")
    errors, files = [], sorted((root / "Puzzle").rglob("*.lua"))
    try:
        lua_version = version(state)
        for script in files:
            data = script.read_bytes()
            relative = script.relative_to(root).as_posix()
            result = load(state, data, len(data), ("@" + relative).encode("utf-8"), b"t")
            if result:
                errors.append({"path": relative, "error": message(state, -1, None).decode("utf-8", "replace")})
            clear(state, 0)
    finally:
        close(state)
    return {"status": "PASS" if not errors else "FAIL", "files": len(files),
            "nativeLuaVersion": lua_version,
            "nativeLibrarySha256": hashlib.sha256(path.read_bytes()).hexdigest(),
            "operation": "Compile text with luaL_loadbufferx; no Lua or duel execution", "errors": errors}


def validate(root):
    manifest = json.loads((root / "Tools/RuntimeData/puzzle-source.json").read_text(encoding="utf-8"))
    files, folded, groups = {}, {}, {}
    for entry in manifest["files"]:
        relative = entry["path"]
        parts = PurePosixPath(relative).parts
        if len(parts) < 2 or parts[0] != "Puzzle" or ".." in parts or not relative.endswith(".lua"):
            raise ValueError("Unexpected manifest path: " + relative)
        for part in parts:
            if (re.search(r'[<>:"\\|?*]', part) or part.endswith((" ", "."))
                    or re.match(r"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)", part, re.I)):
                raise ValueError("Filename incompatible with Windows: " + relative)
        key = relative.casefold()
        if key in folded:
            raise ValueError("Case-insensitive path collision: " + relative)
        folded[key] = relative
        content = (root / relative).read_bytes()
        content.decode("utf-8-sig")
        if len(content) != entry["size"]:
            raise ValueError("Wrong file size: " + relative)
        if git_hash("blob", content).hex() != entry["gitBlobSha1"]:
            raise ValueError("Official Git blob mismatch: " + relative)
        if hashlib.sha256(content).hexdigest() != entry["sha256"]:
            raise ValueError("SHA-256 mismatch: " + relative)
        files[relative] = content
        folder = str(PurePosixPath(relative).parent)
        groups[folder] = groups.get(folder, 0) + 1
    if len(files) != manifest["fileCount"] or groups != manifest["folderCounts"]:
        raise ValueError("Manifest group/count mismatch")
    tree = tree_hash(files, "Puzzle").hex()
    if tree != manifest["sourcePuzzleTree"]:
        raise ValueError("Official Puzzle Git tree mismatch")
    extras = sorted(path.relative_to(root).as_posix() for path in (root / "Puzzle").rglob("*.lua")
                    if path.relative_to(root).as_posix() not in files)
    coverage = card_coverage(root, files)
    return {"status": "PASS", "sourceCommit": manifest["sourceCommit"], "officialFiles": len(files),
            "officialTree": tree, "folderCounts": groups, "additionalLocalPuzzles": extras,
            "utf8AndWindowsPaths": "PASS", "baseCardDatabaseCoverage": coverage, "unityPlayTested": False}


def card_coverage(root, files):
    database = root / "Data/locales/en-US/cards.cdb"
    with closing(sqlite3.connect(database.resolve().as_uri() + "?mode=ro&immutable=1", uri=True)) as connection:
        ids = {row[0] for row in connection.execute("SELECT id FROM datas")}
    missing, dynamic, referenced, calls = {}, {}, set(), 0
    for path, content in sorted(files.items()):
        text = content.decode("utf-8-sig")
        values = [int(value) for value in re.findall(r"Debug\.AddCard\(\s*(\d+)", text)]
        absent = sorted(set(values) - ids)
        if absent:
            missing[path] = absent
        expressions = re.findall(r"Debug\.AddCard\(\s*([^,\r\n]+)", text)
        nonliteral = [value for value in expressions if not re.fullmatch(r"\d+\s*", value)]
        if nonliteral:
            dynamic[path] = nonliteral
        calls += len(values)
        referenced.update(values)
    return {"database": database.relative_to(root).as_posix(), "databaseCards": len(ids),
            "numericAddCardCalls": calls, "uniqueReferencedIds": len(referenced),
            "puzzlesWithoutMissingLiteralIds": len(files) - len(missing),
            "puzzlesWithMissingIds": missing, "dynamicAddCardExpressions": dynamic,
            "scope": "Base English database and literal card references only; not a gameplay test"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--native-lua", action="store_true", help="Also compile Lua using the bundled Linux ocgcore")
    parser.add_argument("--native-worker", action="store_true", help=argparse.SUPPRESS)
    args = parser.parse_args()
    root = args.root.resolve()
    if args.native_worker:
        result = native_syntax(root)
    else:
        result = validate(root)
        if args.native_lua:
            # Isolate a future incompatible native library from the portable verifier.
            worker = subprocess.run([sys.executable, str(Path(__file__).resolve()), "--root", str(root), "--native-worker"],
                                    text=True, capture_output=True, timeout=60)
            if worker.returncode != 0:
                raise RuntimeError("Native Lua worker failed: " + (worker.stderr or worker.stdout))
            result["nativeLua"] = json.loads(worker.stdout)
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 1 if result.get("status") != "PASS" else 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, RuntimeError, AttributeError, sqlite3.Error, subprocess.TimeoutExpired) as error:
        print("FAIL: " + str(error), file=sys.stderr)
        raise SystemExit(1)
