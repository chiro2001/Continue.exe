#!/usr/bin/env python3
"""V5 fixed-listener stereo reference: V4 music model (V5 does not move music) plus the V5 Foley layer.

Not a VRChat/HRTF capture. Foley is placed with distance attenuation and equal-power pan from the emitter
positions in data/episode123-v5-foley.json (or the editor Validate report when present).
"""

import argparse
import importlib.util
import json
import sys
from pathlib import Path

import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parent.parent
TMP = Path.home() / "tmp/vrc-old-web-map"
DEST = TMP / "episode123-v5/review"


def load_v4():
    spec = importlib.util.spec_from_file_location("render_episode123_v4_audio", ROOT / "tools/render_episode123_v4_audio.py")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


V4 = load_v4()
RATE = V4.SAMPLE_RATE
SHOW = V4.SHOW_SECONDS
REVIEW = V4.REVIEW_SECONDS
LISTENER = V4.LISTENER
MARGIN_REQUIRED_DB = 14.0


def db(value):
    return float(20 * np.log10(max(float(value), 1e-12)))


def virtual_z(time):
    """OWV4Journey.VirtualPosition().z: worldFrame sits at -virtual_z."""
    ease = V4.ease
    return 24 * ease((time - 79.36) / 2.56) + 28 * ease((time - 86.40) / 1.92) + 28 * ease((time - 102.40) / 2.56)


def emitter_position(emitter, time, override):
    """Physical position at the onset (OWV4Foley samples emitters[i].position when the event fires)."""
    if emitter["id"] in override:
        return np.asarray(override[emitter["id"]], dtype=np.float64)
    if emitter["kind"] == "frame":
        local = np.asarray(emitter["frame_local"], dtype=np.float64)
        return local - np.array([0.0, 0.0, float(virtual_z(time))])
    return np.asarray(emitter["physical"], dtype=np.float64)


def spatialise(position, reference):
    relative = position - LISTENER
    distance = float(np.linalg.norm(relative))
    pan = float(np.sin(np.arctan2(relative[0], relative[2])))
    angle = (pan + 1.0) * np.pi / 4.0
    attenuation = min(1.0, reference / max(distance, 1e-6))
    return np.array([np.cos(angle), np.sin(angle)]) * attenuation, pan, distance, attenuation


def music_mix(stems_dir, config, silent):
    count = round(SHOW * RATE)
    total = round(REVIEW * RATE)
    mix = np.zeros((total, 2), dtype=np.float32)
    if silent:
        return mix, "silent placeholder"
    for channel in range(4):
        path = stems_dir / f"FC14_Mono_{channel + 1:02}.wav"
        with sf.SoundFile(path) as stem:
            if stem.samplerate != RATE or stem.channels != 1 or len(stem) < count:
                raise ValueError(f"Stem must hold at least {count} mono samples at {RATE} Hz: {path}")
            for start in range(0, count, V4.BLOCK_SIZE):
                length = min(V4.BLOCK_SIZE, count - start)
                wave = stem.read(length, dtype="float32")
                time = np.arange(start, start + length, dtype=np.float64) / RATE
                positions, opening = V4.source_positions(time, channel, config)
                gains, _ = V4.stereo_gains(positions, opening)
                fade = 1.0 - V4.ease((time - SHOW + 0.3) / 0.3)
                mix[start:start + length] += wave[:, None] * gains * (V4.MUSIC_GAIN * fade[:, None])
    return mix, str(stems_dir)


def stats(bus, active_mask=None):
    peak = float(np.abs(bus).max()) if bus.size else 0.0
    rms = float(np.sqrt(np.mean(bus.astype(np.float64) ** 2))) if bus.size else 0.0
    row = {"peak": peak, "peak_dbfs": db(peak), "rms_full_dbfs": db(rms)}
    if active_mask is not None and active_mask.any():
        row["rms_active_dbfs"] = db(float(np.sqrt(np.mean(bus[active_mask].astype(np.float64) ** 2))))
    return row


