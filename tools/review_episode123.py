#!/usr/bin/env python3
"""Rebuild/capture owned Episode scene through Pipeline and retain iteration evidence."""
import argparse
import json
import subprocess
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parent.parent
parser=argparse.ArgumentParser();parser.add_argument('--iteration',required=True);parser.add_argument('--rebuild',action='store_true');args=parser.parse_args()
dest=Path.home()/'tmp/vrc-old-web-map/episode123'/('iteration'+args.iteration);dest.mkdir(parents=True,exist_ok=True)
commands=[]
if args.rebuild:commands+=['RepairPrograms','SaveWorkingScene','Build']
commands+=['CaptureAll','SaveWorkingScene','InspectEpisode']
for entry in commands:
    result=subprocess.run(['python3',str(ROOT/'tools/unity_remote.py'),'--check-result','run_script','--file','AgentScripts/OWEpisodeBuild.cs','--entry','OWEpisodeBuild.'+entry,'--timeout_ms','120000'],cwd=ROOT,capture_output=True,text=True)
    (dest/(entry+'.json')).write_text(result.stdout)
    (dest/(entry+'-stderr.txt')).write_text(result.stderr)
    if result.returncode:
        print(result.stdout[-6500:]);raise SystemExit(result.returncode)
    envelope=json.loads(result.stdout);payload=envelope['data']['result']
    print(entry+': '+str(payload.get('result',payload))[:700],flush=True)
    if payload.get('success') is False:raise SystemExit(6)
subprocess.run(['scp','-q','gpu5080:C:/Users/Chiro/tmp/vrc-old-web-map/episode123/frames/*.png',str(dest)+'/'],check=True)
subprocess.run(['scp','-q','gpu5080:C:/Users/Chiro/tmp/vrc-old-web-map/episode123/capture-report.json',str(dest)+'/'],check=True)
fontfile=subprocess.check_output(['fc-match','monospace','-f','%{file}'],text=True)
font=ImageFont.truetype(fontfile,17);frames=sorted(dest.glob('cue_*.png'))
sheet=Image.new('RGB',(1600,330*4),'#243644')
for i,path in enumerate(frames):
    im=Image.open(path);im.thumbnail((525,295));sheet.paste(im,((i%3)*533,(i//3)*330));ImageDraw.Draw(sheet).text(((i%3)*533+10,(i//3)*330+301),path.stem,font=font,fill='#ebe6d6')
sheet.save(dest/'contact-sheet.jpg',quality=94)
print('Review sheet:',dest/'contact-sheet.jpg',flush=True)
