using System;
using System.Collections.Generic;
using LaoDai.BattleSpirits.Core;
using LaoDai.BattleSpirits.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace LaoDai.BattleSpirits.NativeUI
{
    public enum BattleNativeScreen { Deck = 0, Solo = 1, Online = 2, Room = 3, Duel = 4 }

    public interface IBattleNativeReplaySession
    {
        bool IsReplay { get; }
        void StepReplay(int delta);
    }

    public interface IBattleNativePortraitSession
    {
        void LoadPortrait(string cardId, RawImage image);
    }

    // Character-only RGBA assets are separate from the untouched printed face
    // and legacy closeup renders. A missing cutout is not a full-card fallback.
    public interface IBattleNativeStandeeSession
    {
        bool HasStandee(string cardId);
        void LoadStandee(string cardId, RawImage image);
    }

    public interface IBattleNativePreviewSession
    {
        bool IsPreview { get; }
        void PlayPreview(string eventKind);
    }

    // Optional local presentation state. It never changes network authority or
    // enters a public match snapshot; views only delay local input/AI polling.
    public interface IBattleNativePresentationSession
    {
        event Action<PublicBattleEvent> PresentationEvent;
        event Action PresentationReset;
        bool IsPresentationBusy { get; }
        void SetPresentationBusy(bool busy);
        bool TryGetCardViewport(int instanceId, out Rect bounds);
    }

    // Local cinematic coordinates differ from the field-relative picking rect.
    // Revealing a model changes presentation only, without another game event.
    public interface IBattleNativeCinematicSession
    {
        bool TryGetCardScreenViewport(int instanceId, out Rect bounds);
        void RevealSpiritModel(int instanceId);
    }

    // The original prefab views only consume the public/own-player snapshot.
    // They do not access BattleMatch internals or use YGO card/protocol objects.
    public interface IBattleNativeSession
    {
        IReadOnlyList<CardDefinition> Cards { get; }
        IReadOnlyList<DeckDefinition> Decks { get; }
        DeckDefinition SelectedDeck { get; }
        FilteredMatchView View { get; }
        IReadOnlyList<LobbyPlayerView> Players { get; }
        string Status { get; }
        int Seat { get; }
        bool IsConnected { get; }
        bool IsReady { get; }
        bool CanSendCommands { get; }
        event Action Changed;
        void SelectDeck(DeckDefinition deck);
        bool SaveDeck(DeckDefinition deck, out string error);
        void StartSolo(DeckDefinition playerDeck, DeckDefinition opponentDeck);
        void Host(int port, string playerName);
        void Join(string host, int port, string playerName);
        void SetReady(bool ready);
        void StartOnline();
        void Send(WireMessage command);
        void LoadArt(string cardId, RawImage image);
        void AttachField(RawImage image);
        void Disconnect();
    }
}
