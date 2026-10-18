#!/usr/bin/env python3
"""Encode completed Unity reference frames on GamePC and copy the review film back."""
from pathlib import Path
import importlib.util
import subprocess

ROOT=Path(__file__).resolve().parent.parent
TMP=Path.home()/'tmp/vrc-old-web-map/episode123-v2/review';TMP.mkdir(parents=True,exist_ok=True)
spec=importlib.util.spec_from_file_location('ur',ROOT/'tools/unity_remote.py');remote=importlib.util.module_from_spec(spec);spec.loader.exec_module(remote)
base=r'C:\Users\Chiro\tmp\vrc-old-web-map\episode123-v2'
assert remote.run_pwsh('New-Item -ItemType Directory -Force -Path '+remote.pwsh_literal(base+r'\review')+' | Out-Null').returncode==0
subprocess.run(['scp','-q',str(TMP/'review-audio.wav'),'gpu5080:C:/Users/Chiro/tmp/vrc-old-web-map/episode123-v2/review/review-audio.wav'],check=True)
args=['-y','-hide_banner','-loglevel','warning','-framerate','25','-i',base+r'\movie-frames\frame_%05d.jpg','-i',base+r'\review\review-audio.wav','-frames:v','3376','-c:v','libx264','-preset','medium','-crf','18','-pix_fmt','yuv420p','-c:a','aac','-b:a','256k','-t','135.04','-movflags','+faststart',base+r'\review\episode123-v2-review.mp4']
script='$episodeRenderReport = Get-Content -LiteralPath '+remote.pwsh_literal(base+r'\movie-frames\render-report.json')+' -Raw | ConvertFrom-Json; if (-not $episodeRenderReport.complete) { throw "Reference frames are incomplete" }; '
script+="& 'C:\\ProgramData\\chocolatey\\bin\\ffmpeg.exe' "+' '.join(remote.pwsh_literal(x) for x in args)+'; exit $LASTEXITCODE'
r=remote.run_pwsh(script,capture_output=True)
(TMP/'encode-stdout.txt').write_bytes(r.stdout);(TMP/'encode-stderr.txt').write_bytes(r.stderr)
if r.returncode:print(r.stderr.decode('utf-8','replace')[-3500:]);raise SystemExit(r.returncode)
subprocess.run(['scp','-q','gpu5080:C:/Users/Chiro/tmp/vrc-old-web-map/episode123-v2/review/episode123-v2-review.mp4',str(TMP)+'/'],check=True)
print('Review film:',TMP/'episode123-v2-review.mp4',flush=True)
