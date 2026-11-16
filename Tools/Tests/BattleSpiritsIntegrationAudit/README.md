Run `python Tools/Tests/BattleSpiritsIntegrationAudit/run.py` with the .NET 10 SDK at
`/tmp/codex-dotnet`, or set `DOTNET_ROOT` to another SDK installation.

The tool uses the SDK's Roslyn assemblies without NuGet. It parses every changed
and newly added C# asset since the preserved English baseline using C# 9 under
Windows player and editor symbols. Committed conversion files remain included
after the working tree is clean; `--base` can select another preserved baseline.
A shallow clone needs the baseline commit fetched before running this check. It
also builds a partial source semantic model to audit native CardsManager and
BattleIntegration member references and argument counts across Assets scripts.
This model deliberately does not claim a complete Unity compilation: Unity
assemblies and generated packages are unavailable in the cloud workspace.

Entry checks inspect C# syntax nodes and follow source method symbols, including
unqualified calls and lambdas, with the always-on BattleSpiritsMode branch pruned.
They root the active manager, settings, deck editor, selection-widget and duel
lifecycle methods explicitly, because runtime virtual dispatch is unavailable
in the partial semantic model. They reject old duel/server/bot/protocol calls,
old room transitions, deck publishing and asset-bundle AudioClip loads reachable
from those paths and the direct bootstrap. These checks cover named source
entry paths, not arbitrary reflection, serialized UnityEvents or runtime asset
behavior. The native replay-rename and asynchronous Solo-list regressions execute
the extracted production method bodies.

The JSON report defaults to `/tmp/battle-spirits-integration-audit.json` and
contains exact source locations for findings. `--allow-incomplete` only relaxes
requirements for bridge files that are still being implemented.

Unity Editor Play, player builds, rendering, shader compilation and asset-bundle
contents require separate validation in Unity 6000.0.24f1.
