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
    if match and match['guid'] == '2da0c512f12947e489f739169773d7ca':
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
    if 'm_CellSize:' in body and 'm_Constraint:' in body:
        return 'GridLayoutGroup'
    if 'm_HorizontalFit:' in body and 'm_VerticalFit:' in body:
        return 'ContentSizeFitter'
    if 'm_MinWidth:' in body and 'm_FlexibleWidth:' in body:
        return 'LayoutElement'
    if match and match['guid'] == '30649d3a9faa99c48a7b1166b86bf2a0':
        return 'HorizontalLayoutGroup'
    if match and match['guid'] == '59f8146938fff824cb5fd77236b75775b':
        return 'VerticalLayoutGroup'
    return name if name in {'ElementObjectManager', 'CanvasGroup'} else None


def scalar(body, key):
    match = re.search(r'^  ' + re.escape(key) + r': (.*)$', body, re.M)
    return match[1] if match else None


def vector(body, key):
    value = scalar(body, key)
    if value is None:
        return None
    return {axis: float(number) for axis, number in re.findall(r'(\w+): ([-+\deE.]+)', value)}


def source_origin(row, path):
    return row.get('sourcePrefab', path)


def reference_path(body, key, row, rows, path, blocks):
    value = scalar(body, key)
    match = re.search(r'fileID: (-?\d+)', value or '')
    if not match or match[1] == '0':
        return None
    target = next((block for block in blocks if block.id == match[1]), None)
    if target is None:
        return None
    owner = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', target.body)
    owner_id = owner[1] if owner else target.id
    origin = source_origin(row, path)
    candidates = [other['path'] for other in rows if source_origin(other, path) == origin
                  and other['gameObject'] == owner_id]
    if not candidates:
        return None
    # A nested prefab may have multiple instances; select the reference in the
    # same nearest hierarchy instead of merging nodes by their display name.
    return max(candidates, key=lambda candidate: len(__import__('os').path.commonprefix([candidate, row['path']])))


def component_state(kind, block, row, rows, path, blocks):
    body = block.body
    state = {'type': kind}
    numeric = {
        'm_Enabled': 'enabled', 'm_Interactable': 'interactable',
        'm_ReadOnly': 'readOnly', 'm_RaycastTarget': 'raycastTarget',
        'm_Alpha': 'alpha', 'm_BlocksRaycasts': 'blocksRaycasts',
        'm_IgnoreParentGroups': 'ignoreParentGroups',
        'm_ChildControlWidth': 'childControlWidth', 'm_ChildControlHeight': 'childControlHeight',
        'm_ChildForceExpandWidth': 'childForceExpandWidth', 'm_ChildForceExpandHeight': 'childForceExpandHeight',
        'm_Constraint': 'constraint', 'm_ConstraintCount': 'constraintCount',
        'm_HorizontalFit': 'horizontalFit', 'm_VerticalFit': 'verticalFit',
        'm_FlexibleWidth': 'flexibleWidth', 'm_FlexibleHeight': 'flexibleHeight',
        'm_PreferredWidth': 'preferredWidth', 'm_PreferredHeight': 'preferredHeight',
        'm_MinWidth': 'minWidth', 'm_MinHeight': 'minHeight',
        'm_fontSize': 'fontSize', 'm_ContentType': 'contentType', 'm_LineType': 'lineType'
    }
    for serialized, exported in numeric.items():
        value = scalar(body, serialized)
        if value is not None:
            state[exported] = float(value)
    for serialized, exported in {'m_CellSize': 'cellSize', 'm_Spacing': 'spacing'}.items():
        value = vector(body, serialized)
        if value is not None:
            state[exported] = value
    if kind in ('HorizontalLayoutGroup', 'VerticalLayoutGroup'):
        value = scalar(body, 'm_Spacing')
        if value is not None:
            state['spacingScalar'] = float(value)
    if kind == 'TMP_InputField':
        for key in ('m_TextViewport', 'm_TextComponent', 'm_Placeholder', 'm_TargetGraphic'):
            state[key[2:]] = reference_path(body, key, row, rows, path, blocks)
    if kind == 'ScrollRect':
        for key in ('m_Viewport', 'm_Content'):
            state[key[2:]] = reference_path(body, key, row, rows, path, blocks)
    if kind == 'TextMeshProUGUI':
        value = scalar(body, 'm_text')
        if value is not None:
            try:
                state['text'] = json.loads(value) if value.startswith('"') else value
            except json.JSONDecodeError:
                state['text'] = value
    return state

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
        states = []
        rect = None
        for block in blocks:
            owner = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', block.body)
            if owner and owner[1] == go.id:
                kind = component_type(block)
                if kind and kind not in components:
                    components.append(kind)
                    states.append(component_state(kind, block, row, rows, path, blocks))
                if block.kind == '224':
                    rect = {key[2:]: vector(block.body, key) for key in (
                        'm_AnchorMin', 'm_AnchorMax', 'm_AnchoredPosition', 'm_SizeDelta', 'm_Pivot', 'm_LocalScale')}
        layer = re.search(r'^  m_Layer: (\d+)', go.body, re.M)
        active = re.search(r'^  m_IsActive: (\d+)', go.body, re.M)
        output.append({'path': row['path'], 'components': components,
                       'states': states, 'rect': rect,
                       'layer': int(layer[1]) if layer else 5,
                       'active': active is None or active[1] == '1'})
    result[Path(path).stem] = output
print(json.dumps(result, ensure_ascii=False))
