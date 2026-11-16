using System;
using System.Collections.Generic;
namespace MDPro3.BattleSpirits.Core
{
    public static class AbilityRegistry
    {
        public const string Version = "standard-effects-20261116";
        private static readonly HashSet<string> Supported = new HashSet<string>(StringComparer.Ordinal)
        {
            "26RSD09-001","26RBS02-002","26RBS01-003","26RSD09-004","26RBS03-004","26RBS03-X01","26RBS01-X03","26RBS03-X03","26RSD09-011","26RSD01-012","26RSD09-012","26RBS02-087","26RBS03-087","26RSD01-014",
            "26RBS03-009","26RSD02-001","26RBS03-010","26RBS03-012","26RBS01-019","26RBS03-014","26RSD02-005","26RBS01-X04","26RBS01-024","26RBS03-X04","26RBS03-X05","26RSD02-X01","26RBS01-X06","26RBS03-X06","26RSD02-014","26RBS03-089","26RBS03-097",
            "26RSD03-002","26RSD03-003","26RSD10-003","26RSD10-004","26RSD03-005","26RSD03-X02","26RBS02-034","26RBS02-033","26RBS02-X09","26RSD03-010","26RSD03-012","26RSD03-013","26RSD03-014","26RBS03-091",
            "26RCB01-039","26RCB01-040","26RCB01-041","26RCB01-043","26RCB01-044","26RCB01-046","26RCB01-047","26RCB01-051","26RCB01-052","26RCB01-X06","26RCB01-X07","26RCB01-060","26RSD06-012","26RSD06-013","26RCB01-071","26RBS01-126","26RBS03-022"
        };
        public static bool Contains(string id) => id != null && Supported.Contains(id);
        public static bool IsRegistered(CardDefinition card) => card != null && Contains(card.id);
        public static IReadOnlyCollection<string> CardIds => Supported;
        private static readonly HashSet<string> Succession = new HashSet<string>(StringComparer.Ordinal)
        {
            "26RBS03-X01", "26RBS01-X03", "26RBS03-X03", "26RSD09-012", "26RBS03-X05", "26RSD02-X01", "26RBS01-X06", "26RBS03-X06",
            "26RSD03-X02", "26RBS02-034", "26RBS02-X09", "26RCB01-X06", "26RCB01-X07", "26RBS01-126"
        };
        public static bool HasSuccession(string id) => id != null && Succession.Contains(id);
        public static bool SoulMagicColor(string id, out CardColor color)
        {
            switch (id)
            {
                case "26RBS02-087": case "26RBS03-087": case "26RSD01-014": color = CardColor.Red; return true;
                case "26RSD02-014": case "26RBS03-089": color = CardColor.Purple; return true;
                case "26RSD03-014": case "26RBS03-091": color = CardColor.Green; return true;
                case "26RBS03-097": case "26RSD06-013": case "26RCB01-071": color = CardColor.Blue; return true;
                default: color = default; return false;
            }
        }
        public static bool HasMainMagic(string id) => id == "26RSD01-012" || id == "26RSD09-012" || id == "26RSD03-012" || id == "26RSD06-012";
    }
}
