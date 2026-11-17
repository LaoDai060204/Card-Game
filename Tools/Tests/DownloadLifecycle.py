#!/usr/bin/env python3
"""Exercise production download coroutines with controlled transport responses.

Compiles production SettingServant methods and NetUtil under C# 9/.NET 10.
The transport/UI stand-ins do not exercise Unity or live HTTPS.
"""
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
    return source[start:end]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default=shutil.which("dotnet"))
    args = parser.parse_args()
    if not args.dotnet:
        parser.error("Install .NET SDK 10 or pass --dotnet /path/to/dotnet")
    source = (ROOT / "Assets/Scripts/MDPro3/Servant/SettingServant.cs").read_text()
    methods = "\n".join(extract(source, marker) for marker in (
        "private struct UpdatePreleaseHints", "public void UpdatePrerelease()",
        "private IEnumerator UpdatePrereleaseChunkedCoroutine()",
        "private void DownloadYPK(string url)", "private IEnumerator DownloadYpkCoroutine("))
    settings = (ROOT / "Assets/Scripts/MDPro3/Helper/Settings.cs").read_text()
    with tempfile.TemporaryDirectory(prefix="mdpro3-download-lifecycle-") as temporary:
        folder = Path(temporary)
        (folder / "DownloadLifecycle.csproj").write_text('''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <LangVersion>9.0</LangVersion><ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable>
  </PropertyGroup>
</Project>''')
        (folder / "NetUtil.cs").write_text((ROOT / "Assets/Scripts/MDPro3/Net/NetUtil.cs").read_text())
        (folder / "Program.cs").write_text(PROGRAM.replace("@@METHODS@@", methods).replace(
            "@@RETIRED@@", extract(settings, "public static bool IsRetiredPrereleaseUrl(")))
        return subprocess.run([args.dotnet, "run", "--project", str(folder / "DownloadLifecycle.csproj"),
                               "-c", "Release"], cwd=ROOT, check=False).returncode


