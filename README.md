# Battle Spirits

An unofficial Battle Spirits Standard fan client, using the established native
interface layout from the English project. Startup, decks, Solo opponents,
puzzles, LAN matches and replays use the Battle Spirits rules engine directly.

## Open the project

1. Check out `mdpro3-english` and open the project in **Unity 6000.0.24f1**.
2. Let Unity restore packages. The renderer uses **URP 17.0.3**.
3. Keep the existing interface Addressables/bundles available; the interface
   layout still uses them. The upstream `Platforms` bundle source is
   <https://code.mycard.moe/sherry_chaos/mdpro3-assetbundles>. Build Addressables
   when creating a new player.
4. Open **`Assets/Scenes/Boot.unity`** and press Play. The target is Windows x64.

There is no alternate-game toggle. Solo selects one of four Battle Spirits
opponent decks, Deck edits the Battle Spirits library, Puzzle contains ten
playable challenges, and Online hosts or joins another compatible client over
LAN. Replays record completed Battle Spirits matches.

## Included content

- 62 card definitions with explicit tested effect profiles, English inspection
  text, verified printed symbols and persistent numeric IDs.
- Four exact 40-card Standard competition example decks. These are published
  recipes, not a claim of an exhaustive current tier ranking.
- 61 unofficial English printed card faces and the supplied original Pisketona
  face; Pisketona has English inspection text.
- 43 transparent paper standees, 18 empty card frames, blue Core, red Soul Core,
  the supplied card back and three field appearances.
- Native two-player mirrored field, card inspection, legal-action selection,
  core placement/payment choices and Ritenia/Pisketona summon presentations.
- Battle Spirits wallpaper/avatar choices in the existing appearance widgets.

Sections without replacement content keep their existing place in the menu and
show an empty library or disabled action. Music/voice slots await supplied Battle
Spirits audio; native interface clicks use original procedural tones.

Decks and replays are saved below
`Application.persistentDataPath/BattleSpirits`. Existing legacy deck and replay
folders are not migrated or overwritten.

## Verification

See [integration and test scope](BATTLE_SPIRITS_INTEGRATION.md),
[catalog provenance](Tools/BattleSpiritsImport/README.md), and
[native deck checks](Tools/BattleSpiritsNativeTests/README.md).
Production-source C# harnesses and static audits do **not** replace Unity Play
Mode, shader/render checks or a Windows player build.

## Attribution

This project retains interface framework code and third-party notices from
MDPro3. Original Battle Spirits card designs
and artwork belong to BNP/BANDAI and their respective creators; crossover rights
remain with their owners. Source images, credits and official reference snapshots
are preserved in `Data/BattleSpirits/provenance`. English card translations and
paper-standee derivatives are unofficial. Existing third-party license files
remain in their original locations.
