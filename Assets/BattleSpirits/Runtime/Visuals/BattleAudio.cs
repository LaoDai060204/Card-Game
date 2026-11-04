using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace LaoDai.BattleSpirits.Visuals
{
    // Original procedural sounds, loaded independently of the old YGO sound bank.
    public sealed class BattleAudio : MonoBehaviour
    {
        private static BattleAudio instance;
        private static string dataRoot;
        private static float musicVolume = 0.22f, effectVolume = 0.65f;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private readonly Queue<string> pending = new Queue<string>();
        private AudioSource music, effects;
        private int generation;
        private bool musicRequested;

        public static void Configure(string projectRoot)
        {
            string path = Path.GetFullPath(projectRoot ?? Directory.GetCurrentDirectory());
            if (dataRoot == path) return;
            dataRoot = path;
            if (instance != null) instance.Reload();
        }

        public static void SetVolumes(float music, float effects)
        {
            musicVolume = Mathf.Clamp01(music); effectVolume = Mathf.Clamp01(effects);
            if (instance == null) return;
            instance.music.volume = musicVolume;
            instance.effects.volume = effectVolume;
        }

        public static void PlayMusic(string track = "music")
        {
            var owner = Ensure(); owner.musicRequested = true;
            if (owner.clips.TryGetValue("music", out var clip) && owner.music.clip != clip)
            { owner.music.clip = clip; owner.music.Play(); }
            else if (owner.music.clip != null && !owner.music.isPlaying) owner.music.Play();
        }

        public static void StopMusic()
        {
            if (instance == null) return;
            instance.musicRequested = false; instance.music.Stop();
        }

        public static void PlayCue(string cue, float volume = 1f)
        {
            var owner = Ensure();
            if (owner.clips.TryGetValue(cue ?? string.Empty, out var clip))
                owner.effects.PlayOneShot(clip, Mathf.Clamp01(volume));
            else if (owner.pending.Count < 8 && !string.IsNullOrEmpty(cue)) owner.pending.Enqueue(cue);
        }

        private static BattleAudio Ensure()
        {
            if (instance == null) instance = new GameObject("BattleSpirits_Audio").AddComponent<BattleAudio>();
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            music = gameObject.AddComponent<AudioSource>(); music.loop = true; music.spatialBlend = 0; music.volume = musicVolume;
            effects = gameObject.AddComponent<AudioSource>(); effects.spatialBlend = 0; effects.volume = effectVolume;
            Reload();
        }

        private void Reload()
        {
            int version = ++generation;
            music.Stop(); music.clip = null;
            foreach (var clip in clips.Values) Destroy(clip);
            clips.Clear(); pending.Clear();
            _ = LoadAsync(version);
        }

        private async Task LoadAsync(int version)
        {
            string root = dataRoot ?? Directory.GetCurrentDirectory();
            foreach (string cue in new[] { "music", "summon", "boss_summon", "attack", "core", "damage", "destroy", "victory", "defeat" })
            {
                try
                {
                    string file = Path.Combine(root, "Sound/BattleSpirits", cue + ".wav");
                    PcmWave wave = await Task.Run(() => PcmWave.Read(File.ReadAllBytes(file)));
                    if (this == null || version != generation) return;
                    var clip = AudioClip.Create("BS_" + cue, wave.samples.Length / wave.channels, wave.channels, wave.sampleRate, false);
                    clip.SetData(wave.samples, 0); clips[cue] = clip;
                    if (cue == "music" && musicRequested) { music.clip = clip; music.Play(); }
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
                { if (this != null && version == generation) Debug.LogWarning("Battle Spirits audio " + cue + ": " + error.Message); }
            }
            if (this == null || version != generation) return;
            while (pending.Count > 0)
                if (clips.TryGetValue(pending.Dequeue(), out var clip)) effects.PlayOneShot(clip);
        }

        private void OnDestroy()
        {
            generation++;
            if (music != null) music.Stop();
            if (effects != null) effects.Stop();
            foreach (var clip in clips.Values) Destroy(clip);
            clips.Clear(); pending.Clear();
            if (instance == this) instance = null;
        }
    }
}
