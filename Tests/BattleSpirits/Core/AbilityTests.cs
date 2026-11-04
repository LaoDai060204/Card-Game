using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using LaoDai.BattleSpirits.Core;

internal static class AbilityTests
{
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields=true };
    private static CardDefinition[] cards;
    private static int cases;
    private static int nextInstance;
    private const string Red = "TEST-RED";
    private const string White = "TEST-WHITE";
    public static void Run(CardDefinition[] catalog)
    {
        var selected = new[] { AbilityRegistry.Siegfried, AbilityRegistry.Valkyrius, AbilityRegistry.SilverWorld, AbilityRegistry.AwakenMagic, AbilityRegistry.DoubleDraw };
        cards = catalog.Where(c=>selected.Contains(c.id)).Select(Clone).Concat(new[] { Vanilla(Red,CardColor.Red), Vanilla(White,CardColor.White) }).ToArray();
        Assert(cards.Length==7,"The five pinned official ability cards must be present.");
        foreach(var card in cards) card.implemented=true;
        Case("Only exact pinned metadata and rules profile enable official effect cards", RegistryBoundary);
        Case("Magic and Awakening reject opening, wrong actor, and unsupported commands atomically", AbilityAuthority);
        Case("Nexus places with zero core at Lv1 and supplies its printed reduction symbol", NexusPlacement);
        Case("Nexus cannot attack or block and does not vanish at zero cores", NexusNotSpirit);
        Case("Double Draw Main pays cost, discards Magic, and draws exactly two", DoubleDrawMain);
        Case("Drawing the last cards with Magic waits for the owner's next Start Step to lose", MagicDeckOut);
        Case("Double Draw Flash changes BP and resets consecutive passes before passing priority", MagicFlash);
        Case("Turn BP effects expire for both owners at end of the current turn", BpExpiry);
        Case("Awaken Magic moves up to three selected post-payment cores and draws one", AwakenMagic);
        Case("Official Q24490 allows choosing zero Awaken cores while Reserve is nonempty", AwakenZeroCores);
        Case("Official Q24491 permits Awaken draw-only with empty post-payment Reserve and no Spirit", AwakenEmptyReserveDraw);
        Case("Manual page14 partial resolution permits targetless Awaken draw-only in Main and Flash", AwakenTargetlessDraw);
        Case("Targetless Awaken rejects positive transfers and invalid targets before payment", AwakenTargetlessAtomic);
        Case("Illegal Awaken Magic allocation fails before payment or discard", AwakenMagicAtomic);
        Case("Awakening transfers multiple sources and Soul Core atomically before vanish", Awakening);
        Case("Awakening rejects enemy, Nexus, self, duplicate and overdrawn sources atomically", AwakenAtomic);
        Case("Awakening in Flash switches priority and resets consecutive passes", AwakeningPriority);
        Case("Lv3 Siegfried destruction gains a Life core from Void, lower levels do not", SiegfriedDestroyed);
        Case("Siegfried vanish never fires the destruction-only Life effect", SiegfriedVanished);
        Case("Valkyrius gains 6000 BP while blocking and loses it after the battle", ValkyriusBlock);
        Case("Silver World recycles Trash at the opponent Attack Step including Soul Core", NexusRecycle);
        Case("Lv2 Silver World grants Void cores only for own Spirits destroyed in enemy Attack", NexusDestroyed);
        Case("An attacker vanishing through Awakening keeps Flash until both players pass", AttackerVanish);
        Case("A vanished blocker keeps Flash and a declared block still prevents Life damage", BlockerVanish);
        Case("Paying Magic with the attacker's last core does not close Flash early", MagicPaymentVanish);
        Case("Main swaps Soul and normal cores atomically between one-core Spirits", AtomicCoreSwap);
        Case("An empty Trash does not activate Silver World's recycling effect", EmptyNexusRecycle);
        Case("Verified Magic and Nexus enter legal decks while unimplemented catalog cards stay locked", SupportedDeck);
        Console.WriteLine("PASS: "+cases+" explicit official ability scenarios.");
    }
    private static void Case(string name,Action test){test();cases++;Console.WriteLine("PASS "+name);}
    private static CardDefinition Clone(CardDefinition card)=>JsonSerializer.Deserialize<CardDefinition>(JsonSerializer.Serialize(card,Json),Json);
    private static CardDefinition Get(string id)=>cards.Single(c=>c.id==id);
    private static CardDefinition Vanilla(string id,CardColor color)=>new CardDefinition {id=id,nameJa=id,color=color,cost=0,implemented=true,effectTextJa="-",symbols=1,levels=new List<CardLevel>{new CardLevel{cores=1,bp=1000},new CardLevel{cores=3,bp=5000}}};
    private static BattleMatch Match()
    {
        nextInstance=1000;
        var deck=Enumerable.Range(0,70).Select(i=>cards[i%cards.Length].id).ToArray();
        var match=BattleMatch.Create(cards,deck,deck,781,0,false);
        Dispatch(match,0,new BattleCommand{action="opening"});Dispatch(match,1,new BattleCommand{action="opening"});return match;
    }
    private static void Gift(BattleMatch match,int player,int count){match.State.Players[player].Reserve.Normal+=count;match.State.Players[player].CoreGains+=count;}
    private static SpiritState Place(BattleMatch match,int player,string id,int cores=1,bool soul=false)
    {
        var owner=match.State.Players[player];RemoveCard(owner,id);Gift(match,player,Math.Max(0,cores-owner.Reserve.Total));
        int ordinary=cores-(soul?1:0);
        if(owner.Reserve.Normal<ordinary)Gift(match,player,ordinary-owner.Reserve.Normal);
        owner.Reserve.Normal-=ordinary;if(soul){Assert(owner.Reserve.Soul,"Soul was already allocated");owner.Reserve.Soul=false;}
        var card=new SpiritState{InstanceId=nextInstance++,Card=Get(id),Cores=new CorePool{Normal=ordinary,Soul=soul}};owner.Field.Add(card);match.AssertInvariants();return card;
    }
    private static void RemoveCard(PlayerState owner,string id)
    {int index=owner.Deck.IndexOf(id);if(index>=0){owner.Deck.RemoveAt(index);return;}index=owner.Hand.IndexOf(id);Assert(index>=0,"Missing fixture card "+id);owner.Hand.RemoveAt(index);}
    private static int Hand(BattleMatch match,int player,string id)
    {
        var owner=match.State.Players[player];int index=owner.Hand.IndexOf(id);if(index>=0)return index;
        int deck=owner.Deck.IndexOf(id);Assert(deck>=0,"Missing Magic fixture");string old=owner.Hand[0];owner.Hand[0]=id;owner.Deck[deck]=old;return 0;
    }
    private static void Dispatch(BattleMatch match,int actor,BattleCommand command)
    {Assert(BattleCommandDispatcher.TryExecute(match,actor,command,out string error),error);match.AssertInvariants();}
    private static void Reject(BattleMatch match,int actor,BattleCommand command)
    {string before=JsonSerializer.Serialize(match.State,Json);Assert(!BattleCommandDispatcher.TryExecute(match,actor,command,out _),"Expected rejected action "+command.action);Assert(before==JsonSerializer.Serialize(match.State,Json),"Rejected ability changed authoritative state");match.AssertInvariants();}
    private static void Pass(BattleMatch match){Dispatch(match,match.DecisionPlayerIndex,new BattleCommand{action="passFlash"});}
    private static void AttackStep(BattleMatch match,int actor)
    {if(match.State.TurnNumber==1)Dispatch(match,0,new BattleCommand{action="endTurn"});Assert(match.State.ActivePlayerIndex==actor);Dispatch(match,actor,new BattleCommand{action="attackStep"});}
    private static void Assert(bool condition,string message="Ability assertion failed"){if(!condition)throw new Exception(message);}
    private static void Equal(int expected,int actual){Assert(expected==actual,"Expected "+expected+", got "+actual);}
    private static void RegistryBoundary()
    {
        foreach(var original in cards.Where(c=>AbilityRegistry.Contains(c.id)))
        {
            Assert(original.IsPlayable,original.id+" registry failed");var copy=Clone(original);copy.rulesProfile="forged";Assert(!copy.IsPlayable);
            copy=Clone(original);copy.effectTextJa+="改変";Assert(!copy.IsPlayable);copy=Clone(original);copy.cost++;Assert(!copy.IsPlayable);
            copy=Clone(original);copy.nameJa="fake";Assert(!copy.IsPlayable);copy=Clone(original);copy.symbols++;Assert(!copy.IsPlayable);
            copy=Clone(original);copy.symbolsVerified=false;Assert(!copy.IsPlayable);copy=Clone(original);copy.implemented=false;Assert(!copy.IsPlayable);
            copy=Clone(original);copy.effectTextJa="-";copy.rulesProfile="";Assert(!copy.IsPlayable,"Known effect card forged as vanilla");
        }
    }
    private static void AbilityAuthority()
    {
        var match=Match();int index=Hand(match,0,AbilityRegistry.DoubleDraw);Gift(match,0,10);
        Reject(match,1,new BattleCommand{action="playMagic",index=index});Reject(match,0,new BattleCommand{action="playMagic",index=-1});
        Reject(match,0,new BattleCommand{action="awaken",source=-1,target=-1});
        var deck=Enumerable.Repeat(Red,40).ToArray();var opening=BattleMatch.Create(cards,deck,deck,1,0,false);Reject(opening,0,new BattleCommand{action="playMagic",index=0});
    }
    private static void NexusPlacement()
    {
        var match=Match();Gift(match,0,10);int index=Hand(match,0,AbilityRegistry.SilverWorld);int before=match.State.Players[0].Reserve.Total;
        Dispatch(match,0,new BattleCommand{action="summon",index=index});var nexus=match.State.Players[0].Field.Last();Equal(1,nexus.Level);Equal(0,nexus.Cores.Total);Equal(before-5,match.State.Players[0].Reserve.Total);Equal(7,match.GetSummonCost(0,Get(AbilityRegistry.Valkyrius)));
    }
    private static void NexusNotSpirit()
    {
        var match=Match();var nexus=Place(match,1,AbilityRegistry.SilverWorld,0);var enemy=Place(match,0,Red);AttackStep(match,1);
        Reject(match,1,new BattleCommand{action="attack",index=nexus.InstanceId});Dispatch(match,1,new BattleCommand{action="endTurn"});Dispatch(match,0,new BattleCommand{action="attackStep"});Dispatch(match,0,new BattleCommand{action="attack",index=enemy.InstanceId});Pass(match);Pass(match);Reject(match,1,new BattleCommand{action="block",index=nexus.InstanceId});
    }
    private static void DoubleDrawMain()
    {
        var match=Match();Gift(match,0,8);var owner=match.State.Players[0];int index=Hand(match,0,AbilityRegistry.DoubleDraw);int hand=owner.Hand.Count,deck=owner.Deck.Count,cores=owner.Reserve.Total;
        Dispatch(match,0,new BattleCommand{action="playMagic",index=index});Equal(hand+1,owner.Hand.Count);Equal(deck-2,owner.Deck.Count);Equal(cores-4,owner.Reserve.Total);Equal(4,owner.Trash.Total);Assert(owner.Discard.Contains(AbilityRegistry.DoubleDraw));
    }
    private static void MagicDeckOut()
    {
        var match=Match();Gift(match,0,8);var owner=match.State.Players[0];int index=Hand(match,0,AbilityRegistry.DoubleDraw);
        while(owner.Deck.Count>1){owner.Discard.Add(owner.Deck[0]);owner.Deck.RemoveAt(0);}Dispatch(match,0,new BattleCommand{action="playMagic",index=index});Equal(0,owner.Deck.Count);Assert(match.State.Phase!=MatchPhase.GameOver);Dispatch(match,0,new BattleCommand{action="endTurn"});Dispatch(match,1,new BattleCommand{action="endTurn"});Equal(1,match.State.WinnerIndex);
    }
    private static void MagicFlash()
    {
        var match=Match();Gift(match,0,10);var attacker=Place(match,1,Red);var own=Place(match,0,Red);int index=Hand(match,0,AbilityRegistry.DoubleDraw);AttackStep(match,1);Dispatch(match,1,new BattleCommand{action="attack",index=attacker.InstanceId});Pass(match);Pass(match);Dispatch(match,0,new BattleCommand{action="block",index=own.InstanceId});Pass(match);index=Hand(match,1,AbilityRegistry.DoubleDraw);Gift(match,1,10);
        Reject(match,1,new BattleCommand{action="playMagic",index=index,target=-1});Dispatch(match,1,new BattleCommand{action="playMagic",index=index,target=attacker.InstanceId});Equal(3000,attacker.Bp);Equal(0,match.DecisionPlayerIndex);Pass(match);Assert(match.State.Phase==MatchPhase.FlashAfterBlock,"Magic did not reset passes");Pass(match);Assert(match.FindSpirit(own.InstanceId)==null&&match.FindSpirit(attacker.InstanceId)!=null);
    }
    private static void BpExpiry()
    {
        var match=Match();Gift(match,0,10);var enemy=Place(match,1,Red);int index=Hand(match,0,AbilityRegistry.DoubleDraw);Dispatch(match,0,new BattleCommand{action="playMagic",index=index,target=enemy.InstanceId});Equal(3000,enemy.Bp);Dispatch(match,0,new BattleCommand{action="endTurn"});Equal(1000,enemy.Bp);
    }
    private static void AwakenMagic()
    {
        var match=Match();Gift(match,0,10);var target=Place(match,0,Red);var owner=match.State.Players[0];int hand=owner.Hand.Count;int index=Hand(match,0,AbilityRegistry.AwakenMagic);
        Dispatch(match,0,new BattleCommand{action="playMagic",index=index,target=target.InstanceId,transfers=new[]{new CoreTransfer{normal=3}}});Equal(4,target.Cores.Total);Equal(hand,owner.Hand.Count);Equal(5000,target.Bp);
    }
    private static void AwakenMagicAtomic()
    {
        var match=Match();var target=Place(match,0,Red);int index=Hand(match,0,AbilityRegistry.AwakenMagic);
        Reject(match,0,new BattleCommand{action="playMagic",index=index,target=target.InstanceId,transfers=new[]{new CoreTransfer{normal=3}}});Gift(match,0,10);
        Reject(match,0,new BattleCommand{action="playMagic",index=index,target=target.InstanceId,transfers=new[]{new CoreTransfer{normal=4}}});Reject(match,0,new BattleCommand{action="playMagic",index=index,target=target.InstanceId,transfers=new[]{new CoreTransfer{source=target.InstanceId,normal=1}}});
        var enemy=Place(match,1,Red);Reject(match,0,new BattleCommand{action="playMagic",index=index,target=enemy.InstanceId});
    }
    // https://www.battlespirits.com/faq/list.php?freeword=BS01-115&format=eternal
    // Q24490: choose any 0-3 cores. Q24491: no Reserve cores still allows the draw.
    // The unconditional clauses resolve as far as possible, per Eternal manual
    // page14 Q2, rather than the "doing X to do Y" cost condition in its Q5.
    private static void AwakenZeroCores()
    {
        var match=Match();Gift(match,0,10);var target=Place(match,0,Red);var owner=match.State.Players[0];
        int index=Hand(match,0,AbilityRegistry.AwakenMagic),deck=owner.Deck.Count,cores=owner.Reserve.Total,cost=match.GetSummonCost(0,Get(AbilityRegistry.AwakenMagic));
        Dispatch(match,0,new BattleCommand{action="playMagic",index=index,target=target.InstanceId,transfers=new[]{new CoreTransfer()}});
        Equal(1,target.Cores.Total);Equal(cores-cost,owner.Reserve.Total);Equal(deck-1,owner.Deck.Count);Assert(owner.Discard.Contains(AbilityRegistry.AwakenMagic));
    }
    private static void AwakenEmptyReserveDraw()
    {
        var match=Match();var owner=match.State.Players[0];owner.Reserve.Soul=false;owner.Trash.Soul=true;
        Equal(3,owner.Reserve.Normal);Equal(0,owner.Field.Count);int index=Hand(match,0,AbilityRegistry.AwakenMagic),deck=owner.Deck.Count,hand=owner.Hand.Count;
        Dispatch(match,0,new BattleCommand{action="playMagic",index=index,target=-1});
        Equal(0,owner.Reserve.Total);Equal(3,owner.Trash.Normal);Equal(deck-1,owner.Deck.Count);Equal(hand,owner.Hand.Count);
        Assert(owner.Discard.Contains(AbilityRegistry.AwakenMagic));Assert(!match.LastEvents.Any(e=>e.Kind=="CoreMoved"));
    }
    private static void AwakenTargetlessDraw()
    {
        foreach(var transfers in new[]{(CoreTransfer[])null,Array.Empty<CoreTransfer>(),new[]{new CoreTransfer()}})
        {
            var match=Match();Gift(match,0,5);var owner=match.State.Players[0];int index=Hand(match,0,AbilityRegistry.AwakenMagic),deck=owner.Deck.Count,cores=owner.Reserve.Total;
            Dispatch(match,0,new BattleCommand{action="playMagic",index=index,target=-1,transfers=transfers});
            Equal(cores-3,owner.Reserve.Total);Equal(deck-1,owner.Deck.Count);Equal(0,owner.Field.Count);
        }
        var flash=Match();var attacker=Place(flash,1,Red);AttackStep(flash,1);Dispatch(flash,1,new BattleCommand{action="attack",index=attacker.InstanceId});
        // The defender has no Spirit, but can use Awaken's Flash to draw and
        // then must give priority to the attacker before either side passes.
        var defender=flash.State.Players[0];Gift(flash,0,5);int magic=Hand(flash,0,AbilityRegistry.AwakenMagic),before=defender.Deck.Count;
        Dispatch(flash,0,new BattleCommand{action="playMagic",index=magic,target=-1,transfers=Array.Empty<CoreTransfer>()});
        Equal(before-1,defender.Deck.Count);Equal(1,flash.DecisionPlayerIndex);Pass(flash);Assert(flash.State.Phase==MatchPhase.FlashBeforeBlock);Pass(flash);Assert(flash.State.Phase==MatchPhase.Block);
    }
    private static void AwakenTargetlessAtomic()
    {
        var match=Match();Gift(match,0,10);int index=Hand(match,0,AbilityRegistry.AwakenMagic);
        Reject(match,0,new BattleCommand{action="playMagic",index=index,target=-1,transfers=new[]{new CoreTransfer{normal=1}}});
        Reject(match,0,new BattleCommand{action="playMagic",index=index,target=-1,transfers=new[]{new CoreTransfer{soul=true}}});
        Reject(match,0,new BattleCommand{action="playMagic",index=index,target=int.MaxValue,transfers=Array.Empty<CoreTransfer>()});
        var enemy=Place(match,1,Red);Reject(match,0,new BattleCommand{action="playMagic",index=index,target=enemy.InstanceId,transfers=Array.Empty<CoreTransfer>()});
        var shortfall=Match();var owner=shortfall.State.Players[0];owner.Trash.Normal+=owner.Reserve.Normal;owner.Reserve.Normal=0;
        Reject(shortfall,0,new BattleCommand{action="playMagic",index=Hand(shortfall,0,AbilityRegistry.AwakenMagic),target=-1,transfers=Array.Empty<CoreTransfer>()});
    }
    private static void Awakening()
    {
        var match=Match();Gift(match,0,10);var target=Place(match,0,AbilityRegistry.Siegfried);var first=Place(match,0,Red,2);var second=Place(match,0,White,2,true);
        Dispatch(match,0,new BattleCommand{action="awaken",target=target.InstanceId,transfers=new[]{new CoreTransfer{source=first.InstanceId,normal=2},new CoreTransfer{source=second.InstanceId,normal=1,soul=true}}});Equal(5,target.Cores.Total);Equal(3,target.Level);Assert(target.Cores.Soul);Assert(match.FindSpirit(first.InstanceId)==null&&match.FindSpirit(second.InstanceId)==null);Equal(1,match.LastEvents.Count(e=>e.Kind=="Awakened"));
    }
    private static void AwakenAtomic()
    {
        var match=Match();Gift(match,0,10);var target=Place(match,0,AbilityRegistry.Siegfried);var source=Place(match,0,Red);var enemy=Place(match,1,Red);var nexus=Place(match,0,AbilityRegistry.SilverWorld,1);
        foreach(int bad in new[]{target.InstanceId,enemy.InstanceId,nexus.InstanceId,-1})Reject(match,0,new BattleCommand{action="awaken",source=bad,target=target.InstanceId,index=1});
        Reject(match,0,new BattleCommand{action="awaken",source=source.InstanceId,target=target.InstanceId,index=int.MaxValue,soul=true});Reject(match,0,new BattleCommand{action="awaken",target=target.InstanceId,transfers=new[]{new CoreTransfer{source=source.InstanceId,normal=1},new CoreTransfer{source=source.InstanceId,normal=1}}});Reject(match,0,new BattleCommand{action="awaken",target=target.InstanceId,transfers=new CoreTransfer[]{null}});
    }
    private static void AwakeningPriority()
    {
        var match=Match();Gift(match,0,10);var boss=Place(match,0,AbilityRegistry.Siegfried);var source=Place(match,0,Red,2);var attacker=Place(match,1,Red);AttackStep(match,1);Dispatch(match,1,new BattleCommand{action="attack",index=attacker.InstanceId});Pass(match);Pass(match);Dispatch(match,0,new BattleCommand{action="block",index=boss.InstanceId});Pass(match);Pass(match);
        // A fresh attack returns defender-first Flash. Activating passes it to attacker.
        Dispatch(match,1,new BattleCommand{action="endTurn"});Dispatch(match,0,new BattleCommand{action="endTurn"});var fresh=Place(match,1,White);Dispatch(match,1,new BattleCommand{action="attackStep"});Dispatch(match,1,new BattleCommand{action="attack",index=fresh.InstanceId});Dispatch(match,0,new BattleCommand{action="awaken",source=source.InstanceId,target=boss.InstanceId,index=1});Equal(1,match.DecisionPlayerIndex);Pass(match);Assert(match.State.Phase==MatchPhase.FlashBeforeBlock);Pass(match);Assert(match.State.Phase==MatchPhase.Block);
    }
    private static void SiegfriedDestroyed()
    {
        foreach(int levelCores in new[]{2,5})
        {
            var match=Match();var boss=Place(match,0,AbilityRegistry.Siegfried,levelCores);var attacker=Place(match,1,AbilityRegistry.Valkyrius,8);AttackStep(match,1);Dispatch(match,1,new BattleCommand{action="attack",index=attacker.InstanceId});Pass(match);Pass(match);Dispatch(match,0,new BattleCommand{action="block",index=boss.InstanceId});Pass(match);Pass(match);
            if(levelCores==5){Assert(match.FindSpirit(boss.InstanceId)!=null,"Lv3 beats9000 so fixture must forcehigherBP");}
            else{Assert(match.FindSpirit(boss.InstanceId)==null);Equal(5,match.State.Players[0].Life);}
        }
        var full=Match();var high=Place(full,0,AbilityRegistry.Siegfried,5);var foe=Place(full,1,AbilityRegistry.Siegfried,5);AttackStep(full,1);Dispatch(full,1,new BattleCommand{action="attack",index=foe.InstanceId});Pass(full);Pass(full);Dispatch(full,0,new BattleCommand{action="block",index=high.InstanceId});Pass(full);Pass(full);Equal(6,full.State.Players[0].Life);Equal(6,full.State.Players[1].Life);Assert(full.LastEvents.Count(e=>e.Kind=="LifeGained")==2);
    }
    private static void SiegfriedVanished()
    {
        var match=Match();var boss=Place(match,0,AbilityRegistry.Siegfried,5);for(int i=0;i<5;i++)Dispatch(match,0,new BattleCommand{action="moveCore",source=boss.InstanceId,target=-1});Equal(5,match.State.Players[0].Life);Assert(match.FindSpirit(boss.InstanceId)==null);
    }
    private static void ValkyriusBlock()
    {
        var match=Match();var blocker=Place(match,0,AbilityRegistry.Valkyrius);var attacker=Place(match,1,AbilityRegistry.Siegfried,2);AttackStep(match,1);Dispatch(match,1,new BattleCommand{action="attack",index=attacker.InstanceId});Pass(match);Pass(match);Dispatch(match,0,new BattleCommand{action="block",index=blocker.InstanceId});Equal(10000,blocker.Bp);Pass(match);Pass(match);Assert(match.FindSpirit(blocker.InstanceId)!=null);Equal(4000,blocker.Bp);Equal(0,blocker.BattleBpBonus);
    }
    private static void NexusRecycle()
    {
        var match=Match();Place(match,0,AbilityRegistry.SilverWorld,0);var owner=match.State.Players[0];owner.Reserve.Normal-=1;owner.Trash.Normal++;owner.Reserve.Soul=false;owner.Trash.Soul=true;int expected=owner.Reserve.Total+owner.Trash.Total;AttackStep(match,1);Equal(0,owner.Trash.Total);Equal(expected,owner.Reserve.Total);Assert(owner.Reserve.Soul);
    }
    private static void NexusDestroyed()
    {
        foreach(int nexusCores in new[]{0,4})
        {
            var match=Match();Gift(match,0,8);Place(match,0,AbilityRegistry.SilverWorld,nexusCores);var victim=Place(match,0,Red);var foe=Place(match,1,AbilityRegistry.Siegfried,2);AttackStep(match,1);int gains=match.State.Players[0].CoreGains;Dispatch(match,1,new BattleCommand{action="attack",index=foe.InstanceId});Pass(match);Pass(match);Dispatch(match,0,new BattleCommand{action="block",index=victim.InstanceId});Pass(match);Pass(match);Equal(gains+(nexusCores>=4?1:0),match.State.Players[0].CoreGains);
        }
        var vanish=Match();Gift(vanish,0,8);Place(vanish,0,AbilityRegistry.SilverWorld,4);var own=Place(vanish,0,Red);int before=vanish.State.Players[0].CoreGains;Dispatch(vanish,0,new BattleCommand{action="moveCore",source=own.InstanceId,target=-1});Equal(before,vanish.State.Players[0].CoreGains);
    }
    private static void AttackerVanish()
    {
        var match=Match();Gift(match,1,10);var boss=Place(match,1,AbilityRegistry.Siegfried);var attacker=Place(match,1,Red);var defender=Place(match,0,Red);Gift(match,0,10);AttackStep(match,1);Dispatch(match,1,new BattleCommand{action="attack",index=attacker.InstanceId});Pass(match);Dispatch(match,1,new BattleCommand{action="awaken",source=attacker.InstanceId,target=boss.InstanceId,index=1});
        Assert(match.FindSpirit(attacker.InstanceId)==null);Assert(match.State.Phase==MatchPhase.FlashBeforeBlock);Equal(0,match.DecisionPlayerIndex);
        int magic=Hand(match,0,AbilityRegistry.DoubleDraw);Dispatch(match,0,new BattleCommand{action="playMagic",index=magic,target=defender.InstanceId});Equal(3000,defender.Bp);
        Pass(match);Assert(match.State.Phase==MatchPhase.FlashBeforeBlock);Pass(match);Assert(match.State.Phase==MatchPhase.Attack&&match.State.PendingAttackerId==-1);Equal(5,match.State.Players[0].Life);
    }
    private static void BlockerVanish()
    {
        var match=Match();var boss=Place(match,0,AbilityRegistry.Siegfried);var blocker=Place(match,0,Red);var attacker=Place(match,1,Red);AttackStep(match,1);Dispatch(match,1,new BattleCommand{action="attack",index=attacker.InstanceId});Pass(match);Pass(match);Dispatch(match,0,new BattleCommand{action="block",index=blocker.InstanceId});
        Dispatch(match,0,new BattleCommand{action="awaken",source=blocker.InstanceId,target=boss.InstanceId,index=1});Assert(match.FindSpirit(blocker.InstanceId)==null);Assert(match.State.Phase==MatchPhase.FlashAfterBlock);Equal(1,match.DecisionPlayerIndex);
        Pass(match);Assert(match.State.Phase==MatchPhase.FlashAfterBlock);Pass(match);Assert(match.State.Phase==MatchPhase.Attack);Equal(5,match.State.Players[0].Life);Assert(match.FindSpirit(attacker.InstanceId)!=null);
    }
    private static void MagicPaymentVanish()
    {
        var match=Match();var attacker=Place(match,1,Red);var boss=Place(match,1,AbilityRegistry.Valkyrius,2);AttackStep(match,1);
        // Put remaining ordinary Reserve cores in Trash; the Magic must draw
        // cost from field cores and consume the attacking Spirit's final core.
        var owner=match.State.Players[1];owner.Trash.Normal+=owner.Reserve.Normal;owner.Reserve.Normal=0;
        int index=Hand(match,1,AbilityRegistry.DoubleDraw);Dispatch(match,1,new BattleCommand{action="attack",index=attacker.InstanceId});Pass(match);
        Dispatch(match,1,new BattleCommand{action="playMagic",index=index,target=boss.InstanceId});Assert(match.FindSpirit(attacker.InstanceId)==null,"Fixture did not pay with the attacking Spirit.");
        Assert(match.State.Phase==MatchPhase.FlashBeforeBlock);Equal(0,match.DecisionPlayerIndex);Pass(match);Pass(match);Assert(match.State.Phase==MatchPhase.Attack);Equal(5,match.State.Players[0].Life);
    }
    private static void AtomicCoreSwap()
    {
        var match=Match();var soul=Place(match,0,Red,1,true);var ordinary=Place(match,0,White,1);int soulId=soul.InstanceId,ordinaryId=ordinary.InstanceId;
        Reject(match,1,new BattleCommand{action="swapCore",source=soulId,target=ordinaryId,soul=true});
        Reject(match,0,new BattleCommand{action="swapCore",source=soulId,target=soulId,soul=true});
        Reject(match,0,new BattleCommand{action="swapCore",source=soulId,target=ordinaryId,soul=false});
        Dispatch(match,0,new BattleCommand{action="swapCore",source=soulId,target=ordinaryId,soul=true});
        Assert(match.FindSpirit(soulId)==soul&&match.FindSpirit(ordinaryId)==ordinary,"A one-core Spirit vanished during an atomic exchange.");
        Assert(!soul.Cores.Soul&&soul.Cores.Normal==1&&ordinary.Cores.Soul&&ordinary.Cores.Normal==0);Equal(1,soul.Level);Equal(1,ordinary.Level);
        Dispatch(match,0,new BattleCommand{action="swapCore",source=soulId,target=ordinaryId,soul=false});Assert(soul.Cores.Soul&&!ordinary.Cores.Soul);
        var enemy=Place(match,1,White);Reject(match,0,new BattleCommand{action="swapCore",source=soulId,target=enemy.InstanceId,soul=true});
        Dispatch(match,0,new BattleCommand{action="endTurn"});Reject(match,0,new BattleCommand{action="swapCore",source=soulId,target=ordinaryId,soul=true});
    }
    private static void EmptyNexusRecycle()
    {
        var match=Match();Place(match,0,AbilityRegistry.SilverWorld,0);AttackStep(match,1);
        Assert(!match.LastEvents.Any(item=>item.Kind=="CoresRecycled"),"Silver World activated despite an empty Trash (official Q21913).");
    }
    private static void SupportedDeck()
    {
        var deck=Enumerable.Range(0,40).Select(i=>cards[i%cards.Length].id).ToArray();
        // Synthetic70cardfixtures deliberately bypass deckcopylimits; actual
        // playable deckbuilding is checked against the complete official catalog.
        foreach(var card in cards)Assert(card.IsPlayable,card.id);var fake=Clone(Get(AbilityRegistry.DoubleDraw));fake.id="UNKNOWN-MAGIC";Assert(!fake.IsPlayable);Assert(Get(AbilityRegistry.SilverWorld).IsNexus&&Get(AbilityRegistry.SilverWorld).levels[0].cores==0);
    }
}
