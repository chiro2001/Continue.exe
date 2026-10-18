#!/usr/bin/env python3
"""Prepare reviewed, cropped VRChat-history frames and silent clips for V4.

The high-fidelity downloaded source stays in the task temp directory. This
script exports only selected PNG frames and short, silent H.264 excerpts.
"""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import shutil
import subprocess
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont


REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SOURCE = Path.home() / "tmp/vrc-old-web-map/fullfilm-v4/history-video/source-full.mp4"
REFERENCE_DIR = REPO_ROOT / "art/reference/fullfilm-v4-video"
EXPORT_DIR = REPO_ROOT / "art/export/fullfilm-v4-video"
DATA_PATH = REPO_ROOT / "data/fullfilm-v4-history-video.json"

SOURCE_WIDTH = 1280
SOURCE_HEIGHT = 720
# Removes the top-left Bilibili overlay and the lower-right educational-use
# overlay while retaining a 16:9 image. No pixels are inpainted or invented.
# 992×558 is exactly 16:9; it clears the upper-left and lower-right overlays.
CROP = (260, 0, 1252, 558)
OUTPUT_SIZE = (1280, 720)


def frame(seconds, slug, category, description, depicted_date=None, confidence="unknown", evidence="画面没有年份。", slot="VRC 77.44–79.36 的年代未核候选。", masks=None, stable_id=None):
    return {
        "id": stable_id,
        "seconds": float(seconds),
        "slug": slug,
        "category": category,
        "description": description,
        "depicted_date": depicted_date,
        "date_confidence": confidence,
        "date_evidence": evidence,
        "mv_slot": slot,
        "privacy_masks": masks or [],
    }


