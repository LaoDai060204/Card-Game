using UnityEngine;

namespace MDPro3.BattleSpirits
{
    [DisallowMultipleComponent]
    public sealed class BattleRuntimeDriver : MonoBehaviour
    {
        private void Update() => BattleIntegration.Tick(Time.unscaledTime);
        private void OnDestroy() => BattleIntegration.Shutdown();
    }
}
