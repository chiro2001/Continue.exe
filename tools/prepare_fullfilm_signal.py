#!/usr/bin/env python3
"""Bake the 256 s signal layer; reads V5 assets and never overwrites them.

The original V3 PCM baker performs all waveform sampling. Zero post-roll only
provides its forward-looking probe with samples beyond the exact 256 s EOF.
No audio is played, remixed, or changed. Unity import is left to Pipeline.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image

import prepare_episode123_v3_waveforms as waves
import draw_episode123_v5_identity as identity

ROOT = Path(__file__).resolve().parents[1]
TMP = Path.home() / "tmp/vrc-old-web-map/fullfilm-v1/signal"
JOURNEYS = [(79.36,81.92,24), (86.40,88.32,28), (102.40,104.96,28), (217.60,222.72,28)]


def motion_clock(time: float, data: dict | None = None) -> float:
    """Same spatial clock as OWFullSignal; music/sample time itself never pauses."""
    time = min(256.0, max(0.0, time))
    journeys = data["journeys"] if data else [dict(start=a,end=b) for a,b,_ in JOURNEYS]
    paused = 0.0
    for journey in journeys:
        start, end = journey["start"], journey["end"]
        if time < start: break
        if time <= end: return start - paused
        paused += end - start
    return time - paused


def source_world_position(time: float, ch: int, data: dict | None = None) -> tuple[float,float,float]:
    """Reference-mix source position in physical metres, zero-based channel.

    Mirrors OWFullSignal.SourceWorldPosition and the inherited V4 first half.
    Listener reference is (0,1.35,-2.8); no worldFrame offset belongs in a mix.
    """
    if ch not in range(4): raise ValueError("ch must be zero-based 0..3")
    time = min(256.0, max(0.0, time))
    m = motion_clock(time,data); phase = ch * 1.57079633
    config = data.get("source_motion", {}) if data else {}
    fused = config.get("fused_center", [0,4.25,8.2])
    orbit = config.get("orbit_center", [0,1.65,-2.8])
    radius = config.get("orbit_radius", 4.2)
    front = [fused[0]-1.8 + ch*1.2 + math.sin(m*.31+phase)*.07,
        fused[1] + (ch-1.5)*.13 + math.sin(m*.53+phase)*.11, fused[2]+(ch-1.5)*.13]
    def mix(a,b,p): return [x+(y-x)*p for x,y in zip(a,b)]
    if time < 133.12:
        opened = identity.smooth((m-79.36)/2.56)*(1-identity.smooth((m-85.12)/2.56))
        angle = phase+(m-81.92)*.46
        surround = [orbit[0]+math.sin(angle)*radius, orbit[1]+.38+math.sin(angle*1.7+phase)*.4, orbit[2]+math.cos(angle)*radius]
        return tuple(mix(front,surround,opened))
    reborn = identity.smooth((time-207.36)/7.68)
    front[1] -= .15*reborn; front[2] -= reborn
    pressure = identity.smooth((time-143.36)/2.56)*(1-identity.smooth((time-153.60)/2.56))
    if ch>=2:
        near = [-1.45 if ch==2 else 1.45, 1.75+.10*math.sin(m*.4+phase), -.70]
        front = mix(front,near,pressure)
    retreat = identity.smooth((time-186.88)/2.56)*(1-identity.smooth((time-194.56)/12.80))
    far = [(ch-1.5)*.45,1.95+(ch-1.5)*.10,16.2+(ch-1.5)*.15]
    return tuple(mix(front,far,retreat))


def source_spatial_blend(time: float, ch: int, data: dict | None = None) -> float:
    """Same AudioSource spatialBlend as the runtime, for offline approximation."""
    if time < 133.12:
        m=motion_clock(time,data)
        opened=identity.smooth((m-79.36)/2.56)*(1-identity.smooth((m-85.12)/2.56))
        return .55+.45*opened
    pressure=identity.smooth((time-143.36)/2.56)*(1-identity.smooth((time-153.60)/2.56)) if ch>=2 else 0
    retreat=identity.smooth((time-186.88)/2.56)*(1-identity.smooth((time-194.56)/12.80))
    return .55+.45*max(pressure,retreat)


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n")


def bake_waves(audio, output, metadata):
    original = waves.read_pcm
    sources = {}

    def padded(path):
        pcm, rate = original(path)
        if rate != 48000 or len(pcm) != 256 * rate:
            raise ValueError(f"Expected the unchanged 256 s / 48 kHz stems: {path}")
        sources[str(path)] = len(pcm)
        return np.pad(pcm, (0, int(.12 * rate))), rate

    waves.read_pcm = padded
    try:
        result = waves.build(argparse.Namespace(audio=audio, output=output,
            metadata=metadata, duration=256.0, fps=30.0, columns=512))
    finally:
        waves.read_pcm = original
    data = json.loads(metadata.read_text())
    data["schema"] = "oldwebmap.waveforms.fullfilm.1"
    data["baker"] = "prepare_episode123_v3_waveforms.build (unchanged)"
    data["eof"] = "120 ms of zero post-roll is used only by the original baker's forward probe; original source files and their hashes are unchanged"
    for voice in data["voices"]:
        voice["samples"] = sources[voice["source"]]
        voice["source_duration"] = 256.0
    write_json(metadata, data)
    write_json(output / "metadata.json", data)
    return result, data


def bake_greetz(output):
    output.mkdir(parents=True, exist_ok=True)
    overload = [("写 README 的人", 0), ("转载", 1), ("把对白译出来的人", 0),
        ("搬运", 1), ("保种的人", 0), ("来源：网络", 1), ("打时间轴的人", 0),
        ("转载", 1), ("搭世界的人", 0), ("来源：网络", 1), ("还在维护的人", 0), ("转载", 1)]
    renewal = [("尝试新工具的人", 0), ("帮忙校译的人", 0), ("写 README 的人", 0),
        ("分享源文件的人", 0), ("保种的人", 0), ("复现并修正的人", 0),
        ("搭世界的人", 0), ("回答新人问题的人", 0), ("受助后再帮助的人", 0),
        ("一起玩、一起改的人", 0), ("那就一起做下去。", 0)]
    tokens = [dict(text=text, style=style, segment="overload") for text, style in overload]
    tokens += [dict(text="_", style=0, segment="cursor")]
    tokens += [dict(text=text, style=style, segment="renewal") for text, style in renewal]
    face = identity.Face(identity.EM)
    glyphs, end = identity.layout_tokens(face, tokens, 56)
    width = int(math.ceil(end + 56))
    cover, ids, style, _ = identity.render_strip(face, glyphs, width, identity.BAND,
        identity.BASE, interrupt_spans=[(t["start"], t["end"]) for t in tokens if t["style"]])
    # B stores a complete role token id: each labour role falls as one name.
    ids[:] = 0
    for i, token in enumerate(tokens):
        ids[:, int(token["start"]):math.ceil(token["end"])] = i + 1
    rows = math.ceil(width / identity.STRIDE)
    height = 2 ** math.ceil(math.log2(rows * identity.BAND))
    atlas = identity.cut_rows((cover, identity.outline_plane(cover), ids, style), width,
        rows, identity.BAND, identity.STRIDE, identity.PAD, identity.AW, height)
    path = output / "fullfilm-greetz-atlas.png"
    Image.fromarray(atlas, "RGBA").save(path, optimize=True)
    cursor = next(t for t in tokens if t["segment"] == "cursor")
    late = [t for t in tokens if t["segment"] == "renewal"]
    first = tokens[0]
    last = tokens[len(overload) - 1]
    return dict(atlas=dict(file=path.name, width=identity.AW, height=height,
        rows=rows, row_px=identity.BAND, row_stride=identity.STRIDE,
        row_pad=identity.PAD, text_length_px=width, baseline_px=identity.BASE,
        grad_top_px=identity.GRAD_TOP, grad_bottom_px=identity.GRAD_BOT,
        sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
        channels="R cover, G outline, B role token id, A interrupt style"),
        tokens=tokens,
        overload_first=(first["start"] + first["end"]) / 2,
        overload_last=(last["start"] + last["end"]) / 2,
        overload_min=first["start"] - 12, overload_max=last["end"] + 12,
        cursor_px=(cursor["start"] + cursor["end"]) / 2,
        cursor_min=cursor["start"] - 12, cursor_max=cursor["end"] + 12,
        renewal_first=(late[0]["start"] + late[0]["end"]) / 2,
        renewal_last=(late[-1]["start"] + late[-1]["end"]) / 2,
        renewal_min=late[0]["start"] - 12, renewal_max=late[-1]["end"] + 12)


def make_data(greetz):
    base = json.loads((ROOT / "data/episode123-v5-atmosphere.json").read_text())
    late = [
        (133.12, [.06,.06,.07], [.45,.43,.41], [.90,.94,1], .78, [.58,.64,.72], .26),
        (143.36, [.08,.04,.04], [.52,.22,.18], [1,.54,.42], .78, [.45,.38,.50], .25),
        (153.60, [.05,.08,.09], [.12,.18,.20], [.65,.82,.88], .60, [.48,.62,.68], .21),
        (168.96, [.04,.04,.045], [.09,.09,.10], [.72,.75,.80], .36, [.50,.54,.60], .13),
        (184.32, [.004,.005,.007], [.012,.014,.016], [.40,.47,.60], .025, [.30,.36,.44], .012),
        (197.12, [.015,.025,.06], [.10,.09,.15], [.66,.72,.94], .28, [.40,.48,.72], .13),
        (215.04, [.03,.06,.14], [.32,.20,.20], [1,.80,.65], .68, [.48,.64,.92], .25),
        (230.40, [.08,.14,.30], [.48,.32,.23], [1,.88,.72], .88, [.55,.70,1], .30),
        (245.76, [.10,.16,.28], [.42,.32,.26], [1,.87,.72], .76, [.61,.73,.94], .26),
    ]
    sky = base["sky"]["keys"] + [dict(t=t, top=top, horizon=hor) for t,top,hor,*_ in late]
    key = base["lights"]["key"]["keys"] + [dict(t=x[0], color=x[3], intensity=x[4]) for x in late]
    fill = base["lights"]["fill"]["keys"] + [dict(t=x[0], color=x[5], intensity=x[6]) for x in late]
    return dict(schema="oldwebmap.fullfilm.signal.1", duration=256, fps=30,
        waveform_metadata="fullfilm-v1-waveforms.json", waveform_file="waveform_rgba.png",
        source_rules="PCM/RMS drive waves, meters and actual activity; note-on only drives tracker flashes. No silent block is inferred from empty tracker rows.",
        journeys=[dict(start=a,end=b,distance=d) for a,b,d in JOURNEYS],
        source_motion=dict(reference_eye=[0,1.35,-2.8], fused_center=[0,4.25,8.2],
            orbit_center=[0,1.65,-2.8], orbit_radius=4.2,
            selected_pressure_channels=[3,4], pressure=[143.36,145.92,153.60,156.16],
            retreat=[186.88,189.44,194.56,207.36], recovery_nudge=[207.36,215.04],
            implementation="tools.prepare_fullfilm_signal.source_world_position (zero-based ch); matches OWFullSignal.SourceWorldPosition"),
        sky=sky, key=key, fill=fill,
        grid=[dict(t=t, opacity=v) for t,v in base["grid"]["keys"][:-1]] + [dict(t=t, opacity=v) for t,v in
            [(133.12,1),(168.96,.65),(184.32,.08),(186.88,0),(197.12,0),(207.36,.28),(225.28,.65),(245.76,.45),(254.72,.45),(256,.25)]],
        greetz=greetz)


def validate(audio, data, wave_data, output):
    spans = [(163.84,166.40,[1,2,3]), (189.44,194.56,[2,3,4]),
        (245.76,250.88,[1,2,3,4]), (250.88,253.44,[1,2,4])]
    checks=[]
    for start,end,expected in spans:
        active=[]; db=[]
        for ch in range(4):
            pcm,rate=waves.read_pcm(audio/f"FC14_Mono_{ch+1:02d}.wav")
            block=pcm[round((start+.08)*rate):round((end-.08)*rate)]
            block=block[:len(block)//3840*3840].reshape(-1,3840).astype(float)
            rms=float(np.sqrt(np.mean((block-block.mean(axis=1,keepdims=True))**2)))
            db.append(round(20*math.log10(max(rms,1e-12)),2))
            if rms>10**(-70/20): active.append(ch+1)
        checks.append(dict(start=start,end=end,expected=expected,actual=active,dbfs=db,ok=active==expected))
    assert all(c["ok"] for c in checks), checks
    assert wave_data["frames"]==7681 and wave_data["atlas_height"]==8192
    assert len(wave_data["energy"])==4 and all(len(e)==7681 for e in wave_data["energy"])
    assert all(a["t"]<b["t"] for a,b in zip(data["sky"],data["sky"][1:]))
    assert all(a["t"]<b["t"] for a,b in zip(data["grid"],data["grid"][1:]))
    freezes=[]
    for start,end,_ in JOURNEYS:
        error=max(math.dist(source_world_position(start,ch,data),source_world_position(float(t),ch,data))
            for ch in range(4) for t in np.linspace(start,end,65))
        freezes.append(dict(start=start,end=end,max_position_error=error))
    assert all(x["max_position_error"]<1e-10 for x in freezes)
    old_atlas=Path.home()/"tmp/vrc-old-web-map/episode123-v3/wave-fx/waveform_rgba.png"
    prefix_equal=None
    if old_atlas.exists():
        old=np.asarray(Image.open(old_atlas)); new=np.asarray(Image.open(output/"waveform_rgba.png"))
        old_frames=json.loads((ROOT/"data/episode123-v3-waveforms.json").read_text())["frames"]
        prefix_equal=bool(np.array_equal(old[:old_frames],new[:old_frames])); assert prefix_equal
    result=dict(success=True,provenance="offline Python PCM and motion checks; no Unity or listening validation", checks=checks,
        frozen_source_windows=freezes, original_waveform_prefix_identical=prefix_equal,
        frames=7681, atlas=[512,8192], old_metadata_untouched=True)
    write_json(output/"offline-pcm-checks.json",result)
    return result


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument("--audio",type=Path,default=Path.home()/"tmp/vrc-old-web-map/episode123/audio")
    p.add_argument("--output",type=Path,default=TMP)
    p.add_argument("--wave-metadata",type=Path,default=ROOT/"data/fullfilm-v1-waveforms.json")
    p.add_argument("--metadata",type=Path,default=ROOT/"data/fullfilm-v1-signal.json")
    args=p.parse_args()
    before={path:hashlib.sha256(path.read_bytes()).hexdigest() for path in
        [ROOT/"data/episode123-v3-waveforms.json",ROOT/"data/episode123-v4-waveforms.json",ROOT/"data/episode123-v5-atmosphere.json",ROOT/"data/episode123-v5-greetz.json"]}
    result,wave_data=bake_waves(args.audio,args.output,args.wave_metadata)
    data=make_data(bake_greetz(args.output)); write_json(args.metadata,data)
    checks=validate(args.audio,data,wave_data,args.output)
    assert all(hashlib.sha256(path.read_bytes()).hexdigest()==value for path,value in before.items())
    print(json.dumps(dict(waveforms=result,signal=str(args.metadata),offline_checks=checks),ensure_ascii=False,indent=2))


if __name__=="__main__": main()
