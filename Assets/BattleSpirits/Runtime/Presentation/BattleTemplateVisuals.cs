using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace LaoDai.BattleSpirits.Presentation
{
    internal static class BattleTemplateVisuals
    {
        // Copies only RectTransform/Image artwork from the original editor.
        // No Yu-Gi-Oh Awake methods, event handlers or persistent callbacks run.
        internal static async Task CopyDeckFrameAsync(RectTransform target)
        {
            var handle = Addressables.LoadAssetAsync<GameObject>("ServantUI/DeckEditorUI.prefab");
            try
            {
                var prefab = await handle.Task;
                if (target == null || prefab == null) return;
                var art = new GameObject("BS_MDPro3_OriginalDeckFrame", typeof(RectTransform));
                var root = art.GetComponent<RectTransform>(); root.SetParent(target, false); BattleUiTheme.Stretch(root);
                root.SetAsFirstSibling();
                foreach (string path in new[] { "Root/BG", "Root/SafeArea/DeckEditHeader", "Root/SafeArea/DeckEditOverHeader", "Root/SafeArea/Footer" })
                {
                    var source = prefab.transform.Find(path);
                    if (source != null) Copy(source, root);
                }
            }
            catch (Exception error)
            {
                if (target != null) Debug.LogWarning("Battle Spirits template artwork: " + error.Message);
            }
            finally { if (handle.IsValid()) Addressables.Release(handle); }
        }

        private static void Copy(Transform source, Transform parent)
        {
            if (!(source is RectTransform original)) return;
            var go = new GameObject(source.name, typeof(RectTransform));
            var copy = go.GetComponent<RectTransform>(); copy.SetParent(parent, false);
            copy.anchorMin = original.anchorMin; copy.anchorMax = original.anchorMax;
            copy.pivot = original.pivot; copy.sizeDelta = original.sizeDelta; copy.anchoredPosition = original.anchoredPosition;
            copy.localRotation = original.localRotation; copy.localScale = original.localScale;
            var image = source.GetComponent<Image>();
            if (image != null)
            {
                var graphic = go.AddComponent<Image>(); graphic.sprite = image.sprite; graphic.material = image.material;
                graphic.type = image.type; graphic.color = image.color; graphic.fillAmount = image.fillAmount;
                graphic.preserveAspect = image.preserveAspect; graphic.raycastTarget = false;
            }
            foreach (Transform child in source) Copy(child, copy);
            go.SetActive(source.gameObject.activeSelf);
        }
    }
}
