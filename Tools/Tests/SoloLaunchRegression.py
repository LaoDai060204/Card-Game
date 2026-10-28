#!/usr/bin/env python3
"""Exercise production Solo launch guards and the bot-thread exception boundary."""
import argparse
import importlib.util
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[2]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default=shutil.which("dotnet"))
    args = parser.parse_args()
    if not args.dotnet:
        parser.error("Install .NET SDK 10 or pass --dotnet /path/to/dotnet")
    spec = importlib.util.spec_from_file_location("windbot_test_extract", Path(__file__).with_name("WindBotStartup.py"))
    helper = importlib.util.module_from_spec(spec)
    sys.dont_write_bytecode = True
    spec.loader.exec_module(helper)
    source = (ROOT / "Assets/Scripts/MDPro3/Servant/SoloSelector.cs").read_text(encoding="utf-8-sig")
    start_bot = helper.extract(source, "public void StartWindBot(")
    # Record the actual worker for joining/assertions; its implementation is unchanged.
    assert start_bot.count("botThread.Start();") == 1
    start_bot = start_bot.replace("botThread.Start();", "LastThread = botThread; botThread.Start();")
    template = Path(__file__).with_suffix(".cs.txt").read_text(encoding="utf-8")
    template = template.replace("@@START_BOT@@", start_bot)
    template = template.replace("@@LAUNCH@@", helper.extract(source, "private void LaunchWithConfig("))
    tools = (ROOT / "Assets/Scripts/MDPro3/Game/Tools.cs").read_text(encoding="utf-8-sig")
    template = template.replace("@@SPLIT@@", helper.extract(tools, "public static string[] SplitWithPreservedQuotes("))
    with tempfile.TemporaryDirectory(prefix="mdpro3-solo-launch-") as temporary:
        folder = Path(temporary)
        (folder / "Program.cs").write_text(template, encoding="utf-8")
        (folder / "SoloLaunch.csproj").write_text('''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><StartupObject>Regression.Entry</StartupObject>
    <LangVersion>9.0</LangVersion><ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable>
  </PropertyGroup>
</Project>
''', encoding="utf-8")
        return subprocess.run([args.dotnet, "run", "--project", str(folder / "SoloLaunch.csproj"), "-c", "Release"], cwd=folder).returncode


if __name__ == "__main__":
    raise SystemExit(main())
