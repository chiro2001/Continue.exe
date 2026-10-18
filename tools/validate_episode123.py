#!/usr/bin/env python3
"""Validate the Episode123 source package and optionally the actual runtime evidence."""
from pathlib import Path
from decimal import Decimal
import argparse
import json
from PIL import Image
import soundfile as sf

ROOT=Path(__file__).resolve().parent.parent
TMP=Path.home()/'tmp/vrc-old-web-map/episode123'
parser=argparse.ArgumentParser();parser.add_argument('--reports',action='store_true');args=parser.parse_args()
def load(name):return json.loads((ROOT/'data'/name).read_text())
for p in (ROOT/'data').glob('*.json'):json.loads(p.read_text())
score=load('scene-score.json');scenes=[s for s in score['scenes'] if s['section'] in ['connect','create','constrain']]
assert len(scenes)==12
cursor=Decimal(0)
for s in scenes:
    assert Decimal(str(s['start']))==cursor,s['id'];cursor=Decimal(str(s['end']))
assert cursor==Decimal('133.12')
art=load('episode123-art.json');ids=set()
for a in art['assets']:
    assert a['id'] not in ids,a['id'];ids.add(a['id'])
    assert (ROOT/a['source']).is_file(),a['source']
    assert Image.open(ROOT/a['export']).size==(a['width'],a['height']),a['id']
assert len(ids)==40
audio=load('episode123-audio.json');assert audio['sampleRate']==48000 and audio['samples']==12288000
for name in audio['outputs']:
    info=sf.info(TMP/'audio'/name);assert info.frames==12288000 and info.channels==1 and info.samplerate==48000,name
assert len(audio['notes'])==len(audio['envelopes'])==4
for notes,envelope in zip(audio['notes'],audio['envelopes']):
    assert notes==sorted(notes) and all(0<=n<133.12 for n in notes)
    assert len(envelope)==1664 and all(0<=v<=1 for v in envelope)
foley=load('episode123-foley.json');assert len(foley['files'])==5
assert [e['time'] for e in foley['events']]==sorted(e['time'] for e in foley['events'])
for e in foley['events']:assert 0<=e['time']<133.12 and 0<=e['clip']<5 and 0<=e['emitter']<2
for name in foley['files']:
    info=sf.info(TMP/'foley'/name);assert info.channels==1 and info.samplerate==48000
shader=(ROOT/'unity/Assets/OldWebMap/Shaders/OWPageSurface.shader').read_text()
for marker in ['UNITY_VERTEX_INPUT_INSTANCE_ID','UNITY_SETUP_INSTANCE_ID','UNITY_VERTEX_OUTPUT_STEREO','UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO','UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX','STEREO_INSTANCING_ON']:
    assert marker in shader,marker
for name in ['OWEpisodeDirector','OWEpisodeMusic','OWEpisodeStage','OWEpisodeButton','OWEpisodeFoley']:
    assert (ROOT/'unity/Assets/OldWebMap/Scripts'/(name+'.cs')).is_file()
if args.reports:
    folder=TMP/'validation'
    evidence=json.loads((folder/'live-endurance.json').read_text())
    assert evidence['success'] and evidence['cuesSeen']==12 and evidence['endCard']
    assert evidence['maxIntertrackSamples']<=960 and evidence['maxListenerRms']>.0001
    assert all(v>.0001 for v in evidence['maxSourceRms'])
    checks=json.loads((folder/'art-import-check.json').read_text());assert len(checks)==40 and all(c['exact'] and c['npot']=='None' for c in checks)
    stereo=json.loads((folder/'viewpoint-shader-checks.json').read_text());assert stereo['isSupported'] and all(v['added'] for v in stereo['variantResults'])
    assert not any(m['severity']=='Error' for m in stereo['messages'])
    controls=json.loads((folder/'live-buttons.json').read_text());assert len(controls)==6 and controls[0]['playing'] and not controls[1]['playing']
print('Episode123 source assets, timing, mono stems, note/envelope data, Foley and stereo source checks: OK')
if args.reports:print('Actual continuous Udon run, audio output/synchronization, controls and pixel/shader evidence: OK')
