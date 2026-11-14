using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MDPro3.Utility;
using UnityEngine;
using UnityEngine.Networking;

internal static class RemoteImageTests
{
    private static int passed;
    private static readonly string Url = "https://example.com/image.png";
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static HashSet<string> WarningKeys => (HashSet<string>)typeof(RemoteImageDownloader)
        .GetField("WarnedUrls", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
    private static void Reset()
    {
        Application.isPlaying = true;
        Debug.Warnings.Clear(); WarningKeys.Clear();
        UnityWebRequestTexture.Created.Clear(); UnityWebRequestTexture.Factory = null;
        TaskUtility.OnFrame = null; TaskUtility.Frames = 0;
    }
    private static async Task Case(string name, Func<Task> action)
    { Reset(); await action(); passed++; Console.WriteLine("PASS " + name); }
    private static void OneRequestCleaned(UnityWebRequest request)
    { Assert(UnityWebRequestTexture.Created.Count == 1 && request.Disposed, "Request was not disposed exactly as owned."); }
    private static async Task Canceled(Task<Texture2D> operation, CancellationToken token)
    {
        try { await operation; throw new Exception("Cancellation did not propagate."); }
        catch (OperationCanceledException exception) { Assert(exception.CancellationToken == token, "Cancellation token changed."); }
    }
    private static async Task<int> Main()
    {
        await Case("Invalid optional image URLs do not create a request", async () =>
        {
            foreach (string value in new[] { null, "", "  ", "relative/image.png", "ftp://example.com/image.png", "file:///image.png" })
                Assert(await RemoteImageDownloader.DownloadAsync(value) == null, "Invalid URL was accepted.");
            Assert(UnityWebRequestTexture.Created.Count == 0 && Debug.Warnings.Count >= 4, "Invalid inputs created requests or lost diagnostics.");
        });
        await Case("Stopped Play exits before a request", async () =>
        {
            Application.isPlaying = false;
            Assert(await RemoteImageDownloader.DownloadAsync(Url) == null, "Stopped Play returned an image.");
            Assert(UnityWebRequestTexture.Created.Count == 0 && Debug.Warnings.Count == 0, "Stopped Play performed work.");
        });
        await Case("A pre-canceled caller creates no request", async () =>
        {
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            await Canceled(RemoteImageDownloader.DownloadAsync(Url, cancellation.Token), cancellation.Token);
            Assert(UnityWebRequestTexture.Created.Count == 0 && Debug.Warnings.Count == 0, "Pre-cancellation performed work.");
        });
        await Case("A successful image keeps ownership after request disposal", async () =>
        {
            var request = new UnityWebRequest(); UnityWebRequestTexture.Factory = _ => request;
            var image = await RemoteImageDownloader.DownloadAsync(Url);
            Assert(ReferenceEquals(image, request.Image) && request.Decodes == 1 && !request.Aborted, "Successful texture changed ownership or was decoded repeatedly.");
            OneRequestCleaned(request);
            Assert(Debug.Warnings.Count == 0, "Success emitted a warning.");
        });
        await Case("Requests retain the User-Agent and a ten-second native timeout", async () =>
        {
            var request = new UnityWebRequest(); UnityWebRequestTexture.Factory = _ => request;
            await RemoteImageDownloader.DownloadAsync(Url);
            Assert(request.timeout == 10 && request.Headers["User-Agent"].StartsWith("MDPro3/1.4.7 (", StringComparison.Ordinal)
                && request.Headers["User-Agent"].EndsWith("); Unity/6000.0.24f1", StringComparison.Ordinal), "Image request settings changed.");
        });
        await Case("Legacy HTTP remains HTTP without a fabricated alternate URL", async () =>
        {
            const string original = "http://pic.4j4j.cn/upload/pic/20140327/af650cf463.jpg";
            await RemoteImageDownloader.DownloadAsync(original);
            Assert(UnityWebRequestTexture.Created.Single().url == original, "The external source URL was rewritten.");
        });
        await Case("A DNS failure returns null with its source diagnostic", async () =>
        {
            var request = new UnityWebRequest { result = UnityWebRequest.Result.ConnectionError, error = "Cannot resolve destination host" };
            UnityWebRequestTexture.Factory = _ => request;
            Assert(await RemoteImageDownloader.DownloadAsync(Url) == null, "DNS failure returned an image.");
            OneRequestCleaned(request);
            Assert(request.Decodes == 0 && Debug.Warnings.Single().Contains(Url) && Debug.Warnings.Single().Contains(request.error), "DNS diagnostic or decode guard is missing.");
        });
        await Case("An HTTP error does not decode image content", async () =>
        {
            var request = new UnityWebRequest { result = UnityWebRequest.Result.ProtocolError, error = "HTTP 404" };
            UnityWebRequestTexture.Factory = _ => request;
            Assert(await RemoteImageDownloader.DownloadAsync(Url) == null && request.Decodes == 0, "HTTP error was decoded.");
            OneRequestCleaned(request);
        });
        await Case("A data-processing error remains an optional failure", async () =>
        {
            var request = new UnityWebRequest { result = UnityWebRequest.Result.DataProcessingError, error = "Invalid PNG" };
            UnityWebRequestTexture.Factory = _ => request;
            Assert(await RemoteImageDownloader.DownloadAsync(Url) == null && Debug.Warnings.Count == 1, "Decode transport error escaped.");
            OneRequestCleaned(request);
        });
        await Case("A nominally successful response still requires an image", async () =>
        {
            var request = new UnityWebRequest { Image = null }; UnityWebRequestTexture.Factory = _ => request;
            Assert(await RemoteImageDownloader.DownloadAsync(Url) == null && Debug.Warnings.Single().Contains("usable image"), "Empty decoded content was accepted.");
            OneRequestCleaned(request);
        });
        await Case("A Unity image decode exception is diagnosed without escaping", async () =>
        {
            var request = new UnityWebRequest { DecodeFailure = new UnityException("Texture decode failed") }; UnityWebRequestTexture.Factory = _ => request;
            Assert(await RemoteImageDownloader.DownloadAsync(Url) == null && Debug.Warnings.Single().Contains("Texture decode failed"), "Expected Unity decode failure escaped.");
            OneRequestCleaned(request);
        });
        await Case("Repeated failed URLs log once while every requested attempt can retry", async () =>
        {
            UnityWebRequestTexture.Factory = _ => new UnityWebRequest { result = UnityWebRequest.Result.ConnectionError, error = "DNS unavailable" };
            await RemoteImageDownloader.DownloadAsync(Url); await RemoteImageDownloader.DownloadAsync(Url);
            Assert(Debug.Warnings.Count == 1 && UnityWebRequestTexture.Created.Count == 2, "Diagnostics repeat or failures became a permanent negative cache.");
        });
        await Case("A recovered URL can succeed after a failed download", async () =>
        {
            UnityWebRequestTexture.Factory = _ => new UnityWebRequest { result = UnityWebRequest.Result.ConnectionError, error = "DNS unavailable" };
            Assert(await RemoteImageDownloader.DownloadAsync(Url) == null, "Failure fixture failed.");
            var recovered = new UnityWebRequest(); UnityWebRequestTexture.Factory = _ => recovered;
            Assert(ReferenceEquals(await RemoteImageDownloader.DownloadAsync(Url), recovered.Image), "A recovered address stayed blocked.");
        });
        await Case("A pending request yields frames until completion", async () =>
        {
            var request = new UnityWebRequest(); request.Operation.isDone = false; UnityWebRequestTexture.Factory = _ => request;
            TaskUtility.OnFrame = _ => { if (TaskUtility.Frames == 3) request.Operation.isDone = true; };
            Assert(await RemoteImageDownloader.DownloadAsync(Url) == request.Image && TaskUtility.Frames == 3, "Pending operation did not wait through frames.");
            OneRequestCleaned(request);
        });
        await Case("Caller cancellation aborts and disposes an in-flight request", async () =>
        {
            using var cancellation = new CancellationTokenSource();
            var request = new UnityWebRequest(); request.Operation.isDone = false; UnityWebRequestTexture.Factory = _ => request;
            TaskUtility.OnFrame = _ => cancellation.Cancel();
            await Canceled(RemoteImageDownloader.DownloadAsync(Url, cancellation.Token), cancellation.Token);
            Assert(request.Aborted && request.Decodes == 0 && Debug.Warnings.Count == 0, "Canceled operation leaked or warned as a failed image.");
            OneRequestCleaned(request);
        });
        await Case("Stopping Play during the frame wait aborts without a fault", async () =>
        {
            var request = new UnityWebRequest(); request.Operation.isDone = false; UnityWebRequestTexture.Factory = _ => request;
            TaskUtility.OnFrame = _ => Application.isPlaying = false;
            Assert(await RemoteImageDownloader.DownloadAsync(Url) == null && request.Aborted && Debug.Warnings.Count == 0, "Stopped Play escaped as a task error.");
            OneRequestCleaned(request);
        });
        await Case("Cancellation after completion still prevents assigning the image", async () =>
        {
            using var cancellation = new CancellationTokenSource();
            var request = new UnityWebRequest { OnSend = () => cancellation.Cancel() }; UnityWebRequestTexture.Factory = _ => request;
            await Canceled(RemoteImageDownloader.DownloadAsync(Url, cancellation.Token), cancellation.Token);
            Assert(request.Aborted && request.Decodes == 0, "Late cancellation decoded an image.");
            OneRequestCleaned(request);
        });
        await Case("A Play stop observed after completion never decodes a late texture", async () =>
        {
            var request = new UnityWebRequest { OnSend = () => Application.isPlaying = false }; UnityWebRequestTexture.Factory = _ => request;
            Assert(await RemoteImageDownloader.DownloadAsync(Url) == null && request.Aborted && request.Decodes == 0, "Late Play stop assigned content.");
            OneRequestCleaned(request);
        });
        await Case("Expected request setup failures dispose and diagnose the request", async () =>
        {
            var request = new UnityWebRequest { SendFailure = new InvalidOperationException("Cannot send image") }; UnityWebRequestTexture.Factory = _ => request;
            Assert(await RemoteImageDownloader.DownloadAsync(Url) == null && Debug.Warnings.Single().Contains("Cannot send image"), "Expected setup failure escaped.");
            OneRequestCleaned(request);
        });
        await Case("Unexpected implementation failures are not globally swallowed", async () =>
        {
            var request = new UnityWebRequest { SendFailure = new NotSupportedException("Unexpected implementation failure") }; UnityWebRequestTexture.Factory = _ => request;
            bool escaped = false;
            try { await RemoteImageDownloader.DownloadAsync(Url); } catch (NotSupportedException) { escaped = true; }
            Assert(escaped && Debug.Warnings.Count == 0, "Unexpected failure was hidden as an optional download.");
            OneRequestCleaned(request);
        });
        await Case("Warning deduplication has a bounded cache", async () =>
        {
            UnityWebRequestTexture.Factory = _ => new UnityWebRequest { result = UnityWebRequest.Result.ConnectionError, error = "Unavailable" };
            for (int i = 0; i < 130; i++) await RemoteImageDownloader.DownloadAsync("https://example.com/" + i + ".png");
            Assert(WarningKeys.Count <= 128 && Debug.Warnings.Count == 130, "Warning cache is unbounded or lost distinct diagnostics.");
        });
        Console.WriteLine("PASS " + passed + " production RemoteImageDownloader scenarios; controlled Unity/request APIs, no live network or Unity Play/render/build.");
        return 0;
    }
}
