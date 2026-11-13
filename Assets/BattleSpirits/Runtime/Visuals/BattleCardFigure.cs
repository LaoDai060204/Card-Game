using System;
using MDPro3;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LaoDai.BattleSpirits.Visuals
{
    // The texture is borrowed from the session cache. A disabled RawImage keeps
    // the existing asynchronous artwork ownership and cache-pinning rules.
    internal sealed class BattleCardFigure : IDisposable
    {
        internal const float CardWidth = 2.4f;
        internal const float CardHeight = 3.5f;
        internal readonly GameObject root;
        internal readonly RawImage artwork;
        private readonly Transform floorPivot, card, illustration, halo;
        private readonly Renderer cardRenderer, illustrationRenderer, haloRenderer;
        private readonly Material cardMaterial, illustrationMaterial, haloMaterial;
        private readonly Mesh quad;
        private readonly Camera camera;
        private readonly bool nexus;
        private readonly Rect illustrationUv;
        private readonly Action<string, RawImage> loadArt;
        private readonly string cardId;
        private readonly Color accent;
        private readonly BattleSpiritModelLibrary modelLibrary;
        private BattleSpiritModel model;
        private bool modelAttempted;
        private float modelRevealStarted = -1;
        private Texture assignedTexture;
        private bool hadReadyTexture;
        private float portraitWidth = 2.8f, portraitHeight = 2.5f;
        private bool disposed;

        internal BattleCardFigure(Transform parent, Transform artworkParent, string cardId,
            Shader shader, Camera camera, Color accent, Action<string, RawImage> loadArt, bool nexus,
            BattleSpiritModelLibrary modelLibrary = null)
        {
            this.camera = camera; this.nexus = nexus; this.loadArt = loadArt; this.cardId = cardId;
            this.accent = accent; this.modelLibrary = modelLibrary;
            illustrationUv = BattleFieldVisuals.GetIllustrationUv(cardId);
            root = new GameObject("CardSpirit_" + cardId); root.layer = parent.gameObject.layer;
            root.transform.SetParent(parent, false);
            floorPivot = BattleSpiritGeometry.Pivot(root.transform, "CardOrientation", Vector3.zero);
            quad = new Mesh { name = "BS artwork quad " + cardId };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0),
                new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quad.RecalculateNormals(); quad.RecalculateBounds();
            cardMaterial = MakeMaterial(shader, "Card_" + cardId, accent, new Rect(0, 0, 1, 1), 0);
            illustrationMaterial = MakeMaterial(shader, "Illustration_" + cardId, accent, illustrationUv, 0.045f);
            haloMaterial = MakeMaterial(shader, "SummonHalo_" + cardId, accent, new Rect(0, 0, 1, 1), 0);
            haloMaterial.SetFloat("_Ring", 1);
            haloMaterial.SetColor("_BaseColor", accent);
            card = Quad(floorPivot, "PrintedCard", cardMaterial, out cardRenderer);
            card.localRotation = Quaternion.Euler(90, 0, 0);
            illustration = Quad(root.transform, "FloatingIllustration", illustrationMaterial, out illustrationRenderer);
            halo = Quad(root.transform, "SummonHalo", haloMaterial, out haloRenderer);
            halo.localRotation = Quaternion.Euler(90, 0, 0);
            cardRenderer.enabled = illustrationRenderer.enabled = false;

            var carrier = new GameObject("BS_ArtworkCarrier_" + cardId, typeof(RectTransform));
            carrier.layer = artworkParent.gameObject.layer; carrier.transform.SetParent(artworkParent, false);
            artwork = carrier.AddComponent<RawImage>(); artwork.enabled = false; artwork.raycastTarget = false;
            try { loadArt?.Invoke(cardId, artwork); }
            catch (Exception error) { Debug.LogWarning("Battle Spirits card artwork: " + error.Message); }
            RefreshArtwork();
        }

        private static Material MakeMaterial(Shader shader, string name, Color accent, Rect uv, float feather)
        {
            var material = new Material(shader) { name = "BS_" + name };
            material.SetColor("_BaseColor", Color.white); material.SetColor("_Accent", accent);
            material.SetVector("_UvRect", new Vector4(uv.x, uv.y, uv.width, uv.height));
            material.SetFloat("_Opacity", 1); material.SetFloat("_Feather", feather);
            return material;
        }

        private Transform Quad(Transform parent, string name, Material material, out Renderer renderer)
        {
            var go = new GameObject(name); go.layer = root.layer; go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            return go.transform;
        }

        private void RefreshArtwork()
        {
            bool modelVisible = RefreshModelMode();
            Texture candidate = artwork == null ? null : artwork.texture;
            // Inactive cached Servant screens may have their unpinned textures
            // evicted. Re-entering the duel must request that card again rather
            // than keeping a destroyed texture forever or polling the loader.
            if (candidate == null && hadReadyTexture && assignedTexture == null && artwork != null)
            {
                hadReadyTexture = false;
                try { loadArt?.Invoke(cardId, artwork); }
                catch (Exception error) { Debug.LogWarning("Battle Spirits card artwork: " + error.Message); }
                candidate = artwork.texture;
            }
            bool ready = candidate != null && candidate != Texture2D.blackTexture;
            cardRenderer.enabled = ready; illustrationRenderer.enabled = ready && !modelVisible;
            if (!ready || candidate == assignedTexture) return;
            assignedTexture = candidate; hadReadyTexture = true;
            cardMaterial.mainTexture = illustrationMaterial.mainTexture = candidate;
            // Keep the printed illustration's aspect instead of stretching the
            // full card (including rules box) into a monster-shaped rectangle.
            float width = Math.Max(1, candidate.width) * illustrationUv.width;
            float height = Math.Max(1, candidate.height) * illustrationUv.height;
            float aspect = Math.Max(0.45f, Math.Min(2.1f, width / height));
            portraitWidth = nexus ? 2.1f : 2.8f;
            portraitHeight = Math.Min(nexus ? 2.2f : 3.3f, portraitWidth / aspect);
            portraitWidth = portraitHeight * aspect;
        }

        private bool RefreshModelMode()
        {
            bool enabled = !nexus && modelLibrary != null && Config.Get(BattleTheme.SpiritModelConfig, "1") != "0";
            if (enabled && !modelAttempted)
            {
                modelAttempted = true;
                try { modelLibrary.TryCreate(cardId, root.transform, out model); }
                catch (Exception error) { Debug.LogWarning("Battle Spirits model " + cardId + ": " + error.Message); }
            }
            if (model == null) return false;
            model.Renderer.enabled = enabled;
            return enabled;
        }

        internal bool RevealModel()
        {
            if (disposed || !RefreshModelMode()) return false;
            modelRevealStarted = Time.unscaledTime;
            return true;
        }

        internal void Animate(BattleVisualPose pose, float age, float clock, Vector3 position,
            float facing, bool exhausted, float layoutScale = 1)
        {
            if (disposed) return;
            if (float.IsNaN(age) || float.IsInfinity(age)) age = 0;
            if (float.IsNaN(layoutScale) || float.IsInfinity(layoutScale) || layoutScale <= 0) layoutScale = 1;
            RefreshArtwork();
            var sample = BattleVisualMotion.Sample(pose, age, clock);
            float duration = Math.Max(0.001f, BattleVisualMotion.Duration(pose));
            float t = Math.Max(0, Math.Min(1, age / duration));
            bool summon = pose == BattleVisualPose.Summon;
            float reveal = summon ? 1 - (1 - t) * (1 - t) * (1 - t) : 1;
            float fade = pose == BattleVisualPose.Death ? 1 - t : 1;
            root.transform.localPosition = position;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * layoutScale;
            bool modelVisible = model != null && model.Renderer.enabled;
            if (modelVisible)
            {
                float revealAge = Math.Max(0, Time.unscaledTime - modelRevealStarted);
                if (modelRevealStarted >= 0 && (revealAge >= BattleVisualMotion.Duration(BattleVisualPose.Summon)
                    || (pose != BattleVisualPose.Idle && pose != BattleVisualPose.Summon))) modelRevealStarted = -1;
                model.Animate(modelRevealStarted >= 0 ? BattleVisualPose.Summon : pose,
                    modelRevealStarted >= 0 ? revealAge : age, clock, facing, exhausted);
            }
            floorPivot.localPosition = new Vector3(0, 0.04f + (summon ? (1 - reveal) * 2.8f : 0), 0);
            floorPivot.localRotation = Quaternion.Euler(0, (facing < 0 ? 180 : 0) + (exhausted ? 90 : 0), 0);
            float floorScale = summon ? 0.2f + reveal * 0.8f : 1;
            card.localScale = new Vector3(CardWidth * floorScale, CardHeight * floorScale, 1);

            float lift = nexus ? 0.3f : 0.45f;
            illustration.localPosition = new Vector3(0, lift + portraitHeight * 0.5f * reveal + sample.height,
                // Keep the standing artwork behind the printed face from the
                // camera's direction, so the floor card can still be read.
                CardHeight * 0.48f + sample.advance * facing);
            // Match the camera's image axes. Looking the quad's +Z towards the
            // camera would reverse its X axis and mirror the official artwork.
            // This shader renders both sides, so the camera-aligned back face
            // preserves the original left/right orientation.
            illustration.rotation = camera.transform.rotation
                * Quaternion.Euler(sample.tilt * 0.35f, 0, pose == BattleVisualPose.Damage ? sample.tilt : 0);
            float artScale = (0.12f + reveal * 0.88f) * (pose == BattleVisualPose.Death ? sample.scale : 1);
            illustration.localScale = new Vector3(portraitWidth * artScale, portraitHeight * artScale, 1);
            cardMaterial.SetFloat("_Opacity", fade);
            illustrationMaterial.SetFloat("_Opacity", fade * reveal * (exhausted ? 0.78f : 1));
            cardMaterial.SetFloat("_Glow", sample.glow * 0.2f);
            illustrationMaterial.SetFloat("_Glow", sample.glow * 0.45f);
            bool summoning = summon && t < 1;
            haloRenderer.enabled = summoning || modelVisible;
            haloMaterial.SetFloat("_Ring", summoning ? 1 : 0);
            haloMaterial.SetFloat("_Shadow", summoning ? 0 : 1);
            haloMaterial.SetColor("_BaseColor", summoning ? accent : new Color(.005f, .008f, .015f, .8f));
            halo.localPosition = new Vector3(0, .025f, summoning ? 0 : 1.1f);
            float radius = 2.5f + 4f * t;
            halo.localScale = summoning ? new Vector3(radius, radius, 1) : new Vector3(2.8f, 2.1f, 1);
            haloMaterial.SetFloat("_Opacity", summoning ? (1 - t) * .9f : .48f * fade);
            haloMaterial.SetFloat("_Glow", summoning ? .85f : 0);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            model?.Dispose(); model = null;
            if (artwork != null) Object.Destroy(artwork.gameObject);
            Object.Destroy(root); Object.Destroy(quad);
            Object.Destroy(cardMaterial); Object.Destroy(illustrationMaterial); Object.Destroy(haloMaterial);
            // The session owns assignedTexture; destroying it here could break
            // the player's hand, details or another copy of the same card.
            assignedTexture = null;
        }
    }
}
