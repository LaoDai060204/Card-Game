// Additional controlled Unity APIs used by the direct native-prefab integration.
// Geometry state is represented, but no graphics, input modules or audio device runs.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace UnityEngine
{
    public partial class Object
    {
        public static void DontDestroyOnLoad(Object value) { }
        public static GameObject Instantiate(GameObject original, Transform parent, bool worldPositionStays)
        {
            var result = GameObject.Clone(original);
            result.transform.SetParent(parent, worldPositionStays); return result;
        }
    }
    public partial class Component { public T[] GetComponents<T>() where T:Component => gameObject.GetComponents<T>(); }
    public partial class GameObject
    {
        public T[] GetComponents<T>() where T:Component => components.OfType<T>().Where(c=>!c.destroyed).ToArray();
        internal static GameObject Clone(GameObject original)
        {
            var result = new GameObject(original.name, original.transform.GetType()) { layer=original.layer };
            foreach (var component in original.components.Where(c=>!(c is Transform)))
            {
                var copy=result.AddComponent(component.GetType());
                foreach(var field in component.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public))
                    if(field.Name!="gameObject"&&!field.IsInitOnly) field.SetValue(copy,field.GetValue(component));
                if(copy is UI.Button button)button.onClick=new Events.UnityEvent();
            }
            foreach(var child in original.transform.children) Clone(child.gameObject).transform.SetParent(result.transform,false);
            result.SetActive(original.activeSelf); return result;
        }
        public static GameObject CreatePrimitive(PrimitiveType type) => new GameObject(type.ToString(),typeof(MeshRenderer),typeof(MeshFilter),typeof(Collider));
    }
    public partial class Transform {
  public void SetSiblingIndex(int index){if(parent==null)return;parent.children.Remove(this);parent.children.Insert(Math.Max(0,Math.Min(index,parent.children.Count)),this);}
  public Vector3 eulerAngles;
  public Vector3 TransformPoint(Vector3 value){var scaled=new Vector3(value.x*localScale.x,value.y*localScale.y,value.z*localScale.z);var local=localPosition+localRotation*scaled;return parent==null?local:parent.TransformPoint(local);}
  public Vector3 InverseTransformPoint(Vector3 value){var local=parent==null?value:parent.InverseTransformPoint(value);var rotated=Quaternion.Inverse(localRotation)*(local-localPosition);return new Vector3(localScale.x==0?0:rotated.x/localScale.x,localScale.y==0?0:rotated.y/localScale.y,localScale.z==0?0:rotated.z/localScale.z);}
 }
    public partial struct Vector3
    {
        public static Vector3 up=>new Vector3(0,1,0);
        public float magnitude=>(float)Math.Sqrt(x*x+y*y+z*z);
        public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator-(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator*(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);
    }
    public partial struct Quaternion { public static Quaternion FromToRotation(Vector3 a,Vector3 b)=>default; }
    public partial class Mathf
    {
        public const float PI=(float)Math.PI;
        public static float Clamp01(float value)=>Math.Clamp(value,0,1);
        public static float Sin(float value)=>(float)Math.Sin(value);
        public static float Cos(float value)=>(float)Math.Cos(value);
    }
    public static class Time { public static float unscaledTime,unscaledDeltaTime=1f/60; }
    public class CanvasGroup:Behaviour { public float alpha=1; public bool blocksRaycasts=true,interactable=true,ignoreParentGroups; }
    public class Font:Object { public Material material=new Material(); }
    public enum FontStyle { Normal,Bold,Italic,BoldAndItalic }
    public enum HorizontalWrapMode { Wrap,Overflow }
    public enum VerticalWrapMode { Truncate,Overflow }
    public enum TextAlignment { Left,Center,Right }
    public class TextMesh:Component {public string text;public Font font;public int fontSize;public float characterSize;public TextAnchor anchor;public TextAlignment alignment;public Color color;}
    public enum PrimitiveType { Cube,Sphere,Cylinder }
    public class Shader:Object {public static Shader Find(string name)=>new Shader{name=name};}
    public class Renderer:Component {public bool enabled=true;public Material sharedMaterial;public void SetPropertyBlock(MaterialPropertyBlock block){} }
    public class MeshRenderer:Renderer { }
    public class Collider:Component { }
    public class MeshFilter:Component {public Mesh sharedMesh;}
    public class Mesh:Object {public Vector3[] vertices;public Vector2[] uv;public int[] triangles;public void RecalculateNormals(){}public void RecalculateBounds(){} }
    public class MaterialPropertyBlock {public void SetFloat(string name,float value){} }
    public static class Resources {
        public static string ProjectRoot;
        public static readonly Dictionary<string,Texture2D> Textures=new Dictionary<string,Texture2D>(StringComparer.Ordinal);
        public static T Load<T>(string path)where T:Object{
            if(typeof(T)==typeof(Shader))return (T)(Object)new Shader{name=path};
            if(typeof(T)!=typeof(Texture2D)||ProjectRoot==null)return null;
            if(Textures.TryGetValue(path,out var existing)&&existing!=null)return (T)(Object)existing;
            string root=System.IO.Path.Combine(ProjectRoot,"Assets/BattleSpirits/Art/Resources");
            string file=new[]{".png",".jpg",".jpeg"}.Select(ext=>System.IO.Path.Combine(root,path+ext)).FirstOrDefault(System.IO.File.Exists);
            if(file==null)return null;
            var texture=new Texture2D(1,1,TextureFormat.RGBA32,false){name=path,sourcePath=file};
            if(!texture.LoadImage(System.IO.File.ReadAllBytes(file)))throw new System.IO.InvalidDataException("Unsupported test resource image: "+file);
            Textures[path]=texture;return (T)(Object)texture;
        }
        public static T GetBuiltinResource<T>(string path)where T:Object,new()=>new T();
    }
    public class AudioClip:Object {public float[] data;public static AudioClip Create(string name,int count,int channels,int sampleRate,bool stream)=>new AudioClip{name=name,data=new float[count*channels]};public void SetData(float[] samples,int offset)=>Array.Copy(samples,0,data,offset,samples.Length);}
    public class AudioSource:Behaviour {public bool loop,isPlaying;public float spatialBlend,volume;public AudioClip clip;public void Play()=>isPlaying=true;public void Stop()=>isPlaying=false;public void PlayOneShot(AudioClip value,float volume=1){} }
}
namespace UnityEngine.UI {public class Text:Graphic {public string text;public bool supportRichText,resizeTextForBestFit;public UnityEngine.Font font;public int fontSize,resizeTextMinSize,resizeTextMaxSize;public UnityEngine.FontStyle fontStyle;public UnityEngine.TextAnchor alignment;public UnityEngine.HorizontalWrapMode horizontalOverflow;public UnityEngine.VerticalWrapMode verticalOverflow;}}
namespace TMPro
{
    public enum HorizontalAlignmentOptions{Left,Center,Right}
    public partial class TMP_InputField {public bool readOnly;public UnityEngine.Events.UnityEvent<string> onSubmit=new UnityEngine.Events.UnityEvent<string>();public void SetTextWithoutNotify(string value)=>text=value;}
}
namespace UnityEngine.EventSystems {public class EventSystem {public static EventSystem current;public UnityEngine.GameObject currentSelectedGameObject;}}
namespace MDPro3
{
    public partial class Program
    {
        public Servant.Servant currentServant;
        public Func<Servant.Servant,MDPro3.UI.ServantUI.ServantUI> UiFactory;
        public readonly List<Servant.Servant> Registered=new List<Servant.Servant>();
        public void RegisterBattleServant(Servant.Servant servant){if(!Registered.Contains(servant)){Registered.Add(servant);servant.Initialize();}}
    }
    public partial class UIManager
    {
        public static List<string> Popup;
        public static Action PopupSelected;
        public static void ShowPopupText(List<string> text,TMPro.HorizontalAlignmentOptions alignment=TMPro.HorizontalAlignmentOptions.Center){Popup=text;}
        public static void ShowPopupSelection(List<string> text,Action selected,Action cancel){Popup=text;PopupSelected=selected;}
    }
}
namespace MDPro3.Servant
{
    public partial class Servant
    {
        public Servant returnServant;
        protected virtual string Label_UI=>string.Empty;
        protected virtual bool ShowLine=>false;
        public virtual int Depth=>1;
        public virtual void Initialize(){}
        public virtual void OnReturn(){}
        public virtual void OnExit(){}
    }
}
namespace MDPro3.UI.ServantUI
{
    public partial class ServantUI
    {
        public UnityEngine.RectTransform Root=>transform.Find("Root") as UnityEngine.RectTransform??transform as UnityEngine.RectTransform;
        public virtual void ShowEvent(){}
        protected virtual void HideEvent(){}
        protected virtual void OnDestroy(){}
        internal void InvokeHideEvent()=>HideEvent();
    }
}
