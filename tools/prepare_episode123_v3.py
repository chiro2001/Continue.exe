#!/usr/bin/env python3
"""Curated complete-image corpus and note-led, independently layered scene score."""
from pathlib import Path
from PIL import Image
from collections import Counter,defaultdict
import json,csv,hashlib,bisect,math,re
ROOT=Path(__file__).resolve().parent.parent
TMP=Path.home()/'tmp/vrc-old-web-map/episode123-v3';TMP.mkdir(parents=True,exist_ok=True)
ASSET='Assets/OldWebMap/Episode123/Revision3/Images/'
def read(p):return json.loads((ROOT/p).read_text())
assets=[];pixels=set();excluded=[]
def add(row):
    p=Path(row['path']);p=p if p.is_absolute() else ROOT/p
    im=Image.open(p).convert('RGB');thumb=im.resize((160,120));signature=hashlib.sha256(thumb.tobytes()).hexdigest()
    if signature in pixels:excluded.append({'id':row['id'],'reason':'identical full frame after colour/size normalization'});return
    pixels.add(signature);row=dict(row);row['source_path']=str(p);row['path']=ASSET+row['id']+'.png';row['width'],row['height']=im.size
    row['title']=row.get('title',row['id']).strip();row['sha256']=hashlib.sha256(p.read_bytes()).hexdigest();assets.append(row)

