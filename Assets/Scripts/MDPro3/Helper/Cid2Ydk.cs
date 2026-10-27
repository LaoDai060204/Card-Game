using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using MDPro3.Duel.YGOSharp;
using Newtonsoft.Json;

namespace MDPro3
{
    public static class Cid2Ydk
    {
        private const string cardsPath = "Data/cards.json";
        private const string cardsAltPath = "Data/cards_Alt.json";
        private const string cardsLitePath = "Data/cards_Lite.json";

        private static bool initialized;
        private static Dictionary<string, Id2Ydk> dic;

        private static void Initialize()
        {
            if (initialized) return;

            bool hasLite = TryReadMapping(cardsLitePath, out dic);
            bool hasFull = false;
            if (!hasLite)
                hasFull = TryReadMapping(cardsPath, out dic);
            if (dic == null)
                dic = new Dictionary<string, Id2Ydk>();

            // Apply local alternate-art mappings even when the bundled lite file is present.
            if (TryReadMapping(cardsAltPath, out var alternateMappings))
                foreach (var pair in alternateMappings)
                    dic[pair.Key] = pair.Value;

            if (!hasLite && !hasFull)
                Debug.LogWarning("CID mapping data is missing or invalid. Restore Data/cards_Lite.json from the repository; alternate mappings remain available.");

            if (hasFull)
            {
                try
                {
                    File.WriteAllText(cardsLitePath, JsonConvert.SerializeObject(dic));
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    // A read-only cache must not prevent the already-loaded mapping from being used.
                    Debug.LogWarning($"Unable to cache CID mappings in {cardsLitePath}: {exception.Message}");
                }
            }

            initialized = true;
        }

        private static bool TryReadMapping(string path, out Dictionary<string, Id2Ydk> mappings)
        {
            mappings = null;
            if (!File.Exists(path))
                return false;
            try
            {
                var parsed = JsonConvert.DeserializeObject<Dictionary<string, Id2Ydk>>(File.ReadAllText(path));
                if (parsed == null)
                    return false;
                var usable = new Dictionary<string, Id2Ydk>();
                foreach (var pair in parsed)
                    if (pair.Value != null && pair.Value.cid > 0 && pair.Value.id >= 0
                        && pair.Key == pair.Value.cid.ToString())
                        usable.Add(pair.Key, pair.Value);
                if (usable.Count == 0)
                    return false;
                mappings = usable;
                return true;
            }
            catch (Exception exception) when (exception is IOException
                || exception is UnauthorizedAccessException || exception is JsonException)
            {
                Debug.LogWarning($"Unable to read CID mappings from {path}: {exception.Message}");
                return false;
            }
        }

        public static bool HaveCid(int cid)
        {
            Initialize();
            return dic.ContainsKey(cid.ToString());
        }

        public static int GetYDK(int cid)
        {
            Initialize();
            if (dic.ContainsKey(cid.ToString()))
                return dic[cid.ToString()].id;
            return cid;
        }

        public static int GetCID(int ydk)
        {
            Initialize();
            var card = CardsManager.Get(ydk);
            if(card.Alias != 0)
                ydk = card.Alias;
            foreach(var value in dic.Values)
                if(value.id == ydk || value.id == card.Id)
                    return value.cid;
            return ydk;
        }

        public static string ReplaceWithCardName(string origin)
        {
            if (string.IsNullOrEmpty(origin))
                return origin;
            origin = origin.Replace(" get=\'name\'", string.Empty);
            if (origin.IndexOf("<card", StringComparison.Ordinal) < 0)
                return origin;

            Initialize();
            return Regex.Replace(origin, @"<card mrk='(\d+)'/>", EvaluatorGetNameFromNumber);
        }

        private static string EvaluatorGetNameFromNumber(Match match)
        {
            string numberString = match.Groups[1].Value;
            if (!int.TryParse(numberString, out int cid))
                return match.Value;
            dic.TryGetValue(cid.ToString(), out var code);
            if (code != null)
                return CardsManager.Get(code.id).Name;
            else
                return CardsManager.Get(cid).Name;
        }

        public class Id2Ydk
        {
            public int cid;
            public int id;
        }
    }
}
