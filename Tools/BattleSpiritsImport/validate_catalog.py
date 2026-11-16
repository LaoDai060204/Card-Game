#!/usr/bin/env python3
"""Verify the imported catalog, all provenance hashes and native numeric IDs."""
from collections import Counter
import hashlib
import json
from pathlib import Path

project = Path(__file__).resolve().parents[2]
stream = project / 'Assets/StreamingAssets/BattleSpirits'
data = project / 'Data/BattleSpirits'
read = lambda p: json.loads(p.read_text(encoding='utf-8'))
cards = read(stream / 'cards.json')['cards']
by_id = {c['id']: c for c in cards}
assert len(by_id) == len(cards) == 62
assert Counter(c['type'] for c in cards) == {'Spirit': 43, 'Nexus': 3, 'Magic': 16}
assert len(list((stream / 'cards').glob('*.png'))) == 61
assert len(list((stream / 'cards').glob('*.jpg'))) == 1
assert len(list((stream / 'standees').glob('*.png'))) == 43
assert len(list((stream / 'frames').glob('*/*.png'))) == 18
assert not list(stream.rglob('*.zip')) and not list(stream.rglob('*.js'))
mapping = read(data / 'numeric-ids.json')
assert mapping == read(stream / 'numeric-ids.json')
numeric = {v['id']: v['numericId'] for v in mapping['entries']}
assert len(set(numeric.values())) == 62
assert all(c['numericId'] == numeric[c['id']] >= 900000001 for c in cards)
coverage_path = data / 'implemented-cards.json'
if coverage_path.exists():
    coverage = read(coverage_path)
    assert coverage['verified']
    assert {c['id'] for c in cards if c['implemented']} == set(coverage['ids'])
else:
    assert not any(c['implemented'] for c in cards)
for c in cards:
    assert (stream / c['artPath']).is_file()
    assert c['nameEn'] and c['effectTextEn'] and c['sourceUrl']
    assert c['standardBlock'] == 'A' and c['standardLegalityVerified'] and c['symbolsVerified']
    assert sum(r['count'] for r in c['reductions']) == c['reductionSymbols']
    assert sum(s['count'] for s in c['symbolDetails']) == c['symbols']
    if c['type'] == 'Magic':
        assert c['levels'] == [] and c['symbols'] in (0, 1)
        assert all(s['isEx'] for s in c['symbolDetails'])
    else:
        assert len(c['levels']) == 2 and c['symbols'] == 1
        assert c['levels'][0]['cores'] < c['levels'][1]['cores']
assert by_id['26RBS03-022']['reductions'] == [{'color': 'Purple', 'count': 5}]
assert by_id['26RSD03-010']['levels'][1]['soulAlternative']
assert by_id['26RCB01-060']['levels'][1]['soulAlternative']
decks = read(stream / 'starter-decks.json')['decks']
assert len(decks) == 4
for deck in decks:
    assert len(deck['cards']) == 40 and all(i in by_id for i in deck['cards'])
    assert max(Counter(deck['cards']).values()) <= 3
visuals = read(stream / 'visuals.json')['visuals']
assert len(visuals) == 62
assert [v['id'] for v in visuals if v['isBoss']] == ['26RBS03-X01']
for v in visuals:
    if by_id[v['id']]['type'] == 'Spirit':
        assert (stream / 'standees' / (v['id'] + '.png')).is_file()
        assert 0 <= v['left'] < v['right'] <= v['width']
        assert 0 <= v['top'] < v['bottom'] <= v['height']
symbol_records = read(data / 'provenance/printed-symbols.json')['cards']
assert len(symbol_records) == 62
assert sum(s['isEx'] for c in symbol_records for s in c['symbolDetails']) == 23
assert sum(c['symbols'] for c in cards if c['type'] == 'Magic') == 8
for c in symbol_records:
    assert c['verified'] and c['symbolDetails'] == by_id[c['id']]['symbolDetails']
manifest = read(data / 'provenance/asset-manifest.json')['files']
for entry in manifest:
    path = project / entry['path']
    assert path.stat().st_size == entry['bytes']
    assert hashlib.sha256(path.read_bytes()).hexdigest() == entry['sha256'], path
for root in [stream, project / 'Assets/BattleSpirits/Runtime/Data']:
    assert Path(str(root) + '.meta').is_file()
    for path in root.rglob('*'):
        if path.suffix != '.meta' and 'Puzzles' not in path.parts:
            assert Path(str(path) + '.meta').is_file(), path
print(json.dumps({'cards': 62, 'types': dict(Counter(c['type'] for c in cards)), 'decks': [d['name'] for d in decks], 'verifiedSourceCopies': len(manifest), 'symbols': '46 permanent + 8 Magic / 23 EX', 'result': 'passed'}))
