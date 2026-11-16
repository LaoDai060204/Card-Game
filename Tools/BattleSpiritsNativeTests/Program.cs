using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MDPro3.BattleSpirits;
using MDPro3.BattleSpirits.Core;
using MDPro3.Duel.YGOSharp;

public static class NativeTests
{
    private static int assertions;
    private static void Check(bool condition,string message) { assertions++;if(!condition)throw new Exception(message); }
    public static int Main(string[] args)
    {
        string root=Path.GetFullPath(args.Length>0?args[0]:".");
        string temporary=Path.Combine(Path.GetTempPath(),"battle-spirits-native-tests-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            UnityEngine.Application.streamingAssetsPath=Path.Combine(root,"Assets","StreamingAssets");
            UnityEngine.Application.persistentDataPath=temporary;
            CardsManager.Initialize();
            Check(CardsManager.GetAllCards().Count==62,"Native collection must contain exactly the 62 BS cards.");
            foreach(var definition in BattleCatalog.Cards)
            {
                Check(definition.implemented&&definition.IsPlayable,"Shipped catalog flags and registered rules make every native card playable.");
                int id=BattleCatalog.GetNumericId(definition.id);
                var card=CardsManager.Get(id);
                Check(card.IsBattleSpirits&&card.BattleSpiritsId==definition.id,"Native ID round trip.");
                Check(card.Clone().BattleSpiritsId==definition.id,"Clone preserves BS identity.");
                var target=new Card();card.CloneTo(target);Check(target.BattleSpiritsId==definition.id,"CloneTo preserves BS identity.");
                Check(!card.IsExtraCard(),"Every BS card belongs in Main.");
                Check(File.Exists(BattleCatalog.CardImagePath(definition.id)),"Printed face exists.");
            }
            int unknown=BattleCatalog.Cards.Max(card=>BattleCatalog.GetNumericId(card.id))+1;
            Check(CardsManager.Get(unknown,true).Id==0,"Unknown IDs never fall back to an adjacent card.");
            Check(CardsManager.Get(89631139,true).Id==0,"YGO cards cannot enter the BS collection.");
            Check(CardsManager.Search("",null,null,"").Count==62,"Empty native search shows the entire catalog.");
            Check(CardsManager.Search("cost:4",null,null,"").All(card=>card.Level==4),"Cost text search uses BS costs.");
            var bpResults=CardsManager.Search("bp:5000",null,null,"");
            Check(bpResults.Count>0&&bpResults.All(card=>BattleCatalog.TryGet(card.Id,out var definition)&&definition.levels.Any(level=>level.bp>=5000)),"BP text search checks printed levels.");
            var effectResults=CardsManager.Search("draw",null,null,"");
            Check(effectResults.Count>0&&effectResults.All(card=>BattleCatalog.TryGet(card.Id,out var definition)&&((definition.effectTextEn??"")+definition.DisplayName).IndexOf("draw",StringComparison.OrdinalIgnoreCase)>=0),"Native text search finds English printed effects.");
            foreach(var type in new[]{"Spirit","Nexus","Magic"})
                Check(CardsManager.Search(type,null,null,"").All(card=>card.Desc.IndexOf(type,StringComparison.OrdinalIgnoreCase)>=0),"Type text search.");
            foreach(var definition in BattleCatalog.Cards)
                Check(CardsManager.Search(definition.id,null,null,"").Any(card=>card.BattleSpiritsId==definition.id),"Printed ID search.");
            var filters=Enumerable.Repeat(-233L,25).ToList();for(int i=0;i<13;i++)filters[i]=0;
            filters[0]=2;
            Check(CardsManager.Search("",filters,null,"").All(card=>BattleCatalog.TryGet(card.Id,out var definition)&&definition.IsNexus),"Nexus filter uses BS type.");
            filters[0]=0;filters[1]=1;
            Check(CardsManager.Search("",filters,null,"").All(card=>BattleCatalog.TryGet(card.Id,out var definition)&&definition.color==CardColor.Red),"Color filter uses BS colors.");
            filters[1]=0;filters[13]=0;filters[14]=0;
            Check(CardsManager.Search("",filters,null,"").All(card=>card.Level==0),"Zero-cost filter is valid.");
            filters[13]=-233;filters[14]=-233;filters[15]=2000;filters[16]=5000;
            var filteredBP=CardsManager.Search("",filters,null,"");
            Check(filteredBP.Count>0&&filteredBP.All(card=>card.Attack>=2000&&card.Attack<=5000),"Native BP range filter.");
            filters[15]=-233;filters[16]=-233;filters[1]=1L<<(int)CardColor.Blue;
            var multicolor=new CardDefinition {color=CardColor.Red,colors=new[]{"red","blue"}};
            Check(BattleCardAdapter.MatchesFilters(multicolor,filters,""),"Color filters include every printed color.");
            BattleDeckStorage.EnsureInitialized();
            Check(BattleDeckStorage.DeckDirectory.EndsWith(Path.DirectorySeparatorChar.ToString()),"DeckDirectory has a trailing separator.");
            Check(Directory.GetFiles(BattleDeckStorage.DeckDirectory,"*.ydk").Length==4,"Seed all four starter decks exactly once.");
            foreach(var file in Directory.GetFiles(BattleDeckStorage.DeckDirectory,"*.ydk"))
            {
                var definition=BattleDeckStorage.LoadDefinition(file);
                Check(DeckValidator.Validate(BattleCatalog.Cards,definition.cards).IsValid,"Seeded starter deck is Standard-legal.");
                var deck=new Deck(file);
                Check(deck.Main.Count==40&&deck.Extra.Count==0&&deck.Side.Count==0,"Native Main/Extra/Side adapter.");
                Check(deck.type==string.Empty,"Root starter decks save back into the root deck directory.");
                Check(deck.Case==0,"Empty BS deck-case category uses the card-back fallback.");
                Check(deck.GetCardCount(deck.Main[0])==deck.Main.Count(id=>BattleDeckStorage.SameName(id,deck.Main[0])),"Native deck copy count uses BS names.");
                Check(BattleDeckStorage.Validate(deck).IsValid,"Native starter validates.");
                deck.Extra.Add(deck.Main[0]);
                Check(!BattleDeckStorage.Validate(deck).IsValid,"Extra must remain empty.");
                deck.Extra.Clear();deck.Side.Add(deck.Main[0]);
                Check(!BattleDeckStorage.Validate(deck).IsValid,"Side must remain empty.");
            }
            var first=BattleCatalog.Cards.First();
            var illegal=new Deck { Main=Enumerable.Repeat(BattleCatalog.GetNumericId(first.id),40).ToList() };
            Check(!BattleDeckStorage.Validate(illegal).IsValid,"Same-name copy limit is enforced.");
            var shortDeck=new Deck { Main=Enumerable.Repeat(BattleCatalog.GetNumericId(first.id),3).ToList() };
            Check(!BattleDeckStorage.Validate(shortDeck).IsValid,"40-card minimum enforced.");
            bool escaped=false;try { BattleDeckStorage.ResolveDeckPath("../previous-product"); }catch(ArgumentException){escaped=true;}
            Check(escaped,"Deck resolver rejects directory traversal.");
            escaped=false;try { BattleDeckStorage.ResolveDeckPath(Path.Combine(root,"Deck","old-user-deck.ydk")); }catch(ArgumentException){escaped=true;}
            Check(escaped,"Deck resolver rejects the previous product's absolute directory.");
            var defaultDeck=new Deck(BattleDeckStorage.ResolveDeckPath(BattleDeckStorage.DefaultDeckName));
            defaultDeck.type="Tests";
            Check(defaultDeck.Save("round-trip",DateTime.UtcNow),"Native saves use isolated BS storage.");
            string saved=BattleDeckStorage.ResolveDeckPath("Tests/round-trip");
            Check(BattleDeckStorage.LoadDefinition(saved).cards.Length==40,"Saved native deck round trip.");
            Check(File.ReadAllText(saved).StartsWith("#Battle Spirits"),"Serialized deck has BS identity.");
            var roundTrip=new Deck(saved);
            Check(roundTrip.type=="Tests"&&roundTrip.Case==0&&roundTrip.Protector==defaultDeck.Protector&&roundTrip.Field==defaultDeck.Field,"BS group and cosmetics round trip.");
            var extraInput=new List<int>{BattleCatalog.GetNumericId(first.id)};
            var sideInput=new List<int>(extraInput);
            var fromLists=new Deck(defaultDeck.Main,extraInput,sideInput);
            Check(fromLists.Extra.Count==0&&fromLists.Side.Count==0&&extraInput.Count==1&&sideInput.Count==1,"Native deck conversion leaves caller lists intact.");
            var large=new Deck {Main=BattleCatalog.Cards.Where(card=>card.IsPlayable).Take(21).SelectMany(card=>Enumerable.Repeat(BattleCatalog.GetNumericId(card.id),3)).ToList()};
            Check(large.Main.Count==63&&BattleDeckStorage.Validate(large).IsValid,"BS uses a 40-card minimum without the former 60-card ceiling.");
            foreach(var file in Directory.GetFiles(BattleDeckStorage.DeckDirectory,"*.ydk"))File.Delete(file);
            Check(File.Exists(BattleDeckStorage.ResolveDeckPath(BattleDeckStorage.DefaultDeckName)),"Default selection can resolve a deck moved into a group.");
            File.Delete(saved);
            BattleDeckStorage.EnsureInitialized();
            Check(Directory.GetFiles(BattleDeckStorage.DeckDirectory,"*.ydk",SearchOption.AllDirectories).Length==0,"Deleting all decks does not reseed starters.");
            Check(!string.IsNullOrWhiteSpace(BattleDeckStorage.DefaultDeckName),"An empty device library still has a safe new-deck name.");
            Console.WriteLine("PASS: "+assertions+" native catalog/deck/search assertions (production sources; Unity rendering not run).");
            return 0;
        }
        finally { Directory.Delete(temporary,true); }
    }
}
