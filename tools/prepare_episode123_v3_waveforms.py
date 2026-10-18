#!/usr/bin/env python3
"""Generate phase-triggered four-channel oscilloscope data from actual mono stems.

No Unity serialized assets are written. Copy the PNG output to Revision3/Data and
metadata to AgentScripts/data; the Pipeline builder imports and persists assets.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import math
import wave
from pathlib import Path
import numpy as np
from PIL import Image


def read_pcm(path: Path) -> tuple[np.ndarray, int]:
    with wave.open(str(path)) as audio:
        if audio.getnchannels() != 1 or audio.getsampwidth() != 2:
            raise ValueError(f"Expected mono signed 16-bit PCM: {path}")
        rate = audio.getframerate()
        return np.frombuffer(audio.readframes(audio.getnframes()), dtype='<i2').astype(np.float32) / 32768.0, rate


def build(args: argparse.Namespace) -> dict:
    args.output.mkdir(parents=True, exist_ok=True)
    frames = math.ceil(args.duration * args.fps) + 1
    height = 2 ** math.ceil(math.log2(frames))
    width = args.columns
    # Neutral 128 represents exact silence; 127 quantization steps in each direction.
    atlas = np.full((height, width, 4), 128, dtype=np.uint8)
    voices = []
    energies = []
    trigger_summary = []
    for channel in range(4):
        source = args.audio / f"FC14_Mono_{channel + 1:02d}.wav"
        pcm, rate = read_pcm(source)
        if rate != 48000 or len(pcm) < int((args.duration + .1) * rate):
            raise ValueError(f"Unexpected sample rate or source too short: {source}")
        window = width * 6  # 64 ms at 48 kHz, preserves bass structure.
        sample_offsets = np.arange(window, dtype=np.int64)
        energy = np.zeros(frames, dtype=np.float32)
        shifts = []
        triggered = 0
        for frame in range(frames):
            start = min(int(round(frame / args.fps * rate)), len(pcm)-window-1600)
            probe = pcm[start:start+1600]
            probe = probe - probe.mean()  # AC coupling: constant DC is inaudible.
            peak = float(np.max(np.abs(probe)))
            # Schmitt-qualified positive zero crossing. Reject tiny/noisy crossings;
            # choose the first one, always looking forward at most 33 ms.
            crossings = np.flatnonzero((probe[:-1] <= 0) & (probe[1:] > 0))
            shift = 0
            if peak > .003 and crossings.size:
                for crossing in crossings:
                    crossing = int(crossing)
                    if np.max(probe[crossing:crossing+96]) >= peak * .18:
                        shift = crossing
                        triggered += 1
                        break
            raw = pcm[start + shift + sample_offsets]
            # Exact contiguous box integration (six original samples per display
            # column), not invented sine waves, FFT bars or normalized fake shapes.
            displayed = raw.reshape(width, 6).mean(axis=1)
            displayed -= displayed.mean()
            atlas[frame, :, channel] = np.clip(np.rint(displayed * 127) + 128, 1, 255).astype(np.uint8)
            energy_block = pcm[start:start+1920].astype(np.float64)
            energy[frame] = np.sqrt(np.mean((energy_block-energy_block.mean()) ** 2))
            shifts.append(shift / rate)
        energies.append(energy)
        voices.append({"channel": channel, "source": str(source), "source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(), "sample_rate": rate, "samples": len(pcm), "peak": round(float(np.max(np.abs(pcm))), 7)})
        trigger_summary.append({"channel": channel, "qualified_frames": triggered, "max_shift_seconds": round(max(shifts), 7), "mean_shift_seconds": round(float(np.mean(shifts)), 7)})
    # One common RMS reference preserves the actual relative stem energy.
    common_reference = float(np.percentile(np.concatenate(energies), 98))
    levels = [[round(float(min(1.25, value / max(.001, common_reference))), 5) for value in row] for row in energies]
    png = args.output / 'waveform_rgba.png'
    Image.fromarray(atlas, mode='RGBA').save(png, optimize=True)
    metadata = {
        "schema": "oldwebmap.waveforms.v3.1", "source": "FC14 four independently rendered real mono stems",
        "duration": args.duration, "fps": args.fps, "frames": frames, "columns": width, "atlas_height": height,
        "atlas": "waveform_rgba.png", "atlas_sha256": hashlib.sha256(png.read_bytes()).hexdigest(),
        "channel_packing": "R=CH01 G=CH02 B=CH03 A=CH04; all alpha values are data",
        "decode": "(sample * 255 - 128) / 127; AC-coupled, no per-frame amplitude normalization",
        "coupling": "AC: subtract mean of the actual 64 ms display window; RMS subtracts mean of its actual 40 ms block; this removes silent-stem DC introduced by whole-track DC correction",
        "row_uv": "1 - (floor(time*fps)+0.5)/atlas_height; PNG row 0 is top",
        "window_seconds": width*6/48000, "integration_samples_per_column": 6,
        "trigger": "first qualified rising zero crossing within 1600 original samples; positive excursion >=18% local peak within next 96 samples; silent frames remain actual zero",
        "energy_reference_rms": round(common_reference, 8), "energy_step": 1/args.fps, "energy": levels,
        "voices": voices, "trigger_summary": trigger_summary,
        "import": {"sRGB": False, "alphaSource": "FromInput", "alphaIsTransparency": False, "compression": "Uncompressed", "mipmaps": False, "filter": "Point", "wrap": "Clamp", "maxTextureSize": height},
    }
    args.metadata.parent.mkdir(parents=True, exist_ok=True)
    args.metadata.write_text(json.dumps(metadata, ensure_ascii=False, indent=2)+'\n')
    (args.output/'metadata.json').write_text(json.dumps(metadata, ensure_ascii=False, indent=2)+'\n')
    # Contact sheet: only diagnostic, never imported into the scene.
    from PIL import ImageDraw
    preview = Image.new('RGB', (1280, 720), '#0a101e')
    draw = ImageDraw.Draw(preview)
    colors = ['#53d2e2', '#edbd67', '#bf90ef', '#86cf83']
    times = [6.4, 43.52, 84.48, 115.2]
    for row, time in enumerate(times):
        frame = min(frames-1, round(time*args.fps))
        for ch in range(4):
            x0 = ch*320+16
            y0 = row*180+92
            data = (atlas[frame, :, ch].astype(float)-128)/127
            pts = [(x0+i*288/(width-1), y0-value*65) for i,value in enumerate(data)]
            draw.line(pts, fill=colors[ch], width=2)
            draw.text((x0,row*180+12),f'{time:.2f}s CH0{ch+1}',fill=colors[ch])
    preview.save(args.output/'waveform-contact.png')
    return {"frames": frames, "atlas_size": [width,height], "atlas_bytes": png.stat().st_size, "metadata": str(args.metadata), "output": str(args.output), "common_reference_rms": common_reference}

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--audio', type=Path, default=Path.home()/'tmp/vrc-old-web-map/episode123/audio')
    parser.add_argument('--output', type=Path, default=Path.home()/'tmp/vrc-old-web-map/episode123-v3/wave-fx')
    parser.add_argument('--metadata', type=Path, default=Path('data/episode123-v3-waveforms.json'))
    parser.add_argument('--duration', type=float, default=133.12)
    parser.add_argument('--fps', type=float, default=30)
    parser.add_argument('--columns', type=int, default=512)
    print(json.dumps(build(parser.parse_args()),ensure_ascii=False,indent=2))
