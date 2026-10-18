#!/usr/bin/env python3
"""Prepare four dated forum/Tieba emote sets and contact sheets.

Source files live under art/reference/fullfilm-v5/emotes/.  The script does not
download from the network: source retrieval and version verification are kept
separate from deterministic image preparation.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
REFERENCE = ROOT / "art/reference/fullfilm-v5/emotes"
CURRENT_REFERENCE = REFERENCE / "tieba-current-2026"
EXPORT = ROOT / "art/export/fullfilm-v5/emotes"
OUT_MANIFEST = ROOT / "data/fullfilm-v5-emotes.json"
LEGACY_MANIFEST = ROOT / "data/fullfilm-v4-historical-emotes.json"
CURRENT_INFO = CURRENT_REFERENCE / "emotion_info.json"
CURRENT_PKGINFO = CURRENT_REFERENCE / "pkginfo.json"
MIDDLE_PREVIEW = REFERENCE / "tieba-middle-2014-pack-preview.gif"
FORUM_REFERENCE = REFERENCE / "forum-phpbb-3.0.8"
FORUM_SOURCE_MANIFEST = FORUM_REFERENCE / "source-manifest.json"
MOBILE_REFERENCE = REFERENCE / "tieba-mobile-2020"
MOBILE_SOURCE_MANIFEST = MOBILE_REFERENCE / "source-manifest.json"

# The 2014 listing preview has a 7×4 table.  The lower half is partly crossed
# by a watermark; use the first two clean rows only.
MIDDLE_CENTERS = [
    (42, 60), (104, 60), (166, 60), (228, 60), (290, 60), (352, 60), (414, 60),
    (42, 121), (104, 121), (166, 121), (228, 121), (290, 121), (352, 121), (414, 121),
]
MIDDLE_CELL_SIZE = 44
MIDDLE_SCALE = 3
MIDDLE_BACKGROUND = (246, 251, 254)
MIDDLE_BACKGROUND_DISTANCE = 28


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def has_source_alpha(image: Image.Image) -> bool:
    """Check true alpha or a palette transparency table without discarding it."""
    if "A" in image.getbands():
        return image.getchannel("A").getextrema()[0] < 255
    return "transparency" in image.info


def font(size: int) -> ImageFont.ImageFont:
    candidates = [
        ROOT / "art/export/fullfilm-v4/NotoSansCJKsc-Regular.otf",
        Path("/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc"),
        Path("/usr/share/fonts/truetype/noto/NotoSansCJK-Regular.ttc"),
    ]
    for path in candidates:
        if path.exists():
            try:
                return ImageFont.truetype(str(path), size=size)
            except OSError:
                pass
    return ImageFont.load_default()


def remove_connected_preview_background(image: Image.Image) -> Image.Image:
    """Make only the near-uniform cell background connected to crop edges clear."""
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    width, height = rgba.size
    bg = MIDDLE_BACKGROUND

    def is_background(x: int, y: int) -> bool:
        r, g, b, _ = pixels[x, y]
        distance = abs(r - bg[0]) + abs(g - bg[1]) + abs(b - bg[2])
        return distance <= MIDDLE_BACKGROUND_DISTANCE and b >= 230

    seen: set[tuple[int, int]] = set()
    stack = [(x, 0) for x in range(width)] + [(x, height - 1) for x in range(width)]
    stack += [(0, y) for y in range(height)] + [(width - 1, y) for y in range(height)]
    while stack:
        x, y = stack.pop()
        if (x, y) in seen or not is_background(x, y):
            continue
        seen.add((x, y))
        pixels[x, y] = (pixels[x, y][0], pixels[x, y][1], pixels[x, y][2], 0)
        if x:
            stack.append((x - 1, y))
        if x + 1 < width:
            stack.append((x + 1, y))
        if y:
            stack.append((x, y - 1))
        if y + 1 < height:
            stack.append((x, y + 1))
    return rgba


def paste_thumbnail(canvas: Image.Image, image_path: Path, box: tuple[int, int, int, int]) -> None:
    x, y, width, height = box
    with Image.open(image_path) as source:
        image = source.convert("RGBA")
    image.thumbnail((width - 18, height - 42), Image.Resampling.LANCZOS)
    backdrop = Image.new("RGBA", (width - 18, height - 42), (238, 241, 244, 255))
    backdrop.alpha_composite(image, ((backdrop.width - image.width) // 2, (backdrop.height - image.height) // 2))
    canvas.paste(backdrop.convert("RGB"), (x + 9, y + 26))


def draw_contact_sheet(
    path: Path,
    title: str,
    items: list[tuple[str, Path]],
    columns: int = 6,
    cell_width: int = 180,
    cell_height: int = 150,
) -> None:
    rows = (len(items) + columns - 1) // columns
    header_height = 48
    canvas = Image.new("RGB", (columns * cell_width, header_height + rows * cell_height), (32, 35, 40))
    draw = ImageDraw.Draw(canvas)
    title_font = font(20)
    label_font = font(12)
    draw.text((12, 8), title, font=title_font, fill=(245, 246, 248))
    for i, (label, image_path) in enumerate(items):
        x = (i % columns) * cell_width
        y = header_height + (i // columns) * cell_height
        draw.text((x + 8, y + 4), label[:28], font=label_font, fill=(245, 246, 248))
        paste_thumbnail(canvas, image_path, (x, y, cell_width, cell_height))
    canvas.save(path, optimize=True)


def main() -> None:
    EXPORT.mkdir(parents=True, exist_ok=True)
    for stale in EXPORT.glob("tieba-middle-2014-preview-*.png"):
        try:
            number = int(stale.stem.rsplit("-", 1)[-1])
        except ValueError:
            continue
        if number > len(MIDDLE_CENTERS):
            stale.unlink()
    if not MIDDLE_PREVIEW.exists():
        raise FileNotFoundError(f"Missing source preview: {MIDDLE_PREVIEW}")
    if not CURRENT_INFO.exists() or not CURRENT_PKGINFO.exists():
        raise FileNotFoundError("Missing current Tieba preset metadata in the reference directory")
    if not FORUM_SOURCE_MANIFEST.exists() or not MOBILE_SOURCE_MANIFEST.exists():
        raise FileNotFoundError("Missing dated phpBB or 2020 Tieba source manifest")

    middle_pngs: list[dict] = []
    with Image.open(MIDDLE_PREVIEW) as source:
        source_frame = source.convert("RGB")
        preview_width, preview_height = source_frame.size
        half = MIDDLE_CELL_SIZE // 2
        for index, (cx, cy) in enumerate(MIDDLE_CENTERS, start=1):
            box = (cx - half, cy - half, cx + half, cy + half)
            if box[0] < 0 or box[1] < 0 or box[2] > preview_width or box[3] > preview_height:
                raise ValueError(f"Crop {index} is outside the original preview: {box}")
            crop = source_frame.crop(box)
            crop = remove_connected_preview_background(crop)
            crop = crop.resize((MIDDLE_CELL_SIZE * MIDDLE_SCALE, MIDDLE_CELL_SIZE * MIDDLE_SCALE), Image.Resampling.NEAREST)
            output = EXPORT / f"tieba-middle-2014-preview-{index:02d}.png"
            crop.save(output, optimize=True)
            middle_pngs.append({
                "id": f"tieba-middle-2014-preview-{index:02d}",
                "caption": None,
                "preview_cell_row_col_1based": [(index - 1) // 7 + 1, (index - 1) % 7 + 1],
                "source_crop_xyxy_px": list(box),
                "source_path": "art/reference/fullfilm-v5/emotes/tieba-middle-2014-pack-preview.gif",
                "source_sha256": sha256(MIDDLE_PREVIEW),
                "export_path": f"art/export/fullfilm-v5/emotes/{output.name}",
                "export_sha256": sha256(output),
                "native_crop_size_px": [MIDDLE_CELL_SIZE, MIDDLE_CELL_SIZE],
                "export_size_px": list(crop.size),
                "background_processing": "Connected near-uniform pale-blue cell background removed; source cell cropped and enlarged 3× with nearest-neighbor interpolation. No expression names inferred from the preview.",
            })

    forum_source = read_json(FORUM_SOURCE_MANIFEST)
    forum_pngs: list[dict] = []
    forum_sheet_items: list[tuple[str, Path]] = []
    for item in forum_source["items"]:
        source_path = FORUM_REFERENCE / item["filename"]
        with Image.open(source_path) as source:
            frame_count = getattr(source, "n_frames", 1)
            source.seek(0)
            image = source.convert("RGBA")
            original_size = image.size
            alpha = has_source_alpha(source)
        output = EXPORT / f"forum-phpbb-3.0.8-{Path(item['filename']).stem}.png"
        image = image.resize((original_size[0] * 4, original_size[1] * 4), Image.Resampling.NEAREST)
        image.save(output, optimize=True)
        row = {
            "id": f"forum-phpbb-3.0.8-{Path(item['filename']).stem}",
            "label": Path(item["filename"]).stem.replace("icon_", "").replace("e_", ""),
            "source_path": f"art/reference/fullfilm-v5/emotes/forum-phpbb-3.0.8/{item['filename']}",
            "source_sha256": sha256(source_path),
            "source_format": "GIF",
            "source_size_px": list(original_size),
            "source_frame_count": frame_count,
            "source_has_transparency": alpha,
            "export_path": f"art/export/fullfilm-v5/emotes/{output.name}",
            "export_sha256": sha256(output),
            "export_size_px": list(image.size),
            "background_processing": "First GIF frame decoded to RGBA and enlarged 4× with nearest-neighbor interpolation; source transparency preserved.",
        }
        forum_pngs.append(row)
        forum_sheet_items.append((f"phpBB {row['label']}", output))

    mobile_source = read_json(MOBILE_SOURCE_MANIFEST)
    mobile_pngs: list[dict] = []
    mobile_sheet_items: list[tuple[str, Path]] = []
    for item in mobile_source["selected_emotes"]:
        source_path = MOBILE_REFERENCE / item["filename"]
        with Image.open(source_path) as source:
            image = source.convert("RGBA")
            original_size = image.size
            alpha = has_source_alpha(source)
        output = EXPORT / f"tieba-mobile-2020-{item['id'].rsplit('-', 1)[-1]}.png"
        image.save(output, optimize=True)
        row = {
            "id": item["id"],
            "label": f"素材 {item['id'].rsplit('-', 1)[-1]}",
            "source_path": f"art/reference/fullfilm-v5/emotes/tieba-mobile-2020/{item['filename']}",
            "source_sha256": sha256(source_path),
            "source_format": "PNG",
            "source_size_px": list(original_size),
            "source_has_transparency": alpha,
            "export_path": f"art/export/fullfilm-v5/emotes/{output.name}",
            "export_sha256": sha256(output),
            "export_size_px": list(image.size),
            "background_processing": "PNG decoded to RGBA; palette transparency preserved, no crop or resizing. No expression names inferred from the numbered source files.",
        }
        mobile_pngs.append(row)
        mobile_sheet_items.append((f"2020 {row['label']}", output))

    current_info = read_json(CURRENT_INFO)
    pkginfo = read_json(CURRENT_PKGINFO)
    package = current_info["packages"][0]
    source_items = {item["id"]: item for item in package["emoticons"]}
    ordered_ids = package.get("panel_emoticons") or [item["id"] for item in package["emoticons"]]
    current_pngs: list[dict] = []
    current_sheet_items: list[tuple[str, Path]] = []
    for order, emote_id in enumerate(ordered_ids, start=1):
        item = source_items[emote_id]
        source_path = CURRENT_REFERENCE / item["icon"]
        if not source_path.exists():
            raise FileNotFoundError(source_path)
        with Image.open(source_path) as source:
            image = source.convert("RGBA")
            original_size = image.size
            has_alpha = has_source_alpha(source)
        output = EXPORT / f"tieba-current-2026-{emote_id}.png"
        image.save(output, optimize=True)
        row = {
            "id": emote_id,
            "text": item["text"],
            "panel_order_1based": order,
            "source_path": f"art/reference/fullfilm-v5/emotes/tieba-current-2026/{item['icon']}",
            "source_sha256": sha256(source_path),
            "source_format": "WebP",
            "source_size_px": list(original_size),
            "source_has_transparency": has_alpha,
            "export_path": f"art/export/fullfilm-v5/emotes/{output.name}",
            "export_sha256": sha256(output),
            "export_size_px": list(image.size),
            "background_processing": "WebP decoded to RGBA PNG; original alpha preserved, no crop, recolor, or resizing.",
        }
        current_pngs.append(row)
        current_sheet_items.append((f"{emote_id} {item['text']}", output))

    draw_contact_sheet(
        EXPORT / "contact-sheet-forum-phpbb-2010.png",
        "phpBB 3.0.8 默认论坛表情｜release 2010-11-20｜8 枚",
        forum_sheet_items,
    )
    draw_contact_sheet(
        EXPORT / "contact-sheet-tieba-middle-2014.png",
        "2014 贴吧泡泡包目录预览｜14 格干净裁切（无原始表情名）",
        [(row["id"].split("preview-")[-1], ROOT / row["export_path"]) for row in middle_pngs],
    )
    draw_contact_sheet(
        EXPORT / "contact-sheet-tieba-mobile-2020.png",
        "贴吧移动端历史快照｜11.6.8.2 / README 标注 2020-07-13｜8 枚编号图",
        mobile_sheet_items,
    )
    draw_contact_sheet(
        EXPORT / "contact-sheet-tieba-current-2026.png",
        "百度贴吧当前默认表情｜APK 22.12.1.0 / 2026-09-24｜按面板顺序",
        current_sheet_items,
    )
    overview = (
        forum_sheet_items
        + [(f"Tieba14 {i:02d}", ROOT / row["export_path"]) for i, row in enumerate(middle_pngs, start=1)]
        + mobile_sheet_items
        + [(f"Tieba26 {row['id'][-3:]} {row['text']}", ROOT / row["export_path"]) for row in current_pngs]
    )
    draw_contact_sheet(
        EXPORT / "contact-sheet-fullfilm-v5-emotes.png",
        "论坛 phpBB → 贴吧 2014 目录预览 → 贴吧 2020 手机版 → 贴吧 2026 当前包",
        overview,
        columns=8,
        cell_width=150,
        cell_height=136,
    )

    apk_meta = read_json(REFERENCE / "tieba-current-2026-apk-provenance.json")
    flat_items: list[dict] = []

    def flatten_items(
        assets: list[dict],
        timeline_group: str,
        windows: list[list[float]],
        source_title: str,
        source_publisher: str,
        source_version_or_date: str,
        source_url: str,
    ) -> None:
        for asset in assets:
            flat_items.append({
                "id": asset["id"],
                "file": asset["export_path"],
                "source_file": asset["source_path"],
                "sha256": asset["export_sha256"],
                "timeline_group": timeline_group,
                "time_windows_seconds": windows,
                "source_title": source_title,
                "source_publisher_or_author": source_publisher,
                "source_version_or_date": source_version_or_date,
                "source_url": source_url,
                "caption": asset.get("text", asset.get("caption", asset.get("label"))),
            })

    forum_windows = [[51.2, 71.68]]
    middle_windows = [[71.68, 86.4]]
    mobile_windows = [[86.4, 112.64]]
    current_windows = [[112.64, 186.88], [235.52, 249.6]]
    flatten_items(forum_pngs, "forum-phpbb-3.0.8", forum_windows, "phpBB 3.0.8 default smilies", "phpBB Project", "release-3.0.8 / 2010-11-20", forum_source["upstream_repository"])
    flatten_items(middle_pngs, "tieba-middle-pack-listing-2014", middle_windows, "百度贴吧泡泡 QQ 表情包 +50p preview", "JB51 listing; underlying pack author unknown", "listing updated 2014-09-11", "https://www.jb51.net/softs/216567.html")
    flatten_items(mobile_pngs, "tieba-mobile-snapshot-2020", mobile_windows, "Baidu Tieba mobile default emotes snapshot", "microlong666 extraction repository; underlying artwork by Baidu/Tieba", "app 11.6.8.2; README says updated 2020-07-13", mobile_source["repository_url"])
    flatten_items(current_pngs, "tieba-current-default-pack-2026", current_windows, "Baidu Tieba current mobile default emoji preset", "Baidu Tieba", f"app {apk_meta['app_version']} / {apk_meta['app_updated']}", apk_meta["listing_source_url"])

    manifest = {
        "schema": "fullfilm-v5-emotes.1",
        "generated_by": "tools/prepare_fullfilm_v5_emotes.py",
        "generated_on": "2026-10-18",
        "scope_note": "Four dated sets keep forum-native PHPBB smilies distinct from QQ/IM references, then show Tieba's 2014 catalog preview, a separately identified 2020 mobile extraction, and the actual current 2026 app preset. All dates describe release/listing/app evidence, not inferred drawing creation dates.",
        "timeline_groups": [
            {
                "id": "forum-phpbb-3.0.8",
                "display_name": "phpBB 3.0.8 default forum smilies",
                "release_date": forum_source["release_date"],
                "release_tag": forum_source["tag"],
                "date_basis": "Official phpBB release announcement dated 2010-11-20; source GIFs are from the release tag's phpBB/images/smilies directory.",
                "current_placement_seconds": [],
                "recommendation_seconds": forum_windows,
                "source_url": forum_source["release_announcement_url"],
                "source_repository": forum_source["upstream_repository"],
                "license": forum_source["license"],
                "license_file": "art/reference/fullfilm-v5/emotes/forum-phpbb-3.0.8/COPYING",
                "rights_note": "The release package includes GPL-2.0 COPYING. The exhibition should credit the phpBB Project and preserve the individual asset notices/terms.",
                "assets": forum_pngs,
            },
            {
                "id": "tieba-middle-pack-listing-2014",
                "display_name": "百度贴吧泡泡 QQ 表情包 +50p（第三方目录快照）",
                "listing_update_date": "2014-09-11",
                "listing_date_basis": "JB51 listing field `更新时间：2014-09-11`; this dates the listing snapshot, not the original pack's creation.",
                "original_pack_creation_date": None,
                "current_placement_seconds": [],
                "recommendation_seconds": middle_windows,
                "source_page_url": "https://www.jb51.net/softs/216567.html",
                "preview_source_url": "https://img.jbzj.com/do/uploads/litimg/140911/10234433Q91.gif",
                "original_preview_path": "art/reference/fullfilm-v5/emotes/tieba-middle-2014-pack-preview.gif",
                "original_preview_sha256": sha256(REFERENCE / "tieba-middle-2014-pack-preview.gif"),
                "supporting_2013_listing": {
                    "listing_update_date": "2013-09-10",
                    "source_page_url": "https://www.jb51.net/softs/103281.html",
                    "preview_path": "art/reference/fullfilm-v5/emotes/tieba-2013-pack-preview.gif",
                    "preview_sha256": sha256(REFERENCE / "tieba-2013-pack-preview.gif"),
                    "note": "The 2013 and 2014 listing previews show the same 7×4 default-pack family; they are not counted as two distinct artwork generations.",
                },
                "rights_note": "Third-party catalog thumbnails and Tieba artwork rights are not cleared here; preserve attribution and treat as internal historical reference.",
                "uncertainty": "The original .rar download is no longer available at the listing path. The 14 isolated files are transparent crops from the clean upper rows of the visible listing preview and intentionally have no guessed expression captions.",
                "assets": middle_pngs,
            },
            {
                "id": "tieba-mobile-snapshot-2020",
                "display_name": "Baidu Tieba mobile default emotes snapshot",
                "host_app_version": mobile_source["app_version"],
                "host_app_updated": mobile_source["app_update_date"],
                "host_app_updated_evidence": "art/reference/fullfilm-v5/emotes/tieba-mobile-2020/README.md",
                "repository_created_at": mobile_source["repository_created_at"],
                "date_basis": mobile_source["date_basis"],
                "current_placement_seconds": [],
                "recommendation_seconds": mobile_windows,
                "source_repository_url": mobile_source["repository_url"],
                "source_readme": "art/reference/fullfilm-v5/emotes/tieba-mobile-2020/README.md",
                "rights_note": mobile_source["rights_note"],
                "uncertainty": "The upstream extraction has numbered files without per-image captions. These eight figures are identified by source file number only; this 2020 snapshot is not described as the current 2026 app pack.",
                "assets": mobile_pngs,
            },
            {
                "id": "tieba-current-default-pack-2026",
                "display_name": "百度贴吧移动端默认 emoji 预设",
                "host_app_package": "com.baidu.tieba",
                "host_app_version": apk_meta["app_version"],
                "host_app_updated": apk_meta["app_updated"],
                "host_app_updated_evidence": apk_meta["listing_source_url"],
                "preset_package_name": pkginfo["package_name"],
                "preset_version": pkginfo["version_name"],
                "preset_version_code": pkginfo["version_code"],
                "host_min_version": pkginfo["host_min_version"],
                "current_placement_seconds": [],
                "recommendation_seconds": current_windows,
                "asset_archive_path": "art/reference/fullfilm-v5/emotes/tieba-current-2026/com.baidu.spswitch.emotion.preset.zip",
                "asset_archive_sha256": sha256(CURRENT_REFERENCE / "com.baidu.spswitch.emotion.preset.zip"),
                "source_apk": apk_meta,
                "rights_note": "Extracted from Baidu Tieba's app package; Baidu retains the underlying artwork rights. Use only as internally reviewed reference unless exhibition rights are cleared.",
                "assets": current_pngs,
            },
        ],
        "legacy_cross_service_context_not_counted_as_forum_source": {
            "asset_ids": [
                item["id"] for item in read_json(LEGACY_MANIFEST)["items"]
                if item["id"].startswith("qq-2003-") or item["id"].startswith("onionhead-2007-")
            ],
            "note": "These existing sources may be referenced as QQ/IM or cross-service Onion Head context. QQ assets are not forum-native, and the Sohu 2007 four-panel image is not a verified original forum sticker pack; they are excluded from the new flat binding list.",
        },
        "items": flat_items,
        "contact_sheets": [
            "art/export/fullfilm-v5/emotes/contact-sheet-forum-phpbb-2010.png",
            "art/export/fullfilm-v5/emotes/contact-sheet-tieba-middle-2014.png",
            "art/export/fullfilm-v5/emotes/contact-sheet-tieba-mobile-2020.png",
            "art/export/fullfilm-v5/emotes/contact-sheet-tieba-current-2026.png",
            "art/export/fullfilm-v5/emotes/contact-sheet-fullfilm-v5-emotes.png",
        ],
        "counts": {
            "forum_phpbb_2010": len(forum_pngs),
            "middle_listing_preview_cells": len(middle_pngs),
            "tieba_mobile_2020_snapshot": len(mobile_pngs),
            "current_official_preset_items": len(current_pngs),
            "flat_binding_items": len(flat_items),
            "current_export_pngs_preserving_source_alpha": sum(1 for item in current_pngs if item["source_has_transparency"]),
        },
    }
    OUT_MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(manifest["counts"], ensure_ascii=False))
    print(f"Wrote {OUT_MANIFEST.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
