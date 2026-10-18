#!/usr/bin/env python3
"""Encode complete V4 frames on GamePC and copy the 1600x900, 25 fps review back."""

import argparse
import json
import subprocess
import wave
from pathlib import Path

import unity_remote as remote

DEST = Path.home() / "tmp/vrc-old-web-map/episode123-v4/review"
BASE = r"C:\Users\Chiro\tmp\vrc-old-web-map\episode123-v4"
SCP_BASE = "C:/Users/Chiro/tmp/vrc-old-web-map/episode123-v4"
FILENAME = "episode123-v4-review.mp4"
FRAME_COUNT = 3376
FPS = 25
DURATION = 135.04
FFMPEG = r"C:\ProgramData\chocolatey\bin\ffmpeg.exe"
FFPROBE = r"C:\ProgramData\chocolatey\bin\ffprobe.exe"


def validate_progress(report):
    if (report.get("complete") is not True or type(report.get("frame")) is not int
            or report["frame"] != FRAME_COUNT or "error" not in report or report["error"] not in (None, "")):
        raise ValueError(f"V4 frames are not complete and error-free: {report}")


def progress_guard():
    path = remote.pwsh_literal(BASE + r"\reports\movie-progress.json")
    return (
        f"$episodeV4Report=Get-Content -LiteralPath {path} -Raw | ConvertFrom-Json; "
        "if($episodeV4Report.complete -isnot [bool] -or -not $episodeV4Report.complete "
        "-or ($episodeV4Report.frame -isnot [int] -and $episodeV4Report.frame -isnot [long]) "
        f"-or $episodeV4Report.frame -ne {FRAME_COUNT} "
        "-or -not ($episodeV4Report.PSObject.Properties.Name -contains 'error') "
        "-or ($null -ne $episodeV4Report.error -and [string]$episodeV4Report.error -ne ''))"
        "{throw 'V4 frames must be complete, frame=3376, and error empty'}; "
    )


