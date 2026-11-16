#!/usr/bin/env python3
"""Run actual native collection lifecycle/search method bodies with the real catalog."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[3]
production = root / "Assets/Scripts/MDPro3/UI/UIWidget/Deck/CardCollectionView.cs"
source = production.read_text(encoding="utf-8-sig")

def method(signature):
    begin = source.index(signature)
    brace = source.index("{", begin)
    depth, end = 1, brace + 1
    while depth:
        depth += (source[end] == "{") - (source[end] == "}")
        end += 1
    return source[begin:end]

assert "onValueChanged.AddListener(RefreshCurrentArea)" in source
assert "onEndEdit.AddListener(RefreshCurrentArea)" in source
assert "ButtonSearch.SetClickEvent(() => RefreshCurrentArea())" in source
assert "DeckEditor.UseMobileLayout ? 6 : 4" in source
assert "DeckEditor.UseMobileLayout ? 158 : 132" in source
assert "DeckEditor.UseMobileLayout ? 239 : 202" in source
sdk = Path(os.environ.get("DOTNET_ROOT", "/tmp/codex-dotnet"))
version = sorted((sdk / "sdk").iterdir())[-1]
refs = sorted((sdk / "packs/Microsoft.NETCore.App.Ref").glob("*/ref/net10.0"))[-1]
runtime = sorted((sdk / "shared/Microsoft.NETCore.App").iterdir())[-1].name
project = root / "Tools/BattleSpiritsNativeTests/BattleSpiritsNative.Tests.csproj"
with tempfile.TemporaryDirectory(prefix="bs-collection-search-") as directory:
    work = Path(directory)
    extracted = work / "ActualCollectionMethods.cs"
    methods = [method(signature) for signature in (
        "private void Start()", "public void ShowArea(Area area)",
        "public void PrintSearchCards(string text = \"\")", "protected void RefreshCurrentArea(string _ = \"\")")]
    extracted.write_text("using System;using System.Collections.Generic;using MDPro3;using MDPro3.Duel.YGOSharp;public partial class CollectionFixture{\n" + "\n".join(methods) + "\n}")
    sources = [(project.parent / item.attrib["Include"]).resolve() for item in ET.parse(project).findall(".//Compile") if item.attrib["Include"] != "Program.cs"]
    sources += [extracted, Path(__file__).with_name("Program.cs")]
    target = work / "DeckSearchTests.dll"
    newtonsoft = version / "Newtonsoft.Json.dll"
    options = ["-nologo", "-target:exe", "-langversion:9", '-out:"' + str(target) + '"']
    options += ['-r:"' + str(path) + '"' for path in [*refs.glob("*.dll"), newtonsoft]]
    options += ['"' + str(path) + '"' for path in sources]
    response = work / "compile.rsp"
    response.write_text("\n".join(options))
    env = dict(os.environ, DOTNET_CLI_HOME=str(work / "dotnet"), DOTNET_CLI_TELEMETRY_OPTOUT="1")
    subprocess.run([str(sdk / "dotnet"), "exec", str(version / "Roslyn/bincore/csc.dll"), "@" + str(response)], env=env, check=True)
    shutil.copy2(newtonsoft, work)
    target.with_suffix(".runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {"tfm": "net10.0", "framework": {"name": "Microsoft.NETCore.App", "version": runtime}}}))
    subprocess.run([str(sdk / "dotnet"), "exec", str(target), str(root)], env=env, check=True)
