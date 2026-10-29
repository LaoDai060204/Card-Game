#!/usr/bin/env python3
"""Exercise actual WindBot handshake serialization in Debug and Release without Unity."""
import argparse
import json
from pathlib import Path
import shlex
import shutil
import subprocess
import tempfile
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default=shutil.which("dotnet"), help="Path to .NET SDK 10 dotnet")
    args = parser.parse_args()
    if not args.dotnet:
        parser.error("Install .NET SDK 10 or pass --dotnet /path/to/dotnet")

    profiles = []
    for path in sorted((ROOT / "Data/locales").glob("*/bot.conf")):
        for number, line in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
            if not line.startswith("Name="):
                continue
            command = dict(argument.split("=", 1) for argument in shlex.split(line) if "=" in argument)
            profiles.append({"Label": f"{path.parent.name}:{number}", "Name": command["Name"],
                             "Deck": command.get("Deck"), "DeckFile": command.get("DeckFile"),
                             "Dialog": command.get("Dialog")})
    if not profiles:
        raise ValueError("No shipped WindBot names were found")

    sources = ["Assets/Scripts/Windbot/Game/GameClient.cs", "Assets/Scripts/Windbot/WindBotInfo.cs",
               "Assets/Scripts/YGOSharp/YGOSharp.Network/Utils/BinaryExtensions.cs",
               "Assets/Scripts/Windbot/Game/GamePacketFactory.cs",
               "Assets/Scripts/YGOSharp/YGOSharp.Network/Enums/CtosMessage.cs"]
    includes = "\n".join('    <Compile Include="' + escape(str(ROOT / path), {'"': '&quot;'}) + '" />'
                         for path in sources)
    with tempfile.TemporaryDirectory(prefix="mdpro3-windbot-handshake-") as temporary:
        folder = Path(temporary)
        (folder / "Program.cs").write_text(Path(__file__).with_suffix(".cs.txt").read_text(encoding="utf-8"),
                                            encoding="utf-8")
        (folder / "profiles.json").write_text(json.dumps(profiles, ensure_ascii=False), encoding="utf-8")
        project = folder / "WindBotHandshake.csproj"
        project.write_text('''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <LangVersion>9.0</LangVersion><ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
''' + includes + "\n  </ItemGroup>\n</Project>\n", encoding="utf-8")
        for configuration in ("Debug", "Release"):
            completed = subprocess.run([args.dotnet, "run", "--project", str(project), "-c", configuration,
                                        "--", str(folder / "profiles.json")], cwd=folder, check=False)
            if completed.returncode:
                return completed.returncode
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
