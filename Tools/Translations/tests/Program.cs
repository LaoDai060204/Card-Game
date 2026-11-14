using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MDPro3;
using MDPro3.Net;
using MDPro3.Utility;
using Newtonsoft.Json;
using UnityEngine.UI;

internal static class EnglishMigrationTests
{
    private static int passed;
    private static string root,temp;
    private static Dictionary<string,string> aliases;
    private static void Assert(bool value,string text){if(!value)throw new Exception(text);}
    private static readonly List<NewsManager> createdNews = new List<NewsManager>();
    private static readonly HashSet<NewsManager> destroyedNews = new HashSet<NewsManager>();
    private static void DestroyNews(NewsManager news)
    {
        if (destroyedNews.Add(news)) typeof(NewsManager).GetMethod("OnDestroy", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(news, null);
    }
    private static void Case(string name, Action test)
    {
        MDPro3.Tools.ResetImages();
        UnityEngine.Debug.Warnings.Clear();
        UnityEngine.Object.DestroyedObjects.Clear();
        UnityEngine.Application.isPlaying = true;
        createdNews.Clear();
        destroyedNews.Clear();
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        finally
        {
            foreach (var news in createdNews) DestroyNews(news);
            Assert(!UnityEngine.Object.DestroyedObjects.Any(value => ReferenceEquals(value, UnityEngine.Texture2D.blackTexture)
                || ReferenceEquals(value, UnityEngine.Texture2D.whiteTexture) || ReferenceEquals(value, UnityEngine.Texture2D.grayTexture)),
                "A news lifecycle destroyed a shared built-in texture.");
            MDPro3.Tools.ResetImages();
            UnityEngine.Application.isPlaying = true;
        }
    }
    private static string FileFor(string locale)=>Path.Combine(MDPro3.Program.PATH_LOCALES,locale,"translation.conf");
    private static Dictionary<string,string> Read(string path)
    {
        var result=new Dictionary<string,string>();
        foreach(string line in File.ReadAllLines(path))
        {int i=line.IndexOf("->",StringComparison.Ordinal);if(i<0)continue;string key=line.Substring(0,i);if(!result.ContainsKey(key))result.Add(key,line.Substring(i+2));}
        return result;
    }
    private static void Select(string ui="en-US",string cards="en-US",string prerelease="en-US")
    {Language.SetConfig(ui);Language.SetCardConfig(cards);Language.SetPrereleaseConfig(prerelease);InterString.Initialize();}
    private static string Form(string s)=>s.Replace("@n",MDPro3.Program.STRING_LINE_BREAK).Replace("@ui","");
    private static NewsManager News(MyCardNews source)
    {
        var result = new NewsManager { news = source, newsPic = new RawImage(), newsPic2 = new RawImage(),
            newsText = new Text(), newsText2 = new Text(), textCount = new Text() };
        createdNews.Add(result);
        return result;
    }
    private static MyCardNews Feed(params MDPro3.Net.News[] entries) => new MyCardNews { EnglishUS = entries };
    private static MDPro3.Net.News Item(string title, string image, string link = null) =>
        new MDPro3.Net.News { title = title, image = image, url = link ?? "https://example.com/" + title };
    private static void Eventually(Func<bool> condition, string message)
    {
        Assert(SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(2)), message);
    }
    private static int Count(NewsManager n)=>(int)typeof(NewsManager).GetMethod("GetMax",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(n,null);
    public static int Main(string[] args)
    {
        root=Path.GetFullPath(args.Length==0?".":args[0]);temp=Path.Combine(Path.GetTempPath(),"mdpro3-english-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        try
        {
            MDPro3.Program.PATH_LOCALES=Path.Combine(temp,"locales")+Path.DirectorySeparatorChar;
            foreach(string dir in Directory.GetDirectories(Path.Combine(root,"Data/locales")))
            {string source=Path.Combine(dir,"translation.conf");if(!File.Exists(source))continue;string target=FileFor(Path.GetFileName(dir));Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(source,target);}
            foreach(string name in new[]{"legacy-ui-keys.json","legacy-text-contracts.json"})File.Copy(Path.Combine(root,"Data/locales",name),Path.Combine(MDPro3.Program.PATH_LOCALES,name));
            aliases=JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(Path.Combine(MDPro3.Program.PATH_LOCALES,"legacy-ui-keys.json")));
            Config.Initialize(Path.Combine(temp,"config.conf"));
            Case("New configurations retain English UI, cards and prerelease defaults",()=>Assert(Language.GetConfig()==Language.English&&Language.GetCardConfig()==Language.English&&Language.GetPrereleaseConfig()==Language.English,"Fresh defaults changed."));
            Case("Canonical English and legacy upstream keys resolve identically in every original locale",()=>{
                int checkedKeys=0;
                foreach(string dir in Directory.GetDirectories(MDPro3.Program.PATH_LOCALES))
                {string locale=Path.GetFileName(dir);Select(locale,locale,locale);var expected=Read(FileFor(locale));foreach(var item in aliases)if(expected.TryGetValue(item.Value,out string value)){Assert(InterString.Get(item.Value)==Form(value)&&InterString.Get(item.Key)==Form(value),locale+": "+item.Value);checkedKeys++;}}
                Assert(checkedKeys>7000,"Not all original locales were exercised.");
            });
            Case("UI, card and prerelease dictionaries select independent locale values",()=>{Select("de-DE","en-US","fr-FR");foreach(var sample in new[]{("de-DE",0),("en-US",1),("fr-FR",2)})Assert(InterString.Get("Confirm",sample.Item2)==Form(Read(FileFor(sample.Item1))["Confirm"]),"Wrong locale target.");});
            Case("Repeated initialization clears prior aliases without corrupting shared locale dictionaries",()=>{Select("de-DE","de-DE","de-DE");Select("en-US","en-US","fr-FR");Assert(InterString.Get("Confirm")=="Confirm"&&InterString.Get("Confirm",1)=="Confirm"&&InterString.Get("Confirm",2)==Form(Read(FileFor("fr-FR"))["Confirm"]),"Shared dictionary was cleared incorrectly.");});
            Case("A missing localized key falls back to the English value",()=>{string path=FileFor("de-DE"),original=File.ReadAllText(path);File.WriteAllLines(path,File.ReadLines(path).Where(l=>!l.StartsWith("Confirm->",StringComparison.Ordinal)).ToArray());Select("de-DE");Assert(InterString.Get("Confirm")=="Confirm","English fallback failed.");File.WriteAllText(path,original);});
            Case("Old downloaded translation files normalize their original keys while reading",()=>{var pair=aliases.First(p=>p.Value=="Confirm");string path=FileFor("fr-FR"),original=File.ReadAllText(path);File.WriteAllText(path,pair.Key+"->Legacy confirmation\n");Select("fr-FR");Assert(InterString.Get("Confirm")=="Legacy confirmation"&&InterString.Get(pair.Key)=="Legacy confirmation","Legacy file normalization failed.");File.WriteAllText(path,original);});
            Case("Unknown prompts append only to the requested language file using English keys",()=>{Select("en-US","de-DE","fr-FR");string ui=File.ReadAllText(FileFor("en-US"));Assert(InterString.Get("Custom prerelease prompt",2)=="Custom prerelease prompt","Unknown fallback changed.");Assert(File.ReadAllText(FileFor("fr-FR")).Contains("Custom prerelease prompt->Custom prerelease prompt")&&File.ReadAllText(FileFor("en-US"))==ui,"Wrong save target.");});
            Case("Replacement placeholders and newline controls remain intact",()=>{Select();Assert(InterString.Get("Loading [?]","Assets")=="Loading Assets","Placeholder changed.");Assert(InterString.Get("One@nTwo@ui")=="One"+MDPro3.Program.STRING_LINE_BREAK+"Two","Control formatting changed.");});
            Case("Reverse lookup returns canonical keys and locale IDs",()=>{Select("de-DE");Assert(InterString.GetOriginal(InterString.Get("Confirm"))=="Confirm","Reverse key is not English.");Assert(InterString.GetOriginal(InterString.Get("en-US"))=="en-US","Locale selection ID changed.");});
            Case("Legacy card grammar keeps exact parser terms behind semantic English keys",()=>{var grammar=JsonConvert.DeserializeObject<Dictionary<string,string[]>>(File.ReadAllText(Path.Combine(root,"Data/locales/legacy-text-contracts.json")));foreach(var pair in grammar)Assert(LegacyTextContracts.Get(pair.Key).SequenceEqual(pair.Value),"Parser grammar changed: "+pair.Key);foreach(string prefix in grammar["RushMaximumAttackPrefixes"])Assert(LegacyTextContracts.StartsWith("RushMaximumAttackPrefixes",prefix+" 4000"),"Maximum ATK prefix lost.");Assert(!LegacyTextContracts.StartsWith("RushMaximumAttackPrefixes","Normal ATK 4000"),"Unrelated card text accepted.");});
            Case("News captions images and links use one coherent English feed",()=>{var news=News(new MyCardNews{EnglishUS=new[]{new News{title="English one",image="en1",url="https://example.com/en1"},new News{title="English two",image="en2",url="https://example.com/en2"}},ChineseCN=new[]{new News{title="Legacy feed",image="legacy",url="https://example.com/legacy"}}});Assert(Count(news)==2,"News count used the other feed.");news.LoadNews();Assert(news.showing&&news.newsText.text=="English one","Wrong first caption.");news.OnRight();news.OnNewsClick();Assert(news.newsText2.text=="English two"&&UnityEngine.Application.LastURL=="https://example.com/en2","English caption/link mismatch.");});
            Case("An absent English feed stays hidden and navigation is harmless",()=>{var news=News(new MyCardNews{ChineseCN=new[]{new News{title="Legacy feed"}}});Assert(Count(news)==0,"Chinese fallback leaked into English news.");news.LoadNews();news.OnLeft();news.OnRight();news.OnNewsClick();Assert(!news.showing,"Empty feed is visible.");});
            Case("A failed English news thumbnail uses a local fallback while retaining its caption and link", () =>
            {
                MDPro3.Tools.ImageDownloadOverride = (url, token) => Task.FromResult<UnityEngine.Texture2D>(null);
                var news = News(Feed(Item("English guide", "https://missing.example/image.jpg", "https://example.com/guide")));
                news.LoadNews();
                Assert(news.showing && ReferenceEquals(news.newsPic.texture, UnityEngine.Texture2D.blackTexture)
                    && news.newsText.text == "English guide" && news.textCount.text == "1/1", "Failed image did not produce a usable local news fallback.");
                news.OnNewsClick();
                Assert(UnityEngine.Application.LastURL == "https://example.com/guide", "Fallback changed the source news link.");
            });
            Case("Mixed successful and failed thumbnails keep English entry indexes aligned", () =>
            {
                var first = new UnityEngine.Texture2D("First image");
                var third = new UnityEngine.Texture2D("Third image");
                MDPro3.Tools.ImageDownloadOverride = (url, token) => Task.FromResult(url == "first" ? first : url == "third" ? third : null);
                var news = News(Feed(Item("First", "first"), Item("Second", "missing"), Item("Third", "third")));
                news.LoadNews();
                Assert(Count(news) == 3 && ReferenceEquals(news.newsPic.texture, first), "Mixed image load dropped or reordered entries.");
                news.OnRight(); news.OnNewsClick();
                Assert(news.newsText2.text == "Second" && ReferenceEquals(news.newsPic2.texture, UnityEngine.Texture2D.blackTexture)
                    && UnityEngine.Application.LastURL == "https://example.com/Second", "Missing middle image shifted its title or link.");
                news.OnRight(); news.OnNewsClick();
                Assert(news.newsText2.text == "Third" && ReferenceEquals(news.newsPic2.texture, third)
                    && UnityEngine.Application.LastURL == "https://example.com/Third", "Later success no longer matches its source entry.");
            });
            Case("A feed whose thumbnails all fail remains navigable with local fallback backgrounds", () =>
            {
                MDPro3.Tools.ImageDownloadOverride = (url, token) => Task.FromResult<UnityEngine.Texture2D>(null);
                var news = News(Feed(Item("First unavailable image", "missing-first"), Item("Second unavailable image", "missing-second")));
                news.LoadNews(); news.OnRight(); news.OnNewsClick();
                Assert(news.showing && Count(news) == 2 && ReferenceEquals(news.newsPic.texture, UnityEngine.Texture2D.blackTexture)
                    && ReferenceEquals(news.newsPic2.texture, UnityEngine.Texture2D.blackTexture) && news.newsText2.text == "Second unavailable image"
                    && UnityEngine.Application.LastURL == "https://example.com/Second unavailable image", "All-failed feed lost navigation or its English source link.");
            });
            Case("A faulted image task remains observable and leaves the optional news entry usable", () =>
            {
                MDPro3.Tools.ImageDownloadOverride = (url, token) => Task.FromException<UnityEngine.Texture2D>(new InvalidOperationException("Synthetic image failure"));
                var news = News(Feed(Item("Image unavailable", "fault")));
                news.LoadNews();
                Assert(news.showing && ReferenceEquals(news.newsPic.texture, UnityEngine.Texture2D.blackTexture), "Faulted image task prevented the news fallback.");
                Assert(UnityEngine.Debug.Warnings.Count > 0, "Unexpected image task failure was silently discarded.");
            });
            Case("Reloading news resets its entries and releases only the previous owned thumbnails", () =>
            {
                var oldImage = new UnityEngine.Texture2D("Old image");
                var newImage = new UnityEngine.Texture2D("New image");
                MDPro3.Tools.ImageDownloadOverride = (url, token) => Task.FromResult(url == "old" ? oldImage : newImage);
                var news = News(Feed(Item("Old entry", "old")));
                news.LoadNews();
                news.news = Feed(Item("New entry", "new"));
                news.LoadNews(); news.OnNewsClick();
                Assert(oldImage.IsDestroyed && !newImage.IsDestroyed && ReferenceEquals(news.newsPic.texture, newImage)
                    && news.newsText.text == "New entry" && Count(news) == 1 && UnityEngine.Application.LastURL == "https://example.com/New entry",
                    "Reload retained stale images or detached the new image from its entry.");
            });
            Case("News text and links use a stable snapshot until the next explicit reload", () =>
            {
                var news = News(Feed(Item("Original entry", "original")));
                news.LoadNews();
                news.news.EnglishUS = new[] { Item("Unloaded replacement", "replacement") };
                news.OnNewsClick();
                Assert(news.newsText.text == "Original entry" && UnityEngine.Application.LastURL == "https://example.com/Original entry",
                    "A changed feed paired an old image/title with an unloaded replacement link.");
            });
            Case("A superseded image completion cannot repaint the new news generation", () =>
            {
                var pending = new TaskCompletionSource<UnityEngine.Texture2D>();
                var stale = new UnityEngine.Texture2D("Superseded image");
                var current = new UnityEngine.Texture2D("Current image");
                MDPro3.Tools.ImageDownloadOverride = (url, token) => url == "slow" ? pending.Task : Task.FromResult(current);
                var news = News(Feed(Item("Old delayed entry", "slow")));
                news.LoadNews();
                var oldToken = MDPro3.Tools.ImageRequests[0].Token;
                news.news = Feed(Item("Current entry", "current")); news.LoadNews();
                Assert(oldToken.IsCancellationRequested, "Reload did not cancel the old request generation.");
                pending.SetResult(stale);
                Eventually(() => stale.IsDestroyed, "Late old image was not released.");
                Assert(ReferenceEquals(news.newsPic.texture, current) && news.newsText.text == "Current entry"
                    && !current.IsDestroyed, "A late old completion replaced the current entry.");
            });
            Case("Destroying a news manager cancels pending loads and disposes late returned textures", () =>
            {
                var pending = new TaskCompletionSource<UnityEngine.Texture2D>();
                var late = new UnityEngine.Texture2D("Late destroyed owner image");
                MDPro3.Tools.ImageDownloadOverride = (url, token) => pending.Task;
                var news = News(Feed(Item("Delayed entry", "slow")));
                news.LoadNews(); var token = MDPro3.Tools.ImageRequests[0].Token;
                DestroyNews(news);
                Assert(token.IsCancellationRequested, "Owner destruction did not cancel the pending request.");
                pending.SetResult(late);
                Eventually(() => late.IsDestroyed, "Destroyed owner leaked the late downloaded texture.");
                Assert(!news.showing && !ReferenceEquals(news.newsPic.texture, late), "Destroyed owner assigned or showed a late image.");
            });
            Case("A cancellation-obeying pending task exits without assigning or warning", () =>
            {
                var pending = new TaskCompletionSource<UnityEngine.Texture2D>();
                MDPro3.Tools.ImageDownloadOverride = (url, token) =>
                {
                    token.Register(() => pending.TrySetCanceled(token));
                    return pending.Task;
                };
                var news = News(Feed(Item("Pending cancellation", "cancel")));
                news.LoadNews(); DestroyNews(news);
                Eventually(() => pending.Task.IsCanceled, "The active request did not receive cancellation.");
                Assert(!news.showing && news.newsPic.texture == null && UnityEngine.Debug.Warnings.Count == 0,
                    "Cancellation assigned an image or was incorrectly reported as a download failure.");
            });
            Case("Closing before the first image completes does not reopen the news panel", () =>
            {
                var pending = new TaskCompletionSource<UnityEngine.Texture2D>();
                var image = new UnityEngine.Texture2D("Closed panel image");
                MDPro3.Tools.ImageDownloadOverride = (url, token) => pending.Task;
                var news = News(Feed(Item("Delayed entry", "slow")));
                news.LoadNews(); news.OnClose(); pending.SetResult(image);
                Assert(!news.showing, "A late first image reopened a panel the user closed.");
                DestroyNews(news);
                Eventually(() => image.IsDestroyed, "Closing then destroying the owner leaked its completed image.");
                Assert(!news.showing, "A delayed callback reopened the closed/destroyed panel.");
            });
            Case("Stopping Play discards a late image without showing stale news", () =>
            {
                var pending = new TaskCompletionSource<UnityEngine.Texture2D>();
                var image = new UnityEngine.Texture2D("Stopped Play image");
                MDPro3.Tools.ImageDownloadOverride = (url, token) => pending.Task;
                var news = News(Feed(Item("Delayed entry", "slow")));
                news.LoadNews(); UnityEngine.Application.isPlaying = false; pending.SetResult(image);
                Eventually(() => image.IsDestroyed, "Stopping Play retained an unused completed image.");
                Assert(!news.showing && !ReferenceEquals(news.newsPic.texture, image), "A stopped game showed the late news entry.");
            });
            Console.WriteLine("PASS "+passed+" production-source English migration scenarios; controlled Unity/HTTP APIs, not Unity Play/render/build.");return 0;
        }
        finally{Directory.Delete(temp,true);}
    }
}
