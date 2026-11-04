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
        public async void Load(RawImage image)
        {
            image.raycastTarget = false;
            image.color = new Color(0.2f, 0.3f, 0.45f, 1);
            try
            {
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                byte[] bytes = await Task.Run(() => File.ReadAllBytes(Path.Combine(root, "Picture/Art/bs-menu.png")));
                if (this == null || image == null) return;
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes, true)) { Destroy(texture); texture = null; return; }
                image.texture = texture; image.color = Color.white;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            { if (this != null) Debug.LogWarning("Battle Spirits wallpaper: " + error.Message); }
        }
        private void OnDestroy() { if (texture != null) Destroy(texture); }
    }
}
