#!/usr/bin/env python3
"""Package the completed V5 scene, muted preview and actual editor evidence."""
import hashlib
import html
import json
import shutil
import subprocess
from pathlib import Path

from PIL import Image, ImageDraw
from render_fullfilm_v5_audio import validate_reference

ROOT = Path(__file__).resolve().parents[1]
TMP = Path.home() / 'tmp/vrc-old-web-map/fullfilm-v5'
OUT = TMP / 'review'
REQUIRED = [
    'baseline-stopped', 'static-validation', 'v4-inherited-validation',
    'v5-story-validation', 'interaction-validation', 'qvpen-drawing-validation',
    'player-names-validation', 'final-floor-walk', 'live-validation', 'ending-layout-validation',
    'saved', 'reopen', 'sdk-build', 'sdk-bundle-inspection',
    'sdk-dynamic-materials', 'sdk-scene-transition', 'final-editor-state',
    'source-integrity', 'model-export-selection', 'movie-progress', 'encode-report', 'cover', 'heights',
    'room-details', 'credits-build', 'model-material-bindings',
    'room01-wood-material-bindings', 'canopy-integration', 'emotes-build',
    'timeline-eac-build', 'curation', 'waiting-walls', 'runtime-console-review', 'sdk-console-review',
]
METADATA = [
    'fullfilm-v5-acceptance', 'fullfilm-v5-story', 'fullfilm-v5-emotes',
    'fullfilm-v5-related-focus', 'fullfilm-v5-curation',
    'fullfilm-v5-eac-credits', 'fullfilm-v5-material-audit',
    'fullfilm-v5-room01-material-recovery', 'fullfilm-v5-integration-resources', 'fullfilm-v5-final-layout', 'fullfilm-v5-model-assets',
]


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def esc(value):
    return html.escape(str(value), quote=True)


def evidence():
    reports = {name: read(TMP / 'reports' / f'{name}.json') for name in REQUIRED}
    for name, report in reports.items():
        okay = (report.get('complete') is True and report.get('frame') == 6386
                and report.get('error') in (None, '')) if name == 'movie-progress' else report.get('success') is True
        if not okay:
            raise ValueError('Incomplete evidence: ' + name)
    final, sdk = reports['final-editor-state'], reports['sdk-build']
    if (not final['playStopped'] or not final['sceneSaved'] or not final['waiting']
            or final['compiling'] or final['udonCompileErrors'] or final['shaderErrors']):
        raise ValueError('Editor is not saved and ready')
    if (final['sceneSha256'] != sdk['outputSceneSha256']
            or final['sceneSha256'] != reports['reopen']['sha256'] or sdk['uploaded']):
        raise ValueError('Saved scene and local SDK bundle differ')
    for report in (final, reports['reopen']):
        actual = {row['path']: row['sha256'] for row in report['protectedFiles']}
        for row in reports['baseline-stopped']['protectedFiles']:
            if actual.get(row['path']) != row['sha256']:
                raise ValueError('Protected scene changed: ' + row['path'])
    for filename, digest in [('OWM_FullFilm_v5.vrcw', sdk['sha256']),
                             ('fullfilm-v5-review.mp4', reports['encode-report']['movieSha256'])]:
        if sha(OUT / filename) != digest:
            raise ValueError('Artifact checksum differs: ' + filename)
    if validate_reference(OUT / 'review-audio.wav') != reports['encode-report']['audioSha256']:
        raise ValueError('Audio reference mismatch')
    if not reports['sdk-scene-transition']['allBytesOutsideOnlyListsIdentical']:
        raise ValueError('Unexpected SDK scene changes')
    for row in read(ROOT / 'data/fullfilm-v5-integration-resources.json')['resources']:
        if sha(ROOT / row['source']) != row['sha256']:
            raise ValueError('Integration source changed: ' + row['source'])
    if not reports['credits-build'].get('readViewMeshAndCanvasChecks'):
        raise ValueError('Credits reading area has not been checked against scene geometry and UI')
    if reports['model-export-selection']['selected']['sha256'] != reports['source-integrity']['modelImport']['sha256']:
        raise ValueError('Selected model export differs from the verified Unity import')
    movie = OUT / 'fullfilm-v5-review.mp4'
    decoded = TMP / 'reports/full-decode.json'
    if not (decoded.exists() and read(decoded).get('sha256') == sha(movie)
            and read(decoded).get('all_frames_decoded') is True):
        result = subprocess.run(['ffmpeg', '-v', 'error', '-i', str(movie), '-f', 'null', '-'],
                                capture_output=True, text=True)
        if result.returncode or result.stderr.strip():
            raise ValueError('Full movie decode failed: ' + result.stderr)
        decoded.write_text(json.dumps(dict(success=True, sha256=sha(movie), all_frames_decoded=True)) + '\n')
    state = read(TMP / 'reports/output-mute-state.json')
    if state.get('active') is not False:
        raise ValueError('Restore the recorded Windows mute state before packaging')
    return reports


