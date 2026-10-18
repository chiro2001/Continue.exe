#!/usr/bin/env python3
"""Assemble the V5 review page: film, V4/V5 comparisons, module evidence and real validation."""
import hashlib
import html
import json
import shutil
import subprocess
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
TMP = Path.home() / "tmp/vrc-old-web-map/episode123-v5"
V4 = Path.home() / "tmp/vrc-old-web-map/episode123-v4"
OUT = TMP / "review"
REQUIRED = ["interaction", "live-validation", "sdk-build", "sdk-bundle-inspection", "persistence", "text-space",
            "keyline-visibility", "atmosphere-validate", "signal-validate", "identity-validate", "foley-validate", "cast-validate"]
PAIRS = [0.96, 5.6, 34.56, 44.16, 55.36, 66.4, 74.88, 85.12, 89.6, 105.6, 118.8, 129.28]


def read(path):
    return json.loads(path.read_text())


def nearest_v4(t):
    frames = [p for p in sorted((V4 / "frames").glob("v4_*.png")) if p.stem[3:].replace(".", "").isdigit()]
    return min(frames, key=lambda p: abs(float(p.stem[3:]) - t))


def main():
    for name in REQUIRED:
        report = read(TMP / "reports" / (name + ".json"))
        if report.get("success") is not True:
            raise RuntimeError("Incomplete validation: " + name)
    movie = OUT / "episode123-v5-review.mp4"
    probe = json.loads(subprocess.check_output(["ffprobe", "-v", "error", "-show_format", "-show_streams", "-of", "json", str(movie)]))
    assert abs(float(probe["format"]["duration"]) - 135.04) < .1
    (OUT / "ffprobe.json").write_text(json.dumps(probe, ensure_ascii=False, indent=2))
    for folder in ["stills", "compare", "reports", "runtime"]:
        (OUT / folder).mkdir(exist_ok=True, parents=True)
    for report in (TMP / "reports").glob("*.json"):
        if not report.name.startswith("cli-") and "failure" not in report.name:
            shutil.copy2(report, OUT / "reports" / report.name)
    frames = sorted((TMP / "frames").glob("v5_*.png"))
    for frame in frames:
        shutil.copy2(frame, OUT / "stills" / frame.name)
    if (TMP / "runtime").exists():
        shutil.copytree(TMP / "runtime", OUT / "runtime", dirs_exist_ok=True)
    pairs = []
    for t in PAIRS:
        v5 = TMP / "frames" / f"v5_{t:06.2f}.png"
        v4 = nearest_v4(t)
        if not v5.exists():
            continue
        a, b = Image.open(v4).convert("RGB").resize((800, 450)), Image.open(v5).convert("RGB").resize((800, 450))
        sheet = Image.new("RGB", (1600, 450))
        sheet.paste(a, (0, 0)); sheet.paste(b, (800, 0))
        name = f"compare_{t:06.2f}.jpg"
        sheet.save(OUT / "compare" / name, quality=88)
        pairs.append((t, name))
    sdk = TMP / "OWM_Episode123_v5.vrcw"
    assert sdk.stat().st_size > 100000
    shutil.copy2(sdk, OUT / sdk.name)
    text = read(ROOT / "data/episode123-v5-text.json")
    live = read(TMP / "reports/live-validation.json")
    foley = read(ROOT / "data/episode123-v5-foley.json")
    score = read(ROOT / "data/episode123-v4-score.json")
    nav = "".join(f'<button onclick="jump({s["start"]})">{int(s["start"]//60):02}:{int(s["start"]%60):02}　{html.escape(s["title"])}</button>' for s in score["scenes"])
    compare = "".join(f'<a href="compare/{n}"><img loading="lazy" src="compare/{n}"><span>{t:.2f} 秒 · 左 V4 / 右 V5</span></a>' for t, n in pairs)
    stills = "".join(f'<a href="stills/{p.name}"><img loading="lazy" src="stills/{p.name}"><span>{html.escape(p.stem[3:])}</span></a>' for p in frames)
    actual = "".join(f'<a href="runtime/{p.name}"><img loading="lazy" src="runtime/{p.name}"><span>{html.escape(p.stem)}</span></a>' for p in sorted((TMP / "runtime").glob("*.png")))
    keylines = "".join(f"<li>{k['start']:.1f} 秒　{html.escape(k['text'])}</li>" for k in text["keylines"])
    links = " · ".join(f'<a href="reports/{n}.json">{l}</a>' for n, l in [
        ("interaction", "开始、结束及重播"), ("live-validation", "完整 Udon 播放"), ("keyline-visibility", "关键句遮挡渲染检查"),
        ("atmosphere-validate", "天穹/星场/地面/屏幕光"), ("signal-validate", "荧光示波器与 tracker 谱面"), ("identity-validate", "cracktro 窗与署名环"),
        ("foley-validate", "拟音事件与发声点"), ("cast-validate", "人物近场与动作"), ("persistence", "保存重开与 V4 保护"), ("sdk-build", "SDK 本地构建")])
    css = """body{margin:0;background:#0d1419;color:#e0e7e8;font:16px/1.65 system-ui,sans-serif}main{max-width:1200px;margin:auto;padding:24px}h1{font-size:28px}p,li{color:#b9cdd0}video{display:block;width:100%;background:#000}button{border:1px solid #395364;background:#1d3040;color:#e9eff0;padding:9px 12px;border-radius:5px;cursor:pointer}nav{display:flex;gap:8px;flex-wrap:wrap;margin:16px 0}a{color:#95d9dd}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(250px,1fr));gap:14px}.wide{grid-template-columns:1fr}.grid img{width:100%;object-fit:contain;background:#05090c}.grid span{display:block;font-size:13px;color:#9cb5be}details{margin:24px 0}summary{cursor:pointer;font-size:21px}.note{padding:12px 18px;background:#18272f;border-left:3px solid #80bac2}"""
    page = f"""<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>前三段 V5 验收</title><style>{css}</style><main>
<h1>前三段 · V5：质感、贯穿符号与声音层</h1><p>原曲 133.12 秒 · 在 V4 内容（374 项素材、580 次事件）之上完善画面、文字与声音。方案见 spec/42。</p>
<video id="film" controls preload="metadata" src="episode123-v5-review.mp4" poster="stills/v5_055.36.png"></video><nav>{nav}</nav>
<p><b>画面：</b>按段落变化的天穹色彩剧本；三层星场，旅行时拉出星流；房间撤去后出现稳定的微光网格地面；焦点图背后有光晕，并把主色投到人物和地面。</p>
<p><b>注册机身份：</b>等待/结束时是 cracktro 小窗（像素 EDGE 标识、铜条星场、正弦滚动字）。30.72 秒起，观众上方出现彩色 greetz 署名环，按劳动角色致谢，条件段插入灰色中断。示波器改为四色荧光余辉；左右两块 tracker 谱面按 80 ms 行滚动真实 FC14 音符。</p>
<p><b>文字：</b>关键句 15→8 句，移到视线下方的空地面区域；弹幕 {len(text['comments'])} 句全部不重复，按论坛、弹幕、教程、Hub、条件变化分期。</p>
<ol>{keylines}</ol>
<p><b>声音：</b>新增 {len(foley.get('clips', []))} 个原创合成年代拟音、{len(foley.get('events', []))} 个事件，全部对齐 80 ms 行网格；四轨音乐编配与位置不变。</p>
<p><a href="{sdk.name}">本地 SDK 包</a> · {links}</p>
<p class="note">单 Editor ClientSim 真实 Udon 全片播放：{live['eventsObserved']}/{live['eventsTotal']} 事件、{live['keylinesObserved']} 条关键句，检查了实际按钮、结束重播、接收时间锚点和四轨音源。参考片为固定视点渲染，音频为固定听点立体声参考混音。头显中的双眼呈现、VR 控制器、真实多客户端同步和世界内实际响度仍待实机验收。</p>
<details open><summary>V4 / V5 同一时刻对照</summary><div class="grid wide">{compare}</div></details>
<details><summary>V5 演出画面与等待/结束状态</summary><div class="grid">{stills}</div></details>
<details><summary>真实 Udon 运行画面</summary><div class="grid">{actual}</div></details>
</main><script>function jump(t){{let v=document.getElementById('film');v.currentTime=t;v.play();v.scrollIntoView({{behavior:'smooth',block:'start'}})}}</script></html>"""
    (OUT / "index.html").write_text(page)
    meta = dict(complete=True, version=5, scene="Assets/Scenes/OWM_Episode123_v5.unity", keylines=len(text["keylines"]),
                comments=len(text["comments"]), foley_events=len(foley.get("events", [])), sdk_bytes=sdk.stat().st_size,
                sdk_sha256=hashlib.sha256(sdk.read_bytes()).hexdigest(), film_seconds=float(probe["format"]["duration"]),
                hmd_tested=False, real_multiplayer_tested=False)
    (OUT / "package.json").write_text(json.dumps(meta, ensure_ascii=False, indent=2))
    print("Review ready:", OUT / "index.html")
    print(json.dumps(meta, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
