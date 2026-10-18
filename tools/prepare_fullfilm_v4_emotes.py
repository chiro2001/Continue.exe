#!/usr/bin/env python3
"""Prepare sourced historical emote PNGs, metadata, and review contact sheets."""
from __future__ import annotations

import hashlib
import json
import re
import shutil
import subprocess
from pathlib import Path
from xml.etree import ElementTree

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
REFERENCE = ROOT / "art/reference/fullfilm-v4-emotes"
EXPORT = ROOT / "art/export/fullfilm-v4-emotes"
TEMP = Path.home() / "tmp/vrc-old-web-map/fullfilm-v4/emotes"
DATE = "2026-10-18"
QQ_MAP_URL = "https://raw.githubusercontent.com/YiJio/emoji-chinese/main/_map/qq2003.json"
QQ_REPO_URL = "https://github.com/YiJio/emoji-chinese"
QQ_ASSET_BASE = "https://cdn.jsdelivr.net/gh/YiJio/emoji-chinese@1.1.0/qq/qq-2003"
QQ_NAMES = [
    "surprised", "pout", "love-struck", "blank", "smug", "tears", "shy", "shush", "sleep", "cry",
    "awkward", "angry", "playful", "toothy-grin", "smile", "sad", "cool", "sars", "rage", "vomit",
]

