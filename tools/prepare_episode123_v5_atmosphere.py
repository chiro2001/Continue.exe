#!/usr/bin/env python3
"""V5 atmosphere data: colour script, light script, star/grid parameters and per-focus halo colours.

Reads the V4 score (images + the 15 role==focus events), averages each focus image
into a halo / screen-light colour, and writes data/episode123-v5-atmosphere.json for
AgentScripts/OWEpisodeV5Atmosphere.cs. Deterministic; run with ~/miniforge3/bin/python.
"""
import argparse
import colorsys
import json
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
DURATION = 133.12
TRANSITION = 2.56
REGION_OFFSETS = [0.0, 24.0, 52.0, 80.0]

# Shared brief colour script: key time, sky top, horizon, mood. Transition = 2.56 s smoothstep starting at the key.
SKY_KEYS = [
    (0.00, (0.012, 0.018, 0.024), (0.040, 0.070, 0.078), 'CRT night'),
    (30.72, (0.035, 0.030, 0.055), (0.240, 0.150, 0.105), 'warm early-community dusk'),
    (51.20, (0.050, 0.090, 0.200), (0.200, 0.360, 0.520), 'glossy Web 2.0 blue'),
    (61.44, (0.035, 0.025, 0.090), (0.280, 0.110, 0.290), 'danmaku-era magenta/cyan night'),
    (71.68, (0.025, 0.030, 0.080), (0.120, 0.150, 0.300), 'making/tutorials violet-blue'),
    (81.92, (0.020, 0.028, 0.070), (0.210, 0.140, 0.160), 'VRC campfire night'),
    (102.40, (0.055, 0.062, 0.070), (0.150, 0.165, 0.180), 'conditions: cold desaturated'),
    (122.88, (0.016, 0.019, 0.022), (0.060, 0.068, 0.075), 'maintenance: dark, one lamp'),
]
WAITING_SKY = ((0.008, 0.010, 0.014), (0.020, 0.030, 0.036))

# Directional "Stable actor key/fill" script at the same key times. Waiting/finished = exact V4 values
# (key #fff2d9 0.85, fill #c5e0e8 0.28 from OWEpisodeBuild.BuildCameraAndLighting).
V4_KEY = ((255 / 255, 242 / 255, 217 / 255), 0.85)
V4_FILL = ((197 / 255, 224 / 255, 232 / 255), 0.28)
LIGHT_KEYS = [
    # t, key colour, key intensity, fill colour, fill intensity
    (0.00, (0.78, 0.93, 0.90), 0.62, (0.45, 0.72, 0.80), 0.30),
    (30.72, (1.00, 0.80, 0.58), 0.92, (0.62, 0.60, 0.82), 0.26),
    (51.20, (1.00, 0.93, 0.82), 0.90, (0.50, 0.72, 1.00), 0.34),
    (61.44, (1.00, 0.78, 0.92), 0.84, (0.42, 0.88, 1.00), 0.34),
    (71.68, (0.90, 0.86, 1.00), 0.80, (0.55, 0.62, 1.00), 0.30),
    (81.92, (1.00, 0.76, 0.50), 0.88, (0.42, 0.52, 0.86), 0.24),
    (102.40, (0.80, 0.87, 0.95), 0.70, (0.70, 0.80, 0.90), 0.30),
    (122.88, (0.72, 0.78, 0.88), 0.36, (0.55, 0.62, 0.72), 0.16),
]

# Grid floor opacity keys; smoothstep between consecutive keys.
GRID_KEYS = [
    (0.00, 0.00), (30.72, 0.00), (46.08, 1.00), (76.80, 1.00),
    (79.36, 0.35), (102.40, 0.35), (104.96, 1.00), (DURATION, 1.00),
]

# OWV4Journey.VirtualPosition: z = sum distance * smoothstep((t - start) / duration).
JOURNEYS = [(79.36, 2.56, 24.0), (86.40, 1.92, 28.0), (102.40, 2.56, 28.0)]

STAR_LAYERS = [
    dict(name='far', parent='V5 Atmosphere', count=2800, seed=70101, shape='shell', r_min=100.0, r_max=140.0,
         wrap=0.0, fade=[0.0, 1.0, 1.0e5, 2.0e5], ang_size=0.0023, trail=0.0, streak_max=0.0, boost=0.0,
         twinkle=1.0, below_floor=0.40, intensity_showing=1.0, intensity_waiting=0.35, tint_mix=0.30),
    dict(name='mid', parent='Revision5 Starfield', count=2800, seed=70102, shape='cube', r_min=30.0, r_max=70.0,
         wrap=140.0, fade=[24.0, 32.0, 56.0, 70.0], ang_size=0.0022, trail=0.15, streak_max=0.42, boost=1.0,
         twinkle=0.8, below_floor=0.45, intensity_showing=0.95, intensity_waiting=0.0, tint_mix=0.30),
    dict(name='near', parent='Revision5 Starfield', count=2800, seed=70103, shape='cube', r_min=10.0, r_max=30.0,
         wrap=60.0, fade=[6.0, 10.0, 22.0, 30.0], ang_size=0.0019, trail=0.09, streak_max=0.36, boost=1.4,
         twinkle=0.6, below_floor=0.50, intensity_showing=1.0, intensity_waiting=0.0, tint_mix=0.25),
]

HALO = dict(scale=1.35, back_offset=0.05, peak=0.85, min_bottom_y=0.75, band_y=0.6, power=1.8)
SCREEN = dict(peak=1.6, range=7.0, toward_eye=1.2, pool_radius=3.4, pool_gain=0.30)
GRID = dict(y=-0.02, radius=120.0, segments=128, cell=1.0, major=8.0, fade_start=8.0, fade_end=40.0,
            line_alpha=0.32, major_alpha=0.62, fill_alpha=0.10, shimmer=0.12, waiting=0.25, finished=0.25)
