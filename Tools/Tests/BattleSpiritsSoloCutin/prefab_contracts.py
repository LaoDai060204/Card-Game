#!/usr/bin/env python3
"""Contracts against shipped prefab bytes and actual Unity object references."""
from hashlib import sha256
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[3]
# These are the existing reviewed assets, recorded before runtime controller fixes.
# Byte equality also preserves every serialized layout, fileID, GUID and callback.
EXPECTED = {
 'Assets/Prefabs/UI/ItemSolo.prefab': 'bd9ef3dd7687a32441acb8dc6b7b17aaa09f4271fef9d203ef8959950eeadc83',
 'Assets/Prefabs/UI/ItemSolo.prefab.meta': '6c65669182ae4a0e7f4670b4d4dc3d7629d363930acd472a3c636bdee4cad611',
 'Assets/Prefabs/UI/ItemCutin.prefab': 'b940ed20d3da549a276a36bee925be167d940d86714f70bb8d27dc61866c0b8c',
 'Assets/Prefabs/UI/ItemCutin.prefab.meta': '7911f6c2048ac9a60ef0b18d79dae8ac3916f52c79498a494fa5b38867969660',
 'Assets/Prefabs/ServantUI/SoloSelectorUI.prefab': '670bdf8a82902cd1078764b6a648b93102e0a286b33e5f9d1bf8cbbf320263f5',
 'Assets/Prefabs/ServantUI/SoloSelectorUI.prefab.meta': '8c539af6182be0980afc34611aea1d11684c7926bc0050e3c0895345e0e10694',
 'Assets/Prefabs/ServantUI/CutinViewerUI.prefab': 'a7ba268c9d58f2107ae232decdfdcc1a760fc145faf7bf67000c88927dec5aca',
 'Assets/Prefabs/ServantUI/CutinViewerUI.prefab.meta': 'bbb165ce147376d17b577dc0581d305b0003dc1a37f9ba92c4bd0f4e8ff5f574',
}
for path, digest in EXPECTED.items():
 assert sha256((ROOT / path).read_bytes()).hexdigest() == digest, f'Preserved prefab changed: {path}'

def docs(path):
 return dict(re.findall(r'--- !u!\d+ &(\d+)\n(.*?)(?=--- !u!|\Z)', (ROOT / path).read_text(), re.S))

def object_by_name(parts, name):
 matches = [d for d in parts.values() if d.startswith('GameObject:') and f'  m_Name: {name}\n' in d]
 assert len(matches) == 1, f'Expected one {name} GameObject'
 return matches[0]

def components(parts, gameobject):
 return [parts[ref] for ref in re.findall(r'component: \{fileID: (\d+)\}', gameobject)]

def rect(parts, gameobject):
 return next(d for d in components(parts, gameobject) if d.startswith('RectTransform:'))

def label_components(parts, label):
 return [d for d in parts.values() if f'  label: {label}\n' in d]

solo = docs('Assets/Prefabs/UI/ItemSolo.prefab')
row = rect(solo, object_by_name(solo, 'ItemSolo'))
assert 'm_SizeDelta: {x: -50, y: 122}' in row
assert 'm_AnchorMin: {x: 0, y: 1}' in row and 'm_AnchorMax: {x: 1, y: 1}' in row
assert len(label_components(solo, 'Image')) == len(label_components(solo, 'Title')) == 1
assert object_by_name(solo, 'Mask')  # Banner is clipped inside the preserved row.
# The production fix must opt this title out of the retained layout group.
title_label = label_components(solo, 'Title')[0]
title_go = solo[re.search(r'm_GameObject: \{fileID: (\d+)\}', title_label).group(1)]
assert any('guid: 306cc8c2b49d7114eaa3623786fc2126' in d for d in components(solo, title_go))
name_area = object_by_name(solo, 'NameArea')
assert any('guid: 30649d3a9faa99c48a7b1166b86bf2a0' in d and 'm_ChildControlWidth: 1' in d and 'm_ChildControlHeight: 1' in d for d in components(solo, name_area))
title_parent = re.search(r'm_Father: \{fileID: (\d+)\}', rect(solo, title_go)).group(1)
assert solo[title_parent] == rect(solo, name_area)
cutin = docs('Assets/Prefabs/UI/ItemCutin.prefab')
assert 'm_SizeDelta: {x: 360, y: 40}' in rect(cutin, object_by_name(cutin, 'ItemCutin'))
for file, callback in [('SoloSelectorUI', 'OnPlay'), ('CutinViewerUI', 'OnAutoPlay')]:
 parts = docs(f'Assets/Prefabs/ServantUI/{file}.prefab')
 assert len(label_components(parts, 'ScrollRect')) == 1
 assert sum(f'        m_MethodName: {callback}\n' in d for d in parts.values()) == 1
 # The ScrollRect must reference a real preserved content and scrollbar.
 scroll = next(d for d in parts.values() if '  m_Content: {fileID:' in d)
 for field in ['Content', 'VerticalScrollbar']:
  ref = re.search(rf'  m_{field}: \{{fileID: (\d+)\}}', scroll).group(1)
  assert ref != '0' and ref in parts, f'{file} missing {field}'
assert any('        m_MethodName: Print\n' in d for d in docs('Assets/Prefabs/ServantUI/CutinViewerUI.prefab').values())
print('Solo/Cutin prefab contracts: 8 assets unchanged; row dimensions, list references and callbacks verified.')
