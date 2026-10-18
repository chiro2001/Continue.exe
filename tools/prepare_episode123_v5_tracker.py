#!/usr/bin/env python3
"""Bake the V5 tracker side-panel atlas and data from the real FC14 note table.

Input : audio/keil-keygen-2008/tracks.csv (+ tune.fc14 for an independent cross-check)
Output: art/export/episode123-v5/signal/tracker-atlas.png
        data/episode123-v5-tracker.json
        ~/tmp/vrc-old-web-map/episode123-v5/signal/{tracker-selfcheck.json,tracker-preview-*.png}

No Unity serialized assets are written; the main agent copies the PNG to
Revision5/Signal/Textures and the JSON to AgentScripts/data.  Run with
~/miniforge3/bin/python.  --check re-reads the PNG/JSON and decodes every row.
"""
from __future__ import annotations
import argparse
import csv
import hashlib
import json
import struct
import sys
import zlib
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
CSV_PATH = ROOT / "audio/keil-keygen-2008/tracks.csv"
TUNE_PATH = ROOT / "audio/keil-keygen-2008/tune.fc14"
ATLAS_PATH = ROOT / "art/export/episode123-v5/signal/tracker-atlas.png"
JSON_PATH = ROOT / "data/episode123-v5-tracker.json"
TMP = Path.home() / "tmp/vrc-old-web-map/episode123-v5/signal"

