#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using LaoDai.BattleSpirits;
using LaoDai.BattleSpirits.Core;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Unity does not copy the template's external runtime folders into a build.
// Package active BS data/art and only the retained pet/wallpaper dependencies.
public sealed class BattleSpiritsWindowsPackage : IPostprocessBuildWithReport
{
    public int callbackOrder => 1000;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (!BattleSpiritsMode.Enabled || report.summary.platform != BuildTarget.StandaloneWindows64) return;
        string source = Directory.GetParent(Application.dataPath).FullName;
        string output = Path.GetDirectoryName(Path.GetFullPath(report.summary.outputPath));
        string data = Path.Combine(output, Path.GetFileNameWithoutExtension(report.summary.outputPath) + "_Data");
        if (string.Equals(output, source, StringComparison.OrdinalIgnoreCase)
            || new[] { "Assets", "Data", "Picture", "Sound", "Platforms", "Packages", "ProjectSettings" }
                .Any(name => string.Equals(output, Path.Combine(source, name), StringComparison.OrdinalIgnoreCase)
                    || output.StartsWith(Path.Combine(source, name) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            throw new BuildFailedException("Choose a separate Windows build output directory, such as Builds/Windows.");

        foreach (string locale in Directory.EnumerateDirectories(Path.Combine(source, "Data/locales")))
            CopyOptional(Path.Combine(locale, "translation.conf"), Path.Combine(output, "Data/locales", Path.GetFileName(locale), "translation.conf"));
        CopyRelative(source, output, "Data/Settings.json");
        CopyDirectory(Path.Combine(source, "Data/BattleSpirits"), Path.Combine(output, "Data/BattleSpirits"),
            path => !path.Split(Path.DirectorySeparatorChar).Any(part => part == "Decks" || part == "Replays"));
        var catalog = JsonConvert.DeserializeObject<CardsDatabase>(File.ReadAllText(Path.Combine(source, BattleSpiritsMode.CatalogPath)));
        if (catalog?.cards == null) throw new BuildFailedException("The Battle Spirits catalog is missing or invalid.");
        foreach (var card in catalog.cards)
        {
            CopyRelative(source, output, card.artPath);
            CopyOptional(Path.Combine(source, "Picture/Closeup", card.id + ".png"), Path.Combine(output, "Picture/Closeup", card.id + ".png"));
        }
        CopyRelative(source, output, "Picture/Art/bs-menu.png");
        CopyDirectory(Path.Combine(source, "Sound/BattleSpirits"), Path.Combine(output, "Sound/BattleSpirits"));
        string cosmeticSource = Path.Combine(source, "Platforms/StandaloneWindows64/MasterDuel");
        foreach (string directory in MDPro3.BattleCosmeticCatalog.BundleDirectories)
            if (!Directory.Exists(Path.Combine(cosmeticSource, directory)))
                throw new BuildFailedException("Missing retained cosmetic directory: " + directory);
        if (!File.Exists(Path.Combine(cosmeticSource, MDPro3.BattleCosmeticCatalog.SharedShader)))
            throw new BuildFailedException("Missing retained pet/wallpaper shared shaders.");
        foreach (string file in MDPro3.BattleCosmeticCatalog.RetainedFiles(cosmeticSource))
            CopyFile(file, Path.Combine(data, "StandaloneWindows64/MasterDuel", Path.GetRelativePath(cosmeticSource, file)));
        // Data/config.conf and personal decks/logins/replays are deliberately
        // created locally by the game, never copied from the developer's PC.
        Debug.Log("Battle Spirits Windows package: native screens, BS data/art/audio, retained pet/wallpaper bundles and their shared shader dependency copied.");
    }

    private static void CopyRelative(string root, string output, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new BuildFailedException("Invalid Battle Spirits resource path.");
        string file = Path.GetFullPath(Path.Combine(root, relative));
        if (!file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new BuildFailedException("Battle Spirits resource path escapes the project.");
        if (!File.Exists(file)) throw new BuildFailedException("Missing runtime resource: " + relative);
        CopyFile(file, Path.Combine(output, relative));
    }

    private static void CopyOptional(string source, string target)
    { if (File.Exists(source)) CopyFile(source, target); }

    private static void CopyFile(string source, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(source, target, true);
    }

    private static void CopyDirectory(string source, string target, Func<string, bool> include = null)
    {
        if (!Directory.Exists(source)) throw new BuildFailedException("Missing runtime directory: " + source);
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file).StartsWith(".", StringComparison.Ordinal) || file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase) || (include != null && !include(file))) continue;
            CopyFile(file, Path.Combine(target, Path.GetRelativePath(source, file)));
        }
    }
}
#endif
