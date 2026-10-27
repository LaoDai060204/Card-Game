using System.Collections;
using System.Collections.Generic;
using LaoDai.BattleSpirits.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LaoDai.BattleSpirits.Presentation
{
    /// <summary>Small original stage; card art and the summoned creature are separate assets.</summary>
    public sealed class ArenaWorld : MonoBehaviour
    {
        private sealed class SpiritVisual
        {
            public GameObject Root;
            public GameObject Creature;
            public GameObject CoreGroup;
            public DragonAnimationPlayer Animation;
            public LineRenderer Ring;
            public int CoreCount = -1;
            public bool HasSoul;
            public bool Exhausted;
        }

        private readonly Dictionary<int, SpiritVisual> spirits = new Dictionary<int, SpiritVisual>();
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private readonly List<GameObject> lifeOrbs = new List<GameObject>();
        private readonly List<GameObject> reserveOrbs = new List<GameObject>();
        private readonly List<GameObject> transientEffects = new List<GameObject>();
        private readonly Color[] colors =
        {
            new Color(1f, .27f, .16f), new Color(.66f, .28f, 1f),
            new Color(.22f, .85f, .5f), new Color(.8f, .91f, 1f),
            new Color(1f, .81f, .23f), new Color(.18f, .58f, 1f)
        };
        private GameObject model;
        private AnimationClip[] clips;
        private Transform stage;
        private int selected = -1;
        private VolumeProfile volumeProfile;

        public void Build()
        {
            stage = new GameObject("Eternal Arena").transform;
            stage.SetParent(transform, false);
            var cameraObject = new GameObject("Arena Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(stage, false);
            cameraObject.transform.position = new Vector3(0f, 12.6f, -11.8f);
            cameraObject.transform.LookAt(new Vector3(0f, 0f, .4f));
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.018f, .028f, .055f);
            camera.fieldOfView = 39f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 80f;
            camera.allowHDR = true;
            cameraObject.AddComponent<AudioListener>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.29f, .38f, .56f);
            RenderSettings.ambientEquatorColor = new Color(.17f, .21f, .32f);
            RenderSettings.ambientGroundColor = new Color(.05f, .045f, .06f);
            AddLight("Moonlight", new Vector3(3, 9, -4), new Color(.63f, .78f, 1f), 1.25f, true);
            AddLight("Warm fill", new Vector3(-5, 5, 3), new Color(1f, .5f, .22f), 3.5f, false);

            var volume = new GameObject("Arena Bloom").AddComponent<Volume>();
            volume.transform.SetParent(stage, false);
            volume.isGlobal = true;
            volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = volumeProfile;
            var bloom = volumeProfile.Add<Bloom>();
            bloom.intensity.Override(.8f);
            bloom.threshold.Override(1f);
            var tone = volumeProfile.Add<Tonemapping>();
            tone.mode.Override(TonemappingMode.ACES);

            Primitive("Obsidian plinth", PrimitiveType.Cube, stage, new Vector3(0, -.28f, 0), new Vector3(12.6f, .5f, 8.3f), Surface("stone", new Color(.055f, .075f, .12f), .25f));
            Primitive("Playing surface", PrimitiveType.Cube, stage, new Vector3(0, -.005f, 0), new Vector3(12.2f, .035f, 7.9f), Surface("surface", new Color(.095f, .115f, .16f), .1f));
            var bronze = Surface("bronze", new Color(.57f, .34f, .13f), .8f);
            foreach (var z in new[] { -4.03f, 4.03f })
                Primitive("Bronze frame", PrimitiveType.Cube, stage, new Vector3(0, .01f, z), new Vector3(12.6f, .075f, .09f), bronze);
            foreach (var x in new[] { -6.2f, 6.2f })
                Primitive("Bronze frame", PrimitiveType.Cube, stage, new Vector3(x, .01f, 0), new Vector3(.09f, .075f, 8f), bronze);
            var goldGlow = Surface("gold glow", new Color(.48f, .29f, .1f), .6f, 1.5f);
            Ring("Center seal", stage, Vector3.up * .03f, 1.05f, goldGlow, .025f);
            Ring("Outer seal", stage, Vector3.up * .03f, 1.25f, bronze, .018f);
            for (var i = 0; i < 6; i++)
            {
                var angle = i * Mathf.PI / 3f;
                var token = Primitive("Color sigil", PrimitiveType.Cylinder, stage, new Vector3(Mathf.Sin(angle) * 1.15f, .055f, Mathf.Cos(angle) * 1.15f), new Vector3(.11f, .025f, .11f), Surface("sigil " + i, colors[i], .45f, .8f));
                token.transform.Rotate(0, i * 60, 0);
            }
            for (var p = 0; p < 2; p++)
            {
                var sign = p == 0 ? -1f : 1f;
                Primitive("Reserve tray", PrimitiveType.Cylinder, stage, new Vector3(-5.35f, .06f, sign * 2.2f), new Vector3(.75f, .035f, .75f), bronze);
                Ring("Life shrine", stage, new Vector3(5.35f, .07f, sign * 2.2f), .7f, goldGlow, .04f);
            }
            model = Resources.Load<GameObject>("Models/SummonedDragon");
            clips = Resources.LoadAll<AnimationClip>("Models/SummonedDragon");
        }

        public void ResetMatch()
        {
            StopAllCoroutines();
            foreach (var visual in spirits.Values) if (visual.Root != null) Destroy(visual.Root);
            spirits.Clear();
            foreach (var orb in lifeOrbs) if (orb != null) Destroy(orb);
            foreach (var orb in reserveOrbs) if (orb != null) Destroy(orb);
            foreach (var effect in transientEffects) if (effect != null) Destroy(effect);
            lifeOrbs.Clear();
            reserveOrbs.Clear();
            transientEffects.Clear();
            selected = -1;
        }

        public void Synchronize(BattleMatch match)
        {
            if (match == null) return;
            var present = new HashSet<int>();
            for (var p = 0; p < 2; p++)
            {
                var field = match.State.Players[p].Field;
                for (var i = 0; i < field.Count; i++)
                {
                    var spirit = field[i];
                    present.Add(spirit.InstanceId);
                    if (!spirits.TryGetValue(spirit.InstanceId, out var visual))
                    {
                        visual = CreateSpirit(spirit, p);
                        spirits.Add(spirit.InstanceId, visual);
                    }
                    var columns = Mathf.Min(5, field.Count);
                    var row = i / 5;
                    var rows = (field.Count + 4) / 5;
                    var rowSpacing = rows <= 1 ? 1.65f : Mathf.Min(1.65f, 2.25f / (rows - 1));
                    var x = ((i % 5) - (columns - 1) * .5f) * 1.85f;
                    var z = (p == 0 ? -1f : 1f) * (1f + row * rowSpacing);
                    visual.Root.transform.position = new Vector3(x, .05f, z);
                    visual.Root.transform.localScale = Vector3.one * Mathf.Min(1f, rowSpacing / 1.65f);
                    visual.Exhausted = spirit.Exhausted;
                    visual.Creature.transform.localRotation = Quaternion.Euler(0, (p == 0 ? 0 : 180) + (spirit.Exhausted ? 28 : 0), 0);
                    visual.Ring.startWidth = visual.Ring.endWidth = spirit.InstanceId == selected ? .065f : .025f;
                    UpdateCores(visual, spirit);
                }
            }
            var removed = new List<int>();
            foreach (var pair in spirits)
                if (!present.Contains(pair.Key)) { Destroy(pair.Value.Root); removed.Add(pair.Key); }
            foreach (var id in removed) spirits.Remove(id);
            RefreshPools(match);
        }

        public void SetSelected(int spiritId)
        {
            selected = spiritId;
            foreach (var pair in spirits)
                pair.Value.Ring.startWidth = pair.Value.Ring.endWidth = pair.Key == selected ? .065f : .025f;
        }

        public void OnBattleEvent(BattleEvent battleEvent, BattleMatch match)
        {
            if (battleEvent.Kind == "Summoned")
            {
                Synchronize(match);
                if (spirits.TryGetValue(battleEvent.SpiritId, out var visual))
                {
                    PlayClip(visual, "Summon");
                    StartCoroutine(SummonGlow(visual.Root.transform.position));
                }
            }
            else if ((battleEvent.Kind == "Attacked" || battleEvent.Kind == "Blocked") && spirits.TryGetValue(battleEvent.SpiritId, out var attacker))
                PlayClip(attacker, "Attack");
            else if ((battleEvent.Kind == "Destroyed" || battleEvent.Kind == "Vanished") && spirits.TryGetValue(battleEvent.SpiritId, out var destroyed))
                StartCoroutine(SummonGlow(destroyed.Root.transform.position));
        }

        private SpiritVisual CreateSpirit(SpiritState spirit, int owner)
        {
            var root = new GameObject("Spirit " + spirit.InstanceId + " " + spirit.Card.id);
            root.transform.SetParent(stage, false);
            var index = Mathf.Clamp((int)spirit.Card.color, 0, colors.Length - 1);
            var color = colors[index];
            var plinth = Primitive("Spirit plinth", PrimitiveType.Cylinder, root.transform, Vector3.zero, new Vector3(.72f, .025f, .72f), Surface("plinth", new Color(.035f, .045f, .07f), .6f));
            var ring = Ring("Symbol ring", root.transform, new Vector3(0, .065f, 0), .69f, Surface("spirit color " + index, color, .5f, 1.2f), .025f);
            var art = Resources.Load<Texture2D>("CardArt/" + spirit.Card.id);
            if (art != null)
            {
                var card = Primitive("Summoned card", PrimitiveType.Quad, root.transform, new Vector3(0, .06f, -.35f), new Vector3(.68f, 1f, 1f), Surface("art " + spirit.Card.id, Color.white));
                card.transform.localRotation = Quaternion.Euler(90, 0, 0);
                var artMaterial = card.GetComponent<Renderer>().sharedMaterial;
                if (artMaterial.HasProperty("_BaseMap")) artMaterial.SetTexture("_BaseMap", art);
                else artMaterial.mainTexture = art;
            }
            GameObject creature;
            if (model != null)
            {
                creature = Instantiate(model, root.transform);
                creature.name = "Summoned Aether Drake";
                creature.transform.localPosition = Vector3.up * .1f;
                creature.transform.localScale = Vector3.one * .36f;
                foreach (var renderer in creature.GetComponentsInChildren<Renderer>())
                {
                    var originals = renderer.sharedMaterials;
                    var replacements = new Material[originals.Length];
                    for (var i = 0; i < originals.Length; i++)
                    {
                        var original = originals[i];
                        var materialName = original == null ? "Body" : original.name;
                        var sourceColor = original != null && original.HasProperty("_Color") ? original.color : new Color(.55f, .19f, .11f);
                        replacements[i] = Surface("dragon " + materialName, sourceColor, .35f, materialName.ToLowerInvariant().Contains("eye") ? 1.7f : 0f);
                    }
                    renderer.sharedMaterials = replacements;
                }
            }
            else
            {
                creature = Primitive("Spirit placeholder", PrimitiveType.Capsule, root.transform, Vector3.up * .65f, new Vector3(.5f, .55f, .5f), Surface("spirit color " + index, color));
            }
            var animation = creature.AddComponent<DragonAnimationPlayer>();
            animation.Initialize(clips);
            var cores = new GameObject("Allocated cores");
            cores.transform.SetParent(root.transform, false);
            var visual = new SpiritVisual { Root = root, Creature = creature, CoreGroup = cores, Animation = animation, Ring = ring };
            PlayClip(visual, "Idle");
            return visual;
        }

        private void UpdateCores(SpiritVisual visual, SpiritState spirit)
        {
            if (visual.CoreCount == spirit.Cores.Total && visual.HasSoul == spirit.Cores.Soul) return;
            visual.CoreCount = spirit.Cores.Total;
            visual.HasSoul = spirit.Cores.Soul;
            foreach (Transform child in visual.CoreGroup.transform) Destroy(child.gameObject);
            for (var i = 0; i < spirit.Cores.Total; i++)
            {
                var angle = i * Mathf.PI * 2f / Mathf.Max(1, spirit.Cores.Total);
                var soul = spirit.Cores.Soul && i == spirit.Cores.Total - 1;
                Primitive(soul ? "Soul Core" : "Core", PrimitiveType.Sphere, visual.CoreGroup.transform,
                    new Vector3(Mathf.Sin(angle) * .57f, .17f, Mathf.Cos(angle) * .57f), Vector3.one * (soul ? .135f : .1f), CoreMaterial(soul));
            }
        }

        private void RefreshPools(BattleMatch match)
        {
            foreach (var orb in lifeOrbs) Destroy(orb);
            foreach (var orb in reserveOrbs) Destroy(orb);
            lifeOrbs.Clear();
            reserveOrbs.Clear();
            for (var p = 0; p < 2; p++)
            {
                var player = match.State.Players[p];
                var sign = p == 0 ? -1f : 1f;
                for (var i = 0; i < player.Life; i++)
                {
                    var angle = i * Mathf.PI * 2f / 5f;
                    lifeOrbs.Add(Primitive("Life Core", PrimitiveType.Sphere, stage,
                        new Vector3(5.35f + Mathf.Sin(angle) * .4f, .26f, sign * 2.2f + Mathf.Cos(angle) * .4f), Vector3.one * .21f, Surface("life glow", new Color(1f, .48f, .18f), .4f, 1.4f)));
                }
                for (var i = 0; i < player.Reserve.Total; i++)
                {
                    var angle = i * 2.4f;
                    var radius = .09f * Mathf.Sqrt(i);
                    reserveOrbs.Add(Primitive("Reserve Core", PrimitiveType.Sphere, stage,
                        new Vector3(-5.35f + Mathf.Sin(angle) * radius, .21f, sign * 2.2f + Mathf.Cos(angle) * radius), Vector3.one * .12f,
                        CoreMaterial(player.Reserve.Soul && i == player.Reserve.Total - 1)));
                }
            }
        }

        private Material CoreMaterial(bool soul) => Surface(soul ? "soul glow" : "core glow", soul ? new Color(.98f, .25f, .8f) : new Color(.1f, .7f, 1f), .55f, 1.5f);

        private void PlayClip(SpiritVisual visual, string name)
        {
            if (visual.Animation != null) visual.Animation.Play(name);
        }
        private IEnumerator SummonGlow(Vector3 position)
        {
            var effect = new GameObject("Summoning sigil");
            transientEffects.Add(effect);
            effect.transform.SetParent(stage, false);
            effect.transform.position = position + Vector3.up * .09f;
            Ring("Expanding seal", effect.transform, Vector3.zero, .75f, Surface("summon glow", new Color(.24f, .74f, 1f), .2f, 3f), .055f);
            var elapsed = 0f;
            while (elapsed < .8f)
            {
                elapsed += Time.deltaTime;
                effect.transform.localScale = Vector3.one * Mathf.Lerp(.25f, 1.8f, elapsed / .8f);
                effect.transform.position = position + Vector3.up * (.09f + elapsed * .7f);
                yield return null;
            }
            Destroy(effect);
            transientEffects.Remove(effect);
        }

        private void AddLight(string name, Vector3 position, Color color, float intensity, bool directional)
        {
            var go = new GameObject(name);
            go.transform.SetParent(stage, false);
            go.transform.position = position;
            go.transform.LookAt(Vector3.zero);
            var light = go.AddComponent<Light>();
            light.type = directional ? LightType.Directional : LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = 25;
            light.shadows = directional ? LightShadows.Soft : LightShadows.None;
        }

        private Material Surface(string key, Color color, float metallic = .1f, float glow = 0)
        {
            if (materials.TryGetValue(key, out var material)) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var template = Resources.Load<Material>(glow > 0 ? "Materials/ArenaEmission" : "Materials/ArenaLit");
            material = template != null ? new Material(template) : new Material(shader);
            material.name = key;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .42f);
            if (glow > 0)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * glow);
            }
            materials.Add(key, material);
            return material;
        }
        private static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var result = GameObject.CreatePrimitive(type);
            result.name = name;
            result.transform.SetParent(parent, false);
            result.transform.localPosition = position;
            result.transform.localScale = scale;
            result.GetComponent<Renderer>().sharedMaterial = material;
            var collider = result.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            return result;
        }
        private static LineRenderer Ring(string name, Transform parent, Vector3 position, float radius, Material material, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var ring = go.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 80;
            ring.startWidth = ring.endWidth = width;
            ring.sharedMaterial = material;
            for (var i = 0; i < ring.positionCount; i++)
            {
                var a = i * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Sin(a) * radius, 0, Mathf.Cos(a) * radius));
            }
            return ring;
        }
        private void OnDestroy()
        {
            foreach (var material in materials.Values) if (material != null) Destroy(material);
            if (volumeProfile != null) Destroy(volumeProfile);
        }
    }
}
