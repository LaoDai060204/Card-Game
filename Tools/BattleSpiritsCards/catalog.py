#!/usr/bin/env python3
"""Import, check and validate a pinned official Japanese Battle Spirits catalog.

Only the bundled, verified vanilla starter IDs can remain implemented. Importing
an official card's text never installs an implementation of its ability.
"""
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
from datetime import date, datetime, timezone, timedelta
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import parse_qs, urljoin, urlparse
import argparse
import hashlib
import json
import os
import re
import shutil
import struct
import subprocess
import tempfile
import time
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
HOST = "https://www.battlespirits.com"
COLORS = {"赤": "Red", "紫": "Purple", "緑": "Green", "白": "White", "黄": "Yellow", "青": "Blue", "神": "God", "究極": "Ultimate"}
COLOR_NUMBERS = {name: number for number, name in enumerate(("Red", "Purple", "Green", "White", "Yellow", "Blue"))}
TYPES = {"スピリット": "Spirit", "契約スピリット": "Contract Spirit", "アルティメット": "Ultimate", "契約アルティメット": "Contract Ultimate", "ブレイヴ": "Brave", "ネクサス": "Nexus", "契約ネクサス": "Contract Nexus", "マジック": "Magic"}
ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_-]{1,70}$")


class Node:
    def __init__(self, tag="", attributes=()):
        self.tag, self.attrs, self.children = tag, dict(attributes), []

    def has(self, css_class):
        return css_class in self.attrs.get("class", "").split()

    def all(self, tag=None, css_class=None):
        found = []
        for child in self.children:
            if isinstance(child, Node):
                if (tag is None or child.tag == tag) and (css_class is None or child.has(css_class)):
                    found.append(child)
                found.extend(child.all(tag, css_class))
        return found

    def first(self, tag=None, css_class=None):
        return next(iter(self.all(tag, css_class)), None)

    def text(self, multiline=False):
        def walk(node):
            if isinstance(node, str):
                return node
            if node.tag in ("rt", "rp", "script", "style"):
                return ""
            if node.tag == "br":
                return "\n" if multiline else " "
            return "".join(walk(c) for c in node.children)
        raw = walk(self)
        if multiline:
            return "\n".join(re.sub(r"[\t ]+", " ", line).strip() for line in raw.splitlines() if line.strip())
        return re.sub(r"\s+", " ", raw).strip()


class Document(HTMLParser):
    VOID = {"area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr"}

    def __init__(self, source):
        super().__init__(convert_charrefs=True)
        self.root, self.stack = Node(), []
        self.stack.append(self.root)
        self.feed(source)

    def handle_starttag(self, tag, attributes):
        node = Node(tag, attributes)
        self.stack[-1].children.append(node)
        if tag not in self.VOID:
            self.stack.append(node)

    def handle_startendtag(self, tag, attributes):
        self.stack[-1].children.append(Node(tag, attributes))

    def handle_endtag(self, tag):
        for index in range(len(self.stack) - 1, 0, -1):
            if self.stack[index].tag == tag:
                del self.stack[index:]
                return

    def handle_data(self, text):
        self.stack[-1].children.append(text)


def digest(data):
    return hashlib.sha256(data).hexdigest()


class Fetcher:
    def __init__(self, cache, refresh=False):
        self.cache, self.refresh = Path(cache), refresh
        self.cache.mkdir(parents=True, exist_ok=True)

    def get(self, url):
        parsed = urlparse(url)
        if parsed.scheme != "https" or parsed.hostname != "www.battlespirits.com":
            raise ValueError("Unexpected official source: " + url)
        path = self.cache / digest(url.encode())
        if path.exists() and not self.refresh:
            return path.read_bytes()
        last = None
        for attempt in range(3):
            try:
                request = urllib.request.Request(url, headers={"User-Agent": "LaoDai-BattleSpirits-Catalog/1.0"})
                with urllib.request.urlopen(request, timeout=30) as response:
                    if urlparse(response.geturl()).hostname != parsed.hostname:
                        raise ValueError("Unexpected redirect from official source")
                    content = response.read(6_000_001)
                if len(content) > 6_000_000:
                    raise ValueError("Official response exceeded the per-file limit")
                temporary = path.with_suffix(".part")
                temporary.write_bytes(content)
                temporary.replace(path)
                return content
            except Exception as error:
                last = error
                time.sleep(0.25 * (attempt + 1))
        raise RuntimeError(f"Could not read {url}: {last}")


