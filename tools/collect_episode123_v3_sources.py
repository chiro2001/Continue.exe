#!/usr/bin/env python3
"""Collect full, attributed historical UI images; never cut the reference MV."""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor, as_completed
from urllib.parse import urljoin, urlparse
import requests, json, hashlib, re, io, time
from bs4 import BeautifulSoup
from PIL import Image

ROOT=Path(__file__).resolve().parent.parent
OUT=ROOT/'art/reference/episode123-v3';OUT.mkdir(parents=True,exist_ok=True)
TMP=Path.home()/'tmp/vrc-old-web-map/episode123-v3/sources';TMP.mkdir(parents=True,exist_ok=True)
HEAD={'User-Agent':'Mozilla/5.0 (historical visual research; private creative prototype)'}
jobs=[];seen_urls=set();failures=[]
def add(url,page,title,category,date=None,kind='documented-software-screenshot'):
    url=url.split('#')[0]
    if url in seen_urls:return
    seen_urls.add(url);jobs.append(dict(image_url=url,source_url=page,title=title,category=category,source_date=date,kind=kind))
def page(url):
    r=requests.get(url,headers=HEAD,timeout=25);r.raise_for_status()
    (TMP/(hashlib.sha1(url.encode()).hexdigest()+'.html')).write_bytes(r.content)
    return BeautifulSoup(r.content,'html.parser')
def guide(system,limit):
    u='https://www.guidebookgallery.org/screenshots/'+system
    s=page(u);n=0
    for im in s.select('img[src]'):
        src=im['src'];alt=im.get('alt','')
        if not src.startswith('pics/gui/') or any(x in src for x in ['settings/','installation/','about/']):continue
        if n>=limit:break
        add(urljoin('https://www.guidebookgallery.org/',src),u,alt,'desktop',None,'museum-software-version-screenshot');n+=1
    return n
def docs(slug):
    u='https://aegisub.org/docs/latest/'+slug+'/'
    s=page(u)
    for im in s.select('img[src]'):
        src=im['src']
        if '/img/' not in src:continue
        add(urljoin(u,src),u,'Aegisub · '+slug.replace('_',' ')+' · '+im.get('alt',''),'fansub',None,'official-documentation-screenshot')
def article(u,category,selector=None):
    s=page(u);body=s.select_one(selector) if selector else s.select_one('article, #artibody, .article, .post')
    if body is None:body=s
    for im in body.select('img'):
        src=im.get('data-original') or im.get('data-src') or im.get('data-url') or im.get('src')
        if not src or any(x in src for x in ['avatar','logo','icon','banner','qrcode','.svg','.gif?']):continue
        cap=im.get('alt','') or im.get('title','')
        parent=im.parent
        if not cap:
            for sibling in list(parent.next_siblings)[:4]:
                if hasattr(sibling,'get_text'):
                    t=sibling.get_text(' ',strip=True)
                    if 4<len(t)<180:cap=t;break
        src=src.split('!')[0]
        # A circulating article mistakes the unrelated 2001 renren.com for Xiaonei/Renren.
        if '2001年2月人人' in cap:continue
        add(urljoin(u,src),u,cap or s.title.get_text()[:100],category,None,'retrospective-article-image-date-needs-caption-check')
def vrcnews():
    u='https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/'
    data=requests.get(u,params=dict(appid=438100,count=100,maxlength=0,enddate=1577836800),timeout=30).json()
    (TMP/'vrc-steam-news-before-2020.json').write_text(json.dumps(data,ensure_ascii=False,indent=2))
    n=0
    for it in data['appnews']['newsitems']:
        if it.get('feedname')!='steam_community_announcements':continue
        text=it['contents'];title=it['title'];date=time.strftime('%Y-%m-%d',time.gmtime(it['date']))
        urls=re.findall(r'\[img\](.*?)\[/img\]',text)
        if not urls:urls=re.findall(r'https?://[^\s"\]]+\.(?:png|jpg)',text)
        for i,src in enumerate(urls[:5]):
            src=src.replace('{STEAM_CLAN_IMAGE}','https://clan.akamai.steamstatic.com/images/')
            add(src,it['url'],f'VRChat {date} · {title} · {i+1}','vrc',date,'official-steam-announcement-image');n+=1
            if n>=45:return
