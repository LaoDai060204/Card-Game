#!/usr/bin/env python3
"""Run the actual URL/version/news parsing methods without a Unity Editor.

Requires a .NET 10 SDK. The SDK's bundled Newtonsoft.Json is used only by this
isolated test harness; the game keeps its existing Unity package dependency.
"""
import argparse
from pathlib import Path
import shutil
import subprocess
import tempfile
import textwrap


ROOT = Path(__file__).resolve().parents[2]


def extract(source, marker):
    start = source.index(marker)
    opening = source.index("{", start)
    depth = 0
    for offset in range(opening, len(source)):
        if source[offset] == "{":
            depth += 1
        elif source[offset] == "}":
            depth -= 1
            if depth == 0:
                return source[start:offset + 1]
    raise ValueError("Unclosed production method/class: " + marker)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default=shutil.which("dotnet"), help="Path to a .NET 10 dotnet executable")
    args = parser.parse_args()
    if not args.dotnet:
        parser.error("Install .NET SDK 10 or pass --dotnet /path/to/dotnet")

    scripts = ROOT / "Assets/Scripts/MDPro3"
    settings = (scripts / "Helper/Settings.cs").read_text(encoding="utf-8")
    online = (scripts / "Net/OnlineService.cs").read_text(encoding="utf-8")
    models = (scripts / "Net/MyCard.cs").read_text(encoding="utf-8")
    methods = [
        extract(settings, "public static bool IsRetiredPrereleaseUrl("),
        extract(settings, "public static bool TryReadPrereleaseVersion("),
        extract(online, "public static string GetRequestDisplayUrl("),
        extract(online, "private static bool TryParseMyCardNews(").replace("private static bool", "public static bool", 1),
    ]
    classes = [extract(models, "public class MyCardNews\n"), extract(models, "public class News\n")]

    with tempfile.TemporaryDirectory(prefix="mdpro3-network-tests-") as temporary:
        test = Path(temporary)
        (test / "NetworkMetadata.csproj").write_text(textwrap.dedent("""\
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
                <LangVersion>9.0</LangVersion><ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="Newtonsoft.Json"><HintPath>$(MSBuildSDKsPath)/../Newtonsoft.Json.dll</HintPath></Reference>
              </ItemGroup>
            </Project>
            """), encoding="utf-8")
        (test / "ProductionMethods.cs").write_text(
            "using System; using System.IO; using Newtonsoft.Json; using Newtonsoft.Json.Linq;\n"
            + "namespace MDPro3.Net {\n" + "\n".join(classes) + "\n"
            + "public static class ProductionMetadata {\n" + "\n".join(methods) + "\n}}\n",
            encoding="utf-8")
        (test / "Program.cs").write_text(PROGRAM, encoding="utf-8")
        subprocess.run([args.dotnet, "run", "--project", str(test / "NetworkMetadata.csproj"), "-c", "Release"], check=True, cwd=ROOT)


PROGRAM = r'''using System;
using MDPro3.Net;

internal static class Program
{
    private static int passed;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        passed++;
    }
    private static void News(string json, bool expected, bool hasNews, string name)
    {
        var actual = ProductionMetadata.TryParseMyCardNews(json, out var news);
        Check(actual == expected && (news != null) == hasNews, name);
    }
    private static void Main()
    {
        foreach (var url in new[] {
            "https://github.com/ElderLich/TransSuperpre/raw/main/EN/version.txt",
            "https://raw.githubusercontent.com/ElderLich/TransSuperpre/main/EN/version.txt",
            "https://GITHUB.COM/elderlich/transsuperpre",
            "https://github.com/ElderLich/TransSuperpre/",
            "https://raw.githubusercontent.com/ElderLich/TransSuperpre/main/DE/file.ypk?download=1"
        }) Check(ProductionMetadata.IsRetiredPrereleaseUrl(url), "Retired source: " + url);
        foreach (var url in new[] {
            null, "", "not a URL", "file:///ElderLich/TransSuperpre/main/EN/version.txt",
            "https://cdn02.moecube.com:444/ygopro-super-pre/data/version.txt",
            "https://example.com/ElderLich/TransSuperpre/version.txt",
            "https://github.com/OtherOwner/TransSuperpre/raw/main/EN/version.txt",
            "https://github.com/ElderLich/TransSuperpre-Active/raw/main/EN/version.txt",
            "https://raw.githubusercontent.com/ElderLich/AnotherRepo/main/EN/version.txt",
            "https://example.com/?source=https://github.com/ElderLich/TransSuperpre"
        }) Check(!ProductionMetadata.IsRetiredPrereleaseUrl(url), "Custom/non-source preserved");

        Check(ProductionMetadata.TryReadPrereleaseVersion("1793029945\n", out var version) && version == "1793029945", "Timestamp version");
        Check(ProductionMetadata.TryReadPrereleaseVersion(" v1.2.0 \r\nnotes", out version) && version == "v1.2.0", "Custom version text");
        foreach (var response in new[] { null, "", " \t\r\n", "\n1793029945", "<!DOCTYPE html>\n<html>", "<html>", new string('1', 129) })
            Check(!ProductionMetadata.TryReadPrereleaseVersion(response, out version), "Invalid version rejected");

        News("[{\"id\":\"ygopro\",\"news\":{\"zh-CN\":[{\"title\":\"Title\",\"image\":\"https://example.com/a.png\"}]}}]", true, true, "Valid feed");
        ProductionMetadata.TryParseMyCardNews("[{\"id\":\"ygopro\",\"news\":{\"en-US\":[{\"title\":\"English\"}]}}]", out var parsed);
        Check(parsed.EnglishUS[0].title == "English" && parsed.ChineseCN == null, "Source model language attributes");
        News("[{\"id\":\"ygopro\",\"news\":{}}]", true, true, "Empty news object");
        News("[{\"id\":\"ygopro\",\"news\":null}]", true, false, "Null news");
        News("[{\"id\":\"ygopro\",\"news\":[]}]", true, false, "Absent array news");
        News("[{\"id\":\"ygopro\"}]", true, false, "Missing news");
        News("[null,{\"id\":\"ygopro\",\"news\":{}}]", true, true, "Null unrelated entry");
        News("[{\"id\":{},\"news\":{}}]", false, false, "Invalid ID object");
        News("[{\"id\":[],\"news\":{}}]", false, false, "Invalid ID array");
        News("[{\"id\":3,\"news\":{}}]", false, false, "Numeric unrelated ID");
        News("[{\"id\":\"ygopro\",\"news\":[{}]}]", false, false, "Unexpected nonempty news array");
        News("[{\"id\":\"ygopro\",\"news\":\"bad\"}]", false, false, "Invalid news scalar");
        News("[{\"id\":\"ygopro\",\"news\":{\"zh-CN\":\"bad\"}}]", false, false, "Invalid language list");
        News("[{\"id\":\"ygopro\",\"news\":{\"zh-CN\":[{\"title\":{}}]}}]", false, false, "Invalid story title");
        foreach (var json in new[] { null, "", "{broken", "{}", "[]", "[{\"id\":\"other\"}]" })
            News(json, false, false, "Invalid or unmatched feed");

        var display = ProductionMetadata.GetRequestDisplayUrl("https://name:secret@example.com:8443/news?token=private#section");
        Check(display == "https://example.com:8443/news", "Diagnostic URL excludes credentials, query and fragment");
        Check(ProductionMetadata.GetRequestDisplayUrl(null) == "(invalid URL)", "Missing diagnostic URL");
        Console.WriteLine("PASS: " + passed + " production network metadata checks.");
    }
}
'''


if __name__ == "__main__":
    main()
