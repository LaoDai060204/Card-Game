"""Source-image contracts behind the duel's declared UI contrast tests.

These examine the reviewed PNG bytes. They do not render Unity's Image/TMP.
The native C# harness separately checks the actual controller's panel tint and
foreground contrast; treating the white mask as a dark background caused 0.6.4.
"""
import hashlib
import json
from pathlib import Path
import unittest

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
SKIN = ROOT / "Assets/BattleSpirits/Art/Resources/BattleSpirits/Skin"


class DuelStyleImageTests(unittest.TestCase):
    def test_panel_is_a_reviewed_white_alpha_mask_requiring_a_dark_tint(self):
        manifest = json.loads((ROOT / "Data/BattleSpirits/skin-sources.json").read_text())
        record = next(item for item in manifest["assets"] if item["name"] == "ChromePanel")
        path = SKIN / "ChromePanel.png"
        self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), record["sha256"])
        with Image.open(path) as source:
            image = source.convert("RGBA")
            self.assertEqual(image.size, (34, 34))
            self.assertEqual(image.getpixel((17, 17)), (255, 255, 255, 255))
            self.assertLess(image.getpixel((0, 0))[3], 255)
            opaque = [image.getpixel((x, y)) for y in range(image.height) for x in range(image.width)
                      if image.getpixel((x, y))[3] == 255]
            self.assertTrue(opaque and all(pixel[:3] == (255, 255, 255) for pixel in opaque))

    def test_button_has_a_dark_center_and_does_not_share_the_panel_tint_contract(self):
        with Image.open(SKIN / "ChromeButton.png") as source:
            image = source.convert("RGBA")
            center = image.getpixel((image.width // 2, image.height // 2))
            self.assertLess(max(center[:3]), 64)
            self.assertEqual(center[3], 255)
            self.assertTrue(any(max(image.getpixel((x, y))[:3]) > 128 and image.getpixel((x, y))[3] > 192
                                for y in range(image.height) for x in range(image.width)))


if __name__ == "__main__":
    unittest.main()