FRAMES = [
    frame(4, "legacy-room-list", "legacy-ui", "VR Forums 房间列表与 Player Name、Create、Join 控件。", "2014 chapter context", "medium", "紧接片内 2014 年份卡；单帧自身没有日期。", masks=[[566, 177, 716, 224]], stable_id="F01"),
    frame(8, "legacy-social-avatar", "social-world", "旧式室内世界中的 T-pose 头像与 Channel Menu。", "2014 chapter context", "medium", "位于 2014 年份卡之后、2015 年份卡之前；具体采集日未标。", masks=[[600, 60, 758, 106]], stable_id="F02"),
    frame(20, "stereo-vr-view", "vr-gameplay", "双目视野中的第一人称社交世界画面。", "2014 chapter context", "medium", "紧邻片内 2015 年卡之前；本画面未独立标日。", masks=[[295, 187, 465, 232], [865, 187, 1035, 232]], stable_id="F03"),
    frame(1, "year-2014-card", "year-card", "章节卡直接标出 2014。", "2014", "high", "画面中央直接显示“2014”。", stable_id="F04"),
    frame(24, "avatar-profile-panel", "legacy-ui", "旧式头像/个人资料创建面板。", "2015 chapter context", "medium", "紧接片内 2015 年份卡；具体采集日未标。", stable_id="F05"),
    frame(21, "year-2015-card", "year-card", "章节卡直接标出 2015。", "2015", "high", "画面中央直接显示“2015”。", stable_id="F06"),
    frame(44, "luminous-vr-game", "live-gameplay", "高亮特效的 VR 游戏世界与玩家视角。", "2015 chapter context", "medium", "位于片内 2015 年份卡之后、2016 卡之前。", stable_id="F07"),
    frame(51, "all-rooms-menu", "legacy-ui", "All Rooms / My Rooms 房间浏览界面；遮蔽右上角聊天用户名。", "2015 chapter context", "medium", "位于片内 2015 年章节范围；画面本身无年份。", masks=[[780, 0, 1260, 190]], stable_id="F08"),
    frame(85, "hub-home-interior", "hub-home", "室内 Hub/Home 风格场景与到访头像；名牌内用户名已像素化。", "2016 chapter context", "medium", "位于片内 2016 年份卡之后、2017 卡之前；Home/Hub 归类据场景外观。", masks=[[200, 275, 430, 322], [448, 260, 620, 314], [785, 242, 900, 294], [625, 393, 815, 450], [745, 347, 860, 390]], stable_id="F09"),
    frame(56, "in-world-chat-screen", "in-world-ui", "世界内留言屏幕与观看者视角。", "2015 chapter context", "medium", "位于片内 2015 年章节范围；画面本身无年份。", masks=[[260, 0, 1252, 112]], stable_id="F10"),
    frame(70, "particle-music-world", "social-world", "紫色几何/音频视觉世界。", "2016 chapter context", "medium", "位于片内 2016 年份卡之后、2017 卡之前。", stable_id="F11"),
    frame(115, "2018-unity-editor", "development-tool", "Unity 编辑器中的 VRChat 世界制作画面。", "2018 chapter context", "medium", "紧接片内 2018 年份卡；该帧自身没有独立日期。", slot="VRC 79.36–86.4。", stable_id="F12"),
    frame(120, "unity-world-editor", "development-tool", "Unity 编辑器与资源面板中的世界制作画面。", "2018 chapter context", "medium", "位于片内 2018 年份卡之后；画面内容是 Unity 编辑器，不是游戏实况。", slot="VRC 79.36–86.4。", stable_id="F13"),
    frame(104, "single-nameplate-ui", "nameplate-ui", "头像上方的单个绿色名牌样式；用户名像素化，边框与图标保留。", "2017 chapter context", "medium", "位于片内 2017 年章节；该帧自身没有独立年份。", masks=[[500, 170, 1030, 234]], stable_id="F14"),
    frame(150, "beach-social-world", "social-world", "海滩社交世界与多人头像。", "2018 chapter context", "medium", "位于片内 2018 年份卡之后的章节；画面自身没有年份。", slot="VRC 79.36–86.4。", stable_id="F15"),
    frame(156, "2018-backstage-world", "social-world", "Backstage 舞台世界中的头像与名牌；用户名已像素化。", "2018 chapter context", "medium", "位于片内 2018 年章节；画面自身没有独立日期。", slot="VRC 79.36–86.4。", masks=[[520, 260, 825, 330]], stable_id="F16"),
    frame(180, "new-year-2018-world", "year-evidence", "活动世界背景中出现 NEW YEAR 2018 字样。", "2018", "high", "画面背景清楚出现“NEW YEAR 2018”。", slot="VRC 79.36–86.4。", stable_id="F17"),
    frame(90, "full-body-grass-world", "avatar-world", "草地场景中的全身头像与身体动作。", "2017 chapter context", "medium", "位于片内 2017 年份卡之后、2018 卡之前。", stable_id="F18"),
    frame(210, "avatar-3-card", "avatar-system", "Avatar 3.0 章节卡。", "Avatar 3.0; year unverified", "low", "画面明确写有 Avatar 3.0，但没有年份。", slot="VRC 86.4–102.4 的功能变化候选，年份待核。", stable_id="F19"),
    frame(214, "new-action-menu-card", "vrchat-menu", "VRChat New Action Menu 章节卡。", "New Action Menu; year unverified", "low", "画面明确写有 New Action Menu，没有年份。", slot="VRC 86.4–102.4 的功能变化候选，年份待核。", stable_id="F20"),
    frame(184, "year-2020-card", "year-card", "章节卡直接标出 2020。", "2020", "high", "画面中央直接显示“2020”。", slot="VRC 86.4–102.4。", stable_id="F21"),
    frame(238, "incoming-ui-features", "vrchat-ui", "Incoming UI Features 功能说明页。", None, "unknown", "标题为 Incoming UI Features；没有年份。", slot="VRC 86.4–102.4 的界面变化候选，年份待核。", stable_id="F22"),
    frame(254, "udon-networking-update", "udon-update", "Udon Networking Update 功能介绍画面。", None, "unknown", "可见 Udon Networking Update 文案，但未显示年份。", slot="VRC 86.4–102.4 的技术更新候选，年份待核。", stable_id="F23"),
    frame(257, "custom-menu-pages", "vrchat-ui", "Create Custom Menu Pages for your worlds 功能卡。", None, "unknown", "画面直接显示 Custom Menu Pages，但没有年份。", slot="VRC 86.4–102.4 或条件段过渡；年份待核。", stable_id="F24"),
    frame(274, "unity-2019-lts", "development-tool", "Engine Upgrade / Unity 2019 LTS 版本提示卡。", "Unity 2019 LTS version label", "high", "卡片明确显示 Unity 2019 LTS；这证明画面提及的版本，不等于拍摄日期。", slot="VRC 86.4–102.4。", stable_id="F25"),
    frame(285, "spookality-2021", "event-card", "Spookality 2021 活动卡。", "2021", "high", "卡片明确写有 Spookality 2021。", slot="VRC 86.4–102.4。", stable_id="F26"),
    frame(259, "launch-pad-ui", "vrchat-ui", "Launch Pad 主界面、Quick Links 与 Quick Actions。", None, "unknown", "界面写有 Launch Pad，但画面没有年份。", slot="条件/过载段界面变化候选，年份待核。", stable_id="F27"),
    frame(287, "new-quick-menu", "vrchat-ui", "Launch Pad 上的 New Quick Menu 介绍。", None, "unknown", "界面写有 Launch Pad / New Quick Menu，但没有年份。", slot="条件/过载段界面变化候选，年份待核。", masks=[[920, 165, 1100, 246]], stable_id="F28"),
    frame(334, "year-2022-card", "year-card", "章节卡直接标出 2022。", "2022", "high", "画面中央直接显示“2022”。", slot="VRC 102.4 之后。", stable_id="F29"),
    frame(336, "osc-for-avatars", "avatar-system", "OSC for Avatars / Enabling External Controllers 标题卡。", "after 2022 card; exact year unverified", "low", "位于片内 2022 年卡之后，但该功能画面没有独立年份。", slot="VRC 102.4 之后；年份待核。", stable_id="F30"),
    frame(356, "ik-2-title", "tracking-feature", "IK 2.0 Improvements & Fine-Tuning for Tracking and Full Body 标题卡。", "IK 2.0; year unverified", "low", "卡片明确写有 IK 2.0，但没有年份。", slot="VRC 102.4 之后的功能变化候选，年份待核。", stable_id="F31"),
    frame(364, "calibration-saving", "tracking-feature", "Calibration Saving 功能说明画面。", None, "unknown", "可见 Calibration Saving 文案，没有年份。", slot="VRC 102.4 之后的功能更新候选，年份待核。", stable_id="F32"),
    frame(10, "sunset-home-world", "hub-home", "旧式海边/落日世界与中央头像。", "2014 chapter context", "medium", "章节位置在片内 2014 年份卡之后、2015 卡之前。", stable_id="F33"),
    frame(29, "social-world-help-panel", "social-world", "社交世界内的 Help Yourself! 提示与多人空间。", "2015 chapter context", "medium", "位于片内 2015 年章节范围，具体采集日未标。", masks=[[530, 285, 760, 346]], stable_id="F34"),
    frame(64, "year-2016-card", "year-card", "章节卡直接标出 2016。", "2016", "high", "画面中央直接显示“2016”。", stable_id="F35"),
    frame(78, "outdoor-world-plaza", "social-world", "草地公共世界与远处头像。", "2016 chapter context", "medium", "位于片内 2016 年份卡之后、2017 卡之前。", masks=[[680, 45, 1030, 110]], stable_id="F36"),
    frame(88, "year-2017-card", "year-card", "章节卡直接标出 2017。", "2017", "high", "画面中央直接显示“2017”。", stable_id="F37"),
    frame(227, "year-2021-card", "year-card", "章节卡直接标出 2021。", "2021", "high", "画面中央直接显示“2021”。", slot="VRC 86.4–102.4。", stable_id="F38"),
    frame(262, "regions-card", "vrchat-ui", "VRChat Regions 功能卡。", None, "unknown", "画面写有 Regions / are Live，没有年份。", slot="VRC 86.4–102.4 或条件段过渡；年份待核。", stable_id="F39"),
    frame(264, "region-selection-menu", "vrchat-ui", "创建实例时选择 Region 的界面。", None, "unknown", "菜单说明 Choose a Region when creating an instance；没有年份。", slot="VRC 86.4–102.4 或条件段过渡；年份待核。", stable_id="F40"),
]


