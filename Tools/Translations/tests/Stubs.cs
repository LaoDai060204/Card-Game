using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace UnityEngine
{
    public class Object
    {
        public bool IsDestroyed;
        public static readonly List<Object> DestroyedObjects = new List<Object>();
        public static void Destroy(Object value)
        {
            if (value == null) return;
            value.IsDestroyed = true;
            DestroyedObjects.Add(value);
        }
    }
    public class GameObject { public bool activeInHierarchy = true; }
    public class MonoBehaviour : Object
    {
        private readonly Dictionary<Type, object> components = new Dictionary<Type, object>();
        public readonly GameObject gameObject = new GameObject();
        public bool isActiveAndEnabled = true;
        public T GetComponent<T>() where T : new()
        {
            if (!components.TryGetValue(typeof(T), out var component))
                components[typeof(T)] = component = new T();
            return (T)component;
        }
    }
    public class Texture2D : Object
    {
        public string name;
        public Texture2D(string name = null) { this.name = name; }
        public static readonly Texture2D blackTexture = new Texture2D("Built-in black");
        public static readonly Texture2D whiteTexture = new Texture2D("Built-in white");
        public static readonly Texture2D grayTexture = new Texture2D("Built-in gray");
    }
    public class Transform { public Vector3 localPosition; }
    public struct Vector2 { public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static implicit operator Vector3(Vector2 v)=>new Vector3{ x=v.x,y=v.y }; }
    public struct Vector3 {public float x,y,z;}
    public class RectTransform {public Rect rect=new Rect{width=455};public Vector2 anchoredPosition;}
    public struct Rect {public float width;}
    public class CanvasGroup {public float alpha;public bool blocksRaycasts;}
    public static class Time {public static float deltaTime;}
    public static class Application { public static bool isPlaying=true;public static string LastURL;public static void OpenURL(string url)=>LastURL=url; }
    public static class Debug
    {
        public static readonly List<string> Warnings = new List<string>();
        public static void Log(object value) { }
        public static void LogWarning(object value) => Warnings.Add(value?.ToString());
    }
}
namespace UnityEngine.UI
{
    public class RawImage {public UnityEngine.Texture2D texture;public UnityEngine.RectTransform rectTransform=new UnityEngine.RectTransform();public UnityEngine.Transform transform=new UnityEngine.Transform();}
    public class Text {public string text;}
}
namespace DG.Tweening {public static class Extensions {public static void DOLocalMoveX(this UnityEngine.Transform t,float x,float time){} } }
namespace MDPro3
{
    public class Manager:UnityEngine.MonoBehaviour { }
    public class Program
    {
        public const string STRING_LINE_BREAK="\r\n";public static string PATH_LOCALES;public static bool noAccess;
        public static Program instance=new Program();public Selector deckSelector=new Selector();public Menu menu=new Menu();
    }
    public class Selector {public string DeckType="";}
    public class Menu {public bool showing=true;}
    public static class Tools
    {
        public sealed class ImageRequest
        {
            public string Url;
            public CancellationToken Token;
        }
        public static readonly List<ImageRequest> ImageRequests = new List<ImageRequest>();
        public static Func<string, CancellationToken, Task<UnityEngine.Texture2D>> ImageDownloadOverride;
        public static Task<UnityEngine.Texture2D> DownloadImageAsync(string url, CancellationToken cancellationToken = default)
        {
            ImageRequests.Add(new ImageRequest { Url = url, Token = cancellationToken });
            return ImageDownloadOverride != null ? ImageDownloadOverride(url, cancellationToken)
                : Task.FromResult(new UnityEngine.Texture2D(url));
        }
        public static void ResetImages()
        {
            ImageRequests.Clear();
            ImageDownloadOverride = null;
        }
    }
}
namespace MDPro3.Utility {public static class TaskUtility {public static Task WaitUntil(Func<bool> ready){if(!ready())throw new Exception("Fake download did not complete.");return Task.CompletedTask;} } }
namespace MDPro3.Net
{
    public class MyCardNews {public News[] EnglishUS{get;set;}public News[] ChineseCN{get;set;} }
    public class News {public string title,image,url;}
}