def encode_script():
    frames = BASE + r"\movie-frames"
    pending = BASE + "\\review\\episode123-v4-review.encoding.mp4"
    output = BASE + "\\review\\" + FILENAME
    args = [
        "-y", "-hide_banner", "-loglevel", "warning", "-framerate", str(FPS), "-start_number", "0",
        "-i", frames + r"\frame_%05d.jpg", "-i", BASE + r"\review\review-audio.wav",
        "-map", "0:v:0", "-map", "1:a:0", "-frames:v", str(FRAME_COUNT),
        "-c:v", "libx264", "-preset", "medium", "-crf", "18", "-pix_fmt", "yuv420p",
        "-c:a", "aac", "-b:a", "256k", "-t", str(DURATION), "-movflags", "+faststart", pending,
    ]
    script = progress_guard()
    script += f"$episodeV4Frames=@(Get-ChildItem -LiteralPath {remote.pwsh_literal(frames)} -Filter 'frame_*.jpg' -File); "
    script += f"if($episodeV4Frames.Count -ne {FRAME_COUNT}){{throw 'V4 frame directory does not contain exactly 3376 frames'}}; "
    script += (
        f"for($episodeV4Index=0;$episodeV4Index -lt {FRAME_COUNT};$episodeV4Index++)"
        "{$episodeV4Frame=Join-Path " + remote.pwsh_literal(frames) + " ('frame_{0:D5}.jpg' -f $episodeV4Index); "
        "if(-not (Test-Path -LiteralPath $episodeV4Frame -PathType Leaf) -or (Get-Item -LiteralPath $episodeV4Frame).Length -eq 0)"
        "{throw ('Missing or empty V4 frame: '+$episodeV4Frame)}}; "
    )
    script += (
        "$episodeV4Size=& " + remote.pwsh_literal(FFPROBE)
        + " -v error -select_streams v:0 -show_entries stream=width,height -of csv=p=0:s=x "
        + remote.pwsh_literal(frames + r"\frame_00000.jpg") + "; "
        "if($LASTEXITCODE -ne 0 -or [string]$episodeV4Size -ne '1600x900'){throw 'V4 frames must be 1600x900'}; "
    )
    script += "& " + remote.pwsh_literal(FFMPEG) + " " + " ".join(remote.pwsh_literal(arg) for arg in args)
    script += "; if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}; "
    script += (
        "$episodeV4ProbeRaw=& " + remote.pwsh_literal(FFPROBE)
        + " -v error -count_frames -show_entries 'stream=codec_type,width,height,r_frame_rate,nb_read_frames:format=duration' -of json "
        + remote.pwsh_literal(pending) + "; "
        "if($LASTEXITCODE -ne 0){throw 'Cannot validate encoded V4 movie'}; "
        "$episodeV4Probe=($episodeV4ProbeRaw -join \"`n\") | ConvertFrom-Json; "
        "$episodeV4Video=@($episodeV4Probe.streams | Where-Object {$_.codec_type -eq 'video'}); "
        "$episodeV4Audio=@($episodeV4Probe.streams | Where-Object {$_.codec_type -eq 'audio'}); "
        "if($episodeV4Video.Count -ne 1 -or $episodeV4Audio.Count -ne 1 "
        "-or $episodeV4Video[0].width -ne 1600 -or $episodeV4Video[0].height -ne 900 "
        "-or $episodeV4Video[0].r_frame_rate -ne '25/1' -or $episodeV4Video[0].nb_read_frames -ne '3376' "
        "-or [Math]::Abs([double]$episodeV4Probe.format.duration-135.04) -gt 0.04)"
        "{throw 'Encoded V4 movie failed dimensions/fps/frame-count/audio/duration validation'}; "
        "$episodeV4Probe | ConvertTo-Json -Depth 5; "
    )
    script += "Move-Item -LiteralPath " + remote.pwsh_literal(pending) + " -Destination " + remote.pwsh_literal(output) + " -Force; exit 0"
    return script


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", default=remote.DEFAULT_HOST)
    parser.add_argument("--dry-run", action="store_true", help="Print the guarded encoding script without connecting or writing files")
    options = parser.parse_args()
    if options.dry_run:
        print(encode_script())
        return 0
    audio = DEST / "review-audio.wav"
    with wave.open(str(audio), "rb") as sound:
        if (sound.getframerate(), sound.getnchannels(), sound.getnframes(), sound.getsampwidth()) != (48000, 2, 6481920, 2):
            raise ValueError("Generate V4's 135.04 s, 48 kHz, stereo PCM16 review-audio.wav first")

    # Read-only preflight precedes even mkdir/upload. The same guard runs again
    # immediately before ffmpeg so a changed progress report cannot bypass it.
    preflight = remote.run_pwsh(progress_guard() + "$episodeV4Report | ConvertTo-Json -Compress", options.host, capture_output=True)
    if preflight.returncode:
        print(preflight.stderr.decode("utf-8", "replace"))
        return preflight.returncode
    validate_progress(json.loads(preflight.stdout.decode("utf-8-sig")))
    DEST.mkdir(parents=True, exist_ok=True)
    created = remote.run_pwsh("New-Item -ItemType Directory -Force -Path " + remote.pwsh_literal(BASE + r"\review") + " | Out-Null", options.host, capture_output=True)
    if created.returncode:
        print(created.stderr.decode("utf-8", "replace"))
        return created.returncode
    subprocess.run(["scp", "-q", str(audio), options.host + ":" + SCP_BASE + "/review/review-audio.wav"], check=True)
    # Keep the Windows SSH command below cmd.exe's command-line limit. The
    # complete, reviewable encode program lives in the task's temporary folder.
    program = DEST / 'encode-episode123-v4.ps1'
    program.write_text("$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue';\n" + encode_script() + '\n', encoding='utf-8')
    subprocess.run(["scp", "-q", str(program), options.host + ":" + SCP_BASE + "/review/" + program.name], check=True)
    result = remote.run_pwsh('& ' + remote.pwsh_literal(BASE + '\\review\\' + program.name), options.host, capture_output=True)
    (DEST / "encode-stdout.txt").write_bytes(result.stdout)
    (DEST / "encode-stderr.txt").write_bytes(result.stderr)
    if result.returncode:
        print(result.stderr.decode("utf-8", "replace")[-4000:])
        return result.returncode
    subprocess.run(["scp", "-q", options.host + ":" + SCP_BASE + "/review/" + FILENAME, str(DEST / FILENAME)], check=True)
    print("Film:", DEST / FILENAME)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