def gallery(folder, target):
    destination = OUT / target
    destination.mkdir(exist_ok=True)
    rows = []
    for source in sorted((TMP / folder).glob('*.png')):
        if source.stem.startswith('contact'):
            continue
        shutil.copy2(source, destination / source.name)
        time = float(source.stem[3:]) if source.stem.startswith('v5_') else None
        rows.append(dict(file=target + '/' + source.name, label=source.stem, time=time))
    return rows


def figures(rows):
    return ''.join(
        f'<figure><a href="{esc(row["file"])}" target="_blank"><img loading="lazy" src="{esc(row["file"])}"></a>'
        f'<figcaption>{esc(row["label"])}' +
        (f' <button data-time="{row["time"]}">{row["time"]:.2f}s</button>' if row['time'] is not None else '') +
        '</figcaption></figure>' for row in rows)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    reports = evidence()
    stills = gallery('final-check', 'stills')
    live = gallery('live', 'live')
    heights = gallery('heights', 'heights')
    details = gallery('details', 'details')
    if len(stills) < 32 or len(live) < 16 or len(heights) < 12 or len(details) < 12:
        raise ValueError('Missing current composition, continuous playback or model screenshots')
    shutil.copy2(TMP / 'map-cover-1200x900.png', OUT / 'map-cover-1200x900.png')
    cover = Image.open(OUT / 'map-cover-1200x900.png')
    if cover.size != (1200, 900) or cover.format != 'PNG':
        raise ValueError('Wrong map cover')
    sheet = Image.new('RGB', (1600, 245 * ((len(stills) + 3) // 4)), '#10151d')
    draw = ImageDraw.Draw(sheet)
    for index, row in enumerate(stills):
        image = Image.open(OUT / row['file']).convert('RGB')
        image.thumbnail((400, 225))
        x, y = index % 4 * 400, index // 4 * 245
        sheet.paste(image, (x, y))
        draw.text((x + 5, y + 226), row['label'], fill='white')
    sheet.save(OUT / 'contact-sheet.jpg', quality=92)
    destination = OUT / 'reports'
    destination.mkdir(exist_ok=True)
    for name in REQUIRED + ['full-decode', 'runtime-console', 'sdk-editor-console', 'output-mute-state']:
        source = TMP / 'reports' / f'{name}.json'
        if source.exists():
            shutil.copy2(source, destination / source.name)
    for name in METADATA:
        shutil.copy2(ROOT / 'data' / f'{name}.json', OUT / f'{name}.json')
    credit_images = OUT / 'credits'
    credit_images.mkdir(exist_ok=True)
    for index, filename in enumerate(('credits-01-tools-and-world.png', 'credits-02-music-and-media.png'), 1):
        shutil.copy2(ROOT / 'art/export/fullfilm-v5/credits' / filename, credit_images / f'page-{index}.png')
    documents = [
        ('76-fullfilm-v5-acceptance.md', 'acceptance.md'),
        ('77a-fullfilm-v5-material-audit.md', 'material-audit.md'),
        ('77b-fullfilm-v5-model-refinement.md', 'models-and-materials.md'),
        ('77c-fullfilm-v5-eac-credits.md', 'eac-and-credits.md'),
        ('78-fullfilm-v5-implementation-and-review.md', 'implementation.md'),
    ]
    for source, target in documents:
        text = (ROOT / 'spec' / source).read_text()
        for original, packaged in documents:
            text = text.replace(original, packaged)
        (OUT / target).write_text(text.replace('../data/', ''))
    jumps = [(0, '从第一字开始'), (30.72, '汉化与字幕'), (51.2, '论坛'), (71.68, '贴吧'),
             (79.36, 'VRC相遇'), (104.96, '连接条件'), (133.12, '信息过载与盗模'),
             (149.76, '完整邮戳'), (186.88, '骤空'), (197.12, '制作'),
             (217.6, '世界展开'), (243.2, '创作仍将继续'), (253.44, '演后空间')]
    nav = ''.join(f'<button data-time="{time}">{label}</button>' for time, label in jumps)
    page = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>全片 V5 验收</title><style>
    *{box-sizing:border-box}body{margin:0;background:#10151d;color:#e6edf4;font:16px/1.7 system-ui,"Microsoft YaHei",sans-serif}main{max-width:1380px;margin:auto;padding:28px 20px}a{color:#9dd7ee}p{color:#c4d0dc}video{width:100%;max-height:78vh;background:#000}.grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px}figure{margin:0;background:#1c2835}figure img{width:100%;display:block}figcaption{padding:8px;font-size:13px}button{background:#243d51;color:#f1f8ff;border:1px solid #52798d;padding:7px 10px;cursor:pointer}nav{display:flex;gap:8px;flex-wrap:wrap;margin:14px 0}h2{margin-top:38px}@media(max-width:850px){.grid{grid-template-columns:repeat(2,minmax(0,1fr))}}@media(max-width:520px){.grid{grid-template-columns:1fr}}
    </style><main><h1>全片 V5 · 创作仍将继续</h1><p>253.44秒演出＋2秒演后。默认静音，不自动播放；时间按钮只定位。声音为原四轨立体声节奏参考，空间音场请在工程内验收。</p>
    <video id="movie" controls muted playsinline preload="metadata" poster="map-cover-1200x900.png"><source src="fullfilm-v5-review.mp4" type="video/mp4"></video><nav>''' + nav + '''</nav>
    <p><a href="map-cover-1200x900.png">1200×900 封面</a> · <a href="fullfilm-v5-review.mp4">完整参考片</a> · <a href="OWM_FullFilm_v5.vrcw">本地世界包</a> · <a href="contact-sheet.jpg">全片重点帧</a> · <a href="credits/page-1.png">借物表原图 1</a> · <a href="credits/page-2.png">借物表原图 2</a> · <a href="implementation.md">实施与验证记录</a> · <a href="package.json">交付清单</a></p>
    <p>本轮调整：飘带从头展开、按焦点换句和局部灰化；论坛至贴吧的四代表情；年代与AI图取舍；盗模材料并入中间过载；同色封闭等待房间；原始木纹恢复；木构遮棚、独立借物表与片尾落地。</p>
    <p>桌面相机、实际单客户端 ClientSim/Udon、完整连续播放、重播、接收时间锚点恢复、QvPen、本地SDK已检查。真实头显双眼、远端多人和人耳空间听感仍待实机验收。EAC当前安装包原图、蓝熊图标和2026-09-24真实报错分别标注，不合成伪启动截图。</p>
    <h2>当前画面</h2><div class="grid">''' + figures(stills) + '''</div><h2>实际连续播放</h2><div class="grid">''' + figures(live) + '''</div><h2>低视角与后向</h2><div class="grid">''' + figures(heights) + '''</div><h2>房间、模型与借物表</h2><div class="grid">''' + figures(details) + '''</div>
    <h2>来源与验证</h2><p>''' + ' · '.join(f'<a href="{name}.json">{esc(name)}</a>' for name in METADATA) + '''</p><details><summary>实际报告</summary><ul>''' + ''.join(f'<li><a href="reports/{name}.json">{name}</a></li>' for name in REQUIRED) + '''</ul></details></main><script>const m=document.getElementById('movie');m.muted=true;document.querySelectorAll('[data-time]').forEach(b=>b.addEventListener('click',()=>{m.pause();m.currentTime=Number(b.dataset.time);m.scrollIntoView({behavior:'smooth',block:'center'});}));</script></html>'''
    (OUT / 'index.html').write_text(page)
    final = reports['final-editor-state']
    package = dict(
        success=True, revision='V5', scene='Assets/Scenes/OWM_FullFilm_v5.unity', date='2026-10-18',
        status='implemented_and_editor_clientsim_verified', spec='spec/78-fullfilm-v5-implementation-and-review.md',
        duration=253.44, review_duration=255.44, frames=6386, default_muted=True, autoplay=False,
        story_ribbon_cues=reports['v5-story-validation']['ribbonCues'],
        redacted_provenance_sources=14, ripping_climax_span=[133.12, 186.88],
        historical_emotes=reports['emotes-build']['uniqueTextures'], emote_timed_uses=reports['emotes-build']['timedUses'],
        stills=len(stills), actual_live_stills=len(live), height_checks=len(heights), detail_checks=len(details),
        scene_sha256=final['sceneSha256'], movie_sha256=sha(OUT / 'fullfilm-v5-review.mp4'),
        bundle_sha256=sha(OUT / 'OWM_FullFilm_v5.vrcw'), cover_sha256=sha(OUT / 'map-cover-1200x900.png'),
        sdk_uploaded=False, hmd_tested=False, real_multiplayer_tested=False, human_spatial_hearing_tested=False,
        editor_play_stopped=True, editor_scene_saved=True,
        sdk_editor_playerloop_recursive_logs=reports['sdk-console-review']['new_editor_playerloop_recursion_logs'],
        known_limits=['HMD stereo, real remote multiplayer and human spatial hearing remain untested',
                      'Two Unity Editor PlayerLoop recursion logs occurred during the successful local SDK build; actual VRChat client loading remains untested'],
        max_confirmed_eight_source_spread_samples=reports['live-validation']['maxConfirmedEightSourceSpreadSamples'],
        qvpen_actual_single_client_draw_clear_replay_tested=True,
        protected_scenes=reports['baseline-stopped']['protectedFiles'],
        windows_original_output_state_restored=True,
        review_linux=str(OUT / 'index.html'),
        review_windows='C:/Users/Chiro/tmp/vrc-old-web-map/fullfilm-v5/review/index.html')
    package['artifacts'] = [dict(file=str(path.relative_to(OUT)), bytes=path.stat().st_size, sha256=sha(path))
                            for path in sorted(OUT.rglob('*')) if path.is_file() and path.name != 'package.json']
    blob = json.dumps(package, ensure_ascii=False, indent=2) + '\n'
    (OUT / 'package.json').write_text(blob)
    (ROOT / 'data/fullfilm-v5-delivery.json').write_text(blob)
    print(json.dumps({key: value for key, value in package.items() if key != 'artifacts'}, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
