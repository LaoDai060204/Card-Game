using MDPro3.BattleSpirits;
using MDPro3.Servant;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using MDPro3.UI.PropertyOverride;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MDPro3.UI.ServantUI
{
    public class CutinViewerUI : ServantUI
    {

        #region Elements

        private const string LABEL_SR = "ScrollRect";
        private ScrollRect m_ScrollRect;
        private ScrollRect ScrollRect =>
            m_ScrollRect = m_ScrollRect != null ? m_ScrollRect
            : Manager.GetElement<ScrollRect>(LABEL_SR);

        private const string LABEL_IPT = "InputField";
        private TMP_InputField m_Input;
        private TMP_InputField Input =>
            m_Input = m_Input != null ? m_Input
            : Manager.GetElement<TMP_InputField>(LABEL_IPT);

        private const string LABEL_SBN_AUTOPLAY = "ButtonAutoPlay";
        private SelectionButton m_ButtonAutoPlay;
        public SelectionButton ButtonAutoPlay =>
            m_ButtonAutoPlay = m_ButtonAutoPlay != null ? m_ButtonAutoPlay
            : Manager.GetElement<SelectionButton>(LABEL_SBN_AUTOPLAY);

        #endregion

        public SuperScrollView superScrollView;
        private GameObject itemPrefab;
        private AsyncOperationHandle<GameObject> itemHandle;
        private bool loadingItems;
        private bool disposed;
        private List<string[]> pendingTasks = new();
        private int selectedCode;
        private string currentSearch = "";
        private RawImage previewImage;
        private TextMeshProUGUI previewName;
        public int SelectedCode => selectedCode;
        public string CurrentSearch => currentSearch;

        public override void Initialize(Servant.Servant servant)
        {
            base.Initialize(servant);
            EnsurePreview();
        }

        public void Print(string search = "")
        {
            currentSearch = search ?? "";
            pendingTasks = new List<string[]>();
            foreach (var card in CutinViewer.cards)
            {
                if (card.Name.IndexOf(currentSearch, System.StringComparison.OrdinalIgnoreCase) >= 0 || card.Id.ToString() == currentSearch)
                    pendingTasks.Add(new[] { card.Id.ToString(), card.Name });
            }
            if (itemPrefab != null) { PrintReadyItems(); return; }
            if (loadingItems) return;
            loadingItems = true;
            itemHandle = Addressables.LoadAssetAsync<GameObject>("UI/ItemCutin.prefab");
            itemHandle.Completed += result =>
            {
                if (disposed || this == null) return;
                loadingItems = false;
                if (result.Status != AsyncOperationStatus.Succeeded || result.Result == null)
                {
                    ShowPreview(0, "The cut-in list could not load. Return and try again.");
                    Debug.LogError("Battle Spirits cut-ins: UI/ItemCutin.prefab could not load.");
                    if (itemHandle.IsValid()) Addressables.Release(itemHandle);
                    return;
                }
                itemPrefab = result.Result;
                PrintReadyItems();
            };
        }

        private void PrintReadyItems()
        {
            // Search changes and first-show loading share one pending request,
            // one list instance and one scrollbar subscription.
            if (superScrollView == null)
            {
                var itemWidth = PropertyOverrider.NeedMobileLayout() ? 460f : 360f;
                var itemHeight = PropertyOverrider.NeedMobileLayout() ? 80f : 40f;
                superScrollView = new SuperScrollView(
                    1,
                    itemWidth,
                    itemHeight,
                    0,
                    0,
                    itemPrefab,
                    ItemOnListRefresh,
                    ScrollRect, 4);
            }
            foreach (var row in superScrollView.gameObjects)
                if (row != null) row.SetActive(false);
            int selection = pendingTasks.FindIndex(task => task[0] == selectedCode.ToString());
            superScrollView.selected = pendingTasks.Count == 0 ? -1 : Mathf.Max(0, selection);
            superScrollView.Print(pendingTasks);
            var item = superScrollView.GetItemByIndex(superScrollView.selected) as SelectionToggle_Cutin;
            if (item != null)
            {
                item.SetToggleOn();
                SelectCutin(item);
            }
            else
            {
                Program.instance.cutin.lastSelectedCutinItem = null;
                ShowPreview(0, "No matching Battle Spirits cut-ins.");
            }
        }

        private void ItemOnListRefresh(string[] task, GameObject item)
        {
            var handler = item.GetComponent<SelectionToggle_Cutin>();
            if (handler == null || !int.TryParse(task[0], out int code)) return;
            handler.code = code;
            handler.cardName = task[1];
            handler.Refresh();
        }

        public void SelectCutin(SelectionToggle_Cutin item)
        {
            if (item == null || !CutinViewer.CutinExist(item.code)) return;
            if (superScrollView != null) superScrollView.selected = item.index;
            Program.instance.cutin.lastSelectedCutinItem = item;
            ShowPreview(item.code, item.cardName);
        }

        private void EnsurePreview()
        {
            if (previewImage != null) return;
            // The original screen reserves its left column for searching.
            // Add the selected artwork only to this private runtime instance.
            var area = new GameObject("Battle Spirits Cutin Preview", typeof(RectTransform));
            area.layer = gameObject.layer;
            area.transform.SetParent(Root, false);
            var rect = area.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(.32f, .26f);
            rect.anchorMax = new Vector2(.94f, .86f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var artwork = new GameObject("Selected Boss Cutout", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            artwork.layer = gameObject.layer;
            artwork.transform.SetParent(rect, false);
            previewImage = artwork.GetComponent<RawImage>();
            previewImage.raycastTarget = false;
            previewImage.rectTransform.anchorMin = Vector2.zero;
            previewImage.rectTransform.anchorMax = Vector2.one;
            previewImage.rectTransform.offsetMin = Vector2.zero;
            previewImage.rectTransform.offsetMax = Vector2.zero;
            artwork.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            artwork.SetActive(false);

            var caption = new GameObject("Selected Boss Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            caption.layer = gameObject.layer;
            caption.transform.SetParent(Root, false);
            previewName = caption.GetComponent<TextMeshProUGUI>();
            previewName.font = Title.font;
            previewName.color = Color.white;
            previewName.fontSize = 30;
            previewName.enableAutoSizing = true;
            previewName.fontSizeMin = 22;
            previewName.fontSizeMax = 30;
            previewName.textWrappingMode = TextWrappingModes.Normal;
            previewName.alignment = TextAlignmentOptions.Center;
            previewName.raycastTarget = false;
            previewName.rectTransform.anchorMin = new Vector2(.32f, .17f);
            previewName.rectTransform.anchorMax = new Vector2(.94f, .25f);
            previewName.rectTransform.offsetMin = Vector2.zero;
            previewName.rectTransform.offsetMax = Vector2.zero;
            ButtonAutoPlay.GetSelectable().interactable = false;
        }

        private void ShowPreview(int code, string cardName)
        {
            EnsurePreview();
            selectedCode = code;
            previewName.text = cardName;
            string id = BattleCatalog.GetCardId(code);
            var texture = BattleCosmetics.LoadTexture(BattleCatalog.StandeePath(id))
                ?? BattleCosmetics.LoadTexture(BattleCatalog.CardImagePath(id));
            previewImage.texture = texture;
            previewImage.gameObject.SetActive(texture != null);
            if (texture != null)
                previewImage.GetComponent<AspectRatioFitter>().aspectRatio = (float)texture.width / texture.height;
            ButtonAutoPlay.GetSelectable().interactable = CutinViewer.CutinExist(code);
        }

        public void FocusOnInputField()
        {
            Input.ActivateInputField();
        }

        public void OnAutoPlay()
        {
            Program.instance.cutin.AutoPlay();
        }

        public void SelectLastCutinItem()
        {
            UserInput.NextSelectionIsAxis = true;
            if (Program.instance.cutin.lastSelectedCutinItem != null)
                Program.instance.cutin.lastSelectedCutinItem.GetSelectable().Select();
        }

        protected override void OnDestroy()
        {
            disposed = true;
            if (superScrollView?.scrollRect != null && superScrollView.scrollRect.verticalScrollbar != null)
                superScrollView.scrollRect.verticalScrollbar.onValueChanged.RemoveListener(superScrollView.OnScrollBarChange);
            if (itemHandle.IsValid()) Addressables.Release(itemHandle);
            base.OnDestroy();
        }

    }
}
