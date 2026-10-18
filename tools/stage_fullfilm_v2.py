#!/usr/bin/env python3
"""Copy only external sources and plain text; Unity performs every import."""
import json
from pathlib import Path
import subprocess

ROOT=Path(__file__).resolve().parent.parent
WIN='gpu5080:D:/Projects/VRChatProjects2/vrc-old-web-map/'

def send(files,destination):
    paths=[str(p) for p in files]
    if paths:subprocess.run(['scp',*paths,WIN+destination],check=True)

def main():
    staging=Path.home()/'tmp/vrc-old-web-map/fullfilm-v2/staging';staging.mkdir(parents=True,exist_ok=True)
    manifest=json.loads((ROOT/'data/fullfilm-v2-native-ui.json').read_text())
    crops=[]
    for group in manifest['groups']:
        for state in group['states']:
            layer=next(x for x in state['layers'] if x['id']=='secondary')
            original=ROOT/'art/export/fullfilm-v2-ui/native-overlays'/layer['crop']
            target=staging/f"native-{group['id']}-{state['index']:02d}-secondary-crop.png"
            target.write_bytes(original.read_bytes());crops.append(target)
    send([*sorted((ROOT/'art/export/fullfilm-v2-atlas').glob('source-*.png')),ROOT/'art/export/fullfilm-v2-atlas/archive-atlas.png',*crops,*sorted((ROOT/'art/export/fullfilm-v2-failure-ui').glob('*-main.png')),*sorted((ROOT/'art/export/fullfilm-v2-failure-ui').glob('*-detail.png')),*sorted((ROOT/'art/export/fullfilm-v2-software').glob('*.png'))], 'Assets/OldWebMap/FullFilm/Revision2/Textures/')
    send([ROOT/'art/models/fullfilm-v2/editor-frame/fullfilm_v2_editor_frame.fbx'],'Assets/OldWebMap/FullFilm/Revision2/Models/')
    send([ROOT/'data/fullfilm-v2-atlas.json',ROOT/'data/fullfilm-v2-native-ui.json',ROOT/'data/fullfilm-v2-failure-ui.json',ROOT/'data/fullfilm-v2-software.json'],'AgentScripts/data/')
    send(list((ROOT/'unity/Assets/OldWebMap/Scripts').glob('OWV2*.cs')),'Assets/OldWebMap/Scripts/')
    send(list((ROOT/'unity/Assets/OldWebMap/Shaders').glob('OWV2*.shader')),'Assets/OldWebMap/Shaders/')
    send(list((ROOT/'unity/AgentScripts').glob('OWV2*.cs')),'AgentScripts/')
    print(json.dumps({'copied':True,'atlas_sources':json.loads((ROOT/'data/fullfilm-v2-atlas.json').read_text())['source_count'],'native_detail_crops':len(crops),'imports_pending':True}))

if __name__=='__main__':main()
