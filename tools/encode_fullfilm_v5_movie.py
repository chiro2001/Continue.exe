#!/usr/bin/env python3
"""Guard, encode and retrieve the 6386-frame full-film review; never play it.

The complete PowerShell program is uploaded as a .ps1 file. Only its short
invocation goes through Windows SSH, avoiding command-line length limits.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import subprocess
import wave
from fractions import Fraction
from render_fullfilm_v5_audio import validate_reference
from pathlib import Path

import unity_remote as remote

DEST = Path.home() / "tmp/vrc-old-web-map/fullfilm-v5/review"
BASE = r"C:\Users\Chiro\tmp\vrc-old-web-map\fullfilm-v5"
SCP_BASE = "C:/Users/Chiro/tmp/vrc-old-web-map/fullfilm-v5"
FILENAME = "fullfilm-v5-review.mp4"
FRAME_COUNT, FPS, DURATION = 6386, 25, 255.44
FFMPEG = r"C:\ProgramData\chocolatey\bin\ffmpeg.exe"
FFPROBE = r"C:\ProgramData\chocolatey\bin\ffprobe.exe"
ROOT = Path(__file__).resolve().parents[1]


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def checked_sha(value,label):
    if not isinstance(value,str) or re.fullmatch(r"[0-9a-fA-F]{64}",value) is None:
        raise ValueError("Missing or invalid SHA256: "+label)
    return value.lower()


def validate_audio_inputs(report):
    inputs=report.get("inputs")
    if not isinstance(inputs,list) or not inputs:
        raise ValueError("Audio reference must record the inputs used to render it")
    seen=set()
    for group in ("inputs","sources","clip_sources"):
        records=report.get(group)
        if not isinstance(records,list) or not records:
            raise ValueError("Audio reference is missing "+group)
        for record in records:
            if not isinstance(record,dict) or not isinstance(record.get("path"),str):
                raise ValueError("Invalid audio input record: "+group)
            path=Path(record["path"]).expanduser()
            if not path.is_absolute(): path=ROOT/path
            expected=checked_sha(record.get("sha256"),group+": "+str(path))
            if not path.is_file() or sha(path)!=expected:
                raise ValueError("Audio input changed or is missing; render the complete reference again: "+str(path))
            if group=="inputs":
                resolved=path.resolve()
                if resolved in seen: raise ValueError("Duplicate audio input: "+str(path))
                seen.add(resolved)
    return True


def validate_audio_stream(stream):
    # A long video/container does not establish that its audio is complete.
    try:
        duration=float(stream.get("duration"))
    except (TypeError,ValueError):
        try: duration=float(stream["duration_ts"])*float(Fraction(stream["time_base"]))
        except (KeyError,TypeError,ValueError,ZeroDivisionError):
            raise ValueError("Encoded audio stream has no verifiable duration") from None
    if not math.isfinite(duration) or abs(duration-DURATION)>.04:
        raise ValueError(f"Encoded audio stream must preserve all {DURATION:g} seconds; got {duration}")
    return duration


def validate_progress(report):
    if (report.get("complete") is not True or type(report.get("frame")) is not int
            or report["frame"] != FRAME_COUNT or "error" not in report
            or report["error"] not in (None, "")):
        raise ValueError(f"Full-film frames must be complete, frame={FRAME_COUNT}, error empty: {report}")


def validate_audio(path):
    return validate_reference(path)


def progress_guard():
    path=remote.pwsh_literal(BASE+r"\reports\movie-progress.json")
    return f"""$fullfilmMovieProgress=Get-Content -LiteralPath {path} -Raw | ConvertFrom-Json
if($fullfilmMovieProgress.complete -isnot [bool] -or -not $fullfilmMovieProgress.complete `
    -or ($fullfilmMovieProgress.frame -isnot [int] -and $fullfilmMovieProgress.frame -isnot [long]) `
    -or $fullfilmMovieProgress.frame -ne {FRAME_COUNT} `
    -or -not ($fullfilmMovieProgress.PSObject.Properties.Name -contains 'error') `
    -or ($null -ne $fullfilmMovieProgress.error -and [string]$fullfilmMovieProgress.error -ne '')) {{
    throw 'Full-film frames must be complete, frame=6386, and error empty'
}}
"""


def encode_script(audio_sha256="DRY_RUN_AUDIO_SHA256"):
    literal=remote.pwsh_literal
    frames=BASE+r"\movie-frames"
    pending=BASE+"\\review\\"+FILENAME.replace(".mp4",".encoding.mp4")
    output=BASE+"\\review\\"+FILENAME
    audio=BASE+r"\review\review-audio.wav"
    report=BASE+r"\reports\encode-report.json"
    args=["-y","-hide_banner","-loglevel","warning","-framerate",str(FPS),"-start_number","0",
        "-i",frames+r"\frame_%05d.jpg","-i",audio,"-map","0:v:0","-map","1:a:0",
        "-frames:v",str(FRAME_COUNT),"-c:v","libx264","-preset","medium","-crf","18",
        "-pix_fmt","yuv420p","-c:a","aac","-b:a","256k","-t",str(DURATION),"-movflags","+faststart",pending]
    return "$ErrorActionPreference='Stop'\n$ProgressPreference='SilentlyContinue'\n"+progress_guard()+f"""
