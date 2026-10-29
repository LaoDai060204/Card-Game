#!/usr/bin/env python3
"""Check Vietnamese localization and English fallback; --write exports vi-VN first."""

import argparse
from collections import Counter
import csv
import json
from pathlib import Path
import re
import unicodedata


ROOT = Path(__file__).resolve().parents[2]
LOCALES = ROOT / "Data/locales"
VI = LOCALES / "vi-VN"
HAN = re.compile(r"[\u3400-\u9fff]")
TOKENS = re.compile(r"\[\?\]|@n|@ui|\{\d+(?:[^{}]*)\}|<[^>\n]+>")
PRINTF = re.compile(r"%(?:\d+\$)?[-+#0 ]*\d*(?:\.\d+)?(?:hh|ll|[hljztL])?[diuoxXfFeEgGaAcspn%]")


def require(condition, message):
    if not condition:
        raise ValueError(message)


def read_ui(path):
    values = {}
    for number, line in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
        if not line or line.startswith("#"):
            continue
        require(line.count("->") == 1, f"{path}:{number}: invalid separator")
        key, value = line.split("->")
        require(key and value, f"{path}:{number}: empty key/value")
        require(key not in values or values[key] == value,
                f"{path}:{number}: conflicting duplicate key {key}")
        values[key] = value
    return values


def csv_vietnamese():
    path = ROOT / "Tools/Translations/translations.csv"
    with path.open(encoding="utf-8-sig", newline="") as stream:
        rows = list(csv.reader(stream))
    column = rows[0].index("vi-VN")
    lines = []
    for number, row in enumerate(rows[1:], 2):
        key = row[0] if row else ""
        if not key or key.startswith("#"):
            lines.append(key)
            continue
        require(len(row) > column and row[column], f"CSV row {number}: missing Vietnamese")
        require("->" not in key + row[column] and "\n" not in row[column]
                and "\r" not in row[column], f"CSV row {number}: use @n for newlines")
        lines.append(key + "->" + row[column])
    return "\n".join(lines) + "\n"


def read_system(path):
    result = {}
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        if not line.startswith("!"):
            continue
        parts = line.split(maxsplit=2)
        require(len(parts) == 3, f"{path}: invalid system entry {line}")
        key = tuple(parts[:2])
        require(key not in result, f"{path}: duplicate system ID {key}")
        result[key] = parts[2]
    return result


def read_bots(path):
    lines = path.read_text(encoding="utf-8-sig").splitlines()
    return [lines[index:index + 4] for index, line in enumerate(lines)
            if line.startswith("!")]


def validate():
    english = read_ui(LOCALES / "en-US/translation.conf")
    vietnamese = read_ui(VI / "translation.conf")
    with (ROOT / "Tools/Translations/translations.csv").open(encoding="utf-8-sig", newline="") as stream:
        rows = list(csv.reader(stream))
    english_column = rows[0].index("en-US")
    csv_english = {}
    for number, row in enumerate(rows[1:], 2):
        if not row or not row[0] or row[0].startswith("#"):
            continue
        require(len(row) > english_column and row[english_column], f"CSV row {number}: missing English")
        key, value = row[0], row[english_column]
        require(key not in csv_english or csv_english[key] == value, f"Conflicting English CSV translation: {key}")
        csv_english[key] = value
    require(csv_english == english, "CSV and en-US/translation.conf differ")
    for key, value in english.items():
        require(not HAN.search(value), f"Untranslated Chinese English UI value: {key}")
    require(english.keys() <= vietnamese.keys(), "Vietnamese is missing English UI keys")
    for key, value in vietnamese.items():
        require(value == unicodedata.normalize("NFC", value), f"Non-NFC Vietnamese: {key}")
        require(not HAN.search(value), f"Untranslated Chinese UI value: {key}")
        if key in english:
            expected = Counter(TOKENS.findall(english[key]))
            # The upstream English exit dialog dropped a line break present in
            # the source key. Vietnamese intentionally restores that line break.
            if key == "即将退出应用程序，@n是否确认？":
                expected["@n"] = 1
            require(Counter(TOKENS.findall(value)) == expected,
                    f"UI placeholders/tags changed: {key}")

    csv_values = {}
    for line in csv_vietnamese().splitlines():
        if line and not line.startswith("#"):
            key, value = line.split("->")
            require(key not in csv_values or csv_values[key] == value,
                    f"Conflicting CSV translation: {key}")
            csv_values[key] = value
    require(csv_values == vietnamese, "CSV and vi-VN/translation.conf differ; run --write")

    literal_count = 0
    for path in (ROOT / "Assets/Scripts/MDPro3").rglob("*.cs"):
        content = path.read_text(encoding="utf-8-sig")
        for match in re.finditer(r'InterString\.Get\(\s*"((?:[^"\\]|\\.)*)"', content):
            key = json.loads('"' + match.group(1) + '"')
            if HAN.search(key):
                require(key in english, f"Missing runtime English translation: {path}: {key}")
                require(key in vietnamese, f"Missing runtime UI translation: {path}: {key}")
                literal_count += 1

    original_system = read_system(LOCALES / "en-US/strings.conf")
    translated_system = read_system(VI / "strings.conf")
    require(list(original_system) == list(translated_system), "System IDs/order changed")
    for key, value in translated_system.items():
        source = original_system[key]
        require(PRINTF.findall(value) == PRINTF.findall(source), f"Printf arguments changed: {key}")
        require(Counter(re.findall(r"\\[nrt]", value)) == Counter(re.findall(r"\\[nrt]", source)),
                f"Escaped system line breaks changed: {key}")
        if key[0] == "!setname":
            require(value == source, f"Archetype name changed: {key}")

    original_bots = read_bots(LOCALES / "en-US/bot.conf")
    translated_bots = read_bots(VI / "bot.conf")
    require(len(original_bots) == len(translated_bots), "AI profile count changed")
    for original, translated in zip(original_bots, translated_bots):
        require(len(translated) == 4 and original[1] == translated[1]
                and original[3] == translated[3], "AI command or flags changed")

    group = (ROOT / "Assets/AddressableAssetsData/AssetGroups/Text.asset").read_text(encoding="utf-8-sig")
    for name in ["AboutGame_vi", "AboutVersion_vi", "AboutUpdate_vi", "UpdateContent_vi"]:
        text = ROOT / f"Assets/Addressables/Text/{name}.txt"
        content = text.read_text(encoding="utf-8-sig")
        if name.startswith("About"):
            require(content.rstrip().endswith("_END_"), f"Missing About terminator: {name}")
        guid = re.search(r"^guid: ([0-9a-f]{32})$", Path(str(text) + ".meta").read_text(encoding="utf-8-sig"), re.M)
        require(guid and f"m_GUID: {guid[1]}\n    m_Address: {name}\n" in group,
                f"About asset is not registered in Addressables: {name}")
    print(f"PASS: {len(english)} English and {len(vietnamese)} Vietnamese UI keys; {literal_count} runtime uses; "
          f"{len(translated_system)} system entries; {len(translated_bots)} AI profiles; "
          "CSV, placeholders and About Addressables verified.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true", help="Export vi-VN from the CSV before checking")
    args = parser.parse_args()
    try:
        if args.write:
            VI.mkdir(parents=True, exist_ok=True)
            (VI / "translation.conf").write_text(csv_vietnamese(), encoding="utf-8")
        validate()
    except (ValueError, KeyError, OSError) as error:
        parser.exit(1, f"FAIL: {error}\n")