def discover(products_source, list_source, as_of):
    ids = {}
    for option in Document(list_source).root.all("option"):
        match = re.match(r"\[([^]]+)\]", option.text())
        if match and option.attrs.get("value", "").isdigit():
            ids[match[1]] = option.attrs["value"]
    products = []
    for item in Document(products_source).root.all("li", "productListItem"):
        title = item.first("span", "js_producttitle")
        released = item.first("dd", "date")
        link = item.first("a")
        if title is None or released is None or link is None:
            continue
        code = re.match(r"\[([^]]+)\]", title.text())
        when = re.search(r"(\d{4})年(\d+)月(\d+)日", released.text())
        if not code or not when:
            continue
        release_date = date(*(int(p) for p in when.groups())).isoformat()
        name = title.text()
        format_name = "Eternal" if "エターナル" in name else "Standard" if "スタンダード" in item.text() or code[1].startswith("26R") else "Unspecified"
        products.append({"setId": code[1], "setName": name, "releaseDate": release_date,
                         "format": format_name, "released": release_date <= as_of,
                         "productUrl": urljoin(HOST, link.attrs.get("href", "/product/")), "productId": ids.get(code[1], "")})
    selected = []
    for format_name in ("Eternal", "Standard"):
        candidates = [p for p in products if p["released"] and p["productId"] and p["format"] == format_name
                      and re.fullmatch(r"BS\d+|\d\dRBS\d+", p["setId"])]
        if not candidates:
            raise ValueError("No released official booster found for " + format_name)
        selected.append(max(candidates, key=lambda p: p["releaseDate"]))
    return selected, [p for p in products if not p["released"] and re.fullmatch(r"BS\d+|\d\dRBS\d+", p["setId"])]


def list_cards(source):
    result = {}
    for item in Document(source).root.all("li", "cardCol"):
        link = next((a for a in item.all("a") if "detail_iframe.php" in a.attrs.get("data-src", "")), None)
        if link is None:
            continue
        url = urljoin(HOST + "/cardlist/", link.attrs["data-src"])
        args = parse_qs(urlparse(url).query)
        card_id, variant = args["card_no"][0], args["card_no2"][0]
        if not ID.fullmatch(card_id) or not ID.fullmatch(variant):
            raise ValueError("Unexpected official card ID")
        candidate = {"id": card_id, "variant": variant, "url": url}
        if card_id not in result or variant == card_id:
            result[card_id] = candidate
    if not result:
        raise ValueError("The official set page had no card entries")
    return list(result.values())


