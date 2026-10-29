#!/usr/bin/env python3
"""Exercise production display-only translation, Puzzle packets and release-note selection."""
import argparse
import ast
import json
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]
HAN = re.compile(r"[\u3400-\u9fff]")


def extract(source, signature):
    start = source.index(signature)
    opening = source.index("{", start)
    depth = 0
    for offset in range(opening, len(source)):
        if source[offset] == "{":
            depth += 1
        elif source[offset] == "}":
            depth -= 1
            if depth == 0:
                return source[start:offset + 1]
    raise ValueError("Unclosed production method: " + signature)


def translations(path):
    return dict(line.split("->", 1) for line in path.read_text(encoding="utf-8-sig").splitlines()
                if line and not line.startswith("#") and "->" in line)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default=shutil.which("dotnet"))
    args = parser.parse_args()
    if not args.dotnet:
        parser.error("Install .NET SDK 10 or pass --dotnet /path/to/dotnet")

    english = translations(ROOT / "Data/locales/en-US/translation.conf")
    vietnamese = translations(ROOT / "Data/locales/vi-VN/translation.conf")
    puzzle_strings = set()
    pattern = re.compile(r'''Debug\.(ShowHint|SetAIName)\(\s*("(?:[^"\\]|\\.)*"|'(?:[^'\\]|\\.)*')\s*\)''')
    for path in (ROOT / "Puzzle").rglob("*.lua"):
        for match in pattern.finditer(path.read_text(encoding="utf-8-sig")):
            key = ast.literal_eval(match[2])
            if HAN.search(key):
                puzzle_strings.add((match[1], key))
    if not puzzle_strings:
        raise ValueError("No official Puzzle display strings found")
    puzzles = []
    for operation, key in sorted(puzzle_strings):
        if key not in english or key not in vietnamese or HAN.search(english[key]) or HAN.search(vietnamese[key]):
            raise ValueError("Missing non-Chinese Puzzle display translation: " + key)
        puzzles.append({"operation": operation, "key": key,
                        "english": english[key], "vietnamese": vietnamese[key]})

    text_group = (ROOT / "Assets/AddressableAssetsData/AssetGroups/Text.asset").read_text(encoding="utf-8-sig")
    for address in ("UpdateContent", "UpdateContent_vi", "UpdateContent_en"):
        asset = ROOT / "Assets/Addressables/Text" / (address + ".txt")
        guid = re.search(r"^guid: ([0-9a-f]{32})$", Path(str(asset) + ".meta").read_text(), re.M)
        if not guid or f"m_GUID: {guid[1]}\n    m_Address: {address}\n" not in text_group:
            raise ValueError("Release-note Addressable missing: " + address)
    if HAN.search((ROOT / "Assets/Addressables/Text/UpdateContent_en.txt").read_text()):
        raise ValueError("English release notes still contain untranslated Han text")

    source = (ROOT / "Assets/Scripts/MDPro3/Duel/Message/DuelMessage.cs").read_text(encoding="utf-8-sig")
    template = Path(__file__).with_suffix(".cs.txt").read_text()
    for name in ("ShowHint", "AiName"):
        method = extract(source, "protected override UniTask GameMessage_" + name + "(")
        template = template.replace("@@" + name.upper() + "@@", method.replace("protected override", "public", 1))
    paths = ["Assets/Scripts/MDPro3/Helper/InterString.cs", "Assets/Scripts/MDPro3/Utility/Language.cs",
             "Assets/Scripts/YGOSharp/YGOSharp.Network/Utils/BinaryExtensions.cs"]
    includes = "\n".join('    <Compile Include="' + escape(str(ROOT / path), {'"': '&quot;'}) + '" />' for path in paths)
    with tempfile.TemporaryDirectory(prefix="mdpro3-display-localization-") as temporary:
        folder = Path(temporary)
        for path in (ROOT / "Data/locales").glob("*/translation.conf"):
            target = folder / "Data/locales" / path.parent.name / path.name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(path, target)
        (folder / "puzzles.json").write_text(json.dumps(puzzles, ensure_ascii=False), encoding="utf-8")
        (folder / "Program.cs").write_text(template, encoding="utf-8")
        project = folder / "DisplayLocalization.csproj"
        project.write_text('''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <LangVersion>9.0</LangVersion><ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable>
  </PropertyGroup><ItemGroup>
''' + includes + "\n  </ItemGroup>\n</Project>\n", encoding="utf-8")
        return subprocess.run([args.dotnet, "run", "--project", str(project), "-c", "Release"],
                              cwd=folder, check=False, timeout=60).returncode


if __name__ == "__main__":
    raise SystemExit(main())
