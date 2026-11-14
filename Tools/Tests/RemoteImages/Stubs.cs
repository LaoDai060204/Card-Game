using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UnityEngine
{
    public sealed class Texture2D { public string Label; }
    public sealed class UnityException : Exception { public UnityException(string message) : base(message) { } }
    public static class Application
    {
        public static bool isPlaying = true;
        public const string version = "1.4.7";
        public const string unityVersion = "6000.0.24f1";
    }
    public static class Debug
    {
        public static readonly List<string> Warnings = new List<string>();
        public static void LogWarning(object value) => Warnings.Add(value.ToString());
    }
}

namespace UnityEngine.Networking
{
    public sealed class UnityWebRequestAsyncOperation { public bool isDone; }
    public sealed class UnityWebRequest : IDisposable
    {
        public enum Result { InProgress, Success, ConnectionError, ProtocolError, DataProcessingError }
        public string url;
        public int timeout;
        public Result result = Result.Success;
        public string error;
        public readonly Dictionary<string, string> Headers = new Dictionary<string, string>();
        public readonly UnityWebRequestAsyncOperation Operation = new UnityWebRequestAsyncOperation { isDone = true };
        public Texture2D Image = new Texture2D { Label = "downloaded" };
        public Exception SendFailure, DecodeFailure;
        public Action OnSend;
        public bool Aborted, Disposed;
        public int Sends, Decodes;
        public void SetRequestHeader(string name, string value) => Headers[name] = value;
        public UnityWebRequestAsyncOperation SendWebRequest()
        {
            Sends++;
            if (SendFailure != null) throw SendFailure;
            OnSend?.Invoke();
            return Operation;
        }
        public void Abort() { Aborted = true; Operation.isDone = true; }
        public void Dispose() { Disposed = true; }
    }
    public static class UnityWebRequestTexture
    {
        public static readonly List<UnityWebRequest> Created = new List<UnityWebRequest>();
        public static Func<string, UnityWebRequest> Factory;
        public static UnityWebRequest GetTexture(string url)
        {
            var request = Factory == null ? new UnityWebRequest() : Factory(url);
            request.url = url;
            Created.Add(request);
            return request;
        }
    }
    public static class DownloadHandlerTexture
    {
        public static Texture2D GetContent(UnityWebRequest request)
        {
            request.Decodes++;
            if (request.DecodeFailure != null) throw request.DecodeFailure;
            return request.Image;
        }
    }
}

namespace MDPro3.Utility
{
    public static class TaskUtility
    {
        public static int Frames;
        public static Action<CancellationToken> OnFrame;
        public static Task WaitOneFrame(CancellationToken token)
        {
            Frames++;
            OnFrame?.Invoke(token);
            if (!UnityEngine.Application.isPlaying) throw new OperationCanceledException();
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
