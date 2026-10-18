#!/usr/bin/env python3
"""Render the static FullFilm V5 credits pages from the verified JSON manifest."""

from __future__ import annotations

import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "data/fullfilm-v5-eac-credits.json"
FONT_PATH = ROOT / "art/export/fullfilm-v4/NotoSansCJKsc-Regular.otf"
CANVAS = (2048, 1152)
MARGIN_X = 84
CONTENT_W = CANVAS[0] - MARGIN_X * 2
COLUMNS = (180, 470, 420, CONTENT_W - 180 - 470 - 420)

BG = (7, 15, 27)
BOARD = (10, 22, 38)
ROW_A = (14, 29, 48)
ROW_B = (17, 34, 54)
RULE = (41, 66, 86)
ACCENT = (239, 195, 113)
ACCENT_BLUE = (102, 194, 223)
TEXT = (238, 243, 248)
MUTED = (166, 183, 199)


def font(size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(FONT_PATH), size=size)


def wrap_text(draw: ImageDraw.ImageDraw, text: str, face: ImageFont.FreeTypeFont, width: int) -> list[str]:
    lines: list[str] = []
    for paragraph in text.split("\n"):
        current = ""
        for char in paragraph:
            candidate = current + char
            if current and draw.textlength(candidate, font=face) > width:
                lines.append(current)
                current = char
            else:
                current = candidate
        lines.append(current)
    return lines or [""]


def draw_cell(
    draw: ImageDraw.ImageDraw,
    text: str,
    box: tuple[int, int, int, int],
    face: ImageFont.FreeTypeFont,
    fill: tuple[int, int, int],
    line_gap: int,
    max_lines: int = 2,
) -> None:
    x0, y0, x1, y1 = box
    inset_x = 20
    max_width = x1 - x0 - inset_x * 2
    lines = wrap_text(draw, text, face, max_width)
    if len(lines) > max_lines:
        lines = lines[:max_lines]
        while lines[-1] and draw.textlength(lines[-1] + "…", font=face) > max_width:
            lines[-1] = lines[-1][:-1]
        lines[-1] = lines[-1].rstrip() + "…"
    text_h = len(lines) * line_gap
    y = y0 + (y1 - y0 - text_h) // 2 - 1
    for line in lines:
        draw.text((x0 + inset_x, y), line, font=face, fill=fill)
        y += line_gap


def render_page(page: dict, page_number: int, total_pages: int) -> Path:
    image = Image.new("RGB", CANVAS, BG)
    draw = ImageDraw.Draw(image)

    draw.rounded_rectangle((34, 30, CANVAS[0] - 34, CANVAS[1] - 30), radius=32, fill=BOARD, outline=(36, 60, 82), width=2)
    draw.rounded_rectangle((MARGIN_X, 58, MARGIN_X + 7, 166), radius=4, fill=ACCENT_BLUE)
    draw.text((MARGIN_X + 31, 52), page["title"], font=font(60), fill=TEXT)
    draw.text((MARGIN_X + 34, 133), page["subtitle"], font=font(26), fill=MUTED)
    badge = f"CREDITS   {page_number:02d} / {total_pages:02d}"
    badge_face = font(20)
    badge_w = int(draw.textlength(badge, font=badge_face))
    draw.text((CANVAS[0] - MARGIN_X - badge_w, 83), badge, font=badge_face, fill=ACCENT)
    draw.line((MARGIN_X, 186, CANVAS[0] - MARGIN_X, 186), fill=RULE, width=2)

    table_y = 208
    header_h = 64
    body_h = CANVAS[1] - table_y - header_h - 86
    row_h = body_h // len(page["rows"])
    x_positions = [MARGIN_X]
    for width in COLUMNS:
        x_positions.append(x_positions[-1] + width)

    draw.rounded_rectangle((MARGIN_X, table_y, CANVAS[0] - MARGIN_X, table_y + header_h), radius=12, fill=(23, 42, 60))
    labels = ("分类", "项目 / 作品", "作者", "来源")
    header_face = font(25)
    for i, label in enumerate(labels):
        draw.text((x_positions[i] + 20, table_y + 17), label, font=header_face, fill=ACCENT)
    for i in range(1, len(x_positions) - 1):
        draw.line((x_positions[i], table_y + 12, x_positions[i], table_y + header_h - 12), fill=RULE, width=1)

    row_face = font(23 if len(page["rows"]) <= 9 else 22)
    line_gap = 30 if len(page["rows"]) <= 9 else 28
    for row_index, row in enumerate(page["rows"]):
        y0 = table_y + header_h + row_index * row_h
        y1 = y0 + row_h
        fill = ROW_A if row_index % 2 == 0 else ROW_B
        draw.rounded_rectangle((MARGIN_X, y0 + 3, CANVAS[0] - MARGIN_X, y1 - 3), radius=8, fill=fill)
        if row_index:
            draw.line((MARGIN_X + 14, y0, CANVAS[0] - MARGIN_X - 14, y0), fill=RULE, width=1)
        values = (row["category"], row["work"], row["author"], row["source"])
        for column, value in enumerate(values):
            color = ACCENT_BLUE if column == 0 else TEXT
            draw_cell(
                draw,
                value,
                (x_positions[column], y0, x_positions[column + 1], y1),
                row_face,
                color,
                line_gap,
                max_lines=2,
            )
        for i in range(1, len(x_positions) - 1):
            draw.line((x_positions[i], y0 + 10, x_positions[i], y1 - 10), fill=RULE, width=1)

    footer_y = CANVAS[1] - 72
    draw.line((MARGIN_X, footer_y - 11, CANVAS[0] - MARGIN_X, footer_y - 11), fill=RULE, width=1)
    footer_face = font(19)
    footer = page.get("footer", "")
    draw.text((MARGIN_X + 2, footer_y), footer, font=footer_face, fill=MUTED)
    cx = CANVAS[0] - MARGIN_X - 12
    cy = footer_y + 14
    draw.polygon(((cx, cy - 8), (cx + 8, cy), (cx, cy + 8), (cx - 8, cy)), fill=ACCENT)

    out = ROOT / page["output_png"]
    out.parent.mkdir(parents=True, exist_ok=True)
    image.save(out, format="PNG", optimize=True)
    return out


def main() -> None:
    data = json.loads(MANIFEST.read_text(encoding="utf-8"))
    pages = data["credits"]["pages"]
    for index, page in enumerate(pages, start=1):
        output = render_page(page, index, len(pages))
        print(output.relative_to(ROOT).as_posix())


if __name__ == "__main__":
    main()
