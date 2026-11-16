// Narrow, deterministic Unity/native-layout API doubles. These do not run Unity.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace UnityEngine
{
    public class Object
    {
        public string name;
        public static void Destroy(Object obj) { if (obj is GameObject go) go.SetActive(false); }
        public static T Instantiate<T>(T original, Transform parent, bool world = false) where T : Object
        {
            if (original is TMPro.TextMeshProUGUI prototype)
            {
                var clone = new GameObject(prototype.name ?? "text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                clone.transform.SetParent(parent, world); return clone.GetComponent<TMPro.TextMeshProUGUI>() as T;
            }
            if (original is GameObject)
            {
                var clone = new GameObject("row", typeof(RectTransform), typeof(UI.Button), typeof(UI.Text));
                clone.transform.SetParent(parent, world); return clone as T;
            }
            throw new InvalidOperationException("Unsupported test clone: " + original?.GetType().Name);
        }
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
        public T[] GetComponentsInChildren<T>(bool includeInactive = false) where T : Component => gameObject.GetComponentsInChildren<T>(includeInactive);
        public T GetComponentInChildren<T>(bool includeInactive = false) where T : Component => GetComponentsInChildren<T>(includeInactive).FirstOrDefault();
    }
    public class Behaviour : Component { public bool enabled = true; }
    public class MonoBehaviour : Behaviour { }
    public class GameObject : Object
    {
        public bool activeSelf = true;
        public Transform transform;
        private readonly List<Component> components = new List<Component>();
        public GameObject(string name, params Type[] types)
        {
            this.name = name;
            transform = types.Contains(typeof(RectTransform)) ? new RectTransform() : new Transform();
            transform.gameObject = this; components.Add(transform);
            foreach (var type in types) if (type != typeof(RectTransform)) AddComponent(type);
        }
        private Component AddComponent(Type type)
        {
            var component = (Component)Activator.CreateInstance(type); component.gameObject = this;
            components.Add(component); return component;
        }
        public T AddComponent<T>() where T : Component, new() => (T)AddComponent(typeof(T));
        public T GetComponent<T>() where T : Component => components.OfType<T>().FirstOrDefault();
        public T GetComponentInChildren<T>(bool includeInactive = false) where T : Component => GetComponentsInChildren<T>(includeInactive).FirstOrDefault();
        public T[] GetComponentsInChildren<T>(bool includeInactive = false) where T : Component => components.OfType<T>()
            .Concat(transform.Children.SelectMany(c => c.gameObject.GetComponentsInChildren<T>(includeInactive))).ToArray();
        public void SetActive(bool active) => activeSelf = active;
    }
    public class Transform : Component
    {
        public Transform parent;
        public readonly List<Transform> Children = new List<Transform>();
        public Vector3 localScale, localEulerAngles;
        public void SetParent(Transform value, bool world = false) { parent?.Children.Remove(this); parent = value; value?.Children.Add(this); }
        public void SetAsFirstSibling() { if (parent == null) return; parent.Children.Remove(this); parent.Children.Insert(0, this); }
        public void SetAsLastSibling() { if (parent == null) return; parent.Children.Remove(this); parent.Children.Add(this); }
    }
    public class RectTransform : Transform
    {
        public Vector2 anchorMin, anchorMax, offsetMin, offsetMax, pivot, anchoredPosition, sizeDelta;
        public Vector3 anchoredPosition3D;
    }
    public struct Vector2
    {
        public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0); public static Vector2 one => new Vector2(1, 1);
    }
    public struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } public static Vector3 zero => new Vector3(); }
    public struct Color { public static Color white => new Color(); }
    public class Texture : Object { }
    public class Texture2D : Texture { }
    public class CanvasGroup : Behaviour { public float alpha; public bool blocksRaycasts; }
    public class SerializeField : Attribute { }
    public class RequireComponent : Attribute { public RequireComponent(Type type) { } }
    public class HideInInspector : Attribute { }
    public class DisallowMultipleComponent : Attribute { }
    public class Header : Attribute { public Header(string value) { } }
    public static class Application { public static string streamingAssetsPath, persistentDataPath; }
    public static class Time { public static float unscaledTime; }
    public static class Debug { public static string LastError; public static void LogError(object error) => LastError = error?.ToString(); }
    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Overflow }
}
namespace UnityEngine.Events
{
    public delegate void UnityAction();
    public enum UnityEventCallState { Off, RuntimeOnly }
    public abstract class UnityEventBase
    {
        protected readonly List<Tuple<UnityAction, UnityEventCallState>> persistent = new List<Tuple<UnityAction, UnityEventCallState>>();
        protected readonly List<UnityAction> runtime = new List<UnityAction>();
        public int GetPersistentEventCount() => persistent.Count;
        public void SetPersistentListenerState(int index, UnityEventCallState state) => persistent[index] = Tuple.Create(persistent[index].Item1, state);
        public void RemoveAllListeners() => runtime.Clear();
        public void AddPersistent(UnityAction action) => persistent.Add(Tuple.Create(action, UnityEventCallState.RuntimeOnly));
    }
    public class UnityEvent : UnityEventBase
    {
        public void AddListener(UnityAction action) => runtime.Add(action);
        public void Invoke()
        {
            foreach (var pair in persistent.ToArray()) if (pair.Item2 != UnityEventCallState.Off) pair.Item1();
            foreach (var action in runtime.ToArray()) action();
        }
    }
}
namespace UnityEngine.UI
{
    using UnityEngine;
    public class Selectable : Behaviour { public bool interactable = true; public Navigation navigation; public void Select() { } }
    public struct Navigation
    {
        public enum Mode { Explicit }
        public Mode mode; public Selectable selectOnUp, selectOnDown, selectOnLeft, selectOnRight;
    }
    public class Graphic : Behaviour { public bool raycastTarget; public RectTransform rectTransform => (RectTransform)transform; }
    public class Image : Graphic { }
    public class RawImage : Graphic { }
    public class Text : Graphic { public string text; public int fontSize; public float preferredHeight => Math.Max(1, (text?.Length ?? 0) / 60 + 1) * Math.Max(1, fontSize); public HorizontalWrapMode horizontalOverflow; public VerticalWrapMode verticalOverflow; }
    public class Button : Selectable { public Graphic targetGraphic; public Events.UnityEvent onClick = new Events.UnityEvent(); }
    public class ScrollRect : Behaviour { public RectTransform content; public float verticalNormalizedPosition; }
}
namespace TMPro
{
    public enum TextAlignmentOptions { Center }
    public class TextMeshProUGUI : UnityEngine.UI.Graphic { public string text; public float fontSize; public TextAlignmentOptions alignment; }
}
namespace UnityEngine.AddressableAssets
{
    public static class Addressables
    {
        public static Func<string, UnityEngine.GameObject> Factory;
        public static Handle InstantiateAsync(string path) => new Handle { Result = Factory(path) };
    }
    public class Handle { public UnityEngine.GameObject Result; public Task Task => System.Threading.Tasks.Task.CompletedTask; }
}
namespace YgomSystem.ElementSystem
{
    public class ElementObjectManager : UnityEngine.MonoBehaviour
    {
        public readonly Dictionary<string, UnityEngine.GameObject> Elements = new Dictionary<string, UnityEngine.GameObject>();
        public UnityEngine.GameObject GetElement(string key) => Elements.TryGetValue(key, out var obj) ? obj : null;
        public T GetElement<T>(string key) where T : UnityEngine.Component => GetElement(key)?.GetComponent<T>();
    }
}
namespace MDPro3
{
    public static class Config
    {
        public const string STRING_YES = "1";
        public static int Saves;
        private static readonly Dictionary<string, string> values = new Dictionary<string, string>();
        public static bool GetBool(string key, bool fallback) => values.TryGetValue(key, out var value) ? value == "1" : fallback;
        public static string Get(string key, string fallback = "") => values.TryGetValue(key, out var value) ? value : fallback;
        public static string GetConfigDeckName() => Get("DeckName");
        public static void Set(string key, string value) => values[key] = value;
        public static void Save() { Saves++; }
    }
    public static class MessageManager { public static string LastMessage; public static void Cast(string message) => LastMessage = message; }
    public class CardDescription : UnityEngine.MonoBehaviour { }
    public static class UIManager
    {
        public static object Inspected;
        public static void ShowCardInfoDetail(object card) => Inspected = card;
        public static void HideExitButton(float time) { } public static void HideLine(float time) { }
        public static void HideWallpaper(float time) { } public static void ShowBlackBack(float alpha, float time) { }
    }
    public class Program : UnityEngine.MonoBehaviour
    {
        public static readonly Program instance = new UnityEngine.GameObject("Program").AddComponent<Program>();
        public Servant.Servant menu;
        public Servant.Servant setting;
        public Servant.Servant currentServant, currentSubServant;
        public Servant.Servant LastServant;
        public int Registered;
        public void RegisterServant(Servant.Servant servant) => Registered++;
        public void ShiftToServant(Servant.Servant servant) { LastServant = currentServant = servant; }
        public void ShowSubServant(Servant.Servant servant) => LastServant = servant;
    }
}
namespace MDPro3.Servant
{
    public class Servant : UnityEngine.MonoBehaviour
    {
        public MDPro3.UI.ServantUI.ServantUI servantUI;
        public Servant returnServant;
        public bool inTransition;
        public virtual int Depth => 1;
        protected virtual string Label_UI => "";
        protected virtual bool NeedExitButton => true;
        public virtual float TransitionTime => .4f;
        public virtual void OnReturn() { }
        public virtual bool NeedResponseInput() => true;
        public void Select() { }
        protected virtual void FirstLoadEvent() { }
        protected virtual void ApplyShowArrangement(int depth) { }
        protected virtual void ApplyHideArrangement(int depth) { }
        protected virtual void AfterShowingEvent() { }
        protected virtual void OnMouseCursorHide() { }
    }
}
namespace MDPro3.UI.ServantUI
{
    public class ServantUI : UnityEngine.MonoBehaviour
    {
        protected MDPro3.Servant.Servant parentServant;
        public YgomSystem.ElementSystem.ElementObjectManager Manager => GetComponent<YgomSystem.ElementSystem.ElementObjectManager>();
        public UnityEngine.RectTransform Root => Manager.GetElement<UnityEngine.RectTransform>("Root");
        public virtual void Initialize(MDPro3.Servant.Servant servant) => parentServant = servant;
        public virtual void ResetUI() { }
        public virtual void ShutDown() => gameObject.SetActive(false);
        public virtual void Show(bool cover) { }
        public virtual void ShowEvent() { }
        protected virtual void OnDestroy() { }
    }
    public class OcgCoreUI : ServantUI { }
}
namespace MDPro3.UI
{
    public class PhaseButtonHandler : UnityEngine.MonoBehaviour { }
    public class SelectionButton : UnityEngine.MonoBehaviour
    {
        protected SelectionButtonClickEvent clickEvent = new SelectionButtonClickEvent();
        public void Click() => clickEvent.onLeftClick.Invoke();
        public void AddOldPersistent(UnityEngine.Events.UnityAction call) => clickEvent.onLeftClick.AddPersistent(call);
    }
    public class SelectionButtonClickEvent { public UnityEngine.Events.UnityEvent onLeftClick = new UnityEngine.Events.UnityEvent(); }
    public class PopupDuelSelection : UnityEngine.MonoBehaviour
    {
        public UnityEngine.RectTransform window, baseRect;
        public UnityEngine.UI.Text title;
        public UnityEngine.UI.Button btnHide;
        public UnityEngine.UI.ScrollRect scrollRect;
        public UnityEngine.GameObject item;
        public float showY;
    }
}
namespace MDPro3.Utility
{
    public static class CardImageLoader
    {
        public static Task<UnityEngine.Texture2D> LoadBattleFaceAsync(string id, CancellationToken token) => Task.FromResult(new UnityEngine.Texture2D());
    }
}
namespace MDPro3.BattleSpirits
{
    using Core;
#if !REAL_SESSION
    public static class BattleCatalog
    {
        public static readonly Dictionary<string, CardDefinition> Cards = new Dictionary<string, CardDefinition>();
        public static bool TryGet(string id, out CardDefinition card) => Cards.TryGetValue(id, out card);
    }
#endif
    public static class BattleCardAdapter
    {
        public static string Colors(CardDefinition definition) => definition.color.ToString();
        public static string Description(CardDefinition definition) => definition.effectTextEn;
        public static object ToNative(CardDefinition definition) => definition;
        public static void FitFace(UnityEngine.UI.RawImage image, UnityEngine.Texture texture) { }
    }
#if !REAL_SESSION
    public static class BattleIntegration
    {
        public static int Shutdowns;
        public static void Shutdown() => Shutdowns++;
    }
    public sealed class BattleGameSession
    {
        public BattleSnapshot View;
        public IReadOnlyList<BattleCommand> LegalCommands = Array.Empty<BattleCommand>();
        public int Seat;
        public bool IsReplay, IsOnline, IsConnected = true;
        public bool PresentationBusy;
        public string Status = "Ready";
        public IReadOnlyList<string> Log = Array.Empty<string>();
        public Puzzles.PuzzleSession Puzzle;
        public event Action Changed;
        public event Action<BattleEvent> CommittedEvent;
        public BattleCommand Sent;
        public int ReplayDelta, Concessions, Resets, Retries;
        public void Change() => Changed?.Invoke();
        public void Commit(BattleEvent evt) => CommittedEvent?.Invoke(evt);
        public bool Send(BattleCommand command, out string error) { error = ""; Sent = command; Changed?.Invoke(); return true; }
        public void StepReplay(int delta) => ReplayDelta += delta;
        public bool ResetPuzzle() { Resets++; return true; }
        public bool Retry() { Retries++; return true; }
        public void Concede() => Concessions++;
    }
#endif
}
#if REAL_SESSION
namespace MDPro3.Duel.YGOSharp
{
    public class Deck
    {
        public readonly List<int> Main = new List<int>(), Extra = new List<int>(), Side = new List<int>();
    }
}
#endif
#if !REAL_SESSION
namespace MDPro3.BattleSpirits.Puzzles
{
    public class PuzzleSession { public PuzzleDefinition Definition = new PuzzleDefinition(); public string Status = "InProgress"; public string ResultMessage => "Puzzle in progress"; }
    public class PuzzleDefinition { public string title, description, hint; }
}
#endif
namespace UnityEngine.EventSystems
{
    public class EventTrigger : UnityEngine.MonoBehaviour
    {
        public readonly List<Entry> triggers = new List<Entry>();
        public class Entry { public UnityEngine.Events.UnityEvent callback = new UnityEngine.Events.UnityEvent(); }
    }
}
namespace MDPro3.BattleSpirits.Visuals
{
    public class BattleFieldRenderer : UnityEngine.MonoBehaviour
    {
        public event Action<int> CardSelected;
        public event Action<bool> PresentationBusyChanged;
        public bool IsPresentationBusy;
        public int Sets, Summons, Skips, Disposals;
        public BattlePresentationState State;
        public void Setup(UnityEngine.UI.RawImage image) { }
        public void SetState(BattlePresentationState value) { Sets++; State = value; }
        public void SetPreferences(bool reduced, bool muted) { }
        public void NotifyCommittedSummon(int instance, string eventId) { Summons++; Busy(true); }
        public void Busy(bool busy) { IsPresentationBusy = busy; PresentationBusyChanged?.Invoke(busy); }
        public void SkipPresentation() { Skips++; Busy(false); }
        public void SelectCard(int instance) => CardSelected?.Invoke(instance);
        public void Dispose() { Disposals++; Busy(false); }
    }
}
