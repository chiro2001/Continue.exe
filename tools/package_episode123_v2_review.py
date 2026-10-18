#!/usr/bin/env python3
"""Package the verified Episode 123 v2 local review bundle.

The script refuses to package while any live, interaction, typing, or SDK
build report is missing or unsuccessful. It only copies source artifacts.
"""
from __future__ import annotations

import hashlib
import html
import json
import math
import re
import shutil
import subprocess
import sys
from datetime import datetime, timezone
from fractions import Fraction
from pathlib import Path
from typing import Any

try:
    from PIL import Image
except ImportError:
    Image = None  # type: ignore[assignment]

ROOT = Path(__file__).resolve().parent.parent
TMP = Path.home() / "tmp/vrc-old-web-map/episode123-v2"
DEST = TMP / "review"
REPORTS = TMP / "reports"
ITERATION = TMP / "iteration3"
VIDEO_SOURCE = DEST / "episode123-v2-review.mp4"
PACKAGE_NAME = "OWM_Episode123_v2.vrcw"
PACKAGE_DEFAULT = TMP / PACKAGE_NAME
LIVE_REPORT_NAMES = (
    "live-v2.json", "interaction-v2.json", "typing-checks.json", "sdk-build-v2.json",
)
SHOW_DURATION = 133.12
VIDEO_DURATION = 135.04
EXPECTED_CUES = 12
EXPECTED_CUTS = 208
EXPECTED_CAPTURE_COUNT = 14
EXPECTED_SOURCE_COUNT = 42
EXPECTED_MONTAGE_IMAGE_COUNT = 57
EXPECTED_AUTHORED_ASSETS = 12
EXPECTED_SCENE_VIEW_CAPTURES = 3


class PackageError(RuntimeError):
    pass


def read_json(path: Path, label: str) -> Any:
    if not path.is_file():
        raise PackageError(f"缺少{label}：{path}")
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise PackageError(f"无法读取{label}：{path}（{exc}）") from exc


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def finite_number(value: Any) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(float(value))


def require_reports() -> dict[str, dict[str, Any]]:
    missing = [name for name in LIVE_REPORT_NAMES if not (REPORTS / name).is_file()]
    if missing:
        paths = "\n".join(f"  - {REPORTS / name}" for name in missing)
        raise PackageError("评审包需要全部通过的实测报告；缺少：\n" + paths)

    validator = ROOT / "tools/validate_episode123_v2.py"
    result = subprocess.run(
        [sys.executable, str(validator), "--reports", "--reports-dir", str(REPORTS)],
        cwd=ROOT, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
    )
    if result.returncode != 0:
        raise PackageError("v2静态/运行报告验证失败，未生成评审包：\n" + result.stdout.strip())

    reports = {name: read_json(REPORTS / name, name) for name in LIVE_REPORT_NAMES}
    sdk = reports["sdk-build-v2.json"]
    if sdk.get("success") is not True:
        raise PackageError(f"SDK构建报告失败：{sdk.get('error', sdk.get('message', 'success != true'))}")
    if sdk.get("uploaded") is not False:
        raise PackageError("SDK构建报告未明确证明包未上传（uploaded 必须为 false）")
    if not isinstance(sdk.get("path"), str) or Path(sdk["path"].replace("\\", "/")).name != PACKAGE_NAME:
        raise PackageError(f"SDK构建报告 path 必须指向 {PACKAGE_NAME}")
    if not isinstance(sdk.get("bytes"), int) or sdk["bytes"] <= 0:
        raise PackageError("SDK构建报告没有有效的本地包字节数")
    return reports


def locate_package(sdk_report: dict[str, Any]) -> Path:
    reported = sdk_report["path"]
    reported_path = Path(reported).expanduser()
    candidates = [
        reported_path,
        PACKAGE_DEFAULT,
        DEST / PACKAGE_NAME,
    ]
    expected_bytes = sdk_report["bytes"]
    for candidate in candidates:
        if candidate.is_file() and candidate.stat().st_size == expected_bytes:
            return candidate.resolve()
    checked = "\n".join(f"  - {p}" for p in candidates)
    raise PackageError(
        f"SDK 报告记录 {expected_bytes} 字节，但本地找不到匹配的 {PACKAGE_NAME}。检查过：\n{checked}"
    )


