#!/usr/bin/env python3
"""Verify the imported skin bytes, dimensions, resource names and Unity import settings."""
import hashlib
import json
from pathlib import Path
import re
import struct

ROOT = Path(__file__).resolve().parents[2]
DIRECTORY = "Assets/BattleSpirits/Art/Resources/BattleSpirits/Skin/"


def dimensions(data):
    if data.startswith(b"\x89PNG\r\n\x1a\n"):
        return struct.unpack(">II", data[16:24])
    if not data.startswith(b"\xff\xd8"):
        raise ValueError("Expected PNG or JPEG image")
    offset = 2
    while offset < len(data):
        if data[offset] != 0xFF:
            raise ValueError("Malformed JPEG marker")
        while data[offset] == 0xFF:
            offset += 1
        marker = data[offset]
        offset += 1
        if marker in (0xD8, 0xD9) or 0xD0 <= marker <= 0xD7:
            continue
        length = struct.unpack(">H", data[offset:offset + 2])[0]
        if marker in {0xC0, 0xC1, 0xC2, 0xC3, 0xC5, 0xC6, 0xC7, 0xC9, 0xCA, 0xCB, 0xCD, 0xCE, 0xCF}:
            height, width = struct.unpack(">HH", data[offset + 3:offset + 7])
            return width, height
        offset += length
    raise ValueError("JPEG dimensions are missing")


def verify():
    manifest = json.loads((ROOT / "Data/BattleSpirits/skin-sources.json").read_text())
    required = {"ColorRed", "ColorPurple", "ColorGreen", "ColorWhite", "ColorYellow", "ColorBlue",
                "CoreNormal", "SoulCore", "CardBack", "PlaymatStandard", "PlaymatReference",
                "ChromeButton", "ChromePanel", "ChromeButtonOver"}
    assets = manifest["assets"]
    assert {item["name"] for item in assets} == required and len(assets) == len(required)
    guids = set()
    for item in assets:
        relative = item["path"]
        assert relative.startswith(DIRECTORY) and ".." not in Path(relative).parts
        path = ROOT / relative
        assert path.stem == item["name"]
        assert item["resourcePath"] == "BattleSpirits/Skin/" + item["name"]
        data = path.read_bytes()
        assert hashlib.sha256(data).hexdigest() == item["sha256"], relative
        assert dimensions(data) == (item["width"], item["height"]), relative
        metadata = Path(str(path) + ".meta").read_text()
        guid = re.search(r"^guid: ([0-9a-f]{32})$", metadata, re.M)[1]
        assert guid not in guids, "Duplicate skin GUID"
        guids.add(guid)
        for setting in ("textureType: 0", "maxTextureSize: 2048", "textureCompression: 0", "enableMipMap: 0"):
            assert setting in metadata, relative + ": " + setting
        assert item["sourceUrl"].startswith("https://")
        if item["name"].startswith("Chrome"):
            assert (ROOT / item["sourcePath"]).read_bytes() == data
    return {"status": "PASS", "skinImages": len(assets), "uniqueGuids": len(guids),
            "hashesDimensionsAndImportSettings": True, "unityRenderingTested": False}


if __name__ == "__main__":
    print(json.dumps(verify(), indent=2))
