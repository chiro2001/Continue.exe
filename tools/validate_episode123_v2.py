#!/usr/bin/env python3
"""Validate the episode 123 v2 source data and optional human/live reports.

Run from any directory:
  python tools/validate_episode123_v2.py
  python tools/validate_episode123_v2.py --reports

With --reports, the three live reports are read from
  ~/tmp/vrc-old-web-map/episode123-v2/reports/
Use --reports-dir to point at a different report folder. Missing reports are
failures; this script never treats its own presence as evidence of a run.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import sys
from pathlib import Path
from typing import Any
from urllib.parse import urlparse

try:
    from PIL import Image
except ImportError:  # provide a clear preflight failure rather than a traceback
    Image = None  # type: ignore[assignment]

ROOT = Path(__file__).resolve().parent.parent
REPORTS_DEFAULT = Path.home() / "tmp/vrc-old-web-map/episode123-v2/reports"
MANIFEST_PATH = ROOT / "data/episode123-historical-images.json"
SOFTWARE_MANIFEST_PATH = ROOT / "data/episode123-software-images.json"
SPECTRUM_PATH = ROOT / "data/episode123-v2-spectrum.json"
MONTAGE_PATH = ROOT / "data/episode123-v2-montage.json"
DURATION_SECONDS = 133.12
PULSE_SECONDS = 0.64
EXPECTED_CUES = 12
EXPECTED_FFT_CHANNELS = 4
EXPECTED_FFT_BANDS = 16
EXPECTED_CUTS = round(DURATION_SECONDS / PULSE_SECONDS)
MIN_HISTORICAL_ASSETS = 30
MIN_COMBINED_HISTORICAL_ASSETS = 42
EXPECTED_AUTHORED_ASSETS = 12
EXPECTED_SCENE_VIEW_CAPTURES = 3
VALID_CATEGORIES = {
    "system", "software", "translation", "distribution", "community",
    "vrc", "event", "security",
}


class Validator:
    def __init__(self) -> None:
        self.failures: list[str] = []
        self.passes: list[str] = []

    def check(self, condition: bool, message: str) -> None:
        (self.passes if condition else self.failures).append(message)

    def require(self, condition: bool, message: str) -> None:
        self.check(condition, message)


def load_json(v: Validator, path: Path, label: str) -> dict[str, Any] | None:
    if not path.is_file():
        v.check(False, f"{label}: missing file {path}")
        return None
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        v.check(False, f"{label}: cannot read JSON ({exc})")
        return None
    if not isinstance(value, dict):
        v.check(False, f"{label}: top-level JSON must be an object")
        return None
    return value


def finite_number(value: Any) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(float(value))


def validate_image_manifest(
    v: Validator,
    path: Path,
    label: str,
    allowed_root: str,
    *,
    minimum_assets: int,
    required_trace_field: str | None,
    seen_ids: set[str] | None = None,
    seen_paths: set[str] | None = None,
) -> dict[str, dict[str, Any]]:
    data = load_json(v, path, label)
    if data is None:
        return {}
    assets = data.get("assets")
    if not isinstance(assets, list):
        v.check(False, f"{label}: assets must be an array")
        return {}
    if "asset_count" in data:
        v.check(data.get("asset_count") == len(assets), f"{label}: asset_count matches assets length")
    v.check(len(assets) >= minimum_assets, f"{label}: at least {minimum_assets} assets ({len(assets)} found)")
    if Image is None:
        v.check(False, f"{label}: Pillow is required to inspect image dimensions")
        return {}

    ids = seen_ids if seen_ids is not None else set()
    paths = seen_paths if seen_paths is not None else set()
    by_id: dict[str, dict[str, Any]] = {}
    asset_errors = 0
    for row_number, asset in enumerate(assets, 1):
        row_label = f"{label} asset #{row_number}"
        if not isinstance(asset, dict):
            v.check(False, f"{row_label}: row must be an object")
            asset_errors += 1
            continue
        asset_id = asset.get("id")
        relpath = asset.get("path")
        if not isinstance(asset_id, str) or not asset_id.strip():
            v.check(False, f"{row_label}: id is missing")
            asset_errors += 1
            continue
        if asset_id in ids:
            v.check(False, f"{asset_id}: duplicate id across image manifests")
            asset_errors += 1
        ids.add(asset_id)
        if not isinstance(relpath, str) or not relpath.strip():
            v.check(False, f"{asset_id}: path is missing")
            asset_errors += 1
            continue
        if relpath in paths:
            v.check(False, f"{asset_id}: duplicate path across image manifests ({relpath})")
            asset_errors += 1
        paths.add(relpath)
        if not relpath.startswith(allowed_root):
            v.check(False, f"{asset_id}: path is outside {allowed_root}")
            asset_errors += 1
            continue
        image_path = (ROOT / relpath).resolve()
        try:
            image_path.relative_to(ROOT)
        except ValueError:
            v.check(False, f"{asset_id}: path escapes repository root")
            asset_errors += 1
            continue
        if not image_path.is_file():
            v.check(False, f"{asset_id}: image file is missing ({relpath})")
            asset_errors += 1
            continue
        try:
            with Image.open(image_path) as image:
                actual_size = image.size
                image.verify()
            v.check(
                isinstance(asset.get("width"), int)
                and isinstance(asset.get("height"), int)
                and asset.get("width", 0) > 0
                and asset.get("height", 0) > 0
                and actual_size == (asset.get("width"), asset.get("height")),
                f"{asset_id}: recorded size matches image ({actual_size[0]}x{actual_size[1]})",
            )
        except Exception as exc:  # Pillow raises several format-specific errors
            v.check(False, f"{asset_id}: image cannot be decoded ({exc})")
            asset_errors += 1
            continue
        source_url = asset.get("source_url")
        parsed = urlparse(source_url) if isinstance(source_url, str) else None
        v.check(
            parsed is not None and parsed.scheme in {"http", "https"} and bool(parsed.netloc),
            f"{asset_id}: source_url is a valid HTTP(S) URL",
        )
        v.check(isinstance(asset.get("kind"), str) and bool(asset["kind"].strip()), f"{asset_id}: image nature/kind is recorded")
        v.check("source_date" in asset and (asset["source_date"] is None or isinstance(asset["source_date"], str)), f"{asset_id}: source_date is recorded or explicitly null")
        if required_trace_field is not None:
            trace = asset.get(required_trace_field)
            v.check(isinstance(trace, str) and bool(trace.strip()), f"{asset_id}: {required_trace_field} is recorded")
        else:
            trace_fields = ("evidence", "date_note", "date_basis", "source_page_date")
            has_trace = any(isinstance(asset.get(key), str) and bool(asset[key].strip()) for key in trace_fields)
            v.check(has_trace, f"{asset_id}: evidence/date note provides traceable source context")
        v.check(asset.get("category") in VALID_CATEGORIES, f"{asset_id}: recognized category")
        expected_hash = asset.get("sha256")
        if isinstance(expected_hash, str) and expected_hash:
            actual_hash = hashlib.sha256(image_path.read_bytes()).hexdigest()
            v.check(actual_hash == expected_hash, f"{asset_id}: SHA-256 matches manifest")
        else:
            v.check(False, f"{asset_id}: sha256 is missing")
            asset_errors += 1
        v.check(isinstance(asset.get("title"), str) and bool(asset["title"].strip()), f"{asset_id}: title is recorded")
        by_id[asset_id] = asset
    v.check(asset_errors == 0, f"{label}: no missing or undecodable image rows ({asset_errors} errors)")
    return by_id


def validate_historical_images(v: Validator) -> tuple[dict[str, dict[str, Any]], bool]:
    ids: set[str] = set()
    paths: set[str] = set()
    primary = validate_image_manifest(
        v, MANIFEST_PATH, "historical image manifest", "art/reference/episode123-v2/",
        minimum_assets=MIN_HISTORICAL_ASSETS, required_trace_field="date_basis",
        seen_ids=ids, seen_paths=paths,
    )
    if not SOFTWARE_MANIFEST_PATH.is_file():
        v.check(True, "optional software image manifest is absent")
        return primary, False
    supplemental = validate_image_manifest(
        v, SOFTWARE_MANIFEST_PATH, "supplemental software image manifest", "art/reference/episode123-v2-software/",
        minimum_assets=1, required_trace_field=None,
        seen_ids=ids, seen_paths=paths,
    )
    combined = {**primary, **supplemental}
    v.check(
        len(combined) >= MIN_COMBINED_HISTORICAL_ASSETS,
        f"combined historical manifests: at least {MIN_COMBINED_HISTORICAL_ASSETS} assets ({len(combined)} found)",
    )
    return combined, True


def validate_spectrum(v: Validator) -> dict[str, Any] | None:
    data = load_json(v, SPECTRUM_PATH, "FFT spectrum")
    if data is None:
        return None
    step = data.get("step")
    frames = data.get("frames")
    bands = data.get("bands")
    edges = data.get("band_edges_hz")
    channels = data.get("channels")
    v.check(data.get("version") == 2, "FFT spectrum: version is 2")
    v.check(finite_number(step) and abs(float(step) - 0.04) <= 1e-9, "FFT spectrum: frame step is 0.04 s")
    expected_frames = round(DURATION_SECONDS / float(step)) + 1 if finite_number(step) and float(step) > 0 else -1
    v.check(isinstance(frames, int) and frames == expected_frames, f"FFT spectrum: frame count matches {DURATION_SECONDS:g} s at {step} s ({expected_frames})")
    v.check(bands == EXPECTED_FFT_BANDS, f"FFT spectrum: exactly {EXPECTED_FFT_BANDS} bands")
    v.check(data.get("sample_rate") == 48000 and data.get("fft_size") == 4096, "FFT spectrum: 48 kHz sample rate and 4096-point FFT")
    v.check(finite_number(data.get("normalization_reference")) and float(data["normalization_reference"]) > 0, "FFT spectrum: normalization reference is finite and positive")

    edges_ok = isinstance(edges, list) and len(edges) == EXPECTED_FFT_BANDS + 1
    if edges_ok:
        edges_ok = all(finite_number(edge) for edge in edges)
        edges_ok = edges_ok and all(float(a) < float(b) for a, b in zip(edges, edges[1:]))
        edges_ok = edges_ok and abs(float(edges[0]) - 35.0) <= 0.02 and abs(float(edges[-1]) - 14000.0) <= 0.02
        edges_ok = edges_ok and float(edges[-1]) <= 48000 / 2
    v.check(bool(edges_ok), "FFT spectrum: 17 ordered band edges span 35–14,000 Hz within Nyquist")

    channels_ok = isinstance(channels, list) and len(channels) == EXPECTED_FFT_CHANNELS
    if channels_ok:
        expected_values = expected_frames * EXPECTED_FFT_BANDS
        for index, values in enumerate(channels, 1):
            if not isinstance(values, list) or len(values) != expected_values:
                channels_ok = False
                v.check(False, f"FFT spectrum: channel {index} has {len(values) if isinstance(values, list) else 'non-array'} values; expected {expected_values}")
                continue
            finite_range = all(finite_number(value) and 0.0 <= float(value) <= 1.0 for value in values)
            channels_ok = channels_ok and finite_range
            v.check(finite_range, f"FFT spectrum: channel {index} values are finite and within [0, 1]")
    else:
        v.check(False, f"FFT spectrum: expected {EXPECTED_FFT_CHANNELS} channel arrays")
    if channels_ok:
        v.check(True, f"FFT spectrum: 4 channels × {expected_frames} frames × {EXPECTED_FFT_BANDS} bands")
    return data


def validate_montage(
    v: Validator,
    manifest_assets: dict[str, dict[str, Any]],
    spectrum: dict[str, Any] | None,
    supplemental_manifest_present: bool,
) -> dict[str, Any] | None:
    data = load_json(v, MONTAGE_PATH, "0.64 s montage")
    if data is None:
        return None
    v.check(data.get("version") == 2, "montage: version is 2")
    v.check(finite_number(data.get("duration")) and abs(float(data["duration"]) - DURATION_SECONDS) <= 1e-6, "montage: duration is 133.12 s")
    v.check(finite_number(data.get("pulse_seconds")) and abs(float(data["pulse_seconds"]) - PULSE_SECONDS) <= 1e-9, "montage: primary pulse is 0.64 s")
    v.check(finite_number(data.get("secondary_tick_seconds")) and abs(float(data["secondary_tick_seconds"]) - 0.32) <= 1e-9, "montage: secondary tick is 0.32 s")

    cuts = data.get("cuts")
    v.check(isinstance(cuts, list) and len(cuts) == EXPECTED_CUTS, f"montage: exactly {EXPECTED_CUTS} primary cuts")
    if isinstance(cuts, list):
        times = [row.get("time") if isinstance(row, dict) else None for row in cuts]
        time_ok = len(times) == EXPECTED_CUTS and all(finite_number(t) for t in times)
        if time_ok:
            time_ok = abs(float(times[0])) <= 1e-6 and abs(float(times[-1]) - (DURATION_SECONDS - PULSE_SECONDS)) <= 1e-4
            time_ok = time_ok and all(abs(float(t) - index * PULSE_SECONDS) <= 1e-4 for index, t in enumerate(times))
            time_ok = time_ok and all(abs((float(b) - float(a)) - PULSE_SECONDS) <= 1e-4 for a, b in zip(times, times[1:]))
        v.check(bool(time_ok), "montage: cuts are continuous at 0.64 s from 0 to 132.48 s")
        image_count = len(data.get("images", [])) if isinstance(data.get("images"), list) else 0
        cut_refs_ok = all(
            isinstance(row, dict)
            and isinstance(row.get("image"), int)
            and 0 <= row["image"] < image_count
            for row in cuts
        )
        v.check(cut_refs_ok, "montage: every cut references an existing image")
    else:
        v.check(False, "montage: cuts must be an array")

    v.check(data.get("primary_cuts") == EXPECTED_CUTS, f"montage: primary_cuts is {EXPECTED_CUTS}")
    images = data.get("images")
    montage_by_id = {row.get("id"): row for row in images if isinstance(row, dict) and isinstance(row.get("id"), str)} if isinstance(images, list) else {}
    missing_ids = sorted(set(manifest_assets) - set(montage_by_id))
    missing_text = ", ".join(missing_ids[:10])
    if len(missing_ids) > 10:
        missing_text += f", … (+{len(missing_ids) - 10})"
    v.check(not missing_ids, f"montage: includes all historical manifest assets ({len(missing_ids)} missing{': ' + missing_text if missing_ids else ''})")
    if isinstance(images, list):
        v.check(len(montage_by_id) == len(images), "montage: image ids are unique")
        if supplemental_manifest_present:
            expected_total = len(manifest_assets) + EXPECTED_AUTHORED_ASSETS + EXPECTED_SCENE_VIEW_CAPTURES
            v.check(len(images) == expected_total, f"montage: {len(images)} image entries matches {expected_total} source, authored, and SceneView images")
    for asset_id, source in manifest_assets.items():
        row = montage_by_id.get(asset_id)
        if row is None:
            continue
        same = row.get("path") == source.get("path") and row.get("width") == source.get("width") and row.get("height") == source.get("height")
        v.check(same, f"montage: {asset_id} path and dimensions match its source manifest")

    if spectrum is not None:
        same_fft = data.get("fft_channels") in (None, EXPECTED_FFT_CHANNELS) and data.get("fft_bands") in (None, EXPECTED_FFT_BANDS)
        v.check(same_fft, "montage: any embedded FFT shape agrees with 4×16 spectrum data")
    return data


def report_number(v: Validator, report: dict[str, Any], key: str, label: str, *, maximum: float | None = None) -> None:
    value = report.get(key)
    valid = finite_number(value) and float(value) >= 0
    if valid and maximum is not None:
        valid = float(value) <= maximum
    limit = f" and <= {maximum:g}" if maximum is not None else ""
    v.check(valid, f"{label}: {key} is finite, >= 0{limit}")


def validate_live_report(v: Validator, path: Path, expected_cuts: int) -> None:
    report = load_json(v, path, "live-v2 report")
    if report is None:
        return
    label = "live-v2 report"
    v.check(report.get("success") is True, f"{label}: success is true")
    v.check(finite_number(report.get("duration")) and abs(float(report["duration"]) - DURATION_SECONDS) <= 1e-6, f"{label}: duration is {DURATION_SECONDS:g} s")
    v.check(report.get("cuesSeen") == EXPECTED_CUES, f"{label}: cuesSeen is {EXPECTED_CUES}")
    v.check(report.get("cutsSeen") == expected_cuts, f"{label}: cutsSeen matches montage ({expected_cuts})")
    v.check(report.get("fftChannels") == EXPECTED_FFT_CHANNELS, f"{label}: fftChannels is {EXPECTED_FFT_CHANNELS}")
    v.check(report.get("fftBands") == EXPECTED_FFT_BANDS, f"{label}: fftBands is {EXPECTED_FFT_BANDS}")
    report_number(v, report, "maxIntertrackSamples", label, maximum=960)
    report_number(v, report, "maxMontageMs", label)
    v.check(report.get("endStopped") is True, f"{label}: endStopped is true")


def validate_interaction_report(v: Validator, path: Path) -> None:
    report = load_json(v, path, "interaction-v2 report")
    if report is None:
        return
    label = "interaction-v2 report"
    required = [
        "rayShapeHit", "pointerClickPlayed", "panelHiddenWhilePlaying", "pauseIgnored",
        "lateJoinRebuilt", "endPanelShown",
    ]
    v.check(report.get("success") is True, f"{label}: success is true")
    for key in required:
        v.check(report.get(key) is True, f"{label}: {key} is true")
    scope = report.get("scope")
    scope_text = scope.casefold() if isinstance(scope, str) else ""
    compact_scope = re.sub(r"[\s_-]+", "", scope_text)
    runtime_scope_ok = (
        "真实udon/clientsim" in compact_scope
        or ("udon" in compact_scope and "clientsim" in compact_scope)
    )
    anchor_scope_ok = (
        "迟到为收到锚点的本地状态重建" in scope_text
        or ("receivedanchor" in compact_scope and "localreconstruction" in compact_scope)
    )
    multiclient_scope = "多客户端传输" in scope_text or "multiclientnetworktransport" in compact_scope
    untested_scope = (
        "未声称" in scope_text
        or "notreal" in compact_scope
        or "nottested" in compact_scope
        or "notclaim" in compact_scope
        or "noreal" in compact_scope
    )
    scope_ok = runtime_scope_ok and anchor_scope_ok and multiclient_scope and untested_scope
    v.check(scope_ok, f"{label}: scope states Udon/ClientSim, received-anchor local reconstruction, and no real multiclient transport test")


def validate_typing_report(v: Validator, path: Path) -> None:
    report = load_json(v, path, "typing-checks report")
    if report is None:
        return
    label = "typing-checks report"
    v.check(report.get("success") is True, f"{label}: success is true")
    rows = report.get("rows")
    if not isinstance(rows, list):
        v.check(False, f"{label}: rows is an array")
        return
    counts: dict[str, set[float]] = {"Kipfel": set(), "Nemesis": set()}
    gaps: list[float] = []
    valid_rows = True
    for index, row in enumerate(rows, 1):
        if not isinstance(row, dict):
            v.check(False, f"{label}: row {index} is an object")
            valid_rows = False
            continue
        avatar = row.get("appearanceId")
        phase = row.get("phase")
        left = row.get("leftX")
        right = row.get("rightX")
        row_ok = avatar in counts and finite_number(phase) and 0.0 <= float(phase) <= 1.0
        row_ok = row_ok and finite_number(left) and finite_number(right) and float(left) < float(right)
        row_ok = row_ok and float(right) - float(left) > 0.04 and row.get("crossed") is False
        v.check(bool(row_ok), f"{label}: row {index} has valid avatar/phase, ordered hands, >0.04 gap, crossed=false")
        if row_ok:
            counts[avatar].add(round(float(phase), 6))
            gaps.append(float(right) - float(left))
        else:
            valid_rows = False
    for avatar, phases in counts.items():
        v.check(len(phases) >= 5, f"{label}: {avatar} has at least 5 distinct phases ({len(phases)})")
    minimum = report.get("minSeparation")
    computed = min(gaps) if gaps else math.nan
    min_ok = finite_number(minimum) and float(minimum) > 0.04 and math.isfinite(computed)
    if min_ok:
        min_ok = abs(float(minimum) - computed) <= 0.002
    v.check(min_ok, f"{label}: minSeparation matches row minimum and is >0.04")
    v.check(valid_rows and len(gaps) >= 10, f"{label}: at least 10 valid phase rows across both avatars")


def validate_reports(v: Validator, reports_dir: Path, montage: dict[str, Any] | None) -> None:
    if montage is None:
        v.check(False, "--reports requires a valid generated montage to compare cutsSeen")
        expected_cuts = EXPECTED_CUTS
    else:
        expected_cuts = montage.get("primary_cuts", EXPECTED_CUTS)
    validate_live_report(v, reports_dir / "live-v2.json", expected_cuts)
    validate_interaction_report(v, reports_dir / "interaction-v2.json")
    validate_typing_report(v, reports_dir / "typing-checks.json")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reports", action="store_true", help="also require and validate the three live reports")
    parser.add_argument("--reports-dir", type=Path, default=REPORTS_DEFAULT, help=f"report directory (default: {REPORTS_DEFAULT})")
    parser.add_argument("--verbose", action="store_true", help="print every successful check")
    args = parser.parse_args()

    validator = Validator()
    manifest_assets, supplemental_manifest_present = validate_historical_images(validator)
    spectrum = validate_spectrum(validator)
    montage = validate_montage(validator, manifest_assets, spectrum, supplemental_manifest_present)
    if args.reports:
        validate_reports(validator, args.reports_dir.expanduser().resolve(), montage)

    if args.verbose:
        for message in validator.passes:
            print(f"PASS {message}")
    for message in validator.failures:
        print(f"FAIL {message}", file=sys.stderr)
    print(f"{len(validator.passes)} passed; {len(validator.failures)} failed")
    return 1 if validator.failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
