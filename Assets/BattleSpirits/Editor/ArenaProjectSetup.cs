using System.IO;
using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LaoDai.BattleSpirits.Editor
{
    /// <summary>Creates fresh version-matched render assets; no serialized MDPro3 URP settings are carried over.</summary>
    public static class ArenaProjectSetup
    {
        private const string ScenePath = "Assets/BattleSpirits/Scenes/Arena.unity";
        private const string SettingsFolder = "Assets/BattleSpirits/Settings";
        private const string PipelinePath = SettingsFolder + "/EternalUniversalRenderPipeline.asset";

        [InitializeOnLoadMethod]
        private static void OnEditorReady()
        {
            EditorApplication.delayCall += EnsureSetup;
        }

        private static void EnsureSetup()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += EnsureSetup;
                return;
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                Directory.CreateDirectory(SettingsFolder);
                AssetDatabase.Refresh();
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.name = "Eternal Universal Renderer";
                ResourceReloader.ReloadAllNullIn(renderer, "Packages/com.unity.render-pipelines.universal");
                AssetDatabase.CreateAsset(renderer, SettingsFolder + "/EternalUniversalRenderer.asset");
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "Eternal Universal Render Pipeline";
                pipeline.supportsHDR = true;
                pipeline.msaaSampleCount = 4;
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
                AssetDatabase.SaveAssets();
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            PlayerSettings.companyName = "LaoDai060204";
            PlayerSettings.productName = "Battle Spirits - Eternal";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EnsureRuntimeMaterials();
        }

        [MenuItem("Tools/Battle Spirits/Open Arena")]
        private static void OpenArena()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureSetup();
            EditorSceneManager.OpenScene(ScenePath);
        }

        private static void EnsureRuntimeMaterials()
        {
            const string folder = "Assets/BattleSpirits/Resources/Materials";
            Directory.CreateDirectory(folder);
            foreach (var name in new[] { "ArenaLit", "ArenaEmission" })
            {
                var path = folder + "/" + name + ".mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) continue;
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) continue;
                var material = new Material(shader) { name = name };
                if (name == "ArenaEmission")
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", Color.white);
                }
                AssetDatabase.CreateAsset(material, path);
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Battle Spirits/Build Windows x64")]
        private static void BuildWindowsMenu()
        {
            var folder = EditorUtility.SaveFolderPanel("Windows build", "Build", "Windows");
            if (string.IsNullOrEmpty(folder)) return;
            BuildWindows(Path.Combine(folder, "BattleSpirits.exe"));
        }

        /// <summary>Unity -batchmode -projectPath ... -executeMethod LaoDai.BattleSpirits.Editor.ArenaProjectSetup.BuildWindowsFromCommandLine</summary>
        public static void BuildWindowsFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var output = "Build/Windows/BattleSpirits.exe";
            for (var i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-battleSpiritsOutput") output = args[i + 1];
            BuildWindows(output);
        }

        private static void BuildWindows(string output)
        {
            EnsureSetup();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.StrictMode
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Windows build failed. See the Unity build log.");
        }
    }

    internal sealed class ArenaModelImporter : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.EndsWith("/Resources/Models/SummonedDragon.fbx")) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }

        private void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            if (!assetPath.EndsWith("/Resources/Models/SummonedDragon.fbx")) return;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = clip.name.EndsWith("Idle");
            settings.loopBlend = settings.loopTime;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        public override uint GetVersion() => 2;
    }
}