# These are different expressions cut from public image sheets or rendered from
# public vector originals. White backgrounds remain intact unless the source has alpha.
EXTRA_EMOTES = [
    {
        "id": "onionhead-2007-shout", "name": "洋葱头：张口喊叫", "contact_label": "Onion shout", "family": "洋葱头 / Onion Head",
        "source_file": "onionhead-sohu-2007-sheet.jpg", "source_url": "https://photocdn.sohu.com/20071010/Img252578736.jpg",
        "evidence_url": "https://dm.sohu.com/s2007/onion/index.shtml", "year_range": "2007-10",
        "year_evidence": "Sohu identifies Onion Head as created around 2006 by Taiwanese cartoonist Ethan; the source image path is dated 2007-10-10 and its alt text labels it as an Onion Head big-head-sticker collage. Individual panels have no captions.",
        "年代证据": "搜狐称洋葱头约于2006年由台湾漫画家Ethan创作；源图CDN路径为2007-10-10，图片替代文字标为“洋葱头大头贴”。各小格没有独立表情名。",
        "crop_box_px": [66, 0, 133, 74], "mv_range": [51.2, 253.44], "suggested_section": "工具/汉化/BT",
        "uncertainty": "This is a contemporaneous four-panel character photo-booth image, not verified as the original MSN/QQ Onion Head emoji pack.",
        "rights_note": "Character and artwork rights are not cleared by the archive page; keep as historical reference unless separately licensed.",
        "description_confidence": "medium; named from visible expression, no panel-level caption",
    },
    {
        "id": "onionhead-2007-blush", "name": "洋葱头：闭眼脸红", "contact_label": "Onion blush", "family": "洋葱头 / Onion Head",
        "source_file": "onionhead-sohu-2007-sheet.jpg", "source_url": "https://photocdn.sohu.com/20071010/Img252578736.jpg",
        "evidence_url": "https://dm.sohu.com/s2007/onion/index.shtml", "year_range": "2007-10",
        "year_evidence": "Sohu identifies Onion Head as created around 2006 by Taiwanese cartoonist Ethan; the source image path is dated 2007-10-10 and its alt text labels it as an Onion Head big-head-sticker collage. Individual panels have no captions.",
        "年代证据": "搜狐称洋葱头约于2006年由台湾漫画家Ethan创作；源图CDN路径为2007-10-10，图片替代文字标为“洋葱头大头贴”。各小格没有独立表情名。",
        "crop_box_px": [66, 74, 133, 150], "mv_range": [51.2, 253.44], "suggested_section": "工具/汉化/BT",
        "uncertainty": "This is a contemporaneous four-panel character photo-booth image, not verified as the original MSN/QQ Onion Head emoji pack.",
        "rights_note": "Character and artwork rights are not cleared by the archive page; keep as historical reference unless separately licensed.",
        "description_confidence": "medium; named from visible expression, no panel-level caption",
    },
    {
        "id": "onionhead-2007-smile", "name": "洋葱头：微笑抬手", "contact_label": "Onion smile", "family": "洋葱头 / Onion Head",
        "source_file": "onionhead-sohu-2007-sheet.jpg", "source_url": "https://photocdn.sohu.com/20071010/Img252578736.jpg",
        "evidence_url": "https://dm.sohu.com/s2007/onion/index.shtml", "year_range": "2007-10",
        "year_evidence": "Sohu identifies Onion Head as created around 2006 by Taiwanese cartoonist Ethan; the source image path is dated 2007-10-10 and its alt text labels it as an Onion Head big-head-sticker collage. Individual panels have no captions.",
        "年代证据": "搜狐称洋葱头约于2006年由台湾漫画家Ethan创作；源图CDN路径为2007-10-10，图片替代文字标为“洋葱头大头贴”。各小格没有独立表情名。",
        "crop_box_px": [0, 0, 66, 74], "mv_range": [51.2, 253.44], "suggested_section": "工具/汉化/BT",
        "uncertainty": "This is a contemporaneous four-panel character photo-booth image, not verified as the original MSN/QQ Onion Head emoji pack.",
        "rights_note": "Character and artwork rights are not cleared by the archive page; keep as historical reference unless separately licensed.",
        "description_confidence": "medium; named from visible expression, no panel-level caption",
    },
    {
        "id": "tieba-huaji-color-2014", "name": "彩色滑稽", "contact_label": "Tieba Huaji", "family": "百度贴吧 / 滑稽",
        "source_file": "tieba-huaji-baike-36.png",
        "source_url": "https://bkimg.cdn.bcebos.com/smart/0b7b02087bf40ad16ee4e2945f2c11dfa9ecce40-bkimg-process,v_1,rw_36,rh_36,maxl_284?x-bce-process=image/format,f_auto",
        "evidence_url": "https://bkso.baidu.com/item/%E6%BB%91%E7%A8%BD/19219706?fromModule=lemma_inlink", "year_range": "2012–2014+",
        "year_evidence": "The Baidu article says the Tieba expression became widely used from 2012; this particular 36x36 colored image is a later article graphic, not proven to be the original 2012 file.",
        "年代证据": "百度词条称该贴吧表情自2012年起广泛使用；本张36×36彩色图为词条后续配图，不能认定为2012年原始文件。",
        "crop_box_px": None, "mv_range": [51.2, 253.44], "suggested_section": "工具/汉化/BT",
        "uncertainty": "The label is the Baidu article's '彩色滑稽' image caption; exact mapping to a specific 2012 Tieba client asset is not independently verified.",
        "rights_note": "Baidu image rights are not cleared; historical reference only.",
        "description_confidence": "medium",
    },
    {
        "id": "rage-guy-openclipart-2011", "name": "Rage Guy / FFFFFFFUUUU", "contact_label": "Rage Guy", "family": "早期 Rage Comic",
        "source_file": "rage-guy-openclipart-142225.svg", "source_url": "https://openclipart.org/download/142225",
        "evidence_url": "https://openclipart.org/detail/142225/rage-guy", "year_range": "2011 (vector upload)",
        "year_evidence": "Openclipart's source record identifies the 2011 vector upload; this is a contemporaneous vectorization of the Rage Guy motif, not the first 2008 imageboard original.",
        "年代证据": "Openclipart记录为2011年上传的向量版本；这是同期矢量化作品，不是2008年前后最初的图片版原稿。",
        "crop_box_px": None, "mv_range": [51.2, 253.44], "suggested_section": "工具/汉化/BT",
        "uncertainty": "It represents an early Rage Comic motif but is a 2011 vector interpretation; do not identify it as the original 2008 upload.",
        "rights_note": "Openclipart source is released under CC0/public-domain terms; keep author/source attribution in the provenance list.",
        "render_width": 256,
    },
    {
        "id": "rage-forever-alone-openclipart-2011", "name": "Forever Alone", "contact_label": "Forever Alone", "family": "早期 Rage Comic",
        "source_file": "forever-alone-openclipart-166561.svg", "source_url": "https://openclipart.org/download/166561",
        "evidence_url": "https://openclipart.org/detail/166561/forever-alone", "year_range": "2011-12-25 (vector upload)",
        "year_evidence": "Openclipart dates the vector upload to 2011-12-25 and describes it as traced in Inkscape; this is a contemporary vector trace, not the motif's first publication.",
        "年代证据": "Openclipart标注上传日期2011-12-25，并注明Inkscape描摹；这是同期矢量描摹，不是该表情最初发布的原图。",
        "crop_box_px": None, "mv_range": [51.2, 253.44], "suggested_section": "工具/汉化/BT",
        "uncertainty": "The motif's origin predates this vector upload; a precise first-use day is not asserted.",
        "rights_note": "Openclipart source is released under CC0/public-domain terms; keep author/source attribution in the provenance list.",
        "render_width": 256,
    },
    {
        "id": "rage-trollface-openclipart-2011", "name": "Trollface", "contact_label": "Trollface", "family": "早期 Rage Comic",
        "source_file": "trollface-openclipart-146953.svg", "source_url": "https://openclipart.org/download/146953",
        "evidence_url": "https://openclipart.org/detail/146953/troll-face-problem", "year_range": "2011 (vector upload)",
        "year_evidence": "Openclipart's source record identifies the vector upload in the early 2010s; the face is used as an early web Rage Comic motif, while its first 2008 drawing is a different source artifact.",
        "年代证据": "Openclipart记录为2010年代初向量稿；本项目将其作为早期网络 Rage Comic 图样，不能冒充2008年最初上传的原图。",
        "crop_box_px": None, "mv_range": [51.2, 253.44], "suggested_section": "工具/汉化/BT",
        "uncertainty": "This is a contemporaneous vector interpretation, not the artist's first-posted drawing.",
        "rights_note": "Openclipart source is released under CC0/public-domain terms; keep author/source attribution in the provenance list.",
        "render_width": 256,
    },
    {
        "id": "tuzki-2011-wave", "name": "兔斯基：挥手姿势", "contact_label": "Tuzki wave", "family": "兔斯基 / Tuzki",
        "source_file": "tuzki-2011-pack-preview.jpg", "source_url": "https://www.rjzxw.com/dats/sucai/1/1251/1251.jpg",
        "evidence_url": "https://www.rjzxw.com/sucai/1251.html", "year_range": "2011-07-30 page; original pack date unknown",
        "year_evidence": "The public resource page is dated 2011-07-30 and identifies www.mytuzki.net as the official website; the selected item is cropped from its 8x5 preview, so exact sticker release date is uncertain.",
        "年代证据": "公开资源页日期为2011-07-30，页面注明兔斯基官网为www.mytuzki.net；所选图来自8×5预览，原表情包确切发行日期未确认。",
        "crop_box_px": [0, 0, 76, 81], "mv_range": [51.2, 253.44], "suggested_section": "工具/汉化/BT",
        "uncertainty": "This is a dated archival preview from a third-party resource page; author identity is associated with Momo Wang, but the exact source pack and its terms are not available here.",
        "rights_note": "Tuzki artwork rights are not cleared by the mirror; reference use only unless separately licensed.",
        "description_confidence": "high for character, medium for exact panel crop",
    },
    {
        "id": "twemoji-2016-joy", "name": "Face with Tears of Joy", "contact_label": "Twemoji joy", "family": "Twemoji / 社交平台 emoji",
        "source_file": "twemoji-v2-joy.png", "source_url": "https://raw.githubusercontent.com/twitter/twemoji/v2.0.0/2/72x72/1f602.png",
        "evidence_url": "https://github.com/twitter/twemoji/releases/tag/v2.0.0", "year_range": "2016 release series",
        "year_evidence": "The artwork is downloaded from the official Twemoji v2.0.0 release tree; use the 2016 release date as the source-edition marker.",
        "年代证据": "文件来自官方 Twemoji v2.0.0 发布目录；以该版2016年发行期作为素材版本锚点。",
        "crop_box_px": None, "mv_range": [133.12, 253.44], "suggested_section": "过载 / 社交反应",
        "uncertainty": "Release-tag dating identifies the design revision, not the first Unicode approval date of the emoji character.",
        "rights_note": "Twemoji graphics are CC BY 4.0; include Twitter/Twemoji attribution when published.",
    },
]

