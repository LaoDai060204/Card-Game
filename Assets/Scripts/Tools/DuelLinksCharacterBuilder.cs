using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

public class DuelLinksCharacterBuilder : MonoBehaviour
{
    private void Start()
    {
        _ = AsyncStart();
    }

    private async UniTask AsyncStart()
    {
        string input = @"D:\Users\Desktop\new\characters\";
        string output = @"D:\Users\Desktop\new\characters output\";
        var results = await ProcessSnFolders(input, output);
        foreach (var kv in results)
            if(!kv.Value)
                Debug.Log($"sn{kv.Key}: Failed.");
        Debug.Log("End");
    }


    /// <summary>
    /// Process all sn folders, combine Body and Face images, and save the results.
    /// </summary>
    /// <param name="inputFolder">Absolute input folder path</param>
    /// <param name="outputFolder">Absolute output folder path</param>
    /// <returns>A dictionary mapping the numeric suffix after sn to whether both images were found and combined successfully.</returns>
    public static async UniTask<Dictionary<string, bool>> ProcessSnFolders(string inputFolder, string outputFolder)
    {
        var resultDict = new Dictionary<string, bool>();

        if (!Directory.Exists(inputFolder))
        {
            Debug.LogError($"Input folder does not exist: {inputFolder}");
            return resultDict;
        }

        // Create the output folder.
        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        // Match folder names consisting of sn followed by one or more digits.
        Regex snRegex = new(@"^sn(\d+)$", RegexOptions.IgnoreCase);

        // Get all subfolders.
        string[] subDirs = Directory.GetDirectories(inputFolder);
        foreach (string dirPath in subDirs)
        {
            string dirName = Path.GetFileName(dirPath);
            Match match = snRegex.Match(dirName);
            if (!match.Success) continue;

            string snNumber = match.Groups[1].Value;  // Numeric suffix
            if( snNumber == "9005")
                snNumber = "9995"; // Tour Guide
            bool success = true;

            // 1. Find the Body image.
            string bodyEnUs = Path.Combine(dirPath, "images", "en-us", "Chara007_0.png");
            string bodyJaJp = Path.Combine(dirPath, "images", "ja-jp", "Chara007_0.png");
            string bodyPath = File.Exists(bodyEnUs) ? bodyEnUs : (File.Exists(bodyJaJp) ? bodyJaJp : null);

            // 2. Find the Face image.
            string faceEnUs = Path.Combine(dirPath, "images", "en-us", "Chara007_1.png");
            string faceJaJp = Path.Combine(dirPath, "images", "ja-jp", "Chara007_1.png");
            string facePath = File.Exists(faceEnUs) ? faceEnUs : (File.Exists(faceJaJp) ? faceJaJp : null);

            if (string.IsNullOrEmpty(bodyPath))
            {
                Debug.LogWarning($"sn{snNumber}: Body image is missing");
                resultDict[snNumber] = false;
                continue;
            }

            string outputFileName = $"sn{snNumber}_2.png";
            string outputPath = Path.Combine(outputFolder, outputFileName);

            // 3. Load the images.
            Texture2D bodyTex = LoadTextureFromFile(bodyPath);
            if (string.IsNullOrEmpty(facePath) && bodyTex != null)
            {
                if (bodyTex.width != 1024 || bodyTex.height != 1024)
                    Debug.LogWarning($"sn{snNumber}: Body image is not 1024x1024; actual size: {bodyTex.width}x{bodyTex.height}");
                SaveTextureAsPNG(bodyTex, outputPath);
                resultDict[snNumber] = true;
                await UniTask.Yield();
                continue;
            }
            Texture2D faceTex = LoadTextureFromFile(facePath);

            if (bodyTex == null || faceTex == null)
            {
                Debug.LogError($"sn{snNumber}: Failed to load images (Body: {bodyPath}, Face: {faceEnUs})");
                resultDict[snNumber] = false;
                continue;
            }

            if (bodyTex.width != 1024 || bodyTex.height != 1024)
                Debug.LogWarning($"sn{snNumber}: Body image is not 1024x1024; actual size: {bodyTex.width}x{bodyTex.height}");
            if (faceTex.width != 240 || faceTex.height != 240)
                Debug.LogWarning($"sn{snNumber}: Face image is not 240x240; actual size: {faceTex.width}x{faceTex.height}");

            // 4. Overlay Face 160 pixels above the center of Body.
            Texture2D mergedTex = MergeImages(bodyTex, faceTex, out bool mergeSuccess);
            if (!mergeSuccess)
            {
                Debug.LogError($"sn{snNumber}: Failed to combine images");
                resultDict[snNumber] = false;
                continue;
            }

            // 5. Save the result.
            SaveTextureAsPNG(mergedTex, outputPath);
            //Debug.Log($"sn{snNumber}: Generated successfully: {outputPath}");

            resultDict[snNumber] = true;
            await UniTask.Yield();
        }

        return resultDict;
    }

