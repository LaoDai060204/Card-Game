#!/usr/bin/env python3
"""Replace Yu-Gi-Oh controllers in the original screen assets without rebuilding their layout.

The original prefab GUIDs, GameObjects, transforms, sprites, fonts and material
references are retained. Run with --write only after preserving the current
working version as required by AGENTS.md. Default mode verifies the migration.
"""
from __future__ import annotations

import argparse
from collections import Counter
from dataclasses import dataclass
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
BASE = "9d1d1055ed1d7cf996ead3b5f8ef0d46ca5a5466"
CONTROLLER = "a5a4f7790f184bf893839293bd793101"
BASE_BUTTON = "3f88580cb54a6d247b0379b8f582fa90"
SCREENS = {"DeckEditorUI": 0, "SoloSelectorUI": 1, "OnlineServantUI": 2,
           "RoomServantUI": 3, "OcgCoreUI": 4}
PARTS = ("DeckView", "CardDetailView", "CardActionMenu")
PATHS = tuple("Assets/Prefabs/ServantUI/Parts/" + name + ".prefab" for name in PARTS) + tuple(
    "Assets/Prefabs/ServantUI/" + name + ".prefab" for name in SCREENS)
BLOCK = re.compile(r"(?ms)^(?P<header>--- !u!(?P<kind>\d+) &(?P<id>-?\d+)[^\n]*\n)(?P<body>.*?)(?=^---|\Z)")
SCRIPT = re.compile(r"m_Script: \{fileID: -?\d+, guid: (?P<guid>[0-9a-f]+), type: \d+\}")


@dataclass
class Block:
    kind: str
    id: str
    header: str
    body: str


def blocks(source):
    return [Block(match["kind"], match["id"], match["header"], match["body"]) for match in BLOCK.finditer(source)]


def require(condition, message):
    if not condition:
        raise ValueError(message)


def original(path, base):
    return subprocess.check_output(["git", "show", base + ":" + path], cwd=ROOT).decode("utf-8")


def guid_for(path):
    return re.search(r"^guid: ([0-9a-f]+)$", Path(str(ROOT / path) + ".meta").read_text(), re.M)[1]


def scripts():
    result = {}
    for path in (ROOT / "Assets").rglob("*.cs.meta"):
        match = re.search(r"^guid: ([0-9a-f]+)$", path.read_text(errors="replace"), re.M)
        if match:
            result[match[1]] = str(path.relative_to(ROOT))[:-5]
    return result


def action_for(guid, paths, screen):
    path = paths.get(guid, "")
    if path == "Assets/Scripts/MDPro3/UI/ServantUI/" + screen + ".cs":
        return "replace-controller"
    # Package scripts not residing in Assets are UGUI/TMP/layout components.
    if not path or not path.startswith("Assets/Scripts/MDPro3/"):
        return "keep"
    if path.startswith("Assets/Scripts/MDPro3/UI/SelectionButton/"):
        if Path(path).stem in {"SelectionButton", "SelectionInputField", "SelectionToggle"}:
            return "keep"
        return "generic-button"
    if path.startswith("Assets/Scripts/MDPro3/UI/UIExtensions/"):
        return "keep"
    if path.startswith("Assets/Scripts/MDPro3/UI/Tween/"):
        return "keep"
    if path in {"Assets/Scripts/MDPro3/UI/New UI/UIHover.cs",
                "Assets/Scripts/MDPro3/UI/New UI/UIEventWithAudio.cs",
                "Assets/Scripts/MDPro3/UI/New UI/ButtonHold.cs"}:
        return "keep"
    return "remove"


def clear_calls(body):
    lines = body.splitlines(keepends=True)
    result = []
    index = 0
    while index < len(lines):
        line = lines[index]
        if line.strip() == "m_PersistentCalls:":
            indent = len(line) - len(line.lstrip(" "))
            result.append(line)
            result.append(" " * (indent + 2) + "m_Calls: []\n")
            index += 1
            while index < len(lines):
                current = lines[index]
                if current.strip() and len(current) - len(current.lstrip(" ")) <= indent:
                    break
                index += 1
        else:
            result.append(line)
            index += 1
    return "".join(result)


