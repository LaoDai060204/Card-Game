"""Resource closure and import-object tests; these do not render Unity UI."""
import hashlib
import json
from pathlib import Path
import re
import struct
import unittest

import replace_master_duel_art as cleanup


class UiArtCleanupTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.manifest = cleanup.load_manifest()
        cls.removed = {row["guid"] for row in cls.manifest["removedAssets"] if row["guid"]}

    def test_removed_assets_and_their_metadata_are_absent(self):
        for item in self.manifest["removedAssets"]:
            path = cleanup.ROOT / item["path"]
            self.assertFalse(path.exists(), item["path"])
            self.assertFalse(Path(str(path) + ".meta").exists(), item["path"])

    def test_directory_structure_and_folder_guids_survive(self):
        for folder in self.manifest["preservedFolders"]:
            path = cleanup.ROOT / folder
            self.assertTrue(path.is_dir(), folder)
            self.assertTrue(Path(str(path) + ".meta").is_file(), folder)
            self.assertTrue(any(path.iterdir()), "Missing .gitkeep: " + folder)

    def test_retained_cosmetics_and_generic_font_are_outside_removal_scope(self):
        for path in ("Assets/Addressables/Icon/Mate/1000001.png",
                     "Assets/Addressables/Icon/Wallpaper/WallPaperIcon1130001.png",
                     "Assets/Addressables/Icon/SpriteAtlas_Mate.spriteatlas",
                     "Assets/Addressables/Icon/SpriteAtlas_Wallpaper.spriteatlas",
                     "Assets/Addressables/Sound/SE_MATE/example.wav",
                     "Assets/Fonts/SourceHanSansSC-Medium.asset"):
            self.assertFalse(cleanup.removable(path), path)
        self.assertTrue((cleanup.ROOT / "Assets/Fonts/SourceHanSansSC-Medium.asset").is_file())
        for directory in ("Mate", "Wallpaper"):
            self.assertTrue(any((cleanup.ROOT / "Assets/Addressables/Icon" / directory).glob("*.png")))

    def test_surviving_unity_serialization_has_no_deleted_art_guid(self):
        for path in (cleanup.ROOT / "Assets").rglob("*"):
            if not path.is_file() or path.suffix not in cleanup.YAML_SUFFIXES:
                continue
            text = path.read_text(encoding="utf-8-sig", errors="replace")
            refs = {match[2] for match in cleanup.REF.finditer(text)}
            self.assertFalse(refs.intersection(self.removed), str(path))

    def test_addressable_entries_do_not_point_to_deleted_assets(self):
        for path in (cleanup.ROOT / "Assets/AddressableAssetsData/AssetGroups").glob("*.asset"):
            guids = set(re.findall(r"(?m)^  - m_GUID: ([0-9a-f]{32})$", path.read_text()))
            self.assertFalse(guids.intersection(self.removed), str(path))

    def test_original_ui_pngs_and_import_guids_match_manifest(self):
        for name, item in self.manifest["originalUiSkin"].items():
            path = cleanup.ROOT / item["path"]
            data = path.read_bytes()
            self.assertEqual(data[:8], b"\x89PNG\r\n\x1a\n")
            self.assertEqual(struct.unpack(">II", data[16:24]), (64, 64))
            self.assertEqual(hashlib.sha256(data).hexdigest(), item["sha256"])
            self.assertIn("guid: " + cleanup.skin_guid(name), Path(str(path) + ".meta").read_text())

    def test_sprite_and_raw_texture_use_correct_importer_subobjects(self):
        manifest = {"removedAssets": [{"guid": "a" * 32, "replacement": "clear"}]}
        for identifier, expected in (("21300000", "21300000"), ("2800000", "2800000"), ("8400000", "2800000")):
            value = "{fileID: " + identifier + ", guid: " + "a" * 32 + ", type: 3}"
            self.assertIn("fileID: " + expected, cleanup.reskin_text(value, manifest))

    def test_font_asset_and_embedded_material_are_mapped_separately(self):
        manifest = {"removedAssets": [{"guid": "b" * 32, "replacement": "font"}]}
        value = "{fileID: 11400000, guid: " + "b" * 32 + ", type: 2}"
        self.assertEqual(cleanup.reskin_text(value, manifest), "{fileID: 11400000, guid: " + cleanup.FONT + ", type: 2}")
        material = value.replace("11400000", "2100000")
        self.assertIn("fileID: " + cleanup.FONT_MATERIAL, cleanup.reskin_text(material, manifest))

    def test_group_pruning_keeps_the_other_entry_and_following_fields(self):
        deleted, kept = "c" * 32, "d" * 32
        text = "  m_SerializeEntries:\n  - m_GUID: " + deleted + "\n    m_Address: old\n  - m_GUID: " + kept + "\n    m_Address: pet\n  m_SchemaSet: []\n"
        actual = cleanup.prune_group(text, {deleted})
        self.assertNotIn(deleted, actual)
        self.assertIn(kept, actual)
        self.assertIn("m_SchemaSet: []", actual)


if __name__ == "__main__": unittest.main()
