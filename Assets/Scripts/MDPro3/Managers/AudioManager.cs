using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace MDPro3
{
    /// <summary>Battle Spirits audio, shared by the retained interface widgets.</summary>
    public class AudioManager : Manager
    {
        // Keep these serialized fields: the existing scene owns the three sources.
        public AudioSource seR;
        public AudioSource bgmR;
        public AudioSource voiceR;

        public const string BGM_MENU_MAIN = "BattleSpirits/Audio/Menu";
        private const string ResourceRoot = "BattleSpirits/Audio/";
        private static AudioSource se;
        private static AudioSource bgm;
        private static AudioSource voice;
        private static AudioManager owner;
        private static float musicVolume = .7f, currentBGMScale = 1f;
        private static string lastSE;
        private static float lastSETime = float.NegativeInfinity;
        private static readonly Dictionary<int, AudioClip> tones = new Dictionary<int, AudioClip>();
        private static readonly HashSet<AudioClip> allowedClips = new HashSet<AudioClip>();
        private static readonly HashSet<AudioClip> ownedClips = new HashSet<AudioClip>();
        public static string nextMuteSE;

        public override void Initialize()
        {
            base.Initialize();
            if (owner != null && owner != this) owner.Detach();
            owner = this;
            se = seR;
            bgm = bgmR;
            voice = voiceR;
            lastSE = null;
            lastSETime = float.NegativeInfinity;
            nextMuteSE = null;
            // A prefab may contain an assigned clip. It is never an audio source
            // for this game; approved clips are selected explicitly below.
            foreach (var source in new[] { se, bgm, voice })
            {
                if (source == null) continue;
                source.Stop();
                source.clip = null;
                source.playOnAwake = false;
            }
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
            PlayBGM(BGM_MENU_MAIN);
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
#if !UNITY_EDITOR && UNITY_ANDROID
            if (deviceWasChanged)
            {
                var config = AudioSettings.GetConfiguration();
                AudioSettings.Reset(config);
            }
#endif
            if (owner == this && bgm != null && bgm.clip != null && allowedClips.Contains(bgm.clip)) bgm.Play();
        }

        public static void SetSeVol(float vol)
        {
            if (se != null) se.volume = Mathf.Clamp01(vol);
        }

        public static void SetBGMVol(float vol)
        {
            musicVolume = Mathf.Clamp01(vol);
            if (bgm != null) bgm.volume = musicVolume * currentBGMScale;
        }

        public static void SetVoiceVol(float vol)
        {
            if (voice != null) voice.volume = Mathf.Clamp01(vol);
        }

        private static bool IsResourcePath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith(ResourceRoot, StringComparison.Ordinal)
                || path.Length == ResourceRoot.Length || path.Contains("..")) return false;
            foreach (char value in path)
                if (!(char.IsLetterOrDigit(value) || value == '/' || value == '_' || value == '-')) return false;
            return true;
        }

        private static AudioClip ResourceClip(string path)
        {
            if (!IsResourcePath(path)) return null;
            var clip = Resources.Load<AudioClip>(path);
            if (clip != null) allowedClips.Add(clip);
            return clip;
        }

        // The retained interface passes event names, not file paths. Map these
        // events to a bounded set of new sounds rather than old asset addresses.
        private static int ToneKind(string path)
        {
            var key = path.ToUpperInvariant();
            if (key.Contains("ERROR") || key.Contains("INVALID") || key.Contains("CAUTION")) return 4;
            if (key.Contains("CANCEL") || key.Contains("BACK") || key.Contains("CLOSE")) return 2;
            if (key.Contains("CARD") || key.Contains("SHUFFLE") || key.Contains("DRAW")) return 3;
            if (key.Contains("HOVER") || key.Contains("CURSOR") || key.Contains("SELECT")) return 0;
            if (key.Contains("DECIDE") || key.Contains("CONFIRM") || key.Contains("OK")) return 1;
            return 5;
        }

        private static AudioClip Tone(int kind)
        {
            if (tones.TryGetValue(kind, out var cached) && cached != null) return cached;
            const int sampleRate = 22050;
            float duration = kind == 4 ? .13f : kind == 1 ? .11f : .065f;
            float frequency = kind == 0 ? 1080 : kind == 1 ? 780 : kind == 2 ? 440 : kind == 3 ? 310 : kind == 4 ? 220 : 660;
            var samples = new float[(int)(duration * sampleRate)];
            for (int index = 0; index < samples.Length; index++)
            {
                double progress = (double)index / samples.Length;
                double envelope = Math.Sin(Math.PI * progress) * Math.Exp(-5 * progress);
                double phase = 2 * Math.PI * frequency * index / sampleRate;
                samples[index] = (float)(.17 * envelope * (Math.Sin(phase) + .25 * Math.Sin(phase * 1.5)));
            }
            var clip = AudioClip.Create("BattleSpirits_UI_" + kind, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            tones[kind] = clip;
            allowedClips.Add(clip);
            ownedClips.Add(clip);
            return clip;
        }

        public static void PlaySE(string path, float volumeScale = 1)
        {
            if (se == null || string.IsNullOrEmpty(path)) return;
            if (path == nextMuteSE) { nextMuteSE = null; return; }
            float now = Time.unscaledTime;
            if (lastSE == path && now - lastSETime >= 0 && now - lastSETime < .08f) return;
            lastSE = path;
            lastSETime = now;
            var clip = IsResourcePath(path) ? ResourceClip(path) : Tone(ToneKind(path));
            if (clip != null) se.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        public void PlayShuffleSE() => StartCoroutine(PlayShuffle());

        private static IEnumerator PlayShuffle()
        {
            for (int index = 0; index < 4; index++)
            {
                PlaySE("BattleSpirits_Card_Shuffle_" + index, .7f);
                yield return new WaitForSecondsRealtime(.055f);
            }
        }

        public static void PlaySEClip(AudioClip clip, float volumeScale = 1)
        {
            if (se != null && clip != null && allowedClips.Contains(clip)) se.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        public static void ResetSESource()
        {
            if (se != null) se.Stop();
            lastSE = null;
            lastSETime = float.NegativeInfinity;
        }

        public static void PlayVoiceByResourcePath(string path)
        {
            var clip = ResourceClip(path);
            if (voice != null && clip != null) voice.PlayOneShot(clip);
        }

        public static void PlayVoice(AudioClip clip)
        {
            if (voice != null && clip != null && allowedClips.Contains(clip)) voice.PlayOneShot(clip);
        }

        public static void PlayBGM(string path, float volumeScale = 1f)
        {
            currentBGMScale = Mathf.Clamp01(volumeScale);
            if (bgm == null) return;
            // No old-address alias or soundtrack fallback. Missing music content
            // leaves this section silent until a Battle Spirits clip is provided.
            var clip = ResourceClip(path);
            if (bgm.clip == clip && clip != null && bgm.isPlaying)
            {
                bgm.volume = musicVolume * currentBGMScale;
                return;
            }
            bgm.Stop();
            bgm.clip = clip;
            bgm.loop = true;
            bgm.volume = musicVolume * currentBGMScale;
            if (clip != null) bgm.Play();
        }

        public static void StopBGM()
        {
            if (bgm == null) return;
            bgm.Stop();
            bgm.clip = null;
        }

        public static void PlayBgmNormal(string fieldName) => PlayBGM(ResourceRoot + "Duel");
        public static void PlayBgmKeyCard() => PlayBGM(ResourceRoot + "Boss");
        public static void PlayBgmClimax() => PlayBGM(ResourceRoot + "Climax");
        public static void PlayRandomKeyCardBGM() => PlayBgmKeyCard();

        private static string AudioFilePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Contains("://")) return null;
            try
            {
                string full = Path.GetFullPath(path);
                foreach (string basePath in new[] { Application.streamingAssetsPath, Application.persistentDataPath })
                {
                    string audioRoot = Path.GetFullPath(Path.Combine(basePath, "BattleSpirits", "audio"))
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    if (full.StartsWith(audioRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(full)) return full;
                }
            }
            catch (Exception error) when (error is ArgumentException || error is IOException || error is NotSupportedException) { }
            return null;
        }

        private static AudioClip DownloadedClip(UnityWebRequest request)
        {
            if (request.result != UnityWebRequest.Result.Success) return null;
            var clip = DownloadHandlerAudioClip.GetContent(request);
            if (clip != null) { allowedClips.Add(clip); ownedClips.Add(clip); }
            return clip;
        }

        public static IEnumerator<AudioClip> LoadAudioFileAsync(string path, AudioType audioType)
        {
            string full = AudioFilePath(path);
            if (full == null) yield break;
            using var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(full).AbsoluteUri, audioType);
            var operation = request.SendWebRequest();
            while (!operation.isDone) yield return null;
            var clip = DownloadedClip(request);
            if (clip != null) yield return clip;
        }

        public static async UniTask<AudioClip> LoadAudioFileUniAsync(string path, AudioType audioType)
        {
            string full = AudioFilePath(path);
            if (full == null) return null;
            using var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(full).AbsoluteUri, audioType);
            await request.SendWebRequest();
            return DownloadedClip(request);
        }

        private void Detach()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            if (owner != this) return;
            foreach (var source in new[] { se, bgm, voice }) if (source != null) { source.Stop(); source.clip = null; }
            foreach (var clip in ownedClips) if (clip != null) Destroy(clip);
            ownedClips.Clear();
            allowedClips.Clear();
            tones.Clear();
            se = null; bgm = null; voice = null; owner = null;
        }

        private void OnDestroy() => Detach();
    }
}
