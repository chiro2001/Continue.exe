#!/usr/bin/env python3
"""Stereo reference mix for the review film; headset HRTF remains in Unity."""
from pathlib import Path
import json
import numpy as np
import soundfile as sf

ROOT=Path(__file__).resolve().parent.parent
TMP=Path.home()/'tmp/vrc-old-web-map/episode123'
sr=48000;show=int(133.12*sr);total=int(135.04*sr)
time=np.arange(show,dtype=np.float32)/sr
def smooth(x):x=np.clip(x,0,1);return x*x*(3-2*x)
opening=smooth((time-81.92)/2.56)*(1-smooth((time-89.6)/2.56))
mix=np.zeros((total,2),dtype=np.float32)
for i in range(4):
    wave,rate=sf.read(TMP/'audio'/f'FC14_Mono_{i+1:02}.wav',dtype='float32');assert rate==sr
    x=np.full(show,(i-1.5)*.8,dtype=np.float32);z=np.full(show,6,dtype=np.float32)
    if i in [0,3]:
        x=x*(1-opening)+(-3.8 if i==0 else 3.8)*opening;z=z*(1-opening)+5.3*opening
    pan=np.sin(np.arctan2(x,z));angle=(pan+1)*np.pi/4
    left=.88*np.cos(angle)+.12/np.sqrt(2);right=.88*np.sin(angle)+.12/np.sqrt(2)
    mix[:show,0]+=wave[:show]*left*.8;mix[:show,1]+=wave[:show]*right*.8
foley=json.loads((ROOT/'data/episode123-foley.json').read_text())
for event in foley['events']:
    wave,rate=sf.read(TMP/'foley'/foley['files'][event['clip']],dtype='float32');assert rate==sr
    at=round(event['time']*sr);n=min(len(wave),show-at)
    pan=-.28 if event['emitter']==1 else 0;angle=(pan+1)*np.pi/4
    level=foley['volume']*(.9 if event['emitter']==1 else .73)
    mix[at:at+n,0]+=wave[:n]*np.cos(angle)*level;mix[at:at+n,1]+=wave[:n]*np.sin(angle)*level
fade=round(.3*sr);mix[show-fade:show]*=(1-smooth(np.linspace(0,1,fade,dtype=np.float32)))[:,None]
peak=float(np.max(np.abs(mix)));assert peak<1,peak
dest=TMP/'review';dest.mkdir(parents=True,exist_ok=True)
sf.write(dest/'review-audio.wav',mix,sr,subtype='PCM_16')
(dest/'audio-reference.json').write_text(json.dumps({'sampleRate':sr,'samples':total,'seconds':135.04,'showSeconds':133.12,'tailSeconds':1.92,'peak':peak,'kind':'fixed-viewpoint equal-power stereo reference with original Foley; does not reproduce headset HRTF or player position changes'},ensure_ascii=False,indent=2)+'\n')
print('Review stereo mix:',dest/'review-audio.wav','peak',round(peak,4))
