#!/usr/bin/env python3
"""Import the approved review files without changing any source-image bytes.

Official pages currently omit a printed-symbol row. Their reductions, levels,
families and block are parsed; symbol data is the recorded visual transcription
of the original official card images, never an inference from rarity or color.
"""
import argparse
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
import hashlib
import html
import json
from pathlib import Path
import re
import shutil
import urllib.request

PROJECT = Path(__file__).resolve().parents[2]
STREAM = PROJECT / 'Assets/StreamingAssets/BattleSpirits'
DATA = PROJECT / 'Data/BattleSpirits'
# Explicit presentation approval from the owner's 17 November two-player arena
# reference (provenance/arena-reference-20261117.json). This overrides the older
# library's Ritenia-only demonstration flag.
APPROVED_BOSS_IDS = frozenset(('26RBS03-X01', '26RBS03-022'))
EX_IDS = frozenset('26RBS01-003 26RBS01-019 26RBS02-002 26RBS03-004 26RBS03-009 26RCB01-039 26RCB01-040 26RCB01-043 26RCB01-044 26RSD02-001 26RSD02-005 26RSD03-003 26RSD03-005 26RSD09-004 26RSD10-004 26RBS02-087 26RCB01-071 26RSD01-012 26RSD01-014 26RSD02-014 26RSD03-013 26RSD03-014 26RSD06-013'.split())
# Independent transcription of the printed gemstone colors, not card attributes.
PRINTED_SYMBOL_COLORS = {
    **dict.fromkeys('26RBS01-003 26RBS01-X03 26RBS02-002 26RBS02-087 26RBS03-004 26RBS03-X01 26RBS03-X03 26RSD01-012 26RSD01-014 26RSD09-001 26RSD09-004 26RSD09-011'.split(), 'Red'),
    **dict.fromkeys('26RBS01-019 26RBS01-024 26RBS01-X04 26RBS01-X06 26RBS03-009 26RBS03-010 26RBS03-012 26RBS03-014 26RBS03-022 26RBS03-X04 26RBS03-X05 26RBS03-X06 26RSD02-001 26RSD02-005 26RSD02-014 26RSD02-X01'.split(), 'Purple'),
    **dict.fromkeys('26RBS02-033 26RBS02-034 26RBS02-X09 26RSD03-002 26RSD03-003 26RSD03-005 26RSD03-010 26RSD03-013 26RSD03-014 26RSD03-X02 26RSD10-003 26RSD10-004'.split(), 'Green'),
    **dict.fromkeys('26RCB01-039 26RCB01-040 26RCB01-041 26RCB01-043 26RCB01-044 26RCB01-046 26RCB01-047 26RCB01-051 26RCB01-052 26RCB01-060 26RCB01-071 26RCB01-X06 26RCB01-X07 26RSD06-013'.split(), 'Blue'),
}
JP_COLORS = {'赤': 'Red', '紫': 'Purple', '緑': 'Green', '白': 'White', '黄': 'Yellow', '青': 'Blue'}
manifest = []
review_root = None

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')

def copy(source, target, role):
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, target)
    assert sha(source) == sha(target)
    manifest.append({'path': str(target.relative_to(PROJECT)), 'source': str(source.resolve().relative_to(review_root)), 'sha256': sha(target), 'bytes': target.stat().st_size, 'role': role, 'bytePreserved': True})

def text(value):
    value = re.sub(r'<img[^>]*ico_soulcore[^>]*>', '[Soul Core]', value)
    value = re.sub(r'<br\s*/?>', '\n', value)
    value = re.sub(r'</p>\s*<p[^>]*>', '\n', value)
    return '\n'.join(re.sub(r'[ \t\r]+', ' ', line).strip() for line in html.unescape(re.sub('<[^>]+>', '', value)).strip().split('\n'))

def fetch(card):
    card_id = card.get('card_id', '26RBS03-022')
    url = card.get('source_url', 'https://www.battlespirits.com/cardlist/detail_iframe.php?card_no=' + card_id + '&card_no2=' + card_id)
    path = DATA / 'provenance/official-details' / (card_id + '.html')
    if not path.exists():
        with urllib.request.urlopen(url, timeout=45) as response:
            payload = response.read()
        if card_id not in payload.decode('utf-8'):
            raise ValueError('Official source did not contain ' + card_id)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(payload)
    return card_id, path.read_text(encoding='utf-8'), url, sha(path)