CONTEXT_SPECS = [
    {
        "id": "ali-2009-qqpinyin-theme-preview", "name": "阿狸 QQ 输入法主题预览", "contact_label": "Ali theme", "family": "阿狸 / Ali",
        "source_file": "ali-2009-qqpinyin-preview.jpg", "source_url": "https://dl.qqpy.sogou.com/skin/70/240/1337258054.jpg",
        "evidence_url": "https://skin.qq.pinyin.cn/skin_detail?skin_id=1337258054", "year_range": "2009-12-11",
        "year_evidence": "The official QQ Pinyin skin page lists the upload date as 2009-12-11 and identifies the uploader as 阿狸梦之城堡. This file is a full skin preview, not a standalone sticker sheet.",
        "年代证据": "QQ输入法官方皮肤页标注上传日期2009-12-11、作者“阿狸梦之城堡”。此文件是完整皮肤预览，不是独立表情包。",
        "mv_range": [51.2, 253.44], "suggested_section": "工具/汉化/BT", "rights_note": "Preview image contains Ali character artwork; no public redistribution license is stated.",
        "uncertainty": "Useful as verified period context; not counted as an independent emoticon.",
    },
    {
        "id": "vrchat-plus-sticker-feature-card-2024", "name": "VRChat+ Stickers and Sharing 功能图", "contact_label": "VRC stickers", "family": "VRChat / 社交后期",
        "source_file": "vrchat-stickers-2024-official-feature.png", "source_url": "https://help.vrchat.com/hc/article_attachments/34228038274195",
        "evidence_url": "https://help.vrchat.com/hc/en-us/articles/34198079589267-New-VRC-Feature-Stickers-Sharing", "year_range": "2024",
        "year_evidence": "The official VRChat help page documents Stickers and Sharing; the feature was introduced in the official 2024-09-05 Developer Update. This is a platform feature illustration, not a sticker pack.",
        "年代证据": "VRChat官方帮助页介绍 Stickers and Sharing；官方开发者更新在2024-09-05宣布该功能。此图是功能宣传图，不是表情包文件。",
        "mv_range": [235.52, 249.6], "suggested_section": "高潮 / 交接分享", "rights_note": "Official VRChat promotional art; usage terms are not separately cleared here.",
        "uncertainty": "Context image only; not included in distinct-emote count.",
    },
    {
        "id": "vrchat-plus-sticker-demo-2024-frame68", "name": "VRChat+ 贴纸分享演示帧", "contact_label": "VRC demo frame", "family": "VRChat / 社交后期",
        "source_file": "vrchat-stickers-trailer-2024.gif", "source_url": "https://help.vrchat.com/hc/article_attachments/34232080833043",
        "evidence_url": "https://help.vrchat.com/hc/en-us/articles/34198079589267-New-VRC-Feature-Stickers-Sharing", "year_range": "2024",
        "year_evidence": "The official help article embeds StickersTrailer_v5.gif; the feature's first-party announcement is dated 2024-09-05. This is one representative frame of a 155-frame demo.",
        "年代证据": "官方帮助页嵌入 StickersTrailer_v5.gif；功能的一手宣布日期为2024-09-05。本图只导出155帧演示中的单个代表帧。",
        "mv_range": [235.52, 249.6], "suggested_section": "高潮 / 交接分享", "rights_note": "The demo includes platform and user-created sticker imagery; do not redistribute as an independent pack.",
        "uncertainty": "Context screenshot only; multiple frames of the trailer are not counted as multiple stickers.",
        "export_frame": 68,
    },
]