def probe_video(path: Path) -> dict[str, Any]:
    if not path.is_file():
        raise PackageError(f"缺少参考影片：{path}")
    try:
        output = subprocess.check_output(
            ["ffprobe", "-v", "error", "-show_entries", "format=duration:stream=codec_type,width,height,r_frame_rate", "-of", "json", str(path)],
            text=True, stderr=subprocess.STDOUT,
        )
        probe = json.loads(output)
    except (OSError, subprocess.CalledProcessError, json.JSONDecodeError) as exc:
        raise PackageError(f"无法检查参考影片（ffprobe）：{exc}") from exc
    duration = float(probe.get("format", {}).get("duration", "nan"))
    if not math.isfinite(duration) or abs(duration - VIDEO_DURATION) > 0.12:
        raise PackageError(f"参考影片时长应为 {VIDEO_DURATION:.2f} 秒，实际为 {duration:.3f} 秒")
    streams = probe.get("streams", [])
    video_streams = [s for s in streams if s.get("codec_type") == "video"]
    audio_streams = [s for s in streams if s.get("codec_type") == "audio"]
    if not video_streams or not audio_streams:
        raise PackageError("参考影片必须同时含视频与音轨")
    video = video_streams[0]
    if video.get("width") != 1600 or video.get("height") != 900:
        raise PackageError(f"参考影片分辨率应为 1600×900，实际为 {video.get('width')}×{video.get('height')}")
    if Fraction(video.get("r_frame_rate", "0/1")) != Fraction(25, 1):
        raise PackageError(f"参考影片帧率应为 25 fps，实际为 {video.get('r_frame_rate')}")
    return {"duration_seconds": duration, "width": video["width"], "height": video["height"], "fps": 25, "audio_streams": len(audio_streams)}


def load_source_assets() -> list[dict[str, Any]]:
    manifest_paths = [ROOT / "data/episode123-historical-images.json", ROOT / "data/episode123-software-images.json"]
    all_assets: list[dict[str, Any]] = []
    seen_ids: set[str] = set()
    for manifest_path in manifest_paths:
        doc = read_json(manifest_path, "来源图清单")
        assets = doc.get("assets")
        if not isinstance(assets, list):
            raise PackageError(f"来源图清单 assets 必须是数组：{manifest_path}")
        for asset in assets:
            if not isinstance(asset, dict):
                raise PackageError(f"来源图记录必须是对象：{manifest_path}")
            asset_id = asset.get("id")
            relpath = asset.get("path")
            if not isinstance(asset_id, str) or not isinstance(relpath, str):
                raise PackageError(f"来源图缺少 id/path：{manifest_path}")
            if asset_id in seen_ids:
                raise PackageError(f"来源图 ID 重复：{asset_id}")
            seen_ids.add(asset_id)
            source_path = (ROOT / relpath).resolve()
            try:
                source_path.relative_to(ROOT)
            except ValueError as exc:
                raise PackageError(f"来源图路径越出仓库：{relpath}") from exc
            if not source_path.is_file():
                raise PackageError(f"来源图文件缺失：{relpath}")
            if Image is None:
                raise PackageError("打包来源图需要 Pillow")
            try:
                with Image.open(source_path) as image:
                    actual_width, actual_height = image.size
                    image.verify()
            except Exception as exc:
                raise PackageError(f"来源图无法读取：{relpath}（{exc}）") from exc
            if (actual_width, actual_height) != (asset.get("width"), asset.get("height")):
                raise PackageError(f"来源图尺寸与清单不符：{relpath}，实际 {actual_width}×{actual_height}")
            if sha256_file(source_path) != asset.get("sha256"):
                raise PackageError(f"来源图 SHA-256 与清单不符：{relpath}")
            source_url = asset.get("source_url")
            if not isinstance(source_url, str) or not source_url.startswith(("http://", "https://")):
                raise PackageError(f"来源图缺少原始 URL：{asset_id}")
            trace = any(
                isinstance(asset.get(key), str) and bool(asset[key].strip())
                for key in ("date_basis", "evidence", "date_note", "source_page_date")
            )
            if not trace:
                raise PackageError(f"来源图缺少日期/来源说明：{asset_id}")
            all_assets.append({**asset, "_source_path": source_path})
    if len(all_assets) != EXPECTED_SOURCE_COUNT:
        raise PackageError(f"应有 {EXPECTED_SOURCE_COUNT} 张来源图，清单合计 {len(all_assets)} 张")
    return all_assets


