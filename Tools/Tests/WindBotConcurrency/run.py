#!/usr/bin/env python3
"""Compile actual WindBot startup, option parsing and card-catalog publication."""
import argparse
from pathlib import Path
import shutil
import subprocess
import tempfile
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[3]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default=shutil.which("dotnet"))
    args = parser.parse_args()
    if not args.dotnet:
        parser.error("Install .NET SDK 10 or supply --dotnet")
    sources = [
        "Assets/Scripts/Windbot/Program.cs", "Assets/Scripts/Windbot/Config.cs",
        "Assets/Scripts/Windbot/WindBotInfo.cs",
        "Assets/Scripts/YGOSharp/YGOSharp.OCGWrapper/NamedCardsManager.cs",
        "Assets/Scripts/YGOSharp/YGOSharp.OCGWrapper/NamedCard.cs",
        "Assets/Scripts/YGOSharp/YGOSharp.OCGWrapper/Card.cs",
        "Assets/Scripts/YGOSharp/YGOSharp.OCGWrapper.Enums/CardType.cs",
    ]
    includes = "\n".join('<Compile Include="' + escape(str(ROOT / source)) + '" />' for source in sources)
    with tempfile.TemporaryDirectory(prefix="mdpro3-windbot-concurrency-") as temporary:
        folder = Path(temporary)
        for name in ("Program.cs", "Stubs.cs"):
            shutil.copyfile(Path(__file__).with_name(name), folder / name)
        (folder / "Concurrency.csproj").write_text('''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <LangVersion>9.0</LangVersion><ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable>
    <StartupObject>WindBotConcurrencyTests</StartupObject>
  </PropertyGroup>
  <ItemGroup>''' + includes + '''</ItemGroup>
</Project>
''', encoding="utf-8")
        return subprocess.run([args.dotnet, "run", "--project", str(folder / "Concurrency.csproj"),
                               "-c", "Release"], cwd=folder, check=False, timeout=45).returncode


if __name__ == "__main__":
    raise SystemExit(main())
