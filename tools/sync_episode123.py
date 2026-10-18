#!/usr/bin/env python3
"""Copy only owned Episode123 source files; Unity imports/settings remain Pipeline work."""
import importlib.util
from pathlib import Path
import subprocess
import sys

ROOT=Path(__file__).resolve().parent.parent
spec=importlib.util.spec_from_file_location('unity_remote',ROOT/'tools/unity_remote.py')
remote=importlib.util.module_from_spec(spec);spec.loader.exec_module(remote)
base='D:/Projects/VRChatProjects2/vrc-old-web-map'
folders=['Assets/OldWebMap/Episode123/Art','Assets/OldWebMap/Episode123/Audio','Assets/OldWebMap/Scripts','Assets/OldWebMap/Shaders','AgentScripts/data']
cmd='; '.join('New-Item -ItemType Directory -Force -Path '+remote.pwsh_literal(base+'/'+p)+' | Out-Null' for p in folders)
assert remote.run_pwsh(cmd).returncode==0
def copy(files,target):
    if not files:return
    result=subprocess.run(['scp','-q',*[str(p) for p in files],'gpu5080:'+base+'/'+target+'/'])
    if result.returncode:raise SystemExit(result.returncode)

if '--art-only' not in sys.argv:
    copy(list((ROOT/'unity/Assets/OldWebMap/Scripts').glob('OWEpisode*.cs')),'Assets/OldWebMap/Scripts')
    copy([ROOT/'unity/Assets/OldWebMap/Shaders/OWPageSurface.shader'],'Assets/OldWebMap/Shaders')
    if '--defer-builder' not in sys.argv:copy([ROOT/'unity/AgentScripts/OWEpisodeBuild.cs'],'AgentScripts')
copy([ROOT/'data/scene-score.json',ROOT/'data/episode123-art.json',ROOT/'data/episode123-audio.json',ROOT/'data/episode123-foley.json'],'AgentScripts/data')
if '--scripts-only' not in sys.argv:copy(list((ROOT/'art/export/episode123').glob('*.png')),'Assets/OldWebMap/Episode123/Art')
if '--audio' in sys.argv:
    copy(list((Path.home()/'tmp/vrc-old-web-map/episode123/audio').glob('FC14_Mono_*.wav')),'Assets/OldWebMap/Episode123/Audio')
if '--foley' in sys.argv:copy(list((Path.home()/'tmp/vrc-old-web-map/episode123/foley').glob('*.wav')),'Assets/OldWebMap/Episode123/Audio')
print('Episode123 owned source batch copied; refresh and validate with Pipeline.')