def load_captures(montage: dict[str, Any]) -> list[dict[str, Any]]:
    capture_report = read_json(ITERATION / "capture-report.json", "最终截图时间码报告")
    if not isinstance(capture_report, list) or len(capture_report) != EXPECTED_CAPTURE_COUNT:
        raise PackageError(f"capture-report.json 必须包含 {EXPECTED_CAPTURE_COUNT} 个截图记录")
    cuts = montage.get("cuts", [])
    captures: list[dict[str, Any]] = []
    section_seen: dict[int, int] = {}
    for index, record in enumerate(capture_report):
        if not isinstance(record, dict) or not finite_number(record.get("time")) or not isinstance(record.get("cut"), int):
            raise PackageError(f"截图时间码记录 #{index + 1} 字段不完整")
        expected_name = f"v2_{index:02d}.png"
        recorded_path = record.get("path", "")
        filename = str(recorded_path).replace("\\", "/").rsplit("/", 1)[-1]
        if filename != expected_name:
            raise PackageError(f"截图 #{index + 1} 应为 {expected_name}，报告写的是 {filename}")
        source_path = ITERATION / expected_name
        if not source_path.is_file():
            raise PackageError(f"场面截图缺失：{source_path}")
        if Image is None:
            raise PackageError("检查截图需要 Pillow")
        try:
            with Image.open(source_path) as image:
                size = image.size
                image.verify()
        except Exception as exc:
            raise PackageError(f"场面截图无法读取：{source_path}（{exc}）") from exc
        cut_index = record["cut"]
        if not (0 <= cut_index < len(cuts)):
            raise PackageError(f"截图 {expected_name} 的 cut 越界：{cut_index}")
        cut = cuts[cut_index]
        if abs(float(cut.get("time", -1)) - float(record["time"])) > 1e-3:
            raise PackageError(f"截图 {expected_name} 时间码与 montage cut {cut_index} 不一致")
        section = cut.get("section")
        section_no = int(section) if isinstance(section, int) else -1
        section_seen[section_no] = section_seen.get(section_no, 0) + 1
        captures.append({
            "filename": expected_name,
            "source_path": source_path,
            "time_seconds": float(record["time"]),
            "cut": cut_index,
            "section": section_no,
            "label": cut.get("label", "场面截图"),
            "word": cut.get("word", ""),
            "width": size[0],
            "height": size[1],
            "supplemental": False,
        })
    seen: dict[int, int] = {}
    for capture in captures:
        section = capture["section"]
        seen[section] = seen.get(section, 0) + 1
        capture["supplemental"] = seen[section] > 1
    if set(seen) != set(range(EXPECTED_CUES)):
        raise PackageError("14张截图必须覆盖12个场面段")
    supplementals = sum(capture["supplemental"] for capture in captures)
    if supplementals != 2:
        raise PackageError(f"14张截图中应有2张补充视图，实际为{supplementals}张")
    contact = ITERATION / "contact-sheet.jpg"
    if not contact.is_file():
        raise PackageError(f"缺少最终截图接触表：{contact}")
    return captures


