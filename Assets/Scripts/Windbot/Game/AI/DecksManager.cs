using System;
using System.Collections.Generic;
using System.Reflection;

namespace WindBot.Game.AI
{
    public static class DecksManager
    {
        private class DeckInstance
        {
            public string Deck { get; private set; }
            public Type Type { get; private set; }
            public string Level { get; private set; }

            public DeckInstance(string deck, Type type, string level)
            {
                Deck = deck;
                Type = type;
                Level = level;
            }
        }

        private static Dictionary<string, DeckInstance> _decks;
        private static List<DeckInstance> _normalDecks;
        private static Random _rand;
        private static readonly object _sync = new object();

        public static void Init()
        {
            var decks = new Dictionary<string, DeckInstance>();
            var normalDecks = new List<DeckInstance>();

            Assembly asm = Assembly.GetExecutingAssembly();
            Type[] types = asm.GetTypes();
            
            foreach (Type type in types)
            {
                if (!typeof(Executor).IsAssignableFrom(type) || type.IsAbstract || type.ContainsGenericParameters)
                    continue;

                // Assembly-CSharp also contains Unity UI and editor classes. Their
                // attribute constructors must not run on the bot's worker thread.
                object[] attributes = type.GetCustomAttributes(typeof(DeckAttribute), false);
                foreach (object attribute in attributes)
                {
                    DeckAttribute deck = (DeckAttribute)attribute;
                    var instance = new DeckInstance(deck.File, type, deck.Level);
                    decks.Add(deck.Name, instance);
                    if (deck.Level == "Normal")
                        normalDecks.Add(instance);
                }
            }

            // Multiple room bots can initialize concurrently. Publish complete
            // collections rather than exposing a catalog while it is being filled.
            lock (_sync)
            {
                _decks = decks;
                _normalDecks = normalDecks;
                _rand = new Random();
            }

            Logger.WriteLine("Decks initialized, " + decks.Count + " found.");
        }

        public static Executor Instantiate(GameAI ai, Duel duel)
        {
            DeckInstance infos;

            lock (_sync)
            {
                if (_decks == null)
                    throw new InvalidOperationException("WindBot deck catalog has not been initialized.");

                string deck = ai.Game.Deck;
                if (deck == null || !_decks.TryGetValue(deck, out infos))
                {
                    if (_normalDecks.Count == 0)
                        throw new InvalidOperationException("WindBot has no Normal deck executor available for random selection.");
                    infos = _normalDecks[_rand.Next(_normalDecks.Count)];
                }
            }

            Executor executor = (Executor)Activator.CreateInstance(infos.Type, ai, duel);
            executor.Deck = infos.Deck;
            return executor;
        }
    }
}
