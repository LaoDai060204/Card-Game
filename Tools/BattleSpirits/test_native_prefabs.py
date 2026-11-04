"""Structural safety checks for changing the actual original screen prefabs.

These tests inspect Unity serialization. They do not claim an Editor/render test.
"""
import re
import unittest

import migrate_native_prefabs as migration


class NativePrefabReplacementTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.script_paths = migration.scripts()
        cls.removed_by_guid = {}
        cls.original = {}
        cls.migrated = {}
        cls.removed = {}
        for path in migration.PATHS:
            source = migration.original(path, migration.BASE)
            actual = (migration.ROOT / path).read_text()
            expected, removed, actions = migration.migrate(path, source, cls.script_paths, cls.removed_by_guid)
            cls.original[path] = source
            cls.migrated[path] = actual
            cls.removed[path] = removed
            if actual != expected:
                raise AssertionError("Native migration drift: " + path)

    def test_original_asset_guids_are_unchanged(self):
        for path in migration.PATHS:
            with self.subTest(path=path):
                before = migration.original(path + ".meta", migration.BASE)
                after = (migration.ROOT / (path + ".meta")).read_text()
                self.assertEqual(before, after)

    def test_original_hierarchy_geometry_and_art_references_are_preserved(self):
        for path in migration.PATHS:
            with self.subTest(path=path):
                migration.verify_visuals(path, self.original[path], self.migrated[path], self.removed[path])

    def test_five_original_screens_use_their_native_controller_mode(self):
        for name, screen in migration.SCREENS.items():
            with self.subTest(screen=name):
                path = "Assets/Prefabs/ServantUI/" + name + ".prefab"
                controllers = [block for block in migration.blocks(self.migrated[path])
                               if migration.CONTROLLER in block.body]
                self.assertEqual(len(controllers), 1)
                self.assertIn("  screen: " + str(screen) + "\n", controllers[0].body)

    def test_legacy_rule_and_data_behaviours_are_absent(self):
        for path in migration.PATHS:
            screen = migration.Path(path).stem
            for block in migration.blocks(self.migrated[path]):
                script = migration.SCRIPT.search(block.body)
                if not script or script["guid"] == migration.CONTROLLER:
                    continue
                with self.subTest(path=path, component=block.id):
                    self.assertEqual(migration.action_for(script["guid"], self.script_paths, screen), "keep")

    def test_old_persistent_event_callbacks_are_cleared(self):
        for path in migration.PATHS:
            with self.subTest(path=path):
                self.assertNotIn("m_MethodName:", self.migrated[path])
                self.assertNotIn("m_TargetAssemblyTypeName:", self.migrated[path])

    def test_deleted_component_references_and_nested_overrides_are_absent(self):
        for path in migration.PATHS:
            with self.subTest(path=path):
                references = set(re.findall(r"\{fileID: (-?\d+)\}", self.migrated[path]))
                self.assertFalse(references.intersection(self.removed[path]))
                for target, guid in re.findall(r"- target: \{fileID: (-?\d+), guid: ([0-9a-f]+), type: \d+\}", self.migrated[path]):
                    self.assertNotIn(target, self.removed_by_guid.get(guid, set()))

    def test_original_nested_prefab_addresses_are_retained(self):
        path = "Assets/Prefabs/ServantUI/DeckEditorUI.prefab"
        pattern = r"m_SourcePrefab: \{fileID: -?\d+, guid: ([0-9a-f]+), type: \d+\}"
        self.assertEqual(re.findall(pattern, self.original[path]), re.findall(pattern, self.migrated[path]))
        self.assertEqual(len(re.findall(pattern, self.migrated[path])), 3)

    def test_legacy_source_is_rejected_as_the_replacement(self):
        # The baseline still has old controllers and non-empty callbacks. It is
        # deliberately not accepted simply because its look matches the form.
        for name in migration.SCREENS:
            path = "Assets/Prefabs/ServantUI/" + name + ".prefab"
            self.assertNotIn(migration.CONTROLLER, self.original[path])
            self.assertNotEqual(self.original[path], self.migrated[path])

    def test_callback_clearing_preserves_following_serialized_fields(self):
        source = "  click:\n    m_PersistentCalls:\n      m_Calls:\n      - m_Target: {fileID: 20}\n        m_MethodName: OldGame\n  m_Sprite: {fileID: 99}\n"
        self.assertEqual(migration.clear_calls(source), "  click:\n    m_PersistentCalls:\n      m_Calls: []\n  m_Sprite: {fileID: 99}\n")


if __name__ == "__main__":
    unittest.main()