def timecode(seconds: float) -> str:
    minutes = int(seconds // 60)
    remainder = seconds - minutes * 60
    return f"{minutes:02d}:{remainder:05.2f}"


def safe_filename(text: str) -> str:
    return re.sub(r"[^A-Za-z0-9._-]+", "-", text).strip("-._") or "source"


def source_display(asset: dict[str, Any]) -> dict[str, Any]:
    source_url = asset.get("source_url")
    page_url = asset.get("source_page_url") or (source_url if asset.get("image_url") else None)
    image_url = asset.get("image_url") or (source_url if asset.get("source_page_url") else None)
    note_parts = []
    for key in ("real_context", "evidence", "date_note", "date_basis", "source_page_date"):
        value = asset.get(key)
        if isinstance(value, str) and value.strip() and value not in note_parts:
            note_parts.append(value.strip())
    date = asset.get("source_date")
    return {
        "title": asset["title"],
        "category": asset.get("category", ""),
        "source_date": date,
        "date_display": date if date else "未标（见来源说明）",
        "kind": asset["kind"],
        "source_url": source_url,
        "source_page_url": page_url,
        "image_url": image_url,
        "source_note": "\n".join(note_parts),
    }


def build_gallery_cards(assets: list[dict[str, Any]], copied: list[dict[str, Any]]) -> str:
    cards = []
    for index, (asset, output) in enumerate(zip(assets, copied), 1):
        info = source_display(asset)
        title = html.escape(str(info["title"]))
        date = html.escape(str(info["date_display"]))
        kind = html.escape(str(info["kind"]))
        category = html.escape(str(info["category"]))
        local = html.escape(output["relative_path"], quote=True)
        page_link = html.escape(str(info["source_page_url"] or info["source_url"]), quote=True)
        image_link = info["image_url"]
        image_link_html = f'<a href="{html.escape(str(image_link), quote=True)}" target="_blank" rel="noopener">原图链接</a>' if image_link else ""
        note = html.escape(str(info["source_note"]))
        cards.append(
            f'<article class="source-card"><img src="{local}" alt="{title}" loading="lazy">'
            f'<div class="source-copy"><span class="source-number">{index:02d}</span>'
            f'<span class="category">{category}</span><h3>{title}</h3>'
            f'<p><b>日期：</b>{date}<br><b>性质：</b>{kind}</p>'
            f'<p><a href="{page_link}" target="_blank" rel="noopener">原始来源</a> {image_link_html}</p>'
            f'<details><summary>来源说明</summary><p>{note}</p><small>{html.escape(asset["path"])}</small></details></div></article>'
        )
    return "\n".join(cards)


def build_still_cards(captures: list[dict[str, Any]]) -> str:
    cards = []
    for capture in captures:
        name = capture["filename"]
        label = html.escape(str(capture["label"]))
        word = html.escape(str(capture["word"]))
        supplemental = " · 补充视图" if capture["supplemental"] else ""
        cards.append(
            f'<button class="still-card" data-time="{capture["time_seconds"]:.2f}">'
            f'<img src="stills/{html.escape(name, quote=True)}" alt="{label}" loading="lazy">'
            f'<span>{timecode(capture["time_seconds"])} · 场面 {capture["section"] + 1}{supplemental}</span>'
            f'<strong>{label}</strong><small>{word}</small></button>'
        )
    return "\n".join(cards)


def write_index(dest: Path, capture_cards: str, gallery_cards: str, live: dict[str, Any], interaction: dict[str, Any], typing: dict[str, Any], sdk: dict[str, Any], movie_info: dict[str, Any]) -> None:
    phases: dict[str, set[float]] = {"Kipfel": set(), "Nemesis": set()}
    for row in typing.get("rows", []):
        if isinstance(row, dict) and row.get("appearanceId") in phases and finite_number(row.get("phase")):
            phases[row["appearanceId"]].add(round(float(row["phase"]), 6))
    interaction_ok = all(interaction.get(key) is True for key in ("rayShapeHit", "pointerClickPlayed", "panelHiddenWhilePlaying", "pauseIgnored", "lateJoinRebuilt", "endPanelShown"))
    source_count = EXPECTED_SOURCE_COUNT
    authored_count = EXPECTED_AUTHORED_ASSETS
    sceneview_count = EXPECTED_SCENE_VIEW_CAPTURES
    template = """<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">
<title>前三段 v2 · 本地验收</title><style>
*{box-sizing:border-box}body{margin:0;background:#111a25;color:#eef2f2;font:16px/1.65 system-ui,"Noto Sans CJK SC",sans-serif}main{max-width:1240px;margin:auto;padding:36px 22px 70px}h1{font-size:34px;line-height:1.2;margin:5px 0 12px}.eyebrow{color:#e5bf6c;letter-spacing:.13em;font-size:13px}.lead{color:#c4d2d9}.chips,.chapters{display:flex;flex-wrap:wrap;gap:10px;margin:18px 0}.chip{background:#253849;border:1px solid #466074;padding:8px 12px;border-radius:999px;font-size:14px}video{width:100%;background:#000;border:1px solid #435867;border-radius:9px}.chapters button,.still-card{color:inherit;background:#213546;border:1px solid #496174;border-radius:7px;cursor:pointer;font:inherit}.chapters button{padding:9px 16px}.chapters button:hover,.still-card:hover{background:#2e4a5e}.review-note{padding:18px 20px;background:#192b39;border-left:3px solid #e5bf6c;margin:20px 0 28px}.review-note a,a{color:#f0cf83}.grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px}.still-card{padding:0;text-align:left;overflow:hidden}.still-card img{width:100%;display:block;background:#0b1018}.still-card span,.still-card strong,.still-card small{display:block;padding:7px 12px 0}.still-card span{color:#d4b968}.still-card small{padding-bottom:12px;color:#b7c7d1}.still-card strong{font-size:15px}.gallery{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px}.source-card{background:#1a2b38;border:1px solid #405769;border-radius:8px;overflow:hidden}.source-card img{width:100%;height:210px;object-fit:contain;background:#0d141d;display:block}.source-copy{padding:10px 12px 14px}.source-number{font-weight:700;color:#e5bf6c;margin-right:8px}.category{font-size:12px;color:#9eb8c7}.source-copy h3{font-size:16px;line-height:1.35;margin:7px 0}.source-copy p{margin:7px 0;color:#c7d5db;font-size:13px;word-break:break-word}.source-copy details{font-size:13px;color:#c4d2d9}.source-copy summary{cursor:pointer}.source-copy small{color:#91a9b8;word-break:break-all}section{margin-top:32px}details.gallery-shell{margin-top:26px}summary{cursor:pointer}table{border-collapse:collapse;width:100%;font-size:14px}td{padding:9px;border-bottom:1px solid #394e5e;vertical-align:top}.foot{color:#aabac2;font-size:13px;margin-top:28px}@media(max-width:820px){.grid,.gallery{grid-template-columns:repeat(2,minmax(0,1fr))}}@media(max-width:560px){main{padding:22px 13px}.grid,.gallery{grid-template-columns:1fr}h1{font-size:27px}}
</style><main><div class=\"eyebrow\">VRCHAT · EPISODE 123 · V2</div><h1>接入，繁荣，条件改变。</h1>
<p class=\"lead\">本地评审包 · 演出133.12秒 · 12个场面 · 0.64秒主图切换</p>
<div class=\"chips\"><span class=\"chip\">42张来源图</span><span class=\"chip\">12张原创示意</span><span class=\"chip\">3张当前SceneView自截</span><span class=\"chip\">4×16 实际FFT频带</span><span class=\"chip\">双手间距通过</span></div>
<video id=\"film\" src=\"episode123-v2-review.mp4\" poster=\"stills/v2_00.png\" controls preload=\"metadata\"></video>
<div class=\"chapters\"><button data-time=\"0\">01 接入 · 0:00</button><button data-time=\"30.72\">02 繁荣 · 0:30.72</button><button data-time=\"102.4\">03 条件改变 · 1:42.40</button></div>
<p>视频以固定相机离线采样并配参考混音，不是实时头显录屏。末尾留1.92秒提示，方便完整播放与检查收尾。</p>
<div class=\"review-note\"><b>Unity工程</b><br>场景：<code>Assets/Scenes/OWM_Episode123_v2.unity</code>。原场景保留：<code>Assets/Scenes/OWM_Episode123.unity</code>。<br><a href=\"OWM_Episode123_v2.vrcw\" download>下载本地 VRChat 世界包（OWM_Episode123_v2.vrcw）</a><br>SDK本地构建通过，报告标记 uploaded=false。没有上传世界。</div>
<section><h2>14张场面截图</h2><p>按第二轮 capture-report 时间码排列；12个粗场面另有2个补充视角。点击缩略图会跳到影片对应时间。</p><div class=\"grid\">__STILL_CARDS__</div><p><a href=\"contact-sheet.jpg\">查看场面接触表</a> · <a href=\"checks/capture-report.json\">截图时间码报告</a></p></section>
<section><h2>验证记录</h2><table><tr><td>真实Udon / ClientSim 连续运行</td><td>__LIVE_CUES__个提示、__LIVE_CUTS__次切换；4轨同步最大差__INTERTRACK__ samples；最大蒙太奇耗时__MONTAGE_MS__ ms；结束停止状态__END_STOPPED__。</td></tr><tr><td>交互</td><td>射线命中VRCUiShape、pointerClick、播放时隐藏面板、暂停忽略、收到锚点后的本地状态恢复、结束面板：__INTERACTION__。</td></tr><tr><td>双手打字动作</td><td>Kipfel __KIPFEL_PHASES__相位；Nemesis __NEMESIS_PHASES__相位；报告最小手间距__MIN_SEPARATION__；每行 crossed=false。</td></tr><tr><td>FFT</td><td>4声道×16频带，数据预计算自原轨，不使用随机跳动柱。</td></tr></table>
<p>真实Udon/ClientSim连续运行和程序pointerClick/物理ray已检查。迟到加入只验证本地收到锚点后的状态重建；没有声称真实多客户端网络传输或真实头显体验。</p></section>
<section><details class=\"gallery-shell\"><summary><h2 style=\"display:inline\">展开42张真实来源图</h2> · 每张含日期、性质、来源链接与证据说明</summary><div class=\"gallery\">__SOURCE_CARDS__</div></details></section>
<section><h2>素材留存与来源</h2><p>详细逐图记录见<a href=\"sources-provenance.md\">逐项图像来源</a>。历史网页回放使用<a href=\"https://web.archive.org/\" target=\"_blank\" rel=\"noopener\">Internet Archive Wayback Machine</a>；旧软件条目来自<a href=\"https://archive.org/\" target=\"_blank\" rel=\"noopener\">Internet Archive</a>；单图说明与上传时间参考<a href=\"https://commons.wikimedia.org/\" target=\"_blank\" rel=\"noopener\">Wikimedia Commons</a>。未标截图日期的后世实机图保持未标。</p></section>
<p class=\"foot\">本地包只用于本地验收。完整报告副本位于 <a href=\"checks/\">checks/</a>。影片、来源图片和场景截屏分别保存在当前目录中。</p></main>
<script>const film=document.querySelector('#film');document.querySelectorAll('[data-time]').forEach(b=>b.addEventListener('click',()=>{film.currentTime=Number(b.dataset.time);film.play().catch(()=>{});film.scrollIntoView({behavior:'smooth',block:'center'})}));</script></html>"""
    replacements = {
        "__STILL_CARDS__": capture_cards,
        "__SOURCE_CARDS__": gallery_cards,
        "__LIVE_CUES__": str(live.get("cuesSeen")),
        "__LIVE_CUTS__": str(live.get("cutsSeen")),
        "__INTERTRACK__": f"{float(live['maxIntertrackSamples']):.0f}",
        "__MONTAGE_MS__": f"{float(live['maxMontageMs']):.3f}",
        "__END_STOPPED__": "是" if live.get("endStopped") else "否",
        "__INTERACTION__": "全部通过" if interaction_ok else "有未通过项",
        "__KIPFEL_PHASES__": str(len(phases["Kipfel"])),
        "__NEMESIS_PHASES__": str(len(phases["Nemesis"])),
        "__MIN_SEPARATION__": f"{float(typing['minSeparation']):.4f}",
    }
    page = template
    for marker, replacement in replacements.items():
        page = page.replace(marker, html.escape(replacement) if marker not in {"__STILL_CARDS__", "__SOURCE_CARDS__"} else replacement)
    (dest / "index.html").write_text(page, encoding="utf-8")


def write_acceptance(dest: Path, reports: dict[str, dict[str, Any]], movie_info: dict[str, Any], package_path: Path, source_assets: list[dict[str, Any]], captures: list[dict[str, Any]]) -> None:
    live = reports["live-v2.json"]
    interaction = reports["interaction-v2.json"]
    typing = reports["typing-checks.json"]
    sdk = reports["sdk-build-v2.json"]
    phases = {"Kipfel": set(), "Nemesis": set()}
    for row in typing["rows"]:
        if row.get("appearanceId") in phases:
            phases[row["appearanceId"]].add(round(float(row["phase"]), 6))
    capture_lines = "\n".join(
        f"- `{Path(c['filename']).stem}` · {timecode(c['time_seconds'])} · section {c['section'] + 1} · cut {c['cut']} · {c['label']}"
        + ("（补充视角）" if c["supplemental"] else "")
        for c in captures
    )
    report_links = "\n".join(f"- [`{name}`](checks/{name})" for name in LIVE_REPORT_NAMES)
    spec_link = "[逐项图像来源](sources-provenance.md)"
    content = f"""# 前三段 v2 本地验收包

## 查看入口

- [`index.html`](index.html)：连续参考影片、章节跳转、14张场景截屏和可展开的42张来源图图库。
- [场面接触表](contact-sheet.jpg)
- [参考影片](episode123-v2-review.mp4)：{movie_info['duration_seconds']:.2f}秒，1600×900，25 fps；演出内容133.12秒，末尾含1.92秒验收提示。
- [本地VRChat包](OWM_Episode123_v2.vrcw)：{sdk['bytes']}字节，SDK构建报告明确 `uploaded=false`。
- Unity工程场景：`Assets/Scenes/OWM_Episode123_v2.unity`。原场景仍为 `Assets/Scenes/OWM_Episode123.unity`。

影片是固定相机离线采样并配参考混音，不是实时头显录屏。章节入口：接入 0秒、繁荣 30.72秒、条件改变 102.4秒。

## 内容与自动检查

- 蒙太奇：208次0.64秒主图/短字交接，0.32秒局部闪回，四声道×16频带真实FFT数据；包括EDGE式3D点阵星场、射线Canvas/VRCUiShape、播放时隐藏面板、暂停忽略、收到锚点后的本地状态恢复和双手分开打字动作。
- 图片：42张真实来源图、12张原创示意、3张当前SceneView相机截屏，共57张；原图、日期、性质、来源链接和说明随图库一同保留。
- ClientSim连续运行：{live['cuesSeen']}个提示、{live['cutsSeen']}次切换；四轨最大持续差{live['maxIntertrackSamples']}个采样（上限960），最大蒙太奇耗时{float(live['maxMontageMs']):.3f}ms；结束停止状态为{live['endStopped']}。
- 交互：射线形状命中、程序pointerClick、播放时隐藏面板、暂停忽略、锚点后迟到状态重建、结束面板显示均为通过。
- 打字动作：Kipfel 有{len(phases['Kipfel'])}个相位，Nemesis 有{len(phases['Nemesis'])}个相位；最小手间距{float(typing['minSeparation']):.4f}，所有记录均未交叉。
- SDK本地构建成功，包未上传。报告包含构建字节数和本地输出路径。

## 检查范围与边界

实际运行检查使用真实 Udon/ClientSim 连续运行、程序 `pointerClick` 和物理射线。迟到加入只验证本地收到锚点后的状态重建；没有测真实多客户端网络传输。没有测真实头显体验。

参考影片由Unity固定相机离线采样并配参考混音，不能当作实时头显录屏；视频长度135.04秒，演出本体133.12秒。

### 第二轮场面截图时间码

{capture_lines}

### 实测记录

{report_links}

截图时间码原表：[capture-report.json](checks/capture-report.json)。

## 图片来源留存

来源图片已复制到 `sources/`，图库逐张保留来源日期、图像性质、证据说明和原始链接。完整逐项说明见 {spec_link}。

本包使用的主要留存服务： [Internet Archive Wayback Machine](https://web.archive.org/)、[Internet Archive 条目](https://archive.org/) 和 [Wikimedia Commons](https://commons.wikimedia.org/)。Wayback按固定时间戳回放并确认实际捕获日期；Archive.org用于旧软件/文件条目；Commons用于单图作者、上传日期与说明。未标截图日期的后世系统截图不会被写成同期拍摄。
"""
    (dest / "acceptance.md").write_text(content, encoding="utf-8")


def package() -> dict[str, Any]:
    reports = require_reports()
    sdk = reports["sdk-build-v2.json"]
    package_source = locate_package(sdk)
    movie_info = probe_video(VIDEO_SOURCE)
    source_assets = load_source_assets()
    montage = read_json(ROOT / "data/episode123-v2-montage.json", "v2蒙太奇数据")
    if len(montage.get("images", [])) != EXPECTED_MONTAGE_IMAGE_COUNT:
        raise PackageError(f"蒙太奇应有 {EXPECTED_MONTAGE_IMAGE_COUNT} 张图，实际 {len(montage.get('images', []))} 张")
    if len(montage.get("cuts", [])) != EXPECTED_CUTS:
        raise PackageError(f"蒙太奇应有 {EXPECTED_CUTS} 次主切换，实际 {len(montage.get('cuts', []))} 次")
    authored_count = sum(row.get("kind") == "original-demonstration" for row in montage["images"] if isinstance(row, dict))
    sceneview_count = sum(row.get("kind") == "current-project-self-capture" for row in montage["images"] if isinstance(row, dict))
    if (authored_count, sceneview_count) != (EXPECTED_AUTHORED_ASSETS, EXPECTED_SCENE_VIEW_CAPTURES):
        raise PackageError(
            f"蒙太奇来源构成应为12张原创示意+3张SceneView自截，实际为{authored_count}+{sceneview_count}"
        )
    captures = load_captures(montage)
    contact_sheet = ITERATION / "contact-sheet.jpg"
    if not (contact_sheet.is_file() and Image is not None):
        raise PackageError(f"缺少可读取的第二轮 contact-sheet.jpg：{contact_sheet}")
    try:
        with Image.open(contact_sheet) as image:
            contact_size = image.size
            image.verify()
    except Exception as exc:
        raise PackageError(f"接触表无法读取：{exc}") from exc

    live = reports["live-v2.json"]
    interaction = reports["interaction-v2.json"]
    typing = reports["typing-checks.json"]
    if not all(report.get("success") is True for report in (live, interaction, typing, sdk)):
        raise PackageError("至少一份实测报告 success != true")

    DEST.mkdir(parents=True, exist_ok=True)
    still_dir = DEST / "stills"
    source_dir = DEST / "sources"
    checks_dir = DEST / "checks"
    still_dir.mkdir(exist_ok=True)
    source_dir.mkdir(exist_ok=True)
    checks_dir.mkdir(exist_ok=True)
    if VIDEO_SOURCE.resolve() != (DEST / VIDEO_SOURCE.name).resolve():
        shutil.copy2(VIDEO_SOURCE, DEST / VIDEO_SOURCE.name)
    if package_source.resolve() != (DEST / PACKAGE_NAME).resolve():
        shutil.copy2(package_source, DEST / PACKAGE_NAME)
    shutil.copy2(contact_sheet, DEST / "contact-sheet.jpg")
    shutil.copy2(ITERATION / "capture-report.json", checks_dir / "capture-report.json")
    copied_sources: list[dict[str, Any]] = []
    source_metadata: list[dict[str, Any]] = []
    for index, asset in enumerate(source_assets, 1):
        suffix = asset["_source_path"].suffix.lower()
        filename = f"{index:02d}-{safe_filename(asset['id'])}{suffix}"
        target = source_dir / filename
        shutil.copy2(asset["_source_path"], target)
        display = source_display(asset)
        output = {
            "number": index,
            "local_path": f"sources/{filename}",
            "source_path": asset["path"],
            "sha256": sha256_file(target),
            "width": asset["width"],
            "height": asset["height"],
            **display,
        }
        copied_sources.append({"relative_path": f"sources/{filename}"})
        source_metadata.append(output)
    for name in LIVE_REPORT_NAMES:
        shutil.copy2(REPORTS / name, checks_dir / name)

    still_metadata = []
    for capture in captures:
        shutil.copy2(capture["source_path"], still_dir / capture["filename"])
        still_metadata.append({
            "path": f"stills/{capture['filename']}",
            "time_seconds": capture["time_seconds"],
            "timecode": timecode(capture["time_seconds"]),
            "cut": capture["cut"],
            "section": capture["section"],
            "label": capture["label"],
            "word": capture["word"],
            "supplemental": capture["supplemental"],
            "width": capture["width"],
            "height": capture["height"],
        })

    provenance = ["# 图像来源", "", "本文件与图库对应；年代界面、后世旧系统截图和存档图各自按性质标记。", ""]
    for item in source_metadata:
        provenance.extend([f"## {item['number']:02d} {item['title']}", "", f"[本地图像]({item['local_path']}) · [原始来源]({item['source_url']})", "", f"日期：{item['date_display']}。性质：{item['kind']}。", "", item['source_note'], ""])
    (DEST / "sources-provenance.md").write_text("\n".join(provenance), encoding="utf-8")
    gallery_html = build_gallery_cards(source_assets, copied_sources)
    still_html = build_still_cards(captures)
    write_index(DEST, still_html, gallery_html, live, interaction, typing, sdk, movie_info)
    write_acceptance(DEST, reports, movie_info, package_source, source_assets, captures)

    package_dest = DEST / PACKAGE_NAME
    movie_dest = DEST / VIDEO_SOURCE.name
    manifest = {
        "status": "packaged-after-successful-reports",
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "entry": "index.html",
        "acceptance": "acceptance.md",
        "reference_video": {
            "path": movie_dest.name,
            "sha256": sha256_file(movie_dest),
            **movie_info,
            "show_duration_seconds": SHOW_DURATION,
            "capture_mode": "Unity fixed-camera offline sampling with reference mix; not a live headset recording",
        },
        "local_world_package": {
            "path": package_dest.name,
            "source_report_path": sdk["path"],
            "bytes": package_dest.stat().st_size,
            "sha256": sha256_file(package_dest),
            "uploaded": False,
        },
        "scene": "Assets/Scenes/OWM_Episode123_v2.unity",
        "preserved_original_scene": "Assets/Scenes/OWM_Episode123.unity",
        "coverage": {
            "historical_source_images": len(source_metadata),
            "authored_demonstrations": EXPECTED_AUTHORED_ASSETS,
            "current_sceneview_captures": EXPECTED_SCENE_VIEW_CAPTURES,
            "montage_images": len(montage["images"]),
            "main_cuts": len(montage["cuts"]),
            "pulse_seconds": 0.64,
            "local_flashback_seconds": 0.32,
        },
        "captures": still_metadata,
        "source_images": source_metadata,
        "reports": {name: {"path": f"checks/{name}", "success": reports[name].get("success")} for name in LIVE_REPORT_NAMES},
        "limits": {
            "late_join": "local reconstruction after receiving the playback anchor only",
            "network": "no real multi-client transport test",
            "headset": "no real headset experience test",
        },
    }
    (DEST / "review-manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return manifest


def main() -> int:
    try:
        manifest = package()
    except PackageError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1
    print(f"Review package created: {DEST}")
    print(f"{manifest['coverage']['historical_source_images']} source images; {manifest['coverage']['montage_images']} montage images; {manifest['coverage']['main_cuts']} cuts")
    print(f"Entry: {DEST / 'index.html'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
