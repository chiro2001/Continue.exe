#!/usr/bin/env python3
"""Render V4's fixed-listener stereo review; this is not a VRChat/HRTF capture."""

import argparse
import hashlib
import json
import re
from pathlib import Path

import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parent.parent
TMP = Path.home() / "tmp/vrc-old-web-map"
DEST = TMP / "episode123-v4/review"
SAMPLE_RATE = 48000
SHOW_SECONDS = 133.12
REVIEW_SECONDS = 135.04
LISTENER = np.array([0.0, 1.35, -2.8])
MUSIC_GAIN = 0.8
BLOCK_SIZE = SAMPLE_RATE * 4
JOURNEY_INTERVALS = ((79.36, 81.92), (86.40, 88.32), (102.40, 104.96))
SAVED_SCENE_REPORT = TMP / "episode123-v4/reports/stable-sound-saved-scene.json"
RUNTIME_SOURCE = ROOT / "unity/Assets/OldWebMap/Scripts/OWV4Oscilloscope.cs"
FOLEY_SOURCE = ROOT / "unity/Assets/OldWebMap/Scripts/OWV4Foley.cs"


def ease(value):
    value = np.clip(value, 0.0, 1.0)
    return value * value * (3.0 - 2.0 * value)


def builder_vector(source, field):
    """Use the builder assignment, not the behaviour's older field initializer."""
    match = re.search(r"osc\." + re.escape(field) + r"\s*=\s*new Vector3\(([^)]+)\)", source)
    if not match:
        raise ValueError(f"No literal osc.{field} assignment in the V4 AudioFx builder")
    values = [float(part.strip().removesuffix("f")) for part in match[1].split(",")]
    if len(values) != 3:
        raise ValueError(f"Expected three components for {field}")
    return np.array(values)


def configuration(orbit_radius, scene_report=None, require_saved_scene=True):
    builder = ROOT / "unity/AgentScripts/OWEpisodeV4AudioFx.cs"
    source = builder.read_text()
    match = re.search(r"public float orbitRadius\s*=\s*([\d.]+)f", RUNTIME_SOURCE.read_text())
    if not match:
        raise ValueError("Cannot read the runtime orbitRadius default")
    radius = float(match[1]) if orbit_radius is None else orbit_radius
    if not np.isfinite(radius) or radius <= 0:
        raise ValueError("orbitRadius must be finite and positive")
    result = {
        "fused_center": builder_vector(source, "fusedCenter"),
        "orbit_center": builder_vector(source, "orbitCenter"),
        "orbit_radius": radius,
        "orbit_radius_source": "runtime default; builder preserves previous scene value" if orbit_radius is None else "explicit --orbit-radius override",
    }
    if require_saved_scene or scene_report is not None:
        report_path = SAVED_SCENE_REPORT if scene_report is None else Path(scene_report)
        if not report_path.exists():
            raise ValueError("Run OWEpisodeV4StableSoundValidation.SavedScene through Pipeline and retrieve "
                             f"its fresh report before rendering: {report_path}")
        scene = json.loads(report_path.read_text())
        if scene.get("success") is not True or scene.get("sceneSavedUnchanged") is not True:
            raise ValueError("Saved-scene stable-sound verification has not passed")
        for path, field in [(RUNTIME_SOURCE, "runtimeSourceSha256"), (FOLEY_SOURCE, "foleySourceSha256")]:
            if hashlib.sha256(path.read_bytes()).hexdigest() != scene.get(field):
                raise ValueError(f"Saved-scene report predates the current runtime source: {path.name}")
        if not np.allclose(scene["journeyIntervals"], JOURNEY_INTERVALS, rtol=0, atol=1e-5):
            raise ValueError("Saved-scene journey intervals differ from the reference trajectory")
        for field, saved in [("fused_center", "fusedCenter"), ("orbit_center", "orbitCenter")]:
            if not np.allclose(result[field], scene[saved], rtol=0, atol=1e-5):
                raise ValueError(f"Audio builder and saved scene disagree: {field}")
        if orbit_radius is not None and abs(radius - scene["orbitRadius"]) > 1e-5:
            raise ValueError("Requested radius differs from the reviewed saved scene")
        result["orbit_radius"] = float(scene["orbitRadius"])
        result["orbit_radius_source"] = "V4 stable-sound saved-scene verification"
        result["verified_scene_sha256"] = scene["sceneSha256"]
        result["saved_scene_report"] = str(report_path)
        result["content_foley_world_position"] = scene["contentFoleyWorldPosition"]
        if abs(scene["musicBaseVolume"] - MUSIC_GAIN) > 1e-6 or abs(scene["musicEndFadeSeconds"] - 0.3) > 1e-6:
            raise ValueError("Saved-scene music gain/end fade differ from the preserved reference balance")
        max_error = 0.0
        for sample in scene["trajectorySamples"]:
            for channel in range(4):
                predicted, opening = source_positions(sample["time"], channel, result)
                max_error = max(max_error, float(np.linalg.norm(predicted - np.asarray(sample["positions"][channel]))))
                if abs(float(opening) - sample["open"]) > 3e-5:
                    raise ValueError("Python and saved Unity scene disagree on spatial opening")
        if max_error > 0.00015:
            raise ValueError(f"Python and saved Unity source positions disagree: {max_error} m")
        result["saved_scene_max_trajectory_error"] = max_error
        persisted = TMP / "episode123-v4/reports/persistence.json"
        if persisted.exists():
            persistence = json.loads(persisted.read_text())
            if persistence.get("success") is not True or persistence.get("v4Sha256") != scene["sceneSha256"]:
                raise ValueError("Scene persistence and stable-sound reports refer to different saved scenes")
    return result


