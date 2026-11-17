#!/usr/bin/env python3
"""Exercise production LAN/hand-test launch callers with controlled Unity timing."""
import argparse
from pathlib import Path
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]


def extract(source, marker):
    start = source.index(marker)
    opening = source.index("{", start)
    depth = 1
    end = opening + 1
    while depth and end < len(source):
        if source[end] == "{":
            depth += 1
        elif source[end] == "}":
            depth -= 1
        end += 1
    if depth:
        raise ValueError("Unbalanced production method: " + marker)
    return source[start:end].replace("private async UniTask", "private async Task")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default=shutil.which("dotnet"))
    args = parser.parse_args()
    if not args.dotnet:
        parser.error("Install .NET SDK 10 or pass --dotnet /path/to/dotnet")
    online = (ROOT / "Assets/Scripts/MDPro3/Servant/OnlineServant.cs").read_text(encoding="utf-8-sig")
    deck = (ROOT / "Assets/Scripts/MDPro3/UI/ServantUI/DeckEditorUI.cs").read_text(encoding="utf-8-sig")
    source = Path(__file__).with_suffix(".cs.txt").read_text(encoding="utf-8")
    source = source.replace("@@LAN@@", "\n".join(extract(online, marker) for marker in
        ("public void CreateServer(", "private async UniTask JoinCreatedServerAsync(")))
    source = source.replace("@@HAND@@", "\n".join(extract(deck, marker) for marker in
        ("private bool TryStartHandTest(", "private async UniTask HandTestAsync(")))
    with tempfile.TemporaryDirectory(prefix="mdpro3-local-callers-") as temporary:
        folder = Path(temporary)
        (folder / "Program.cs").write_text(source, encoding="utf-8")
        project = folder / "Callers.csproj"
        project.write_text('''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
<LangVersion>9.0</LangVersion><ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable>
</PropertyGroup></Project>''', encoding="utf-8")
        return subprocess.run([args.dotnet, "run", "--project", str(project), "-c", "Release"],
                              cwd=folder, check=False, timeout=45).returncode


if __name__ == "__main__":
    raise SystemExit(main())
