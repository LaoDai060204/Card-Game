using System;
using System.Collections;
using UnityEngine;

namespace MDPro3.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class ConditionalAlignmentController : MonoBehaviour
    {
        [Serializable]
        public struct AlignmentPreset
        {
            [Tooltip("Minimum anchor (bottom left)")]
            public Vector2 anchorMin;
            [Tooltip("Maximum anchor (top right)")]
            public Vector2 anchorMax;
            [Tooltip("Offset relative to the anchors")]
            public Vector2 anchoredPosition;
        }

        [Header("Child configuration")]
        [SerializeField] private RectTransform targetChild;

        [Header("Layout parameters")]
        [SerializeField, Tooltip("Width threshold for switching layouts")]
        private float widthThreshold = 512f;

        [Header("Wide-screen layout")]
        [SerializeField]
        private AlignmentPreset wideScreenPreset = new()
        {
            anchorMin = new Vector2(0.5f, 0.5f),
            anchorMax = new Vector2(0.5f, 0.5f),
            anchoredPosition = Vector2.zero
        };

        [Header("Narrow-screen layout")]
        [SerializeField]
        private AlignmentPreset narrowScreenPreset = new AlignmentPreset
        {
            anchorMin = new Vector2(0f, 0.5f),
            anchorMax = new Vector2(0f, 0.5f),
            anchoredPosition = new Vector2(256f, 0)
        };

        private RectTransform _parentRect;

        private void Awake() => Initialize();

        //private void OnValidate() => Initialize();

        private void OnEnable()
        {
            SystemEvent.OnResolutionChange += Initialize;
        }

        private void OnDisable()
        {
            SystemEvent.OnResolutionChange -= Initialize;
        }


        private void Initialize()
        {
            if (_parentRect == null)
                _parentRect = GetComponent<RectTransform>();

            StartCoroutine(UpdateChildAlignment());
        }

        private IEnumerator UpdateChildAlignment()
        {
            if (targetChild == null) yield break;

            while (_parentRect.rect.width < 0f)
                yield return null;
            var preset = _parentRect.rect.width > 
                widthThreshold ? wideScreenPreset : narrowScreenPreset;
            targetChild.anchorMin = preset.anchorMin;
            targetChild.anchorMax = preset.anchorMax;
            targetChild.anchoredPosition = preset.anchoredPosition;
        }
    }
}