using System;
using System.Collections.Generic;
using System.Linq;

namespace LaoDai.BattleSpirits.Core
{
    // Explicit, reviewed printed-card implementations. Importing card text or
    // setting implemented=true never invents an ability. Metadata must match
    // the pinned original Japanese card, including its exact effect and stats.
    public static class AbilityRegistry
    {
        public const string Version = "bs01-effects-v1";
        public const string Siegfried = "BS01-X01";
        public const string Valkyrius = "BS01-097";
        public const string SilverWorld = "BS01-113";
        public const string AwakenMagic = "BS01-115";
        public const string DoubleDraw = "BS01-117";

        private sealed class Registration
        {
            public readonly string Name, Type, Effect;
            public readonly CardColor Color;
            public readonly int Cost, Reduction, Symbols;
            public readonly int[] Levels;
            public Registration(string name, string type, CardColor color, int cost, int reduction, int symbols, string effect, int[] levels)
            { Name=name; Type=type; Color=color; Cost=cost; Reduction=reduction; Symbols=symbols; Effect=effect; Levels=levels; }
        }
        private static readonly Dictionary<string, Registration> Registrations = new Dictionary<string, Registration>(StringComparer.Ordinal)
        {
            { "BS01-X01", new Registration("龍皇ジークフリード", "Spirit", CardColor.Red, 6, 3, 1,
                "[Lv1-Lv2-Lv3:フラッシュ]【覚醒】自分のスピリット上から、コアを好きなだけこのスピリット上に置くことができる。 [Lv3]『このスピリットの破壊時』ボイドから、コア1個を自分のライフに置く。", new[] { 1, 4000, 2, 6000, 5, 10000 }) },
            { "BS01-097", new Registration("飛龍ヴァルキュリウス", "Spirit", CardColor.White, 8, 5, 1,
                "[Lv1-Lv2-Lv3]『このスピリットのブロック時』このスピリットをBP+6000する。", new[] { 1, 4000, 4, 6000, 8, 9000 }) },
            { "BS01-113", new Registration("侵食されゆく銀世界", "Nexus", CardColor.White, 5, 3, 1,
                "[Lv1-Lv2]『相手のアタックステップ』ステップ開始時、自分のトラッシュにあるコアすべてを、自分のリザーブに置く。 [Lv2]『相手のアタックステップ』自分のスピリットが破壊されたとき、ボイドからコア1個を自分のリザーブに置く", new[] { 0, 0, 4, 0 }) },
            { "BS01-117", new Registration("ダブルドロー", "Magic", CardColor.Red, 4, 2, 0,
                "[メイン]自分はデッキから2枚ドローする。。 [フラッシュ]このターンの間、スピリット1体をBP+2000する。", Array.Empty<int>()) },
            { "BS01-115", new Registration("アウェイクン", "Magic", CardColor.Red, 3, 1, 0,
                "[フラッシュ]自分のリザーブにあるコア3個までを、自分のスピリット1体に置き、デッキから1枚ドローする。", Array.Empty<int>()) },
        };

        public static bool Contains(string id) => id != null && Registrations.ContainsKey(id);
        public static bool HasAwaken(CardDefinition card) => card != null && card.id == Siegfried && card.IsPlayable;
        public static bool IsRegistered(CardDefinition card)
        {
            if (card == null || card.rulesProfile != Version || !Registrations.TryGetValue(card.id ?? string.Empty, out var definition)) return false;
            if (card.nameJa != definition.Name || card.type != definition.Type || card.color != definition.Color
                || card.cost != definition.Cost || card.reductionSymbols != definition.Reduction || card.symbols != definition.Symbols
                || Compact(card.effectTextJa) != Compact(definition.Effect) || card.levels == null
                || card.levels.Count * 2 != definition.Levels.Length) return false;
            for (int i=0;i<card.levels.Count;i++)
                if (card.levels[i] == null || card.levels[i].cores != definition.Levels[i*2] || card.levels[i].bp != definition.Levels[i*2+1]) return false;
            return true;
        }
        private static string Compact(string text) => new string((text ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray());
    }
}
