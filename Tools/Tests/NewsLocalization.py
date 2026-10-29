#!/usr/bin/env python3
"""Compile actual NewsManager and exercise language/feed changes with controlled image downloads."""
import argparse
from pathlib import Path
import shutil
import subprocess
import tempfile
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]


def extract_class(source, name):
    start = source.index("public class " + name + "\n")
    opening = source.index("{", start)
    depth = 0
    for position in range(opening, len(source)):
        if source[position] == "{":
            depth += 1
        elif source[position] == "}":
            depth -= 1
            if depth == 0:
                return source[start:position + 1]
    raise ValueError("Unclosed production class: " + name)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default=shutil.which("dotnet"))
    args = parser.parse_args()
    if not args.dotnet:
        parser.error("Install .NET SDK 10 or pass --dotnet /path/to/dotnet")
    source = (ROOT / "Assets/Scripts/MDPro3/Net/MyCard.cs").read_text(encoding="utf-8-sig")
    models = "using Newtonsoft.Json;\nnamespace MDPro3.Net {\n" + "\n".join(
        extract_class(source, name) for name in ("MyCardNews", "News")) + "\n}\n"
    includes = "\n".join('    <Compile Include="' + escape(str(ROOT / name), {'"': '&quot;'}) + '" />'
                         for name in ("Assets/Scripts/MDPro3/Managers/NewsManager.cs",
                                      "Assets/Scripts/MDPro3/Utility/Language.cs"))
    with tempfile.TemporaryDirectory(prefix="mdpro3-news-localization-") as temporary:
        folder = Path(temporary)
        (folder / "Program.cs").write_text(Path(__file__).with_suffix(".cs.txt").read_text(), encoding="utf-8")
        (folder / "Models.cs").write_text(models, encoding="utf-8")
        project = folder / "NewsLocalization.csproj"
        project.write_text('''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <LangVersion>9.0</LangVersion><ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="Newtonsoft.Json"><HintPath>$(MSBuildToolsPath)/Newtonsoft.Json.dll</HintPath></Reference>
''' + includes + "\n  </ItemGroup>\n</Project>\n", encoding="utf-8")
        return subprocess.run([args.dotnet, "run", "--project", str(project), "-c", "Release"],
                              cwd=folder, check=False, timeout=60).returncode


if __name__ == "__main__":
    raise SystemExit(main())
