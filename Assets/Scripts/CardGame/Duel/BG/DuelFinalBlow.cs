using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace CardGame.Duel
{
    public class DuelFinalBlow : LoopTrackManager
    {
        public void Destroy()
        {
            StopLoop();
            Destroy(gameObject, 0.5f);
        }
    }
}