#!/usr/bin/env python3
"""Check actual V3 content use, independent layers and original-note provenance."""
from pathlib import Path
from collections import Counter,defaultdict
import json,csv,hashlib
from PIL import Image
ROOT=Path(__file__).resolve().parent.parent
def main():
    score=json.loads((ROOT/'data/episode123-v3-score.json').read_text());images=score['images'];events=score['events']
    assert score['duration']==133.12 and score['slots']==20 and len(images)>=285
    ids={r['id'] for r in images};assert len(ids)==len(images);assert {e['image'] for e in events}==ids
    pixels=set()
    for r in images:
        p=Path(r['source_path']);assert p.exists(),p
        im=Image.open(p).convert('RGB');assert im.width>=220 and im.height>=100
        sig=hashlib.sha256(im.resize((160,120)).tobytes()).hexdigest();assert sig not in pixels,r['id'];pixels.add(sig)
        assert r['path']=='Assets/OldWebMap/Episode123/Revision3/Images/'+r['id']+'.png'
    notes=set()
    for r in csv.DictReader(open(ROOT/'audio/keil-keygen-2008/tracks.csv')):notes.add((int(r['channel'])-1,round(float(r['time_s']),5)))
    by_slot=defaultdict(list);actual=0
    for e in events:
        assert e['image'] in ids and 0<=e['slot']<20
        assert 0<=e['start']<e['end']<=133.12
        by_slot[e['slot']].append(e)
        if e['trigger']['type']=='actual-note':
            assert (e['trigger']['channel'],round(e['start'],5)) in notes,e
            assert e['channel']==e['trigger']['channel'],e
            actual+=1
    for slot,rows in by_slot.items():
        assert rows==sorted(rows,key=lambda e:e['start'])
        assert all(a['end']<=b['start']+.00001 for a,b in zip(rows,rows[1:])),slot
    assert len(score['scenes'])==13
    assert all(abs(s['start']-i*10.24)<.00001 and abs(s['end']-(i+1)*10.24)<.00001 for i,s in enumerate(score['scenes']))
    assert actual>=len(events)*.95
    starts=sorted(set(e['start'] for e in events));deltas={round(b-a,3) for a,b in zip(starts,starts[1:])};assert len(deltas)>=5
    result=dict(success=True,unique_full_content=len(images),baseline=57,multiplier=len(images)/57,events=len(events),actual_note_events=actual,distinct_start_intervals=sorted(deltas),source_kinds=dict(Counter(r['kind'] for r in images)),all_content_used=True,no_same_slot_overlap=True)
    tmp=Path.home()/'tmp/vrc-old-web-map/episode123-v3/reports';tmp.mkdir(parents=True,exist_ok=True);(tmp/'static-data-validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2));print(json.dumps({k:v for k,v in result.items() if k!='source_kinds'},ensure_ascii=False,indent=2))
if __name__=='__main__':main()
