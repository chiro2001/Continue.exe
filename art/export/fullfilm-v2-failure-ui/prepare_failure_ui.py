from pathlib import Path
import json,hashlib,shutil
from PIL import Image,ImageDraw,ImageFont
ROOT=Path('/home/chiro/projects/vrc-old-web-map');OUT=ROOT/'art/export/fullfilm-v2-failure-ui';OUT.mkdir(exist_ok=True)
FONT='/usr/share/fonts/noto-cjk/NotoSansCJK-Regular.ttc'
SCORE=json.loads((ROOT/'data/episode123-v4-score.json').read_text());SRC={s['id']:s for s in SCORE['images']}
IE=Image.open(SRC['desktop-f125ce10236d']['source_path']).convert('RGBA');BT=Image.open(SRC['bitcomet-053-main']['source_path']).convert('RGBA')
M={'schema':'fullfilm-v2-failure-ui.1','range':[168.96,186.88],'kind':'original-state-reconstruction','source_metadata':[SRC['desktop-f125ce10236d'],SRC['bitcomet-053-main']],
 'notes':'Read-only historical software window shells, newly authored failure states. They are not screenshots of an actual service or incident. No explanatory QA text is burned into performance plates.',
 'states':[],'references':[]}
for id,name in [('desktop-f125ce10236d','source-ie6-xp.png'),('bitcomet-053-main','source-bitcomet-053.png')]:
 p=Path(SRC[id]['source_path']);shutil.copyfile(p,OUT/name);M['references'].append({'id':id,'file':name,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'source_path':str(p)})
def tx(im,xy,s,n=16,c='#111111'):
 f=ImageFont.truetype(FONT,n);d=ImageDraw.Draw(im);b=d.textbbox((0,0),s,font=f,anchor='lt');mask=Image.new('L',(max(1,b[2]+2),max(1,b[3]+2)));ImageDraw.Draw(mask).text((0,0),s,font=f,anchor='lt',fill=255);mask=mask.point(lambda p:255 if p>100 else 0);im.paste(Image.new('RGBA',mask.size,c),xy,mask)
def lines(im,x,y,ls,n=16,gap=28,c='#111111'):
 for j,s in enumerate(ls):tx(im,(x,y+j*gap),s,n,c)
def box(im,b,fill,outline=None):ImageDraw.Draw(im).rectangle(b,fill=fill,outline=outline)
def ie(title,url):
 # Real XP/IE6 chrome; only the native content height is reduced from 412 to 374px.
 # Header controls remain at original pixels; bottom status and scrollbar are reanchored.
 im=Image.new('RGBA',(778,518),'#ffffff');im.paste(IE.crop((0,0,778,118)),(0,0));im.paste(IE.crop((0,528,778,556)),(0,490))
 im.paste(IE.crop((0,118,6,528)).resize((6,372),Image.Resampling.NEAREST),(0,118));im.paste(IE.crop((755,118,778,528)).resize((23,372),Image.Resampling.NEAREST),(755,118))
 # Keep native icon/XP controls; replace only title text and address interior.
 im.paste(IE.crop((500,0,501,30)).resize((677,30),Image.Resampling.NEAREST),(24,0));tx(im,(26,9),title+' - Microsoft Internet Explorer',12,'#ffffff')
 box(im,(77,97,644,113),'#ffffff');tx(im,(78,99),url,12)
 return im
def status(im,text):
 # Original source status bar's text field, not a new fake control.
 y=im.height-23;box(im,(25,y,340,y+17),'#ece9d8');tx(im,(26,y+4),text,11)
def slot(id,rect,n,color,values):return {'id':id,'rect_native_px':rect,'font_native_px':n,'color':color,'value':values}
def save(id,title,im,crop,times,fields,slots,shell,note):
 out=im.convert('RGB').resize((1200,800),Image.Resampling.NEAREST);out.save(OUT/(id+'-main.png'))
 sx=1200/im.width;sy=800/im.height;x,y,w,h=crop;r=[round(x*sx),round(y*sy),round(w*sx),round(h*sy)]
 detail=out.crop((r[0],r[1],r[0]+r[2],r[1]+r[3]));detail.save(OUT/(id+'-detail.png'))
 for s in slots:
  xx,yy,ww,hh=s['rect_native_px'];s['rect_output_px']=[round(xx*sx),round(yy*sy),round(ww*sx),round(hh*sy)];s['font_output_px']=round(s['font_native_px']*sy,2)
 M['states'].append({'id':id,'title':title,'preferred_range':times,'main':id+'-main.png','main_size':[1200,800],'detail':id+'-detail.png','detail_size':list(detail.size),'detail_crop_output_px':r,
  'detail_uv_rect':[round(r[0]/1200,6),round(1-(r[1]+r[3])/800,6),round(r[2]/1200,6),round(r[3]/800,6)],'native_canvas':list(im.size),
  'resampling':'nearest-neighbour, after native window content-only resize; source chrome controls retain relative position and remain unredrawn','source_id':shell,'fields':fields,'text_slots':slots,'nature':'original-state-reconstruction','limitations':note})
 return out