ROW_SECONDS = 0.08
ROWS_PER_ORDER = 32
ORDERS = 100
ROWS = ORDERS * ROWS_PER_ORDER          # 3200 rows = 256 s
SHOW_SECONDS = 133.12                   # the performance ends here
SLOT_W, SLOT_H, SLOTS_X = 128, 8, 16    # one strip = 16 chars x 8 px; 16 strips per atlas row
PAIR_SLOTS = ROWS // 2                  # even/odd rows share a slot (R,G | B,A)
HEADER_SLOT = PAIR_SLOTS                # 1600
FONT_SLOT = PAIR_SLOTS + 1              # 1601
TOTAL_SLOTS = PAIR_SLOTS + 2
ATLAS_W = SLOT_W * SLOTS_X              # 2048
ATLAS_H = SLOT_H * ((TOTAL_SLOTS + SLOTS_X - 1) // SLOTS_X)   # 808

# Brightness classes baked into the glyph coverage (shader multiplies by coverage/255).
LV_NOTE = 255
LV_EMPTY = 78
LV_SEP = 110
LV_ROW_BEAT8, LV_ROW_BEAT4, LV_ROW = 255, 200, 140
LV_HEAD, LV_HEAD_DIM = 255, 140

NOTE_NAMES = ["C-", "C#", "D-", "D#", "E-", "F-", "F#", "G-", "G#", "A-", "A#", "B-"]

# 5x7 font (classic dot-matrix shapes); placed at x=1..5 of an 8x8 cell and emboldened by 1 px.
_GLYPHS = {
    "0": ["01110", "10001", "10011", "10101", "11001", "10001", "01110"],
    "1": ["00100", "01100", "00100", "00100", "00100", "00100", "01110"],
    "2": ["01110", "10001", "00001", "00010", "00100", "01000", "11111"],
    "3": ["11111", "00010", "00100", "00010", "00001", "10001", "01110"],
    "4": ["00010", "00110", "01010", "10010", "11111", "00010", "00010"],
    "5": ["11111", "10000", "11110", "00001", "00001", "10001", "01110"],
    "6": ["00110", "01000", "10000", "11110", "10001", "10001", "01110"],
    "7": ["11111", "00001", "00010", "00100", "01000", "01000", "01000"],
    "8": ["01110", "10001", "10001", "01110", "10001", "10001", "01110"],
    "9": ["01110", "10001", "10001", "01111", "00001", "00010", "01100"],
    "A": ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
    "B": ["11110", "10001", "10001", "11110", "10001", "10001", "11110"],
    "C": ["01110", "10001", "10000", "10000", "10000", "10001", "01110"],
    "D": ["11110", "10001", "10001", "10001", "10001", "10001", "11110"],
    "E": ["11111", "10000", "10000", "11110", "10000", "10000", "11111"],
    "F": ["11111", "10000", "10000", "11110", "10000", "10000", "10000"],
    "G": ["01110", "10001", "10000", "10111", "10001", "10001", "01111"],
    "H": ["10001", "10001", "10001", "11111", "10001", "10001", "10001"],
    "I": ["01110", "00100", "00100", "00100", "00100", "00100", "01110"],
    "N": ["10001", "10001", "11001", "10101", "10011", "10001", "10001"],
    "O": ["01110", "10001", "10001", "10001", "10001", "10001", "01110"],
    "P": ["11110", "10001", "10001", "11110", "10000", "10000", "10000"],
    "S": ["01111", "10000", "10000", "01110", "00001", "00001", "11110"],
    "-": ["00000", "00000", "00000", "11111", "00000", "00000", "00000"],
    "#": ["01010", "01010", "11111", "01010", "11111", "01010", "01010"],
    "|": ["00100", "00100", "00100", "00100", "00100", "00100", "00100"],
    " ": ["00000"] * 7,
}


def glyph_bitmap(ch: str, bold: bool = True) -> np.ndarray:
    rows = _GLYPHS[ch]
    bmp = np.zeros((8, 8), dtype=bool)
    for y, line in enumerate(rows):
        for x, bit in enumerate(line):
            if bit == "1":
                bmp[y, 1 + x] = True
                if bold and ch != "|":
                    bmp[y, 2 + x] = True
    if bold and ch == "|":
        bmp[:7, 3] = True
        bmp[:7, 4] = False
    return bmp


GLYPHS = {ch: glyph_bitmap(ch) for ch in _GLYPHS}


def note_name(note: int) -> str:
    """FC14 final note index (pattern byte + sequence transpose) -> tracker name.

    Same labelling as OpenMPT's Load_fc (NOTE_MIN + index, octave 0 = C-0) and as
    ProTracker period labelling (index 12 = period 856 = C-1).
    """
    if not 0 <= note < 72:
        raise ValueError(f"note index out of table range: {note}")
    return NOTE_NAMES[note % 12] + str(note // 12)


def cell_text(event) -> str:
    if event is None:
        return "--- --"
    return f"{note_name(event[0])} {event[1]:02X}"


def load_events():
    """events[ch][row] = (note, instrument) from tracks.csv (verbatim CSV integers)."""
    events = [dict() for _ in range(4)]
    times = [[] for _ in range(4)]
    with CSV_PATH.open(newline="") as fh:
        for r in csv.DictReader(fh):
            ch = int(r["channel"]) - 1
            row = int(r["order"]) * ROWS_PER_ORDER + int(r["row"])
            if int(r["volume_slide"]) or int(r["pitch_slide"]):
                raise SystemExit("CSV has an effect value; extend the strip layout before baking")
            t = float(r["time_s"])
            if abs(t - row * ROW_SECONDS) > 1e-6:
                raise SystemExit(f"row/time mismatch at ch{ch + 1} row {row}: {t}")
            if row in events[ch]:
                raise SystemExit(f"duplicate event ch{ch + 1} row {row}")
            events[ch][row] = (int(r["note"]), int(r["instrument"]), int(r["raw"]), t)
            times[ch].append((row, t))
    for ch in range(4):
        times[ch].sort()
    return events, times


def strip_chars(row: int, panel: int, events):
    """Return (text, levels) of the 16-char strip for one panel and tracker row."""
    rin = row % ROWS_PER_ORDER
    gutter = f"{rin:02X}"
    lv_g = LV_ROW_BEAT8 if rin % 8 == 0 else LV_ROW_BEAT4 if rin % 4 == 0 else LV_ROW
    text, levels = gutter, [lv_g, lv_g]
    for idx in range(2):
        ch = panel * 2 + idx
        ev = events[ch].get(row)
        text += "|"
        levels.append(LV_SEP)
        body = cell_text(ev[:2] if ev else None)
        text += body
        levels += [LV_NOTE if ev else LV_EMPTY] * len(body)
    assert len(text) == 16, text
    return text, levels


def header_chars(panel: int):
    a, b = panel * 2 + 1, panel * 2 + 2
    text = f"  |CH{a} IN|CH{b} IN"
    levels = [0, 0, LV_SEP] + [LV_HEAD] * 3 + [0] + [LV_HEAD_DIM] * 2 + [LV_SEP] + [LV_HEAD] * 3 + [0] + [LV_HEAD_DIM] * 2
    assert len(text) == 16 and len(levels) == 16
    return text, levels


def draw_strip(text: str, levels) -> np.ndarray:
    out = np.zeros((SLOT_H, SLOT_W), dtype=np.uint8)
    for i, ch in enumerate(text):
        g = GLYPHS[ch]
        out[:, i * 8:(i + 1) * 8][g] = levels[i]
    return out


def slot_origin(slot: int):
    return (slot % SLOTS_X) * SLOT_W, (slot // SLOTS_X) * SLOT_H


def build_atlas(events) -> np.ndarray:
    atlas = np.zeros((ATLAS_H, ATLAS_W, 4), dtype=np.uint8)
    for row in range(ROWS):
        slot, parity = row >> 1, row & 1
        x0, y0 = slot_origin(slot)
        for panel in range(2):
            text, levels = strip_chars(row, panel, events)
            atlas[y0:y0 + SLOT_H, x0:x0 + SLOT_W, parity * 2 + panel] = draw_strip(text, levels)
    x0, y0 = slot_origin(HEADER_SLOT)
    for panel in range(2):
        text, levels = header_chars(panel)
        atlas[y0:y0 + SLOT_H, x0:x0 + SLOT_W, panel] = draw_strip(text, levels)
    x0, y0 = slot_origin(FONT_SLOT)
    hexes = "0123456789ABCDEF"
    atlas[y0:y0 + SLOT_H, x0:x0 + SLOT_W, 0] = draw_strip(hexes, [255] * 16)
    return atlas


def strip_region(atlas: np.ndarray, slot: int) -> np.ndarray:
    x0, y0 = slot_origin(slot)
    return atlas[y0:y0 + SLOT_H, x0:x0 + SLOT_W, :]


def crc(region: np.ndarray) -> int:
    return zlib.crc32(np.ascontiguousarray(region).tobytes()) & 0xFFFFFFFF


def sha256_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


# ---------------------------------------------------------------- decoding (self-check)
_REV = {g.tobytes(): ch for ch, g in GLYPHS.items()}


def decode_strip(channel_plane: np.ndarray):
    """128x8 uint8 plane -> (text, per-char level)."""
    text, levels = "", []
    for i in range(16):
        cell = channel_plane[:, i * 8:(i + 1) * 8]
        mask = cell > 0
        ch = _REV.get(mask.tobytes())
        if ch is None:
            raise ValueError(f"unknown glyph at char {i}")
        text += ch
        levels.append(int(cell[mask].max()) if mask.any() else 0)
        if mask.any() and len(set(cell[mask].tolist())) != 1:
            raise ValueError("glyph pixels must share one level")
    return text, levels


def decode_row(atlas: np.ndarray, row: int, panel: int):
    slot, parity = row >> 1, row & 1
    return decode_strip(strip_region(atlas, slot)[:, :, parity * 2 + panel])


def parse_cell(text: str):
    """'F-2 0B' -> (note index, instrument) ; '--- --' -> None."""
    if text == "--- --":
        return None
    name, ins = text[:3], text[4:6]
    base = NOTE_NAMES.index(name[:2])
    return int(name[2]) * 12 + base, int(ins, 16)


def fc14_module_events():
    """Independent cross-check straight from the FC14 module (note = pattern byte + sequence transpose)."""
    d = TUNE_PATH.read_bytes()
    assert d[:4] == b"FC14"
    seq_len, pat_off, pat_len = struct.unpack(">3I", d[4:16])
    pats = d[pat_off:pat_off + pat_len]
    out = [dict() for _ in range(4)]
    for order in range(seq_len // 13):
        blk = d[0xB4 + order * 13:0xB4 + (order + 1) * 13]
        for ch in range(4):
            pat = blk[ch * 3]
            tr = struct.unpack("b", blk[ch * 3 + 1:ch * 3 + 2])[0]
            itr = struct.unpack("b", blk[ch * 3 + 2:ch * 3 + 3])[0]
            for r in range(32):
                n, info = pats[pat * 64 + r * 2], pats[pat * 64 + r * 2 + 1]
                if n == 0:
                    continue
                if n == 0x49 or info & 0xC0:
                    raise SystemExit("pattern break/portamento flag found; extend the effect column")
                out[ch][order * 32 + r] = ((n + tr) & 0x7F, (info & 0x3F) + itr + 1)
    return out


# ---------------------------------------------------------------- build
SPOT_TIMES = [0.0, 2.16, 5.12, 10.24, 29.44, 30.72, 51.2, 81.92, 92.16, 102.40, 112.64, 122.88, 133.04]


def build_json(events, times, atlas):
    channels = []
    for ch in range(4):
        evs = [[row, events[ch][row][0], events[ch][row][1]] for row in sorted(events[ch])]
        channels.append({
            "channel": ch + 1,
            "events": evs,
            "noteOnTimes": [t for _, t in times[ch] if t <= SHOW_SECONDS + 0.5],
        })
    spots = []
    for t in SPOT_TIMES:
        row = int(round(t / ROW_SECONDS))
        slot = row >> 1
        entry = {
            "time": t, "row": row, "order": row // ROWS_PER_ORDER, "rowInOrder": row % ROWS_PER_ORDER,
            "cells": [cell_text((events[c][row][0], events[c][row][1]) if row in events[c] else None) for c in range(4)],
            "strip": [strip_chars(row, p, events)[0] for p in range(2)],
            "slot": slot, "parity": row & 1, "stripCrc32": crc(strip_region(atlas, slot)),
        }
        spots.append(entry)
    return {
        "schema": "oldwebmap.tracker.v5",
        "source": "audio/keil-keygen-2008/tracks.csv",
        "csvSha256": sha256_file(CSV_PATH),
        "tuneSha256": sha256_file(TUNE_PATH),
        "rowSeconds": ROW_SECONDS, "rowsPerOrder": ROWS_PER_ORDER, "orders": ORDERS, "rows": ROWS,
        "showSeconds": SHOW_SECONDS, "eventCounts": [len(c["events"]) for c in channels],
        "atlas": {
            "file": "tracker-atlas.png", "width": ATLAS_W, "height": ATLAS_H,
            "slotWidth": SLOT_W, "slotHeight": SLOT_H, "slotsX": SLOTS_X, "pairSlots": PAIR_SLOTS,
            "headerSlot": HEADER_SLOT, "fontSlot": FONT_SLOT, "sha256": sha256_file(ATLAS_PATH),
            "packing": "slot = row>>1; even row -> R(panel0) G(panel1), odd row -> B(panel0) A(panel1); header slot R,G; hex font R. "
                       "Strip = 16 chars x 8 px: 'RR|NNN II|NNN II'. Coverage value = brightness class "
                       f"(note {LV_NOTE}, empty {LV_EMPTY}, separator {LV_SEP}, row gutter {LV_ROW_BEAT8}/{LV_ROW_BEAT4}/{LV_ROW}). "
                       "Linear data, point filter, no mips. PNG row 0 is top: v = 1 - (y+0.5)/height.",
        },
        "noteNaming": {
            "rule": "name = NOTE[n%12] + str(n//12) with n = tracks.csv 'note' (pattern byte + sequence note transpose)",
            "names": NOTE_NAMES,
            "range": "observed n = 0..46 (C-0..A#3); table range 0..71",
            "instrument": "tracks.csv 'instrument' (1-based: info&0x3F + sequence instrument transpose + 1), printed as 2 hex digits",
            "effects": "none: volume_slide and pitch_slide are 0 for all 2953 events; the module has no pattern break (0x49) or portamento flags",
            "emptyCell": "--- --  (FC14 holds the previous voice until the next event; empty rows are not silence)",
            "uncertainty": "Absolute pitch also depends on each instrument's waveform length; octave labels are tracker conventions, not concert pitch.",
        },
        "channels": channels,
        "spot": spots,
    }


def build():
    events, times = load_events()
    counts = [len(e) for e in events]
    if counts != [963, 487, 656, 847]:
        raise SystemExit(f"unexpected CSV event counts {counts}")
    atlas = build_atlas(events)
    ATLAS_PATH.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(atlas, "RGBA").save(ATLAS_PATH, optimize=True)
    data = build_json(events, times, atlas)
    JSON_PATH.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
    print(f"atlas {ATLAS_W}x{ATLAS_H} -> {ATLAS_PATH}  ({ATLAS_PATH.stat().st_size} bytes)")
    print(f"json  -> {JSON_PATH}  ({JSON_PATH.stat().st_size} bytes)  events={counts}")


# ---------------------------------------------------------------- self check
def check():
    TMP.mkdir(parents=True, exist_ok=True)
    events, times = load_events()
    img = np.array(Image.open(ATLAS_PATH).convert("RGBA"))
    assert img.shape == (ATLAS_H, ATLAS_W, 4), img.shape
    data = json.loads(JSON_PATH.read_text(encoding="utf-8"))
    problems = []
    if data["atlas"]["sha256"] != sha256_file(ATLAS_PATH):
        problems.append("atlas sha256 differs from JSON")
    if data["csvSha256"] != sha256_file(CSV_PATH):
        problems.append("csv sha256 differs from JSON")

    # 1. decode every row of both panels and compare with the CSV integers
    decoded_events = 0
    for row in range(ROWS):
        rin = row % ROWS_PER_ORDER
        for panel in range(2):
            text, levels = decode_row(img, row, panel)
            if text[:2] != f"{rin:02X}" or text[2] != "|" or text[9] != "|":
                problems.append(f"row {row} p{panel}: gutter/separator '{text}'")
                continue
            for idx, a in enumerate((3, 10)):
                ch = panel * 2 + idx
                cell = text[a:a + 6]
                parsed = parse_cell(cell)
                csv_ev = events[ch].get(row)
                want = (csv_ev[0], csv_ev[1]) if csv_ev else None
                if parsed != want:
                    problems.append(f"row {row} ch{ch + 1}: atlas '{cell}' -> {parsed}, csv {want}")
                lv = set(levels[a:a + 6]) - {0}
                if lv != {LV_NOTE if csv_ev else LV_EMPTY}:
                    problems.append(f"row {row} ch{ch + 1}: brightness class {lv}")
                if csv_ev:
                    decoded_events += 1
                    if abs(csv_ev[3] - row * ROW_SECONDS) > 1e-6:
                        problems.append(f"row {row} ch{ch + 1}: time mismatch")
    # 2. header and font strips
    for panel in range(2):
        text, _ = decode_strip(strip_region(img, HEADER_SLOT)[:, :, panel])
        want = f"  |CH{panel * 2 + 1} IN|CH{panel * 2 + 2} IN"
        if text != want:
            problems.append(f"header p{panel}: '{text}'")
    ftext, _ = decode_strip(strip_region(img, FONT_SLOT)[:, :, 0])
    if ftext != "0123456789ABCDEF":
        problems.append("font strip mismatch")
    # 3. independent FC14 module cross-check (note = pattern byte + transpose; instrument)
    mod = fc14_module_events()
    for ch in range(4):
        csv_set = {row: (e[0], e[1]) for row, e in events[ch].items()}
        if csv_set != mod[ch]:
            problems.append(f"ch{ch + 1}: CSV differs from module decode")
    # 4. JSON spot rows against CSV, flash timing arrays against CSV, CRCs against PNG
    for s in data["spot"]:
        row = s["row"]
        for ch in range(4):
            ev = events[ch].get(row)
            want = cell_text((ev[0], ev[1]) if ev else None)
            if s["cells"][ch] != want:
                problems.append(f"json spot {s['time']} ch{ch + 1}: {s['cells'][ch]} != {want}")
        if crc(strip_region(img, s["slot"])) != s["stripCrc32"]:
            problems.append(f"json spot {s['time']}: strip CRC differs from PNG")
    for ch in range(4):
        want = [t for _, t in times[ch] if t <= SHOW_SECONDS + 0.5]
        if data["channels"][ch]["noteOnTimes"] != want:
            problems.append(f"ch{ch + 1} noteOnTimes differ from CSV")
        if len(data["channels"][ch]["events"]) != len(events[ch]):
            problems.append(f"ch{ch + 1} event count differs")
    report = {
        "success": not problems, "problems": problems[:40], "rowsDecoded": ROWS * 2, "eventsDecoded": decoded_events,
        "atlas": [ATLAS_W, ATLAS_H], "spotRows": [(s["time"], s["strip"]) for s in data["spot"] if s["time"] in (5.12, 29.44, 81.92, 102.4)],
        "moduleCrossCheck": "tracks.csv == FC14 module decode (note = pattern byte + sequence transpose, instrument = info&0x3F + instrument transpose + 1)",
    }
    (TMP / "tracker-selfcheck.json").write_text(json.dumps(report, ensure_ascii=False, indent=1), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=1))
    if problems:
        sys.exit(1)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--check", action="store_true", help="only decode the existing PNG/JSON and compare with the CSV")
    ap.add_argument("--no-check", action="store_true", help="skip the automatic self-check after building")
    args = ap.parse_args(argv)
    if not args.check:
        build()
    if args.check or not args.no_check:
        check()


if __name__ == "__main__":
    main()
