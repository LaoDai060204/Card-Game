using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using LaoDai.BattleSpirits.Networking;
using MDPro3;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace LaoDai.BattleSpirits.Presentation
{
    /// <summary>
    /// Renders the installed Master Duel field and cosmetic Mates into a private
    /// camera. No YGO duel controller, network state or gameplay model is used.
    /// </summary>
    public sealed class BattleFieldBackdrop : MonoBehaviour
    {
        private const int StageLayer = 31;
        private const int StageMask = 1 << StageLayer;
        private static readonly Vector3 StageOrigin = new Vector3(10000f, 0f, 0f);

        private GameObject stage;
        private Camera stageCamera;
        private RenderTexture renderTexture;
        private RawImage image;
        private readonly Mate[] mates = new Mate[2];
        private readonly Transform[] anchors = new Transform[2];
        private int generation;
        private bool disposed;

        public bool IsLoaded { get; private set; }
        public string LastError { get; private set; } = string.Empty;

        public void Load(RawImage target)
        {
            if (target == null || disposed)
                return;
            if (image == target && stage != null)
                return;

            generation++;
            ReleaseStage();
            image = target;
            IsLoaded = false;
            LastError = string.Empty;
            try
            {
                CreateStage();
                _ = LoadAssetsAsync(generation);
            }
            catch (Exception exception)
            {
                ReportError("Unable to create the Battle Spirits field preview", exception);
                ReleaseStage();
            }
        }

        private void CreateStage()
        {
            stage = new GameObject("Battle Spirits cosmetic field");
            stage.transform.position = StageOrigin;
            stage.layer = StageLayer;

            var cameraObject = new GameObject("Battle Spirits field camera");
            cameraObject.transform.SetParent(stage.transform, false);
            cameraObject.layer = StageLayer;
            stageCamera = cameraObject.AddComponent<Camera>();
            stageCamera.transform.localPosition = new Vector3(0f, 95f, -37f);
            stageCamera.transform.localEulerAngles = new Vector3(70f, 0f, 0f);
            stageCamera.fieldOfView = 40f;
            stageCamera.nearClipPlane = 0.3f;
            stageCamera.farClipPlane = 400f;
            stageCamera.cullingMask = StageMask;
            stageCamera.clearFlags = CameraClearFlags.SolidColor;
            stageCamera.backgroundColor = new Color(0.015f, 0.025f, 0.045f, 1f);
            stageCamera.allowHDR = false;
            stageCamera.allowMSAA = false;

            // The existing UI URP renderer also supports normal opaque and
            // transparent geometry. Do not change the project's render pipeline.
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = false;
            cameraData.renderType = CameraRenderType.Base;
            cameraData.SetRenderer(0);

            renderTexture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32)
            {
                name = "Battle Spirits field preview",
                filterMode = FilterMode.Bilinear,
                antiAliasing = 1
            };
            renderTexture.Create();
            stageCamera.targetTexture = renderTexture;
            image.texture = renderTexture;
            image.color = Color.white;
            image.raycastTarget = false;

            var lightObject = new GameObject("Battle Spirits field light");
            lightObject.transform.SetParent(stage.transform, false);
            lightObject.transform.localEulerAngles = new Vector3(50f, -30f, 0f);
            lightObject.layer = StageLayer;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1.15f;
            light.shadows = LightShadows.None;
            light.cullingMask = StageMask;

            for (int side = 0; side < anchors.Length; side++)
            {
                var anchor = new GameObject(side == 0 ? "Near Mate" : "Far Mate");
                anchor.transform.SetParent(stage.transform, false);
                anchor.transform.localPosition = side == 0
                    ? new Vector3(-34f, 0f, -17f) : new Vector3(34f, 0f, 17f);
                anchor.layer = StageLayer;
                anchors[side] = anchor.transform;
            }
        }

        private async Task LoadAssetsAsync(int loadGeneration)
        {
            if (MDPro3.Program.items == null)
            {
                LastError = "The cosmetic item catalog has not been initialized.";
                return;
            }

            for (int side = 0; side < 2; side++)
            {
                await LoadFieldAsync(side, loadGeneration);
                if (!IsCurrent(loadGeneration))
                    return;
                await LoadMateAsync(side, loadGeneration);
                if (!IsCurrent(loadGeneration))
                    return;
            }
        }

        private async Task LoadFieldAsync(int side, int loadGeneration)
        {
            try
            {
                if (!TryGetItem(MDPro3.Program.items.mats, "Field", side, out var item))
                    return;

                var field = await ABLoader.LoadFromFileAsync(
                    "MasterDuel/" + item.path + (side == 0 ? "_near" : "_far"), false, true);
                if (field == null)
                    return;
                if (!IsCurrent(loadGeneration))
                {
                    Destroy(field);
                    return;
                }

                field.transform.SetParent(stage.transform, false);
                ChangeLayer(field);
                Tools.PlayAnimation(field.transform, "StartToPhase1");

                var avatar = FindDescendant(field.transform,
                    side == 0 ? "POS_Avatar_near" : "POS_Avatar_far");
                if (avatar != null)
                    anchors[side] = avatar;
                IsLoaded = true;
            }
            catch (Exception exception)
            {
                if (IsCurrent(loadGeneration))
                    ReportError("Unable to load an installed Master Duel field", exception);
            }
        }

        private async Task LoadMateAsync(int side, int loadGeneration)
        {
            try
            {
                if (!TryGetItem(MDPro3.Program.items.mates, "Mate", side, out var item))
                    return;

                var mate = await ABLoader.LoadMateAsync(item.id);
                if (mate == null)
                    return;
                if (!IsCurrent(loadGeneration))
                {
                    Destroy(mate.gameObject);
                    return;
                }

                mate.parent = anchors[side];
                mate.transform.SetParent(anchors[side], false);
                mate.transform.localPosition = Vector3.zero;
                mate.transform.localRotation = Quaternion.identity;
                ChangeLayer(mate.gameObject);
                mate.gameObject.SetActive(true);
                mates[side] = mate;
                mate.Play(Mate.MateAction.Entry);
                IsLoaded = true;
            }
            catch (Exception exception)
            {
                if (IsCurrent(loadGeneration))
                    ReportError("Unable to load an installed cosmetic Mate", exception);
            }
        }

        private static bool TryGetItem(List<Items.Item> items, string kind, int side, out Items.Item result)
        {
            result = default;
            if (items == null || items.Count == 0)
                return false;

            string fallback = items[0].id.ToString(CultureInfo.InvariantCulture);
            string value = Config.Get("BattleSpirits" + kind + side,
                Config.Get("Duel" + kind + side, fallback));
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
            {
                if (id == Items.CODE_NONE)
                    return false;
                foreach (var item in items)
                    if (item.id == id && !item.notReady && !string.IsNullOrEmpty(item.path))
                    {
                        result = item;
                        return true;
                    }
            }

            // Resolve random/same/unknown cosmetic choices locally, without
            // mutating Items.GetAssetPath's legacy duel appearance bookkeeping.
            foreach (var item in items)
                if (!item.notReady && !string.IsNullOrEmpty(item.path))
                {
                    result = item;
                    return true;
                }
            return false;
        }

        public void PlayEvent(PublicBattleEvent evt, int viewer)
        {
            if (evt == null || disposed || evt.actor < 0 || evt.actor > 1)
                return;
            int side = evt.actor == viewer ? 0 : 1;
            switch (evt.kind)
            {
                case "Summoned":
                    PlayMate(side, Mate.MateAction.Entry);
                    break;
                case "Attacked":
                    PlayMate(side, Mate.MateAction.Attack);
                    break;
                case "LifeLost":
                    PlayMate(side, Mate.MateAction.GetDamage);
                    break;
                case "GameWon":
                    PlayMate(side, Mate.MateAction.Victory);
                    PlayMate(1 - side, Mate.MateAction.Defeat);
                    break;
            }
        }

        private void PlayMate(int side, Mate.MateAction action)
        {
            var mate = mates[side];
            if (mate != null)
                mate.Play(action);
        }

        private bool IsCurrent(int loadGeneration)
        {
            return this != null && !disposed && generation == loadGeneration && stage != null;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name)
                    return child;
            return null;
        }

        private static void ChangeLayer(GameObject root)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = StageLayer;
        }

        private void ReportError(string context, Exception exception)
        {
            LastError = context + ": " + exception.Message;
            Debug.LogWarning(LastError);
        }

        private void ReleaseStage()
        {
            if (stageCamera != null)
                stageCamera.targetTexture = null;
            if (image != null && image.texture == renderTexture)
                image.texture = null;
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
            renderTexture = null;
            stageCamera = null;
            if (stage != null)
                Destroy(stage);
            stage = null;
            for (int i = 0; i < mates.Length; i++)
            {
                mates[i] = null;
                anchors[i] = null;
            }
            IsLoaded = false;
        }

        private void OnDestroy()
        {
            disposed = true;
            generation++;
            ReleaseStage();
        }
    }
}