def parse_detail(source, card_id):
    sections = Document(source).root.all("section", "detailCol")
    if not sections:
        raise ValueError("Missing official card detail section: " + card_id)
    faces = []
    for section in sections:
        name = section.first("h2", "cardName")
        if name is None:
            continue
        blocks = {}
        for block in section.all("dl", "detailColList"):
            term, description = block.first("dt"), block.first("dd")
            if term is not None and description is not None:
                blocks[term.text()] = description
        def text(key):
            return blocks[key].text(multiline=True) if key in blocks else ""
        cost_node = section.first("span", "textCost")
        cost_text = cost_node.text() if cost_node is not None else ""
        attributes = section.all("span", "attributeItem")
        colors = [COLORS.get(a.text(), a.text()) for a in attributes]
        if not colors:
            colors = [COLORS.get(x, x) for x in re.findall(r"[赤紫緑白黄青神究極]", text("属性"))]
        reductions = Counter()
        if "軽減コスト" in blocks:
            for image in blocks["軽減コスト"].all("img"):
                label = image.attrs.get("alt", "")
                if label in COLORS:
                    reductions[COLORS[label]] += 1
        levels, raw_levels = [], []
        for level in section.all("div", "bpCoreItem"):
            bp, cores = level.first("span", "bpCoreVal"), level.first("span", "bpCoreLevel")
            if bp is None or cores is None:
                continue
            raw_levels.append({"bp": bp.text(), "cores": cores.text()})
            if bp.text().isdigit() and cores.text().isdigit():
                levels.append({"cores": int(cores.text()), "bp": int(bp.text())})
        rarity = section.first("span", "cardRarity")
        image = section.first("div", "cardImg")
        image = image.first("img") if image is not None else None
        category = text("カテゴリー")
        faces.append({"nameJa": name.text(), "type": TYPES.get(category, category), "typeJa": category,
                      "colors": colors, "color": next((COLOR_NUMBERS[c] for c in colors if c in COLOR_NUMBERS), -1),
                      "cost": int(cost_text) if cost_text.isdigit() else -1, "costText": cost_text,
                      "reductions": [{"color": c, "count": n} for c, n in reductions.items()],
                      "reductionSymbols": sum(reductions.values()), "levels": levels, "levelsRaw": raw_levels,
                      "families": [x.strip() for x in re.split(r"[・/／]", text("系統")) if x.strip() and x.strip() != "-"],
                      "effectTextJa": text("能力・効果"), "rarity": rarity.text() if rarity else "",
                      "imageUrl": urljoin(HOST, image.attrs["src"]) if image else ""})
    if not faces or not faces[0]["nameJa"] or not faces[0]["imageUrl"]:
        raise ValueError("Incomplete official card detail: " + card_id)
    card = dict(faces[0])
    if len(faces) > 1:
        card["faces"] = faces
    return card


def parse_limits(source):
    root = Document(source).root
    restriction_counts = {"banned": 0, "restricted-1": 1, "restricted-2": 2, "restricted-20": 20}
    result = []
    for section in root.all("div", "js-target"):
        category = section.attrs.get("data-tags")
        if category not in restriction_counts:
            continue
        for item in section.all("li", "cardCol"):
            number, name = item.first("span", "cardNum"), item.first("span", "cardName")
            if number is not None:
                result.append({"id": number.text(), "nameJa": name.text() if name else "",
                               "maxCopies": restriction_counts[category]})
    if not result:
        raise ValueError("Official ban/restriction list unexpectedly empty")
    return result


def png_info(data):
    if len(data) < 24 or data[:8] != b"\x89PNG\r\n\x1a\n" or data[12:16] != b"IHDR":
        raise ValueError("Converted artwork is not PNG")
    return struct.unpack(">II", data[16:24])


def artwork(fetcher, card, source_entry, root, magick):
    original = fetcher.get(card["imageUrl"])
    if original[:4] != b"RIFF" or original[8:12] != b"WEBP":
        raise ValueError("Official artwork is not WebP: " + card["id"])
    target = root / card["artPath"]
    target.parent.mkdir(parents=True, exist_ok=True)
    environment = dict(os.environ, MAGICK_THREAD_LIMIT="1")
    with tempfile.TemporaryDirectory(prefix="bs-official-art-") as temp:
        source_path, png_path = Path(temp) / "source.webp", Path(temp) / "card.png"
        source_path.write_bytes(original)
        subprocess.run([magick, str(source_path), "-define", "png:exclude-chunk=date,time", "PNG32:" + str(png_path)],
                       check=True, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE, env=environment, timeout=30)
        png = png_path.read_bytes()
        before = subprocess.check_output([magick, str(source_path), "-depth", "8", "rgba:-"], env=environment, timeout=30)
        after = subprocess.check_output([magick, str(png_path), "-depth", "8", "rgba:-"], env=environment, timeout=30)
        if before != after:
            raise ValueError("Artwork pixel values changed during format conversion")
        width, height = png_info(png)
        target.write_bytes(png)
    source_entry.update({"officialImageUrl": card["imageUrl"], "originalWebpSha256": digest(original),
                         "convertedPngSha256": digest(png), "decodedRgbaSha256": digest(before),
                         "width": width, "height": height, "artPath": card["artPath"],
                         "conversion": "ImageMagick lossless PNG encoding; exact decoded RGBA equality checked; no resizing, cropping or artwork changes"})
    return len(original)


