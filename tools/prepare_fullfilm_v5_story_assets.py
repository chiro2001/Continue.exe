#!/usr/bin/env python3
"""Prepare the V5 story strips and derivatives of the approved redacted plates."""
import hashlib
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageOps
import draw_episode123_v5_identity as identity

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'art/export/fullfilm-v5/story'
TMP = Path.home() / 'tmp/vrc-old-web-map/fullfilm-v5/design'
SAFE = Path.home() / 'tmp/vrc-old-web-map/vrc-model-hack/redacted'

# A cue begins with its first token. No prefilled or repeated ring is generated.
# 'fall' selects word groups, rather than turning the complete sentence grey.
CUES = [
    ('idle', -1, 0, ['一首旧曲，一起听。'], []),
    ('welcome', 0, 10.24, ['欢迎来到我的主页。', '请稍候，正在载入……'], []),
    ('tools', 10.24, 20.48, ['欢迎使用。', '解压后即可运行。', 'README.TXT'], []),
    ('homepage', 20.48, 30.72, ['欢迎交换链接。', '欢迎留言。', '本站更新中。'], []),
    ('translation', 30.72, 40.96, ['翻译', '校对', '清字', '嵌字'], []),
    ('napster', 40.96, 43.52, ['分享一首歌。', '正在连接……'], []),
    ('sharing', 43.52, 48.64, ['下载中……', '校验完成。', '继续做种。'], []),
    ('bt-night', 48.64, 51.2, ['晚安，BT。'], [0]),
    ('forum', 51.2, 61.44, ['欢迎回来。', '最后回复：刚刚。', '谢谢楼主。'], []),
    ('subtitles', 61.44, 71.68, ['时间轴已对齐。', '字幕加载完成。'], []),
    ('choice', 71.68, 74.24, ['请选择其一。', '2010'], [0]),
    ('blackout', 74.24, 77.44, ['Wikipedia', 'Blackout / 2012'], [1]),
    ('vrc-year', 77.44, 78.08, ['2014'], []),
    ('subtitle-close', 78.08, 79.36, ['链接失效。'], [0]),
    ('vrc-join', 79.36, 86.4, ['Joining world…', '欢迎来到这里。'], []),
    ('vrc-return', 86.4, 102.4, ['欢迎回来。', '从这里，去下一个世界。'], []),
    ('conditions-a', 102.4, 104.96, ['正在连接……'], []),
    ('security', 104.96, 110.08, ['Easy Anti-Cheat', '安全更新'], []),
    ('startup', 110.08, 112.64, ['启动中……'], []),
    ('conditions-b', 112.64, 120.32, ['Launch Error', '再试一次。'], [0]),
    ('permission', 120.32, 133.12, ['Private', '使用许可', '保留来源'], [0]),
    ('overload-a', 133.12, 143.36, ['新消息', '正在接收……', '窗口未响应。'], [2]),
    ('overload-b', 143.36, 153.6, ['模型来源', 'Private', '上传'], [0, 1]),
    ('overload-c', 153.6, 163.84, ['使用许可', '复制', '转卖'], [0, 1]),
    ('overload-d', 163.84, 174.08, ['模型不错，', '我扫走了哈'], [1]),
    ('overload-e', 174.08, 186.88, ['正在连接……', '请求超时。'], [0, 1]),
    ('quiet', 186.88, 197.12, [''], []),
    ('try', 197.12, 202.24, ['新建工程。', '我也试试。'], []),
    ('animate', 202.24, 207.36, ['正在渲染。', '再试一帧。'], []),
    ('edit', 207.36, 212.48, ['保存场景。', '还差一点。'], []),
    ('rewrite', 212.48, 217.6, ['再调一遍。'], []),
    ('play', 217.6, 235.52, ['进入 Play Mode。', '世界，运行起来。'], []),
    ('together', 235.52, 250.88, ['一起补完。', '创作仍将继续。'], []),
    ('land', 250.88, 253.44, ['还可以写点什么。'], []),
]

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def ribbons():
    face = identity.Face(80)
    rows, previews = [], []
    for index, (key, start, end, words, falling) in enumerate(CUES):
        tokens = [{'text': t} for t in words]
        glyphs, length = identity.layout_tokens(face, tokens, 20, trailing_separator=False)
        length = max(64, math.ceil(length + 20))
        if length > 4064:
            raise ValueError(key + ' exceeds the strip width')
        cover, ids, style, _ = identity.render_strip(face, glyphs, 4096, 128, 92)
        # A is word identity and fall selection, not opacity. It is constant
        # across a complete word, including its empty background pixels.
        for ti, token in enumerate(tokens):
            a, b = int(token['start']), min(4096, math.ceil(token['end']))
            style[:, a:b] = (128 if ti in falling else 0) + ti + 1
        path = OUT / f'ribbon-{index:02d}-{key}.png'
        Image.fromarray(np.stack([cover, identity.outline_plane(cover), ids, style], -1)).save(path)
        duration = max(1, end - start)
        speed = (length + 280) / duration if start >= 0 else 0
        rows.append(dict(index=index, id=key, text='　✦　'.join(words), start=start, end=end,
                         tokens=tokens, falling_tokens=falling, speed_px_s=round(speed, 5),
                         pixels=length, file=str(path.relative_to(ROOT)), sha256=sha(path),
                         font=face.font.getname()[0], repeat=False))
        thumb = Image.new('RGB', (1700, 104), '#152130')
        mask = Image.fromarray(cover[:, :length]); mask.thumbnail((1360, 96))
        thumb.paste('#bbe8ff', (8, 4, 8+mask.width, 4+mask.height), mask)
        ImageDraw.Draw(thumb).text((1400, 10), f'{start:.2f}  {key}', fill='white')
        previews.append(thumb)
    sheet = Image.new('RGB', (1700, 104*len(previews)))
    for i, im in enumerate(previews): sheet.paste(im, (0, i*104))
    sheet.save(TMP/'ribbon-score.jpg', quality=94)
    return rows

