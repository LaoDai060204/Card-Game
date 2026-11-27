#!/usr/bin/env python3
"""Exercise current native layout method bodies with real serialized prefab inputs."""
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[3]


def method(source, signature):
    begin = source.index(signature)
    brace = source.index("{", begin)
    depth, end = 1, brace + 1
    while depth:
        depth += (source[end] == "{") - (source[end] == "}")
        end += 1
    return source[begin:end]


class Prefab:
    def __init__(self, path):
        text = path.read_text(encoding="utf-8-sig")
        self.docs = {
            int(match[2]): (int(match[1]), match[3])
            for match in re.finditer(r"^--- !u!(\d+) &(\d+)(?: stripped)?\n(.*?)(?=^---|\Z)", text, re.M | re.S)
        }
        self.names = {
            key: re.search(r"^  m_Name: (.*)$", body, re.M)[1]
            for key, (kind, body) in self.docs.items() if kind == 1
        }
        self.rects = {
            key: body for key, (kind, body) in self.docs.items() if kind == 224
        }

    def name(self, rect):
        return self.names[int(re.search(r"m_GameObject: \{fileID: (\d+)\}", self.rects[rect])[1])]

    def rect(self, name, parent=None):
        found = [key for key in self.rects if self.name(key) == name
                 and (parent is None or self.name(self.parent(key)) == parent)]
        assert len(found) == 1, (name, parent, found)
        return found[0]

    def parent(self, rect):
        return int(re.search(r"m_Father: \{fileID: (\d+)\}", self.rects[rect])[1])

    def vector(self, rect, property_name):
        match = re.search(property_name + r": \{x: ([-.\d]+), y: ([-.\d]+)\}", self.rects[rect])
        return tuple(float(match[index]) for index in (1, 2))

    def size(self, rect):
        delta = self.vector(rect, "m_SizeDelta")
        parent = self.parent(rect)
        if parent == 0:
            return delta
        parent_size = self.size(parent)
        low, high = self.vector(rect, "m_AnchorMin"), self.vector(rect, "m_AnchorMax")
        return tuple(delta[index] + parent_size[index] * (high[index] - low[index]) for index in (0, 1))

    def components(self, rect):
        game = re.search(r"m_GameObject: \{fileID: (\d+)\}", self.rects[rect])[1]
        return [body for kind, body in self.docs.values() if kind == 114
                and re.search(r"m_GameObject: \{fileID: " + game + r"\}", body)]