def import_catalog(args):
    root = Path(args.root).resolve()
    fetcher = Fetcher(args.cache, args.refresh)
    product_url, list_url, limits_url = HOST + "/product/", HOST + "/cardlist/", HOST + "/rule/limited.php"
    products_raw, list_raw, limits_raw = fetcher.get(product_url), fetcher.get(list_url), fetcher.get(limits_url)
    sets, announced = discover(products_raw.decode(), list_raw.decode(), args.as_of)
    limitations = parse_limits(limits_raw.decode())
    limits_document = Document(limits_raw.decode()).root
    heading = limits_document.first("h3", "prodLoadTit")
    effective = re.search(r"(\d{4})年(\d+)月(\d+)日", heading.text() if heading else "")
    if not effective:
        raise ValueError("Official restriction effective date could not be read")
    effective_date = date(*(int(x) for x in effective.groups())).isoformat()
    if effective_date > args.as_of:
        raise ValueError("Official restriction page describes a future list; review before importing")
    if any(n.attrs.get("data-tags") == "pair" and n.all("li", "cardCol") for n in limits_document.all("div", "js-target")):
        raise ValueError("Official forbidden-pair list changed; explicit pair handling must be reviewed")
    seeds = json.loads((root / "Data/BattleSpirits/starter-cards.json").read_text())["cards"]
    requests, set_sources = {}, []
    for product in sets:
        url = HOST + "/cardlist/index.php?search=true&prodid=" + product["productId"]
        raw = fetcher.get(url)
        entries = list_cards(raw.decode())
        set_sources.append(dict(product, cardListUrl=url, cardListSha256=digest(raw), uniqueCards=len(entries)))
        for entry in entries:
            requests[entry["id"]] = (entry, product, None)
    for seed in seeds:
        requests[seed["id"]] = ({"id": seed["id"], "variant": seed["id"], "url": seed["sourceUrl"]},
                                {"setId": seed["id"].split("-")[0], "setName": "Verified vanilla starter cards",
                                 "releaseDate": "", "format": "Eternal", "released": True}, seed)

    def detail(item):
        entry, product, seed = item
        raw = fetcher.get(entry["url"])
        card = parse_detail(raw.decode("utf-8"), entry["id"])
        card.update({"id": entry["id"], "nameVi": seed["nameVi"] if seed else "", "setId": product["setId"],
                     "setName": product["setName"], "releaseDate": product["releaseDate"], "format": product["format"],
                     "released": product["released"], "eternalLegal": product["format"] == "Eternal",
                     "legalityStatus": "format-confirmed" if product["format"] == "Eternal" else "not-verified",
                     "maxCopies": 3, "implemented": False, "symbols": 0, "symbolDetails": [], "symbolsVerified": False,
                     "sourceUrl": entry["url"], "sourceCardNumber": entry["variant"],
                     "artPath": "Picture/BattleSpirits/" + entry["id"] + ".png"})
        matches = [r["maxCopies"] for r in limitations if card["id"] == r["id"] or card["nameJa"] == r["nameJa"]]
        card["maxCopies"] = min(matches) if matches else 3
        card["restrictionSourceUrl"] = limits_url
        card["restrictionVerified"] = product["format"] == "Eternal"
        if seed is not None:
            for key in ("nameJa", "color", "cost", "reductionSymbols", "levels", "effectTextJa"):
                if card[key] != seed[key]:
                    raise ValueError(f"Starter {card['id']} changed in official {key}; review its implementation before importing")
            if card["type"] != "Spirit" or card["effectTextJa"] != "-":
                raise ValueError("Starter is no longer a verified vanilla Spirit")
            card.update({"implemented": True, "symbols": seed["symbols"], "symbolsVerified": True,
                         "symbolDetails": [{"color": card["colors"][0], "count": seed["symbols"]}]})
        provenance = {"id": card["id"], "officialDetailUrl": entry["url"], "detailHtmlSha256": digest(raw),
                      "sourceCardNumber": entry["variant"], "retrievedDate": args.as_of}
        return card, provenance

    with ThreadPoolExecutor(max_workers=args.workers) as pool:
        parsed = list(pool.map(detail, requests.values()))
    magick = shutil.which("magick") or shutil.which("convert")
    if not magick:
        raise ValueError("Install ImageMagick for lossless WebP-to-PNG import; validation/check commands do not require it")
    with ThreadPoolExecutor(max_workers=args.workers) as pool:
        original_sizes = list(pool.map(lambda pair: artwork(fetcher, pair[0], pair[1], root, magick), parsed))
    if sum(original_sizes) > 150_000_000:
        raise ValueError("Artwork import exceeded the documented 150 MB source limit")
    cards = sorted((card for card, _ in parsed), key=lambda card: (not card["implemented"], card["setId"], card["id"]))
    catalog = {"schemaVersion": 1, "asOfDate": args.as_of, "ruleset": "Japanese Battle Spirits Eternal",
               "latestReleasedSets": set_sources, "announcedFutureBoosters": announced, "cards": cards}
    manifest = {"asOfDate": args.as_of, "officialProductsUrl": product_url, "productsHtmlSha256": digest(products_raw),
                "officialCardListUrl": list_url, "cardListHtmlSha256": digest(list_raw), "sets": set_sources,
                "announcedFutureBoosters": announced, "artworkSourceBytes": sum(original_sizes),
                "symbolNote": "Official detail HTML does not expose printed symbol counts. Only the verified starter symbols are implemented; other symbol values are unknown, not inferred.",
                "formatNote": "Standard-labeled cards are catalog entries only. Their Eternal legality has not been verified; eternalLegal=false does not claim they are prohibited.",
                "implementationNote": "Importing card data or artwork does not implement an ability. Only verified vanilla starter IDs are enabled.",
                "attribution": "Battle Spirits card artwork © BANDAI / BNP and credited illustrators; original printed notices retained.",
                "license": "Official third-party card art; no open-source license or redistribution permission is inferred.",
                "cards": sorted((entry for _, entry in parsed), key=lambda entry: entry["id"])}
    limit_manifest = {"sourceUrl": limits_url, "sourceHtmlSha256": digest(limits_raw), "retrievedDate": args.as_of,
                      "effectiveDate": effective_date, "restrictions": limitations, "forbiddenPairs": [],
                      "scope": "Current official Japanese Eternal ban/restriction list; no forbidden pairs are currently listed."}
    directory = root / "Data/BattleSpirits"
    for filename, value in (("cards.json", catalog), ("card-sources.json", manifest), ("banlist.json", limit_manifest)):
        (directory / filename).write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return validate_catalog(root)


