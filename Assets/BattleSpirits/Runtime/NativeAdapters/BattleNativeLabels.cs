using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MDPro3.BattleSpirits
{
    // Relabel existing native text objects without changing scene/prefab hierarchy or geometry.
    public static class BattleNativeLabels
    {
        private static readonly Dictionary<string, string> labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Monster"] = "Spirit", ["Monsters"] = "Spirits", ["Spell"] = "Nexus", ["Spells"] = "Nexuses",
            ["Trap"] = "Magic", ["Traps"] = "Magic", ["ATK"] = "BP", ["Attack"] = "BP",
            ["DEF"] = "Symbols", ["Defense"] = "Symbols", ["Level"] = "Cost", ["Level/Rank/Link"] = "Cost",
            ["Attribute"] = "Color", ["Race"] = "Family", ["Spell/Trap"] = "Nexus/Magic",
            ["Card Pool"] = "Standard", ["Forbidden & Limited Lists"] = "Standard Rules",
            ["Test Hand"] = "Test Deck", ["Genesys Points"] = "Symbols",
            ["OCG"] = "Standard", ["TCG"] = "Standard", ["Master Duel"] = "Battle Spirits"
        };

        public static void Apply(GameObject root)
        {
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                if (labels.TryGetValue(label.text.Trim(), out var replacement)) label.text = replacement;
        }
    }
}
