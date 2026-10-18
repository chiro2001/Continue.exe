#!/usr/bin/env python3
"""Package current V3 evidence and an explicitly muted local review page."""
from pathlib import Path
import hashlib
import html
import json
import shutil
import subprocess
from PIL import Image, ImageDraw
from render_fullfilm_v3_audio import validate_reference

ROOT=Path(__file__).resolve().parents[1]
TMP=Path.home()/'tmp/vrc-old-web-map/fullfilm-v3'
OUT=TMP/'review'
REQUIRED=('static-validation','v3-detail-validation','interaction-validation','qvpen-drawing-validation','player-names-validation','final-floor-walk','live-validation','saved','reopen','sdk-build','sdk-bundle-inspection','sdk-dynamic-materials','sdk-scene-transition','final-editor-state','movie-progress','encode-report')
PROTECTED={'v1Sha256':'1a379b09482b3dbcd9836ebaad7191d2e28a0c30e3cd81fe77aff43460d77608','v2Sha256':'7009c3aecbf51f5d7824a9bde830a306e89894c11f115c2f5817c00bcaeb1a52','v5Sha256':'f34c994bd0e1123af79d601517814116d81d96f017105c650d3106fabeec093d'}

def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def esc(s):return html.escape(str(s),quote=True)

def evidence():
    reports={name:read(TMP/'reports'/f'{name}.json') for name in REQUIRED}
    for name,j in reports.items():
        okay=(j.get('complete') is True and j.get('frame')==6386 and j.get('error') is None) if name=='movie-progress' else j.get('success') is True
        if not okay:raise ValueError('Incomplete V3 evidence: '+name)
    sdk=reports['sdk-build'];final=reports['final-editor-state'];reopen=reports['reopen']
    if sdk['uploaded'] is not False or final['sceneSha256']!=sdk['outputSceneSha256'] or reopen['sha256']!=final['sceneSha256']:raise ValueError('Saved/reopened scene differs from the local SDK build')
    for name,value in PROTECTED.items():
        if final[name]!=value or reopen[name]!=value:raise ValueError('Previous scene changed: '+name)
    if not final['playStopped'] or not final['waiting'] or not final['sceneSaved'] or final['compiling'] or final['udonCompileErrors'] or final['shaderErrors']:raise ValueError('Editor is not clean and stopped')
    live=reports['live-validation']
    if tuple(len(live[n]) for n in ('scenes','nativeGroups','failureStates','softwareStates'))!=(24,6,4,11) or live['maxConfirmedEightSourceSpreadSamples']>2:raise ValueError('Incomplete actual Udon coverage')
    movie=OUT/'fullfilm-v3-review.mp4';bundle=OUT/'OWM_FullFilm_v3.vrcw'
    if sha(bundle)!=sdk['sha256'] or sha(movie)!=reports['encode-report']['movieSha256']:raise ValueError('Artifact checksum mismatch')
    if validate_reference(OUT/'review-audio.wav')!=reports['encode-report']['audioSha256']:raise ValueError('Audio checksum mismatch')
    transition=reports['sdk-scene-transition']
    if transition['inputSceneSha256']!=sdk['inputSceneSha256'] or transition['outputSceneSha256']!=sdk['outputSceneSha256']:raise ValueError('SDK transition proof belongs to another build')
    dst=OUT/'reports';dst.mkdir(exist_ok=True)
    decoded=dst/'full-decode.json'
    cached=decoded.exists() and read(decoded).get('sha256')==sha(movie) and read(decoded).get('all_frames_decoded') is True
    if not cached:
        result=subprocess.run(['ffmpeg','-v','error','-i',str(movie),'-f','null','-'],capture_output=True,text=True)
        if result.returncode or result.stderr.strip():raise ValueError('Movie decode failed: '+result.stderr)
    for name in REQUIRED:shutil.copy2(TMP/'reports'/f'{name}.json',dst/f'{name}.json')
    for name in ('sdk-editor-console','runtime-console'):
        source=TMP/'reports'/f'{name}.json'
        if source.exists():shutil.copy2(source,dst/source.name)
    (dst/'full-decode.json').write_text(json.dumps({'success':True,'sha256':sha(movie),'all_frames_decoded':True})+'\n')
    return reports

def gallery(folder,target,pattern='*.png'):
    (OUT/target).mkdir(exist_ok=True);rows=[]
    for source in sorted((TMP/folder).glob(pattern)):
        if source.stem.startswith('contact'):continue
        shutil.copy2(source,OUT/target/source.name)
        rows.append({'file':target+'/'+source.name,'label':source.stem,'time':float(source.stem[3:]) if source.stem.startswith('v3_') else None})
    return rows

