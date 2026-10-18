#!/usr/bin/env python3
"""Batch-copy new V3 content; Unity Pipeline owns asset import and persistence."""
from pathlib import Path
import json,subprocess,sys
from PIL import Image
sys.path.insert(0,str(Path(__file__).resolve().parent));import unity_remote as remote
ROOT=Path(__file__).resolve().parent.parent
TMP=Path.home()/'tmp/vrc-old-web-map/episode123-v3/image-import';TMP.mkdir(parents=True,exist_ok=True)
BASE='D:/Projects/VRChatProjects2/vrc-old-web-map'
def copy(paths,directory):
    if paths:subprocess.run(['scp','-q',*[str(p) for p in paths],'gpu5080:'+BASE+'/'+directory+'/'],check=True)
def main():
    if '--images-only' not in sys.argv:
        copy(list((ROOT/'unity/Assets/OldWebMap/Scripts').glob('OWV3*.cs'))+[ROOT/'unity/Assets/OldWebMap/Scripts/OWEpisodeMusic.cs'],'Assets/OldWebMap/Scripts')
        copy(list((ROOT/'unity/Assets/OldWebMap/Shaders').glob('OWV3*.shader')),'Assets/OldWebMap/Shaders')
        copy(list((ROOT/'unity/AgentScripts').glob('OWEpisodeV3*.cs')),'AgentScripts')
        copy([ROOT/'data'/f for f in ['episode123-v3-score.json','episode123-v3-waveforms.json','episode123-audio.json']],'AgentScripts/data')
    if '--scripts-only' not in sys.argv:
        rows=json.loads((ROOT/'data/episode123-v3-score.json').read_text())['images'];files=[]
        for r in rows:
            p=TMP/(r['id']+'.png');im=Image.open(r['source_path']).convert('RGB');im.thumbnail((1600,1600));im.save(p);files.append(p)
        for start in range(0,len(files),70):copy(files[start:start+70],'Assets/OldWebMap/Episode123/Revision3/Images')
        copy([Path.home()/'tmp/vrc-old-web-map/episode123-v3/wave-fx/waveform_rgba.png'],'Assets/OldWebMap/Episode123/Revision3/Data')
    print('V3 source batch copied; import/build/validation still belongs to Pipeline.',flush=True)
if __name__=='__main__':main()
