#!/usr/bin/env python3
"""Read-only audit of published cutout provenance, original artwork and RGBA.

Requires Pillow for PNG decoding. Does not create or modify image pixels, and
does not prove semantic segmentation quality or a Unity shader/render result.
"""
import hashlib
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verify():
    manifest = json.loads((ROOT / "Data/BattleSpirits/standee-sources.json").read_text())
    cards = {row["id"]: row for row in json.loads((ROOT / "Data/BattleSpirits/cards.json").read_text())["cards"]}
    assets = manifest["assets"]
    ids = {row["id"] for row in assets}
    assert len(ids) == len(assets) == manifest["cutoutCount"]
    published = {path.stem for path in (ROOT / "Picture/Standee").glob("*.png")}
    assert ids == published, "Unrecorded or missing cutout PNG"
    playable = {card["id"] for card in cards.values()
                if card["implemented"] and card["format"] == "Standard" and card["type"] == "Spirit"}
    assert len(playable) == manifest["playableStandardSpirits"]
    assert sorted(playable - ids) == manifest["missingPlayableCutouts"]
    assert ids <= playable, "This release only publishes reviewed playable Standard cutouts"
    for row in assets:
        card = cards[row["id"]]
        assert row["path"] == "Picture/Standee/" + row["id"] + ".png"
        assert row["sourceArtPath"] == card["artPath"]
        assert row["sourceUrl"] == card["sourceUrl"] and row["nameJa"] == card["nameJa"]
        assert sha256(ROOT / row["sourceArtPath"]) == row["sourceArtSha256"], row["id"] + ": original changed"
        path = ROOT / row["path"]
        assert sha256(path) == row["sha256"], str(path)
        with Image.open(path) as image:
            assert image.mode == row["mode"] == "RGBA"
            assert image.size == (row["width"], row["height"])
            assert min(image.size) >= 512
            histogram = image.getchannel("A").histogram()
            total = image.width * image.height
            assert histogram[0] == row["transparentPixels"] > total * .1
            foreground = sum(histogram[manifest["alphaThreshold"]:])
            assert foreground == row["foregroundPixelsAtAlpha32"]
            assert total * .05 < foreground < total * .90, row["id"] + ": empty or opaque rectangle"
        assert row["pixelIdenticalExtraction"] is False and row["official3DModel"] is False
    return {"status": "PASS", "cutouts": len(assets), "playableStandardSpirits": len(playable),
            "missingPlayableCutouts": sorted(playable - ids), "originalAndCutoutHashes": True,
            "rgbaDimensionsAndAlpha": True, "unityRenderingTested": False}


if __name__ == "__main__":
    print(json.dumps(verify(), indent=2))
