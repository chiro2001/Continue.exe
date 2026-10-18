#!/usr/bin/env python3
"""Author V4 SC glyph strips, borderless archive tiles and related-focus score.

Only new source files are written. Unity imports and serializes its own assets.
"""
import hashlib
import json
import math
import re
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageOps
from fontTools.ttLib import TTCollection
import draw_episode123_v5_identity as identity

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'art/export/fullfilm-v4'
TMP = Path.home() / 'tmp/vrc-old-web-map/fullfilm-v4/design'

RIBBONS = [
    ('idle', '一首旧曲，一起听。', -1, 0, 0),
    ('tools', '写 README 的人　✦　打包绿色版的人', 0, 30.72, 100),
    ('manga', '翻译、校对的人　✦　清字、嵌字的人', 30.72, 40.96, 120),
    ('bt', '做种的人　✦　留种的人', 40.96, 51.2, 115),
    ('forum', '回帖的人　✦　补档的人', 51.2, 61.44, 125),
    ('subtitles', '打时间轴的人　✦　写特效字幕的人', 61.44, 71.68, 135),
    ('making', '第一次上传的人　✦　写教程的人', 71.68, 77.44, 145),
    ('vrc', '第一次来的人　✦　带路的人　✦　搭起世界的人', 77.44, 102.4, 150),
    ('conditions', '需要登录　　无法访问　　链接失效', 102.4, 133.12, 70),
    ('overload', '转发　再转发　原帖呢　转发　再转发　原帖呢', 133.12, 186.88, 270),
    ('silence', '', 186.88, 197.12, 0),
    ('try', '我也试试　✦　一起补完　✦　接着做', 197.12, 235.52, 110),
    ('climax', '一起补完　✦　接着做', 235.52, 250.88, 200),
    ('landing', '还可以写点什么。', 250.88, 253.44, 0),
]


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def ribbons():
    face = identity.Face(80)
    assert ' SC' in ' '.join(face.font.getname())
    rows = []
    thumbs = []
    for i, (key, value, start, end, speed) in enumerate(RIBBONS):
        tokens = [{'text': value}]
        glyphs, length = identity.layout_tokens(face, tokens, 20, trailing_separator=False)
        length = max(64, math.ceil(length + 20))
        if length > 4064:
            raise ValueError(key + ' strip exceeds safe texture width')
        cover, ids, style, _ = identity.render_strip(face, glyphs, 4096, 128, 92)
        if key == 'conditions':
            style[:, :length] = 255
        pixels = np.stack([cover, identity.outline_plane(cover), ids, style], -1)
        path = OUT / f'ribbon-{i:02d}-{key}.png'
        Image.fromarray(pixels).save(path)
        rows.append(dict(id=key, text=value, start=start, end=end, speed_px_s=speed,
                         index=i, pixels=length, file=str(path.relative_to(ROOT)), sha256=sha(path),
                         font=face.font.getname()[0], font_collection_index=identity.sc_face_index()))
        # R/G/B/A are packed glyph metadata, so a flattened review image is separate.
        preview = Image.new('RGB', (1700, 112), '#07101c')
        mask = Image.fromarray(cover[:, :length]).resize((min(1550, length), 100))
        col = Image.new('RGB', mask.size, '#a9ddeb')
        preview.paste(col, (10, 4), mask)
        ImageDraw.Draw(preview).text((1570, 10), key, fill='white')
        thumbs.append(preview)
    sheet = Image.new('RGB', (1700, 112 * len(thumbs)), '#07101c')
    for i, thumb in enumerate(thumbs):
        sheet.paste(thumb, (0, i * 112))
    sheet.save(TMP / 'ribbon-wording.jpg', quality=94)
    return rows


def fonts():
    collection = TTCollection('/usr/share/fonts/noto-cjk/NotoSansCJK-Regular.ttc', lazy=False)
    sc = collection.fonts[2]
    assert sc['name'].getDebugName(1) == 'Noto Sans CJK SC'
    path = OUT / 'NotoSansCJKsc-Regular.otf'
    sc.save(path)
    return dict(file=str(path.relative_to(ROOT)), face=sc['name'].getDebugName(1),
                ttc_index=2, sha256=sha(path), license='SIL Open Font License 1.1')


