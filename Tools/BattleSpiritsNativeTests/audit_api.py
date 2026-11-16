#!/usr/bin/env python3
"""Check native manager compatibility after replacing the former catalog implementation."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
manager_file = root / "Assets/Scripts/MDPro3/Duel/YGOSharp/CardsManager.cs"
cards, packs = manager_file.read_text().split("    internal static class PacksManager", 1)
declaration = re.compile(r"\b(?:public|internal)\s+static\s+[^\n{;]+?\b(\w+)\s*(?=\(|=|;)")
members = {"CardsManager": set(declaration.findall(cards)), "PacksManager": set(declaration.findall(packs))}
references = set()
missing = []
for path in sorted((root / "Assets").rglob("*.cs")):
    source = path.read_text(encoding="utf-8-sig")
    relative = path.relative_to(root).as_posix()
    # The original engine and bot retain separate YGOSharp manager types.
    if not (relative.startswith("Assets/Scripts/MDPro3/") or relative.startswith("Assets/BattleSpirits/")):
        source = re.sub(r"(?<!MDPro3\.Duel\.YGOSharp\.)\b(?:CardsManager|PacksManager)\.", "", source)
    for kind, name in re.findall(r"\b(CardsManager|PacksManager)\.(\w+)", source):
        references.add((kind, name))
        if name not in members[kind]:
            missing.append(f"{relative}: {kind}.{name}")
if missing:
    raise SystemExit("Missing native API references:\n" + "\n".join(missing))
print(f"PASS: {len(references)} referenced native manager members across Assets have retained APIs.")
print(", ".join(kind + "." + name for kind, name in sorted(references)))
