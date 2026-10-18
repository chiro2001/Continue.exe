#!/usr/bin/env python3
"""Prepare deterministic per-channel FFT bands and beat montage from cited images."""
from pathlib import Path
import json
import numpy as np
import soundfile as sf
from PIL import Image
ROOT=Path(__file__).resolve().parent.parent
TMP=Path.home()/'tmp/vrc-old-web-map/episode123-v2';TMP.mkdir(parents=True,exist_ok=True)
end=133.12;step=.04;frames=round(end/step)+1;bands=16;nfft=4096;rate=48000
edges=np.geomspace(35,14000,bands+1);freq=np.fft.rfftfreq(nfft,1/rate);window=np.hanning(nfft)
raw=[]
for ch in range(1,5):
    wave,sr=sf.read(Path.home()/f'tmp/vrc-old-web-map/episode123/audio/FC14_Mono_{ch:02}.wav',dtype='float32');assert sr==rate
    wave=wave[:round(end*rate)];wave=np.pad(wave,(nfft//2,nfft//2+round(step*rate)))
    chunks=np.lib.stride_tricks.sliding_window_view(wave,nfft)[::round(step*rate)][:frames]
    chunks=chunks-chunks.mean(axis=1,keepdims=True)
    fft=np.abs(np.fft.rfft(chunks*window,axis=1))*2/window.sum()
    values=np.stack([np.sqrt(np.mean(fft[:,(freq>=edges[b])&(freq<edges[b+1])]**2,axis=1)) for b in range(bands)],axis=1)
    raw.append(values)
reference=float(np.percentile(np.stack(raw),98.5));outputs=[]
for values in raw:
    result=np.clip(np.log1p(values*160)/np.log1p(reference*160),0,1)
    # Keep onsets immediate; a short release keeps low-level bands legible.
    for j in range(1,len(result)):result[j]=np.maximum(result[j],result[j-1]*.62)
    outputs.append(np.round(result,4).ravel().tolist())
report={'version':2,'step':step,'frames':frames,'bands':bands,'band_edges_hz':np.round(edges,2).tolist(),'sample_rate':rate,'fft_size':nfft,'method':'Centered Hann FFT on each original mono stem, DC removed per window, logarithmic frequency bands and common log-amplitude reference; prebaked deterministic data, no random beat bars.','normalization_reference':reference,'channels':outputs}
(ROOT/'data/episode123-v2-spectrum.json').write_text(json.dumps(report,ensure_ascii=False,separators=(',',':'))+'\n')
print('Spectrum:',frames,'frames x 4 x',bands,'bands')
manifest=ROOT/'data/episode123-historical-images.json'
if not manifest.exists():print('Historical image manifest pending; spectrum prepared.');raise SystemExit(0)
assets=json.loads(manifest.read_text())['assets']
extra=ROOT/'data/episode123-software-images.json'
if extra.exists():assets+=json.loads(extra.read_text())['assets']
pictures=[]
for a in assets:
    path=ROOT/a['path']
    if not path.is_file():continue
    w,h=Image.open(path).size
    pictures.append({'id':a['id'],'path':a['path'],'title':a['title'],'category':a['category'],'kind':a['kind'],'width':w,'height':h})
# Original authored layers are used as authored work, never labelled as historical screenshots.
for stem,cat,title in [('manga_ink','translation','原画'),('manga_original','translation','原文'),('manga_chinese','translation','汉化'),('subtitle_editor','translation','字幕时间轴 · 原创示例'),('animation_bg','translation','原创动画 · 背景层'),('animation_front','translation','原创动画 · 前景层'),('credits','translation','翻译 / 校对 / 嵌字'),('explorer','software','目录 / 文件'),('readme','software','README · 原创演示'),('bt_transfer','distribution','分块传递 · 机制示意'),('maintenance_queue','maintenance','维护请求 · 原创示意'),('error_dialog','event','错误提示 · 原创示意')]:
 p=ROOT/f'art/export/episode123/{stem}.png';w,h=Image.open(p).size;pictures.append({'id':'authored-'+stem,'path':str(p.relative_to(ROOT)),'title':title,'category':cat,'kind':'original-demonstration','width':w,'height':h})
for p in sorted((TMP/'self-captures').glob('*.png')):
 w,h=Image.open(p).size;pictures.append({'id':p.stem,'path':str(p),'title':'本项目制作过程 · 当前自截','category':'making','kind':'current-project-self-capture','width':w,'height':h})
chapters=[(0,10.24,['system','software'],['HELLO','连接','OPEN','1998','欢迎','START','双击','读取']),(10.24,20.48,['software','distribution','making'],['README','解压','打开','复制','PORTABLE','工具','GREETZ','试试看']),(20.48,30.72,['making','vrc'],['WORLD','骨骼','材质','BUILD','AVATAR','UPLOAD','创建','进入']),(30.72,40.96,['translation','software'],['原文','翻译','清字','嵌字','校对','理解','汉化','共享']),(40.96,51.2,['translation','making'],['00:00:01','对齐','字幕','下一句','一起试一下','Aegisub','TIMING','播放']),(51.2,71.68,['distribution','software','community'],['分块','下载','上传','继续做种','100%','BT','分享','收到']),(71.68,81.92,['community','software'],['楼主好人','路过','顶','相册','日志','转发','233','在吗']),(81.92,92.16,['vrc','making'],['HELLO','WELCOME','换个身体','一起','WORLD','GREETZ','在这里','来玩']),(92.16,102.4,['event','community','software'],['ORZ','报错了','重试','求助','已解决',':)','谢谢','再来一次']),(102.4,112.64,['event','security','software'],['请选择','版本','兼容','登录','继续？','允许','拒绝','下一步']),(112.64,122.88,['security','vrc','event'],['2022','安全','EAC','能力','便利','回应','更新','取舍']),(122.88,133.12,['maintenance','making','software'],['依赖','检查','旧版本','修复','等待','维护','有人在做','继续可用'])]
cuts=[]
for ci,(a,b,categories,words) in enumerate(chapters):
 pool=[i for i,p in enumerate(pictures) if p['category'] in categories]
 if ci==2:
  pool=[i for i,p in enumerate(pictures) if p['category']=='making' or p['id'].startswith('vrc-world-')]+[i for i,p in enumerate(pictures) if p['id'].startswith('vrc-2017-hub-')][:3]
 if ci==8:
  pool=[i for i,p in enumerate(pictures) if 'panda' in p['id'] or p['category']=='community' or p['id']=='authored-error_dialog']
 if ci==9:
  pool=[i for i,p in enumerate(pictures) if p['id'].startswith('3q-') or p['id'].startswith('qq-') or p['id'].startswith('msn-') or p['id']=='authored-error_dialog']
 if ci==10:
  pool=[i for i,p in enumerate(pictures) if 'eac' in p['id']]*3+[i for i,p in enumerate(pictures) if p['id'] in ['vrc-2017-hub-avatar-hall','vrc-2017-hub-mirror','vrc-world-sdk-build-controller-2017','vrc-avatar-world-2018-example']]
 if not pool:pool=list(range(len(pictures)))
 for j in range(round((b-a)/.64)):
  # Coprime traversal avoids repeating the same few pages; a four-beat memory flash returns earlier material.
  index=pool[(j+ci*3)%len(pool)]
  if len(cuts)>24 and j%8==7:index=cuts[-24]['image']
  p=pictures[index]
  cuts.append({'time':round(a+j*.64,4),'image':index,'layout':(j+ci)%4,'word':words[j%len(words)],'label':p['title']+(' · 原创示意' if p['kind']=='original-demonstration' and '原创' not in p['title'] else ''),'section':ci})
result={'version':2,'duration':end,'pulse_seconds':.64,'secondary_tick_seconds':.32,'primary_cuts':len(cuts),'old_primary_scenes':12,'primary_change_ratio':len(cuts)/12,'images':pictures,'cuts':cuts,'note':'0.64s is an eight-row visual pulse mapped to FC14; no claim of independently confirmed musical metre. Main images, flashbacks, words, 4x16 FFT bars and spatial micro-motion share the show clock.'}
(ROOT/'data/episode123-v2-montage.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
print('Montage:',len(pictures),'images;',len(cuts),'primary cuts;',round(len(cuts)/12,2),'x first pass')
