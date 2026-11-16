"""Shipped catalog, Unity metadata and preserved-layout consistency checks."""
from collections import Counter, defaultdict
import json
from pathlib import Path
import re
import subprocess


def validate(root: Path, allow_incomplete=False, base="2c7fd2ee7e5d2100eb85cccff7fb8418ec52439c"):
    findings = []

    def check(condition, message, severity="error"):
        if not condition:
            findings.append({"severity": severity, "check": "AssetConsistency", "message": message})

    stream = root / "Assets/StreamingAssets/BattleSpirits"
    source = root / "Data/BattleSpirits"
    cards = json.loads((stream / "cards.json").read_text())["cards"]
    decks = json.loads((stream / "starter-decks.json").read_text())["decks"]
    ids = json.loads((stream / "numeric-ids.json").read_text())["entries"]
    visuals = json.loads((stream / "visuals.json").read_text())["visuals"]
    by_id = {card["id"]: card for card in cards}
    check(len(cards) == len(by_id) == 62, "Catalog must contain 62 unique printed IDs.")
    check(len(ids) == len({item["numericId"] for item in ids}) == len({item["id"] for item in ids}) == 62, "Persistent numeric IDs must be unique and complete.")
    check({item["id"]: item["numericId"] for item in ids} == {card["id"]: card["numericId"] for card in cards}, "Catalog and persistent numeric map differ.")
    check((source / "numeric-ids.json").read_bytes() == (stream / "numeric-ids.json").read_bytes(), "Source and shipped numeric-ID maps differ.")
    unimplemented = [card["id"] for card in cards if not card.get("implemented")]
    check(not unimplemented, "Shipped cards are unavailable to the rules engine: " + ", ".join(unimplemented))
    for card in cards:
        check(card.get("symbolsVerified") and card.get("standardLegalityVerified") and card.get("standardLegal") and card.get("standardBlock") == "A", "Unverified Standard card or symbols: " + card["id"])
        image = (stream / card["artPath"]).resolve()
        check(image.is_relative_to(stream.resolve()) and image.is_file(), "Missing or escaping printed face: " + card["id"])
        check(bool(card.get("nameEn")) and bool(card.get("effectTextEn")), "Missing English card content: " + card["id"])
    check(len(decks) == 4, "The Solo roster requires four starter decks.")
    for deck in decks:
        check(len(deck["cards"]) == 40 and set(deck["cards"]) <= set(by_id), "Invalid starter deck size/content: " + deck["name"])
        for card_id, count in Counter(deck["cards"]).items():
            if card_id in by_id:
                check(count <= by_id[card_id].get("maxCopies", 3), "Printed copy limit exceeded: " + deck["name"] + "/" + card_id)
    check(len(visuals) == 62 and {item["id"] for item in visuals} == set(by_id), "Visual metadata and catalog coverage differ.")
    check([item["id"] for item in visuals if item.get("isBoss")] == ["26RBS03-X01"], "Only Ritenia has the boss cutscene flag.")
    check(sum(item.get("width", 0) > 0 for item in visuals) == 43, "Reviewed paper-actor silhouette count changed.")
    puzzles = list((source / "Puzzles").glob("*.json"))
    check(len(puzzles) >= 10, "At least ten Battle Spirits puzzle fixtures must ship.")
    for path in puzzles:
        target = stream / "Puzzles" / path.name
        check(target.exists() and target.read_bytes() == path.read_bytes(), "Source and shipped puzzle differ: " + path.name)

    changed = subprocess.check_output(["git", "diff", "--name-only", "--diff-filter=DMRT", base, "--", "Assets"], cwd=root, text=True).splitlines()
    preserved = [path for path in changed if Path(path).suffix in (".prefab", ".unity", ".meta")]
    check(not preserved, "Existing prefab/scene/meta files changed: " + ", ".join(preserved))
    required_meta = []
    for asset_root in (root / "Assets/BattleSpirits", stream):
        for path in [asset_root, *asset_root.rglob("*")]:
            if path.suffix != ".meta" and not Path(str(path) + ".meta").exists():
                required_meta.append(str(path.relative_to(root)))
    check(not required_meta, "Missing Unity metadata: " + ", ".join(required_meta), "warning" if allow_incomplete else "error")
    guid_paths = defaultdict(list)
    new_meta = set()
    tracked = set(subprocess.check_output(["git", "ls-tree", "-r", "--name-only", base, "--", "Assets"], cwd=root, text=True).splitlines())
    for path in (root / "Assets").rglob("*.meta"):
        relative = str(path.relative_to(root))
        text = path.read_text(errors="replace")
        match = re.search(r"^guid: ([0-9a-f]{32})$", text, re.MULTILINE)
        if relative not in tracked:
            new_meta.add(relative)
            check(match is not None, "Malformed new Unity GUID: " + relative)
        if match:
            guid_paths[match[1]].append(relative)
    for guid, paths in guid_paths.items():
        check(len(paths) == 1 or not new_meta.intersection(paths), "New Unity GUID collides with another asset: " + guid + " / " + ", ".join(paths))
    return {"catalogCards": len(cards), "starterDecks": len(decks), "puzzleFixtures": len(puzzles), "newUnityMetadataFiles": len(new_meta), "findings": findings}
