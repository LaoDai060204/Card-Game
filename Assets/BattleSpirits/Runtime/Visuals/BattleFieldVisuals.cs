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
    /// Battle Spirits' own open field and original 3D Spirit figures. The stage
    /// renders into the existing duel view, not a separate game Canvas.
    /// </summary>
    public sealed class BattleFieldVisuals : MonoBehaviour, IDisposable
    {
        private const int StageLayer = 31;
        private static readonly Vector3 StageOrigin = new Vector3(20000, 0, 0);
        private sealed class Unit
        {
            internal int owner;
            internal string cardId;
            internal BattleSpiritGeometry figure;
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
        private readonly TextMesh[,] counts = new TextMesh[2, 4];
        private readonly Mate[] mates = new Mate[2];
        private readonly Transform[] mateAnchors = new Transform[2];
        private readonly Transform[,] poolRoots = new Transform[2, 4];
        private Material dark, panel, trim, gold, white, red, purple, green, blue, yellow, crystal, soul;
        private Shader shader;
        private int viewer, generation;
        private bool disposed;
        private Font font;
        private string poolSignature;

        public bool IsLoaded => stage != null && !disposed;
        public bool IsDisposed => disposed;
        public string LastError { get; private set; } = string.Empty;

        public void SetCatalog(IReadOnlyList<CardDefinition> definitions)
        {
            catalog.Clear();
            if (definitions == null) return;
            foreach (var card in definitions) if (card != null && !string.IsNullOrEmpty(card.id)) catalog[card.id] = card;
        }

        public void Load(RawImage output)
        {
            if (disposed || output == null) return;
            if (target == output && stage != null) return;
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
            materials.Add(result); return result;
        }

        private void CreateStage()
        {
            shader = Resources.Load<Shader>("BattleSpirits/FieldUnlit");
            if (shader == null) shader = Shader.Find("LaoDai/BattleSpirits/Field");
            if (shader == null) throw new InvalidOperationException("The Battle Spirits field shader is missing.");
            dark = Material("NightStone", new Color(0.018f, 0.035f, 0.065f));
            panel = Material("FieldBlue", new Color(0.035f, 0.10f, 0.14f));
            trim = Material("CircuitCyan", new Color(0.12f, 0.65f, 0.72f), 0.35f);
            gold = Material("DragonGold", new Color(0.82f, 0.55f, 0.17f));
            white = Material("IvoryArmor", new Color(0.81f, 0.88f, 0.91f));
            red = Material("DragonRed", new Color(0.58f, 0.035f, 0.045f));
            purple = Material("WingPurple", new Color(0.25f, 0.08f, 0.32f));
            green = Material("ForestGreen", new Color(0.13f, 0.42f, 0.20f));
            blue = Material("IceBlue", new Color(0.10f, 0.33f, 0.67f));
            yellow = Material("SolarYellow", new Color(0.93f, 0.72f, 0.16f));
            crystal = Material("CoreBlue", new Color(0.17f, 0.8f, 1f), 0.6f);
            soul = Material("SoulCore", new Color(0.95f, 0.2f, 0.6f), 0.7f);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            stage = new GameObject("BattleSpirits_Field"); stage.layer = StageLayer; stage.transform.position = StageOrigin;
            var cameraObject = new GameObject("BattleSpirits_FieldCamera"); cameraObject.layer = StageLayer;
            cameraObject.transform.SetParent(stage.transform, false);
            camera = cameraObject.AddComponent<Camera>();
            camera.transform.localPosition = new Vector3(0, 28, -27);
            camera.transform.localRotation = Quaternion.Euler(47, 0, 0);
            camera.fieldOfView = 48; camera.nearClipPlane = 0.3f; camera.farClipPlane = 100;
            camera.cullingMask = 1 << StageLayer; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.007f, 0.015f, 0.03f); camera.allowHDR = false; camera.allowMSAA = false;
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
            texture = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32)
            { name = "BattleSpirits_NativeField", antiAliasing = 1, filterMode = FilterMode.Bilinear };
            texture.Create(); camera.targetTexture = texture;
            target.texture = texture; target.color = Color.white; target.raycastTarget = false;
            BuildField();
        }

        private void BuildField()
        {
            Part(stage.transform, "EternalArena", PrimitiveType.Cube, new Vector3(0, -0.4f, 0), new Vector3(35, 0.8f, 27), dark);
            Part(stage.transform, "FieldInset", PrimitiveType.Cube, new Vector3(0, 0.015f, 0), new Vector3(33.6f, 0.06f, 25.6f), panel);
            Part(stage.transform, "CenterLine", PrimitiveType.Cube, new Vector3(0, 0.07f, 0), new Vector3(32, 0.04f, 0.06f), trim);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Part(stage.transform, "SideRail", PrimitiveType.Cube, new Vector3(sign * 17, 0.12f, 0), new Vector3(0.08f, 0.25f, 26), trim);
                Part(stage.transform, "EndRail", PrimitiveType.Cube, new Vector3(0, 0.12f, sign * 13), new Vector3(34, 0.25f, 0.08f), trim);
                int side = sign < 0 ? 0 : 1;
                var half = BattleSpiritGeometry.Pivot(stage.transform, side == 0 ? "NearPlayerField" : "FarPlayerField", Vector3.zero);
                Zone(half, "SPIRIT", new Vector3(0, 0.1f, sign * 3.8f), new Vector3(23, 0.12f, 4.8f), side);
                Zone(half, "NEXUS", new Vector3(0, 0.1f, sign * 7.4f), new Vector3(18, 0.12f, 1.8f), side);
                string[] labels = { "LIFE", "RESERVE", "TRASH", "DECK" };
                var positions = new[] { new Vector3(-13, 0.15f, sign * 9), new Vector3(-5.5f, 0.15f, sign * 10.5f), new Vector3(5.5f, 0.15f, sign * 10.5f), new Vector3(13, 0.15f, sign * 9) };
                for (int zone = 0; zone < 4; zone++)
                {
                    poolRoots[side, zone] = BattleSpiritGeometry.Pivot(half, labels[zone], positions[zone]);
                    Part(poolRoots[side, zone], "ZoneDisc", PrimitiveType.Cylinder, Vector3.zero, new Vector3(3.2f, 0.055f, 3.2f), zone == 0 ? gold : dark);
                    counts[side, zone] = Label(half, labels[zone], positions[zone] + new Vector3(0, 0.17f, -sign * 1.4f), side);
                }
                mateAnchors[side] = BattleSpiritGeometry.Pivot(stage.transform, side == 0 ? "NearMate" : "FarMate", new Vector3(side == 0 ? -15.5f : 15.5f, 0.25f, sign * 3));
                Label(half, side == 0 ? "BATTLE SPIRITS" : "ETERNAL", new Vector3(0, 0.12f, sign * 12), side);
            }
            var colors = new[] { red, purple, green, white, yellow, blue };
            for (int index = 0; index < colors.Length; index++)
            {
                float angle = index * Mathf.PI / 3;
                var emblem = Part(stage.transform, "SixColorEmblem", PrimitiveType.Sphere, new Vector3(Mathf.Cos(angle) * 1.1f, 0.17f, Mathf.Sin(angle) * 0.6f), new Vector3(0.38f, 0.14f, 0.38f), colors[index]);
                emblem.transform.localRotation = Quaternion.Euler(0, index * 60, 0);
            }
        }

        private void Zone(Transform parent, string text, Vector3 position, Vector3 scale, int side)
        {
            Part(parent, text + "Border", PrimitiveType.Cube, position, scale, trim);
            Part(parent, text + "OpenArea", PrimitiveType.Cube, position + Vector3.up * 0.07f, new Vector3(scale.x - 0.1f, scale.y, scale.z - 0.1f), dark);
            Label(parent, text, position + new Vector3(-scale.x * 0.42f, 0.15f, 0), side);
        }

        private TextMesh Label(Transform parent, string value, Vector3 position, int side)
        {
            if (font == null) return null;
            var go = new GameObject("Label_" + value); go.layer = StageLayer; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localRotation = Quaternion.Euler(90, side == 0 ? 0 : 180, 0);
            var text = go.AddComponent<TextMesh>(); text.text = value; text.font = font; text.fontSize = 42;
            text.characterSize = 0.09f; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
            text.color = new Color(0.72f, 0.91f, 0.95f); go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return text;
        }

        public void Render(FilteredMatchView view)
        {
            if (disposed || stage == null || view == null || view.players == null) return;
            viewer = view.viewer;
            var present = new HashSet<int>();
            foreach (var player in view.players)
            {
                if (player == null || player.playerIndex < 0 || player.playerIndex > 1) continue;
                int side = player.playerIndex == viewer ? 0 : 1;
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
                        unit.layoutScale = Math.Min(0.75f, 1f / rows);
                        unit.home = new Vector3((column - (rowCount - 1) * 0.5f) * (16f / columns), 0.22f,
                            sign * (6.55f + (row + 0.5f) * (1.7f / rows)));
                    }
                    else
                    {
                        // Battle Spirits has an open field, not YGO's fixed
                        // monster slots. Fit every current Spirit in its zone.
                        int columns = Math.Min(6, Math.Max(1, spiritCount));
                        int rows = Math.Max(1, (spiritCount + columns - 1) / columns);
                        int index = spiritIndex++, row = index / columns, column = index % columns;
                        int rowCount = Math.Min(columns, spiritCount - row * columns);
                        unit.layoutScale = Math.Min(1f, 1.5f / rows);
                        unit.home = new Vector3((column - (rowCount - 1) * 0.5f) * (23f / columns), 0.22f,
                            sign * (1.4f + (row + 0.5f) * (4.8f / rows)));
                    }
                    ShowSpiritCores(unit, spirit);
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
            Material skin = color == CardColor.White ? white : color == CardColor.Blue ? blue : color == CardColor.Green ? green : color == CardColor.Purple ? purple : color == CardColor.Yellow ? yellow : red;
            bool steel = spirit.cardId == "BS01-097";
            var figure = new BattleSpiritGeometry(stage.transform, spirit.cardId ?? string.Empty,
                steel ? blue : skin, steel || color == CardColor.Red ? gold : white, steel ? blue : purple, color == CardColor.Red ? yellow : crystal, meshes, IsNexus(spirit.cardId));
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
                var marker = Part(unit.figure.root.transform, "SpiritCore", PrimitiveType.Sphere,
                    new Vector3((index % 4 - 1.5f) * 0.25f, 0.14f, -0.75f - index / 4 * 0.25f), new Vector3(0.18f, 0.3f, 0.18f), crystal);
                unit.cores.Add(marker);
            }
            if (view.soulCore) unit.cores.Add(Part(unit.figure.root.transform, "SpiritSoulCore", PrimitiveType.Sphere,
                new Vector3(0, 0.4f, -0.85f), new Vector3(0.24f, 0.4f, 0.24f), soul));
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
                if (height > 0) poolCrystals.Add(Part(poolRoots[side, 3], "DeckStack", PrimitiveType.Cube, new Vector3(0, 0.15f, 0), new Vector3(1.2f, height * 0.055f, 1.8f), panel));
            }
        }

        private void Pool(Transform root, int normal, bool hasSoul)
        {
            // Counts remain exact in labels; render at most twelve crystals per
            // pool so large late-game reserves do not create unlimited meshes.
            int count = Math.Min(12, normal);
            for (int index = 0; index < count; index++)
            {
                var core = Part(root, "Core", PrimitiveType.Sphere,
                    new Vector3((index % 4 - 1.5f) * 0.45f, 0.27f, (index / 4 - 1) * 0.45f), new Vector3(0.32f, 0.48f, 0.32f), crystal);
                core.transform.localRotation = Quaternion.Euler(0, 45, 15); poolCrystals.Add(core);
            }
            if (hasSoul) poolCrystals.Add(Part(root, "SoulCore", PrimitiveType.Sphere, new Vector3(0, 0.7f, 0), new Vector3(0.48f, 0.68f, 0.48f), soul));
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
            if (stage != null) Destroy(stage);
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            foreach (var material in materials) if (material != null) Destroy(material);
            meshes.Clear(); materials.Clear(); units.Clear(); poolCrystals.Clear(); expired.Clear();
            for (int side = 0; side < 2; side++)
            { mates[side] = null; mateAnchors[side] = null; for (int zone = 0; zone < 4; zone++) { counts[side, zone] = null; poolRoots[side, zone] = null; } }
            texture = null; camera = null; stage = null; poolSignature = null;
        }
    }
}
