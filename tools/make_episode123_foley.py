#!/usr/bin/env python3
"""Original, quiet non-vocal interface/handling sounds; no sampled system audio."""
from pathlib import Path
import json
import numpy as np
import soundfile as sf

ROOT=Path(__file__).resolve().parent.parent
DEST=Path.home()/'tmp/vrc-old-web-map/episode123/foley';DEST.mkdir(parents=True,exist_ok=True)
rate=48000;rng=np.random.default_rng(731)
def make(name,duration,kind):
    t=np.arange(round(duration*rate))/rate
    noise=rng.normal(0,1,len(t));noise=np.convolve(noise,[.25,.5,.25],mode='same')
    if kind==0:wave=(noise*.5+np.sin(2*np.pi*1450*t)*.2)*np.exp(-t*95)
    elif kind==1:wave=(noise*.45+np.sin(2*np.pi*340*t)*.25)*np.exp(-t*75)
    elif kind==2:wave=np.sin(2*np.pi*950*t)*np.exp(-t*35)+np.sin(2*np.pi*1450*t)*np.exp(-t*65)*.16
    elif kind==3:wave=noise*np.sin(np.pi*t/duration)**2*np.exp(-t*5)
    else:
        phase=2*np.pi*(480*t+540*t*t)
        wave=(np.sin(phase)*.20+noise*.13)*(np.sin(np.pi*t/duration)**2)
    wave=wave/max(np.max(np.abs(wave)),1e-9)*.42
    wave[:120]*=np.linspace(0,1,120);wave[-120:]*=np.linspace(1,0,120)
    sf.write(DEST/(name+'.wav'),wave.astype(np.float32),rate,subtype='PCM_16')
for name,secs,kind in [('mouse_click',.07,0),('keyboard_tap',.065,1),('notification',.15,2),('page_flip',.24,3),('connection',.65,4)]:make(name,secs,kind)
events=[(1.28,4,0),(6.4,0,0),(11.52,0,1),(12.8,1,1),(13.12,1,1),(13.44,1,1),(17.92,2,1),(23.04,0,0),(25.6,1,0),(30.72,3,0),(35.84,3,0),(38.4,2,0),(43.52,0,0),(46.08,2,0),(52.48,0,0),(58.88,0,0),(66.56,2,0),(72.96,0,0),(76.8,2,0),(79.36,0,0),(83.2,3,0),(93.44,2,0),(98.56,2,0),(103.68,0,0),(108.8,0,0),(115.2,2,0),(123.52,0,1),(126.08,1,1),(128.64,1,1),(131.2,0,1)]
data={'version':1,'authorship':'Original procedural interface and handling Foley, not recordings of Windows or VRChat system sounds.','files':['mouse_click.wav','keyboard_tap.wav','notification.wav','page_flip.wav','connection.wav'],'events':[{'time':t,'clip':clip,'emitter':emitter} for t,clip,emitter in events],'emitters':['main-content-position','left-keyboard'],'volume':.13,'directory':str(DEST)}
(ROOT/'data/episode123-foley.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
print('5 original Foley clips,',len(events),'events:',DEST)
