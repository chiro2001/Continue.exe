#!/usr/bin/env python3
"""Create the local review entry from verified reports, actual scene stills and film."""
from pathlib import Path
import json
import html
import shutil
import subprocess

ROOT=Path(__file__).resolve().parent.parent
TMP=Path.home()/'tmp/vrc-old-web-map/episode123'
DEST=TMP/'review';DEST.mkdir(parents=True,exist_ok=True)
VALID=TMP/'validation'
def load(name):return json.loads((VALID/name).read_text())
endurance=load('live-endurance.json');assert endurance['success'] and endurance['cuesSeen']==12
buttons=load('live-buttons.json');assert len(buttons)==6
stereo=load('viewpoint-shader-checks.json');assert all(v['added'] for v in stereo['variantResults'])
artcheck=load('art-import-check.json');assert all(r['exact'] for r in artcheck)
movie=DEST/'episode123-review.mp4';assert movie.is_file()
duration=float(subprocess.check_output(['ffprobe','-v','error','-show_entries','format=duration','-of','default=nw=1:nk=1',str(movie)],text=True))
assert abs(duration-135.04)<.12,duration
iterations=[p for p in TMP.glob('iteration*') if p.name[9:].isdigit() and (p/'contact-sheet.jpg').is_file()]
latest=max(iterations,key=lambda p:int(p.name[9:]));stills=DEST/'stills';stills.mkdir(exist_ok=True)
for p in latest.glob('cue_*.png'):shutil.copy2(p,stills/p.name)
shutil.copy2(latest/'contact-sheet.jpg',DEST/'contact-sheet.jpg')
checks=DEST/'checks';checks.mkdir(exist_ok=True)
for name in ['live-endurance.json','live-buttons.json','viewpoint-shader-checks.json','art-import-check.json','motion-checks.json','render-report.json','sdk-build.json','sdk-layer-setup.json','post-sdk-live-smoke.json','final-editor-state.json','final-scene-state.json','console-final.json']:
    source=VALID/name
    if source.is_file():shutil.copy2(source,checks/name)