SKY = dict(radius=150.0, far_clip=180.0, lon_segments=96, lat_segments=48, ground_darken=0.55, curve=0.5,
           haze_gain=0.30, haze_falloff=11.0, dither=1.0)


def srgb_to_linear(c):
    c = np.asarray(c, dtype=np.float64)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def linear_to_srgb(c):
    c = np.clip(np.asarray(c, dtype=np.float64), 0.0, 1.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def focus_colour(path):
    """Average + saturation-weighted accent -> boosted glow colour (sRGB 0..1)."""
    image = Image.open(path).convert('RGBA')
    image.thumbnail((256, 256))
    px = np.asarray(image, dtype=np.float64) / 255.0
    alpha = px[..., 3]
    rgb = px[..., :3]
    weight = alpha.sum()
    if weight < 1e-6:
        raise ValueError('fully transparent image ' + str(path))
    lin = srgb_to_linear(rgb)
    mean = linear_to_srgb((lin * alpha[..., None]).sum((0, 1)) / weight)
    top = rgb.max(-1)
    low = rgb.min(-1)
    sat = (top - low) / np.maximum(top, 1e-6)
    w_acc = alpha * sat ** 1.5 * (0.2 + top)
    if w_acc.sum() > 1e-3 * weight:
        accent = linear_to_srgb((lin * w_acc[..., None]).sum((0, 1)) / w_acc.sum())
    else:
        accent = mean
    base = 0.45 * mean + 0.55 * accent
    h, s, v = colorsys.rgb_to_hsv(*[float(x) for x in base])
    s = min(1.0, s * 1.5 + 0.10)
    v = min(1.0, max(v * 1.1, 0.62))
    glow = colorsys.hsv_to_rgb(h, s, v)
    return [float(x) for x in mean], [float(x) for x in accent], [round(float(x), 4) for x in glow]


def r3(v):
    return [round(float(x), 4) for x in v]


def smoothstep(x):
    x = min(max(x, 0.0), 1.0)
    return x * x * (3 - 2 * x)


def virtual_z(t):
    return sum(d * smoothstep((t - s) / dur) for s, dur, d in JOURNEYS)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--score', default='data/episode123-v4-score.json')
    parser.add_argument('--out', default='data/episode123-v5-atmosphere.json')
    args = parser.parse_args()
    score = json.loads((ROOT / args.score).read_text())
    images = {i['id']: i for i in score['images']}
    focus = []
    for index, event in enumerate(score['events']):
        if event.get('role') != 'focus':
            continue
        image = images[event['image']]
        src = image.get('display_source_path') or image.get('source_path')
        path = Path(src)
        if not path.is_file():
            raise FileNotFoundError(path)
        mean, accent, glow = focus_colour(path)
        to = event['to']
        bottom = to[1] - 0.5 * HALO['scale'] * event['height'] - 0.03
        assert bottom >= HALO['min_bottom_y'] - 1e-6, ('halo reaches keyline band', index, bottom)
        focus.append(dict(event=index, image=event['image'], source=str(path), start=event['start'], end=event['end'],
                          enter=event.get('enter', 0.45), exit=event.get('exit', 0.55), region=event.get('region', 0),
                          width=event['width'], height=event['height'], to=to, halo_bottom_y=round(bottom, 4),
                          mean=r3(mean), accent=r3(accent), color=glow))
    assert len(focus) == 15, len(focus)
    keys = [dict(t=t, top=list(top), horizon=list(hz), mood=mood) for t, top, hz, mood in SKY_KEYS]
    assert [k['t'] for k in keys] == sorted(k['t'] for k in keys)
    assert [k[0] for k in LIGHT_KEYS] == [k[0] for k in SKY_KEYS]
    for gk in zip(GRID_KEYS, GRID_KEYS[1:]):
        assert gk[0][0] < gk[1][0]
    data = dict(
        schema='oldwebmap.v5.atmosphere.1', duration=DURATION, transition=TRANSITION, intro=1.28, outro=1.28,
        reference_eye=[0.0, 1.35, -2.8], region_offsets=REGION_OFFSETS,
        sky=dict(SKY, waiting=dict(top=list(WAITING_SKY[0]), horizon=list(WAITING_SKY[1])),
                 finished=dict(top=list(WAITING_SKY[0]), horizon=list(WAITING_SKY[1])), keys=keys),
        lights=dict(
            key=dict(waiting=dict(color=r3(V4_KEY[0]), intensity=V4_KEY[1]),
                     keys=[dict(t=k[0], color=list(k[1]), intensity=k[2]) for k in LIGHT_KEYS]),
            fill=dict(waiting=dict(color=r3(V4_FILL[0]), intensity=V4_FILL[1]),
                      keys=[dict(t=k[0], color=list(k[3]), intensity=k[4]) for k in LIGHT_KEYS]),
            screen=SCREEN),
        stars=STAR_LAYERS,
        grid=dict(GRID, keys=[list(k) for k in GRID_KEYS]),
        journey=[dict(start=s, duration=d, distance=z) for s, d, z in JOURNEYS],
        halo=HALO,
        focus=focus,
    )
    out = ROOT / args.out
    out.write_text(json.dumps(data, ensure_ascii=False, indent=1) + '\n')
    print('wrote', out)
    print('final virtual z', virtual_z(DURATION), 'sky keys', len(keys), 'focus', len(focus))
    for f in focus:
        print('%2d %-38s %7.2f-%7.2f h=%.2f bottom=%.2f glow=%s' % (f['event'], f['image'], f['start'], f['end'], f['height'], f['halo_bottom_y'], f['color']))


if __name__ == '__main__':
    main()
