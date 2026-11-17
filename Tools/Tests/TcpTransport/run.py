#!/usr/bin/env python3
"""Exercise production TCP transport with loopback sockets and controlled Unity stubs."""
import argparse
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[3]


def extract(source, marker):
    start = source.index(marker)
    opening = source.index("{", start)
    depth = 1
    end = opening + 1
    while depth and end < len(source):
        depth += (source[end] == "{") - (source[end] == "}")
        end += 1
    if depth:
        raise ValueError("Unclosed production member: " + marker)
    return source[start:end]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default=shutil.which("dotnet"))
    args = parser.parse_args()
    if not args.dotnet:
        parser.error("Install .NET SDK 10 or supply --dotnet")
    source = (ROOT / "Assets/Scripts/MDPro3/Duel/TcpHelper.cs").read_text(encoding="utf-8-sig")
    if ".Abort(" in source:
        raise ValueError("TCP transport must stop through socket lifetime, not Thread.Abort")
    methods = [
        "public static bool LinkStart(", "private static bool Join(",
        "public static void InitializeSender(", "public static void Receiver()",
        "private static void Receiver(TcpClient", "private static void MarkDisconnected(",
        "private static bool TryCloseConnection(", "public static void AddDateJumoLine(",
        "public static void PerFrameFunction(", "public static void Send(",
        "private static void Sender(", "public static void CtosMessage_ExternalAddress(",
        "public static void CtosMessage_PlayerInfo(", "public static void CtosMessage_JoinGame(",
    ]
    fields = source[source.index("public static volatile TcpClient tcpClient;"):
                    source.index("public static bool LinkStart(")]
    production = """using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MDPro3.Duel.YGOSharp;
using MDPro3.Net;
using MDPro3.Servant;
using UnityEngine;
namespace MDPro3 { public static class TcpHelper {
""" + fields + "\n" + "\n".join(extract(source, marker) for marker in methods) + "\n}\n"
    for marker in ("public class Package", "public class BinaryMaster", "public class SocketMaster",
                   "public class TcpClientWithTimeout"):
        production += extract(source, marker) + "\n"
    production += "public static class BinaryExtensions {\n" + extract(source,
        "public static void WriteUnicode(") + "\n}\n}\n"
    frame = extract(source, "public static void PerFrameFunction(")
    room_methods = sorted(set(re.findall(r"Program.instance.room.(StocMessage_\w+)\(", frame)))
    room_stubs = "\n".join(
        ("public void " + method + "(BinaryReader reader) { " +
         ("LastChat = reader.ReadInt32(); ChatCount++;" if method == "StocMessage_Chat" else "") + " }")
        for method in room_methods)
    stubs = (Path(__file__).with_name("Stubs.cs").read_text(encoding="utf-8")
             .replace("@@ROOM_METHODS@@", room_stubs))
    with tempfile.TemporaryDirectory(prefix="mdpro3-tcp-transport-") as temporary:
        folder = Path(temporary)
        (folder / "Production.cs").write_text(production, encoding="utf-8")
        (folder / "Stubs.cs").write_text(stubs, encoding="utf-8")
        shutil.copyfile(Path(__file__).with_name("Program.cs"), folder / "Program.cs")
        enum_path = escape(str(ROOT / "Assets/Scripts/MDPro3/Duel/YGOSharp/YGOSharpNetworkEnums.cs"))
        (folder / "Transport.csproj").write_text('''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <LangVersion>9.0</LangVersion><ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup><Compile Include="''' + enum_path + '''" Link="Production/NetworkEnums.cs" /></ItemGroup>
</Project>
''', encoding="utf-8")
        result = subprocess.run([args.dotnet, "run", "--project", str(folder / "Transport.csproj"),
                                 "-c", "Release"], cwd=folder, check=False, timeout=45)
        return result.returncode


if __name__ == "__main__":
    raise SystemExit(main())
