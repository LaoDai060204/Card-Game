#!/usr/bin/env python3
"""Run actual Puzzle UI/parser methods in an isolated C# harness, without Unity."""
from pathlib import Path
import argparse
from contextlib import closing
import json
import re
import shutil
import sqlite3
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]


def extract_method(source, signature):
    start = source.find(signature)
    if start < 0:
        raise ValueError("Production method not found: " + signature)
    opening = source.index("{", start)
    # These selected methods have no brace literals; fail loudly on an unbalanced change.
    depth = 1
    closing = opening + 1
    while closing < len(source) and depth:
        if source[closing] == "{":
            depth += 1
        elif source[closing] == "}":
            depth -= 1
        closing += 1
    if depth:
        raise ValueError("Unbalanced production method: " + signature)
    return source[start:closing]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default=shutil.which("dotnet"), help="Path to .NET SDK 10 dotnet executable")
    args = parser.parse_args()
    if not args.dotnet:
        parser.error("Install .NET SDK 10 or provide --dotnet /path/to/dotnet")
    portal = (ROOT / "Assets/Scripts/MDPro3/UI/ServantUI/PuzzlePortalUI.cs").read_text(encoding="utf-8-sig")
    selector = (ROOT / "Assets/Scripts/MDPro3/UI/ServantUI/PuzzleSelectorUI.cs").read_text(encoding="utf-8-sig")
    dependency = (ROOT / "Assets/Scripts/MDPro3/Servant/PuzzleSelector.cs").read_text(encoding="utf-8-sig")
    template = Path(__file__).with_suffix(".cs.txt").read_text(encoding="utf-8")
    signatures = ["private static PuzzleGroup? GetPuzzleGroupByPath", "private static Puzzle GetPuzzle(",
                  "private static void GetPuzzles(", "public static Puzzle GetFirstPuzzle(",
                  "public static List<Puzzle> GetRecommendPuzzles(", "private static List<Puzzle> GetAllPuzzles(",
                  "private static void AddRecommendPuzzle(", "private static void AddNotClearPuzzle(",
                  "public static Puzzle? GetLastPlayPuzzle("]
    template = template.replace("@@SELECTOR_METHODS@@", "\n".join(extract_method(selector, s) for s in signatures))
    template = template.replace("@@PORTAL_METHOD@@", extract_method(portal, "private void SetRecommendPuzzleButtons("))
    template = template.replace("@@NAME_PATTERN_METHOD@@", extract_method(portal, "public static string GetPuzzleNamePattern("))
    template = template.replace("@@DEPENDENCY_METHOD@@", extract_method(dependency, "internal static List<int> FindMissingPuzzleCardIds("))
    ignore = re.search(r"private static readonly List<string> ignoreInPuzzleListPrefix.*?\};", selector, re.S)
    if ignore is None:
        raise ValueError("Production ignore-prefix list not found")
    template = template.replace("@@IGNORE_PREFIXES@@", ignore.group(0))
    with tempfile.TemporaryDirectory(prefix="mdpro3-puzzle-tests-") as folder:
        path = Path(folder)
        (path / "Program.cs").write_text(template, encoding="utf-8")
        database = ROOT / "Data/locales/en-US/cards.cdb"
        with closing(sqlite3.connect(database.resolve().as_uri() + "?mode=ro&immutable=1", uri=True)) as connection:
            ids = sorted(row[0] for row in connection.execute("SELECT id FROM datas"))
        (path / "known-card-ids.json").write_text(json.dumps(ids), encoding="utf-8")
        (path / "PuzzleRegression.csproj").write_text('''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <LangVersion>9.0</LangVersion><ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable>
  </PropertyGroup>
</Project>
''', encoding="utf-8")
        result = subprocess.run([args.dotnet, "run", "--project", str(path / "PuzzleRegression.csproj"), "-c", "Release", "--", str(ROOT), str(path / "known-card-ids.json")],
                                cwd=path, text=True, check=False)
        return result.returncode


if __name__ == "__main__":
    raise SystemExit(main())
