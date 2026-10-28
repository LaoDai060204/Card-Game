#!/usr/bin/env python3
"""Test actual WindBot startup/deck-discovery code without a Unity Editor."""
import argparse
from pathlib import Path
import shutil
import subprocess
import tempfile
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]


def extract(source, marker):
    start = source.index(marker)
    opening = source.index("{", start)
    depth = 0
    for offset in range(opening, len(source)):
        if source[offset] == "{":
            depth += 1
        elif source[offset] == "}":
            depth -= 1
            if depth == 0:
                return source[start:offset + 1]
    raise ValueError("Unclosed production method: " + marker)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default=shutil.which("dotnet"), help="Path to .NET SDK 10 dotnet")
    args = parser.parse_args()
    if not args.dotnet:
        parser.error("Install .NET SDK 10 or pass --dotnet /path/to/dotnet")

    source = (ROOT / "Assets/Scripts/Windbot/Program.cs").read_text(encoding="utf-8-sig")
    if "Console.ReadKey(" in source or "Environment.Exit(" in source:
        raise ValueError("The embedded WindBot must not block console input or terminate Unity")
    methods = "\n".join(extract(source, marker) for marker in
                        ("public static void InitDatas(", "internal static string ResolveDatabasePath("))
    startup = """using System;
using System.IO;
using WindBot.Game.AI;
using YGOSharp.OCGWrapper;
using MDPro3.IO;
namespace WindBot {
    public class Program {
        internal static Random Rand;
""" + methods + "\n    }\n}\n"

    links = ["Assets/Scripts/Windbot/Game/AI/DecksManager.cs",
             "Assets/Scripts/Windbot/Game/AI/DeckAttribute.cs",
             "Assets/Scripts/MDPro3/Utility/Language.cs"]
    includes = "\n".join('    <Compile Include="' + escape(str(ROOT / path), {'"': '&quot;'}) + '" />'
                         for path in links)
    with tempfile.TemporaryDirectory(prefix="mdpro3-windbot-startup-") as temporary:
        folder = Path(temporary)
        (folder / "StartupMethods.cs").write_text(startup, encoding="utf-8")
        (folder / "Program.cs").write_text(Path(__file__).with_suffix(".cs.txt").read_text(encoding="utf-8"), encoding="utf-8")
        (folder / "WindBotStartup.csproj").write_text('''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <LangVersion>9.0</LangVersion><ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
''' + includes + "\n  </ItemGroup>\n</Project>\n", encoding="utf-8")
        completed = subprocess.run([args.dotnet, "run", "--project", str(folder / "WindBotStartup.csproj"),
                                    "-c", "Release", "--", str(ROOT)], cwd=folder, check=False)
        return completed.returncode


if __name__ == "__main__":
    raise SystemExit(main())
