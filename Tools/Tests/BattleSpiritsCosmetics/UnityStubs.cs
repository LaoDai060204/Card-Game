using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Text.Json;
namespace Cysharp.Threading.Tasks {
[AsyncMethodBuilder(typeof(AsyncUniTaskMethodBuilder<>))] public struct UniTask<T> { internal Task<T> Inner; public TaskAwaiter<T> GetAwaiter()=>Inner.GetAwaiter(); }
public struct AsyncUniTaskMethodBuilder<T> {
private AsyncTaskMethodBuilder<T> inner;
public static AsyncUniTaskMethodBuilder<T> Create()=>new AsyncUniTaskMethodBuilder<T>{inner=AsyncTaskMethodBuilder<T>.Create()};
public UniTask<T> Task=>new UniTask<T>{Inner=inner.Task};
public void SetResult(T result)=>inner.SetResult(result); public void SetException(Exception e)=>inner.SetException(e); public void SetStateMachine(IAsyncStateMachine m)=>inner.SetStateMachine(m);
public void Start<M>(ref M m) where M:IAsyncStateMachine=>inner.Start(ref m);
public void AwaitOnCompleted<A,M>(ref A a,ref M m) where A:INotifyCompletion where M:IAsyncStateMachine=>inner.AwaitOnCompleted(ref a,ref m);
public void AwaitUnsafeOnCompleted<A,M>(ref A a,ref M m) where A:ICriticalNotifyCompletion where M:IAsyncStateMachine=>inner.AwaitUnsafeOnCompleted(ref a,ref m);
}}
namespace UnityEngine {
public class Object {public string name;public static void Destroy(Object o){} }
public class ScriptableObject:Object{} public class CreateAssetMenuAttribute:Attribute{public string fileName,menuName;}
public class Component:Object{public GameObject gameObject;public Transform transform=>gameObject.transform;public T GetComponent<T>() where T:Component=>gameObject.GetComponent<T>();}
public class GameObject:Object{public Transform transform;public Dictionary<Type,Component> components=new Dictionary<Type,Component>();public GameObject(string name,params Type[] types){this.name=name;transform=types.Contains(typeof(RectTransform))?new RectTransform():new Transform();transform.gameObject=this;components[transform.GetType()]=transform;foreach(var type in types)if(type!=typeof(RectTransform))AddComponent(type);}public T GetComponent<T>() where T:Component=>components.Values.OfType<T>().FirstOrDefault();private void AddComponent(Type t){var c=(Component)Activator.CreateInstance(t);c.gameObject=this;components[t]=c;}}
public class Transform:Component{public Transform parent;public readonly List<Transform> children=new List<Transform>();public void SetParent(Transform p,bool world=false){parent=p;p.children.Add(this);}}
public class RectTransform:Transform{public Vector2 anchorMin,anchorMax,offsetMin,offsetMax,pivot,anchoredPosition,sizeDelta;}
public class CanvasRenderer:Component{}
public struct Vector2{public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 one=>new Vector2(1,1);public static Vector2 zero=>new Vector2(0,0);}
public struct Color{public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}}
public struct Rect{public float x,y,width,height;public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}}
public class Texture:Object{public int width,height;}
public enum TextureFormat{RGBA32}public enum TextureWrapMode{Clamp}public enum FilterMode{Bilinear}
public class Texture2D:Texture{public TextureWrapMode wrapMode;public FilterMode filterMode;public Texture2D(int w,int h,TextureFormat f,bool mip){width=w;height=h;}public static Texture2D whiteTexture=new Texture2D(2,2,TextureFormat.RGBA32,false);}
public static class ImageConversion{public static bool LoadImage(Texture2D texture,byte[] bytes,bool unreadable){if(bytes.Length<24)return false;texture.width=(bytes[16]<<24)|(bytes[17]<<16)|(bytes[18]<<8)|bytes[19];texture.height=(bytes[20]<<24)|(bytes[21]<<16)|(bytes[22]<<8)|bytes[23];return true;}}
public class Sprite:Object{public Texture2D texture;public Rect rect;public static Sprite Create(Texture2D tex,Rect rect,Vector2 pivot,float ppu=100)=>new Sprite{texture=tex,rect=rect};}
public class Shader:Object{public static Shader Find(string name)=>new Shader{name=name};}
public class Material:Object{public Texture mainTexture;public Material(Shader s){} }
public static class Random{public static int Range(int min,int max)=>min;}
public static class Application{public static string streamingAssetsPath;}
}
namespace UnityEngine.UI{using UnityEngine;public class RawImage:Component{public Texture texture;public Color color;public bool raycastTarget=true;public Rect uvRect;public RectTransform rectTransform=>(RectTransform)transform;}}
namespace UnityEngine.AddressableAssets{
public sealed class Handle<T>{public T Result;public Task<T> Task=>System.Threading.Tasks.Task.FromResult(Result);public bool IsValid()=>true;public TaskAwaiter<T> GetAwaiter()=>Task.GetAwaiter();}
public static class Addressables{public static Handle<T> LoadAssetAsync<T>(string p)=>throw new Exception("Legacy addressable load: "+p);public static Handle<List<object>> LoadResourceLocationsAsync(string p,Type type)=>throw new Exception("Legacy addressable location: "+p);public static void Release<T>(Handle<T> h){} }
}
namespace MDPro3{
public static class Program{public const string PATH_LOCALES="missing",PATH_DIY="missing",EXPANSION_PNG=".png",EXPANSION_JPG=".jpg";}
public static class InterString{public static string Get(string s,string arg)=>s.Replace("[?]",arg);}
}
namespace MDPro3.Servant{public static class Appearance{public const string meString="Me",opString="Op",meTagString="MeTag",opTagString="OpTag";}}
namespace MDPro3.Utility{
public static class Language{public static string GetConfig()=>"en";}
public static class Cid2Ydk{public static string ReplaceWithCardName(string s)=>s;}
public static class TextureManager{public static Task<UnityEngine.Texture2D> LoadPicFromFileAsync(string p)=>throw new Exception("Legacy DIY image load");public static UnityEngine.Sprite Texture2Sprite(UnityEngine.Texture2D t)=>null;}
}
namespace MDPro3.BattleSpirits{
public static class BattleSpiritsMode{public static bool IsEnabled=>true;}
public sealed class TestCard{public string id,nameEn,sourceUrl;public int numericId;public string DisplayName=>nameEn;}
public sealed class BattleCardVisual{public int width,height,left,top,right,bottom;}
public static class BattleCatalog{
public static readonly List<TestCard> cards=new List<TestCard>();private static readonly Dictionary<string,BattleCardVisual> visuals=new Dictionary<string,BattleCardVisual>();private static string root;
public static IReadOnlyList<TestCard> Cards=>cards;
public static void Initialize(string streaming){root=Path.Combine(streaming,"BattleSpirits");cards.Clear();using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"cards.json"))))foreach(var c in doc.RootElement.GetProperty("cards").EnumerateArray())cards.Add(new TestCard{id=c.GetProperty("id").GetString(),nameEn=c.GetProperty("nameEn").GetString(),sourceUrl=c.GetProperty("sourceUrl").GetString(),numericId=c.GetProperty("numericId").GetInt32()});
using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"visuals.json"))))foreach(var c in doc.RootElement.GetProperty("visuals").EnumerateArray())visuals[c.GetProperty("id").GetString()]=new BattleCardVisual{width=c.GetProperty("width").GetInt32(),height=c.GetProperty("height").GetInt32(),left=c.GetProperty("left").GetInt32(),right=c.GetProperty("right").GetInt32(),top=c.GetProperty("top").GetInt32(),bottom=c.GetProperty("bottom").GetInt32()};}
public static string StandeePath(string id){string path=Path.Combine(root,"standees",id+".png");return File.Exists(path)?path:null;}
public static BattleCardVisual GetVisual(string id)=>visuals.TryGetValue(id,out var v)?v:null;
}}
