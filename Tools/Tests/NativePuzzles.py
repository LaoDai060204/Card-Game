#!/usr/bin/env python3
"""Initialize original Puzzles in the bundled Linux core with real card/script data.

Each puzzle runs in a fresh subprocess. This executes Lua setup and card initial
effects, not a solution, Unity Play mode, rendering or a Windows player build.
"""
import argparse
from contextlib import closing
import ctypes
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import sqlite3
import subprocess
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[2]
LITERAL_CARD = re.compile(r"(?m)^[\t ]*(?:(?:local[\t ]+)?[A-Za-z_]\w*[\t ]*=[\t ]*)?Debug\.AddCard\s*\(\s*(\d+)\s*,")


class CardData(ctypes.Structure):
    # Matches Percy.CardData in CoreWrapper.cs and ocgcore/card_data.h.
    _fields_ = [("code", ctypes.c_uint32), ("alias", ctypes.c_uint32),
                ("setcode", ctypes.c_uint16 * 16), ("type", ctypes.c_uint32),
                ("level", ctypes.c_uint32), ("attribute", ctypes.c_uint32),
                ("race", ctypes.c_uint32), ("attack", ctypes.c_int32),
                ("defense", ctypes.c_int32), ("lscale", ctypes.c_uint32),
                ("rscale", ctypes.c_uint32), ("link_marker", ctypes.c_uint32)]


def cards_from_databases(paths):
    cards = {}
    for path in paths:
        with closing(sqlite3.connect(path.resolve().as_uri() + "?mode=ro&immutable=1", uri=True)) as connection:
            for row in connection.execute("SELECT id,alias,setcode,type,atk,def,level,race,attribute FROM datas"):
                code, alias, setcode, kind, attack, defense, packed_level, race, attribute = row
                value = CardData()
                value.code, value.alias = code, 13331639 if code == 6218705 else alias
                for index in range(4):
                    value.setcode[index] = (setcode >> (16 * index)) & 0xffff
                value.type, value.level = kind, packed_level & 0xff
                value.attribute, value.race = attribute, race
                value.attack, value.defense = attack, defense
                value.lscale, value.rscale = (packed_level >> 24) & 0xff, (packed_level >> 16) & 0xff
                value.link_marker = defense
                cards[code] = value
    return cards


