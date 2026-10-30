#!/usr/bin/env python3
"""Check Unity metadata and portable source-project wiring for the conversion."""
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]


def require(condition, message):
    if not condition:
        raise ValueError(message)


def validate():
    version = (ROOT / "ProjectSettings/ProjectVersion.txt").read_text()
    require("m_EditorVersion: 6000.0.24f1\n" in version, "The conversion must retain Unity 6000.0.24f1")
    require((ROOT / "Assets/Scenes/Boot.unity").is_file(), "Boot scene is missing")

    assets = ROOT / "Assets/BattleSpirits"
    new_guids = {}
    for path in [assets, *assets.rglob("*")]:
        if path.suffix == ".meta":
            continue
        metadata = Path(str(path) + ".meta")
        require(metadata.is_file(), f"Missing Unity metadata: {path.relative_to(ROOT)}")
        match = re.search(r"^guid: ([0-9a-f]{32})$", metadata.read_text(), re.M)
        require(match is not None, f"Invalid Unity GUID: {metadata.relative_to(ROOT)}")
        require(match[1] not in new_guids, f"Duplicate conversion GUID: {match[1]}")
        new_guids[match[1]] = metadata
    for path in (ROOT / "Assets").rglob("*.meta"):
        if path in new_guids.values():
            continue
        match = re.search(r"^guid: ([0-9a-f]{32})$", path.read_text(encoding="utf-8-sig", errors="replace"), re.M)
        require(not match or match[1] not in new_guids, f"Conversion GUID collides with {path.relative_to(ROOT)}")

    projects = list((ROOT / "Tests/BattleSpirits").rglob("*.csproj"))
    projects += list((ROOT / "Tools/BattleSpiritsServer").rglob("*.csproj"))
    projects = [p for p in projects if not {"bin", "obj", "Library"}.intersection(p.relative_to(ROOT).parts)]
    require(projects, "Standalone test/server projects are missing")
    for path in projects:
        xml = ET.parse(path).getroot()
        for reference in xml.iter("ProjectReference"):
            target = reference.attrib["Include"]
            require(not Path(target).is_absolute(), f"Absolute project reference in {path.relative_to(ROOT)}")
            require((path.parent / target).is_file(), f"Missing project reference: {path.relative_to(ROOT)} -> {target}")
        require("/workspace/" not in path.read_text(), f"Machine-specific source path in {path.relative_to(ROOT)}")

    core = json.loads((assets / "Runtime/Core/LaoDai.BattleSpirits.Core.asmdef").read_text())
    network = json.loads((assets / "Runtime/Networking/LaoDai.BattleSpirits.Networking.asmdef").read_text())
    require(core["noEngineReferences"] and network["noEngineReferences"], "Rules/network must remain independent of Unity")
    require(core["name"] in network["references"], "Network assembly is missing its rules reference")
    require("Unity.Newtonsoft.Json" in network["references"], "Network assembly is missing its JSON reference")

    catalog = json.loads((ROOT / "Data/BattleSpirits/cards.json").read_text())
    ids = [card["id"] for card in catalog["cards"]]
    require(len(ids) == len(set(ids)), "Duplicate official card IDs")
    require(all((ROOT / card["artPath"]).is_file() for card in catalog["cards"]), "A catalog artwork file is missing")
    return {"status": "PASS", "editor": "6000.0.24f1", "conversionAssetGuids": len(new_guids),
            "portableSourceProjects": len(projects), "catalogCards": len(ids),
            "windowsUnityPlayTested": False}


if __name__ == "__main__":
    try:
        print(json.dumps(validate(), indent=2))
    except (ValueError, OSError, KeyError, ET.ParseError) as error:
        raise SystemExit("FAIL: " + str(error))