def fetch(job):
    try:
        key=hashlib.sha1(job['image_url'].encode()).hexdigest()[:12]
        dest=OUT/(job['category']+'-'+key+'.png')
        if dest.exists():im=Image.open(dest);raw=dest.read_bytes()
        else:
            r=requests.get(job['image_url'],headers={**HEAD,'Referer':job['source_url']},timeout=35);r.raise_for_status()
            im=Image.open(io.BytesIO(r.content)).convert('RGB')
            if im.width<220 or im.height<140:return None
            # Preserve the full frame, bounded only by a practical import resolution.
            im.thumbnail((1600,1600));im.save(dest);raw=dest.read_bytes()
        job.update(id=dest.stem,path=str(dest.relative_to(ROOT)),width=im.width,height=im.height,sha256=hashlib.sha256(raw).hexdigest(),rights='Source screenshot and marks retain original rights; private prototype reference, publication clearance not asserted',verified='downloaded-full-image; contextual caption preserved; not an original-era capture claim')
        return job
    except Exception as e:return {'error':str(e),'url':job['image_url']}
def main():
    tasks=[(guide,(s,n)) for s,n in [('win95',22),('win98',25),('winxppro',25),('win2000',12),('win311',10)]]
    tasks += [(docs,(s,)) for s in ['audio','audio_timing_tutorial','translation_assistant','visual_typesetting','styles','styling_assistant','editing_subtitles','exporting','attachments','karaoke_timing_tutorial','automation','video','spell_checker']]
    tasks += [(article,(u,c)) for u,c in [
        ('https://tech.sina.com.cn/roll/2019-10-05/doc-iicezuev0110956.shtml','chinese-web'),
        ('https://www.usold.cn/post/314.html','chinese-web'),
        ('https://www.ifanr.com/1333145','web'),
        ('http://www.emule-mods.de/bittorrent/screen-BitComet.html','distribution'),
        ('http://www.emule-mods.de/bittorrent/screen-Azureus.html','distribution'),
        ('http://www.emule-mods.de/bittorrent/screen-uTorrent.html','distribution'),
        ('http://www.emule-mods.de/?gallery','distribution')]]
    tasks.append((vrcnews,()))
    with ThreadPoolExecutor(max_workers=8) as ex:
        futures={ex.submit(f,*a):str(a) for f,a in tasks}
        for p in as_completed(futures):
            try:p.result()
            except Exception as e:failures.append({'page':futures[p],'error':str(e)})
    print('Image candidates',len(jobs),flush=True)
    assets=[];hashes=set()
    with ThreadPoolExecutor(max_workers=10) as ex:
        futures=[ex.submit(fetch,j.copy()) for j in jobs]
        for f in as_completed(futures):
            r=f.result()
            if not r:continue
            if 'error' in r:failures.append(r);continue
            if r['sha256'] in hashes:continue
            hashes.add(r['sha256']);assets.append(r)
            if len(assets)%25==0:print('Downloaded distinct',len(assets),flush=True)
    assets.sort(key=lambda x:(x['category'],x['id']))
    result={'version':3,'checked_on':'2026-10-16','assets':assets,'unique_image_count':len(assets),'failures':failures,'counting_rule':'Full distinct frames only; no crop/recolour increments. Software screenshots are version references, not asserted original-era captures.'}
    (ROOT/'data/episode123-v3-historical-images.json').write_text(json.dumps(result,ensure_ascii=False,indent=2))
    print('Saved',len(assets),'distinct images. Failures',len(failures),flush=True)
if __name__=='__main__':main()
