# Official Puzzle runtime data

`Puzzle/` belongs at the project root, beside `Assets` and `Data`. The 104 Lua
files are restored unchanged from the official `mdpro3-other` snapshot:

- Source: <https://code.moenext.com/mycard/mdpro3-other>
- Commit: `3a7423e6b389c413cc64b157dc213ab67b2ae551`
- Puzzle tree: `4da0f2aaa80190b12b0b001c9753aadad26961be`
- Per-file original paths, sizes and hashes: `puzzle-source.json`.

There are 55 files at the root, 40 in `CardEffect`, 6 in `FinalAttack`, and 3 in
`SpecialWin`. Original filenames, credits, rules and effects are preserved.
Do not rename these files or replace their card IDs with modern alternatives.

`Data/locales/puzzle-display-text.json` contains 51 reviewed English display
translations for the original Lua AI names and hints. The packet handlers apply
this adapter after reading the complete payload; the Lua files and protocol
data are unchanged. Chinese interfaces retain the original text, and other
interfaces use the reviewed English fallback. Unknown server text remains
unchanged and is never appended to a translation file. Ship this adapter beside
the other locale JSON files when packaging `Data`.

The bundled English CDB supplies all literal card references for 99 puzzles.
Five older WCS puzzles need six legacy definitions/scripts that are not supplied
in a verified compatible form with this project:

| Puzzle | Missing IDs |
| --- | --- |
| `[WCS2006]34_Riddle Challenge` | `511000228` |
| `[WCS2006]35_Interdimensional Invitation` | `511001039` |
| `[WCS2008] Download Puzzle 07` | `511002997` |
| `[WCS2011] Download Puzzle 01` | `511002993` |
| `[WCS2011] Duel Puzzle 04` | `511000229`, `511002995` |

The launcher checks literal setup calls against the game's loaded card data,
including enabled expansions, before creating the native core. Missing files or
cards produce a recoverable message and leave the previous puzzle history
unchanged. This checks basic dependencies; it does not prove a puzzle is solvable.

From the repository root:

```bash
python3 Tools/RuntimeData/validate_puzzles.py --native-lua
python3 Tools/Tests/PuzzleRegression.py
python3 Tools/Tests/DisplayLocalization.py
```

The portable validator checks hashes, the source Git tree, UTF-8, Windows paths
and card-ID coverage. `--native-lua` additionally compiles all 104 files through
the bundled Linux Lua library without executing them. The C# regression harness
requires .NET SDK 10 outside Unity and exercises production parser, launcher and
recommendation methods with controlled UI/native stand-ins. It includes empty
libraries, a one-item library, restored recommendations, deleted files, missing
card data and valid launch side effects.

These checks do not replace Unity 6000.0.24f1 Play mode, a Windows player build,
or playing/solving the individual puzzles.

## Real Linux core initialization

```bash
python3 Tools/Tests/NativePuzzles.py --report /tmp/puzzle-init.json
```

This diagnostic loads the actual English CDB and bundled Lua helpers, executes
the Puzzle and card initialization functions, and queries native field state.
Each Puzzle runs in a fresh subprocess. No helper API is mocked or supplied to
hide missing functions. The current bundled Linux binary produces **97 passes,
two failures and five explicit skips** for the missing legacy IDs. The diagnostic
returns nonzero for the two failures:

- `[WCS2006]24_Dragon Killer`: the obsolete `DUEL_1_FIELD` flag is absent from
  both the bundled constants and current core source. Assigning an arbitrary
  replacement would change the original rules without verified equivalence.
- `[WCS2008] Download Puzzle 08`: the bundled binary selects `c76812113` for
  Harpie Lady 1/2/3 through their shared alias, rather than their actual scripts.
  The included `ocgcore/card_data.h` source restricts alias script selection to
  alternate artworks, but the older shipped Linux binary does not. Clearing the
  CDB alias would break the treated-as-name rule, so original data is preserved.

These results concern the shipped Linux core; Windows native initialization,
effects and puzzle solutions still require validation on the supported target.
Resolving the native mismatch needs a compatible core build and verified legacy
ruling semantics, rather than suppressing the error or replacing card effects.

A local GCC build of the included core/Lua sources confirms the alias diagnosis:
**98 initializations pass, one fails and five skip**. The remaining failure is
the obsolete flag. To reproduce on this Linux cloud image:

```bash
python3 Tools/RuntimeData/build_linux_core.py
python3 Tools/Tests/NativePuzzles.py --library /workspace/.cloud-build/english-ocgcore/libocgcore.so
```

The rebuilt library is a diagnostic output outside the checkout. The cloud's
GCC/glibc produces a GLIBC 2.38 requirement, versus 2.29 in the shipped plugin;
it is not copied over the plugin or advertised as a portable player binary.
Windows binaries and original CDB/scripts remain unchanged.

## Legacy-source investigation

All six pre-errata definitions exist in ProjectIgnis/BabelCDB
`cards-unofficial.cdb` at commit `a40b0ef4e1f25bb527fff533e9563fbfb8863a59`
(SHA-256 `9bf3ddb901baba92aa9c5f0b887f36c3c242c76e3ce8545e5b1d6fa13c380f5a`).
Their exact scripts are in ProjectIgnis/CardScripts `pre-errata/` at
`ed5ed45b3dde041ba8f3655b3ebb0cf517f09321`. They are not drop-in replacements:
an actual probe of this project's core/helpers finds no `Synchro`,
`Duel.CheckReleaseGroupCost`, `Duel.SelectReleaseGroupCost`,
`aux.GetMustBeMaterialGroup`, `Card.IsSpell` or `Card.IsMonster` APIs required by
those scripts. They have therefore not been installed or presented as working
legacy dependencies.