CLIPS = [
    {"start": 114.0, "end": 118.0, "slug": "2018-card-to-editor", "category": "year-card", "description": "2018 年份卡过渡到 Unity 编辑器。", "depicted_date": "2018 chapter context", "date_confidence": "high for card; medium for editor", "date_evidence": "年份卡直接标 2018，随后切到未独立定年的编辑器镜头。", "mv_slot": "VRC 79.36–86.4。", "privacy_masks": []},
    {"start": 199.0, "end": 202.0, "slug": "udon-introduction", "category": "udon-update", "description": "Introducing Udon / Unlocking Coding for Creators 功能卡。", "depicted_date": "2020 chapter context", "date_confidence": "medium", "date_evidence": "位于片内 2020 年份卡之后；画面本身未标年。", "mv_slot": "VRC 86.4–102.4。", "privacy_masks": []},
    {"start": 210.0, "end": 216.0, "slug": "avatar3-action-menu", "category": "vrchat-menu", "description": "Avatar 3.0、头像控制页与 New Action Menu。", "depicted_date": "Avatar 3.0 / Action Menu; year unverified", "date_confidence": "low", "date_evidence": "卡片显示版本/功能名称，没有年份。", "mv_slot": "VRC 86.4–102.4 的功能演变候选，年份待核。", "privacy_masks": []},
    {"start": 262.0, "end": 266.0, "slug": "regions-ui", "category": "vrchat-ui", "description": "Regions 卡过渡到创建实例时的地区选择界面。", "depicted_date": None, "date_confidence": "unknown", "date_evidence": "画面有 Regions 文案，但没有年份。", "mv_slot": "VRC 86.4–102.4 或条件段过渡；年份待核。", "privacy_masks": []},
    {"start": 274.0, "end": 278.0, "slug": "unity-engine-upgrade-2019", "category": "development-tool", "description": "Engine Upgrade / Unity 2019 LTS 提示卡。", "depicted_date": "Unity 2019 LTS version label", "date_confidence": "high", "date_evidence": "卡片明确写有 Unity 2019 LTS；不据此推断拍摄日。", "mv_slot": "VRC 86.4–102.4。", "privacy_masks": []},
    {"start": 285.0, "end": 287.0, "slug": "spookality-2021-card", "category": "event-card", "description": "Spookality 2021 活动卡，无黑帧。", "depicted_date": "2021", "date_confidence": "high", "date_evidence": "卡片明确标出 Spookality 2021。", "mv_slot": "VRC 86.4–102.4。", "privacy_masks": []},
    {"start": 334.0, "end": 338.0, "slug": "2022-card-transition", "category": "year-card", "description": "2022 年份卡过渡到 OSC for Avatars 标题。", "depicted_date": "2022 card followed by unverified feature", "date_confidence": "high for card; low for following feature", "date_evidence": "前段明确显示 2022；后续 OSC 画面没有独立年份。", "mv_slot": "VRC 102.4 之后。", "privacy_masks": []},
    {"start": 356.0, "end": 362.0, "slug": "ik-2-tracking", "category": "tracking-feature", "description": "IK 2.0 与动作跟踪功能标题/演示。", "depicted_date": "IK 2.0; year unverified", "date_confidence": "low", "date_evidence": "有 IK 2.0 功能名，没有年份。", "mv_slot": "VRC 102.4 之后的功能变化候选，年份待核。", "privacy_masks": []},
]

