using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MDPro3;
using UnityEngine;
using UnityEngine.Networking;
internal static class Program
{
    private static int assertions;
    private static void Check(bool condition,string detail) { assertions++; if (!condition) throw new Exception(detail); }
    private static AudioManager Create() => new AudioManager { seR=new AudioSource(),bgmR=new AudioSource(),voiceR=new AudioSource() };
    private static void DestroyManager(AudioManager manager)=>typeof(AudioManager).GetMethod("OnDestroy",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(manager,null);
    public static async Task Main()
    {
        string temporary=Path.Combine(Path.GetTempPath(),"bs-audio-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        Application.streamingAssetsPath=Path.Combine(temporary,"StreamingAssets");
        Application.persistentDataPath=Path.Combine(temporary,"User");
        try
        {
            var manager=Create();
            manager.seR.clip=new AudioClip{name="LegacyPrefabClip"}; manager.bgmR.clip=manager.seR.clip; manager.voiceR.clip=manager.seR.clip;
            manager.Initialize();
            Check(manager.seR.clip==null && manager.bgmR.clip==null && manager.voiceR.clip==null,"Serialized old clips cleared");
            Check(!manager.seR.playOnAwake && !manager.bgmR.playOnAwake && !manager.voiceR.playOnAwake,"Prefab autoplay disabled");
            Check(Resources.Requests.SequenceEqual(new[]{AudioManager.BGM_MENU_MAIN}),"Initialize loads only optional BS menu");
            Check(!manager.bgmR.isPlaying,"Missing optional music remains silent");
            AudioManager.PlaySE("SE_SYS_DECIDE");
            var first=manager.seR.Shots.Last().clip;
            Check(first.name.StartsWith("BattleSpirits_UI_"),"Old event maps to original own tone");
            Check(first.data.Any(x=>x!=0) && first.data.All(x=>!float.IsNaN(x)&&Math.Abs(x)<.25f),"Generated samples are finite and bounded");
            Check(first.frequency==22050 && first.channels==1,"Small cached mono UI tone");
            AudioManager.PlaySE("SE_SYS_DECIDE"); Check(manager.seR.Shots.Count==1,"Repeated event throttled even at time zero");
            Time.unscaledTime+=.081f; AudioManager.PlaySE("SE_SYS_DECIDE"); Check(manager.seR.Shots.Count==2,"Unscaled throttle expires");
            Check(manager.seR.Shots.Last().clip==first,"Repeated tones cached");
            AudioManager.nextMuteSE="SE_MUTE"; AudioManager.PlaySE("SE_MUTE");
            Check(manager.seR.Shots.Count==2 && AudioManager.nextMuteSE==null,"One-shot mute consumed");
            AudioManager.PlaySE("SE_MUTE"); Check(manager.seR.Shots.Count==3,"Subsequent muted event plays");
            foreach(string key in new[]{"HOVER","CONFIRM","CANCEL","CARD","ERROR","GENERIC"}) AudioManager.PlaySE(key);
            Check(AudioClip.Created.Count==6,"All old identifiers map to six bounded clips");
            AudioManager.PlaySE("ERROR2",4); Check(manager.seR.Shots.Last().scale==1,"Event volume bounded high");
            AudioManager.PlaySE("ERROR3",-1); Check(manager.seR.Shots.Last().scale==0,"Event volume bounded low");
            var legacy=new AudioClip{name="BGM_MENU_01"}; int shots=manager.seR.Shots.Count;
            AudioManager.PlaySEClip(legacy); Check(manager.seR.Shots.Count==shots,"Unregistered supplied SE refused");
            AudioManager.PlayVoice(legacy); Check(manager.voiceR.Shots.Count==0,"Unregistered supplied voice refused");
            int requests=Resources.Requests.Count;
            foreach(var invalid in new[]{"Voice/Old","BattleSpirits/Audio/../Old","BattleSpirits/Audio/Bad.wav","BattleSpirits/Audio/","https://old/audio"}) AudioManager.PlayVoiceByResourcePath(invalid);
            Check(Resources.Requests.Count==requests,"Legacy/traversing voice paths never loaded");
            var approved=new AudioClip{name="ApprovedNarration"}; Resources.Clips["BattleSpirits/Audio/Voice/Intro"]=approved;
            AudioManager.PlayVoiceByResourcePath("BattleSpirits/Audio/Voice/Intro");
            Check(manager.voiceR.Shots.Count==1 && manager.voiceR.Shots.Last().clip==approved,"Explicit approved voice allowed");
            AudioManager.PlayVoice(approved); Check(manager.voiceR.Shots.Count==2,"Registered BS voice can replay");
            AudioManager.SetSeVol(-1); AudioManager.SetVoiceVol(2); AudioManager.SetBGMVol(.4f);
            Check(manager.seR.volume==0 && manager.voiceR.volume==1 && manager.bgmR.volume==.4f,"Native volume settings preserved and bounded");
            var music=new AudioClip{name="OwnMenu"}; Resources.Clips[AudioManager.BGM_MENU_MAIN]=music;
            AudioManager.PlayBGM(AudioManager.BGM_MENU_MAIN,.5f);
            Check(manager.bgmR.clip==music && manager.bgmR.isPlaying && manager.bgmR.loop,"Approved music loops");
            Check(Math.Abs(manager.bgmR.volume-.2f)<.00001,"Music scale multiplied by preference");
            int plays=manager.bgmR.playCount; AudioManager.PlayBGM(AudioManager.BGM_MENU_MAIN);
            Check(manager.bgmR.playCount==plays && manager.bgmR.volume==.4f,"Same music updates volume without restart");
            AudioManager.PlayBGM("BGM_MENU_01"); Check(manager.bgmR.clip==null && !manager.bgmR.isPlaying,"Legacy BGM never aliases or falls back");
            AudioManager.PlayBgmNormal("old-field-malformed"); AudioManager.PlayBgmKeyCard(); AudioManager.PlayBgmClimax(); AudioManager.PlayRandomKeyCardBGM();
            Check(Resources.Requests.TakeLast(4).SequenceEqual(new[]{"BattleSpirits/Audio/Duel","BattleSpirits/Audio/Boss","BattleSpirits/Audio/Climax","BattleSpirits/Audio/Boss"}),"Legacy BGM entry APIs map only new explicit BS sections");
            AudioManager.PlayBGM(AudioManager.BGM_MENU_MAIN); AudioSettings.Raise();
            Check(manager.bgmR.isPlaying,"Device changes retain approved music");
            AudioManager.StopBGM(); plays=manager.bgmR.playCount; AudioSettings.Raise();
            Check(manager.bgmR.clip==null && manager.bgmR.playCount==plays,"Stopped music is not restarted on device change");
            int before=manager.seR.Shots.Count; manager.PlayShuffleSE();
            Check(manager.seR.Shots.Count==before+4,"Shuffle uses four own effects without old loads");
            AudioManager.ResetSESource(); AudioManager.PlaySE("SE_SYS_DECIDE"); Check(manager.seR.Shots.Last().clip==first,"Reset source clears repeat throttle");
            int downloads=UnityWebRequestMultimedia.Requests.Count;
            var invalidLoad=await AudioManager.LoadAudioFileUniAsync(Path.Combine(temporary,"legacy.ogg"),AudioType.OGGVORBIS);
            Check(invalidLoad==null && downloads==UnityWebRequestMultimedia.Requests.Count,"External loader rejects old root");
            invalidLoad=await AudioManager.LoadAudioFileUniAsync("http://old/voice.ogg",AudioType.OGGVORBIS);
            Check(invalidLoad==null && downloads==UnityWebRequestMultimedia.Requests.Count,"External loader rejects network path");
            string audioDir=Path.Combine(Application.persistentDataPath,"BattleSpirits","audio"); Directory.CreateDirectory(audioDir);
            string audioPath=Path.Combine(audioDir,"voice.ogg"); File.WriteAllText(audioPath,"");
            var loaded=await AudioManager.LoadAudioFileUniAsync(audioPath,AudioType.OGGVORBIS);
            Check(loaded!=null && UnityWebRequestMultimedia.Requests.Last()==new Uri(audioPath).AbsoluteUri,"Approved local BS file loader retained");
            AudioManager.PlayVoice(loaded); Check(manager.voiceR.Shots.Last().clip==loaded,"Approved downloaded clip registered for playback");
            string traversing=Path.Combine(audioDir,"..","..","..","legacy.ogg"); File.WriteAllText(Path.GetFullPath(traversing),"");
            invalidLoad=await AudioManager.LoadAudioFileUniAsync(traversing,AudioType.OGGVORBIS);
            Check(invalidLoad==null,"Canonical path traversal rejected");
            var iterator=AudioManager.LoadAudioFileAsync(audioPath,AudioType.OGGVORBIS); Check(iterator.MoveNext() && iterator.Current!=null && !iterator.MoveNext(),"Approved coroutine loader retained");
            var oldTones=AudioClip.Created.ToArray(); var next=Create(); next.Initialize();
            Check(oldTones.All(clip=>clip.destroyed) && loaded.destroyed,"Replacement manager releases owned generated/downloaded clips");
            Check(!approved.destroyed && !music.destroyed,"Shared resource assets not destroyed");
            int previous=manager.bgmR.playCount; AudioSettings.Raise(); Check(manager.bgmR.playCount==previous,"Old manager unsubscribed from device event");
            DestroyManager(manager); AudioManager.PlaySE("CONFIRM"); Check(next.seR.Shots.Count==1,"Old OnDestroy cannot clear replacement manager");
            DestroyManager(next); AudioManager.PlaySE("AFTER_DESTROY"); AudioManager.SetSeVol(.4f); AudioManager.SetVoiceVol(.4f); AudioManager.StopBGM();
            Check(next.seR.Shots.Count==1,"Shutdown APIs safe and no new shots");
            Check(Resources.Requests.All(x=>x.StartsWith("BattleSpirits/Audio/",StringComparison.Ordinal)),"Every resource lookup stays in BS audio");
            Console.WriteLine("PASS Battle Spirits audio: "+assertions+" production-source assertions; own tones, explicit assets, no legacy soundtrack/voice loads.");
        }
        finally { Directory.Delete(temporary,true); }
    }
}
