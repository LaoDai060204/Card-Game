using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace MDPro3.BattleSpirits.Visuals
{
    /// <summary>Reference-counted textures: six idle pairs, four concurrent reads.
    /// Live field cards are pinned; the catalog bounds the set of live keys.</summary>
    internal sealed class BattleTextureCache : IDisposable
    {
        private sealed class Entry
        {
            internal string path;
            internal int references;
            internal long lastUsed;
            internal Texture2D texture;
            internal UnityWebRequest request;
            internal Coroutine coroutine;
            internal readonly List<Action<Texture2D>> waiting = new List<Action<Texture2D>>();
        }

        private const int IdleLimit = 12;
        private const int RequestLimit = 4;
        private readonly MonoBehaviour owner;
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private long clock;
        private int reading;
        private bool disposed;
        internal bool Paused { get; set; }

        internal BattleTextureCache(MonoBehaviour owner) { this.owner = owner; }

        internal void Acquire(string path, Action<Texture2D> completed)
        {
            if (disposed || string.IsNullOrEmpty(path)) { completed?.Invoke(null); return; }
            if (!entries.TryGetValue(path, out var entry))
            {
                entry = new Entry { path = path };
                entries.Add(path, entry);
            }
            entry.references++;
            entry.lastUsed = ++clock;
            if (entry.texture != null) { completed?.Invoke(entry.texture); return; }
            if (completed != null) entry.waiting.Add(completed);
            if (entry.coroutine == null) entry.coroutine = owner.StartCoroutine(Read(entry));
        }

        internal void Release(string path)
        {
            if (disposed || string.IsNullOrEmpty(path) || !entries.TryGetValue(path, out var entry)) return;
            entry.references = Math.Max(0, entry.references - 1);
            entry.lastUsed = ++clock;
            if (entry.references == 0 && entry.coroutine != null && entry.request == null)
            {
                // A superseded snapshot should not decode artwork still
                // waiting for a read slot. In-flight reads finish safely.
                owner.StopCoroutine(entry.coroutine);
                entry.coroutine = null;
                entry.waiting.Clear();
            }
            Trim();
        }

        private IEnumerator Read(Entry entry)
        {
            // StartCoroutine may advance the iterator before returning its handle.
            yield return null;
            while (!disposed && (Paused || reading >= RequestLimit)) yield return null;
            if (disposed) yield break;
            reading++;
            Texture2D texture = null;
            try
            {
                var uri = entry.path.Contains("://") || entry.path.StartsWith("jar:", StringComparison.Ordinal)
                    ? entry.path : new Uri(Path.GetFullPath(entry.path)).AbsoluteUri;
                entry.request = UnityWebRequestTexture.GetTexture(uri, true);
                yield return entry.request.SendWebRequest();
                if (!disposed && entry.request.result == UnityWebRequest.Result.Success)
                {
                    texture = DownloadHandlerTexture.GetContent(entry.request);
                    if (texture != null)
                    {
                        texture.name = Path.GetFileName(entry.path);
                        texture.wrapMode = TextureWrapMode.Clamp;
                        texture.filterMode = FilterMode.Bilinear;
                        texture.anisoLevel = 4;
                    }
                }
                else if (!disposed)
                    Debug.LogWarning("Battle Spirits artwork could not be loaded: " + Path.GetFileName(entry.path));
            }
            finally
            {
                entry.request?.Dispose();
                entry.request = null;
                entry.coroutine = null;
                reading--;
            }
            if (disposed) { Destroy(texture); yield break; }
            entry.texture = texture;
            var callbacks = entry.waiting.ToArray();
            entry.waiting.Clear();
            foreach (var callback in callbacks) callback(texture);
            Trim();
        }

        private void Trim()
        {
            var idle = new List<Entry>();
            foreach (var entry in entries.Values)
                if (entry.references == 0 && entry.coroutine == null) idle.Add(entry);
            idle.Sort((a, b) => a.lastUsed.CompareTo(b.lastUsed));
            for (var i = 0; i < idle.Count - IdleLimit; i++)
            {
                entries.Remove(idle[i].path);
                Destroy(idle[i].texture);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var entry in entries.Values)
            {
                entry.waiting.Clear();
                entry.request?.Abort();
                if (entry.coroutine != null) owner.StopCoroutine(entry.coroutine);
                entry.request?.Dispose();
                Destroy(entry.texture);
            }
            entries.Clear();
        }

        private static void Destroy(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