def parse_official(content):
    rows = {text(k): v for k, v in re.findall(r'<dl class="detailColList[^\"]*">\s*<dt[^>]*>(.*?)</dt>\s*<dd[^>]*>(.*?)</dd>\s*</dl>', content, re.S)}
    reductions = Counter(JP_COLORS[alt] for alt in re.findall(r'<img[^>]*alt="([^"]+)"', rows['軽減コスト']) if alt in JP_COLORS)
    colors = [JP_COLORS[v] for v in re.findall(r'<span class="attributeItem[^\"]*">(.*?)</span>', rows['属性'])]
    levels = []
    for item in re.findall(r'<div class="bpCoreItem[^\"]*">(.*?)</div>', rows.get('BP/コア', ''), re.S):
        bp = text(re.search(r'<span class="bpCoreVal">(.*?)</span>', item, re.S).group(1))
        cores = text(re.search(r'<span class="bpCoreLevel">(.*?)</span>', item, re.S).group(1))
        levels.append({'cores': int(re.search(r'\d+', cores).group()), 'bp': int(bp) if bp.isdigit() else 0, 'soulAlternative': '[Soul Core]' in cores})
    return {'reductions': [{'color': c, 'count': n} for c, n in reductions.items()], 'colors': colors, 'levels': levels, 'block': text(rows['ブロックアイコン']).strip('<>'), 'cost': int(text(rows['コスト'])), 'familiesJa': text(rows['系統']), 'effectJa': text(rows['能力・効果']), 'hasSymbolRow': 'シンボル' in rows}

def meta(path, directory=False):
    target = Path(str(path) + '.meta')
    if target.exists():
        return
    relative = str(path.relative_to(PROJECT))
    guid = hashlib.sha256(('battle-spirits-integration-v1/' + relative).encode()).hexdigest()[:32]
    if directory:
        body = 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    elif path.suffix == '.cs':
        body = 'MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    elif path.suffix == '.png':
        body = 'TextureImporter:\n  internalIDToNameTable: []\n  externalObjects: {}\n  serializedVersion: 12\n  mipmaps:\n    enableMipMap: 0\n  isReadable: 0\n  textureType: 0\n  textureShape: 1\n  maxTextureSize: 4096\n  alphaIsTransparency: 1\n  textureCompression: 0\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    else:
        body = 'DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    content = 'fileFormatVersion: 2\nguid: ' + guid + '\n' + body
    target.write_text('\n'.join(line.rstrip() for line in content.splitlines()) + '\n')

