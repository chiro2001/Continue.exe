#!/usr/bin/env python3
"""V5 identity assets: greetz atlas, window scroller strip, EDGE pixel logo, token/speed JSON and local previews.

Outputs
  art/export/episode123-v5/identity/greetz-atlas.png      4096x1024 RGBA  R cover, G outline, B glyph id, A style (255 = interrupt)
  art/export/episode123-v5/identity/window-scroller.png   4096x128  RGBA  same channels, one cyclic row
  art/export/episode123-v5/identity/cracktro-logo.png     512x144   RGBA  point-filtered EDGE logo + credit line
  data/episode123-v5-greetz.json                          layout, ring/window parameters, speed curve, tokens
  ~/tmp/vrc-old-web-map/episode123-v5/identity-preview/   flattened ring frames, review-camera frames, window mock-up

Atlas row mapping (shared with OWV5SineScroller.shader): row = floor(s / row_stride), x = s - row*row_stride + row_pad.
Every row stores row_pad extra pixels of the neighbouring text on both sides, so glyphs may straddle a row seam and
bilinear/mip/outline taps stay seamless. Names are anonymous labour roles only.
"""
import argparse
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "art/export/episode123-v5/identity"
DATA = ROOT / "data/episode123-v5-greetz.json"
PREVIEW = Path.home() / "tmp/vrc-old-web-map/episode123-v5/identity-preview"
FONT_PATH = "/usr/share/fonts/noto-cjk/NotoSansCJK-Bold.ttc"

# ---------------------------------------------------------------- content (anonymous labour roles only)
GREETING = "GREETINGS TO"
A_PHRASES = [
    ("tools", "写 README 的人"), ("tools", "打包绿色版的人"),
    ("translation", "把对白一句句译出来的人"), ("translation", "清字、嵌字、校对的人"),
    ("bt", "做种到天亮的人"), ("bt", "保种的人"),
    ("forums", "发帖说“已补档”的人"), ("forums", "回帖说“谢谢楼主”的人"), ("forums", "写签名档的人"),
    ("subtitles", "打时间轴的人"), ("subtitles", "压制的人"), ("subtitles", "写特效字幕的人"),
    ("making", "第一次上传视频的人"), ("making", "写教程的人"), ("making", "改骨骼、调材质的人"),
    ("hub", "搭第一个世界的人"), ("hub", "在 Hub 等新人的人"), ("hub", "教别人手势的人"), ("hub", "带人去下一个世界的人"),
]
B_PHRASES = [
    ("interrupt", "【需要登录】", 1), ("thanks", "仍然感谢", 0), ("thanks", "写迁移说明的人", 0),
    ("interrupt", "【此链接已失效】", 1), ("thanks", "做备份的人", 0), ("interrupt", "【请选择其一】", 1),
    ("thanks", "回答“还能用吗”的人", 0), ("interrupt", "【该功能不再支持】", 1), ("thanks", "还在维护的人", 0),
]
WINDOW_TEXT = ["EDGE", "FC14", "RHINO OF TORMENT", "4 CHANNELS", "80 MS PER ROW", "按下开始", "戴好耳机"]
FORBIDDEN = ["serial", "keygen", "generate", "crack", "patch", "register", "license", "password",
             "序列号", "注册码", "注册机", "破解", "补丁", "激活", "生成"]

# ---------------------------------------------------------------- atlas / ring constants
EM = 80            # glyph em in atlas pixels
BAND = 112         # atlas row height
BASE = 82          # baseline from band top
PAD = 16           # overlap pixels stored on each side of a row
AW, AH = 4096, 1024
STRIDE = AW - 2 * PAD
OUTLINE_R = 5
GRAD_TOP, GRAD_BOT = 13, 90
START_MARGIN = 56
LAT, CJK, SPACE, PUNCT, STAR, GAP = 0.97, 0.94, 0.22, 0.16, 0.62, 0.20   # advances in em

RING = dict(radius=10.0, centre_x=0.0, centre_z=-2.8, arc_degrees=200.0, segments=1024, baseline_y=5.5,
            em_m=0.42, amplitude=0.17, wavelength=6.4, omega=2 * math.pi / 2.56, hue_speed=0.125, end_fade=0.09,
            eye=[0.0, 1.35, -2.8])
RING["px_per_metre"] = EM / RING["em_m"]
OPACITY = dict(fade_in_start=30.72, fade_in_length=1.28, dim_start=81.92, dim_end=92.16, dim_opacity=0.55,
               dim_ramp=0.64, fade_out_length=0.8, duration=133.12)
CURVE = dict(start=30.72, step=0.32, a_front_greeting=31.2, a_front_last=100.6, b_start=102.4, b_blend_start=101.9,
             b_blend_length=1.0, b_ratio=0.55, b_hold_until=116.0, fade_out_begin=132.32, b_target_degrees=38.0)
WIN = dict(em_px=64, band=128, base=92, em_m=0.15, strip_width_m=2.5, segments=256, amplitude=0.028, wavelength=0.9,
           omega=3.2, scroll_px_per_s=130.0, end_fade=0.07)
LOGO = dict(width=512, height=144, metres_per_texel=0.0034)


def smooth(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3 - 2 * x)


