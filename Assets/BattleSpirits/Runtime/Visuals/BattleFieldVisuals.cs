using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using LaoDai.BattleSpirits.Core;
using LaoDai.BattleSpirits.Networking;
using MDPro3;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LaoDai.BattleSpirits.Visuals
{
    /// <summary>
    /// Battle Spirits' open field, printed cards and card-specific 3D figures. The stage
    /// renders into the existing duel view, not a separate game Canvas.
    /// </summary>
    public sealed class BattleFieldVisuals : MonoBehaviour, IDisposable
    {
        private const int StageLayer = 31;
        private const float CameraPitch = 62;
        private const float CameraFocusZ = -2.5f;
        private static readonly Vector3 StageOrigin = new Vector3(20000, 0, 0);
        private sealed class Unit
        {
            internal int owner;
            internal string cardId;
            internal BattleCardFigure figure;
            internal Vector3 home;
            internal BattleVisualPose pose;
            internal float poseStarted;
            internal float layoutScale = 1;
            internal bool exhausted, removed, nexus;
            internal int visibleNormalCores = -1;
            internal bool visibleSoulCore;
            internal readonly List<GameObject> cores = new List<GameObject>();
        }

        private GameObject stage;
        private Camera camera;
        private RenderTexture texture;
        private RawImage target;
        private readonly Dictionary<int, Unit> units = new Dictionary<int, Unit>();
        private readonly Dictionary<string, CardDefinition> catalog = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<GameObject> poolCrystals = new List<GameObject>();
        private readonly List<int> expired = new List<int>();
        private readonly HashSet<int> present = new HashSet<int>();
        private readonly TextMesh[,] counts = new TextMesh[2, 4];
        private readonly Mate[] mates = new Mate[2];
        private readonly Transform[] mateAnchors = new Transform[2];
        private readonly Transform[,] poolRoots = new Transform[2, 4];
        private readonly Renderer[] decisionRails = new Renderer[2];
        private Material dark, panel, trim, white, red, purple, green, blue, yellow, crystal, soul;
        private Material zoneSurface, zoneFrame, guide;
        private Material playerAccent, opponentAccent;
        private Material cardBackMaterial;
        private Material normalGemMaterial, soulGemMaterial;
        private string normalCoreSkin;
        private Mesh skinQuad;
        private Shader shader, artworkShader;
        private BattleSpiritModelLibrary modelLibrary;
        private Action<string, RawImage> artworkProvider;
        private int viewer, generation;
        private bool disposed;
        private Font font;
        private string poolSignature;
        private int pendingWidth, pendingHeight;
        private float resizeReadyAt;

        public bool IsLoaded => stage != null && !disposed;
        public bool IsDisposed => disposed;
        public string LastError { get; private set; } = string.Empty;

        public void SetArtworkProvider(Action<string, RawImage> provider) => artworkProvider = provider;

        // The cinematic card landing can reveal its already-created model
        // without replaying field audio, moving the printed card or changing
        // the authoritative snapshot.
        public bool RevealSpiritModel(int instanceId)
        {
            if (disposed || stage == null || !units.TryGetValue(instanceId, out var unit) || unit.removed) return false;
            return unit.figure.RevealModel();
        }

        // Unity texture UV origin is bottom-left. These are illustration crops
        // from the official face, not fabricated transparent monster cutouts.
        public static Rect GetIllustrationUv(string cardId)
            => cardId != null && cardId.StartsWith("26R", StringComparison.Ordinal)
                ? new Rect(0.13f, 0.51f, 0.79f, 0.44f) : new Rect(0.10f, 0.48f, 0.82f, 0.46f);

        public bool TryGetCardViewport(int instanceId, out Rect bounds)
        {
            bounds = default;
            if (disposed || camera == null || stage == null || !units.TryGetValue(instanceId, out var unit) || unit.removed) return false;
            RefreshRenderTarget();
            float width = BattleCardFigure.CardWidth * unit.layoutScale;
            float height = BattleCardFigure.CardHeight * unit.layoutScale;
            if (unit.exhausted) { float swap = width; width = height; height = swap; }
            float left = 1, right = 0, bottom = 1, top = 0;
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    var world = stage.transform.TransformPoint(unit.home + new Vector3(x * width * 0.5f, 0.07f, z * height * 0.5f));
                    var point = camera.WorldToViewportPoint(world);
                    if (point.z <= 0) return false;
                    left = Math.Min(left, point.x); right = Math.Max(right, point.x);
                    bottom = Math.Min(bottom, point.y); top = Math.Max(top, point.y);
                }
            left = Math.Max(0, left); right = Math.Min(1, right);
            bottom = Math.Max(0, bottom); top = Math.Min(1, top);
            if (right <= left || top <= bottom) return false;
            bounds = new Rect(left, bottom, right - left, top - bottom); return true;
        }

        public void SetCatalog(IReadOnlyList<CardDefinition> definitions)
        {
            catalog.Clear();
            if (definitions == null) return;
            foreach (var card in definitions) if (card != null && !string.IsNullOrEmpty(card.id)) catalog[card.id] = card;
        }

        public void Load(RawImage output)
        {
            if (disposed || output == null) return;
            if (target == output && stage != null) { RefreshRenderTarget(); return; }
            ReleaseStage(); target = output; generation++;
            try
            {
                CreateStage();
                _ = LoadMatesAsync(generation);
                BattleAudio.PlayMusic();
            }
            catch (Exception error)
            {
                LastError = "Battle Spirits field: " + error.Message;
                Debug.LogError(LastError); ReleaseStage();
            }
        }

        private Material Material(string name, Color color, float glow = 0)
        {
            var result = new Material(shader) { name = "BS_" + name };
            result.SetColor("_BaseColor", color); result.SetFloat("_Glow", glow);
            result.SetVector("_StageOrigin", new Vector4(StageOrigin.x, StageOrigin.y, StageOrigin.z, 0));
            materials.Add(result); return result;
        }

        private void CreateStage()
        {
            shader = Resources.Load<Shader>("BattleSpirits/FieldUnlit");
            if (shader == null) shader = Shader.Find("LaoDai/BattleSpirits/Field");
            if (shader == null) throw new InvalidOperationException("The Battle Spirits field shader is missing.");
            artworkShader = Resources.Load<Shader>("BattleSpirits/CardArtwork");
            if (artworkShader == null) artworkShader = Shader.Find("LaoDai/BattleSpirits/CardArtwork");
            if (artworkShader == null) throw new InvalidOperationException("The Battle Spirits card artwork shader is missing.");
            var modelShader = Resources.Load<Shader>("BattleSpirits/SpiritModel");
            if (modelShader == null) modelShader = Shader.Find("LaoDai/BattleSpirits/SpiritModel");
            // An unavailable optional model shader keeps the complete card/art
            // fallback working instead of preventing a duel from opening.
            if (modelShader != null) modelLibrary = new BattleSpiritModelLibrary(modelShader);
            dark = Material("Obsidian", new Color(0.018f, 0.026f, 0.038f));
            panel = Material("ArenaSlate", new Color(0.068f, 0.088f, 0.113f));
            trim = Material("BrushedChrome", new Color(0.29f, 0.39f, 0.45f), 0.12f);
            zoneSurface = Material("OpenZoneSlate", new Color(0.045f, 0.064f, 0.085f));
            zoneFrame = Material("InsetFrame", new Color(0.105f, 0.15f, 0.18f));
            guide = Material("CyanGuide", new Color(0.13f, 0.42f, 0.49f), 0.2f);
            playerAccent = Material("PlayerActionLime", BattleTheme.Accent, 0.35f);
            opponentAccent = Material("OpponentActionRed", BattleTheme.Red, 0.3f);
            white = Material("IvoryArmor", new Color(0.81f, 0.88f, 0.91f));
            red = Material("DragonRed", new Color(0.58f, 0.035f, 0.045f));
            purple = Material("WingPurple", new Color(0.25f, 0.08f, 0.32f));
            green = Material("ForestGreen", new Color(0.13f, 0.42f, 0.20f));
            blue = Material("IceBlue", new Color(0.10f, 0.33f, 0.67f));
            yellow = Material("SolarYellow", new Color(0.93f, 0.72f, 0.16f));
            crystal = Material("CoreBlue", new Color(0.17f, 0.8f, 1f), 0.6f);
            soul = Material("SoulCore", new Color(0.95f, 0.12f, 0.17f), 0.7f);
            normalCoreSkin = BattleTheme.NormalizeCoreSkin(Config.Get(BattleTheme.CoreSkinConfig, BattleTheme.DefaultCoreSkin));
            normalGemMaterial = ArtworkMaterial("NormalCoreGem", BattleTheme.CoreGem(normalCoreSkin), new Rect(0, 0, 1, 1));
            soulGemMaterial = ArtworkMaterial("SoulCoreGem", BattleTheme.ColorIcon(CardColor.Red), new Rect(0, 0, 1, 1));
            if (BattleTheme.CardBack != null)
                cardBackMaterial = ArtworkMaterial("BattleSpiritsCardBack", BattleTheme.CardBack, new Rect(0, 0, 1, 1));
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            stage = new GameObject("BattleSpirits_Field"); stage.layer = StageLayer; stage.transform.position = StageOrigin;
            var cameraObject = new GameObject("BattleSpirits_FieldCamera"); cameraObject.layer = StageLayer;
            cameraObject.transform.SetParent(stage.transform, false);
            camera = cameraObject.AddComponent<Camera>();
            // A steeper view exposes both players' printed faces instead of
            // looking through large floating illustrations across the table.
            camera.transform.localRotation = Quaternion.Euler(CameraPitch, 0, 0);
            camera.fieldOfView = 46; camera.nearClipPlane = 0.3f; camera.farClipPlane = 200;
            camera.cullingMask = 1 << StageLayer; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.008f, 0.013f, 0.022f); camera.allowHDR = false; camera.allowMSAA = false;
            var urp = cameraObject.AddComponent<UniversalAdditionalCameraData>(); urp.renderType = CameraRenderType.Base;
            urp.renderPostProcessing = false; urp.SetRenderer(0);
            // The field shader is self lit, but retained Master Duel cosmetics
            // still need real illumination after the old YGO field light stops.
            var lightObject = new GameObject("BattleSpirits_FieldLight"); lightObject.layer = StageLayer;
            lightObject.transform.SetParent(stage.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(45, -35, 0);
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional;
            light.intensity = 1.6f; light.color = new Color(0.85f, 0.93f, 1f);
            light.cullingMask = 1 << StageLayer; light.shadows = LightShadows.None;
            RefreshRenderTarget();
            target.color = Color.white; target.raycastTarget = false;
            BuildField();
        }

        private void RefreshRenderTarget()
        {
            if (target == null || camera == null) return;
            var rect = target.rectTransform.rect;
            var canvas = target.GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1;
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0) scale = 1;
            float width = Math.Abs(rect.width) * scale, height = Math.Abs(rect.height) * scale;
            // During initial layout the view can still have a zero rect. Use
            // the previous target until Unity has resolved its actual bounds.
            if (width < 1 || height < 1)
            {
                if (texture != null) return;
                width = 1280; height = 720;
            }
            string quality = Config.Get(BattleTheme.DuelQualityConfig, "Balanced");
            float resolutionScale = string.Equals(quality, "Low", StringComparison.OrdinalIgnoreCase) ? .8f
                : string.Equals(quality, "High", StringComparison.OrdinalIgnoreCase) ? 1.35f : 1;
            width *= resolutionScale; height *= resolutionScale;
            float aspect = width / height;
            int edge = Math.Min(RenderEdgeLimit(), (int)Math.Ceiling(Math.Max(640, Math.Max(width, height)) / 32) * 32);
            int desiredWidth = width >= height ? edge : Math.Max(1, (int)Math.Round(edge * aspect));
            int desiredHeight = width >= height ? Math.Max(1, (int)Math.Round(edge / aspect)) : edge;
            if (texture != null && texture.width == desiredWidth && texture.height == desiredHeight)
            { pendingWidth = pendingHeight = 0; return; }
            if (texture != null)
            {
                // Window drags otherwise create/release a GPU texture for every
                // pixel. Refit once the requested size has settled briefly.
                if (pendingWidth != desiredWidth || pendingHeight != desiredHeight)
                {
                    pendingWidth = desiredWidth; pendingHeight = desiredHeight;
                    resizeReadyAt = Time.unscaledTime + 0.12f; return;
                }
                if (Time.unscaledTime < resizeReadyAt) return;
            }
            var previous = texture;
            texture = new RenderTexture(desiredWidth, desiredHeight, 24, RenderTextureFormat.ARGB32)
            { name = "BattleSpirits_NativeField", antiAliasing = 1, filterMode = FilterMode.Bilinear };
            texture.Create(); camera.targetTexture = texture; target.texture = texture;
            camera.aspect = (float)desiredWidth / desiredHeight;
            pendingWidth = pendingHeight = 0;
            FitCamera((float)desiredWidth / desiredHeight);
            if (previous != null) { previous.Release(); Destroy(previous); }
        }

        private static int RenderEdgeLimit()
        {
            string quality = Config.Get(BattleTheme.DuelQualityConfig, "Balanced");
            if (string.Equals(quality, "Low", StringComparison.OrdinalIgnoreCase)) return 1280;
            return string.Equals(quality, "High", StringComparison.OrdinalIgnoreCase) ? 2560 : 1920;
        }

        private void FitCamera(float aspect)
        {
            double pitch = CameraPitch * Math.PI / 180;
            double sin = Math.Sin(pitch), cos = Math.Cos(pitch);
            double tan = Math.Tan(camera.fieldOfView * Math.PI / 360);
            double distance = 1;
            // Fit the actual table corners, including near-side perspective,
            // rather than clipping Life/Deck pools in a narrow Game window.
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    FitPoint(x * 17.6, 0.3, z * 13.6);
                    FitPoint(x * 12, 5, z * 8);
                }
            void FitPoint(double x, double y, double z)
            {
                z -= CameraFocusZ;
                double depth = z * cos - y * sin;
                distance = Math.Max(distance, Math.Abs(x) / (tan * aspect) - depth);
                distance = Math.Max(distance, Math.Abs(y * cos + z * sin) / tan - depth);
            }
            distance *= 1.045;
            // Bias the target toward the near half to balance perspective:
            // aiming exactly at world zero wastes space above the far rail.
            camera.transform.localPosition = new Vector3(0, (float)(sin * distance), CameraFocusZ + (float)(-cos * distance));
        }

        private void BuildField()
        {
            Part(stage.transform, "StandardArena", PrimitiveType.Cube, new Vector3(0, -0.4f, 0), new Vector3(35, 0.8f, 27), dark);
            // Layered edges give the platform depth without restoring YGO slot
            // meshes or competing with the card faces. All surfaces remain
            // inside the camera's already fitted table bounds.
            Part(stage.transform, "ArenaLowerBevel", PrimitiveType.Cube, new Vector3(0, -0.03f, 0), new Vector3(34.8f, 0.09f, 26.8f), zoneFrame);
            Part(stage.transform, "FieldInset", PrimitiveType.Cube, new Vector3(0, 0.015f, 0), new Vector3(33.6f, 0.06f, 25.6f), panel);
            Part(stage.transform, "CenterLine", PrimitiveType.Cube, new Vector3(0, 0.07f, 0), new Vector3(31.8f, 0.025f, 0.035f), guide);
            BuildPlaymatChrome();
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Part(stage.transform, "SideRail", PrimitiveType.Cube, new Vector3(sign * 17, 0.12f, 0), new Vector3(0.12f, 0.25f, 26), trim);
                Part(stage.transform, "EndRail", PrimitiveType.Cube, new Vector3(0, 0.12f, sign * 13), new Vector3(34, 0.25f, 0.12f), trim);
                Part(stage.transform, "SideInnerRail", PrimitiveType.Cube, new Vector3(sign * 16.23f, 0.09f, 0), new Vector3(0.035f, 0.035f, 24.4f), guide);
                Part(stage.transform, "EndInnerRail", PrimitiveType.Cube, new Vector3(0, 0.09f, sign * 12.23f), new Vector3(31.8f, 0.035f, 0.035f), guide);
                for (int edge = -1; edge <= 1; edge += 2)
                {
                    var corner = Part(stage.transform, "CornerBrace", PrimitiveType.Cube,
                        new Vector3(sign * 16.1f, 0.105f, edge * 12.1f), new Vector3(1.05f, 0.1f, 0.16f), zoneFrame);
                    corner.transform.localRotation = Quaternion.Euler(0, -sign * edge * 45, 0);
                    Part(stage.transform, "SideInlay", PrimitiveType.Cube,
                        new Vector3(sign * 14.6f, 0.055f, edge * 4.5f), new Vector3(0.035f, 0.025f, 8.3f), zoneFrame);
                    // Small raised fixtures give the arena a room silhouette
                    // without covering cards or creating point-light shadows.
                    Part(stage.transform, "CornerPlinth", PrimitiveType.Cube,
                        new Vector3(sign * 16.1f, .18f, edge * 12.1f), new Vector3(.72f, .32f, .72f), dark);
                    Part(stage.transform, "CornerLens", PrimitiveType.Cube,
                        new Vector3(sign * 16.1f, .35f, edge * 12.1f), new Vector3(.48f, .025f, .48f), guide);
                }
                int side = sign < 0 ? 0 : 1;
                var half = BattleSpiritGeometry.Pivot(stage.transform, side == 0 ? "NearPlayerField" : "FarPlayerField", Vector3.zero);
                Zone(half, "SPIRIT", new Vector3(0, 0.1f, sign * 3.8f), new Vector3(23, 0.12f, 4.8f), side);
                Zone(half, "NEXUS", new Vector3(0, 0.1f, sign * 7.75f), new Vector3(18, 0.12f, 2.7f), side);
                decisionRails[side] = Part(half, "DecisionAccent", PrimitiveType.Cube,
                    new Vector3(0, 0.18f, sign * 1.25f), new Vector3(22.8f, 0.035f, 0.05f), trim).GetComponent<Renderer>();
                string[] labels = { "LIFE", "RESERVE", "TRASH", "DECK" };
                var positions = new[] { new Vector3(-13, 0.15f, sign * 9), new Vector3(-5.5f, 0.15f, sign * 10.5f), new Vector3(5.5f, 0.15f, sign * 10.5f), new Vector3(13, 0.15f, sign * 9) };
                for (int zone = 0; zone < 4; zone++)
                {
                    poolRoots[side, zone] = BattleSpiritGeometry.Pivot(half, labels[zone], positions[zone]);
                    Part(poolRoots[side, zone], "ZonePedestal", PrimitiveType.Cube, new Vector3(0, -.075f, 0),
                        new Vector3(3.86f, .16f, 3.36f), dark);
                    Part(poolRoots[side, zone], "ZoneFrame", PrimitiveType.Cube, Vector3.zero,
                        new Vector3(3.7f, 0.055f, 3.2f), zoneFrame);
                    Part(poolRoots[side, zone], "ZonePanel", PrimitiveType.Cube, new Vector3(0, 0.035f, 0),
                        new Vector3(3.54f, 0.035f, 3.04f), zone == 0 ? panel : zoneSurface);
                    Part(poolRoots[side, zone], "ZoneFrontLight", PrimitiveType.Cube, new Vector3(0, .066f, -1.46f),
                        new Vector3(2.8f, .018f, .025f), guide);
                    counts[side, zone] = Label(half, labels[zone], positions[zone] + new Vector3(0, 0.17f, -sign * 1.4f), side);
                }
                mateAnchors[side] = BattleSpiritGeometry.Pivot(stage.transform, side == 0 ? "NearMate" : "FarMate", new Vector3(side == 0 ? -15.5f : 15.5f, 0.25f, sign * 3));
                Label(half, side == 0 ? "BATTLE SPIRITS" : "STANDARD", new Vector3(0, 0.12f, sign * 12), side);
            }
            var colors = new[] { red, purple, green, white, yellow, blue };
            var cardColors = new[] { CardColor.Red, CardColor.Purple, CardColor.Green, CardColor.White, CardColor.Yellow, CardColor.Blue };
            for (int index = 0; index < colors.Length; index++)
            {
                var icon = BattleTheme.ColorIcon(cardColors[index]);
                if (icon != null)
                    TexturedPlane(stage.transform, "ColorSymbol_" + cardColors[index],
                        new Vector3((index - 2.5f) * 0.9f, 0.14f, -0.75f), new Vector2(0.68f, 0.68f),
                        ArtworkMaterial("ColorSymbol_" + cardColors[index], icon, new Rect(0, 0, 1, 1)));
                else
                    Part(stage.transform, "ColorSymbolFallback_" + cardColors[index], PrimitiveType.Sphere,
                        new Vector3((index - 2.5f) * 0.9f, 0.17f, -0.75f), new Vector3(0.38f, 0.14f, 0.38f), colors[index]);
            }
        }

        private void BuildPlaymatChrome()
        {
            var sheet = BattleTheme.PlaymatStandard;
            if (sheet == null) return;
            // Reuse the official Standard sheet's ornament and logo, keeping
            // their aspect at the corners. Copying a complete one-player sheet
            // into this two-player field would distort it and duplicate printed
            // Japanese labels over the actual Life/Core/Deck controls.
            const float edge = 0.85f, halfWidth = 16.8f, halfDepth = 12.8f;
            var top = ArtworkMaterial("PlaymatTop", sheet, new Rect(0.035f, 0.95f, 0.93f, 0.05f));
            var bottom = ArtworkMaterial("PlaymatBottom", sheet, new Rect(0.035f, 0, 0.93f, 0.05f));
            var left = ArtworkMaterial("PlaymatLeft", sheet, new Rect(0, 0.05f, 0.035f, 0.90f));
            var right = ArtworkMaterial("PlaymatRight", sheet, new Rect(0.965f, 0.05f, 0.035f, 0.90f));
            var ornamentTint = new Color(0.34f, 0.46f, 0.51f);
            top.SetColor("_BaseColor", ornamentTint); bottom.SetColor("_BaseColor", ornamentTint);
            left.SetColor("_BaseColor", ornamentTint); right.SetColor("_BaseColor", ornamentTint);
            TexturedPlane(stage.transform, "PlaymatBorderTop", new Vector3(0, 0.11f, halfDepth), new Vector2(halfWidth * 2 - edge, edge), top);
            TexturedPlane(stage.transform, "PlaymatBorderBottom", new Vector3(0, 0.11f, -halfDepth), new Vector2(halfWidth * 2 - edge, edge), bottom);
            TexturedPlane(stage.transform, "PlaymatBorderLeft", new Vector3(-halfWidth, 0.11f, 0), new Vector2(edge, halfDepth * 2 - edge), left);
            TexturedPlane(stage.transform, "PlaymatBorderRight", new Vector3(halfWidth, 0.11f, 0), new Vector2(edge, halfDepth * 2 - edge), right);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    var uv = new Rect(x < 0 ? 0 : 0.965f, z < 0 ? 0 : 0.95f, 0.035f, 0.05f);
                    var corner = ArtworkMaterial("PlaymatCorner", sheet, uv); corner.SetColor("_BaseColor", ornamentTint);
                    TexturedPlane(stage.transform, "PlaymatCorner", new Vector3(x * halfWidth, 0.115f, z * halfDepth),
                        new Vector2(edge, edge), corner);
                }
            var logoUv = new Rect(0.30f, 0.60f, 0.40f, 0.16f);
            float logoHeight = 4.9f * sheet.height * logoUv.height / (sheet.width * logoUv.width);
            var logo = ArtworkMaterial("PlaymatLogo", sheet, logoUv); logo.SetColor("_BaseColor", new Color(0.62f, 0.71f, 0.75f));
            TexturedPlane(stage.transform, "BattleSpiritsPlaymatLogo", new Vector3(0, 0.12f, 0.35f),
                new Vector2(4.9f, logoHeight), logo);
        }

        private Material ArtworkMaterial(string name, Texture image, Rect uv)
        {
            var result = new Material(artworkShader) { name = "BS_" + name, mainTexture = image };
            result.SetColor("_BaseColor", Color.white); result.SetColor("_Accent", Color.clear);
            result.SetVector("_UvRect", new Vector4(uv.x, uv.y, uv.width, uv.height));
            result.SetFloat("_Opacity", 1); result.SetFloat("_Glow", 0); result.SetFloat("_Feather", 0);
            materials.Add(result); return result;
        }

        private GameObject TexturedPlane(Transform parent, string name, Vector3 position, Vector2 size, Material material, float facing = 0)
        {
            if (skinQuad == null)
            {
                skinQuad = new Mesh { name = "BattleSpiritsSkinQuad" };
                skinQuad.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(-0.5f, 0, 0.5f) };
                skinQuad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                skinQuad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                skinQuad.RecalculateNormals(); skinQuad.RecalculateBounds(); meshes.Add(skinQuad);
            }
            var go = new GameObject(name); go.layer = StageLayer; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localRotation = Quaternion.Euler(0, facing, 0);
            go.transform.localScale = new Vector3(size.x, 1, size.y);
            go.AddComponent<MeshFilter>().sharedMesh = skinQuad;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        private void Zone(Transform parent, string text, Vector3 position, Vector3 scale, int side)
        {
            Part(parent, text + "Border", PrimitiveType.Cube, position, scale, zoneFrame);
            Part(parent, text + "OpenArea", PrimitiveType.Cube, position + Vector3.up * 0.07f, new Vector3(scale.x - 0.08f, scale.y, scale.z - 0.08f), zoneSurface);
            // Thin edge markers describe an open Battle Spirits area; they do
            // not divide the space into Yu-Gi-Oh monster/card slots.
            float edge = scale.z * .5f - .12f;
            Part(parent, text + "Guide", PrimitiveType.Cube, position + new Vector3(0, .145f, side == 0 ? -edge : edge),
                new Vector3(scale.x - .24f, .015f, .025f), guide);
            Label(parent, text, position + new Vector3(-scale.x * 0.40f, 0.15f, 0), side);
        }

        private TextMesh Label(Transform parent, string value, Vector3 position, int side)
        {
            if (font == null) return null;
            var go = new GameObject("Label_" + value); go.layer = StageLayer; go.transform.SetParent(parent, false);
            // Printed card orientation still reflects ownership; readable UI
            // labels are always upright from the local camera's side.
            go.transform.localPosition = position; go.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var text = go.AddComponent<TextMesh>(); text.text = value; text.font = font; text.fontSize = 42;
            text.characterSize = 0.15f; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
            text.color = new Color(0.79f, 0.85f, 0.89f); go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return text;
        }

        public void Render(FilteredMatchView view)
        {
            if (disposed || stage == null || view == null || view.players == null) return;
            RefreshCoreGemSkin();
            viewer = view.viewer;
            present.Clear();
            foreach (var player in view.players)
            {
                if (player == null || player.playerIndex < 0 || player.playerIndex > 1) continue;
                int side = player.playerIndex == viewer ? 0 : 1;
                if (decisionRails[side] != null)
                    decisionRails[side].sharedMaterial = view.winner < 0 && view.decisionPlayer == player.playerIndex
                        ? side == 0 ? playerAccent : opponentAccent : trim;
                var field = player.field ?? Array.Empty<SpiritView>();
                int spiritCount = field.Count(item => item != null && !IsNexus(item.cardId));
                int nexusCount = field.Count(item => item != null && IsNexus(item.cardId));
                int spiritIndex = 0, nexusIndex = 0;
                for (int slot = 0; slot < field.Length; slot++)
                {
                    var spirit = field[slot]; if (spirit == null) continue;
                    present.Add(spirit.instanceId);
                    if (!units.TryGetValue(spirit.instanceId, out var unit))
                    {
                        unit = NewUnit(spirit, player.playerIndex);
                        units.Add(spirit.instanceId, unit);
                    }
                    unit.removed = false; unit.exhausted = spirit.exhausted;
                    float sign = side == 0 ? -1 : 1;
                    if (unit.nexus)
                    {
                        int columns = Math.Min(8, Math.Max(1, nexusCount));
                        int rows = Math.Max(1, (nexusCount + columns - 1) / columns);
                        int index = nexusIndex++, row = index / columns, column = index % columns;
                        int rowCount = Math.Min(columns, nexusCount - row * columns);
                        unit.layoutScale = Math.Min(0.72f, Math.Min(16.8f / columns, 2.6f / rows) / 3.7f);
                        unit.home = new Vector3((column - (rowCount - 1) * 0.5f) * (16f / columns), 0.22f,
                            sign * (6.4f + (row + 0.5f) * (2.7f / rows)));
                    }
                    else
                    {
                        // Battle Spirits has an open field, not YGO's fixed
                        // monster slots. Fit every current Spirit in its zone.
                        int columns = Math.Min(6, Math.Max(1, spiritCount));
                        int rows = Math.Max(1, (spiritCount + columns - 1) / columns);
                        int index = spiritIndex++, row = index / columns, column = index % columns;
                        int rowCount = Math.Min(columns, spiritCount - row * columns);
                        // Leave room for the full long edge after exhausting a
                        // card. Sparse fields can use larger faces; crowded
                        // fields keep every instance inside its own cell.
                        unit.layoutScale = Math.Min(1.22f, Math.Min(23f / columns, 4.8f / rows) / 3.7f);
                        unit.home = new Vector3((column - (rowCount - 1) * 0.5f) * (23f / columns), 0.22f,
                            sign * (1.4f + (row + 0.5f) * (4.8f / rows)));
                    }
                    ShowSpiritCores(unit, spirit);
                    // Establish the correct card transform immediately. A UI
                    // snapshot may need hit bounds before Unity's next Update.
                    unit.figure.Animate(unit.pose, Time.unscaledTime - unit.poseStarted, Time.unscaledTime,
                        unit.home, unit.owner == viewer ? 1 : -1, unit.exhausted, unit.layoutScale);
                }
                SetCount(side, 0, "LIFE " + player.life);
                SetCount(side, 1, "RESERVE " + player.reserveNormal + (player.reserveSoul ? " + SOUL" : string.Empty));
                SetCount(side, 2, "TRASH " + player.trashNormal + (player.trashSoul ? " + SOUL" : string.Empty));
                SetCount(side, 3, "DECK " + player.deckCount);
            }
            foreach (var pair in units)
                if (!present.Contains(pair.Key) && !pair.Value.removed)
                { pair.Value.removed = true; Animate(pair.Value, BattleVisualPose.Death); }
            RefreshCorePools(view);
        }

        private Unit NewUnit(SpiritView spirit, int owner)
        {
            CardColor color = catalog.TryGetValue(spirit.cardId ?? string.Empty, out var card) ? card.color : CardColor.Red;
            Color accent = color == CardColor.White ? new Color(0.8f, 0.92f, 1)
                : color == CardColor.Blue ? new Color(0.1f, 0.5f, 1)
                : color == CardColor.Green ? new Color(0.15f, 1, 0.4f)
                : color == CardColor.Purple ? new Color(0.7f, 0.25f, 1)
                : color == CardColor.Yellow ? new Color(1, 0.78f, 0.12f) : new Color(1, 0.2f, 0.12f);
            var figure = new BattleCardFigure(stage.transform, target.transform, spirit.cardId ?? string.Empty,
                artworkShader, camera, accent, artworkProvider, IsNexus(spirit.cardId), modelLibrary);
            var unit = new Unit { owner = owner, cardId = spirit.cardId, figure = figure, nexus = IsNexus(spirit.cardId) };
            Animate(unit, BattleVisualPose.Summon); return unit;
        }

        private bool IsNexus(string id) => id != null && catalog.TryGetValue(id, out var card)
            && string.Equals(card.type, "Nexus", StringComparison.OrdinalIgnoreCase);

        private void ShowSpiritCores(Unit unit, SpiritView view)
        {
            if (unit.visibleNormalCores == view.normalCores && unit.visibleSoulCore == view.soulCore) return;
            foreach (var item in unit.cores) if (item != null) Destroy(item);
            unit.cores.Clear(); unit.visibleNormalCores = view.normalCores; unit.visibleSoulCore = view.soulCore;
            int count = Math.Min(8, Math.Max(0, view.normalCores));
            for (int index = 0; index < count; index++)
            {
                var marker = Gem(unit.figure.root.transform, "SpiritCore",
                    new Vector3((index % 4 - 1.5f) * 0.25f, 0.25f, -0.75f - index / 4 * 0.25f), 0.22f, false);
                unit.cores.Add(marker);
            }
            if (view.soulCore) unit.cores.Add(Gem(unit.figure.root.transform, "SpiritSoulCore",
                new Vector3(0, 0.56f, -0.85f), 0.36f, true));
        }

        private void SetCount(int side, int zone, string text)
        { if (counts[side, zone] != null) counts[side, zone].text = text; }

        private void RefreshCorePools(FilteredMatchView view)
        {
            string signature = string.Join(";", view.players.Where(player => player != null).Select(player =>
                player.playerIndex + ":" + player.life + ":" + player.reserveNormal + ":" + player.reserveSoul
                + ":" + player.trashNormal + ":" + player.trashSoul + ":" + player.deckCount)) + ":" + viewer;
            if (signature == poolSignature) return;
            poolSignature = signature;
            foreach (var core in poolCrystals) if (core != null) Destroy(core);
            poolCrystals.Clear();
            foreach (var player in view.players)
            {
                if (player == null || player.playerIndex < 0 || player.playerIndex > 1) continue;
                int side = player.playerIndex == viewer ? 0 : 1;
                Pool(poolRoots[side, 0], Math.Max(0, player.life), false);
                Pool(poolRoots[side, 1], Math.Max(0, player.reserveNormal), player.reserveSoul);
                Pool(poolRoots[side, 2], Math.Max(0, player.trashNormal), player.trashSoul);
                int height = Math.Min(5, Math.Max(0, player.deckCount));
                if (height > 0)
                {
                    poolCrystals.Add(Part(poolRoots[side, 3], "DeckStack", PrimitiveType.Cube,
                        new Vector3(0, 0.15f, 0), new Vector3(1.2f, height * 0.055f, 1.8f), panel));
                    if (cardBackMaterial != null)
                    {
                        var back = BattleTheme.CardBack;
                        float cardHeight = 1.2f * back.height / Math.Max(1, back.width);
                        poolCrystals.Add(TexturedPlane(poolRoots[side, 3], "DeckCardBack",
                            new Vector3(0, 0.155f + height * 0.0275f, 0), new Vector2(1.2f, cardHeight), cardBackMaterial, side == 0 ? 0 : 180));
                    }
                }
            }
        }

        private void Pool(Transform root, int normal, bool hasSoul)
        {
            // Counts remain exact in labels; render at most twelve crystals per
            // pool so large late-game reserves do not create unlimited meshes.
            int count = Math.Min(12, normal);
            for (int index = 0; index < count; index++)
            {
                var core = Gem(root, "Core",
                    new Vector3((index % 4 - 1.5f) * 0.45f, 0.32f, (index / 4 - 1) * 0.45f), 0.4f, false);
                poolCrystals.Add(core);
            }
            if (hasSoul) poolCrystals.Add(Gem(root, "SoulCore", new Vector3(0, 0.75f, 0), 0.52f, true));
        }

        private GameObject Gem(Transform parent, string name, Vector3 position, float width, bool isSoul)
        {
            var material = isSoul ? soulGemMaterial : normalGemMaterial;
            var image = material == null ? null : material.mainTexture;
            if (image == null)
                return Part(parent, name, PrimitiveType.Sphere, position,
                    new Vector3(width, width * 1.4f, width), isSoul ? soul : crystal);
            float height = width * image.height / Math.Max(1, image.width);
            var gem = TexturedPlane(parent, name, position, new Vector2(width, height), material);
            // skinQuad is horizontal (u=+X, v=+Z). This rotation aligns the
            // image to the camera's right/up axes without mirroring its face.
            gem.transform.rotation = camera.transform.rotation * Quaternion.Euler(-90, 0, 0);
            return gem;
        }

        private void RefreshCoreGemSkin()
        {
            string desired = BattleTheme.NormalizeCoreSkin(Config.Get(BattleTheme.CoreSkinConfig, BattleTheme.DefaultCoreSkin));
            if (normalGemMaterial == null || desired == normalCoreSkin) return;
            bool wasTextured = normalGemMaterial.mainTexture != null;
            normalCoreSkin = desired;
            normalGemMaterial.mainTexture = BattleTheme.CoreGem(desired);
            if (wasTextured == (normalGemMaterial.mainTexture != null)) return;
            // Rebuild only if the loader changes between image and fallback
            // geometry. Normal cosmetic changes update the shared material.
            poolSignature = null;
            foreach (var unit in units.Values) unit.visibleNormalCores = -1;
        }

        public void PlayEvent(PublicBattleEvent message)
        {
            if (disposed || stage == null || message == null) return;
            Unit unit = null; if (message.spiritId >= 0) units.TryGetValue(message.spiritId, out unit);
            int side = message.actor == viewer ? 0 : 1;
            switch (message.kind)
            {
                case "NexusPlaced": case "Summoned":
                    if (unit != null) Animate(unit, BattleVisualPose.Summon);
                    BattleAudio.PlayCue(message.cardId == "BS01-X01" || message.cardId == "BS01-097" ? "boss_summon" : "summon");
                    PlayMate(side, Mate.MateAction.Entry); break;
                case "Attacked":
                    if (unit != null) Animate(unit, BattleVisualPose.Attack);
                    BattleAudio.PlayCue("attack"); PlayMate(side, Mate.MateAction.Attack); break;
                case "Blocked":
                    if (unit != null) Animate(unit, BattleVisualPose.Block);
                    BattleAudio.PlayCue("core", 0.6f); break;
                case "CoreMoved": case "CoreSwapped": case "CoreGained": case "CoresRecycled": case "LifeGained": case "Awakened":
                    BattleAudio.PlayCue("core", 0.6f); break;
                case "MagicPlayed": case "BpChanged": BattleAudio.PlayCue("summon", 0.55f); break;
                case "LifeLost":
                    BattleAudio.PlayCue("damage"); PlayMate(side, Mate.MateAction.GetDamage);
                    foreach (var targetUnit in units.Values) if (targetUnit.owner == message.actor && !targetUnit.removed) Animate(targetUnit, BattleVisualPose.Damage);
                    break;
                case "Destroyed": case "Vanished":
                    if (unit != null) { unit.removed = true; Animate(unit, BattleVisualPose.Death); }
                    BattleAudio.PlayCue("destroy"); break;
                case "GameWon":
                    BattleAudio.PlayCue(message.actor == viewer ? "victory" : "defeat");
                    foreach (var targetUnit in units.Values) if (!targetUnit.removed) Animate(targetUnit, targetUnit.owner == message.actor ? BattleVisualPose.Victory : BattleVisualPose.Defeat);
                    PlayMate(side, Mate.MateAction.Victory); PlayMate(1 - side, Mate.MateAction.Defeat); break;
            }
        }

        private void PlayMate(int side, Mate.MateAction action)
        { if (side >= 0 && side < mates.Length && mates[side] != null) mates[side].Play(action); }

        private static void Animate(Unit unit, BattleVisualPose pose)
        { unit.pose = pose; unit.poseStarted = Time.unscaledTime; }

        private void Update()
        {
            if (stage == null || disposed) return;
            RefreshRenderTarget();
            RefreshCoreGemSkin();
            float time = Time.unscaledTime; expired.Clear();
            foreach (var pair in units)
            {
                var unit = pair.Value;
                float age = time - unit.poseStarted;
                if (unit.removed && age >= BattleVisualMotion.Duration(BattleVisualPose.Death))
                { unit.figure.Dispose(); expired.Add(pair.Key); continue; }
                if (unit.pose != BattleVisualPose.Idle && unit.pose != BattleVisualPose.Defeat && !unit.removed && age > BattleVisualMotion.Duration(unit.pose)) unit.pose = BattleVisualPose.Idle;
                float facing = unit.owner == viewer ? 1 : -1;
                unit.figure.Animate(unit.pose, age, time + pair.Key * 0.7f, unit.home, facing, unit.exhausted, unit.layoutScale);
            }
            foreach (int id in expired) units.Remove(id);
        }

        private async Task LoadMatesAsync(int version)
        {
            if (Program.items == null || Program.items.mates == null || Program.items.mates.Count == 0) return;
            for (int side = 0; side < 2; side++)
            {
                int fallback = Program.items.mates[0].id;
                string setting = Config.Get("BattleSpiritsMate" + side, Config.Get("DuelMate" + side, fallback.ToString(CultureInfo.InvariantCulture)));
                if (!int.TryParse(setting, out int id) || id == Items.CODE_NONE) continue;
                try
                {
                    Mate mate = await ABLoader.LoadMateAsync(id);
                    if (mate == null) continue;
                    if (this == null || disposed || version != generation || stage == null)
                    { Destroy(mate.gameObject); return; }
                    mate.parent = mateAnchors[side]; mate.transform.SetParent(mateAnchors[side], false);
                    mate.transform.localPosition = Vector3.zero; mate.transform.localRotation = Quaternion.identity;
                    mate.transform.localScale = Vector3.one * 0.12f;
                    foreach (var child in mate.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = StageLayer;
                    mates[side] = mate; mate.Play(Mate.MateAction.Entry);
                }
                catch (Exception error) { if (this != null && version == generation && !disposed) Debug.LogWarning("Battle Spirits cosmetic Mate: " + error.Message); }
            }
        }

        private static GameObject Part(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 size, Material material)
            => BattleSpiritGeometry.Part(parent, name, shape, position, size, material);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; generation++; ReleaseStage();
        }

        private void OnDestroy() => Dispose();

        private void ReleaseStage()
        {
            if (target != null && target.texture == texture) target.texture = null;
            if (camera != null) camera.targetTexture = null;
            if (texture != null) { texture.Release(); Destroy(texture); }
            foreach (var unit in units.Values) unit.figure.Dispose();
            modelLibrary?.Dispose(); modelLibrary = null;
            if (stage != null) Destroy(stage);
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            foreach (var material in materials) if (material != null) Destroy(material);
            meshes.Clear(); materials.Clear(); units.Clear(); poolCrystals.Clear(); expired.Clear(); present.Clear();
            for (int side = 0; side < 2; side++)
            { mates[side] = null; mateAnchors[side] = null; decisionRails[side] = null; for (int zone = 0; zone < 4; zone++) { counts[side, zone] = null; poolRoots[side, zone] = null; } }
            texture = null; camera = null; stage = null; poolSignature = null; skinQuad = null; cardBackMaterial = null;
            pendingWidth = pendingHeight = 0; resizeReadyAt = 0;
            normalGemMaterial = soulGemMaterial = null; normalCoreSkin = null;
        }
    }
}
