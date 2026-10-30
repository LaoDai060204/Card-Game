using System;

namespace LaoDai.BattleSpirits.Core
{
    [Serializable]
    public sealed class BattleCommand
    {
        public string action = string.Empty;
        // Hand index for summon; instance ID for attack and block.
        public int index;
        // -1 denotes the owner's Reserve.
        public int source = -1;
        public int target = -1;
        public bool soul;
        public bool redraw;
    }

    public static class BattleCommandDispatcher
    {
        // actor is assigned by the server connection, never trusted from a
        // client-supplied player index. Each rule operation checks its phase.
        public static bool TryExecute(BattleMatch match, int actor, BattleCommand command, out string error)
        {
            if (match == null) return Reject("Chưa bắt đầu trận đấu.", out error);
            if (actor < 0 || actor > 1) return Reject("Người chơi không hợp lệ.", out error);
            if (command == null) return Reject("Lệnh trận đấu không hợp lệ.", out error);
            switch (command.action)
            {
                case "opening": return match.TryConfirmOpening(actor, command.redraw, out error);
                case "summon": return match.TrySummon(actor, command.index, out error);
                case "moveCore": return match.TryMoveCore(actor, command.source, command.target, command.soul, out error);
                case "attackStep": return match.TryBeginAttackStep(actor, out error);
                case "attack": return match.TryAttack(actor, command.index, out error);
                case "passFlash": return match.TryPassFlash(actor, out error);
                case "block": return match.TryBlock(actor, command.index, out error);
                case "takeLife": return match.TryTakeLife(actor, out error);
                case "endTurn": return match.TryEndTurn(actor, out error);
                case "concede": return match.TryConcede(actor, out error);
                default: return Reject("Lệnh Battle Spirits không được hỗ trợ.", out error);
            }
        }

        private static bool Reject(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