# ---------------------------------------------------------------- fonts / glyph layout
def sc_face_index():
    for i in range(12):
        try:
            f = ImageFont.truetype(FONT_PATH, 20, index=i)
        except OSError:
            break
        name = " ".join(f.getname())
        if " SC" in name and "Mono" not in name:
            return i
    raise SystemExit("Simplified Chinese face not found in " + FONT_PATH)


class Face:
    def __init__(self, em):
        self.em = em
        self.font = ImageFont.truetype(FONT_PATH, em, index=sc_face_index())
        self.cache = {}

    def ink(self, ch):
        if ch not in self.cache:
            e = self.em
            img = Image.new("L", (e * 3, e * 3), 0)
            ImageDraw.Draw(img).text((e, e * 2), ch, font=self.font, anchor="ls", fill=255)
            cols = np.where(np.asarray(img).max(0) > 40)[0]
            self.cache[ch] = (int(cols.min()) - e, int(cols.max()) + 1 - e) if len(cols) else (0, 0)
        return self.cache[ch]

    def kind(self, ch):
        if ch == " ":
            return "space"
        if ch == "✦":
            return "star"
        if ch in "、“”【】·":
            return "punct"
        return "latin" if ord(ch) < 0x2E80 else "cjk"

    def place(self, ch, pen):
        """Returns (advance, draw_x) in pixels for a character whose cell starts at pen."""
        e, k = self.em, self.kind(ch)
        if k == "space":
            return e * SPACE, pen
        if k == "star":
            return e * STAR, pen
        if k == "latin":
            return self.font.getlength(ch) * LAT, pen
        left, right = self.ink(ch)
        adv = e * CJK if k == "cjk" else (right - left) + e * PUNCT
        return adv, pen + (adv - (right - left)) / 2 - left


def layout_tokens(face, tokens, pen, trailing_separator=True):
    """tokens: list of dicts with 'text'. Returns glyph list and fills token start/end. Separator = gap, star, gap."""
    glyphs = []
    gap = face.em * GAP
    for ti, token in enumerate(tokens):
        token["start"] = pen
        for ch in token["text"]:
            adv, dx = face.place(ch, pen)
            glyphs.append(dict(ch=ch, kind=face.kind(ch), pen=pen, adv=adv, x=dx, token=ti))
            pen += adv
        token["end"] = pen
        if ti < len(tokens) - 1 or trailing_separator:
            pen += gap
            adv = face.em * STAR
            glyphs.append(dict(ch="✦", kind="star", pen=pen, adv=adv, x=pen, token=-1))
            pen += adv + gap
    return glyphs, pen


def star_mask(width, em, size_scale=4):
    s = size_scale
    w, h = int(math.ceil(width)) + 4, em + 8
    img = Image.new("L", (w * s, h * s), 0)
    cx, cy = w * s / 2, (h - 4 - em * 0.36) * s
    ro, ri = em * 0.33 * s, em * 0.085 * s
    pts = []
    for i in range(8):
        r = ro if i % 2 == 0 else ri
        a = math.pi / 2 + i * math.pi / 4
        pts.append((cx + math.cos(a) * r * (1.0 if i % 2 == 0 else 1.0), cy - math.sin(a) * r * 1.18))
    ImageDraw.Draw(img).polygon(pts, fill=255)
    return img.resize((w, h), Image.LANCZOS), (w, h)


def render_strip(face, glyphs, width, height, base, offsets=(0,), id_start=0, interrupt_spans=()):
    """Draws glyphs into cover/id/style planes (uint8, height x width). Returns planes and glyph id count."""
    cover = Image.new("L", (width, height), 0)
    draw = ImageDraw.Draw(cover)
    ids = np.zeros((height, width), np.uint8)
    style = np.zeros((height, width), np.uint8)
    star_cache = {}
    for ox in offsets:
        idx = id_start
        for g in glyphs:
            if g["kind"] == "space":
                continue
            x0 = int(round(g["pen"] + ox))
            x1 = int(round(g["pen"] + g["adv"] + ox))
            if x1 > 0 and x0 < width:
                ids[:, max(0, x0):min(width, x1)] = idx % 256
            idx += 1
            if g["kind"] == "star":
                key = round(g["adv"], 1)
                if key not in star_cache:
                    star_cache[key] = star_mask(g["adv"], face.em)
                mask, (mw, mh) = star_cache[key]
                px = int(round(g["pen"] + ox - (mw - g["adv"]) / 2))
                layer = Image.new("L", (width, height), 0)
                layer.paste(mask, (px, base + 4 - mh))
                cover = Image.fromarray(np.maximum(np.asarray(cover), np.asarray(layer)))
                draw = ImageDraw.Draw(cover)
            else:
                draw.text((g["x"] + ox, base), g["ch"], font=face.font, anchor="ls", fill=255)
        for a, b in interrupt_spans:
            style[:, max(0, int(a + ox)):max(0, min(width, int(b + ox)))] = 255
    return np.asarray(cover).copy(), ids, style, idx


def dilate_disc(a, r):
    out = a.copy()
    h, w = a.shape
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if (dx or dy) and dx * dx + dy * dy <= r * r + r * 0.5:
                sh = np.zeros_like(a)
                ys, ye = max(0, dy), min(h, h + dy)
                xs, xe = max(0, dx), min(w, w + dx)
                sh[ys:ye, xs:xe] = a[ys - dy:ye - dy, xs - dx:xe - dx]
                np.maximum(out, sh, out=out)
    return out


