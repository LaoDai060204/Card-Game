using System.Collections.Generic;
using System.Data;
using Mono.Data.Sqlite;
using System;
using System.IO;

namespace YGOSharp.OCGWrapper
{
    public static class NamedCardsManager
    {
        private static volatile IDictionary<int, NamedCard> _cards = new Dictionary<int, NamedCard>();

        public static void Init(string databaseFullPath)
        {
            try
            {
                if (!File.Exists(databaseFullPath))
                {
                    throw new Exception("Could not find the cards database.");
                }

                var cards = new Dictionary<int, NamedCard>();

                using (SqliteConnection connection = new SqliteConnection("Data Source=" + databaseFullPath))
                {
                    connection.Open();

                    using (IDbCommand command = new SqliteCommand(
                        "SELECT datas.id, ot, alias, setcode, type, level, race, attribute, atk, def, texts.name, texts.desc"
                        + " FROM datas INNER JOIN texts ON datas.id = texts.id",
                        connection))
                    {
                        using (IDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                LoadCard(reader, cards);
                            }
                        }
                    }
                }
                _cards = cards;
            }
            catch (Exception ex)
            {
                throw new Exception("Could not initialize the cards database. Check the inner exception for more details.", ex);
            }
        }

        public static void InitForMulti(List<string> databaseFullPaths)
        {
            try
            {
                var cards = new Dictionary<int, NamedCard>();

                foreach(var databaseFullPath in databaseFullPaths)
                {
                    if (!File.Exists(databaseFullPath))
                        continue;

                    using (SqliteConnection connection = new SqliteConnection("Data Source=" + databaseFullPath))
                    {
                        connection.Open();

                        using (IDbCommand command = new SqliteCommand(
                            "SELECT datas.id, ot, alias, setcode, type, level, race, attribute, atk, def, texts.name, texts.desc"
                            + " FROM datas INNER JOIN texts ON datas.id = texts.id",
                            connection))
                        {
                            using (IDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    LoadCard(reader, cards);
                                }
                            }
                        }
                    }
                }
                _cards = cards;
            }
            catch (Exception ex)
            {
                throw new Exception("Could not initialize the cards database. Check the inner exception for more details.", ex);
            }
        }

        internal static NamedCard GetCard(int id)
        {
            var cards = _cards;
            cards.TryGetValue(id, out var card);
            return card;
        }

        internal static IList<NamedCard> GetAllCards()
        {
            // Published dictionaries are never mutated; readers retain one snapshot
            // while another room bot builds a replacement catalog privately.
            var cards = _cards;
            return new List<NamedCard>(cards.Values);
        }

        private static void LoadCard(IDataRecord reader, IDictionary<int, NamedCard> cards)
        {
            NamedCard card = new NamedCard(reader);
            cards[card.Id] = card;
        }
    }
}