scenes=[s for s in json.loads((ROOT/'data/scene-score.json').read_text())['scenes'] if s['end']<=133.1201]
def esc(s):return html.escape(str(s))
def stamp(sec):return f'{int(sec)//60}:{int(sec)%60:02}'
cards=''.join(f'<button class="card" data-time="{s["start"]}"><img src="stills/cue_{i:02}.png" loading="lazy"><span>{i+1:02} · {esc(s["title"])}</span><small>{stamp(s["start"])} — {stamp(s["end"])}</small></button>' for i,s in enumerate(scenes))
sdkfile=VALID/'sdk-build.json'
sdk=json.loads(sdkfile.read_text()) if sdkfile.exists() else {'success':False}
sdktext=('本地 SDK 测试包已生成：'+sdk.get('path','')+'。') if sdk.get('success') else ('本地 SDK 构建尚未通过：'+sdk.get('message','未取得成功构建记录')+'。以已保存的 Unity 场景与 ClientSim 连续运行记录验收。')
sdklink='<p><a href="OWM_Episode123.vrcw" download>本地世界测试包（.vrcw）</a> · <a href="checks/sdk-build.json">构建记录</a></p>' if sdk.get('success') and (DEST/'OWM_Episode123.vrcw').is_file() else ''
content=f'''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>前三段 · 验收入口</title><style>
*{{box-sizing:border-box}}body{{margin:0;background:#172a35;color:#e7ece7;font:16px/1.7 system-ui,"Noto Sans CJK SC",sans-serif}}main{{max-width:1180px;margin:auto;padding:45px 24px 70px}}h1{{font-size:36px;line-height:1.2;margin:8px 0 18px}}p{{color:#bed0d1}}.overline{{color:#d5bc83;letter-spacing:.16em;font-size:13px}}video{{width:100%;display:block;background:#10212a;border:1px solid #52666e;border-radius:10px}}.chapters{{display:flex;gap:10px;flex-wrap:wrap;margin:16px 0 22px}}button{{cursor:pointer;color:inherit;background:#2b4653;border:1px solid #526c75;border-radius:6px;padding:10px 16px;font:inherit}}button:hover{{background:#3a5b66}}.entry{{background:#223c48;padding:20px 24px;border-left:3px solid #d5bc83;margin:24px 0}}code{{font-size:14px;word-break:break-all}}.grid{{display:grid;grid-template-columns:repeat(3,1fr);gap:14px}}.card{{padding:0;overflow:hidden;text-align:left}}.card img{{width:100%;display:block}}.card span,.card small{{display:block;padding:8px 12px 0}}.card small{{padding-bottom:12px;color:#a9c2c6}}details{{margin-top:26px;border-top:1px solid #47606a;padding-top:18px}}table{{width:100%;border-collapse:collapse;font-size:14px}}td{{padding:9px;border-bottom:1px solid #3c5661}}a{{color:#e3ca92}}@media(max-width:700px){{.grid{{grid-template-columns:1fr}}h1{{font-size:28px}}main{{padding:24px 14px}}}}
</style><main><div class="overline">VRCHAT · 前三段连续演出</div><h1>接入，繁荣，条件改变。</h1><p>演出 133.12 秒，12 个场面。先看连续参考影片，再从下方跳到想检查的场面。</p>
<video id="film" src="episode123-review.mp4" poster="stills/cue_03.png" controls preload="metadata"></video><div class="chapters"><button data-time="0">01 接入</button><button data-time="30.72">02 繁荣</button><button data-time="102.4">03 条件改变</button></div>
<p>影片采用固定观察视点和四轨参考混音，末尾附 1.92 秒验收提示。头显中的空间、声音与舒适度请在工程中体验。</p>
<div class="entry"><b>Unity 验收入口</b><br><code>D:\\Projects\\VRChatProjects2\\vrc-old-web-map</code><br><code>Assets/Scenes/OWM_Episode123.unity</code><p>进入 Play Mode 后按回车播放／暂停，左侧控制台支持重播和跳段。精细动作仍可按既定计划替换动捕。</p><p>{esc(sdktext)}</p>{sdklink}</div>
<div class="grid">{cards}</div><details><summary>查看自动检查范围</summary><table><tr><td>连续运行</td><td>133.12 秒，12 个场面均经过真实 Udon 更新，结束后停止音乐</td></tr><tr><td>四轨同步</td><td>持续复核最大采样差 {endurance['maxIntertrackSamples']}；跨音频缓冲区的瞬时读数单独记录</td></tr><tr><td>操作</td><td>播放、暂停、重播、跳段与收到锚点后的场面恢复</td></tr><tr><td>图像与 Shader</td><td>{len(artcheck)} 张图保持原始像素尺寸；立体渲染变体编译与左右／高低视点检查</td></tr><tr><td>体验范围</td><td>Editor / ClientSim；真实头显与多人客户端体验留给验收</td></tr></table></details><p><a href="acceptance.md">详细验收说明</a> · <a href="contact-sheet.jpg">全部场面接触表</a></p>
</main><script>const film=document.querySelector('#film');document.querySelectorAll('[data-time]').forEach(b=>b.onclick=()=>{{film.currentTime=Number(b.dataset.time);film.play().catch(()=>{{}});film.scrollIntoView({{behavior:'smooth',block:'center'}})}});</script></html>'''
(DEST/'index.html').write_text(content)
text=f'''# 前三段验收

演出范围：接入 0—30.72 秒、繁荣 30.72—102.40 秒、条件改变 102.40—133.12 秒。12 个场面，4 位可替换骨骼演员，40 张原创/年代重建图，7 个 Humanoid 占位动作，4 条空间音乐声部与 5 个原创非语音操作音效。

## 打开

- 此目录 `index.html`：影片、场面跳转和全部截图。
- Windows 工程：`D:\\Projects\\VRChatProjects2\\vrc-old-web-map`。
- 场景：`Assets/Scenes/OWM_Episode123.unity`。进入 Play Mode 后按回车播放/暂停，左侧控制台可以重播和跳段。
- {sdktext}

## 验收关注点

1. 年代界面、漫画/字幕和主要动作能否在实际观看距离下理解。
2. 撤界、图层深度和 Hub 穿行是否成立，转头与同伴在场是否自然。
3. 四轨常态前方融合、短高潮展开和操作音量是否合适。

## 已检查

- 133.12 秒真实 Udon 连续运行，12 个场面覆盖与结束状态。
- 控制按钮、暂停/恢复/重播/跳段；收到播放锚点后的本地状态重建。
- 四轨持续同步，跨缓冲区复核最大差 {endurance['maxIntertrackSamples']} 个采样；有实际音频输出。
- 双外观共用 7 个 Humanoid 动作，关键骨骼与手部位移；基本脚底和工位接触。
- {len(artcheck)} 张图的原始像素尺寸、Shader 立体变体和多视点。
- 参考影片 1600×900、25 fps、135.04 秒，完整解码与 29 个时刻抽查。

原始通过记录：[`checks/live-endurance.json`](checks/live-endurance.json)、[`checks/live-buttons.json`](checks/live-buttons.json)、[`checks/viewpoint-shader-checks.json`](checks/viewpoint-shader-checks.json)、[`checks/art-import-check.json`](checks/art-import-check.json)。四轨有少数瞬时跨缓冲区读数差，完整报告同时保留这些读数与复核结果。若要对照低视点真实运行截图，可查看上一级 `validation/endurance_*.png`。

## 验证边界

参考影片以 Unity 固定视点离线采样并配参考混音。完整运行由独立 ClientSim 记录覆盖。左右视点和 Shader 编译不能代替真实头显体验；真实多客户端网络传输与容量也尚未覆盖。精细表情、手指和最终动捕仍按之前约定后补。本次结束处的 0.3 秒共同淡出用于截段预览。

ClientSim 启动时有一条重复 EventSystem 警告，场景模板内保留了原 EventSystem；最终运行无新增 Udon 错误。SDK 标准图层与碰撞矩阵已通过官方方法配置，并保留前后记录。构建后播放/暂停/跳段/重播复查通过，编辑器已停止播放并保存到开场。

旧主场景及 180 秒运行数据保留，其余四段不在本次制作范围。原始界面来源和原创示例性质见仓库 `spec/25-panorama-scene-treatment.md`、`spec/26-episode123-production.md` 及素材清单。
'''
(DEST/'acceptance.md').write_text(text)
manifest={'status':'review-package-created','directory':str(DEST),'entry':str(DEST/'index.html'),'movie':str(movie),'movieDuration':duration,'showDuration':133.12,'latestIteration':latest.name,'stills':len(scenes),'sdkBuild':sdk,'headsetTested':False,'realMulticlientTransportTested':False}
manifest['windowsEntry']=r'C:\Users\Chiro\tmp\vrc-old-web-map\episode123\review\index.html'
manifest['scene']='Assets/Scenes/OWM_Episode123.unity'
(DEST/'review-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
print('Review entry:',DEST/'index.html')