def outline_plane(cover):
    c = cover.astype(np.float32) / 255
    d = dilate_disc(c, OUTLINE_R)
    img = Image.fromarray((d * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.1))
    o = np.clip(np.asarray(img).astype(np.float32) / 255 * 1.25, 0, 1)
    return (np.maximum(o, c) * 255).astype(np.uint8)


def cut_rows(planes, total, rows, band, stride, pad, atlas_w, atlas_h):
    cover, outline, ids, style = planes
    atlas = np.zeros((atlas_h, atlas_w, 4), np.uint8)
    for r in range(rows):
        ws = r * stride - pad
        for ci, plane in enumerate((cover, outline, ids, style)):
            x0, x1 = max(0, ws), min(plane.shape[1], ws + atlas_w)
            if x1 > x0:
                atlas[r * band:(r + 1) * band, x0 - ws:x1 - ws, ci] = plane[:, x0:x1]
    return atlas


# ---------------------------------------------------------------- speed curve
def build_curve(tokens, em_px, b_ratio, theta_target):
    greet, last_a = tokens[0], tokens[len(A_PHRASES)]
    xg, x19 = (greet["start"] + greet["end"]) / 2, (last_a["start"] + last_a["end"]) / 2
    c = CURVE
    va = (x19 - xg) / (c["a_front_last"] - c["a_front_greeting"])
    last = tokens[-1]
    x_last = (last["start"] + last["end"]) / 2
    target_front = x_last - math.radians(theta_target) * RING["radius"] * RING["px_per_metre"]
    dt = 0.005

    def profile(r_end):
        def speed(t):
            if t <= c["b_blend_start"]:
                return va
            if t <= c["b_blend_start"] + c["b_blend_length"]:
                return va * (1 + (b_ratio - 1) * smooth((t - c["b_blend_start"]) / c["b_blend_length"]))
            if t <= c["b_hold_until"]:
                return va * b_ratio
            if t <= c["fade_out_begin"]:
                return va * (b_ratio + (r_end - b_ratio) * (t - c["b_hold_until"]) / (c["fade_out_begin"] - c["b_hold_until"]))
            return va * r_end
        return speed

    x_start = xg - va * (c["a_front_greeting"] - c["start"])
    steps = int(round((OPACITY["duration"] - c["start"]) / dt))

    def cumulative(speed):
        x = np.empty(steps + 1)
        x[0] = x_start
        for i in range(steps):
            x[i + 1] = x[i] + speed(c["start"] + (i + 0.5) * dt) * dt
        return x

    fade_index = int(round((c["fade_out_begin"] - c["start"]) / dt))
    lo, hi = b_ratio, 2.0
    if cumulative(profile(lo))[fade_index] >= target_front:
        r_end = lo
    else:
        for _ in range(30):
            mid = (lo + hi) / 2
            if cumulative(profile(mid))[fade_index] < target_front:
                lo = mid
            else:
                hi = mid
        r_end = (lo + hi) / 2
    speed = profile(r_end)
    x = cumulative(speed)
    per = int(round(c["step"] / dt))
    n = steps // per + 1
    times = [c["start"] + i * c["step"] for i in range(n)]
    px = [float(x[i * per]) for i in range(n)]
    return dict(times=times, px=px, speed=speed, va=va, r_end=r_end, target_front=target_front, xg=xg, x19=x19)


def invert_time(curve, x):
    times, px = curve["times"], curve["px"]
    if x <= px[0]:
        return times[0]
    for i in range(len(px) - 1):
        if px[i] <= x <= px[i + 1] and px[i + 1] > px[i]:
            return times[i] + (x - px[i]) / (px[i + 1] - px[i]) * (times[i + 1] - times[i])
    return None


def scroll_at(curve, t):
    times, px = curve["times"], curve["px"]
    if t <= times[0]:
        return px[0]
    if t >= times[-1]:
        return px[-1]
    u = (t - times[0]) / (times[1] - times[0])
    i = int(u)
    return px[i] + (px[i + 1] - px[i]) * (u - i)


def opacity_at(t):
    o = OPACITY
    if t < o["fade_in_start"]:
        return 0.0
    fade_in = smooth((t - o["fade_in_start"]) / o["fade_in_length"])
    dim = 1 - (1 - o["dim_opacity"]) * smooth((t - o["dim_start"]) / o["dim_ramp"]) * (1 - smooth((t - o["dim_end"]) / o["dim_ramp"]))
    fade_out = 1 - smooth((t - (o["duration"] - o["fade_out_length"])) / o["fade_out_length"])
    return fade_in * dim * fade_out


# ---------------------------------------------------------------- logo (5x7 pixel font with metal LUT)
GLYPHS5 = {
    "A": "01110/10001/10001/11111/10001/10001/10001", "C": "01111/10000/10000/10000/10000/10000/01111",
    "D": "11110/10001/10001/10001/10001/10001/11110", "E": "11111/10000/10000/11110/10000/10000/11111",
    "F": "11111/10000/10000/11110/10000/10000/10000", "G": "01111/10000/10000/10111/10001/10001/01110",
    "H": "10001/10001/10001/11111/10001/10001/10001", "I": "11111/00100/00100/00100/00100/00100/11111",
    "M": "10001/11011/10101/10101/10001/10001/10001", "N": "10001/11001/10101/10011/10001/10001/10001",
    "O": "01110/10001/10001/10001/10001/10001/01110", "R": "11110/10001/10001/11110/10100/10010/10001",
    "T": "11111/00100/00100/00100/00100/00100/00100", "1": "00100/01100/00100/00100/00100/00100/01110",
    "4": "00010/00110/01010/10010/11111/00010/00010", "+": "00000/00100/00100/11111/00100/00100/00000",
    "·": "00000/00000/00000/00100/00000/00000/00000", " ": "00000/00000/00000/00000/00000/00000/00000",
}
LUT = [(0.0, (225, 247, 255)), (0.22, (112, 190, 250)), (0.48, (28, 100, 220)), (0.6, (8, 52, 140)),
       (0.74, (60, 140, 240)), (1.0, (190, 232, 255))]