TEXT_EMOTES = [
    {"id": "ascii-1982-smile", "text": ":-)", "name": "BBS joke marker / smile", "contact_label": "1982 :-)", "mv_range": [20.48, 253.44]},
    {"id": "ascii-1982-frown", "text": ":-(", "name": "BBS not-joke marker / frown", "contact_label": "1982 :-(", "mv_range": [20.48, 253.44]},
]


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def source_info(path: Path) -> dict:
    suffix = path.suffix.lower()
    if suffix == ".svg":
        root = ElementTree.parse(path).getroot()
        viewbox = root.attrib.get("viewBox", "")
        if viewbox:
            values = [float(v) for v in re.split(r"[ ,]+", viewbox.strip())]
            width, height = int(round(values[2])), int(round(values[3]))
        else:
            width = int(float(root.attrib.get("width", "256").replace("px", "")))
            height = int(float(root.attrib.get("height", "256").replace("px", "")))
        return {"format": "SVG", "dimensions": [width, height], "animated": False, "frames": 1}
    with Image.open(path) as image:
        return {
            "format": image.format or suffix.lstrip("."),
            "dimensions": [image.width, image.height],
            "animated": bool(getattr(image, "is_animated", False)),
            "frames": int(getattr(image, "n_frames", 1)),
        }


