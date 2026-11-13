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
  public Matrix4x4 localToWorldMatrix=>(parent==null?Matrix4x4.identity:parent.localToWorldMatrix)*Matrix4x4.TRS(localPosition,localRotation,localScale);
  public Matrix4x4 worldToLocalMatrix=>localToWorldMatrix.inverse;
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
    public class Renderer:Component {public bool enabled=true,receiveShadows;public Material sharedMaterial;public Rendering.ShadowCastingMode shadowCastingMode;public MaterialPropertyBlock propertyBlock;public void SetPropertyBlock(MaterialPropertyBlock block){propertyBlock=block;} }
    public class MeshRenderer:Renderer { }
    public enum SkinQuality { Auto,Bone1,Bone2,Bone4 }
    public class SkinnedMeshRenderer:Renderer {public Mesh sharedMesh;public Transform[] bones;public Transform rootBone;public Bounds localBounds;public SkinQuality quality;public bool updateWhenOffscreen;}
    public class Collider:Component { }
    public class MeshFilter:Component {public Mesh sharedMesh;}
    public struct BoneWeight {public int boneIndex0,boneIndex1,boneIndex2,boneIndex3;public float weight0,weight1,weight2,weight3;}
    public struct Bounds {public Vector3 center,size;public Bounds(Vector3 center,Vector3 size){this.center=center;this.size=size;}public Vector3 extents=>size*.5f;public Vector3 min=>center-extents;public Vector3 max=>center+extents;}
    // System.Numerics uses row vectors; reverse stored multiplication to match
    // Unity's column-vector A*B convention, including nontrivial parent TRS.
    public struct Matrix4x4 {
        private System.Numerics.Matrix4x4 value;
        private Matrix4x4(System.Numerics.Matrix4x4 value){this.value=value;}
        public static Matrix4x4 identity=>new Matrix4x4(System.Numerics.Matrix4x4.Identity);
        public Matrix4x4 inverse{get{if(!System.Numerics.Matrix4x4.Invert(value,out var result))throw new InvalidOperationException("Singular test transform.");return new Matrix4x4(result);}}
        public static Matrix4x4 TRS(Vector3 position,Quaternion rotation,Vector3 scale)=>new Matrix4x4(System.Numerics.Matrix4x4.CreateScale(scale.x,scale.y,scale.z)*System.Numerics.Matrix4x4.CreateFromQuaternion(new System.Numerics.Quaternion(rotation.x,rotation.y,rotation.z,rotation.w))*System.Numerics.Matrix4x4.CreateTranslation(position.x,position.y,position.z));
        public static Matrix4x4 Translate(Vector3 value)=>TRS(value,Quaternion.identity,Vector3.one);
        public static Matrix4x4 operator*(Matrix4x4 a,Matrix4x4 b)=>new Matrix4x4(b.value*a.value);
        public Vector3 MultiplyPoint3x4(Vector3 point){var result=System.Numerics.Vector3.Transform(new System.Numerics.Vector3(point.x,point.y,point.z),value);return new Vector3(result.X,result.Y,result.Z);}
        public Vector3 MultiplyVector(Vector3 point){var result=System.Numerics.Vector3.TransformNormal(new System.Numerics.Vector3(point.x,point.y,point.z),value);return new Vector3(result.X,result.Y,result.Z);}
    }
    public class Mesh:Object {
        public Vector3[] vertices,normals;public Vector2[] uv;public int[] triangles;public Color[] colors;public BoneWeight[] boneWeights;public Matrix4x4[] bindposes;public Bounds bounds;
        public void RecalculateNormals(){if(vertices==null)return;normals=new Vector3[vertices.Length];if(triangles!=null)for(int i=0;i+2<triangles.Length;i+=3){int a=triangles[i],b=triangles[i+1],c=triangles[i+2];var u=vertices[b]-vertices[a];var v=vertices[c]-vertices[a];var n=new Vector3(u.y*v.z-u.z*v.y,u.z*v.x-u.x*v.z,u.x*v.y-u.y*v.x);normals[a]=normals[a]+n;normals[b]=normals[b]+n;normals[c]=normals[c]+n;}for(int i=0;i<normals.Length;i++){float length=normals[i].magnitude;if(length>1e-12f)normals[i]=normals[i]*(1/length);}}
        public void RecalculateBounds(){if(vertices==null||vertices.Length==0){bounds=default;return;}float x0=vertices[0].x,x1=x0,y0=vertices[0].y,y1=y0,z0=vertices[0].z,z1=z0;foreach(var p in vertices){x0=Math.Min(x0,p.x);x1=Math.Max(x1,p.x);y0=Math.Min(y0,p.y);y1=Math.Max(y1,p.y);z0=Math.Min(z0,p.z);z1=Math.Max(z1,p.z);}bounds=new Bounds(new Vector3((x0+x1)*.5f,(y0+y1)*.5f,(z0+z1)*.5f),new Vector3(x1-x0,y1-y0,z1-z0));}
    }
    public class MaterialPropertyBlock {public readonly Dictionary<string,float> Floats=new Dictionary<string,float>();public void SetFloat(string name,float value){Floats[name]=value;} }
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
namespace UnityEngine.Rendering {public enum ShadowCastingMode{Off,On,TwoSided,ShadowsOnly}}
namespace TMPro
{
    public enum HorizontalAlignmentOptions{Left,Center,Right}
    public partial class TMP_InputField {public bool readOnly,customCaretColor;public UnityEngine.Color caretColor,selectionColor;public UnityEngine.Events.UnityEvent<string> onSubmit=new UnityEngine.Events.UnityEvent<string>();public void SetTextWithoutNotify(string value){text=value;ForceLabelUpdate();}public void ForceLabelUpdate(){if(placeholder!=null)placeholder.enabled=string.IsNullOrEmpty(text);}}
}
namespace UnityEngine.EventSystems {public class EventSystem {public static EventSystem current;public UnityEngine.GameObject currentSelectedGameObject;public void SetSelectedGameObject(UnityEngine.GameObject value){currentSelectedGameObject=value;}}}
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