def remove_dead_overrides(body, removed_by_guid):
    # Prefab variants can override controller fields. Those entries must not
    # target components removed from the nested original Parts assets.
    lines = body.splitlines(keepends=True)
    result = []
    index = 0
    while index < len(lines):
        match = re.match(r"^(\s*)- target: \{fileID: (-?\d+), guid: ([0-9a-f]+), type: \d+\}", lines[index])
        if match and match[2] in removed_by_guid.get(match[3], set()):
            indent = len(match[1])
            index += 1
            while index < len(lines):
                current = lines[index]
                current_indent = len(current) - len(current.lstrip(" "))
                if current.strip() and (current_indent < indent or (current_indent == indent and current.lstrip().startswith("- "))):
                    break
                index += 1
        else:
            result.append(lines[index])
            index += 1
    return "".join(result)


def migrate(path, source, script_paths, removed_by_guid):
    screen = Path(path).stem
    objects = blocks(source)
    removed = set()
    actions = Counter()
    controller_count = 0
    for block in objects:
        script = SCRIPT.search(block.body)
        corresponding = re.search(r"m_CorrespondingSourceObject: \{fileID: (-?\d+), guid: ([0-9a-f]+), type: \d+\}", block.body)
        if corresponding and corresponding[1] in removed_by_guid.get(corresponding[2], set()):
            removed.add(block.id)
            actions["removed-stripped"] += 1
            continue
        if not script:
            continue
        action = action_for(script["guid"], script_paths, screen)
        if action == "remove":
            removed.add(block.id)
            actions["removed-behaviour"] += 1
        elif action == "generic-button":
            block.body = block.body[:script.start("guid")] + BASE_BUTTON + block.body[script.end("guid"):]
            actions["generic-buttons"] += 1
        elif action == "replace-controller":
            controller_count += 1
            block.body = block.body[:script.start("guid")] + CONTROLLER + block.body[script.end("guid"):]
            block.body += "  screen: " + str(SCREENS[screen]) + "\n"
            actions["native-controller"] += 1
    require(controller_count == (1 if screen in SCREENS else 0), "Unexpected screen controller count: " + path)
    output = source[:source.index(objects[0].header)]
    for block in objects:
        if block.id in removed:
            continue
        body = clear_calls(block.body)
        if block.kind == "1":
            body = re.sub(r"(?m)^  - component: \{fileID: (-?\d+)\}\n", lambda m: "" if m[1] in removed else m[0], body)
        body = re.sub(r"\{fileID: (-?\d+)\}", lambda m: "{fileID: 0}" if m[1] in removed else m[0], body)
        if block.kind == "1001":
            body = remove_dead_overrides(body, removed_by_guid)
        output += block.header + body
    removed_by_guid[guid_for(path)] = removed
    return output, removed, dict(actions)


def path_rows(source):
    parsed = {block.id: block for block in blocks(source)}
    names = {}
    transforms = {}
    for block in parsed.values():
        name = re.search(r"^  m_Name: (.*)$", block.body, re.M)
        if block.kind == "1" and name:
            names[block.id] = name[1].strip().strip("\"'")
        owner = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", block.body)
        parent = re.search(r"m_Father: \{fileID: (-?\d+)\}", block.body)
        if block.kind in {"4", "224"} and owner and parent:
            transforms[block.id] = (owner[1], parent[1])
    result = []
    for transform_id, (owner, parent) in transforms.items():
        if owner not in names:
            continue
        pieces = [names[owner]]
        seen = set()
        while parent in transforms and parent not in seen:
            seen.add(parent)
            ancestor, parent = transforms[parent]
            pieces.insert(0, names.get(ancestor, "unknown"))
        result.append({"path": "/".join(pieces), "gameObject": owner, "transform": transform_id})
    return result


