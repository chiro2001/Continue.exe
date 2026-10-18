#!/usr/bin/env python3
"""Prepare static native UI plates from read-only project references. No runtime code."""
from pathlib import Path
import hashlib
import json
import shutil
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'art/export/fullfilm-v2-ui/native-overlays'
OUT.mkdir(parents=True, exist_ok=True)
FONT = '/usr/share/fonts/noto-cjk/NotoSansCJK-Regular.ttc'
MONO = '/usr/share/fonts/TTF/DejaVuSansMono.ttf'
SCORE = json.loads((ROOT/'data/episode123-v4-score.json').read_text())
SOURCE_MAP = {x['id']:x for x in SCORE['images']}
ART_MAP = {x['export']:x for x in json.loads((ROOT/'data/episode123-art.json').read_text())['assets']}
MANIFEST = {'schema':'fullfilm-v2-native-ui.1', 'generated_by':str(Path(__file__).relative_to(ROOT)),
 'nature':'Static authored derivative states. Historical screenshot backgrounds are references, not evidence for the new content.',
 'coordinates':'pixel rectangles [x,y,width,height], top-left origin; uv rectangles [u,v,width,height], bottom-left origin',
 'presentation':'Performance plates contain native application/document text only. Provenance is in metadata and separate QA plates.',
 'groups':[]}


def font(n=16, mono=False):
    try:return ImageFont.truetype(MONO if mono else FONT,n)
    except OSError:return ImageFont.truetype(FONT,n)


def text(im, xy, s, size=16, color='#202020', mono=False):
    # Bilevel masks preserve screenshot-like pixel glyphs at source resolution.
    f=font(size,mono);d=ImageDraw.Draw(im)
    if not s:return
    box=d.textbbox((0,0),s,font=f,anchor='lt')
    mask=Image.new('L',(max(1,box[2]+2),max(1,box[3]+2)))
    ImageDraw.Draw(mask).text((0,0),s,font=f,fill=255,anchor='lt')
    mask=mask.point(lambda p:255 if p>100 else 0)
    plate=Image.new('RGBA',mask.size,color);im.paste(plate,tuple(map(int,xy)),mask)


def lines(im, xy, seq, size=16, gap=28, color='#202020', mono=False):
    for i,s in enumerate(seq):text(im,(xy[0],xy[1]+i*gap),s,size,color,mono)


def rect(im, box, fill=None, outline=None, width=1):
    ImageDraw.Draw(im).rectangle(box,fill=fill,outline=outline,width=width)


def uv(r,size):
    x,y,w,h=r;W,H=size
    return [round(x/W,6),round(1-(y+h)/H,6),round(w/W,6),round(h/H,6)]


def pxbox(r):
    x,y,w,h=r;return x,y,x+w-1,y+h-1


