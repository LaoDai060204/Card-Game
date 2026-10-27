using System;
using System.IO;
using System.Reflection;
using MDPro3;
using MDPro3.Duel.YGOSharp;

internal static class Program
{
    private static int passed;
    private static string shippedLite;
    private const string Mapping = "{\"1234\":{\"cid\":1234,\"id\":5678,\"unused\":\"ignored\"}}";
    private const string Alternate = "{\"1234\":{\"cid\":1234,\"id\":9876},}";

    private static void Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Pass the project's Data/cards_Lite.json path.");
        shippedLite = Path.GetFullPath(args[0]);
        Case("Plain/null/empty item text does not load mapping files", () =>
        {
            Equal("Hello", Cid2Ydk.ReplaceWithCardName("Hello"));
            Equal(null, Cid2Ydk.ReplaceWithCardName(null));
            Equal("", Cid2Ydk.ReplaceWithCardName(""));
            Equal(false, typeof(Cid2Ydk).GetField("initialized", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null));
            Equal(0, UnityEngine.Debug.Warnings.Count);
        });
        Case("No files: safe CID fallback and no file-not-found exception", () =>
        {
            Equal(42, Cid2Ydk.GetYDK(42));
            Equal(false, Cid2Ydk.HaveCid(42));
            Equal("Card#42", Cid2Ydk.ReplaceWithCardName("<card mrk='42'/>"));
            Equal(1, UnityEngine.Debug.Warnings.Count);
        });
        Case("Lite-only data: forward/reverse lookup and names", () =>
        {
            Write("cards_Lite.json", Mapping);
            Equal(5678, Cid2Ydk.GetYDK(1234));
            Equal(1234, Cid2Ydk.GetCID(5678));
            Equal("Name: Card#5678", Cid2Ydk.ReplaceWithCardName("Name: <card mrk='1234' get='name'/>"));
            Equal(321, Cid2Ydk.GetYDK(321));
        });
        Case("Full data builds a compact usable cache", () =>
        {
            Write("cards.json", Mapping);
            Equal(5678, Cid2Ydk.GetYDK(1234));
            Equal(true, File.Exists("Data/cards_Lite.json"));
            Equal(false, File.ReadAllText("Data/cards_Lite.json").Contains("unused"));
        });
        Case("Alternate overrides apply to a previously cached lite mapping", () =>
        {
            Write("cards_Lite.json", Mapping); Write("cards_Alt.json", Alternate);
            Equal(9876, Cid2Ydk.GetYDK(1234));
        });
        Case("Full and alternate data are merged before writing the cache", () =>
        {
            Write("cards.json", Mapping); Write("cards_Alt.json", Alternate);
            Equal(9876, Cid2Ydk.GetYDK(1234));
            Equal(true, File.ReadAllText("Data/cards_Lite.json").Contains("9876"));
        });
        Case("Malformed lite file falls back to full data", () =>
        {
            Write("cards_Lite.json", "{bad-json"); Write("cards.json", Mapping);
            Equal(5678, Cid2Ydk.GetYDK(1234));
            Equal(true, File.ReadAllText("Data/cards_Lite.json").Contains("5678"));
        });
        Case("Null and empty lite objects fall back safely", () =>
        {
            Write("cards_Lite.json", "null"); Write("cards.json", Mapping);
            Equal(5678, Cid2Ydk.GetYDK(1234));
        });
        Case("Malformed source with alternate-only data still works", () =>
        {
            Write("cards.json", "[]"); Write("cards_Alt.json", Alternate);
            Equal(9876, Cid2Ydk.GetYDK(1234));
            Equal(77, Cid2Ydk.GetYDK(77));
        });
        Case("Cache write failure does not discard the loaded mapping", () =>
        {
            Write("cards.json", Mapping);
            Directory.CreateDirectory("Data/cards_Lite.json");
            Equal(5678, Cid2Ydk.GetYDK(1234));
            Equal(true, UnityEngine.Debug.Warnings.Count > 0);
        });
        Case("Invalid/null entries are excluded without corrupting valid entries", () =>
        {
            Write("cards_Lite.json", "{\"1\":null,\"2\":{\"cid\":99,\"id\":7},\"3\":{\"cid\":3,\"id\":-7},\"1234\":{\"cid\":1234,\"id\":5678}}");
            Equal(false, Cid2Ydk.HaveCid(1));
            Equal(false, Cid2Ydk.HaveCid(2));
            Equal(false, Cid2Ydk.HaveCid(3));
            Equal(5678, Cid2Ydk.GetYDK(1234));
        });
        Case("An unassigned official YDK zero is retained", () =>
        {
            Write("cards_Lite.json", "{\"1234\":{\"cid\":1234,\"id\":0}}");
            Equal(true, Cid2Ydk.HaveCid(1234)); Equal(0, Cid2Ydk.GetYDK(1234));
        });
        Case("Malformed alternate file leaves the valid lite mapping intact", () =>
        {
            Write("cards_Lite.json", Mapping); Write("cards_Alt.json", "{");
            Equal(5678, Cid2Ydk.GetYDK(1234));
        });
        Case("Overflow CID tags remain intact instead of throwing", () =>
        {
            Write("cards_Lite.json", Mapping);
            string tag = "<card mrk='999999999999999999999999999999'/>";
            Equal(tag, Cid2Ydk.ReplaceWithCardName(tag));
        });
        Case("Reverse lookup respects an existing card alias", () =>
        {
            Write("cards_Lite.json", Mapping); CardsManager.Aliases[9999] = 5678;
            Equal(1234, Cid2Ydk.GetCID(9999));
        });
        Case("The shipped real-world mapping resolves original and alternate Blue-Eyes", () =>
        {
            File.Copy(shippedLite, "Data/cards_Lite.json");
            Equal(89631139, Cid2Ydk.GetYDK(3801));
            Equal(89631141, Cid2Ydk.GetYDK(4007));
            Equal("Card#89631139", Cid2Ydk.ReplaceWithCardName("<card mrk='3801'/>"));
        });
        Console.WriteLine("PASS: " + passed + " runtime-data scenarios using the production Cid2Ydk.cs.");
    }

    private static void Case(string name, Action action)
    {
        string previous = Environment.CurrentDirectory;
        string directory = Path.Combine(Path.GetTempPath(), "cardgame-cid-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "Data"));
        try
        {
            Environment.CurrentDirectory = directory;
            typeof(Cid2Ydk).GetField("initialized", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, false);
            typeof(Cid2Ydk).GetField("dic", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
            UnityEngine.Debug.Warnings.Clear(); CardsManager.Aliases.Clear();
            action(); passed++; Console.WriteLine("PASS " + name);
        }
        finally
        {
            Environment.CurrentDirectory = previous;
            Directory.Delete(directory, true);
        }
    }

    private static void Write(string file, string value) => File.WriteAllText(Path.Combine("Data", file), value);
    private static void Equal(object expected, object actual)
    {
        if (!Equals(expected, actual)) throw new Exception("Expected " + expected + ", got " + actual);
    }
}