def aspect(size: list[int]) -> float:
    return round(size[0] / size[1], 6) if size[1] else 0.0


def save_context_item(spec: dict) -> dict:
    source = REFERENCE / spec["source_file"]
    info = source_info(source)
    display = EXPORT / f'{spec["id"]}.png'
    frame = spec.get("export_frame")
    if source.suffix.lower() == ".gif":
        with Image.open(source) as image:
            image.seek(int(frame or 0))
            image.convert("RGBA").save(display, optimize=True)
        operation = f"Exported one representative frame ({frame or 0}) from the preserved GIF; other frames are not separate assets."
    else:
        with Image.open(source) as image:
            image.convert("RGBA").save(display, optimize=True)
        operation = "PNG conversion of full source preview; no crop or background removal."
    dsize = source_info(display)["dimensions"]
    return {
        **spec,
        "source_path": str(source.relative_to(ROOT)),
        "source_sha256": sha256(source),
        "source_format": info["format"],
        "source_dimensions": info["dimensions"],
        "source_aspect_ratio": aspect(info["dimensions"]),
        "is_animated": info["animated"],
        "frame_count": info["frames"],
        "crop_box_px": None,
        "display_path": str(display.relative_to(ROOT)),
        "display_sha256": sha256(display),
        "display_dimensions": dsize,
        "display_aspect_ratio": aspect(dsize),
        "background_processing": operation,
        "unique_expression": False,
        "counted_as_distinct_emote": False,
    }


def create_emote_from_source(spec: dict) -> dict:
    source = REFERENCE / spec["source_file"]
    if not source.exists():
        raise FileNotFoundError(source)
    info = source_info(source)
    display = EXPORT / f'{spec["id"]}.png'
    crop_box = spec.get("crop_box_px")
    render_width = spec.get("render_width")
    if source.suffix.lower() == ".svg":
        tool = shutil.which("rsvg-convert")
        if not tool:
            raise RuntimeError("rsvg-convert is required to rasterize preserved Openclipart SVG sources.")
        subprocess.run([tool, "--width", str(render_width or 256), "--output", str(display), str(source)], check=True)
        operation = f"SVG rasterized with rsvg-convert at {render_width or 256}px wide; original vector remains preserved."
    else:
        with Image.open(source) as image:
            if info["animated"]:
                image.seek(0)
            result = image.convert("RGBA")
            if crop_box:
                result = result.crop(tuple(crop_box))
            result.save(display, optimize=True)
        if info["animated"]:
            operation = "First complete source GIF frame exported as RGBA PNG; only one distinct named expression is counted."
        elif crop_box:
            operation = "Exact source-image crop; background and source alpha preserved, no erasure or upscaling."
        else:
            operation = "Full source image converted to RGBA PNG; no crop or background removal."
    d_info = source_info(display)
    dsize = d_info["dimensions"]
    return {
        **spec,
        "source_path": str(source.relative_to(ROOT)),
        "source_sha256": sha256(source),
        "source_format": info["format"],
        "source_dimensions": info["dimensions"],
        "source_aspect_ratio": aspect(info["dimensions"]),
        "is_animated": info["animated"],
        "frame_count": info["frames"],
        "crop_box_px": crop_box,
        "display_path": str(display.relative_to(ROOT)),
        "display_sha256": sha256(display),
        "display_dimensions": dsize,
        "display_aspect_ratio": aspect(dsize),
        "background_processing": operation,
        "unique_expression": True,
        "counted_as_distinct_emote": True,
    }


