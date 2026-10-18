#!/usr/bin/env python3
"""Package V2 evidence, stills and a muted-by-default review page; no playback."""
from __future__ import annotations

import hashlib
import html
import json
from pathlib import Path
import shutil
import subprocess

from PIL import Image, ImageDraw
from render_fullfilm_v2_audio import validate_reference

ROOT = Path(__file__).resolve().parents[1]
TMP = Path.home()/"tmp/vrc-old-web-map/fullfilm-v2"
OUT = TMP/"review"
REQUIRED = ("static-validation", "interaction-validation", "live-validation", "saved", "reopen", "sdk-build", "sdk-bundle-inspection", "sdk-dynamic-materials", "sdk-scene-transition", "final-editor-state", "movie-progress", "encode-report")


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def esc(value):
    return html.escape(str(value), quote=True)


def timecode(value):
    return f"{int(value)//60:02}:{value%60:05.2f}"


def evidence():
    reports = {name: read(TMP/"reports"/(name+".json")) for name in REQUIRED}
    for name, report in reports.items():
        if name == "movie-progress":
            okay = report.get("complete") is True and report.get("frame") == 6450 and report.get("error") is None
        else:
            okay = report.get("success") is True
        if not okay:
            raise ValueError("Incomplete V2 evidence: " + name)
    sdk, final, reopen = (reports[key] for key in ("sdk-build", "final-editor-state", "reopen"))
    current = sdk["outputSceneSha256"]
    if final["sceneSha256"] != current or reopen["sha256"] != current or sdk["uploaded"] is not False:
        raise ValueError("Final saved scene does not match this local SDK build")
    transition=reports["sdk-scene-transition"]
    if transition["inputSceneSha256"]!=sdk["inputSceneSha256"] or transition["outputSceneSha256"]!=current or transition["allBytesOutsideOnlyListIdentical"] is not True:
        raise ValueError("SDK input/output transition has not been verified")
    for key, value in (("v1Sha256", "1a379b09482b3dbcd9836ebaad7191d2e28a0c30e3cd81fe77aff43460d77608"), ("v5Sha256", "f34c994bd0e1123af79d601517814116d81d96f017105c650d3106fabeec093d")):
        if final[key] != value or reopen[key] != value:
            raise ValueError("Protected previous scene changed: " + key)
    live = reports["live-validation"]
    if len(live["scenes"]) != 24 or len(live["nativeGroups"]) != 6 or len(live["failureStates"]) != 4 or len(live["softwareStates"]) != 11 or live["maxConfirmedEightSourceSpreadSamples"] > 2:
        raise ValueError("Full runtime coverage is incomplete")
    if not final["playStopped"] or not final["waiting"] or not final["sceneSaved"] or final["compiling"] or final["udonCompileErrors"] or final["shaderErrors"]:
        raise ValueError("Editor was not left in a clean stopped waiting state")
    bundle = OUT/"OWM_FullFilm_v2.vrcw"
    movie = OUT/"fullfilm-v2-review.mp4"
    if sha(bundle) != sdk["sha256"] or sha(bundle) != reports["sdk-bundle-inspection"]["sha256"]:
        raise ValueError("Wrong V2 world bundle")
    if sha(movie) != reports["encode-report"]["movieSha256"]:
        raise ValueError("Wrong V2 movie")
    if validate_reference(OUT/"review-audio.wav") != reports["encode-report"]["audioSha256"]:
        raise ValueError("Wrong music reference")
    decoded = OUT/"reports/full-decode.json"
    if not decoded.exists() or read(decoded).get("sha256") != sha(movie):
        result = subprocess.run(["ffmpeg", "-v", "error", "-i", str(movie), "-map", "0:v:0", "-map", "0:a:0", "-f", "null", "-"], capture_output=True)
        report = dict(success=result.returncode == 0 and not result.stderr.strip(), returncode=result.returncode, error=result.stderr.decode("utf-8", "replace"), sha256=sha(movie), played=False)
        decoded.parent.mkdir(exist_ok=True)
        decoded.write_text(json.dumps(report, indent=2)+"\n")
    if not read(decoded)["success"]:
        raise ValueError("Movie failed full decode")
    report_out = OUT/"reports"
    report_out.mkdir(exist_ok=True)
    for source in (TMP/"reports").glob("*.json"):
        if not source.name.startswith("cli-"):
            shutil.copy2(source, report_out/source.name)
    return reports