def validate_catalog(root):
    catalog = json.loads((root / "Data/BattleSpirits/cards.json").read_text())
    sources = json.loads((root / "Data/BattleSpirits/card-sources.json").read_text())
    seeds = {card["id"]: card for card in json.loads((root / "Data/BattleSpirits/starter-cards.json").read_text())["cards"]}
    provenance = {entry["id"]: entry for entry in sources["cards"]}
    cards = catalog["cards"]
    if len({c["id"] for c in cards}) != len(cards) or {c["id"] for c in cards} != set(provenance):
        raise ValueError("Catalog IDs and provenance entries differ")
    enabled = set()
    for card in cards:
        if not ID.fullmatch(card["id"]) or not card["sourceUrl"].startswith(HOST + "/cardlist/detail_iframe.php?"):
            raise ValueError("Invalid card identity/source")
        path = Path(card["artPath"])
        if path.parts != ("Picture", "BattleSpirits", card["id"] + ".png"):
            raise ValueError("Invalid artwork path")
        data = (root / path).read_bytes()
        if digest(data) != provenance[card["id"]]["convertedPngSha256"]:
            raise ValueError("Artwork checksum mismatch: " + card["id"])
        width, height = png_info(data)
        if width < 300 or height < 400:
            raise ValueError("Unexpected artwork dimensions")
        if card["implemented"]:
            enabled.add(card["id"])
            if card["id"] not in seeds or card["type"] != "Spirit" or card["effectTextJa"] != "-" or not card["symbolsVerified"]:
                raise ValueError("An unsupported ability was marked implemented")
        if card["format"] == "Standard" and (card["implemented"] or card["legalityStatus"] != "not-verified"):
            raise ValueError("Unverified Standard card was promoted into the Eternal implementation")
        if card["releaseDate"] and card["releaseDate"] > catalog["asOfDate"]:
            raise ValueError("Announced card incorrectly imported as released")
    if enabled != set(seeds):
        raise ValueError("Verified starter implementation set changed")
    result = {"status": "PASS", "asOfDate": catalog["asOfDate"], "cards": len(cards), "implementedVanillaSpirits": len(enabled),
              "browseOnlyCards": len(cards) - len(enabled), "artImages": len(provenance),
              "sets": [{"id": p["setId"], "released": p["releaseDate"], "format": p["format"], "cards": p["uniqueCards"]} for p in catalog["latestReleasedSets"]],
              "scope": "Official metadata, artwork checksums and supported-card flags; not Unity Play or full card-ability validation"}
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return result