def archive():
    sources = json.loads((ROOT / 'data/fullfilm-v2-atlas.json').read_text())['sources']
    im = Image.new('RGB', (4096, 2048))
    for item in sources:
        src = Image.open(ROOT / item['export']).convert('RGB')
        # contain+black padding was the V3 frame defect. Cover keeps pixels square;
        # full source plates remain uncropped for the readable foreground.
        tile = ImageOps.fit(src, (256, 256), Image.Resampling.LANCZOS, centering=(.35,.22))
        i = item['index']
        im.paste(tile, (i % 16 * 256, i // 16 * 256))
    path = OUT / 'archive-cover-atlas.png'
    im.save(path)
    return dict(file=str(path.relative_to(ROOT)), source_count=len(sources),
                unique_new_sources=0, operation='aspect-preserving square cover, no added padding or frame', sha256=sha(path))


def focus():
    # Every pool describes related but distinct documents, never enlarged copies.
    # Period sections are ordered; overload is explicitly a memory collage.
    pools = [
        (0, 10.24, [76, 86, 94, 100, 106, 112, 75, 85], 'desktop'),
        (10.24, 20.48, [8, 42, 93, 13, 16, 43, 44], 'tools'),
        (20.48, 30.72, [25, 38, 26, 39, 24, 10], 'homepage-flash'),
        (30.72, 40.96, [51, 41, 74, 77, 9, 58, 68], 'translation'),
        (40.96, 51.2, [34, 11, 33, 82, 72, 59, 60, 61, 56], 'sharing'),
        (51.2, 61.44, [2, 3, 28, 48, 80, 70, 89, 97, 103], 'bbs-sns'),
        (61.44, 71.68, [9, 68, 77, 74, 99, 105, 111, 117], 'fansub'),
        (71.68, 77.44, [13, 16, 41, 46, 51], 'making'),
        (102.4, 120.32, [73, 83, 66, 57, 56, 35, 9], 'conditions'),
        (133.12, 168.96, list(range(52)) + [56, 57, 66, 73, 80, 83, 89, 97, 103], 'memory-overload'),
    ]
    sources = json.loads((ROOT / 'data/fullfilm-v2-atlas.json').read_text())['sources']
    groups = []
    for start, end, indices, key in pools:
        groups.append(dict(start=start, end=end, id=key, indices=indices,
                           note='Dates refer to original sources; montage is a memory, not a claim that all material is contemporaneous.'))
    return dict(schema='fullfilm-v4-related-focus.1',
                layout='one held primary + independently changing upper/lower/side related sources; min .32s flashes and 1.28s reading holds',
                groups=groups, sources=[dict(index=s['index'], id=s['id'], unity_path='Assets/OldWebMap/FullFilm/Revision4/Textures/'+Path(s['export']).name, aspect=s['display_aspect'], label=source_label(s)) for s in sources])


def source_label(s):
    # A live-page capture date (2026) must not replace a 2008 post timestamp.
    depicted=str(s.get('depicted_year') or s.get('source_date') or '')
    year=re.search(r'\b(?:19|20)\d{2}\b',depicted)
    host=s['display_label'].split('  /  ')[0]
    return host+('  /  '+year.group() if year else '')


def native_sc():
    source = (ROOT / 'tools/prepare_fullfilm_v2_native_ui.py').read_text()
    source = source.replace('fullfilm-v2-ui', 'fullfilm-v4-ui').replace('fullfilm-v2-native-ui', 'fullfilm-v4-native-ui')
    source = source.replace('ImageFont.truetype(MONO if mono else FONT,n)',
                            'ImageFont.truetype(MONO if mono else FONT,n,index=0 if mono else 2)')
    source = source.replace('ImageFont.truetype(FONT,n)', 'ImageFont.truetype(FONT,n,index=2)')
    exec(compile(source, str(ROOT / 'tools/prepare_fullfilm_v2_native_ui.py'), 'exec'),
         {'__file__': str(ROOT / 'tools/prepare_fullfilm_v2_native_ui.py'), '__name__': 'v4_native_sc'})
    manifest = json.loads((ROOT / 'data/fullfilm-v4-native-ui.json').read_text())
    overlay = ROOT / 'art/export/fullfilm-v4-ui/native-overlays'
    for group in manifest['groups']:
        for state in group['states']:
            i = state['index']
            Image.open(overlay / state['full']).save(OUT / f'native-{group["id"]}-{i:02d}-full.png')
            layer = next(x for x in state['layers'] if x['id'] == 'secondary')
            Image.open(overlay / layer['crop']).save(OUT / f'native-{group["id"]}-{i:02d}-secondary.png')


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    TMP.mkdir(parents=True, exist_ok=True)
    result = dict(schema='fullfilm-v4-design.1', ribbons=ribbons(), font=fonts(), archive=archive())
    (ROOT / 'data/fullfilm-v4-design.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    (ROOT / 'data/fullfilm-v4-related-focus.json').write_text(json.dumps(focus(), ensure_ascii=False, indent=2) + '\n')
    native_sc()
    print(json.dumps(dict(success=True, ribbons=len(result['ribbons']), font=result['font']['face'], archive_sources=result['archive']['source_count']), ensure_ascii=False))


if __name__ == '__main__':
    main()
