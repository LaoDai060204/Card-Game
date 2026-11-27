using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MDPro3.BattleSpirits.Visuals
{
    /// <summary>Native Unity presentation of the approved Battle Spirits arena.
    /// The host supplies committed state and owns every gameplay decision.</summary>
    [DisallowMultipleComponent]
    public sealed class BattleFieldRenderer : MonoBehaviour, IDisposable
    {
        private const int PresentationLayer = 30;
        internal const float TableWidth = 15.6f;
        internal const float TableDepth = 15.2f;
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
        private readonly Vector3[] screenCorners = new Vector3[4];
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
        private Material playmatMaterial;
        private Material bossBackdropMaterial;
        private string playmatCode;
        private string playmatPath;
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
            field = NewObject("Mirrored navy and silver table", world.transform).transform;
            bossStage = NewObject("Committed boss presentation", world.transform).transform;
            BuildBossBackdrop();
            bossStage.gameObject.SetActive(false);
            var cameraObject = NewObject("Battle Spirits render camera", world.transform);
            renderCamera = cameraObject.AddComponent<Camera>();
            renderCamera.enabled = false;
            renderCamera.cullingMask = 1 << PresentationLayer;
            renderCamera.clearFlags = CameraClearFlags.SolidColor;
            renderCamera.backgroundColor = new Color(.018f, .035f, .06f, 1);
            renderCamera.orthographic = false;
            renderCamera.fieldOfView = 42;
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
            RefreshSelectedPlaymat();
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
                        trashCardCount = player.trashCardCount, burstCount = player.burstCount,
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
            var columns = nexus ? group.Count : Math.Min(group.Count, Mathf.Max(5, Mathf.CeilToInt(Mathf.Sqrt(group.Count * 2.5f))));
            var rows = Mathf.CeilToInt((float)group.Count / columns);
            var cellWidth = Mathf.Min(1.8f, 9f / columns);
            var cellDepth = (nexus ? 1.55f : 4.25f) / rows;
            var tileScale = Mathf.Min(1, cellWidth / 1.62f, cellDepth / 2.15f);
            var opponent = owner != state.viewer;
            for (var i = 0; i < group.Count; i++)
            {
                var row = i / columns;
                var rowCount = Math.Min(columns, group.Count - row * columns);
                var x = ((i % columns) - (rowCount - 1) * .5f) * cellWidth;
                var z = nexus ? 5.8f : rows == 1 ? 2.9f : .55f + cellDepth * (row + .5f);
                if (opponent) { x = -x; z = -z; }
                group[i].Layout(new Vector3(x, .05f, z), tileScale, cellWidth * .94f, opponent);
            }
        }

        private void PresentCommitted(BattlePaperActor actor)
        {
            actor.Reveal(reducedMotion);
            Invalidate();
            if (!reducedMotion && actor.HasCharacter && BattleCatalog.IsBoss(actor.Data.cardId))
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
                var accent = BattlePaperActor.Accent(actor.Data.cardId);
                foreach (var material in bossRingMaterials) material.SetColor("_Accent", new Color(accent.r, accent.g, accent.b, .5f));
                bossBackdropMaterial.SetColor("_BaseColor", new Color(accent.r * .25f, accent.g * .13f, accent.b * .32f));
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
            RefreshSelectedPlaymat();
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
            var canvas = target.canvas != null ? target.canvas.rootCanvas : null;
            var scaleFactor = canvas != null ? canvas.scaleFactor : 1;
            var pixelsWide = Mathf.Abs(rect.width) * scaleFactor;
            var pixelsHigh = Mathf.Abs(rect.height) * scaleFactor;
            if (canvas != null)
            {
                // Rect dimensions alone miss the native servant's inherited
                // transform scale. Measure the viewport in actual screen pixels.
                target.rectTransform.GetWorldCorners(screenCorners);
                var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                var bottomLeft = RectTransformUtility.WorldToScreenPoint(uiCamera, screenCorners[0]);
                pixelsWide = Vector2.Distance(bottomLeft, RectTransformUtility.WorldToScreenPoint(uiCamera, screenCorners[3]));
                pixelsHigh = Vector2.Distance(bottomLeft, RectTransformUtility.WorldToScreenPoint(uiCamera, screenCorners[1]));
            }
            var width = Mathf.Clamp(Mathf.RoundToInt(pixelsWide), 256, 3840);
            var height = Mathf.Clamp(Mathf.RoundToInt(pixelsHigh), 256, 3840);
            // Native 4K landscape remains sharp, with a bounded 8.3 MP target.
            var factor = Mathf.Min(1, Mathf.Sqrt(8294400f / (width * (float)height)));
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
                renderCamera.orthographic = true;
                renderCamera.transform.localPosition = new Vector3(0, 2.6f, 9);
                renderCamera.transform.LookAt(world.transform.TransformPoint(new Vector3(0, 1.9f, 0)));
                renderCamera.orthographicSize = Mathf.Max(3.2f, 3.25f / renderCamera.aspect);
            }
            else
            {
                renderCamera.orthographic = false;
                renderCamera.fieldOfView = 42;
                var fit = Mathf.Max(1.25f, 2.1f / renderCamera.aspect);
                renderCamera.farClipPlane = Mathf.Max(75, 32 * fit);
                var lookAt = new Vector3(0, 0, 2.2f);
                renderCamera.transform.localPosition = lookAt + new Vector3(0, 11, 11) * fit;
                renderCamera.transform.LookAt(world.transform.TransformPoint(lookAt));
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
            Plane("Navy table plinth", field, navy, new Vector3(0, -.055f, 0), new Vector2(TableWidth + .22f, TableDepth + .22f), true);
            var table = Own(FieldMaterial(Color.white)); table.SetFloat("_Detail", 1);
            Plane("Woven navy arena", field, table, Vector3.zero, new Vector2(TableWidth, TableDepth), true);
            playmatMaterial = Own(SurfaceMaterial());
            playmatMaterial.SetColor("_BaseColor", new Color(.3f, .42f, .54f, 1));
            playmatMaterial.SetFloat("_Opacity", 0);
            // Crop only the central decorative artwork. The source play sheets'
            // zone labels and rules never replace the readable mirrored zones.
            foreach (var sign in new[] { 1, -1 })
            {
                var art = Plane("Selected Battle Spirits field artwork", field, playmatMaterial,
                    new Vector3(0, .001f, 3.55f * sign), new Vector2(13.3f, 6.9f), true);
                if (sign < 0) art.transform.localRotation = Quaternion.Euler(90, 0, 180);
            }
            var silver = Own(FieldMaterial(new Color(.72f, .79f, .88f))); silver.SetFloat("_Silver", 1);
            var lightSilver = Own(FieldMaterial(new Color(.87f, .92f, .98f))); lightSilver.SetFloat("_Silver", 1);
            var dimSilver = Own(FieldMaterial(new Color(.36f, .47f, .57f))); dimSilver.SetFloat("_Silver", .5f);
            AddFrame(field, TableWidth - .36f, TableDepth - .36f, .09f, .5f, silver, new Vector3(0, .006f, 0));
            AddFrame(field, TableWidth - .68f, TableDepth - .68f, .03f, .4f, lightSilver, new Vector3(0, .007f, 0));
            for (var i = 1; i < 13; i++)
            {
                var x = -TableWidth * .5f + TableWidth * i / 13f;
                foreach (var z in new[] { -7.34f, 7.34f })
                {
                    var diamond = Plane("Silver border diamond", field, silver, new Vector3(x, .009f, z), new Vector2(.1f, .1f), true);
                    diamond.transform.localRotation = Quaternion.Euler(90, 0, 45);
                }
            }
            foreach (var sign in new[] { 1, -1 })
            {
                var side = NewObject("Mirrored resource zones", field).transform;
                side.localRotation = Quaternion.Euler(0, sign < 0 ? 180 : 0, 0);
                Zone(side, -6.2f, 1.45f, 2.4f, 2.4f, "BURST", silver);
                Zone(side, 6.2f, 1.45f, 2.4f, 2.4f, "DECK", silver);
                Zone(side, -6.2f, 3.85f, 2.2f, 2f, "LIFE", silver);
                Zone(side, -6.2f, 6.2f, 2.2f, 2.2f, "RESERVE", silver);
                Zone(side, 6.2f, 4f, 2.4f, 2.2f, "TRASH CARD", silver);
                Zone(side, 6.2f, 6.25f, 2.4f, 2f, "TRASH CORE", silver);
                // One open field expands with the engine's population. The
                // reference's five decorative slots never become a card limit.
                for (var i = 0; i < 22; i++)
                {
                    var x = Mathf.Lerp(-4.48f, 4.48f, i / 21f);
                    foreach (var z in new[] { .38f, 4.85f })
                        Plane("Open Spirit field boundary", side, dimSilver, new Vector3(x, .009f, z), new Vector2(.23f, .018f), true);
                }
                for (var i = 0; i < 11; i++)
                    foreach (var x in new[] { -4.56f, 4.56f })
                        Plane("Open Spirit field boundary", side, dimSilver, new Vector3(x, .009f, .55f + i * .4f), new Vector2(.018f, .21f), true);
            }
            Plane("Central silver line", field, dimSilver, new Vector3(0, .009f, 0), new Vector2(14.4f, .04f), true);
            var emblem = NewObject("Central silver diamond", field).transform;
            emblem.localPosition = new Vector3(0, .012f, 0);
            emblem.localRotation = Quaternion.Euler(0, 45, 0);
            Plane("Diamond navy inset", emblem, navy, Vector3.zero, new Vector2(.6f, .6f), true);
            AddFrame(emblem, .6f, .6f, .04f, .025f, silver, new Vector3(0, .002f, 0));
        }

        private void BuildBossBackdrop()
        {
            bossBackdropMaterial = Own(FieldMaterial(new Color(.12f, .025f, .08f)));
            bossBackdropMaterial.SetFloat("_Backdrop", 1);
            var backdrop = Plane("Spirit-colored radial boss backdrop", bossStage, bossBackdropMaterial,
                new Vector3(0, 2, -2), new Vector2(30, 20), false);
            backdrop.transform.localRotation = Quaternion.Euler(0, 180, 0);
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
            // The approved cutscene arrives large, then settles on the real
            // silhouette. This remains separate from the committed field card.
            bossActor?.Layout(Vector3.zero, Mathf.Lerp(2.45f, 2f, arrival), 6, false);
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
            var fill = Own(FieldMaterial(new Color(.04f, .075f, .105f)));
            Plane(label + " zone", parent, fill, new Vector3(x, .004f, z), new Vector2(width, depth), true);
            AddFrame(parent, width, depth, .045f, .1f, border, new Vector3(x, .009f, z));
            AddFrame(parent, width - .2f, depth - .2f, .012f, .06f, border, new Vector3(x, .01f, z));
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
                ZoneText("BURST", new Vector3(-6.2f * sign, .02f, .6f * sign));
                ZoneText("DECK", new Vector3(6.2f * sign, .02f, .6f * sign));
                ZoneText("LIFE", new Vector3(-6.2f * sign, .02f, 2.85f * sign));
                ZoneText("RESERVE", new Vector3(-6.2f * sign, .02f, 5.1f * sign));
                ZoneText("TRASH · CARD", new Vector3(6.2f * sign, .02f, 2.9f * sign));
                ZoneText("TRASH · CORE", new Vector3(6.2f * sign, .02f, 5.25f * sign));
                ZoneText("FIELD", new Vector3(0, .02f, 6.7f * sign), 16);
            }
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

        private Text ZoneText(string text, Vector3 position, int size = 14, Transform relativeTo = null)
        {
            var label = MakeText(text, overlay.transform, size);
            label.text = text;
            label.color = new Color(.83f, .9f, .96f, size == 16 ? .35f : 1);
            labels.Add(new ZoneLabel { text = label, position = position, relativeTo = relativeTo });
            Anchor(label.rectTransform, Vector2.zero, new Vector2(150, size + 10));
            return label;
        }

        private void UpdateLabels()
        {
            if (IsPresentationBusy) return;
            foreach (var label in labels)
            {
                var p = renderCamera.WorldToViewportPoint((label.relativeTo ?? field).TransformPoint(label.position));
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

        private void RefreshSelectedPlaymat()
        {
            string code = Config.Get("DuelField0", Items.CODE_NONE.ToString());
            // Resolve Random only when the selection changes, not every frame.
            if (playmatCode == code) return;
            playmatCode = code;
            // Missing or retained legacy selections start on the approved navy
            // arena. Only an explicit Battle Spirits theme adds decorative art.
            bool selectedTheme = code == Items.CODE_RANDOM.ToString() || code == "1099001" || code == "1099002" || code == "1099003";
            string path = selectedTheme ? BattleCosmetics.GetPath(code, Items.ItemType.Mat) : string.Empty;
            if (string.Equals(playmatPath, path, StringComparison.Ordinal)) return;
            if (!string.IsNullOrEmpty(playmatPath)) ReleaseTexture(playmatPath);
            playmatPath = path;
            playmatMaterial.mainTexture = null;
            playmatMaterial.SetFloat("_Opacity", 0);
            Invalidate();
            if (string.IsNullOrEmpty(path)) return;
            playmatMaterial.SetVector("_UvRect", PlaymatArtBounds(path));
            AcquireTexture(path, texture =>
            {
                if (disposed || texture == null || !string.Equals(playmatPath, path, StringComparison.Ordinal)) return;
                playmatMaterial.mainTexture = texture;
                playmatMaterial.SetFloat("_Opacity", .12f);
                Invalidate();
            });
        }

        internal static Vector4 PlaymatArtBounds(string path)
        {
            switch (Path.GetFileName(path))
            {
                case "field.png": return new Vector4(.40f, .32f, .34f, .53f);
                case "play-sheet-white.png": return new Vector4(.25f, .30f, .35f, .55f);
                default: return new Vector4(.25f, .24f, .45f, .63f);
            }
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
            var obj = new GameObject(objectName, typeof(RectTransform)) { layer = parent.gameObject.layer };
            obj.transform.SetParent(parent, false);
            return obj;
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

        private sealed class ZoneLabel { internal Text text; internal Vector3 position; internal Transform relativeTo; }

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
            private readonly GameObject burst;
            private readonly Text lifeCount;
            private readonly Text reserveCount;
            private readonly Text trashCount;
            private readonly Text deckCount;
            private readonly Text trashCardCount;
            internal PlayerVisual(BattleFieldRenderer host, Transform parent, int seat)
            {
                this.seat = seat;
                root = host.NewObject("Player " + seat + " resources", parent).transform;
                life = Icons(host, root, "Life Core", 18, -6.9f, 3.45f, host.coreMaterial, 3, .7f, .8f, .62f);
                reserve = Icons(host, root, "Reserve Core", 32, -6.95f, 5.78f, host.coreMaterial, 4, .5f, .84f, .48f);
                trash = Icons(host, root, "Trash Core", 32, 5.375f, 5.83f, host.coreMaterial, 4, .55f, .84f, .48f);
                reserveSoul = host.Plane("Reserve Soul Core", root, host.soulMaterial, new Vector3(-6.2f, .16f, 6.2f), new Vector2(.56f, .56f), true);
                trashSoul = host.Plane("Trash Soul Core", root, host.soulMaterial, new Vector3(6.2f, .16f, 6.25f), new Vector2(.56f, .56f), true);
                deck = host.Plane("Battle Spirits deck", root, host.backMaterial, new Vector3(6.2f, .08f, 1.45f), new Vector2(1.45f, 2.03f), true);
                burst = host.Plane("Face-down Burst", root, host.backMaterial, new Vector3(-6.2f, .08f, 1.45f), new Vector2(1.45f, 2.03f), true);
                lifeCount = host.ZoneText("", new Vector3(-6.2f, .2f, 4.68f), 13, root);
                reserveCount = host.ZoneText("", new Vector3(-6.2f, .2f, 7.08f), 13, root);
                trashCount = host.ZoneText("", new Vector3(6.2f, .2f, 7.08f), 13, root);
                deckCount = host.ZoneText("", new Vector3(6.2f, .2f, 2.35f), 13, root);
                trashCardCount = host.ZoneText("", new Vector3(6.2f, .2f, 4f), 13, root);
            }
            internal void Apply(BattlePlayerPresentation[] people, int viewer)
            {
                BattlePlayerPresentation data = null;
                foreach (var value in people) if (value.playerIndex == seat) { data = value; break; }
                root.localRotation = Quaternion.Euler(0, seat == viewer ? 0 : 180, 0);
                Count(life, data?.life ?? 0); Count(reserve, data?.reserveNormal ?? 0); Count(trash, data?.trashNormal ?? 0);
                reserveSoul.SetActive(data?.reserveSoul == true); trashSoul.SetActive(data?.trashSoul == true);
                deck.SetActive(data != null && data.deckCount > 0);
                burst.SetActive(data != null && data.burstCount > 0);
                lifeCount.text = (data?.life ?? 0).ToString();
                reserveCount.text = CoreCount(data?.reserveNormal ?? 0, data?.reserveSoul == true);
                trashCount.text = CoreCount(data?.trashNormal ?? 0, data?.trashSoul == true);
                deckCount.text = (data?.deckCount ?? 0).ToString() + " cards";
                trashCardCount.text = (data?.trashCardCount ?? 0).ToString() + " cards";
            }
            private static string CoreCount(int normal, bool soul) => normal + " Core" + (soul ? " + Soul" : "");
            private static GameObject[] Icons(BattleFieldRenderer host, Transform root, string name, int count, float x, float z, Material material, int columns, float spacing, float rowSpacing, float size)
            {
                var result = new GameObject[count];
                for (var i = 0; i < count; i++) result[i] = host.Plane(name, root, material,
                    new Vector3(x + (i % columns) * spacing, .04f + (i / (columns * 2)) * .025f,
                        z + ((i / columns) % 2) * rowSpacing), new Vector2(size, size), true);
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
