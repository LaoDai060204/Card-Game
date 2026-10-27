using System;
using UnityEngine;

namespace LaoDai.BattleSpirits.Presentation
{
    /// <summary>Small original synthesized cues, generated once and released with the scene.</summary>
    public sealed class ArenaAudio : MonoBehaviour
    {
        private AudioSource source;
        private AudioClip summon;
        private AudioClip strike;
        private AudioClip chime;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.22f;
            summon = CreateCue("Summon", 190f, 630f, 0.55f);
            strike = CreateCue("Strike", 130f, 45f, 0.28f);
            chime = CreateCue("Core", 740f, 960f, 0.18f);
        }

        public void Play(string kind)
        {
            if (kind == "Summoned") source.PlayOneShot(summon);
            else if (kind == "Attacked" || kind == "LifeLost") source.PlayOneShot(strike);
            else if (kind == "CoreMoved" || kind == "TurnStarted" || kind == "GameWon") source.PlayOneShot(chime);
        }

        private static AudioClip CreateCue(string name, float start, float end, float duration)
        {
            const int rate = 22050;
            var samples = new float[Mathf.CeilToInt(rate * duration)];
            double phase = 0;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / samples.Length;
                phase += 2.0 * Math.PI * Mathf.Lerp(start, end, t) / rate;
                var envelope = Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-2f * t);
                samples[i] = (float)(Math.Sin(phase) + 0.25 * Math.Sin(phase * 2.01)) * envelope * 0.6f;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            if (summon != null) Destroy(summon);
            if (strike != null) Destroy(strike);
            if (chime != null) Destroy(chime);
        }
    }
}
