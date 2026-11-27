using System;
using System.Collections.Generic;
using UnityEngine;

namespace MDPro3.BattleSpirits.Visuals
{
    /// <summary>The reviewed 2.5D paper hologram, with the complete printed face below it.</summary>
    internal sealed class BattlePaperActor : IDisposable
    {
        private readonly BattleFieldRenderer host;
        private readonly List<Material> materials = new List<Material>();
        private readonly string facePath;
        private readonly string standeePath;
        private readonly Transform card;
        private readonly Transform billboard;
        private readonly Transform character;
        private readonly Transform motes;
        private readonly Transform beam;
        private readonly Material characterMaterial;
        private readonly List<Material> effects = new List<Material>();
        private readonly List<GameObject> cores = new List<GameObject>();
        private GameObject soul;
        private float age = 2.2f;
        private float clock;
        private float scale = 1;
        private float width = 1;
        private float height = 2.2f;
        private bool disposed;
        internal readonly GameObject Root;
        internal BattleCardPresentation Data { get; private set; }
        internal bool HasCharacter => character != null;
        internal Vector3 LabelPosition => Root.transform.TransformPoint(new Vector3(0, .12f, 1.13f));

        internal BattlePaperActor(BattleFieldRenderer host, Transform parent, BattleCardPresentation data)
        {
            this.host = host;
            Root = host.NewObject("Spirit " + data.instanceId, parent);
            Data = Copy(data);
            card = host.NewObject("Complete printed card", Root.transform).transform;
            var edge = Own(host.FieldMaterial(new Color(.13f, .18f, .23f)));
            host.Plane("Card body", card, edge, new Vector3(0, .025f, 0), new Vector2(1.47f, 2.06f), true);
            var face = Own(host.SurfaceMaterial());
            face.SetFloat("_Opacity", 0);
            // The shared XY plane lies face-up after its +90-degree X turn.
            // Reverse V so the printed top points toward the opposing player.
            face.SetVector("_UvRect", new Vector4(0, 1, 1, -1));
            host.Plane("Uncropped printed face", card, face, new Vector3(0, .031f, 0), new Vector2(1.45f, 2.03f), true);
            facePath = BattleCatalog.CardImagePath(data.cardId);
            host.AcquireTexture(facePath, texture =>
            {
                if (disposed || texture == null) return;
                face.mainTexture = texture;
                face.SetFloat("_Opacity", 1);
                host.Invalidate();
            });

            var visual = BattleCatalog.GetVisual(data.cardId);
            if (BattleCatalog.TryGet(data.cardId, out var definition) && definition.IsSpirit && visual != null
                && visual.width > 0 && visual.height > 0 && visual.right > visual.left && visual.bottom > visual.top)
                standeePath = BattleCatalog.StandeePath(data.cardId);
            if (!string.IsNullOrEmpty(standeePath))
            {
                var accent = Accent(data.cardId);
                width = height * (visual.right - visual.left) / (visual.bottom - visual.top);
                characterMaterial = Own(host.SurfaceMaterial());
                characterMaterial.SetVector("_UvRect", new Vector4((float)visual.left / visual.width,
                    1f - (float)visual.bottom / visual.height, (float)(visual.right - visual.left) / visual.width,
                    (float)(visual.bottom - visual.top) / visual.height));
                characterMaterial.SetColor("_Accent", accent);
                characterMaterial.SetFloat("_Glow", .42f);
                billboard = host.NewObject("World upright billboard", Root.transform).transform;
                character = host.Plane("Transparent reviewed silhouette", billboard, characterMaterial,
                    Vector3.zero, new Vector2(width, height), false).transform;
                character.gameObject.SetActive(false);
                host.AcquireTexture(standeePath, texture =>
                {
                    if (disposed || texture == null) return;
                    characterMaterial.mainTexture = texture;
                    character.gameObject.SetActive(true);
                    host.Invalidate();
                });
                var ring = Effect(accent, 0);
                host.Plane("Soft summoning rings", Root.transform, ring, new Vector3(0, .018f, 0), new Vector2(2.7f, 2.7f), true);
                beam = host.Plane("Low projection beam", billboard, Effect(accent, 1),
                    new Vector3(0, -.45f, -.025f), new Vector2(1.1f, 1.5f), false).transform;
                host.Plane("Subtle printed face shimmer", card, Effect(accent, 2), new Vector3(0, .034f, 0), new Vector2(1.45f, 2.03f), true);
                motes = host.MeshObject("Fixed projection motes", Root.transform, host.MoteMesh, Effect(accent, 3)).transform;
            }
            Apply(data);
        }

