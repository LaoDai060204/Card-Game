# Official Battle Spirits card catalog

The bundled snapshot was checked on **30 October 2026** against the Japanese official [product list](https://www.battlespirits.com/product/), [card list](https://www.battlespirits.com/cardlist/) and [Eternal restriction list](https://www.battlespirits.com/rule/limited.php).

| Included source | Release date | Distinct printed card IDs |
| --- | --- | --- |
| BS76 — エターナルブースター 永皇の輝き, Eternal | 2026-05-30 | 123 |
| 26RBS03 — 絶界の覇者, Standard | 2026-10-17 | 117 |
| Previously verified vanilla Spirit starter cards | Historical BS01/BS02 cards | 16 |

The catalog contains **256 cards and 256 PNG images**. All 240 cards imported from the two newer releases are available for browsing, but their abilities are **not implemented**. Only the 16 verified vanilla Spirits are enabled for matches. `implemented` is not inferred from an effect description.

The Standard entries have `legalityStatus: "not-verified"` and `eternalLegal: false`. This records that their Eternal legality has not been verified; it does not assert that every such card is prohibited. Announced BS77 (2026-11-21) and 26RBS04 (2027-01-23) are recorded as upcoming products, not imported as released cards.

## Files

- `Data/BattleSpirits/cards.json`: official IDs, Japanese names/effects, observed stats, release/format metadata and local artwork paths. Alternate illustrations are deduplicated by printed card ID; multi-face observations remain in the raw catalog's `faces` field.
- `Data/BattleSpirits/card-sources.json`: product/card HTML hashes, original artwork URLs, WebP hashes, bundled PNG hashes, dimensions and decoded RGBA hashes.
- `Data/BattleSpirits/banlist.json`: the official Eternal restrictions effective **2026-09-01**, including restriction provenance. Cards are matched by ID and original Japanese name.
- `Data/BattleSpirits/starter-cards.json`: the baseline of verified vanilla cards. Updating official data does not silently change this implementation baseline.
- `Picture/BattleSpirits/*.png`: Unity-compatible, lossless format conversions of official WebP images. Import verifies exact decoded RGBA equality; the artwork is not resized, cropped or redrawn. Original notices on the cards remain intact.

The original printed symbol counts are not exposed by the inspected card-detail HTML. The 240 new entries therefore keep their symbol counts unknown and remain browse-only. The existing starter symbol counts are preserved from their verified baseline.

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

An import fetches the latest released booster in each format plus the verified starter baseline. It needs Internet access and ImageMagick for WebP-to-PNG conversion. It stages and validates the complete snapshot before replacing bundled metadata or artwork, and restores replaced files if publication fails. Retained artwork from older snapshots is not deleted automatically. Use Git backups to retain older catalog versions and decks built with them.

The importer fails for changed starter stats/effects, malformed source pages, changed forbidden-pair restrictions, future restriction lists, or artwork that does not preserve pixels. Review such changes before modifying an implementation. New abilities never become playable merely because a new snapshot is imported.

`--as-of YYYY-MM-DD` selects the Japanese calendar date used to classify releases; the default is today's date in Japan. It does not retrieve a historical version of the official website. `--workers` accepts 1–4; `--cache` selects the response-cache folder.

## Rights and verification scope

Names, artwork and card text belong to BANDAI/BNP and the credited owners/illustrators. The source manifest does not infer an open-source artwork license or redistribution permission. These tools verify source data, packaged images and supported-card flags; they do not establish complete Eternal card behavior or substitute for Unity Play/build testing.
