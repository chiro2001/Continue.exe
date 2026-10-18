#!/usr/bin/env python3
"""Fixed-listener stereo review of V3's actual fused/orbit trajectories, no HRTF claim."""
from pathlib import Path
import numpy as np,json,soundfile as sf
ROOT=Path(__file__).resolve().parent.parent;TMP=Path.home()/'tmp/vrc-old-web-map'
DEST=TMP/'episode123-v3/review';DEST.mkdir(parents=True,exist_ok=True)
sr=48000;count=round(133.12*sr);total=round(135.04*sr);t=np.arange(count,dtype=np.float32)/sr
def ease(x):x=np.clip(x,0,1);return x*x*(3-2*x)
opening=ease((t-81.92)/2.56)*(1-ease((t-89.6)/2.56));mix=np.zeros((total,2),np.float32)
for ch in range(4):
    wave,rate=sf.read(TMP/'episode123/audio'/f'FC14_Mono_{ch+1:02}.wav',dtype='float32');assert rate==sr
    phase=ch*np.pi/2;angle=phase+(t-84.48)*.46
    frontX=np.sin(t*.31+phase)*.22;frontZ=np.full(count,8.2+(ch-1.5)*.13+2.8,np.float32)
    x=frontX*(1-opening)+np.sin(angle)*4.2*opening;z=frontZ*(1-opening)+np.cos(angle)*4.2*opening
    pan=np.sin(np.arctan2(x,z));blend=.55+.45*opening;equalAngle=(pan+1)*np.pi/4
    left=(1-blend)/np.sqrt(2)+blend*np.cos(equalAngle);right=(1-blend)/np.sqrt(2)+blend*np.sin(equalAngle)
    mix[:count,0]+=wave[:count]*left*.8;mix[:count,1]+=wave[:count]*right*.8
foley=json.loads((ROOT/'data/episode123-foley.json').read_text())
for e in foley['events']:
    wave,rate=sf.read(TMP/'episode123/foley'/foley['files'][e['clip']],dtype='float32');assert rate==sr
    start=round(e['time']*sr);n=min(len(wave),count-start);pan=-.28 if e['emitter']==1 else 0;angle=(pan+1)*np.pi/4;volume=foley['volume']*(.9 if e['emitter']==1 else .73)
    mix[start:start+n,0]+=wave[:n]*np.cos(angle)*volume;mix[start:start+n,1]+=wave[:n]*np.sin(angle)*volume
fade=round(.3*sr);mix[count-fade:count]*=(1-ease(np.linspace(0,1,fade,dtype=np.float32)))[:,None]
peak=float(np.abs(mix).max());assert peak<1,peak;sf.write(DEST/'review-audio.wav',mix,sr,subtype='PCM_16')
(DEST/'audio-reference.json').write_text(json.dumps(dict(kind='Equal-power stereo reference derived from V3 waveform/source world trajectories at one fixed listener. Not headset HRTF or a captured VRChat audio stream.',sample_rate=sr,show_seconds=133.12,review_seconds=135.04,peak=peak,normal_spatial_blend=.55,climax_spatial_blend=1),ensure_ascii=False,indent=2))
print('Stereo reference',DEST/'review-audio.wav','peak',peak)
