using System;
using MDPro3.BattleSpirits.Visuals;
using UnityEngine;
using UnityEngine.UI;
static class Program {
static int checks;
static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
static void Main(){
var mesh=BattleGeometry.Frame(14.4f,14.4f,.03f,.32f);
Check(mesh.vertices.Length==88,"Rounded frame topology");
for(int i=0;i<mesh.triangles.Length;i+=3){var a=mesh.vertices[mesh.triangles[i]];var b=mesh.vertices[mesh.triangles[i+1]];var c=mesh.vertices[mesh.triangles[i+2]];Check(Vector3.Cross(b-a,c-a).y>0,"Silver inlay must face the camera above the table");}
var particles=BattleGeometry.Motes();Check(particles.vertices.Length==96&&particles.triangles.Length==144,"Bounded 24-mote geometry");
var host=new GameObject("host").AddComponent<BattleFieldRenderer>();
var card=new BattleCardPresentation{instanceId=1,cardId="26RBS03-X01",owner=0,level=1,bp=6000,normalCores=1};
var state=new BattlePresentationState{cards=new[]{card}};
host.SetState(state);card.cardId="missing";
var image=new GameObject("native field",typeof(RectTransform)).AddComponent<RawImage>();
var original=new Texture2D();image.texture=original;
host.Setup(image);Check(image.texture!=original,"Renderer owns render target");
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
string MatPath()=>(string)typeof(BattleFieldRenderer).GetField("playmatPath",flags).GetValue(host);
void RefreshMat()=>typeof(BattleFieldRenderer).GetMethod("RefreshSelectedPlaymat",flags).Invoke(host,null);
Check(MatPath()=="field.png","Selected Battle Spirits mat is used during native field setup");
int resolutions=MDPro3.BattleSpirits.BattleCosmetics.Resolutions;RefreshMat();
Check(MDPro3.BattleSpirits.BattleCosmetics.Resolutions==resolutions,"Stable or Random field selection is resolved only once");
foreach(var code in new[]{"1099002","1099003"}){MDPro3.Config.Field=code;RefreshMat();Check(MatPath()==(code=="1099002"?"play-sheet.png":"play-sheet-white.png"),"Appearance field changes update the native field");}
MDPro3.Config.Field="0";RefreshMat();Check(MatPath()==string.Empty,"None field choice restores the readable navy table");
foreach(var path in new[]{"field.png","play-sheet.png","play-sheet-white.png"}){var uv=BattleFieldRenderer.PlaymatArtBounds(path);Check(uv.x>0&&uv.y>0&&uv.z>0&&uv.w>0&&uv.x+uv.z<1&&uv.y+uv.w<1,"Field crops exclude outer play-sheet border and rules");}
Check(host.TryGetCardViewport(1,out var bounds)&&bounds.width>0,"Snapshot is copied before native setup");
int busyTransitions=0;host.PresentationBusyChanged+=v=>busyTransitions++;
host.NotifyCommittedSummon(1,"summon-1");Check(host.IsPresentationBusy,"Committed approved boss starts presentation");
Check(!host.TryGetCardViewport(1,out _),"Field labels are hidden while boss stage covers field");
host.SkipPresentation();Check(!host.IsPresentationBusy&&busyTransitions==2,"Skip settles busy state without changing snapshot");
host.NotifyCommittedSummon(1,"summon-1");Check(!host.IsPresentationBusy,"Duplicate committed events cannot replay boss");
host.SetPreferences(true,true);host.NotifyCommittedSummon(1,"summon-2");Check(!host.IsPresentationBusy,"Reduced motion stays static");
Check(host.TryGetCardViewport(1,out _),"Skip and reduced motion preserve committed card");
host.SetPreferences(false,true);
host.NotifyCommittedSummon(2,"summon-pending");Check(!host.IsPresentationBusy,"Pre-snapshot event queues safely");
host.SetState(new BattlePresentationState{cards=new[]{new BattleCardPresentation{instanceId=2,cardId="26RBS03-X01"}}});Check(host.IsPresentationBusy,"Pending committed event resolves with new snapshot");
host.SetState(new BattlePresentationState());Check(!host.IsPresentationBusy,"Removing a presented card releases presentation");
host.Dispose();host.Dispose();Check(image.texture==original,"Idempotent cleanup restores native field texture");
Console.WriteLine("PASS: "+checks+" production presentation smoke assertions (Unity API stubs; not Unity Play/build).");
}}
