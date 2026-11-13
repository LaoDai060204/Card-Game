using System;
using System.Collections.Generic;
using LaoDai.BattleSpirits.Core;
using LaoDai.BattleSpirits.Networking;
using LaoDai.BattleSpirits.Visuals;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LaoDai.BattleSpirits.NativeUI
{
    /// <summary>
    /// An artwork-driven summon presentation inside the original duel UI root.
    /// It borrows card textures and does not own a Canvas, gameplay or network clock.
    /// </summary>
    public sealed class BattleSummonPresentation : MonoBehaviour, IDisposable
    {
        private const int MaximumPending = 4;
        private const float ArtworkWaitLimit = 1.5f;

        private sealed class Request
        {
            internal string key, id, name;
            internal int spirit, actor;
            internal bool nexus;
            internal Color accent;
        }

        private IBattleNativeSession session;
        private readonly Queue<Request> pending = new Queue<Request>();
        private readonly HashSet<string> scheduled = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, CardDefinition> catalog = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);
        private readonly List<Image> frame = new List<Image>();
        private readonly List<Image> streaks = new List<Image>();
        private RectTransform screen, cardSlot, illustrationSlot;
        private RawImage cardArt, illustrationArt, colorSymbol;
        private AspectRatioFitter cardAspect, illustrationAspect;
        private Image dim, flash, horizon;
        private TextMeshProUGUI heading, title, identity;
        private Request current;
        private float elapsed, artworkWait;
        private bool disposed;
        private bool modelRevealed;

        public bool IsPlaying => current != null;
        public int PendingCount => pending.Count;

        public void Configure(IBattleNativeSession source)
        {
            if (disposed) return;
            Clear(); session = source; catalog.Clear();
            if (source?.Cards != null)
                foreach (var card in source.Cards)
                    if (card != null && !string.IsNullOrWhiteSpace(card.id)) catalog[card.id] = card;
            if (screen == null) Build();
        }

        public void Enqueue(PublicBattleEvent item)
        {
            if (disposed || !gameObject.activeInHierarchy || session == null || screen == null || item == null
                || !(item.kind == "Summoned" || item.kind == "NexusPlaced")
                || string.IsNullOrWhiteSpace(item.cardId) || !catalog.TryGetValue(item.cardId, out var definition)) return;
            string key = item.kind + ":" + item.actor + ":" + item.spiritId + ":" + item.cardId;
            if (!scheduled.Add(key)) return;
            if (pending.Count == MaximumPending)
            {
                var dropped = pending.Dequeue(); scheduled.Remove(dropped.key);
            }
            pending.Enqueue(new Request {
                key = key, id = definition.id, name = definition.DisplayName,
                spirit = item.spiritId, actor = item.actor, nexus = item.kind == "NexusPlaced",
                accent = Accent(definition.color)
            });
            if (current == null) StartNext();
        }

        private void Build()
        {
            screen = BattleNativeWidgets.Rect(transform, "BS_SummonPresentation");
            BattleNativeWidgets.Place(screen, 0, 0, 1, 1);
            dim = Solid(screen, "Dim", new Color(0.005f, 0.012f, 0.025f, 0), 0, 0, 1, 1);
            // The full-screen graphic swallows pointer clicks while local action
            // controls are blocked; the server continues to run independently.
            dim.raycastTarget = true;
            for (int i = 0; i < 7; i++)
            {
                var streak = Solid(screen, "Sweep_" + i, Color.clear, 0, 0, 1, 1);
                streak.rectTransform.localEulerAngles = new Vector3(0, 0, i % 2 == 0 ? -12 : 12);
                streaks.Add(streak);
            }
            horizon = Solid(screen, "SummonHorizon", Color.clear, 0.04f, 0.26f, 0.96f, 0.264f);
            illustrationSlot = BattleNativeWidgets.Rect(screen, "IllustrationSlot");
            illustrationArt = Artwork(illustrationSlot, "Illustration", out illustrationAspect);
            illustrationArt.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3, 3);
            cardSlot = BattleNativeWidgets.Rect(screen, "CardSlot");
            cardArt = Artwork(cardSlot, "CardFace", out cardAspect);
            cardArt.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3, 3);
            frame.Add(Solid(screen, "FrameTop", Color.clear, 0.04f, 0.88f, 0.96f, 0.883f));
            frame.Add(Solid(screen, "FrameBottom", Color.clear, 0.04f, 0.21f, 0.96f, 0.213f));
            frame.Add(Solid(screen, "FrameLeft", Color.clear, 0.04f, 0.21f, 0.042f, 0.883f));
            frame.Add(Solid(screen, "FrameRight", Color.clear, 0.958f, 0.21f, 0.96f, 0.883f));
            heading = BattleNativeWidgets.Text(screen, "Heading", string.Empty, 28);
            BattleNativeWidgets.Place(heading.rectTransform, 0.06f, 0.90f, 0.94f, 0.96f);
            var symbol = BattleNativeWidgets.Rect(screen, "BS_SummonColorSymbol");
            BattleNativeWidgets.Place(symbol, 0.06f, 0.90f, 0.10f, 0.96f);
            colorSymbol = symbol.gameObject.AddComponent<RawImage>(); colorSymbol.raycastTarget = false;
            var symbolAspect = symbol.gameObject.AddComponent<AspectRatioFitter>();
            symbolAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent; symbolAspect.aspectRatio = 1;
            title = BattleNativeWidgets.Text(screen, "CardName", string.Empty, 40);
            BattleNativeWidgets.Place(title.rectTransform, 0.06f, 0.12f, 0.94f, 0.20f);
            identity = BattleNativeWidgets.Text(screen, "CardId", string.Empty, 20);
            BattleNativeWidgets.Place(identity.rectTransform, 0.06f, 0.075f, 0.94f, 0.12f);
            flash = Solid(screen, "RevealFlash", Color.clear, 0, 0, 1, 1);
            var skip = BattleNativeWidgets.Button(screen, "BS_SkipSummon", "BỎ QUA", Clear);
            BattleNativeWidgets.Place(skip.transform as RectTransform, 0.84f, 0.02f, 0.98f, 0.07f);
            screen.gameObject.SetActive(false);
        }

        private static Image Solid(Transform parent, string name, Color color,
            float left, float bottom, float right, float top)
        {
            var rect = BattleNativeWidgets.Rect(parent, name);
            BattleNativeWidgets.Place(rect, left, bottom, right, top);
            var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
            return image;
        }

        private static RawImage Artwork(Transform parent, string name, out AspectRatioFitter ratio)
        {
            var rect = BattleNativeWidgets.Rect(parent, name); BattleNativeWidgets.Place(rect, 0, 0, 1, 1);
            var image = rect.gameObject.AddComponent<RawImage>(); image.raycastTarget = false; image.color = Color.clear;
            ratio = rect.gameObject.AddComponent<AspectRatioFitter>();
            ratio.aspectMode = AspectRatioFitter.AspectMode.FitInParent; ratio.aspectRatio = 675f / 983f;
            return image;
        }

        private void StartNext()
        {
            if (disposed || screen == null || pending.Count == 0) { Finish(); return; }
            current = pending.Dequeue(); elapsed = artworkWait = 0; modelRevealed = false;
            cardArt.texture = null; illustrationArt.texture = null;
            cardArt.color = illustrationArt.color = Color.clear;
            illustrationArt.uvRect = BattleFieldVisuals.GetIllustrationUv(current.id);
            bool local = session.View == null || current.actor == session.View.viewer;
            heading.text = (local ? "" : "ĐỐI THỦ · ") + (current.nexus ? "ĐẶT NEXUS" : "TRIỆU HỒI SPIRIT");
            title.text = current.name; identity.text = current.id;
            colorSymbol.texture = BattleTheme.ColorIcon(catalog[current.id].color);
            colorSymbol.gameObject.SetActive(colorSymbol.texture != null);
            screen.SetAsLastSibling(); screen.gameObject.SetActive(true);
            SetBusy(true);
            session.LoadArt(current.id, cardArt); session.LoadArt(current.id, illustrationArt);
            Draw(BattleSummonMotion.Sample(0));
        }

        private void Update() => Tick(Time.unscaledDeltaTime);

        public void Tick(float unscaledDelta)
        {
            if (disposed || current == null || screen == null) return;
            if (!gameObject.activeInHierarchy) { Clear(); return; }
            float delta = float.IsNaN(unscaledDelta) || float.IsInfinity(unscaledDelta)
                ? 0 : Math.Max(0, Math.Min(0.25f, unscaledDelta));
            if (!Ready(cardArt.texture) || !Ready(illustrationArt.texture))
            {
                artworkWait += delta;
                if (artworkWait >= ArtworkWaitLimit) CompleteCurrent();
                return;
            }
            elapsed += delta;
            if (elapsed >= 1.70f) RevealModel();
            UpdateAspectRatios(); Draw(BattleSummonMotion.Sample(elapsed));
            if (elapsed >= BattleSummonMotion.Duration) CompleteCurrent();
        }

        private void UpdateAspectRatios()
        {
            if (cardArt.texture != null && cardArt.texture.width > 0 && cardArt.texture.height > 0)
                cardAspect.aspectRatio = (float)cardArt.texture.width / cardArt.texture.height;
            if (illustrationArt.texture != null && illustrationArt.texture.width > 0 && illustrationArt.texture.height > 0)
            {
                Rect uv = illustrationArt.uvRect;
                illustrationAspect.aspectRatio = illustrationArt.texture.width * uv.width
                    / (illustrationArt.texture.height * uv.height);
            }
        }

        private void Draw(BattleSummonSample pose)
        {
            if (current == null) return;
            dim.color = new Color(0.005f, 0.005f, 0.008f, pose.backdropAlpha);
            flash.color = new Color(0.78f, 0.96f, 1, pose.flashAlpha);
            Color accent = current.accent;
            foreach (var edge in frame) edge.color = Alpha(accent, pose.glow * 0.66f);
            horizon.color = Alpha(accent, pose.glow * 0.9f);
            for (int i = 0; i < streaks.Count; i++)
            {
                var streak = streaks[i];
                float center = -0.7f + pose.sweep * 2.6f + i * 0.08f;
                float y = 0.28f + i * 0.073f;
                BattleNativeWidgets.Place(streak.rectTransform, center - 0.28f, y, center + 0.28f, y + (i % 2 == 0 ? 0.003f : 0.0014f));
                streak.color = Alpha(accent, pose.glow * (i % 2 == 0 ? 0.45f : 0.18f));
            }

            Rect destination = new Rect(0.45f, 0.30f, 0.10f, 0.11f);
            if (session is IBattleNativeCinematicSession cinematic
                && cinematic.TryGetCardScreenViewport(current.spirit, out Rect screenViewport)
                && screenViewport.width > 0 && screenViewport.height > 0) destination = screenViewport;
            else if (session is IBattleNativePresentationSession presentation
                && presentation.TryGetCardViewport(current.spirit, out Rect viewport)
                && viewport.width > 0 && viewport.height > 0) destination = viewport;
            else if (session.View != null && current.actor != session.View.viewer)
                destination = new Rect(0.45f, 0.60f, 0.10f, 0.11f);
            float centerX = Lerp(0.31f, destination.x + destination.width * 0.5f, pose.landing);
            float centerY = Lerp(0.53f, destination.y + destination.height * 0.5f, pose.landing);
            var canvasRect = (transform as RectTransform)?.rect ?? new Rect(0, 0, 1600, 900);
            float canvasAspect = canvasRect.height > 0 ? canvasRect.width / canvasRect.height : 16f / 9;
            BattleSummonMotion.GetLanding(destination.width, destination.height, canvasAspect,
                cardAspect.aspectRatio, out float landingHeight, out float landingTilt);
            float width = Lerp(0.225f, destination.width, pose.landing) * pose.cardScale;
            float height = Lerp(0.64f, landingHeight, pose.landing) * pose.cardScale;
            PlaceCentered(cardSlot, centerX, centerY, width, height);
            cardSlot.localEulerAngles = new Vector3(landingTilt * pose.landing, pose.cardFlipY, pose.cardTilt);
            cardArt.color = Alpha(Color.white, Ready(cardArt.texture) ? pose.cardAlpha : 0);
            cardArt.GetComponent<Outline>().effectColor = Alpha(accent, pose.glow * 0.75f);

            float artWidth = 0.48f * pose.illustrationScale, artHeight = 0.64f * pose.illustrationScale;
            PlaceCentered(illustrationSlot, 0.61f, 0.48f + 0.07f * pose.illustrationRise, artWidth, artHeight);
            illustrationSlot.localEulerAngles = new Vector3(0, -8 * (1 - pose.illustrationRise), 0);
            illustrationArt.color = Alpha(Color.white, Ready(illustrationArt.texture) ? pose.illustrationAlpha : 0);
            illustrationArt.GetComponent<Outline>().effectColor = Alpha(accent, pose.glow * 0.7f);
            heading.color = Alpha(accent, pose.nameAlpha);
            title.color = identity.color = Alpha(Color.white, pose.nameAlpha);
            colorSymbol.color = Alpha(Color.white, pose.nameAlpha);
        }

        private static void PlaceCentered(RectTransform rect, float x, float y, float width, float height)
            => BattleNativeWidgets.Place(rect, x - width * 0.5f, y - height * 0.5f, x + width * 0.5f, y + height * 0.5f);
        private static float Lerp(float from, float to, float amount) => from + (to - from) * amount;
        private static bool Ready(Texture texture) => texture != null && texture != Texture2D.blackTexture
            && texture.width > 0 && texture.height > 0;
        private static Color Alpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);
        private static Color Accent(CardColor color)
        {
            switch (color)
            {
                case CardColor.Red: return new Color(1, 0.34f, 0.15f);
                case CardColor.Purple: return new Color(0.76f, 0.35f, 1);
                case CardColor.Green: return new Color(0.28f, 1, 0.54f);
                case CardColor.White: return new Color(0.78f, 0.91f, 1);
                case CardColor.Yellow: return new Color(1, 0.83f, 0.25f);
                default: return new Color(0.17f, 0.76f, 1);
            }
        }

        private void CompleteCurrent()
        {
            RevealModel();
            if (current != null) scheduled.Remove(current.key);
            current = null;
            if (pending.Count > 0) StartNext(); else Finish();
        }

        private void RevealModel()
        {
            if (modelRevealed || current == null) return;
            modelRevealed = true;
            if (!current.nexus && session is IBattleNativeCinematicSession cinematic) cinematic.RevealSpiritModel(current.spirit);
        }

        private void SetBusy(bool busy)
        {
            if (session is UnityEngine.Object owner && owner == null) return;
            if (session is IBattleNativePresentationSession presentation) presentation.SetPresentationBusy(busy);
        }

        private void Finish()
        {
            current = null;
            if (screen != null) screen.gameObject.SetActive(false);
            SetBusy(false);
        }

        public void Clear()
        {
            pending.Clear(); scheduled.Clear(); current = null; elapsed = artworkWait = 0;
            if (cardArt != null) { cardArt.texture = null; cardArt.color = Color.clear; }
            if (illustrationArt != null) { illustrationArt.texture = null; illustrationArt.color = Color.clear; }
            Finish();
        }

        private void OnDisable() => Clear();
        private void OnDestroy() => Dispose();
        public void Dispose()
        {
            if (disposed) return;
            Clear(); disposed = true; session = null; catalog.Clear();
            if (screen != null) { Destroy(screen.gameObject); screen = null; }
        }
    }
}
