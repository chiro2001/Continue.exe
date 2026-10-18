#!/usr/bin/env python3
"""Chronological recognition anchors, fast side layers, and retained memories."""
from pathlib import Path
from collections import defaultdict,Counter
from PIL import Image
import json,csv,hashlib,re,math
ROOT=Path(__file__).resolve().parent.parent
TMP=Path.home()/'tmp/vrc-old-web-map/episode123-v4';TMP.mkdir(parents=True,exist_ok=True)
def read(p):return json.loads((ROOT/p).read_text())
def write(p,data):p.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
def region(t):return 0 if t<79.36 else 1 if t<86.4 else 2 if t<102.4 else 3
def offset(r):return [0,24,52,80][r]
def main():
    old=read('data/episode123-v3-score.json');audit={r['id']:r for r in read('data/episode123-v4-chronology-audit.json')['items']}
    workflows={r['id']:r for r in read('data/episode123-v4-workflows.json')['assets']}
    images=[];excluded=[];seen=set();ranges={};periods={}
    def add(r,span,year='',mode='chronological'):
        r=dict(r);p=Path(r.get('source_path',r.get('local_path',r.get('path',''))));p=p if p.is_absolute() else ROOT/p
        if not p.is_file():return False
        try:im=Image.open(p).convert('RGB')
        except Exception:return False
        sig=hashlib.sha256(im.resize((160,120)).tobytes()).hexdigest()
        if sig in seen:excluded.append(dict(id=r.get('id'),reason='duplicate complete image'));return False
        seen.add(sig);r.update(source_path=str(p),width=im.width,height=im.height,sha256=hashlib.sha256(p.read_bytes()).hexdigest(),placement_mode=mode,display_year=year)
        if not str(r.get('path','')).startswith('Assets/') or '/episode123-v4-' in str(r.get('path','')):r['path']='Assets/OldWebMap/Episode123/Revision4/Images/'+r['id']+'.png'
        images.append(r);ranges[r['id']]=span;periods[r['id']]=year;return True
    workflow_ranges={
        'dial':[0,10.24],'explorer':[10.24,20.48],'portable':[10.24,20.48],'winamp':[20.48,30.72],
        'homepage':[10.24,30.72],'paint':[20.48,30.72],'manga':[30.72,40.96],'game':[30.72,40.96],
        'subtitles':[61.44,71.68],'typeset':[61.44,71.68],'torrent':[40.96,51.2],'emule':[40.96,51.2],
        'release':[40.96,51.2],'bbs':[92.16,102.4],'webring':[10.24,30.72],'qq':[30.72,40.96],
        'msn':[30.72,40.96],'mail':[20.48,30.72],'rss':[51.2,61.44],'tianya':[51.2,61.44],
        'xiaonei':[51.2,61.44],'renren':[51.2,61.44],'weibo':[51.2,61.44],'tieba':[30.72,40.96],
        'blog':[51.2,61.44],'danmaku':[61.44,81.92],'flash':[20.48,40.96],'opensource':[92.16,102.4],
        'world':[71.68,79.36],'avatar':[71.68,79.36],'vrc17':[81.92,86.4],'vrc19':[88.32,102.4],
        'virus':[92.16,102.4],'3q':[105,112.64],'eac':[112.64,122.88],'maintain':[122.88,133.12]}
    for original in old['images']:
        r=dict(original);a=audit[r['id']];span=a.get('recommended_time_range_s');year='';mode='chronological'
        r['title']=a.get('corrected_title') or r.get('title','');title=r['title'];wf=r.get('workflow');year=a.get('display_year') or ''
        if span is not None and span[0]>=122.88:mode='archive-recall'
        if wf:
            fresh=workflows[r['id']];r.update(fresh);r['source_path']=str(ROOT/fresh['path']);r['path']='Assets/OldWebMap/Episode123/Revision4/Images/'+r['id']+'.png';r['source_url']='spec/36-episode123-v4-production.md';span=workflow_ranges[wf];mode='authored-period-operation'
            year='2010' if wf=='3q' else '2022' if wf=='eac' else ''
        else:
            if 'Windows XP' in title or r['id'].startswith('xp-'):year='2001';span=[20.48,30.72]
            elif 'Windows 98' in title:year='1998';span=[10.24,20.48]
            elif 'Windows 95' in title:year='1997' if 'OSR2' in title else '1995';span=[0,10.24]
            else:
                match=re.search(r'\b(199\d|200\d|201\d|202[0-6])\b',title)
                if match:year=match.group(1)
            if r['id'].startswith('vrc-2017-'):year='2017';span=[81.92,86.4]
            if r['id'].startswith('official-2019-'):year='2019';span=[88.32,102.4]
            if r.get('category')=='vrc' and '2018' in str(r.get('source_date','')):year='2018';span=[83.2,86.4]
            if r['kind']=='current-project-self-capture':span=[128.0,133.12];year='现在';mode='present-production'
            if span is None:span=[122.88,133.12];year='';mode='archive-recall'
            if '3Q' in title:year='2010';span=[105,112.64];mode='dated-recall'
            if 'EAC' in title or 'Security Update' in title:year='2022';span=[112.64,122.88]
        r['chronology_evidence']=a.get('evidence_url');r['chronology_note']=a.get('recommendation_reason');add(r,span,year,mode)
    extra_file=ROOT/'data/episode123-v4-conditions-sources.json'
    if extra_file.exists():
        extra=json.loads(extra_file.read_text());rows=extra.get('assets',extra.get('sources',extra.get('items',[])))
        for raw in rows:
            p=raw.get('local_path') or raw.get('path') or raw.get('image_path')
            if not p:continue
            r=dict(raw);r.update(source_path=p,path='Assets/OldWebMap/Episode123/Revision4/Images/'+r['id']+'.png',title=r.get('readable_title') or r.get('title') or r.get('topic') or r['id'],kind=r.get('kind') or r.get('source_kind') or 'verified-public-source',category='conditions',source_url=r.get('source_url') or r.get('original_url') or r.get('url') or '')
            text=' '.join(str(raw.get(k,'')) for k in ['content_or_event_date','source_publication_date','event_date','event_year','publication_date','topic','title']);match=re.search(r'\b(20\d\d)\b',text);year=match.group(1) if match else ''
            span=[105,112.64] if '2010' in text or '3q' in text.lower() or '3Q' in text else [108.0,112.64] if year and int(year)<2017 else [110.0,113.0] if year and int(year)<2022 else [112.64,122.88] if year and int(year)<=2022 else [122.88,133.12]
            add(r,span,year,'dated-recall' if span[0]==105 else 'chronological')
    by_id={r['id']:r for r in images};notes=[]
    for r in csv.DictReader(open(ROOT/'audio/keil-keygen-2008/tracks.csv')):
        t=float(r['time_s'])
        if t<133.12:notes.append(dict(time=t,channel=int(r['channel'])-1,pattern=int(r['pattern']),row=int(r['row']),instrument=int(r['instrument'])))
    def snap(t,ch,lo,hi):
        candidates=[n for n in notes if lo<=n['time']<=hi and n['channel']==ch]
        if not candidates or min(abs(n['time']-t) for n in candidates)>.32:candidates=[n for n in notes if lo<=n['time']<=hi]
        if not candidates:return max(lo,min(hi,t)),dict(type='phrase-boundary',channel=ch)
        n=min(candidates,key=lambda n:abs(n['time']-t));return n['time'],dict(n,type='actual-note')
    events=[];busy=defaultdict(list)
    def free(slot,start,end):return all(end<=a+.00001 or start>=b-.00001 for a,b in busy[slot])
    def emit(id,slot,start,end,ch,trigger,focus=False,hold=0):
        r=by_id[id];reg=region(start);z=offset(reg)
        if focus:
            width=4.0;height=width*r['height']/r['width']
            if height>2.75:width*=2.75/height;height=2.75
            target=[0,2.65,5.4+z];origin=[-4.9,2.8,7.6+z];out=[5.1,2.9,7.8+z]
        else:
            side=-1 if ch<2 else 1;band=(slot-1)//6;level=(slot-1)%3
            width=1.38+(slot%3)*.16;height=width*r['height']/r['width']
            if height>1.65:width*=1.65/height;height=1.65
            x=side*(3.8+band*.85+(slot%2)*.3);y=2.35+level*.72;depth=7.1+band*.8+(slot%2)*.32
            target=[x,y,depth+z];origin=[x+side*1.0,y-.13,depth+z+1.9];out=target
        e=dict(slot=slot,image=id,start=round(start,5),end=round(end,5),role='focus' if focus else 'periphery',enter=.45 if focus else .12,hold=round(hold,5) if focus else 0,exit=.55 if focus else .16,region=reg,**{'from':origin,'to':target,'out':out},width=round(width,4),height=round(height,4),channel=ch,year=r.get('display_year','') if focus or r['placement_mode'] in ['dated-recall','present-production'] else '',web_title='',trigger=trigger,placement_mode=r['placement_mode'])
        events.append(e);busy[slot].append((e['start'],e['end']))
    focus_specs=[
        ('desktop-a2a3aabf7bc5',.64,2.60),('desktop-6f670e11ded1',4.64,3.55),
        ('desktop-408b3546a859',20.8,3.45),('workflow-homepage-2',25.44,3.30),
        ('workflow-tieba-2',31.04,3.35),('workflow-manga-2',35.52,3.45),
        ('panda-sina-2007',51.36,2.8),('xiaonei-2007-wayback',55.52,3.50),
        ('world-creator-2017-sdk',72.0,2.60),('workflow-world-3',75.68,2.60)]
    def locate(exact,words):
        if exact in by_id:return exact
        return next((r['id'] for r in images if any(w.lower() in r['id'].lower() or w.lower() in r.get('title','').lower() for w in words)),None)
    for i,(id,start,hold) in enumerate(focus_specs):
        if id not in by_id:
            id=locate(id, ['panda','熊猫'] if 'panda' in id else ['xiaonei','校内'] if 'xiaonei' in id else ['world-creator','Build & Publish'] if 'creator' in id else [id])
        if not id:continue
        st,tr=snap(start,i%4,start-.05,start+.05);emit(id,0,st,st+hold+1,tr['channel'],tr,True,hold)
    # Explicit news anchors progress from the 2010 recall to the 2022 update.
    choices=[([105,112.64],105.12,3.4),([105,112.64],109.60,2.4),([112.64,122.88],113.04,3.4),([112.64,122.88],117.52,3.4),([122.88,133.12],123.12,3.4),([125.44,133.12],128.0,3.35)]
    preferred=['3q-cnet-popup','3q-yukict-forum','vrchat-eac-2022','vrchat-accessibility-request','reddit-private-notice','current-unity-world-authoring']
    used_focus={e['image'] for e in events}
    for index,(span,start,hold) in enumerate(choices):
        if index==5:continue  # End on the actual maintenance/archiving performance.
        candidates=[r for r in images if ranges[r['id']]==span and r['id'] not in used_focus and r['kind']!='original-operation-reconstruction']
        if index==5:candidates=[r for r in images if r['kind']=='current-project-self-capture']
        candidates.sort(key=lambda r:(not r['id'].startswith('v4-'), 'official' not in r['kind'], r['id']))
        if preferred[index] in by_id:candidates=[by_id[preferred[index]]]
        if not candidates:continue
        r=candidates[0];used_focus.add(r['id']);st,tr=snap(start,index%4,start-.06,start+.06);emit(r['id'],0,st,min(133.10,st+hold+1),tr['channel'],tr,True,hold)
    groups=defaultdict(list)
    for r in images:groups[tuple(ranges[r['id']])].append(r)
    def place(r,rank,repeat=False):
        lo,hi=ranges[r['id']];hi=min(hi,132.96)
        duration=(.48+.08*(rank%5)) if repeat else (1.16+.16*(rank%5))
        if r.get('workflow'):duration=.64+.08*(rank%4)
        count=len(groups[tuple(ranges[r['id']])]);base=lo+.08+(rank/max(1,count))*(hi-lo-duration-.1)
        if repeat:base=lo+(.27+rank*.381)%(max(.2,hi-lo-duration-.2))
        for step in range(150):
            trial=lo+(base-lo+step*.08)%max(.1,hi-lo-duration-.08);st,tr=snap(trial,rank%4,lo,hi-duration)
            for slot in [1+(rank+j)%23 for j in range(23)]:
                if free(slot,st,st+duration):emit(r['id'],slot,st,st+duration,tr['channel'],tr);return
        if repeat:return
        # Sparse phrases may have too few distinct onsets for every archival
        # image. Score-row entrances remain explicitly distinct from note hits.
        for line in range(int((hi-lo-duration)/.08)+1):
            st=round(lo+line*.08,5)
            for slot in range(1,24):
                if free(slot,st,st+duration):emit(r['id'],slot,st,st+duration,rank%4,dict(type='score-row',channel=rank%4));return
        raise RuntimeError('Could not place '+r['id'])
    for span,rows in sorted(groups.items()):
        rows.sort(key=lambda r:(r.get('workflow',''),r.get('state',0),r['id']))
        for i,r in enumerate(rows):place(r,i)
        for i,r in enumerate(rows):
            if i%2==0:place(r,i,True)
    events.sort(key=lambda e:(e['slot'],e['start']))
    first={id:min(e['start'] for e in events if e['image']==id) for id in by_id}
    # Retain a curated chronological sample. Far-window copies are composition,
    # never counted as additional content assets.
    candidates=sorted((r for r in images if r['kind']!='original-operation-reconstruction' and r['placement_mode']!='archive-recall'),key=lambda r:first[r['id']])
    ambient=[]
    for target in [i*2.45+.1 for i in range(48)]:
        available=[r for r in candidates if r['id'] not in {a['image'] for a in ambient}]
        if not available:break
        r=min(available,key=lambda r:abs(first[r['id']]-target));ambient.append(dict(image=r['id'],reveal=round(first[r['id']]+.45,5),channel=len(ambient)%4))
    ambient.sort(key=lambda r:r['reveal'])
    phrases=[(53.0,61.3,['踩个脚印','这个页面我存过','楼主还在吗','围观一下']),
             (61.6,71.5,['字幕组辛苦了','这句我会背','这里对上了','谢谢翻译','前方高能']),
             (72.0,79.1,['求教程','原来还可以这样','我也想试试','学会了','楼主更新了']),
             (82.2,85.9,['听得见吗','你也来了','我带你过去']),
             (89.0,101.7,['这边坐','换个地方聊','原来你在这里','晚上好']),
             (106.0,122.3,['先导出一份','换个入口试试','我的收藏夹打不开了','有人找到办法了吗']),
             (124.0,131.7,['我这里还有备份','这个方法能用','谢谢，还在维护','收到了'])]
    comments=[]
    for band,(lo,hi,texts) in enumerate(phrases):
        interval=1.28 if band<2 else .88 if band==2 else 1.76 if band<5 else 2.56
        t=lo;k=0
        while t<hi:
            st,tr=snap(t,(band+k)%4,t,min(t+.32,hi));comments.append(dict(time=round(st,5),text=texts[k%len(texts)],region=region(st),channel=tr['channel'],duration=3.2 if band<5 else 4.0));t+=interval;k+=1
    lines=[(1.6,5.9,'这些界面，你认得吗？'),(16.4,19.9,'工具背后，也有一个人'),(23.2,27.6,'我们开始，做自己的东西'),(33.2,37.7,'让喜欢的作品，多一个人读懂'),(46.3,50.5,'收到了，也继续传出去'),(55.1,59.8,'生活，住进了这些页面'),(66.0,70.3,'一条字幕，许多双手'),(74.1,78.4,'从一个方块，到一个世界'),(83.1,85.9,'换个身体，还是你'),(89.5,94.1,'原来，我们能在这里见面'),(96.4,100.8,'有人回应，就不是一个人'),(105.2,109.4,'联系人还在，选择却变了'),(114.7,119.1,'安全，也要容得下每个人'),(124.0,128.0,'旧网页，还能再打开吗？'),(128.5,132.7,'把可用的方法，留给后来的人')]
    keylines=[dict(start=a,end=b,text=text,region=region(a),style='black_on_white' if i%3==1 else 'white_on_black') for i,(a,b,text) in enumerate(lines)]
    titles=['接通','工具与作者','开始制作','汉化与社区','下载与做种','生活在页面里','字幕与共同劳动','从方块到世界','共同在场','彼此回应','入口与选择','安全与可访问性','维护与保存']
    scenes=[dict(start=round(i*10.24,5),end=round((i+1)*10.24,5),title=t) for i,t in enumerate(titles)]
    score=dict(schema='oldwebmap.v4.recognition.1',duration=133.12,slots=24,overload_start=71.68,images=images,events=events,ambient=ambient,comments=sorted(comments,key=lambda c:c['time']),keylines=keylines,scenes=scenes)
    # Keep reviewed anonymous display copies when the underlying archival file
    # is unchanged. A changed original must be reviewed before reuse of its mask.
    previous_path=ROOT/'data/episode123-v4-score.json'
    if previous_path.exists():
        previous={r['id']:r for r in json.loads(previous_path.read_text())['images']}
        for r in images:
            saved=previous.get(r['id'],{})
            if saved.get('sha256')==r['sha256'] and saved.get('display_source_path'):
                for key in ['display_source_path','display_path','display_sha256','display_processing','anonymous_regions']:
                    if key in saved:r[key]=saved[key]
    write(ROOT/'data/episode123-v4-score.json',score)
    used={e['image'] for e in events};assert len(used)==len(images)
    focus=[e for e in events if e['role']=='focus'];assert all(2.4-1e-4<=e['end']-e['start']-1<=5 for e in focus)
    report=dict(status='authored-awaiting-unity',images=len(images),unique_images_used=len(used),events=len(events),focus_anchors=len(focus),focus_hold_range=[min(e['hold'] for e in focus),max(e['hold'] for e in focus)],actual_note_events=sum(e['trigger']['type']=='actual-note' for e in events),ambient=len(ambient),comments=len(comments),keylines=len(keylines),ranges=dict(Counter(str(ranges[r['id']]) for r in images)),archive_recall_images=sum(r['placement_mode']=='archive-recall' for r in images),excluded=excluded)
    write(ROOT/'data/episode123-v4-material-audit.json',report);print(json.dumps(report,ensure_ascii=False,indent=2))
if __name__=='__main__':main()
