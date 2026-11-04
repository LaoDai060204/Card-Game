using System;
using System.Collections.Generic;
using MDPro3;
using UnityEngine;

namespace LaoDai.BattleSpirits.NativeUI
{
    public static class BattleNativeNavigation
    {
        private static readonly Dictionary<BattleNativeScreen, BattleNativeServant> screens = new Dictionary<BattleNativeScreen, BattleNativeServant>();
        public static IBattleNativeSession Session { get; set; }
        public static bool IsOpen => Program.instance != null && Program.instance.currentServant is BattleNativeServant;

        public static string PrefabName(BattleNativeScreen screen)
        {
            switch (screen)
            {
                case BattleNativeScreen.Deck: return "DeckEditorUI";
                case BattleNativeScreen.Solo: return "SoloSelectorUI";
                case BattleNativeScreen.Online: return "OnlineServantUI";
                case BattleNativeScreen.Room: return "RoomServantUI";
                default: return "OcgCoreUI";
            }
        }

        public static void Open(BattleNativeScreen screen)
        {
            if (Session == null) throw new InvalidOperationException("Battle Spirits session has not been initialized.");
            var program = Program.instance;
            if (!screens.TryGetValue(screen, out var servant) || servant == null)
            {
                var go = new GameObject("BattleSpirits_" + PrefabName(screen), typeof(RectTransform));
                go.layer = program.ui_.gameObject.layer;
                go.transform.SetParent(program.ui_.transform, false);
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                servant = go.AddComponent<BattleNativeServant>();
                servant.Screen = screen;
                servant.returnServant = program.menu;
                program.RegisterBattleServant(servant);
                screens[screen] = servant;
            }
            program.ShiftToServant(servant);
        }

        public static void ReturnToMenu()
        {
            Session?.Disconnect();
            if (Program.instance != null)
                Program.instance.ShiftToServant(Program.instance.menu);
        }
    }
}
