#!/usr/bin/env python3
"""V5 era Foley: original numpy synthesis (no recordings, no voices, no system sounds).

Writes 48 kHz mono PCM_16 WAVs to art/export/episode123-v5/foley/ and the event contract
data/episode123-v5-foley.json (80 ms grid). Every random draw is seeded; reruns are bit-identical.
"""
import argparse
import json
import math
from pathlib import Path

import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "art/export/episode123-v5/foley"
DATA = ROOT / "data/episode123-v5-foley.json"
RATE = 48000
GRID = 0.08
PEAK_DB = -3.0  # every clip file is peak-normalised to this level; AudioSource.volume carries the scene level
PEAK_LIN = 10 ** (PEAK_DB / 20)
SHOW = 133.12
JOURNEYS = ((79.36, 81.92), (86.40, 88.32), (102.40, 104.96))
CAMPFIRE_WINDOW = (88.80, 101.92)
MECH_UNTIL = 51.20
# F is the tonal centre of the FC14 stems (F ~46 % of pitch-class energy); chimes use the F-major triad.
F5, A5, C6, F6 = 698.456, 880.0, 1046.502, 1396.913


def n_of(seconds):
    return int(round(seconds * RATE))


def tt(n):
    return np.arange(n) / RATE


def fft_filter(x, lo=None, hi=None, order=2):
    """Zero-phase Butterworth-magnitude band filter (no scipy)."""
    n = len(x)
    size = 1 << int(math.ceil(math.log2(max(2, n * 2))))
    spec = np.fft.rfft(x, size)
    f = np.fft.rfftfreq(size, 1 / RATE)
    h = np.ones_like(f)
    if lo:
        h *= 1 / np.sqrt(1 + (lo / np.maximum(f, 1e-6)) ** (2 * order))
    if hi:
        h *= 1 / np.sqrt(1 + (f / hi) ** (2 * order))
    return np.fft.irfft(spec * h, size)[:n]


def unit(x):
    return x / np.sqrt(np.mean(x ** 2) + 1e-12)


def band_noise(rng, n, lo, hi, order=2):
    return unit(fft_filter(rng.standard_normal(n), lo, hi, order))


def slow_noise(rng, n, cutoff):
    return unit(fft_filter(rng.standard_normal(n), None, cutoff, 2))


