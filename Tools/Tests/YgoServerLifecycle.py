#!/usr/bin/env python3
"""Exercise the production YgoServer class with a controllable native DLL stand-in."""
import argparse
import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import sys
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]


def production_class(source):
    start = source.index("public class YgoServer")
    opening = source.index("{", start)
    depth, end = 1, opening + 1
    while end < len(source) and depth:
        if source[end] == "{":
            depth += 1
        elif source[end] == "}":
            depth -= 1
        end += 1
    if depth:
        raise ValueError("Unbalanced YgoServer class")
    return source[start:end]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default=shutil.which("dotnet"), help=".NET SDK 10 executable")
    parser.add_argument("--native-linux", action="store_true", help="Also exercise the real bundled Linux DLL through production P/Invoke")
    args = parser.parse_args()
    if not args.dotnet:
        parser.error("Install .NET SDK 10 or pass --dotnet /path/to/dotnet")
    source_path = ROOT / "Assets/Scripts/MDPro3/Duel/YgoServer.cs"
    source = source_path.read_text(encoding="utf-8-sig")
    if ".Abort(" in source:
        raise ValueError("Production server lifecycle must not abort a thread")
    template = Path(__file__).with_suffix(".cs.txt").read_text(encoding="utf-8")
    generated = template.replace("@@PRODUCTION_CLASS@@", production_class(source))
    with tempfile.TemporaryDirectory(prefix="mdpro3-server-lifecycle-") as folder:
        project = Path(folder)
        (project / "Program.cs").write_text(generated, encoding="utf-8")
        (project / "Lifecycle.csproj").write_text('''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <LangVersion>9.0</LangVersion><ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable>
  </PropertyGroup>
</Project>
''', encoding="utf-8")
        result = subprocess.run([args.dotnet, "run", "--project", str(project / "Lifecycle.csproj"), "-c", "Release"],
                                cwd=project, check=False, timeout=45)
        print("Production source SHA-256: " + hashlib.sha256(source_path.read_bytes()).hexdigest())
        if result.returncode or not args.native_linux:
            return result.returncode
    if not sys.platform.startswith("linux"):
        raise ValueError("--native-linux requires Linux")
    with tempfile.TemporaryDirectory(prefix="mdpro3-native-lifecycle-") as folder:
        project = Path(folder)
        # Native source uses lowercase data/script.zip; Windows is case-insensitive.
        (project / "Data").symlink_to(ROOT / "Data", target_is_directory=True)
        (project / "data").symlink_to(ROOT / "Data", target_is_directory=True)
        (project / "expansions").mkdir()
        (project / "Program.cs").write_text(Path(__file__).with_name("YgoServerLifecycle.Native.cs.txt").read_text(), encoding="utf-8")
        (project / "NativeLifecycle.csproj").write_text('''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <LangVersion>9.0</LangVersion><ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  <ItemGroup><Compile Include="''' + escape(str(source_path), {'"': '&quot;'}) + '''" Link="Production/YgoServer.cs" /></ItemGroup>
</Project>
''', encoding="utf-8")
        environment = os.environ.copy()
        environment["LD_LIBRARY_PATH"] = str(ROOT / "Assets/Plugins/Linux") + os.pathsep + environment.get("LD_LIBRARY_PATH", "")
        return subprocess.run([args.dotnet, "run", "--project", str(project / "NativeLifecycle.csproj"), "-c", "Release"],
                              cwd=project, env=environment, check=False, timeout=40).returncode


if __name__ == "__main__":
    raise SystemExit(main())
