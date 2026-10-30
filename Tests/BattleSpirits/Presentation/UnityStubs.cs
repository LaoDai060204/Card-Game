// Controlled API stand-ins for compiling and testing presentation flow without an Editor.
// These do not test Unity rendering, layout calculation, input modules or AssetBundles.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace UnityEngine {
 public class Object {
  internal bool destroyed;public string name;
  public static void Destroy(Object value){if(ReferenceEquals(value,null)||value.destroyed)return;if(value is GameObject go){foreach(var c in go.GetComponentsInChildren<Component>(true).Reverse().ToArray())if(!c.destroyed){c.GetType().GetMethod("OnDestroy",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic)?.Invoke(c,null);c.destroyed=true;}go.transform.SetParent(null,false);}value.destroyed=true;}
  public static bool operator==(Object a,Object b){bool an=ReferenceEquals(a,null)||(!ReferenceEquals(a,null)&&a.destroyed);bool bn=ReferenceEquals(b,null)||(!ReferenceEquals(b,null)&&b.destroyed);return an&&bn||!an&&!bn&&ReferenceEquals(a,b);}public static bool operator!=(Object a,Object b)=>!(a==b);public override bool Equals(object o)=>ReferenceEquals(this,o);public override int GetHashCode()=>base.GetHashCode();
 }
 public class Component:Object {public GameObject gameObject;public Transform transform=>gameObject.transform;public new string name {get=>gameObject.name;set=>gameObject.name=value;}public T GetComponent<T>() where T:Component=>gameObject.GetComponent<T>();public T[] GetComponentsInChildren<T>(bool includeInactive=false)where T:Component=>gameObject.GetComponentsInChildren<T>(includeInactive);public T GetComponentInChildren<T>(bool includeInactive=false)where T:Component=>GetComponentsInChildren<T>(includeInactive).FirstOrDefault();public T GetComponentInParent<T>()where T:Component{for(var p=transform;p!=null;p=p.parent){var c=p.GetComponent<T>();if(c!=null)return c;}return null;}}
 public class Behaviour:Component{public bool enabled=true;}public class MonoBehaviour:Behaviour{}
 public class GameObject:Object {public static readonly List<GameObject> All=new List<GameObject>();public int layer;public Transform transform;public bool activeSelf=true;public bool activeInHierarchy=>activeSelf&&(transform.parent==null||transform.parent.gameObject.activeInHierarchy);private readonly List<Component> components=new List<Component>();public GameObject(string n,params Type[] types){name=n;var t=types.FirstOrDefault(x=>typeof(Transform).IsAssignableFrom(x))??typeof(Transform);transform=(Transform)Activator.CreateInstance(t);transform.gameObject=this;components.Add(transform);All.Add(this);foreach(var type in types)if(!typeof(Transform).IsAssignableFrom(type))AddComponent(type);}public Component AddComponent(Type type){var c=(Component)Activator.CreateInstance(type);c.gameObject=this;components.Add(c);return c;}public T AddComponent<T>()where T:Component,new()=>(T)AddComponent(typeof(T));public T GetComponent<T>()where T:Component=>components.OfType<T>().FirstOrDefault(x=>!x.destroyed);public T[] GetComponentsInChildren<T>(bool includeInactive=false)where T:Component=>components.OfType<T>().Where(c=>!c.destroyed&&(includeInactive||activeInHierarchy)).Concat(transform.children.SelectMany(c=>c.gameObject.GetComponentsInChildren<T>(includeInactive))).ToArray();public void SetActive(bool value)=>activeSelf=value;}
 public class Transform:Component,IEnumerable<Transform>{public Transform parent;internal readonly List<Transform> children=new List<Transform>();public Vector3 position,localPosition,localEulerAngles,localScale;public Quaternion localRotation;public int childCount=>children.Count;public void SetParent(Transform p,bool worldPositionStays){parent?.children.Remove(this);parent=p;p?.children.Add(this);}public Transform GetChild(int index)=>children[index];public void SetAsFirstSibling(){if(parent!=null){parent.children.Remove(this);parent.children.Insert(0,this);}}public void SetAsLastSibling(){if(parent!=null){parent.children.Remove(this);parent.children.Add(this);}}public Transform Find(string path){var node=this;foreach(string name in path.Split('/')){node=node.children.FirstOrDefault(c=>c.name==name);if(node==null)break;}return node;}public IEnumerator<Transform> GetEnumerator()=>children.ToArray().AsEnumerable().GetEnumerator();IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();}
 public class RectTransform:Transform {public Vector2 anchorMin,anchorMax,offsetMin,offsetMax,sizeDelta,pivot,anchoredPosition;public Rect rect=new Rect{width=220,height=44};}
 public struct Rect {public float width,height;}
 public struct Vector2{public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 zero=>default;public static Vector2 one=>new Vector2(1,1);}
 public struct Vector3{public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 zero=>default;public static Vector3 one=>new Vector3(1,1,1);}
 public struct Quaternion{public static Quaternion identity=>default;public static Quaternion Euler(float x,float y,float z)=>default;}
 public struct Color{public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color white=>new Color(1,1,1,1);public static Color clear=>default;}
 public class Material:Object{}public class Sprite:Object{}public class Texture:Object{}
 public enum TextureFormat{RGBA32}public class Texture2D:Texture{public static readonly Texture2D blackTexture=new Texture2D(1,1,TextureFormat.RGBA32,false);public Texture2D(int width,int height,TextureFormat format,bool mipmap){}public bool LoadImage(byte[] data,bool markNonReadable=false)=>data!=null&&data.Length>0;}
 public enum CameraClearFlags{SolidColor}public enum FilterMode{Bilinear}public enum RenderTextureFormat{ARGB32}public enum LightType{Directional}public enum LightShadows{None}
 public class RenderTexture:Texture{public bool released,created;public FilterMode filterMode;public int antiAliasing;public RenderTexture(int a,int b,int c,RenderTextureFormat d){}public void Create()=>created=true;public void Release()=>released=true;}
 public class Camera:Behaviour{public float fieldOfView,nearClipPlane,farClipPlane;public int cullingMask;public CameraClearFlags clearFlags;public Color backgroundColor;public bool allowHDR,allowMSAA;public RenderTexture targetTexture;}
 public class Light:Behaviour{public LightType type;public Color color;public float intensity;public LightShadows shadows;public int cullingMask;}
 public class Canvas:Behaviour{public bool overrideSorting;public int sortingOrder;public Camera worldCamera;}
 public class RectOffset{public RectOffset(int l,int r,int t,int b){}}
 public enum TextAnchor{UpperLeft,UpperCenter}public enum KeyCode{Escape}public static class Input{public static bool GetKeyDown(KeyCode key)=>throw new InvalidOperationException("Legacy Input is disabled: activeInputHandler is Input System package.");}
 public static class Mathf{public static float Abs(float f)=>Math.Abs(f);public static float Max(float a,float b)=>Math.Max(a,b);}
 public static class Application{public static string dataPath,productName="Battle Spirits",version="0.1.0";}
 public static class Debug{public static readonly List<string> Logs=new List<string>();public static void Log(object o)=>Logs.Add(o?.ToString());public static void LogWarning(object o)=>Logs.Add(o?.ToString());public static void LogError(object o)=>Logs.Add(o?.ToString());}
}
namespace UnityEngine.InputSystem {
 public class Keyboard{public static Keyboard current;public Controls.KeyControl escapeKey=new Controls.KeyControl();}
}
namespace UnityEngine.InputSystem.Controls {public class KeyControl{public bool wasPressedThisFrame;}}
namespace UnityEngine.Events {public delegate void UnityAction();public delegate void UnityAction<T>(T arg);public class UnityEvent{private event UnityAction list;public void AddListener(UnityAction action)=>list+=action;public void Invoke()=>list?.Invoke();}public class UnityEvent<T>{private event UnityAction<T> list;public void AddListener(UnityAction<T> action)=>list+=action;public void Invoke(T arg)=>list?.Invoke(arg);}}
namespace UnityEngine.UI {
 public class Graphic:UnityEngine.Behaviour{public UnityEngine.Color color;public bool raycastTarget;public UnityEngine.Material material;public UnityEngine.RectTransform rectTransform=>transform as UnityEngine.RectTransform;}
 public class Image:Graphic{public enum Type{Simple,Sliced}public UnityEngine.Sprite sprite;public Type type;public float fillAmount;public bool preserveAspect;}
 public class RawImage:Graphic{public UnityEngine.Texture texture;}
 public struct ColorBlock{public UnityEngine.Color normalColor,highlightedColor,selectedColor,pressedColor,disabledColor;public float fadeDuration;}
 public class Selectable:UnityEngine.Behaviour{public bool interactable=true;public Graphic targetGraphic;public ColorBlock colors;}
 public class Button:Selectable{public UnityEngine.Events.UnityEvent onClick=new UnityEngine.Events.UnityEvent();}
 public class Toggle:Selectable{public Graphic graphic;public bool isOn;}
 public class GraphicRaycaster:UnityEngine.Behaviour{}public class RectMask2D:UnityEngine.Behaviour{}
 public class LayoutElement:UnityEngine.Behaviour{public float minHeight,preferredHeight,minWidth,preferredWidth;}
 public class LayoutGroup:UnityEngine.Behaviour{public UnityEngine.RectOffset padding;public UnityEngine.TextAnchor childAlignment;}
 public class HorizontalLayoutGroup:LayoutGroup{public float spacing;public bool childControlHeight,childControlWidth,childForceExpandWidth,childForceExpandHeight;}
 public class VerticalLayoutGroup:HorizontalLayoutGroup{}
 public class GridLayoutGroup:LayoutGroup{public enum Constraint{FixedColumnCount}public UnityEngine.Vector2 cellSize,spacing;public Constraint constraint;public int constraintCount;}
 public class ContentSizeFitter:UnityEngine.Behaviour{public enum FitMode{Unconstrained,PreferredSize}public FitMode verticalFit,horizontalFit;}
 public class ScrollRect:UnityEngine.Behaviour{public enum MovementType{Clamped}public UnityEngine.RectTransform viewport,content;public bool horizontal,vertical;public float scrollSensitivity;public MovementType movementType;}
 public class AspectRatioFitter:UnityEngine.Behaviour{public enum AspectMode{FitInParent}public AspectMode aspectMode;public float aspectRatio;}
 public class Outline:UnityEngine.Behaviour{public UnityEngine.Color effectColor;public UnityEngine.Vector2 effectDistance;}
}
namespace TMPro {
 public class TMP_FontAsset:UnityEngine.Object{}public static class TMP_Settings{public static TMP_FontAsset defaultFontAsset;}
 public enum TextAlignmentOptions{TopLeft,Center,MidlineLeft,MidlineRight,BottomLeft}public enum TextOverflowModes{Ellipsis}
 public class TextMeshProUGUI:UnityEngine.UI.Graphic{public string text;public int fontSize;public TMP_FontAsset font;public TextAlignmentOptions alignment;public bool enableWordWrapping,richText;public TextOverflowModes overflowMode;}
 public class TMP_InputField:UnityEngine.UI.Selectable{public enum LineType{SingleLine}public enum ContentType{IntegerNumber}public UnityEngine.RectTransform textViewport;public TextMeshProUGUI textComponent;public UnityEngine.UI.Graphic placeholder;public LineType lineType;public ContentType contentType;public string text;public int characterLimit;public bool richText;public UnityEngine.Events.UnityEvent<string> onValueChanged=new UnityEngine.Events.UnityEvent<string>();}
 public class TMP_Dropdown:UnityEngine.UI.Selectable{public class OptionData{public string text;public OptionData(string text){this.text=text;}}public TextMeshProUGUI captionText,itemText;public UnityEngine.RectTransform template;public List<OptionData> options=new List<OptionData>();public int value;public UnityEngine.Events.UnityEvent<int> onValueChanged=new UnityEngine.Events.UnityEvent<int>();public void RefreshShownValue(){if(captionText!=null&&options.Count>0)captionText.text=options[value].text;}}
}
namespace UnityEngine.EventSystems{public class BaseEventData{}public enum EventTriggerType{PointerEnter}public class EventTrigger:UnityEngine.Behaviour{public class Entry{public EventTriggerType eventID;public UnityEngine.Events.UnityEvent<BaseEventData> callback=new UnityEngine.Events.UnityEvent<BaseEventData>();}public List<Entry> triggers=new List<Entry>();}}
namespace DG.Tweening{public static class Extensions {public static void DOKill(this UnityEngine.Transform t){}public static void DOPunchPosition(this UnityEngine.Transform t,UnityEngine.Vector3 v,float d,int vibrato,float elasticity){}public static void DOPunchScale(this UnityEngine.Transform t,UnityEngine.Vector3 v,float d,int vibrato,float elasticity){}}}
namespace UnityEngine.ResourceManagement.AsyncOperations {public struct AsyncOperationHandle<T>{public Task<T> Task;public bool IsValid()=>Task!=null;}}
namespace UnityEngine.AddressableAssets {public static class Addressables{public static Func<string,Task<UnityEngine.GameObject>> Load=path=>Task.FromResult<UnityEngine.GameObject>(null);public static int Releases;public static UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<T> LoadAssetAsync<T>(string path){return new UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<T>{Task=Convert<T>(Load(path))};}private static async Task<T> Convert<T>(Task<UnityEngine.GameObject> task)=>(T)(object)await task;public static void Release<T>(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<T> handle)=>Releases++;}}
namespace UnityEngine.Rendering.Universal{public enum CameraRenderType{Base}public class UniversalAdditionalCameraData:UnityEngine.Component{public bool renderPostProcessing;public CameraRenderType renderType;public void SetRenderer(int n){}}}
namespace UnityEngine.Networking{}
namespace YgomSystem.ElementSystem{public class ElementObjectManager:UnityEngine.Component{public T GetElement<T>(string key)where T:UnityEngine.Component=>GetComponentsInChildren<T>(true).FirstOrDefault(x=>x.name==key);}}
namespace MDPro3 {
 public static class Config{public const string STRING_YES="1";public static readonly Dictionary<string,string> Values=new Dictionary<string,string>();public static string Get(string key,string fallback)=>Values.TryGetValue(key,out var v)?v:fallback;public static void Set(string key,string value)=>Values[key]=value;public static void Save(){}}
 public class Program:UnityEngine.MonoBehaviour{public static Program instance;public static Items items;public static bool exitOnReturn;public UIManager ui_;public Servant.Servant menu,online,puzzlePortal,replay,cutin,mate,setting;public Servant.SoloSelector solo;public Servant.DeckSelector deckSelector;public void ShiftToServant(Servant.Servant servant){Shifted=servant;}public Servant.Servant Shifted;public static void GameQuit(){}}
 public class UIManager:UnityEngine.MonoBehaviour{public TMPro.TMP_FontAsset tmpFont;public static UnityEngine.MonoBehaviour InputBlocker;public static void ShowPopupYesOrNo(List<string> text,Action yes,Action no){}}
 public static class InterString{public static string Get(string value)=>value;}
 public static class MessageManager{public static string Last;public static void Cast(string text)=>Last=text;}
 public class NewsManager:UnityEngine.MonoBehaviour{}
 public class Items{public const int CODE_NONE=0;public struct Item{public int id;public string path;public bool notReady;}public List<Item> mats=new List<Item>(),mates=new List<Item>();}
 public class Mate:UnityEngine.MonoBehaviour{public enum MateAction{Entry,Attack,GetDamage,Victory,Defeat}public UnityEngine.Transform parent;public readonly List<MateAction> Played=new List<MateAction>();public void Play(MateAction a)=>Played.Add(a);}
 public static class ABLoader{public static Func<string,Task<UnityEngine.GameObject>> Field=p=>Task.FromResult(new UnityEngine.GameObject(p));public static Func<int,Task<Mate>> Pet=id=>Task.FromResult(new UnityEngine.GameObject("Mate").AddComponent<Mate>());public static Task<UnityEngine.GameObject> LoadFromFileAsync(string p,bool cache,bool instantiate)=>Field(p);public static Task<Mate> LoadMateAsync(int id)=>Pet(id);}
 public static class Tools{public static void PlayAnimation(UnityEngine.Transform t,string name){}}
}
namespace MDPro3.Net{}
namespace MDPro3.Servant{public class Servant:UnityEngine.MonoBehaviour{public UI.ServantUI.ServantUI servantUI;public T GetUI<T>()where T:UI.ServantUI.ServantUI=>servantUI as T;}public class SoloSelector:Servant{public enum Condition{ForSolo}public void SwitchCondition(Condition c){}}public class DeckSelector:Servant{public enum Condition{ForEdit}public void SwitchCondition(Condition c){}}}
namespace MDPro3.UI{public class SelectionButton:UnityEngine.MonoBehaviour{}}
namespace MDPro3.UI.ServantUI{public class ServantUI:UnityEngine.MonoBehaviour{public TMPro.TextMeshProUGUI Title;public YgomSystem.ElementSystem.ElementObjectManager Manager;public virtual void Initialize(MDPro3.Servant.Servant servant){}}}
