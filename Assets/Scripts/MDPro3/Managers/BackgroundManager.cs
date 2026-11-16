using System.Collections.Generic;
using MDPro3.BattleSpirits;
using UnityEngine;
using MDPro3.UI;
using System.Collections;
using System.IO;
using static UnityEngine.UI.Image;
using YgomSystem.Effect;
using Cysharp.Threading.Tasks;
using System.Threading.Tasks;

namespace MDPro3
{
    public class BackgroundManager : Manager
    {
        GameObject back;
        private Sprite battleBackgroundSprite;
        int cid;
        public static readonly Dictionary<int, string> backgrounds = new Dictionary<int, string>()
        {
            { 1, "Battle Spirits Navy" },
            { 2, "Deep Blue" },
            { 3, "Midnight Violet" },
            { 4, "Slate" },
        };

        public void Refresh()
        {
            Change(cid);
        }

        public void Change(int id)
        {
            if (BattleSpiritsMode.IsEnabled)
            {
                if (back != null) { back.SetActive(false); Destroy(back); }
                if (battleBackgroundSprite != null) Destroy(battleBackgroundSprite);
                if (id == 0) id = Random.Range(1, 5);
                cid = backgrounds.ContainsKey(id) ? id : 1;
                back = new GameObject("Battle Spirits Background");
                back.transform.SetParent(transform, false);
                var renderer = back.AddComponent<SpriteRenderer>();
                // A native solid sprite supplies the canvas base without altering any source pixels.
                Texture2D texture = Texture2D.whiteTexture;
                renderer.color = BattleCosmetics.Navy;
                // Play sheets belong to the duel surface. Menus need a quiet
                // background so their labels and card lists remain readable.
                if (cid == 2) renderer.color = new Color(.018f, .06f, .10f, 1);
                else if (cid == 3) renderer.color = new Color(.045f, .025f, .075f, 1);
                else if (cid == 4) renderer.color = new Color(.045f, .055f, .07f, 1);
                if (texture == null) texture = Texture2D.whiteTexture;
                battleBackgroundSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
                renderer.sprite = battleBackgroundSprite;
                var scaler = back.AddComponent<SpriteScaler>();
                scaler.isApplyOnUpdate = true;
                scaler.SetFitMode(SpriteScaler.FitMode.FitHighestResolutionMaintainAspectRatio);
                Tools.ChangeLayer(back, "2D");
                return;
            }
            Destroy(back);

            if (id == 0)
            {
                var random = Random.Range(0, backgrounds.Count);
                id = Tools.GetNthDictionaryElement(backgrounds, random).Key;
            }

            cid = id;
            if (id == 50)
                id = 1;
            if (id == 51)
                id = 2;
            if (id == 52)
                id = 11;
            if (id == 53)
                id = 5;
            if (id == 54)
                id = 4;
            if (id == 55)
                id = 6;
            if (id == 56)
                id = 7;
            if (id == 57)
                id = 8;
            if (id == 58)
                id = 9;
            if (id == 59)
                id = 10;

            var endString = id.ToString("D4");
            back = ABLoader.LoadFromFolder<SpriteRenderer>("MasterDuel/Background/Back" + endString, true, true);
            if (back.TryGetComponent<SpriteScaler>(out var spriteScaler))
            {
                spriteScaler.isApplyOnUpdate = true;
                spriteScaler.SetFitMode(SpriteScaler.FitMode.FitHighestResolutionMaintainAspectRatio);
            }
            Tools.ChangeLayer(back, "2D");
            back.transform.SetParent(transform, false);

            if(id == 12 || cid >= 50)
            {
                _ = SetDIYBGAsync(back.GetComponent<SpriteRenderer>(), id);
            }
        }

        private async UniTask SetDIYBGAsync(SpriteRenderer renderer, int id)
        {
            var bg = Program.PATH_DIY + "Background";
            if (File.Exists(bg + Program.EXPANSION_PNG))
                bg += Program.EXPANSION_PNG;
            else if (File.Exists(bg + Program.EXPANSION_JPG))
                bg += Program.EXPANSION_JPG;
            else
                return;

            var pic = await TextureManager.LoadPicFromFileAsync(bg);
            int targetHeight = 1080;
            Texture2D scaledTexture;
            if(pic.height != targetHeight)
            {
                int newWidth = Mathf.RoundToInt(pic.width * (targetHeight / (float)pic.height));
                scaledTexture = TextureProcessor.ResizeTexture2D(pic, newWidth, targetHeight);
            }
            else
                scaledTexture = pic;

            if (renderer != null)
                renderer.sprite = TextureManager.Texture2Sprite(scaledTexture);
            if(id == 9)
                renderer.material.SetTexture("_MainTex01", scaledTexture);
        }

        public int GetIDByName(string bgName)
        {
            var id = 0;
            if(bgName == InterString.Get("Random"))
                return 0;
            foreach (var background in backgrounds)
            {
                if(bgName == background.Value)
                {
                    id = background.Key;
                    break;
                }
            }
            return id;
        }
    }
}
