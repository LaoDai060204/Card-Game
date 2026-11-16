#!/usr/bin/env python3
"""Compile the shipped assembly boundaries against .NET Standard 2.1, not net10."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import urllib.request
import zipfile


ROOT = Path(__file__).resolve().parents[3]
SDK = Path(os.environ.get("DOTNET_ROOT", "/tmp/codex-dotnet"))
CACHE = Path(os.environ.get("BS_COMPATIBILITY_CACHE", "/tmp/bs-netstandard21"))
PACKAGES = {
    "netstandard": (
        "netstandard.library.ref", "2.1.0",
        "46ea2fcbd10a817685b85af7ce0c397d12944bdc81209e272de1e05efd33c78a"),
    "newtonsoft": (
        "newtonsoft.json", "13.0.3",
        "872fc189e638ab1056555b03aaa38f68bcb54286e221aa646eb1129babf63c77"),
}


def package(name):
    identifier, version, expected = PACKAGES[name]
    CACHE.mkdir(parents=True, exist_ok=True)
    archive = CACHE / (name + ".nupkg")
    if not archive.exists():
        url = "https://api.nuget.org/v3-flatcontainer/{0}/{1}/{0}.{1}.nupkg".format(identifier, version)
        print("Downloading pinned reference package:", identifier, version, flush=True)
        with urllib.request.urlopen(url, timeout=30) as response:
            content = response.read()
        if hashlib.sha256(content).hexdigest() != expected:
            raise RuntimeError("Unexpected package SHA256: " + identifier)
        archive.write_bytes(content)
    if hashlib.sha256(archive.read_bytes()).hexdigest() != expected:
        raise RuntimeError("Cached package SHA256 mismatch: " + str(archive))
    destination = CACHE / name
    # Re-extract verified bytes so stale or manually changed files cannot mask the check.
    with zipfile.ZipFile(archive) as files:
        files.extractall(destination)
    return destination


def main():
    ref_dir = Path(os.environ["NETSTANDARD_REF_DIR"]) if "NETSTANDARD_REF_DIR" in os.environ else package("netstandard") / "ref/netstandard2.1"
    newtonsoft = Path(os.environ["NEWTONSOFT_JSON_DLL"]) if "NEWTONSOFT_JSON_DLL" in os.environ else package("newtonsoft") / "lib/netstandard2.0/Newtonsoft.Json.dll"
    refs = sorted(ref_dir.glob("*.dll"))
    if not (ref_dir / "netstandard.dll").exists() or len(refs) < 10:
        raise RuntimeError("NETSTANDARD_REF_DIR must contain .NET Standard 2.1 reference DLLs.")
    if not newtonsoft.exists():
        raise FileNotFoundError(newtonsoft)
    version = sorted((SDK / "sdk").iterdir())[-1]
    compiler = version / "Roslyn/bincore/csc.dll"
    runtime = ROOT / "Assets/BattleSpirits/Runtime"
    reports = []
    with tempfile.TemporaryDirectory(prefix="bs-compatibility-") as directory:
        work = Path(directory)

        def compile_library(name, sources, references=()):
            target = work / (name + ".dll")
            options = ["-nologo", "-langversion:9", "-target:library", "-nostdlib+", "-out:" + str(target)]
            options += ["-r:" + str(p) for p in refs + list(references)]
            options += [str(p) for p in sources]
            response = work / (name + ".rsp")
            response.write_text("\n".join('"' + p + '"' for p in options))
            result = subprocess.run([str(SDK / "dotnet"), "exec", str(compiler), "@" + str(response)], text=True, capture_output=True)
            if result.stdout:
                print(result.stdout.rstrip())
            if result.stderr:
                print(result.stderr.rstrip())
            if result.returncode:
                raise subprocess.CalledProcessError(result.returncode, name)
            reports.append({"assembly": name, "sourceCount": len(sources), "result": "PASS"})
            print("PASS .NET Standard 2.1 / C#9:", name, flush=True)
            return target

        # Match all three production asmdefs. No Unity or test doubles enter Core/AI/Puzzles.
        core = compile_library("MDPro3.BattleSpirits.Core", sorted((runtime / "Core").glob("*.cs")))
        ai = compile_library("MDPro3.BattleSpirits.AI", sorted((runtime / "AI").glob("*.cs")), [core])
        puzzles = compile_library("MDPro3.BattleSpirits.Puzzles", sorted((runtime / "Puzzles").glob("*.cs")), [core])
        backend_sources = [runtime / "Integration" / name for name in ("BattleGameSession.cs", "BattleReplay.cs", "BattleLanTransport.cs")]
        backend_sources.append(runtime / "Data/BattleCatalog.cs")
        backend = compile_library("BattleBackend", backend_sources, [core, ai, puzzles, newtonsoft])

        # Reuse the native harness's Unity/UniTask framework doubles only. Its executable test
        # Program.cs and any net10 reference assemblies are deliberately excluded.
        stubs = (ROOT / "Tools/BattleSpiritsNativeTests/Stubs.cs").read_text()
        marker = "public class GameObject {"
        if stubs.count(marker) != 1:
            raise RuntimeError("Update the native GameObject double for this harness.")
        stubs = stubs.replace(marker, marker + " public T[] GetComponentsInChildren<T>(bool all) => Array.Empty<T>();")
        stubs += "\nnamespace TMPro { public class TMP_Text { public string text; } }\n"
        stub_file = work / "UnityNativeStubs.cs"
        stub_file.write_text(stubs)
        native_sources = sorted((runtime / "NativeAdapters").glob("*.cs"))
        native_sources += [ROOT / "Assets/Scripts/MDPro3/Duel/YGOSharp" / name for name in ("Card.cs", "CardsManager.cs", "Deck.cs", "YGOSharpOCGWrapperEnums.cs")]
        native_sources += [ROOT / "Assets/Scripts/MDPro3/Helper/Config.cs", stub_file]
        compile_library("BattleNativeAdapters", native_sources, [core, backend, newtonsoft])

    report = {
        "referenceProfile": "netstandard2.1", "languageVersion": "9", "bclReferenceCount": len(refs),
        "newtonsoft": str(newtonsoft), "assemblies": reports,
        "limitations": "Compile only; unavailable Unity/TMP/UniTask framework types use doubles. Not Unity Play/build, shader, asset or runtime validation."
    }
    path = Path(os.environ.get("BS_COMPATIBILITY_REPORT", "/tmp/battle-spirits-netstandard-compatibility.json"))
    path.write_text(json.dumps(report, indent=2) + "\n")
    print("PASS all five separated libraries; no .NET10 BCL references. Report:", path)


if __name__ == "__main__":
    main()