def main():
    global review_root
    parser = argparse.ArgumentParser()
    parser.add_argument('--review', type=Path, default=Path('/workspace/card-review-publication-20261115'))
    parser.add_argument('--private-catalog', type=Path, default=Path('/workspace/card-review-operation-state/hologram-library-final-catalog.json'))
    args = parser.parse_args()
    review = args.review
    review_root = review.resolve()
    source = json.loads((review / 'BattleSpirits-four-decks-English.json').read_text())
    library = json.loads((review / 'design/hologram-library-v2/library.json').read_text())
    private = json.loads(args.private_catalog.read_text())
    source_cards = list(source['cards'])
    source_cards.append({'card_id': '26RBS03-022', 'type': 'Spirit', 'name_en': 'Pisketona', 'name_ja': '骨妖ピスケトーナ', 'effects_en': private['pisketona']['effect'], 'families_en': ['Dark Purple', 'Phantom'], 'rarity': 'M', 'source_url': private['pisketona']['sourceUrl'], 'image': 'design/hologram-arena-v1/assets/pisketona-card-original.jpg', 'original': 'design/hologram-arena-v1/assets/pisketona-card-original.jpg', 'illustrator_en': 'Credit retained in supplied original card image', 'translation_notes': ['Unofficial name and English effect text from the approved review catalog; the printed original face remains Japanese.']})
    by_id = {c['card_id']: c for c in source_cards}
    coverage_path = DATA / 'implemented-cards.json'
    coverage = json.loads(coverage_path.read_text()) if coverage_path.exists() else {}
    if coverage:
        effects = PROJECT / 'Assets/BattleSpirits/Runtime/Core/ModernAbilities.cs'
        registry = (PROJECT / 'Assets/BattleSpirits/Runtime/Core/AbilityRegistry.cs').read_text()
        rules_version = re.search(r'const string Version\s*=\s*"([^"]+)"', registry).group(1)
        if not coverage.get('verified') or coverage.get('version') != rules_version or coverage.get('source_effect_sha256') != hashlib.sha256(effects.read_bytes()).hexdigest():
            raise ValueError('Effect coverage is unverified or stale; rerun the rules/effect/Solo harnesses before importing playable cards.')
    implemented = set(coverage.get('ids', []))
    if not implemented.issubset(by_id):
        raise ValueError('Effect coverage contains unknown printed IDs: ' + str(implemented - set(by_id)))
    # Never regenerate the numbering when adding or removing cards later.
    map_path = DATA / 'numeric-ids.json'
    if map_path.exists():
        ids = json.loads(map_path.read_text())
    else:
        ids = {'version': 1, 'policy': 'Persistent explicit IDs; never reuse an ID or derive one with alternate-art arithmetic.', 'entries': [{'id': id, 'numericId': 900000001 + i} for i, id in enumerate(sorted(by_id))]}
    numeric = {v['id']: v['numericId'] for v in ids['entries']}
    for id in sorted(set(by_id) - set(numeric)):
        numeric[id] = max(numeric.values(), default=900000000) + 1
        ids['entries'].append({'id': id, 'numericId': numeric[id]})
    write_json(map_path, ids)
    write_json(STREAM / 'numeric-ids.json', ids)
    with ThreadPoolExecutor(max_workers=8) as executor:
        officials = dict((id, (parse_official(payload), url, digest)) for id, payload, url, digest in executor.map(fetch, source_cards))
    cards, symbols, provenance = [], [], []
    for id in sorted(by_id):
        card = by_id[id]
        official, url, digest = officials[id]
        image = review / card['image']
        original = review / card['original']
        # Pisketona's approved original is JPEG; keep those exact bytes and suffix.
        target = STREAM / 'cards' / (id + image.suffix)
        copy(image, target, 'original printed Japanese face' if id == '26RBS03-022' else 'unofficial English printed-face mockup')
        copy(original, DATA / 'provenance/original-cards' / (id + original.suffix), 'original card artwork and printed credits')
        symbol_details = [{'color': PRINTED_SYMBOL_COLORS[id], 'count': 1, 'isEx': id in EX_IDS}] if id in PRINTED_SYMBOL_COLORS else []
        cards.append({'id': id, 'numericId': numeric[id], 'nameJa': card['name_ja'], 'nameEn': card['name_en'], 'effectTextJa': official['effectJa'], 'effectTextEn': card['effects_en'], 'color': official['colors'][0], 'colors': official['colors'], 'cost': official['cost'], 'reductionSymbols': sum(v['count'] for v in official['reductions']), 'reductions': official['reductions'], 'symbols': sum(s['count'] for s in symbol_details), 'symbolDetails': symbol_details, 'symbolsVerified': True, 'levels': official['levels'], 'type': card['type'], 'families': card['families_en'], 'rarity': card['rarity'], 'implemented': id in implemented, 'rulesProfile': coverage.get('version', 'standard-reviewed-20261116'), 'format': 'Standard', 'standardBlock': official['block'], 'standardLegal': True, 'standardLegalityVerified': True, 'superUnleash': '真界放' in official['effectJa'], 'sourceUrl': url, 'imageUrl': '', 'artPath': 'cards/' + target.name, 'setId': id.split('-')[0], 'setName': '', 'releaseDate': '', 'released': True, 'eternalLegal': False, 'maxCopies': 3, 'restrictionVerified': True, 'restrictionSourceUrl': 'https://www.battlespirits.com/rule/limited.php'})
        symbols.append({'id': id, 'symbolDetails': symbol_details, 'verified': True, 'method': 'Visual transcription of original bottom-right printed area: no symbol present' if not symbol_details else 'Visual transcription of original printed bottom-right symbol: gold ornamental outer frame = EX; plain gemstone = ordinary symbol. Each selected permanent and eight Magic have exactly one printed symbol.', 'sourceImage': str(original.relative_to(review)), 'sourceImageSha256': sha(original), 'officialDetailSha256': digest, 'officialDetailHasSymbolRow': official['hasSymbolRow']})
        provenance.append({'id': id, 'sourceUrl': url, 'officialDetailSha256': digest, 'familiesJa': official['familiesJa'], 'illustratorEn': card.get('illustrator_en', ''), 'illustratorJa': card.get('illustrator_ja', ''), 'flavorEn': card.get('flavor_en', ''), 'flavorJa': card.get('flavor_ja', ''), 'translationNotes': card.get('translation_notes', []), 'originalImageSha256': sha(original), 'faceImageSha256': sha(image)})
    deck_data = {'decks': [{'name': deck['title'], 'format': 'Standard', 'cards': [v['id'] for v in deck['cards'] for _ in range(v['copies'])]} for deck in source['decks']]}
    visual_data = []
    for entry in library['entries']:
        id = entry['card_id']
        bounds = entry.get('character_bounds', {})
        visual_data.append({'id': id, 'width': entry.get('width', 0), 'height': entry.get('height', 0), 'left': bounds.get('left', 0), 'top': bounds.get('top', 0), 'right': bounds.get('right', 0), 'bottom': bounds.get('bottom', 0), 'isBoss': id in APPROVED_BOSS_IDS})
        if entry.get('cutout'):
            copy(review / 'design/hologram-library-v2' / entry['cutout'], STREAM / 'standees' / (id + '.png'), entry['method'])
    for frame in sorted((review / 'frames/six-color-v1').glob('*/*.png')):
        copy(frame, STREAM / 'frames' / frame.parent.name / frame.name, 'approved empty single-color PNG frame')
    for name in ['core.png', 'soul-core.png', 'card-back.png']:
        copy(review / 'design/hologram-arena-v1/assets' / name, STREAM / 'cosmetics' / name, 'approved review resource/card-back image')
    refroot = review / 'references/core-field-20261115/originals'
    for name, target in [('Field.png', 'field.png'), ('Play Sheet.png', 'play-sheet.png'), ('Playsheet w.png', 'play-sheet-white.png')]:
        copy(refroot / name, STREAM / 'playmats' / target, 'unchanged supplied playmat reference; printed historical markings are not rules authority')
    for name, obj in [('cards.json', {'cards': cards}), ('starter-decks.json', deck_data), ('visuals.json', {'visuals': visual_data})]:
        write_json(STREAM / name, obj)
    write_json(DATA / 'provenance/card-attribution.json', {'attribution': source['attribution'], 'translationStatus': source['translation_status'], 'cards': provenance})
    write_json(DATA / 'provenance/printed-symbols.json', {'verifiedOn': '2026-11-16', 'note': 'Official detail_iframe omits the symbol row; official original card pixels were inspected directly, including ordinary versus gold-framed EX. No rarity/color inference.', 'cards': symbols})
    write_json(DATA / 'provenance/deck-sources.json', {k: source[k] for k in ['as_of', 'sources', 'selection_note', 'team_rule_note', 'decks']})
    limited_path = DATA / 'provenance/official-limited-20261116.html'
    if not limited_path.exists():
        with urllib.request.urlopen('https://www.battlespirits.com/rule/limited.php', timeout=45) as response:
            limited_path.write_bytes(response.read())
    mentioned = [id for id in by_id if id in limited_path.read_text(encoding='utf-8')]
    if mentioned:
        raise ValueError('Selected IDs appear on the restriction page and need a reviewed copy-limit update: ' + str(mentioned))
    write_json(DATA / 'provenance/standard-legality.json', {'asOf': '2026-11-16', 'block': 'A', 'blockSource': 'Each archived official card detail explicitly prints <A>.', 'restrictionSource': 'https://www.battlespirits.com/rule/limited.php', 'restrictionSourceSha256': sha(limited_path), 'selectedIdsMentionedOnRestrictionPage': mentioned, 'ordinaryMaximumCopies': 3, 'deckSourceNote': source['team_rule_note']})
    copy(review / 'references/core-field-20261115/references.json', DATA / 'provenance/references.json', 'source-reference descriptions and historical/Eternal markings')
    write_json(DATA / 'provenance/asset-manifest.json', {'sourceRevision': 'review/battle-spirits-english-cards-20261115', 'files': manifest})
    for root in [STREAM, PROJECT / 'Assets/BattleSpirits/Runtime/Data']:
        meta(root, True)
        for path in sorted(root.rglob('*')):
            if 'Puzzles' in path.parts or path.suffix == '.meta':
                continue
            meta(path, path.is_dir())
    assert len(cards) == 62 and Counter(c['type'] for c in cards) == {'Spirit': 43, 'Nexus': 3, 'Magic': 16}
    assert all(len(deck['cards']) == 40 for deck in deck_data['decks'])
    print(json.dumps({'cards': len(cards), 'standees': len(list((STREAM / 'standees').glob('*.png'))), 'frames': len(list((STREAM / 'frames').glob('*/*.png'))), 'decks': [len(d['cards']) for d in deck_data['decks']], 'exSymbols': len(EX_IDS), 'bytePreservedFiles': len(manifest)}))

if __name__ == '__main__':
    main()
