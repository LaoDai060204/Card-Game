# CID / YDK runtime mapping

`Data/cards_Lite.json` is shipped in Git so a fresh clone does not need the full
`Data/cards.json` to open items, mates, descriptions or other CID-based resources.
The restored pre-Vietnamese baseline omitted that ignored runtime file while
`Cid2Ydk.Initialize` read it unconditionally. This caused FileNotFoundException
from MateViewerUI even for plain item names with no embedded card tag.

The compact file contains **14,390 mappings**, retaining only `cid` and `id`.
It is the exact verified snapshot previously stored in repository history at
`db9007d14`, sourced from <https://ygocdb.com/api/v0/cards.zip>, with the project's
93 alternate-art overrides applied. It is not a guessed map from card database
aliases. `cards-source.json` records the original archive/JSON, alternate input
and output SHA-256 hashes; no new download or latest-data claim is made.

The loader now:

- Returns plain/null/empty text without reading mapping files.
- Reads the lite file first, then valid full data if the lite file is unavailable
  or corrupt. Local alternate-art mappings apply to either source.
- Warns and retains safe existing fallback behavior when neither source exists.
  A cache-write failure does not discard already loaded data.
- Handles overflowing card tags without throwing and preserves official ID-zero
  records. Zero/negative reverse lookups remain sentinel values rather than
  matching an unassigned official mapping.

After updating, close/reopen Unity 6000.0.24f1 and run `Assets/Scenes/Boot.unity`.
Keep `Data` beside `Assets` in the project, or beside the Windows executable in a
player package. **Do not create an empty cards.json to hide the error.** The full
JSON is optional when the bundled lite data is present.

## Rebuild and test

To regenerate from a separately downloaded/extracted official snapshot:

```bash
python Tools/RuntimeData/build_cards_lite.py --source /path/to/cards.json --source-archive /path/to/cards.zip
```

Review the resulting mapping and provenance together before committing. The
builder keeps the original CID/YDK values, including an official unassigned ID 0.

The optional harness uses .NET SDK 10 outside Unity, compiling the actual helper
with controlled Unity/CardsManager stubs under C# 9:

```bash
dotnet run --project Tools/Tests/RuntimeData/RuntimeData.Tests.csproj -c Release -- Data/cards_Lite.json
```

**17 scenarios pass**, including a clone with lite data but no cards.json, corrupt
inputs, alternate overrides, a read-only cache path, overflowing tags and reverse
sentinels. All 93 alternate mappings and the compact file checksum were verified.
This is not a Unity Editor Play/render or Windows build test. Puzzle, AI launch
and other gameplay fixes are outside this change.