def safe_plates():
    plates = []
    for index in range(1, 15):
        path = next(SAFE.glob(f'{index:02d}_*.png'))
        if index == 14:
            path = SAFE.parent/'stamp-redraw/扫走了_完整邮戳.png'
        im = Image.open(path).convert('RGBA')
        im.thumbnail((2048, 2048), Image.Resampling.LANCZOS)
        dst = OUT/f'provenance-{index:02d}.png'
        im.save(dst)
        plates.append(dict(index=index, file=str(dst.relative_to(ROOT)), source=str(path),
                           sha256=sha(dst), source_sha256=sha(path), width=im.width,
                           height=im.height, aspect=im.width/im.height,
                           policy='approved baked redaction; derivatives only; no source identifiers in captions'))
    # Separate close views are always crops of the already-redacted image.
    crops = [(1,(0,.47,1,.91)), (2,(0,.34,1,.86)), (3,(0,.14,1,.76)),
             (4,(0,.28,1,.87)), (5,(0,0,1,1)), (6,(0,.2,1,.84)),
             (7,(0,.23,1,.93)), (8,(0,0,1,1)), (9,(0,0,.61,.6)),
             (10,(0,.62,1,.94)), (11,(0,.52,1,.85)), (12,(0,.1,.67,.8)),
             (13,(0,0,1,1)), (14,(0,0,1,1))]
    for index, box in crops:
        im = Image.open(OUT/f'provenance-{index:02d}.png').convert('RGBA')
        crop = im.crop(tuple(round(v*(im.width if j%2==0 else im.height)) for j,v in enumerate(box)))
        dst = OUT/f'provenance-{index:02d}-detail.png';crop.save(dst)
        plates[index-1].update(detail_file=str(dst.relative_to(ROOT)), detail_aspect=crop.width/crop.height, detail_sha256=sha(dst), normalized_crop=list(box))
    im=Image.open(OUT/'provenance-10.png').convert('RGBA')
    hero=im.crop((0,round(im.height*.11),im.width,round(im.height*.33)))
    dst=OUT/'provenance-10-hero.png';hero.save(dst)
    plates[9].update(hero_file=str(dst.relative_to(ROOT)),hero_aspect=hero.width/hero.height,hero_sha256=sha(dst))
    return plates

def main():
    OUT.mkdir(parents=True,exist_ok=True);TMP.mkdir(parents=True,exist_ok=True)
    result=dict(schema='fullfilm-v5-story.1',ribbons=ribbons(),provenance=safe_plates(),
                end_phrase='创作仍将继续',main_ripping_span=[133.12,186.88],
                ribbon_method='episode123-v5 cumulative flow; right upper entry; detached glyphs; position-triggered flat grey hold before falling')
    (ROOT/'data/fullfilm-v5-story.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({'ribbon_cues':len(result['ribbons']),'approved_plates':len(result['provenance']),'output':str(OUT)},ensure_ascii=False))

if __name__=='__main__':main()
