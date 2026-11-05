# Official Battle Spirits card catalog

The active format is **Japanese Battle Spirits Standard**, using the supplied Standard v1.0 PDFs and the block-A season **1 April 2026–31 March 2027**. The catalog was extended on **4 November 2026** against the Japanese official [product list](https://www.battlespirits.com/product/), [card list](https://www.battlespirits.com/cardlist/) and [restriction list linked by the Standard manual](https://www.battlespirits.com/rule/limited.php). Original per-card access dates remain in the source manifest.

| Included source | Release date | Distinct printed card IDs |
| --- | --- | --- |
| BS76 — エターナルブースター 永皇の輝き, Eternal | 2026-05-30 | 123 |
| 26RBS03 — 絶界の覇者, Standard | 2026-10-17 | 117 |
| Previously verified vanilla Spirit starter cards | Historical BS01/BS02 cards | 16 |
| Explicit BS01 boss / Nexus / Magic baselines | Historical BS01 cards | 5 |
| Supported Standard prints from 26RBS01 | 2026-04-18 | 4 |
| Supported Standard prints from 26RBS02 | 2026-07-18 | 5 |

The catalog contains **270 official cards and 270 PNG images**. The active **19-card Standard pool** contains 16 Spirits with no printed ability and three Spirits with the explicitly supported **Super Unleash** Lv2 alternative: `26RBS03-052`, `26RBS03-061`, `26RBS03-071`. All 19 original faces were individually inspected for block A and their single printed color symbol; official HTML verifies block, stats and the alternate Soul Core threshold. Three supplied Standard decks have 40 cards and respect the three-per-name limit.

The 21 historical supported BS01/BS02 cards remain **Eternal-only compatibility data** and cannot enter an active Standard match. Another 230 cards have no implemented rules in either format; in total **251 catalog entries cannot be played in the active Standard format**. `implemented` never substitutes for format eligibility or actual rules code. Unreviewed Standard entries retain `legalityStatus: "not-verified"`; their Eternal legality is separately unknown. Announced BS77 (2026-11-21) and 26RBS04 (2027-01-23) remain upcoming products.

## Files

- `Data/BattleSpirits/cards.json`: official IDs, Japanese names/effects, observed stats, release/format metadata and local artwork paths. Alternate illustrations are deduplicated by printed card ID; multi-face observations remain in the raw catalog's `faces` field.
- `Data/BattleSpirits/card-sources.json`: product/card HTML hashes, original artwork URLs, WebP hashes, bundled PNG hashes, dimensions and decoded RGBA hashes.
- `Data/BattleSpirits/banlist.json`: the official restriction list effective **2026-09-01**, fetched again on **2026-11-04**. Both current official format manuals link to this page. Cards are matched by ID and original Japanese name within the applicable format.
- `Data/BattleSpirits/starter-cards.json`: the baseline of verified vanilla cards. Updating official data does not silently change this implementation baseline.
- `Data/BattleSpirits/implemented-cards.json`: exact official stat/effect baselines, printed-symbol observations and the approved runtime rules profile for the five additional cards. Changing a data flag cannot substitute for the runtime registry.
- `Data/BattleSpirits/standard-starter-cards.json`: the reviewed 19-card block-A baseline, printed symbols, normal Core level thresholds and the three explicit `superUnleash` flags. This is not inferred from a set ID or empty effect text.
- `Data/BattleSpirits/StarterDecks/*.json`: the three actual 40-card Standard recipes generated from the production AI profile API.
- `Picture/Art/BS*.png` and `Picture/Art/26RBS*.png`: Unity-compatible, lossless format conversions of official WebP images. Import verifies exact decoded RGBA equality; the artwork is not resized, cropped or redrawn. Original notices on the cards remain intact.

Printed symbol counts are not exposed by the inspected detail HTML. Unreviewed cards therefore keep their symbols unknown. Standard starter symbols are preserved from individual original-image inspection. The three Super Unleash cards keep their ordinary Lv2 thresholds (3, 4 and 4 normal Cores respectively) and additionally permit Lv2 with a Soul Core; this is **not** a universal rule for other Spirits. HTML fixtures pin the Soul Core icon, which older parsing had omitted from the Lv2 thresholds.

## Commands

Run these commands from the repository root with **Python 3.9 or later**:

```bash
python Tools/BattleSpiritsCards/test_catalog.py
python Tools/BattleSpiritsCards/catalog.py validate
python Tools/BattleSpiritsCards/catalog.py discover --refresh
python Tools/BattleSpiritsCards/catalog.py check --id BS76-X01
```

`discover` reports the latest released Eternal and Standard boosters and separates upcoming releases. `check` reports changes to the official restriction list and parsed card fields without editing the game data. Omit `--id` to check the entire bundled catalog. It exits nonzero when an update, changed card, restriction or source error needs review.

For a deeper image audit, install ImageMagick (`magick` or `convert` on PATH):

```bash
python Tools/BattleSpiritsCards/catalog.py validate --pixels
```

This checks every decoded PNG pixel hash. If the original official WebP response is still in the download cache, it also checks its source hash and exact WebP/PNG pixel equality. The cache is optional for validation; missing source files are reported by a lower verified-source count rather than downloaded silently.

## Importing a later snapshot

Commit and push a backup branch before updating, as required by this repository. Then run:

```bash
python Tools/BattleSpiritsCards/catalog.py import --refresh
python Tools/BattleSpiritsCards/catalog.py validate --pixels
```

An import fetches the latest released booster in each format plus all reviewed Eternal and Standard baselines, including the nine supported prints from older Standard sets. It needs Internet access and ImageMagick for WebP-to-PNG conversion. It stages and validates the complete snapshot before replacing bundled metadata or artwork, and restores replaced files if publication fails. Retained artwork from older snapshots is not deleted automatically. Use Git backups to retain older catalog versions and decks built with them.

The importer fails for changed starter stats/effects, Standard block/keyword changes, imports outside the verified block-A season, malformed source pages, changed forbidden-pair restrictions, future restriction lists, or artwork that does not preserve pixels. Review such changes before modifying an implementation. Soul Magic, Succession and other text effects remain unsupported; new abilities never become playable merely because a snapshot is imported.

`--as-of YYYY-MM-DD` selects the Japanese calendar date used to classify releases; the default is today's date in Japan. It does not retrieve a historical version of the official website. `--workers` accepts 1–4; `--cache` selects the response-cache folder.

## Rights and verification scope

Names, artwork and card text belong to BANDAI/BNP and the credited owners/illustrators. The source manifest does not infer an open-source artwork license or redistribution permission. These tools verify source data, packaged images and supported-card flags; they do not establish complete Standard/Eternal behavior or substitute for Unity Play/build testing.

## Active template resource replacement

The native forms resolve Battle Spirits faces through each catalog entry's `artPath` in the existing `Picture/Art` directory. The separate `Picture/BattleSpirits` folder has been removed. Old numeric Yu-Gi-Oh artwork was removed from this branch and remains in Git backups and `main`. Original printed Battle Spirits faces retain exact decoded official artwork pixels.

`Picture/Closeup/BS01-X01.png` and `BS01-097.png`, when present, are original fan-made renders from the project's procedural 3D boss models. Their provenance is separate from official card artwork; they are not official transparent cutouts and do not imply a full set of per-card models.

The original regular-booster snapshot was imported in October. On 4 November, Standard records were reviewed against their current HTML, nine supported prints from 26RBS01/02 were added, and all 19 supported Standard prints were individually inspected. Existing old source dates are preserved unless the specific record was fetched again. Supporting-set metadata identifies the older sets as partial supported-card imports, not complete set imports.