def lut(v):
    v = min(1.0, max(0.0, v))
    for (a, ca), (b, cb) in zip(LUT, LUT[1:]):
        if v <= b:
            f = (v - a) / (b - a)
            return tuple(ca[i] + (cb[i] - ca[i]) * f for i in range(3))
    return LUT[-1][1]


def text_cells(text):
    cols = len(text) * 6 - 1
    fill = np.zeros((cols, 7), bool)
    for i, ch in enumerate(text):
        rows = GLYPHS5[ch].split("/")
        for y in range(7):
            for x in range(5):
                fill[i * 6 + x, y] = rows[y][x] == "1"
    return fill


def paint_cells(canvas, fill, cell, ox, oy, bevel=True, shadow=0):
    h, w = canvas.shape[:2]
    cols, rows = fill.shape
    total = rows * cell
    if shadow:
        for x in range(cols):
            for y in range(rows):
                if fill[x, y]:
                    x0, y0 = ox + x * cell + shadow, oy + y * cell + shadow
                    canvas[y0:y0 + cell, x0:x0 + cell] = (4, 10, 40, 215)
    for x in range(cols):
        for y in range(rows):
            if not fill[x, y]:
                continue
            for py in range(cell):
                v = (y * cell + py + 0.5) / total
                base = np.array(lut(v), np.float32)
                for px in range(cell):
                    col = base.copy()
                    if bevel and cell >= 8:
                        if py == 0 or px == 0:
                            col = col * 0.55 + 255 * 0.45
                        elif py >= cell - 2 or px >= cell - 2:
                            col = col * 0.55
                    canvas[oy + y * cell + py, ox + x * cell + px] = (*np.clip(col, 0, 255).astype(np.uint8), 255)
    # single-pixel hard edge on cell groups is left to the point filter


