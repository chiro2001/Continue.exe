#!/usr/bin/env python3
"""Fetch archived pages or original image files for the full-film v2 history pack.

Archived HTML is saved under ~/tmp/vrc-old-web-map/fullfilm-v2/archives/raw and
rendered locally with Chromium. Display copies can apply explicitly listed
blur rectangles; the unmodified capture remains in the temporary archive.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import time
from datetime import datetime
from pathlib import Path
from typing import Any
from urllib.parse import urlparse

import requests
from PIL import Image, ImageFilter
from requests.adapters import HTTPAdapter
from urllib3.util.retry import Retry
from selenium import webdriver
from selenium.common.exceptions import TimeoutException


ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "data" / "fullfilm-v2-archive-materials.json"
DISPLAY_DIR = ROOT / "art" / "reference" / "fullfilm-v2-archives"
TEMP_DIR = Path.home() / "tmp" / "vrc-old-web-map" / "fullfilm-v2" / "archives"
RAW_DIR = TEMP_DIR / "raw"
USER_AGENT = (
    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/152.0.0.0 Safari/537.36"
)


def make_session() -> requests.Session:
    session = requests.Session()
    session.headers.update({"User-Agent": USER_AGENT})
    retry = Retry(
        total=2,
        connect=2,
        read=1,
        backoff_factor=0.3,
        status_forcelist=(429, 500, 502, 503, 504),
        allowed_methods=("GET",),
        raise_on_status=False,
    )
    session.mount("https://", HTTPAdapter(max_retries=retry))
    session.mount("http://", HTTPAdapter(max_retries=retry))
    return session


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def apply_display_processing(
    source: Path,
    output: Path,
    blur_rectangles: list[list[float]],
    mask_rectangles: list[list[float]],
) -> None:
    image = Image.open(source).convert("RGB")
    width, height = image.size
    for rect in blur_rectangles:
        x, y, w, h = rect
        box = (
            max(0, int(x * width)),
            max(0, int(y * height)),
            min(width, int((x + w) * width)),
            min(height, int((y + h) * height)),
        )
        if box[2] <= box[0] or box[3] <= box[1]:
            continue
        crop = image.crop(box)
        radius = max(8, min(crop.size) // 12)
        image.paste(crop.filter(ImageFilter.GaussianBlur(radius)), box)
    for rect in mask_rectangles:
        x, y, w, h = rect
        box = (
            max(0, int(x * width)),
            max(0, int(y * height)),
            min(width, int((x + w) * width)),
            min(height, int((y + h) * height)),
        )
        if box[2] > box[0] and box[3] > box[1]:
            image.paste((224, 224, 224), box)
    output.parent.mkdir(parents=True, exist_ok=True)
    image.save(output, format="PNG", optimize=True)


def render_archived_html(
    html_path: Path, png_path: Path, width: int, height: int
) -> tuple[int, int, int]:
    """Render archived HTML offline, with scripts and remote content disabled."""
    chromium = shutil.which("chromium") or shutil.which("chromium-browser")
    chromedriver = shutil.which("chromedriver")
    if not chromium or not chromedriver:
        raise RuntimeError("截图需要 Chromium 和 chromedriver；已下载 HTML，但未生成页面图")

    raw = html_path.read_bytes()
    static_bytes, scripts_removed = re.subn(
        rb"(?is)<script\b[^>]*>.*?</script\s*>", b"", raw
    )
    static_bytes, objects_removed = re.subn(
        rb"(?is)<object\b[^>]*>.*?</object\s*>", b"", static_bytes
    )
    static_bytes, embeds_removed = re.subn(
        rb"(?is)<embed\b[^>]*?/?>", b"", static_bytes
    )
    static_bytes, frames_removed = re.subn(
        rb"(?is)<iframe\b[^>]*>.*?</iframe\s*>", b"", static_bytes
    )
    static_bytes = re.sub(
        rb"(?i)\s+on(?:load|error|mouseover|mouseout|click)=([\"']).*?\1",
        b"",
        static_bytes,
    )
    static_html = html_path.with_name(html_path.stem + ".static.html")
    static_html.write_bytes(static_bytes)

    options = webdriver.ChromeOptions()
    options.binary_location = chromium
    options.page_load_strategy = "none"
    for argument in (
        "--headless=new",
        "--no-sandbox",
        "--disable-gpu",
        "--disable-background-networking",
        "--disable-extensions",
        "--hide-scrollbars",
        "--allow-file-access-from-files",
        "--host-resolver-rules=MAP * ~NOTFOUND",
        f"--window-size={width},{height}",
    ):
        options.add_argument(argument)

    driver = webdriver.Chrome(options=options)
    try:
        driver.set_page_load_timeout(7)
        try:
            driver.get(static_html.resolve().as_uri())
        except TimeoutException:
            driver.execute_script("window.stop()")
        time.sleep(0.6)
        broken = driver.execute_script(
            "return Array.from(document.images).filter(i => !i.complete || i.naturalWidth === 0).length"
        )
        if not driver.save_screenshot(str(png_path)):
            raise RuntimeError("WebDriver 拒绝保存截图")
        if not png_path.exists() or png_path.stat().st_size < 5000:
            raise RuntimeError("Chromium 生成的截图小于 5 KB")
        return int(broken or 0), scripts_removed, objects_removed + embeds_removed + frames_removed
    finally:
        driver.quit()


def archive_timestamp(url: str) -> str | None:
    match = re.search(r"/web/(\d{14})", url)
    if not match:
        return None
    stamp = match.group(1)
    return f"{stamp[:4]}-{stamp[4:6]}-{stamp[6:8]}T{stamp[8:10]}:{stamp[10:12]}:{stamp[12:14]}Z"


def validate_payload(response: requests.Response, kind: str) -> None:
    if response.status_code != 200:
        raise RuntimeError(f"HTTP {response.status_code}")
    content_type = response.headers.get("content-type", "").lower()
    if kind == "wayback-html":
        if "html" not in content_type:
            raise RuntimeError(f"存档返回非 HTML 内容：{content_type or 'unknown'}")
        lower = response.content[:3000].lower()
        if b"site not found" in lower or b"no web site is configured" in lower:
            raise RuntimeError("Internet Archive 返回 Site Not Found")
        if b"err_connection_closed" in lower or b"this site can't be reached" in lower:
            raise RuntimeError("存档页面不是成功取得的历史页面")
    elif not content_type.startswith("image/"):
        raise RuntimeError(f"原图链接返回非图像内容：{content_type or 'unknown'}")


def acquire(item: dict[str, Any], session: requests.Session) -> None:
    item_id = item["id"]
    kind = item.get("asset_kind", "wayback-html")
    image_url = item["image_url"]
    filename = item.get("filename") or f"{item_id}.png"
    raw_base = RAW_DIR / item_id
    display_path = DISPLAY_DIR / filename
    RAW_DIR.mkdir(parents=True, exist_ok=True)
    DISPLAY_DIR.mkdir(parents=True, exist_ok=True)

    cached_html = item.get("cached_html_path") if kind == "wayback-html" else None
    if cached_html and Path(cached_html).is_file():
        response_url = image_url
        response_content = Path(cached_html).read_bytes()
        if len(response_content) < 256 or b"<" not in response_content[:4096]:
            raise RuntimeError("缓存文件不是可渲染的存档 HTML/HTML片段")
        item["archive_html_source_path"] = str(Path(cached_html))
    else:
        response = session.get(image_url, timeout=(8, 20))
        validate_payload(response, kind)
        response_url = response.url
        response_content = response.content
        item["image_url"] = response.url
    item["capture_date"] = archive_timestamp(response_url) or item.get("capture_date")

    if kind == "wayback-html":
        html_path = raw_base.with_suffix(".html")
        raw_capture = raw_base.with_suffix(".png")
        html_path.write_bytes(response_content)
        item["raw_html_local_path"] = str(html_path)
        item["raw_html_sha256"] = sha256(html_path)
        missing_images, scripts_removed, embedded_removed = render_archived_html(
            html_path,
            raw_capture,
            int(item.get("render_width", 1600)),
            int(item.get("render_height", 1100)),
        )
        item["render_missing_image_count"] = missing_images
        item["render_scripts_removed"] = scripts_removed
        item["render_embedded_content_removed"] = embedded_removed
    else:
        suffix = Path(urlparse(response.url).path).suffix.lower()
        if suffix not in (".png", ".jpg", ".jpeg", ".webp", ".gif", ".tif", ".tiff"):
            suffix = ".image"
        raw_capture = raw_base.with_suffix(suffix)
        raw_capture.write_bytes(response.content)

    raw_image = Image.open(raw_capture)
    raw_width, raw_height = raw_image.size
    raw_hash = sha256(raw_capture)
    apply_display_processing(
        raw_capture,
        display_path,
        item.get("redact_rects", []),
        item.get("mask_rects", []),
    )
    with Image.open(display_path) as display:
        display_width, display_height = display.size

    if kind == "wayback-html":
        processing = "Chromium 将 Internet Archive 返回的同期 HTML 在本地离线静态渲染；"
        processing += "屏蔽所有网络资源、禁用页面脚本，并移除iframe/Flash嵌入，避免当前网站内容混入旧页。"
        if item.get("render_missing_image_count"):
            processing += f"本地渲染时有 {item['render_missing_image_count']} 个图像资源未能载入。"
        if item.get("render_embedded_content_removed"):
            processing += f"另有 {item['render_embedded_content_removed']} 个旧嵌入对象/框架未恢复。"
        if item.get("mask_rects"):
            reason = item.get("mask_reasons") or "按数据条目标记遮盖非展示内容"
            processing += f"按标记区域用中性灰遮盖内容：{reason}。"
        if item.get("redact_rects"):
            reason = item.get("redact_reasons") or "匿名化处理可识别用户名或头像"
            processing += f"按标记区域模糊处理：{reason}。"
        processing += "原始 HTML 与重渲染前图片保存在临时目录。"
    else:
        processing = "保留来源原图。"
        if item.get("redact_rects"):
            processing += "按标记区域模糊处理可识别用户名或头像。"

    item.update(
        {
            "download_status": "downloaded",
            "raw_local_path": str(raw_capture),
            "source_sha256": raw_hash,
            "source_width": raw_width,
            "source_height": raw_height,
            "local_path": display_path.relative_to(ROOT).as_posix(),
            "sha256": sha256(display_path),
            "width": display_width,
            "height": display_height,
            "display_processing": processing,
            "screenshot_date": datetime.now().astimezone().isoformat(timespec="seconds"),
            "error": None,
        }
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--limit", type=int, default=0, help="只处理前 N 个待下载项目")
    parser.add_argument("--only", nargs="*", help="只处理指定 ID")
    parser.add_argument("--retry-failed", action="store_true", help="重新尝试此前失败的项目")
    args = parser.parse_args()
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    items = manifest["items"]
    selected = [
        item
        for item in items
        if (not args.only or item["id"] in args.only)
        and (
            item.get("download_status") in (None, "pending")
            or (args.retry_failed and item.get("download_status") == "failed")
            or bool(args.only)
        )
    ]
    if args.limit:
        selected = selected[: args.limit]
    session = make_session()
    for index, item in enumerate(selected, 1):
        try:
            acquire(item, session)
            print(
                f"[{index}/{len(selected)}] {item['id']} 下载完成 "
                f"{item['width']}×{item['height']} {item['sha256'][:12]}",
                flush=True,
            )
        except Exception as exc:  # Keep failures explicit in the source ledger.
            item.update(
                {
                    "download_status": "failed",
                    "local_path": None,
                    "sha256": None,
                    "width": None,
                    "height": None,
                    "error": f"{type(exc).__name__}: {exc}",
                }
            )
            print(f"[{index}/{len(selected)}] {item['id']} 失败：{exc}", flush=True)
        manifest["downloaded_count"] = sum(
            x.get("download_status") == "downloaded" for x in items
        )
        manifest["failed_count"] = sum(
            x.get("download_status") == "failed" for x in items
        )
        manifest["downloaded_ids"] = [
            x["id"] for x in items if x.get("download_status") == "downloaded"
        ]
        manifest["failed_ids"] = [
            x["id"] for x in items if x.get("download_status") == "failed"
        ]
        MANIFEST.write_text(
            json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
        )
        time.sleep(float(item.get("request_delay_seconds", 2.0)))
    manifest["downloaded_count"] = sum(x.get("download_status") == "downloaded" for x in items)
    manifest["failed_count"] = sum(x.get("download_status") == "failed" for x in items)
    manifest["downloaded_ids"] = [x["id"] for x in items if x.get("download_status") == "downloaded"]
    manifest["failed_ids"] = [x["id"] for x in items if x.get("download_status") == "failed"]
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
