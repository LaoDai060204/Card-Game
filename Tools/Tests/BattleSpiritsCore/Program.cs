using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using MDPro3.BattleSpirits.Core;

static class CoreTests
{
    static int checks;
    static CardDefinition[] catalog;
    static JsonSerializerOptions json = new JsonSerializerOptions { IncludeFields = true, Converters = { new JsonStringEnumConverter() } };
    static void Check(bool condition, string why) { checks++; if (!condition) throw new Exception(why); }
    static void Execute(BattleMatch m, int actor, BattleCommand c) { Check(m.TryExecute(actor, c, out var error), c.action + ": " + error); m.AssertInvariants(); }
    static void Act(BattleMatch m, string action) => Execute(m, m.DecisionPlayerIndex, new BattleCommand { action = action });
    static BattleMatch Match() => BattleMatch.Create(catalog, Enumerable.Repeat("26RBS01-003",40).ToArray(), Enumerable.Repeat("26RBS01-003",40).ToArray(), validateDecks:false);
    static string Serialize(BattleMatch m) => JsonSerializer.Serialize(m.State,json);
    static void Blank(BattleMatch m, MatchPhase phase=MatchPhase.Main, int active=0)
    {
        foreach(var p in m.State.Players)
        {
            p.Deck.Clear(); p.Hand.Clear(); p.Discard.Clear(); p.Removed.Clear(); p.Revealed.Clear(); p.Field.Clear();
            p.Deck.AddRange(Enumerable.Repeat("26RBS01-003",40)); p.Life=5; p.Reserve.Normal=3; p.Reserve.Soul=true; p.Trash.Normal=0; p.Trash.Soul=false; p.CoreGains=0;
        }
        m.State.ActivePlayerIndex=active; m.State.TurnNumber=2; m.State.Phase=phase; m.State.PendingChoice=null; m.State.PendingAttackerId=m.State.PendingBlockerId=m.State.FlashPlayerIndex=-1;
    }
    static void Hand(BattleMatch m,int owner,string id) { var p=m.State.Players[owner]; p.Deck.RemoveAt(0); p.Hand.Add(id); }
    static void Trash(BattleMatch m,int owner,string id) { var p=m.State.Players[owner]; p.Deck.RemoveAt(0); p.Discard.Add(id); }
    static SpiritState Field(BattleMatch m,int owner,string id,int normal,bool soul=false)
    {
        var p=m.State.Players[owner]; p.Deck.RemoveAt(0); p.Reserve.Normal-=normal; if(soul) p.Reserve.Soul=false;
        var s=new SpiritState { Owner=owner, InstanceId=100+owner*20+p.Field.Count, Card=catalog.Single(c=>c.id==id), Cores=new CorePool { Normal=normal,Soul=soul } }; p.Field.Add(s); return s;
    }
    static void Extra(BattleMatch m,int owner,int count) { m.State.Players[owner].Reserve.Normal+=count; m.State.Players[owner].CoreGains+=count; }
    static void ChooseFirst(BattleMatch m)
    {
        int guard=0; while(m.State.PendingChoice!=null) { if(++guard>100) throw new Exception("Choice loop"); var c=m.State.PendingChoice; Execute(m,c.Owner,new BattleCommand { action="choose", selection=Enumerable.Range(0,c.Min).ToArray() }); }
    }
    static void Main(string[] args)
    {
        catalog=JsonSerializer.Deserialize<CardsDatabase>(File.ReadAllText(Path.Combine(args[0],"Assets/StreamingAssets/BattleSpirits/cards.json")),json).cards;
        Check(catalog.Length==62,"Reviewed roster62"); Check(catalog.All(c=>c.IsPlayable),"All actual reviewed definitions playable with explicit handlers");
        OpeningAndSteps(); SnapshotsAndRejectedCommands(); PaymentsAndSuccession(); InteractiveCostsAndSwap(); BattleAndHeavyRefresh(); PendingDestructionAndEnd();
        Console.WriteLine("PASS Battle Spirits core: "+checks+" checks.");
    }
    static void OpeningAndSteps()
    {
        var m=Match();
        Check(m.State.Players.All(p=>p.Life==5&&p.Reserve.Normal==3&&p.Reserve.Soul&&p.Hand.Count==4&&p.Deck.Count==36),"Opening5life3reserve1Soul4hand");
        Execute(m,0,new BattleCommand { action="opening",redraw=true }); Check(m.State.Players[0].MulliganUsed,"Redraw recorded"); Act(m,"opening");
        Check(m.State.Phase==MatchPhase.Start&&m.State.TurnNumber==1,"VisibleStartStep"); Act(m,"advance"); Check(m.State.Phase==MatchPhase.Draw&&m.State.Players[0].CoreGains==0&&m.State.Players[0].Hand.Count==5,"First skipsCore but draws");
        Act(m,"advance"); Check(m.State.Phase==MatchPhase.Refresh,"VisibleRefresh"); Act(m,"advance"); Check(m.State.Phase==MatchPhase.Main,"FirstMain");
        Act(m,"advance"); Check(m.State.Phase==MatchPhase.End,"First skipsAttackAndSecondMain"); Act(m,"advance"); Check(m.State.ActivePlayerIndex==1&&m.State.Phase==MatchPhase.Start,"NextPlayerStart");
        Act(m,"advance"); Check(m.State.Phase==MatchPhase.Core&&m.State.Players[1].CoreGains==1,"SecondplayerCore");
        Act(m,"advance"); Act(m,"advance"); Act(m,"advance"); Act(m,"advance"); Check(m.State.Phase==MatchPhase.Attack,"VisibleAttackStep"); Act(m,"advance"); Check(m.State.Phase==MatchPhase.SecondMain,"StandardSecondMain"); Act(m,"advance"); Check(m.State.Phase==MatchPhase.End,"VisibleEnd");
    }
    static void SnapshotsAndRejectedCommands()
    {
        var m=Match(); var view=m.GetSnapshot(0); Check(view.Players[0].Hand.Length==4&&view.Players[1].Hand.Length==0&&view.Players[1].HandCount==4,"Owner filtering");
        view.Players[0].Hand[0]="tampered"; view.Players[0].ReserveNormal=400; Check(m.State.Players[0].Hand[0]!="tampered"&&m.State.Players[0].Reserve.Normal==3,"Snapshot detached");
        string before=Serialize(m); Check(!m.TryExecute(1,new BattleCommand { action="opening" },out _),"Reject outofturn"); Check(before==Serialize(m),"Rejection leaves state unchanged");
        Check(m.GetLegalCommands(1).Count==0,"No opposing legalactions");
        Blank(m,MatchPhase.Attack); var attacker=Field(m,0,"26RBS01-003",1); m.RebasePuzzleState();
        m.QueueChoice(0,"Resolve this effect first",1,1,new[]{new ChoiceOption {key="effect",label="Resolve"}},_=>{}); before=Serialize(m);
        Check(!m.TryAttack(0,attacker.InstanceId,out _)&&before==Serialize(m),"Direct attack API cannot bypass a pending effect choice");
        Blank(m); Hand(m,0,"26RBS03-004"); m.RebasePuzzleState(); before=Serialize(m);
        Check(!m.TryExecute(0,new BattleCommand { action="summon",index=0,payment=new[]{new CoreTransfer {source=-1,normal=99}} },out _),"Reject overpayment"); Check(before==Serialize(m),"Invalidpayment atomic");
    }
    static void PaymentsAndSuccession()
    {
        var m=Match(); Blank(m); Extra(m,0,10); Hand(m,0,"26RBS03-004"); var target=Field(m,1,"26RBS01-003",1); m.RebasePuzzleState();
        Execute(m,0,new BattleCommand { action="summon",index=0,payment=new[]{new CoreTransfer {source=-1,normal=3,soul=true}} });
        Check(m.State.Players[0].Trash.Soul,"ExplicitSoulpaid"); Check(m.State.PendingChoice!=null,"SoulpaidLumiuscanselect4000BP"); ChooseFirst(m); Check(m.FindSpirit(target.InstanceId)==null,"Summon effect resolves after payment");
        m=Match(); Blank(m); Extra(m,0,12); Hand(m,0,"26RBS01-X03"); Trash(m,0,"26RSD01-014"); Trash(m,0,"26RBS01-003"); Field(m,0,"26RBS01-003",1); Field(m,0,"26RBS01-003",1); m.RebasePuzzleState();
        Check(m.GetSummonCost(0,m.GetCard("26RBS01-X03"))==5,"Twofieldsymbols");
        Execute(m,0,new BattleCommand {action="summon",index=0,succession=new[]{"26RSD01-014","26RBS01-003"}});
        Check(m.State.Players[0].Trash.Normal==3&&m.State.Players[0].Removed.Count==2,"SuccessionMagicEXandSpiritEXbeforefieldslots");
        m=Match(); Blank(m); Extra(m,0,12); Hand(m,0,"26RBS03-X01"); Trash(m,0,"26RSD01-014"); Trash(m,0,"26RBS01-003"); Trash(m,0,"26RBS01-003"); Trash(m,0,"26RSD03-014"); m.RebasePuzzleState();
        string before=Serialize(m); Check(!m.TryExecute(0,new BattleCommand {action="summon",index=0,succession=new[]{"26RSD01-014","26RBS01-003","26RBS01-003"}},out _),"RejectEXbeyondprintedsymbolslots"); Check(before==Serialize(m),"ExcessSuccessionatomic");
        Check(!m.TryExecute(0,new BattleCommand {action="summon",index=0,succession=new[]{"26RSD03-014"}},out _),"RejectoffcolorSuccession");
        m=Match(); Blank(m); Hand(m,0,"26RSD01-014"); Field(m,0,"26RBS01-003",1); m.RebasePuzzleState(); Execute(m,0,new BattleCommand {action="magic",index=0,alternateCost=true}); Check(m.State.Players[0].Trash.Soul&&m.State.Players[0].Trash.Normal==0,"SoulMagicpaysonlySoul");
        m=Match(); Blank(m); Hand(m,0,"26RSD01-014"); m.RebasePuzzleState(); before=Serialize(m); Check(!m.TryExecute(0,new BattleCommand {action="magic",index=0,alternateCost=true},out _)&&before==Serialize(m),"SoulMagicmatchingfieldrequired");
    }
    static void BattleAndHeavyRefresh()
    {
        var m=Match(); Blank(m,MatchPhase.Attack); var a=Field(m,0,"26RBS01-003",1); var b=Field(m,1,"26RBS01-003",1); m.RebasePuzzleState();
        Execute(m,0,new BattleCommand{action="attack",source=a.InstanceId}); Check(m.State.Phase==MatchPhase.FlashBeforeBlock&&m.DecisionPlayerIndex==1,"DefenderfirstFlash"); Act(m,"pass"); Act(m,"pass"); Check(m.State.Phase==MatchPhase.Block,"TwoPassesopensBlock");
        Execute(m,1,new BattleCommand{action="block",source=b.InstanceId}); Check(m.State.Phase==MatchPhase.FlashAfterBlock&&m.DecisionPlayerIndex==1,"DefenderfirstFlashafterBlock"); Act(m,"pass"); Act(m,"pass"); Check(m.State.Players.All(p=>p.Field.Count==0)&&m.State.Phase==MatchPhase.Attack,"BPtiebothdestroyed");
        m=Match(); Blank(m,MatchPhase.Draw); a=Field(m,0,"26RBS01-003",1); a.Exhausted=true; a.HeavyExhausted=true; m.RebasePuzzleState(); Act(m,"advance"); Check(a.Exhausted&&!a.HeavyExhausted,"HeavyRefreshoneorientation");
        m=Match(); Blank(m,MatchPhase.Attack); a=Field(m,0,"26RBS01-003",1); m.RebasePuzzleState(); Execute(m,0,new BattleCommand{action="attack",source=a.InstanceId}); Act(m,"pass"); Act(m,"pass"); Execute(m,1,new BattleCommand{action="take-life"}); Check(m.State.Players[1].Life==4&&m.State.Players[1].Reserve.Normal==4,"LifeToReserve");
    }
    static void Pick(BattleMatch m, string key)
    {
        var c=m.State.PendingChoice; Check(c!=null,"Owner choice exists for "+key);
        int selected=Array.FindIndex(c.Options,o=>o.key==key); Check(selected>=0,"Choice exposes "+key);
        Execute(m,c.Owner,new BattleCommand { action="choose",selection=new[]{selected} });
    }
    static void InteractiveCostsAndSwap()
    {
        var m=Match(); Blank(m); Extra(m,0,12); Hand(m,0,"26RBS03-004"); var donor=Field(m,0,"26RBS01-003",1,true); m.RebasePuzzleState();
        var manual=m.GetLegalCommands(0).First(c=>c.action=="summon"&&c.chooseCores);
        int paymentCost=m.GetSummonCost(0,m.GetCard("26RBS03-004"));
        Execute(m,0,manual); Check(m.State.PendingChoice.Owner==0&&m.State.Players[0].Hand.Contains("26RBS03-004"),"Manual payment waits without consuming card");
        Pick(m,"normal:"+donor.InstanceId); for(int i=1;i<paymentCost;i++) Pick(m,"normal:-1"); Pick(m,"soul:"+donor.InstanceId);
        Check(m.FindSpirit(donor.InstanceId)==null&&m.State.Players[0].Discard.Contains(donor.Card.id),"Payment and placement may deplete the selected donor");
        var lumius=m.State.Players[0].Field.Single(); Check(lumius.Card.id=="26RBS03-004"&&lumius.Cores.Soul&&lumius.Cores.Total==1,"Owner chooses Soul for initial placement");
        m=Match(); Blank(m); Extra(m,0,12); Hand(m,0,"26RBS03-X01"); Trash(m,0,"26RBS01-003"); Trash(m,0,"26RSD03-014"); Trash(m,0,"26RSD01-014"); m.RebasePuzzleState();
        var succession=m.GetLegalCommands(0).First(c=>c.action=="summon"&&c.chooseSuccession); Execute(m,0,succession);
        Check(!m.State.PendingChoice.Options.Any(o=>o.key=="trash:1"),"Off-color EX does not appear as legal Succession option");
        Pick(m,"trash:0"); Pick(m,"trash:2"); Pick(m,"finish"); ChooseFirst(m);
        Check(m.State.Players[0].Removed.SequenceEqual(new[]{"26RSD01-014","26RBS01-003"})&&m.State.Players[0].Discard.SequenceEqual(new[]{"26RSD03-014"}),"Non-prefix mixed Spirit/Magic Succession is reachable through legal commands");
        m=Match(); Blank(m); Extra(m,0,3); var first=Field(m,0,"26RBS01-003",0,true); var second=Field(m,0,"26RBS03-004",1); m.RebasePuzzleState();
        var swap=m.GetLegalCommands(0).First(c=>c.action=="swap"&&c.source==first.InstanceId&&c.target==second.InstanceId); Execute(m,0,swap);
        Check(m.FindSpirit(first.InstanceId)==first&&first.Cores.Normal==1&&!first.Cores.Soul&&second.Cores.Soul&&second.Cores.Normal==0,"Soul/normal swap is atomic and preserves both Spirits");
    }
    static void PendingDestructionAndEnd()
    {
        var m=Match(); Blank(m,MatchPhase.Attack); Extra(m,0,3); var a=Field(m,0,"26RBS03-009",3); var b=Field(m,1,"26RBS01-003",1); m.RebasePuzzleState();
        Execute(m,0,new BattleCommand{action="attack",source=a.InstanceId}); Act(m,"pass"); Act(m,"pass"); Execute(m,1,new BattleCommand{action="block",source=b.InstanceId}); Act(m,"pass"); Act(m,"pass"); Check(a.PendingRemoval&&m.FindSpirit(a.InstanceId)==a&&a.Cores.Normal==3,"DestroyedSpiritretainscoresandidentityuntiltriggerresolves"); ChooseFirst(m); Check(m.FindSpirit(a.InstanceId)==null&&m.FindSpirit(b.InstanceId)==null,"Destroytriggerresolvesbeforefinalcleanup"); m.AssertInvariants();
        m=Match(); Blank(m,MatchPhase.SecondMain); Extra(m,0,5); a=Field(m,0,"26RBS03-X01",3); m.State.Players[0].Reserve.Normal--; m.State.Players[0].Trash.Normal++; m.RebasePuzzleState(); Act(m,"advance"); Check(m.State.Phase==MatchPhase.End&&m.State.PendingChoice!=null,"Endtriggerpausesstep"); string before=Serialize(m); Check(!m.TryExecute(0,new BattleCommand{action="advance"},out _)&&before==Serialize(m),"CannotSkipEndChoice"); Execute(m,0,new BattleCommand{action="choose",selection=new[]{0}}); Check(a.Cores.Normal==4&&m.State.Players[0].Trash.Normal==0,"NaturalLv2SuperUnleashEndEffect");
    }
}