def check_catalog(args):
    root, fetcher = Path(args.root), Fetcher(args.cache, True)
    catalog = json.loads((root / "Data/BattleSpirits/cards.json").read_text())
    selected, announced = discover(fetcher.get(HOST + "/product/").decode(), fetcher.get(HOST + "/cardlist/").decode(), args.as_of)
    old_sets = {s["setId"] for s in catalog["latestReleasedSets"]}
    result = {"asOfDate": args.as_of, "latestReleasedSets": selected, "newReleasedSets": [s for s in selected if s["setId"] not in old_sets],
              "announcedFutureBoosters": announced, "checkedCards": 0, "changedCards": [], "errors": []}
    cards = catalog["cards"] if not args.id else [c for c in catalog["cards"] if c["id"] in args.id]
    if args.id and set(args.id) - {c["id"] for c in cards}:
        raise ValueError("Unknown catalog card ID")
    def compare(card):
        try:
            observed = parse_detail(fetcher.get(card["sourceUrl"]).decode(), card["id"])
            changes = {key: {"bundled": card[key], "official": observed[key]} for key in ("nameJa", "type", "cost", "reductions", "levels", "effectTextJa") if observed[key] != card[key]}
            return {"id": card["id"], "changes": changes}, None
        except Exception as error:
            return None, {"id": card["id"], "error": str(error)}
    with ThreadPoolExecutor(max_workers=args.workers) as pool:
        for change, error in pool.map(compare, cards):
            result["checkedCards"] += 1
            if error:
                result["errors"].append(error)
            elif change["changes"]:
                result["changedCards"].append(change)
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 1 if result["newReleasedSets"] or result["changedCards"] or result["errors"] else 0


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("import", "check", "validate", "discover"))
    parser.add_argument("--root", default=str(ROOT))
    parser.add_argument("--as-of", default=datetime.now(timezone(timedelta(hours=9))).date().isoformat(), help="ISO Japanese calendar date to include released products")
    parser.add_argument("--cache", default=str(Path(tempfile.gettempdir()) / "bs-official-card-cache"))
    parser.add_argument("--refresh", action="store_true", help="Refresh cached official responses during import")
    parser.add_argument("--workers", type=int, default=3, choices=range(1, 5))
    parser.add_argument("--id", action="append", help="Restrict check to this card ID; repeatable")
    args = parser.parse_args()
    date.fromisoformat(args.as_of)
    if args.command == "validate":
        validate_catalog(Path(args.root))
    elif args.command == "import":
        import_catalog(args)
    elif args.command == "check":
        return check_catalog(args)
    else:
        fetcher = Fetcher(args.cache, args.refresh)
        selected, announced = discover(fetcher.get(HOST + "/product/").decode(), fetcher.get(HOST + "/cardlist/").decode(), args.as_of)
        print(json.dumps({"asOfDate": args.as_of, "latestReleasedSets": selected, "announcedFutureBoosters": announced}, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (ValueError, KeyError, OSError, RuntimeError, subprocess.SubprocessError) as error:
        raise SystemExit("FAIL: " + str(error))
