#!/usr/bin/env python3
"""Lossless user-art crops and authored, period-style manga workflow UI.

Originals are read only. No generated illustration and no claim of historical capture.
Pixel crop coordinates are [left, top, right, bottom], PIL-exclusive at right/bottom.
"""
from pathlib import Path
import hashlib
import json
from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT=Path(__file__).resolve().parents[1]
SRC=ROOT/'art/source/fullfilm-v3-user-images'
OUT=ROOT/'art/export/fullfilm-v3-user-art'
OUT.mkdir(parents=True,exist_ok=True)
FONT='/usr/share/fonts/noto-cjk/NotoSansCJK-Regular.ttc'
UI=ROOT/'art/export/fullfilm-v2-ui/native-overlays/manga/state-00.png'
MANIFEST={'schema':'fullfilm-v3-user-art.1','generator':'tools/prepare_fullfilm_v3_user_art.py',
 'crop_convention':'[left,top,right,bottom] pixels, top-left origin; right/bottom exclusive',
 'source_policy':'Original user PNG files are read-only. Crops preserve source RGB pixels without resizing.',
 'ui_nature':'Authored performance UI derivative using a project period-style frame; not a historical software screenshot.',
 'assets':[],'manga_states':[]}


def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def crop(name,source,rect=None,role='art'):
 path=SRC/source; im=Image.open(path).convert('RGB')
 rect=rect or (0,0,*im.size)
 result=im.crop(rect); target=OUT/name;result.save(target,optimize=True)
 row={'file':str(target.relative_to(ROOT)),'name':name,'role':role,
      'source':str(path.relative_to(ROOT)),'source_sha256':sha(path),
      'source_size_px':list(im.size),'crop_px':list(rect),'size_px':list(result.size),
      'sha256':sha(target),'resized':False}
 MANIFEST['assets'].append(row)
 return result,row


def text(im,xy,value,size=16,color='#202020'):
 if not value:return
 f=ImageFont.truetype(FONT,size,index=2)
 box=ImageDraw.Draw(im).textbbox((0,0),value,font=f,anchor='lt')
 mask=Image.new('L',(box[2]+2,box[3]+2))
 ImageDraw.Draw(mask).text((0,0),value,font=f,anchor='lt',fill=255)
 mask=mask.point(lambda p:255 if p>100 else 0)
 im.paste(Image.new('RGB',mask.size,color),tuple(map(int,xy)),mask)