def render(args):
    data = json.loads(args.foley_data.read_text())
    config, music_model = None, "builder-derived (unverified)"
    try:
        config = V4.configuration(args.orbit_radius, args.scene_report, require_saved_scene=True)
        music_model = "V4 stable-sound saved-scene verification"
    except ValueError as error:
        if args.strict:
            raise
        print(f"WARNING: saved-scene check failed ({error}); using the builder/runtime music model", file=sys.stderr)
        config = V4.configuration(args.orbit_radius, None, require_saved_scene=False)
    count = round(SHOW * RATE)
    total = round(REVIEW * RATE)
    music, music_source = music_mix(args.stems_dir, config, args.silent_music)

    override = {}
    if args.emitter_report and args.emitter_report.exists():
        report = json.loads(args.emitter_report.read_text())
        override = {k: v for k, v in report.get("emitterPhysical", {}).items() if isinstance(v, list) and len(v) == 3 and k == "keyboard"}
    clips, volumes, groups = [], [], []
    for clip in data["clips"]:
        wave, rate = sf.read(args.foley_dir / clip["file"], dtype="float32")
        if rate != RATE or wave.ndim != 1 or not np.isfinite(wave).all():
            raise ValueError(f"Expected finite {RATE} Hz mono PCM: {clip['file']}")
        clips.append(wave)
        volumes.append(float(clip["volume"]))
        groups.append(clip["group"])
    group_names = sorted(set(groups))
    buses = {g: np.zeros((total, 2), dtype=np.float32) for g in group_names}
    active = np.zeros(total, dtype=bool)
    rows = []
    for event in data["events"]:
        clip_index, emitter_index = event["clip"], event["emitter"]
        time = float(event["time"])
        if not 0 <= clip_index < len(clips) or not 0 <= emitter_index < len(data["emitters"]) or not 0 <= time < SHOW:
            raise ValueError(f"Invalid V5 Foley event: {event}")
        emitter = data["emitters"][emitter_index]
        position = emitter_position(emitter, time, override)
        gains, pan, distance, attenuation = spatialise(position, args.reference_distance)
        start = round(time * RATE)
        wave = clips[clip_index][: min(len(clips[clip_index]), count - start)]
        scaled = wave[:, None] * gains[None, :].astype(np.float32) * np.float32(volumes[clip_index])
        buses[groups[clip_index]][start:start + len(wave)] += scaled
        active[start:start + len(wave)] = True
        rows.append({"time": time, "clip": data["clips"][clip_index]["id"], "group": groups[clip_index], "emitter": emitter["id"],
                     "position": [round(float(v), 3) for v in position], "distance": round(distance, 2), "pan": round(pan, 3),
                     "attenuation": round(attenuation, 3), "peak": float(np.abs(scaled).max()), "length": len(wave) / RATE})
    foley = sum(buses.values())
    mix = music + foley
    peak = float(np.abs(mix).max())
    if not np.isfinite(peak) or peak >= 1.0:
        raise ValueError(f"Reference would clip; no automatic normalisation was applied (peak={peak})")

    args.output_dir.mkdir(parents=True, exist_ok=True)
    audio_path = args.output_dir / "review-audio.wav"
    sf.write(audio_path, mix, RATE, subtype="PCM_16")
    sf.write(args.output_dir / "review-foley-only.wav", foley, RATE, subtype="PCM_16")

    music_stats = stats(music[:count])
    music_peak = music_stats["peak"]
    # Local music peak per 10 ms block, for the per-event margins.
    block = RATE // 100
    blocks = np.abs(music[: (total // block) * block]).max(axis=1).reshape(-1, block).max(axis=1)
    worst = []
    for row in rows:
        b0 = int(row["time"] * 100)
        b1 = b0 + max(1, int(min(row["length"], 0.6) * 100))
        local = float(blocks[b0:b1].max()) if b1 > b0 else 0.0
        row["music_local_peak_dbfs"] = db(local)
        row["peak_dbfs"] = db(row["peak"])
        row["margin_to_local_music_db"] = row["music_local_peak_dbfs"] - row["peak_dbfs"] if local > 0 else None
        row["margin_to_music_peak_db"] = db(music_peak) - row["peak_dbfs"] if music_peak > 0 else None
        if row["margin_to_local_music_db"] is not None:
            worst.append(row)
    worst.sort(key=lambda r: r["margin_to_local_music_db"])
    per_group = {}
    for g, bus in buses.items():
        info = stats(bus, active)
        info["events"] = sum(1 for r in rows if r["group"] == g)
        info["margin_to_music_peak_db"] = (music_stats["peak_dbfs"] - info["peak_dbfs"]) if music_peak > 0 else None
        per_group[g] = info
    foley_stats = stats(foley, active)
    margin = (music_stats["peak_dbfs"] - foley_stats["peak_dbfs"]) if music_peak > 0 else None
    min_local = worst[0]["margin_to_local_music_db"] if worst else None
    report = {
        "revision": 5,
        "kind": "Fixed-listener equal-power stereo reference; V4 music trajectories plus V5 Foley at emitter positions. Not headset HRTF or a captured VRChat stream.",
        "sample_rate": RATE, "channels": 2, "subtype": "PCM_16", "show_seconds": SHOW, "review_seconds": REVIEW,
        "listener_position": LISTENER.tolist(), "music_source": music_source, "music_model": music_model,
        "orbit_radius": config["orbit_radius"], "orbit_radius_source": config["orbit_radius_source"],
        "saved_scene_max_trajectory_error": config.get("saved_scene_max_trajectory_error"),
        "foley_data": str(args.foley_data.relative_to(ROOT)) if args.foley_data.is_relative_to(ROOT) else str(args.foley_data),
        "foley_events": len(rows), "reference_distance": args.reference_distance, "emitter_override": override,
        "mix_peak": peak, "mix_peak_dbfs": db(peak),
        "music": music_stats, "foley": foley_stats,
        "foley_peak_below_music_peak_db": margin, "required_margin_db": MARGIN_REQUIRED_DB,
        "margin_ok": None if margin is None else bool(margin >= MARGIN_REQUIRED_DB),
        "min_event_margin_to_local_music_peak_db": min_local,
        "groups": per_group,
        "worst_events": [{k: r[k] for k in ("time", "clip", "emitter", "distance", "peak_dbfs", "music_local_peak_dbfs", "margin_to_local_music_db")} for r in worst[:8]],
        "assumptions": [
            "Music uses V4's unchanged trajectory model, gains and 0.3 s end fade (V5 does not move music).",
            "Foley: spatialBlend 1, equal-power pan = sin(atan2(dx,dz)), attenuation = min(1, reference_distance/distance); reference 6 m reproduces V4's reference balance (content 0.73 at 8.4 m).",
            "Emitters inside worldFrame (campfire, gates, archive) sit at frame_local minus the virtual offset at the onset time; sources keep that point while a one-shot plays.",
            "Clip level at the source = clip file x AudioSource.volume (peak = source_peak in the data file); no VRC spatial gain/rolloff curve is simulated.",
        ],
        "events": rows,
    }
    (args.output_dir / "foley-audio-reference.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    print(f"Stereo reference: {audio_path}  ({REVIEW}s, {total} samples)  mix peak {db(peak):.2f} dBFS")
    print(f"Music  peak {music_stats['peak_dbfs']:.2f}  rms {music_stats['rms_full_dbfs']:.2f} dBFS  ({music_model}; {music_source})")
    print(f"Foley  peak {foley_stats['peak_dbfs']:.2f}  rms(full) {foley_stats['rms_full_dbfs']:.2f}  rms(active) {foley_stats.get('rms_active_dbfs', float('nan')):.2f} dBFS; "
          f"{len(rows)} events; peak margin to music {margin if margin is None else round(margin, 1)} dB (need >= {MARGIN_REQUIRED_DB:g})")
    for g in group_names:
        info = per_group[g]
        print(f"  {g:<8} n={info['events']:>3} peak {info['peak_dbfs']:7.2f}  rms_full {info['rms_full_dbfs']:7.2f}  rms_active {info.get('rms_active_dbfs', float('nan')):7.2f} dBFS")
    if worst:
        print("  closest to the music (event peak vs local music peak): " + ", ".join(f"{r['time']:.2f}s {r['clip']} {r['margin_to_local_music_db']:.1f} dB" for r in worst[:4]))
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--stems-dir", type=Path, default=TMP / "episode123/audio", help="folder with FC14_Mono_01..04.wav (48 kHz mono)")
    parser.add_argument("--silent-music", action="store_true", help="test the Foley path with a silent music placeholder")
    parser.add_argument("--foley-data", type=Path, default=ROOT / "data/episode123-v5-foley.json")
    parser.add_argument("--foley-dir", type=Path, default=ROOT / "art/export/episode123-v5/foley")
    parser.add_argument("--output-dir", type=Path, default=DEST)
    parser.add_argument("--scene-report", type=Path, default=V4.SAVED_SCENE_REPORT, help="V4 saved-scene stable-sound report (V5 keeps the music unchanged)")
    parser.add_argument("--orbit-radius", type=float)
    parser.add_argument("--strict", action="store_true", help="fail instead of falling back when the V4 saved-scene check does not pass")
    parser.add_argument("--emitter-report", type=Path, default=TMP / "episode123-v5/reports/foley-validate.json", help="editor Validate report: actual keyboard/content emitter positions")
    parser.add_argument("--reference-distance", type=float, default=6.0, help="distance (m) up to which the reference mix applies no attenuation")
    report = render(parser.parse_args())
    if report["margin_ok"] is False:
        print(f"FAIL: Foley peaks are only {report['foley_peak_below_music_peak_db']:.1f} dB below the music (need >= {MARGIN_REQUIRED_DB:g})", file=sys.stderr)
        sys.exit(2)


if __name__ == "__main__":
    main()
