#!/usr/bin/env python3
"""Prepare aligned mono stems and note pulses without rewriting FC14 composition."""
from pathlib import Path
import csv
import json
import numpy as np
import soundfile as sf

ROOT=Path(__file__).resolve().parent.parent
TMP=Path.home()/'tmp/vrc-old-web-map/episode123'
SOURCE=Path.home()/'tmp/vrc-old-web-map/keygen-music/fc14-renders'
DEST=TMP/'audio'
DEST.mkdir(parents=True,exist_ok=True)
rate=48000
length=256*rate
stems=[]
dc=[]
for i in range(1,5):
    x,sr=sf.read(SOURCE/f'ch{i:02}.wav',dtype='float32',always_2d=True)
    assert sr==rate and len(x)>=length
    mono=x[:length].mean(axis=1,dtype=np.float64).astype(np.float32)
    offset=float(mono.mean(dtype=np.float64));dc.append(offset);stems.append(mono-offset)
mix=np.sum(stems,axis=0)
peak=max(float(np.max(np.abs(mix))),*(float(np.max(np.abs(x))) for x in stems))
gain=min(1.0,0.90/max(peak,1e-9))
ramp=np.linspace(0,1,240,dtype=np.float32)
for i,x in enumerate(stems,1):
    x=x*gain;x[:240]*=ramp;x[-240:]*=ramp[::-1]
    sf.write(DEST/f'FC14_Mono_{i:02}.wav',x,rate,subtype='PCM_16')
sf.write(DEST/'FC14_Review_Mono.wav',mix*gain,rate,subtype='PCM_16')
notes=[[],[],[],[]]
with (ROOT/'audio/keil-keygen-2008/tracks.csv').open() as f:
    for r in csv.DictReader(f):
        if float(r['time_s'])<133.12:notes[int(r['channel'])-1].append(round(float(r['time_s']),6))
envelopes=[]
for x in stems:
    usable=x[:int(133.12*rate)].reshape(-1,3840).astype(np.float64)
    usable=usable-usable.mean(axis=1,keepdims=True)
    envelopes.append(np.sqrt(np.mean(usable**2,axis=1)))
reference=max(float(np.percentile(x,95)) for x in envelopes)
envelopes=[np.round(np.clip(x/max(reference,1e-9),0,1),4).tolist() for x in envelopes]
report={'envelopes':envelopes,'sampleRate':rate,'samples':length,'duration':256,'preview_end':133.12,'source_dc_offsets':dc,'commonGain':gain,'preGainPeak':peak,'method':'Each stereo stem collapsed by arithmetic mean; constant DC removed; common gain preserves inter-stem balance; identical 5 ms edge ramps; exact logical cycle excludes next-loop tail.','notes':notes,'outputs':[f'FC14_Mono_{i:02}.wav' for i in range(1,5)]}
(ROOT/'data/episode123-audio.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
(TMP/'audio-preparation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print('4 aligned mono stems prepared:',DEST,'gain',round(gain,6),'DC',[round(v,5) for v in dc])
