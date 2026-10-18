#!/usr/bin/env python3
"""Crop actual Windows application captures; never redraw an application UI."""
import hashlib
import json
from pathlib import Path
from PIL import Image,ImageOps
import numpy as np

ROOT=Path(__file__).resolve().parent.parent
CAP=Path.home()/'tmp/vrc-old-web-map/fullfilm-v3/software'
OUT=ROOT/'art/export/fullfilm-v3-software'

def main():
    OUT.mkdir(parents=True,exist_ok=True);records=[]
    def crop(source,name,rect=None,aspect=None):
        src=CAP/source;im=Image.open(src).convert('RGB');w,h=im.size
        box=(0,0,w,h) if rect is None else tuple(round(x*(w if i%2==0 else h)) for i,x in enumerate(rect))
        result=im.crop(box)
        if aspect:
            sw,sh=result.size
            target=(max(sw,round(sh*aspect)),max(sh,round(sw/aspect)))
            result=ImageOps.pad(result,target,Image.Resampling.LANCZOS,color=(30,33,37))
        result.thumbnail((2048,1536),Image.Resampling.LANCZOS)
        path=OUT/(name+'.png');result.save(path)
        records.append(dict(id=name,file=path.name,source=str(src),source_sha256=hashlib.sha256(src.read_bytes()).hexdigest(),crop_px=box,dimensions=list(result.size),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),operation='Actual capture crop, optional aspect-preserving letterbox; no UI drawn'))
    unity='unity-scene-block-editor-actual.png'
    blender='blender-scene-block-actual.png'
    wire='blender-scene-wire-actual.png'
    crop(unity,'software-unity-editor')
    crop(blender,'software-blender-material')
    crop(wire,'software-blender-wire')
    crop(unity,'software-unity-hierarchy',(.648,.060,.819,.610),.44/.7)
    crop(unity,'software-unity-inspector',(.819,.060,1,.74),.46/.7)
    crop(blender,'software-blender-timeline',(0,.904,.822,.977),1.2/.14)
    crop(unity,'software-frame-title',(0,0,.932,.032),3.8/.058)
    crop(unity,'software-frame-toolbar-idle',(.31,.035,.69,.072),3.18/.15)
    crop(unity,'software-frame-hierarchy',(.648,.060,.691,.718),.265/2.16)
    crop(unity,'software-frame-inspector',(.819,.060,.862,.718),.265/2.16)
    crop(blender,'software-blender-viewport',(0,.07,.82,.90),16/9)
    crop(wire,'software-blender-wire-viewport',(0,.07,.82,.90),16/9)
    crop(unity,'software-unity-viewport',(0,.09,.645,.86),16/9)
    play=CAP/'unity-play-world-actual.png'
    def verified(path):
        meta=path.with_suffix('.json')
        return meta.exists() and json.loads(meta.read_text()).get('windowVerified') is True
    if not play.exists() or not verified(play):raise RuntimeError('Actual V3 Play screenshot required; no source fallback')
    if play.exists():
        crop(play.name,'software-unity-play')
        crop(play.name,'software-frame-toolbar-playing',(.31,.035,.69,.072),3.18/.15)
    else:
        # A visible source fallback while waiting for the actual runtime capture.
        crop(unity,'software-unity-play')
        crop(unity,'software-frame-toolbar-playing',(.31,.035,.69,.072),3.18/.15)
    timeline=[dict(time=197.12,file='software-unity-editor.png'),dict(time=202.24,file='software-blender-material.png'),dict(time=204.8,file='software-blender-wire.png'),dict(time=207.36,file='software-unity-editor.png'),dict(time=212.48,file='software-unity-play.png')]
    for i,label in enumerate(('unity-play-miniature-actual.png','unity-play-expansion-actual.png','unity-play-world-actual.png')):
        if (CAP/label).exists() and verified(CAP/label):
            name='software-runtime-stage-'+str(i);crop(label,name);timeline.append(dict(time=235.52+i*2.56,file=name+'.png'))
    if play.exists():timeline.extend([dict(time=243.2,file='software-blender-wire.png'),dict(time=245.76,file='software-unity-play.png'),dict(time=248.32,file='software-unity-editor.png')])
    pixels=np.array(Image.open(OUT/'software-frame-toolbar-playing.png').convert('RGB'),dtype=float)
    red,green,blue=[pixels[:,:,i] for i in range(3)]
    mask=(blue>60)&(blue>red*1.3)&(green>red*1.1)&(blue>green*.95)
    yy,xx=np.where(mask)
    if len(xx)<100:raise RuntimeError('Actual active Play control could not be located')
    box=[int(xx.min()),int(yy.min()),int(xx.max())+1,int(yy.max())+1]
    uv=[float((xx.min()+xx.max())*.5/pixels.shape[1]),float(1-(yy.min()+yy.max())*.5/pixels.shape[0])]
    if not .3<uv[0]<.7:raise RuntimeError('Play control outside expected actual toolbar')
    data=dict(schema='fullfilm-v3-real-software.1',captures_are_actual_windows_pixels=True,play_capture_ready=play.exists(),source_geometry='art/models/fullfilm-v3/fullfilm_v3_scene_block.blend / .fbx',records=records,timeline=timeline,play_control_uv=uv,play_control_bbox_px=box,play_control_method='Blue active Play button bounds in actual captured toolbar, image V inverted for Unity UV')
    (ROOT/'data/fullfilm-v3-software.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(dict(images=len(records),play_capture_ready=play.exists(),output=str(OUT)),ensure_ascii=False))

if __name__=='__main__':main()
