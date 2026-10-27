using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace LaoDai.BattleSpirits.Presentation
{
    /// <summary>Generic-rig animation through Animator and Playables; no controller asset is required.</summary>
    public sealed class DragonAnimationPlayer : MonoBehaviour
    {
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private readonly List<AnimationClipPlayable> inputs = new List<AnimationClipPlayable>();
        private readonly List<AnimationClip> clips = new List<AnimationClip>();
        private readonly List<float> weights = new List<float>();
        private readonly Dictionary<string, int> indices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private int activeIndex = -1;
        private int generation;
        private int returnGeneration = -1;

        public void Initialize(AnimationClip[] importedClips)
        {
            ReleaseGraph();
            if (importedClips == null) return;
            var animator = GetComponent<Animator>();
            if (animator == null) animator = gameObject.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var clip in importedClips)
            {
                if (clip == null || clip.legacy || clip.name.StartsWith("__preview__")) continue;
                var name = clip.name.Contains("|") ? clip.name.Substring(clip.name.LastIndexOf('|') + 1) : clip.name;
                if (indices.ContainsKey(name)) continue;
                indices.Add(name, clips.Count);
                clips.Add(clip);
                weights.Add(0);
            }
            if (clips.Count == 0) return;
            graph = PlayableGraph.Create("Aether Drake Animation");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, clips.Count);
            var output = AnimationPlayableOutput.Create(graph, "Dragon", animator);
            output.SetSourcePlayable(mixer);
            for (var i = 0; i < clips.Count; i++)
            {
                var playable = AnimationClipPlayable.Create(graph, clips[i]);
                playable.SetApplyFootIK(false);
                playable.SetDuration(double.PositiveInfinity);
                inputs.Add(playable);
                graph.Connect(playable, 0, mixer, i);
                mixer.SetInputWeight(i, 0);
            }
            Play("Idle");
            if (activeIndex < 0) SetActiveClip(0, false);
            weights[activeIndex] = 1;
            mixer.SetInputWeight(activeIndex, 1);
            graph.Play();
        }

        public void Play(string name)
        {
            if (!graph.IsValid() || !indices.TryGetValue(name, out var index)) return;
            if (activeIndex == index && string.Equals(name, "Idle", StringComparison.OrdinalIgnoreCase)) return;
            SetActiveClip(index, !string.Equals(name, "Idle", StringComparison.OrdinalIgnoreCase));
        }

        private void SetActiveClip(int index, bool returnToIdle)
        {
            activeIndex = index;
            generation++;
            returnGeneration = returnToIdle ? generation : -1;
            inputs[index].SetTime(0);
        }

        private void Update()
        {
            if (!graph.IsValid() || activeIndex < 0) return;
            var totalWeight = 0f;
            for (var i = 0; i < inputs.Count; i++)
            {
                weights[i] = Mathf.MoveTowards(weights[i], i == activeIndex ? 1 : 0, Time.deltaTime / .14f);
                totalWeight += weights[i];
            }
            for (var i = 0; i < inputs.Count; i++)
                mixer.SetInputWeight(i, totalWeight > 0 ? weights[i] / totalWeight : (i == activeIndex ? 1 : 0));
            var length = clips[activeIndex].length;
            if (length <= 0 || inputs[activeIndex].GetTime() < length) return;
            if (returnGeneration == generation)
            {
                // A newer Play call changes generation, so an earlier summon cannot interrupt an attack.
                Play("Idle");
            }
            else
            {
                // Explicit wrapping also supports clips whose importer has not yet saved loop metadata.
                inputs[activeIndex].SetTime(inputs[activeIndex].GetTime() % length);
            }
        }

        private void OnEnable()
        {
            if (graph.IsValid()) graph.Play();
        }

        private void OnDisable()
        {
            if (graph.IsValid()) graph.Stop();
        }

        private void ReleaseGraph()
        {
            if (graph.IsValid()) graph.Destroy();
            indices.Clear();
            inputs.Clear();
            clips.Clear();
            weights.Clear();
            activeIndex = -1;
            returnGeneration = -1;
        }

        private void OnDestroy() => ReleaseGraph();
    }
}
