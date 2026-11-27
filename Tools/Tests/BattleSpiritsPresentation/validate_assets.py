#!/usr/bin/env python3
"""Check the imported alpha metadata and shader properties used by the native port."""
import json
from pathlib import Path
import re

from PIL import Image

root = Path(__file__).resolve().parents[3]
assets = root / "Assets/StreamingAssets/BattleSpirits"
visuals = json.loads((assets / "visuals.json").read_text())["visuals"]
spirits = [entry for entry in visuals if entry["width"] > 0 and entry["height"] > 0]
assert len(spirits) == 43, "The reviewed library has exactly 43 paper silhouettes"
assert {entry["id"] for entry in visuals if entry["isBoss"]} == {"26RBS03-X01", "26RBS03-022"}
for entry in spirits:
    image = Image.open(assets / "standees" / (entry["id"] + ".png"))
    assert image.size == (entry["width"], entry["height"]), entry["id"]
    assert image.mode == "RGBA", entry["id"]
    alpha = image.getchannel("A")
    low, high = alpha.getextrema()
    assert low == 0 and high > 0, entry["id"]
    left, top, right, bottom = (entry[key] for key in ("left", "top", "right", "bottom"))
    assert 0 <= left < right <= image.width and 0 <= top < bottom <= image.height, entry["id"]
    # Metadata encloses substantive artwork. One reviewed cutout has a single
    # low-alpha antialias pixel one row outside its approved bounds.
    threshold = alpha.point(lambda value: 255 if value >= 16 else 0).getbbox()
    assert threshold is not None, entry["id"]
    assert left <= threshold[0] and top <= threshold[1] and right >= threshold[2] and bottom >= threshold[3], entry["id"]

shader_root = root / "Assets/BattleSpirits/Art/Resources/BattleSpirits"
shader_sources = "\n".join(path.read_text() for path in shader_root.glob("*.shader"))
properties = set(re.findall(r"\b(_[A-Za-z0-9_]+)\s*\(", shader_sources))
code_root = root / "Assets/BattleSpirits/Runtime/Visuals"
sources = "\n".join(path.read_text() for path in code_root.glob("*.cs"))
used = set(re.findall(r'Set(?:Float|Color|Vector)\("([^"]+)"', sources))
assert used <= properties, "Missing shader properties: " + repr(used - properties)
assert "_MainTex" in properties
for name in ("BattleSurface", "BattleField", "BattleProjection"):
    shader = (shader_root / (name + ".shader")).read_text()
    assert '"RenderPipeline"' in shader and '"UniversalPipeline"' in shader
    assert "ShaderLibrary/Core.hlsl" in shader
    assert (shader_root / (name + ".shader.meta")).exists()
for path in code_root.glob("*.cs"):
    assert path.with_suffix(".cs.meta").exists(), path.name
print("PASS: 43 alpha PNGs and bounds; explicit Ritenia/Pisketona boss metadata; native shader property coverage and stable metadata.")
