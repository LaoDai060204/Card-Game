using System;
using System.IO;
using MDPro3.BattleSpirits.Puzzles;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MDPro3.BattleSpirits
{
    public sealed partial class StandardFrontend : MonoBehaviour, IBattleFrontend
    {
        private const int DefaultLanPort = 7911;
        private static readonly Color CanvasColor = new Color32(13, 19, 27, 255);
        private static readonly Color PanelColor = new Color32(26, 38, 51, 255);
        private static readonly Color ControlColor = new Color32(38, 53, 69, 255);
        private static readonly Color Ink = new Color32(235, 242, 248, 255);
        private static readonly Color Quiet = new Color32(169, 188, 204, 255);
        private static readonly Color Glacier = new Color32(125, 214, 238, 255);
        private static readonly Color Copper = new Color32(248, 179, 113, 255);

        private RectTransform surface;
        private RectTransform screen;
        private Font font;
        private Text message;
        private Action back;
        private Items cosmetics;
        private AudioSource audioSource;
        private AudioClip clickTone;
        private bool interfaceMuted;

        internal static void Launch(Canvas bootCanvas)
        {
            var root = new GameObject("Standard interface", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            if (bootCanvas != null) bootCanvas.enabled = false;

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var input = new GameObject("Standard input");
                input.transform.SetParent(root.transform, false);
                input.AddComponent<EventSystem>();
                var module = input.AddComponent<InputSystemUIInputModule>();
                if (module.actionsAsset == null) module.AssignDefaultActions();
            }
            root.AddComponent<StandardFrontend>().Initialize();
        }

        private void Initialize()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            surface = Panel(transform, "Background", 0, 0, 1, 1, CanvasColor);
            try
            {
                Program.SetRoot();
                Directory.CreateDirectory(Path.GetDirectoryName(Program.PATH_CONFIG));
                Config.Initialize(Program.PATH_CONFIG);
                InitializeSound();
                BattleIntegration.AttachFrontend(this, this);
                cosmetics = ScriptableObject.CreateInstance<Items>();
                BattleCosmetics.Apply(cosmetics);
                Home();
                BattleIntegration.ReadCommandLine(Environment.GetCommandLineArgs());
            }
            catch (Exception error)
            {
                Page("Unable to open Standard", null);
                ReportError(error.Message);
            }
        }

        private void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true
                || Gamepad.current?.buttonEast.wasPressedThisFrame == true)
            {
                if (EventSystem.current != null
                    && EventSystem.current.currentSelectedGameObject?.GetComponent<InputField>() is InputField input
                    && input.isFocused)
                    return;
                back?.Invoke();
            }
        }

        private void LateUpdate()
        {
            var events = EventSystem.current;
            if (screen == null || events == null) return;
            var selected = events.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy) return;
            foreach (var button in screen.GetComponentsInChildren<Button>())
                if (button.IsInteractable())
                {
                    button.Select();
                    break;
                }
        }

        private void OnDestroy()
        {
            ReleaseArena();
            BattleIntegration.DetachFrontend(this);
            if (cosmetics != null) Destroy(cosmetics);
            if (clickTone != null) Destroy(clickTone);
        }

        private void InitializeSound()
        {
            interfaceMuted = Config.GetBool("BattleMuted", false);
            if (FindFirstObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            const int sampleRate = 24000;
            var samples = new float[1080];
            for (int i = 0; i < samples.Length; i++)
            {
                float envelope = 1f - i / (float)samples.Length;
                samples[i] = .14f * envelope * envelope * (float)Math.Sin(2 * Math.PI * 480 * i / sampleRate);
            }
            clickTone = AudioClip.Create("Standard UI click", samples.Length, 1, sampleRate, false);
            clickTone.SetData(samples, 0);
        }

        private RectTransform Page(string title, Action returnAction)
        {
            ResetScreen();
            back = returnAction;
            Label(screen, "STANDARD", .053f, .919f, .225f, .967f, 34, Glacier);
            Label(screen, title, .244f, .895f, .825f, .973f, 47, Ink);
            Panel(screen, "Heading rule", .052f, .883f, .948f, .885f, ControlColor);
            if (returnAction != null) Button(screen, "Back", .842f, .916f, .948f, .963f, returnAction);
            message = Label(screen, "", .245f, .027f, .948f, .084f, 23, Copper);
            return Node(screen, "Page content", .244f, .095f, .947f, .865f);
        }

        private void ResetScreen()
        {
            if (screen != null)
            {
                screen.gameObject.SetActive(false);
                Destroy(screen.gameObject);
            }
            screen = Node(surface, "Screen", 0, 0, 1, 1);
            message = null;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void Home()
        {
            ReleaseArena();
            var body = Page("Battle Spirits", null);
            Navigation();
            var hero = Panel(body, "Solo", 0, .50f, 1, 1, PanelColor);
            Panel(hero, "Accent", 0, 0, .012f, 1, Glacier);
            Picture(hero, Cosmetic("Wallpaper", Items.ItemType.Wallpaper, BattleCosmetics.DefaultWallpaperId), .63f, .03f, .98f, .98f);
            Label(hero, "Enter the arena", .053f, .60f, .665f, .90f, 67, Ink);
            Label(hero, "Pick a deck and face a computer opponent.", .057f, .39f, .62f, .61f, 30, Quiet);
            Button(hero, "Choose opponent", .057f, .10f, .361f, .33f, Solo, true);
            var study = Panel(body, "Practice", 0, .102f, .482f, .445f, PanelColor);
            Label(study, "Puzzle", .075f, .59f, .95f, .89f, 38, Ink);
            Label(study, "Play a set position with a specific goal.", .075f, .32f, .95f, .64f, 25, Quiet);
            Button(study, "Browse puzzles", .075f, .097f, .66f, .34f, Puzzles);
            var local = Panel(body, "Multiplayer", .518f, .102f, 1, .445f, PanelColor);
            Label(local, "Local network", .075f, .59f, .95f, .89f, 38, Ink);
            Label(local, "Host a match or join a player by IP.", .075f, .32f, .95f, .64f, 25, Quiet);
            Button(local, "Open LAN setup", .075f, .097f, .66f, .34f, Online);
            Focus(hero.GetComponentInChildren<Button>());
        }

        private void Navigation()
        {
            var rail = Panel(screen, "Navigation", .052f, .113f, .220f, .856f, PanelColor);
            Button(rail, "Solo", .08f, .84f, .92f, .96f, Solo);
            Button(rail, "Deck library", .08f, .69f, .92f, .81f, Decks);
            Button(rail, "Appearance", .08f, .54f, .92f, .66f, Appearance);
            Button(rail, "Replays", .08f, .39f, .92f, .51f, Replays);
            Button(rail, "Settings", .08f, .24f, .92f, .36f, Settings);
            Button(rail, "Quit", .08f, .05f, .92f, .17f, ConfirmQuit);
        }

        private void Solo()
        {
            var body = Page("Choose a Solo opponent", Home);
            Navigation();
            var column = Scroll(body, "Opponents", 0, .08f, .68f, 1);
            string deck = Config.GetConfigDeckName();
            string displayName = string.IsNullOrEmpty(deck) ? "Default deck" : Path.GetFileName(deck);
            Label(body, "YOUR DECK", .72f, .86f, 1, .98f, 23, Glacier);
            Label(body, displayName, .72f, .70f, 1, .86f, 32, Ink);
            Button(body, "Change deck", .72f, .59f, 1, .70f, () => PickDeck(Solo));
            foreach (var opponent in BattleCatalog.StarterDecks)
                Row(column, opponent.name, () => ConfirmSolo(deck, displayName, opponent.name, opponent.cards), 84);
        }

        private void ConfirmSolo(string deck, string displayName, string opponent, string[] opponentCards)
        {
            var body = Page("Match briefing", Solo);
            var local = Panel(body, "Player", 0, .31f, .48f, .94f, PanelColor);
            var rival = Panel(body, "Opponent", .52f, .31f, 1, .94f, PanelColor);
            Label(local, "YOUR DECK", .08f, .74f, .95f, .93f, 26, Glacier);
            Label(local, displayName, .08f, .45f, .60f, .75f, 40, Ink);
            Label(local, Config.Get("DuelPlayerName0", "Player"), .08f, .25f, .60f, .44f, 28, Quiet);
            Picture(local, Cosmetic("DuelFace0", Items.ItemType.Face, BattleCosmetics.DefaultFaceId), .60f, .07f, .97f, .74f);
            Label(rival, "CPU OPPONENT", .08f, .74f, .95f, .93f, 26, Copper);
            Label(rival, opponent, .08f, .30f, .60f, .75f, 40, Ink);
            if (opponentCards.Length > 0)
                Picture(rival, BattleCosmetics.LoadTexture(BattleCatalog.CardImagePath(opponentCards[0])), .61f, .10f, .95f, .73f);
            Label(body, "5 Life   ·   4 opening cards", 0, .17f, 1, .27f, 30, Quiet);
            Focus(Button(body, "Start duel", .64f, .035f, 1, .16f,
                () => BattleIntegration.StartSolo(deck, opponent), true));
        }

        private void Online()
        {
            var body = Page("Local network", Home);
            Navigation();
            Label(body, "Player name", .01f, .88f, .36f, .98f, 25, Quiet);
            var name = Input(body, Config.Get("DuelPlayerName0", "Player"), .01f, .77f, .66f, .88f);
            Label(body, "Host address", .01f, .62f, .36f, .72f, 25, Quiet);
            var address = Input(body, "127.0.0.1", .01f, .52f, .65f, .63f);
            Label(body, "Port", .70f, .62f, .98f, .72f, 25, Quiet);
            var port = Input(body, DefaultLanPort.ToString(), .70f, .52f, .98f, .63f);
            void Enter(bool host)
            {
                if (!int.TryParse(port.text, out int number) || number < 1 || number > 65535)
                {
                    ReportError("Enter a port between 1 and 65535.");
                    return;
                }
                if (host) BattleIntegration.Host(number, name.text);
                else BattleIntegration.Join(address.text.Trim(), number, name.text);
            }
            Button(body, "Host", .01f, .32f, .34f, .44f, () => Enter(true), true);
            Button(body, "Join", .37f, .32f, .66f, .44f, () => Enter(false));
            Button(body, "Change deck", .70f, .32f, .98f, .44f, () => PickDeck(Online));
        }

        private void Puzzles()
        {
            var body = Page("Puzzles", Home);
            Navigation();
            var column = Scroll(body, "Puzzle list", 0, .06f, 1, 1);
            foreach (var puzzle in BattlePuzzleCatalog.All)
                Row(column, puzzle.title + "  /  " + puzzle.category, () => BattleIntegration.StartPuzzle(puzzle.id), 75);
        }

        private void Replays()
        {
            var body = Page("Replays", Home);
            Navigation();
            var column = Scroll(body, "Replay files", 0, .06f, 1, 1);
            var files = BattleIntegration.GetReplayFiles();
            if (files.Length == 0) Row(column, "Completed matches appear here.", null, 70);
            foreach (var path in files)
                Row(column, Path.GetFileName(path), () => BattleIntegration.PlayReplay(path), 72);
        }

        private void Settings()
        {
            var body = Page("Settings", Home);
            Navigation();
            Label(body, "Player name", 0, .87f, .64f, .97f, 27, Quiet);
            var name = Input(body, Config.Get("DuelPlayerName0", "Player"), 0, .74f, .68f, .87f);
            bool reduced = Config.GetBool("BattleReducedMotion", false);
            bool muted = Config.GetBool("BattleMuted", false);
            Button motion = null;
            motion = Button(body, "Reduced motion: " + (reduced ? "On" : "Off"), 0, .54f, .48f, .66f, () =>
            {
                reduced = !reduced;
                motion.GetComponentInChildren<Text>().text = "Reduced motion: " + (reduced ? "On" : "Off");
            });
            Button sound = null;
            sound = Button(body, "Interface sounds: " + (muted ? "Off" : "On"), .51f, .54f, 1, .66f, () =>
            {
                muted = !muted;
                sound.GetComponentInChildren<Text>().text = "Interface sounds: " + (muted ? "Off" : "On");
            });
            Button(body, "Save settings", 0, .31f, .40f, .44f, () =>
            {
                string value = name.text.Trim().Replace("->", "→");
                Config.Set("DuelPlayerName0", string.IsNullOrEmpty(value) ? "Player" : value);
                Config.SetBool("BattleReducedMotion", reduced);
                Config.SetBool("BattleMuted", muted);
                interfaceMuted = muted;
                Config.Save();
                ReportError("Settings saved.");
            }, true);
        }

        private void ConfirmQuit()
        {
            var body = Page("Quit Battle Spirits?", Home);
            Button(body, "Quit", 0, .73f, .30f, .88f, Application.Quit, true);
            Button(body, "Keep playing", .35f, .73f, .68f, .88f, Home);
        }

        void IBattleFrontend.ShowMatch(BattleGameSession session) => OpenArena(session);
        void IBattleFrontend.ShowBoss(string cardId) => PreviewBoss(cardId);
        void IBattleFrontend.ReportError(string value) => ReportError(value);
        private static Texture Cosmetic(string key, Items.ItemType type, int defaultId)
        {
            string id = Config.Get(key, defaultId.ToString());
            return BattleCosmetics.LoadTexture(BattleCosmetics.GetPath(id, type));
        }

        private void ReportError(string value)
        {
            if (message != null) message.text = value;
        }
    }
}
