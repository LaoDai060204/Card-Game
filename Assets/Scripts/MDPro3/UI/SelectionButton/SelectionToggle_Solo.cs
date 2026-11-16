using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MDPro3.Servant;
using MDPro3.UI.ServantUI;
using MDPro3.UI.PropertyOverride;

namespace MDPro3.UI
{
    public class SelectionToggle_Solo : SelectionToggle_ScrollRectItem
    {

        #region Elements

        private const string LABEL_TXT_TITLE = "Title";
        private TextMeshProUGUI m_Title;
        private TextMeshProUGUI Title =>
            m_Title = m_Title != null ? m_Title
            : Manager.GetElement<TextMeshProUGUI>(LABEL_TXT_TITLE);

        private const string LABEL_ART = "Image";
        private ArtRawImageHandler m_Art;
        private ArtRawImageHandler Art =>
            m_Art = m_Art != null ? m_Art
            : Manager.GetElement<ArtRawImageHandler>(LABEL_ART);

        private const string LABEL_GO_NUMBADGE = "NumBadge";
        private GameObject m_NumBadge;
        private GameObject NumBadge =>
            m_NumBadge = m_NumBadge != null ? m_NumBadge
            : Manager.GetElement(LABEL_GO_NUMBADGE);

        private const string LABEL_GO_TEXTCLEAR = "TextClear";
        private GameObject m_TextClear;
        private GameObject TextClear =>
            m_TextClear = m_TextClear != null ? m_TextClear
            : Manager.GetElement(LABEL_GO_TEXTCLEAR);

        #endregion

        public SoloSelector.BotInfo botInfo;
        private bool isDiyDeck;
        private Texture lastBannerTexture;
        private Vector2 lastBannerSize;

        public override void Refresh()
        {
            base.Refresh();
            if (botInfo == null) return;
            Title.text = botInfo.name;
            // The retained name strip uses a layout group. Opt this title out
            // before applying a bounded two-line area on this runtime row.
            var titleLayout = Title.GetComponent<LayoutElement>();
            if (titleLayout != null) titleLayout.ignoreLayout = true;
            var titleArea = Title.transform.parent as RectTransform;
            if (titleArea != null)
                titleArea.sizeDelta = new Vector2(titleArea.sizeDelta.x, PropertyOverrider.NeedMobileLayout() ? 76f : 64f);
            Title.enableAutoSizing = true;
            Title.fontSizeMin = 22;
            Title.fontSizeMax = 26;
            Title.fontSize = 26;
            Title.textWrappingMode = TextWrappingModes.Normal;
            Title.lineSpacing = 0;
            Title.overflowMode = TextOverflowModes.Ellipsis;
            Title.alignment = TextAlignmentOptions.MidlineLeft;
            Title.rectTransform.anchorMin = Vector2.zero;
            Title.rectTransform.anchorMax = Vector2.one;
            Title.rectTransform.offsetMin = new Vector2(18, 4);
            Title.rectTransform.offsetMax = new Vector2(-18, -4);
            Title.raycastTarget = false;
            isDiyDeck = false;
            Art.SetArt(botInfo.main0);
            var image = Art.GetComponent<RawImage>();
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = Vector2.zero;
            image.rectTransform.offsetMax = Vector2.zero;
            image.raycastTarget = false;
            lastBannerTexture = null;

            NumBadge.SetActive(false);
            TextClear.SetActive(false);
        }

        private void LateUpdate()
        {
            var image = Art.GetComponent<RawImage>();
            if (image == null || image.texture == null) return;
            var size = image.rectTransform.rect.size;
            if (image.texture == lastBannerTexture && size == lastBannerSize) return;
            if (size.x <= 0 || size.y <= 0) return;
            lastBannerTexture = image.texture;
            lastBannerSize = size;
            image.uvRect = ArtworkViewport((float)image.texture.width / image.texture.height, size.x / size.y);
        }

        internal static Rect ArtworkViewport(float textureAspect, float viewAspect)
        {
            // Crop the central artwork on the full printed face. Keep the source
            // proportions; never squeeze the entire tall card into a wide banner.
            var art = new Rect(.12f, .54f, .80f, .40f);
            if (textureAspect <= 0 || viewAspect <= 0) return art;
            float aspect = art.width * textureAspect / art.height;
            if (aspect < viewAspect)
            {
                float height = art.width * textureAspect / viewAspect;
                art.y += (art.height - height) * .5f;
                art.height = height;
            }
            else
            {
                float width = art.height * viewAspect / textureAspect;
                art.x += (art.width - width) * .5f;
                art.width = width;
            }
            return art;
        }

        protected override void CallToggleOnEvent()
        {
            base.CallToggleOnEvent();
            var ui = Program.instance.solo.GetUI<SoloSelectorUI>();
            if (ui.superScrollView == null || botInfo == null) return;
            ui.superScrollView.selected = index;
            ui.SetOverview(botInfo.desc, isDiyDeck);
            Program.instance.solo.lastSoloItem = this;
        }

        protected override void CallSubmitEvent()
        {
            base.CallSubmitEvent();
            if (SoloSelector.condition == SoloSelector.Condition.ForSolo)
                Program.instance.solo.StartAIForSolo(index, isDiyDeck);
            else
                Program.instance.solo.StartAIForRoom(index, isDiyDeck);
        }

        public void PublicSubmit()
        {
            CallSubmitEvent();
        }

        protected override void OnNavigation(AxisEventData eventData)
        {
            base.OnNavigation(eventData);

            if (eventData.moveDir == MoveDirection.Right)
            {
                UserInput.NextSelectionIsAxis = true;
                Program.instance.solo.GetUI<SoloSelectorUI>().SelectOnRight();
            }
        }
    }
}