PROGRAM = r'''using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MDPro3.Net;

public class ProbeSetting
{
    private bool checkingPrereleaseUpdate, downloadingYPK;
    private readonly UpdatePreleaseHints pHints = new UpdatePreleaseHints();
    public readonly SettingServantUI UI = new SettingServantUI();
    public IEnumerator Pending;
    private T GetUI<T>() => (T)(object)UI;
    private void StartCoroutine(IEnumerator action) { Pending = action; }
    public bool Busy => checkingPrereleaseUpdate || downloadingYPK;
    public IEnumerator BeginUpdate() { checkingPrereleaseUpdate = true; return UpdatePrereleaseChunkedCoroutine(); }
    public void BeginPackage(string url) => DownloadYPK(url);
    @@METHODS@@
}

public class Button { public string Text = ""; public void SetModeText(string value) { Text = value; } }
public class SettingServantUI { public Button ButtonUpdatePrerelease = new Button(), ButtonDownloadYPK = new Button(); }
public class WaitForSeconds { public WaitForSeconds(float seconds) {} }
public static class Mathf { public static float Clamp01(float value) => Math.Clamp(value, 0, 1); }
public static class Debug { public static void LogWarning(string value) {} public static void LogError(string value) {} }
public static class MessageManager { public static string Last; public static void Cast(string value) { Last = value; } public static void Toast(string value) { Last = value; } }
public static class InterString { public static string Get(string value) => value; }
public static class Config
{
    public const string STRING_NO = "0";
    public static Dictionary<string,string> Values = new Dictionary<string,string>();
    public static int Saves;
    public static void Set(string key, string value) { Values[key] = value; }
    public static string Get(string key, string fallback) => Values.TryGetValue(key, out var value) ? value : fallback;
    public static void Save() { Saves++; }
}
public static class Language
{
    public static string Current = "en-US", Installed = "en-US";
    public static string GetConfig() => Current;
    public static string GetPrereleaseConfig() => Installed;
    public static void SetPrereleaseConfig(string value) { Installed = value; }
}
public class Program
{
    public static string PATH_EXPANSIONS;
    public static Program instance = new Program();
    public int Reloads;
    public void InitializeForDataChange() { Reloads++; }
}
public static class Settings
{
    public static string Url = "https://example.com/pack.ypk?token=private";
    public static string GetPrereleasePackUrl() => Url;
    @@RETIRED@@
}
public static class OnlineService { public static void LogRequestFailure(UnityWebRequest request, string operation) {} }

public class DownloadHandlerBuffer { public byte[] data = Array.Empty<byte>(); }
public class Operation { public bool isDone = true; }
public class UnityWebRequest : IDisposable
{
    public enum Result { Success, ConnectionError, ProtocolError }
    public class Response
    {
        public Result Result = Result.Success;
        public long Code = 200;
        public string Range;
        public byte[] Body = Array.Empty<byte>();
        public string Etag = "new", Length = "12";
    }
    public static Queue<Response> Responses = new Queue<Response>();
    public static List<UnityWebRequest> Requests = new List<UnityWebRequest>();
    public Result result; public long responseCode; public string error = "test transport";
    public int timeout; public bool isDone, Disposed; public string Range;
    public ulong downloadedBytes => (ulong)downloadHandler.data.Length;
    public float downloadProgress => 1;
    public DownloadHandlerBuffer downloadHandler = new DownloadHandlerBuffer();
    private Response response;
    public static UnityWebRequest Get(string url) => new UnityWebRequest();
    public static UnityWebRequest Head(string url) => new UnityWebRequest();
    public void SetRequestHeader(string key, string value) { Range = value; }
    public string GetResponseHeader(string key) => key == "ETag" ? response.Etag : key == "Content-Length" ? response.Length : response.Range;
    public Operation SendWebRequest()
    {
        response = Responses.Dequeue(); result = response.Result; responseCode = response.Code;
        downloadHandler.data = response.Body; isDone = true; Requests.Add(this); return new Operation();
    }
    public void Dispose() { Disposed = true; }
}

public static class Tests
{
    private static int passed;
    private static readonly byte[] Old = Encoding.UTF8.GetBytes("old-content!"), New = Encoding.UTF8.GetBytes("new-content!");
    private static string folder, target;
    private static void Check(bool condition, string name)
    { if (!condition) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
    private static ProbeSetting Reset(bool installed = true)
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        target = Path.Combine(folder, "pack.ypk");
        Program.PATH_EXPANSIONS = folder;
        if (installed) { Directory.CreateDirectory(folder); File.WriteAllBytes(target, Old); }
        UnityWebRequest.Responses.Clear(); UnityWebRequest.Requests.Clear();
        Config.Values.Clear(); Config.Values["Prerelease"] = "old"; Config.Saves = 0;
        Program.instance.Reloads = 0; Settings.Url = "https://example.com/pack.ypk?token=private";
        Language.Current = Language.Installed = "en-US"; MessageManager.Last = null;
        return new ProbeSetting();
    }
    private static UnityWebRequest.Response Reply(byte[] body = null, long code = 200, string range = null)
        => new UnityWebRequest.Response { Body = body ?? Array.Empty<byte>(), Code = code, Range = range };
    private static void Plan(bool range, UnityWebRequest.Response download)
    {
        UnityWebRequest.Responses.Enqueue(Reply());
        UnityWebRequest.Responses.Enqueue(Reply(new byte[1], range ? 206 : 200, "bytes 0-0/12"));
        UnityWebRequest.Responses.Enqueue(download);
    }
    private static void Run(IEnumerator coroutine)
    {
        using (coroutine as IDisposable)
        { int steps = 0; while (coroutine.MoveNext()) if (++steps > 20) throw new Exception("Coroutine did not finish"); }
    }
    private static bool Clean(ProbeSetting probe) => !probe.Busy && probe.UI.ButtonUpdatePrerelease.Text == ""
        && (!Directory.Exists(folder) || Directory.GetFiles(folder, "*.download").Length == 0)
        && UnityWebRequest.Requests.TrueForAll(r => r.Disposed && r.timeout > 0);
    public static void Main()
    {
        folder = Path.Combine(Path.GetTempPath(), "mdpro3-expansions-" + Guid.NewGuid().ToString("N"));
        try
        {
            var probe = Reset(false); Plan(true, Reply(New, 206, "bytes 0-11/12")); Run(probe.BeginUpdate());
            Check(File.ReadAllText(target) == "new-content!" && Program.instance.Reloads == 1 && Clean(probe), "Fresh clone creates Expansions and installs verified range pack");

            probe = Reset(); Plan(false, Reply(New)); Run(probe.BeginUpdate());
            Check(File.ReadAllText(target) == "new-content!" && Config.Get("Prerelease", "") == "new" && Clean(probe), "Full download atomically replaces installed pack");

            probe = Reset(); Plan(true, Reply(Array.Empty<byte>(), 206, "bytes 0-11/12")); Run(probe.BeginUpdate());
            Check(File.ReadAllText(target) == "old-content!" && Config.Get("Prerelease", "") == "old" && Clean(probe), "Empty successful range fails promptly and preserves installed pack");

            probe = Reset(); Plan(true, Reply(New, 200)); Run(probe.BeginUpdate());
            Check(File.ReadAllText(target) == "old-content!" && Clean(probe), "Server ignoring range cannot corrupt installed pack");

            probe = Reset(); Plan(true, Reply(New, 206, "bytes 1-12/13")); Run(probe.BeginUpdate());
            Check(File.ReadAllText(target) == "old-content!" && Clean(probe), "Wrong Content-Range preserves installed pack");

            probe = Reset(); Plan(false, Reply(new byte[3])); Run(probe.BeginUpdate());
            Check(File.ReadAllText(target) == "old-content!" && Clean(probe), "Truncated full download preserves installed pack");

            probe = Reset(); Plan(true, Reply(New, 206, "bytes 0-11/12")); var action = probe.BeginUpdate();
            action.MoveNext(); action.MoveNext(); action.MoveNext(); (action as IDisposable).Dispose();
            Check(File.ReadAllText(target) == "old-content!" && Clean(probe), "Cancelled coroutine disposes active range requests and resets busy state");

            probe = Reset(); Settings.Url = "https://github.com/ElderLich/TransSuperpre/raw/main/EN/pack.ypk"; probe.UpdatePrerelease();
            Check(probe.Pending == null && UnityWebRequest.Requests.Count == 0 && Config.Saves == 0 && !probe.Busy, "Retired source keeps installed data without a request or config write");

            probe = Reset(); Language.Current = "ja-JP"; Plan(false, Reply(New)); Run(probe.BeginUpdate());
            Check(Language.Installed == "ja-JP" && Config.Get("Prerelease", "") == "new", "Successful update records installed language and ETag together");

            probe = Reset(); UnityWebRequest.Responses.Enqueue(Reply(New));
            probe.BeginPackage("https://example.com/pack.ypk?token=private"); Run(probe.Pending);
            Check(File.ReadAllText(target) == "new-content!" && Clean(probe), "Manual signed URL saves decoded filename and keeps TLS defaults");

            probe = Reset(); UnityWebRequest.Responses.Enqueue(new UnityWebRequest.Response { Result = UnityWebRequest.Result.ConnectionError });
            probe.BeginPackage("https://example.com/pack.ypk"); Run(probe.Pending);
            Check(File.ReadAllText(target) == "old-content!" && Clean(probe) && MessageManager.Last == "Download failed.", "Manual transport failure preserves pack and clears busy state");
            Console.WriteLine("PASS: " + passed + " production download lifecycle scenarios; controlled transport, no Unity/HTTPS execution.");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}
'''


if __name__ == "__main__":
    raise SystemExit(main())
