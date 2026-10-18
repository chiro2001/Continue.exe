#!/usr/bin/env python3
"""Build a muted V4 review page only from current, completed editor evidence."""
import hashlib
import html
import json
import shutil
import subprocess
from pathlib import Path
from PIL import Image,ImageDraw
from render_fullfilm_v4_audio import validate_reference

ROOT=Path(__file__).resolve().parents[1]
TMP=Path.home()/'tmp/vrc-old-web-map/fullfilm-v4'
OUT=TMP/'review'
REQUIRED=['baseline','static-validation','v4-inherited-validation','story-validation','interaction-validation',
          'qvpen-drawing-validation','player-names-validation','final-floor-walk','live-validation','media-build',
          'saved','reopen','sdk-build','sdk-bundle-inspection','sdk-dynamic-materials','sdk-scene-transition',
          'final-editor-state','movie-progress','encode-report','cover','heights','credits','tail-stills']

def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def esc(value):return html.escape(str(value),quote=True)


def evidence():
    result={name:read(TMP/'reports'/f'{name}.json') for name in REQUIRED}
    for name,value in result.items():
        okay=value.get('complete') is True and value.get('frame')==6386 and value.get('error') is None if name=='movie-progress' else value.get('success') is True
        if not okay:raise ValueError('Incomplete evidence: '+name)
    final=result['final-editor-state'];sdk=result['sdk-build']
    if not final['playStopped'] or not final['sceneSaved'] or not final['waiting'] or final['compiling'] or final['udonCompileErrors'] or final['shaderErrors']:raise ValueError('Editor is not saved and ready')
    if final['sceneSha256']!=sdk['outputSceneSha256'] or final['sceneSha256']!=result['reopen']['sha256'] or sdk['uploaded'] is not False:raise ValueError('Saved scene/build mismatch')
    fields={'OWM_FullFilm_v1.unity':'v1Sha256','OWM_FullFilm_v2.unity':'v2Sha256','OWM_FullFilm_v3.unity':'v3Sha256','OWM_Episode123_v5.unity':'v5Sha256'}
    for old in result['baseline']['protectedScenes']:
        key=fields[Path(old['path']).name]
        if final[key]!=old['sha256'] or result['reopen'][key]!=old['sha256']:raise ValueError('Protected scene changed: '+old['path'])
    media=read(ROOT/'data/fullfilm-v4-media-binding.json')
    for key,name in [('input_video_manifest_sha256','fullfilm-v4-history-video.json'),('input_emote_manifest_sha256','fullfilm-v4-historical-emotes.json')]:
        if media[key]!=sha(ROOT/'data'/name):raise ValueError('Media metadata changed after binding: '+name)
    if sha(OUT/'OWM_FullFilm_v4.vrcw')!=sdk['sha256'] or sha(OUT/'fullfilm-v4-review.mp4')!=result['encode-report']['movieSha256']:raise ValueError('Artifact hash mismatch')
    if validate_reference(OUT/'review-audio.wav')!=result['encode-report']['audioSha256']:raise ValueError('Audio reference mismatch')
    if not result['sdk-scene-transition']['allBytesOutsideOnlyListsIdentical']:raise ValueError('Unexpected SDK scene mutations')
    movie=OUT/'fullfilm-v4-review.mp4';decoded=TMP/'reports/full-decode.json'
    if not(decoded.exists() and read(decoded).get('sha256')==sha(movie) and read(decoded).get('all_frames_decoded') is True):
        decode=subprocess.run(['ffmpeg','-v','error','-i',str(movie),'-f','null','-'],capture_output=True,text=True)
        if decode.returncode or decode.stderr.strip():raise ValueError('Full decode failed: '+decode.stderr)
        decoded.write_text(json.dumps(dict(success=True,sha256=sha(movie),all_frames_decoded=True))+'\n')
    return result


def gallery(folder,target):
    dst=OUT/target;dst.mkdir(exist_ok=True);rows=[]
    for p in sorted((TMP/folder).glob('*.png')):
        if p.stem.startswith('contact'):continue
        shutil.copy2(p,dst/p.name)
        time=float(p.stem[3:]) if p.stem.startswith('v4_') else None
        rows.append(dict(file=target+'/'+p.name,label=p.stem,time=time))
    return rows


