using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace MDPro3.Utility
{
    // News and avatars are optional resources. Their failures remain visible as
    // diagnostics while callers retain ownership of their local fallback image.
    public static class RemoteImageDownloader
    {
        private const int TimeoutSeconds = 10;
        private const int WarningCapacity = 128;
        private static readonly HashSet<string> WarnedUrls = new HashSet<string>(StringComparer.Ordinal);

        public static async Task<Texture2D> DownloadAsync(string url, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Application.isPlaying) return null;

            if (string.IsNullOrWhiteSpace(url)
                || !Uri.TryCreate(url, UriKind.Absolute, out var address)
                || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
            {
                Warn(url, "expected an absolute HTTP or HTTPS image URL");
                return null;
            }

            string requestUrl = address.AbsoluteUri;
            try
            {
                using (var request = UnityWebRequestTexture.GetTexture(requestUrl))
                {
                    request.timeout = TimeoutSeconds;
                    request.SetRequestHeader("User-Agent", "MDPro3/" + Application.version + " ("
                        + Environment.OSVersion + "); Unity/" + Application.unityVersion);
                    try
                    {
                        var operation = request.SendWebRequest();
                        while (!operation.isDone)
                        {
                            if (!Application.isPlaying)
                            {
                                request.Abort();
                                return null;
                            }
                            await TaskUtility.WaitOneFrame(cancellationToken);
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        if (!Application.isPlaying)
                        {
                            request.Abort();
                            return null;
                        }

                        if (request.result == UnityWebRequest.Result.Success)
                        {
                            var image = DownloadHandlerTexture.GetContent(request);
                            if (image != null) return image;
                            Warn(requestUrl, "the response did not contain a usable image");
                        }
                        else Warn(requestUrl, request.error ?? request.result.ToString());
                        return null;
                    }
                    catch (OperationCanceledException)
                    {
                        request.Abort();
                        // TaskUtility also signals cancellation when Play stops.
                        // Explicit caller cancellation retains its Task contract.
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!Application.isPlaying) return null;
                        throw;
                    }
                }
            }
            catch (Exception error) when (error is ArgumentException
                || error is InvalidOperationException || error is UnityException)
            {
                Warn(requestUrl, error.Message);
                return null;
            }
        }

        private static void Warn(string url, string reason)
        {
            string key = string.IsNullOrWhiteSpace(url) ? "<empty>" : url;
            lock (WarnedUrls)
            {
                if (WarnedUrls.Contains(key)) return;
                if (WarnedUrls.Count >= WarningCapacity) WarnedUrls.Clear();
                WarnedUrls.Add(key);
            }
            Debug.LogWarning("Optional image unavailable [" + key + "]: " + reason);
        }
    }
}
