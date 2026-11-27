# Battle Spirits Standard

This branch uses the project's native Battle Spirits Standard runtime. The English UI
opens Solo, the deck editor, Puzzle, direct LAN and replay from the main menu. The
duel HUD puts the eight Standard steps across the top and match controls along
the bottom. Each player has a Life display and public counters for Reserve,
Deck, Hand and Trash. Turn ownership and decision priority have separate labels.
The opponent's hand shows card backs. Selecting a card opens its printed face,
English effect text and legal actions. In the deck editor, **Deck overview**
shows the number of cards by type and cost. The deck editor header also shows
how many cards are missing from the 40-card minimum as the deck is edited.
In Solo, **Play** opens a match preview with your selected deck and CPU
opponent. Choose **Start Duel** to enter the arena, or **Back** to return to
the opponent list.

Open `Assets/Scenes/Boot.unity` in Unity **6000.0.24f1** with the Windows x64 target.
Keep the project's interface bundles available. For a first check, select a
starter deck, open Solo and play through an opening hand, a Main Step and an
opponent-turn Flash decision. `BATTLE_SPIRITS_INTEGRATION.md` describes the
shipped 62-card catalog, four starter decks, ten puzzles and the source checks.
The rules and card support cover the shipped pool. The supplied tournament floor
rules are reference material for organized play.

## MDPro3 baseline and attribution

A new version of YGOPro in Unity with MasterDuel Assets.

Unity version: 6000.0.24f1

The recovered English Yu-Gi-Oh! baseline is saved on
`backup/2026-11-27-before-standard-ui-conversion` at `cb251af31`.
It was recovered from `2c7fd2ee7`. See [recovery and verification](ENGLISH_REPAIR.md)
for the historical MDPro3 fixes.

The original English restoration uses the **MDPro3 1.4.7** snapshot
from before Vietnamese localization. UI, card and prerelease languages default
to English. Known URP/texture compatibility settings are retained. See [restore notes](RESTORE_ENGLISH.md)
for the exact baseline, backup, preserved branches and validation limits.

The deep English migration also converts application comments, Editor menus,
lookup keys and authored prefab text to English. Legacy input keys and original
multilingual card/locale data remain compatible. See [English migration details](ENGLISH_MIGRATION.md).

### Other required folders

* Platforms: https://code.mycard.moe/sherry_chaos/mdpro3-assetbundles
* Picture/Art: https://code.mycard.moe/mycard/hd-arts
* Picture/Closeup: https://code.mycard.moe/mycard/ygopro2-closeup
* Picture/DIY: You can find it from the released MDPro3
* Sound: https://code.moenext.com/mycard/mdpro3-sound

### Tools

* DumpShaders: Used for replacement during built-in assets packaging.
* YGO Classes: Used to compile dependencies from YGOPro in this project.
* Translations: python scripts used to split translation.csv to translation.conf.
* QuickBMS: decrypt IDS\_ITEM.bytes(names of items in Master Duel) and IDS\_ITEMDESC.bytes(descriptions of items in Master Duel);

### For Contributors:

* If you want to edit in-game translations, please visit https://docs.google.com/spreadsheets/d/1BbSTxgobDqLyHL7De6uFSqfGiK9WIH3no5fQPW2Xwls/edit?usp=sharing.
* If you want to edit bot.conf(Windbot), please edit it on YGOMobile(https://github.com/fallenstardust/YGOMobile-cn-ko-en), this project copy these files from it.

### Unity Notes:

Unity 6000.32f1 Crashes when loading the following Assetbundles:
fxp\_HL\_EXdeck\_001 (Material 2020.3.48f1)
PlayableGuide\_C001\_Far
PlayableGuide\_C001\_Far\_Mat13
PlayableGuide\_C001\_Near
PlayableGuide\_C001\_Near\_Mat13


The CID startup-data fix ships `Data/cards_Lite.json`, so opening mates/items
does not require a separately downloaded full `cards.json`. See
[runtime mapping data and verification](Tools/RuntimeData/README.md).

WindBot also fits long names into the existing PlayerInfo field without changing
the selected bot/deck. See [handshake fix and test limits](Tools/RuntimeData/WINDBOT_HANDSHAKE.md).

Optional news/avatar images now fail gracefully: English news keeps its title
and link over a local fallback when the upstream thumbnail is unavailable. See
[remote image diagnostics and validation](Tools/RuntimeData/REMOTE_IMAGES.md).
