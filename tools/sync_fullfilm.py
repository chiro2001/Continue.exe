#!/usr/bin/env python3
"""Copy only new full-film sources and exports; Unity Pipeline handles import/assets."""
import json
import subprocess
from pathlib import Path
import unity_remote as remote

ROOT=Path(__file__).resolve().parent.parent
BASE='D:/Projects/VRChatProjects2/vrc-old-web-map'
TARGET='Assets/OldWebMap/FullFilm/Revision1'

def copy(files,target):
    files=[str(p) for p in files if p.is_file()]
    for i in range(0,len(files),45):
        subprocess.run(['scp','-q',*files[i:i+45],'gpu5080:'+BASE+'/'+target+'/'],check=True)
    return len(files)

def main():
    sets=[(sorted((ROOT/'unity/AgentScripts').glob('OWFull*.cs')),'AgentScripts'),
          (sorted((ROOT/'unity/Assets/OldWebMap/Scripts').glob('OWFull*.cs')),'Assets/OldWebMap/Scripts'),
          (sorted((ROOT/'unity/Assets/OldWebMap/Shaders').glob('OWFull*.shader')),'Assets/OldWebMap/Shaders'),
          (sorted((ROOT/'data').glob('fullfilm*.json')),'AgentScripts/data'),
          (sorted((ROOT/'art/export/fullfilm/overload').glob('*.png')),TARGET+'/Overload/Textures'),
          (sorted((ROOT/'art/export/fullfilm/renewal').glob('*.png')),TARGET+'/Renewal/Textures'),
          (sorted((ROOT/'art/reference/fullfilm-anchors').glob('*-display.png')),TARGET+'/History/Textures'),
          (sorted((Path.home()/'tmp/vrc-old-web-map/fullfilm-v1/signal').glob('*.png')),TARGET+'/Signal/Textures')]
    for _,folder in sets:
        result=remote.run_pwsh('New-Item -ItemType Directory -Force -Path '+remote.pwsh_literal(BASE+'/'+folder)+' | Out-Null',capture_output=True)
        if result.returncode:raise RuntimeError(result.stderr.decode('utf-8','replace'))
    results={target:copy(files,target) for files,target in sets}
    print(json.dumps(results,ensure_ascii=False))

if __name__=='__main__':main()
