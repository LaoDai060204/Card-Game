using System;
using System.IO;
using System.Linq;
using MDPro3;
using MDPro3.BattleSpirits;
using UnityEngine;
using UnityEngine.UI;

internal static class CosmeticsSmoke
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new Exception(message);
    }

    private static void Main()
    {
        Application.streamingAssetsPath = Path.GetFullPath("Assets/StreamingAssets");
        var items = new Items();
        items.Initialize();
        Check(items.wallpapers.Count == 43, "Every supplied cutout is a wallpaper.");
        Check(items.faces.Count == 43, "Every supplied cutout is an avatar.");
        Check(items.protectors.Count == 1 && items.mats.Count == 3, "Approved card back and fields are selectable.");
        Check(items.frames.Count + items.graves.Count + items.stands.Count + items.mates.Count + items.cases.Count == 0, "Unavailable categories contain no old game content.");
        var faceList = items.faces;
        var wallpaperList = items.wallpapers;
        var ids = items.wallpapers.Select(i => i.id).ToArray();
        items.Initialize();
        BattleCosmetics.Apply(items);
        Check(ReferenceEquals(faceList, items.faces) && ReferenceEquals(wallpaperList, items.wallpapers), "Reload preserves native list references.");
        Check(ids.SequenceEqual(items.wallpapers.Select(i => i.id)), "Reload preserves stable IDs without duplicates.");
        foreach (var item in items.wallpapers)
        {
            Check(File.Exists(item.path) && item.name != Items.STRING_NULL, "Wallpaper has supplied artwork and a card name.");
            Check(item.description.Contains("BNP/BANDAI") && item.description.Contains("battlespirits.com"), "Card credits and source are available.");
            var sprite = items.LoadItemIconAsync(item.id.ToString(), Items.ItemType.Wallpaper).GetAwaiter().GetResult();
            Check(sprite != null && sprite.rect.width > 0 && sprite.rect.height > 0, "Wallpaper icon loads through BS texture service.");
            var card = BattleCatalog.Cards.Single(c => 1139000 + c.numericId - 900000000 == item.id);
            var bounds = BattleCatalog.GetVisual(card.id);
            Check(sprite.rect.width == bounds.right - bounds.left && sprite.rect.y == bounds.height - bounds.bottom, "Alpha bounds crop the view without changing source pixels.");
        }
        foreach (Items.ItemType type in new[] { Items.ItemType.Frame, Items.ItemType.Grave, Items.ItemType.Stand, Items.ItemType.Mate, Items.ItemType.Case })
        {
            Check(items.GetRandomItem(type).id == 0, "Random empty categories are safe.");
            Check(items.GetAssetPath("missing", type) == string.Empty, "Empty category path is safe.");
            Check(items.LoadConcreteItemIconAsync("missing", type).GetAwaiter().GetResult() == null, "Empty category icon is safe.");
        }
        Check(items.LoadDeckCaseIconAsync(1080001, "_L").GetAwaiter().GetResult() == null, "No legacy deck case fallback.");
        Check(items.TryLoadItemIconAsync("1010001", Items.ItemType.Face).GetAwaiter().GetResult() != null, "Old avatar config resolves to a BS avatar.");
        Check(items.LoadConcreteItemIconAsync("9999", Items.ItemType.Face).GetAwaiter().GetResult() != null, "Random avatars resolve to BS textures.");
        Check(items.GetWallpaperPath("1130001").Contains("BattleSpirits"), "Old wallpaper config resolves to a BS asset.");
        Check(Items.GetIconAddress("1030001") == string.Empty, "Old unavailable frame IDs cannot return MD addresses.");
        Check(!items.ListHaveRandom(items.frames) && !items.ListHaveNone(items.mates) && !items.ListHaveDIY(items.faces), "Empty categories and unavailable DIY artwork stay empty.");
        var parent = new GameObject("Existing Wallpaper Canvas", typeof(RectTransform)).transform;
        var wallpaper = BattleCosmetics.CreateWallpaper(parent, "1130001");
        Check(wallpaper != null && wallpaper.parent == parent && wallpaper.children.Count == 2, "Wallpaper uses original parent and native content widgets.");
        var navy = wallpaper.children[0].GetComponent<RawImage>();
        Check(navy.color.b == BattleCosmetics.Navy.b && !navy.raycastTarget, "Navy background cannot intercept navigation.");
        var cutout = wallpaper.children[1].GetComponent<RawImage>();
        Check(cutout.texture != null && cutout.uvRect.width > 0 && !cutout.raycastTarget, "Native wallpaper displays the BS cutout.");
        Check(BattleCosmetics.CreateWallpaper(parent, "0") == null, "No image choice is retained.");
        Check(BattleCosmetics.LoadProtectorMaterial().mainTexture != null, "Native protector uses the approved card back.");
        Console.WriteLine("PASS: " + checks + " production cosmetics/item assertions; Unity rendering and Play Mode were not run.");
    }
}
