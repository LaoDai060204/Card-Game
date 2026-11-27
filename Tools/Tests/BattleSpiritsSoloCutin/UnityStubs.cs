// Test doubles for Unity/framework boundaries. Target controllers, row handlers
// and SuperScrollView are compiled directly from Assets by run.py.
using System;
using System.Collections.Generic;
using System.Linq;
namespace Mono.WebBrowser { }
namespace Unity.Mathematics { public static class math { public static float floor(float f)=>(float)Math.Floor(f); } }
namespace UnityEngine.Rendering { public class DebugUI { } }
namespace Cysharp.Threading.Tasks { public struct UniTask { public static UniTask CompletedTask=>default; } }
namespace UnityEngine {
public class HideInInspector:Attribute { }
public class Object {
 public string name;
 public bool destroyed;
 public static T Instantiate<T>(T original) where T:Object => (T)(Object)((GameObject)(Object)original).Factory();
 public static readonly List<Object> DestroyQueue=new();
 public static void Destroy(Object obj) { if(obj!=null)DestroyQueue.Add(obj); }
 public static void FlushDestroy() {foreach(var obj in DestroyQueue){obj.destroyed=true;if(obj is GameObject go){go.SetActive(false);go.transform.parent?.children.Remove(go.transform);foreach(var c in go.components.Values)c.destroyed=true;}}DestroyQueue.Clear();}
 public static bool operator ==(Object a,Object b)=>ReferenceEquals(a,b) || (ReferenceEquals(a,null)?b?.destroyed==true:ReferenceEquals(b,null)&&a.destroyed);
 public static bool operator !=(Object a,Object b)=>!(a==b);
 public override bool Equals(object o)=>ReferenceEquals(this,o); public override int GetHashCode()=>base.GetHashCode();
}
public class Component:Object { public GameObject gameObject; public Transform transform=>gameObject.transform; public T GetComponent<T>() where T:Component=>gameObject.GetComponent<T>(); public bool TryGetComponent<T>(out T value) where T:Component=>gameObject.TryGetComponent(out value); public T[] GetComponentsInChildren<T>(bool includeInactive=false) where T:Component=>gameObject.GetComponentsInChildren<T>(includeInactive); }
public class MonoBehaviour:Component { }
public class GameObject:Object {
 public Dictionary<Type,Component> components=new(); public Func<GameObject> Factory;
 public bool activeSelf=true; public bool activeInHierarchy=>activeSelf && (transform.parent==null || transform.parent.gameObject.activeInHierarchy);
 public Transform transform; public int layer;
 public GameObject(string name,params Type[] types) { this.name=name; transform=new RectTransform(); transform.gameObject=this; components[typeof(RectTransform)]=transform; foreach(var t in types) if(t!=typeof(RectTransform)&&t!=typeof(Transform))AddComponent(t); }
 public T AddComponent<T>() where T:Component,new()=>(T)AddComponent(typeof(T));
 public Component AddComponent(Type t) { var c=(Component)Activator.CreateInstance(t);c.gameObject=this; components[t]=c;return c; }
 public T GetComponent<T>() where T:Component=>components.Values.OfType<T>().FirstOrDefault();
 public bool TryGetComponent<T>(out T value) where T:Component {value=GetComponent<T>();return value!=null;}
 public void SetActive(bool active)=>activeSelf=active; public T[] GetComponentsInChildren<T>(bool includeInactive=false) where T:Component=>components.Values.OfType<T>().Concat(transform.children.SelectMany(c=>c.gameObject.GetComponentsInChildren<T>(includeInactive))).ToArray();
}
public class Transform:Component { public Transform parent; public List<Transform> children=new(); public int childCount=>children.Count; public Transform GetChild(int i)=>children[i]; public void SetParent(Transform p,bool world=false){parent?.children.Remove(this);parent=p;p?.children.Add(this);} public void SetAsLastSibling(){if(parent==null)return;parent.children.Remove(this);parent.children.Add(this);} public Vector3 localScale=Vector3.one; }
public class RectTransform:Transform { public Vector2 anchorMin,anchorMax,offsetMin,offsetMax,pivot,anchoredPosition,sizeDelta; public Rect rect=>new Rect(0,0,sizeDelta.x==0?700:sizeDelta.x,sizeDelta.y==0?600:sizeDelta.y); }
public struct Vector2 { public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 zero=>new(0,0);public static Vector2 one=>new(1,1);public static bool operator ==(Vector2 a,Vector2 b)=>a.x==b.x&&a.y==b.y;public static bool operator !=(Vector2 a,Vector2 b)=>!(a==b);public static implicit operator Vector2(Vector3 v)=>new(v.x,v.y);public override bool Equals(object o)=>o is Vector2 v&&this==v;public override int GetHashCode()=>HashCode.Combine(x,y); }
public struct Vector3 { public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 one=>new(1,1,1); }
public struct Rect { public Vector2 size=>new(width,height);public float x,y,width,height;public Rect(float x,float y,float width,float height){this.x=x;this.y=y;this.width=width;this.height=height;} }
public struct Color { public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color white=>new(1,1,1);public static Color gray=>new(.5f,.5f,.5f);public static Color clear=>new(0,0,0,0); }
public class Texture:Object {public int width,height;}
public class Texture2D:Texture { }
public static class Mathf {public static int Clamp(int v,int a,int b)=>Math.Clamp(v,a,b);public static float Clamp(float v,float a,float b)=>Math.Clamp(v,a,b);public static float Min(float a,float b)=>Math.Min(a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static int Max(int a,int b)=>Math.Max(a,b);}
public enum CursorLockMode { None,Locked } public static class Cursor {public static CursorLockMode lockState;}
public static class Debug { public static void LogWarning(object s)=>Console.WriteLine(s);public static void LogError(object s)=>Console.WriteLine(s); }
}
namespace UnityEngine.Events { public class UnityEvent<T> {public readonly List<Action<T>> listeners=new();public void AddListener(Action<T> a)=>listeners.Add(a);public void RemoveListener(Action<T> a)=>listeners.Remove(a);public void Invoke(T t){foreach(var a in listeners.ToArray())a(t);} } }
namespace UnityEngine.UI {
using UnityEngine;
public class Selectable:Component { public bool interactable=true;public int SelectCount; public void Select()=>SelectCount++; }
public class Graphic:Component {public bool raycastTarget;public Color color;public RectTransform rectTransform=>(RectTransform)transform;}
public class RawImage:Graphic {public Texture texture;public Rect uvRect;}
public class Image:Graphic { }
public class RectMask2D:Component { }
public class LayoutElement:Component {public bool ignoreLayout;}
public class HorizontalLayoutGroup:Component {public bool childControlWidth=true,childControlHeight=true;}
public class CanvasRenderer:Component { }
public class Scrollbar:Component {public float value=1; public RectTransform handleRect; public UnityEngine.Events.UnityEvent<float> onValueChanged=new();}
public class ScrollRect:Component { public RectTransform content,viewport;public Scrollbar verticalScrollbar;public float verticalScrollbarSpacing; public float verticalNormalizedPosition=1; }
public class AspectRatioFitter:Component { public enum AspectMode {None,WidthControlsHeight,HeightControlsWidth,FitInParent,EnvelopeParent} public AspectMode aspectMode;public float aspectRatio; }
}
namespace UnityEngine.EventSystems {public enum MoveDirection {None,Left,Right,Up,Down}public class AxisEventData {public MoveDirection moveDir;} }
namespace TMPro {
using UnityEngine;
public enum TextOverflowModes { Ellipsis,Truncate,Overflow } public enum TextAlignmentOptions { MidlineLeft,Center,TopLeft,BottomLeft } public enum TextWrappingModes { Normal,NoWrap }
public class TextMeshProUGUI:UnityEngine.UI.Graphic {public object font;public float lineSpacing;public string text;public bool enableAutoSizing,enableWordWrapping;public float fontSize,fontSizeMin,fontSizeMax;public TextOverflowModes overflowMode;public TextAlignmentOptions alignment;public TextWrappingModes textWrappingMode;}
public class TMP_InputField:Component {public bool interactable=true;public string text="";public void ActivateInputField(){} }
}
namespace UnityEngine.ResourceManagement.AsyncOperations {
public enum AsyncOperationStatus { None,Succeeded,Failed }
public sealed class Operation<T> {public T Result;public AsyncOperationStatus Status;public bool Valid=true;public event Action<AsyncOperationHandle<T>> Completed;public void Finish(T result,bool success){Result=result;Status=success?AsyncOperationStatus.Succeeded:AsyncOperationStatus.Failed;Completed?.Invoke(new AsyncOperationHandle<T>(this));}public void Listen(Action<AsyncOperationHandle<T>> cb){Completed+=cb;if(Status!=AsyncOperationStatus.None)cb(new AsyncOperationHandle<T>(this));}public void Unlisten(Action<AsyncOperationHandle<T>> cb)=>Completed-=cb;}
public struct AsyncOperationHandle<T> {public Operation<T> Op;public AsyncOperationHandle(Operation<T> op){Op=op;}public T Result=>Op.Result;public AsyncOperationStatus Status=>Op.Status;public bool IsValid()=>Op?.Valid==true;public event Action<AsyncOperationHandle<T>> Completed {add=>Op.Listen(value);remove=>Op.Unlisten(value);} }
}
namespace UnityEngine.AddressableAssets {
using UnityEngine.ResourceManagement.AsyncOperations;
public static class Addressables { public static readonly List<(string Key,Operation<UnityEngine.GameObject> Op)> Pending=new();public static readonly Dictionary<string,int> Loads=new();public static int Releases;
 public static AsyncOperationHandle<T> LoadAssetAsync<T>(string key) {if(typeof(T)!=typeof(UnityEngine.GameObject))throw new Exception("Unsupported test asset");var op=new Operation<UnityEngine.GameObject>();Pending.Add((key,op));Loads[key]=Loads.GetValueOrDefault(key)+1;return (AsyncOperationHandle<T>)(object)new AsyncOperationHandle<UnityEngine.GameObject>(op);}
 public static void Complete(string key,UnityEngine.GameObject prefab,bool success=true){var p=Pending.First(x=>x.Key==key);Pending.Remove(p);p.Op.Finish(prefab,success);}
 public static void Release<T>(AsyncOperationHandle<T> h){if(h.Op!=null)h.Op.Valid=false;Releases++;}
}
}
namespace MDPro3.Net { }
namespace MDPro3.UI.PropertyOverride {public static class PropertyOverrider {public static bool Mobile;public static bool NeedMobileLayout()=>Mobile;} }
namespace MDPro3 {
public class Program { public static Program instance=new();public Servant.SoloSelector solo;public Servant.CutinViewer cutin;public Servant.Servant menu=new(),online=new();public Servant.DeckSelector deckSelector=new();public void ShiftToServant(Servant.Servant s){} }
public static class UserInput {public static bool NextSelectionIsAxis,MouseRightDown,WasCancelPressed,WasGamepadButtonWestPressed,WasGamepadButtonNorthPressed;public static bool NeedDefaultSelect()=>true;}
public static class Config {public static string GetConfigDeckName()=>"Player Deck";}
public static class MessageManager {public static readonly List<string> Messages=new();public static void Cast(string s)=>Messages.Add(s);}
public static class AudioManager {public static void PlaySE(string s){} }
}
namespace MDPro3.Servant {
public class Servant:UnityEngine.MonoBehaviour {public Servant returnServant;public UI.ServantUI.ServantUI servantUI;public virtual int Depth=>0;protected virtual bool ShowLine=>true;public virtual void Initialize(){}protected virtual void FirstLoadEvent(){}protected virtual void ApplyShowArrangement(int previous){}public virtual void Select(bool forced=false){}public virtual void PerFrameFunction(){}public bool NeedResponseInput()=>true;public void OnReturn(){}public T GetUI<T>() where T:UI.ServantUI.ServantUI=>(T)servantUI;}
public class DeckSelector:Servant {public enum Condition {ForSolo}public void SwitchCondition(Condition c){} }
}
namespace MDPro3.UI {
public class ElementManager { public Dictionary<string,UnityEngine.GameObject> Elements=new();public T GetElement<T>(string label) where T:UnityEngine.Component=>Elements[label].GetComponent<T>();public UnityEngine.GameObject GetElement(string label)=>Elements[label];public T GetNestedElement<T>(string label) where T:UnityEngine.Component=>GetElement<T>(label); }
public class SelectionButton:UnityEngine.MonoBehaviour {public ElementManager Manager=new();public int index;public UnityEngine.UI.Selectable GetSelectable()=>gameObject.GetComponent<UnityEngine.UI.Selectable>();private string text;public void SetButtonText(string t)=>text=t;public string GetButtonText()=>text;}
public class SelectionToggle:SelectionButton {public bool isOn;protected virtual void CallToggleOnEvent(){}protected virtual void CallSubmitEvent(){}protected virtual void OnNavigation(UnityEngine.EventSystems.AxisEventData data){}protected virtual void OnClick(){}protected virtual void ToggleOn()=>isOn=true;protected virtual void ToggleOff()=>isOn=false;public void SetToggleOn(){ToggleOn();CallToggleOnEvent();} }
public class SelectionToggle_ScrollRectItem:SelectionToggle {public virtual void Refresh(){}public virtual void Dispose()=>UnityEngine.Object.Destroy(gameObject);public virtual void ToggleOnNow()=>isOn=true;public virtual void ToggleOffNow()=>isOn=false;}
public class SuperScrollViewItem:UnityEngine.MonoBehaviour {public SuperScrollView handler;public int id;}
public class ArtRawImageHandler:UnityEngine.MonoBehaviour {public int Code;public void SetArt(int code){Code=code;GetComponent<UnityEngine.UI.RawImage>().texture=new UnityEngine.Texture{width=430,height=600};} }
}
namespace MDPro3.UI.ServantUI {public class ServantUI:UnityEngine.MonoBehaviour {public MDPro3.UI.ElementManager Manager=new();public UnityEngine.RectTransform Root=>Manager.GetElement<UnityEngine.RectTransform>("Root");protected TMPro.TextMeshProUGUI Title=>Manager.GetElement<TMPro.TextMeshProUGUI>("TextTitle");public virtual void Initialize(MDPro3.Servant.Servant servant){servant.servantUI=this;}public virtual void ShowEvent(){}public void SelectDefaultSelectable(){}protected virtual void OnDestroy(){} } }
namespace MDPro3.Duel.YGOSharp {public class Card {public int Id;public string Name;}public static class CardsManager {public static Card Get(int id)=>new(){Id=id,Name=MDPro3.BattleSpirits.BattleCatalog.Cards.Find(x=>x.numericId==id).nameEn};} }
namespace MDPro3.BattleSpirits {
public class Definition {public string id,nameEn;public int numericId;public bool boss;}
public class StarterDeck {public string name;public string[] cards;}
public static class BattleCatalog {public static readonly List<Definition> Cards=new(){new(){id="boss-a",numericId=100,nameEn="Ritenia",boss=true},new(){id="boss-b",numericId=200,nameEn="Pisketona",boss=true},new(){id="normal",numericId=300,nameEn="Ordinary",boss=false}};public static readonly List<StarterDeck> StarterDecks=new(){new(){name="Infernowing",cards=new[]{"boss-a"}},new(){name="Dark Purple",cards=new[]{"boss-b"}},new(){name="Armored Fish",cards=new[]{"normal"}},new(){name="Kamen Rider Ex-Aid",cards=new[]{"normal"}}};public static bool IsBoss(string id)=>Cards.Exists(c=>c.id==id&&c.boss);public static int GetNumericId(string id)=>Cards.Find(c=>c.id==id)?.numericId??0;public static string GetCardId(int id)=>Cards.Find(c=>c.numericId==id)?.id;public static string StandeePath(string id)=>id==null?null:"/test/"+id+".png";public static string CardImagePath(string id)=>id==null?null:"/test/cards/"+id+".png";}
public static class BattleCosmetics {public static UnityEngine.Texture2D LoadTexture(string path)=>path==null?null:new(){name=path,width=400,height=600};}
public static class BattleIntegration {public static readonly List<(string Deck,string Opponent)> Starts=new();public static readonly List<string> Previews=new();public static void StartSolo(string deck,string opponent)=>Starts.Add((deck,opponent));public static void PreviewCutscene(string id=null)=>Previews.Add(id);}
}
