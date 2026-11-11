"""Boot scene coverage invariants; this does not run Unity or verify GPU pixels."""
from pathlib import Path
import re
import unittest


ROOT = Path(__file__).resolve().parents[3]
HEADER = re.compile(r"(?m)^--- !u!(\d+) &(-?\d+)[^\n]*\n")
TEXT_SCRIPT = "5f7201a12d95ffc409449d95f23cf332"
BOOT_SCRIPT = "4aa6c67e1b765844bb92f318dfb584a7"


def scene_objects():
    source = (ROOT / "Assets/Scenes/Boot.unity").read_text()
    headers = list(HEADER.finditer(source))
    return {
        int(header[2]): (int(header[1]), source[header.end():headers[index + 1].start()
                            if index + 1 < len(headers) else len(source)])
        for index, header in enumerate(headers)
    }


def field(block, name):
    match = re.search(r"(?m)^  " + re.escape(name) + r": (.*)$", block)
    if not match:
        raise AssertionError("Missing Unity field: " + name)
    return match[1]


class BootSceneTests(unittest.TestCase):
    def setUp(self):
        self.objects = scene_objects()

    def test_loading_has_one_canvas_one_status_text_and_one_boot_owner(self):
        canvases = [body for kind, body in self.objects.values() if kind == 223]
        texts = [body for kind, body in self.objects.values() if kind == 114 and TEXT_SCRIPT in body]
        owners = [body for kind, body in self.objects.values() if kind == 114 and BOOT_SCRIPT in body]
        self.assertEqual(len(canvases), 1)
        self.assertEqual(len(texts), 1)
        self.assertEqual(len(owners), 1)
        status_id = int(re.search(r"fileID: (\d+)", field(owners[0], "text"))[1])
        self.assertEqual(self.objects[status_id][1], texts[0])

    def test_backdrop_covers_the_canvas_and_draws_before_loading_controls(self):
        canvas_rect = self.objects[1651159589][1]
        children = re.search(r"(?m)^  m_Children:\n((?:  - .*\n)+)", canvas_rect)[1]
        self.assertEqual(int(re.search(r"fileID: (\d+)", children)[1]), 750831838)
        backdrop = self.objects[750831838][1]
        self.assertEqual(field(backdrop, "m_Father"), "{fileID: 1651159589}")
        for name in ("m_AnchorMin", "m_AnchoredPosition", "m_SizeDelta"):
            self.assertEqual(field(backdrop, name), "{x: 0, y: 0}")
        self.assertEqual(field(backdrop, "m_AnchorMax"), "{x: 1, y: 1}")
        self.assertEqual(field(backdrop, "m_LocalScale"), "{x: 1, y: 1, z: 1}")

    def test_backdrop_is_opaque_without_a_transparent_sprite_or_custom_material(self):
        # A transparent replacement texture defeats alpha=1 on the Image and
        # can leave previous status/dot glyphs uncleared in a camera-less scene.
        image = self.objects[750831839][1]
        self.assertEqual(field(image, "m_Color"), "{r: 0, g: 0, b: 0, a: 1}")
        self.assertEqual(field(image, "m_Sprite"), "{fileID: 0}")
        self.assertEqual(field(image, "m_Material"), "{fileID: 0}")
        self.assertEqual(field(image, "m_Type"), "0")
        self.assertEqual(field(image, "m_RaycastTarget"), "1")

    def test_curtain_can_block_pointer_input_until_main_is_ready(self):
        raycaster = self.objects[1651159586][1]
        self.assertIn("guid: dc42784cf147c0c48a680349fa168899", raycaster)
        self.assertEqual(field(raycaster, "m_GameObject"), "{fileID: 1651159585}")
        self.assertEqual(field(raycaster, "m_Enabled"), "1")


if __name__ == "__main__":
    unittest.main()
