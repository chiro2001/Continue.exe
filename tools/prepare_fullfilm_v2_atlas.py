#!/usr/bin/env python3
"""Build a provenance-linked archive atlas; no crop is counted as a new source."""
import hashlib
import json
from urllib.parse import urlparse
from collections import Counter, defaultdict
from pathlib import Path
from PIL import Image, ImageOps, ImageDraw

ROOT=Path(__file__).resolve().parent.parent
OUT=ROOT/'art/export/fullfilm-v2-atlas'
CAPACITY=128
COLS=16
ROWS=8
CELL=256
SOURCES=[('fullfilm-v2-archive-materials.json','items',True),('fullfilm-v2-public-media.json','items',True),('episode123-historical-images.json','assets',False),('episode123-software-images.json','assets',False),('episode123-v3-historical-images.json','assets',False),('fullfilm-historical-anchors.json','assets',False)]

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    candidates=[];seen=set()
    for name,key,new in SOURCES:
        path=ROOT/'data'/name
        if not path.exists():continue
        data=json.loads(path.read_text());items=data.get(key,data.get('assets',[]))
        for item in items:
            value=next((item.get(k) for k in ('local_path','path','file','local_file','downloaded_path') if item.get(k)),None)
            if not value:continue
            src=Path(value);src=src if src.is_absolute() else ROOT/src
            if not src.is_file():continue
            try:
                with Image.open(src) as im:w,h=im.size
            except Exception:continue
            banner=item.get('id','')
            if (min(w,h)<160 and banner!='btchina-homepage-2009') or w/h>5.5 or h/w>3:continue
            digest=hashlib.sha256(src.read_bytes()).hexdigest()
            if digest in seen:continue
            seen.add(digest)
            # Existing VRC image anonymisation is mandatory in performed copies.
            ident=str(item.get('id',src.stem));kind=str(item.get('category','archive'))
            if any(x in ident.lower() for x in ('vrchat','vrc-','official-2019')):
                anon=ROOT/'art/reference/episode123-v4/anonymized-display'/(ident+'.png')
                if anon.exists():src=anon
                elif 'vrchat-eac-security-banner' not in ident:continue
            candidates.append(dict(item,id=ident,original_path=str(src.relative_to(ROOT)),original_sha256=digest,newly_downloaded_in_v2=new,category=kind,original_width=w,original_height=h))
    selected=[]
    fresh=[x for x in candidates if x['newly_downloaded_in_v2']]
    selected.extend(fresh[:72])
    buckets=defaultdict(list)
    for item in candidates:
        if not item['newly_downloaded_in_v2']:buckets[item['category']].append(item)
    while len(selected)<CAPACITY and any(buckets.values()):
        for key in sorted(buckets):
            if buckets[key] and len(selected)<CAPACITY:selected.append(buckets[key].pop(0))
    atlas=Image.new('RGBA',(COLS*CELL,ROWS*CELL),(8,13,20,255));contact=Image.new('RGB',(1600,((len(selected)+7)//8)*145),(8,13,20));draw=ImageDraw.Draw(contact)
    for i,item in enumerate(selected):
        src=ROOT/item['original_path'];im=Image.open(src).convert('RGB')
        tile=ImageOps.contain(im,(CELL-6,CELL-6),Image.Resampling.LANCZOS)
        quality=item.get('visual_quality','recognizable-layout')
        weight=.20 if quality=='fragment-only' else .36 if quality.startswith('partial-') else .56 if quality.startswith('sparse-') else .68 if quality=='redacted-layout-only' else 1.0
        tile=tile.convert('RGBA');tile.putalpha(round(weight*255));item['atlas_visibility_weight']=weight
        x=(i%COLS)*CELL;y=(i//COLS)*CELL;atlas.paste(tile,(x+(CELL-tile.width)//2,y+(CELL-tile.height)//2))
        preview=ImageOps.contain(im,(196,119),Image.Resampling.LANCZOS);cx=(i%8)*200;cy=(i//8)*145
        contact.paste(preview,(cx,cy));draw.text((cx+2,cy+120),f'{i:02d} '+item['id'][:20],fill='white')
        full=ImageOps.contain(im,(1600,1200),Image.Resampling.LANCZOS);filename='source-'+str(i).zfill(2)+'.png';full.save(OUT/filename)
        item['index']=i;item['export']=str((OUT/filename).relative_to(ROOT));item['unity_path']='Assets/OldWebMap/FullFilm/Revision2/Textures/'+filename
        item['display_aspect']=full.width/full.height
        source_url=item.get('source_url') or item.get('image_url') or ''
        host=urlparse(source_url).netloc.removeprefix('www.') or item['id'].replace('-',' ')
        when=str(item.get('capture_date') or item.get('source_date') or '')
        item['display_label']=host+('  /  '+when[:4] if len(when)>=4 and when[:4].isdigit() else '')
        item['atlas_rect']=[(i%COLS)/COLS,1-(i//COLS+1)/ROWS,1/COLS,1/ROWS]
    if len(selected)<50:raise RuntimeError(f'Insufficient distinct archive images: {len(selected)}')
    atlas.save(OUT/'archive-atlas.png');contact.save(OUT/'contact.jpg',quality=92)
    # Preserve several-second readable holds. Related detail advances separately.
    preferred=[x for x in selected if x['newly_downloaded_in_v2']]
    remaining=[x for x in selected if not x['newly_downloaded_in_v2']]
    ordered=[]
    for i in range(14):
        pool=preferred if i%2==0 and preferred else remaining
        if not pool:pool=selected.copy()
        ordered.append(pool.pop(0))
    pairs=[
        ('archive-sina-portal-2004','archive-163-portal-2004'),
        ('tianya-bbs-2009','archive-mop-portal-2005'),
        ('sina-weibo-home-2009','xiaonei-home-2007'),
        ('cbn-weekly-goodnight-bt-cover-2009','btchina-homepage-2009'),
        ('somdom-thai-subtitle-recruitment-thread-2008','aegisub-translation-assistant-doc'),
        ('emule-047c-windows-xp','azureus-client-screen-2006'),
        ('wikipedia-blackout-screen-2012','wikipedia-homepage-2001'),
        ('vrchat-eac-security-banner-2022','3q-security-choice-options-2010'),
        ('irc-remote-collaboration-2013','wikimania-italian-wikipedians-2005'),
        ('archive-cnbeta-news-2006','pork-irc-client-status-2006'),
    ]
    by_id={x['id']:x for x in selected}
    events=[]
    for i,item in enumerate(ordered):
        related=next((x for x in selected if x['index']!=item['index'] and x['category']==item['category']),selected[(item['index']+5)%len(selected)])
        if i<len(pairs):
            item=by_id.get(pairs[i][0],item);related=by_id.get(pairs[i][1],related)
        events.append(dict(start=round(133.12+i*3.84,2),end=round(min(186.88,133.12+(i+1)*3.84),2),primary=item['index'],secondary=related['index'],file=item['id'],source_date=item.get('capture_date') or item.get('source_date') or '',kind=item.get('kind') or item.get('asset_kind') or 'source-image'))
    source_pages={x.get('source_page_url') or x.get('source_url') or x.get('image_url') or x['original_path'] for x in selected}
    manifest=dict(schema='fullfilm-v2-archive-atlas.1',source_count=len(selected),source_page_count=len(source_pages),atlas_columns=COLS,atlas_rows=ROWS,new_downloaded_sources=sum(x['newly_downloaded_in_v2'] for x in selected),categories=dict(Counter(x['category'] for x in selected)),counting='source_count counts distinct original image file hashes; source_page_count separately counts source pages. Prepared sizes, crops, repeated instances, focus/detail pairs do not add to either count.',atlas='art/export/fullfilm-v2-atlas/archive-atlas.png',sources=selected,focus_events=events)
    (ROOT/'data/fullfilm-v2-atlas.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({k:v for k,v in manifest.items() if k not in ('sources','focus_events')},ensure_ascii=False))

if __name__=='__main__':main()
