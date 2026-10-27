#!/usr/bin/env python3
"""Check packaged data, provenance and import references without Unity."""
from pathlib import Path
import hashlib
import json
import re
import struct
import sys

ROOT = Path(__file__).resolve().parents[1]


def load(relative):
    return json.loads((ROOT / relative).read_text(encoding="utf-8"))


def require(condition, message):
    if not condition:
        raise ValueError(message)


def main():
    version = (ROOT / "ProjectSettings/ProjectVersion.txt").read_text()
    require("6000.6.7f1" in version and "b22767f04f04" in version, "Editor version mismatch")
    manifest = load("Packages/manifest.json")
    verified = load("Documentation/versions.json")
    for package, expected in verified["packages"].items():
        require(manifest["dependencies"].get(package) == expected, "Package mismatch: " + package)

    base = "Assets/BattleSpirits/Resources/"
    cards = load(base + "Data/cards.json")["cards"]
    source = {c["id"]: c for c in load("Documentation/official-card-sources.json")}
    images = {c["id"]: c for c in load(base + "CardArt/SOURCES.json")["sources"]}
    require(len(cards) >= 14, "Not enough distinct names for a 40-card deck")
    require(len({c["id"] for c in cards}) == len(cards), "Duplicate card ID")
    require(len({c["nameJa"] for c in cards}) == len(cards), "Duplicate card name")
    colors = {"Red": 0, "Purple": 1, "Green": 2, "White": 3, "Yellow": 4, "Blue": 5}
    require(set(source) == set(images) == {c["id"] for c in cards}, "Catalog/source/art sets differ")
    for card in cards:
        card_id = card["id"]
        original = source[card_id]
        for key in ("id", "nameJa", "cost", "effectTextJa", "sourceUrl", "imageUrl"):
            require(card[key] == original[key], f"{card_id}: source mismatch in {key}")
        require(card["implemented"] and card["effectTextJa"] == "-", card_id + ": unsupported effect")
        require(card["color"] == colors[original["color"]], card_id + ": wrong color")
        require(card["reductionSymbols"] == sum(x["count"] for x in original["reductions"]), card_id + ": reduction mismatch")
        require(card["symbols"] == sum(x["count"] for x in original["symbols"]), card_id + ": symbol mismatch")
        expected_levels = [{"cores": x["minCores"], "bp": x["bp"]} for x in original["levels"]]
        require(card["levels"] == expected_levels, card_id + ": level/BP mismatch")
        require(all(a["cores"] < b["cores"] for a, b in zip(expected_levels, expected_levels[1:])), card_id + ": invalid levels")
        png = (ROOT / base / "CardArt" / (card_id + ".png")).read_bytes()
        require(png[:8] == b"\x89PNG\r\n\x1a\n", card_id + ": invalid PNG signature")
        require(struct.unpack(">II", png[16:24]) == (675, 983), card_id + ": unexpected art dimensions")
        require(hashlib.sha256(png).hexdigest() == images[card_id]["convertedPngSha256"], card_id + ": art checksum mismatch")

    # Verify scene script references against actual, stable asset GUIDs.
    guids = {}
    for meta in (ROOT / "Assets").rglob("*.meta"):
        match = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(), re.M)
        require(match is not None, "Missing GUID: " + str(meta))
        guid = match.group(1)
        require(guid not in guids, "Duplicate GUID: " + str(meta))
        guids[guid] = meta
    scenes = list((ROOT / "Assets/BattleSpirits/Scenes").glob("*.unity"))
    require(bool(scenes), "No startup scene")
    for scene in scenes:
        for guid in re.findall(r"m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32}), type: 3\}", scene.read_text()):
            require(guid in guids, "Missing scene script GUID: " + guid)

    model = ROOT / base / "Models/SummonedDragon.fbx"
    require(model.is_file() and model.stat().st_size > 10000, "Missing summoned model")
    require((ROOT / "Tools/Art/SummonedDragon.blend").is_file(), "Missing editable model source")
    require((ROOT / base / "UI/Fonts/NotoSansJP.otf").stat().st_size > 1000000, "Missing UI font")
    print(json.dumps({"status": "PASS", "cards": len(cards), "verified_art_images": len(images),
                      "editor": "6000.6.7f1", "scenes": len(scenes), "asset_guids": len(guids),
                      "scope": "Data and import references; not Unity Play/build validation"}, indent=2))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, OSError) as error:
        print("FAIL: " + str(error), file=sys.stderr)
        raise SystemExit(1)
