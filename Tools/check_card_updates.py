#!/usr/bin/env python3
"""Read official card pages and report changed stats/effects; never edit game data."""
from concurrent.futures import ThreadPoolExecutor
from html.parser import HTMLParser
from pathlib import Path
import argparse
import json
import re
import urllib.request

ROOT = Path(__file__).resolve().parents[1]


class Text(HTMLParser):
    def __init__(self):
        super().__init__()
        self.parts = []

    def handle_data(self, data):
        if data.strip():
            self.parts.append(data.strip())


def text(fragment):
    parser = Text()
    parser.feed(fragment)
    return " ".join(parser.parts)


def check(card):
    try:
        url = card["sourceUrl"]
        if not url.startswith("https://www.battlespirits.com/cardlist/detail_iframe.php?"):
            raise ValueError("Unexpected source host/path")
        request = urllib.request.Request(url, headers={"User-Agent": "BattleSpiritsPrototype-CardCheck/1.0"})
        with urllib.request.urlopen(request, timeout=20) as response:
            raw = response.read(512000).decode("utf-8")
        name = text(re.search(r'<h2 class="cardName">(.*?)</h2>', raw, re.S).group(1))
        cost = int(text(re.search(r'<span class="textCost">(.*?)</span>', raw, re.S).group(1)))
        blocks = re.findall(r"<dl\b.*?</dl>", raw, re.S)
        effect = text(next(x for x in blocks if "能力・効果" in x)).replace("能力・効果", "").strip()
        levels = []
        for _, bp, core in re.findall(r'bg_bpcore_lv(\d+)\.png.*?<span class="bpCoreVal">(.*?)</span>.*?<span class="bpCoreLevel">(.*?)</span>', raw, re.S):
            if bp.strip().isdigit() and core.strip().isdigit():
                levels.append({"cores": int(core), "bp": int(bp)})
        observed = {"nameJa": name, "cost": cost, "effectTextJa": effect, "levels": levels}
        changes = {key: {"bundled": card[key], "official": value} for key, value in observed.items() if value != card[key]}
        return {"id": card["id"], "status": "changed" if changes else "unchanged", "changes": changes}
    except Exception as error:
        return {"id": card["id"], "status": "error", "error": str(error)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--id", action="append", help="Check only this catalog card ID; repeatable")
    args = parser.parse_args()
    cards = json.loads((ROOT / "Assets/BattleSpirits/Resources/Data/cards.json").read_text())["cards"]
    if args.id:
        ids = set(args.id)
        known = {card["id"] for card in cards}
        if ids - known:
            parser.error("Unknown card ID: " + ", ".join(sorted(ids - known)))
        cards = [card for card in cards if card["id"] in ids]
    with ThreadPoolExecutor(max_workers=3) as executor:
        results = list(executor.map(check, cards))
    print(json.dumps({"results": results, "note": "Report only. Review rule/errata changes before changing implemented cards."}, ensure_ascii=False, indent=2))
    return 1 if any(result["status"] != "unchanged" for result in results) else 0


if __name__ == "__main__":
    raise SystemExit(main())
