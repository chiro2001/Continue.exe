#!/usr/bin/env python3
"""Build the local V3 review site only from completed, verified artifacts."""
from pathlib import Path
from collections import defaultdict
import json,html,csv,shutil,subprocess,hashlib
from PIL import Image
from urllib.parse import urlparse
ROOT=Path(__file__).resolve().parent.parent;TMP=Path.home()/'tmp/vrc-old-web-map/episode123-v3';OUT=TMP/'review';OUT.mkdir(parents=True,exist_ok=True)
def read(p):return json.loads(p.read_text())
def main():
    score=read(ROOT/'data/episode123-v3-score.json');audit=read(ROOT/'data/episode123-v3-material-audit.json')
    for name in ['interaction.json','live-validation.json','sdk-build.json']:
        r=read(TMP/'reports'/name)
        if r.get('success') is not True:raise RuntimeError('Unsuccessful report: '+name)
    movie=OUT/'episode123-v3-review.mp4'
    if not movie.exists():raise RuntimeError('Complete reference film missing')
    probe=json.loads(subprocess.check_output(['ffprobe','-v','error','-show_format','-show_streams','-of','json',str(movie)]))
    assert abs(float(probe['format']['duration'])-135.04)<.1
    assert any(s['codec_type']=='audio' for s in probe['streams'])
    (OUT/'ffprobe.json').write_text(json.dumps(probe,ensure_ascii=False,indent=2))
    (OUT/'stills').mkdir(exist_ok=True);(OUT/'gallery').mkdir(exist_ok=True);(OUT/'reports').mkdir(exist_ok=True)
    for report in (TMP/'reports').glob('*.json'):shutil.copy2(report,OUT/'reports'/report.name)
    frames=sorted((TMP/'frames').glob('v3_*.png'))
    for f in frames:shutil.copy2(f,OUT/'stills'/f.name)
    for directory in ['runtime','views']:
        if (TMP/directory).exists():shutil.copytree(TMP/directory,OUT/directory,dirs_exist_ok=True)
    appearances=defaultdict(list)
    for e in score['events']:appearances[e['image']].append((e['start'],e['end']))
    catalogue=[]
    for r in score['images']:
        p=Path(r['source_path']);im=Image.open(p).convert('RGB');im.thumbnail((320,240));im.save(OUT/'gallery'/(r['id']+'.jpg'),quality=86)
        catalogue.append(dict(id=r['id'],title=r['title'],kind=r['kind'],category=r['category'],source_url=r.get('source_url',r.get('source_page_url','')),source_path=str(p),appearances=appearances[r['id']]))
    (OUT/'source-index.json').write_text(json.dumps(catalogue,ensure_ascii=False,indent=2))
    with (OUT/'source-index.csv').open('w',newline='',encoding='utf-8-sig') as stream:
        writer=csv.DictWriter(stream,fieldnames=['id','title','kind','category','source_url','source_path','appearances']);writer.writeheader();writer.writerows(catalogue)
    sdk=TMP/'OWM_Episode123_v3.vrcw';assert sdk.exists();shutil.copy2(sdk,OUT/sdk.name)
    sections=''.join(f'<button onclick="jump({s["start"]})">{int(s["start"]//60):02}:{int(s["start"]%60):02}　{html.escape(s["title"])}</button>' for s in score['scenes'])
    stills=''.join(f'<a href="stills/{p.name}"><img loading="lazy" src="stills/{p.name}"><span>{p.stem[3:]} s</span></a>' for p in frames)
    def source_link(r):return r['source_url'] if urlparse(r['source_url']).scheme in ['http','https'] else 'gallery/'+r['id']+'.jpg'
    cards=''.join(f'<article data-search="{html.escape(r["title"]+r["category"]+r["kind"],quote=True)}"><a href="{html.escape(source_link(r),quote=True)}"><img loading="lazy" src="gallery/{r["id"]}.jpg"></a><b>{html.escape(r["title"])}</b><small>{html.escape(r["kind"])}</small><small>{r["appearances"][0][0]:.2f} 秒起 · {len(r["appearances"])} 次使用</small></article>' for r in catalogue)
    css='body{margin:0;background:#111b23;color:#e0e7e8;font:16px/1.6 system-ui,sans-serif}main{max-width:1200px;margin:auto;padding:28px}h1{font-size:28px}p{color:#b5c9cc}video{width:100%;background:#000}button,input{border:1px solid #395364;background:#203442;color:#e9eff0;padding:9px 12px;border-radius:5px}button{cursor:pointer;text-align:left}nav{display:flex;gap:8px;flex-wrap:wrap;margin:16px 0}a{color:#95d9dd}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(250px,1fr));gap:14px}.grid img{width:100%;height:190px;object-fit:contain;background:#080d12}.grid a{position:relative}.grid span{display:block}article{padding:10px;background:#1b2c36}article b,small{display:block}small{color:#8fa9b3;font-size:12px}input{width:90%;margin:20px 0}details{margin:24px 0}summary{cursor:pointer;font-size:22px}'
    page=f'<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>前三段 V3 验收</title><style>{css}</style><main><h1>前三段 · V3</h1><p>{audit["unique_used_assets"]} 项独立内容（上一版的 {audit["multiplier"]:.2f} 倍）· {len(score["events"])} 次分层事件 · 原曲 133.12 秒</p><video id="film" controls preload="metadata" src="episode123-v3-review.mp4"></video><nav>{sections}</nav><p>影片为固定视点、按演出时钟渲染的参考片；耳机内双眼视差与头部移动请在世界中验收。音轨为同一四轨运动轨迹的立体声参考混音。</p><p><a href="OWM_Episode123_v3.vrcw">本地 SDK 构建包</a> · <a href="source-index.csv">素材来源与使用时间</a> · <a href="source-index.json">完整素材记录</a></p><details open><summary>演出画面</summary><div class="grid">{stills}</div></details><details><summary>全部 {len(catalogue)} 项内容</summary><input placeholder="筛选素材、来源性质或类别" oninput="filterAssets(this.value)"><div class="grid" id="assets">{cards}</div></details><p>已验证单个 Editor ClientSim 的完整 Udon 演出、连续播放、重播与接收时间锚点恢复；真实头显、镜面/摄影机的 VRChat 最终呈现及多客户端网络仍需实机验收。原始第二版场景和 Avatar 源材质保留。</p></main><script>function jump(t){{let v=document.getElementById("film");v.currentTime=t;v.play()}}function filterAssets(v){{for(let a of document.querySelectorAll("#assets article"))a.hidden=!a.dataset.search.toLowerCase().includes(v.toLowerCase())}}</script></html>'
    report_links=' · '.join(f'<a href="reports/{name}">{label}</a>' for name,label in [('interaction.json','交互与接收锚点'),('live-validation.json','完整 Udon 演出'),('shader-checks.json','Shader 桌面对照'),('static-data-validation.json','素材与音符审计'),('sdk-build.json','SDK 本地构建')])
    page=page.replace('<details open>','<p>208 项来源图、3 项当前工程自截、144 项原创操作状态。裁切与换色不重复计数。</p><p>'+report_links+'</p><details open>',1)
    runtime_cards=''.join(f'<a href="runtime/{p.name}"><img loading="lazy" src="runtime/{p.name}"><span>{p.stem[5:]} s · ClientSim Udon 运行</span></a>' for p in sorted((TMP/'runtime').glob('live_*.jpg')))
    page=page.replace('<details><summary>全部', '<details><summary>实际 Udon 运行截图</summary><div class="grid">'+runtime_cards+'</div></details><details><summary>全部',1)
    (OUT/'index.html').write_text(page)
    metadata=dict(complete=True,assets=len(catalogue),events=len(score['events']),sourced_images=sum(r['kind'] not in ['original-operation-reconstruction','current-project-self-capture'] for r in catalogue),self_captures=sum(r['kind']=='current-project-self-capture' for r in catalogue),original_operation_states=sum(r['kind']=='original-operation-reconstruction' for r in catalogue),sdk_bytes=sdk.stat().st_size,sdk_sha256=hashlib.sha256(sdk.read_bytes()).hexdigest(),film_seconds=135.04,hmd_tested=False,real_multiplayer_tested=False)
    (OUT/'package.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2));print('Review ready:',OUT/'index.html');print(metadata)
if __name__=='__main__':main()
