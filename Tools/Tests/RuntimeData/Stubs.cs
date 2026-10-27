using System.Collections.Generic;

namespace UnityEngine
{
    public static class Debug
    {
        public static readonly List<string> Warnings = new List<string>();
        public static void LogWarning(object message) => Warnings.Add(message.ToString());
    }
}

namespace MDPro3.Duel.YGOSharp
{
    public sealed class Card
    {
        public int Id;
        public int Alias;
        public string Name;
    }

    public static class CardsManager
    {
        public static readonly Dictionary<int, int> Aliases = new Dictionary<int, int>();
        public static Card Get(int id) => new Card
        {
            Id = id,
            Alias = Aliases.TryGetValue(id, out var alias) ? alias : 0,
            Name = "Card#" + id
        };
    }
}
