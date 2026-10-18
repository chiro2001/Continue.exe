#!/usr/bin/env python3
"""Render unchanged FC14 PCM as a modest stereo reference, without playback.

This reference intentionally does not pretend to reproduce Unity's spatial audio,
HRTF, Foley, distance attenuation, or the V3 dry-layer blend. Those are checked in
the actual Udon performance. A browser movie only conveys timing and composition.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import wave

import numpy as np
import soundfile as sf

RATE, SHOW, REVIEW = 48000, 253.44, 255.44
GAIN, FADE = .8, .16
DEST = Path.home()/"tmp/vrc-old-web-map/fullfilm-v4/review"
STEMS = Path.home()/"tmp/vrc-old-web-map/episode123/audio"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def validate_reference(path):
    report = json.loads((path.parent/"audio-reference.json").read_text())
    with wave.open(str(path), "rb") as stream:
        if (stream.getframerate(), stream.getnchannels(), stream.getnframes(), stream.getsampwidth()) != (RATE, 2, round(RATE*REVIEW), 2):
            raise ValueError("Incomplete stereo reference")
        stream.setpos(round(RATE*SHOW))
        if any(stream.readframes(RATE*2)):
            raise ValueError("Afternath must contain zero PCM")
    digest = sha(path)
    if report.get("success") is not True or report.get("output_sha256") != digest or report.get("spatial_simulation") is not False:
        raise ValueError("Reference metadata or checksum mismatch")
    if len(report.get("sources", [])) != 4:
        raise ValueError("Four unchanged source stems required")
    for row in report["sources"] + report["inputs"]:
        source = Path(row["path"])
        if not source.is_file() or sha(source) != row["sha256"]:
            raise ValueError("Reference input changed: " + str(source))
    return digest


def main():
    DEST.mkdir(parents=True, exist_ok=True)
    count, total = round(RATE*SHOW), round(RATE*REVIEW)
    mix = np.zeros((total, 2), dtype=np.float32)
    sources = []
    for ch, pan in enumerate((-.15, -.05, .05, .15)):
        path = STEMS/f"FC14_Mono_{ch+1:02d}.wav"
        pcm, rate = sf.read(path, dtype="float32")
        if rate != RATE or pcm.ndim != 1 or len(pcm) != RATE*256 or not np.isfinite(pcm).all():
            raise ValueError("Expected unchanged complete mono source: " + str(path))
        angle = (pan+1)*np.pi/4
        mix[:count] += pcm[:count, None] * np.array([np.cos(angle), np.sin(angle)], dtype=np.float32) * GAIN
        sources.append(dict(channel=ch+1, path=str(path), sha256=sha(path), samples=len(pcm), playback_samples=count, sample_rate=rate, pan=pan))
    fade_count = round(FADE*RATE)
    phase = np.arange(fade_count, dtype=np.float32)/fade_count
    mix[count-fade_count:count] *= (1-phase*phase*(3-2*phase))[:, None]
    peak = float(np.abs(mix).max())
    if not 0 < peak < 1:
        raise ValueError("Reference clips; no automatic normalization allowed")
    path = DEST/"review-audio.wav"
    sf.write(path, mix, RATE, subtype="PCM_16")
    report = dict(success=True, kind="Original four-stem stereo timing reference, last 2.56s playback omitted; not captured Unity/VRChat audio", spatial_simulation=False,
        audio_played=False, source_music_muted=False, foley_included=False, output=str(path), output_sha256=sha(path),
        show_seconds=SHOW, review_seconds=REVIEW, sample_rate=RATE, channels=2, samples=total, subtype="PCM_16",
        final_zero_pcm_seconds=2, music_gain=GAIN, common_gain=True, per_stem_normalization=False, end_fade_seconds=FADE,
        music=dict(peak=peak, peak_dbfs=float(20*np.log10(peak))), sources=sources,
        inputs=[dict(path=str(Path(__file__).resolve()), sha256=sha(Path(__file__).resolve()))])
    (DEST/"audio-reference.json").write_text(json.dumps(report, ensure_ascii=False, indent=2)+"\n")
    validate_reference(path)
    print(json.dumps({k: report[k] for k in ("success", "output", "show_seconds", "review_seconds", "music", "audio_played")}, ensure_ascii=False))


if __name__ == "__main__":
    main()
