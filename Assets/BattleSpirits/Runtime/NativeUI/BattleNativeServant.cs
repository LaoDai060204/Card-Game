using MDPro3.Servant;

namespace LaoDai.BattleSpirits.NativeUI
{
    // Participates in the original Program/Servant transitions and input routing.
    public sealed class BattleNativeServant : Servant
    {
        public BattleNativeScreen Screen { get; internal set; }
        protected override string Label_UI => "ServantUI/" + BattleNativeNavigation.PrefabName(Screen) + ".prefab";
        protected override bool ShowLine => Screen != BattleNativeScreen.Duel;
        public override int Depth => Screen == BattleNativeScreen.Duel ? -1 : 1;

        public override void OnReturn()
        {
            if (GetUI<BattleNativeScreenUI>()?.TryCloseCardZoom() == true) return;
            OnExit();
        }
        public override void OnExit() => BattleNativeNavigation.ReturnToMenu();
    }
}
