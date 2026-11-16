using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MDPro3.BattleSpirits.Visuals
{
    /// <summary>Native Unity presentation of the reviewed square Battle Spirits table.
    /// The host supplies committed state and owns every gameplay decision.</summary>
    [DisallowMultipleComponent]
    public sealed class BattleFieldRenderer : MonoBehaviour, IDisposable
    {
        private const int PresentationLayer = 30;
        private const float BossDuration = 4.2f;
        private readonly Dictionary<int, BattlePaperActor> actors = new Dictionary<int, BattlePaperActor>();
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly List<ZoneLabel> labels = new List<ZoneLabel>();
        private readonly List<PlayerVisual> players = new List<PlayerVisual>();
        private readonly Queue<string> committedOrder = new Queue<string>();
        private readonly HashSet<string> committed = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<int, string> pendingSummons = new Dictionary<int, string>();
        private readonly Queue<int> bossQueue = new Queue<int>();
        private readonly List<Transform> bossRings = new List<Transform>();
        private readonly List<Material> bossRingMaterials = new List<Material>();
        private RawImage target;
        private Texture previousTexture;
        private bool previousRaycast;
        private BattleFieldPointer pointer;
        private Camera renderCamera;
        private RenderTexture renderTexture;
        private GameObject world;
        private Transform field;
        private Transform bossStage;
        private GameObject overlay;
        private GameObject bossControls;
        private Text bossTitle;
        private Text bossBp;
        private BattlePaperActor bossActor;
        private BattleTextureCache textures;
        private Shader surfaceShader;
        private Shader fieldShader;
        private Shader projectionShader;
        private Mesh paperMesh;
        private Mesh moteMesh;
        private Material coreMaterial;
        private Material soulMaterial;
        private Material backMaterial;
        private BattlePresentationState state = new BattlePresentationState();
        private bool initialized;
        private bool disposed;
        private bool reducedMotion;
        private bool muted = true;
        private bool focused = true;
        private bool applicationPaused;
        private bool dirty = true;
        private bool wasVisible;
        private float bossAge;

        public event Action<int> CardSelected;
        public event Action<bool> PresentationBusyChanged;
        public bool IsPresentationBusy { get; private set; }
        internal Material CoreMaterial => coreMaterial;
        internal Material SoulMaterial => soulMaterial;
        internal Mesh MoteMesh => moteMesh;

        public void Setup(RawImage image)
        {
            if (initialized)
            {
                if (target == image) return;
                throw new InvalidOperationException("A Battle Spirits field is already attached.");
            }
            if (disposed) throw new ObjectDisposedException(nameof(BattleFieldRenderer));
            if (image == null) throw new ArgumentNullException(nameof(image));
            target = image;
            previousTexture = image.texture;
            previousRaycast = image.raycastTarget;
            surfaceShader = LoadShader("BattleSurface", "MDPro3/BattleSpirits/Surface");
            fieldShader = LoadShader("BattleField", "MDPro3/BattleSpirits/Field");
            projectionShader = LoadShader("BattleProjection", "MDPro3/BattleSpirits/Projection");
            textures = new BattleTextureCache(this);
            paperMesh = Own(BattleGeometry.Quad());
            moteMesh = Own(BattleGeometry.Motes());
            world = NewObject("Battle Spirits native presentation", null);
            // Isolate this off-screen stage from the retained native UI cameras.
            world.transform.position = new Vector3(0, -10000, 0);
            field = NewObject("Mirrored square table", world.transform).transform;
            bossStage = NewObject("Committed boss presentation", world.transform).transform;
            BuildBossBackdrop();
            bossStage.gameObject.SetActive(false);
            var cameraObject = NewObject("Battle Spirits render camera", world.transform);
            renderCamera = cameraObject.AddComponent<Camera>();
            renderCamera.enabled = false;
            renderCamera.cullingMask = 1 << PresentationLayer;
            renderCamera.clearFlags = CameraClearFlags.SolidColor;
            renderCamera.backgroundColor = new Color(.018f, .035f, .06f, 1);
            renderCamera.orthographic = true;
            renderCamera.nearClipPlane = .05f;
            renderCamera.farClipPlane = 75;
            renderCamera.allowHDR = false;
            renderCamera.allowMSAA = true;
            renderCamera.useOcclusionCulling = false;
            renderCamera.depth = -100;
            coreMaterial = Own(SurfaceMaterial());
            soulMaterial = Own(SurfaceMaterial());
            backMaterial = Own(SurfaceMaterial());
            LoadCosmetic("core.png", coreMaterial);
            LoadCosmetic("soul-core.png", soulMaterial);
            LoadCosmetic("card-back.png", backMaterial);
            BuildTable();
            BuildOverlay();
            players.Add(new PlayerVisual(this, field, 0));
            players.Add(new PlayerVisual(this, field, 1));
            pointer = target.gameObject.AddComponent<BattleFieldPointer>();
            pointer.Clicked += OnFieldPointer;
            target.raycastTarget = true;
            initialized = true;
            RefreshSize();
            ApplyState();
        }

        public void SetState(BattlePresentationState snapshot)
        {
            if (disposed) return;
            // Copy the incoming DTOs, so a subsequently edited snapshot cannot
            // race an in-flight artwork load or committed summon presentation.
            var copy = new BattlePresentationState();
            if (snapshot != null)
            {
                copy.viewer = snapshot.viewer;
                copy.currentPlayer = snapshot.currentPlayer;
                var cards = new List<BattleCardPresentation>();
                foreach (var card in snapshot.cards ?? Array.Empty<BattleCardPresentation>())
                    if (card != null) cards.Add(BattlePaperActor.Copy(card));
                copy.cards = cards.ToArray();
                var people = new List<BattlePlayerPresentation>();
                foreach (var player in snapshot.players ?? Array.Empty<BattlePlayerPresentation>())
                    if (player != null) people.Add(new BattlePlayerPresentation
                    {
                        playerIndex = player.playerIndex, life = player.life, reserveNormal = player.reserveNormal,
                        trashNormal = player.trashNormal, deckCount = player.deckCount,
                        reserveSoul = player.reserveSoul, trashSoul = player.trashSoul
                    });
                copy.players = people.ToArray();
            }
            state = copy;
            if (initialized) ApplyState();
        }

        public void SetPreferences(bool reduced, bool muted)
        {
            reducedMotion = reduced;
            this.muted = muted;
            // Sound is intentionally silent: the reviewed sound is not a
            // licensed runtime asset. The explicit mute preference is retained.
            if (reduced && IsPresentationBusy) SkipPresentation();
            Invalidate();
        }

        /// <summary>Called only after the match engine has committed a summon.</summary>
        public void NotifyCommittedSummon(int instanceId, string eventId = null)
        {
            if (disposed) return;
            var key = string.IsNullOrEmpty(eventId) ? "instance:" + instanceId : eventId;
            if (!committed.Add(key)) return;
            committedOrder.Enqueue(key);
            while (committedOrder.Count > 128) committed.Remove(committedOrder.Dequeue());
            if (!actors.TryGetValue(instanceId, out var actor))
            {
                if (pendingSummons.Count < 32) pendingSummons[instanceId] = key;
                return;
            }
            PresentCommitted(actor);
        }

        public void SkipPresentation()
        {
            if (!IsPresentationBusy) return;
            FinishBoss();
        }

        /// <summary>Normalized field-relative bounds, with a bottom-left origin.</summary>
        public bool TryGetCardViewport(int instanceId, out Rect viewport)
        {
            viewport = default;
            if (!initialized || disposed || IsPresentationBusy || renderCamera == null || !actors.TryGetValue(instanceId, out var actor)) return false;
            var center = actor.LabelPosition;
            var point = renderCamera.WorldToViewportPoint(center);
            if (point.z <= 0) return false;
            var edge = renderCamera.WorldToViewportPoint(center + renderCamera.transform.right * .68f * actor.Root.transform.localScale.x);
            var width = Mathf.Max(.025f, Mathf.Abs(edge.x - point.x) * 2);
            viewport = new Rect(point.x - width * .5f, point.y - .015f, width, .03f);
            return true;
        }

        private void ApplyState()
        {
            var wanted = new HashSet<int>();
            foreach (var data in state.cards)
            {
                if (string.IsNullOrEmpty(data.cardId) || !BattleCatalog.TryGet(data.cardId, out var _) || !wanted.Add(data.instanceId)) continue;
                if (actors.TryGetValue(data.instanceId, out var current) && current.Data.cardId != data.cardId)
                {
                    current.Dispose(); actors.Remove(data.instanceId); current = null;
                }
                if (current == null) { current = new BattlePaperActor(this, field, data); actors.Add(data.instanceId, current); }
                else current.Apply(data);
            }
            var removed = new List<int>();
            foreach (var pair in actors) if (!wanted.Contains(pair.Key)) removed.Add(pair.Key);
            foreach (var id in removed) { actors[id].Dispose(); actors.Remove(id); pendingSummons.Remove(id); }
            for (var seat = 0; seat < 2; seat++)
            {
                LayoutCards(seat, false);
                LayoutCards(seat, true);
            }
            foreach (var player in players) player.Apply(state.players, state.viewer);
            var ready = new List<int>();
            foreach (var pair in pendingSummons) if (actors.ContainsKey(pair.Key)) ready.Add(pair.Key);
            foreach (var id in ready) { pendingSummons.Remove(id); PresentCommitted(actors[id]); }
            if (bossActor != null && !actors.ContainsKey(bossActor.Data.instanceId)) FinishBoss();
            Invalidate();
        }

        private void LayoutCards(int owner, bool nexus)
        {
            var group = new List<BattlePaperActor>();
            foreach (var actor in actors.Values)
                if (actor.Data.owner == owner && BattleCatalog.TryGet(actor.Data.cardId, out var definition) && definition.IsNexus == nexus)
                    group.Add(actor);
            group.Sort((a, b) => a.Data.instanceId.CompareTo(b.Data.instanceId));
            if (group.Count == 0) return;
            // Layout grows to arbitrary field population; no five-slot rule.
            var columns = nexus ? group.Count : Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(group.Count * 2.1f)));
            var rows = Mathf.CeilToInt((float)group.Count / columns);
            var cellWidth = 8.05f / columns;
            var cellDepth = (nexus ? 1.35f : 3.1f) / rows;
            var tileScale = Mathf.Min(1, cellWidth / 1.42f, cellDepth / 1.9f);
            var opponent = owner != state.viewer;
            for (var i = 0; i < group.Count; i++)
            {
                var row = i / columns;
                var rowCount = Math.Min(columns, group.Count - row * columns);
                var x = ((i % columns) - (rowCount - 1) * .5f) * cellWidth;
                var z = nexus ? 4.75f : .45f + cellDepth * (row + .5f);
                if (opponent) { x = -x; z = -z; }
                group[i].Layout(new Vector3(x, .05f, z), tileScale, cellWidth * .94f, opponent);
            }
        }

        private void PresentCommitted(BattlePaperActor actor)
        {
            actor.Reveal(reducedMotion);
            Invalidate();
            if (!reducedMotion && actor.HasCharacter && actor.Data.cardId == "26RBS03-X01" && BattleCatalog.IsBoss(actor.Data.cardId))
            {
                bossQueue.Enqueue(actor.Data.instanceId);
                StartNextBoss();
            }
        }

        private bool StartNextBoss(bool continuing = false)
        {
            if ((!continuing && IsPresentationBusy) || reducedMotion || disposed) return false;
            while (bossQueue.Count > 0)
            {
                var id = bossQueue.Dequeue();
                if (!actors.TryGetValue(id, out var actor)) continue;
                bossActor = new BattlePaperActor(this, bossStage, actor.Data);
                bossActor.Layout(Vector3.zero, 1.7f, 5.5f, false);
                bossActor.Reveal(false);
                bossAge = 0;
                field.gameObject.SetActive(false);
                bossStage.gameObject.SetActive(true);
                bossControls.SetActive(true);
                foreach (var label in labels) label.text.gameObject.SetActive(false);
                bossTitle.text = BattleCatalog.TryGet(actor.Data.cardId, out var definition) ? definition.DisplayName : "Spirit summon";
                bossBp.text = "Lv " + actor.Data.level + "  /  " + actor.Data.bp.ToString("N0") + " BP";
                TickBossBackdrop();
                SetBusy(true);
                UpdateCamera();
                Invalidate();
                return true;
            }
            return false;
        }

        private void FinishBoss()
        {
            bossActor?.Dispose(); bossActor = null;
            // Consecutive committed presentations retain the busy state, so a
            // host cannot accidentally advance AI between two queued bosses.
            if (!reducedMotion && StartNextBoss(true)) return;
            bossStage?.gameObject.SetActive(false);
            field?.gameObject.SetActive(true);
            if (bossControls != null) bossControls.SetActive(false);
            foreach (var label in labels) if (label.text != null) label.text.gameObject.SetActive(true);
            SetBusy(false);
            UpdateCamera();
            Invalidate();
            if (reducedMotion) bossQueue.Clear();
        }

        private void SetBusy(bool value)
        {
            if (IsPresentationBusy == value) return;
            IsPresentationBusy = value;
            PresentationBusyChanged?.Invoke(value);
        }

        private void LateUpdate()
        {
            if (!initialized || disposed) return;
            var visible = focused && !applicationPaused && IsTargetVisible();
            textures.Paused = !visible;
            renderCamera.enabled = false;
            if (!visible) { wasVisible = false; return; }
            if (!wasVisible) { dirty = true; wasVisible = true; }
            RefreshSize();
            var delta = Mathf.Clamp(Time.unscaledDeltaTime, 0, .1f);
            if (IsPresentationBusy)
            {
                if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                    SkipPresentation();
                else
                {
                    bossAge += delta;
                    if (bossAge >= BossDuration) FinishBoss();
                }
            }
            var animated = !reducedMotion && (IsPresentationBusy || HasHolograms());
            if (!dirty && !animated) return;
            if (IsPresentationBusy)
            {
                bossActor?.Tick(renderCamera, delta, reducedMotion);
                TickBossBackdrop();
            }
            else foreach (var actor in actors.Values) actor.Tick(renderCamera, delta, reducedMotion);
            UpdateLabels();
            renderCamera.enabled = true;
            dirty = false;
        }

        private bool HasHolograms()
        {
            foreach (var actor in actors.Values) if (actor.HasCharacter) return true;
            return false;
        }

        private bool IsTargetVisible()
        {
            if (target == null || !target.isActiveAndEnabled || !target.gameObject.activeInHierarchy || target.color.a <= .001f) return false;
            if (target.canvas != null && !target.canvas.isActiveAndEnabled) return false;
            for (var parent = target.transform; parent != null; parent = parent.parent)
            {
                var group = parent.GetComponent<CanvasGroup>();
                if (group == null) continue;
                if (group.alpha <= .001f) return false;
                if (group.ignoreParentGroups) break;
            }
            return true;
        }

        private void OnApplicationFocus(bool value) { focused = value; if (value) Invalidate(); }
        private void OnApplicationPause(bool value) { applicationPaused = value; if (!value) Invalidate(); }
        private void OnEnable() { Invalidate(); }
        private void OnDisable()
        {
            if (renderCamera != null) renderCamera.enabled = false;
            if (textures != null) textures.Paused = true;
            wasVisible = false;
        }
        private void OnDestroy() { Dispose(); }

        private void RefreshSize()
        {
            var rect = target.rectTransform.rect;
            var scaleFactor = target.canvas != null ? target.canvas.scaleFactor : 1;
            var width = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(rect.width) * scaleFactor), 256, 1920);
            var height = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(rect.height) * scaleFactor), 256, 1920);
            // Keep a strict pixel budget on narrow / high-density displays.
            var factor = Mathf.Min(1, Mathf.Sqrt(2073600f / (width * (float)height)));
            width = Mathf.Max(1, Mathf.RoundToInt(width * factor));
            height = Mathf.Max(1, Mathf.RoundToInt(height * factor));
            if (renderTexture != null && renderTexture.width == width && renderTexture.height == height) return;
            if (renderTexture != null) { renderCamera.targetTexture = null; renderTexture.Release(); DestroyOwned(renderTexture); }
            renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Battle Spirits field", antiAliasing = 2, useMipMap = false, autoGenerateMips = false
            };
            renderTexture.Create();
            renderCamera.targetTexture = renderTexture;
            target.texture = renderTexture;
            renderCamera.aspect = (float)width / height;
            UpdateCamera();
            Invalidate();
        }

        private void UpdateCamera()
        {
            if (renderCamera == null || world == null) return;
            if (IsPresentationBusy)
            {
                renderCamera.transform.localPosition = new Vector3(0, 2.6f, 9);
                renderCamera.transform.LookAt(world.transform.TransformPoint(new Vector3(0, 1.9f, 0)));
                renderCamera.orthographicSize = Mathf.Max(3.2f, 3.25f / renderCamera.aspect);
            }
            else
            {
                renderCamera.transform.localPosition = new Vector3(0, 16.5f, 16.8f);
                renderCamera.transform.LookAt(world.transform.position);
                renderCamera.orthographicSize = Mathf.Max(7.2f, 8.05f / renderCamera.aspect);
            }
        }

        private void OnFieldPointer(PointerEventData data)
        {
            if (IsPresentationBusy || disposed || target == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(target.rectTransform, data.position, data.pressEventCamera, out var p)) return;
            var rect = target.rectTransform.rect;
            var point = new Vector2((p.x - rect.xMin) / rect.width, (p.y - rect.yMin) / rect.height);
            var nearest = 0f;
            var best = 0;
            var found = false;
            foreach (var actor in actors.Values)
            {
                var center = renderCamera.WorldToViewportPoint(actor.Root.transform.position + Vector3.up * .2f);
                var d = Vector2.Distance(point, new Vector2(center.x, center.y));
                var threshold = Mathf.Max(.025f, .085f * actor.Root.transform.localScale.x);
                if (d <= threshold && (!found || d < nearest)) { found = true; nearest = d; best = actor.Data.instanceId; }
            }
            if (!found) return;
            CardSelected?.Invoke(best);
            Invalidate();
        }

        private void BuildTable()
        {
            var navy = Own(FieldMaterial(new Color(.012f, .028f, .047f)));
            Plane("Square navy plinth", field, navy, new Vector3(0, -.055f, 0), new Vector2(15.05f, 15.05f), true);
            var table = Own(FieldMaterial(Color.white)); table.SetFloat("_Detail", 1);
            Plane("Woven navy square table", field, table, Vector3.zero, new Vector2(14.8f, 14.8f), true);
            var silver = Own(FieldMaterial(new Color(.56f, .69f, .77f)));
            var lightSilver = Own(FieldMaterial(new Color(.78f, .87f, .91f)));
            var dimSilver = Own(FieldMaterial(new Color(.21f, .34f, .42f)));
            AddFrame(field, 14.42f, 14.42f, .028f, .32f, silver, new Vector3(0, .006f, 0));
            AddFrame(field, 14.24f, 14.24f, .035f, .27f, lightSilver, new Vector3(0, .007f, 0));
            AddFrame(field, 14.01f, 14.01f, .018f, .23f, silver, new Vector3(0, .008f, 0));
            for (var i = 0; i < 22; i++)
            {
                var x = Mathf.Lerp(-6.65f, 6.65f, i / 21f);
                foreach (var z in new[] { -7.12f, 7.12f })
                    AddFrame(field, .34f, .14f, .012f, .065f, silver, new Vector3(x, .009f, z));
            }
            foreach (var sign in new[] { 1, -1 })
            {
                var side = NewObject("Mirrored resource zones", field).transform;
                side.localRotation = Quaternion.Euler(0, sign < 0 ? 180 : 0, 0);
                Zone(side, -5.7f, 3.6f, 2.1f, 2.05f, "LIFE", dimSilver);
                Zone(side, -5.7f, 5.95f, 2.1f, 1.9f, "RESERVE", dimSilver);
                Zone(side, 5.7f, 3.6f, 2.1f, 2.05f, "DECK", dimSilver);
                Zone(side, 5.7f, 5.95f, 2.1f, 1.9f, "TRASH", dimSilver);
                Zone(side, 0, 4.75f, 8.25f, 1.55f, "NEXUS", dimSilver);
                for (var i = 0; i < 20; i++)
                {
                    var x = Mathf.Lerp(-4.04f, 4.04f, i / 19f);
                    foreach (var z in new[] { .05f, 3.55f })
                        Plane("Open Spirit field boundary", side, dimSilver, new Vector3(x, .009f, z), new Vector2(.23f, .018f), true);
                }
                for (var i = 0; i < 9; i++)
                    foreach (var x in new[] { -4.1f, 4.1f })
                        Plane("Open Spirit field boundary", side, dimSilver, new Vector3(x, .009f, .2f + i * .4f), new Vector2(.018f, .21f), true);
            }
            Plane("Central silver line", field, dimSilver, new Vector3(0, .009f, 0), new Vector2(13.6f, .025f), true);
        }

        private void BuildBossBackdrop()
        {
            for (var i = 0; i < 6; i++)
            {
                var material = Own(ProjectionMaterial(new Color(.93f, .54f, .42f, .18f), 0));
                bossRingMaterials.Add(material);
                bossRings.Add(Plane("Warm converging boss light", bossStage, material,
                    new Vector3(0, 1.9f, -.15f - i * .01f), Vector2.one, false).transform);
            }
        }

        private void TickBossBackdrop()
        {
            var arrival = Mathf.Clamp01(bossAge / .85f);
            arrival = arrival * arrival * (3 - 2 * arrival);
            for (var i = 0; i < bossRings.Count; i++)
            {
                var diameter = Mathf.Lerp(7.2f + i * .25f, 3.5f + i * .48f, arrival);
                bossRings[i].localScale = new Vector3(diameter, diameter, 1);
                bossRingMaterials[i].SetFloat("_Ready", Mathf.Clamp01(bossAge / .22f));
                bossRingMaterials[i].SetFloat("_Clock", bossAge * .6f);
            }
            var titleAlpha = Mathf.InverseLerp(1.55f, 2.05f, bossAge);
            bossTitle.color = new Color(.88f, .93f, .96f, titleAlpha);
            bossBp.color = new Color(.82f, .88f, .93f, titleAlpha);
        }

        private void Zone(Transform parent, float x, float z, float width, float depth, string label, Material border)
        {
            var fill = Own(FieldMaterial(new Color(.012f, .031f, .052f)));
            Plane(label + " zone", parent, fill, new Vector3(x, .004f, z), new Vector2(width, depth), true);
            AddFrame(parent, width, depth, .018f, .085f, border, new Vector3(x, .009f, z));
        }

        private void AddFrame(Transform parent, float width, float depth, float thickness, float radius, Material material, Vector3 position)
        {
            var obj = MeshObject("Silver table inlay", parent, Own(BattleGeometry.Frame(width, depth, thickness, radius)), material);
            obj.transform.localPosition = position;
        }

        private void BuildOverlay()
        {
            overlay = NewUI("Battle Spirits field labels", target.transform);
            Stretch(overlay.GetComponent<RectTransform>());
            foreach (var sign in new[] { 1, -1 })
            {
                ZoneText("LIFE", new Vector3(-5.7f * sign, .02f, 2.83f * sign));
                ZoneText("RESERVE", new Vector3(-5.7f * sign, .02f, 5.23f * sign));
                ZoneText("DECK", new Vector3(5.7f * sign, .02f, 2.83f * sign));
                ZoneText("TRASH", new Vector3(5.7f * sign, .02f, 5.23f * sign));
                ZoneText("NEXUS", new Vector3(0, .02f, 4.16f * sign));
                ZoneText("SPIRIT FIELD", new Vector3(0, .02f, 3.32f * sign), 10);
            }
            ZoneText("Battle Spirits", new Vector3(0, .02f, 0), 18);
            bossControls = NewUI("Boss presentation controls", overlay.transform);
            Stretch(bossControls.GetComponent<RectTransform>());
            bossTitle = MakeText("Committed Spirit name", bossControls.transform, 24);
            HorizontalText(bossTitle.rectTransform, .16f, 38);
            bossBp = MakeText("Committed Spirit level and BP", bossControls.transform, 16);
            HorizontalText(bossBp.rectTransform, .10f, 30);
            var buttonObject = NewUI("Skip presentation", bossControls.transform);
            var plate = buttonObject.AddComponent<Image>(); plate.color = new Color(.035f, .09f, .14f, .96f);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = plate;
            button.onClick.AddListener(SkipPresentation);
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            Anchor(buttonRect, Vector2.one, new Vector2(125, 34));
            buttonRect.pivot = Vector2.one;
            buttonRect.anchoredPosition = new Vector2(-12, -12);
            var caption = MakeText("Skip caption", buttonObject.transform, 15); caption.text = "Skip / Escape";
            Stretch(caption.rectTransform);
            bossControls.SetActive(false);
        }

        private void ZoneText(string text, Vector3 position, int size = 12)
        {
            var label = MakeText(text, overlay.transform, size);
            label.text = text;
            label.color = new Color(.68f, .8f, .87f, size == 10 ? .5f : .88f);
            labels.Add(new ZoneLabel { text = label, position = position });
            Anchor(label.rectTransform, Vector2.zero, new Vector2(size == 18 ? 220 : 140, size + 10));
        }

        private void UpdateLabels()
        {
            if (IsPresentationBusy) return;
            foreach (var label in labels)
            {
                var p = renderCamera.WorldToViewportPoint(field.TransformPoint(label.position));
                label.text.rectTransform.anchorMin = label.text.rectTransform.anchorMax = new Vector2(p.x, p.y);
                label.text.rectTransform.anchoredPosition = Vector2.zero;
            }
        }

        private void LoadCosmetic(string file, Material material)
        {
            var path = Path.Combine(Application.streamingAssetsPath, "BattleSpirits", "cosmetics", file);
            material.SetFloat("_Opacity", 0);
            AcquireTexture(path, texture =>
            {
                if (disposed || texture == null) return;
                material.mainTexture = texture;
                material.SetFloat("_Opacity", 1);
                Invalidate();
            });
        }

        internal void AcquireTexture(string path, Action<Texture2D> completed) => textures.Acquire(path, completed);
        internal void ReleaseTexture(string path) => textures?.Release(path);
        internal void Invalidate() { dirty = true; }
        internal Material FieldMaterial(Color color) { var m = new Material(fieldShader); m.SetColor("_BaseColor", color); return m; }
        internal Material SurfaceMaterial() { return new Material(surfaceShader); }
        internal Material ProjectionMaterial(Color color, int kind)
        {
            var m = new Material(projectionShader); m.SetColor("_Accent", color); m.SetFloat("_Kind", kind); return m;
        }

        internal GameObject NewObject(string objectName, Transform parent)
        {
            var obj = new GameObject(objectName) { layer = PresentationLayer };
            obj.transform.SetParent(parent, false);
            return obj;
        }

        internal GameObject MeshObject(string objectName, Transform parent, Mesh mesh, Material material)
        {
            var obj = NewObject(objectName, parent);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return obj;
        }

        internal GameObject Plane(string objectName, Transform parent, Material material, Vector3 position, Vector2 size, bool flat)
        {
            var obj = MeshObject(objectName, parent, paperMesh, material);
            obj.transform.localPosition = position;
            obj.transform.localScale = new Vector3(size.x, size.y, 1);
            if (flat) obj.transform.localRotation = Quaternion.Euler(90, 0, 0);
            return obj;
        }

        private T Own<T>(T value) where T : UnityEngine.Object { owned.Add(value); return value; }
        internal void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value);
        }

        private static Shader LoadShader(string resource, string shaderName)
        {
            var shader = Resources.Load<Shader>("BattleSpirits/" + resource) ?? Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Battle Spirits shader is unavailable: " + resource);
            return shader;
        }

        private static GameObject NewUI(string objectName, Transform parent)
        {
            var obj = new GameObject(objectName, typeof(RectTransform)); obj.transform.SetParent(parent, false); return obj;
        }

        private static Text MakeText(string objectName, Transform parent, int size)
        {
            var obj = NewUI(objectName, parent);
            var text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(.84f, .91f, .95f);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static void Anchor(RectTransform rect, Vector2 point, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = point; rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = size;
        }
        private static void HorizontalText(RectTransform rect, float height, float size)
        {
            rect.anchorMin = new Vector2(.04f, height); rect.anchorMax = new Vector2(.96f, height);
            rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(0, size);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            SetBusy(false);
            if (pointer != null) { pointer.Clicked -= OnFieldPointer; DestroyOwned(pointer); }
            if (renderCamera != null) { renderCamera.enabled = false; renderCamera.targetTexture = null; }
            foreach (var actor in actors.Values) actor.Dispose(); actors.Clear();
            bossActor?.Dispose(); bossActor = null;
            textures?.Dispose(); textures = null;
            if (target != null) { target.texture = previousTexture; target.raycastTarget = previousRaycast; }
            if (renderTexture != null) { renderTexture.Release(); DestroyOwned(renderTexture); renderTexture = null; }
            DestroyOwned(overlay); DestroyOwned(world);
            foreach (var value in owned) DestroyOwned(value); owned.Clear();
            labels.Clear(); players.Clear(); bossRings.Clear(); bossRingMaterials.Clear();
            pendingSummons.Clear(); bossQueue.Clear(); committed.Clear(); committedOrder.Clear();
            CardSelected = null; PresentationBusyChanged = null;
        }

        private sealed class ZoneLabel { internal Text text; internal Vector3 position; }

        private sealed class PlayerVisual
        {
            private readonly int seat;
            private readonly Transform root;
            private readonly GameObject[] life;
            private readonly GameObject[] reserve;
            private readonly GameObject[] trash;
            private readonly GameObject reserveSoul;
            private readonly GameObject trashSoul;
            private readonly GameObject deck;
            internal PlayerVisual(BattleFieldRenderer host, Transform parent, int seat)
            {
                this.seat = seat;
                root = host.NewObject("Player " + seat + " resources", parent).transform;
                life = Icons(host, root, "Life Core", 18, -6.25f, 3.35f, host.coreMaterial, 4, .36f);
                reserve = Icons(host, root, "Reserve Core", 12, -6.23f, 5.7f, host.coreMaterial, 4, .36f);
                trash = Icons(host, root, "Trash Core", 12, 5.17f, 5.7f, host.coreMaterial, 4, .36f);
                reserveSoul = host.Plane("Reserve Soul Core", root, host.soulMaterial, new Vector3(-5.05f, .05f, 6.4f), new Vector2(.39f, .39f), true);
                trashSoul = host.Plane("Trash Soul Core", root, host.soulMaterial, new Vector3(6.35f, .05f, 6.4f), new Vector2(.39f, .39f), true);
                deck = host.Plane("Battle Spirits deck", root, host.backMaterial, new Vector3(5.7f, .08f, 3.7f), new Vector2(.9f, 1.31f), true);
            }
            internal void Apply(BattlePlayerPresentation[] people, int viewer)
            {
                BattlePlayerPresentation data = null;
                foreach (var value in people) if (value.playerIndex == seat) { data = value; break; }
                root.localRotation = Quaternion.Euler(0, seat == viewer ? 0 : 180, 0);
                Count(life, data?.life ?? 0); Count(reserve, data?.reserveNormal ?? 0); Count(trash, data?.trashNormal ?? 0);
                reserveSoul.SetActive(data?.reserveSoul == true); trashSoul.SetActive(data?.trashSoul == true);
                deck.SetActive(data != null && data.deckCount > 0);
            }
            private static GameObject[] Icons(BattleFieldRenderer host, Transform root, string name, int count, float x, float z, Material material, int columns, float spacing)
            {
                var result = new GameObject[count];
                for (var i = 0; i < count; i++) result[i] = host.Plane(name, root, material,
                    new Vector3(x + (i % columns) * spacing, .04f + i * .0003f, z + (i / columns) * .27f), new Vector2(.29f, .29f), true);
                return result;
            }
            private static void Count(GameObject[] objects, int count)
            {
                for (var i = 0; i < objects.Length; i++) objects[i].SetActive(i < count);
            }
        }
    }

    /// <summary>Pointer adapter on the retained native RawImage.</summary>
    internal sealed class BattleFieldPointer : MonoBehaviour, IPointerClickHandler
    {
        internal event Action<PointerEventData> Clicked;
        public void OnPointerClick(PointerEventData eventData) { Clicked?.Invoke(eventData); }
    }
}