def taper(x, seconds=0.008):
    """Half-cosine end taper so no primitive ends on a step."""
    m = min(len(x) // 3, n_of(seconds))
    if m > 1:
        x[len(x) - m:] *= 0.5 + 0.5 * np.cos(np.linspace(0, np.pi, m))
    return x


def burst(rng, length, lo, hi, tau, amp, order=2, attack=0.0002):
    n = n_of(length)
    t = tt(n)
    x = band_noise(rng, n, lo, hi, order) * np.exp(-t / tau) * (1 - np.exp(-t / attack))
    return taper(x * amp)


def ring(f, tau, amp, length, attack=0.0003, phase=0.0):
    n = n_of(length)
    t = tt(n)
    return taper(amp * np.sin(2 * np.pi * f * t + phase) * np.exp(-t / tau) * (1 - np.exp(-t / attack)))


def glide(f0, f1, sweep_tau, tau, amp, length, attack=0.0004):
    n = n_of(length)
    t = tt(n)
    f = f1 + (f0 - f1) * np.exp(-t / sweep_tau)
    phase = 2 * np.pi * np.cumsum(f) / RATE
    return taper(amp * np.sin(phase) * np.exp(-t / tau) * (1 - np.exp(-t / attack)))


def add(buf, seg, at):
    i = n_of(at)
    j = min(len(buf), i + len(seg))
    if j > i:
        buf[i:j] += seg[: j - i]


def tone(f, length, amp, fade=0.003, harmonics=()):
    n = n_of(length)
    t = tt(n)
    x = np.sin(2 * np.pi * f * t)
    for k, a in harmonics:
        x = x + a * np.sin(2 * np.pi * f * k * t)
    w = np.ones(n)
    m = min(n // 2, n_of(fade))
    ramp = 0.5 - 0.5 * np.cos(np.linspace(0, np.pi, m))
    w[:m] = ramp
    w[n - m:] = ramp[::-1]
    return amp * x * w


def bell(f, tau, amp, length, attack=0.004, shimmer=0.0):
    """Soft consonant chime voice: harmonic partials only (F-major friendly)."""
    n = n_of(length)
    t = tt(n)
    x = np.sin(2 * np.pi * f * t) + 0.22 * np.sin(2 * np.pi * 2 * f * (1 + shimmer) * t) + 0.06 * np.sin(2 * np.pi * 3 * f * t)
    return taper(amp * x * np.exp(-t / tau) * (1 - np.exp(-t / attack)))


def finish(x, duration, fade_in=0.0008, fade_out=0.02, hp=22.0, edge=None):
    """Fix length, remove DC/subsonic, add short fades, peak-normalise to PEAK_DB."""
    n = n_of(duration)
    x = np.asarray(x, dtype=np.float64)
    x = np.concatenate([x, np.zeros(max(0, n - len(x)))])[:n]
    x = x - np.mean(x)
    x = fft_filter(x, lo=hp, order=2)
    w = np.ones(n)
    if edge is None:
        a, b = n_of(fade_in), n_of(fade_out)
        w[:a] = 0.5 - 0.5 * np.cos(np.linspace(0, np.pi, a))
        w[n - b:] = np.minimum(w[n - b:], 0.5 + 0.5 * np.cos(np.linspace(0, np.pi, b)))
    else:
        w = edge
    x = x * w
    x = x - (np.sum(x) / np.sum(w)) * w  # exact zero mean, ends stay at zero
    peak = float(np.max(np.abs(x)))
    return (x / peak * PEAK_LIN).astype(np.float32)


# ---------------------------------------------------------------- clips

def crt_power_on(rng):
    dur = 0.82
    n = n_of(dur)
    t = tt(n)
    y = np.zeros(n)
    # degauss coil thump: low glide, mains-frequency ring and a soft LF noise puff
    add(y, glide(150, 46, 0.060, 0.115, 1.0, 0.55), 0.0)
    add(y, ring(50.0, 0.20, 0.34, 0.7), 0.004)
    add(y, ring(100.0, 0.11, 0.16, 0.5), 0.004)
    add(y, burst(rng, 0.25, 40, 380, 0.060, 0.50), 0.0)
    add(y, burst(rng, 0.02, 900, 6500, 0.0012, 0.16), 0.0)  # electrostatic pop
    # faint 15.625 kHz line whine, swells in after the thump and relaxes before the end
    env = np.clip((t - 0.07) / 0.16, 0, 1) * np.clip((dur - 0.04 - t) / 0.22, 0, 1)
    env = env * env * (3 - 2 * env)
    y += 0.050 * np.sin(2 * np.pi * 15625.0 * t + 0.4 * np.sin(2 * np.pi * 3.1 * t)) * env
    return finish(y, dur, fade_out=0.05, hp=24)


DTMF = {1: (697, 1209), 2: (697, 1336), 3: (697, 1477), 4: (770, 1209), 5: (770, 1336), 6: (770, 1477),
        7: (852, 1209), 8: (852, 1336), 9: (852, 1477), 0: (941, 1336)}


def modem_handshake(rng):
    dur = 3.2
    n = n_of(dur)
    y = np.zeros(n)
    at = 0.03
    for digit in (9, 5, 2, 7, 3, 1):  # arbitrary digits, not a real number
        lo, hi = DTMF[digit]
        add(y, tone(lo, 0.055, 0.40) + tone(hi, 0.055, 0.40), at)
        at += 0.100
    add(y, burst(rng, 0.012, 700, 4200, 0.0025, 0.30), 0.72)  # relay click
    add(y, tone(2100.0, 0.58, 0.44, fade=0.014), 0.82)  # answer tone
    add(y, burst(rng, 0.008, 1500, 5000, 0.0018, 0.22), 1.12)  # phase-reversal tick inside the tone
    pos, tones = 1.46, [980, 1180, 1650, 1850, 1200, 2400, 2225, 2025, 1500, 1750]
    while pos < 2.90:
        progress = (pos - 1.46) / 1.44
        length = max(0.028, 0.150 - 0.108 * progress + rng.uniform(-0.012, 0.012))
        kind = rng.random()
        amp = rng.uniform(0.26, 0.42)
        if kind < 0.40 - 0.10 * progress:
            seg = tone(tones[rng.integers(len(tones))], length, amp, 0.003)
        elif kind < 0.62:
            seg = tone(tones[rng.integers(len(tones))], length, amp * 0.7, 0.003) + tone(tones[rng.integers(len(tones))], length, amp * 0.6, 0.003)
        elif kind < 0.84 + 0.10 * progress:
            m = n_of(length)
            seg = band_noise(rng, m, 700, 3300) * amp * 0.9
            seg *= 0.5 - 0.5 * np.cos(np.linspace(0, 2 * np.pi, m))
        else:  # scrambled carrier: FM with a random deviation
            m = n_of(length)
            tm = tt(m)
            fc = tones[rng.integers(len(tones))]
            seg = amp * np.sin(2 * np.pi * fc * tm + 5.0 * np.sin(2 * np.pi * rng.uniform(40, 130) * tm))
            seg *= 0.5 - 0.5 * np.cos(np.linspace(0, 2 * np.pi, m))
        add(y, seg, pos)
        pos += length + rng.uniform(0.0, 0.008)
    m = n_of(0.17)
    wash = band_noise(rng, m, 600, 3400) * np.linspace(0.0, 0.46, m) ** 1.5
    add(y, wash, 2.90)
    add(y, band_noise(rng, n_of(0.13), 900, 2800) * np.exp(-tt(n_of(0.13)) / 0.05) * 0.10, 3.07)  # carrier hiss tail
    y = fft_filter(y, lo=300, hi=3800, order=2)  # narrow telephone band
    return finish(y, dur, fade_in=0.004, fade_out=0.05)


def hdd_seek(rng, variant):
    dur = 0.34
    n = n_of(dur)
    y = np.zeros(n)
    offsets = ([0.000, 0.011, 0.024, 0.040, 0.063, 0.098, 0.150], [0.000, 0.015, 0.029, 0.052, 0.078, 0.108, 0.146, 0.205])[variant]
    for i, off in enumerate(offsets):
        j = rng.uniform(0.85, 1.15)
        a = (0.55, 0.85, 1.0, 0.8, 0.7, 0.55, 0.4, 0.3)[i] * rng.uniform(0.85, 1.0)
        add(y, burst(rng, 0.02, 900 * j, 4200 * j, 0.0021, 0.65 * a), off)
        add(y, glide(190 * j, 120 * j, 0.004, 0.006, 0.45 * a, 0.03), off)
    # actuator motion whirr under the clicks
    m = n_of(0.17)
    whirr = band_noise(rng, m, 220, 1100) * np.sin(np.pi * np.linspace(0, 1, m)) ** 2 * 0.22
    add(y, whirr, 0.0)
    y = fft_filter(y, hi=7500)
    return finish(y, dur, fade_out=0.04)


def keyboard_mech(rng):
    dur = 0.17
    n = n_of(dur)
    y = np.zeros(n)
    j = rng.uniform(0.92, 1.08)
    add(y, burst(rng, 0.03, 2200 * j, 7600, 0.0014, 0.55), 0.0)  # switch click
    add(y, ring(3600 * j * rng.uniform(0.95, 1.05), 0.0030, 0.16, 0.05), 0.0)
    t2 = rng.uniform(0.009, 0.014)  # bottom-out clack
    add(y, burst(rng, 0.06, 650, 3300 * j, 0.0042, 0.95), t2)
    add(y, glide(330 * j, 210 * j, 0.006, 0.011, 0.55, 0.08), t2)
    add(y, ring(1900 * j, 0.0055, 0.12, 0.06), t2)
    add(y, burst(rng, 0.03, 1400, 5400, 0.0016, 0.24), rng.uniform(0.072, 0.096))  # key-up
    y = fft_filter(y, hi=11000)
    return finish(y, dur, fade_out=0.04)


def keyboard_laptop(rng):
    dur = 0.12
    n = n_of(dur)
    y = np.zeros(n)
    j = rng.uniform(0.92, 1.08)
    add(y, glide(260 * j, 170 * j, 0.005, 0.0075, 0.70, 0.07), 0.0)
    add(y, burst(rng, 0.04, 450, 2600 * j, 0.0030, 0.55, order=3), 0.0)
    add(y, burst(rng, 0.02, 1800, 4200, 0.0010, 0.12), 0.0)
    add(y, burst(rng, 0.04, 900, 2800, 0.0022, 0.18), rng.uniform(0.050, 0.065))
    y = fft_filter(y, hi=4800)
    return finish(y, dur, fade_out=0.04)


def mouse_click_old(rng):
    dur = 0.14
    n = n_of(dur)
    y = np.zeros(n)
    add(y, burst(rng, 0.03, 1200, 6200, 0.0020, 1.0), 0.0)
    add(y, ring(2600, 0.0050, 0.32, 0.05), 0.0)
    add(y, ring(1450, 0.0120, 0.25, 0.08), 0.0)
    add(y, glide(420, 300, 0.008, 0.010, 0.30, 0.05), 0.0)
    add(y, burst(rng, 0.03, 800, 3600, 0.0025, 0.45), 0.052)  # release
    return finish(y, dur, fade_out=0.05)


def bt_piece(rng, f):
    dur = 0.07
    y = np.zeros(n_of(dur))
    add(y, ring(f, 0.0042, 1.0, 0.06, attack=0.0004), 0.0)
    add(y, burst(rng, 0.01, 3000, 9000, 0.0006, 0.22), 0.0)
    return finish(y, dur, fade_in=0.0006, fade_out=0.018)


def bt_complete(rng):
    dur = 0.62
    y = np.zeros(n_of(dur))
    add(y, bell(C6, 0.16, 1.0, 0.55), 0.0)
    add(y, bell(F6, 0.19, 0.9, 0.55, shimmer=0.0012), 0.085)
    return finish(y, dur, fade_out=0.08)


def forum_post(rng):
    dur = 0.36
    y = np.zeros(n_of(dur))
    add(y, glide(190, 85, 0.050, 0.045, 1.0, 0.25), 0.0)
    add(y, burst(rng, 0.05, 500, 2500, 0.0085, 0.80), 0.0)
    add(y, ring(1100, 0.004, 0.25, 0.04), 0.0)
    for at, amp in ((0.045, 0.14), (0.095, 0.11), (0.155, 0.08)):
        add(y, burst(rng, 0.08, 1500, 6000, 0.012, amp), at)
    return finish(y, dur, fade_out=0.07)


def subtitle_snap(rng):
    dur = 0.075
    y = np.zeros(n_of(dur))
    add(y, burst(rng, 0.02, 2500, 8200, 0.0010, 0.90), 0.0)
    add(y, ring(2900, 0.0040, 0.28, 0.03), 0.0)
    add(y, burst(rng, 0.02, 1200, 4200, 0.0015, 0.48), 0.014)
    add(y, ring(900, 0.0060, 0.22, 0.04), 0.014)
    return finish(y, dur, fade_out=0.02)


def journey_air(rng, dur, peak_at, spread):
    n = n_of(dur)
    t = tt(n)

    def bump(c, s):
        return np.exp(-0.5 * ((t - c) / s) ** 2)

    low = band_noise(rng, n, 150, 700) * bump(peak_at - 0.07, spread * 1.20)
    mid = band_noise(rng, n, 700, 2800) * bump(peak_at + 0.02, spread * 1.05) * 0.80
    high = band_noise(rng, n, 2600, 8000) * bump(peak_at + 0.11, spread * 0.92) * 0.30
    gust = 1 + 0.22 * slow_noise(rng, n, 5.0)
    y = (low + mid + high) * np.clip(gust, 0.4, 1.8)
    return finish(y, dur, fade_in=0.10, fade_out=0.18, hp=110)


def campfire_crackle(rng, dur=7.2, fade=1.28):
    n = n_of(dur)
    t = tt(n)
    bed = band_noise(rng, n, 80, 600) * np.clip(1 + 0.20 * slow_noise(rng, n, 1.1), 0.4, 1.8)
    hiss = band_noise(rng, n, 2800, 9000) * np.clip(0.5 + 0.35 * slow_noise(rng, n, 3.0), 0, 2.0)
    y = 0.095 * bed + 0.030 * hiss
    rate = np.clip(5.5 + 3.5 * slow_noise(rng, n, 0.7), 1.2, 12)  # clustered crackle rate (events/s)
    step = 48  # 1 ms
    hits = np.nonzero(rng.random(n // step) < rate[::step][: n // step] * (step / RATE))[0]
    for h in hits:
        at = h * step / RATE
        amp = float(min(1.0, math.exp(rng.normal(-1.25, 0.85))))
        j = rng.uniform(0.7, 1.35)
        add(y, burst(rng, 0.014, 1800 * j, 7600 * j, rng.uniform(0.0007, 0.0024), 0.55 * amp), at)
        if rng.random() < 0.12:
            add(y, glide(rng.uniform(520, 900), 250, 0.006, rng.uniform(0.008, 0.016), 0.45 * amp, 0.05), at)
            add(y, burst(rng, 0.03, 600, 3000, 0.006, 0.60 * amp), at)
    sigma = float(np.sqrt(np.mean(y ** 2)))
    y = np.tanh(y / (3.0 * sigma)) * 3.0 * sigma  # soft-limit rare outliers: both beds keep a similar RMS/peak ratio for the seamless overlap
    x = np.clip(t / fade, 0, 1)
    y_in = np.sin(0.5 * np.pi * x)
    x = np.clip((dur - t) / fade, 0, 1)
    y_out = np.sin(0.5 * np.pi * x)
    return finish(y, dur, edge=y_in * y_out, hp=60)


def gate_close(rng):
    dur = 1.0
    y = np.zeros(n_of(dur))
    add(y, glide(135, 68, 0.055, 0.075, 1.0, 0.4), 0.0)
    add(y, burst(rng, 0.03, 1500, 8200, 0.0016, 0.85), 0.0)
    for f, a, tau in ((412, 0.50, 0.20), (937, 0.60, 0.14), (1583, 0.45, 0.10), (2410, 0.30, 0.07), (3590, 0.20, 0.05)):
        add(y, ring(f * rng.uniform(0.985, 1.015), tau, a, 0.9, attack=0.0004, phase=rng.uniform(0, 6.28)), 0.0)
    add(y, burst(rng, 0.04, 1000, 5200, 0.0022, 0.62), 0.135)  # latch click
    add(y, ring(2200, 0.025, 0.22, 0.2), 0.135)
    add(y, glide(240, 150, 0.01, 0.02, 0.30, 0.1), 0.135)
    return finish(y, dur, fade_out=0.12)


def gate_open(rng):
    dur = 1.30
    n = n_of(dur)
    t = tt(n)
    y = np.zeros(n)
    add(y, burst(rng, 0.03, 1200, 5000, 0.0015, 0.50), 0.02)  # unlatch
    add(y, ring(1700, 0.012, 0.15, 0.12), 0.02)
    env = np.clip((t - 0.06) / 0.30, 0, 1) * np.clip((0.88 - t) / 0.46, 0, 1)
    env = env * env * (3 - 2 * env)
    friction = np.clip(1 + 0.35 * slow_noise(rng, n, 24.0), 0.2, 2.0)
    y += band_noise(rng, n, 120, 900) * env * friction * 0.55
    y += band_noise(rng, n, 700, 2600) * env * friction * 0.16
    y += band_noise(rng, n, 55, 170) * env * 0.50
    add(y, glide(220, 110, 0.012, 0.028, 0.58, 0.12), 0.88)  # soft latch
    add(y, burst(rng, 0.04, 600, 2600, 0.0032, 0.46), 0.88)
    add(y, ring(1100, 0.015, 0.14, 0.15), 0.88)
    return finish(y, dur, fade_out=0.12)


def verify_ok(rng):
    dur = 0.60
    y = np.zeros(n_of(dur))
    add(y, bell(F5, 0.15, 1.0, 0.5, attack=0.008), 0.0)
    add(y, bell(A5, 0.17, 0.85, 0.5, attack=0.008, shimmer=0.0010), 0.10)
    return finish(y, dur, fade_out=0.10)


def verify_wait(rng):
    dur = 0.45
    n = n_of(dur)
    t = tt(n)
    y = (np.sin(2 * np.pi * 174.614 * t) + 0.25 * np.sin(2 * np.pi * 349.228 * t) + 0.08 * np.sin(2 * np.pi * 523.842 * t))
    y = y * np.exp(-t / 0.12) * (1 - np.exp(-t / 0.018))
    return finish(y, dur, fade_in=0.004, fade_out=0.09, hp=40)


def save_write(rng):
    dur = 1.52
    n = n_of(dur)
    t = tt(n)
    y = np.zeros(n)
    motor = band_noise(rng, n, 80, 380) * (0.5 - 0.5 * np.cos(2 * np.pi * np.clip(t / dur, 0, 1))) ** 0.6
    y += motor * 0.20
    pos, big = 0.02, {0.38, 0.86, 1.28}
    while pos < 1.36:
        j = rng.uniform(0.85, 1.2)
        amp = rng.uniform(0.30, 0.65)
        add(y, burst(rng, 0.016, 1000 * j, 4200 * j, 0.0019, amp), pos)
        add(y, glide(180 * j, 120 * j, 0.004, 0.005, 0.30 * amp, 0.02), pos)
        gap = rng.uniform(0.012, 0.034) if rng.random() < 0.78 else rng.uniform(0.07, 0.16)
        pos += gap
    for at in sorted(big):
        add(y, burst(rng, 0.03, 700, 3600, 0.0035, 0.95), at)
        add(y, glide(170, 105, 0.006, 0.010, 0.55, 0.04), at)
    add(y, glide(160, 85, 0.02, 0.04, 0.55, 0.15), 1.38)  # final settle
    add(y, burst(rng, 0.03, 500, 2800, 0.004, 0.48), 1.38)
    return finish(y, dur, fade_in=0.004, fade_out=0.06)


def msg_pop(rng):
    dur = 0.26
    y = np.zeros(n_of(dur))
    add(y, glide(420, 1050, 0.012, 0.034, 1.0, 0.18, attack=0.0007), 0.0)
    add(y, burst(rng, 0.01, 1000, 5000, 0.0007, 0.18), 0.0)
    add(y, glide(520, 1250, 0.010, 0.026, 0.52, 0.14, attack=0.0007), 0.088)
    return finish(y, dur, fade_out=0.07)


def notification(rng):
    dur = 0.42
    y = np.zeros(n_of(dur))
    add(y, bell(A5, 0.12, 1.0, 0.4, attack=0.004), 0.0)
    add(y, bell(A5, 0.10, 0.34, 0.3, attack=0.004), 0.11)
    return finish(y, dur, fade_out=0.08)


def page_flip(rng):
    dur = 0.32
    n = n_of(dur)
    y = np.zeros(n)
    add(y, burst(rng, 0.012, 1500, 7000, 0.0015, 0.20), 0.0)
    for at, amp, lo in ((0.004, 1.0, 700), (0.070, 0.55, 900), (0.150, 0.32, 1400)):
        m = n_of(0.16)
        tm = tt(m)
        swell = band_noise(rng, m, lo, 7200) * np.exp(-tm / 0.045) * (1 - np.exp(-tm / 0.012))
        add(y, swell * amp, at)
    return finish(y, dur, fade_out=0.08)


# name, file, group, function, seed, target listener-near-field peak (linear at the source, before spatial/gain), notes
CLIPS = [
    ("crt_power_on", "crt_power_on.wav", "opening", lambda r: crt_power_on(r), 5101, 0.070, "degauss thump + faint 15.625 kHz line whine"),
    ("modem_handshake", "modem_handshake.wav", "opening", lambda r: modem_handshake(r), 5102, 0.075, "procedural DTMF digits, 2100 Hz answer tone, scrambled tone/noise bursts, telephone band"),
    ("hdd_seek_a", "hdd_seek_a.wav", "disk", lambda r: hdd_seek(r, 0), 5103, 0.060, "actuator click burst, variant a"),
    ("hdd_seek_b", "hdd_seek_b.wav", "disk", lambda r: hdd_seek(r, 1), 5104, 0.060, "actuator click burst, variant b"),
    ("keyboard_mech_a", "keyboard_mech_a.wav", "keys", lambda r: keyboard_mech(r), 5105, 0.050, "clacky mechanical key"),
    ("keyboard_mech_b", "keyboard_mech_b.wav", "keys", lambda r: keyboard_mech(r), 5106, 0.050, "clacky mechanical key"),
    ("keyboard_mech_c", "keyboard_mech_c.wav", "keys", lambda r: keyboard_mech(r), 5107, 0.050, "clacky mechanical key"),
    ("keyboard_laptop_a", "keyboard_laptop_a.wav", "keys", lambda r: keyboard_laptop(r), 5108, 0.046, "soft shallow-travel key"),
    ("keyboard_laptop_b", "keyboard_laptop_b.wav", "keys", lambda r: keyboard_laptop(r), 5109, 0.046, "soft shallow-travel key"),
    ("keyboard_laptop_c", "keyboard_laptop_c.wav", "keys", lambda r: keyboard_laptop(r), 5110, 0.046, "soft shallow-travel key"),
    ("mouse_click_old", "mouse_click_old.wav", "ui", lambda r: mouse_click_old(r), 5111, 0.060, "stiff old-mouse click with release"),
    ("bt_piece_a", "bt_piece_a.wav", "bt", lambda r: bt_piece(r, F6), 5112, 0.050, "tiny soft tick, F6"),
    ("bt_piece_b", "bt_piece_b.wav", "bt", lambda r: bt_piece(r, 1760.0), 5113, 0.050, "tiny soft tick, A6"),
    ("bt_piece_c", "bt_piece_c.wav", "bt", lambda r: bt_piece(r, 2093.0), 5114, 0.050, "tiny soft tick, C7"),
    ("bt_complete", "bt_complete.wav", "bt", lambda r: bt_complete(r), 5115, 0.038, "two-note soft chime C6 -> F6"),
    ("forum_post", "forum_post.wav", "forum", lambda r: forum_post(r), 5116, 0.070, "soft paper/stamp thunk"),
    ("subtitle_snap", "subtitle_snap.wav", "ui", lambda r: subtitle_snap(r), 5117, 0.054, "short two-stage tick"),
    ("journey_air_long", "journey_air_long.wav", "air", lambda r: journey_air(r, 2.60, 1.52, 0.50), 5118, 0.085, "airy broadband swell for the 2.56 s journeys (peak 1.52 s into the clip)"),
    ("journey_air_short", "journey_air_short.wav", "air", lambda r: journey_air(r, 2.10, 1.20, 0.42), 5119, 0.085, "airy broadband swell for the 1.92 s journey (peak 1.20 s into the clip)"),
    ("campfire_crackle_a", "campfire_crackle_a.wav", "fire", lambda r: campfire_crackle(r), 5120, 0.058, "crackle bed 7.2 s, 1.28 s equal-power ends, overlaps seamlessly with b"),
    ("campfire_crackle_b", "campfire_crackle_b.wav", "fire", lambda r: campfire_crackle(r), 5121, 0.058, "crackle bed 7.2 s, different random pattern"),
    ("gate_close", "gate_close.wav", "gates", lambda r: gate_close(r), 5122, 0.075, "metal latch clank"),
    ("gate_open", "gate_open.wav", "gates", lambda r: gate_open(r), 5123, 0.070, "slide plus soft latch"),
    ("verify_ok", "verify_ok.wav", "gates", lambda r: verify_ok(r), 5124, 0.050, "soft two-tone F5 -> A5"),
    ("verify_wait", "verify_wait.wav", "gates", lambda r: verify_wait(r), 5125, 0.060, "soft low F3 pulse"),
    ("save_write", "save_write.wav", "disk", lambda r: save_write(r), 5126, 0.060, "disk write chatter, 1.52 s"),
    ("msg_pop", "msg_pop.wav", "ui", lambda r: msg_pop(r), 5127, 0.044, "original soft double pop for IM-context focus"),
    ("notification", "notification.wav", "ui", lambda r: notification(r), 5128, 0.040, "single soft A5 ping with echo (replaces V4 notification)"),
    ("page_flip", "page_flip.wav", "ui", lambda r: page_flip(r), 5129, 0.050, "paper flick (replaces V4 page_flip)"),
]
CLIP_INDEX = {c[0]: i for i, c in enumerate(CLIPS)}

# emitters: id -> (kind, payload). 'frame' positions are worldFrame-local (region offset included); the
# physical position at use time is frame_local - virtual_offset(region).
EMITTERS = [
    {"id": "content", "kind": "existing", "source": "OWV4Foley.contentWorldPosition", "physical": [0.0, 2.1, 5.6]},
    {"id": "keyboard", "kind": "existing", "source": "OWV4Foley.emitters[1] (V1 keyboard contact point)", "physical": [-1.862, 0.645, 1.534]},
    {"id": "journey-air", "kind": "physical", "physical": [0.0, 2.0, -1.2]},
    {"id": "campfire", "kind": "frame", "region": 2, "frame_local": [0.0, 0.96, 56.6]},
    {"id": "gate-a", "kind": "frame", "region": 3, "frame_local": [0.0, 1.1, 86.0]},
    {"id": "gate-b", "kind": "frame", "region": 3, "frame_local": [0.0, 1.1, 90.2]},
    {"id": "gate-c", "kind": "frame", "region": 3, "frame_local": [4.8, 1.1, 86.0]},
    {"id": "archive-desk", "kind": "frame", "region": 3, "frame_local": [4.65, 0.8, 84.9]},
]
EM = {e["id"]: i for i, e in enumerate(EMITTERS)}
REGION_Z = {0: 0.0, 1: 24.0, 2: 52.0, 3: 80.0}

# fate of every V4 event: keep (clip mapped), replace (by V5 sequence) or drop (collides / inside a journey)
V4_FATE = {1.28: "replace", 6.4: "keep", 11.52: "keep", 12.8: "drop", 13.12: "drop", 13.44: "drop", 17.92: "drop", 23.04: "keep",
           25.6: "keep", 30.72: "keep", 35.84: "keep", 38.4: "keep", 42.24: "drop", 48.64: "drop", 64.0: "drop", 66.56: "drop",
           72.96: "keep", 76.8: "keep", 79.36: "drop", 83.2: "keep", 93.44: "keep", 98.56: "keep", 103.68: "drop", 108.8: "drop",
           115.2: "drop", 123.52: "drop", 126.08: "drop", 128.64: "drop", 131.2: "keep"}


def q(t):
    k = int(math.floor(t / GRID + 0.5 + 1e-9))
    return round(k * GRID, 2)


def build_events():
    ops = {g["id"]: g for g in json.loads((ROOT / "data/episode123-v4-operations.json").read_text())["groups"]}
    score = json.loads((ROOT / "data/episode123-v4-score.json").read_text())
    v4 = json.loads((ROOT / "data/episode123-v4-foley.json").read_text())
    events = []

    def ev(t, clip, emitter, tag):
        events.append({"time": q(t), "clip": CLIP_INDEX[clip], "emitter": EM[emitter], "name": clip, "tag": tag})

    def taps(start, gaps, kind, seed, tag, emitter="keyboard"):
        rng = np.random.default_rng(seed)
        t = start
        for i, gap in enumerate(gaps):
            names = ("keyboard_mech_a", "keyboard_mech_b", "keyboard_mech_c") if kind == "mech" else ("keyboard_laptop_a", "keyboard_laptop_b", "keyboard_laptop_c")
            ev(t, names[int(rng.integers(3))], emitter, tag)
            t += gap * GRID

    # opening
    ev(0.08, "crt_power_on", "content", "CRT power on")
    ev(1.28, "modem_handshake", "content", "connection handshake replaces V4 connection")
    # V4 events that survive (cursor hits, page flips, notifications)
    for item in v4["events"]:
        t = round(item["time"], 2)
        fate = V4_FATE.get(t)
        if fate is None:
            raise SystemExit(f"V4 event at {t} has no decision in V4_FATE")
        if fate != "keep":
            continue
        clip = {0: "mouse_click_old", 1: "keyboard_mech_b", 2: "notification", 3: "page_flip"}[item["clip"]]
        if clip == "keyboard_mech_b" and t >= MECH_UNTIL:
            clip = "keyboard_laptop_b"
        ev(t, clip, "keyboard" if clip.startswith("keyboard") else "content", f"V4 event kept at {t}")
    # explorer / README (10.24-20.48)
    e = ops["explorer"]
    ev(e["drag_end"], "mouse_click_old", "content", "file dropped (V4 click 15.36)")
    ev(15.52, "mouse_click_old", "content", "second click opens (V4 click 15.56)")
    ev(15.68, "hdd_seek_a", "content", "file opens: disk read")
    ev(15.92, "hdd_seek_b", "content", "disk read")
    ev(16.24, "hdd_seek_a", "content", "README loads")
    taps(e["type_start"] + 0.08, [1, 2, 1, 1, 2, 1, 3, 1, 1, 2, 1, 1], "mech", 6201, "README typing")
    ev(e["completed"], "mouse_click_old", "content", "README saved")
    ev(18.96, "hdd_seek_b", "content", "README write")
    # early making / translating bursts (mechanical keyboard era)
    taps(26.40, [2, 1, 1, 2], "mech", 6202, "homepage typing")
    taps(32.56, [2, 1, 2], "mech", 6203, "forum reply typing")
    taps(36.80, [1, 2, 1, 2], "mech", 6204, "translation typing")
    # BT (40.96-51.20)
    bt = ops["torrent"]
    for r in bt["receive"]:
        ev(r["arrival"], ("bt_piece_a", "bt_piece_b", "bt_piece_c")[r["piece"] % 3], "content", f"piece {r['piece']} received")
    ev(bt["complete_at"] + 0.08, "bt_complete", "content", "download complete, hash passes")
    ev(47.52, "hdd_seek_a", "content", "hash read")
    for i, u in enumerate(bt["upload"]):
        ev(u["arrival"], "bt_complete", "content", f"upload {i} delivered")
    # Chinese communities (51.20-61.44): focus landings and one reply
    focus = [x for x in score["events"] if x["role"] == "focus" and MECH_UNTIL <= x["start"] + x["enter"] < 61.44]
    for x in focus:
        ev(x["start"] + x["enter"], "forum_post", "content", f"community page lands: {x['image']}")
    taps(58.88, [2, 1, 2], "laptop", 6205, "reply typing", "keyboard")
    ev(59.52, "forum_post", "content", "reply posted")
    # subtitles (61.44-71.68): Aegisub confirmations, typing and the Play Video click
    sub = ops["subtitles"]
    confirm = [round(sub["start"] + 0.88 + sub["line_step"] * i, 2) for i in range(len(sub["lines"]))]
    patterns = {0: [3, 3, 2], 1: [5], 2: [1, 1, 2, 3], 3: [4], 4: [2, 3, 2], 5: [4], 6: [2, 3, 2], 7: [5]}
    for i, c in enumerate(confirm):
        gaps = patterns[i]
        total = sum(gaps)
        taps(c - total * GRID, gaps, "laptop", 6300 + i, f"subtitle line {i + 1} typing")
        ev(c, "subtitle_snap", "content", f"subtitle {i + 1} confirmed")
    ev(sub["start"] + sub["play_video_click"]["relative_time"], "mouse_click_old", "content", "Aegisub Play Video click")
    ev(sub["export_at"], "notification", "content", "movie.zh-cn.ass exported")
    # making / tutorials
    taps(73.44, [1, 2, 1, 2], "laptop", 6206, "SDK/build typing")
    taps(77.52, [2, 1, 2, 1], "laptop", 6207, "lighting parameter typing")
    # journeys
    for i, (a, b) in enumerate(JOURNEYS):
        ev(a - 0.24, "journey_air_long" if b - a > 2.0 else "journey_air_short", "journey-air", f"journey {i + 1} air")
    # 2019 campfire
    ev(CAMPFIRE_WINDOW[0], "campfire_crackle_a", "campfire", "campfire bed in")
    ev(94.72, "campfire_crackle_b", "campfire", "campfire bed continues (1.28 s equal-power overlap)")
    # conditions: 3Q-era IM pops, gates, verification
    ev(105.60, "msg_pop", "content", "3Q focus lands (IM context)")
    ev(107.52, "msg_pop", "gate-a", "gate A shows the cannot-run dialog")
    ev(108.96, "gate_close", "gate-a", "gate A leaf latches")
    ev(110.08, "msg_pop", "content", "3Q forum focus lands")
    ev(112.64, "gate_close", "gate-b", "gate B closes: validating")
    ev(113.92, "verify_wait", "gate-b", "validating pulse")
    ev(115.20, "gate_open", "gate-a", "gate A reopens")
    ev(116.48, "verify_wait", "gate-b", "validating pulse")
    ev(117.76, "gate_close", "gate-c", "gate C locks")
    ev(118.40, "gate_open", "gate-b", "gate B opens")
    ev(120.32, "verify_ok", "gate-b", "gate B verified")
    ev(120.40, "gate_open", "gate-c", "gate C opens")
    ev(122.88, "verify_ok", "gate-c", "gate C: connection restored")
    # maintenance / archive
    taps(123.92, [3, 3, 3, 4], "laptop", 6208, "checking the work", "archive-desk")
    ev(125.44, "page_flip", "archive-desk", "document slides into the archive")
    ev(126.48, "save_write", "archive-desk", "archive save, ends 128.00")
    ev(128.00, "verify_ok", "archive-desk", "saved copy confirmed")
    events.sort(key=lambda x: (x["time"], x["clip"], x["emitter"]))
    return events, v4


def check(events, durations):
    """Local contract check, mirrored by OWEpisodeV5Foley.Validate in the editor."""
    last = -1.0
    for x in events:
        t = x["time"]
        assert abs(t / GRID - round(t / GRID)) < 1e-4, ("grid", x)
        assert 0.0 <= t < SHOW, ("range", x)
        assert t >= last - 1e-9, ("sorted", x)
        last = t
        name = x["name"]
        if name.startswith("journey_air"):
            continue
        for a, b in JOURNEYS:
            assert not (a - 1e-4 <= t <= b + 1e-4), ("inside journey", x)
        if name.startswith("keyboard_mech"):
            assert t < MECH_UNTIL, ("mechanical keyboard after 51.20", x)
        if name.startswith("keyboard_laptop"):
            assert t >= MECH_UNTIL, ("laptop keyboard before 51.20", x)
        if name.startswith("campfire"):
            assert CAMPFIRE_WINDOW[0] - 1e-6 <= t and t + durations[name] <= CAMPFIRE_WINDOW[1] + 1e-6, ("campfire outside window", x)
    air = [x["time"] for x in events if x["name"].startswith("journey_air")]
    assert [round(a - 0.24, 2) for a, _ in JOURNEYS] == air, ("journey air times", air)
    fire = sorted(x["time"] for x in events if x["name"].startswith("campfire"))
    assert fire == [88.80, 94.72], fire


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--no-audio", action="store_true", help="rewrite the JSON only (reads existing WAV headers)")
    parser.add_argument("--master-scale", type=float, default=1.0, help="global multiplier applied to every AudioSource.volume (recorded in the JSON)")
    options = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    clip_rows = []
    durations = {}
    for name, filename, group, fn, seed, target, note in CLIPS:
        path = OUT / filename
        if options.no_audio:
            wave, rate = sf.read(path, dtype="float32")
        else:
            wave = fn(np.random.default_rng(seed))
            rate = RATE
            assert wave.ndim == 1 and np.isfinite(wave).all() and abs(float(np.mean(wave))) < 2e-4
            assert abs(float(wave[0])) < 2e-3 and abs(float(wave[-1])) < 2e-3, name
            sf.write(path, wave, RATE, subtype="PCM_16")
        peak = float(np.max(np.abs(wave)))
        rms = float(np.sqrt(np.mean(wave.astype(np.float64) ** 2)))
        durations[name] = len(wave) / RATE
        clip_rows.append({
            "id": name, "file": filename, "group": group, "seed": seed, "duration": round(len(wave) / RATE, 4),
            "sample_rate": RATE, "channels": 1, "peak_dbfs": round(20 * math.log10(peak), 2), "rms_dbfs": round(20 * math.log10(rms), 2),
            "source_peak": target, "volume": round(target / peak * options.master_scale, 4), "note": note,
        })
    events, v4 = build_events()
    check(events, durations)
    out_events = [{"time": x["time"], "clip": x["clip"], "emitter": x["emitter"], "name": x["name"], "tag": x["tag"]} for x in events]
    counts = {}
    for x in events:
        counts[x["name"]] = counts.get(x["name"], 0) + 1
    data = {
        "version": 1,
        "revision": 5,
        "authorship": "Original numpy-synthesised era Foley. No recordings, no voices, no imitation of Windows/VRChat/QQ/MSN or other system sounds.",
        "grid_seconds": GRID,
        "clip_peak_dbfs": PEAK_DB,
        "master_scale": options.master_scale,
        "level_policy": "Clip files are peak-normalised to clip_peak_dbfs; AudioSource.volume = master_scale * source_peak / clip peak, so scene level is set per clip by source_peak (V4 sources peaked at 0.0546).",
        "tonal_centre": "F (FC14 stems: ~46 % of pitch-class energy); chimes use C6/F6 and F5/A5, low pulse F3",
        "journeys": [list(j) for j in JOURNEYS],
        "campfire_window": list(CAMPFIRE_WINDOW),
        "mechanical_keyboard_until": MECH_UNTIL,
        "audio_directory": "art/export/episode123-v5/foley",
        "remote_audio_folder": "Assets/OldWebMap/Episode123/Revision5/Foley/Audio",
        "files": [c["file"] for c in clip_rows],
        "clips": clip_rows,
        "emitters": EMITTERS,
        "region_offsets_z": REGION_Z,
        "events": out_events,
        "event_counts": counts,
        "event_total": len(out_events),
        "v4_event_fate": {f"{t:.2f}": fate for t, fate in sorted(V4_FATE.items())},
    }
    DATA.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n")
    print(f"{len(clip_rows)} clips -> {OUT}\n{len(out_events)} events -> {DATA}")
    for row in clip_rows:
        print(f"  {row['id']:<20} {row['duration']:6.3f}s peak {row['peak_dbfs']:6.2f} rms {row['rms_dbfs']:7.2f} volume {row['volume']:.4f}")


if __name__ == "__main__":
    main()
