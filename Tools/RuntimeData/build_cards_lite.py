#!/usr/bin/env python3
"""Build the compact CID/YDK mapping used by the game from verified YGOCDB data."""
import argparse
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]


def read_mapping(path):
    raw = path.read_text(encoding="utf-8-sig")
    try:
        value = json.loads(raw)
    except json.JSONDecodeError:
        # Newtonsoft accepts the final trailing comma in the upstream cards_Alt.json.
        value = json.loads(re.sub(r",\s*}(\s*)$", r"}\1", raw))
    if not isinstance(value, dict):
        raise ValueError("Expected a CID-keyed object: " + str(path))
    result = {}
    for key, item in value.items():
        if not isinstance(item, dict):
            raise ValueError("Invalid mapping: " + key)
        cid, ydk = item.get("cid"), item.get("id")
        if type(cid) is not int or type(ydk) is not int or cid <= 0 or ydk < 0 or key != str(cid):
            raise ValueError("Invalid CID/YDK pair: " + key)
        result[key] = {"cid": cid, "id": ydk}
    return result


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True, help="cards.json extracted from https://ygocdb.com/api/v0/cards.zip")
    parser.add_argument("--source-archive", type=Path, help="Optional original cards.zip for provenance")
    parser.add_argument("--alt", type=Path, default=ROOT / "Data/cards_Alt.json")
    parser.add_argument("--output", type=Path, default=ROOT / "Data/cards_Lite.json")
    parser.add_argument("--manifest", type=Path, default=ROOT / "Tools/RuntimeData/cards-source.json")
    args = parser.parse_args()
    mappings = read_mapping(args.source)
    source_count = len(mappings)
    overrides = read_mapping(args.alt)
    mappings.update(overrides)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(mappings, separators=(",", ":")) + "\n", encoding="utf-8")
    report = {
        "sourceUrl": "https://ygocdb.com/api/v0/cards.zip",
        "sourceJsonSha256": sha(args.source),
        "alternateMappingsPath": "Data/cards_Alt.json",
        "alternateMappingsSha256": sha(args.alt),
        "outputPath": "Data/cards_Lite.json",
        "outputSha256": sha(args.output),
        "sourceEntries": source_count,
        "alternateEntries": len(overrides),
        "mappingEntries": len(mappings),
        "fieldsRetained": ["cid", "id"],
    }
    if args.source_archive:
        report["sourceArchiveSha256"] = sha(args.source_archive)
    args.manifest.parent.mkdir(parents=True, exist_ok=True)
    args.manifest.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