    /// <summary>
    /// Load a Texture2D from an arbitrary file path without AssetDatabase.
    /// </summary>
    private static Texture2D LoadTextureFromFile(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        byte[] fileData = File.ReadAllBytes(filePath);
        Texture2D tex = new Texture2D(2, 2);
        if (tex.LoadImage(fileData))
            return tex;
        else
        {
            Object.Destroy(tex);
            return null;
        }
    }

    /// <summary>
    /// Overlay face 160 pixels above the center of body.
    /// Use Graphics.Blit and a custom shader for precise placement.
    /// </summary>
    /// <param name="body">Background image (1024x1024)</param>
    /// <param name="face">Foreground image (240x240)</param>
    /// <param name="success">Whether the operation succeeded</param>
    /// <returns>The combined Texture2D</returns>
    private static Texture2D MergeImages(Texture2D body, Texture2D face, out bool success)
    {
        success = false;
        if (body == null || face == null) return null;

        int bodyW = body.width;
        int bodyH = body.height;
        int faceW = face.width;
        int faceH = face.height;

        // Calculate the Face destination, 160 pixels above the center of Body.
        float centerX = bodyW * 0.5f;
        float centerY = bodyH * 0.5f + 160f;        // 160 pixels above center
        float left = centerX - faceW * 0.5f;
        float bottom = centerY - faceH * 0.5f;

        // Check bounds: a 1024x1024 body can contain a 240x240 face offset by 160 pixels.
        if (left < 0 || left + faceW > bodyW || bottom < 0 || bottom + faceH > bodyH)
        {
            Debug.LogError($"Face region exceeds Body bounds: left={left}, bottom={bottom}, bodySize={bodyW}x{bodyH}");
            return null;
        }

        // Calculate the destination UV range in the Body texture.
        Rect uvRect = new Rect(
            left / bodyW,
            bottom / bodyH,
            faceW / (float)bodyW,
            faceH / (float)bodyH
        );

        // Create a temporary RenderTexture.
        RenderTexture rt = RenderTexture.GetTemporary(bodyW, bodyH, 0, RenderTextureFormat.ARGB32);
        Graphics.SetRenderTarget(rt);
        GL.Clear(true, true, Color.clear);

        // Create the compositing material using a custom shader.
        Material mergeMat = CreateMergeMaterial();
        if (mergeMat == null)
        {
            Debug.LogError("Cannot create the compositing material. Verify custom shader support.");
            RenderTexture.ReleaseTemporary(rt);
            return null;
        }

        mergeMat.SetTexture("_OverlayTex", face);
        mergeMat.SetVector("_OverlayUVRect", new Vector4(uvRect.x, uvRect.y, uvRect.width, uvRect.height));

        // Graphics.Blit: process source body into destination rt using the material.
        Graphics.Blit(body, rt, mergeMat);

        // Read the RenderTexture back into a Texture2D.
        Texture2D result = new Texture2D(bodyW, bodyH, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        result.ReadPixels(new Rect(0, 0, bodyW, bodyH), 0, 0);
        result.Apply();

        // Cleanup
        RenderTexture.active = null;
        Graphics.SetRenderTarget(null);
        RenderTexture.ReleaseTemporary(rt);
        Object.Destroy(mergeMat);

        success = true;
        return result;
    }

    /// <summary>
    /// Create a compositing material; its shader may be generated or loaded from built-in assets.
    /// </summary>
    private static Material CreateMergeMaterial()
    {

        Shader shader = Shader.Find("Hidden/ImageMerger");
        Material mat = new(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        return mat;
    }

    /// <summary>
    /// Save a Texture2D as a PNG file.
    /// </summary>
    private static void SaveTextureAsPNG(Texture2D tex, string path)
    {
        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        Object.Destroy(tex); // Release memory.
    } 
}