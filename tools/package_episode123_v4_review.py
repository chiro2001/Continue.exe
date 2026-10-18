#!/usr/bin/env python3
"""Assemble the V4 review page from completed film, sources and real validation."""
from collections import Counter, defaultdict
import csv
import hashlib
import html
import json
from pathlib import Path
import shutil
import subprocess
from urllib.parse import urlparse
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
TMP = Path.home() / 'tmp/vrc-old-web-map/episode123-v4'
OUT = TMP / 'review'


def read(path):
    return json.loads(path.read_text())


def main():
    score = read(ROOT / 'data/episode123-v4-score.json')
    for name in ['interaction', 'live-validation', 'sdk-build', 'sdk-bundle-inspection', 'persistence', 'text-space', 'keyline-visibility', 'static-data-validation', 'stable-sound-saved-scene', 'stable-sound-runtime', 'anonymity-import', 'anonymity-sources']:
        report = read(TMP / 'reports' / (name + '.json'))
        if report.get('success') is not True:
            raise RuntimeError('Incomplete validation: ' + name)
    layout = read(TMP / 'montage/presentation-layout-validation.json')
    assert layout['focusChecks'] == 15 and layout['reverseSeekRepeatable'] is True
    assert layout['historyTextureBindingChecks'] == 192
    layout_build = read(TMP / 'montage/presentation-layout.json')
    movie = OUT / 'episode123-v4-review.mp4'
    probe = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_format', '-show_streams', '-of', 'json', str(movie)]))
    assert abs(float(probe['format']['duration']) - 135.04) < .1
    assert any(stream['codec_type'] == 'audio' for stream in probe['streams'])
    assert any(stream['codec_type'] == 'video' and stream['width'] == 1600 and stream['height'] == 900 for stream in probe['streams'])
    (OUT / 'ffprobe.json').write_text(json.dumps(probe, ensure_ascii=False, indent=2))
    for folder in ['stills', 'gallery', 'reports']:
        (OUT / folder).mkdir(exist_ok=True, parents=True)
    for report in (TMP / 'reports').glob('*.json'):
        if report.name.startswith('cli-') or 'failure' in report.name:
            continue
        shutil.copy2(report, OUT / 'reports' / report.name)
    for folder in ['montage', 'operations', 'hub-cast', 'wave-fx']:
        if (TMP / folder).exists():
            shutil.copytree(TMP / folder, OUT / 'reports' / folder, dirs_exist_ok=True,
                            ignore=shutil.ignore_patterns('idle-sole-fit.json'))
    frames = sorted((TMP / 'frames').glob('v4_*.png'))
    for frame in frames:
        shutil.copy2(frame, OUT / 'stills' / frame.name)
    if (TMP / 'runtime').exists():
        shutil.copytree(TMP / 'runtime', OUT / 'runtime', dirs_exist_ok=True)
    appearances = defaultdict(list)
    for event in score['events']:
        appearances[event['image']].append(dict(start=event['start'], end=event['end'], role=event['role']))
    catalogue = []
    for image in score['images']:
        path = Path(image.get('display_source_path', image['source_path']))
        thumb = Image.open(path).convert('RGB')
        thumb.thumbnail((384, 256))
        thumb.save(OUT / 'gallery' / (image['id'] + '.jpg'), quality=88)
        catalogue.append(dict(id=image['id'], title=image['title'], kind=image['kind'], category=image['category'],
                              source_url=image.get('source_url', image.get('source_page_url', '')), unity_asset_path=image.get('display_path', image['path']),
                              image_url=image.get('image_url', ''), source_path=str(path),
                              source_date=image.get('source_date', ''), display_year=image.get('display_year', ''),
                              chronology_note=image.get('chronology_note', ''), placement_mode=image['placement_mode'],
                              source_semantics=image.get('date_basis') or image.get('source_date_semantics') or image.get('real_context', ''),
                              sha256=image['sha256'], display_processing=image.get('display_processing', ''), display_sha256=image.get('display_sha256', ''), appearances=sorted(appearances[image['id']], key=lambda row: row['start'])))
    assert all(image['appearances'] for image in catalogue)
    (OUT / 'source-index.json').write_text(json.dumps(catalogue, ensure_ascii=False, indent=2))
    with (OUT / 'source-index.csv').open('w', newline='', encoding='utf-8-sig') as stream:
        writer = csv.DictWriter(stream, fieldnames=list(catalogue[0]))
        writer.writeheader()
        writer.writerows(catalogue)
    for name in ['chronology-audit', 'conditions-sources', 'operations', 'anonymity-audit', 'support-layout', 'presentation-policy']:
        shutil.copy2(ROOT / f'data/episode123-v4-{name}.json', OUT / f'{name}.json')
    sdk = TMP / 'OWM_Episode123_v4.vrcw'
    assert sdk.stat().st_size > 100000
    shutil.copy2(sdk, OUT / sdk.name)
    kinds = Counter(image['kind'] for image in catalogue)
    own = kinds['original-operation-reconstruction']
    current = kinds['current-project-self-capture']
    sourced = len(catalogue) - own - current
    focus = [event for event in score['events'] if event['role'] == 'focus']
    live = read(TMP / 'reports/live-validation.json')
    nav = ''.join(f'<button onclick="jump({scene["start"]})">{int(scene["start"]//60):02}:{int(scene["start"]%60):02}　{html.escape(scene["title"])}</button>' for scene in score['scenes'])
    stills = ''.join(f'<a href="stills/{path.name}"><img loading="lazy" src="stills/{path.name}"><span>{html.escape(path.stem[3:])}</span></a>' for path in frames)
    actual = ''.join(f'<a href="runtime/{path.name}"><img loading="lazy" src="runtime/{path.name}"><span>{html.escape(path.stem)}</span></a>' for path in sorted((TMP / 'runtime').glob('*.png')))

    def source_url(image):
        return image['source_url'] if urlparse(image['source_url']).scheme in ['http', 'https'] else 'gallery/' + image['id'] + '.jpg'

    cards = []
    for image in catalogue:
        escaped = lambda value: html.escape(str(value), quote=True)
        first = image['appearances'][0]['start']
        cards.append(f'<article data-search="{escaped(image["title"] + image["category"] + image["kind"] + image["display_year"])}"><a href="{escaped(source_url(image))}" target="_blank" rel="noreferrer"><img loading="lazy" src="gallery/{image["id"]}.jpg"></a><b>{escaped(image["title"])}</b><small>{escaped(image["kind"])}</small><small>{escaped(image["display_year"])} · {first:.2f} 秒起 · {len(image["appearances"])} 次使用</small><button onclick="jump({first})">看片中位置</button></article>')
    links = ' · '.join(f'<a href="reports/{name}.json">{label}</a>' for name, label in [('interaction', '开始、结束及重播'), ('live-validation', '完整 Udon 播放'), ('montage/presentation-layout-validation', '图片间距与中央辅助'), ('stable-sound-saved-scene', '转场声源密集采样'), ('stable-sound-runtime', '真实 Udon 声源检查'), ('keyline-visibility', '字幕遮挡渲染检查'), ('static-data-validation', '素材、年代与音符审计'), ('persistence', '保存重开与原场景保护'), ('sdk-build', 'SDK 本地构建')])
    css = '''body{margin:0;background:#111b23;color:#e0e7e8;font:16px/1.65 system-ui,sans-serif}main{max-width:1200px;margin:auto;padding:24px}h1{font-size:28px}p{color:#b5c9cc}video{display:block;width:100%;background:#000;scroll-margin-top:16px}button,input{border:1px solid #395364;background:#203442;color:#e9eff0;padding:9px 12px;border-radius:5px}button{cursor:pointer;text-align:left}nav{display:flex;gap:8px;flex-wrap:wrap;margin:16px 0}a{color:#95d9dd}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(250px,1fr));gap:14px}.grid img{width:100%;height:190px;object-fit:contain;background:#080d12}.grid span{display:block}article{padding:10px;background:#1b2c36}article b,small{display:block}small{color:#9cb5be;font-size:12px}article button{margin-top:9px}input{width:90%;margin:20px 0}details{margin:24px 0}summary{cursor:pointer;font-size:21px}.note{padding:12px 18px;background:#1b2c36;border-left:3px solid #80bac2}'''
    page = f'''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>前三段 V4 验收</title><style>{css}</style><main>
<h1>前三段 · V4</h1><p>{len(catalogue)} 项独立内容 · {len(score['events'])} 次分层事件 · 原曲 133.12 秒</p>
<video id="film" controls preload="metadata" src="episode123-v4-review.mp4" poster="stills/v4_085.12.png"></video><nav>{nav}</nav>
<p>中央焦点停留 {min(event['hold'] for event in focus):.1f}—{max(event['hold'] for event in focus):.2f} 秒；{layout_build['supportSceneCount']} 个场面在主体近旁配有相关小图，通常 2 张，个别 3 张。外围窗口按最大振动预留间距，逐渐向侧后方展开，活动上限为 48 个。三个连续操作段分别展示文件、下载做种和字幕编辑。</p>
<p>三次场景旅行期间四轨声源保持物理位置，退出旅行连续衔接。高潮仍按乐段展开空间运动，波形继续采样真实音乐。所有名牌为 xxxx，5 张相关原图的 16 个账号区域在展示副本中匿名，原始来源保留。</p>
<p>片头及末段使用 EDGE／FC14／RHINO OF TORMENT 圆柱立体字。2017 与 2019 场景中人物和玩家显示旧版匿名名牌（xxxx）；后段用实体闸门表现入口条件变化，收在维护与保存动作。</p>
<p>{sourced} 项来源图、{current} 项当前工程自截、{own} 项原创操作状态。裁切和场景副本单独标识用途，内容总量按原始独立素材统计。图像捕获日期与所描绘年代详见来源索引。</p>
<p><a href="{sdk.name}">本地 SDK 包</a> · <a href="source-index.csv">素材来源与使用时间</a> · <a href="source-index.json">完整素材记录</a> · <a href="conditions-sources.json">条件变化段来源</a></p><p>{links}</p>
<p class="note">已通过单 Editor ClientSim 的真实 Udon 全片播放：{live['eventsObserved']}/{live['eventsTotal']} 事件、{live['focusHolds']} 个焦点停留、{live['keylinesObserved']} 条关键句；检查了实际按钮、结束重播、7 个接收时间锚点、真实 LocalPlayer 匿名名牌和四轨音源。参考片为固定视点渲染，音频为相同轨迹的立体声参考混音。头显中的双眼呈现、VR 控制器及真实多客户端同步留待实机验收。</p>
<details open><summary>演出画面与完整开合</summary><div class="grid">{stills}</div></details>
<details><summary>真实 Udon 运行画面</summary><div class="grid">{actual}</div></details>
<details><summary>全部 {len(catalogue)} 项内容</summary><input aria-label="筛选素材" placeholder="筛选名称、年代、来源性质或类别" oninput="filterAssets(this.value)"><div class="grid" id="assets">{''.join(cards)}</div></details>
</main><script>function jump(t){{let v=document.getElementById('film');v.currentTime=t;v.play();v.scrollIntoView({{behavior:'smooth',block:'start'}})}}function filterAssets(v){{for(let a of document.querySelectorAll('#assets article'))a.hidden=!a.dataset.search.toLowerCase().includes(v.toLowerCase())}}</script></html>'''
    (OUT / 'index.html').write_text(page)
    metadata = dict(complete=True, version=4, scene='Assets/Scenes/OWM_Episode123_v4.unity',
                    assets=len(catalogue), events=len(score['events']), focus_anchors=len(focus),
                    sourced_images=sourced, self_captures=current, original_operation_states=own,
                    support_scenes=layout_build['supportSceneCount'], support_pool=4, anonymous_source_copies=5,
                    sdk_bytes=sdk.stat().st_size, sdk_sha256=hashlib.sha256(sdk.read_bytes()).hexdigest(),
                    film_seconds=float(probe['format']['duration']), hmd_tested=False, real_multiplayer_tested=False)
    (OUT / 'package.json').write_text(json.dumps(metadata, ensure_ascii=False, indent=2))
    print('Review ready:', OUT / 'index.html')
    print(json.dumps(metadata, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
