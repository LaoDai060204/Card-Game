#!/usr/bin/env python3
"""Validate English lookup keys, resource relationships and migration invariants.

With --base, also compare immutable data, locale values, Unity GUID/fileID
references and serialized structure against the saved commit. Requires PyYAML
for Unity text-structure comparison. This does not run Unity or its shader compiler.
"""
import argparse
import collections
import csv
import hashlib
import io
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
HAN = re.compile(r"[\u3400-\u9fff\uf900-\ufaff]")
SERIALIZED = {".prefab", ".unity", ".asset", ".controller", ".anim"}
TEXT_FIELDS = {"m_text", "m_Text", "m_description", "value"}


def digest(value):
    return hashlib.sha256(json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode()).hexdigest()


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT, text=True)


def entries(text):
    return [tuple(line.split("->", 1)) for line in text.splitlines() if "->" in line]


def unity_documents(text):
    import yaml
    text = re.sub(r"(?m)^%.*\n", "", text)
    text = re.sub(r"(?m)^--- !u!\d+ &-?\d+.*$", "---", text)
    return list(yaml.load_all(text, Loader=getattr(yaml, "CBaseLoader", yaml.BaseLoader)))


def compare_structure(before, after, path=()):
    if isinstance(before, dict):
        assert isinstance(after, dict) and before.keys() == after.keys(), path
        for key in before:
            compare_structure(before[key], after[key], (*path, key))
    elif isinstance(before, list):
        assert isinstance(after, list) and len(before) == len(after), path
        for index, (old, new) in enumerate(zip(before, after)):
            compare_structure(old, new, (*path, index))
    elif before != after:
        assert path[-1] in TEXT_FIELDS and isinstance(before, str) and isinstance(after, str), path
        assert HAN.search(before) and not HAN.search(after), path


