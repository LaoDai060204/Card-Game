using CardGame.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;

namespace CardGame.Duel.YGOSharp
{
    public static class BanlistManager
    {
        public static List<Banlist> Banlists { get; private set; }
        public const string FILE_NAME = "lflist.conf";
        private const string FILE_NAME_MERGED = "lflist_merged.conf";
        public const string FILE_NAME_GENESYS = "lflist_genesys.conf";
        public const string EMPTY_LIST_NAME = "N/A";
        public static int emptyListIndex;
        public static Banlist currentBanList;
        public static Banlist currentGenesysBanList;

        private static bool dirty;
        private static List<string> allNames;
        private static List<string> allNamesNoGenesys;
        private static List<string> allGenesysNames;
        private static string allTextNoGenesys;

        private static readonly StringBuilder builder = new();

        private const bool CORE_SUPPORT_GENESYS_LFLIST = false;

        public static void Initialize()
        {
            Banlists = new();
            builder.Clear();

            var allListsInExpanions = ResourceManager.GetTextsByExtensions(new string[] { FILE_NAME });
            foreach (var list in allListsInExpanions)
                InitializeFromText(list);

            var path = Program.PATH_DATA + FILE_NAME;
            if (File.Exists(path))
            {
                var reader = new StreamReader(path);
                InitializeFromReader(reader);
                reader.Close();
            }
            allTextNoGenesys = builder.ToString();

            Banlist current = new()
            {
                Name = EMPTY_LIST_NAME
            };
            Banlists.Add(current);
            emptyListIndex = Banlists.Count - 1;

            InitializeForGenesys();
        }

        public static async UniTask InitializeAsync()
        {
            Banlists = new();
            builder.Clear();

            var allListsInExpanions = ResourceManager.GetTextsByExtensions(new string[] { FILE_NAME });
            foreach (var list in allListsInExpanions)
                InitializeFromText(list);

            var path = Program.PATH_DATA + FILE_NAME;
            if (File.Exists(path))
            {
                var text = await UniTask.RunOnThreadPool(() => File.ReadAllText(path));
                InitializeFromText(text);
            }
            allTextNoGenesys = builder.ToString();

            Banlist current = new()
            {
                Name = EMPTY_LIST_NAME
            };
            Banlists.Add(current);
            emptyListIndex = Banlists.Count - 1;

            await InitializeForGenesysAsync();
        }

        public static void InitializeFromText(string text)
        {
            Banlist current = null;
            var lines = text.Replace("\r", "").Split('\n');
            foreach (var line in lines)
            {
                AppendLflistText(builder, line);
                try
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    var trimedLine = line.Trim();                     // Trim leading and trailing whitespace.
                    if (trimedLine[0] == '#')                    // Comment line.
                        continue;

                    if (trimedLine[0] == '!')                    // Start a new list.
                    {
                        current = new Banlist
                        {
                            Name = trimedLine[1..].Trim()
                        };
                        AddList(current);
                        continue;
                    }

                    if (current == null)                   // No list is active yet.
                        continue;

                    if (trimedLine[0] == '$')                    // Line beginning with $.
                    {
                        string rest = trimedLine[1..].Trim();
                        // Whitelist mode directive: $whitelist
                        if (rest.StartsWith("whitelist", StringComparison.OrdinalIgnoreCase))
                        {
                            current.EnableWhitelistMode();
                            continue;
                        }
                        // Otherwise parse a global credit budget: $key value
                        string[] parts = rest.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            string key = parts[0];
                            if (uint.TryParse(parts[1], out uint limit))
                                current.AddCreditLimit(key, limit);
                        }
                        continue;
                    }

                    // A normal line can specify a card limit or a card credit cost.
                    string[] data = trimedLine.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (data.Length < 2)
                        continue;

                    // Check for $ to identify a credit-cost entry.
                    bool isCreditLine = false;
                    int dollarIndex = -1;
                    for (int i = 0; i < data.Length; i++)
                    {
                        if (data[i].StartsWith("$"))
                        {
                            isCreditLine = true;
                            dollarIndex = i;
                            break;
                        }
                    }

                    if (isCreditLine)
                    {
                        // Format: card_id $credit_type cost
                        if (dollarIndex + 1 >= data.Length)
                            continue;
                        if (int.TryParse(data[0], out int cardId) && int.TryParse(data[dollarIndex + 1], out int value))
                        {
                            string key = data[dollarIndex][1..]; // Remove the $ prefix.
                            current.AddCardCredit(cardId, key, value);
                        }
                    }
                    else
                    {
                        // Legacy format: card_id count
                        if (int.TryParse(data[0], out int id) && int.TryParse(data[1], out int count))
                            current.Add(id, count);
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.Log(line);
                    UnityEngine.Debug.Log(e);
                }
            }
        }

        public static void InitializeFromReader(StreamReader reader)
        {
            Banlist current = null;
            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();
                AppendLflistText(builder, line);
                try
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    line = line.Trim();                     // Trim leading and trailing whitespace.
                    if (line[0] == '#')                    // Comment line.
                        continue;

                    if (line[0] == '!')                    // Start a new list.
                    {
                        current = new Banlist
                        {
                            Name = line[1..].Trim()
                        };
                        AddList(current);
                        continue;
                    }

                    if (current == null)                   // No list is active yet.
                        continue;

                    if (line[0] == '$')                    // Line beginning with $.
                    {
                        string rest = line[1..].Trim();
                        // Whitelist mode directive: $whitelist
                        if (rest.StartsWith("whitelist", StringComparison.OrdinalIgnoreCase))
                        {
                            current.EnableWhitelistMode();
                            continue;
                        }
                        // Otherwise parse a global credit budget: $key value
                        string[] parts = rest.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            string key = parts[0];
                            if (uint.TryParse(parts[1], out uint limit))
                                current.AddCreditLimit(key, limit);
                        }
                        continue;
                    }

