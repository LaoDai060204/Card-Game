# Nguồn gốc và thành phần bên thứ ba

**LaoDai Battle Spirits** là bản tùy biến và Việt hóa do **LaoDai060204** duy trì, phát triển từ [MDPro3 của Sherry Chaos](https://code.moenext.com/sherry_chaos/MDPro3) và các cộng tác viên. Mã nền được nhập tại commit `e177cd8d53092ae9249e37b38d3cef0040e41095`.

## Mã và dữ liệu nền

- [MDPro3](https://code.moenext.com/sherry_chaos/MDPro3) — Sherry Chaos và các cộng tác viên.
- [YGOPro](https://github.com/Fluorohydride/ygopro).
- [YGOProUnity_V2](https://github.com/lllyasviel/YGOProUnity_V2) và [bản MyCard](https://code.moenext.com/mycard/YGOProUnity_V2).
- [MDPro2](https://code.moenext.com/sherry_chaos/MDPro2).
- [YGOPro Database](https://code.moenext.com/mycard/ygopro-database).
- [YGOPro Server](https://github.com/mycard/ygopro/tree/server).
- [YGOSharp](https://github.com/IceYGO/ygosharp).
- [WindBot](https://github.com/IceYGO/windbot) và dữ liệu cấu hình từ [YGOMobile](https://github.com/fallenstardust/YGOMobile-cn-ko-en).
- [Yugioh HD Artworks](https://code.moenext.com/mycard/hd-arts), [YGOPro2-Closeup](https://code.moenext.com/mycard/ygopro2-closeup), [MDPro3 Asset Bundles](https://code.moenext.com/sherry_chaos/mdpro3-assetbundles), [MDPro3 Other](https://code.moenext.com/mycard/mdpro3-other) và các gói âm thanh phân phối qua MyCard.

Danh sách ghi công đầy đủ được giữ trong `Assets/Addressables/Text/AboutGame.txt`, `AboutGame_en.txt` và bản tiếng Việt `AboutGame_vi.txt`, bao gồm các nhóm dịch, người bảo trì dữ liệu, người hỗ trợ kỹ thuật, âm thanh và hoạt cảnh.

## Tài nguyên trò chơi

Bản template và lịch sử kho từng dùng tài nguyên từ [Yu-Gi-Oh! Master Duel](https://www.konami.com/yugioh/masterduel/), [Yu-Gi-Oh! Cross Duel](https://www.konami.com/yugioh/crossduel/) và [Yu-Gi-Oh! Duel Links](https://www.konami.com/yugioh/duel_links/). Nhánh Standard 0.3.0 đã bỏ đồ họa duel/UI/cut-in, giọng và dữ liệu trò chơi cũ, nhưng giữ **pet và wallpaper Master Duel**, icon/SE_MATE và shader chung cần cho chúng. Theo yêu cầu mới, 0.5.0 phục hồi 141 tài nguyên UI dùng chung theo `Documentation/original-ui-style-restoration.json`; không phục hồi engine đấu, cut-in quái hoặc dữ liệu nhân vật cũ. Tài nguyên giữ lại vẫn thuộc chủ sở hữu tương ứng; việc dọn tệp không xóa lịch sử/ghi công của mã dẫn xuất.

Thông báo gốc trong mục giới thiệu nêu rằng tài nguyên được cung cấp cho việc học tập cá nhân và chia sẻ cộng đồng, không dành cho mục đích thương mại. Bản tùy biến giữ thông báo này.

## Giấy phép và thông báo đi kèm

Các thành phần trong kho có giấy phép và thông báo riêng, được giữ cùng mã và tài nguyên. Một số vị trí có tệp giấy phép trong kho:

- `Tools/YGO Classes/ocgcore/LICENSE`
- `Tools/YGO Classes/event/LICENSE`
- `Tools/YGO Classes/irrlicht/source/Irrlicht/bzip2/LICENSE`
- `Tools/YGO Classes/irrlicht/source/Irrlicht/libpng/LICENSE`
- `Assets/Tools/com.esotericsoftware.spine.timeline-4.2/LICENSE.md`

Các thư viện, plugin, shader và font khác có thể kèm thông báo trong tệp nguồn hoặc thư mục tương ứng. Thông báo này ghi nhận các nguồn đã được dùng; nó không thay thế giấy phép của từng thành phần.
# Battle Spirits conversion

The `battle-spirits-mdpro3-ui` branch includes card metadata and artwork obtained
from the official Japanese Battle Spirits website at
https://www.battlespirits.com/cardlist/. Battle Spirits names, illustrations and
other rights belong to BANDAI/BNP and the rights holders identified on the cards.
These assets are not original artwork by LaoDai060204 and this project is not an
official BANDAI game.

`Data/BattleSpirits/card-sources.json` records individual source URLs, retrieval
dates, official WebP hashes and bundled PNG hashes. PNG conversion preserves the
decoded RGBA pixels; original card numbers, names, effect text and printed
credits are retained. `Data/BattleSpirits/banlist.json` records the source and
effective date of observed restrictions.

Version 0.3.0 retains the MDPro3 screen layouts and generic compatibility code,
but replaces Master Duel UI graphics with original procedural sprites. Version
0.5.0 later restores 141 reviewed shared GUI assets and their exact references
at the owner's request, alongside the retained pet/wallpaper assets, icons,
sound effects and shared shader bundle. The restored chrome remains third-party
template/game material; it is not newly authored art. Serialized
component names and namespaces needed by these bundles are not renamed merely
to conceal provenance. Their original notices continue to apply. Legacy
Yu-Gi-Oh card faces, duel graphics, voices and unused game databases removed from
this branch remain in main/history and the recorded backups.

The retained Source Han Sans font is an independent Adobe open-source typeface,
licensed under the [SIL Open Font License 1.1](Assets/Fonts/SourceHanSans-OFL.txt),
not original artwork by this project. Keep its copyright/license with redistributed
font files. Game-specific font/glyph assets that were removed do not transfer
their rights to the replacement UI.

## Original procedural visuals and audio in 0.2.0

`Assets/BattleSpirits/Runtime/Visuals` contains newly created field and creature
geometry plus animation curves. `Tools/BattleSpiritsAssets/render_originals.py`
and `Source/OriginalSpirits.blend` produce the new menu render and the two boss
closeups assigned to BS01-X01 and BS01-097. These are original fan-made low-poly
interpretations, not official BANDAI creature models, extracted commercial
models, screenshots, or edits of the official card-face illustrations.

`Tools/BattleSpiritsAssets/audio.py` synthesizes the new music and sound effects
without third-party recordings. The procedural visual/render and sound-bank
manifests identify the original contributions as CC0-1.0 and record file hashes:

- `Tools/BattleSpiritsAssets/visual-manifest.json`
- `Sound/BattleSpirits/manifest.json`

That identification applies only to the original contributions described by
those manifests. It does not change rights in Battle Spirits names, character
designs or official card artwork, Master Duel assets, the MDPro3 template,
WindBot, or any other third-party component listed above.

## Original UI skin in 0.3.0

The three panel/button/clear PNG sprites under `Assets/Texture/GUI_Used` were
created procedurally for this project. `Documentation/master-duel-art-replacement.json`
records their hashes, dimensions and CC0-1.0 identification. This applies only to
those new graphics, not to the retained template code, fonts, pet/wallpaper assets
or official Battle Spirits card faces. Cleanup manifests document which
resources were removed without changing ownership of retained third-party work.

## Battle Spirits interface sources in 0.5.0

`Data/BattleSpirits/skin-sources.json` records source URLs, hashes, original
dimensions and differences from the inline reference images supplied by the
owner. The six faceted card-color symbols and normal/Soul Core chips are original
47×47 files from the official Battle Spirits Standard manual. Playmat JPEGs were
extracted as original embedded images from the official PDFs, without redrawing
or resampling. They retain BANDAI/right-holder notices. Runtime uses the Standard
sheet's border/logo regions; the gray Eternal reference sheet is not an
authoritative Standard phase guide.

The card back is the public 350×490 copy in
[BattleSpiritsLackeyccg](https://github.com/guari1999/BattleSpiritsLackeyccg),
pinned to the commit in the manifest. It uses the same Battle Spirits design but
differs in resolution and bottom copyright marking from the inline 500×730
reference. This is a source mirror, not a claim of authorship or a new license
for BANDAI artwork.

The three `Chrome*` resource copies are byte-identical original template GUI
images, with source commit and hashes recorded. They are separate from the
procedural 0.3.0 sprites above and are not covered by that CC0 identification.
