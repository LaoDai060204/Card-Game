using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace MDPro3.BattleSpirits
{
    /// <summary>Local Battle Spirits artwork in the original appearance and wallpaper widgets.</summary>
    public static class BattleCosmetics
    {
        public const int DefaultWallpaperId = 1139001;
        public const int DefaultFaceId = 1019001;
        public const int ProtectorId = 1079001;
        public const int DefaultFieldId = 1099001;
        public static readonly Color Navy = new Color(.025f, .045f, .085f, 1f);
        private static readonly Dictionary<int, Items.Item> entries = new Dictionary<int, Items.Item>();
        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static readonly Dictionary<int, string> cardIds = new Dictionary<int, string>();
        private static Items current;
        private static Material protectorMaterial;
        private static readonly Dictionary<string, string> illustrators = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "26RBS01-003", "Misa Matoki" },
            { "26RBS01-019", "Illustration: Nekobayashi" },
            { "26RBS01-024", "Illustration: Shikidoji" },
            { "26RBS01-126", "Jun Ishimaru" },
            { "26RBS01-X03", "Y\u014dsuke Adachi" },
            { "26RBS01-X04", "Illustration: Hayaken Sarena" },
            { "26RBS01-X06", "Illustration: Takumi Kousaka" },
            { "26RBS02-002", "Poporucha" },
            { "26RBS02-033", "Norikatsu Miyoshi" },
            { "26RBS02-034", "Dai-XT" },
            { "26RBS02-087", "Misa Matoki" },
            { "26RBS02-X09", "Daisuke Izuka" },
            { "26RBS03-004", "Sakichi Funaya" },
            { "26RBS03-009", "Illustration: Kankurou" },
            { "26RBS03-010", "Concept: Hiroshi Maruyama; Illustration: Kankurou" },
            { "26RBS03-012", "Illustration: Tatsuki Mutou" },
            { "26RBS03-014", "Illustration: Yousuke Ishibashi" },
            { "26RBS03-022", "Credit retained in supplied original card image" },
            { "26RBS03-087", "Dan Takayama" },
            { "26RBS03-089", "Illustration: Shikidoji" },
            { "26RBS03-091", "Koki Saito" },
            { "26RBS03-097", "Illustration: Hisashi Fujiwara" },
            { "26RBS03-X01", "sho" },
            { "26RBS03-X03", "Ichinogi" },
            { "26RBS03-X04", "Illustration: Studio Vigor Co., Ltd." },
            { "26RBS03-X05", "Illustration: ikuyoan" },
            { "26RBS03-X06", "Concept: Junya Ishigaki; Illustration: Junya Ishigaki" },
            { "26RCB01-039", "Souji Kusaka" },
            { "26RCB01-040", "Denji" },
            { "26RCB01-041", "Mari Izura" },
            { "26RCB01-043", "touge369" },
            { "26RCB01-044", "Sakichi Funaya" },
            { "26RCB01-046", "Rokushiro Koushi" },
            { "26RCB01-047", "Kankurou" },
            { "26RCB01-051", "Yu Shimotsuki" },
            { "26RCB01-052", "Denji" },
            { "26RCB01-060", "Tsuda Numato" },
            { "26RCB01-071", "Tonji Matsuno" },
            { "26RCB01-X06", "Yuzuru Takayanagi" },
            { "26RCB01-X07", "Tonji Matsuno" },
            { "26RSD01-012", "Yasushi Yamaguchi" },
            { "26RSD01-014", "touge369" },
            { "26RSD02-001", "Illustration: Tsukapon" },
            { "26RSD02-005", "Illustration: Banira" },
            { "26RSD02-014", "Illustration: Yu Shimotsuki" },
            { "26RSD02-X01", "Illustration: Airi Yoshioka" },
            { "26RSD03-002", "Koki Saito" },
            { "26RSD03-003", "Yoshiyasu Tomoe" },
            { "26RSD03-005", "Mira Aizawa" },
            { "26RSD03-010", "Tatsuki Muto" },
            { "26RSD03-012", "Masato Yoshimura" },
            { "26RSD03-013", "Yoshiyasu Tomoe" },
            { "26RSD03-014", "Miho Midorikawa" },
            { "26RSD03-X02", "Tatsuki Muto" },
            { "26RSD06-012", "Mizuchi Sunakawa" },
            { "26RSD06-013", "Hisashi Fujiwara" },
            { "26RSD09-001", "Poporucha" },
            { "26RSD09-004", "Dan Takayama" },
            { "26RSD09-011", "Numato Tsuda" },
            { "26RSD09-012", "Kankur\u014d" },
            { "26RSD10-003", "Yosuke Ishibashi" },
            { "26RSD10-004", "Yoshiyasu Tomoe" },
        };

        /// <summary>Keep list identities so native appearance pools survive language reloads.</summary>
        public static void Apply(Items items)
        {
            if (items == null) return;
            if (BattleCatalog.Cards.Count == 0) BattleCatalog.Initialize(Application.streamingAssetsPath);
            current = items;
            items.wallpapers = Clear(items.wallpapers);
            items.faces = Clear(items.faces);
            items.protectors = Clear(items.protectors);
            items.mats = Clear(items.mats);
            items.frames = Clear(items.frames);
            items.graves = Clear(items.graves);
            items.stands = Clear(items.stands);
            items.mates = Clear(items.mates);
            items.cases = Clear(items.cases);
            entries.Clear();
            cardIds.Clear();
            foreach (var card in BattleCatalog.Cards)
            {
                string cutout = BattleCatalog.StandeePath(card.id);
                if (string.IsNullOrEmpty(cutout)) continue;
                // The catalog's persistent numeric mapping also keeps cosmetic IDs stable.
                int ordinal = card.numericId - 900000000;
                string description = card.DisplayName + " (" + card.id + ")\n";
                if (illustrators.TryGetValue(card.id, out var illustrator)) description += illustrator + "\n";
                description += "Original card design and artwork \u00a9 BNP/BANDAI; crossover rights remain with their respective owners.\n" + card.sourceUrl;
                Add(items.wallpapers, 1139000 + ordinal, card.DisplayName, description, cutout, card.id);
                Add(items.faces, 1019000 + ordinal, card.DisplayName, description, cutout, card.id);
            }
            Add(items.protectors, ProtectorId, "Battle Spirits Card Back", "Battle Spirits card back. Original supplied artwork \u00a9 BNP/BANDAI.", AssetPath("cosmetics/card-back.png"));
            Add(items.mats, 1099001, "Dragon Field", "Battle Spirits dragon field. Original supplied playmat artwork; rights remain with their respective owners.", AssetPath("playmats/field.png"));
            Add(items.mats, 1099002, "Standard Play Sheet", "Battle Spirits standard play sheet. Original supplied artwork \u00a9 BNP/BANDAI.", AssetPath("playmats/play-sheet.png"));
            Add(items.mats, 1099003, "White Play Sheet", "Battle Spirits white play sheet. Original supplied artwork \u00a9 BNP/BANDAI.", AssetPath("playmats/play-sheet-white.png"));
            items.kinds = new List<List<Items.Item>> { items.wallpapers, items.faces, items.frames, items.protectors, items.mats, items.graves, items.stands, items.mates, items.cases };
        }

        private static List<Items.Item> Clear(List<Items.Item> list)
        {
            if (list == null) return new List<Items.Item>();
            list.Clear();
            return list;
        }

        private static void Add(List<Items.Item> list, int id, string name, string description, string path, string cardId = null)
        {
            var item = new Items.Item { id = id, m_name = name, m_description = description, path = path };
            list.Add(item);
            entries[id] = item;
            if (cardId != null) cardIds[id] = cardId;
        }

        private static string AssetPath(string relative) => Path.Combine(Application.streamingAssetsPath, "BattleSpirits", relative);

        public static List<Items.Item> GetList(Items.ItemType type)
        {
            if (current == null) return null;
            switch (type)
            {
                case Items.ItemType.Wallpaper: return current.wallpapers;
                case Items.ItemType.Face: return current.faces;
                case Items.ItemType.Frame: return current.frames;
                case Items.ItemType.Protector: return current.protectors;
                case Items.ItemType.Mat: return current.mats;
                case Items.ItemType.Grave: return current.graves;
                case Items.ItemType.Stand: return current.stands;
                case Items.ItemType.Mate: return current.mates;
                case Items.ItemType.Case: return current.cases;
                default: return null;
            }
        }

        public static Items.ItemType GetItemType(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length < 3) return Items.ItemType.Unknown;
            switch (id.Substring(0, 3))
            {
                case "113": return Items.ItemType.Wallpaper;
                case "101": return Items.ItemType.Face;
                case "103": return Items.ItemType.Frame;
                case "107": return Items.ItemType.Protector;
                case "108": return Items.ItemType.Case;
                case "109": return Items.ItemType.Mat;
                case "110": return Items.ItemType.Grave;
                case "111": return Items.ItemType.Stand;
                case "100": return Items.ItemType.Mate;
                default: return Items.ItemType.Unknown;
            }
        }

        public static string ResolveId(string code, Items.ItemType type)
        {
            if (code == Items.CODE_NONE.ToString()) return code;
            if (type == Items.ItemType.Unknown) type = GetItemType(code);
            var list = GetList(type);
            if (list == null || list.Count == 0) return Items.CODE_NONE.ToString();
            if (code == Items.CODE_RANDOM.ToString()) return list[UnityEngine.Random.Range(0, list.Count)].id.ToString();
            if (int.TryParse(code, out int id) && entries.ContainsKey(id) && GetItemType(code) == type) return code;
            return list[0].id.ToString();
        }

        public static string GetPath(string code, Items.ItemType type)
        {
            code = ResolveId(code, type);
            return int.TryParse(code, out int id) && entries.TryGetValue(id, out var item) ? item.path : string.Empty;
        }

        public static Texture2D LoadTexture(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (textures.TryGetValue(path, out var cached)) return cached;
            if (!File.Exists(path)) return null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), true))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }
            texture.name = Path.GetFileNameWithoutExtension(path);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            textures[path] = texture;
            return texture;
        }

        private static Rect Crop(int id, Texture2D texture)
        {
            if (cardIds.TryGetValue(id, out string cardId))
            {
                var visual = BattleCatalog.GetVisual(cardId);
                if (visual != null && visual.width == texture.width && visual.height == texture.height
                    && visual.right > visual.left && visual.bottom > visual.top)
                    return new Rect(visual.left, texture.height - visual.bottom, visual.right - visual.left, visual.bottom - visual.top);
            }
            return new Rect(0, 0, texture.width, texture.height);
        }

        public static Sprite LoadIcon(string code, Items.ItemType type = Items.ItemType.Unknown)
        {
            if (code == Items.CODE_RANDOM.ToString() || code == Items.CODE_SAME.ToString())
            {
                string corePath = AssetPath("cosmetics/core.png");
                return SpriteFor(corePath, 0);
            }
            string resolved = ResolveId(code, type);
            string path = GetPath(resolved, type);
            return SpriteFor(path, int.TryParse(resolved, out int id) ? id : 0);
        }

        private static Sprite SpriteFor(string path, int id)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string key = id + ":" + path;
            if (sprites.TryGetValue(key, out var cached)) return cached;
            var texture = LoadTexture(path);
            if (texture == null) return null;
            var sprite = Sprite.Create(texture, Crop(id, texture), new Vector2(.5f, .5f), 100f);
            sprite.name = Path.GetFileNameWithoutExtension(path);
            sprites[key] = sprite;
            return sprite;
        }

        public static Material LoadProtectorMaterial()
        {
            if (protectorMaterial != null) return protectorMaterial;
            var shader = Shader.Find("UI/Default");
            if (shader == null) return null;
            protectorMaterial = new Material(shader) { name = "Battle Spirits Card Back", mainTexture = LoadTexture(AssetPath("cosmetics/card-back.png")) };
            return protectorMaterial;
        }

        /// <summary>Replace only wallpaper content inside the existing native wallpaper canvas.</summary>
        public static Transform CreateWallpaper(Transform parent, string code)
        {
            if (parent == null || code == Items.CODE_NONE.ToString()) return null;
            var root = new GameObject("Battle Spirits Wallpaper", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            Stretch((RectTransform)root.transform);
            var background = NewImage("Battle Spirits Navy", root.transform);
            background.texture = Texture2D.whiteTexture;
            background.color = Navy;
            Stretch(background.rectTransform);
            string resolved = ResolveId(code, Items.ItemType.Wallpaper);
            string path = GetPath(resolved, Items.ItemType.Wallpaper);
            var texture = LoadTexture(path);
            if (texture != null && int.TryParse(resolved, out int id))
            {
                var character = NewImage("Battle Spirits Spirit", root.transform);
                character.texture = texture;
                var crop = Crop(id, texture);
                character.uvRect = new Rect(crop.x / texture.width, crop.y / texture.height, crop.width / texture.width, crop.height / texture.height);
                var rect = character.rectTransform;
                rect.anchorMin = new Vector2(.68f, .48f);
                rect.anchorMax = rect.anchorMin;
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero;
                float height = 920f;
                rect.sizeDelta = new Vector2(height * crop.width / crop.height, height);
            }
            return root.transform;
        }

        private static RawImage NewImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
