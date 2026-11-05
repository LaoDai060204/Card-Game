#!/usr/bin/env python3
"""Replace legacy game graphics with an original small UI skin, preserving layout.

Default mode reports the bounded cleanup. --apply requires a verified backup,
as documented in AGENTS.md. Pet/wallpaper assets and their generic fonts remain.
This tool edits Unity serialization, not gameplay or controller code.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import struct
import zlib

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / "Documentation/master-duel-art-replacement.json"
SKIN = "Assets/Texture/GUI_Used"
FONT = "4f2b4779da094154fa119234dbc5a2cb"
FONT_SOURCE = "cdf4c81f8bf6eb248a105d8a5edf19fd"
FONT_MATERIAL = "1463209681449585872"
REF = re.compile(r"\{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: \d+\}")
GUID = re.compile(r"(?m)^guid: ([0-9a-f]{32})$")
YAML_SUFFIXES = {".prefab", ".unity", ".asset", ".mat", ".controller", ".overrideController", ".anim", ".playable", ".spriteatlas", ".renderTexture"}


def skin_guid(name):
    return hashlib.md5(("LaoDai original UI/" + name).encode()).hexdigest()


def inside(path, directory):
    return path == directory or path.startswith(directory + "/")


def removable(path):
    if path.startswith(SKIN + "/BS_"):
        return False
    if inside(path, "Assets/Texture") or inside(path, "Assets/AssetBundles"):
        return True
    if inside(path, "Assets/Materials") or inside(path, "Assets/Shaders"):
        return True
    if inside(path, "Assets/Addressables/Icon"):
        return not any(inside(path, "Assets/Addressables/Icon/" + keep) for keep in ("Mate", "Wallpaper")) and Path(path).name not in {
            "SpriteAtlas_Mate.spriteatlas", "SpriteAtlas_Wallpaper.spriteatlas"}
    if inside(path, "Assets/Addressables/Sound"):
        return not inside(path, "Assets/Addressables/Sound/SE_MATE")
    if inside(path, "Assets/Fonts"):
        return not Path(path).name.startswith("SourceHanSansSC-")
    return False


def replacement_kind(path):
    if path.startswith("Assets/Fonts/"):
        if Path(path).suffix.lower() in {".ttf", ".otf"}: return "fontSource"
        if "Sprites_" in path: return "empty"
        if path.endswith(".mat"): return "fontMaterial"
        return "font"
    if Path(path).suffix.lower() in {".mat", ".shader", ".shadergraph", ".shadervariants"}:
        return "empty"
    if Path(path).suffix.lower() in {".png", ".jpg", ".jpeg", ".tga", ".psd", ".rendertexture"}:
        stem = Path(path).stem.lower()
        if any(word in stem for word in ("icon", "badge", "number", "attribute", "counter", "duelbutton", "arrow", "logo")):
            return "clear"
        if "button" in stem: return "button"
        if any(word in stem for word in ("frame", "window", "base", "mask", "title", "background", "panel")):
            return "panel"
        return "clear"
    return "empty"


def plan():
    records = []
    folders = set()
    for path in (ROOT / "Assets").rglob("*"):
        if path.is_dir():
            if removable(str(path.relative_to(ROOT))): folders.add(str(path.relative_to(ROOT)))
            continue
        if path.suffix == ".meta" or path.name == ".gitkeep": continue
        relative = str(path.relative_to(ROOT))
        if not removable(relative): continue
        metadata = Path(str(path) + ".meta")
        guid = GUID.search(metadata.read_text()) if metadata.is_file() else None
        records.append({"path": relative, "bytes": path.stat().st_size,
                        "guid": guid[1] if guid else None, "replacement": replacement_kind(relative)})
    records.sort(key=lambda item: item["path"])
    return {"schemaVersion": 1, "baseCommit": "c70747a026ff558a80ddb7f0b6705be549f5d0aa",
            "purpose": "Remove Master Duel/Yu-Gi-Oh graphics except pet/wallpaper, replace UI art in-place",
            "originalUiSkin": {name: {"path": SKIN + "/BS_" + name + ".png", "guid": skin_guid(name)} for name in ("panel", "button", "clear")},
            "preserved": ["Assets/Addressables/Icon/Mate", "Assets/Addressables/Icon/Wallpaper",
                          "Assets/Addressables/Sound/SE_MATE", "Assets/Fonts/SourceHanSansSC-*"],
            "removedAssetCount": len(records), "removedAssetBytes": sum(item["bytes"] for item in records),
            "removedPathListSha256": hashlib.sha256("\n".join(item["path"] for item in records).encode()).hexdigest(),
            "preservedFolders": sorted(folders), "removedAssets": records, "unityEditorRenderingTested": False}


def load_manifest():
    return json.loads(MANIFEST.read_text()) if MANIFEST.is_file() else None


def reskin_text(text, manifest=None):
    manifest = manifest or load_manifest()
    if not manifest: return text
    replacements = {item["guid"]: item["replacement"] for item in manifest["removedAssets"] if item["guid"]}

    def replace(match):
        fileid, guid = match[1], match[2]
        kind = replacements.get(guid)
        if kind is None: return match[0]
        if kind == "empty": return "{fileID: 0}"
        if kind == "fontSource": return "{fileID: 12800000, guid: " + FONT_SOURCE + ", type: 3}"
        if kind == "fontMaterial" or kind == "font" and fileid != "11400000":
            return "{fileID: " + FONT_MATERIAL + ", guid: " + FONT + ", type: 2}"
        if kind == "font": return "{fileID: 11400000, guid: " + FONT + ", type: 2}"
        # Image and RawImage reference different importer subobjects.
        sprite_id = "2800000" if fileid in {"2800000", "8400000"} else "21300000"
        return "{fileID: " + sprite_id + ", guid: " + skin_guid(kind) + ", type: 3}"

    return REF.sub(replace, text)


def prune_group(text, removed):
    return re.sub(r"(?ms)^  - m_GUID: ([0-9a-f]{32})\n.*?(?=^  - m_GUID:|^  m_\w|\Z)",
                  lambda m: "" if m[1] in removed else m[0], text)


def png(width, height, pixel):
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xffffffff)
    data = b"".join(b"\0" + b"".join(bytes(pixel(x, y)) for x in range(width)) for y in range(height))
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(data, 9)) + chunk(b"IEND", b"")


def write_skin():
    metadata = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  textureCompression: 0
  maxTextureSize: 64
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 6, y: 6, z: 6, w: 6}}
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 8
  textureShape: 1
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 64
    textureFormat: -1
    textureCompression: 0
    overridden: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID: {guid}
    internalID: 0
  userData: Original procedural UI by LaoDai060204 project; CC0-1.0
  assetBundleName:
  assetBundleVariant:
"""
    for name in ("panel", "button", "clear"):
        target = ROOT / SKIN / ("BS_" + name + ".png")
        def pixel(x, y):
            if name == "clear": return (255, 255, 255, 0)
            edge = min(x, y, 63 - x, 63 - y)
            if edge < 2: return (74, 211, 230, 255)
            if edge < 4: return (17, 60, 78, 255)
            shade = 35 if name == "button" else 22
            return (shade, shade + 16 + y // 8, shade + 25 + y // 8, 245)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(png(64, 64, pixel))
        Path(str(target) + ".meta").write_text(metadata.format(guid=skin_guid(name)))


def apply(manifest):
    removed_guids = {item["guid"] for item in manifest["removedAssets"] if item["guid"]}
    changed = []
    for item in manifest["removedAssets"]:
        path = ROOT / item["path"]
        if not removable(item["path"]): raise ValueError("Out-of-scope path: " + item["path"])
        path.unlink(missing_ok=True)
        Path(str(path) + ".meta").unlink(missing_ok=True)
    write_skin()
    for name, item in manifest["originalUiSkin"].items():
        item["sha256"] = hashlib.sha256((ROOT / item["path"]).read_bytes()).hexdigest()
        item["width"] = item["height"] = 64
        item["license"] = "CC0-1.0, original procedural UI art"
    for directory in manifest["preservedFolders"]:
        path = ROOT / directory
        path.mkdir(parents=True, exist_ok=True)
        if not any(child.is_file() and child.name != ".gitkeep" for child in path.iterdir()):
            (path / ".gitkeep").touch()
    for path in (ROOT / "Assets").rglob("*"):
        if not path.is_file() or path.suffix not in YAML_SUFFIXES: continue
        text = path.read_text(encoding="utf-8-sig", errors="replace")
        if not text.startswith("%YAML"): continue
        if inside(str(path.relative_to(ROOT)), "Assets/AddressableAssetsData/AssetGroups"):
            updated = prune_group(text, removed_guids)
        else:
            updated = reskin_text(text, manifest)
        if updated != text:
            path.write_text(updated, encoding="utf-8")
            changed.append(str(path.relative_to(ROOT)))
    manifest["rewrittenSerializedFiles"] = sorted(set(manifest.get("rewrittenSerializedFiles", [])) | set(changed))
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
    return changed


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    manifest = load_manifest() or plan()
    changed = apply(manifest) if args.apply else []
    print(json.dumps({"apply": args.apply, "removedAssets": manifest["removedAssetCount"],
                      "removedBytes": manifest["removedAssetBytes"], "serializedFilesRewritten": len(changed),
                      "preservedFolders": len(manifest["preservedFolders"]), "manifest": str(MANIFEST.relative_to(ROOT))}, indent=2))


if __name__ == "__main__": main()
