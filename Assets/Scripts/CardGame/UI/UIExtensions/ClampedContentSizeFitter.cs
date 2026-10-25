using UnityEngine;
using UnityEngine.UI;

namespace CardGame.UI
{
    public class ClampedContentSizeFitter : ContentSizeFitter
    {
        [SerializeField] private float m_MaxWidth = -1f;
        public float maxWidth { get => m_MaxWidth; set => m_MaxWidth = value; }

        [SerializeField] private float m_MinWidth = 0f;
        public float minWidth { get => m_MinWidth; set => m_MinWidth = value; }

        private DrivenRectTransformTracker m_Tracker;
        private RectTransform m_Rect;
        private RectTransform RectTransform => m_Rect ??= GetComponent<RectTransform>();

        protected override void OnEnable()
        {
            base.OnEnable();
            SetDirty();
        }

        protected override void OnDisable()
        {
            m_Tracker.Clear();
            base.OnDisable();
        }

        public override void SetLayoutHorizontal()
        {
            m_Tracker.Clear();

            if (horizontalFit == FitMode.Unconstrained)
            {
                m_Tracker.Add(this, RectTransform, DrivenTransformProperties.None);
                return;
            }

            m_Tracker.Add(this, RectTransform, DrivenTransformProperties.SizeDeltaX);

            // Calculate the initial target width.
            float targetWidth = horizontalFit == FitMode.MinSize ?
                LayoutUtility.GetMinWidth(RectTransform) :
                LayoutUtility.GetPreferredWidth(RectTransform);

            // Apply minimum and maximum width constraints.
            if (m_MinWidth > 0)
                targetWidth = Mathf.Max(targetWidth, m_MinWidth); // Keep the width at or above the minimum.
            if (m_MaxWidth > 0)
                targetWidth = Mathf.Min(targetWidth, m_MaxWidth); // Keep the width at or below the maximum.

            RectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                targetWidth
            );
        }
    }
}