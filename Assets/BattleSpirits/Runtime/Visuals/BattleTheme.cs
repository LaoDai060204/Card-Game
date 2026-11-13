using System;
using System.Collections.Generic;
using LaoDai.BattleSpirits.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LaoDai.BattleSpirits.Visuals
{
    // Shared template palette and reviewed BS skin assets. Resources owns the
    // textures; callers must not destroy them when closing a screen or field.
    public static class BattleTheme
    {
        public const string CoreSkinConfig = "BattleSpiritsCoreSkin";
        public const string DefaultCoreSkin = "Blue";
        public const string SpiritModelConfig = "BattleSpiritsSpiritModels";
        public const string DuelQualityConfig = "BattleSpiritsDuelQuality";
        public static readonly Color PanelBackground = new Color(0.15294118f, 0.15294118f, 0.1764706f, 1);
        public static readonly Color Frame = new Color(0.8f, 0.82f, 0.86f, 1);
        public static readonly Color Accent = new Color(0.7529412f, 1, 0, 1);
        public static readonly Color Red = new Color(0.6117647f, 0.09411765f, 0.09411765f, 1);
        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        public static Texture2D CardBack => Texture("CardBack");
        public static Texture2D CoreNormal => Texture("CoreNormal");
        public static Texture2D SoulCore => Texture("SoulCore");
        public static Texture2D PlaymatStandard => Texture("PlaymatStandard");
        public static Texture2D PlaymatReference => Texture("PlaymatReference");
        public static Texture2D ColorIcon(CardColor color) => Texture("Color" + color);
        public static string NormalizeCoreSkin(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "purple": return "Purple";
                case "green": return "Green";
                case "yellow": return "Yellow";
                case "white": return "White";
                default: return DefaultCoreSkin;
            }
        }

        // A cosmetic choice changes ordinary Core artwork only. Red remains
        // reserved for the unique Soul Core and never follows a card's color.
        public static Texture2D CoreGem(string style)
        {
            switch (NormalizeCoreSkin(style))
            {
                case "Purple": return ColorIcon(CardColor.Purple);
                case "Green": return ColorIcon(CardColor.Green);
                case "Yellow": return ColorIcon(CardColor.Yellow);
                case "White": return ColorIcon(CardColor.White);
                default: return ColorIcon(CardColor.Blue);
            }
        }
        public static Sprite ButtonSprite => SlicedSprite("ChromeButton", 22);
        public static Sprite PanelSprite => SlicedSprite("ChromePanel", 16);

        private static Texture2D Texture(string name)
        {
            if (textures.TryGetValue(name, out var value) && value != null) return value;
            value = Resources.Load<Texture2D>("BattleSpirits/Skin/" + name);
            if (value != null) textures[name] = value;
            return value;
        }

        private static Sprite SlicedSprite(string name, float border)
        {
            if (sprites.TryGetValue(name, out var sprite) && sprite != null) return sprite;
            var texture = Texture(name); if (texture == null) return null;
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.name = "BS_" + name; sprites[name] = sprite; return sprite;
        }

        public static void StyleButton(Button button, Image face)
        {
            face.material = null;
            face.sprite = ButtonSprite;
            face.type = Image.Type.Sliced;
            face.color = face.sprite != null ? Color.white : PanelBackground;
            button.targetGraphic = face;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Accent;
            colors.selectedColor = Accent;
            colors.pressedColor = new Color(0.6f, 0.8f, 0, 1);
            colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.65f);
            button.colors = colors;
        }
    }
}