frames=[]
# 01 Native IE error page: one clearly recognisable error heading and remaining URL.
a=ie('Cannot find server','http://myweb.example/shortfilm/')
# Classic document/error symbol is a page-state icon, not a button.
box(a,(30,145,53,175),'#ffffff',outline='#a0a0a0');ImageDraw.Draw(a).line((35,151,47,164),fill='#a94343',width=2);ImageDraw.Draw(a).line((47,151,35,164),fill='#a94343',width=2)
tx(a,(68,148),'无法显示该网页',24)
lines(a,31,198,['您要查看的网页目前不可用。','该网站可能遇到技术问题，或需要调整浏览器设置。'],15,25)
ImageDraw.Draw(a).line((31,259,709,259),fill='#a6a6a6')
lines(a,31,283,['请尝试以下操作：','  检查地址是否正确。','  如果该地址来自收藏夹，请确认它仍然有效。','  使用工具栏中的“刷新”后重试。'],14,27)
tx(a,(31,423),'无法找到服务器或 DNS 错误',14,'#555555');tx(a,(31,450),'Internet Explorer',12,'#777777');status(a,'Done')
frames.append(save('01-page-unavailable','无法显示该网页',a,[25,143,695,106],[168.96,173.44],{'browser_title':'Cannot find server','url':'http://myweb.example/shortfilm/','heading':'无法显示该网页','error':'无法找到服务器或 DNS 错误'},
 [slot('error.heading',[68,148,640,33],24,'#111111','无法显示该网页'),slot('error.detail',[31,423,678,23],14,'#555555','无法找到服务器或 DNS 错误')],'desktop-f125ce10236d','Chinese failure body is authored; native English XP/IE6 toolbar is a real source screenshot. No actual request was made.'))
