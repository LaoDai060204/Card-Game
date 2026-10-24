## MDPro3

A new version of YGOPro in Unity with MasterDuel Assets.

Unity version: 6000.0.10f1

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