def build_logo():
    w, h = LOGO["width"], LOGO["height"]
    canvas = np.zeros((h, w, 4), np.uint8)
    edge = text_cells("EDGE")
    cell = 12
    paint_cells(canvas, edge, cell, (w - edge.shape[0] * cell) // 2 - 4, 6, bevel=True, shadow=9)
    sub = text_cells("+ FC14 · RHINO OF TORMENT")
    paint_cells(canvas, sub, 3, (w - sub.shape[0] * 3) // 2, 112, bevel=False, shadow=1)
    return canvas


# ---------------------------------------------------------------- preview helpers (numpy mirror of the shaders)
SKY_KEYS = [(0, (.012, .018, .024), (.040, .070, .078)), (30.72, (.035, .030, .055), (.240, .150, .105)),
            (51.20, (.050, .090, .200), (.200, .360, .520)), (61.44, (.035, .025, .090), (.280, .110, .290)),
            (71.68, (.025, .030, .080), (.120, .150, .300)), (81.92, (.020, .028, .070), (.210, .140, .160)),
            (102.40, (.055, .062, .070), (.150, .165, .180)), (122.88, (.016, .019, .022), (.060, .068, .075))]


def sky_at(t):
    cur = SKY_KEYS[0]
    prev = SKY_KEYS[0]
    for k in SKY_KEYS:
        if k[0] <= t:
            prev, cur = cur, k
    f = smooth((t - cur[0]) / 2.56) if cur is not SKY_KEYS[0] else 1.0
    top = np.array(prev[1]) * (1 - f) + np.array(cur[1]) * f
    hor = np.array(prev[2]) * (1 - f) + np.array(cur[2]) * f
    return top, hor


def hsv(h, s, v):
    h = np.mod(h, 1.0)
    r = np.clip(np.abs(h * 6 - 3) - 1, 0, 1)
    g = np.clip(2 - np.abs(h * 6 - 2), 0, 1)
    b = np.clip(2 - np.abs(h * 6 - 4), 0, 1)
    rgb = np.stack([r, g, b], -1)
    return ((rgb - 1) * s[..., None] + 1) * v[..., None]


def bilinear(atlas, x, y):
    h, w = atlas.shape[:2]
    x = np.clip(x - 0.5, 0, w - 1.001)
    y = np.clip(y - 0.5, 0, h - 1.001)
    x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
    fx, fy = (x - x0)[..., None], (y - y0)[..., None]
    a = atlas[y0, x0] * (1 - fx) + atlas[y0, x0 + 1] * fx
    b = atlas[y0 + 1, x0] * (1 - fx) + atlas[y0 + 1, x0 + 1] * fx
    return a * (1 - fy) + b * fy


class RingPreview:
    def __init__(self, atlas, info, curve):
        self.atlas = atlas.astype(np.float32) / 255
        self.info = info
        self.curve = curve
        self.base_y = RING["baseline_y"] - (BAND - BASE) / RING["px_per_metre"]
        self.band_m = BAND / RING["px_per_metre"]

    def sample(self, t, a, yw):
        """a: arc metres from front, yw: world y. Returns rgb, alpha arrays (premixed with opacity)."""
        r = RING
        T = t
        u = np.clip(a / (math.radians(r["arc_degrees"]) * r["radius"]) + 0.5, 0, 1)
        f0, f1 = np.clip(u / r["end_fade"], 0, 1), np.clip((1 - u) / r["end_fade"], 0, 1)
        fade = (f0 * f0 * (3 - 2 * f0)) * (f1 * f1 * (3 - 2 * f1))
        s = scroll_at(self.curve, t) + a * r["px_per_metre"]
        total = self.info["text_length_px"]
        valid = (s >= 0) & (s < total)
        row = np.clip(np.floor(s / STRIDE), 0, self.info["rows"] - 1)
        x = s - row * STRIDE + PAD
        # style at mid band decides wobble amplitude
        mid = row * BAND + BAND * 0.5
        style = bilinear(self.atlas, x, mid)[..., 3]
        wob = r["amplitude"] * np.sin(a * 2 * math.pi / r["wavelength"] + r["omega"] * T) * (1 - style)
        y_local = (self.base_y + self.band_m - (yw - wob)) * r["px_per_metre"]
        inside = (y_local >= 0) & (y_local < BAND)
        yy = row * BAND + np.clip(y_local, 0, BAND - 0.01)
        tex = bilinear(self.atlas, x, yy)
        sh = bilinear(self.atlas, x - 3, yy - 3)  # shadow offset (right, down) samples up/left
        cov, out, idc, sty = tex[..., 0], tex[..., 1], tex[..., 2], tex[..., 3]
        hue = np.mod(idc * 255 * (18 / 256) + T * r["hue_speed"], 1.0)
        top = hsv(hue, np.full_like(hue, 0.30), np.ones_like(hue))
        bot = hsv(hue + 0.10, np.full_like(hue, 0.88), np.full_like(hue, 0.92))
        g = np.clip((y_local - GRAD_TOP) / (GRAD_BOT - GRAD_TOP), 0, 1)
        g = (np.clip((g - 0.30) / 0.40, 0, 1))
        g = g * g * (3 - 2 * g)
        rainbow = top * (1 - g[..., None]) + bot * g[..., None]
        grey = np.array([0.74, 0.77, 0.80]) * (1 - g[..., None]) + np.array([0.46, 0.49, 0.53]) * g[..., None]
        ist = np.clip((sty - 0.4) * 5, 0, 1)[..., None]
        col = rainbow * (1 - ist) + grey * ist
        outline = np.array([0.015, 0.02, 0.05])
        halo = np.maximum(out, sh[..., 1] * 0.55)
        alpha = np.clip(cov + (1 - cov) * halo, 0, 1)
        rgb = outline * (1 - cov[..., None]) + col * cov[..., None]
        alpha = alpha * (valid & inside) * fade
        return rgb, alpha


def sky_image(width, height, t, elev_top, elev_bottom):
    top, hor = sky_at(t)
    e = np.linspace(elev_top, elev_bottom, height)[:, None]
    f = np.clip(e / 60.0, 0, 1)
    col = hor[None, None, :] * (1 - f[..., None]) + top[None, None, :] * f[..., None]
    return np.repeat(col, width, 1)


def preview_ring(rp, t, out_dir):
    r = RING
    op = opacity_at(t)
    arc_m = math.radians(r["arc_degrees"]) * r["radius"]
    # flattened arc, 1:1 texel scale then downsample
    scale = r["px_per_metre"]
    W = int(arc_m * scale)
    y_lo, y_hi = rp.base_y - 0.08, rp.base_y + rp.band_m + 0.20
    H = int((y_hi - y_lo) * scale)
    aa = (np.arange(W) + 0.5) / scale - arc_m / 2
    yy = y_hi - (np.arange(H) + 0.5) / scale
    A, Y = np.meshgrid(aa, yy)
    rgb, alpha = rp.sample(t, A, Y)
    alpha = alpha * op
    elev_hi = math.degrees(math.atan2(y_hi - 1.35, r["radius"]))
    elev_lo = math.degrees(math.atan2(y_lo - 1.35, r["radius"]))
    bg = sky_image(W, H, t, elev_hi, elev_lo)
    img = bg * (1 - alpha[..., None]) + rgb * alpha[..., None]
    im = Image.fromarray((np.clip(img, 0, 1) ** (1 / 2.2) * 255).astype(np.uint8)).resize((W // 2, H // 2), Image.LANCZOS)
    im.save(out_dir / ("ring-flat-t%03d.png" % round(t)))
    # review camera 1600x900 FOV 52 vertical pitch 0, and a wide 70 degree view pitched up 15 degrees
    for name, fov, pitch in (("review", 52.0, 0.0), ("up15", 70.0, 15.0)):
        w, h = 1600, 900
        th = math.tan(math.radians(fov / 2))
        xs = (np.arange(w) + 0.5) / w * 2 - 1
        ys = 1 - (np.arange(h) + 0.5) / h * 2
        X, Yn = np.meshgrid(xs * th * w / h, ys * th)
        d = np.stack([X, Yn, np.ones_like(X)], -1)
        cp, sp = math.cos(math.radians(pitch)), math.sin(math.radians(pitch))
        dy = d[..., 1] * cp + d[..., 2] * sp
        dz = -d[..., 1] * sp + d[..., 2] * cp
        dx = d[..., 0]
        horiz = np.hypot(dx, dz)
        theta = np.arctan2(dx, dz)
        a = theta * r["radius"]
        yw = 1.35 + r["radius"] * dy / horiz
        rgb2, alpha2 = rp.sample(t, a, yw)
        alpha2 = alpha2 * op
        elev = np.degrees(np.arctan2(dy, horiz))
        top, hor = sky_at(t)
        f = np.clip(elev / 60.0, 0, 1)[..., None]
        bg2 = hor * (1 - f) + top * f
        img2 = bg2 * (1 - alpha2[..., None]) + rgb2 * alpha2[..., None]
        Image.fromarray((np.clip(img2, 0, 1) ** (1 / 2.2) * 255).astype(np.uint8)).save(out_dir / ("ring-%s-t%03d.png" % (name, round(t))))


def window_mock(win_atlas, logo, t, out_path):
    """Layout mock of the cracktro window (not shader exact): 500 px per metre, viewer side."""
    S = 500
    W, H = int(2.9 * S), int(2.3 * S)
    img = np.zeros((H, W, 3), np.float32)
    cx, cy = W / 2, H / 2 + 0.10 * S          # canvas centre (origin) is 0.10 m below frame centre -> y down
    cy = H / 2 + 0.10 * S
    # frame bevel
    bev = int(0.10 * S)
    yy, xx = np.mgrid[0:H, 0:W]
    d = np.minimum(np.minimum(xx, W - 1 - xx), np.minimum(yy, H - 1 - yy))
    shade = np.where(d < bev * 0.25, 0.30, np.where(d < bev * 0.5, 0.92, 0.55 + 0.25 * (xx / W - yy / H)))
    metal = np.stack([0.10 + 0.8 * shade ** 1.6, 0.30 + 0.65 * shade ** 1.3, 0.62 + 0.38 * shade], -1)
    img[:] = metal
    # field
    fx0, fy0, fx1, fy1 = bev, bev, W - bev, H - bev
    fw, fh = fx1 - fx0, fy1 - fy0
    field = np.zeros((fh, fw, 3), np.float32)
    field[:] = np.array([0.008, 0.015, 0.04])
    rng = np.random.default_rng(7)
    for layer, (n, v) in enumerate(((220, .35), (120, .65), (60, 1.0))):
        xs = (rng.random(n) * fw).astype(int)
        ys = (rng.random(n) * fh).astype(int)
        for x, y in zip(xs, ys):
            field[y:y + 1 + layer, x:x + 1 + layer] = np.array([0.6, 0.8, 1.0]) * v
    uvy = np.linspace(1, 0, fh)[:, None]
    for centre, amp, hw, cols in ((0.88, 0.05, 0.05, [(.15, .35, 1.0), (.1, .8, .95), (.9, .3, .8), (1.0, .65, .2)]),
                                  (0.075, 0.03, 0.035, [(.15, .35, 1.0), (.1, .8, .95), (.9, .3, .8)])):
        for i, col in enumerate(cols):
            c = centre + amp * math.sin(t * (1.1 + i * .4) + i * 1.7)
            p = np.clip(1 - np.abs(uvy - c) / hw, 0, 1) ** 1.6
            field += p[..., None] * np.array(col) * 0.55
    field *= (0.88 + 0.12 * np.sin(np.arange(fh) * 0.19))[:, None, None]
    img[fy0:fy1, fx0:fx1] = np.clip(field, 0, 1)
    # canvas rect (1.98 x 1.19 m) with panel (0,0,0,.55)
    cw, ch = int(1.98 * S), int(1.19 * S)
    x0, y0 = int(cx - cw / 2), int(cy - ch / 2)
    img[y0:y0 + ch, x0:x0 + cw] *= 0.45
    base = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))
    d = ImageDraw.Draw(base)
    f_big, f_mid = ImageFont.truetype(FONT_PATH, 32, index=sc_face_index()), ImageFont.truetype(FONT_PATH, 24, index=sc_face_index())
    pxc = S * 0.0036
    d.text((cx, cy - 110 * pxc), "连接尚未开始", font=f_big, fill=(255, 255, 255), anchor="mm")
    d.text((cx, cy - 36 * pxc), "按下开始，让第一扇窗口打开。", font=f_mid, fill=(176, 242, 255), anchor="mm")
    bw, bh = int(460 * pxc), int(88 * pxc)
    d.rectangle([cx - bw / 2, cy + 70 * pxc - bh / 2, cx + bw / 2, cy + 70 * pxc + bh / 2], fill=(77, 140, 217))
    d.text((cx, cy + 70 * pxc), "开始", font=f_big, fill=(245, 252, 255), anchor="mm")
    # logo
    lw, lh = int(logo.shape[1] * LOGO["metres_per_texel"] * S), int(logo.shape[0] * LOGO["metres_per_texel"] * S)
    lg = Image.fromarray(logo).resize((lw, lh), Image.NEAREST)
    base.paste(lg, (int(cx - lw / 2), int(cy - 0.895 * S - lh / 2)), lg)
    # scroller strip (rainbow tint)
    sc = win_atlas.astype(np.float32) / 255
    sw, sh_ = int(2.5 * S), int(0.30 * S)
    scroll = t * WIN["scroll_px_per_s"]
    ppm = WIN["em_px"] / WIN["em_m"]
    aa = (np.arange(sw) + 0.5) / S - 1.25
    yy_ = (np.arange(sh_) + 0.5) / S
    A, Yv = np.meshgrid(aa, yy_)
    s = scroll + A * ppm
    L = WIN["text_length_px"]
    sw_ = np.mod(s, L)
    wob = WIN["amplitude"] * np.sin(A * 2 * math.pi / WIN["wavelength"] + WIN["omega"] * t)
    ylocal = (Yv + wob) * ppm
    tex = bilinear(sc, sw_ + PAD, np.clip(ylocal, 0, WIN["band"] - 0.01))
    hue = np.mod(tex[..., 2] * 255 * (18 / 256) + t * 0.125, 1.0)
    rgb = hsv(hue, np.full_like(hue, 0.55), np.full_like(hue, 1.0))
    al = np.clip(tex[..., 0] + (1 - tex[..., 0]) * tex[..., 1], 0, 1)
    rgb = rgb * tex[..., 0:1] + np.array([0.015, 0.02, 0.05]) * (1 - tex[..., 0:1])
    arr = np.asarray(base).astype(np.float32) / 255
    sx0, sy0 = int(cx - sw / 2), int(cy + 0.80 * S - sh_ / 2)
    reg = arr[sy0:sy0 + sh_, sx0:sx0 + sw]
    arr[sy0:sy0 + sh_, sx0:sx0 + sw] = reg * (1 - al[..., None]) + rgb * al[..., None]
    Image.fromarray((np.clip(arr, 0, 1) * 255).astype(np.uint8)).save(out_path)


# ---------------------------------------------------------------- main
def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--b-ratio", type=float, default=CURVE["b_ratio"], help="speed ratio of the B segment hold (0.55)")
    ap.add_argument("--b-target-degrees", type=float, default=CURVE["b_target_degrees"],
                    help="where the last B token centre should sit (degrees right of front) at fade-out start")
    ap.add_argument("--no-preview", action="store_true")
    args = ap.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    PREVIEW.mkdir(parents=True, exist_ok=True)
    big = Face(EM)

    tokens = [dict(text=GREETING, group="greeting", segment="A", style=0)]
    tokens += [dict(text=t, group=g, segment="A", style=0) for g, t in A_PHRASES]
    tokens += [dict(text=t, group=g, segment="B", style=s) for g, t, s in B_PHRASES]
    for tok in tokens:
        low = tok["text"].lower()
        assert not any(w.lower() in low for w in FORBIDDEN), tok["text"]
    glyphs, length = layout_tokens(big, tokens, START_MARGIN)
    total = int(math.ceil(length + START_MARGIN))
    rows = int(math.ceil(total / STRIDE))
    assert rows * BAND <= AH, rows
    interrupt = [(t["start"] - 6, t["end"] + 6) for t in tokens if t["style"] == 1]
    cover, ids, style, glyph_count = render_strip(big, glyphs, total, BAND, BASE, interrupt_spans=interrupt)
    outline = outline_plane(cover)
    atlas = cut_rows((cover, outline, ids, style), total, rows, BAND, STRIDE, PAD, AW, AH)
    for r in range(1, rows):  # seam check: overlapping columns of neighbouring rows are identical
        for k in range(-PAD, PAD):
            a = atlas[(r - 1) * BAND:r * BAND, STRIDE + PAD + k]
            b = atlas[r * BAND:(r + 1) * BAND, PAD + k]
            assert np.array_equal(a, b), ("seam mismatch", r, k)
    Image.fromarray(atlas, "RGBA").save(OUT / "greetz-atlas.png")

    # window scroller strip (cyclic, one row)
    small = Face(WIN["em_px"])
    win_tokens = [dict(text=t, group="window", segment="W", style=0) for t in WINDOW_TEXT]
    wglyphs, wlen = layout_tokens(small, win_tokens, 0.0)
    L = int(round(wlen))
    assert L <= AW - 2 * PAD, L
    wc, wi, ws, _ = render_strip(small, wglyphs, AW, WIN["band"], WIN["base"], offsets=(PAD - L, PAD, PAD + L))
    wo = outline_plane(wc)
    win_atlas = np.stack([wc, wo, wi, ws], -1)
    Image.fromarray(win_atlas, "RGBA").save(OUT / "window-scroller.png")
    WIN["text_length_px"] = L

    logo = build_logo()
    Image.fromarray(logo, "RGBA").save(OUT / "cracktro-logo.png")

    # curve + token timing
    curve = build_curve(tokens, EM, args.b_ratio, args.b_target_degrees)
    speed = curve["speed"]
    for tok in tokens:
        centre = (tok["start"] + tok["end"]) / 2
        tok["centre"] = round(centre, 2)
        ft = invert_time(curve, centre)
        tok["front_time"] = None if ft is None else round(ft, 2)
        tok["theta_at_fade_out"] = round(math.degrees((centre - scroll_at(curve, CURVE["fade_out_begin"])) / RING["px_per_metre"] / RING["radius"]), 1)
    em_s = lambda v: v / EM
    a_speed = curve["va"] / EM
    chars_a = sum(1 for g in glyphs if g["kind"] != "space" and g["pen"] > tokens[0]["centre"] and g["pen"] < tokens[len(A_PHRASES)]["centre"])
    stats = dict(
        a_speed_em_per_s=round(a_speed, 3), a_speed_glyphs_per_s=round(chars_a / (CURVE["a_front_last"] - CURVE["a_front_greeting"]), 3),
        b_hold_ratio=args.b_ratio, b_end_ratio=round(curve["r_end"], 3),
        b_mean_ratio=round((curve["px"][-1] - scroll_at(curve, CURVE["b_start"])) / (OPACITY["duration"] - CURVE["b_start"]) / curve["va"], 3),
        max_speed_em_per_s=round(max(speed(t) for t in np.arange(30.72, 133.12, 0.1)) / EM, 3),
        glyph_count=glyph_count, text_length_px=total, rows=rows,
    )
    a_groups = {}
    for tok in tokens[:1 + len(A_PHRASES)]:
        a_groups.setdefault(tok["group"], []).append(tok["front_time"])
    stats["a_group_front_times"] = {k: [v[0], v[-1]] for k, v in a_groups.items()}

    ring = dict(RING)
    ring.update(arc_length_m=math.radians(RING["arc_degrees"]) * RING["radius"], band_px=BAND,
                band_height_m=BAND / RING["px_per_metre"], baseline_from_bottom_m=(BAND - BASE) / RING["px_per_metre"],
                root_y=RING["baseline_y"] - (BAND - BASE) / RING["px_per_metre"],
                ink_bottom_from_baseline_m=8 / RING["px_per_metre"], ink_top_from_baseline_m=70 / RING["px_per_metre"],
                shadow_px=[3, 3], outline_px=OUTLINE_R)
    data = dict(
        version=1, description="V5 identity: greetz ring + cracktro window data (anonymous labour roles only)",
        atlas=dict(file="greetz-atlas.png", width=AW, height=AH, rows=rows, row_px=BAND, row_stride=STRIDE, row_pad=PAD,
                   em_px=EM, baseline_px=BASE, grad_top_px=GRAD_TOP, grad_bottom_px=GRAD_BOT, text_length_px=total,
                   channels="R cover, G outline, B glyph id (hue step 18/256), A style (255 interrupt)"),
        ring=ring, opacity=OPACITY,
        curve=dict(start=CURVE["start"], step=CURVE["step"], px=[round(v, 2) for v in curve["px"]],
                   a_front_greeting=CURVE["a_front_greeting"], a_front_last=CURVE["a_front_last"], b_start=CURVE["b_start"],
                   b_ratio=args.b_ratio, b_hold_until=CURVE["b_hold_until"], b_end_ratio=round(curve["r_end"], 4),
                   fade_out_begin=CURVE["fade_out_begin"]),
        tokens=[dict(index=i, text=t["text"], segment=t["segment"], group=t["group"], style=t["style"],
                     start=round(t["start"], 2), end=round(t["end"], 2), width=round(t["end"] - t["start"], 2),
                     centre=t["centre"], front_time=t["front_time"], theta_at_fade_out=t["theta_at_fade_out"]) for i, t in enumerate(tokens)],
        window=dict(file="window-scroller.png", width=AW, height=WIN["band"], band_px=WIN["band"], baseline_px=WIN["base"],
                    em_px=WIN["em_px"], em_m=WIN["em_m"], px_per_metre=WIN["em_px"] / WIN["em_m"], row_pad=PAD,
                    text_length_px=L, strip_width_m=WIN["strip_width_m"], segments=WIN["segments"],
                    ribbon_height_m=WIN["band"] / (WIN["em_px"] / WIN["em_m"]), baseline_from_bottom_m=(WIN["band"] - WIN["base"]) / (WIN["em_px"] / WIN["em_m"]),
                    amplitude=WIN["amplitude"], wavelength=WIN["wavelength"], omega=WIN["omega"],
                    scroll_px_per_s=WIN["scroll_px_per_s"], end_fade=WIN["end_fade"], text=" ✦ ".join(WINDOW_TEXT) + " ✦"),
        logo=dict(file="cracktro-logo.png", width=LOGO["width"], height=LOGO["height"], metres_per_texel=LOGO["metres_per_texel"],
                  text="EDGE / + FC14 · RHINO OF TORMENT"),
        stats=stats,
    )
    DATA.write_text(json.dumps(data, ensure_ascii=False, indent=1), encoding="utf-8")
    print(json.dumps(stats, ensure_ascii=False, indent=1))
    print("tokens:", [(t["text"], t["front_time"]) for t in tokens])

    if not args.no_preview:
        info = dict(text_length_px=total, rows=rows)
        rp = RingPreview(atlas, info, curve)
        for t in (35, 60, 90, 110, 130):
            preview_ring(rp, float(t), PREVIEW)
        window_mock(win_atlas, logo, 2.0, PREVIEW / "window-mock-t2.png")
        window_mock(win_atlas, logo, 7.3, PREVIEW / "window-mock-t7.png")
        vis = np.stack([atlas[..., 0], atlas[..., 1], atlas[..., 3]], -1)
        Image.fromarray(vis, "RGB").save(PREVIEW / "atlas-cover-outline-style.png")
        Image.fromarray(np.stack([win_atlas[..., 0], win_atlas[..., 1], win_atlas[..., 3]], -1), "RGB").save(PREVIEW / "window-strip-cover-outline.png")
        print("previews:", PREVIEW)


if __name__ == "__main__":
    sys.exit(main())