                    // A normal line can specify a card limit or a card credit cost.
                    string[] data = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (data.Length < 2)
                        continue;

                    // Check for $ to identify a credit-cost entry.
                    bool isCreditLine = false;
                    int dollarIndex = -1;
                    for (int i = 0; i < data.Length; i++)
                    {
                        if (data[i].StartsWith("$"))
                        {
                            isCreditLine = true;
                            dollarIndex = i;
                            break;
                        }
                    }

                    if (isCreditLine)
                    {
                        // Format: card_id $credit_type cost
                        if (dollarIndex + 1 >= data.Length)
                            continue;
                        if (int.TryParse(data[0], out int cardId) && int.TryParse(data[dollarIndex + 1], out int value))
                        {
                            string key = data[dollarIndex][1..]; // Remove the $ prefix.
                            current.AddCardCredit(cardId, key, value);
                        }
                    }
                    else
                    {
                        // Legacy format: card_id count
                        if (int.TryParse(data[0], out int id) && int.TryParse(data[1], out int count))
                            current.Add(id, count);
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.Log(line);
                    UnityEngine.Debug.Log(e);
                }
            }
        }

        public static void InitializeForGenesys()
        {
            builder.Clear();

            var gPath = Program.PATH_DATA + FILE_NAME_GENESYS;
            if (File.Exists(gPath))
            {
                var reader = new StreamReader(gPath);
                InitializeFromReader(reader);
                reader.Close();
            }

            InitialCurrentBanlists();
            if (CORE_SUPPORT_GENESYS_LFLIST)
            {
                var text = Tools.MergeWithNewLine(allTextNoGenesys, builder.ToString());
                SaveMergedText(text);
            }
            else
                SaveMergedText(allTextNoGenesys);
        }

        public static async UniTask InitializeForGenesysAsync()
        {
            builder.Clear();

            var gPath = Program.PATH_DATA + FILE_NAME_GENESYS;
            if (File.Exists(gPath))
            {
                var text = await UniTask.RunOnThreadPool(() => File.ReadAllText(gPath));
                InitializeFromText(text);
            }

            InitialCurrentBanlists();
            if (CORE_SUPPORT_GENESYS_LFLIST)
            {
                var text = Tools.MergeWithNewLine(allTextNoGenesys, builder.ToString());
                await SaveMergedTextAsync(text);
            }
            else
                await SaveMergedTextAsync(allTextNoGenesys);
        }

        private static void InitialCurrentBanlists()
        {
            currentBanList = null;
            currentGenesysBanList = null;
            foreach (var banlist in Banlists)
            {
                if(currentBanList == null && !banlist.isCredit)
                    currentBanList = banlist;
                if(currentGenesysBanList == null && banlist.isCredit)
                    currentGenesysBanList = banlist;
                if (currentBanList != null && currentGenesysBanList != null)
                    break;
            }
            currentBanList ??= new();
            currentGenesysBanList ??= new();
        }

        private static void SaveMergedText(string text)
        {
            File.WriteAllText(Path.Combine(Program.PATH_DATA, FILE_NAME_MERGED), text, Encoding.UTF8);
        }

        private static async UniTask SaveMergedTextAsync(string text)
        {
            var savePath = Path.Combine(Program.PATH_DATA, FILE_NAME_MERGED);
            await UniTask.RunOnThreadPool(() => File.WriteAllText(savePath, text, Encoding.UTF8));
        }

        private static void AddList(Banlist banlist)
        {
            Banlists.Add(banlist);
            dirty = true;
        }

        private static void AppendLflistText(StringBuilder builder, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (builder.Length > 0 && builder[^1] != '\n')
                builder.AppendLine();
            builder.Append(text);
        }

        public static int GetIndex(uint hash)
        {
            for (int i = 0; i < Banlists.Count; i++)
                if (Banlists[i].Hash == hash)
                    return i;
            return 0;
        }

        public static int GetIndexByName(string name)
        {
            for (int i = 0; i < Banlists.Count; i++)
                if (Banlists[i].Name == name)
                    return i;
            return 0;
        }

        public static string GetName(uint hash)    
        {
            for (int i = 0; i < Banlists.Count; i++)
                if (Banlists[i].Hash == hash)
                    return Banlists[i].Name;
            return InterString.Get("Unknown Banlist");
        }

        public static List<string> GetAllNames(bool includeGenesys = false)
        {
            allNames ??= new();
            allNamesNoGenesys ??= new();

            if (dirty)
            {
                allNames.Clear();
                allNamesNoGenesys.Clear();
                foreach (var item in Banlists)
                {
                    allNames.Add(item.Name);
                    if(!item.isCredit)
                        allNamesNoGenesys.Add(item.Name);
                }
            }

            if (includeGenesys)
                return allNames;
            else
                return allNamesNoGenesys;
        }

        public static List<string> GetAllGenesysNames()
        {
            allGenesysNames ??= new();

            if (dirty)
            {
                allGenesysNames.Clear();
                foreach(var item in Banlists)
                    if(item.isCredit)
                        allGenesysNames.Add(item.Name);
            }

            return allGenesysNames;
        }

        public static Banlist GetByName(string name)
        {
            foreach (var item in Banlists)
                if (item.Name == name)
                    return item;
            return Banlists[emptyListIndex];
        }

        public static Banlist GetByHash(uint hash)
        {
            foreach (var item in Banlists)
                if (item.Hash == hash)
                    return item;
            return Banlists[emptyListIndex];
        }

    }
}
