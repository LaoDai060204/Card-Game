using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    private static void Case(string name,Action test){test();passed++;Console.WriteLine("PASS "+name);}
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
    private static NewsManager News(MyCardNews source)=>new NewsManager{news=source,newsPic=new RawImage(),newsPic2=new RawImage(),newsText=new Text(),newsText2=new Text(),textCount=new Text()};
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
            Console.WriteLine("PASS "+passed+" production-source English migration scenarios; controlled Unity/HTTP APIs, not Unity Play/render/build.");return 0;
        }
        finally{Directory.Delete(temp,true);}
    }
}