# 02 Real BitComet shell. The original titlebar and actual table positions carry stalled values.
b=Image.new('RGBA',(1024,683),'#ffffff');b.paste(BT.crop((0,0,1024,665)),(0,0));b.paste(BT.crop((0,690,1024,708)),(0,665))
b.paste(BT.crop((0,670,146,689)),(0,646));b.paste(BT.crop((1008,405,1024,689)).resize((16,260),Image.Resampling.NEAREST),(1008,405))
box(b,(146,111,1022,360),'#ffffff');box(b,(262,405,1006,664),'#ffffff');box(b,(32,225,141,645),'#ffffff')
b.paste(BT.crop((718,1,950,24)).resize((929,23),Image.Resampling.NEAREST),(21,1));tx(b,(22,4),'BitComet 0.53 - Download: 0 KB/s, Upload: 0 KB/s',12,'#ffffff')
lines(b,39,230,['我的种子','  shortfilm','  subtitles','  readme','本地文件'],12,22)
box(b,(148,112,1022,133),'#ece6df');ImageDraw.Draw(b).ellipse((153,117,164,128),fill='#af4c43')
for x,s in [(173,'shortfilm-v0.1.mkv'),(496,'128.00 MB'),(580,'37.5%'),(625,'0 KB/s'),(692,'0 KB/s'),(752,'--:--')]:tx(b,(x,116),s,12)
lines(b,268,410,['Torrent File:','SaveLocation:','Task State:','Downloaded:','Available:','Last Error:'],12,22)
lines(b,419,410,['shortfilm-v0.1.torrent',r'D:\Shared\Shortfilm','Stopped','48.00 MB / 128.00 MB','6 / 16 pieces','File not found'],12,22)
for j in range(16):
 x=270+(j%8)*89;y=558+(j//8)*18;box(b,(x,y,x+83,y+12),'#8997a7' if j<6 else '#e3e6e8',outline='#b5bdc4')
tx(b,(268,608),'下载已中断。',17,'#8b3931');tx(b,(268,637),'未完成的文件：shortfilm-v0.1.mkv.bc!',12,'#666666')
frames.append(save('02-download-interrupted','文件不存在 / 下载中断',b,[263,404,735,236],[173.44,177.92],{'filename':'shortfilm-v0.1.mkv','size_mb':128,'downloaded_mb':48,'percent':37.5,'pieces':{'received':6,'total':16},'task_state':'Stopped','error':'File not found','download_kbps':0,'upload_kbps':0},
 [slot('download.file',[173,116,290,20],12,'#111111','shortfilm-v0.1.mkv'),slot('download.error',[419,520,560,20],12,'#111111','File not found'),slot('download.status',[268,608,700,25],17,'#8b3931','下载已中断。')],'bitcomet-053-main','Only native shell controls and real column geometry are reused. New task, progress and error text are a generic authored failure, not an actual BitComet network transfer.'))
# 03 Cached original page. Broken-image frames use normal HTML image-state layout.
c=ie('原创短片与制作记录','http://myweb.example/shortfilm/')
box(c,(19,136,740,177),'#e7edf4');tx(c,(30,146),'原创短片与制作记录',22,'#1d426a')
tx(c,(31,195),'第一个版本  v0.1',18,'#333333');tx(c,(542,199),'[ 缓存副本 ]',13,'#777777')
ImageDraw.Draw(c).line((30,229,723,229),fill='#b4c3d2')
for x,y,w,h,name in [(31,249,322,145,'preview_01.jpg'),(381,249,322,145,'preview_02.jpg')]:
 box(c,(x,y,x+w,y+h),'#ffffff',outline='#b3b3b3');box(c,(x+4,y+4,x+19,y+19),'#f7f7f7',outline='#999999');ImageDraw.Draw(c).line((x+7,y+7,x+16,y+16),fill='#b94848',width=1);ImageDraw.Draw(c).line((x+16,y+7,x+7,y+16),fill='#b94848',width=1);tx(c,(x+25,y+6),name,13,'#555555')
tx(c,(31,419),'文件：shortfilm-v0.1.mkv',15,'#555555');tx(c,(31,451),'readme.txt',14,'#777777');status(c,'Working Offline')
frames.append(save('03-cached-page-missing-images','缓存旧页 / 缺图',c,[21,137,697,268],[177.92,182.40],{'document_title':'原创短片与制作记录','version':'v0.1','url':'http://myweb.example/shortfilm/','cache':'缓存副本','missing_images':['preview_01.jpg','preview_02.jpg'],'status':'Working Offline'},
 [slot('cache.title',[30,146,680,31],22,'#1d426a','原创短片与制作记录'),slot('cache.version',[31,195,450,25],18,'#333333','第一个版本  v0.1'),slot('cache.file',[31,419,670,24],15,'#555555','文件：shortfilm-v0.1.mkv')],'desktop-f125ce10236d','The page title and file belong to this film’s authored example, not a historical website. Missing-image boxes are browser content states, not decorative buttons.'))
# 04 The same original title survives while the actual work and its previews are gone.
d=ie('原创短片与制作记录','http://myweb.example/shortfilm/')
tx(d,(31,148),'原创短片与制作记录',23,'#1d426a');ImageDraw.Draw(d).line((31,187,720,187),fill='#c4c4c4')
tx(d,(31,216),'shortfilm-v0.1.mkv',17,'#666666');tx(d,(31,250),'404 Not Found',14,'#777777');status(d,'Done')
frames.append(save('04-title-with-dead-entry','仅剩标题与失效入口',d,[23,141,704,142],[182.40,186.88],{'document_title':'原创短片与制作记录','filename':'shortfilm-v0.1.mkv','error':'404 Not Found','url':'http://myweb.example/shortfilm/'},
 [slot('remaining.title',[31,148,690,34],23,'#1d426a','原创短片与制作记录'),slot('remaining.entry',[31,216,690,27],17,'#666666','shortfilm-v0.1.mkv'),slot('remaining.error',[31,250,690,24],14,'#777777','404 Not Found')],'desktop-f125ce10236d','Same authored document title/file as the cached page and stalled transfer. Whitespace is intentional. This is not a claim that any named real service disappeared.'))
# Separate QA artefact only. Performance PNGs have no source disclaimers or notes.
sheet=Image.new('RGB',(1500,1200),'#242b32');tx(sheet,(18,15),'末段失效状态 / QA ONLY · 原创状态复原',24,'#ffffff');tx(sheet,(18,49),'真实软件壳；正文与文件状态为原创。演出用 *-main.png / *-detail.png。',15,'#c7d3da')
for i,im in enumerate(frames):
 x=(i%2)*750;y=87+(i//2)*556;thumb=im.resize((750,500),Image.Resampling.NEAREST);sheet.paste(thumb,(x,y+28));tx(sheet,(x+12,y+4),M['states'][i]['title'],17,'#ffffff')
sheet.save(OUT/'qa-contact.png')
# Small review page explicitly separated from the performance plates.
html=['<!doctype html><meta charset="utf-8"><title>Failure UI QA</title><style>body{background:#242b32;color:#edf1f3;font:16px sans-serif;margin:24px}img{max-width:1200px;width:100%}section{margin:36px 0}.detail{width:auto;max-width:900px}</style><h1>末段失效状态 / QA</h1><p>来源说明仅在此页和metadata，不烧入演出图。新增状态均为原创复原。</p>']
for s in M['states']:html.append(f'<section><h2>{s["title"]}</h2><p>{s["limitations"]}</p><img src="{s["main"]}"><br><img class="detail" src="{s["detail"]}"></section>')
(OUT/'index.html').write_text('\n'.join(html),encoding='utf-8')
M['files']=[{'file':p.name,'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(OUT.glob('*.png'))]
(ROOT/'data/fullfilm-v2-failure-ui.json').write_text(json.dumps(M,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'states':len(M['states']),'png':len(M['files'])}))