def motion_clock(time):
    """Deterministic continuous clock; all samples on a journey share one value."""
    time = np.clip(np.asarray(time, dtype=np.float64), 0.0, SHOW_SECONDS)
    result = time.copy()
    consumed = 0.0
    for start, end in JOURNEY_INTERVALS:
        result = np.where(time < start, result, np.where(time <= end, start - consumed, time - consumed - (end - start)))
        consumed += end - start
    return result


def source_positions(time, channel, config):
    """Physical world trajectory; regional scenery and worldFrame never move it."""
    time = np.asarray(time, dtype=np.float64)
    motion = motion_clock(time)
    phase = channel * 1.57079633
    opening = ease((motion - 79.36) / 2.56) * (1.0 - ease((motion - 85.12) / 2.56))
    front = np.empty(time.shape + (3,), dtype=np.float64)
    front[..., 0] = config["fused_center"][0] - 1.8 + channel * 1.2 + np.sin(motion * 0.31 + phase) * 0.07
    front[..., 1] = config["fused_center"][1] + (channel - 1.5) * 0.13 + np.sin(motion * 0.53 + phase) * 0.11
    front[..., 2] = config["fused_center"][2] + (channel - 1.5) * 0.13
    angle = phase + (motion - 81.92) * 0.46
    surround = np.empty_like(front)
    surround[..., 0] = config["orbit_center"][0] + np.sin(angle) * config["orbit_radius"]
    surround[..., 1] = config["orbit_center"][1] + 0.38 + np.sin(angle * 1.7 + phase) * 0.4
    surround[..., 2] = config["orbit_center"][2] + np.cos(angle) * config["orbit_radius"]
    return front * (1.0 - opening[..., None]) + surround * opening[..., None], opening


def validate_trajectories(config):
    """Dense independent check before mixing; no PCM is retimed or resampled."""
    rows = []
    for start, end in JOURNEY_INTERVALS:
        times = np.linspace(start, end, int(np.ceil((end - start) * 240)) + 1)
        drift = 0.0
        boundary_step = 0.0
        for channel in range(4):
            positions, _ = source_positions(times, channel, config)
            drift = max(drift, float(np.max(np.linalg.norm(positions - positions[0], axis=-1))))
            for boundary in (start, end):
                around, _ = source_positions(np.array([boundary - 1e-4, boundary, boundary + 1e-4]), channel, config)
                boundary_step = max(boundary_step, float(np.max(np.linalg.norm(around - around[1], axis=-1))))
        if drift != 0.0 or boundary_step >= 0.001:
            raise ValueError(f"Unstable source trajectory on journey {start}..{end}: drift={drift}, boundary={boundary_step}")
        rows.append({"start": start, "end": end, "samples": len(times), "max_source_drift": drift, "max_boundary_step_at_0_1ms": boundary_step})
    landmarks = []
    for time, expected in [(81.92, 0.0), (84.48, 1.0), (89.60, 1.0), (92.16, 0.0)]:
        _, opening = source_positions(time, 0, config)
        if abs(float(opening) - expected) >= 1e-10:
            raise ValueError(f"Spatial opening no longer follows the score landmark {time}")
        landmarks.append({"time": time, "opening": float(opening)})
    return {"success": True, "sample_rate": 240, "intervals": rows, "opening_landmarks": landmarks}


