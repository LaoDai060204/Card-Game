#!/usr/bin/env python3
"""Restore reviewed template UI art without restoring old game controllers.

The source is the saved 0.2.0 template. Only a graphical reference that still
equals its explicit 0.3.0 replacement may be restored, keyed by file, serialized
component ID, property and occurrence. Fonts, gameplay assets and audio stay out.
"""
from __future__ import annotations

import argparse
from collections import defaultdict
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / "Documentation/original-ui-style-restoration.json"
SOURCE = "c70747a026ff558a80ddb7f0b6705be549f5d0aa"
GRAPHICS = {"m_Sprite", "m_Texture", "m_Material", "m_HighlightedSprite",
            "m_PressedSprite", "m_SelectedSprite", "m_DisabledSprite",
            "m_DefaultValue", "m_MobileValue", "value", "objectReference"}
BLOCK = re.compile(r"(?m)^--- !u!\d+ &(-?\d+)[^\n]*\n")
FIELD = re.compile(r"(?m)^(\s*)(\w+): (\{fileID: -?\d+(?:, guid: [0-9a-f]{32}, type: \d+)?\})[ \t]*$")
GUID = re.compile(r"[0-9a-f]{32}")
SCREEN_NAMES = {"MainMenuUI", "MateViewerUI", "SettingServantUI", "DeckEditorUI",
                "SoloSelectorUI", "OnlineServantUI", "RoomServantUI", "OcgCoreUI"}


def source_file(path):
    return subprocess.check_output(["git", "show", SOURCE + ":" + path], cwd=ROOT)


def ui_document(path):
    if path in {"Assets/Scenes/Boot.unity", "Assets/Scenes/Main.unity"}: return True
    if path.startswith("Assets/Animation/") and path.endswith(".controller"): return True
    if path.startswith(("Assets/Prefabs/Popup/", "Assets/Prefabs/Templates/")): return True
    if path.startswith("Assets/Prefabs/ServantUI/"):
        return Path(path).stem in SCREEN_NAMES or path in {
            "Assets/Prefabs/ServantUI/Parts/DeckView.prefab",
            "Assets/Prefabs/ServantUI/Parts/CardDetailView.prefab",
            "Assets/Prefabs/ServantUI/Parts/CardActionMenu.prefab"}
    return path in {"Assets/Prefabs/UI/ItemMate.prefab", "Assets/Prefabs/UI/MessageCast.prefab",
                    "Assets/Prefabs/UI/MessageToast.prefab"}


def direct_art(path):
    # Game-specific monster/type/rank/action glyphs are not a BS skin. In
    # particular the old AUTO icon must not reappear behind GIỮ 4 LÁ.
    return path.startswith(("Assets/Texture/GUI/", "Assets/Texture/GUI_Used/")) and not any(
        marker in path for marker in ("Icon_Attribute", "Icon_Rarity", "Icon_SpellTrap"))


def graphical_dependency(path):
    return path.startswith(("Assets/Texture/", "Assets/Materials/", "Assets/Shaders/",
                            "Assets/AssetBundles/")) and not path.endswith(".cs")


def component_fields(text):
    matches = list(BLOCK.finditer(text))
    for index, block in enumerate(matches):
        end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
        counts = defaultdict(int)
        for match in FIELD.finditer(text, block.end(), end):
            name = match[2]
            occurrence = counts[name]; counts[name] += 1
            yield (block[1], name, occurrence), match


def restore_reviewed_references(text, path, manifest=None):
    if manifest is None:
        if not MANIFEST.is_file(): return text
        manifest = json.loads(MANIFEST.read_text())
    records = {(item["componentId"], item["property"], item["occurrence"]): item
               for item in manifest["restoredReferences"] if item["path"] == path}
    edits = []
    for key, match in component_fields(text):
        item = records.get(key)
        if item is not None and match[3] == item["before"]:
            edits.append((match.start(3), match.end(3), item["after"]))
    for start, end, value in reversed(edits): text = text[:start] + value + text[end:]
    return text


