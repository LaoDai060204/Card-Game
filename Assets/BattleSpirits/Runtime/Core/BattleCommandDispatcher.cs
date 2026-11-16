using System;
namespace MDPro3.BattleSpirits.Core
{
    [Serializable] public sealed class CoreTransfer { public int source = -1; public int normal; public bool soul; }
    [Serializable]
    public sealed class BattleCommand
    {
        public string action = "";
        public int index, source = -1, target = -1, mode;
        public bool soul, redraw, alternateCost, chooseCores, chooseSuccession;
        public CoreTransfer[] transfers, payment;
        public int[] selection, targets, successionIndices;
        public string[] succession;
    }
    public static class BattleCommandDispatcher
    {
        public static bool TryExecute(BattleMatch match, int actor, BattleCommand command, out string error)
        {
            if (match == null || command == null || actor < 0 || actor > 1) { error = "Invalid match command."; return false; }
            return match.TryExecute(actor, command, out error);
        }
    }
}