def create_text_emote(spec: dict) -> dict:
    display = EXPORT / f'{spec["id"]}.png'
    canvas = Image.new("RGBA", (360, 150), (255, 255, 255, 0))
    draw = ImageDraw.Draw(canvas)
    font = ImageFont.load_default()
    box = draw.textbbox((0, 0), spec["text"], font=font)
    text_w, text_h = box[2] - box[0], box[3] - box[1]
    draw.text(((canvas.width - text_w) / 2, (canvas.height - text_h) / 2 - box[1]), spec["text"], fill=(25, 25, 25, 255), font=font)
    # Nearest-neighbor enlargement keeps the printed glyphs crisp; it is a transcription, not a source scan.
    canvas.resize((720, 300), Image.Resampling.NEAREST).save(display, optimize=True)
    return {
        **spec,
        "family": "早期 BBS / 文字颜文字",
        "source_url": "https://www.cs.cmu.edu/~sef/Orig-Smiley.htm",
        "evidence_url": "https://www.cs.cmu.edu/~sef/Orig-Smiley.htm",
        "year_range": "1982-09-19",
        "year_evidence": "CMU reproduces Scott Fahlman's original bboard message dated 19 September 1982 and the exact character sequence.",
        "年代证据": "CMU保存的原始BBS帖子日期为1982-09-19，并逐字记录该字符序列。",
        "source_path": None,
        "source_sha256": None,
        "source_format": "text transcription",
        "source_dimensions": None,
        "source_aspect_ratio": None,
        "is_animated": False,
        "frame_count": 1,
        "crop_box_px": None,
        "display_path": str(display.relative_to(ROOT)),
        "display_sha256": sha256(display),
        "display_dimensions": [720, 300],
        "display_aspect_ratio": 2.4,
        "background_processing": "Typeset from exact source text using a local bitmap font; no scan or original image is claimed.",
        "unique_expression": False,
        "counted_as_distinct_emote": False,
        "rights_note": "Text sequence transcribed from the CMU-hosted historical message; font rendering is a local reconstruction.",
        "uncertainty": "A raster rendering of text is included as a visual cue; it is not a downloaded historic pixel file.",
    }