def figures(rows):
    result=[]
    for r in rows:
        jump=f' <button data-time="{r["time"]}">{r["time"]:.2f}s</button>' if r['time'] is not None else ''
        result.append(f'<figure><a href="{esc(r["file"])}" target="_blank"><img loading="lazy" src="{esc(r["file"])}" alt="{esc(r["label"])}"></a><figcaption>{esc(r["label"])}{jump}</figcaption></figure>')
    return ''.join(result)

def main():
    OUT.mkdir(parents=True,exist_ok=True);reports=evidence()
    stills=gallery('final-check','stills');runtime=gallery('live','live','live-*.png');heights=gallery('heights','heights')
    if len(stills)<20 or len(runtime)<16 or len(heights)<12:raise ValueError('Missing current V3 screenshots')
    sheet=Image.new('RGB',(1600,245*((len(stills)+3)//4)),(12,14,21));draw=ImageDraw.Draw(sheet)
    for i,row in enumerate(stills):
        im=Image.open(OUT/row['file']).convert('RGB');im.thumbnail((400,225));x=i%4*400;y=i//4*245;sheet.paste(im,(x,y));draw.text((x+5,y+226),row['label'],fill='white')
    sheet.save(OUT/'contact-sheet.jpg',quality=92)
    for name in ('fullfilm-v3-edit','fullfilm-v3-user-images','fullfilm-v3-user-art','fullfilm-v3-model-assets','fullfilm-v3-avatar-provenance','fullfilm-v3-provenance-presentation','fullfilm-v3-local-assets','fullfilm-v3-software'):
        shutil.copy2(ROOT/'data'/f'{name}.json',OUT/f'{name}.json')
    shutil.copy2(ROOT/'data/fullfilm-v2-integration-source-audit.json',OUT/'inherited-history-source-audit.json')
    shutil.copy2(ROOT/'spec/70-fullfilm-v3-implementation-and-review.md',OUT/'implementation.md')
    changes=[('素材','8张用户原图、26个同源裁图与四态漫画、游戏本地化、Flash、贴纸进入实际时间轴；补入3组有日期的公开头像归属资料。'),('面板与EDGE','逐眼视点Shader异空间；蓝边半透明黑底像素方片组成EDGE TEAM圆柱。'),('空间声音','同时修正Unity与VRC组件的30/150米距离范围，转场冻结四源位置、空间混合和音量。'),('节奏与光效','恢复逐字彩带、语境弹幕；128个过载像素鼠标，圆截面/圆端帽PCM波形，光晕直接跟图。'),('最后的世界','细化同一份模型，11个不透明材质使用lilToon；桌面0.60米，地板/坡道/座面28个演后碰撞，真实玩家使用真实用户名。'),('演后留下','固定高度的重播牌、柔和的“继续”、低笔架与原版QvPen；演出隐藏笔、按钮、碰撞与动态墨迹，后台管理器常驻。'),('收尾','253.44秒演出，排除末2.56秒过门；原四轨仍为256秒。参考片另保留2秒演后画面。')]
    rows=''.join(f'<tr><th>{esc(a)}</th><td>{esc(b)}</td></tr>' for a,b in changes)
    jumps=''.join(f'<button data-time="{t}">{esc(label)}</button>' for t,label in [(0,'开场'),(30.72,'漫画汉化'),(77.44,'VRC'),(120.32,'头像归属'),(133.12,'过载'),(186.88,'骤空'),(197.12,'制作过程'),(217.6,'世界展开'),(235.52,'高潮'),(245.76,'继续')])
    links=''.join(f'<li><a href="reports/{name}.json">{esc(name)}</a></li>' for name in REQUIRED if name!='movie-progress')
    spread=reports['live-validation']['maxConfirmedEightSourceSpreadSamples']
    page=f'''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>全片V3验收</title>
<style>*{{box-sizing:border-box}}body{{margin:0;background:#11161e;color:#e5edf5;font:16px/1.7 system-ui,"Microsoft YaHei",sans-serif}}main{{max-width:1360px;margin:auto;padding:28px 22px}}a{{color:#99d9e9}}p,small{{color:#bdc9d5}}video{{width:100%;max-height:78vh;background:#000}}.grid{{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px}}figure{{margin:0;background:#1b2532}}figure img{{display:block;width:100%;aspect-ratio:16/9;object-fit:contain;background:#090d13}}figcaption{{padding:9px;font-size:13px}}button{{background:#23394b;border:1px solid #527184;color:#edfaff;padding:7px 12px;cursor:pointer}}nav{{display:flex;gap:8px;flex-wrap:wrap;margin:12px 0 24px}}table{{border-collapse:collapse;width:100%}}td,th{{padding:11px;border-bottom:1px solid #364453;text-align:left;vertical-align:top}}th{{width:140px}}h2{{margin-top:40px}}.note{{border-left:3px solid #8bcbdc;background:#1c2a38;padding:12px 16px}}@media(max-width:850px){{.grid{{grid-template-columns:repeat(2,minmax(0,1fr))}}}}@media(max-width:520px){{.grid{{grid-template-columns:1fr}}}}</style>
<main><h1>全片 V3 · 继续</h1><p>2026-10-17 · 253.44秒演出＋2秒演后 · OWM_FullFilm_v3</p>
<p class="note">默认静音，不自动播放。时间按钮只定位。视频使用原四轨生成的立体声节奏参考，空间音频与头显观感请在工程内验收。</p>
<video id="movie" controls muted preload="metadata" playsinline poster="stills/v3_224.00.png"><source src="fullfilm-v3-review.mp4" type="video/mp4"></video><nav>{jumps}</nav><p id="status" aria-live="polite"></p>
<p><a href="fullfilm-v3-review.mp4">参考片</a> · <a href="OWM_FullFilm_v3.vrcw">本地世界包</a> · <a href="contact-sheet.jpg">全片重点帧</a> · <a href="implementation.md">21项处理说明</a> · <a href="package.json">交付清单</a></p>
<h2>本轮变化</h2><table>{rows}</table>
<h2>已测与待验</h2><p>实际ClientSim/Udon全片覆盖24场、6组界面、4个失效状态和11个软件状态；缓冲更新后确认的八源最大差为{spread}采样。实际画笔输入、画线、清除和重播隐藏均有独立报告。</p>
<p>尚需实机验收：HMD双眼深度与波形发光、低身高拿笔、人耳空间听感、真实远端多人迟到加入及同步笔迹。单人时间锚点恢复与桌面相机截图不能替代这些测试。本地SDK包未上传。</p><details><summary>实际验证报告</summary><ul>{links}</ul></details>
<h2>当前V3画面</h2><p>编辑器相机确定性采样，可打开原图；源图、字幕与鼠标的变化需配合连续参考片观看。</p><div class="grid">{figures(stills)}</div>
<h2>实际连续播放截图</h2><p>来自本轮最终完整ClientSim/Udon播放，保留实际相机位置。</p><div class="grid">{figures(runtime)}</div>
<h2>低视角和后墙</h2><p>0.65、1.0、1.35米离线眼高检查；测试后恢复相机。</p><div class="grid">{figures(heights)}</div>
<h2>出处与制作记录</h2><p><a href="inherited-history-source-audit.json">继承的128张历史来源核验</a> · <a href="fullfilm-v3-user-images.json">8张用户原图接收记录</a> · <a href="fullfilm-v3-avatar-provenance.json">头像归属公开资料</a> · <a href="fullfilm-v3-software.json">Windows实际软件截屏</a> · <a href="fullfilm-v3-edit.json">剪尾与时长依据</a></p></main>
<script>const video=document.getElementById('movie');video.muted=true;document.querySelectorAll('[data-time]').forEach(b=>b.addEventListener('click',()=>{{video.pause();video.currentTime=Number(b.dataset.time);document.getElementById('status').textContent='已定位 '+b.textContent;video.scrollIntoView({{behavior:'smooth',block:'center'}});}}));</script></html>'''
    (OUT/'index.html').write_text(page)
    package={'success':True,'revision':'V3','scene':'Assets/Scenes/OWM_FullFilm_v3.unity','duration':253.44,'review_duration':255.44,'frames':6386,'default_muted':True,'autoplay':False,'historical_source_count':128,'received_user_images':8,'cutouts_from_user_images':26,'new_provenance_views':3,'stills':len(stills),'actual_live_stills':len(runtime),'height_checks':len(heights),'scene_sha256':reports['final-editor-state']['sceneSha256'],'movie_sha256':sha(OUT/'fullfilm-v3-review.mp4'),'bundle_sha256':sha(OUT/'OWM_FullFilm_v3.vrcw'),'sdk_uploaded':False,'hmd_tested':False,'real_multiplayer_tested':False,'human_spatial_hearing_tested':False,'qvpen_actual_single_client_draw_clear_replay_tested':True}
    package['artifacts']=[{'file':str(p.relative_to(OUT)),'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(OUT.rglob('*')) if p.is_file() and p.name!='package.json']
    (OUT/'package.json').write_text(json.dumps(package,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({'success':True,'review':str(OUT/'index.html'),'stills':len(stills),'live_stills':len(runtime),'artifacts':len(package['artifacts'])},ensure_ascii=False))

if __name__=='__main__':main()