def verify(base=None):
    aliases = json.loads((ROOT / "Data/locales/legacy-ui-keys.json").read_text())
    assert len(aliases) == len(set(aliases.values())) == 752
    assert all(not HAN.search(value) for value in aliases.values())
    manifest = json.loads((ROOT / "Tools/Translations/english-migration-manifest.json").read_text())
    locales = {}
    duplicate_rows = {}
    for path in sorted((ROOT / "Data/locales").glob("*/translation.conf")):
        pairs = entries(path.read_text())
        assert all(not HAN.search(key) for key, value in pairs), str(path)
        assert digest([value for key, value in pairs]) == manifest["localeValueHashes"][path.parent.name]
        assert len(pairs) == 755
        counts = collections.Counter(key for key, value in pairs)
        duplicate_rows[path.parent.name] = [key for key, count in counts.items() if count > 1]
        assert duplicate_rows[path.parent.name] == ["No Shuffle"]
        locales[path.parent.name] = pairs
        if base:
            old = entries(git("show", base + ":" + path.relative_to(ROOT).as_posix()))
            assert pairs == [(aliases.get(key, key), value) for key, value in old], str(path)
    assert len(locales) == 10
    with (ROOT / "Tools/Translations/translations.csv").open(encoding="utf-8-sig", newline="") as file:
        rows = list(csv.reader(file))
    assert rows[0][0] == "key" and len(rows[0]) == 12
    assert all(not row or not HAN.search(row[0]) for row in rows)
    assert digest([row[1:] for row in rows]) == manifest["csvOriginalColumnsHash"]
    if base:
        old = list(csv.reader(io.StringIO(git("show", base + ":Tools/Translations/translations.csv").lstrip("\ufeff"))))
        assert [row[1:] for row in rows] == old
    # Comment/source scan is lexical; Roslyn tests additionally decode C# literals.
    source_files = [path for path in (ROOT / "Assets").rglob("*") if path.suffix in {".cs", ".shader", ".compute"}]
    assert all(not HAN.search(path.read_text(encoding="utf-8-sig")) for path in source_files)
    names = git("ls-files").splitlines()
    assert not any(HAN.search(name) for name in names)
    grammar = json.loads((ROOT / "Data/locales/legacy-text-contracts.json").read_text())
    assert digest(grammar) == manifest["legacyGrammarHash"]
    assert "Flip Effect" not in grammar["CardSetNameSuffixes"]
    assert all(grammar[key] for key in ("CardSetNamePrefixes", "CardSetNameSuffixes", "RushMaximumAttackPrefixes", "ChineseRaceSuffix", "PuzzleIgnoredFilePrefixes"))
    # New Addressables resource must point to the actual English text GUID.
    group = unity_documents((ROOT / "Assets/AddressableAssetsData/AssetGroups/Text.asset").read_text())[0]["MonoBehaviour"]
    english_meta = (ROOT / "Assets/Addressables/Text/UpdateContent_en.txt.meta").read_text()
    english_guid = re.search(r"(?m)^guid: ([a-f0-9]{32})$", english_meta)[1]
    matches = [entry for entry in group["m_SerializeEntries"] if entry["m_Address"] == "UpdateContent_en"]
    assert len(matches) == 1 and matches[0]["m_GUID"] == english_guid
    assert not HAN.search((ROOT / "Assets/Addressables/Text/UpdateContent_en.txt").read_text())
    # Check current authored text fields against the reviewed attribution allowlist.
    allowed = set(manifest["attributionTextValues"])
    remaining = []
    for relative in manifest["serializedTextFiles"]:
        def visit(value, name=""):
            if isinstance(value, dict):
                for key, child in value.items():
                    visit(child, key)
            elif isinstance(value, list):
                for child in value:
                    visit(child, name)
            elif name in TEXT_FIELDS and isinstance(value, str) and HAN.search(value):
                assert value in allowed, (relative, name, value)
                remaining.append((relative, name, value))
        visit(unity_documents((ROOT / relative).read_text()))
    assert len(remaining) == 10
    compared = 0
    if base:
        changed = git("diff", "--name-only", base).splitlines()
        protected = ("Picture/", "Platforms/", "Sound/", "Data/Windbot/", "Assets/Fonts/")
        assert not any(name.startswith(protected) or name.endswith(".cdb") for name in changed)
        for name in changed:
            path = ROOT / name
            if path.suffix not in SERIALIZED:
                continue
            old = git("show", base + ":" + name)
            new = path.read_text()
            # Existing references and local object identities are unchanged.
            old_guids = collections.Counter(re.findall(r"guid: ([0-9a-f]{32})", old))
            new_guids = collections.Counter(re.findall(r"guid: ([0-9a-f]{32})", new))
            if name == "Assets/AddressableAssetsData/AssetGroups/Text.asset":
                # m_GUID entries are Addressables IDs, not lower-case guid refs.
                old_group = unity_documents(old)[0]["MonoBehaviour"]
                new_group = unity_documents(new)[0]["MonoBehaviour"]
                new_group["m_SerializeEntries"] = [row for row in new_group["m_SerializeEntries"] if row["m_Address"] != "UpdateContent_en"]
                assert old_group == new_group
            else:
                assert re.findall(r"(?m)^--- !u!\d+ &(-?\d+)", old) == re.findall(r"(?m)^--- !u!\d+ &(-?\d+)", new), name
                assert re.findall(r"fileID: (-?\d+)", old) == re.findall(r"fileID: (-?\d+)", new), name
                compare_structure(unity_documents(old), unity_documents(new), (name,))
            assert old_guids == new_guids, name
            compared += 1
    return {"status": "PASS", "legacyLookupAliases": len(aliases), "localeFiles": len(locales),
            "valuesPreserved": True, "csvLocaleColumnsPreserved": True, "productionSourceFilesScanned": len(source_files),
            "hanSourceFiles": 0, "hanFileNames": 0, "attributionTextExceptions": len(remaining),
            "serializedStructuresCompared": compared, "unityEditorPlayOrBuildTested": False}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--base", help="Optional saved commit for exact migration/reference checks")
    args = parser.parse_args()
    print(json.dumps(verify(args.base), indent=2))
