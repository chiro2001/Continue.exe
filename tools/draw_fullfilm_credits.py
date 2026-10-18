#!/usr/bin/env python3
"""Draw the readable, post-performance music/archive attribution panel."""
from pathlib import Path
import json
import subprocess
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parent.parent
fontfile=subprocess.check_output(['fc-match','-f','%{file}','Noto Sans CJK SC'],text=True).strip()
image=Image.new('RGB',(1280,1100),'#14212b');draw=ImageDraw.Draw(image)
font=lambda size:ImageFont.truetype(fontfile,size)
draw.rounded_rectangle((24,24,1256,1076),18,outline='#a9c6cb',width=3)
draw.text((76,70),'曲目与档案',font=font(54),fill='#eef4ea')
lines=[('Future Composer 1.4 · FC14',37,'#a9d7ca'),('作曲署名：Rhino of Torment',35,'#ecede3'),
 ('音源：2008 年 EDGE Keil 注册机内嵌模块',31,'#c1d4dc'),('视觉语汇致意 EDGE 与 demoscene',31,'#c1d4dc'),('',20,'white'),
 ('人物资产：Kipfel / Nemesis',33,'#ecede3'),('历史界面与图像',39,'#a9d7ca'),('VRChat 官方公告 · Adobe · Aegisub',31,'#c1d4dc'),
 ('Internet Archive / Wayback Machine',31,'#c1d4dc'),('Guidebook Gallery 及各原始资料作者',31,'#c1d4dc'),('',20,'white'),
 ('谢谢每一位制作、翻译、保种、维护的人。',31,'#ecede3'),('那就一起做下去。',39,'#e7cb8f')]
y=170
for text,size,color in lines:
    draw.text((76,y),text,font=font(size),fill=color);y+=64 if size>=31 else 32
path=ROOT/'art/reference/fullfilm-anchors/fullfilm-credits-display.png';path.parent.mkdir(parents=True,exist_ok=True);image.save(path)
data_path=ROOT/'data/fullfilm-presentation.json';data=json.loads(data_path.read_text())
data['history_anchors']=[a for a in data.get('history_anchors',[]) if a['id']!='credits']
data['history_anchors'].append({'id':'credits','path':'Assets/OldWebMap/FullFilm/Revision1/History/Textures/fullfilm-credits-display.png','start':256,'end':100000,'position':[4.8,1.8,4.2],'width':2.3,'caption':''})
data_path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
print(path)
