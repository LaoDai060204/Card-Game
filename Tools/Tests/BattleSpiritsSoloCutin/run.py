#!/usr/bin/env python3
"""Exercise the production Solo/Cutin controllers, handlers and scroll owner."""
import json
import os
from pathlib import Path
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
SDK = Path(os.environ.get('DOTNET_ROOT', '/tmp/codex-dotnet'))
version = sorted((SDK / 'sdk').iterdir())[-1]
reference = sorted((SDK / 'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net10.0'))[-1]
runtime = sorted((SDK / 'shared/Microsoft.NETCore.App').iterdir())[-1].name
sources = [
    'Assets/Scripts/MDPro3/Servant/SoloSelector.cs',
    'Assets/Scripts/MDPro3/Servant/CutinViewer.cs',
    'Assets/Scripts/MDPro3/UI/ServantUI/SoloSelectorUI.cs',
    'Assets/Scripts/MDPro3/UI/ServantUI/CutinViewerUI.cs',
    'Assets/Scripts/MDPro3/UI/SelectionButton/SelectionToggle_Solo.cs',
    'Assets/Scripts/MDPro3/UI/SelectionButton/SelectionToggle_Cutin.cs',
    'Assets/Scripts/MDPro3/UI/SuperScrollView/SuperScrollView.cs',
]
with tempfile.TemporaryDirectory(prefix='bs-solo-cutin-') as work:
    target = Path(work) / 'SoloCutin.dll'
    response = Path(work) / 'compile.rsp'
    options = ['-nologo', '-target:exe', '-nowarn:0649', '-out:"' + str(target) + '"']
    options += ['-r:"' + str(p) + '"' for p in sorted(reference.glob('*.dll'))]
    options += ['"' + str(ROOT / p) + '"' for p in sources]
    options += ['"' + str(p) + '"' for p in sorted(HERE.glob('*.cs'))]
    response.write_text('\n'.join(options))
    env = dict(os.environ, DOTNET_CLI_HOME=str(Path(work) / 'dotnet'), DOTNET_CLI_TELEMETRY_OPTOUT='1')
    subprocess.run([str(SDK / 'dotnet'), 'exec', str(version / 'Roslyn/bincore/csc.dll'), '@' + str(response)], env=env, check=True)
    target.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {'tfm': 'net10.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime}}}))
    subprocess.run([str(SDK / 'dotnet'), 'exec', str(target)], cwd=ROOT, env=env, check=True)
subprocess.run(['python3', str(HERE / 'prefab_contracts.py')], cwd=ROOT, check=True)
