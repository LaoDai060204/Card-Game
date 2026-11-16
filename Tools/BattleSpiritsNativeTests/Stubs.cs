using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Cysharp.Threading.Tasks
{
    [AsyncMethodBuilder(typeof(AsyncUniTaskMethodBuilder<>))]
    public struct UniTask<T>
    {
        internal Task<T> Inner;
        public TaskAwaiter<T> GetAwaiter() => Inner.GetAwaiter();
    }
    public struct AsyncUniTaskMethodBuilder<T>
    {
        private AsyncTaskMethodBuilder<T> inner;
        public static AsyncUniTaskMethodBuilder<T> Create() => new AsyncUniTaskMethodBuilder<T> { inner = AsyncTaskMethodBuilder<T>.Create() };
        public UniTask<T> Task => new UniTask<T> { Inner = inner.Task };
        public void SetResult(T result) => inner.SetResult(result);
        public void SetException(Exception exception) => inner.SetException(exception);
        public void SetStateMachine(IAsyncStateMachine machine) => inner.SetStateMachine(machine);
        public void Start<TStateMachine>(ref TStateMachine machine) where TStateMachine : IAsyncStateMachine => inner.Start(ref machine);
        public void AwaitOnCompleted<TAwaiter,TStateMachine>(ref TAwaiter awaiter, ref TStateMachine machine) where TAwaiter : INotifyCompletion where TStateMachine : IAsyncStateMachine => inner.AwaitOnCompleted(ref awaiter, ref machine);
        public void AwaitUnsafeOnCompleted<TAwaiter,TStateMachine>(ref TAwaiter awaiter, ref TStateMachine machine) where TAwaiter : ICriticalNotifyCompletion where TStateMachine : IAsyncStateMachine => inner.AwaitUnsafeOnCompleted(ref awaiter, ref machine);
    }
}
namespace UnityEngine
{
    public static class Application { public static string streamingAssetsPath; public static string persistentDataPath; }
    public static class Debug { public static void Log(object value) { } }
    public struct Color
    {
        public Color(float r,float g,float b) { }
        public static Color white => new Color(1,1,1);
    }
    public enum TextureFormat { RGBA32 }
    public class Texture { public int width=650,height=900; }
    public class Texture2D : Texture
    {
        public Texture2D(int width,int height,TextureFormat format,bool mip) { this.width=width;this.height=height; }
        public void SetPixels(Color[] colors) { }
        public void Apply(bool updateMipmaps,bool unreadable) { }
    }
    public struct Rect { public Rect(float x,float y,float width,float height) { } }
    public struct Vector2 { public Vector2(float x,float y) { } }
    public class Sprite { public static Sprite Create(Texture2D texture,Rect rect,Vector2 pivot) => new Sprite(); }
    public class GameObject { public T AddComponent<T>() where T : new() => new T(); }
}
namespace UnityEngine.UI
{
    public class AspectRatioFitter { public enum AspectMode { FitInParent };public AspectMode aspectMode;public float aspectRatio; }
    public class RawImage { public UnityEngine.Texture texture;public object material;public UnityEngine.Rect uvRect;public UnityEngine.GameObject gameObject=new UnityEngine.GameObject();public T GetComponent<T>() where T:class=>null; }
}
namespace MDPro3
{
    public class Program
    {
        public const string EXPANSION_YDK=".ydk",STRING_LINE_BREAK="\n",STRING_SLASH="/";
        public static bool noAccess;
        public static Program instance = new Program();
        public Selector deckSelector = new Selector();
    }
    public class Selector { public string DeckType = string.Empty; }
    public static class InterString { public static string Get(string text,int type=0)=>text; }
    public static class CardRenderer { public const string SMALL_SLASH="/",BIG_SLASH="/"; }
    public static class StringHelper
    {
        public static string GetSetName(long id)=>"";public static string GetUnsafe(int id,int type=0)=>"";
        public static string Attribute(int id,int type=0)=>"";public static string Race(int id,int type=0)=>"";
        public static string MainType(int id,int type=0)=>"";public static string SecondType(int id,int type=0)=>"";
    }
}
namespace MDPro3.Net { }
namespace MDPro3.Utility
{
    public static class Language
    {
        public const string English="en",SimplifiedChinese="zh",TraditionalChinese="tw",Korean="ko",Portuguese="pt",French="fr",German="de",Italian="it",Spanish="es";
        public const string ConfigName="Language",CardConfigName="CardLanguage",PrereleaseName="PrereleaseLanguage";
        public static string GetConfig()=>English;public static string GetCardConfig()=>English;public static string GetPrereleaseConfig()=>English;
        public static string GetLeftBracket()=>"[";public static string GetRightBracket()=>"]";
        public static bool NeedSmallBracket(string s)=>false;public static bool UseLatin(string s)=>true;public static bool CardUseLatin()=>true;
    }
    public static class LegacyTextContracts { public static bool StartsWith(string name,string input)=>false; }
    public static class CardImageLoader
    {
        public static Task<UnityEngine.Texture2D> LoadBattleTexturePathAsync(string path,CancellationToken token=default)
            => Task.FromResult(new UnityEngine.Texture2D(650,900,UnityEngine.TextureFormat.RGBA32,false));
    }
}
namespace MDPro3.UI
{
    public class CardCollectionView { public enum SortOrder { ByType=1,ByTypeReverse,ByLevelUp,ByLevelDown,ByAttackUp,ByAttackDown,ByDefenceUp,ByDefenceDown,ByRarityUp,ByRarityDown,ByGPUp,ByGPDown }; }
}
namespace MDPro3.Duel.YGOSharp
{
    public class Banlist { public int GetCredit(int id)=>0;public string GetCreditString(int id)=>""; }
    public static class BanlistManager { public static Banlist currentGenesysBanList=new Banlist(); }
}
