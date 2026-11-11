using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace LaoDai.BattleSpirits.Integration
{
    public sealed class BattleMenuWallpaper : MonoBehaviour
    {
        private Texture2D texture;
        private RawImage target;
        private int loadGeneration;

        public async Task<bool> LoadAsync(RawImage image, Func<bool> canAssign = null)
        {
            int generation = ++loadGeneration;
            ReleaseTexture();
            if (image == null) return false;
            image.raycastTarget = false;
            image.color = new Color(0.2f, 0.3f, 0.45f, 1);
            Texture2D loadedTexture = null;
            try
            {
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                byte[] bytes = await Task.Run(() => File.ReadAllBytes(Path.Combine(root, "Picture/Art/bs-menu.png")));
                if (!CanAssign(image, generation, canAssign)) return false;
                loadedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!loadedTexture.LoadImage(bytes, true))
                {
                    Destroy(loadedTexture);
                    loadedTexture = null;
                    Debug.LogWarning("Battle Spirits wallpaper image could not be decoded; keeping the neutral background.");
                    return false;
                }
                texture = loadedTexture;
                loadedTexture = null;
                target = image;
                image.texture = texture; image.color = Color.white;
                return true;
            }
            catch (Exception error)
            {
                if (loadedTexture != null) Destroy(loadedTexture);
                if (CanAssign(image, generation, canAssign))
                    Debug.LogWarning("Battle Spirits wallpaper: " + error.Message);
                return false;
            }
        }

        private bool CanAssign(RawImage image, int generation, Func<bool> canAssign)
            => this != null && image != null && generation == loadGeneration && (canAssign == null || canAssign());

        private void ReleaseTexture()
        {
            if (target != null && target.texture == texture) target.texture = null;
            target = null;
            if (texture != null) Destroy(texture);
            texture = null;
        }

        private void OnDestroy()
        {
            ++loadGeneration;
            ReleaseTexture();
        }
    }
}