def plan():
    from replace_master_duel_art import load_manifest, reskin_text
    cleanup = load_manifest()
    removed = {row["guid"]: row for row in cleanup["removedAssets"] if row["guid"]}
    references = []; selected = set()
    for path in cleanup["rewrittenSerializedFiles"]:
        if not ui_document(path) or not (ROOT / path).is_file(): continue
        original = source_file(path).decode("utf-8-sig")
        current = (ROOT / path).read_text()
        actual = {key: match[3] for key, match in component_fields(current)}
        for key, match in component_fields(original):
            if key[1] not in GRAPHICS: continue
            guids = set(GUID.findall(match[3]))
            if len(guids) != 1: continue
            guid = guids.pop(); asset = removed.get(guid)
            if asset is None or not direct_art(asset["path"]): continue
            replaced = reskin_text(match[3], cleanup)
            if actual.get(key) != replaced or replaced == match[3]: continue
            references.append({"path": path, "componentId": key[0], "property": key[1],
                               "occurrence": key[2], "before": replaced, "after": match[3]})
            selected.add(guid)
    pending = list(selected); restored = {}; data = {}
    while pending:
        guid = pending.pop()
        if guid in restored: continue
        record = removed[guid]; path = record["path"]
        if not graphical_dependency(path):
            raise ValueError("Non-graphical dependency requires review: " + path)
        payload = source_file(path); meta = source_file(path + ".meta")
        data[path] = payload; data[path + ".meta"] = meta
        restored[guid] = {"path": path, "guid": guid, "bytes": len(payload),
                          "sha256": hashlib.sha256(payload).hexdigest(),
                          "metaSha256": hashlib.sha256(meta).hexdigest()}
        text = meta.decode("utf-8-sig", errors="replace")
        if Path(path).suffix.lower() in {".asset", ".mat", ".shader", ".shadergraph", ".shadersubgraph", ".hlsl"}:
            text += payload.decode("utf-8-sig", errors="replace")
        for dependency in set(GUID.findall(text)) & removed.keys():
            if dependency != guid and dependency not in restored: pending.append(dependency)
    records = sorted(restored.values(), key=lambda item: item["path"])
    result = {"schemaVersion": 1, "date": "2026-11-06", "sourceCommit": SOURCE,
              "baseCommit": "035abd48947bb6a7bd4461a6cfda91e585dc359a",
              "purpose": "Restore original shared GUI chrome and palette while retaining Battle Spirits controllers, cards and gameplay",
              "sourceFontRestored": False, "legacyDuelAssetsRestored": False,
              "restoredAssetCount": len(records), "restoredAssetBytes": sum(item["bytes"] for item in records),
              "restoredReferenceCount": len(references), "restoredAssets": records,
              "restoredReferences": references, "unityEditorRenderingTested": False}
    result["originalPaletteReferences"] = {
        "menuHighlight": {"rgba": [0.7529412, 1, 0, 1], "source": "MainMenuUI.prefab"},
        "charcoalPanel": {"rgba": [0.15294118, 0.15294118, 0.1764706, 1], "source": "DeckEditorUI.prefab"},
        "opponentRed": {"rgba": [0.6117647, 0.09411765, 0.09411765, 1], "source": "OcgCoreUI.prefab"},
        "selectionYellow": {"rgba": [1, 1, 0, 1], "source": "OcgCoreUI.prefab"}}
    return result, data


def apply(manifest, data):
    # Never overwrite an independently edited resource, even when it has the
    # same path as a resource in the old template.
    for path, content in data.items():
        target = ROOT / path
        if target.exists() and target.read_bytes() != content:
            raise ValueError("Existing resource differs from reviewed original: " + path)
    for path, content in data.items():
        target = ROOT / path; target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(content)
    changed = []
    for path in sorted({item["path"] for item in manifest["restoredReferences"]}):
        target = ROOT / path; before = target.read_text()
        after = restore_reviewed_references(before, path, manifest)
        if before != after: target.write_text(after); changed.append(path)
    manifest["rewrittenSerializedFiles"] = sorted(set(manifest.get("rewrittenSerializedFiles", [])) | set(changed))
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")


def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    if MANIFEST.is_file():
        manifest = json.loads(MANIFEST.read_text())
        data = {}
        for item in manifest["restoredAssets"]:
            for path in [item["path"], item["path"] + ".meta"]: data[path] = source_file(path)
    else: manifest, data = plan()
    if args.apply: apply(manifest, data)
    print(json.dumps({"apply": args.apply, "assets": manifest["restoredAssetCount"],
                      "bytes": manifest["restoredAssetBytes"], "references": manifest["restoredReferenceCount"],
                      "files": len({item["path"] for item in manifest["restoredReferences"]})}, indent=2))


if __name__ == "__main__": main()
