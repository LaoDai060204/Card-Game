using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MDPro3.BattleSpirits.Core;
using MDPro3.Duel.YGOSharp;
using UnityEngine;
using Cysharp.Threading.Tasks;
using MDPro3.Utility;
using System.Threading;
using System.IO;
using UnityEngine.UI;

namespace MDPro3.BattleSpirits
{
    // Display records only. These compatibility IDs never enter a YGO duel.
    public static class BattleCardAdapter
    {
        private static readonly Dictionary<CardColor, Sprite> colorIcons = new();
        private static Texture2D back;
        private static Sprite backIcon;
        private static readonly SemaphoreSlim backLock = new SemaphoreSlim(1, 1);

        public static async UniTask<Texture2D> BackAsync(CancellationToken token = default)
        {
            await backLock.WaitAsync(token);
            try
            {
                if (back == null)
                    back = await CardImageLoader.LoadBattleTexturePathAsync(Path.Combine(Application.streamingAssetsPath, "BattleSpirits", "cosmetics", "card-back.png"), token);
                return back;
            }
            finally { backLock.Release(); }
        }

        public static async UniTask<Sprite> BackIconAsync(CancellationToken token = default)
        {
            if (backIcon == null)
            {
                var texture = await BackAsync(token);
                backIcon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
            }
            return backIcon;
        }

        public static void FitFace(RawImage image, Texture texture)
        {
            if (image == null || texture == null) return;
            image.texture = texture;
            image.material = null;
            image.uvRect = new Rect(0, 0, 1, 1);
            var aspect = image.GetComponent<AspectRatioFitter>();
            if (aspect == null) aspect = image.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = texture.width / (float)texture.height;
        }

        public static Card ToNative(CardDefinition definition)
        {
            return new Card
            {
                Id = BattleCatalog.GetNumericId(definition.id),
                BattleSpiritsId = definition.id,
                Name = definition.DisplayName,
                Desc = Description(definition),
                Type = definition.IsSpirit ? (int)(CardType.Monster | CardType.Effect)
                    : definition.IsNexus ? (int)(CardType.Spell | CardType.Field) : (int)CardType.Spell,
                Level = definition.cost,
                Attack = definition.levels?.FirstOrDefault()?.bp ?? 0,
                Defense = definition.symbols,
                Attribute = 1 << (int)definition.color,
                Ot = 0,
                strSetName = definition.setName ?? string.Empty,
                packFullName = definition.setName ?? string.Empty,
                packShortName = definition.setId ?? string.Empty,
                reality = definition.rarity ?? string.Empty
            };
        }

        public static string Description(CardDefinition card)
        {
            string levels = string.Join(" / ", (card.levels ?? new List<CardLevel>())
                .Select((level, index) => "Lv" + (index + 1) + " (" + level.cores + " cores): " + level.bp + " BP"));
            string families = string.Join(" / ", card.families ?? Array.Empty<string>());
            return card.id + " · " + card.type + " · " + Colors(card)
                + "\nCost " + card.cost + " · Reduction " + card.reductionSymbols + " · Symbols " + Symbols(card)
                + (levels.Length > 0 ? "\n" + levels : string.Empty)
                + (families.Length > 0 ? "\nFamily: " + families : string.Empty)
                + "\n\n" + (card.effectTextEn ?? string.Empty);
        }

        public static string Colors(CardDefinition card) => card.colors != null && card.colors.Length > 0
            ? string.Join(" / ", card.colors.Select(value => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value)))
            : card.color.ToString();

        public static string Symbols(CardDefinition card) => card.symbolDetails != null && card.symbolDetails.Length > 0
            ? string.Join(" / ", card.symbolDetails.Select(symbol => symbol.color + " " + symbol.count + (symbol.isEx ? " EX" : string.Empty)))
            : card.symbols.ToString(CultureInfo.InvariantCulture);

        public static Sprite ColorIcon(CardDefinition card)
        {
            if (colorIcons.TryGetValue(card.color, out var icon)) return icon;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Color color = card.color switch
            {
                CardColor.Red => new Color(.9f, .15f, .12f),
                CardColor.Purple => new Color(.63f, .25f, .85f),
                CardColor.Green => new Color(.15f, .75f, .25f),
                CardColor.White => Color.white,
                CardColor.Yellow => new Color(1f, .82f, .12f),
                _ => new Color(.2f, .5f, 1f)
            };
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply(false, true);
            icon = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f));
            colorIcons[card.color] = icon;
            return icon;
        }

        public static bool Matches(CardDefinition card, string[] terms)
        {
            string text = Description(card) + " " + card.DisplayName + " " + card.nameJa;
            foreach (string term in terms)
            {
                if (string.IsNullOrWhiteSpace(term)) continue;
                if (term.StartsWith("cost:", StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(term.Substring(5), out int cost) || card.cost != cost) return false;
                }
                else if (term.StartsWith("bp:", StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(term.Substring(3), out int bp) || !(card.levels?.Any(level => level.bp >= bp) ?? false)) return false;
                }
                else if (text.IndexOf(term.TrimStart('@'), StringComparison.OrdinalIgnoreCase) < 0) return false;
            }
            return true;
        }

        public static bool MatchesFilters(CardDefinition card, List<long> filters, string pack)
        {
            if (!string.IsNullOrEmpty(pack) && card.setName != pack) return false;
            if (filters == null || filters.Count == 0) return true;
            long type = card.IsSpirit ? 1 : card.IsNexus ? 2 : 4;
            if (filters[0] != 0 && (filters[0] & type) == 0) return false;
            if (filters.Count > 1 && filters[1] != 0 && (filters[1] & ColorMask(card)) == 0) return false;
            if (!Range(filters, 13, card.cost)) return false;
            if (!Range(filters, 15, card.levels?.FirstOrDefault()?.bp ?? 0)) return false;
            return Range(filters, 17, card.symbols);
        }

        private static bool Range(List<long> filters, int index, int value) => filters.Count <= index + 1
            || ((filters[index] < 0 || value >= filters[index]) && (filters[index + 1] < 0 || value <= filters[index + 1]));

        private static long ColorMask(CardDefinition card)
        {
            long mask = 1L << (int)card.color;
            foreach (var value in card.colors ?? Array.Empty<string>())
                if (Enum.TryParse<CardColor>(value, true, out var color)) mask |= 1L << (int)color;
            return mask;
        }
    }
}