def resolved_paths(source, by_guid):
    rows = path_rows(source)
    parents = {row["transform"]: row["path"] for row in rows}
    for block in blocks(source):
        if block.kind != "1001":
            continue
        prefab = re.search(r"m_SourcePrefab: \{fileID: -?\d+, guid: ([0-9a-f]+), type: \d+\}", block.body)
        parent = re.search(r"m_TransformParent: \{fileID: (-?\d+)\}", block.body)
        if not prefab or not parent or prefab[1] not in by_guid:
            continue
        require(parent[1] in parents, "Unknown parent for original nested prefab")
        nested_path, nested_source = by_guid[prefab[1]]
        for nested in path_rows(nested_source):
            rows.append({**nested, "path": parents[parent[1]] + "/" + nested["path"], "sourcePrefab": nested_path})
    return rows


def verify_visuals(path, source, migrated, removed):
    before = {block.id: block for block in blocks(source)}
    after = {block.id: block for block in blocks(migrated)}
    # All transforms, mesh/UI geometry and animation data remain byte-identical.
    for identifier, block in before.items():
        if block.kind in {"4", "224", "222", "212", "33", "23", "95", "96"}:
            require(identifier in after and after[identifier].body == block.body, "Changed original geometry: " + path + ":" + identifier)
        if block.kind == "1":
            require(identifier in after, "Removed original GameObject: " + path)
            clean = re.sub(r"(?ms)^  m_Component:\n(?:  - component: .*\n)*", "", block.body)
            actual = re.sub(r"(?ms)^  m_Component:\n(?:  - component: .*\n)*", "", after[identifier].body)
            require(clean == actual, "Changed original GameObject appearance/activation: " + path + ":" + identifier)
    for block in after.values():
        local_references = set(re.findall(r"\{fileID: (-?\d+)\}", block.body))
        require(not local_references.intersection(removed), "Dangling removed component reference in " + path)
        for match in re.finditer(r"m_PersistentCalls:\n\s+m_Calls:([^\n]*)", block.body):
            require(match[1].strip() == "[]", "Legacy persistent callback survived in " + path)
    asset_values = r"(?m)^\s*(?:m_Sprite|m_FontAsset|m_fontAsset|m_Material|m_sharedMaterial|m_Texture|m_Font|m_fontMaterial):.*$"
    require(re.findall(asset_values, source) == re.findall(asset_values, migrated), "Changed original sprite/font/material references: " + path)


def execute(write, base):
    script_paths = scripts()
    removed_by_guid = {}
    reports = []
    paths_manifest = {}
    migrated_by_guid = {}
    for path in PATHS:
        source = original(path, base)
        expected, removed, actions = migrate(path, source, script_paths, removed_by_guid)
        verify_visuals(path, source, expected, removed)
        target = ROOT / path
        if write:
            with target.open("w", encoding="utf-8", newline="") as output:
                output.write(expected)
        actual = target.read_text()
        require(actual == expected, "Prefab differs from verified replacement: " + path)
        reports.append({"path": path, "guid": guid_for(path), "actions": actions,
                        "originalSha256": hashlib.sha256(source.encode()).hexdigest(),
                        "migratedSha256": hashlib.sha256(actual.encode()).hexdigest(),
                        "gameObjects": len(path_rows(actual))})
        paths_manifest[path] = resolved_paths(actual, migrated_by_guid)
        migrated_by_guid[guid_for(path)] = (path, actual)
    return {"status": "PASS", "base": base, "nativeControllerGuid": CONTROLLER,
            "prefabs": reports, "originalLayoutPreserved": True,
            "originalSpritesFontsMaterialsPreserved": True,
            "legacyCallbacksCleared": True, "unityEditorPlayTested": False}, paths_manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true")
    parser.add_argument("--base", default=BASE)
    parser.add_argument("--report", type=Path)
    parser.add_argument("--paths", type=Path)
    args = parser.parse_args()
    report, paths = execute(args.write, args.base)
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    if args.paths:
        args.paths.parent.mkdir(parents=True, exist_ok=True)
        args.paths.write_text(json.dumps(paths, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        raise SystemExit("FAIL: " + str(error))
