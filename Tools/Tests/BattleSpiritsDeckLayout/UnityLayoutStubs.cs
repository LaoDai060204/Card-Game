// Narrow API doubles for the extracted production layout methods. Rect size
// follows anchors and parent size; the fixture does not emulate Unity rendering.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector2 : IEquatable<Vector2>
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 one => new Vector2(1, 1);
        public bool Equals(Vector2 other) => x == other.x && y == other.y;
        public override bool Equals(object value) => value is Vector2 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(x, y);
        public static bool operator ==(Vector2 left, Vector2 right) => left.Equals(right);
        public static bool operator !=(Vector2 left, Vector2 right) => !left.Equals(right);
    }
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public Vector2 size => new Vector2(width, height);
    }
    public class Component
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();
    }
    public class Transform : Component
    {
        public Transform parent;
        public readonly List<Transform> children = new List<Transform>();
        public int childCount => children.Count;
        public Transform GetChild(int index) => children[index];
        public void SetParent(Transform value, bool worldPositionStays = false)
        {
            parent?.children.Remove(this); parent = value; parent?.children.Add(this);
        }
    }
    public class RectTransform : Transform
    {
        public enum Axis { Horizontal, Vertical }
        public Vector2 anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta;
        private Vector2 minOffset, maxOffset;
        public Vector2 offsetMin { get => minOffset; set { minOffset = value; ApplyOffsets(); } }
        public Vector2 offsetMax { get => maxOffset; set { maxOffset = value; ApplyOffsets(); } }
        private void ApplyOffsets() { sizeDelta = new Vector2(maxOffset.x - minOffset.x, maxOffset.y - minOffset.y); }
        public Rect rect
        {
            get
            {
                var parentSize = parent is RectTransform parentRect ? parentRect.rect.size : Vector2.zero;
                return new Rect(0, 0, parentSize.x * (anchorMax.x - anchorMin.x) + sizeDelta.x,
                    parentSize.y * (anchorMax.y - anchorMin.y) + sizeDelta.y);
            }
        }
        public void SetSizeWithCurrentAnchors(Axis axis, float size)
        {
            var parentSize = parent is RectTransform parentRect ? parentRect.rect.size : Vector2.zero;
            if (axis == Axis.Horizontal) sizeDelta.x = size - parentSize.x * (anchorMax.x - anchorMin.x);
            else sizeDelta.y = size - parentSize.y * (anchorMax.y - anchorMin.y);
        }
    }
    public class GameObject
    {
        public bool activeSelf = true;
        public readonly RectTransform transform;
        private readonly Dictionary<Type, Component> components = new Dictionary<Type, Component>();
        public GameObject()
        {
            transform = new RectTransform { gameObject = this };
            components[typeof(RectTransform)] = transform;
        }
        public void SetActive(bool value) { activeSelf = value; }
        public T GetComponent<T>() where T : class => components.TryGetValue(typeof(T), out var value) ? value as T : null;
        public T AddComponent<T>() where T : Component, new()
        {
            var value = new T { gameObject = this }; components[typeof(T)] = value; return value;
        }
    }
    public class CanvasGroup : Component { }
    public class Texture { public int width, height; }
    public static class Mathf
    {
        public static float Max(float left, float right) => Math.Max(left, right);
        public static int Max(int left, int right) => Math.Max(left, right);
        public static float Min(float left, float right) => Math.Min(left, right);
        public static int CeilToInt(float value) => (int)Math.Ceiling(value);
    }
}
namespace UnityEngine.UI
{
    using UnityEngine;
    public class RectMask2D : Component { }
    public class RectOffset { public int left, right, top, bottom; }
    public class GridLayoutGroup : Component
    {
        public enum Constraint { Flexible, FixedColumnCount, FixedRowCount }
        public Vector2 cellSize, spacing;
        public Constraint constraint;
        public int constraintCount;
        public RectOffset padding = new RectOffset();
    }
    public class ScrollRect : Component
    {
        public RectTransform content, viewport;
        public bool horizontal, vertical, enabled;
        public float scrollSensitivity;
    }
    public class RawImage : Component
    {
        public Texture texture;
        public object material;
        public Rect uvRect;
    }
    public class AspectRatioFitter : Component
    {
        public enum AspectMode { None, WidthControlsHeight, HeightControlsWidth, FitInParent, EnvelopeParent }
        public AspectMode aspectMode;
        public float aspectRatio;
    }
}
public class UIScrollToSelection : UnityEngine.Component { }
public static class DeckEditor { public static bool UseMobileLayout; }