def worker(args):
    if not sys.platform.startswith("linux"):
        raise RuntimeError("Native Puzzle initialization requires Linux")
    if ctypes.sizeof(CardData) != 76:
        raise RuntimeError("Unexpected native card-data layout")
    root = args.root.resolve()
    cards = cards_from_databases([root / "Data/locales/en-US/cards.cdb", *args.expansion_cdb])
    library = ctypes.CDLL(str(args.library.resolve() if args.library else root / "Assets/Plugins/Linux/libocgcore.so"))

    def native(name, result, arguments):
        function = getattr(library, name)
        function.restype, function.argtypes = result, arguments
        return function

    script_type = ctypes.CFUNCTYPE(ctypes.c_void_p, ctypes.c_char_p, ctypes.POINTER(ctypes.c_int32))
    card_type = ctypes.CFUNCTYPE(ctypes.c_uint32, ctypes.c_uint32, ctypes.POINTER(CardData))
    message_type = ctypes.CFUNCTYPE(ctypes.c_uint32, ctypes.c_void_p, ctypes.c_uint32)
    set_script = native("set_script_reader", None, [script_type])
    set_card = native("set_card_reader", None, [card_type])
    set_message = native("set_message_handler", None, [message_type])
    create = native("create_duel", ctypes.c_void_p, [ctypes.c_uint32])
    end = native("end_duel", None, [ctypes.c_void_p])
    set_player = native("set_player_info", None, [ctypes.c_void_p, ctypes.c_int32, ctypes.c_int32, ctypes.c_int32, ctypes.c_int32])
    preload = native("preload_script", ctypes.c_int32, [ctypes.c_void_p, ctypes.c_char_p])
    start = native("start_duel", None, [ctypes.c_void_p, ctypes.c_uint32])
    count = native("query_field_count", ctypes.c_int32, [ctypes.c_void_p, ctypes.c_uint8, ctypes.c_uint8])
    get_message = native("get_message", ctypes.c_int32, [ctypes.c_void_p, ctypes.c_void_p])
    get_log = native("get_log_message", None, [ctypes.c_void_p, ctypes.c_void_p])
    archives = [zipfile.ZipFile(root / "Data/script.zip"), *(zipfile.ZipFile(p) for p in args.script_archive)]
    buffers, reads, card_reads, missing_scripts, missing_cards, callback_errors, logs = {}, [], [], set(), set(), [], []

    @script_type
    def read_script(raw_name, length):
        try:
            name = raw_name.decode("utf-8").replace("\\", "/")
            while name.startswith("./"):
                name = name[2:]
            if name.startswith("/") or ".." in PurePosixPath(name).parts:
                raise ValueError("Unexpected native script path: " + name)
            reads.append(name)
            content = None
            if name.startswith("Puzzle/"):
                content = (root / name).read_bytes()
            else:
                for directory in reversed(args.script_dir):
                    for candidate in (directory / name, directory / PurePosixPath(name).name):
                        if candidate.is_file():
                            content = candidate.read_bytes()
                            break
                    if content is not None:
                        break
                if content is None:
                    for archive in reversed(archives):
                        try:
                            content = archive.read(name)
                            break
                        except KeyError:
                            continue
            if content is None:
                missing_scripts.add(name)
                length[0] = 0
                return None
            buffer = ctypes.create_string_buffer(content)
            buffers[name] = buffer
            length[0] = len(content)
            return ctypes.addressof(buffer)
        except Exception as error:
            callback_errors.append("script: " + str(error))
            length[0] = 0
            return None

    @card_type
    def read_card(code, output):
        try:
            card_reads.append(code)
            if code not in cards:
                missing_cards.add(code)
                ctypes.memset(output, 0, ctypes.sizeof(CardData))
                return 0
            ctypes.memmove(output, ctypes.byref(cards[code]), ctypes.sizeof(CardData))
            return code
        except Exception as error:
            callback_errors.append("card: " + str(error))
            return 0

    @message_type
    def message(duel, kind):
        try:
            buffer = ctypes.create_string_buffer(4096)
            get_log(duel, buffer)
            logs.append({"type": kind, "message": buffer.value.decode("utf-8", "replace")})
        except Exception as error:
            callback_errors.append("message: " + str(error))
        return 0

    set_script(read_script)
    set_card(read_card)
    set_message(message)
    duel = None
    result = {"path": args.worker, "status": "FAIL"}
    try:
        duel = create(5489)
        if not duel:
            raise RuntimeError("create_duel returned a null handle")
        for player in range(2):
            set_player(duel, player, 8000, 5, 1)
        loaded = preload(duel, args.worker.encode("utf-8"))
        fields = {str(player): {str(location): count(duel, player, location)
                               for location in (1, 2, 4, 8, 16, 32, 64)} for player in range(2)}
        total_cards = sum(sum(locations.values()) for locations in fields.values())
        start(duel, 0)
        messages = ctypes.create_string_buffer(1024 * 1024)
        message_bytes = get_message(duel, messages)
        required_helpers = {"script/constant.lua", "script/utility.lua", "script/procedure.lua"}
        helper_loads = required_helpers <= set(reads)
        failures = [entry for entry in logs if entry["type"] == 1]
        passed = (loaded == 1 and total_cards > 0 and card_reads and helper_loads and
                  not failures and not callback_errors and not missing_scripts and not missing_cards)
        result.update(status="PASS" if passed else "FAIL", preloadReturn=loaded, fieldCards=total_cards,
                      fieldCounts=fields, queuedMessageBytes=message_bytes, helpersLoaded=helper_loads,
                      scriptReads=len(reads), cardReads=len(card_reads), missingScripts=sorted(missing_scripts),
                      missingCards=sorted(missing_cards), callbackErrors=callback_errors, nativeLogs=logs)
    finally:
        if duel:
            end(duel)
        for archive in archives:
            archive.close()
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--library", type=Path, help="Optional native core build to validate instead of the bundled binary")
    parser.add_argument("--puzzle", help="Run one manifest path instead of every bundled Puzzle")
    parser.add_argument("--expansion-cdb", type=Path, action="append", default=[])
    parser.add_argument("--script-dir", type=Path, action="append", default=[])
    parser.add_argument("--script-archive", type=Path, action="append", default=[])
    parser.add_argument("--report", type=Path, help="Save the full current-run JSON report")
    parser.add_argument("--worker", help=argparse.SUPPRESS)
    args = parser.parse_args()
    if args.worker:
        result = worker(args)
        print(json.dumps(result, ensure_ascii=False))
        return 0 if result["status"] == "PASS" else 1
    root = args.root.resolve()
    manifest = json.loads((root / "Tools/RuntimeData/puzzle-source.json").read_text())
    cards = cards_from_databases([root / "Data/locales/en-US/cards.cdb", *args.expansion_cdb])
    results = []
    for entry in manifest["files"]:
        path = entry["path"]
        if args.puzzle and path != args.puzzle:
            continue
        content = (root / path).read_bytes()
        if hashlib.sha256(content).hexdigest() != entry["sha256"]:
            raise ValueError("Original Puzzle checksum differs: " + path)
        absent = sorted({int(value) for value in LITERAL_CARD.findall(content.decode("utf-8-sig"))} - cards.keys())
        if absent:
            results.append({"path": path, "status": "SKIP", "missingCards": absent})
            print("SKIP " + path + ": missing card data " + ", ".join(map(str, absent)))
            continue
        command = [sys.executable, str(Path(__file__).resolve()), "--root", str(root), "--worker", path]
        if args.library:
            command.extend(["--library", str(args.library.resolve())])
        for option, paths in (("--expansion-cdb", args.expansion_cdb), ("--script-dir", args.script_dir),
                              ("--script-archive", args.script_archive)):
            for source in paths:
                command.extend([option, str(source.resolve())])
        try:
            completed = subprocess.run(command, cwd=root, text=True, capture_output=True, timeout=20)
            try:
                result = json.loads(completed.stdout)
            except json.JSONDecodeError:
                result = {"path": path, "status": "FAIL", "error": "Native worker did not return JSON",
                          "exitCode": completed.returncode, "stdout": completed.stdout, "stderr": completed.stderr}
            if completed.returncode and result.get("status") == "PASS":
                result.update(status="FAIL", error="Native worker exited unsuccessfully", exitCode=completed.returncode)
        except subprocess.TimeoutExpired:
            result = {"path": path, "status": "FAIL", "error": "Native worker exceeded 20 seconds"}
        results.append(result)
        print(result["status"] + " " + path)
        if result["status"] == "FAIL":
            print(json.dumps(result, ensure_ascii=False))
    if not results:
        raise ValueError("No original Puzzle selected")
    counts = {status: sum(result["status"] == status for result in results) for status in ("PASS", "FAIL", "SKIP")}
    report = {"test": "NativePuzzleInitialization", "status": "PASS" if counts["FAIL"] == 0 and counts["PASS"] > 0 else "FAIL",
              "counts": counts, "scope": "Real Linux core, CDB and Lua setup/card initial effects; no puzzle solution or Unity execution",
              "coreSha256": hashlib.sha256((args.library.resolve() if args.library else root / "Assets/Plugins/Linux/libocgcore.so").read_bytes()).hexdigest(),
              "scriptZipSha256": hashlib.sha256((root / "Data/script.zip").read_bytes()).hexdigest(), "results": results}
    if args.report:
        args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps({key: value for key, value in report.items() if key != "results"}, ensure_ascii=False))
    return 0 if report["status"] == "PASS" else 1


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, RuntimeError, sqlite3.Error, zipfile.BadZipFile) as error:
        print("FAIL: " + str(error), file=sys.stderr)
        raise SystemExit(1)