$fullfilmFrameFiles=@(Get-ChildItem -LiteralPath {literal(frames)} -Filter 'frame_*.jpg' -File)
if($fullfilmFrameFiles.Count -ne {FRAME_COUNT}) {{ throw 'Frame directory must contain exactly 6386 JPEGs' }}
$fullfilmFramesByName=@{{}}
foreach($fullfilmFrameFile in $fullfilmFrameFiles) {{ $fullfilmFramesByName[$fullfilmFrameFile.Name]=$fullfilmFrameFile.Length }}
for($fullfilmFrameIndex=0;$fullfilmFrameIndex -lt {FRAME_COUNT};$fullfilmFrameIndex++) {{
    $fullfilmFrameName='frame_{{0:D5}}.jpg' -f $fullfilmFrameIndex
    if(-not $fullfilmFramesByName.ContainsKey($fullfilmFrameName) -or $fullfilmFramesByName[$fullfilmFrameName] -eq 0) {{
        throw ('Missing or empty full-film frame: '+$fullfilmFrameName)
    }}
}}
foreach($fullfilmCheckIndex in @(0,3193,6385)) {{
    $fullfilmCheckPath=Join-Path {literal(frames)} ('frame_{{0:D5}}.jpg' -f $fullfilmCheckIndex)
    $fullfilmSize=& {literal(FFPROBE)} -v error -select_streams v:0 -show_entries stream=width,height -of csv=p=0:s=x $fullfilmCheckPath
    if($LASTEXITCODE -ne 0 -or [string]$fullfilmSize -ne '1600x900') {{ throw 'Full-film frames must be 1600x900' }}
}}
if((Get-FileHash -LiteralPath {literal(audio)} -Algorithm SHA256).Hash.ToLowerInvariant() -ne {literal(audio_sha256)}) {{
    throw 'Uploaded audio differs from the validated complete four-stem reference'
}}
& {literal(FFMPEG)} {" ".join(literal(arg) for arg in args)}
if($LASTEXITCODE -ne 0) {{ throw ('ffmpeg failed: '+$LASTEXITCODE) }}
$fullfilmProbeRaw=& {literal(FFPROBE)} -v error -count_frames -show_entries 'stream=codec_type,codec_name,width,height,r_frame_rate,nb_read_frames,sample_rate,channels,duration:format=duration' -of json {literal(pending)}
if($LASTEXITCODE -ne 0) {{ throw 'Cannot probe the encoded full-film movie' }}
$fullfilmProbe=($fullfilmProbeRaw -join "`n") | ConvertFrom-Json
$fullfilmVideo=@($fullfilmProbe.streams | Where-Object {{ $_.codec_type -eq 'video' }})
$fullfilmAudio=@($fullfilmProbe.streams | Where-Object {{ $_.codec_type -eq 'audio' }})
if($fullfilmVideo.Count -ne 1 -or $fullfilmAudio.Count -ne 1 `
    -or $fullfilmVideo[0].width -ne 1600 -or $fullfilmVideo[0].height -ne 900 `
    -or $fullfilmVideo[0].r_frame_rate -ne '25/1' -or $fullfilmVideo[0].nb_read_frames -ne '6386' `
    -or $fullfilmVideo[0].codec_name -ne 'h264' -or $fullfilmAudio[0].codec_name -ne 'aac' `
    -or $fullfilmAudio[0].sample_rate -ne '48000' -or $fullfilmAudio[0].channels -ne 2 `
    -or $null -eq $fullfilmAudio[0].duration -or [Math]::Abs([double]$fullfilmAudio[0].duration-255.44) -gt 0.04 `
    -or [Math]::Abs([double]$fullfilmProbe.format.duration-255.44) -gt 0.04) {{
    throw 'Full-film encode failed dimensions/fps/frame-count/audio/duration validation'
}}
Move-Item -LiteralPath {literal(pending)} -Destination {literal(output)} -Force
$fullfilmEncodedHash=(Get-FileHash -LiteralPath {literal(output)} -Algorithm SHA256).Hash.ToLowerInvariant()
$fullfilmEncodeReport=@{{success=$true;complete=$true;frame={FRAME_COUNT};fps={FPS};duration={DURATION};width=1600;height=900;audioSha256={literal(audio_sha256)};movieSha256=$fullfilmEncodedHash;movie={literal(output)};played=$false;uploaded=$false;probe=$fullfilmProbe}}
$fullfilmEncodeReport | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath {literal(report)} -Encoding utf8
$fullfilmEncodeReport | ConvertTo-Json -Depth 8
exit 0
"""


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host",default=remote.DEFAULT_HOST)
    parser.add_argument("--dry-run",action="store_true",help="Print the guarded .ps1 without connecting or writing files")
    options=parser.parse_args()
    if options.dry_run:
        print(encode_script());return 0
    audio=DEST/"review-audio.wav";audio_hash=validate_audio(audio)
    # This read-only preflight happens before creating remote directories or
    # uploading files; the full .ps1 repeats the guard immediately before encode.
    preflight=remote.run_pwsh(progress_guard()+"$fullfilmMovieProgress | ConvertTo-Json -Compress",options.host,capture_output=True)
    if preflight.returncode:
        print(preflight.stderr.decode("utf-8","replace"));return preflight.returncode
    progress=json.loads(preflight.stdout.decode("utf-8-sig"));validate_progress(progress)
    DEST.mkdir(parents=True,exist_ok=True)
    (DEST/"movie-progress-preflight.json").write_text(json.dumps(progress,ensure_ascii=False,indent=2)+"\n")
    created=remote.run_pwsh("New-Item -ItemType Directory -Force -Path "+remote.pwsh_literal(BASE+r"\review")+" | Out-Null",options.host,capture_output=True)
    if created.returncode:
        print(created.stderr.decode("utf-8","replace"));return created.returncode
    program=DEST/("encode-"+FILENAME.replace("-review.mp4",".ps1"));program.write_text(encode_script(audio_hash),encoding="utf-8")
    subprocess.run(["scp", *remote.ssh_control_args(),"-q",str(audio),options.host+":"+SCP_BASE+"/review/review-audio.wav"],check=True)
    subprocess.run(["scp", *remote.ssh_control_args(),"-q",str(program),options.host+":"+SCP_BASE+"/review/"+program.name],check=True)
    result=remote.run_pwsh("& "+remote.pwsh_literal(BASE+"\\review\\"+program.name),options.host,capture_output=True)
    (DEST/"encode-stdout.txt").write_bytes(result.stdout);(DEST/"encode-stderr.txt").write_bytes(result.stderr)
    if result.returncode:
        print(result.stderr.decode("utf-8","replace")[-6000:]);return result.returncode
    reports=DEST.parent/"reports";reports.mkdir(parents=True,exist_ok=True)
    report_path=reports/"encode-report.json"
    subprocess.run(["scp", *remote.ssh_control_args(),"-q",options.host+":"+SCP_BASE+"/reports/encode-report.json",str(report_path)],check=True)
    report=json.loads(report_path.read_text(encoding="utf-8-sig"))
    if report.get("success") is not True or report.get("audioSha256")!=audio_hash or report.get("frame")!=FRAME_COUNT:
        raise ValueError("Remote encode report does not match this requested full-film reference")
    audio_streams=[row for row in report.get("probe",{}).get("streams",[]) if row.get("codec_type")=="audio"]
    if len(audio_streams)!=1: raise ValueError("Remote encode must contain one complete audio stream")
    validate_audio_stream(audio_streams[0])
    pending=DEST/FILENAME.replace(".mp4",".download.mp4")
    subprocess.run(["scp", *remote.ssh_control_args(),"-q",options.host+":"+SCP_BASE+"/review/"+FILENAME,str(pending)],check=True)
    if sha(pending)!=report.get("movieSha256"): raise ValueError("Retrieved movie hash differs from the validated remote encode")
    pending.replace(DEST/FILENAME)
    print(json.dumps(dict(success=True,movie=str(DEST/FILENAME),sha256=sha(DEST/FILENAME),frames=FRAME_COUNT,duration=DURATION,played=False),ensure_ascii=False,indent=2))
    return 0


if __name__=="__main__": raise SystemExit(main())