def figures(rows):
    return ''.join(f'<figure><a href="{esc(r["file"])}" target="_blank"><img loading="lazy" src="{esc(r["file"])}"></a><figcaption>{esc(r["label"])}'+(f' <button data-time="{r["time"]}">{r["time"]:.2f}s</button>' if r['time'] is not None else '')+'</figcaption></figure>' for r in rows)


def main():
    OUT.mkdir(parents=True,exist_ok=True);reports=evidence();stills=gallery('final-check','stills');live=gallery('live','live');heights=gallery('heights','heights')
    if len(stills)<25 or len(live)<16 or len(heights)<12:raise ValueError('Missing current screenshots')
    shutil.copy2(TMP/'map-cover-1200x900.png',OUT/'map-cover-1200x900.png')
    cover=Image.open(OUT/'map-cover-1200x900.png')
    if cover.size!=(1200,900) or cover.format!='PNG':raise ValueError('Wrong map cover')
    sheet=Image.new('RGB',(1600,245*((len(stills)+3)//4)),'#10151d');draw=ImageDraw.Draw(sheet)
    for i,row in enumerate(stills):
        im=Image.open(OUT/row['file']).convert('RGB');im.thumbnail((400,225));x=i%4*400;y=i//4*245;sheet.paste(im,(x,y));draw.text((x+5,y+226),row['label'],fill='white')
    sheet.save(OUT/'contact-sheet.jpg',quality=92)
    dst=OUT/'reports';dst.mkdir(exist_ok=True)
    for name in REQUIRED:shutil.copy2(TMP/'reports'/f'{name}.json',dst/f'{name}.json')
    for name in ['runtime-console','sdk-editor-console','output-mute-state']:
        p=TMP/'reports'/f'{name}.json'
        if p.exists():shutil.copy2(p,dst/p.name)
    metadata=['fullfilm-v4-design','fullfilm-v4-related-focus','fullfilm-v4-media-binding','fullfilm-v4-history-video','fullfilm-v4-historical-emotes','fullfilm-v4-workspace-control']
    for name in metadata:shutil.copy2(ROOT/'data'/f'{name}.json',OUT/f'{name}.json')
    implementation=(ROOT/'spec/72-fullfilm-v4-implementation-and-review.md').read_text().replace('(../data/','(').replace('(71-fullfilm-v4-acceptance.md)','(acceptance.md)').replace('(71a-fullfilm-v4-history-video.md)','(history-video.md)')
    (OUT/'implementation.md').write_text(implementation)
    for source,target in [('71-fullfilm-v4-acceptance.md','acceptance.md'),('71a-fullfilm-v4-history-video.md','history-video.md'),('71b-fullfilm-v4-historical-emotes.md','historical-emotes.md')]:shutil.copy2(ROOT/'spec'/source,OUT/target)
    jumps=[(0,'开场'),(30.72,'汉化'),(51.2,'中文社区'),(77.44,'VRC历史'),(120.32,'盗模与授权'),(133.12,'过载'),(186.88,'骤空'),(197.12,'制作'),(217.6,'从屏幕展开'),(245.76,'继续')]
    nav=''.join(f'<button data-time="{t}">{label}</button>' for t,label in jumps)
    page='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>全片 V4 验收</title><style>
    *{box-sizing:border-box}body{margin:0;background:#10151d;color:#e6edf4;font:16px/1.7 system-ui,"Microsoft YaHei",sans-serif}main{max-width:1380px;margin:auto;padding:28px 20px}a{color:#9dd7ee}p{color:#c4d0dc}video{width:100%;max-height:78vh;background:#000}.grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px}figure{margin:0;background:#1c2835}figure img{width:100%;display:block}figcaption{padding:8px;font-size:13px}button{background:#243d51;color:#f1f8ff;border:1px solid #52798d;padding:7px 10px;cursor:pointer}nav{display:flex;gap:8px;flex-wrap:wrap;margin:14px 0}h2{margin-top:38px}.cover{max-width:600px;width:100%}@media(max-width:850px){.grid{grid-template-columns:repeat(2,minmax(0,1fr))}}@media(max-width:520px){.grid{grid-template-columns:1fr}}
    </style><main><h1>全片 V4 · 继续</h1><p>253.44秒演出＋2秒演后。默认静音，不自动播放；时间按钮只定位。声音为原四轨立体声节奏参考，空间音场请在工程内验收。</p>
    <video id="movie" controls muted playsinline preload="metadata" poster="map-cover-1200x900.png"><source src="fullfilm-v4-review.mp4" type="video/mp4"></video><nav>'''+nav+'''</nav>
    <p><a href="map-cover-1200x900.png">1200×900 地图封面 PNG</a> · <a href="fullfilm-v4-review.mp4">参考片</a> · <a href="OWM_FullFilm_v4.vrcw">本地世界包</a> · <a href="contact-sheet.jpg">全片重点帧</a> · <a href="implementation.md">十四项处理与验证</a> · <a href="package.json">交付清单</a></p>
    <p>已完成桌面相机、实际ClientSim/Udon、迟到时钟锚点、连续全片、QvPen及本地SDK检查；本地包未上传。HMD双眼、真实远端多人和人耳空间听感仍须实机验收。</p>
    <h2>本轮重点</h2><p>小字开场与剧情飘带；逐列波浪 EDGE TEAM；简体中文字形；历史表情与真实VRC影像；头像盗用主题；主次多素材闪回；去除过载留黑；作品从电脑屏幕展开；“继续”组装扫光；侧边画笔与闭合等待房间。</p>
    <h2>当前画面</h2><div class="grid">'''+figures(stills)+'''</div><h2>实际连续播放</h2><div class="grid">'''+figures(live)+'''</div><h2>低视角与后向</h2><div class="grid">'''+figures(heights)+'''</div>
    <h2>来源与验证</h2><p>'''+ ' · '.join(f'<a href="{name}.json">{esc(name)}</a>' for name in metadata)+'''</p><details><summary>实际报告</summary><ul>'''+''.join(f'<li><a href="reports/{name}.json">{name}</a></li>' for name in REQUIRED)+'''</ul></details></main><script>const m=document.getElementById('movie');m.muted=true;document.querySelectorAll('[data-time]').forEach(b=>b.addEventListener('click',()=>{m.pause();m.currentTime=Number(b.dataset.time);m.scrollIntoView({behavior:'smooth',block:'center'});}));</script></html>'''
    (OUT/'index.html').write_text(page)
    media=read(ROOT/'data/fullfilm-v4-media-binding.json');final=reports['final-editor-state'];live_report=reports['live-validation']
    package=dict(success=True,revision='V4',scene='Assets/Scenes/OWM_FullFilm_v4.unity',date='2026-10-18',duration=253.44,review_duration=255.44,frames=6386,default_muted=True,autoplay=False,
                 historical_original_video_sources=1,historical_video_frames=len(media['history_frames']),silent_footage_atlases=len(media['films']),historical_emotes=len(media['emotes']),related_foreground_sources=128,
                 stills=len(stills),actual_live_stills=len(live),height_checks=len(heights),scene_sha256=final['sceneSha256'],movie_sha256=sha(OUT/'fullfilm-v4-review.mp4'),bundle_sha256=sha(OUT/'OWM_FullFilm_v4.vrcw'),cover_sha256=sha(OUT/'map-cover-1200x900.png'),sdk_uploaded=False,
                 hmd_tested=False,real_multiplayer_tested=False,human_spatial_hearing_tested=False,editor_play_stopped=True,editor_scene_saved=True,max_confirmed_eight_source_spread_samples=live_report['maxConfirmedEightSourceSpreadSamples'],
                 protected_scenes=reports['baseline']['protectedScenes'],review_linux=str(OUT/'index.html'),review_windows='C:/Users/Chiro/tmp/vrc-old-web-map/fullfilm-v4/review/index.html')
    package['artifacts']=[dict(file=str(p.relative_to(OUT)),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(OUT.rglob('*')) if p.is_file() and p.name!='package.json']
    (OUT/'package.json').write_text(json.dumps(package,ensure_ascii=False,indent=2)+'\n');(ROOT/'data/fullfilm-v4-delivery.json').write_text(json.dumps(package,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({k:v for k,v in package.items() if k!='artifacts'},ensure_ascii=False,indent=2))


if __name__=='__main__':main()
