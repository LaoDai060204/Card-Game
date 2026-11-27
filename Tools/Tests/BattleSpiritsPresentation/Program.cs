using System;
using System.Linq;
using MDPro3.BattleSpirits.Visuals;
using UnityEngine;
using UnityEngine.UI;
static class Program {
static int checks;
static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
static bool Close(float a,float b)=>Math.Abs(a-b)<.001f;
static bool Under(GameObject obj,Transform ancestor){for(var p=obj.transform;p!=null;p=p.parent)if(p==ancestor)return true;return false;}
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
var canvasObject=new GameObject("native UI",typeof(RectTransform)){layer=5};
var canvas=canvasObject.AddComponent<Canvas>();canvas.scaleFactor=2;canvas.transform.localScale=new Vector3(2,2,1);
image.gameObject.layer=5;image.transform.SetParent(canvas.transform,false);image.transform.localScale=new Vector3(.75f,.75f,1);image.canvas=canvas;
var original=new Texture2D();image.texture=original;
host.Setup(image);Check(image.texture!=original,"Renderer owns render target");
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
T Field<T>(string name)=>(T)typeof(BattleFieldRenderer).GetField(name,flags).GetValue(host);
void Invoke(string name,params object[] args)=>typeof(BattleFieldRenderer).GetMethod(name,flags).Invoke(host,args);
var camera=Field<Camera>("renderCamera");
Check(!camera.orthographic&&Close(camera.fieldOfView,42),"Field uses the approved 42-degree perspective");
var fit=Math.Max(1.25f,2.1f/camera.aspect);
Check(Close(camera.transform.localPosition.y,11*fit)&&Close(camera.transform.localPosition.z,2.2f+11*fit),"Camera fits the approved target and offset to the actual viewport");
var targetTexture=(RenderTexture)image.texture;
Check(targetTexture.width==1200&&targetTexture.height==900,"Render target measures inherited native UI scale in screen pixels");
image.rectTransform.rect=new Rect(-960,-540,1920,1080);image.transform.localScale=Vector3.one;Invoke("RefreshSize");
targetTexture=(RenderTexture)image.texture;
Check(targetTexture.width==3840&&targetTexture.height==2160,"Native 4K viewport stays sharp within the pixel budget");
image.rectTransform.rect=new Rect(-400,-300,800,600);image.transform.localScale=new Vector3(.75f,.75f,1);Invoke("RefreshSize");
var uiObjects=GameObject.Created.Where(o=>Under(o,image.transform)).ToArray();
Check(uiObjects.Length>25&&uiObjects.All(o=>o.layer==5),"Every field label, resource count and Skip control inherits the native UI camera layer");
var world=Field<GameObject>("world");
Check(GameObject.Created.Where(o=>Under(o,world.transform)).All(o=>o.layer==30),"Off-screen geometry stays isolated on the presentation layer");
Check(camera.cullingMask==(1<<30),"Field camera renders only its isolated stage");
var table=GameObject.Created.Single(o=>o.name=="Woven navy arena");
Check(Close(table.transform.localScale.x,15.6f)&&Close(table.transform.localScale.y,15.2f),"Native table uses the supplied arena dimensions");
var printed=GameObject.Created.Single(o=>o.name=="Uncropped printed face");
var faceUv=printed.GetComponent<MeshRenderer>().sharedMaterial.GetVector("_UvRect");
Check(Close(faceUv.x,0)&&Close(faceUv.y,1)&&Close(faceUv.z,1)&&Close(faceUv.w,-1),"Uncropped printed faces orient their top toward the opponent after the face-up plane turn");
foreach(var zone in new[]{("BURST",-6.2f,1.45f),("DECK",6.2f,1.45f),("LIFE",-6.2f,3.85f),("RESERVE",-6.2f,6.2f),("TRASH CARD",6.2f,4f),("TRASH CORE",6.2f,6.25f)}){
var meshes=GameObject.Created.Where(o=>o.name==zone.Item1+" zone").ToArray();
Check(meshes.Length==2&&meshes.All(o=>Close(o.transform.localPosition.x,zone.Item2)&&Close(o.transform.localPosition.z,zone.Item3)),"Mirrored "+zone.Item1+" zone retains the approved resource geometry");
}
Check(GameObject.Created.Where(o=>o.name=="Mirrored resource zones").Select(o=>o.transform.localRotation.y).OrderBy(a=>a).SequenceEqual(new[]{0f,180f}),"Opponent zones rotate a half turn");
Invoke("LateUpdate");Check(camera.enabled,"Visible field schedules its camera through URP");
host.SetPreferences(true,true);Invoke("LateUpdate");Invoke("LateUpdate");Check(!camera.enabled,"Static reduced-motion field does not redraw continuously");
image.gameObject.SetActive(false);Invoke("LateUpdate");Check(!camera.enabled,"Hidden native field stops its camera");
image.gameObject.SetActive(true);Invoke("LateUpdate");Check(camera.enabled,"Reopening the native field redraws its render target");
host.SetPreferences(false,true);
string MatPath()=>(string)typeof(BattleFieldRenderer).GetField("playmatPath",flags).GetValue(host);
void RefreshMat()=>typeof(BattleFieldRenderer).GetMethod("RefreshSelectedPlaymat",flags).Invoke(host,null);
Check(MatPath()=="field.png","Selected Battle Spirits mat is used during native field setup");
int resolutions=MDPro3.BattleSpirits.BattleCosmetics.Resolutions;RefreshMat();
Check(MDPro3.BattleSpirits.BattleCosmetics.Resolutions==resolutions,"Stable or Random field selection is resolved only once");
foreach(var code in new[]{"1099002","1099003"}){MDPro3.Config.Field=code;RefreshMat();Check(MatPath()==(code=="1099002"?"play-sheet.png":"play-sheet-white.png"),"Appearance field changes update the native field");}
MDPro3.Config.Field="0";RefreshMat();Check(MatPath()==string.Empty,"None field choice restores the readable navy table");
foreach(var stale in new string[]{null,"1","1090001"}){MDPro3.Config.Field=stale;RefreshMat();Check(MatPath()==string.Empty,"Missing or old field settings default to the approved navy arena");}
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
host.SetState(new BattlePresentationState{cards=new[]{new BattleCardPresentation{instanceId=3,cardId="26RBS03-022",level=2,bp=11000}}});
host.NotifyCommittedSummon(3,"pisketona-committed");Check(host.IsPresentationBusy,"Explicitly approved Pisketona starts the same committed boss path");
Check(Field<Text>("bossBp").text.Contains("11,000"),"Boss BP comes from committed engine state");
host.SkipPresentation();
var manyCards=Enumerable.Range(10,13).Select(i=>new BattleCardPresentation{instanceId=i,cardId="ordinary",owner=0}).ToArray();
host.SetState(new BattlePresentationState{cards=manyCards,players=new[]{new BattlePlayerPresentation{playerIndex=0,life=5,reserveNormal=27,reserveSoul=true,trashNormal=3,deckCount=29,trashCardCount=7}}});
Check(Field<System.Collections.IDictionary>("actors").Count==13,"Native field has no decorative five-card population limit");
foreach(var item in manyCards)Check(host.TryGetCardViewport(item.instanceId,out var visible)&&visible.width>0,"Every crowded-field card keeps projected inspection bounds");
Check(GameObject.Created.Any(o=>o.GetComponent<Text>()?.text=="27 Core + Soul"),"Exact resource counts remain readable beyond the visible icon stack");
Check(GameObject.Created.Any(o=>o.GetComponent<Text>()?.text=="7 cards"),"Discard-card count is rendered separately from Trash Core");
Check(!GameObject.Created.Single(o=>o.name=="Face-down Burst"&&o.transform.parent.gameObject.name=="Player 0 resources").activeSelf,"An empty engine Burst state never invents a face-down card");
host.Dispose();host.Dispose();Check(image.texture==original,"Idempotent cleanup restores native field texture");
Console.WriteLine("PASS: "+checks+" production presentation smoke assertions (Unity API stubs; not Unity Play/build).");
}}
