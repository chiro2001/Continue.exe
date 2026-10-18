#!/usr/bin/env python3
"""Normalize reviewed historical files into timed Unity bindings and silent atlases."""
import hashlib
import io
import json
import math
from pathlib import Path
import subprocess
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'art/export/fullfilm-v4-media'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def read(path):return json.loads(path.read_text())


def films(source):
    spans=[(79.36,81.92,False),(86.4,88.96,False),(88.96,91.52,False),
           (91.52,94.72,False),(94.72,97.28,False),(97.28,99.84,False),
           (102.4,104.96,False),(107.52,111.36,True)]
    result=[]
    for clip,span in zip(source['clips'],spans):
        path=ROOT/clip['path']
        assert sha(path)==clip['sha256']
        frames=subprocess.run(['ffmpeg','-v','error','-i',str(path),'-vf','fps=10,scale=512:288',
                               '-f','rawvideo','-pix_fmt','rgb24','pipe:1'],capture_output=True,check=True).stdout
        stride=512*288*3
        count=len(frames)//stride
        if not 20<=count<=60:raise ValueError((path,count))
        atlas=Image.new('RGB',(4096,2304))
        for j in range(64):
            k=min(j,count-1)
            frame=Image.frombytes('RGB',(512,288),frames[k*stride:(k+1)*stride])
            atlas.paste(frame,(j%8*512,j//8*288))
        export=OUT/f'film-{clip["id"]}.png';atlas.save(export)
        result.append(dict(id=clip['id'],file=str(export.relative_to(ROOT)),sha256=sha(export),
                           source_file=clip['path'],source_sha256=clip['sha256'],start=span[0],end=span[1],aside=span[2],
                           fps=10,frames=count,grid=[8,8],source_frames_only=True,audio='none'))
    return result


def history(source):
    frames=[]
    for frame in source['frames']:
        p=ROOT/frame['path']
        assert sha(p)==frame['sha256']
        frames.append(dict(id=frame['id'],file=frame['path'],sha256=frame['sha256'],
                           source_seconds=frame['seconds'],depicted_date=frame['depicted_date'],date_confidence=frame['date_confidence']))
    by_id={x['id']:i for i,x in enumerate(frames)}
    # Multi-second readable holds, with short independent secondary flashbacks.
    cues=[(77.44,78.08,'F01','VRChat · 2014段'),(78.08,78.72,'F09','VRChat · 2016段'),
          (78.72,79.36,'F14','Nameplate · 2017段'),
          (79.36,81.92,'F12','VRChat · 2018段'),(81.92,83.84,'F15','VRChat · 2018段'),
          (83.84,85.12,'F16','VRChat · 2018段'),(85.12,86.40,'F17','VRChat · 2018'),
          (86.40,88.96,'F21','Udon'),(88.96,91.52,'F19','Avatar 3.0 / Action Menu'),
          (91.52,94.72,'F39','Regions'),(94.72,97.28,'F25','Unity 2019 LTS'),
          (97.28,99.84,'F26','Spookality 2021'),(99.84,102.4,'F28','Quick Menu'),
          (102.4,104.96,'F29','VRChat · 2022段'),(104.96,107.52,'F30','OSC'),
          (107.52,111.36,'F31','IK 2.0')]
    companions={
        'F01':['F02','F03'],'F09':['F11','F36'],'F14':['F18','F37'],
        'F12':['F13','F15'],'F15':['F16','F17'],'F16':['F15','F17'],'F17':['F15','F13'],
        'F21':['F19','F20'],'F19':['F20','F23'],'F39':['F40','F24'],'F25':['F38','F24'],
        'F26':['F28','F38'],'F28':['F27','F24'],'F29':['F30','F32'],'F30':['F31','F32'],'F31':['F32','F30']}
    events=[dict(start=a,end=b,frame=by_id[k],source_id=k,label=label,aside=[by_id[x] for x in companions[k]]) for a,b,k,label in cues]
    return frames,events


def emotes():
    p=ROOT/'data/fullfilm-v4-historical-emotes.json'
    data=read(p)
    items=data.get('items',data.get('emotes',data.get('assets',[])))
    result=[]
    for row in items:
        value=next((row[k] for k in ['export','export_path','display_path','path','file','local_path','local_file'] if row.get(k)),None)
        if not value:continue
        if isinstance(value,dict):value=value.get('path')
        path=Path(value);path=path if path.is_absolute() else ROOT/path
        if not path.is_file():continue
        im=Image.open(path).convert('RGBA')
        if im.width<8 or im.height<8:continue
        name='emote-'+str(row.get('id',len(result))).replace('/','-')+'.png'
        export=OUT/name;im.save(export)
        # Use the resource-specific recommended narrative window when supplied.
        span=row.get('mv_range') or row.get('mv_range_seconds') or row.get('recommended_mv_range')
        if isinstance(span,list) and len(span)==2:start,end=span
        else:
            category=str(row.get('id',''))+' '+str(row.get('family',''))+' '+str(row.get('name',''))
            start=51.2 if any(k in category.lower() for k in ['tieba','troll','rage','滑稽']) else 20.48
            end=253.44
        result.append(dict(id=row.get('id',export.stem),file=str(export.relative_to(ROOT)),
                           source_file=str(path.relative_to(ROOT)) if path.is_relative_to(ROOT) else str(path),source_sha256=sha(path),
                           sha256=sha(export),aspect=im.width/im.height,start=float(start),end=float(end)))
    if len(result)<20:raise ValueError('Need at least 20 distinct reviewed emotes, got '+str(len(result)))
    return result


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    source=read(ROOT/'data/fullfilm-v4-history-video.json')
    frames,events=history(source)
    result=dict(schema='fullfilm-v4-media-binding.1',
                input_video_manifest_sha256=sha(ROOT/'data/fullfilm-v4-history-video.json'),
                input_emote_manifest_sha256=sha(ROOT/'data/fullfilm-v4-historical-emotes.json'),
                history_frames=frames,history_events=events,films=films(source),emotes=emotes(),
                counting='One original historical video; frames and film atlases are derived views, not separate historical sources.')
    output=ROOT/'data/fullfilm-v4-media-binding.json'
    output.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(dict(success=True,frames=len(frames),history_events=len(events),silent_films=len(result['films']),emotes=len(result['emotes']))))


if __name__=='__main__':main()