inspector = Prefab(ROOT / "Assets/Prefabs/ServantUI/Parts/CardDetailView.prefab")
deck = Prefab(ROOT / "Assets/Prefabs/ServantUI/Parts/DeckView.prefab")
card_area = inspector.rect("CardArea")
main_grid = deck.rect("MainDeckContent")
template_root = deck.rect("Root", "Template")
scroll_rect = next(body for body in deck.components(deck.rect("Scroll View")) if "m_Content:" in body)
grid = next(body for body in deck.components(main_grid) if "m_CellSize:" in body)
cell = tuple(float(value) for value in re.search(r"m_CellSize: \{x: ([-.\d]+), y: ([-.\d]+)\}", grid).groups())
padding = re.search(r"m_Padding:\n    m_Left: (\d+)\n    m_Right: (\d+)\n    m_Top: (\d+)\n    m_Bottom: (\d+)", grid)
assert padding, "Read the real grid padding rather than inventing it in the fixture."
inputs = {
    "InspectorFace": inspector.size(card_area),
    "InspectorPanel": inspector.size(inspector.parent(card_area)),
    "DeckRoot": deck.size(deck.rect("DeckView")),
    "DeckViewport": deck.size(deck.rect("Viewport")),
    "DeckCell": cell,
    "TemplateRoot": deck.size(template_root),
    "GridPadding": tuple(int(value) for value in padding.groups()),
    "GridConstraint": (int(re.search(r"m_Constraint: (\d+)", grid)[1]), int(re.search(r"m_ConstraintCount: (\d+)", grid)[1])),
}
assert inputs["InspectorFace"] == (143, 208)
assert inputs["InspectorPanel"] == (412, 896)
assert inputs["DeckRoot"] == (1920, 1080)
assert inputs["DeckViewport"] == (788, 832)
assert inputs["DeckCell"] == inputs["TemplateRoot"] == (72, 101)
assert "m_Enabled: 0" in scroll_rect
assert "m_Content: {fileID: 0}" in scroll_rect and "m_Viewport: {fileID: 0}" in scroll_rect
assert "m_Constraint: 0" in grid
assert int((deck.size(main_grid)[0] + 4) // (cell[0] + 4)) == 10

inspector_source = (ROOT / "Assets/Scripts/MDPro3/UI/UIWidget/UIWidgetCardBase.cs").read_text(encoding="utf-8-sig")
deck_source = (ROOT / "Assets/Scripts/MDPro3/UI/UIWidget/Deck/DeckView.cs").read_text(encoding="utf-8-sig")
adapter_source = (ROOT / "Assets/BattleSpirits/Runtime/NativeAdapters/BattleCardAdapter.cs").read_text(encoding="utf-8-sig")
loader_source = (ROOT / "Assets/Scripts/MDPro3/Utility/CardImageLoader.cs").read_text(encoding="utf-8-sig")
raw_source = (ROOT / "Assets/Scripts/MDPro3/UI/Function/CardRawImageHandler.cs").read_text(encoding="utf-8-sig")
load_face = method(loader_source, "public static async UniTask<Texture2D> LoadBattleTexturePathAsync(")
assert "UnityWebRequestTexture.GetTexture(uri, true)" in load_face
assert "DownloadHandlerTexture.GetContent(request)" in load_face
assert "texture.filterMode = FilterMode.Bilinear" in load_face
assert "texture.wrapMode = TextureWrapMode.Clamp" in load_face
assert "texture.anisoLevel = 4" in load_face
assert not any(token in load_face for token in ("RenderTexture", "GetArtFrom", "Resize(", "Crop("))
load_card = method(raw_source, "private async UniTaskVoid LoadCardPicAsync()")
native_face = load_card[load_card.index("if (card.IsBattleSpirits)"):load_card.index("if (normalMat == null)")]
assert "BattleCardAdapter.FitFace(RawImage, face);" in native_face and "return;" in native_face
assert "MaterialLoader" not in native_face
native_inspector = inspector_source[inspector_source.index("if (data.IsBattleSpirits"):inspector_source.index("#region Title Area", inspector_source.index("if (data.IsBattleSpirits"))]
assert 'Manager.GetElement("ParameterArea")?.SetActive(false)' in native_inspector
assert "TextDescriptionValue.text = data.Desc" in native_inspector
assert "TextDescriptionValue.fontSize = DeckEditor.UseMobileLayout ? 30f : 24f" in native_inspector
assert "TextDescriptionValue.enableAutoSizing = false" in native_inspector
assert "ConfigureBattleDeckLayout();" in method(deck_source, "protected override void Awake()")
assert "RefreshBattleDeckLayout();" in method(deck_source, "protected virtual void ChangeGridSpacing(DeckLocation location)")
awake = method(deck_source, "protected override void Awake()")
assert awake.index("Template.transform.SetParent(transform, false)") < awake.index("ConfigureBattleDeckLayout();")
printed = method(deck_source, "protected IEnumerator PrintDeckAsync()")
assert printed.index("RefreshCardsCount(DeckLocation.All)") < printed.index("RefreshBattleDeckLayout();")
assert "ScrollRect.StopMovement();" in printed and "ScrollRect.verticalNormalizedPosition = 1f" in printed
card_source = (ROOT / "Assets/Scripts/MDPro3/UI/SelectionButton/SelectionButton_CardInDeck.cs").read_text(encoding="utf-8-sig")
card_root = re.search(r"internal RectTransform CardRoot => [^;]+;", card_source)[0]
assert "child = transform.GetChild(0).GetComponent<RectTransform>()" in card_source
info_source = (ROOT / "Assets/Scripts/MDPro3/UI/UIWidget/Deck/CardInfoDetail.cs").read_text(encoding="utf-8-sig")
native_info = info_source[info_source.index("if (data.IsBattleSpirits"):info_source.index("#region Card Area", info_source.index("if (data.IsBattleSpirits"))]
for area in ("ParamatorAreaTop", "ParamatorAreaBottom", "PlateParamator.gameObject", "PendulumDescriptionArea"):
    assert area + ".SetActive(false)" in native_info
assert "data.Desc" in native_info and "enableWordWrapping = true" in native_info and "richText = false" in native_info
aspect = re.search(r"public const float DEFAULT_ASPECT = ([^;]+);", loader_source)[1]

sdk = Path(os.environ.get("DOTNET_ROOT", "/tmp/codex-dotnet"))
version = sorted((sdk / "sdk").iterdir())[-1]
refs = sorted((sdk / "packs/Microsoft.NETCore.App.Ref").glob("*/ref/net10.0"))[-1]
runtime = sorted((sdk / "shared/Microsoft.NETCore.App").iterdir())[-1].name
with tempfile.TemporaryDirectory(prefix="bs-native-deck-layout-") as directory:
    work = Path(directory)
    extracted = work / "ProductionLayoutMethods.cs"
    deck_methods = [method(deck_source, signature) for signature in (
        "private void ConfigureBattleDeckLayout()", "private void LateUpdate()",
        "private void RefreshBattleDeckLayout()", "private float LayoutBattleDeckSection(",
        "public RectTransform GetDeckLocationParent(DeckLocation location)", "public int GetDeckLocationCount(DeckLocation location)")]
    extracted.write_text("using System;using System.Linq;using UnityEngine;using UnityEngine.UI;\n"
        + "public partial class InspectorFixture {\n" + method(inspector_source, "private void ConfigureBattleInspector()") + "\n}\n"
        + "public partial class DeckFixture {\n" + "\n".join(deck_methods) + "\n}\n"
        + "public static partial class FaceFixture {\n" + method(adapter_source, "public static void FitFace(RawImage image, Texture texture)") + "\n}\n"
        + "public sealed partial class CardFixture {" + card_root + "}\n"
        + "namespace MDPro3.Utility {public static class CardImageLoader { public const float DEFAULT_ASPECT = " + aspect + "; }}\n"
        + "public static class Serialized {\n" + "\n".join(
            "public static readonly float[] " + key + " = new float[] {" + ",".join(str(value) + "f" for value in values) + "};"
            for key, values in inputs.items()) + "\n}\n")
    target = work / "DeckLayoutTests.dll"
    options = ["-nologo", "-target:exe", "-langversion:9", '-out:"' + str(target) + '"']
    options += ['-r:"' + str(path) + '"' for path in refs.glob("*.dll")]
    options += ['"' + str(path) + '"' for path in [extracted, *sorted(Path(__file__).parent.glob("*.cs"))]]
    response = work / "compile.rsp"
    response.write_text("\n".join(options))
    env = dict(os.environ, DOTNET_CLI_HOME=str(work / "dotnet"), DOTNET_CLI_TELEMETRY_OPTOUT="1")
    subprocess.run([str(sdk / "dotnet"), "exec", str(version / "Roslyn/bincore/csc.dll"), "@" + str(response)], env=env, check=True)
    target.with_suffix(".runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {"tfm": "net10.0", "framework": {"name": "Microsoft.NETCore.App", "version": runtime}}}))
    subprocess.run([str(sdk / "dotnet"), "exec", str(target)], env=env, check=True)
print("PASS: real prefab hierarchy/dimensions and full-face loader/material source contracts; Unity rendering not run.")
