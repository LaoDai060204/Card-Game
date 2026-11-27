#!/usr/bin/env python3
"""Compile production catalog types directly, without NuGet or Unity."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import tempfile

parser = argparse.ArgumentParser()
parser.add_argument('--dotnet', type=Path, default=Path('/tmp/codex-dotnet/dotnet'))
args = parser.parse_args()
project = Path(__file__).resolve().parents[2]
installation = args.dotnet.parent
sdk = sorted((installation / 'sdk').glob('*'))[-1]
runtime = sorted((installation / 'shared/Microsoft.NETCore.App').glob('*'))[-1].name
reference = sorted((installation / 'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net10.0'))[-1]
with tempfile.TemporaryDirectory(prefix='bs-catalog-smoke-') as folder:
    output = Path(folder)
    response = output / 'compile.rsp'
    sources = [project / 'Assets/BattleSpirits/Runtime/Data/BattleCatalog.cs', project / 'Assets/BattleSpirits/Runtime/Core/CardDefinition.cs', project / 'Assets/BattleSpirits/Runtime/Core/DeckDefinition.cs', Path(__file__).parent / 'tests/CatalogSmoke.cs']
    lines = ['-nologo', '-target:exe', '-out:' + str(output / 'CatalogSmoke.dll')] + ['-r:' + str(p) for p in sorted(reference.glob('*.dll'))] + ['-r:' + str(sdk / 'Newtonsoft.Json.dll')] + [str(p) for p in sources]
    response.write_text('\n'.join(lines))
    (output / 'CatalogSmoke.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {'tfm': 'net10.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime}}}))
    shutil.copyfile(sdk / 'Newtonsoft.Json.dll', output / 'Newtonsoft.Json.dll')
    subprocess.run([str(args.dotnet), 'exec', str(sdk / 'Roslyn/bincore/csc.dll'), '@' + str(response)], check=True, cwd=project)
    subprocess.run([str(args.dotnet), 'exec', str(output / 'CatalogSmoke.dll')], check=True, cwd=project)
