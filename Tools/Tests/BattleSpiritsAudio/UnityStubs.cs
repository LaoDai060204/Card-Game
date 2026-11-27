using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
namespace Cysharp.Threading.Tasks
{
    [AsyncMethodBuilder(typeof(AsyncUniTaskMethodBuilder<>))]
    public struct UniTask<T> { internal Task<T> Inner; public TaskAwaiter<T> GetAwaiter() => Inner.GetAwaiter(); }
    public struct AsyncUniTaskMethodBuilder<T>
    {
        private AsyncTaskMethodBuilder<T> inner;
        public static AsyncUniTaskMethodBuilder<T> Create() => new AsyncUniTaskMethodBuilder<T> { inner = AsyncTaskMethodBuilder<T>.Create() };
        public UniTask<T> Task => new UniTask<T> { Inner = inner.Task };
        public void SetResult(T value) => inner.SetResult(value);
        public void SetException(Exception exception) => inner.SetException(exception);
        public void SetStateMachine(IAsyncStateMachine stateMachine) => inner.SetStateMachine(stateMachine);
        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine => inner.Start(ref stateMachine);
        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine) where TAwaiter : INotifyCompletion where TStateMachine : IAsyncStateMachine => inner.AwaitOnCompleted(ref awaiter, ref stateMachine);
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine) where TAwaiter : ICriticalNotifyCompletion where TStateMachine : IAsyncStateMachine => inner.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
    }
}
namespace UnityEngine
{
    public class Object
    {
        public string name;
        public bool destroyed;
        public static void Destroy(Object value) { value.destroyed = true; }
    }
    public class MonoBehaviour : Object
    {
        protected object StartCoroutine(IEnumerator routine)
        {
            while (routine.MoveNext()) if (routine.Current is WaitForSecondsRealtime wait) Time.unscaledTime += wait.time;
            return null;
        }
    }
    public class AudioClip : Object
    {
        public float[] data;
        public int frequency, channels;
        public static readonly List<AudioClip> Created = new List<AudioClip>();
        public static AudioClip Create(string name, int samples, int channels, int rate, bool stream)
        {
            var result = new AudioClip { name = name, data = new float[samples], frequency = rate, channels = channels };
            Created.Add(result); return result;
        }
        public bool SetData(float[] samples, int offset) { Array.Copy(samples, 0, data, offset, samples.Length); return true; }
    }
    public class AudioSource : Object
    {
        public float volume = .7f;
        public AudioClip clip;
        public bool loop, playOnAwake = true, isPlaying;
        public int playCount, stopCount;
        public readonly List<(AudioClip clip,float scale)> Shots = new List<(AudioClip,float)>();
        public void PlayOneShot(AudioClip value, float scale = 1) { Shots.Add((value,scale)); }
        public void Play() { playCount++; isPlaying = true; }
        public void Stop() { stopCount++; isPlaying = false; }
    }
    public static class Mathf { public static float Clamp01(float value) => Math.Min(1, Math.Max(0, value)); }
    public static class Time { public static float unscaledTime; }
    public sealed class WaitForSecondsRealtime { public float time; public WaitForSecondsRealtime(float value) { time=value; } }
    public static class Application { public static string streamingAssetsPath, persistentDataPath; }
    public struct AudioConfiguration { }
    public static class AudioSettings
    {
        public static event Action<bool> OnAudioConfigurationChanged;
        public static void Raise() => OnAudioConfigurationChanged?.Invoke(true);
        public static AudioConfiguration GetConfiguration()=>default;
        public static bool Reset(AudioConfiguration config)=>true;
    }
    public enum AudioType { OGGVORBIS }
    public static class Resources
    {
        public static readonly Dictionary<string,AudioClip> Clips = new Dictionary<string,AudioClip>();
        public static readonly List<string> Requests = new List<string>();
        public static T Load<T>(string path) where T : class
        {
            if (!path.StartsWith("BattleSpirits/Audio/",StringComparison.Ordinal)) throw new Exception("Old audio path loaded: "+path);
            Requests.Add(path);
            return Clips.TryGetValue(path,out var value) ? value as T : null;
        }
    }
}
namespace UnityEngine.Networking
{
    public sealed class WebOperation
    {
        public bool isDone => true;
        public TaskAwaiter GetAwaiter() => System.Threading.Tasks.Task.CompletedTask.GetAwaiter();
    }
    public sealed class UnityWebRequest : IDisposable
    {
        public enum Result { Success, ConnectionError }
        public Result result = Result.Success;
        public WebOperation SendWebRequest() => new WebOperation();
        public void Dispose() { }
    }
    public static class UnityWebRequestMultimedia
    {
        public static readonly List<string> Requests = new List<string>();
        public static UnityWebRequest GetAudioClip(string path,UnityEngine.AudioType type) { Requests.Add(path); return new UnityWebRequest(); }
    }
    public static class DownloadHandlerAudioClip
    {
        public static UnityEngine.AudioClip GetContent(UnityWebRequest request)=>new UnityEngine.AudioClip { name="DownloadedBattleAudio" };
    }
}
namespace MDPro3 { public class Manager : UnityEngine.MonoBehaviour { public virtual void Initialize() { } } }
