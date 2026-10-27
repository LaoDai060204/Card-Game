using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using LaoDai.BattleSpirits.Core;
class Program
{
 static JsonSerializerOptions options = new JsonSerializerOptions { IncludeFields = true };
 static int checks, actions, finished;
 static string Snapshot(BattleMatch m) => JsonSerializer.Serialize(m.State, options);
 static bool Act(BattleMatch m, Func<bool> action)
 {
   string before = Snapshot(m);
   bool result = action(); actions++;
   m.AssertInvariants();
   if (!result && before != Snapshot(m)) throw new Exception("Failed action changed gameplay state.");
   return result;
 }
 static string Cards(PlayerState p) => string.Join("|",p.Hand.Concat(p.Deck).Concat(p.Discard).Concat(p.Field.Select(s=>s.Card.id)).GroupBy(s=>s).OrderBy(g=>g.Key).Select(g=>g.Key+":"+g.Count()));
 static void Main(string[] args)
 {
   var cards = JsonSerializer.Deserialize<CardsDatabase>(File.ReadAllText(args.Length > 0 ? args[0] : "Assets/BattleSpirits/Resources/Data/cards.json"), options).cards;
   for(int seed=0;seed<100;seed++)
   {
     var rnd = new Random(seed+711);
     var m = BattleMatch.CreateDefault(cards,seed,seed%2);
     var initial = m.State.Players.Select(Cards).ToArray();
     for(int step=0;step<1500 && m.State.Phase != MatchPhase.GameOver;step++)
     {
       int p=m.DecisionPlayerIndex;
       int invalid = rnd.Next(2)==0 ? -1 : 2;
       Act(m,()=>m.TrySummon(invalid,0,out _));
       Act(m,()=>m.TryMoveCore(invalid,-1,0,false,out _));
       Act(m,()=>m.TryConfirmOpening(invalid,false,out _));
       Act(m,()=>m.TryAttack(invalid,0,out _));
       Act(m,()=>m.TryBlock(invalid,0,out _));
       Act(m,()=>m.TryPassFlash(invalid,out _));
       Act(m,()=>m.TryTakeLife(invalid,out _));
       Act(m,()=>m.TryEndTurn(invalid,out _));
       switch(m.State.Phase)
       {
         case MatchPhase.Opening: Act(m,()=>m.TryConfirmOpening(p,rnd.Next(2)==0,out _)); break;
         case MatchPhase.Main:
           var owner=m.State.Players[p];
           int choice=rnd.Next(4);
           if(choice==0 && owner.Hand.Count>0) Act(m,()=>m.TrySummon(p,rnd.Next(owner.Hand.Count),out _));
           else if(choice==1 && owner.Field.Count>0)
           {
             var ids=owner.Field.Select(s=>s.InstanceId).Concat(new[]{-1}).ToArray();
             Act(m,()=>m.TryMoveCore(p,ids[rnd.Next(ids.Length)],ids[rnd.Next(ids.Length)],rnd.Next(3)==0,out _));
           }
           else if(m.State.TurnNumber>1 && choice==2) Act(m,()=>m.TryBeginAttackStep(p,out _));
           else Act(m,()=>m.TryEndTurn(p,out _));
           break;
         case MatchPhase.Attack:
           var ready=m.State.Players[p].Field.Where(s=>!s.Exhausted).ToArray();
           if(ready.Length>0 && rnd.Next(4)!=0) Act(m,()=>m.TryAttack(p,ready[rnd.Next(ready.Length)].InstanceId,out _));
           else Act(m,()=>m.TryEndTurn(p,out _));
           break;
         case MatchPhase.FlashBeforeBlock:
         case MatchPhase.FlashAfterBlock: Act(m,()=>m.TryPassFlash(p,out _)); break;
         case MatchPhase.Block:
           var blockers=m.State.Players[p].Field.Where(s=>!s.Exhausted).ToArray();
           if(blockers.Length>0 && rnd.Next(3)!=0) Act(m,()=>m.TryBlock(p,blockers[rnd.Next(blockers.Length)].InstanceId,out _));
           else Act(m,()=>m.TryTakeLife(p,out _));
           break;
       }
       for(int i=0;i<2;i++) if(Cards(m.State.Players[i])!=initial[i]) throw new Exception("Card identity conservation failed.");
       checks++;
     }
     if(m.State.Phase!=MatchPhase.GameOver) throw new Exception("Simulation did not finish seed "+seed);
     finished++;
   }
   Console.WriteLine(JsonSerializer.Serialize(new { games=finished, gameplayStatesChecked=checks, attemptedActions=actions, actualCatalogCards=cards.Length, result="PASS" }));
 }
}
