using System;
using System.IO;
using System.Linq;
using MDPro3;
using Newtonsoft.Json;

internal static class Program
{
    private static int scenarios;
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }
    private static void Reject(Action action, string message)
    {
        try { action(); } catch (InvalidDataException) { return; }
        throw new Exception(message);
    }
    private static int Main(string[] args)
    {
        string project = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
        string actualRoot = Path.Combine(project, "Platforms/StandaloneWindows64/MasterDuel");
        var catalog = BattleCosmeticCatalog.Load(Path.Combine(project, BattleCosmeticCatalog.RelativePath), actualRoot);
        Check(catalog.mates.Count >= 100 && catalog.wallpapers.Count >= 100, "Installed retained cosmetic catalog unexpectedly lost entries.");
        Check(catalog.mates.All(item => !item.name.Contains("<card")), "Cosmetic names still require a card/CID resolver.");
        scenarios++;

        var retained = BattleCosmeticCatalog.RetainedFiles(actualRoot).Select(path => Path.GetRelativePath(actualRoot, path).Replace('\\', '/')).ToArray();
        Check(retained.Length > 0 && retained.All(path => path.StartsWith("Mate/") || path.StartsWith("Wallpaper/") || path == "Built-in/shaders"), "Build allowlist includes an unrelated Master Duel resource.");
        Check(retained.Contains("Built-in/shaders"), "Retained shared shader dependency is missing."); scenarios++;

        var petSounds = Directory.GetFiles(Path.Combine(project, "Assets/Addressables/Sound/SE_MATE"), "*.ogg");
        Check(petSounds.Length > 0 && petSounds.All(path => BattleCosmeticCatalog.IsPetSoundLabel(Path.GetFileNameWithoutExtension(path))),
            "A retained pet sound would be replaced with a generic Battle Spirits cue.");
        Check(BattleCosmeticCatalog.IsPetSoundLabel("SE_M14676_c_oneshot") && BattleCosmeticCatalog.IsPetSoundLabel("se_v00005_d"),
            "Pet sound fallback labels or case variants were rejected."); scenarios++;
        Check(new[] { null, "", "SE_MENU_DECIDE", "SE_MONSTER_ATTACK", "SE_VOICE_TEST", "SE_CARD_SUMMON", "SE_LAUNDRY_UNKNOWN" }
            .All(label => !BattleCosmeticCatalog.IsPetSoundLabel(label)), "An unrelated legacy sound bypassed the new Battle Spirits sound bank."); scenarios++;

        string temp = Path.Combine(Path.GetTempPath(), "bs-cosmetic-contract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            foreach (string path in new[] { "Mate/A", "Wallpaper/Front0001/texture", "Built-in/shaders", "Built-in/outduel", "Mat/Field0001", "Mate/.gitkeep", "Mate/A.meta", "Mate/A.manifest" })
            {
                string file = Path.Combine(temp, path); Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllText(file, "fixture");
            }
            string[] selected = BattleCosmeticCatalog.RetainedFiles(temp).Select(path => Path.GetRelativePath(temp, path).Replace('\\', '/')).OrderBy(path => path).ToArray();
            Check(selected.SequenceEqual(new[] { "Built-in/shaders", "Mate/A", "Wallpaper/Front0001/texture" }), "Bundle enumeration copied unrelated content or metadata/sentinel files."); scenarios++;

            foreach (string path in new[] { "../Mate/A", "/Mate/A", "Mate/../Built-in/outduel", "Mate/./A", "Mate//A", "Mate\\A", "Mate/C:/A", "Wallpaper/Front0001" })
                Reject(() => BattleCosmeticCatalog.ResolveBundlePath(temp, path, "Mate/"), "Accepted invalid pet path: " + path);
            scenarios++;

            Check(BattleCosmeticCatalog.ResolveBundlePath(temp, "Mate/A_Folder", "Mate/") == Path.Combine(temp, "Mate/A"), "Folder bundle alias was not resolved."); scenarios++;
            string json = Path.Combine(temp, "cosmetics.json");
            void Write(object value) => File.WriteAllText(json, JsonConvert.SerializeObject(value));
            Write(new { version = 1, mates = new[] { new { id = 1, name = "Pet", path = "Mate/A" }, new { id = 2, name = "Missing", path = "Mate/NotInstalled" } }, wallpapers = new object[0] });
            var available = BattleCosmeticCatalog.Load(json, temp);
            Check(available.mates.Count == 1 && available.mates[0].name == "Pet", "Uninstalled cosmetics were shown, or installed ones were omitted."); scenarios++;

            Write(new { version = 99, mates = new object[0], wallpapers = new object[0] });
            Reject(() => BattleCosmeticCatalog.Load(json, temp), "Unsupported cosmetic version accepted."); scenarios++;
            Write(new { version = 1, mates = new[] { new { id = 1, name = "A", path = "Mate/A" }, new { id = 1, name = "B", path = "Mate/A" } }, wallpapers = new object[0] });
            Reject(() => BattleCosmeticCatalog.Load(json, temp), "Ambiguous duplicate cosmetic IDs accepted."); scenarios++;
            Write(new { version = 1, mates = new[] { new { id = 1, name = " ", path = "Mate/A" } }, wallpapers = new object[0] });
            Reject(() => BattleCosmeticCatalog.Load(json, temp), "Empty cosmetic label accepted."); scenarios++;
            File.WriteAllText(json, new string(' ', 2 * 1024 * 1024 + 1));
            Reject(() => BattleCosmeticCatalog.Load(json, temp), "Oversized cosmetic catalog accepted."); scenarios++;
        }
        finally { Directory.Delete(temp, true); }

        Console.WriteLine($"PASS: {scenarios} production cosmetic/resource scenarios; {catalog.mates.Count} installed pets, {catalog.wallpapers.Count} wallpapers, {retained.Length} allowlisted bundle files.");
        Console.WriteLine("No Unity bundle rendering or Windows build was executed by this file-only test.");
        return 0;
    }
}
