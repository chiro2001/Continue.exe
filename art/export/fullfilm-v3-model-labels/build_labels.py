#!/usr/bin/env python3
"""Four static model signs, plain approved Chinese text. No generated imagery."""
import json
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
OUT=Path(__file__).resolve().parent
FONT='/usr/share/fonts/noto-cjk/NotoSansCJK-Regular.ttc'
SIGNS=[
 ('community-sign.png','作品 / 留言 / 存档',(1950,300),'UI_CommunitySign',1.30,.20),
 ('archive-label.png','资料与来源',(1680,240),'UI_ArchiveLabel',.84,.12),
 ('shared-work-credit-00.png','漫画与翻译',(2048,256),'UI_SharedWorkCredit_00',.52,.065),
 ('shared-work-credit-01.png','游戏与本地化',(2048,256),'UI_SharedWorkCredit_01',.52,.065),
]
rows=[]
for filename,text,size,obj,w,h in SIGNS:
 im=Image.new('RGB',size,'#ddd8c5');d=ImageDraw.Draw(im)
 W,H=size
 d.rectangle((4,4,W-5,H-5),outline='#787e6f',width=3)
 d.line((int(.025*W),int(.19*H),int(.025*W),int(.81*H)),fill='#2e6257',width=max(8,round(H*.035)))
 font=ImageFont.truetype(FONT,round(H*.65),index=2)
 bbox=d.textbbox((0,0),text,font=font)
 while bbox[2]-bbox[0]>W*.88:
  font=ImageFont.truetype(FONT,font.size-2,index=2);bbox=d.textbbox((0,0),text,font=font)
 x=(W-(bbox[2]-bbox[0]))/2-bbox[0]
 y=(H-(bbox[3]-bbox[1]))/2-bbox[1]
 d.text((x,y),text,font=font,fill='#243e39')
 im.save(OUT/filename,optimize=True)
 rows.append({'file':filename,'text':text,'target_mesh':obj,'pixels':list(size),'physical_size_m':[w,h],
   'aspect':W/H,'color_space':'sRGB','alpha':False,'font':'Noto Sans CJK SC Regular; installed TTC index 2',
   'binding':'Unique material per mesh, ScreenshotUV full 0..1. Do not change shared thumbnail material globally.'})
(OUT/'labels.json').write_text(json.dumps({'schema':1,'technique':'Pillow static text layout','labels':rows},ensure_ascii=False,indent=2),encoding='utf8')
