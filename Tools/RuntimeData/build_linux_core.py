#!/usr/bin/env python3
"""Build the bundled ocgcore/Lua sources for local Linux diagnostics only.

The output stays outside the Unity project. A host build does not establish a
portable Linux-player ABI and must not replace the shipped plugin blindly.
"""
import argparse
import concurrent.futures
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT.parent / ".cloud-build/english-ocgcore")
    parser.add_argument("--jobs", type=int, default=4)
    parser.add_argument("--rebuild", action="store_true")
    args = parser.parse_args()
    if not sys.platform.startswith("linux") or not shutil.which("gcc") or not shutil.which("g++"):
        parser.error("This diagnostic build requires Linux, GCC and G++")
    if args.jobs < 1:
        parser.error("--jobs must be positive")
    source = ROOT / "Tools/YGO Classes"
    output = args.output.resolve()
    if output.is_relative_to(ROOT):
        parser.error("Keep diagnostic build outputs outside the project checkout")
    output.mkdir(parents=True, exist_ok=True)
    files = sorted((source / "ocgcore").glob("*.cpp"))
    files += [p for p in sorted((source / "lua/src").glob("*.c")) if p.name not in {"lua.c", "luac.c"}]
    headers = [p for parent in (source / "ocgcore", source / "lua/src")
               for p in parent.iterdir() if p.suffix in {".h", ".hpp"}]
    latest_input = max(p.stat().st_mtime_ns for p in [*files, *headers])

    def compile_one(path):
        target = output / (path.parent.name + "-" + path.stem + ".o")
        if not args.rebuild and target.exists() and target.stat().st_mtime_ns > latest_input:
            return target, False
        command = ["g++" if path.suffix == ".cpp" else "gcc", "-O2", "-fPIC", "-I" + str(source / "lua/src")]
        command += ["-std=c++17"] if path.suffix == ".cpp" else ["-std=c99", "-DLUA_USE_LINUX"]
        subprocess.run([*command, "-c", str(path), "-o", str(target)], check=True)
        return target, True

    with concurrent.futures.ThreadPoolExecutor(max_workers=args.jobs) as pool:
        objects = list(pool.map(compile_one, files))
    library = output / "libocgcore.so"
    if any(changed for _, changed in objects) or not library.exists():
        subprocess.run(["g++", "-shared", "-o", str(library), *[str(p) for p, _ in objects],
                        "-static-libstdc++", "-static-libgcc", "-pthread", "-ldl", "-lm"], check=True)
    print("Diagnostic library: " + str(library))
    print("Compiled " + str(sum(changed for _, changed in objects)) + " / " + str(len(objects)) + " sources; shipped binaries unchanged.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
