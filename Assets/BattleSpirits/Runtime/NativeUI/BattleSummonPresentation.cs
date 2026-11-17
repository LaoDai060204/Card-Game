using System;
using LaoDai.BattleSpirits.Networking;
using UnityEngine;

namespace LaoDai.BattleSpirits.NativeUI
{
    /// <summary>
    /// Compatibility component for cached duel views. Summons are rendered
    /// directly on the field, without an overlay or a presentation input lock.
    /// </summary>
    public sealed class BattleSummonPresentation : MonoBehaviour, IDisposable
    {
        public bool IsPlaying => false;
        public int PendingCount => 0;

        public void Configure(IBattleNativeSession source) { }
        public void Enqueue(PublicBattleEvent item) { }
        public void Tick(float unscaledDelta) { }
        public void Clear() { }
        public void Dispose() { }
    }
}
