using System;
using System.IO;
using System.Linq;
using MDPro3.BattleSpirits.Core;
using MDPro3.Duel.YGOSharp;

namespace MDPro3.BattleSpirits
{
    public sealed partial class StandardFrontend
    {
        private static string[] SavedDecks() => Directory.GetFiles(BattleDeckStorage.DeckDirectory, "*.ydk", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(BattleDeckStorage.DeckDirectory, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();

        private void PickDeck(Action afterPick)
        {
            try
            {
                var body = Page("Select the deck to play", afterPick);
                var list = Scroll(body, "Saved decks", 0, .065f, 1, 1);
                var files = SavedDecks();
                if (files.Length == 0) Row(list, "Save a deck in the library to select it here.", null);
                foreach (string file in files)
                    Row(list, Path.ChangeExtension(file, null), () => UseDeck(file, afterPick));
            }
            catch (Exception error) { ReportError(error.Message); }
        }

        private void UseDeck(string file, Action afterPick)
        {
            try
            {
                var result = DeckValidator.Validate(BattleCatalog.Cards, BattleDeckStorage.LoadDefinition(file).cards);
                if (!result.IsValid) { ReportError(result.Errors[0]); return; }
                Config.SetConfigDeck(Path.ChangeExtension(file, null));
                Config.Save();
                afterPick();
            }
            catch (Exception error) { ReportError(error.Message); }
        }

        private void Decks()
        {
            var body = Page("Deck library", Home);
            Navigation();
            Button(body, "New deck", .77f, .87f, 1, 1, () => EditDeck(null), true);
            string active = Config.GetConfigDeckName();
            Label(body, string.IsNullOrEmpty(active) ? "No deck selected" : "In use · " + Path.GetFileName(active),
                .02f, .85f, .74f, 1, 30, Glacier);
            var list = Scroll(body, "Deck library", 0, .055f, 1, .81f);
            try
            {
                var files = SavedDecks();
                if (files.Length == 0) Row(list, "Use New deck to create your first deck.", null);
                foreach (string file in files)
                    Row(list, Path.ChangeExtension(file, null), () => EditDeck(file), 72);
            }
            catch (Exception error) { ReportError(error.Message); }
        }

        private void EditDeck(string file)
        {
            Deck deck;
            try
            {
                if (file != null && BattleDeckStorage.LoadDefinition(file).cards.Any(id => !BattleCatalog.TryGet(id, out _)))
                {
                    ReportError("This deck contains an unknown card. Its file was left intact.");
                    return;
                }
                deck = file == null ? new Deck() : new Deck(BattleDeckStorage.ResolveDeckPath(file));
            }
            catch (Exception error) { ReportError(error.Message); return; }

            bool modified = false;
            void LeaveEditor(Action destination)
            {
                if (modified) Confirm("Discard unsaved deck changes?", "Discard", destination);
                else destination();
            }
            var body = Page(file == null ? "New Standard deck" : "Edit Standard deck", () => LeaveEditor(Decks));
            var name = Input(body, Path.GetFileNameWithoutExtension(file) ?? "New deck", 0, .895f, .43f, 1);
            name.onValueChanged.AddListener(_ => modified = true);
            var count = Label(body, "", .455f, .895f, .68f, 1, 26, Glacier);
            Button(body, "Save and use", .69f, .895f, 1, 1, () => SaveDeck(deck, file, name.text), true);

            Label(body, "CARD CATALOG", 0, .77f, .45f, .84f, 22, Quiet);
            var search = Input(body, "", 0, .70f, .455f, .77f);
            search.placeholder = Label(search.transform, "Search by name, type or ID", .035f, .05f, .965f, .95f, 25, Quiet);
            Label(body, "IN THIS DECK", .474f, .70f, .72f, .84f, 22, Quiet);
            var catalog = Scroll(body, "Card catalog", 0, .045f, .455f, .68f);
            var selected = Scroll(body, "Deck contents", .474f, .045f, .727f, .68f);
            var inspector = Panel(body, "Card details", .746f, .045f, 1, .84f, PanelColor);

            void ShowOverview()
            {
                Clear(inspector);
                Label(inspector, "Deck overview", .05f, .87f, .95f, .99f, 27, Ink);
                var list = Scroll(inspector, "Cost distribution", .045f, .035f, .955f, .85f);
                var cards = deck.Main.Select(id => BattleCatalog.TryGet(id, out var card) ? card : null)
                    .Where(card => card != null).ToArray();
                Description(list, cards.Length + " / " + DeckValidator.MinimumCards + " minimum\n"
                    + cards.Count(card => card.IsSpirit) + " Spirit · " + cards.Count(card => card.IsNexus) + " Nexus · "
                    + cards.Count(card => card.IsMagic) + " Magic");
                foreach (var group in cards.GroupBy(card => card.cost).OrderBy(group => group.Key))
                    Description(list, "Cost " + group.Key + "  /  " + group.Count());
            }

            void ShowCards()
            {
                int missing = DeckValidator.MinimumCards - deck.Main.Count;
                string label = deck.Main.Count + (deck.Main.Count == 1 ? " card" : " cards");
                count.text = missing > 0 ? label + " · " + missing + " short" : label;
                count.color = missing > 0 ? Copper : Glacier;
                Clear(selected);
                if (deck.Main.Count == 0) Row(selected, "Add cards from the catalog.", null, 72);
                foreach (var group in deck.Main.GroupBy(id => id))
                {
                    if (!BattleCatalog.TryGet(group.Key, out var card)) continue;
                    int copies = group.Count();
                    Row(selected, copies + " × " + card.DisplayName, () => Inspect(card), 76);
                }
            }

            void Inspect(CardDefinition card)
            {
                Clear(inspector);
                Label(inspector, card.DisplayName, .04f, .87f, .96f, .99f, 25, Ink);
                Picture(inspector, BattleCosmetics.LoadTexture(BattleCatalog.CardImagePath(card.id)), .06f, .36f, .94f, .86f);
                var details = Scroll(inspector, "Card effects", .045f, .13f, .955f, .35f);
                Description(details, card.type + " · Cost " + card.cost + "\n" + card.effectTextEn);
                int id = card.numericId;
                Button(inspector, "Remove", .04f, .025f, .45f, .115f,
                    deck.Main.Contains(id) ? (Action)(() =>
                    {
                        deck.Main.Remove(id);
                        modified = true;
                        ShowCards();
                        Inspect(card);
                    }) : null);
                bool canAdd = card.IsPlayable && deck.Main.Count(other => BattleDeckStorage.SameName(id, other)) < BattleDeckStorage.CopyLimit(id);
                Button(inspector, "Add", .51f, .025f, .96f, .115f, canAdd ? (Action)(() =>
                {
                    deck.Main.Add(id);
                    modified = true;
                    ShowCards();
                    Inspect(card);
                }) : null, true);
            }

            void Filter(string query)
            {
                Clear(catalog);
                foreach (var card in BattleCatalog.Cards.Where(card =>
                    card.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                    || card.type.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                    || card.id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                    Row(catalog, card.DisplayName + "   /   " + card.type + "   /   Cost " + card.cost, () => Inspect(card), 64);
            }

            search.onValueChanged.AddListener(Filter);
            Filter("");
            ShowCards();
            ShowOverview();
            Button(body, "Deck overview", .30f, -.071f, .58f, .025f, ShowOverview);
            if (file != null)
                Button(body, "Deck options", 0, -.071f, .285f, .025f, () => LeaveEditor(() => DeckOptions(file)));
        }

        private void SaveDeck(Deck deck, string original, string enteredName)
        {
            try
            {
                var validation = BattleDeckStorage.Validate(deck);
                if (!validation.IsValid)
                {
                    ReportError(validation.Errors[0]);
                    return;
                }
                string name = Deck.NormalizeDeckFileName(enteredName);
                if (!Deck.IsValidDeckFileName(name) || name.Contains('/') || name.Contains('\\'))
                {
                    ReportError("Enter a valid deck name.");
                    return;
                }
                string relative = string.IsNullOrEmpty(deck.type) ? name : Path.Combine(deck.type, name);
                string destination = BattleDeckStorage.ResolveDeckPath(relative);
                string originalPath = original == null ? "" : BattleDeckStorage.ResolveDeckPath(original);
                if (File.Exists(destination) && !string.Equals(destination, originalPath, StringComparison.OrdinalIgnoreCase))
                {
                    ReportError("A deck with that name already exists.");
                    return;
                }
                if (!deck.Save(name, DateTime.UtcNow, false, false))
                {
                    ReportError("Could not save the deck.");
                    return;
                }
                Config.SetConfigDeck(relative);
                Config.Save();
                Decks();
            }
            catch (Exception error) { ReportError(error.Message); }
        }

        private void DeckOptions(string file)
        {
            var body = Page("Deck options", () => EditDeck(file));
            string name = Path.GetFileNameWithoutExtension(file);
            Label(body, name, 0, .76f, 1, .98f, 44, Ink);
            Button(body, "Use this deck", 0, .57f, .38f, .70f, () => UseDeck(file, Decks), true);
            Button(body, "Delete", .42f, .57f, .70f, .70f, () =>
            {
                var confirm = Page("Delete this deck?", () => DeckOptions(file));
                Label(confirm, name, 0, .70f, 1, .94f, 36, Ink);
                Button(confirm, "Delete deck", 0, .51f, .34f, .64f, () =>
                {
                    try { File.Delete(BattleDeckStorage.ResolveDeckPath(file)); Decks(); }
                    catch (Exception error) { ReportError(error.Message); }
                });
            });
        }

        private void Appearance()
        {
            var body = Page("Appearance", Home);
            Navigation();
            Button(body, "Playmat", 0, .80f, .48f, .96f, () => ChooseCosmetic("Playmat", Items.ItemType.Mat, "DuelField0"));
            Button(body, "Player portrait", .52f, .80f, 1, .96f, () => ChooseCosmetic("Player portrait", Items.ItemType.Face, "DuelFace0"));
            Button(body, "Card sleeve", 0, .60f, .48f, .76f, () => ChooseCosmetic("Card sleeve", Items.ItemType.Protector, "DuelProtector0"));
            Button(body, "Home artwork", .52f, .60f, 1, .76f, () => ChooseCosmetic("Home artwork", Items.ItemType.Wallpaper, "Wallpaper"));
            Button(body, "Summon previews", 0, .39f, .48f, .55f, SummonPreviews);
        }

        private void ChooseCosmetic(string title, Items.ItemType type, string key)
        {
            var body = Page(title, Appearance);
            var list = Scroll(body, "Options", 0, .06f, .60f, 1);
            var preview = Panel(body, "Preview", .63f, .06f, 1, 1, PanelColor);
            var items = BattleCosmetics.GetList(type);
            if (items == null || items.Count == 0)
            {
                Row(list, "No items available.", null);
                return;
            }

            void Show(string code, string label, string path)
            {
                Clear(preview);
                Label(preview, label, .06f, .87f, .94f, .99f, 28, Ink);
                Picture(preview, BattleCosmetics.LoadTexture(path), .06f, .15f, .94f, .85f);
                Button(preview, "Apply", .06f, .03f, .94f, .13f, () =>
                {
                    Config.Set(key, code);
                    Config.Save();
                    ReportError(label + " selected.");
                }, true);
            }

            if (type != Items.ItemType.Protector)
                Row(list, "None", () => Show(Items.CODE_NONE.ToString(), "None", null));
            foreach (var item in items)
                Row(list, item.m_name, () => Show(item.id.ToString(), item.m_name, item.path), 70);
            string saved = Config.Get(key, items[0].id.ToString());
            var current = items.FirstOrDefault(item => item.id.ToString() == saved);
            if (current.id != 0) Show(saved, current.m_name, current.path);
        }

        private void SummonPreviews()
        {
            var body = Page("Summon previews", Appearance);
            var list = Scroll(body, "Summon previews", 0, .06f, 1, 1);
            foreach (var card in BattleCatalog.Cards.Where(card => BattleCatalog.IsBoss(card.id)))
                Row(list, card.DisplayName, () => BattleIntegration.PreviewCutscene(card.id), 88);
        }
    }
}