def stereo_gains(positions, opening):
    relative = positions - LISTENER
    pan = np.sin(np.arctan2(relative[..., 0], relative[..., 2]))
    angle = (pan + 1.0) * np.pi / 4.0
    blend = 0.55 + 0.45 * opening
    centered = (1.0 - blend) / np.sqrt(2.0)
    gains = np.stack([centered + blend * np.cos(angle), centered + blend * np.sin(angle)], axis=-1)
    return gains, pan


def read_mono(path):
    wave, rate = sf.read(path, dtype="float32")
    if rate != SAMPLE_RATE or wave.ndim != 1 or not np.isfinite(wave).all():
        raise ValueError(f"Expected finite {SAMPLE_RATE} Hz mono PCM: {path}")
    return wave


def render(stems_dir, foley_data, output_dir, orbit_radius=None, scene_report=None):
    config = configuration(orbit_radius, scene_report)
    trajectory_validation = validate_trajectories(config)
    count = round(SHOW_SECONDS * SAMPLE_RATE)
    total = round(REVIEW_SECONDS * SAMPLE_RATE)
    mix = np.zeros((total, 2), dtype=np.float32)
    pan_ranges = []
    for channel in range(4):
        path = stems_dir / f"FC14_Mono_{channel + 1:02}.wav"
        lo, hi = 1.0, -1.0
        with sf.SoundFile(path) as stem:
            if stem.samplerate != SAMPLE_RATE or stem.channels != 1 or len(stem) < count:
                raise ValueError(f"Stem must contain at least {count} mono samples at {SAMPLE_RATE} Hz: {path}")
            for start in range(0, count, BLOCK_SIZE):
                length = min(BLOCK_SIZE, count - start)
                wave = stem.read(length, dtype="float32")
                if not np.isfinite(wave).all():
                    raise ValueError(f"Non-finite samples in {path}")
                time = np.arange(start, start + length, dtype=np.float64) / SAMPLE_RATE
                positions, opening = source_positions(time, channel, config)
                gains, pan = stereo_gains(positions, opening)
                fade = 1.0 - ease((time - SHOW_SECONDS + 0.3) / 0.3)
                mix[start:start + length] += wave[:, None] * gains * (MUSIC_GAIN * fade[:, None])
                lo, hi = min(lo, float(pan.min())), max(hi, float(pan.max()))
        pan_ranges.append([lo, hi])

    foley = json.loads(foley_data.read_text())
    foley_dir = Path(foley.get("directory", TMP / "episode123/foley")).expanduser()
    clips = [read_mono(foley_dir / filename) for filename in foley["files"]]
    for event in foley["events"]:
        clip, emitter = event["clip"], event["emitter"]
        if emitter not in (0, 1) or not 0 <= clip < len(clips) or not 0 <= event["time"] < SHOW_SECONDS:
            raise ValueError(f"Invalid V4 Foley event: {event}")
        start = round(event["time"] * SAMPLE_RATE)
        wave = clips[clip][:min(len(clips[clip]), count - start)]
        # Preserve V3's review balance. Content now has one fixed physical point;
        # keyboard pan remains the explicit fixed-listener reference assumption.
        pan = -0.28 if emitter == 1 else 0.0
        angle = (pan + 1.0) * np.pi / 4.0
        volume = foley["volume"] * (0.9 if emitter == 1 else 0.73)
        mix[start:start + len(wave)] += wave[:, None] * np.array([np.cos(angle), np.sin(angle)]) * volume

    peak = float(np.abs(mix).max())
    if not np.isfinite(peak) or peak >= 1.0:
        raise ValueError(f"Reference would clip; no automatic normalization was applied (peak={peak})")
    output_dir.mkdir(parents=True, exist_ok=True)
    audio_path = output_dir / "review-audio.wav"
    sf.write(audio_path, mix, SAMPLE_RATE, subtype="PCM_16")
    key_times = np.array([0, 79.36, 80.64, 81.92, 84.48, 86.40, 87.36, 88.32, 89.60, 92.16, 102.40, 103.68, 104.96, 133.12])
    trajectory = []
    for channel in range(4):
        positions, opening = source_positions(key_times, channel, config)
        gains, pan = stereo_gains(positions, opening)
        trajectory.append({"channel": channel + 1, "pan_range": pan_ranges[channel], "samples": [
            {"time": float(time), "motion_time": float(motion), "opening": float(opened), "world_position": position.tolist(), "pan": float(p), "stereo_gains": gain.tolist()}
            for time, motion, opened, position, p, gain in zip(key_times, motion_clock(key_times), opening, positions, pan, gains)
        ]})
    report = {
        "revision": 4,
        "kind": "Equal-power stereo reference from V4 source world trajectories at one fixed listener; not headset HRTF or a captured VRChat audio stream.",
        "sample_rate": SAMPLE_RATE, "channels": 2, "subtype": "PCM_16", "samples": total,
        "show_seconds": SHOW_SECONDS, "review_seconds": REVIEW_SECONDS, "silent_tail_seconds": REVIEW_SECONDS - SHOW_SECONDS,
        "peak": peak, "peak_dbfs": float(20 * np.log10(peak)) if peak > 0 else None,
        "channel_peaks": np.max(np.abs(mix), axis=0).tolist(),
        "channel_rms": np.sqrt(np.mean(mix.astype(np.float64) ** 2, axis=0)).tolist(),
        "music_gain": MUSIC_GAIN, "end_fade_seconds": 0.3, "normal_spatial_blend": 0.55, "climax_spatial_blend": 1.0,
        "listener_position": LISTENER.tolist(), "listener_forward": [0, 0, 1],
        "fused_center": config["fused_center"].tolist(), "orbit_center": config["orbit_center"].tolist(),
        "orbit_radius": config["orbit_radius"], "orbit_radius_source": config["orbit_radius_source"],
        "verified_scene_sha256": config.get('verified_scene_sha256'),
        "saved_scene_report": config.get("saved_scene_report"),
        "saved_scene_max_trajectory_error": config.get("saved_scene_max_trajectory_error"),
        "trajectory_validation": trajectory_validation,
        "content_foley_world_position": config.get("content_foley_world_position"),
        "trajectory_sources": ["unity/Assets/OldWebMap/Scripts/OWV4Oscilloscope.cs", "unity/AgentScripts/OWEpisodeV4AudioFx.cs", "unity/AgentScripts/OWEpisodeV4StableSoundValidation.cs"],
        "assumptions": [
            "Music positions are physical world coordinates independent of worldFrame and region offsets; the position clock pauses continuously during all three journeys. Waveform PCM and stem playback keep real music time.",
            "Horizontal pan=sin(atan2(source.x-listener.x, source.z-listener.z)); rear positions fold into the same left/right panorama.",
            "Spatial gains are cos/sin equal-power; interpolate from centered mono with the runtime 0.55..1 spatialBlend. No HRTF, elevation, spread, Doppler, occlusion or distance rolloff is simulated.",
            "Music uses the runtime 0.8 base volume and 0.3 s end fade; no normalization or limiting is applied.",
            "Content Foley uses one fixed physical point; real keyboard events use the actual keyboard world position at onset. Review balance is preserved: content pan=0, gain=volume*0.73; keyboard pan=-0.28, gain=volume*0.9. These are reference gains, not a Unity attenuation/HRTF reconstruction.",
        ],
        "foley_data": str(foley_data.relative_to(ROOT)) if foley_data.is_relative_to(ROOT) else str(foley_data),
        "foley_events": len(foley["events"]), "duplicate_events_preserved": True,
        "trajectories": trajectory,
    }
    (output_dir / "audio-reference.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    print(f"Stereo reference: {audio_path}\nPeak: {peak:.8f} ({report['peak_dbfs']:.2f} dBFS); {total} samples, {REVIEW_SECONDS}s")
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--stems-dir", type=Path, default=TMP / "episode123/audio")
    parser.add_argument("--foley-data", type=Path, default=ROOT / "data/episode123-v4-foley.json")
    parser.add_argument("--output-dir", type=Path, default=DEST)
    parser.add_argument("--orbit-radius", type=float, help="Override the runtime default if the scene inherited another radius")
    parser.add_argument("--scene-report", type=Path, help="Fresh Pipeline OWEpisodeV4StableSoundValidation.SavedScene report")
    parser.add_argument("--validate-trajectories-only", action="store_true", help="Check the Python trajectory without rendering audio or claiming saved-scene validation")
    options = parser.parse_args()
    if options.validate_trajectories_only:
        config = configuration(options.orbit_radius, options.scene_report, require_saved_scene=False)
        print(json.dumps(validate_trajectories(config), ensure_ascii=False, indent=2))
    else:
        render(options.stems_dir, options.foley_data, options.output_dir, options.orbit_radius, options.scene_report)


if __name__ == "__main__":
    main()