def manga_ui(comics):
 lines=[('おはよう！','早上好！'),('あの星を見に行こう。','去看看那颗星吧。')]
 labels=['原文整理','无字清稿','中文嵌字','校对完成']
 sources=[comics[0],comics[1],comics[2],comics[2]]
 ui_sha=sha(UI)
 for i,(art,asset) in enumerate(sources):
  im=Image.open(UI).convert('RGB');d=ImageDraw.Draw(im)
  # The V2 cartoon's ears intruded above its canvas into the menu row.
  # Restore the same native menu captions before placing the user's page.
  d.rectangle((6,30,793,56),fill='#c0c0c0')
  for x,caption in [(10,'文件(F)'),(68,'编辑(E)'),(126,'查看(V)'),(186,'工具(T)'),(244,'帮助(H)')]:
   text(im,(x,37),caption,12,'#202020')
  d.line((6,57,793,57),fill='#ffffff')
  d.rectangle((7,59,792,76),fill='#ffffff')
  d.rectangle((391,66,401,511),fill='#ffffff')
  # Replace the entire original canvas. Native menu, frame and status line remain.
  d.rectangle((18,77,390,503),fill='#e9e8df')
  fitted=ImageOps.contain(art,(357,410),Image.Resampling.LANCZOS)
  pos=(26+(357-fitted.width)//2,85+(410-fitted.height)//2)
  im.paste(fitted,pos)
  d.rectangle((402,77,785,337),fill='#ffffff')
  text(im,(415,86),'台词表  page01.txt',20,'#000080')
  for j,(jp,zh) in enumerate(lines):
   y=132+j*85
   if (i==1 and j==0) or (i==2 and j==1):
    d.rectangle((420,y+27,772,y+51),fill='#e6e9f0')
   text(im,(415,y),f'{j+1:03d}  {jp}',16)
   if i>0:text(im,(434,y+30),zh,16,'#174475' if i<3 else '#202020')
  text(im,(415,313),['原文 2 句 / 等待翻译','清字完成 / 译文已录入','嵌字 2 / 2','校对 2 / 2 · 已保存'][i],14,'#555555')
  for j,label in enumerate(['图层 1  原稿','图层 2  清稿','图层 3  中文台词','图层 4  校对']):
   active=j==i
   d.rectangle((421,351+j*30,770,377+j*30),fill='#000080' if active else '#d5d7db')
   text(im,(431,357+j*30),label,13,'#ffffff' if active else '#202020')
  d.rectangle((7,519,791,535),fill='#c0c0c0')
  text(im,(12,521),['page01-original.tif  100%','page01-clean.tif  已保存','page01-zh.tif  文字图层','page01-zh.png  已校对'][i],11,'#555555')
  name=f'native-manga-user-{i:02d}-full.png';target=OUT/name;im.save(target,optimize=True)
  rect=[402,77,786,479];detail=im.crop(rect)
  detailname=f'native-manga-user-{i:02d}-detail.png';detailpath=OUT/detailname;detail.save(detailpath,optimize=True)
  MANIFEST['manga_states'].append({'state':i,'label':labels[i],
    'full':str(target.relative_to(ROOT)),'full_size_px':[800,540],'full_sha256':sha(target),
    'detail':str(detailpath.relative_to(ROOT)),'detail_size_px':list(detail.size),
    'detail_crop_from_full_px':rect,'detail_sha256':sha(detailpath),
    'art_asset':asset['name'],'art_source':asset['source'],'art_source_sha256':asset['source_sha256'],
    'art_source_crop_px':asset['crop_px'],'art_paste_rect_px':[pos[0],pos[1],pos[0]+fitted.width,pos[1]+fitted.height],
    'frame_source':str(UI.relative_to(ROOT)),'frame_source_sha256':ui_sha,
    'active_layer':i+1,'dialogue':[{'ja':jp,'zh':zh,'translation_visible':i>0} for jp,zh in lines]})
 return labels


def contact_sheet(items,name,columns,thumb_size,caption=False):
 w,h=thumb_size;rows=(len(items)+columns-1)//columns;gap=12
 im=Image.new('RGB',(columns*(w+gap)+gap,rows*(h+gap+(24 if caption else 0))+gap),'#343b43')
 for i,(label,pic) in enumerate(items):
  x=gap+(i%columns)*(w+gap);y=gap+(i//columns)*(h+gap+(24 if caption else 0))
  fitted=ImageOps.contain(pic,(w,h),Image.Resampling.LANCZOS)
  im.paste(fitted,(x+(w-fitted.width)//2,y+(h-fitted.height)//2))
  if caption:text(im,(x,y+h+4),label,14,'#f1f1ec')
 im.save(OUT/name,optimize=True)


def main():
 originals={p.name:sha(p) for p in SRC.glob('*.png')}
 comics=[crop('user-comic-original.png','星空下的原文与中文译文.png',(18,51,820,921),'comic-original'),
         crop('user-comic-clean.png','星夜山巅的漫画对白.png',(18,51,820,921),'comic-clean'),
         crop('user-comic-chinese.png','星夜山巅的漫画对白.png',(854,51,1656,921),'comic-chinese')]
 manga_ui(comics)
 crop('user-game-original.png','原文与中文译文：月下古桥奇境.png',(0,0,830,941),'game-original')
 crop('user-game-chinese.png','原文与中文译文：月下古桥奇境.png',(842,0,1672,941),'game-chinese')
 crop('user-homepage.png','夜航邮局：星海寄信之夜.png',role='homepage')
 crop('user-still-life.png','温馨创作者桌面时光.png',role='still-life')
 crop('user-concept.png','水上花园圆亭与落日光影.png',role='world-concept')
 flash=[]
 for row,y in enumerate((52,523)):
  for col,x in enumerate((3,562,1118)):
   idx=row*3+col;im,_=crop(f'user-flash-{idx:02d}.png','霓虹像素星辰六格动画分镜.png',(x,y,x+550,y+414),'flash-frame')
   flash.append((f'{idx+1:02d}',im))
 stickers=[]
 for row,(y0,y1) in enumerate(((13,305),(318,605),(618,926))):
  for col,(x0,x1) in enumerate(((14,415),(430,828),(845,1243),(1261,1660))):
   idx=row*4+col;im,_=crop(f'user-sticker-{idx:02d}.png','猫咪聊天贴纸十二格合集.png',(x0,y0,x1,y1),'chat-sticker')
   stickers.append((f'{idx+1:02d}',im))
 contact_sheet([(s['label'],Image.open(ROOT/s['full'])) for s in MANIFEST['manga_states']], 'review-manga-states.png',2,(800,540),True)
 contact_sheet(flash,'review-flash-frames.png',3,(552,414))
 contact_sheet(stickers,'review-sticker-crops.png',4,(398,300))
 contact_sheet([(r['name'],Image.open(OUT/r['name'])) for r in MANIFEST['assets'][:8]],'review-core-art.png',4,(400,400),True)
 assert originals=={p.name:sha(p) for p in SRC.glob('*.png')},'Original source changed'
 MANIFEST['validation']={'original_files_unchanged':True,'source_count':len(originals),
   'crop_count':len(MANIFEST['assets']),'manga_full_count':len(MANIFEST['manga_states']),
   'checks':['All crop sizes equal pixel rectangle extents','Original SHA-256 checked before and after',
      'No source crop resizing','All performance PNG files use English names; text contains content/native UI only']}
 (ROOT/'data/fullfilm-v3-user-art.json').write_text(json.dumps(MANIFEST,ensure_ascii=False,indent=2),encoding='utf8')
 print('FULLFILM_V3_USER_ART_READY',len(MANIFEST['assets']),'crops,',len(MANIFEST['manga_states']),'UI states')


if __name__=='__main__':main()