def render_contact_sheet(items: list[dict], path: Path, columns: int = 5) -> None:
    tile_w, tile_h = 184, 150
    rows = (len(items) + columns - 1) // columns
    sheet = Image.new("RGB", (columns * tile_w, rows * tile_h), (36, 39, 44))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()
    for index, item in enumerate(items):
        x, y = (index % columns) * tile_w, (index // columns) * tile_h
        with Image.open(ROOT / item["display_path"]) as source:
            source = source.convert("RGBA")
            resample = Image.Resampling.NEAREST if item.get("source_format") in {"GIF", "PNG"} and item["display_dimensions"][0] <= 72 else Image.Resampling.LANCZOS
            scale = min(106 / source.width, 106 / source.height)
            preview = source.resize((max(1, round(source.width * scale)), max(1, round(source.height * scale))), resample)
        backing = Image.new("RGBA", (112, 112), (244, 244, 244, 255))
        backing.alpha_composite(preview, ((112 - preview.width) // 2, (112 - preview.height) // 2))
        sheet.paste(backing.convert("RGB"), (x + (tile_w - 112) // 2, y + 3))
        label = item.get("contact_label", item["id"])
        draw.text((x + 6, y + 119), f"{index + 1:02d} {label[:22]}", font=font, fill=(246, 246, 246))
    path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(path, optimize=True)


def main() -> None:
    EXPORT.mkdir(parents=True, exist_ok=True)
    map_path = REFERENCE / "qq-2003-source-map.json"
    if not map_path.exists():
        raise FileNotFoundError(map_path)
    source_map = json.loads(map_path.read_text(encoding="utf-8"))
    if len(source_map) < 20:
        raise RuntimeError("QQ source map must contain at least 20 entries.")

    items = []
    for index, mapped in enumerate(source_map[:20]):
        source = REFERENCE / f"qq-2003-{index:03d}.gif"
        if not source.exists():
            raise FileNotFoundError(source)
        items.append(create_emote_from_source({
            "id": f"qq-2003-{index:02d}-{QQ_NAMES[index]}",
            "name": mapped["name"][0].lstrip("/"),
            "contact_label": f"QQ03 {QQ_NAMES[index]}",
            "family": "QQ 经典黄脸 / 2003版",
            "source_file": source.name,
            "year_range": "2003 edition (community archive label)",
            "year_evidence": "The source archive README identifies qq-2003 as the 2003 edition; the matching map gives each expression name. This is the archive's pack label, not an independently recovered Tencent installer timestamp.",
            "年代证据": "来源仓库README将qq-2003标为2003年版；配套映射文件给出各表情名称。这是档案包版本标注，未独立校验腾讯安装包的首次发布日期。",
            "source_url": f"{QQ_ASSET_BASE}/{index}.gif",
            "evidence_url": QQ_REPO_URL,
            "source_map_url": QQ_MAP_URL,
            "mv_range": [20.48, 253.44],
            "mv_primary_start": 20.48,
            "suggested_section": "工具/汉化/BT",
            "rights_note": "The repository declares GPL-3.0, but this does not independently establish Tencent's rights for the underlying QQ artwork. Keep as historical reference until public-use terms are confirmed.",
            "uncertainty": "The archive's 2003 edition label is credible pack-level evidence, but byte identity with a specific Tencent release build is not independently proven.",
            "export_frame": 0,
        }))

    for spec in EXTRA_EMOTES:
        items.append(create_emote_from_source({
            **spec,
            "source_url": spec["source_url"],
            "source_map_url": None,
            "mv_primary_start": spec["mv_range"][0],
        }))

    unique_hashes = [item["display_sha256"] for item in items]
    if len(set(unique_hashes)) != len(items):
        raise RuntimeError("Two selected emote PNGs are byte-identical; remove the duplicate before claiming uniqueness.")
    if len({item["id"] for item in items}) != len(items):
        raise RuntimeError("Duplicate item IDs detected.")

    contexts = []
    for spec in CONTEXT_SPECS:
        contexts.append(save_context_item({**spec, "source_map_url": None, "mv_primary_start": spec["mv_range"][0]}))
    texts = [create_text_emote(item) for item in TEXT_EMOTES]

    qq_sheet = EXPORT / "contact-sheet-qq2003.png"
    render_contact_sheet(items[:20], qq_sheet, columns=5)
    full_sheet = EXPORT / "contact-sheet.png"
    render_contact_sheet(items, full_sheet, columns=5)
    context_sheet = EXPORT / "contact-sheet-context.png"
    render_contact_sheet(contexts + texts, context_sheet, columns=4)
    TEMP.mkdir(parents=True, exist_ok=True)
    for sheet in (qq_sheet, full_sheet, context_sheet):
        shutil.copy2(sheet, TEMP / sheet.name)

    data = {
        "schema": "fullfilm-v4-historical-emotes.2",
        "review_date": DATE,
        "status": "29 distinct image assets plus clearly separated context references",
        "unique_emote_count": len(items),
        "unique_export_sha256_count": len(set(unique_hashes)),
        "contact_sheet": str(full_sheet.relative_to(ROOT)),
        "contact_sheet_qq2003": str(qq_sheet.relative_to(ROOT)),
        "contact_sheet_context": str(context_sheet.relative_to(ROOT)),
        "temporary_contact_sheet": str((TEMP / full_sheet.name).resolve()),
        "source_scope": {
            "qq_2003": "20 differently named animated expressions; each exports exactly one first frame; the original GIF and map remain in art/reference.",
            "qq_archive_rights": "The source repository declares GPL-3.0; licensing of the underlying QQ artwork is not independently verified.",
            "cross_era": "Three Onion Head panels, one Baidu Tieba colored Huaji image, three early Rage Comic vectors, one Tuzki panel, and one 2016 Twemoji face.",
            "context_only": "Ali QQ Pinyin skin preview, VRChat+ feature image, one VRChat+ demo frame, and two transcribed 1982 text emoticons are not counted as distinct sticker assets.",
            "background_policy": "No automatic background erasure. White or patterned backgrounds are retained unless the original source includes alpha.",
        },
        "items": items,
        "context_items": contexts + texts,
    }
    data_path = ROOT / "data/fullfilm-v4-historical-emotes.json"
    data_path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({
        "unique_emotes": len(items),
        "unique_sha256": len(set(unique_hashes)),
        "context_items": len(contexts) + len(texts),
        "contact_sheet": str(full_sheet),
        "manifest": str(data_path),
    }, ensure_ascii=False))


if __name__ == "__main__":
    main()
