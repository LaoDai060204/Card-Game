"""Read the actual Canvas/camera/servant hierarchy instead of inventing one."""
from pathlib import Path
import re


def validate(root: Path):
    def documents(path):
        source = path.read_text(encoding="utf-8-sig")
        return {int(match[2]): (int(match[1]), match[3]) for match in re.finditer(
            r"^--- !u!(\d+) &(\d+)(?: stripped)?\n(.*?)(?=^---|\Z)", source, re.M | re.S)}

    def named(docs, name):
        matches = [key for key, (kind, body) in docs.items() if kind == 1
                   and re.search(r"^  m_Name: " + re.escape(name) + r"$", body, re.M)]
        assert len(matches) == 1, (name, matches)
        return matches[0]

    def component(docs, game, kind):
        matches = [key for key, (tag, body) in docs.items() if tag == kind
                   and re.search(r"m_GameObject: \{fileID: " + str(game) + r"\}", body)]
        assert len(matches) == 1, (game, kind, matches)
        return matches[0]

    def reference(body, name):
        return int(re.search(r"^  " + re.escape(name) + r": \{fileID: (\d+)\}", body, re.M)[1])

    scene = documents(root / "Assets/Scenes/Main.unity")
    ui = named(scene, "UI")
    canvas = scene[component(scene, ui, 223)][1]
    ui_rect = component(scene, ui, 224)
    assert "m_Layer: 5" in scene[ui][1] and "m_RenderMode: 1" in canvas
    camera_id = reference(canvas, "m_Camera")
    assert scene[camera_id][0] == 20
    camera_mask = int(re.search(r"m_CullingMask:\n    serializedVersion: 2\n    m_Bits: (\d+)", scene[camera_id][1])[1])
    assert camera_mask == (1 << 5) | (1 << 30), "The actual UI camera renders UI layers 5 and 30."
    program = named(scene, "Program")
    program_transform = component(scene, program, 4)
    assert reference(scene[program_transform][1], "m_Father") == 0
    assert not any(kind == 223 and reference(body, "m_GameObject") == program for kind, body in scene.values())
    program_body = next(body for kind, body in scene.values() if kind == 114
                        and reference(body, "m_GameObject") == program and "  menu:" in body)
    menu_game = reference(scene[reference(program_body, "menu")][1], "m_GameObject")
    ocg_game = reference(scene[reference(program_body, "ocgcore")][1], "m_GameObject")
    menu_rect = component(scene, menu_game, 224)
    ocg_rect = component(scene, ocg_game, 224)
    assert reference(scene[menu_rect][1], "m_Father") == ui_rect
    assert reference(scene[ocg_rect][1], "m_Father") == ui_rect
    children_text = scene[ui_rect][1].split("  m_Children:\n", 1)[1].split("  m_Father:", 1)[0]
    children = [int(value) for value in re.findall(r"fileID: (\d+)", children_text)]
    assert children.index(ocg_rect) == 1
    backdrop = named(scene, "BlackBack")
    backdrop_rect = component(scene, backdrop, 224)
    assert reference(scene[backdrop_rect][1], "m_Father") == ui_rect and children.index(backdrop_rect) > children.index(ocg_rect)
    for token in ("m_AnchorMin: {x: 0, y: 0}", "m_AnchorMax: {x: 1, y: 1}", "m_SizeDelta: {x: 0, y: 0}"):
        assert token in scene[ocg_rect][1]

    prefab = documents(root / "Assets/Prefabs/ServantUI/OcgCoreUI.prefab")
    native_root = named(prefab, "Root")
    root_rect = prefab[component(prefab, native_root, 224)][1]
    assert "m_Layer: 5" in prefab[native_root][1] and "m_IsActive: 1" in prefab[native_root][1]
    for token in ("m_AnchorMin: {x: 0, y: 0}", "m_AnchorMax: {x: 1, y: 1}", "m_SizeDelta: {x: 0, y: 0}"):
        assert token in root_rect
    manager = (root / "Assets/Scripts/MDPro3/Managers/UIManager.cs").read_text(encoding="utf-8-sig")
    hide_backdrop = manager.split("public static void HideBlackBack(float time)", 1)[1].split("public static", 1)[0]
    assert "blackBack.raycastTarget = false" in hide_backdrop
    print("PASS actual Main scene Canvas/camera mask/Program separation/native servant order and OcgCoreUI Root contracts; no Unity rendering.")


if __name__ == "__main__":
    validate(Path(__file__).resolve().parents[3])