def button(im, r, caption, size=16, active=False):
    x,y,w,h=r;d=ImageDraw.Draw(im)
    d.rectangle(pxbox(r),fill='#c0c0c0')
    a,b=('#808080','#ffffff') if active else ('#ffffff','#606060')
    d.line((x,y+h-1,x,y,x+w-1,y),fill=a,width=1)
    d.line((x+w-1,y,x+w-1,y+h-1,x,y+h-1),fill=b,width=1)
    tw=d.textbbox((0,0),caption,font=font(size),anchor='lt')[2]
    text(im,(x+(w-tw)//2,y+(h-size)//2),caption,size)


class Group:
    def __init__(self,id,title,size,span,source_ids=(),source_files=(),note='',native_controls=()):
        self.id=id;self.title=title;self.size=size;self.dir=OUT/id;self.dir.mkdir(exist_ok=True)
        self.base=Image.new('RGBA',size,'#ffffff');self.fixed=Image.new('RGBA',size)
        self.data={'id':id,'title':title,'width':size[0],'height':size[1],'preferred_range':span,
         'kind':'original-dynamic-reconstruction','source_metadata':[],
         'reference_files':[],'background':f'{id}/background.png','fixed_overlay':f'{id}/fixed.png',
         'states':[],'change_regions':[],'text_slots':[],'native_controls_preserved':list(native_controls),
         'provenance_note':note,'source_placements':[],
         'suggested_phase_points':[0,.28,.59,.83],
         'display_rule':'Keep one background anchored throughout this group; choose a full state OR composite fixed + primary + secondary. Do not stack a full state and its same layers.'}
        for sid in source_ids:
            s=SOURCE_MAP[sid];self.data['source_metadata'].append(dict(s))
            p=Path(s['source_path']);self.ref(p,sid)
        for rel in source_files:
            self.ref(ROOT/rel,Path(rel).stem)
            if rel in ART_MAP:self.data['source_metadata'].append({'catalogue':'data/episode123-art.json',**ART_MAP[rel]})
    def ref(self,p,id):
        name='reference-'+id+p.suffix
        shutil.copyfile(p,self.dir/name)
        self.data['reference_files'].append({'file':f'{self.id}/{name}','source_path':str(p),
         'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'dimensions':list(Image.open(p).size)})
    def paste(self,rel,xy=(0,0)):
        p=ROOT/rel if not Path(rel).is_absolute() else Path(rel)
        im=Image.open(p).convert('RGBA');self.base.alpha_composite(im,xy)
        self.data['source_placements'].append({'source_path':str(p),'rect_px':[*xy,*im.size],'resampled':False})
    def region(self,id,r,role):
        self.data['change_regions'].append({'id':id,'rect_px':r,'uv_rect':uv(r,self.size),'role':role})
    def slot(self,id,r,size,color,values,role='text',align='top-left'):
        self.data['text_slots'].append({'id':id,'rect_px':r,'uv_rect':uv(r,self.size),'font_px':size,
          'font':'Noto Sans CJK SC pixel-thresholded','color':color,'alignment':align,'role':role,'state_values':values})
    def save(self,states):
        self.base.save(self.dir/'background.png');self.fixed.save(self.dir/'fixed.png')
        merged=[]
        for i,(label,primary,secondary,fields) in enumerate(states):
            stem=f'state-{i:02d}';im=self.base.copy();im.alpha_composite(self.fixed)
            im.alpha_composite(primary);im.alpha_composite(secondary)
            im.convert('RGB').save(self.dir/(stem+'.png'))
            row={'index':i,'title':label,'full':f'{self.id}/{stem}.png','fields':fields,'layers':[]}
            for name,layer in [('primary',primary),('secondary',secondary)]:
                layer.save(self.dir/(stem+'-'+name+'.png'));box=layer.getbbox()
                if not box:continue
                r=[box[0],box[1],box[2]-box[0],box[3]-box[1]]
                crop=stem+'-'+name+'-crop.png';layer.crop(box).save(self.dir/crop)
                row['layers'].append({'id':name,'full':f'{self.id}/{stem}-{name}.png','crop':f'{self.id}/{crop}',
                  'rect_px':r,'uv_rect':uv(r,self.size),'pivot_px':[r[0]+r[2]/2,r[1]+r[3]/2]})
            self.data['states'].append(row);merged.append(im)
        # Clearly separated QA-only plate. Never use it as the performance texture.
        tw=760;th=int(tw*self.size[1]/self.size[0]);sheet=Image.new('RGB',(tw*2,(th+54)*2+72),'#20252b')
        qd=ImageDraw.Draw(sheet);text(sheet,(18,14),self.title+' / QA ONLY · 原创动态复原',23,'#ffffff')
        text(sheet,(18,45),'红框＝固定主素材内的变化区；原图与纯演出版分别保存。',15,'#b7c4cd')
        for i,im in enumerate(merged):
            thumb=im.convert('RGB').resize((tw,th),Image.Resampling.NEAREST)
            x=(i%2)*tw;y=72+(i//2)*(th+54)
            sheet.paste(thumb,(x,y+28));text(sheet,(x+12,y+5),f'{i:02d}  '+states[i][0],17,'#ffffff')
            for rr in self.data['change_regions']:
                xx,yy,ww,hh=rr['rect_px'];scale=tw/self.size[0]
                qd.rectangle((x+xx*scale,y+28+yy*scale,x+(xx+ww)*scale,y+28+(yy+hh)*scale),outline='#e36566',width=2)
        sheet.save(self.dir/'qa-preview.png')
        self.data['qa_preview']=f'{self.id}/qa-preview.png'
        MANIFEST['groups'].append(self.data)


def blank(g):return Image.new('RGBA',g.size)


def readme():
    g=Group('readme','README · 留下可继续的说明',(960,640),[10.24,20.48],
      source_files=['art/export/episode123/readme.png','art/reference/episode123-v2/win98-explorer-vm.png'],
      note='README source is an existing original period-style document. Explorer screenshot is a separate read-only reference; this is not a historical README.',
      native_controls=[{'id':'min-max-close','rect_px':[874,9,75,23]},{'id':'menu','rect_px':[14,47,550,22]}])
    g.paste('art/export/episode123/readme.png')
    rect(g.base,(9,75,950,612),'#ffffff');rect(g.base,(9,617,950,636),'#c0c0c0')
    lines(g.fixed,(28,96),['共享工具箱 / Release 0.4','','作者：河岸工具组','','使用之前，请保留说明与作者信息。'],22,40)
    text(g.fixed,(28,404),'CHANGELOG',20,'#202020',True)
    g.region('document',[24,294,884,92],'README body changes while document and title stay anchored')
    g.region('changelog',[24,442,884,110],'Second content area changes with the same edit')
    v=['如果遇到问题，','如果遇到问题，可以把步骤写下来。','如果遇到问题，可以把步骤写下来。','如果遇到问题，可以把步骤写下来。']
    g.slot('README.body',[28,304,850,30],22,'#202020',v)
    g.slot('README.document_title',[28,96,850,28],22,'#202020',['共享工具箱 / Release 0.4']*4,role='fixed_document_title')
    g.slot('README.credit',[28,347,850,28],20,'#202020',['','翻译、测试、修订，都记在这里。','翻译、测试、修订，都记在这里。','翻译、测试、修订，都记在这里。'])
    g.slot('README.changelog',[28,452,850,80],20,'#202020',['+ 修正字体缺字','+ 修正字体缺字\n+ 加入一个新的预览功能','+ 修正字体缺字\n+ 加入一个新的预览功能\n+ 补充复现步骤与测试结果','+ 修正字体缺字\n+ 加入一个新的预览功能\n+ 补充复现步骤与测试结果'])
    ss=[]
    for i,label in enumerate(['写下问题','补齐贡献者','记录修订','保留说明一起发布']):
        p=blank(g);s=blank(g);text(p,(28,304),v[i],22)
        if i>0:text(p,(28,347),'翻译、测试、修订，都记在这里。',20)
        if i==2:rect(p,(25,342,682,377),'#000080');text(p,(28,347),'翻译、测试、修订，都记在这里。',20,'#ffffff')
        ch=['+ 修正字体缺字']
        if i>0:ch+=['+ 加入一个新的预览功能']
        if i>1:ch+=['+ 补充复现步骤与测试结果']
        lines(s,(28,452),ch,20,31)
        text(s,(15,620),['第 8 行，第 8 列','第 9 行，第 18 列','选定 1 行','README.TXT  已保存'][i],12,'#535353')
        if i==3:text(s,(28,565),'readme.txt 与源文件一起保留。',18,'#45624d')
        ss.append((label,p,s,{'filename':'README.TXT','release':'0.4','characters':[7,20,20,20][i],'modified':i<3}))
    g.save(ss)


def manga():
    g=Group('manga','漫画汉化 · 同一页台词进入画面',(800,540),[30.72,40.96],source_ids=['workflow-manga-2'],
      source_files=['art/export/episode123-v4-workflows/workflow-manga-1.png','art/export/episode123-v4-workflows/workflow-manga-4.png'],
      note='Existing original comic artwork and period-style workflow window. New dialogue and proofreading states are authored, not a real manga/team screenshot.',
      native_controls=[{'id':'min-max-close','rect_px':[736,7,54,17]},{'id':'layer_rows','rect_px':[420,350,352,119]}])
    g.paste('art/export/episode123-v4-workflows/workflow-manga-2.png')
    rect(g.base,(402,77,785,337),'#ffffff');rect(g.base,(7,519,791,535),'#c0c0c0')
    # Clear only the bubble's text. The original artwork, speech balloon and panel borders stay intact.
    rect(g.base,(205,129,347,161),'#ffffff')
    text(g.fixed,(415,86),'台词表  page01.txt',20,'#000080')
    lines(g.fixed,(415,132),['001  今日はここに。','002  手伝うよ。','003  また明日。'],16,58)
    g.region('bubble',[203,126,148,40],'Original artwork remains; text inside its existing balloon changes')
    g.region('translation_and_layers',[414,157,360,315],'Translation lines, active layer and proofreading result')
    g.slot('comic.balloon',[211,134,134,25],18,'#202020',['手伝うよ。','手伝うよ。','手伝うよ。','我来帮你。'])
    g.slot('comic.translation',[426,157,345,145],16,'#202020',['','今天就在这里。\n我来帮你。','今天就在这里。\n我来帮你。\n明天见。','今天就在这里。\n我来帮你。\n明天见。'])
    ss=[]
    for i,label in enumerate(['整理原文','逐句翻译','清字与嵌字','校对完成']):
        p=blank(g);s=blank(g);text(p,(211,134),'我来帮你。' if i==3 else '手伝うよ。',18)
        translations=['','','']
        if i>0:translations[:2]=['今天就在这里。','我来帮你。']
        if i>1:translations[2]='明天见。'
        for j,st in enumerate(translations):text(s,(426,157+j*58),st,16,'#174475' if j==1 else '#333333')
        active=min(i,3)
        for j,st in enumerate(['图层 1  原稿','图层 2  清字','图层 3  台词','图层 4  校对']):
            color='#000080' if j==active else '#d5d7db';rect(s,(421,351+j*30,770,377+j*30),color)
            text(s,(431,357+j*30),st,13,'#ffffff' if j==active else '#202020')
        text(s,(415,313),['page01 / 原文 3 句','已译 2 / 3','已译 3 / 3  · 嵌字中','已校对 3 / 3'][i],14,'#555555')
        text(s,(12,521),['page01.tif  100%','page01.txt  已修改','文字图层已保存','page01-v2.png  已导出'][i],11,'#555555')
        ss.append((label,p,s,{'page':'page01','translated':[0,2,3,3][i],'proofread':[0,0,0,3][i],'layer':i+1}))
    g.save(ss)


def subtitles():
    g=Group('subtitles','字幕 · 译文与时间轴在同一处互相校正',(1200,580),[61.44,71.68],
      source_ids=['aegisub-translation-assistant-doc','workflow-subtitles-2'],
      note='Real 383×546 Aegisub 3.2 Translation Assistant stays unscaled at (0,17). The adjacent 800×540 timeline is an existing original workflow plate, not a second real capture.',
      native_controls=[{'id':'Aegisub.Cancel','rect_px':[167,509,84,24]},{'id':'Aegisub.Help','rect_px':[263,509,84,24]},
       {'id':'Aegisub.PlayVideo','rect_px':[257,378,84,24]},{'id':'Aegisub.keyboard_shortcuts','rect_px':[24,326,215,170]}])
    g.base=Image.new('RGBA',g.size,'#33383e')
    g.paste('art/reference/episode123-v2/aegisub_translation-00.png',(0,17))
    g.paste('art/export/episode123-v4-workflows/workflow-subtitles-2.png',(400,17))
    # Remove the old authored cursor only, preserving the source waveform on either side.
    g.base.paste(g.base.crop((650,92,652,295)),(652,92))
    rect(g.base,(31,109,347,181),'#ffffff');rect(g.base,(31,216,347,309),'#ffffff')
    rect(g.base,(28,83,178,99),'#f0f0f0');text(g.fixed,(29,87),'Current line: 02/07',12,'#000000')
    lines(g.fixed,(33,113),['Leave a light','for the next one.'],14,21,'#000080')
    rect(g.fixed,(417,312,1182,336),'#f4f4f4',outline='#afb5c0')
    text(g.fixed,(425,317),'01    0:00:00.000    0:00:02.200    Default    今天也一起做一点。',13)
    # The companion waveform uses its existing real pixel geometry, not generic new bars.
    g.region('translation',[31,216,317,93],'Aegisub translation field only; native controls fixed')
    g.region('timing',[415,87,765,447],'Existing authored waveform, time fields, subtitle row and cursor')
    vals=['为下一个人留下灯光。','给后来的人，留一盏灯。','给后来的人，留一盏灯。','给后来的人，留一盏灯。']
    g.slot('Aegisub.translation',[34,220,306,75],15,'#000000',vals)
    g.slot('timeline.start',[919,93,260,24],15,'#202020',['0:00:02.120','0:00:02.120','0:00:02.400','0:00:02.400'])
    g.slot('timeline.end',[919,130,260,24],15,'#202020',['0:00:04.600','0:00:04.600','0:00:04.200','0:00:04.200'])
    ss=[]
    for i,label in enumerate(['初译进入','改成自然语气','入点与出点对齐','预览与导出']):
        p=blank(g);s=blank(g);text(p,(34,220),vals[i],15)
        if i==1:rect(p,(32,216,344,242),'#cfe1f5');text(p,(34,220),vals[i],15)
        rect(s,(903,91,1179,204),'#ffffff')
        start='0:00:02.120' if i<2 else '0:00:02.400';end='0:00:04.600' if i<2 else '0:00:04.200'
        lines(s,(919,93),['起始：'+start,'结束：'+end,['初译','人工校对','已对齐语音','字幕已保存'][i]],15,36)
        # Deterministic static selected range and playhead over the original waveform.
        x0=[445,475,505,535][i];x1=[745,745,711,711][i]
        rect(s,(x0,99,x1,266),(226,193,97,48))
        ImageDraw.Draw(s).line((x0,88,x0,295),fill='#d6b940',width=2)
        rect(s,(437,245,860,271),'#151b27')
        text(s,(439,246),start+'     →     '+end,12,'#ffffff',True)
        rect(s,(417,335,1180,360),'#d5dfef')
        text(s,(425,340),'02    '+start+'    '+end+'    Default    '+vals[i],13,'#202020')
        rect(s,(405,536,1193,551),'#c0c0c0');text(s,(412,539),['draft.ass  1 行待校对','draft.ass  修改语气','draft.ass  时间轴已修改','subtitles-v2.ass  已导出'][i],11,'#555555')
        ss.append((label,p,s,{'file':'subtitles-v2.ass','line':2,'line_count':7,'start':start,'end':end,'translation':vals[i]}))
    g.save(ss)


def torrent():
    g=Group('torrent','BT · 区块汇入同一文件，再发给下一人',(1024,708),[40.96,51.2],source_ids=['bitcomet-053-main'],
      source_files=['art/export/episode123-v4-workflows/workflow-torrent-3.png','art/export/episode123-v4-workflows/workflow-release-3.png'],
      note='BitComet screenshot title visibly says 0.53; gallery metadata mismatch is retained. New filename/piece counts/progress are authored visual states, not a real transfer.',
      native_controls=[{'id':'toolbar','rect_px':[0,47,716,45]},{'id':'tabs','rect_px':[149,385,107,130]},{'id':'column_headers','rect_px':[147,92,875,18]}])
    g.paste('art/reference/episode123-v2-software/bitcomet-053-main.png')
    rect(g.base,(146,111,1022,360),'#ffffff');rect(g.base,(262,405,1006,687),'#ffffff')
    # Old task/site content is replaced, but the real toolbar, tree, column/summary dividers remain fixed.
    rect(g.base,(32,225,141,668),'#ffffff')
    lines(g.fixed,(39,230),['我的种子','  shortfilm','  subtitles','  readme','本地文件'],12,22,'#202020')
    text(g.fixed,(173,116),'我们的短片.mkv',13)
    lines(g.fixed,(268,409),['Torrent File:','SaveLocation:','Piece Size:','Total Files Size:','Selected Files Size:'],12,17)
    lines(g.fixed,(419,409),['shortfilm-v2.torrent',r'D:\Shared\Shortfilm','8 MB','128 MB','128 MB'],12,17)
    text(g.fixed,(268,507),'文件区块 / 16 pieces',14,'#202020')
    text(g.fixed,(268,600),'发布附带文件',13,'#202020')
    lines(g.fixed,(286,624),['readme.txt       翻译 / 校对 / 时间轴','checksum.txt     SHA-256'],12,21)
    g.region('selected_task',[148,112,874,45],'Task progress, state, size and transfer rates')
    g.region('pieces_and_publish',[266,530,740,148],'Received pieces, integrity check and publish/readme state')
    g.slot('BT.percent',[580,115,37,17],12,'#202020',['0.0%','37.5%','100%','100%'])
    g.slot('BT.filename',[173,116,290,18],13,'#202020',['我们的短片.mkv']*4,role='fixed_material_filename')
    g.slot('BT.titlebar',[22,4,914,18],12,'#ffffff',
      ['BitComet 0.53 - Download: '+str(dn)+' KB/s, Upload: '+str(up)+' KB/s' for dn,up in [(0,0),(192,24),(0,0),(0,96)]])
    g.slot('BT.state',[419,494,550,17],12,'#202020',['Waiting','Downloading','Hash checking','Seeding'])
    g.slot('BT.download_kbps',[625,115,49,17],12,'#202020',['0 KB/s','192 KB/s','0 KB/s','0 KB/s'])
    g.slot('BT.upload_kbps',[692,115,44,17],12,'#202020',['0 KB/s','24 KB/s','0 KB/s','96 KB/s'])
    g.slot('BT.progress',[268,574,720,18],13,'#202020',['0 / 16','6 / 16','16 / 16  Hash OK','16 / 16  向下一位上传'])
    ss=[]
    for i,label in enumerate(['添加原创短片','收到六个区块','完整校验','继续做种与发布']):
        p=blank(g);s=blank(g)
        title_bg=g.base.crop((718,1,950,24)).resize((929,23),Image.Resampling.NEAREST)
        p.paste(title_bg,(21,1))
        dn=[0,192,0,0][i];up=[0,24,0,96][i]
        text(p,(22,4),f'BitComet 0.53 - Download: {dn} KB/s, Upload: {up} KB/s',12,'#ffffff',True)
        rect(p,(148,112,1022,132),'#e9e9d8' if i else '#ffffff')
        ImageDraw.Draw(p).ellipse((153,117,164,128),fill='#4e9a54' if i==3 else '#92b989' if i else '#999999')
        text(p,(173,116),'我们的短片.mkv',13);text(p,(496,116),'128.00 MB',12)
        for x,st in [(580,['0.0%','37.5%','100%','100%'][i]),
                     (625,['0 KB/s','192 KB/s','0 KB/s','0 KB/s'][i]),(692,['0 KB/s','24 KB/s','0 KB/s','96 KB/s'][i]),
                     (752,['--:--','00:06','00:00','00:00'][i])]:text(p,(x,116),st,12)
        text(s,(268,494),'Task State:',12)
        text(s,(419,494),['Waiting','Downloading','Hash checking','Seeding'][i],12)
        n=[0,6,16,16][i]
        for j in range(16):
            x=270+(j%8)*89;y=534+(j//8)*18
            rect(s,(x,y,x+83,y+12),'#46a56f' if i==3 else '#518fca' if j<n else '#e1e5e9',outline='#b4bcc3')
        text(s,(268,575),['0 / 16  · 等待区块','6 / 16  · 正在合并','16 / 16  · Hash OK','16 / 16  · 向下一位上传'][i],13,'#25603c' if i>1 else '#202020')
        if i==2:rect(s,(286,643,922,662),(224,240,227,72))
        text(s,(268,668),['种子已添加','下载与上传同时进行','文件完整性校验通过','发布 v2  · 保留 readme.txt'][i],12,'#45664e')
        ss.append((label,p,s,{'filename':'我们的短片.mkv','size_bytes':134217728,'pieces_total':16,'pieces_received':n,'percent':[0,37.5,100,100][i],
                             'download_kbps':[0,192,0,0][i],'upload_kbps':[0,24,0,96][i],'publish_version':'v2'}))
    g.save(ss)


def forum():
    g=Group('forum','中文论坛 · 同一主题里收到回复并补上结果',(1440,1100),[51.2,61.44],source_ids=['tianya-bbs-2009'],
      source_files=['art/export/episode123-v4-workflows/workflow-bbs-3.png'],
      note='Real Tianya 2009 homepage is an unchanged reference. The central thread is a clearly documented original anonymous forum interaction, not an archived Tianya post or historical claim.',
      native_controls=[{'id':'global_header','rect_px':[366,0,830,32]},{'id':'search','rect_px':[603,59,368,27]},
       {'id':'left_navigation','rect_px':[233,0,129,1070]},{'id':'bottom_bar','rect_px':[139,1069,1148,31]}])
    g.paste('art/reference/episode123-v2/tianya-2009-archive-screenshot.png')
    rect(g.base,(366,151,1181,1067),'#ffffff')
    rect(g.fixed,(366,154,1180,182),'#d5e8f7');text(g.fixed,(378,160),'创作交流  >  制作问题',16,'#1a6999')
    text(g.fixed,(382,203),'以前保存的素材，还能继续改吗？',24,'#164b7a')
    text(g.fixed,(383,247),'xxxx  /  楼主',14,'#7b7b7b')
    lines(g.fixed,(383,289),['我保存了源文件和说明。','这次试着重新打开，发现有一张贴图找不到了。','先把目录和遇到的问题贴在这里。'],18,36)
    rect(g.fixed,(383,413,1162,414),'#c2d7e7')
    text(g.fixed,(383,435),'附件：folder-list.txt   readme.txt',15,'#206bab')
    g.region('reply',[381,490,782,316],'An anonymous reply appears beneath a persistent opening post')
    g.region('composer_and_index',[381,828,782,219],'Draft becomes result, counters and topic index update')
    vals=['','先检查文件名和路径。','先检查文件名和路径。\n我这里保留了同一份目录。','先检查文件名和路径。\n我这里保留了同一份目录。']
    g.slot('forum.reply',[404,548,730,100],18,'#202020',vals)
    g.slot('forum.subject',[382,203,780,32],24,'#164b7a',['以前保存的素材，还能继续改吗？']*4,role='fixed_authored_thread_title')
    g.slot('forum.reply_count',[383,834,779,24],15,'#2d6fa4',[f'回复 {i}  |  收藏  |  返回目录' for i in range(4)])
    g.slot('forum.result',[401,880,730,75],18,'#202020',['','我再检查一下。','找到了，原来放在旧目录里。','已经补上路径，也把方法写进说明。'])
    ss=[]
    for i,label in enumerate(['问题停留','有人回应','贴出检查结果','把方法留给后来的人']):
        p=blank(g);s=blank(g)
        if i:
            rect(p,(383,492,1160,681),'#f2f7fc',outline='#c5d9e9')
            text(p,(401,510),'xxxx  /  回复',14,'#346e9b')
            lines(p,(404,548),vals[i].split('\n'),18,36)
            text(p,(1040,650),'引用  回复',14,'#2c70a4')
        if i>1:
            rect(p,(383,702,1160,798),'#ffffff',outline='#c5d9e9')
            text(p,(401,716),'xxxx  /  楼主',14,'#346e9b')
            text(p,(404,754),'找到了，谢谢。我把结果补在下面。',18)
        text(s,(383,834),f'回复 {max(i,0)}  |  收藏  |  返回目录',15,'#2d6fa4')
        rect(s,(384,865,1161,978),'#ffffff',outline='#a7bccd')
        result=['','我再检查一下。','找到了，原来放在旧目录里。','已经补上路径，也把方法写进说明。'][i]
        if result:text(s,(401,884),result,18)
        button(s,[1022,995,139,34],'发表回复' if i<3 else '已发表',15,active=i==3)
        text(s,(383,1004),'readme.txt  更新记录' if i==3 else '保存草稿',14,'#2d6fa4')
        ss.append((label,p,s,{'title':'以前保存的素材，还能继续改吗？','author':'xxxx','reply_count':i,'attachment':'readme.txt','draft':result}))
    g.save(ss)


def access():
    g=Group('access','访问门槛 · 同一个入口的条件发生改变',(960,640),[102.4,122.88],
      source_files=['art/export/episode123/access_conditions.png'],
      note='Existing original generic Internet Explorer-style access page, service.example. It does not depict a named service, real incident or actual restriction notice.',
      native_controls=[{'id':'login_conditions','rect_px':[718,258,170,43]},{'id':'compatibility','rect_px':[718,354,170,43]},
       {'id':'usage_scope','rect_px':[718,450,170,43]},{'id':'address','rect_px':[100,105,825,23]}])
    g.paste('art/export/episode123/access_conditions.png')
    rect(g.base,(11,615,949,637),'#c0c0c0');text(g.fixed,(15,620),'Internet',12,'#535353')
    # Preserve the original row titles and button edges, changing only their interior text and native status fields.
    g.region('condition_rows',[60,262,829,236],'Original row labels, secondary details and button captions')
    g.region('result',[40,530,877,70],'The same entry supplies the next step or admits the user')
    g.slot('access.login_button',[727,268,151,24],20,'#202020',['查看条件','需要登录','已登录','已登录'])
    g.slot('access.compatibility_button',[727,364,151,24],20,'#202020',['查看条件','检查版本','需要更新','已兼容'])
    g.slot('access.status',[48,547,849,47],21,'#375778',['可以先查看使用条件。','要继续，请先登录。','已经登录，还需要更新版本。','条件已满足，可以继续。'])
    ss=[]
    for i,label in enumerate(['入口仍可见','登录成为前置条件','版本又成为条件','处理之后重新进入']):
        p=blank(g);s=blank(g)
        captions=[['查看条件','查看条件','查看条件'],['需要登录','检查版本','查看范围'],['已登录','需要更新','查看范围'],['已登录','已兼容','已确认']][i]
        details=[['','',''],['会话尚未建立','检测当前版本','请保留原作者署名'],['会话已建立','当前版本不兼容','请保留原作者署名'],['会话已建立','版本检查完成','保留说明与署名']][i]
        for j in range(3):
            # Software row is unchanged; captions occupy exactly the pre-existing button interior.
            button(p,[718,258+j*96,170,43],captions[j],20,active=i==1 and j==0)
            if details[j]:
                rect(p,(340,270+j*96,694,296+j*96),'#edf2f5')
                text(p,(350,275+j*96),details[j],17,'#596b79')
        msg=['可以先查看使用条件。','要继续，请先登录。','已经登录，还需要更新版本。','条件已满足，可以继续。'][i]
        rect(s,(40,532,915,593),'#f5f7f9');text(s,(48,549),msg,21,'#375778')
        if i==3:button(s,[727,543,160,38],'继续',18)
        text(s,(785,620),['就绪','等待登录','等待更新','完成'][i],12,'#535353')
        ss.append((label,p,s,{'url':'http://service.example/','logged_in':i>=2,'compatible':i==3,'usage_confirmed':i==3,'next_step':['inspect','login','update','continue'][i]}))
    g.save(ss)


readme();manga();subtitles();torrent();forum();access()
# Index is a review-only page with provenance text, never an in-world plate.
parts=['<!doctype html><meta charset="utf-8"><title>FullFilm V2 Native UI QA</title><style>body{background:#20252b;color:#e6edf0;font:16px sans-serif;margin:28px}img{width:100%;max-width:1520px}article{margin:35px 0}code{color:#a7cec9}</style><h1>静态 UI 分层素材 · QA</h1><p>纯演出版：各目录 state-00…03.png。此页与 qa-preview.png 含来源/复原说明，仅用于审核。</p>']
for g in MANIFEST['groups']:
    parts.append(f'<article><h2>{g["title"]}</h2><p>{g["provenance_note"]}</p><p><code>{g["id"]} / {g["width"]}×{g["height"]}</code></p><img src="{g["qa_preview"]}"></article>')
parts.append('<p>所有新增状态均为原创动态复原；历史来源与完整状态字段见 data/fullfilm-v2-native-ui.json。没有Unity/Blender伪造实截。</p>')
(OUT/'index.html').write_text('\n'.join(parts),encoding='utf-8')
files=[]
for p in sorted(OUT.rglob('*.png')):
    files.append({'file':str(p.relative_to(OUT)),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
MANIFEST['files']=files
(ROOT/'data/fullfilm-v2-native-ui.json').write_text(json.dumps(MANIFEST,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'groups':len(MANIFEST['groups']),'png_files':len(files),'states':sum(len(g['states']) for g in MANIFEST['groups'])}))
