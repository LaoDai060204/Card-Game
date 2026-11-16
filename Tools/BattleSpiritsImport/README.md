# Battle Spirits reviewed catalog

`import_review.py` imports the approved 15 November review checkout. It writes
only `Assets/StreamingAssets/BattleSpirits`, its own new Unity metadata, and
`Data/BattleSpirits` catalog maps and provenance. Puzzle content belongs to the
puzzle importer.

Run from the project root:

```sh
python3 Tools/BattleSpiritsImport/import_review.py
python3 Tools/BattleSpiritsImport/validate_catalog.py
python3 Tools/BattleSpiritsImport/run_smoke.py
```

The importer accepts `--review` and `--private-catalog`. The latter supplies the
approved Pisketona English effect transcription; that private source file is not
published. Its resulting card data is included in the public runtime catalog.
The smoke runner accepts `--dotnet` for another .NET 10 SDK installation and
uses its Roslyn compiler directly, avoiding package-restore mutex restrictions.

The 61 English printed faces, original Japanese Pisketona JPEG, 43 alpha standees,
18 empty frames, core/soul/back images and three supplied playmats retain their
exact source bytes. The original 61 Japanese card references, the Pisketona
original, original printed artist credits, individual attribution and hashes
remain in `Data/BattleSpirits/provenance`. English faces and effect translations
are unofficial. Standee silhouettes are reviewed 2.5D derivatives, not exact
masks, meshes or rigs. Ritenia and Pisketona have explicit boss presentations,
as approved in the owner's 17 November two-player arena reference. The importer
records those IDs directly; artwork, cost and rarity never assign boss status. Playmat reference
markings can describe historical/Eternal rules; they do not define gameplay.
The exact supplied arena HTML and its source hash live in
`Data/BattleSpirits/provenance/arena-reference-20261117.html` and the paired JSON
reference entry. This supplement leaves the imported `references.json` intact.

Each official `detail_iframe` HTML response is archived. Its typed cost,
reductions, levels, Soul Core alternatives, family and block values are parsed
directly. Those official pages currently omit a printed-symbol row. Instead,
all 62 card originals were visually inspected at their bottom-right printed
symbols: every permanent has exactly one (15 EX), eight Magic have one EX,
and eight Magic have no printed symbol. There are 23 EX symbols in total.
`printed-symbols.json` records each result and exact source-image
hash. Symbol type is never inferred from cost, rarity, card color or effect text.

`numeric-ids.json` is an explicit persistent mapping starting at 900000001. Once
created, entries retain their IDs when the import is rerun or the catalog grows;
new entries append beyond the existing maximum. Removed IDs must never be reused.
Native deck files store these values directly and never apply alternate-art
arithmetic. `BattleCatalog.Initialize` accepts the Unity streaming-assets root
or the BattleSpirits directory itself, and repeated calls with that same root
preserve loaded objects. `CardImagePath` respects `artPath`, including Pisketona's
original `.jpg` extension.

All four exact 40-card sample decks retain the published recipe. They are
competition examples from a team event, not a tier ranking or newly optimized
single-player builds. Implementation flags are governed by the effect registry
and its tested coverage, not by the presence of an image or translated text.
Catalog smoke checks are ordinary .NET checks, not Unity Play or build tests.
The importer reads `Data/BattleSpirits/implemented-cards.json` when present and
sets `implemented` only for its explicit confirmed roster. Without that file,
all implementation flags remain false.
Coverage must be explicitly verified, match the production registry version and
match the effect source hash. Both import and validation reject stale coverage;
rerun the rules, effect and Solo/puzzle harnesses before updating it.