        internal void Apply(BattleCardPresentation data)
        {
            Data = Copy(data);
            card.localRotation = Quaternion.Euler(0, data.exhausted ? 90 : 0, 0);
            var count = Mathf.Clamp(data.normalCores, 0, 8);
            while (cores.Count < count)
            {
                var i = cores.Count;
                cores.Add(host.Plane("Normal Core", Root.transform, host.CoreMaterial,
                    new Vector3(-.48f + (i % 4) * .32f, .055f, .5f + (i / 4) * .32f), new Vector2(.3f, .3f), true));
            }
            for (var i = 0; i < cores.Count; i++) cores[i].SetActive(i < count);
            if (data.soul && soul == null)
                soul = host.Plane("Soul Core", Root.transform, host.SoulMaterial, new Vector3(.5f, .06f, -.7f), new Vector2(.38f, .38f), true);
            if (soul != null) soul.SetActive(data.soul);
        }

        internal void Layout(Vector3 position, float tileScale, float widthCap, bool opponent)
        {
            scale = Mathf.Max(.035f, tileScale);
            Root.transform.localPosition = position;
            Root.transform.localRotation = Quaternion.Euler(0, opponent ? 180 : 0, 0);
            Root.transform.localScale = Vector3.one * scale;
            if (character != null)
            {
                var fit = Mathf.Min(1, widthCap / (width * scale));
                character.localScale = new Vector3(width * fit, height * fit, 1);
                billboard.localPosition = new Vector3(0, .085f + height * fit * .5f, 0);
            }
        }

        internal void Reveal(bool reducedMotion) { age = reducedMotion ? 2.2f : 0; }

        internal void Tick(Camera camera, float delta, bool reducedMotion)
        {
            if (disposed || character == null) return;
            if (!reducedMotion) { age += delta; clock += delta; }
            // World rotation deliberately cancels the opponent group's half turn.
            var direction = camera.transform.position - billboard.position;
            if (direction.x * direction.x + direction.z * direction.z > .00001f)
                billboard.rotation = Quaternion.Euler(0, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 0);
            motes.rotation = billboard.rotation;
            var reveal = reducedMotion ? 1 : Mathf.Lerp(-.06f, 1.06f, Smooth(.16f, 1.72f, age));
            characterMaterial.SetFloat("_Reveal", reveal);
            characterMaterial.SetFloat("_Clock", clock);
            characterMaterial.SetFloat("_Motion", reducedMotion ? 0 : 1);
            var p = character.localPosition;
            p.y = reducedMotion ? 0 : Mathf.Sin(clock * 1.25f) * .012f * Smooth(1.5f, 2.2f, age);
            character.localPosition = p;
            beam.gameObject.SetActive(!reducedMotion);
            motes.gameObject.SetActive(!reducedMotion);
            foreach (var effect in effects)
            {
                effect.SetFloat("_Clock", clock);
                effect.SetFloat("_Ready", reducedMotion ? 1 : Smooth(.04f, .52f, age));
                effect.SetFloat("_Motion", reducedMotion ? 0 : 1);
                effect.SetFloat("_Pulse", reducedMotion ? 1 : Mathf.Clamp01(age / 1.1f));
            }
        }

        private Material Effect(Color accent, int kind)
        {
            var material = Own(host.ProjectionMaterial(accent, kind));
            effects.Add(material);
            return material;
        }

        private Material Own(Material material) { materials.Add(material); return material; }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            host.ReleaseTexture(facePath);
            host.ReleaseTexture(standeePath);
            host.DestroyOwned(Root);
            foreach (var material in materials) host.DestroyOwned(material);
            materials.Clear();
        }

        internal static BattleCardPresentation Copy(BattleCardPresentation data) => new BattleCardPresentation
        {
            instanceId = data.instanceId, cardId = data.cardId, owner = data.owner,
            exhausted = data.exhausted, level = data.level, bp = data.bp,
            normalCores = data.normalCores, soul = data.soul
        };

        private static float Smooth(float from, float to, float value)
        {
            var t = Mathf.InverseLerp(from, to, value);
            return t * t * (3 - 2 * t);
        }

        internal static Color Accent(string id)
        {
            if (id == "26RBS03-X01") return new Color(.93f, .54f, .42f);
            if (id == "26RBS03-022") return new Color(.76f, .61f, .96f);
            if (!BattleCatalog.TryGet(id, out var definition)) return new Color(.42f, .72f, .9f);
            switch (definition.color)
            {
                case Core.CardColor.Red: return new Color(.9f, .45f, .38f);
                case Core.CardColor.Purple: return new Color(.76f, .61f, .96f);
                case Core.CardColor.Green: return new Color(.46f, .83f, .65f);
                case Core.CardColor.White: return new Color(.72f, .88f, .96f);
                case Core.CardColor.Yellow: return new Color(.96f, .82f, .46f);
                default: return new Color(.42f, .72f, .96f);
            }
        }
    }
}
