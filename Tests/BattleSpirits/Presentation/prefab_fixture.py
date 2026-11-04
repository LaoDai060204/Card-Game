"""Export actual migrated prefab hierarchy/components for controlled C# flow tests.

No renderer runs. Serialized geometry/artwork safety is checked separately by
Tools/BattleSpirits/test_native_prefabs.py.
"""
import importlib.util
import json
from pathlib import Path
import re
import sys

root = Path(sys.argv[1]).resolve()
spec = importlib.util.spec_from_file_location('native_migration_fixture', root / 'Tools/BattleSpirits/migrate_native_prefabs.py')
migration = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = migration
spec.loader.exec_module(migration)
scripts = migration.scripts()
paths = list(migration.PATHS) + ['Assets/Prefabs/ServantUI/MainMenuUI.prefab']
by_guid = {migration.guid_for(path): (path, (root / path).read_text()) for path in paths}

def component_type(block):
    if block.kind == '225':
        return 'CanvasGroup'
    if block.kind != '114':
        return None
    body = block.body
    match = migration.SCRIPT.search(body)
    name = Path(scripts.get(match['guid'], '')).stem if match else ''
    if name.startswith('SelectionToggle'):
        return 'SelectionToggle'
    if name.startswith('SelectionButton'):
        return 'SelectionButton'
    if 'm_TextComponent:' in body:
        return 'TMP_InputField'
    if 'm_CaptionText:' in body:
        return 'TMP_Dropdown'
    if 'm_fontAsset:' in body:
        return 'TextMeshProUGUI'
    if 'm_UVRect:' in body:
        return 'RawImage'
    if 'm_Sprite:' in body:
        return 'Image'
    if 'm_OnClick:' in body:
        return 'Button'
    if 'm_IsOn:' in body:
        return 'Toggle'
    if 'm_Viewport:' in body and 'm_Content:' in body:
        return 'ScrollRect'
    return name if name in {'ElementObjectManager', 'CanvasGroup'} else None

result = {}
for path in paths:
    source = (root / path).read_text()
    rows = migration.resolved_paths(source, by_guid)
    cache = {}
    output = []
    for row in rows:
        origin = row.get('sourcePrefab', path)
        if origin not in cache:
            cache[origin] = list(migration.blocks((root / origin).read_text()))
        blocks = cache[origin]
        go = next(block for block in blocks if block.kind == '1' and block.id == row['gameObject'])
        components = []
        for block in blocks:
            owner = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', block.body)
            if owner and owner[1] == go.id:
                kind = component_type(block)
                if kind and kind not in components:
                    components.append(kind)
        layer = re.search(r'^  m_Layer: (\d+)', go.body, re.M)
        active = re.search(r'^  m_IsActive: (\d+)', go.body, re.M)
        output.append({'path': row['path'], 'components': components,
                       'layer': int(layer[1]) if layer else 5,
                       'active': active is None or active[1] == '1'})
    result[Path(path).stem] = output
print(json.dumps(result, ensure_ascii=False))
