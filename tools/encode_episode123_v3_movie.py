#!/usr/bin/env python3
"""Encode completed V3 Unity frames on GamePC and copy the film back."""
from pathlib import Path
import sys,subprocess
sys.path.insert(0,str(Path(__file__).resolve().parent));import unity_remote as remote
TMP=Path.home()/'tmp/vrc-old-web-map/episode123-v3/review';TMP.mkdir(parents=True,exist_ok=True)
BASE=r'C:\Users\Chiro\tmp\vrc-old-web-map\episode123-v3'
assert remote.run_pwsh('New-Item -ItemType Directory -Force -Path '+remote.pwsh_literal(BASE+r'\review')+' | Out-Null').returncode==0
subprocess.run(['scp','-q',str(TMP/'review-audio.wav'),'gpu5080:C:/Users/Chiro/tmp/vrc-old-web-map/episode123-v3/review/review-audio.wav'],check=True)
args=['-y','-hide_banner','-loglevel','warning','-framerate','25','-i',BASE+r'\movie-frames\frame_%05d.jpg','-i',BASE+r'\review\review-audio.wav','-frames:v','3376','-c:v','libx264','-preset','medium','-crf','18','-pix_fmt','yuv420p','-c:a','aac','-b:a','256k','-t','135.04','-movflags','+faststart',BASE+r'\review\episode123-v3-review.mp4']
script='$episodeV3Report=Get-Content -LiteralPath '+remote.pwsh_literal(BASE+r'\reports\movie-progress.json')+' -Raw | ConvertFrom-Json; if(-not $episodeV3Report.complete -or $episodeV3Report.frame -ne 3376){throw "Incomplete V3 frames"}; '
script+="& 'C:\\ProgramData\\chocolatey\\bin\\ffmpeg.exe' "+' '.join(remote.pwsh_literal(a) for a in args)+'; exit $LASTEXITCODE'
r=remote.run_pwsh(script,capture_output=True)
(TMP/'encode-stderr.txt').write_bytes(r.stderr)
if r.returncode:
    print(r.stderr.decode('utf-8','replace')[-2500:]);raise SystemExit(r.returncode)
subprocess.run(['scp','-q','gpu5080:C:/Users/Chiro/tmp/vrc-old-web-map/episode123-v3/review/episode123-v3-review.mp4',str(TMP)+'/'],check=True)
print('Film:',TMP/'episode123-v3-review.mp4')
