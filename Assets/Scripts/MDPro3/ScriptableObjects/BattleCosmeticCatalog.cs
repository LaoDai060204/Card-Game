using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace MDPro3
{
    // File-only pet/wallpaper metadata. It never reads card databases or CID maps.
    public sealed class BattleCosmeticCatalog
    {
        public const string DefaultWallpaper = "battle-spirits";
        public const string RelativePath = "Data/BattleSpirits/cosmetics.json";
        public const string SharedShader = "Built-in/shaders";
        public static readonly string[] BundleDirectories = { "Mate", "Wallpaper" };

        public int version;
        public string defaultWallpaper;
        public List<BattleCosmeticEntry> mates = new();
        public List<BattleCosmeticEntry> wallpapers = new();

        public static BattleCosmeticCatalog Load(string file, string bundleRoot)
        {
            if (new FileInfo(file).Length > 2 * 1024 * 1024)
                throw new InvalidDataException("The pet/wallpaper catalog is too large.");
            var result = JsonConvert.DeserializeObject<BattleCosmeticCatalog>(File.ReadAllText(file));
            if (result == null || result.version != 1)
                throw new InvalidDataException("Unsupported pet/wallpaper catalog version.");
            result.mates = Validate(result.mates, "Mate/", bundleRoot);
            result.wallpapers = Validate(result.wallpapers, "Wallpaper/", bundleRoot);
            return result;
        }

        private static List<BattleCosmeticEntry> Validate(List<BattleCosmeticEntry> entries, string prefix, string root)
        {
            if (entries == null || entries.Count > 4096)
                throw new InvalidDataException("Invalid pet/wallpaper entries.");
            var ids = new HashSet<int>();
            var available = new List<BattleCosmeticEntry>();
            foreach (var entry in entries)
            {
                if (entry == null || entry.id <= 0 || !ids.Add(entry.id)
                    || string.IsNullOrWhiteSpace(entry.name) || entry.name.Length > 256)
                    throw new InvalidDataException("Invalid or duplicate pet/wallpaper entry.");
                string path = ResolveBundlePath(root, entry.path, prefix);
                // A catalog can be shared between platforms. Show only retained
                // cosmetics whose bundle is installed for the current platform.
                if (File.Exists(path) || Directory.Exists(path)) available.Add(entry);
            }
            return available;
        }

        public static string ResolveBundlePath(string root, string relative, string prefix)
        {
            if (string.IsNullOrWhiteSpace(relative) || !relative.StartsWith(prefix, StringComparison.Ordinal)
                || relative.Contains('\\') || relative.Contains(':') || Path.IsPathRooted(relative)
                || relative.Split('/').Any(part => part.Length == 0 || part == "." || part == ".."))
                throw new InvalidDataException("Cosmetics must stay inside the retained pet/wallpaper folders.");
            if (relative.EndsWith("_Folder", StringComparison.Ordinal)) relative = relative[..^7];
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relative));
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Cosmetic path escapes the bundle root.");
            return fullPath;
        }

        public static IEnumerable<string> RetainedFiles(string root)
        {
            foreach (string directory in BundleDirectories)
            {
                string folder = Path.Combine(root, directory);
                if (!Directory.Exists(folder)) continue;
                foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                    if (IsBundleFile(file)) yield return file;
            }
            string shaders = Path.Combine(root, SharedShader);
            if (File.Exists(shaders)) yield return shaders;
        }

        public static bool IsBundleFile(string file)
        {
            string name = Path.GetFileName(file);
            return !name.StartsWith(".", StringComparison.Ordinal)
                && !name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                && !name.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase);
        }

        // These label families belong to the retained SE_MATE bank. Menu,
        // battle and card sounds continue to use the new Battle Spirits audio.
        public static bool IsPetSoundLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return false;
            const string oneShot = "_oneshot";
            if (label.EndsWith(oneShot, StringComparison.OrdinalIgnoreCase))
                label = label.Substring(0, label.Length - oneShot.Length);
            if (string.Equals(label, "SE_LAUNDRY_CHANGE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(label, "SE_LAUNDRY_RETURN", StringComparison.OrdinalIgnoreCase)) return true;
            return label.Length > 4 && char.IsDigit(label[4])
                && (label.StartsWith("SE_M", StringComparison.OrdinalIgnoreCase)
                    || label.StartsWith("SE_V", StringComparison.OrdinalIgnoreCase));
        }
    }

    public sealed class BattleCosmeticEntry
    {
        public int id;
        public string name;
        public string path;
    }
}