def run(command: list[str], *, capture: bool = False) -> bytes:
    result = subprocess.run(
        command,
        check=True,
        stdout=subprocess.PIPE if capture else subprocess.DEVNULL,
        stderr=subprocess.PIPE,
    )
    return result.stdout if capture else b""


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def timecode(seconds: float) -> str:
    whole = int(seconds)
    millis = round((seconds - whole) * 1000)
    if millis == 1000:
        whole += 1
        millis = 0
    hours, remain = divmod(whole, 3600)
    minutes, sec = divmod(remain, 60)
    return f"{hours:02d}:{minutes:02d}:{sec:02d}.{millis:03d}"


def crop_and_mask(source_frame: Image.Image, masks: list[list[int]]) -> Image.Image:
    if source_frame.size != (SOURCE_WIDTH, SOURCE_HEIGHT):
        raise ValueError(f"Unexpected source frame dimensions: {source_frame.size}")
    cropped = source_frame.crop(CROP).resize(OUTPUT_SIZE, Image.Resampling.LANCZOS)
    scale_x = OUTPUT_SIZE[0] / (CROP[2] - CROP[0])
    scale_y = OUTPUT_SIZE[1] / (CROP[3] - CROP[1])
    for x0, y0, x1, y1 in masks:
        x0 = max(x0, CROP[0])
        y0 = max(y0, CROP[1])
        x1 = min(x1, CROP[2])
        y1 = min(y1, CROP[3])
        if x0 >= x1 or y0 >= y1:
            continue
        box = (
            round((x0 - CROP[0]) * scale_x),
            round((y0 - CROP[1]) * scale_y),
            round((x1 - CROP[0]) * scale_x),
            round((y1 - CROP[1]) * scale_y),
        )
        region = cropped.crop(box)
        small = region.resize(
            (max(1, region.width // 24), max(1, region.height // 14)),
            Image.Resampling.BILINEAR,
        )
        cropped.paste(small.resize(region.size, Image.Resampling.NEAREST), box)
    return cropped


def export_frame(source: Path, item: dict, index: int) -> dict:
    stable_id = item.get("id") or f"F{index:02d}"
    output = EXPORT_DIR / "frames" / f"frame_{stable_id}_{item['seconds']:06.1f}_{item['slug']}.png"
    raw = run(
        [
            "ffmpeg",
            "-hide_banner",
            "-loglevel",
            "error",
            "-ss",
            f"{item['seconds']:.3f}",
            "-i",
            str(source),
            "-frames:v",
            "1",
            "-f",
            "image2pipe",
            "-vcodec",
            "png",
            "pipe:1",
        ],
        capture=True,
    )
    frame = Image.open(io.BytesIO(raw)).convert("RGB")
    image = crop_and_mask(frame, item["privacy_masks"])
    output.parent.mkdir(parents=True, exist_ok=True)
    image.save(output, format="PNG", optimize=True)
    return {
        **item,
        "id": stable_id,
        "path": output.relative_to(REPO_ROOT).as_posix(),
        "source_timecode": timecode(item["seconds"]),
        "source_time_range_seconds": [item["seconds"], round(item["seconds"] + 1 / 30, 3)],
        "source_frame_size": [SOURCE_WIDTH, SOURCE_HEIGHT],
        "crop_bounds_px": list(CROP),
        "crop_rule": "[left, top, right, bottom), crop then aspect-preserving 16:9 resize",
        "output_size_px": list(OUTPUT_SIZE),
        "aspect_ratio": "16:9",
        "size_bytes": output.stat().st_size,
        "sha256": sha256(output),
        "privacy_treatment": "Pixelated only the listed identifiable player-name regions; the plate geometry remains visible." if item["privacy_masks"] else "No name masking required after visual review.",
    }


def export_clip(source: Path, item: dict, index: int) -> dict:
    output = EXPORT_DIR / "clips" / f"clip_{index:02d}_{item['start']:06.1f}-{item['end']:06.1f}_{item['slug']}.mp4"
    output.parent.mkdir(parents=True, exist_ok=True)
    vf = f"crop={CROP[2] - CROP[0]}:{CROP[3] - CROP[1]}:{CROP[0]}:{CROP[1]},scale={OUTPUT_SIZE[0]}:{OUTPUT_SIZE[1]}"
    filters = [vf]
    # The early T-pose clip contains one moving nameplate. Apply a narrow
    # delogo blur to that fixed label area after crop/scale.
    for x0, y0, x1, y1 in item["privacy_masks"]:
        sx = OUTPUT_SIZE[0] / (CROP[2] - CROP[0])
        sy = OUTPUT_SIZE[1] / (CROP[3] - CROP[1])
        ox = round((x0 - CROP[0]) * sx)
        oy = round((y0 - CROP[1]) * sy)
        ow = round((x1 - x0) * sx)
        oh = round((y1 - y0) * sy)
        filters.append(f"delogo=x={ox}:y={oy}:w={ow}:h={oh}:show=0")
    run(
        [
            "ffmpeg",
            "-hide_banner",
            "-loglevel",
            "error",
            "-y",
            "-ss",
            f"{item['start']:.3f}",
            "-i",
            str(source),
            "-t",
            f"{item['end'] - item['start']:.3f}",
            "-vf",
            ",".join(filters),
            "-an",
            "-c:v",
            "libx264",
            "-preset",
            "medium",
            "-crf",
            "18",
            "-pix_fmt",
            "yuv420p",
            "-movflags",
            "+faststart",
            str(output),
        ]
    )
    probe_raw = run(
        [
            "ffprobe",
            "-v",
            "error",
            "-show_entries",
            "format=duration,size:stream=codec_name,codec_type,width,height",
            "-of",
            "json",
            str(output),
        ],
        capture=True,
    )
    probe = json.loads(probe_raw)
    duration = float(probe["format"]["duration"])
    if not 2 <= duration <= 6:
        raise ValueError(f"Clip duration outside 2–6 sec: {output} ({duration:.3f}s)")
    if any(s.get("codec_type") == "audio" for s in probe.get("streams", [])):
        raise ValueError(f"Unexpected audio stream in clip: {output}")
    video = next(s for s in probe["streams"] if s.get("codec_type") == "video")
    return {
        **item,
        "id": f"C{index:02d}",
        "path": output.relative_to(REPO_ROOT).as_posix(),
        "source_time_range_seconds": [item["start"], item["end"]],
        "source_time_range": [timecode(item["start"]), timecode(item["end"])],
        "duration_seconds": round(duration, 3),
        "video_codec": video["codec_name"],
        "audio": "none",
        "output_size_px": [int(video["width"]), int(video["height"])],
        "aspect_ratio": "16:9",
        "crop_bounds_px": list(CROP),
        "crop_rule": "[left, top, right, bottom), crop then aspect-preserving 16:9 resize",
        "size_bytes": output.stat().st_size,
        "sha256": sha256(output),
        "privacy_treatment": "FFmpeg delogo blur over the listed moving nameplate area; plate geometry remains visible." if item["privacy_masks"] else "No name masking required after visual review.",
    }


def load_font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    for path in (
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf",
    ):
        if Path(path).exists():
            return ImageFont.truetype(path, size=size)
    return ImageFont.load_default()


def make_contact_sheet(frames: list[dict], output: Path) -> None:
    cols = 4
    cell_width = 320
    image_height = 180
    label_height = 42
    rows = (len(frames) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * cell_width, rows * (image_height + label_height)), (24, 25, 31))
    draw = ImageDraw.Draw(sheet)
    font = load_font(15)
    small = load_font(12)
    for index, item in enumerate(frames):
        image = Image.open(REPO_ROOT / item["path"]).convert("RGB")
        image.thumbnail((cell_width, image_height), Image.Resampling.LANCZOS)
        x = (index % cols) * cell_width
        y = (index // cols) * (image_height + label_height)
        sheet.paste(image, (x, y))
        draw.rectangle((x, y + image_height, x + cell_width, y + image_height + label_height), fill=(12, 13, 17))
        draw.text((x + 7, y + image_height + 2), f"{item['id']}  {item['source_timecode']}  {item['category']}", font=font, fill=(240, 240, 245))
        description = item["slug"].replace("-", " ")
        draw.text((x + 7, y + image_height + 22), description[:42], font=small, fill=(185, 192, 204))
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output, format="PNG", optimize=True)


def make_clip_sheet(clips: list[dict], output: Path) -> None:
    cols = 4
    cell_width = 320
    image_height = 180
    label_height = 38
    rows = (len(clips) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * cell_width, rows * (image_height + label_height)), (24, 25, 31))
    draw = ImageDraw.Draw(sheet)
    font = load_font(14)
    for index, item in enumerate(clips):
        raw = run(
            ["ffmpeg", "-hide_banner", "-loglevel", "error", "-ss", "0.7", "-i", str(REPO_ROOT / item["path"]), "-frames:v", "1", "-f", "image2pipe", "-vcodec", "png", "pipe:1"],
            capture=True,
        )
        image = Image.open(io.BytesIO(raw)).convert("RGB")
        image.thumbnail((cell_width, image_height), Image.Resampling.LANCZOS)
        x = (index % cols) * cell_width
        y = (index // cols) * (image_height + label_height)
        sheet.paste(image, (x, y))
        draw.rectangle((x, y + image_height, x + cell_width, y + image_height + label_height), fill=(12, 13, 17))
        draw.text((x + 7, y + image_height + 5), f"{item['id']} {item['source_time_range'][0]}–{item['source_time_range'][1]}  {item['slug']}", font=font, fill=(240, 240, 245))
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output, format="PNG", optimize=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--clean-output", action="store_true", help="Remove only this task's generated export directory before rebuilding.")
    args = parser.parse_args()
    source = args.source.expanduser().resolve()
    if not source.is_file():
        raise SystemExit(f"Source video not found: {source}")
    if args.clean_output and EXPORT_DIR.exists():
        shutil.rmtree(EXPORT_DIR)
    EXPORT_DIR.mkdir(parents=True, exist_ok=True)
    REFERENCE_DIR.mkdir(parents=True, exist_ok=True)

    frames = [export_frame(source, item, index) for index, item in enumerate(FRAMES, start=1)]
    clips = [export_clip(source, item, index) for index, item in enumerate(CLIPS, start=1)]
    make_contact_sheet(frames, EXPORT_DIR / "contact-sheet.png")
    make_clip_sheet(clips, EXPORT_DIR / "clip-contact-sheet.png")

    source_record = {
        "bvid": "BV1brH769EtM",
        "title": "6分钟看完VRChat历史",
        "published_at_utc": "2026-10-04T14:10:43Z",
        "page_url": "https://www.bilibili.com/video/BV1brH769EtM",
        "user_reference_url": "https://b23.tv/7SVvqj7",
        "download_route": "https://bs.chiro.work:8443/play/BV1brH769EtM?p=1",
        "download_http_status": 200,
        "download_content_type": "video/mp4",
        "download_content_length_bytes": 51167482,
        "download_response_headers": {
            "ETag": "\"f37362f8f949ef29b38344dca3ea57dd\"",
            "Last-Modified": "Mon, 05 Oct 2026 03:38:41 GMT",
            "Accept-Ranges": "bytes",
            "Content-MD5": "83Ni+PlJ7ymzg0Tco+pX3Q==",
        },
        "resolved_media_url": "CDN URL path /upgcxcode/73/46/42461104673/42461104673_qe1-1-192.mp4; expiring signature retained only in the task temp response log.",
        "full_fidelity_local_source": "~/tmp/vrc-old-web-map/fullfilm-v4/history-video/source-full.mp4",
        "full_fidelity_source_sha256": sha256(source),
        "full_fidelity_source_bytes": source.stat().st_size,
        "downloaded_stream": {
            "duration_seconds": 384.383878,
            "size_px": [1280, 720],
            "video_codec": "h264",
            "video_fps_reported": "30/1 nominal; 29.999... average",
            "audio_codec": "aac",
        },
        "bilibili_api_metadata": {
            "cid": 42461104673,
            "published_at_utc": "2026-10-04T14:10:43Z",
            "page_duration_seconds": 385,
            "reported_size_px": [1920, 1080],
            "api_response_copy": "art/reference/fullfilm-v4-video/source-page-api.json",
        },
        "web_check": "web.run open of the provided b23.tv page was unavailable; Bilibili view API metadata verified the BV title and page details.",
        "download_response_log": "~/tmp/vrc-old-web-map/fullfilm-v4/history-video/source-response.json",
    }
    (REFERENCE_DIR / "source-metadata.json").write_text(
        json.dumps(source_record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    data = {
        "schema_version": 1,
        "title": "Fullfilm V4 historical VRChat video material",
        "created_at_utc": datetime.now(timezone.utc).isoformat(),
        "source": source_record,
        "crop": {
            "source_size_px": [SOURCE_WIDTH, SOURCE_HEIGHT],
            "bounds_px": list(CROP),
            "output_size_px": list(OUTPUT_SIZE),
            "aspect_ratio": "16:9",
            "purpose": "Crop the upper-left Bilibili watermark and lower-right educational-use overlay from the outer edges, preserving a full-bleed 16:9 image.",
            "pixel_retouch": "No inpainting, generative fill, or historical UI alteration. Listed player-name fields are pixelated for privacy.",
        },
        "frames": frames,
        "clips": clips,
        "contact_sheet": "art/export/fullfilm-v4-video/contact-sheet.png",
        "clip_contact_sheet": "art/export/fullfilm-v4-video/clip-contact-sheet.png",
        "counts": {"frames": len(frames), "clips": len(clips)},
    }
    DATA_PATH.parent.mkdir(parents=True, exist_ok=True)
    DATA_PATH.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"frames": len(frames), "clips": len(clips), "frame_contact_sheet": "art/export/fullfilm-v4-video/contact-sheet.png", "clip_contact_sheet": "art/export/fullfilm-v4-video/clip-contact-sheet.png", "data": DATA_PATH.relative_to(REPO_ROOT).as_posix()}, ensure_ascii=False))


if __name__ == "__main__":
    main()