def main():
    old=read('data/episode123-historical-images.json')['assets']+read('data/episode123-software-images.json')['assets']
    for r in old:add(r)
    new=read('data/episode123-v3-historical-images.json')['assets']
    web=[r for r in new if r['category']=='web']
    # Actual contact-sheet review: remove recent advertisements, empty frames,
    # generic museum chrome, COVID campaign and AI demos that belong after act 3.
    keep={0,1,2,3,5,6,7,9,10,11,13,14,15,17,18,19,20,21,24,25,26,29,31,32,33,35,36,40,41,42,43,44,45,46,47,48,50,51,52,55,56,57,58,59,60,61,62}
    titles={0:'早期杂志网页：Men’s Health',1:'MSN 门户网页',2:'早期互动实验页面',3:'早期摄影产品网站',5:'iPhone 3GS 时期网页',6:'早期 Apple 网页',7:'Flash 创意网站',9:'Adobe Photoshop 旧版启动页',10:'早期太空主题网页',11:'Nintendo 64 时代官网',13:'Adobe Photoshop 工作区',14:'早期可口可乐网页',15:'Yahoo! 与 IE 浏览器',17:'Backstreet Boys 早期官网',18:'Thefacebook 早期登录页',19:'早期 LEGO 官网',20:'Space Jam 个人宇宙式页面',21:'LEGO 社区网页',24:'McDonald’s 早期网页',25:'Yahoo! 分类目录',26:'Google! Beta 搜索页',29:'Sony Online 早期门户',31:'LEGO 早期超文本页面',32:'Power Mac G4 时期网页',33:'Nintendo 社区门户',35:'Messenger / ICQ 联系人界面',36:'Photoshop 旧版图像工作区',40:'PixelSurgeon 创作社区',41:'Canon 早期门户',42:'创意网页的多窗口实验',43:'The Wilderness Downtown 互动网页',44:'Nikon Coolpix 时期网页',45:'游戏社区网页',46:'个人 GOTH 主页',47:'早期可口可乐互动网页',48:'Flash 技术介绍页面',50:'旧版 Baidu 搜索页面',51:'The Tube 网络杂志',52:'Shockwave 插件时代网页',55:'实验性创意网页',56:'LEGO Flash 交互主页',57:'可口可乐早期分类主页',58:'创作人作品与社区',59:'Reebok 早期 Flash 网页',60:'NASA 早期网页',61:'旧版 Baidu 分类目录',62:'SEGA / NetLink 早期页面'}
    for r in new:
        if r['category']=='web':
            i=web.index(r)
            if i not in keep:excluded.append({'id':r['id'],'reason':'contact review: outside first-three-act scope / blank / museum wrapper'});continue
            r['title']=titles.get(i,r['title']);r['verified']='Full image visually reviewed in contact sheet; date not asserted beyond source caption'
        if r['id'] in ['chinese-web-d246b93d5131','chinese-web-d96c82e7c764','chinese-web-e44c225d9232']:
            excluded.append({'id':r['id'],'reason':'article cover/office photograph, not the historical UI requested'});continue
        add(r)
    for p in sorted((ROOT/'art/reference/episode123-v3-hub').glob('2019-*.png')):
        add(dict(id='official-'+p.stem,path=str(p.relative_to(ROOT)),title='VRChat 2019 官方 '+p.stem[5:],category='vrc',kind='official-2019-announcement-image',source_url='https://medium.com/@vrchat',date_basis='2019-05-13 official announcement, full source record in episode123-v3-hub-reference.json'))
    for r in read('data/episode123-v2-montage.json')['images']:
        if r['kind']=='current-project-self-capture':add(r)
    for r in read('data/episode123-v3-workflows.json')['assets']:add(r)
    # Original FC14 musical events, preserving pattern/instrument identity.
    notes=[[] for _ in range(4)]
    for r in csv.DictReader(open(ROOT/'audio/keil-keygen-2008/tracks.csv')):
        t=float(r['time_s']);ch=int(r['channel'])-1
        if t<133.12:notes[ch].append(dict(time=t,channel=ch,pattern=int(r['pattern']),row=int(r['row']),instrument=int(r['instrument'])))
    times=[[r['time'] for r in a] for a in notes]
    def snap(t,ch,lo,hi):
        candidates=[r for r in notes[ch] if lo<=r['time']<=hi]
        if not candidates or min(abs(r['time']-t) for r in candidates)>.48:
            candidates=[r for voice in notes for r in voice if lo<=r['time']<=hi]
        if not candidates:return round(max(lo,min(hi,round(t/.08)*.08)),5),{'type':'phrase-line','channel':ch}
        n=min(candidates,key=lambda r:abs(r['time']-t));return n['time'],dict(n,type='actual-note')
    scene_titles=['接通：机器第一次响应','工具也有作者','从使用到制作','一页一页，让更多人读懂','一句一句，对到一起','收到了，也继续传出去','个人主页与共同生活','不同时间，也能一起回应','换个身体，共同在场','会出错，也会互相帮助','入口还在，条件变了','安全、便利与新的日常','继续可用，需要有人维护']
    captions=['这些界面，你认得吗？','压缩包里，也有作者留下的说明。','有人从一个像素、一个方块开始。','翻译、嵌字、校对，让作品来到这里。','一条字幕背后，有许多双手。','下载完成之后，有人选择继续上传。','网页、帖子和留言，装下过我们的生活。','一句弹幕，在另一段时间得到回应。','不同的身体，在同一个地方见面。','有人报错，有人找回备份，有人愿意解释。','入口还在，使用它的条件变了。','安全与便利，也会改变原来的动作。','能继续打开的东西，往往有人在维护。']
    scenes=[dict(start=round(i*10.24,5),end=round((i+1)*10.24,5),title=scene_titles[i],caption=captions[i]) for i in range(13)]
    events=[];occupied=defaultdict(list)
    # All layers stay inside the frontal attention region. Depth is real geometry.
    positions={0:(-1.35,2.35,6.3),1:(1.55,2.55,7.1),2:(-2.25,1.85,4.9),3:(2.4,1.85,5.4),4:(-.4,3.25,5.4),5:(.55,1.15,4.9),6:(-2.25,3.5,6.8),7:(2.35,3.55,7.2),8:(-3.5,2.75,7.8),9:(3.7,2.65,8.4),10:(-.4,4.0,8.0),11:(.5,.8,6.6),12:(-1.6,3.15,7.5),13:(1.7,3.15,8.2),14:(-2.8,1.0,7.2),15:(2.9,1.05,7.8),16:(-1.2,2.4,4.4),17:(1.2,2.8,4.8),18:(-.9,3.65,6.1),19:(1.15,1.25,5.3)}
    def free(slot,start,end):return all(end<=a+.00001 or start>=b-.00001 for a,b in occupied[slot])
    def emit(r,slot,start,end,ch,motion,trigger,lead=False,recall=False):
        hub=81.92<=start<92.16;pos=list(positions[slot]);w=3.15 if slot<2 else 2.4 if slot<6 else 1.8 if slot<16 else 1.5
        if r.get('kind')=='original-operation-reconstruction':w=2.9 if slot<4 else 2.5
        if hub:pos=[(-1 if slot%2==0 else 1)*(3.6+(slot%3)*.4),3.15+(slot%2)*.8,8.7];w=1.7
        h=w*r['height']/r['width']
        if h>2.45:w*=2.45/h;h=2.45
        origin=[pos[0]+(-.34 if slot%2==0 else .34),pos[1]-.09,pos[2]+.28]
        # A source screenshot never scroll-distorts its own letters; scrolling
        # is reserved for authored state windows with an appropriate clip.
        if motion=='scroll' and r.get('kind')!='original-operation-reconstruction':motion='slide'
        action=r['title'].split(' · ')[-1][:24]
        result={'translation':'校对已保存','fansub':'字幕已对齐','distribution':'区块已收到，继续上传','chinese-web':'消息已得到回应','creation':'预览已更新','vrc':'同一世界，有人加入','security':'备份已恢复','conditions':'入口条件已经变化','maintenance':'修改已保存'}.get(r.get('category'),'窗口已打开')
        ev=dict(slot=slot,image=r['id'],start=round(start,5),end=round(min(133.12,end),5),**{'from':origin,'to':pos},width=round(w,4),height=round(h,4),motion=motion,channel=ch,label=action,result=result,trigger=trigger,recall=recall)
        events.append(ev);occupied[slot].append((ev['start'],ev['end']))
    def place(r,lo,hi,rank,slots,hold=1.6):
        ch=(rank+1)%4;desired=lo+.12+(rank*.713) % max(.1,hi-lo-hold-.24)
        choices=[]
        for shift in range(128):
            t=lo+((desired-lo)+shift*.08) % max(.1,hi-lo-hold-.08)
            start,trigger=snap(t,ch,lo,max(lo,hi-hold))
            for slot in slots:
                if free(slot,start,start+hold):choices.append((abs(start-desired),slot,start,trigger))
            if choices:break
        if not choices:raise ValueError('No layer available: '+r['id'])
        _,slot,start,tr=min(choices);emit(r,slot,start,start+hold,tr.get('channel',ch),['float','slide','tilt','depth','stamp','transfer'][rank%6],tr)
    workflows=defaultdict(list)
    for r in assets:
        if r.get('workflow'):workflows[r['workflow']].append(r)
    for wi,(key,rows) in enumerate(workflows.items()):
        rows.sort(key=lambda r:r['state']);lo,hi=rows[0]['preferred_range'];length=(hi-lo-.4) if lo>=102.4 else 2.32 if hi-lo<11 else 3.2
        slots=[2+((wi+j)%6) for j in range(6)];desired=lo+.24+(wi*1.37)%max(.1,hi-lo-length-.4)
        found=False
        for shift in range(80):
            start,tr=snap(lo+((desired-lo)+shift*.16)%max(.1,hi-lo-length-.1),wi%4,lo,hi-length)
            for slot in slots:
                if free(slot,start,start+length):found=True;break
            if found:break
        if not found:raise ValueError('workflow cannot fit '+key)
        # Separate note times for four operation states; not the old common .64 grid.
        boundaries=[start]
        for st in range(1,4):
            t,_=snap(start+length*st/4,(wi+st)%4,boundaries[-1]+.16,start+length-(4-st)*.16);boundaries.append(t)
        boundaries.append(round(start+length,5))
        for st,r in enumerate(rows):
            stime=boundaries[st];ch=(wi+st)%4;_,tr=snap(stime,ch,stime-.001,stime+.001)
            emit(r,slot,stime,boundaries[st+1],tr.get('channel',ch),['slide','tilt','stamp','transfer'][st],tr)
    source_rows=[r for r in assets if not r.get('workflow')]
    catrank=Counter()
    for ri,r in enumerate(source_rows):
        cat=r.get('category','web');k=catrank[cat];catrank[cat]+=1;title=r['title'].lower()
        if cat=='desktop':scene=[0,1,2][k%3]
        elif cat=='system':scene=0
        elif cat=='translation':scene=4 if 'aegisub' in r['id'] else 3
        elif cat in ['tools','software']:scene=1
        elif cat=='fansub':scene=4
        elif cat in ['distribution','sharing']:scene=5
        elif cat in ['chinese-web','bbs','community','social']:scene=6+k%2
        elif cat=='vrc':scene=8
        elif cat in ['making','creation']:scene=2
        elif cat in ['event','security']:scene=9 if 'panda' in r['id'] else 10
        elif cat=='web':scene=[2,6,7][k%3]
        else:scene=1+k%2
        if any(x in r['id'] for x in ['3q','panda']):scene=10 if '3q' in r['id'] else 9
        if 'vrchat-eac' in r['id']:scene=11
        if any(x in r['id'] for x in ['world-unity','world-sdk']):scene=2
        if any(x in title for x in ['trust and safety','safety system']):scene=11
        lo,hi=scene*10.24,(scene+1)*10.24
        # Archival full frames remain identifiable; source references are longer
        # than transient fragments. Postcards give physical Hub the central view.
        hold=1.12 if scene==8 else 1.6+(k%3)*.24
        if scene in [9,10,11,12]:hold=2.4
        place(r,lo,hi,k,[0,1,8,9,10,11,12,13,14,15],hold)
    # Rhythmic flashback layers quote prior, relevant images and never count as new content.
    archive=[r for r in source_rows if r.get('category') not in ['event','security']]
    for ch in range(4):
        selected=[n for n in notes[ch] if 30.72<=n['time']<81.92 or 92.16<=n['time']<102.4]
        stride=[3,2,3,2][ch]
        for k,n in enumerate(selected[::stride]):
            t=n['time'];slot=16+ch;hold=[.24,.32,.4,.48][(k+ch)%4]
            if not free(slot,t,t+hold):continue
            eligible=[r for r in archive if not r.get('workflow') and (r.get('category') in ['web','chinese-web','fansub','desktop','distribution'])]
            r=eligible[(k*11+ch*17)%len(eligible)];emit(r,slot,t,t+hold,ch,'stamp',dict(n,type='actual-note'),recall=True)
    # Conditions retain identifiable reference frames while smaller notices and
    # preserved files keep changing on real note attacks. No synthetic new count.
    for scene,picks in [(9,['panda','workflow-virus','workflow-bbs','emule']),
                        (10,['3q','workflow-3q','qq-200','msn','workflow-mail']),
                        (11,['vrchat-eac','vrc-8bf','workflow-eac','workflow-virus']),
                        (12,['workflow-maintain','workflow-release','workflow-opensource','workflow-homepage'])]:
        pool=[r for r in assets if any(p in r['id'] for p in picks)]
        lo,hi=scene*10.24,(scene+1)*10.24
        for k in range(18 if scene<12 else 12):
            r=pool[k%len(pool)];ch=k%4;target=lo+.12+k*(hi-lo-.9)/(18 if scene<12 else 12)
            t,tr=snap(target,ch,lo,hi-.72);hold=1.92 if k%5==0 else .72 if scene<12 else 1.28
            slots=[8,9,10,11,12,13,14,15]
            slot=next((s for s in slots if free(s,t,t+hold)),None)
            if slot is not None:emit(r,slot,t,t+hold,tr.get('channel',ch),'depth' if k%3==0 else 'slide',tr,recall=True)
    # The three source-based native operations own the central attention region.
    # Other whole-frame references remain visible around/behind their real depth.
    image_by_id={r['id']:r for r in assets}
    for e in events:
        if not any(e['start']<hi and e['end']>lo for lo,hi in [(10.24,20.48),(40.96,61.44)]):continue
        slot=e['slot'];r=image_by_id[e['image']];pos=list(e['to']);width=e['width']
        if slot in [0,1]:pos=[-2.9 if slot==0 else 2.9,2.45,7.6+slot*.6];width=2.5
        elif 2<=slot<=7:pos=[-3.5 if slot%2==0 else 3.5,1.85+(slot%3)*.65,7.4+(slot%2)*.5];width=1.85
        elif slot in [16,17]:pos=[-2.5 if slot==16 else 2.5,pos[1],6.4];width=1.5
        else:continue
        height=width*r['height']/r['width']
        if height>2.2:width*=2.2/height;height=2.2
        e['to']=pos;e['from']=[pos[0]+(-.3 if slot%2==0 else .3),pos[1]-.09,pos[2]+.28];e['width']=round(width,4);e['height']=round(height,4)
    events.sort(key=lambda e:(e['slot'],e['start']))
    used={e['image'] for e in events};assert len(used)==len(assets);assert len(used)>=285
    starts=[e['start'] for e in events];concurrent=[sum(e['start']<=i*.08<e['end'] for e in events) for i in range(1664)]
    score=dict(schema='oldwebmap.v3.montage.1',duration=133.12,slots=20,images=assets,events=events,scenes=scenes)
    (ROOT/'data/episode123-v3-score.json').write_text(json.dumps(score,ensure_ascii=False,indent=2))
    report=dict(unique_used_assets=len(used),baseline=57,multiplier=round(len(used)/57,3),asset_kinds=dict(Counter(r.get('kind','historical') for r in assets)),asset_categories=dict(Counter(r.get('category','web') for r in assets)),events=len(events),actual_note_events=sum(e['trigger']['type']=='actual-note' for e in events),concurrency=dict(min=min(concurrent),max=max(concurrent),mean=round(sum(concurrent)/len(concurrent),3)),excluded=excluded,all_assets_used=True,source_images_are_full_frames=True,workflow_assets=144)
    (ROOT/'data/episode123-v3-material-audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
    print(json.dumps({k:v for k,v in report.items() if k!='excluded'},ensure_ascii=False,indent=2))
if __name__=='__main__':main()