def images():
    stills = OUT/"stills"
    thumbs = OUT/"sources"
    stills.mkdir(exist_ok=True)
    thumbs.mkdir(exist_ok=True)
    rows = []
    for source in sorted((TMP/"final-check").glob("*.png")):
        shutil.copy2(source, stills/source.name)
        stamp = float(source.stem[3:]) if source.stem.startswith("v2_") else None
        rows.append(dict(file=source.name, time=stamp))
    contact = Image.new("RGB", (1920, 228*((len(rows)+3)//4)), (15, 18, 25))
    draw = ImageDraw.Draw(contact)
    for i, row in enumerate(rows):
        im = Image.open(stills/row["file"]).convert("RGB")
        im.thumbnail((480, 204))
        x, y = i%4*480, i//4*228
        contact.paste(im, (x, y+24))
        draw.text((x+8, y+6), row["file"], fill=(240, 240, 240))
    contact.save(OUT/"contact-sheet.jpg", quality=92)
    atlas = read(ROOT/"data/fullfilm-v2-atlas.json")
    for row in atlas["sources"]:
        with Image.open(ROOT/row["export"]) as source:
            im = source.convert("RGB")
            im.thumbnail((320, 240))
            im.save(thumbs/(row["id"]+".jpg"), quality=85)
    return rows, atlas


def render_page(reports, stills, atlas):
    beats = [(0, "景深开场"), (10.24, "EDGE 与 README"), (30.72, "漫画、汉化、分发"), (77.44, "VRC 群像"), (102.4, "入口的条件"), (133.12, "信息过载"), (168.96, "失效"), (186.88, "远去"), (197.12, "实际制作"), (217.6, "Play 展开"), (235.52, "最终高潮"), (251, "继续")]
    jumps = "".join(f'<button data-time="{t}">{timecode(t)} · {esc(label)}</button>' for t, label in beats)
    captions = [
        ("finished.png", "演后世界与重播"), ("waiting.png", "开始前：单一景深窗口"),
        ("v2_007.68.png", "接入：旧界面、立体 EDGE、四通道"), ("v2_035.84.png", "同一画面内的原图与文字变化"),
        ("v2_087.28.png", "VRC：保留群像和匿名名牌"), ("v2_117.76.png", "实际历史图与访问状态并置"),
        ("v2_145.92.png", "过载：主图留住，外围多层推进"), ("v2_175.36.png", "同一对象的可用状态改变"),
        ("v2_203.52.png", "Windows Blender 真实模型截图"), ("v2_208.64.png", "Windows Unity 同模型测试"),
        ("v2_218.88.png", "编辑器框倾转、微缩世界展开"), ("v2_224.00.png", "进入同一场景的世界尺度"),
        ("v2_239.36.png", "四通道高潮与制作过程回响"), ("v2_252.16.png", "继续")]
    caption_map = dict(captions)
    frames = "".join(f'<figure><a href="stills/{esc(r["file"])}" target="_blank"><img loading="lazy" src="stills/{esc(r["file"])}" alt="{esc(caption_map.get(r["file"],r["file"]))}"></a><figcaption>{esc(caption_map.get(r["file"],r["file"]))}'+(f' <button data-time="{r["time"]}">{timecode(r["time"])}</button>' if r["time"] is not None else "")+"</figcaption></figure>" for r in stills)
    comparisons=[]
    (OUT/"compare").mkdir(exist_ok=True)
    for t in (55.36, 87.28, 145.92, 170.24, 203.52, 218.88, 224.0, 239.36):
        name=f"{t:06.2f}"
        before=Path.home()/f"tmp/vrc-old-web-map/fullfilm-v1/review/stills/full_{name}.png"
        after=OUT/f"stills/v2_{name}.png"
        if not before.is_file() or not after.is_file():
            continue
        output=Image.new("RGB",(1600,476),(15,18,25));draw=ImageDraw.Draw(output)
        for i,source in enumerate((before,after)):
            im=Image.open(source).convert("RGB");im.thumbnail((800,450));output.paste(im,(i*800,26));draw.text((i*800+12,7),f"{'V1' if i==0 else 'V2'} / {timecode(t)}",fill=(240,240,240))
        filename="comparison-"+name+".jpg";output.save(OUT/"compare"/filename,quality=94)
        comparisons.append(f'<figure><a href="compare/{filename}" target="_blank"><img loading="lazy" src="compare/{filename}" alt="同刻 V1/V2 对照"></a><figcaption>{timecode(t)} · 左 V1，右 V2</figcaption></figure>')
    sources = []
    for row in atlas["sources"]:
        date = row.get("source_date") or row.get("capture_date") or row.get("depicted_year") or "日期未标"
        provenance = row.get("asset_kind") or row.get("kind") or "见来源记录"
        quality = row.get("visual_quality", "原媒体 / 旧库，见清单")
        title = esc(row.get("title", row["id"]))
        link = row.get("source_page_url") or row.get("source_url") or row.get("image_url")
        if isinstance(link, str) and link.startswith(("https://", "http://")):
            title = f'<a href="{esc(link)}" target="_blank" rel="noreferrer">{title}</a>'
        sources.append(f'<article class="source" data-search="{esc(str(row.get("title",""))+" "+str(row.get("category","")))}"><img loading="lazy" src="sources/{esc(row["id"])}.jpg" alt=""><div>{title}<small>{esc(row.get("category",""))} · {esc(date)}<br>{esc(provenance)} · {esc(quality)}</small></div></article>')
    live = reports["live-validation"]
    report_names = {"static-validation": "210 个静态锚点及旅行声源", "interaction-validation": "射线点击、重播、接收时间锚点恢复", "live-validation": "完整 256 秒实际 Udon", "sdk-build": "V2 本地 SDK 构建", "sdk-bundle-inspection": "V2 世界包场景表", "reopen": "保存重开、V1/V5 文件保护", "final-editor-state": "停止、编译与保存状态", "encode-report": "6450 帧参考片编码"}
    links = "".join(f'<li><a href="reports/{name}.json">{esc(label)}</a></li>' for name, label in report_names.items())
    changes = [
        ("开场与 EDGE", "窗口内部为六层实际三维深度；原挤出字轮廓围绕圆柱纵轴旋转。"),
        ("声音与波形", "远去段保留同 PCM 的低增益融合层；只显示当前区域四条波形，旅行期间四源不移动。"),
        ("素材密度", "128 张不同原图、93 个来源页，本轮新增 69 张；288 个错层面片。实例数量与不同原图数量分别统计。"),
        ("中央主次", "六组原生界面各四态；主图保留，副图、内容文字和光标同步变化；过载主图约 3.84 秒。"),
        ("文案", "撤去大段解释字幕和说明型弹幕，保留原生 UI、日期来源与末尾“继续”。"),
        ("模型与人物", "Blender 重做工作台、场景块、编辑器边框和 Hub 细节；人物只留在 VRC 段。"),
        ("真实制作过程", "Windows Blender/Unity 实际打开同一资产，材质、线框、导入与 Play 过程形成 11 个软件状态。"),
        ("转场与结尾", "页面错层、旋转缩放、屏障收紧、局部故障、骤空；结尾由微缩模型、蓝色拓扑展开到完整世界。")]
    change_rows = "".join(f'<tr><th>{esc(a)}</th><td>{esc(b)}</td></tr>' for a,b in changes)
    template = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>VRC Old Web Map · 全片 V2 验收</title>
<style>*{box-sizing:border-box}body{margin:0;background:#11151e;color:#e9edf3;font:16px/1.65 system-ui,"Microsoft YaHei",sans-serif}main{max-width:1360px;margin:auto;padding:32px 24px}h1,h2{line-height:1.3}h1{font-size:32px;margin-bottom:8px}h2{margin-top:44px}a{color:#a4d8ef}p,small{color:#bac5d3}small{display:block;font-size:12px}video{display:block;width:100%;max-height:75vh;background:#000;margin-top:24px}.jumps{display:flex;flex-wrap:wrap;gap:8px;margin:16px 0}button{color:#e4eaf3;background:#263448;border:1px solid #476079;border-radius:5px;padding:7px 10px;cursor:pointer}button:hover{background:#36516b}.grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:16px}figure{margin:0;background:#1b2430;border:1px solid #354050}figure img{display:block;width:100%;aspect-ratio:16/9;object-fit:contain;background:#000}figcaption{padding:12px;font-size:14px}figcaption button{font-size:12px;padding:3px 6px}.note{padding:14px 18px;background:#1b2a39;border-left:3px solid #79b6cc}table{width:100%;border-collapse:collapse}th,td{text-align:left;vertical-align:top;border-bottom:1px solid #354050;padding:12px}th{width:170px;color:#a4d8ef}.sources{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px}.source{background:#1b2430;border:1px solid #354050;font-size:13px}.source img{width:100%;height:165px;object-fit:contain;background:#101319}.source div{padding:10px}input{width:100%;background:#202b39;color:#e9edf3;border:1px solid #476079;border-radius:5px;padding:12px;margin:10px 0 20px}code{overflow-wrap:anywhere}li{margin:6px 0}@media(max-width:850px){.grid,.sources{grid-template-columns:repeat(2,minmax(0,1fr))}main{padding:20px 12px}th{width:105px}}@media(max-width:520px){.grid,.sources{grid-template-columns:1fr}}</style>
<main><h1>全片 V2 · 继续</h1><p>2026-10-17 · FC14 256 秒演出＋2 秒演后画面 · Unity 2022.3.22f1 · 独立场景 OWM_FullFilm_v2</p>
<p class="note">预览默认静音，不自动播放。视频音轨是原始四轨合成的立体声节奏参考；VR 空间音场、拟音与远近听感请在工程中验收。点击时间只定位，不自动播放。</p>
<video id="movie" controls muted preload="metadata" playsinline poster="stills/v2_224.00.png"><source src="fullfilm-v2-review.mp4" type="video/mp4"></video><div class="jumps">__JUMPS__</div><p id="seek-status" aria-live="polite"></p>
<p><a href="fullfilm-v2-review.mp4">下载参考片</a> · <a href="OWM_FullFilm_v2.vrcw">本地 VRC 世界包</a> · <a href="contact-sheet.jpg">全片重点帧</a> · <a href="chatgpt-prompts.md">8 张手动生图提示词</a> · <a href="package.json">交付清单</a></p>
<h2>这一轮的变化</h2><table>__CHANGES__</table>
<h2>实测与待验</h2><p>完整实际 Udon 覆盖 24 场、6 组中央界面、4 个失效状态、11 个软件状态；稳定缓冲窗口中的八源最大差 __SPREAD__ 采样。远去段监听器数字输出保持非零。Windows 输出测试时静音。</p>
<p>头显双眼观感、真实多人迟到加入和人耳空间听感仍需实机验收。8 张用户手动生成图尚未替换，不影响当前演出运行。早期网站图片含本地离线重渲染，完整度各异，不能统称为当年电脑原始截图。</p><ul>__REPORTS__</ul>
<h2>同刻对照</h2><p>同一观察相机、相同时间，左侧 V1、右侧 V2；可打开大图查看。</p><div class="grid">__COMPARE__</div><h2>重点画面</h2><p>下图为同一 V2 场景的编辑器相机采样；点击打开原尺寸，时间按钮定位连续预览。</p><div class="grid">__FRAMES__</div>
<h2>素材与出处</h2><p>128 张不同源图，93 个来源页；其中本轮下载 69 张。页面片段、原媒体、原创复原分别登记。完整日期类型、哈希、截图时间和质量限制见 <a href="source-audit.json">来源核验</a>；真实软件截屏见 <a href="software-captures.json">软件素材清单</a>。</p><input id="filter" placeholder="筛选名称或类别，例如：天涯、字幕、软件、中文" aria-label="筛选素材"><div class="sources">__SOURCES__</div></main>
<script>const video=document.getElementById('movie');video.muted=true;document.querySelectorAll('[data-time]').forEach(b=>b.addEventListener('click',()=>{video.currentTime=Number(b.dataset.time);document.getElementById('seek-status').textContent='已定位 '+b.textContent+'。';video.scrollIntoView({behavior:'smooth',block:'center'});}));document.getElementById('filter').addEventListener('input',e=>{const q=e.target.value.toLowerCase();document.querySelectorAll('.source').forEach(c=>c.hidden=!c.dataset.search.toLowerCase().includes(q));});</script></html>'''
    return template.replace("__JUMPS__", jumps).replace("__CHANGES__", change_rows).replace("__SPREAD__", str(live["maxConfirmedEightSourceSpreadSamples"])).replace("__REPORTS__", links).replace("__FRAMES__", frames).replace("__COMPARE__", "".join(comparisons)).replace("__SOURCES__", "".join(sources))


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    reports = evidence()
    stills, atlas = images()
    document = ROOT/"art/prompts/fullfilm-v2-chatgpt-prompts.md"
    shutil.copy2(document, OUT/"chatgpt-prompts.md")
    shutil.copy2(ROOT/"data/fullfilm-v2-integration-source-audit.json", OUT/"source-audit.json")
    shutil.copy2(ROOT/"data/fullfilm-v2-software.json", OUT/"software-captures.json")
    page = render_page(reports, stills, atlas)
    (OUT/"index.html").write_text(page)
    package = dict(success=True, revision="V2", scene="Assets/Scenes/OWM_FullFilm_v2.unity", duration=256, review_duration=258,
        frames=6450, default_muted=True, autoplay=False, source_count=atlas["source_count"], source_pages=atlas["source_page_count"],
        new_source_count=atlas["new_downloaded_sources"], stills=len(stills), scene_sha256=reports["final-editor-state"]["sceneSha256"],
        movie_sha256=sha(OUT/"fullfilm-v2-review.mp4"), bundle_sha256=sha(OUT/"OWM_FullFilm_v2.vrcw"), human_images_pending=8,
        known_limits=["HMD and real multiplayer have not been tested", "Spatial sound has digital listener checks but no human listening certification", "Browser audio is an original four-stem stereo reference, not a VR audio capture", "Historical archive images vary in completeness; see source-audit.json"])
    (OUT/"package.json").write_text(json.dumps(package, ensure_ascii=False, indent=2)+"\n")
    print(json.dumps(dict(success=True, review=str(OUT/"index.html"), **{k: package[k] for k in ("frames", "stills", "source_count")}), ensure_ascii=False))


if __name__ == "__main__":
    main()
