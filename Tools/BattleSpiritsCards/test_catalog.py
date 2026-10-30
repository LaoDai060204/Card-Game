#!/usr/bin/env python3
"""Offline parser regressions: official HTML fixtures and edge cases."""
from pathlib import Path
import importlib.util
import struct
import sys
import tempfile
import unittest

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("official_bs_catalog", HERE / "catalog.py")
catalog = importlib.util.module_from_spec(spec)
spec.loader.exec_module(catalog)


def product(code, title, year, month, day):
    return f'''<li class="productListItem"><a href="/product/{code}.php">
<span class="js_producttitle">[{code}]{title}</span>
<dd class="date">{year}年{month}月{day}日</dd></a></li>'''


class CatalogTests(unittest.TestCase):
    def test_official_vanilla_fixture(self):
        card = catalog.parse_detail((HERE / "Fixtures/vanilla-detail.html").read_text(), "BS01-001")
        self.assertEqual("ゴラドン", card["nameJa"])
        self.assertEqual("Spirit", card["type"])
        self.assertEqual(["Red"], card["colors"])
        self.assertEqual(0, card["cost"])
        self.assertEqual([], card["reductions"])
        self.assertEqual([{"cores": 1, "bp": 1000}, {"cores": 3, "bp": 3000}], card["levels"])
        self.assertEqual("-", card["effectTextJa"])
        self.assertEqual(["爬獣"], card["families"])
        self.assertEqual(catalog.HOST + "/images/cardlist/BS01-001.webp", card["imageUrl"])

    def test_official_current_ability_fixture(self):
        card = catalog.parse_detail((HERE / "Fixtures/bs76-x01-detail.html").read_text(), "BS76-X01")
        self.assertEqual("紅蓮の龍星神ジーク・メテオヴルム", card["nameJa"])
        self.assertEqual(6, card["cost"])
        self.assertEqual("X", card["rarity"])
        self.assertNotEqual("-", card["effectTextJa"])
        self.assertTrue(card["levels"])
        self.assertNotIn("implemented", card)  # Parsing does not implement an ability.
        self.assertNotIn("symbols", card)  # No printed symbol count in this HTML.

    def test_discovery_excludes_upcoming_boosters(self):
        source = (product("BS76", "エターナルブースター 永皇の輝き", 2026, 5, 30)
                  + product("26RBS03", "絶界の覇者", 2026, 10, 17)
                  + product("BS77", "エターナルブースター 戦神の轟臨", 2026, 11, 21))
        options = '<option value="62298">[BS76]永皇の輝き</option><option value="62306">[26RBS03]絶界の覇者</option><option value="62307">[BS77]戦神の轟臨</option>'
        selected, announced = catalog.discover(source, options, "2026-10-30")
        self.assertEqual(["BS76", "26RBS03"], [p["setId"] for p in selected])
        self.assertEqual(["BS77"], [p["setId"] for p in announced])
        self.assertEqual("Eternal", selected[0]["format"])
        self.assertEqual("Standard", selected[1]["format"])
        self.assertFalse(announced[0]["released"])

    def test_discovery_fails_when_no_released_format_is_available(self):
        with self.assertRaises(ValueError):
            catalog.discover(product("BS77", "エターナルブースター", 2026, 11, 21),
                             '<option value="62307">[BS77]カード</option>', "2026-10-30")

    def test_list_deduplicates_alt_art_and_prefers_original_variant(self):
        source = '''<li class="cardCol"><a data-src="./detail_iframe.php?card_no=BS76-X01&amp;card_no2=BS76-X01_Secret"></a></li>
<li class="cardCol"><a data-src="./detail_iframe.php?card_no=BS76-X01&amp;card_no2=BS76-X01"></a></li>'''
        cards = catalog.list_cards(source)
        self.assertEqual(1, len(cards))
        self.assertEqual("BS76-X01", cards[0]["variant"])
        self.assertIn("card_no2=BS76-X01", cards[0]["url"])

    def test_list_rejects_path_like_card_ids(self):
        with self.assertRaises(ValueError):
            catalog.list_cards('<li class="cardCol"><a data-src="./detail_iframe.php?card_no=../escape&amp;card_no2=bad"></a></li>')

    def test_missing_detail_is_an_error_not_an_empty_card(self):
        with self.assertRaises(ValueError):
            catalog.parse_detail("<html>Not found</html>", "BS76-X01")

    def test_ruby_readings_do_not_change_printed_card_name(self):
        root = catalog.Document('<h2 class="cardName"><ruby>龍星神<rt>りゅうせいしん</rt></ruby>ジーク</h2>').root
        self.assertEqual("龍星神ジーク", root.first("h2").text())

    def test_line_breaks_in_effect_text_are_preserved(self):
        root = catalog.Document('<dd>【Lv1】<br>『このスピリットの召喚時』<br/>カードを1枚引く。</dd>').root
        self.assertEqual("【Lv1】\n『このスピリットの召喚時』\nカードを1枚引く。", root.first("dd").text(multiline=True))

    def test_limits_are_read_only_from_restriction_sections(self):
        source = '''<span class="cardNum">NAVIGATION-NOT-A-CARD</span>
<div class="featureBox js-target" data-tags="banned"><li class="cardCol"><span class="cardNum">BS72-019</span><span class="cardName">マッドフット</span></li></div>
<div class="featureBox js-target" data-tags="restricted-1"><li class="cardCol"><span class="cardNum">BS11-082</span><span class="cardName">ウィッグバインド</span></li></div>
<div class="featureBox js-target" data-tags="restricted-20"><li class="cardCol"><span class="cardNum">P16-26</span><span class="cardName">BooBooマン</span></li></div>'''
        result = catalog.parse_limits(source)
        self.assertEqual([0, 1, 20], [r["maxCopies"] for r in result])
        self.assertEqual(["BS72-019", "BS11-082", "P16-26"], [r["id"] for r in result])

    def test_empty_restriction_page_is_rejected(self):
        with self.assertRaises(ValueError):
            catalog.parse_limits("<html>Server unavailable</html>")

    def test_fetcher_rejects_untrusted_hosts_without_network(self):
        with tempfile.TemporaryDirectory() as folder:
            fetcher = catalog.Fetcher(folder)
            for url in ("http://www.battlespirits.com/test", "https://example.com/test", "https://www.battlespirits.com.evil.test/test"):
                with self.assertRaises(ValueError):
                    fetcher.get(url)

    def test_png_header_and_dimensions(self):
        data = b"\x89PNG\r\n\x1a\n" + struct.pack(">I", 13) + b"IHDR" + struct.pack(">II", 675, 983)
        self.assertEqual((675, 983), catalog.png_info(data))
        for bad in (b"HTML error", b"\x89PNG\r\n\x1a\n"):
            with self.assertRaises(ValueError):
                catalog.png_info(bad)


if __name__ == "__main__":
    unittest.main(verbosity=2)
