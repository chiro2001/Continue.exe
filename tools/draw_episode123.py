#!/usr/bin/env python3
"""Author editable SVG + matching raster UI/illustration layers for Episode 1–3."""
from __future__ import annotations

import html
import json
import math
import subprocess
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / 'art/source/episode123'
EXPORT = ROOT / 'art/export/episode123'
FONT = '/usr/share/fonts/noto-cjk/NotoSansCJK-Regular.ttc'
WINDOWS_FONT = Path.home()/'tmp/vrc-old-web-map/episode123/fonts/simsun.ttc'
MONO = '/usr/share/fonts/TTF/DejaVuSansMono.ttf'
if not Path(MONO).exists(): MONO = subprocess.check_output(['fc-match','monospace','-f','%{file}'],text=True)
MANIFEST = []


class Art:
    def __init__(self, name, size=(960, 640), bg=None, pixel=True):
        self.name, self.w, self.h, self.pixel = name, *size, pixel
        self.im = Image.new('RGBA', size, bg or (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.im)
        self.svg = []
        if bg: self.rect(0, 0, self.w, self.h, bg)

    def rect(self, x, y, w, h, fill, stroke=None, sw=1):
        self.d.rectangle((x, y, x+w-1, y+h-1), fill=fill, outline=stroke, width=sw)
        self.svg.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="{fill}" stroke="{stroke or "none"}" stroke-width="{sw}"/>')

    def line(self, points, fill, width=2):
        self.d.line(points, fill=fill, width=width, joint='curve')
        self.svg.append(f'<polyline points="{" ".join(f"{x},{y}" for x,y in points)}" fill="none" stroke="{fill}" stroke-width="{width}" stroke-linecap="round" stroke-linejoin="round"/>')

    def poly(self, points, fill, stroke=None, sw=2):
        self.d.polygon(points, fill=fill)
        if stroke: self.d.line(points+[points[0]], fill=stroke, width=sw, joint='curve')
        self.svg.append(f'<polygon points="{" ".join(f"{x},{y}" for x,y in points)}" fill="{fill}" stroke="{stroke or "none"}" stroke-width="{sw}"/>')

    def ellipse(self, x, y, w, h, fill, stroke=None, sw=2):
        self.d.ellipse((x,y,x+w,y+h), fill=fill, outline=stroke, width=sw)
        self.svg.append(f'<ellipse cx="{x+w/2}" cy="{y+h/2}" rx="{w/2}" ry="{h/2}" fill="{fill}" stroke="{stroke or "none"}" stroke-width="{sw}"/>')

    def text(self, x, y, text, size=20, fill='#202020', pixel=None, mono=False):
        use_pixel = pixel if pixel is not None else self.pixel
        windows = use_pixel and self.w >= 640 and WINDOWS_FONT.exists() and not mono
        fontfile = MONO if mono and Path(MONO).exists() else str(WINDOWS_FONT) if windows else FONT
        font = ImageFont.truetype(fontfile, size, index=0 if mono or windows else 2)
        if use_pixel:
            box = font.getbbox(text)
            mask = Image.new('L', (max(1,box[2]+4),size*2),0)
            ImageDraw.Draw(mask).text((0,-box[1]),text,font=font,fill=255)
            mask = mask.point(lambda v:255 if v >= 105 else 0)
            self.im.paste(fill,(int(x),int(y),int(x)+mask.width,int(y)+mask.height),mask)
        else:
            self.d.text((x,y),text,font=font,fill=fill,anchor='lt')
        family='SimSun, Noto Sans CJK SC' if windows else 'Noto Sans CJK SC'
        self.svg.append(f'<text x="{x}" y="{y}" dominant-baseline="hanging" font-family="{family}" font-size="{size}" fill="{fill}">{html.escape(text)}</text>')

    def bevel(self,x,y,w,h,fill='#c0c0c0',pressed=False):
        self.rect(x,y,w,h,fill)
        a,b=('#606060','#ffffff') if pressed else ('#ffffff','#606060')
        self.line([(x,y+h-1),(x,y),(x+w-1,y)],a,2)
        self.line([(x+w-1,y),(x+w-1,y+h-1),(x,y+h-1)],b,2)

    def window(self,title,x=0,y=0,w=None,h=None,xp=False):
        w,h=w or self.w,h or self.h
        self.bevel(x,y,w,h,'#ece9d8' if xp else '#c0c0c0')
        self.rect(x+4,y+4,w-8,34,'#145bc8' if xp else '#000080')
        self.text(x+13,y+10,title,19,'#ffffff')
        for i,t in enumerate(['_','□','×']):
            self.bevel(x+w-86+i*26,y+9,23,23,'#e9e9e9')
            self.text(x+w-82+i*26,y+10,t,16)
        self.text(x+14,y+47,'文件(F)   编辑(E)   查看(V)   工具(T)   帮助(H)',17)
        self.rect(x+8,y+74,w-16,h-101,'#ffffff','#808080')
        self.text(x+14,y+h-21,'原创演示内容 · 年代风格重建',13,'#535353')

    def browser(self,title,url):
        self.window(title+' - Microsoft Internet Explorer')
        self.rect(8,74,self.w-16,56,'#e2e0d9')
        self.text(20,83,'← 后退     →     停止     刷新     主页',19,'#343434')
        self.text(20,110,'地址(D)',15)
        self.rect(100,105,self.w-135,23,'#ffffff','#808080')
        self.text(110,108,url,15,'#343434')

    def save(self,kind='period-ui',refs=()):
        SOURCE.mkdir(parents=True,exist_ok=True);EXPORT.mkdir(parents=True,exist_ok=True)
        (SOURCE/(self.name+'.svg')).write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="{self.w}" height="{self.h}" viewBox="0 0 {self.w} {self.h}">'+''.join(self.svg)+'</svg>')
        self.im.save(EXPORT/(self.name+'.png'),optimize=True)
        MANIFEST.append({'id':self.name,'source':str((SOURCE/(self.name+'.svg')).relative_to(ROOT)),'export':str((EXPORT/(self.name+'.png')).relative_to(ROOT)),'width':self.w,'height':self.h,'kind':kind,'filter':'Point' if self.pixel else 'Bilinear','materials':list(refs),'authorship':'original-drawing-and-layout-reconstruction','historical_image':False})
        return self


def icon(a,x,y,label,color='#f3ce63'):
    a.rect(x,y+13,55,40,color,'#805f35',2);a.rect(x+4,y+6,24,12,color,'#805f35',2)
    a.text(x-5,y+65,label,16,'#ffffff')


def desktop():
    a=Art('desktop98',(960,720),bg='#008080');icon(a,35,25,'我的电脑','#b7c8ce');icon(a,35,140,'我的文档');icon(a,35,255,'网上邻居','#b7c8ce')
    a.window('欢迎连接到一个更大的世界',200,95,690,380)
    a.text(234,193,'把东西做出来，发给别人看看。',29)
    a.text(234,254,'工具、文字、图像，以及一个新想法。',21)
    a.bevel(640,372,195,47);a.text(673,386,'开始连接 →',22)
    a.bevel(250,505,520,150,'#414b54');a.rect(257,512,506,136,'#18201b');a.text(270,521,'MODULE PLAYER',18,'#b5c986')
    a.text(270,558,'FC14',29,'#b5d480');a.text(270,600,'4 CHANNELS',16,'#9bb57c');a.text(270,628,'▶  ▌▌  ■',16,'#b5c986')
    for i in range(4):a.rect(485+i*64,565,38,65,'#26372a','#435342');a.text(497+i*64,633,str(i+1),12,'#9bb57c')
    a.bevel(0,680,960,40);a.bevel(5,685,87,29);a.text(24,692,'开始',18)
    a.bevel(112,686,250,28,pressed=True);a.text(128,693,'欢迎连接',16)
    a.text(853,693,'21:04',17);a.save(refs=['desktop-win95','access-dialup'])
    a=Art('desktop_xp',(960,720),bg='#316fba');a.poly([(0,455),(180,370),(430,420),(690,340),(960,390),(960,720),(0,720)],'#63a942')
    icon(a,35,25,'我的电脑','#bad8eb');icon(a,35,140,'共享文档');a.window('一个正在制作的页面',180,115,700,360,True)
    a.text(225,223,'每个人都可以留下一点东西。',28,'#144780');a.text(225,280,'绘图 · 排版 · 代码 · 发布',23)
    a.rect(0,681,960,39,'#215ac3');a.rect(0,681,110,39,'#278a2a');a.text(30,691,'开始',22,'#ffffff');a.save()


def tools():
    a=Art('explorer',bg='#c0c0c0');a.window('共享工具箱');a.rect(8,74,205,539,'#ece9d8');a.text(24,96,'文件和文件夹',18,'#163e8c')
    for i,t in enumerate(['我的电脑','我的文档','共享工具箱','发布记录']):a.text(32,150+i*43,'▸ '+t,17)
    a.text(244,92,'名称                         类型        修改日期',18)
    for i,(name,typ) in enumerate([('README.txt','说明文件'),('images','文件夹'),('language.ini','配置文件'),('viewer.exe','应用程序'),('CHANGELOG.txt','修订记录')]):
        y=153+i*62;a.rect(235,y-8,690,49,'#dce7ff' if i==0 else '#ffffff');a.rect(246,y,22,27,'#f4d576','#6d6d6d');a.text(286,y+2,name,20);a.text(661,y+2,typ,18)
    a.save(refs=['software-green-sites','software-readme'])
    a=Art('readme');a.window('README.txt - 记事本');lines=['共享工具箱 / Release 0.4','','作者：河岸工具组（原创示例）','翻译、测试、修订，都记在这里。','','使用之前，请保留说明与作者信息。','遇到问题，可以把步骤写下来。','','CHANGELOG','+ 修正字体缺字','+ 加入一个新的预览功能']
    for i,t in enumerate(lines):a.text(28,96+i*40,t,22)
    a.save(refs=['software-readme'])
    a=Art('personal_home');a.browser('小小的主页','http://home.example/');a.text(45,155,'欢迎来到我的小站',34,'#1543a2')
    a.line([(36,207),(918,207)],'#245aaf',2)
    for i,t in enumerate(['首页','我的作品','下载','留言簿','友情链接']):a.text(45+i*178,228,t,22,'#0000ee')
    a.text(55,291,'这里放着涂鸦、照片，还有喜欢的音乐。',26);a.text(55,347,'更新很慢，想起来的时候就写一点 :)',22)
    for i,t in enumerate(['下载工具包  →','看看制作过程 →','给作者留言  →']):a.bevel(52+i*291,424,260,65);a.text(73+i*291,447,t,23,'#1c3d86')
    a.text(55,546,'Made by ordinary people.   最佳浏览：800 × 600',18,'#646464');a.save()
    a=Art('world_editor');a.window('World / Avatar · 制作演示',xp=True);a.rect(9,75,942,538,'#596779')
    for x in range(90,900,70):a.line([(480,120),(x,610)],'#718293')
    for y in [235,310,410,545]:a.line([(18,y),(941,y)],'#718293')
    a.rect(29,97,211,450,'#e6e6e6');a.text(48,120,'Hierarchy',20)
    for i,t in enumerate(['World','  Room','  Window','  Table','Avatar','  Armature','  Materials']):a.text(48,175+i*41,t,20)
    a.poly([(420,260),(630,220),(746,330),(540,370)],'#c8bc9c','#ece9d8',3)
    a.poly([(420,260),(540,370),(540,500),(420,392)],'#a18b6d');a.poly([(540,370),(746,330),(746,460),(540,500)],'#c3ad89')
    a.line([(593,174),(593,295),(530,345),(593,295),(658,345),(593,295),(575,410),(593,295),(622,410)],'#f0d069',5)
    a.ellipse(573,135,40,40,'#cfdfe8');a.text(292,563,'形状、骨骼和材质，在这里逐步调整。',22,'#ffffff');a.save()


def phone():
    a=Art('phone_feature',(320,420),bg='#b4caad');a.text(16,12,'▮▮▮             ▰',19,'#203f27');a.line([(12,43),(307,43)],'#25462d',2)
    a.text(54,67,'消息',35,'#203f27')
    for i,t in enumerate(['新信息  (1)','通讯录','互联网','工具箱']):
        if i==0:a.rect(14,132,290,49,'#294930')
        a.text(30,146+i*56,t,25,'#dce8d5' if i==0 else '#203f27')
    a.text(20,381,'选择             返回',21,'#203f27');a.save()
    a=Art('phone_early',(360,560),bg='#f3f3f3',pixel=False);a.rect(0,0,360,40,'#2e3d4a');a.text(18,12,'3G           21:04       ▰',17,'#ffffff')
    a.rect(0,40,360,59,'#dae6ed');a.text(24,60,'浏览器',26);a.rect(12,116,336,40,'#ffffff','#8b9ca8');a.text(24,128,'m.example / 我的相册',16)
    a.text(25,193,'朋友的新照片',28,'#315a83');landscape(a,26,247,307,169);a.text(26,449,'今天一起完成的东西。',23);a.text(26,507,'◀        ●         ▣',25,'#315a83');a.save()


def landscape(a,x,y,w,h):
    a.rect(x,y,w,h,'#b8d7e6');a.ellipse(x+w*.7,y+h*.12,w*.12,w*.12,'#fff0b4')
    a.poly([(x,y+h*.70),(x+w*.32,y+h*.32),(x+w*.67,y+h*.70)],'#89aaa3')
    a.poly([(x+w*.35,y+h*.78),(x+w*.74,y+h*.25),(x+w,y+h*.70)],'#5f9490')
    a.rect(x,y+h*.76,w,h*.24,'#b9c59b')


def manga():
    a=Art('manga_ink',(800,700),bg='#fffdf6',pixel=False)
    for x,y,w,h in [(20,20,470,285),(510,20,270,285),(20,325,760,355)]:a.rect(x,y,w,h,'#fffdf6','#302d32',4)
    a.rect(42,194,424,88,'#d7dfdb');a.rect(526,195,235,88,'#d7dfdb');a.rect(34,494,730,162,'#d7dfdb')
    a.line([(76,163),(76,283)],'#647984',6);a.rect(49,144,55,45,'#a3bfcb','#302d32',3);a.text(55,159,'BUS',16,'#263c49',pixel=False)
    for x in range(46,468,43):a.line([(x,160),(x-7,184)],'#91a4a8',2)
    canopy=[(424+240*math.cos(k*math.pi/32),435-95*math.sin(k*math.pi/32)) for k in range(33)]
    canopy += [(184,435),(244,448),(304,435),(364,448),(424,435),(484,448),(544,435),(604,448),(664,435)]
    a.poly(canopy,'#9db9c2','#302d32',4);a.line([(424,360),(424,597),(449,613)],'#302d32',5)
    for index,(x,y,s) in enumerate([(169,119,1),(600,143,.8),(300,460,1.05),(548,470,1.0)]):
        a.poly([(x-45*s,y+70*s),(x-65*s,y+188*s),(x+75*s,y+188*s),(x+52*s,y+75*s)],'#aab5a4' if index%2==0 else '#c2a594','#302d32',4)
        a.ellipse(x-45*s,y-20*s,100*s,110*s,'#f1ddc8','#302d32',4)
        a.poly([(x-48*s,y+20*s),(x-23*s,y-48*s),(x+55*s,y-24*s),(x+60*s,y+37*s),(x+24*s,y+3*s)],'#555064','#302d32',3)
        a.line([(x-13*s,y+35*s),(x-2*s,y+35*s)],'#302d32',4);a.line([(x+27*s,y+35*s),(x+38*s,y+35*s)],'#302d32',4)
        a.line([(x+7*s,y+65*s),(x+22*s,y+68*s)],'#302d32',3)
    a.save('illustration-layer',['translation-manga-workflow'])
    a=Art('manga_bubbles',(800,700),pixel=False)
    for x,y,w,h in [(51,38,310,83),(526,40,218,86),(42,348,246,99),(498,346,250,99)]:a.ellipse(x,y,w,h,'#ffffff','#302d32',3)
    a.save('illustration-layer')
    for name,lines in [('manga_original',['The bus is late.','We can walk.','It is raining.','Share my umbrella.']),('manga_chinese',['车又晚点了。','走过去吧。','下雨了。','一起撑伞吧。'])]:
        a=Art(name,(800,700),pixel=False)
        for (x,y),t in zip([(91,65),(575,67),(70,382),(532,381)],lines):a.text(x,y,t,24)
        a.save('illustration-layer')


def subtitle():
    a=Art('subtitle_editor');a.window('字幕工程 - 时间轴 / 原创示意');a.rect(20,90,535,302,'#293c4c');landscape(a,35,105,504,270)
    a.rect(574,91,365,300,'#edf1f2');a.text(592,112,'对白与译文',22,'#325879')
    for i,t in enumerate(['00:02.40 → 00:04.20','Let us try it together.','一起试一下吧。','翻译 / 校对 / 时间轴']):a.text(592,166+i*53,t,18)
    a.rect(20,411,919,89,'#e5ecda')
    for i in range(180):
        h=8+int(abs(math.sin(i*.31)*math.sin(i*.117))*64);a.line([(26+i*5,456-h/2),(26+i*5,456+h/2)],'#4e7358',2)
    for i,(x,w) in enumerate([(36,180),(229,220),(463,155),(638,246)]):a.rect(x,520,w,38,['#9fb7d2','#c5a7d3','#9fc3ac','#d2bd91'][i],'#6c7580');a.text(x+10,531,['对白 01','对白 02','对白 03','对白 04'][i],16)
    a.save(refs=['translation-anime-fansub','translation-subtitle-style-karaoke'])
    a=Art('animation_bg',(960,540),bg='#c9dfde',pixel=False);a.rect(0,385,960,155,'#b9ac91');a.rect(80,40,520,287,'#eff4df','#6c838b',8);landscape(a,91,51,498,265);a.line([(340,45),(340,323)],'#6c838b',8);a.save('illustration-layer')
    a=Art('animation_mid',(960,540),pixel=False);a.rect(110,360,700,45,'#91755c');a.poly([(145,405),(180,405),(170,535),(135,535)],'#6f5745');a.poly([(745,405),(780,405),(790,535),(755,535)],'#6f5745');a.rect(320,320,240,35,'#f6ecd5','#807f74',3);a.save('illustration-layer')
    a=Art('animation_front',(960,540),pixel=False);a.ellipse(753,147,112,145,'#f1e6d6','#485964',4);a.poly([(765,161),(760,117),(799,148)],'#f1e6d6','#485964',3);a.poly([(823,148),(857,118),(857,169)],'#f1e6d6','#485964',3);a.ellipse(778,206,8,9,'#42515e');a.ellipse(826,206,8,9,'#42515e');a.poly([(748,276),(870,276),(901,486),(725,486)],'#9eb9af','#485964',4);a.save('illustration-layer')
    a=Art('subtitle_line',(960,170),pixel=False);a.text(182,55,'一起试一下吧。',56,'#ffffff');a.save('illustration-layer')


def distribution():
    a=Art('release_post');a.browser('作品发布 / 修订版','http://bbs.example/release/');a.rect(22,149,916,49,'#d7e6f1');a.text(42,166,'[发布] 原创短片与工具包 · 修订 02',24,'#224c72')
    a.text(42,227,'制作 / 翻译 / 校对 / 时间轴 / 测试',25);a.text(42,282,'这些名字，请和作品一起留下。',24)
    for i,t in enumerate(['文件：release_02.zip','说明：README + CHANGELOG','状态：已完成校验','发布后仍在提供已有分块']):a.text(44,349+i*50,t,22)
    a.save(refs=['sharing-bt-release-indexes','translation-credits-readme'])
    a=Art('bt_transfer');a.window('传输任务');a.text(35,103,'任务：原创演示内容',27);a.text(35,159,'来自不同地方的部分，正在拼合。',24)
    for i in range(64):a.rect(36+(i%16)*54,230+(i//16)*43,45,32,'#e6edf2','#9cabb4')
    a.text(35,441,'下载       ↓                   上传       ↑',25,'#245d91');a.rect(36,500,860,34,'#e9e9e9','#808080');a.text(35,563,'完成之后，还可以继续提供已有的部分。',22);a.save(refs=['sharing-bt-pieces-upload'])
    a=Art('credits',(800,640),bg='#f9f5df');a.text(54,42,'制作与贡献',35,'#243b61');a.line([(52,102),(744,102)],'#9ea9a7')
    for i,(role,name) in enumerate([('原作 / 图形','河岸'),('翻译 / 校对','青禾 · 木木'),('时间轴 / 排版','阿澄'),('测试 / 整理','小北 · 路过的人'),('修订 / 维护','新加入的伙伴')]):a.text(54,146+i*83,role,23,'#506170');a.text(330,146+i*83,name,25,'#243b61')
    a.text(54,587,'原创演示名单，非真实组史或人员记录。',16,'#727b7b');a.save(refs=['translation-credits-readme'])


def communities():
    a=Art('tianya');a.browser('天涯社区 · 讨论与回帖','http://www.tianya.cn/');a.text(35,153,'天涯社区',35,'#234b79');a.text(296,165,'天涯杂谈   读书   天涯摄影   舞文弄墨',21,'#234b79');a.rect(24,213,912,39,'#dae5ed');a.text(42,224,'[原创示例] 毕业那年，我们约好去看海',23)
    for i,(who,t) in enumerate([('楼主','把这些平常的日子写下来，免得以后忘记。'),('1 楼','先占个沙发，慢慢看。'),('2 楼','楼主，后来你们去了吗？')]):
        y=282+i*74;a.text(42,y,who,19,'#335e91');a.text(175,y,t,20);a.line([(35,y+54),(917,y+54)],'#d7dfe3',1)
    a.save(refs=['cn-tianya-long-threads'])
    reply=Art('tianya_reply');reply.rect(31,490,898,71,'#ffffff');reply.text(42,504,'3 楼',19,'#335e91');reply.text(175,504,'翻到这一页了，故事还在继续。',20);reply.line([(35,558),(917,558)],'#d7dfe3',1);reply.save('graphic-layer',['cn-tianya-long-threads'])
    a=Art('xiaonei');a.browser('校内网 · 我的相册','http://www.xiaonei.com/');a.rect(20,146,920,64,'#29699c');a.text(42,164,'校内网',34,'#ffffff');a.text(285,174,'首页   我的主页   好友   相册   日志',21,'#ffffff')
    a.text(36,238,'最近的照片',27,'#275983')
    for i in range(3):
        x=39+i*303;landscape(a,x,295,278,179)
        if i==0:
            a.rect(x+45,330,188,119,'#e3d4ad','#657882',2)
            for row in range(3):
                for col in range(4):a.rect(x+60+col*41,343+row*30,22,20,'#718f9b')
        if i==1:
            a.rect(x+8,295,263,179,'#f1cc9e');a.ellipse(x+165,311,55,55,'#fff1c4');a.poly([(x,444),(x+110,383),(x+278,437),(x+278,474),(x,474)],'#829d92')
        a.text(47+i*303,497,['食堂门口','毕业旅行','放学以后'][i],23)
    a.text(40,562,'这些平常的日子，也值得被保存。',22,'#546b7b');a.save(refs=['cn-renren-xiaonei'])
    a=Art('weibo');a.browser('新浪微博 · 新鲜事','http://t.sina.com.cn/');a.rect(20,146,920,59,'#f0b34e');a.text(40,160,'新浪微博',32,'#8d3b1b');a.text(44,228,'有什么新鲜事想告诉大家？',24,'#744823');a.rect(39,272,873,82,'#fffef8','#d4b58c');a.text(58,292,'坐末班车回家，路边的灯一盏盏亮起来。',25)
    for i,t in enumerate(['朋友甲：拍得真好。','朋友乙：转发，想起以前放学的路。','朋友丙：我也走过这里。']):a.ellipse(43,389+i*63,36,36,['#99b1ae','#d1ab98','#b0a6c7'][i]);a.text(97,396+i*63,t,22)
    a.save(refs=['cn-sina-weibo-early'])


def vr_and_conditions():
    a=Art('vrchat2017');a.window('VRChat · 世界与 Avatar / 2017 参考');a.rect(16,84,928,514,'#e4eced');a.text(43,109,'WORLDS',34,'#4c6679')
    for i,(label,col) in enumerate([('THE HUB','#b9cdd1'),('AVATAR HALL','#dbc89c'),('COMMUNITY','#b8c6af')]):
        x=40+i*305;a.rect(x,185,278,230,'#c5e4e8' if i<2 else '#b9b7ca');a.rect(x,361,278,54,'#879ca4')
        if i==0:
            a.ellipse(x+54,271,171,44,'#c8c6a0','#8d906f',3);a.rect(x+105,274,20,113,'#e0e2db');a.rect(x+170,274,20,113,'#e0e2db');a.rect(x+99,271,97,19,'#e0e2db')
            for side in [118,160]:
                for j in range(3):a.rect(x+side,204+j*24,16,20,'#d5dce0')
        else:
            for j in range(4):
                px=x+31+j*60;a.ellipse(px,276+(j%2)*12,27,29,['#b8cfbf','#cbaaa6','#aab8cf','#d2c39c'][j]);a.rect(px+3,305+(j%2)*12,22,56,'#687f8c');a.ellipse(px-4,363,37,12,'#d1d4c5')
            if i==1:a.rect(x+14,211,250,30,'#d5d5bb');a.text(x+41,218,'AVATAR HALL',21,'#4a6877')
        a.rect(x,417,278,60,col);a.text(x+17,438,label,23,'#294355')
    a.text(43,529,'早期空间图像与布局参考 · 具体重建见世界内来源',18,'#596e79');a.save(refs=['vrc-2017-hub','vrc-avatar-pedestals'])
    a=Art('help_thread');a.browser('求助与回复','http://bbs.example/help/');a.text(39,159,'[求助] 有些字显示不出来，怎么办？',27,'#214d7b')
    for i,t in enumerate(['楼主：把步骤和截图补在这里了。','回复：先看看字符集与字体。','楼主：这次能正常显示了，谢谢！','回复：把说明也补上，后面的人能看到。']):a.rect(30,224+i*84,899,69,'#edf1f4' if i%2==0 else '#ffffff','#d9e1e5');a.text(50,245+i*84,t,22)
    a.save()
    a=Art('error_dialog',(660,330));a.window('提示');a.ellipse(32,107,70,70,'#c83b37');a.line([(52,128),(82,158)],'#ffffff',8);a.line([(82,128),(52,158)],'#ffffff',8);a.text(125,112,'程序暂时没有响应。',27);a.text(125,166,'先把问题的步骤记下来。',23);a.bevel(459,232,144,45);a.text(497,246,'确定',23);a.save()
    a=Art('access_conditions');a.browser('访问与使用条件','http://service.example/');a.text(40,164,'继续之前，需要完成一些步骤。',29,'#375778')
    for i,t in enumerate(['登录与访问条件','版本与兼容性','许可与使用范围']):a.rect(40,242+i*96,876,74,'#edf2f5','#a4b7c3');a.text(64,267+i*96,t,25);a.bevel(718,258+i*96,170,43);a.text(750,271+i*96,'查看条件',20)
    a.save(refs=['enclosure-subscription'])
    a=Art('news_3q',(900,620),bg='#f7f6f0');a.text(48,43,'2010 · 入口与选择',35,'#2a425d');a.line([(48,108),(852,108)],'#a8aead',2)
    for i,t in enumerate(['3Q 事件','QQ 与 360 的兼容与选择冲突。','熟悉的软件，改变了继续使用的条件。','当时的公告与报道记录下了这次冲突。']):a.text(49,157+i*87,t,30 if i==0 else 25,'#243b51')
    a.text(49,552,'依据 2010-11-03 同期报道 · 概述与自绘演示',18,'#677583');a.save(refs=['enclosure-3q'])
    a=Art('eac_notice',bg='#263e49',pixel=False);a.rect(0,0,960,72,'#216c7b');a.text(36,22,'VRChat / 安全更新 · 2022',27,'#f2f4ee')
    a.text(42,113,'安全、便利与能力的取舍',31,'#e3eeee')
    for i,t in enumerate(['EASY ANTI-CHEAT','加入反作弊，防范恶意客户端。','部分辅助与便利功能需求受到影响。','随后公开回应无障碍与 QoL 反馈。']):
        a.rect(30,187+i*78,900,64,'#35535f');a.text(50,208+i*78,t,27 if i==0 else 24,'#e0eceb')
    a.text(43,578,'2022-07-25 / 07-26 官方公开说明 · 自绘概述',17,'#a5c4ca');a.save('contemporary-ui',['vrc-eac-2022','vrc-eac-accessibility-response'])
    a=Art('blue_bear',(400,420),pixel=False);a.ellipse(54,40,105,105,'#6bb4e9');a.ellipse(243,40,105,105,'#6bb4e9');a.ellipse(54,76,294,281,'#6bb4e9');a.ellipse(112,163,34,43,'#18394e');a.ellipse(258,163,34,43,'#18394e');a.ellipse(146,221,111,87,'#d7effb');a.ellipse(179,235,45,32,'#18394e');a.save('illustration-layer',['vrc-eac-2022'])
    a=Art('maintenance_queue');a.window('待处理事项');a.text(38,108,'让它继续可用，需要有人处理。',29,'#344e65')
    for i,(n,t) in enumerate([('#21','旧版本无法打开'),('#22','字体缺字'),('#23','依赖需要更新'),('#24','缺少复现步骤'),('#25','文档与版本不一致')]):
        y=190+i*73;a.rect(30,y,901,59,'#f0f3f5','#d0dbe1');a.ellipse(48,y+17,23,23,'#74977a');a.text(92,y+17,n,20,'#607a8a');a.text(182,y+17,t,23)
    a.save(refs=['commons-report-flood'])


def stickers():
    for name,t,col in [('sticker_orz','orz','#f0cd7d'),('sticker_smile',':)','#a8d4cf'),('sticker_greetz','greetz!','#c5b5df'),('sticker_hello','你好！','#d5e6cc'),('sticker_jiong','囧','#f0cd7d'),('sticker_233','233','#b5d4ca')]:
        a=Art(name,(512,220));a.text(34,44,t,106,col);a.save('graphic-layer')
    a=Art('pixel_star',(64,64));a.poly([(32,4),(39,24),(60,24),(43,37),(49,59),(32,46),(15,59),(21,37),(4,24),(25,24)],'#ffe2a0');a.save('graphic-layer')
    a=Art('panda_icon',(320,330),pixel=False);a.ellipse(23,20,91,93,'#252935');a.ellipse(208,20,91,93,'#252935');a.ellipse(42,53,238,245,'#f0f0e5','#252935',5);a.ellipse(83,119,60,79,'#252935');a.ellipse(186,119,60,79,'#252935');a.ellipse(111,138,17,22,'#ffffff');a.ellipse(199,138,17,22,'#ffffff');a.ellipse(146,204,36,23,'#252935');a.save('illustration-layer',['security-panda'])


def main():
    desktop();tools();phone();manga();subtitle();distribution();communities();vr_and_conditions();stickers()
    (ROOT/'data/episode123-art.json').write_text(json.dumps({'version':1,'status':'authored-raster-and-editable-vector-sources','generator':'tools/draw_episode123.py','fonts':{'windows_ui':'Local Windows SimSun rasterization; original system font remains in task tmp and is not redistributed' if WINDOWS_FONT.exists() else 'Noto Sans CJK SC fallback','illustrations_and_mobile':'Noto Sans CJK SC (OFL, installed system font)'},'evidence':'Drawn layouts and authored example content, not archival screenshots. Named historical events inherit catalogue verification scope.','assets':MANIFEST},ensure_ascii=False,indent=2)+'\n')
    thumbs=[]
    for item in MANIFEST:
        im=Image.open(ROOT/item['export']);im.thumbnail((300,200),Image.Resampling.NEAREST)
        tile=Image.new('RGB',(330,245),'#263848');tile.paste(im,((330-im.width)//2,9),im)
        ImageDraw.Draw(tile).text((12,218),item['id'],font=ImageFont.truetype(MONO,15),fill='#f0eadc');thumbs.append(tile)
    sheet=Image.new('RGB',(330*5,245*math.ceil(len(thumbs)/5)),'#263848')
    for i,im in enumerate(thumbs):sheet.paste(im,((i%5)*330,(i//5)*245))
    review=Path.home()/'tmp/vrc-old-web-map/episode123/art';review.mkdir(parents=True,exist_ok=True);sheet.save(review/'contact-sheet.png')
    print(f'{len(MANIFEST)} SVG sources and PNG exports; contact sheet: {review / "contact-sheet.png"}')


if __name__=='__main__':main()
